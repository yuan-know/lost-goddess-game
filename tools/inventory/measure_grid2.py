# -*- coding: utf-8 -*-
"""量格子几何 v2:用「羊皮纸」掩码的投影轮廓找枝桠分隔线,从而得到格子中心与节距。

v1 用连通块,但底行的枝桠/翻盖把单格切碎,行距不稳。
这里改用投影:枝桠(深棕)在列/行投影上是窄而深的"谷",谷之间的中心即格子中心。
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

sys.stdout.reconfigure(encoding='utf-8')

RAW = r"C:\Users\yuan\lost-goddess-game\tools\inventory\raw\psd_composite.png"
im = Image.open(RAW).convert("RGBA")
a = np.asarray(im).astype(np.float32)
rgb = a[..., :3]
alpha = a[..., 3]
H, W = alpha.shape

mx = rgb.max(axis=2)
mn = rgb.min(axis=2)
sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)

# 宽松的羊皮纸掩码(浅色 + 低饱和),把阴影羊皮纸也包进来
paper = (alpha > 200) & (mx > 130) & (sat < 0.42)
paper = ndimage.binary_opening(paper, np.ones((3, 3)))

# 只取最大的那一块(网格整体)
lab, n = ndimage.label(paper)
sizes = ndimage.sum(paper, lab, range(1, n + 1))
big = int(np.argmax(sizes)) + 1
grid = lab == big
ys, xs = np.where(grid)
print("grid blob bbox: x", xs.min(), "-", xs.max(), " y", ys.min(), "-", ys.max(),
      " size", xs.max() - xs.min() + 1, "x", ys.max() - ys.min() + 1)

gx0, gx1, gy0, gy1 = xs.min(), xs.max(), ys.min(), ys.max()

# 在 blob 的 bbox 内做投影(用填充后的矩形区域,免得边缘行被裁)
region = grid[gy0:gy1 + 1, gx0:gx1 + 1]
colprof = region.sum(axis=0).astype(np.float32) / region.shape[0]
rowprof = region.sum(axis=1).astype(np.float32) / region.shape[1]


def find_valleys(prof, expect=4, min_gap=40):
    """在轮廓里找 expect+1 个分隔谷(枝桠),返回谷心下标。"""
    sm = ndimage.uniform_filter1d(prof, size=9, mode='nearest')
    # 谷 = 局部极小
    mins = []
    for i in range(2, len(sm) - 2):
        if sm[i] <= sm[i - 1] and sm[i] <= sm[i + 1] and sm[i] < 0.82:
            mins.append(i)
    # 合并邻近
    merged = []
    for i in mins:
        if merged and i - merged[-1][-1] <= min_gap:
            merged[-1].append(i)
        else:
            merged.append([i])
    valleys = [int(np.mean(g)) for g in merged]
    # 转成权重(越深越像谷)
    scored = sorted(valleys, key=lambda v: sm[v])
    return valleys, sm, scored


print("\n--- 列投影(找竖枝桠) ---")
colv, colsm, colscored = find_valleys(colprof)
print("候选谷(相对 blob 左边):", colv)
print("最深几个:", sorted(colscored)[:8], [round(float(colsm[v]), 3) for v in sorted(colscored)[:8]])

print("\n--- 行投影(找横枝桠) ---")
rowv, rowsm, rowscored = find_valleys(rowprof)
print("候选谷(相对 blob 顶边):", rowv)
print("最深几个:", sorted(rowscored)[:8], [round(float(rowsm[v]), 3) for v in sorted(rowscored)[:8]])

# 兜底:直接按 4 等分给出中心
print("\n=== 若按 blob bbox 4 等分 ===")
pw = (gx1 - gx0 + 1) / 4.0
ph = (gy1 - gy0 + 1) / 4.0
for r in range(4):
    rowtxt = []
    for c in range(4):
        cx = gx0 + pw * (c + 0.5)
        cy = gy0 + ph * (r + 0.5)
        rowtxt.append(f"({cx:7.1f},{cy:7.1f})")
    print("  ", "  ".join(rowtxt))
print("cell pitch: ", round(pw, 2), "x", round(ph, 2))
