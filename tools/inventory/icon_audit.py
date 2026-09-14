# -*- coding: utf-8 -*-
"""审计背包候选图标:像素尺寸 + alpha 内容外框 + 内容占比。

用来决定「每个道具在格子里应该多大」——图标自带的透明边距差异极大,
直接 preserveAspect 塞进同一内框会让有的道具显得很小。
"""
import os
import sys
import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding='utf-8')

ROOT = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources"

CANDIDATES = [
    ("crowbar",        "UI/Icons/crowbar_v2"),
    ("crowbar(old)",   "UI/Icons/crowbar"),
    ("wrench",         "UI/Icons/wrench"),
    ("wrench_ground",  "UI/Icons/wrench_ground"),
    ("hex_wrench",     "UI/Icons/hex_wrench"),
    ("pottery_key",    "UI/Icons/pottery_key"),
    ("focus_lens",     "UI/Icons/focus_lens_v2"),
    ("brass_base",     "UI/Icons/brass_base_v2"),
    ("gear_1/small",   "UI/Icons/gear_small_v2"),
    ("gear_2/big",     "UI/Icons/gear_big_v2"),
    ("light_projector","Closeups/light_projector"),
    ("prop_projector", "Props/Prologue/prop_projector"),
    ("hand_lamp",      "UI/Icons/hand_lamp"),
    ("magnifier",      "UI/Icons/magnifier"),
    ("backpack_icon",  "UI/Inventory/backpack_icon"),
]

print(f"{'key':<17}{'path':<34}{'size':>12}{'contentBox':>20}{'covW':>7}{'covH':>7}{'alpha':>7}")
for key, rel in CANDIDATES:
    p = os.path.join(ROOT, rel.replace("/", os.sep) + ".png")
    if not os.path.exists(p):
        print(f"{key:<17}{rel:<34}  <MISSING>")
        continue
    im = Image.open(p).convert("RGBA")
    a = np.asarray(im)[..., 3]
    H, W = a.shape
    m = a > 16
    if not m.any():
        print(f"{key:<17}{rel:<34}{W:>6}x{H:<5}  <ALL TRANSPARENT>")
        continue
    ys, xs = np.where(m)
    bw, bh = xs.max() - xs.min() + 1, ys.max() - ys.min() + 1
    cov = (a > 200).mean()
    print(f"{key:<17}{rel:<34}{W:>6}x{H:<5}{bw:>8}x{bh:<8}"
          f"{bw/W:>7.2f}{bh/H:>7.2f}{cov:>7.2f}")
