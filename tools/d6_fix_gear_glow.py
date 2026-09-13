"""把 d6_gear_wheel.png 的 5 个圆窗做成「透明 + 泛光」效果。

设计要点（对应上一版的两个槽点）：
  1. 不要白板：窗口内部保持半透明（能看到后面的黑），只在窗口里铺一层柔和的暖光，
     深色图标靠"逆光剪影"读出来。
  2. 不要缺口：辉光用连续的径向衰减，边界是羽化的，几何中心差几个像素也不会露缺口。
     另外用「透明环 ∪ 图标」的并集质心迭代求窗心，比五点拟合准。
  3. 泛光要溅到周围铜面上：不透明金属区域做**加光**处理，
     这样窗口像在发光，铜面被照亮 —— 而不是一块贴在齿轮上的白片。

输出三种强度供挑选：v1 半透明泛光窗 / v2 纯边缘辉光 / v3 弱泛光+强金属回光。
原图备份保留（不再删除）。
"""
import math
import os
import shutil
import time

import numpy as np
from PIL import Image, ImageFilter

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
REPO = r"C:\Users\yuan\lost-goddess-game"
OUT = os.path.join(REPO, "docs", "d6")
NAME = "d6_gear_wheel.png"

GLOW_RGB = (255, 238, 206)      # 暖白金
BLOOM_ON_METAL = 0.55           # 溅到铜面上的加光系数

src = os.path.join(D6, NAME)
im = Image.open(src).convert("RGBA")
a = np.array(im).astype(float)
H, W = a.shape[:2]
ga = a[:, :, 3] / 255.0
solid = ga >= 0.985
transparent = ga <= 0.031
yy, xx = np.mgrid[0:H, 0:W]
print(f"不透明 {int(solid.sum())}  透明 {int(transparent.sum())}")

# ── 1. 求 5 个窗心：五点拟合做初值 → 「透明环 ∪ 图标」并集质心迭代 ──
P3 = [(250.5, 147.5), (156.0, 212.0), (183.0, 331.5)]
(x1, y1), (x2, y2), (x3, y3) = P3
d = 2 * (x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2))
gx = ((x1 ** 2 + y1 ** 2) * (y2 - y3) + (x2 ** 2 + y2 ** 2) * (y3 - y1)
      + (x3 ** 2 + y3 ** 2) * (y1 - y2)) / d
gy = ((x1 ** 2 + y1 ** 2) * (x3 - x2) + (x2 ** 2 + y2 ** 2) * (x1 - x3)
      + (x3 ** 2 + y3 ** 2) * (x2 - x1)) / d
grad = math.hypot(x1 - gx, y1 - gy)
cands = [(gx + grad * math.cos(math.radians(-90 + 72 * k)),
          gy + grad * math.sin(math.radians(-90 + 72 * k))) for k in range(5)]

wins = []       # (cx, cy, r_win)
for (cx, cy) in cands:
    for _ in range(8):
        dc = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        ring = transparent & (dc <= 58)          # 窗内的透明环
        icon = solid & (dc <= 34)                # 窗内的图标
        m = ring | icon
        if not m.any():
            break
        ncx, ncy = xx[m].mean(), yy[m].mean()
        if abs(ncx - cx) < 0.05 and abs(ncy - cy) < 0.05:
            cx, cy = ncx, ncy
            break
        cx, cy = ncx, ncy
    dc = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    rw = float(np.max(dc[transparent & (dc <= 58)]))     # 环的最外沿 = 窗半径
    wins.append((cx, cy, rw))
    print(f"  窗心 ({cx:6.2f},{cy:6.2f})  窗半径 {rw:5.2f}")

# ── 2. 辉光层 ──
def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / max(1e-6, (e1 - e0)), 0, 1)
    return t * t * (3 - 2 * t)


def build(variant):
    """variant: 'v1' 半透明泛光窗 / 'v2' 纯边缘辉光 / 'v3' 弱泛光+强回光"""
    acc_a = np.zeros((H, W))
    for (cx, cy, rw) in wins:
        dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        if variant == "v1":
            core = 0.62 * (1 - smoothstep(0.35 * rw, 1.65 * rw, dist))
            rim = 0.20 * np.exp(-((dist - rw) / 7.0) ** 2)
        elif variant == "v2":
            core = 0.10 * (1 - smoothstep(0.30 * rw, 1.55 * rw, dist))
            rim = 0.80 * np.exp(-((dist - rw) / 6.5) ** 2)
        else:                                    # v3
            core = 0.34 * (1 - smoothstep(0.30 * rw, 1.60 * rw, dist))
            rim = 0.26 * np.exp(-((dist - rw) / 9.0) ** 2)
        acc_a = np.maximum(acc_a, np.clip(core + rim, 0, 1))
    acc_a = np.array(Image.fromarray((acc_a * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(1.2))) / 255.0   # 整体再柔化一点,彻底消掉硬边
    return acc_a


def bake(acc_a):
    out = a.copy()
    gl_y = acc_a * (1 - ga)                      # 透明/半透明区域:alpha 合成
    for c in range(3):
        base = a[:, :, c] * ga + GLOW_RGB[c] * gl_y
        lit = np.clip(a[:, :, c] + GLOW_RGB[c] * acc_a * BLOOM_ON_METAL, 0, 255)
        out[:, :, c] = np.where(solid, lit, base)
    out[:, :, 3] = np.clip((ga + gl_y) * 255.0, 0, 255)
    return np.clip(out, 0, 255).astype(np.uint8)


# ── 3. 备份(保留!) + 落盘 ──
stamp = time.strftime("%Y-%m-%d_%H%M")
bak = os.path.join(REPO, "docs", "backups", f"{stamp}_齿轮泛光前_原始素材")
os.makedirs(bak, exist_ok=True)
shutil.copy2(src, os.path.join(bak, NAME))
print("原图备份(保留):", bak)

os.makedirs(OUT, exist_ok=True)
imgs = {}
for v in ("v1", "v2", "v3"):
    imgs[v] = bake(build(v))
    Image.fromarray(imgs[v]).save(os.path.join(OUT, f"泛光方案_{v}.png"))
    print(f"wrote 泛光方案_{v}.png")

# 默认落盘 v1
Image.fromarray(imgs["v1"]).save(src)
print("默认已落盘 v1 到素材")

# ── 4. 放进场景里看（按运行时同样的坐标规则合成 1920×1080）──
SW, SH, CW, CH = 1920, 1080, 1920, 678
CONT_CY = SH / 2.0
GEAR_CY = (CONT_CY - CH / 2.0) + (-180.0)


def load(n):
    return Image.open(os.path.join(D6, n)).convert("RGBA")


def fit(img, w, h):
    return img.resize((int(round(w)), int(round(h))), Image.LANCZOS)


def paste_center(canvas, img, cx, cy, rot=0.0):
    if rot:
        img = img.rotate(rot, resample=Image.BICUBIC, expand=True)
    w, h = img.size
    canvas.alpha_composite(img, (int(round(cx - w / 2)), int(round((SH - cy) - h / 2))))


def scene(gear_img, rot=0.0):
    c = Image.new("RGBA", (SW, SH), (13, 11, 10, 255))
    c.alpha_composite(Image.new("RGBA", (SW, SH), (0, 0, 0, int(0.7 * 255))))
    paste_center(c, fit(load("d6_bg_aperture.png"), CW, CH), SW / 2, CONT_CY)
    paste_center(c, fit(load("d6_prop_dial.png"), CW, CH), SW / 2, CONT_CY)
    paste_center(c, fit(gear_img, 520, 520), SW / 2, GEAR_CY, rot)
    paste_center(c, fit(load("d6_knob.png"), 160, 160), SW - 100, (CONT_CY - CH / 2.0) + 100)
    return c


tiles = []
for label, gimg in (("改前(原图)", im) ,
                    ("v1 半透明泛光", Image.fromarray(imgs["v1"])),
                    ("v2 纯边缘辉光", Image.fromarray(imgs["v2"])),
                    ("v3 弱泛光+强回光", Image.fromarray(imgs["v3"]))):
    cc = scene(gimg)
    cc.convert("RGB").save(os.path.join(OUT, f"泛光预览_{label.split()[0]}.png"), quality=92)
    tiles.append(cc.convert("RGB").crop((690, 780, 1230, 1080)).resize((540 * 2, 300 * 2), Image.LANCZOS))

Wt = sum(t.size[0] for t in tiles) + 20 * (len(tiles) - 1)
strip = Image.new("RGB", (Wt, tiles[0].size[1]), (0, 0, 0))
x = 0
for t in tiles:
    strip.paste(t, (x, 0))
    x += t.size[0] + 20
strip.save(os.path.join(OUT, "泛光对比_齿轮局部_改前_v1_v2_v3.png"), quality=92)
print("wrote 泛光预览_*.png / 泛光对比_齿轮局部_改前_v1_v2_v3.png")
