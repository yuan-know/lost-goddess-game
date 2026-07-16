# -*- coding: utf-8 -*-
"""逐层导出老年组每个图层单独的PNG,判断哪些是完整立绘、哪些是骨骼分件。
   也检查左手相关图层是否真有像素。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from psd_tools import PSDImage
from PIL import Image

path = r"C:\Users\yuan\Desktop\未命名作品.psd"
outdir = r"C:\Users\yuan\lost-goddess-game\tools\layer_probe"
os.makedirs(outdir, exist_ok=True)

psd = PSDImage.open(path)
groups = {l.name: l for l in psd if l.is_group()}

for gname in ["老年", "青年", "中年"]:
    grp = groups.get(gname)
    if not grp:
        continue
    print(f"\n===== {gname} 组 =====")
    for i, layer in enumerate(grp):
        try:
            img = layer.composite()   # 单层自身像素
            if img is None:
                print(f"  [{i}] '{layer.name}' -> composite None")
                continue
            bbox = img.getbbox()
            # 计算非透明像素数量
            alpha = img.getchannel('A') if img.mode == 'RGBA' else None
            nonzero = 0
            if alpha:
                nonzero = sum(1 for p in alpha.getdata() if p > 8)
            safe = layer.name.replace('/', '_').replace(' ', '')
            out = os.path.join(outdir, f"{gname}_{i:02d}_{safe}.png")
            img.crop(bbox).save(out) if bbox else None
            print(f"  [{i}] '{layer.name}'  contentbbox={bbox}  非透明像素={nonzero}")
        except Exception as e:
            print(f"  [{i}] '{layer.name}' -> ERR {e}")
