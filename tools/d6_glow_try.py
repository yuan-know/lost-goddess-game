"""快速试不同辉光径向剖面：不改素材，只出「窗口放大」对比条。"""
import math
import os

import numpy as np
from PIL import Image, ImageFilter

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\d6"
GLOW_RGB = (255, 238, 206)
BLOOM = 0.55
SW, SH, CW, CH = 1920, 1080, 1920, 678
GEAR_CY = (SH / 2.0 - CH / 2.0) + (-180.0)

base = Image.open(os.path.join(D6, "d6_gear_wheel.png")).convert("RGBA")
a = np.array(base).astype(float)
H, W = a.shape[:2]
ga = a[:, :, 3] / 255.0
solid = ga >= 0.985
yy, xx = np.mgrid[0:H, 0:W]

WINS = [(250.74, 146.72, 40.23), (344.26, 212.13, 39.67), (314.43, 328.76, 39.68),
        (181.98, 331.20, 39.62), (158.45, 212.77, 42.36)]


def ss(e0, e1, x):
    t = np.clip((x - e0) / max(1e-6, e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def glow_alpha(core_a, core_lo, core_hi, rim_a, rim_w, amb=0.0):
    acc = np.zeros((H, W))
    for (cx, cy, rw) in WINS:
        d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        g = core_a * (1 - ss(core_lo * rw, core_hi * rw, d))
        g += rim_a * np.exp(-((d - rw) / rim_w) ** 2)
        g += amb * (1 - ss(0.4 * rw, 1.6 * rw, d))
        acc = np.maximum(acc, np.clip(g, 0, 1))
    return np.array(Image.fromarray((acc * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(1.1))) / 255.0


def bake(acc):
    out = a.copy()
    gl = acc * (1 - ga)
    for c in range(3):
        out[:, :, c] = np.where(solid,
                                np.clip(a[:, :, c] + GLOW_RGB[c] * acc * BLOOM, 0, 255),
                                a[:, :, c] * ga + GLOW_RGB[c] * gl)
    out[:, :, 3] = np.clip((ga + gl) * 255, 0, 255)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


VARIANTS = [
    ("v1 满窗柔光", dict(core_a=0.62, core_lo=0.35, core_hi=1.65, rim_a=0.20, rim_w=7.0)),
    ("v4 辉光裹图标·窗缘透明", dict(core_a=0.85, core_lo=0.42, core_hi=0.98, rim_a=0.08, rim_w=6.0, amb=0.10)),
    ("v5 同v4但更亮更宽", dict(core_a=0.95, core_lo=0.40, core_hi=1.15, rim_a=0.14, rim_w=7.0, amb=0.16)),
    ("v6 满窗柔光+边缘透明", dict(core_a=0.70, core_lo=0.30, core_hi=0.80, rim_a=0.10, rim_w=6.0, amb=0.22)),
]


def load(n):
    return Image.open(os.path.join(D6, n)).convert("RGBA")


def fit(img, w, h):
    return img.resize((int(round(w)), int(round(h))), Image.LANCZOS)


def paste(canvas, img, cx, cy):
    w, h = img.size
    canvas.alpha_composite(img, (int(round(cx - w / 2)), int(round((SH - cy) - h / 2))))


def scene(gear_img):
    c = Image.new("RGBA", (SW, SH), (13, 11, 10, 255))
    c.alpha_composite(Image.new("RGBA", (SW, SH), (0, 0, 0, int(0.7 * 255))))
    paste(c, fit(load("d6_bg_aperture.png"), CW, CH), SW / 2, SH / 2)
    paste(c, fit(load("d6_prop_dial.png"), CW, CH), SW / 2, SH / 2)
    paste(c, fit(gear_img, 520, 520), SW / 2, GEAR_CY)
    paste(c, fit(load("d6_knob.png"), 160, 160), SW - 100, (SH / 2 - CH / 2) + 100)
    return c


box = (800, 1080 - 190, 1120, 1080 - 10)
tiles = []
for name, kw in [("改前", None)] + VARIANTS:
    g = base if kw is None else bake(glow_alpha(**kw))
    t = scene(g).convert("RGB").crop(box)
    tiles.append(t.resize((t.size[0] * 3, t.size[1] * 3), Image.LANCZOS))
    if kw is not None:
        g.save(os.path.join(OUT, f"泛光方案_{name.split()[0]}.png"))
        scene(g).convert("RGB").save(os.path.join(OUT, f"泛光预览_{name.split()[0]}.png"), quality=92)

Wt = sum(t.size[0] for t in tiles) + 14 * (len(tiles) - 1)
strip = Image.new("RGB", (Wt, tiles[0].size[1]), (0, 0, 0))
x = 0
for t in tiles:
    strip.paste(t, (x, 0))
    x += t.size[0] + 14
strip.save(os.path.join(OUT, "泛光对比_第二轮.png"), quality=93)
print("wrote 泛光对比_第二轮.png", strip.size)
print("顺序: 改前 |", " | ".join(n for n, _ in VARIANTS))
