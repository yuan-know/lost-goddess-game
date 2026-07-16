# -*- coding: utf-8 -*-
"""确认 old 立绘的 x=8 竖线来自原始PSD。直接看裁剪后(未加pad)的图,
   并把该竖线区域涂红导出预览,看它在人物哪个位置。"""
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from psd_tools import PSDImage
from PIL import Image
import numpy as np

path = r"C:\Users\yuan\Desktop\未命名作品.psd"
psd = PSDImage.open(path)

def set_all(layer, vis):
    layer.visible = vis
    if layer.is_group():
        for c in layer:
            set_all(c, vis)

groups = {l.name: l for l in psd if l.is_group()}
grp = groups["老年"]
for l in psd:
    set_all(l, False)
grp.visible = True
target = next(l for l in grp if l.name == "图层 1")
target.visible = True
img = psd.composite(force=True)
bbox = img.getbbox()
print("老年立绘层 bbox(未裁):", bbox)
cropped = img.crop(bbox)
a = np.array(cropped.convert("RGBA"))
alpha = a[..., 3]
H, W = alpha.shape
print(f"裁剪后 {W}x{H}")

opaque = alpha > 16
col_count = opaque.sum(axis=0)
# 找通顶列(未加pad,所以竖线应在 x=0 附近)
for x in range(min(20, W)):
    print(f"  x={x}: 不透明列高={col_count[x]}/{H} ({col_count[x]/H*100:.0f}%)  该列顶部行={np.argmax(opaque[:,x]) if opaque[:,x].any() else -1}")

# 看最左那列的实际RGBA几个采样
print("\n最左列(x=0)几个像素RGBA:")
for y in [0, H//4, H//2, H*3//4, H-1]:
    print(f"  y={y}: {tuple(a[y,0])}")
