# -*- coding: utf-8 -*-
"""诊断白线成因:检查导出PNG里"透明像素的RGB值"和"半透明边缘像素"。
   假设:透明区RGB是白色(255)→双线性过滤时白色渗入人物边缘=白线。"""
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from PIL import Image
import numpy as np

for en in ["old", "young", "middle"]:
    p = rf"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Characters\{en}.png"
    im = Image.open(p).convert("RGBA")
    a = np.array(im)
    rgb = a[..., :3].astype(int)
    alpha = a[..., 3]

    fully_transparent = alpha == 0
    semi = (alpha > 0) & (alpha < 255)
    opaque = alpha == 255

    # 透明像素的RGB分布(白线嫌疑:透明区是白的)
    if fully_transparent.any():
        tp = rgb[fully_transparent]
        print(f"\n[{en}] 全透明像素RGB: min={tp.min(axis=0)} max={tp.max(axis=0)} mean={tp.mean(axis=0).round(1)}  数量={fully_transparent.sum()}")
    # 半透明边缘像素的RGB(白线嫌疑:边缘RGB是白的);bleed后应≈人物颜色而非255
    if semi.any():
        sp = rgb[semi]
        whiteish = ((sp > 240).all(axis=1)).sum()
        print(f"[{en}] 半透明边缘RGB: mean={sp.mean(axis=0).round(1)}  数量={semi.sum()}  近白像素(>240)={whiteish}")
    print(f"[{en}] 全透明={fully_transparent.sum()} 半透明={semi.sum()} 不透明={opaque.sum()}")
