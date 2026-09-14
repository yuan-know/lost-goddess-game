# -*- coding: utf-8 -*-
"""导出 背包空白格子.psd 的合成图与各图层为 PNG(ASCII 路径,避免中文路径乱码)。"""
import os
import sys
sys.stdout.reconfigure(encoding='utf-8')
from psd_tools import PSDImage

PATH = os.environ.get("BAG_PSD") or (sys.argv[1] if len(sys.argv) > 1 else "")
if not PATH or not os.path.exists(PATH):
    raise SystemExit("用法: 先设 BAG_PSD=<背包空白格子.psd 的本机路径>,再运行本脚本")
OUT = r"C:\Users\yuan\lost-goddess-game\tools\inventory\raw"
os.makedirs(OUT, exist_ok=True)

psd = PSDImage.open(PATH)

comp = psd.composite()
comp.save(os.path.join(OUT, "psd_composite.png"))
print("composite:", comp.size, comp.mode, "->", "psd_composite.png")

for i, layer in enumerate(psd):
    img = layer.composite()
    if img is None:
        print("layer", i, layer.name, "composite None")
        continue
    name = f"layer_{i}_{layer.name}.png"
    name = "".join(ch if ord(ch) < 128 else "_" for ch in name)
    img.save(os.path.join(OUT, name))
    print("layer", i, repr(layer.name), img.size, "bbox=", layer.bbox, "->", name)
