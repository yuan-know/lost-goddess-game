# -*- coding: utf-8 -*-
"""从 背包空白格子.psd 合成图里量出 4x4 格子的中心/尺寸/间距。

思路:羊皮纸格心是大面积浅色低饱和区域,枝桠与皮革都是深棕。
      阈值 -> 形态学 -> 连通块 -> 按面积/形状筛选 -> 聚类成行列。
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

sys.stdout.reconfigure(encoding='utf-8')

RAW = r"C:\Users\yuan\lost-goddess-game\tools\inventory\raw\psd_composite.png"
OUT_DIR = r"C:\Users\yuan\lost-goddess-game\tools\inventory"

im = Image.open(RAW).convert("RGBA")
a = np.asarray(im).astype(np.float32)
rgb = a[..., :3]
alpha = a[..., 3]
H, W = alpha.shape
print("canvas", W, "x", H)

mx = rgb.max(axis=2)
mn = rgb.min(axis=2)
sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)

# 羊皮纸:亮 + 低饱和;同时对 alpha>0 的实体区域
mask = (alpha > 200) & (mx > 165) & (sat < 0.34)
print("raw mask px:", int(mask.sum()))

mask = ndimage.binary_opening(mask, structure=np.ones((5, 5)))
mask = ndimage.binary_closing(mask, structure=np.ones((9, 9)))

lab, n = ndimage.label(mask)
print("components:", n)
objs = ndimage.find_objects(lab)
cands = []
for i, sl in enumerate(objs, start=1):
    ys, xs = sl
    h = ys.stop - ys.start
    w = xs.stop - xs.start
    area = int((lab[sl] == i).sum())
    if area < 3000:
        continue
    fill = area / float(h * w)
    cands.append((i, xs.start, ys.start, w, h, area, fill))

cands.sort(key=lambda c: -c[5])
print(f"{'id':>4} {'x0':>6} {'y0':>6} {'w':>5} {'h':>5} {'area':>8} {'fill':>6}")
for c in cands:
    print(f"{c[0]:>4} {c[1]:>6} {c[2]:>6} {c[3]:>5} {c[4]:>5} {c[5]:>8} {c[6]:>6.2f}")

# 只保留像格子的:宽高都在 100~320,fill > 0.55
cells = [c for c in cands if 100 <= c[3] <= 320 and 100 <= c[4] <= 320 and c[6] > 0.55]
print("\ngrid-like cells:", len(cells))
for c in sorted(cells, key=lambda c: (c[2], c[1])):
    cx = c[1] + c[3] / 2.0
    cy = c[2] + c[4] / 2.0
    print(f"  x0={c[1]:>5} y0={c[2]:>5} w={c[3]:>4} h={c[4]:>4}  center=({cx:8.1f},{cy:8.1f})")

# 按中心坐标聚类成 4 行 4 列
if cells:
    cxs = sorted(c[1] + c[3] / 2.0 for c in cells)
    cys = sorted(c[2] + c[4] / 2.0 for c in cells)
    print("\nall center X:", [round(v, 1) for v in cxs])
    print("all center Y:", [round(v, 1) for v in cys])
