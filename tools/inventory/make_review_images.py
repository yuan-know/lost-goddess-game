# -*- coding: utf-8 -*-
"""出交付评审图:默认参数下的背包 UI 预览 + 大小档位对比 + 含未实装道具版。

默认参数与 InventoryDebugBootstrap 的初值一致:
  PanelHeightRatio = 0.94  ItemFill = 0.70  DimAlpha = 0.60
"""
import os
import sys
sys.stdout.reconfigure(encoding='utf-8')
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import preview_panel as P
from PIL import Image

DOCS = r"C:\Users\yuan\lost-goddess-game\docs\inventory"
os.makedirs(DOCS, exist_ok=True)

# 1) 默认参数主图
main = P.render(0.70, list(P.ITEMS))
p1 = os.path.join(DOCS, "背包UI_调试预览.png")
main.save(p1)
print("->", p1)

# 2) 含未实装道具
extra = P.render(0.70, list(P.ITEMS) + list(P.EXTRA))
p2 = os.path.join(DOCS, "背包UI_调试预览_含未实装.png")
extra.save(p2)
print("->", p2)

# 3) 大小档位对比
fills = (0.63, 0.70, 0.78)
ims = [P.render(f, list(P.ITEMS)) for f in fills]
sheet = Image.new("RGB", (P.BAND_W, (P.BAND_H + 14) * len(ims)), (0, 0, 0))
y = 0
for im in ims:
    sheet.paste(im, (0, y)); y += P.BAND_H + 14
p3 = os.path.join(DOCS, "背包UI_大小档位对比.png")
sheet.save(p3)
print("->", p3, "(0.63 / 0.70 / 0.78 自上而下)")

# 4) 带格子框与十字线(校验居中)
marks = P.render(0.70, list(P.ITEMS), draw_marks=True)
p4 = os.path.join(DOCS, "背包UI_格子对位校验.png")
marks.save(p4)
crop = marks.crop((P.BAND_W // 2 - 420, 8, P.BAND_W // 2 + 420, P.BAND_H - 8))
crop = crop.resize((crop.width * 3 // 2, crop.height * 3 // 2), Image.LANCZOS)
p5 = os.path.join(DOCS, "背包UI_格子对位校验_放大.png")
crop.save(p5)
print("->", p4, "/", p5)
