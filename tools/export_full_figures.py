# -*- coding: utf-8 -*-
"""正确导出:每组只用"完整立绘层"(老年图层1/青年图层29/中年已插入图像),
   不叠骨骼分件。裁到内容包围盒使图底=脚底,写进 Resources/Characters。
   关键:PSD透明区RGB藏着白色→双线性/mipmap采样渗白边。导出前做 alpha bleeding
   (把透明像素RGB外扩成最近不透明像素颜色),从根上消除白线。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from psd_tools import PSDImage
from PIL import Image
import numpy as np
from scipy import ndimage

path = r"C:\Users\yuan\Desktop\未命名作品.psd"
outdir = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Characters"
os.makedirs(outdir, exist_ok=True)


def alpha_bleed(img):
    """把半透明/透明像素的RGB替换为最近"完全不透明"像素的RGB(保留alpha不变)。
       此PSD在白底绘制,抗锯齿边缘的半透明像素RGB被焊成白色→采样渗白边。
       只认 alpha>=250 的像素为颜色源,向其余所有像素外扩,挤掉白边。"""
    a = np.array(img.convert("RGBA"))
    rgb = a[..., :3]
    alpha = a[..., 3]
    solid = alpha >= 250                     # 只有完全不透明像素才算"可信颜色源"
    if solid.all() or not solid.any():
        return img
    inv = ~solid
    idx = ndimage.distance_transform_edt(inv, return_distances=False, return_indices=True)
    bled = rgb[tuple(idx)]
    out = a.copy()
    out[..., :3] = bled                      # 所有非solid像素的RGB取最近solid颜色
    out[..., 3] = alpha                       # alpha原样保留,透明度不变
    return Image.fromarray(out, "RGBA")


psd = PSDImage.open(path)

def set_all(layer, vis):
    layer.visible = vis
    if layer.is_group():
        for c in layer:
            set_all(c, vis)

groups = {l.name: l for l in psd if l.is_group()}

# 每组的"完整立绘层"名 -> 英文输出名
full_layer = {"青年": ("图层 29", "young"),
              "中年": ("已插入图像", "middle"),
              "老年": ("图层 1", "old")}

for gname, (layer_name, en) in full_layer.items():
    grp = groups[gname]
    for l in psd:
        set_all(l, False)
    grp.visible = True
    target = next(l for l in grp if l.name == layer_name)
    target.visible = True
    img = psd.composite(force=True)
    bbox = img.getbbox()
    cropped = img.crop(bbox)
    # ① 先 alpha bleeding:只改 RGB(外扩人物边缘色),alpha 原样保留透明 → 消白边
    bled = alpha_bleed(cropped)
    # ② 再加透明边(左右+顶,底不留保脚底)。这些 pad 像素 alpha=0 完全不可见,
    #    且在 bleed 之后加,不会被外扩污染成实线。
    pad = 8
    padded = Image.new("RGBA", (bled.width + pad * 2, bled.height + pad), (0, 0, 0, 0))
    padded.paste(bled, (pad, pad))
    out = os.path.join(outdir, f"{en}.png")
    padded.save(out)
    w, h = padded.size
    print(f"{gname}[{layer_name}]->{en}: {w}x{h}(先bleed后加透明边)  PPU800->{w/800:.2f}x{h/800:.2f}单位 -> {out}")
