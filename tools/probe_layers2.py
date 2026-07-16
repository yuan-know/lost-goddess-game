# -*- coding: utf-8 -*-
"""借助整体 composite 管线逐层提取(单层 composite 对本PSD失效)。
   一次只让一个图层可见 -> psd.composite(force=True) -> 看该层实际内容。
   目的:确认 图层1/图层29/已插入图像 是不是完整立绘。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from psd_tools import PSDImage

path = r"C:\Users\yuan\Desktop\未命名作品.psd"
outdir = r"C:\Users\yuan\lost-goddess-game\tools\layer_probe2"
os.makedirs(outdir, exist_ok=True)

psd = PSDImage.open(path)

def set_all(layer, vis):
    layer.visible = vis
    if layer.is_group():
        for c in layer:
            set_all(c, vis)

groups = {l.name: l for l in psd if l.is_group()}

def count_nonzero(img):
    if img.mode != 'RGBA':
        img = img.convert('RGBA')
    a = img.getchannel('A')
    return sum(1 for p in a.get_flattened_data() if p > 8)

for gname in ["老年", "青年", "中年"]:
    grp = groups[gname]
    print(f"\n===== {gname} =====")
    layers = list(grp)
    for i, target in enumerate(layers):
        # 全隐藏
        for l in psd:
            set_all(l, False)
        # 只开组 + 这一个层
        grp.visible = True
        target.visible = True
        img = psd.composite(force=True)
        bbox = img.getbbox()
        nz = count_nonzero(img) if bbox else 0
        safe = target.name.replace(' ', '')
        if bbox and nz > 500:
            img.crop(bbox).save(os.path.join(outdir, f"{gname}_{i:02d}_{safe}.png"))
        print(f"  [{i:02d}] '{target.name}'  bbox={bbox}  非透明={nz}")
