# -*- coding: utf-8 -*-
"""量格子几何 v3:16 个羊皮纸格心 = 16 个独立连通块(枝桠把它们完全隔开)。

输出:每格中心 / 内框尺寸 + 行列拟合出的规律节距,并出对照图供肉眼校验。
"""
import sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

sys.stdout.reconfigure(encoding='utf-8')

RAW = r"C:\Users\yuan\lost-goddess-game\tools\inventory\raw\psd_composite.png"
OUT = r"C:\Users\yuan\lost-goddess-game\tools\inventory\grid_measure_overlay.png"

im = Image.open(RAW).convert("RGBA")
a = np.asarray(im).astype(np.float32)
rgb = a[..., :3]
alpha = a[..., 3]
H, W = alpha.shape

mx = rgb.max(axis=2)
mn = rgb.min(axis=2)
sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)

paper = (alpha > 200) & (mx > 130) & (sat < 0.42)
paper = ndimage.binary_opening(paper, np.ones((3, 3)))
paper = ndimage.binary_closing(paper, np.ones((5, 5)))

lab, n = ndimage.label(paper)
objs = ndimage.find_objects(lab)
cells = []
for i, sl in enumerate(objs, start=1):
    ys, xs = sl
    h, w = ys.stop - ys.start, xs.stop - xs.start
    area = int((lab[sl] == i).sum())
    if area < 5000:
        continue
    fill = area / float(h * w)
    if fill < 0.6:
        continue
    cells.append(dict(x0=xs.start, y0=ys.start, w=w, h=h, area=area, fill=fill,
                      cx=xs.start + w / 2.0, cy=ys.start + h / 2.0))

print("cell count:", len(cells))

# 按 y 分 4 行(容差 60),行内按 x 排序
cells.sort(key=lambda c: c['cy'])
rows = []
for c in cells:
    if rows and abs(c['cy'] - np.mean([r['cy'] for r in rows[-1]])) < 60:
        rows[-1].append(c)
    else:
        rows.append([c])

print("\n--- 每格 ---")
grid = []
for ri, row in enumerate(rows):
    row.sort(key=lambda c: c['cx'])
    line = []
    for ci, c in enumerate(row):
        line.append(c)
        print(f"  r{ri}c{ci}: center=({c['cx']:7.1f},{c['cy']:7.1f})  "
              f"inner={c['w']:3d}x{c['h']:3d}  fill={c['fill']:.2f}")
    grid.append(line)

if len(grid) == 4 and all(len(r) == 4 for r in grid):
    cxs = np.array([[c['cx'] for c in r] for r in grid])
    cys = np.array([[c['cy'] for c in r] for r in grid])
    colmean = cxs.mean(axis=0)
    rowmean = cys.mean(axis=1)
    print("\n列中心(4 列均值):", [round(v, 1) for v in colmean])
    print("行中心(4 行均值):", [round(v, 1) for v in rowmean])
    print("列节距:", [round(colmean[i + 1] - colmean[i], 2) for i in range(3)])
    print("行节距:", [round(rowmean[i + 1] - rowmean[i], 2) for i in range(3)])
    inner_w = float(np.mean([c['w'] for r in grid for c in r]))
    inner_h = float(np.mean([c['h'] for r in grid for c in r]))
    print("内框平均:", round(inner_w, 1), "x", round(inner_h, 1))
    print("外接网格:", round(colmean[-1] - colmean[0] + inner_w, 1), "x",
          round(rowmean[-1] - rowmean[0] + inner_h, 1))
else:
    print("!! 行列不是 4x4:", [len(r) for r in grid])

# 出对照图
ov = im.copy()
d = ImageDraw.Draw(ov)
for ri, row in enumerate(grid):
    for ci, c in enumerate(row):
        d.rectangle([c['x0'], c['y0'], c['x0'] + c['w'] - 1, c['y0'] + c['h'] - 1],
                    outline=(255, 0, 0, 255), width=3)
        d.line([c['cx'] - 14, c['cy'], c['cx'] + 14, c['cy']], fill=(0, 120, 255, 255), width=3)
        d.line([c['cx'], c['cy'] - 14, c['cx'], c['cy'] + 14], fill=(0, 120, 255, 255), width=3)
        d.text((c['cx'] - 10, c['cy'] - 8), f"{ri}{ci}", fill=(0, 255, 0, 255))
ov.convert("RGB").save(OUT)
print("\noverlay ->", OUT)
