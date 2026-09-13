"""d6 齿轮/旋钮素材体检：alpha 分布、内容 bbox、中心区域是否透光。
用途：定位「齿轮孔被黑色填死，看不到里面的图层」以及「旋钮实际可抓半径」。
"""
import os
import sys

import numpy as np
from PIL import Image

DIR = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"


def inspect(name):
    p = os.path.join(DIR, name)
    im = Image.open(p).convert("RGBA")
    a = np.array(im)
    H, W = a.shape[:2]
    alpha = a[:, :, 3]
    solid = alpha > 8
    ys, xs = np.nonzero(solid)
    if len(xs) == 0:
        print(f"{name}: 全透明?")
        return
    print(f"\n=== {name}  {W}x{H} ===")
    print(f"不透明像素 {solid.sum()} / {W*H}  占比 {solid.mean()*100:.1f}%")
    print(f"内容 bbox  x[{xs.min()}..{xs.max()}] y[{ys.min()}..{ys.max()}]"
          f"   尺寸 {xs.max()-xs.min()+1}x{ys.max()-ys.min()+1}")
    cx, cy = (xs.min() + xs.max()) / 2.0, (ys.min() + ys.max()) / 2.0
    print(f"内容质心   ({xs.mean():.1f}, {ys.mean():.1f})    bbox 中心 ({cx:.1f}, {cy:.1f})"
          f"   归一化 pivot = ({cx/W:.4f}, {1 - cy/H:.4f})")

    # 沿过中心的水平/垂直线打印 alpha，看有没有被包住的"洞"
    row = int(round(cy))
    col = int(round(cx))
    def runs(arr, label):
        # 找 alpha 由不透明 → 透明 → 不透明的"内部空隙"
        t = arr > 8
        holes = []
        i = 0
        n = len(t)
        # 跳过前导透明
        while i < n and not t[i]:
            i += 1
        while i < n:
            j = i
            while j < n and t[j]:
                j += 1
            k = j
            while k < n and not t[k]:
                k += 1
            if k < n and j > i:          # 内部有一段透明，且后面又变不透明
                holes.append((j, k - 1, k - j))
            i = k
        print(f"  {label}: 内部透明段 {holes if holes else '无'}")
    runs(alpha[row, :], f"水平线 y={row}")
    runs(alpha[:, col], f"垂直线 x={col}")

    # 中心区域取样
    print(f"  中心像素 RGBA = {tuple(a[row, col])}")
    for r in (8, 20, 40, 60, 80):
        vals = []
        th = np.linspace(0, 2 * np.pi, 16, endpoint=False)
        for t in th:
            x = int(round(cx + r * np.cos(t)))
            y = int(round(cy + r * np.sin(t)))
            if 0 <= x < W and 0 <= y < H:
                vals.append(a[y, x])
        if vals:
            v = np.array(vals, dtype=float)
            print(f"  r={r:3d}px  16 点 alpha 均值 {v[:,3].mean():6.1f}  "
                  f"RGB 均值 ({v[:,0].mean():5.1f},{v[:,1].mean():5.1f},{v[:,2].mean():5.1f})  "
                  f"不透明点 {int((v[:,3]>8).sum())}/16")

    # 不透明区域里"接近纯黑"的像素占比（黑色可能是被填死的洞）
    rgb = a[:, :, :3].astype(int)
    nearly_black = solid & (rgb.max(axis=2) < 16)
    print(f"  不透明且近纯黑(管 max<16) 的像素 {int(nearly_black.sum())} "
          f"= 不透明区的 {nearly_black.sum()/max(1,solid.sum())*100:.1f}%")
    if nearly_black.sum() > 0:
        ny, nx = np.nonzero(nearly_black)
        print(f"     其 bbox x[{nx.min()}..{nx.max()}] y[{ny.min()}..{ny.max()}] "
              f"质心 ({nx.mean():.1f},{ny.mean():.1f})")


if __name__ == "__main__":
    for n in (sys.argv[1:] or ["d6_gear_wheel.png", "d6_knob.png"]):
        inspect(n)
