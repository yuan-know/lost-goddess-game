"""拆解 d6_gear_wheel.png：把「透明孔」和「不透明近黑块」分别连成连通域列出来，
并输出放大裁剪图供人眼核对。"""
import os
import numpy as np
import cv2
from PIL import Image

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\d6"


def comps(mask, tag, min_area=200):
    n, lab, stats, cent = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    rows = []
    for i in range(1, n):
        x, y, w, h, area = stats[i]
        if area < min_area:
            continue
        rows.append((area, x, y, w, h, cent[i][0], cent[i][1]))
    rows.sort(reverse=True)
    print(f"\n-- {tag}: {len(rows)} 个大区域 (>= {min_area}px)")
    for area, x, y, w, h, cx, cy in rows[:12]:
        print(f"   area={area:6d}  bbox=({x:3d},{y:3d},{w:3d}x{h:3d})  "
              f"质心=({cx:6.1f},{cy:6.1f})")


im = Image.open(os.path.join(D6, "d6_gear_wheel.png")).convert("RGBA")
a = np.array(im)
alpha = a[:, :, 3]
rgb = a[:, :, :3].astype(int)

solid = alpha > 8
transparent = ~solid
near_black_inside = solid & (rgb.max(axis=2) < 24)

# 只看内容 bbox 内
ys, xs = np.nonzero(solid)
x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
print(f"内容 bbox ({x0},{y0})-({x1},{y1})")

comps(transparent, "透明区域(可能是真孔)")
comps(near_black_inside, "不透明近黑区域(可能是画死的孔)")

# 中心点附近到底什么颜色
cx, cy = int(round((x0 + x1) / 2)), int(round((y0 + y1) / 2))
print(f"\n中心 {cx},{cy} = {tuple(a[cy, cx])}")
for dy in (-60, -30, 0, 30, 60):
    row = []
    for dx in (-60, -30, 0, 30, 60):
        px = a[cy + dy, cx + dx]
        row.append(f"({dx:+3d},{dy:+3d})a{px[3]:3d}r{px[0]:3d}")
    print("   " + "  ".join(row))

# 输出：齿轮原图 + 把「不透明近黑」涂红 / 「透明」涂绿 的诊断图
vis = a.copy()
vis[near_black_inside] = [255, 0, 0, 255]
vis[transparent] = [0, 255, 0, 255]
os.makedirs(OUT, exist_ok=True)
big = im.resize((1000, 1000), Image.NEAREST)
big.save(os.path.join(OUT, "齿轮_原图放大.png"))
Image.fromarray(vis).resize((1000, 1000), Image.NEAREST).save(os.path.join(OUT, "齿轮_诊断_红=不透明近黑_绿=透明.png"))
print("\nwrote 齿轮_原图放大.png / 齿轮_诊断_红=不透明近黑_绿=透明.png")
