# -*- coding: utf-8 -*-
"""解析 背包空白格子.psd 的图层结构(树 + bbox)。"""
import os
import sys
sys.stdout.reconfigure(encoding='utf-8')
from psd_tools import PSDImage

PATH = os.environ.get("BAG_PSD") or (sys.argv[1] if len(sys.argv) > 1 else "")
if not PATH or not os.path.exists(PATH):
    raise SystemExit("用法: 先设 BAG_PSD=<背包空白格子.psd 的本机路径>,再运行本脚本")
psd = PSDImage.open(PATH)
print("CANVAS:", psd.width, "x", psd.height, " mode:", psd.color_mode, " depth:", psd.depth)


def walk(layer, depth=0):
    pad = "  " * depth
    try:
        bbox = layer.bbox
    except Exception:
        bbox = None
    vis = "" if layer.visible else "  [HIDDEN]"
    try:
        size = layer.size
    except Exception:
        size = None
    try:
        off = layer.offset
    except Exception:
        off = None
    print(f"{pad}- {layer.name} ({type(layer).__name__}) bbox={bbox} size={size} offset={off}{vis}")
    if layer.is_group():
        for c in layer:
            walk(c, depth + 1)


for l in psd:
    walk(l)
