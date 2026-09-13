"""v7：让 5 个圆窗的辉光铺满整个「孔」，且 5 个窗大小一致。

上一版(v6)的两个毛病（用户反馈）：
  1. 辉光在 0.80*rw 就衰减到 0，而孔半径是 rw → 光圈比孔小一圈；
  2. 各窗孔径/图标大小不同，图标大的窗辉光全被挡住 → 看起来"有的有光圈、
     有的特别小、有的根本没有"。

v7 做法：
  1. 用「非金属像素」反推每个孔的真实孔径与圆心（金属=不透明齿轮本体；
     孔 = 孔内的透明环 + 图标，两者都不是金属）。
  2. 5 个窗统一用同一个孔径（取量到的最大值），避免大小不一。
  3. 辉光铺满整孔：full 亮到 0.92*r，往外 1.18*r 才衰减到 0（略溢到孔缘的铜上），
     再加一点窗缘辉光，让孔的边界被光勾出来。
"""
import math
import os
import shutil
import sys
import time

import numpy as np
from PIL import Image, ImageFilter

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
REPO = r"C:\Users\yuan\lost-goddess-game"
OUT = os.path.join(REPO, "docs", "d6")
NAME = "d6_gear_wheel.png"
SRC0 = os.path.join(REPO, "docs", "backups", "2026-09-13_1359_齿轮泛光前_原始素材", NAME)

GLOW_RGB = (255, 238, 206)
CORE_A = 0.72          # 孔内辉光亮度（命令行 `--core 0.52` 可覆盖）
NO_SAVE = "--no-save" in sys.argv
for _i, _a in enumerate(sys.argv):
    if _a == "--core" and _i + 1 < len(sys.argv):
        CORE_A = float(sys.argv[_i + 1])
FILL_LO = 0.92         # 孔内保持全亮到 0.92*r
FILL_HI = 1.18         # 到 1.18*r 才衰减到 0（略溢到孔缘铜上）
RIM_A = 0.16           # 孔缘辉光
RIM_W = 6.0
AMB = 0.18             # 极淡的整体泛光
BLOOM = 0.55           # 溅到铜面上的回光
SW, SH, CW, CH = 1920, 1080, 1920, 678
GEAR_CY = (SH / 2.0 - CH / 2.0) + (-180.0)

src = os.path.join(D6, NAME)
a = np.array(Image.open(SRC0).convert("RGBA")).astype(float)
H, W = a.shape[:2]
ga = a[:, :, 3] / 255.0
metal = ga >= 0.985            # 齿轮本体（含图标？不——图标也是不透明的）
yy, xx = np.mgrid[0:H, 0:W]
print(f"金属像素 {int(metal.sum())}")

# ── 1. 初值：三点拟合圆 + 正五边形 ──
P3 = [(250.5, 147.5), (156.0, 212.0), (183.0, 331.5)]
(x1, y1), (x2, y2), (x3, y3) = P3
dd = 2 * (x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2))
gx = ((x1 ** 2 + y1 ** 2) * (y2 - y3) + (x2 ** 2 + y2 ** 2) * (y3 - y1)
      + (x3 ** 2 + y3 ** 2) * (y1 - y2)) / dd
gy = ((x1 ** 2 + y1 ** 2) * (x3 - x2) + (x2 ** 2 + y2 ** 2) * (x1 - x3)
      + (x3 ** 2 + y3 ** 2) * (x2 - x1)) / dd
grad = math.hypot(x1 - gx, y1 - gy)
cands = [(gx + grad * math.cos(math.radians(-90 + 72 * k)),
          gy + grad * math.sin(math.radians(-90 + 72 * k))) for k in range(5)]

# ── 2. 用「非金属像素」迭代求每个孔的圆心与孔径 ──
PROBE = 58
wins = []
for (cx, cy) in cands:
    for _ in range(10):
        d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        hole = (~metal) & (d <= PROBE)
        if not hole.any():
            break
        ncx, ncy = xx[hole].mean(), yy[hole].mean()
        if abs(ncx - cx) < 0.05 and abs(ncy - cy) < 0.05:
            cx, cy = ncx, ncy
            break
        cx, cy = ncx, ncy
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    hole = (~metal) & (d <= PROBE)
    r = float(np.max(d[hole]))
    # 孔内被图标占掉多少（图标 = 孔内的不透明像素）
    icon_r = float(np.max(d[hole & metal])) if (hole & metal).any() else 0.0
    wins.append((cx, cy, r, icon_r))
    print(f"  孔心 ({cx:6.2f},{cy:6.2f})  孔半径 {r:5.2f}   图标半径 {icon_r:5.2f}  "
          f"可见辉光环宽 {r - icon_r:5.2f}")

common_r = max(w[2] for w in wins)
print(f"统一孔径 = {common_r:.2f}  (各窗 {['%.1f' % w[2] for w in wins]})")

# ── 3. 辉光 ──
def ss(e0, e1, x):
    t = np.clip((x - e0) / max(1e-6, e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


acc = np.zeros((H, W))
for (cx, cy, r, _) in wins:
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    g = CORE_A * (1 - ss(FILL_LO * common_r, FILL_HI * common_r, d))
    g += RIM_A * np.exp(-((d - common_r) / RIM_W) ** 2)
    g += AMB * (1 - ss(0.5 * common_r, 1.7 * common_r, d))
    acc = np.maximum(acc, np.clip(g, 0, 1))
acc = np.array(Image.fromarray((acc * 255).astype(np.uint8)).filter(
    ImageFilter.GaussianBlur(1.0))) / 255.0

out = a.copy()
gl = acc * (1 - ga)
for c in range(3):
    out[:, :, c] = np.where(metal,
                            np.clip(a[:, :, c] + GLOW_RGB[c] * acc * BLOOM, 0, 255),
                            a[:, :, c] * ga + GLOW_RGB[c] * gl)
out[:, :, 3] = np.clip((ga + gl) * 255, 0, 255)
out = np.clip(out, 0, 255).astype(np.uint8)

# ── 4. 备份 + 落盘 ──
tag = f"core{CORE_A:.2f}"
if not NO_SAVE:
    stamp = time.strftime("%Y-%m-%d_%H%M")
    bak = os.path.join(REPO, "docs", "backups", f"{stamp}_齿轮泛光v7前_当前素材")
    os.makedirs(bak, exist_ok=True)
    if os.path.exists(src):
        shutil.copy2(src, os.path.join(bak, NAME))
    Image.fromarray(out).save(src)
    print("已烘焙 v7 →", src)
    print("改前素材备份(保留):", bak)
else:
    print("--no-save：只出预览，不改素材")

# ── 5. 场景核验 ──


def load(n):
    return Image.open(os.path.join(D6, n)).convert("RGBA")


def fit(img, w, h):
    return img.resize((int(round(w)), int(round(h))), Image.LANCZOS)


def paste(canvas, img, cx, cy, rot=0.0):
    if rot:
        img = img.rotate(rot, resample=Image.BICUBIC, expand=True)
    w, h = img.size
    canvas.alpha_composite(img, (int(round(cx - w / 2)), int(round((SH - cy) - h / 2))))


def scene(gear_img, rot=0.0):
    c = Image.new("RGBA", (SW, SH), (13, 11, 10, 255))
    c.alpha_composite(Image.new("RGBA", (SW, SH), (0, 0, 0, int(0.7 * 255))))
    paste(c, fit(load("d6_bg_aperture.png"), CW, CH), SW / 2, SH / 2)
    paste(c, fit(load("d6_prop_dial.png"), CW, CH), SW / 2, SH / 2)
    paste(c, fit(gear_img, 520, 520), SW / 2, GEAR_CY, rot)
    paste(c, fit(load("d6_knob.png"), 160, 160), SW - 100, (SH / 2 - CH / 2) + 100)
    return c


os.makedirs(OUT, exist_ok=True)
scene(Image.fromarray(out)).convert("RGB").save(
    os.path.join(OUT, f"核验_v7_{tag}_全屏.png"), quality=92)
crops = []
for rot, tg in ((0.0, "0度"), (-72.0, "转-72度"), (-144.0, "转-144度")):
    sc = scene(Image.fromarray(out), rot).convert("RGB")
    # 齿轮可见区域：中心 (960,21)，可见半径 ~281 → 取 y 图像行 1080-300 .. 1080
    sc.crop((660, 1080 - 300, 1260, 1080)).resize((600 * 2, 300 * 2), Image.LANCZOS) \
        .save(os.path.join(OUT, f"核验_v7_{tag}_齿轮局部_{tg}.png"), quality=92)
    crops.append(sc.crop((660, 1080 - 300, 1260, 1080)).resize((600, 300), Image.LANCZOS))
Wt = sum(t.size[0] for t in crops) + 16 * (len(crops) - 1)
strip = Image.new("RGB", (Wt, crops[0].size[1]), (0, 0, 0))
x = 0
for t in crops:
    strip.paste(t, (x, 0))
    x += t.size[0] + 16
strip.save(os.path.join(OUT, f"核验_v7_{tag}_三档位并排.png"), quality=92)
print(f"wrote 核验_v7_{tag}_*.png")
