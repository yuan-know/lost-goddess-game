# -*- coding: utf-8 -*-
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

path = r"C:\Users\yuan\Desktop\未命名作品.psd"

from PIL import Image
im = Image.open(path)
print("=== Pillow ===")
print("size:", im.size, "mode:", im.mode)

try:
    from psd_tools import PSDImage
    psd = PSDImage.open(path)
    print("\n=== psd_tools ===")
    print("canvas:", psd.width, "x", psd.height, "channels:", psd.channels)
    def walk(layer, depth=0):
        pad = "  " * depth
        vis = "V" if layer.visible else " "
        bbox = layer.bbox
        print(f"{pad}[{vis}] '{layer.name}'  kind={layer.kind}  bbox={bbox}  size={layer.size}")
        if layer.is_group():
            for c in layer:
                walk(c, depth+1)
    for layer in psd:
        walk(layer)
except Exception as e:
    print("psd_tools not ready:", e)
