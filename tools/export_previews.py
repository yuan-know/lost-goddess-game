# -*- coding: utf-8 -*-
"""把 PSD 里青年/中年/老年三个组各自合成一张预览 PNG。
   强制组内所有图层可见后 composite,便于确认美术素材实际长相与尺寸。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

from psd_tools import PSDImage

path = r"C:\Users\yuan\Desktop\未命名作品.psd"
outdir = r"C:\Users\yuan\lost-goddess-game\tools\psd_preview"
os.makedirs(outdir, exist_ok=True)

psd = PSDImage.open(path)

def set_all_visible(layer, vis):
    layer.visible = vis
    if layer.is_group():
        for c in layer:
            set_all_visible(c, vis)

groups = {}
for layer in psd:
    if layer.is_group():
        groups[layer.name] = layer

# 先全部隐藏
for layer in psd:
    set_all_visible(layer, False)

for name, grp in groups.items():
    # 只让当前组及其所有子层可见
    for layer in psd:
        set_all_visible(layer, False)
    set_all_visible(grp, True)
    img = psd.composite(force=True)
    # 裁到非透明包围盒,便于看真实占用与脚底位置
    bbox = img.getbbox()
    out = os.path.join(outdir, f"{name}.png")
    img.save(out)
    print(f"{name}: full={img.size} content_bbox={bbox} -> {out}")
