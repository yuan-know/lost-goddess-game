# -*- coding: utf-8 -*-
"""啃掉人物边缘的浅色描边(白线真凶)。

诊断结论:美术立绘在 alpha=255 的最外 1~2px 轮廓上,RGB 是浅灰白(≈170-205),
内缩 5px 才是深色布料(≈10-50)。这圈浅白描边被引擎缩小 7.4 倍显示时,在深色
背景上渲染成一条常驻竖线。透明区 bleed / mipmap 都治不了它,因为白就在不透明像素里。

做法:对 alpha 做二值化 → 形态学腐蚀 erode_px 像素 → 用腐蚀后的掩膜裁掉最外圈,
即把最外 erode_px 圈像素置为全透明。露出里面的深色布料作新边缘 → 缩小后不再有浅线。
腐蚀后再对新边缘做一次 alpha bleed(把新暴露的半透明边 RGB 外扩成深色),双保险。
"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
import numpy as np
from PIL import Image
from scipy import ndimage

outdir = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Characters"
ERODE_PX = 2   # 啃掉最外 2px 浅边;不够可调 3

def strip_rim(path, erode_px=ERODE_PX):
    im = np.array(Image.open(path).convert("RGBA"))
    alpha = im[..., 3]
    rgb = im[..., :3]
    solid = alpha > 40                       # 人物实体(含半透明边)
    # 腐蚀:去掉最外 erode_px 圈
    eroded = ndimage.binary_erosion(solid, iterations=erode_px)
    # 被啃掉的圈(原来是实体、腐蚀后不是)→ 置全透明
    out = im.copy()
    kill = solid & ~eroded
    out[kill, 3] = 0
    # 对新边缘做 alpha bleed:把 alpha<250 的像素 RGB 外扩成最近 alpha>=250 的颜色
    a2 = out[..., 3]
    src = a2 >= 250
    if src.any() and not src.all():
        idx = ndimage.distance_transform_edt(~src, return_distances=False, return_indices=True)
        out[..., :3] = out[..., :3][tuple(idx)]
    out[..., 3] = a2                          # alpha 保持腐蚀后的结果
    Image.fromarray(out, "RGBA").save(path)
    return kill.sum(), out

def edge_report(im, tag):
    alpha = im[..., 3]; rgb = im[..., :3]; h, w, _ = im.shape
    vals = []
    for y in range(int(h*0.3), int(h*0.7), 60):
        xs = np.where(alpha[y] >= 200)[0]
        if len(xs): vals.append(rgb[y, xs.max()].mean())
    print(f"  {tag} 右轮廓亮度均值: {np.mean(vals):.0f} (越低越好,深色布料<60)")

for n in ["old", "young", "middle"]:
    p = os.path.join(outdir, f"{n}.png")
    before = np.array(Image.open(p).convert("RGBA"))
    edge_report(before, f"{n} 处理前")
    killed, after = strip_rim(p)
    edge_report(after, f"{n} 处理后")
    print(f"  {n}: 啃掉 {killed} 个浅边像素 -> {p}\n")
print("完成。回 Unity 对 Resources/Characters 右键 Reimport 再 Play。")
