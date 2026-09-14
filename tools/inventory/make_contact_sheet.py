# -*- coding: utf-8 -*-
"""把候选道具图标拼成一张对照图(按 alpha 内容框裁紧 + 统一最长边),
用来肉眼判断「同一批图标相对大小是否合理」。
"""
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

sys.stdout.reconfigure(encoding='utf-8')

ROOT = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\inventory\icons_contact_sheet.png"

ICONS = [
    ("crowbar 铁撬棍",   "UI/Icons/crowbar_v2"),
    ("wrench 铸铁扳手",  "UI/Icons/wrench"),
    ("hex_wrench 六角扳手", "UI/Icons/hex_wrench"),
    ("pottery_key 陶罐钥匙", "UI/Icons/pottery_key"),
    ("focus_lens 聚焦透镜", "UI/Icons/focus_lens_v2"),
    ("brass_base 黄铜底座", "UI/Icons/brass_base_v2"),
    ("gear_1 小齿轮",    "UI/Icons/gear_small_v2"),
    ("gear_2 大齿轮",    "UI/Icons/gear_big_v2"),
    ("light_projector 投影仪", "Closeups/light_projector"),
]

CELL = 220
PAD = 18
COLS = 3
ROWS = (len(ICONS) + COLS - 1) // COLS
W = COLS * CELL
H = ROWS * CELL + 34

sheet = Image.new("RGBA", (W, H), (28, 24, 20, 255))
d = ImageDraw.Draw(sheet)


def trim(im):
    a = np.asarray(im)[..., 3]
    ys, xs = np.where(a > 16)
    return im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))


for i, (label, rel) in enumerate(ICONS):
    p = os.path.join(ROOT, rel.replace("/", os.sep) + ".png")
    im = Image.open(p).convert("RGBA")
    t = trim(im)
    # 统一:最长边 -> CELL-2*PAD
    target = CELL - 2 * PAD
    s = target / max(t.size)
    t = t.resize((max(1, int(t.width * s)), max(1, int(t.height * s))), Image.LANCZOS)

    cx = (i % COLS) * CELL + CELL // 2
    cy = (i // COLS) * CELL + CELL // 2
    # 用羊皮纸色底衬出图标
    d.rounded_rectangle([cx - CELL // 2 + 6, cy - CELL // 2 + 6,
                         cx + CELL // 2 - 6, cy + CELL // 2 - 6],
                        radius=14, fill=(232, 220, 192, 255))
    d.rectangle([cx - 3, cy, cx + 3, cy], fill=(220, 40, 40, 255))
    sheet.alpha_composite(t, (cx - t.width // 2, cy - t.height // 2))
    d.text((cx - CELL // 2 + 10, cy + CELL // 2 - 30), label, fill=(255, 240, 210, 255))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
sheet.convert("RGB").save(OUT)
print("->", OUT, sheet.size)
