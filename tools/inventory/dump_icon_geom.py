# -*- coding: utf-8 -*-
"""导出各道具图标的「源尺寸 + alpha 内容外框」,直接生成 C# 常量表。

Unity 侧要按内容框(而不是整张贴图)来居中与定尺寸,否则自带留白大的图标会显得小。
坐标统一转成 Unity 贴图坐标(左下原点, y 向上)。
"""
import os
import sys
import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding='utf-8')

ROOT = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources"

ITEMS = [
    ("crowbar",         "UI/Icons/crowbar_v2",        "铁撬棍"),
    ("wrench",          "UI/Icons/wrench",            "铸铁扳手"),
    ("hex_wrench",      "UI/Icons/hex_wrench",        "六角扳手"),
    ("pottery_key",     "UI/Icons/pottery_key",       "陶罐钥匙"),
    ("focus_lens",      "UI/Icons/focus_lens_v2",     "聚焦透镜"),
    ("brass_base",      "UI/Icons/brass_base_v2",     "黄铜底座"),
    ("gear_1",          "UI/Icons/gear_small_v2",     "小齿轮"),
    ("gear_2",          "UI/Icons/gear_big_v2",       "大齿轮"),
    ("light_projector", "Props/Prologue/prop_projector", "光幕投影仪"),
    ("lantern",         "UI/Icons/hand_lamp",         "提灯(未实装)"),
    ("magnifier",       "UI/Icons/magnifier",         "放大镜(未实装)"),
]

print("// ── 由 tools/inventory/dump_icon_geom.py 生成,勿手改 ──")
print("//   源尺寸 sw,sh;内容框(Unity 贴图坐标,左下原点)y 向上 uint")
print("static readonly ItemGeom[] Items = {")
for item, rel, cn in ITEMS:
    p = os.path.join(ROOT, rel.replace("/", os.sep) + ".png")
    im = Image.open(p).convert("RGBA")
    a = np.asarray(im)[..., 3]
    H, W = a.shape
    ys, xs = np.where(a > 16)
    x0, x1 = int(xs.min()), int(xs.max()) + 1
    y0t, y1t = int(ys.min()), int(ys.max()) + 1
    # 转左下原点
    y0u, y1u = H - y1t, H - y0t
    print(f'    new ItemGeom("{item}", "{rel}", "{cn}", {W}, {H}, '
          f'{x0}, {y0u}, {x1}, {y1u}),')
print("};")
print()
print("// 汇总: 内容宽高")
for item, rel, cn in ITEMS:
    p = os.path.join(ROOT, rel.replace("/", os.sep) + ".png")
    im = Image.open(p).convert("RGBA")
    a = np.asarray(im)[..., 3]
    ys, xs = np.where(a > 16)
    bw = int(xs.max() - xs.min() + 1)
    bh = int(ys.max() - ys.min() + 1)
    print(f"//   {item:<16} {bw:>4}x{bh:<4} aspect={bw/bh:.3f}  (占画布 {bw/im.width:.2f}x{bh/im.height:.2f})")
