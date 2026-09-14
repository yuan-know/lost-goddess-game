# -*- coding: utf-8 -*-
"""从 背包空白格子.psd 生成 Unity 用的背包底图。

PSD 结构:
  · 图层 1        —— 整幅 3400x1200 纯黑 alpha=153(60%),是美术给的"背景压暗"层
                     ★ 不能烘进底图,游戏里有自己的 DimBackground,否则会叠两次
  · 背包空白格子   —— 背包本体(含挂在右侧的油灯),bbox (1092,208)-(2173,1076) = 1081x868

产出:
  Assets/_Project/Resources/UI/Inventory/bag_panel_v2.png   1121x908(背包 + 20px 外扩)
  tools/inventory/panel_grid.json                           槽位几何(供 C# 与预览脚本共用)
"""
import os
import sys
import json
import numpy as np
from PIL import Image
from psd_tools import PSDImage
from scipy import ndimage

sys.stdout.reconfigure(encoding='utf-8')

PSD = os.environ.get("BAG_PSD") or (sys.argv[1] if len(sys.argv) > 1 else "")
if not PSD or not os.path.exists(PSD):
    raise SystemExit("用法: 先设 BAG_PSD=<背包空白格子.psd 的本机路径>,再运行本脚本")
OUT_PNG = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\UI\Inventory\bag_panel_v2.png"
OUT_JSON = r"C:\Users\yuan\lost-goddess-game\tools\inventory\panel_grid.json"
WORK = r"C:\Users\yuan\lost-goddess-game\tools\inventory\raw"

BAG_LAYER = "背包空白格子"
CROP_PAD = 20

psd = PSDImage.open(PSD)
bag = None
for l in psd:
    if l.name.strip() == BAG_LAYER:
        bag = l
        break
if bag is None:
    raise SystemExit("没找到图层: " + BAG_LAYER)

bbox = bag.bbox
print("bag layer bbox:", bbox, "size", bag.size)

# 1) 把背包层贴到与 PSD 等大的透明画布上(保持 PSD 坐标系,方便和量测对齐)
full = Image.new("RGBA", (psd.width, psd.height), (0, 0, 0, 0))
img = bag.composite()
full.alpha_composite(img, (bbox[0], bbox[1]))
full.save(os.path.join(WORK, "bag_layer_full.png"))

# 1b) 清孤岛:背包下方有一串生成残留细线(y≈870, alpha 21~84, 最宽 216px)
#     —— 只保留最大的连通块(背包本体 + 油灯),其余按面积阈值丢掉。
fa = np.asarray(full)[..., 3]
lab0, n0 = ndimage.label(fa > 8, structure=np.ones((3, 3)))
sizes = ndimage.sum(fa > 8, lab0, range(1, n0 + 1))
keep = int(np.argmax(sizes)) + 1
dropped = [i for i in range(1, n0 + 1) if i != keep]
if dropped:
    print(f"清理孤岛: 共 {n0} 块,丢弃 {len(dropped)} 块 "
          f"(面积 {[int(sizes[i-1]) for i in dropped]})")
arr = np.asarray(full).copy()
arr[lab0 != keep] = 0
full = Image.fromarray(arr)

# 2) 裁到 背包外框 + pad
crop = (bbox[0] - CROP_PAD, bbox[1] - CROP_PAD, bbox[2] + CROP_PAD, bbox[3] + CROP_PAD)
panel = full.crop(crop)
os.makedirs(os.path.dirname(OUT_PNG), exist_ok=True)
panel.save(OUT_PNG)
print("panel:", panel.size, "->", OUT_PNG)

# 3) 在成品底图上重新量 16 格(验证裁剪偏移正确,并作为唯一真源写进 json)
a = np.asarray(panel.convert("RGBA")).astype(np.float32)
rgb, alpha = a[..., :3], a[..., 3]
mx, mn = rgb.max(axis=2), rgb.min(axis=2)
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
    if area < 5000 or area / float(h * w) < 0.6:
        continue
    cells.append(dict(cx=xs.start + w / 2.0, cy=ys.start + h / 2.0, w=w, h=h))
print("cells found:", len(cells))
assert len(cells) == 16, "格子数不是 16,先别往下走"

cells.sort(key=lambda c: c['cy'])
rows = []
for c in cells:
    if rows and abs(c['cy'] - np.mean([r['cy'] for r in rows[-1]])) < 60:
        rows[-1].append(c)
    else:
        rows.append([c])
for r in rows:
    r.sort(key=lambda c: c['cx'])

cols_x = [round(float(np.mean([r[i]['cx'] for r in rows])), 1) for i in range(4)]
rows_y = [round(float(np.mean([c['cy'] for c in r])), 1) for r in rows]
tile_w = round(float(np.mean([c['w'] for r in rows for c in r])), 1)
tile_h = round(float(np.mean([c['h'] for r in rows for c in r])), 1)

# ★ 逐格实测中心(行优先 16 个):手绘格子并非严格等距,逐格值能让道具真正落在每格正中
slot_cx = [round(float(rows[r][c]['cx']), 1) for r in range(4) for c in range(4)]
slot_cy = [round(float(rows[r][c]['cy']), 1) for r in range(4) for c in range(4)]
dev_x = [round(slot_cx[i] - cols_x[i % 4], 1) for i in range(16)]
dev_y = [round(slot_cy[i] - rows_y[i // 4], 1) for i in range(16)]
print("逐格中心相对均值列的偏差 X:", dev_x)
print("逐格中心相对均值行的偏差 Y:", dev_y)

meta = dict(
    source_psd=os.path.basename(PSD),
    panel_png="Assets/_Project/Resources/UI/Inventory/bag_panel_v2.png",
    panel_w=panel.width, panel_h=panel.height,
    crop_box=list(crop),                        # 相对 PSD 画布,仅作溯源
    col_x=cols_x,                               # 相对底图左边缘,px(4 列均值)
    row_y=rows_y,                               # 相对底图顶边,px(4 行均值)
    slot_cx=slot_cx,                            # ★ 逐格中心 X(行优先 16 个) = 排版真源
    slot_cy=slot_cy,                            # ★ 逐格中心 Y(行优先 16 个)
    tile_w=tile_w, tile_h=tile_h,               # 单格可见羊皮纸内框(均值)
    col_pitch=[round(cols_x[i + 1] - cols_x[i], 1) for i in range(3)],
    row_pitch=[round(rows_y[i + 1] - rows_y[i], 1) for i in range(3)],
    note="坐标单位为 bag_panel_v2.png 的像素;Unity 侧按 底图高度/panel_h 等比缩放。",
)
with open(OUT_JSON, "w", encoding="utf-8") as f:
    json.dump(meta, f, ensure_ascii=False, indent=2)
print(json.dumps(meta, ensure_ascii=False, indent=2))
