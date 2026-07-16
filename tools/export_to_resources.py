# -*- coding: utf-8 -*-
"""从 PSD 导出青年/中年/老年三形态合成图到 Unity 工程 Resources。
   - 强制组内所有部位图层可见后 composite
   - 裁剪到内容包围盒(去掉透明边),使"图底 = 脚底"
   - 命名为英文 young/middle/old,供 Resources.Load<Sprite>("Characters/xxx")
   PPU / pivot 由 CharacterSpriteImporter.cs(AssetPostprocessor)在 Unity 侧设置。
"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from psd_tools import PSDImage

path = r"C:\Users\yuan\Desktop\未命名作品.psd"
outdir = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Characters"
os.makedirs(outdir, exist_ok=True)

name_map = {"青年": "young", "中年": "middle", "老年": "old"}
psd = PSDImage.open(path)

def set_all_visible(layer, vis):
    layer.visible = vis
    if layer.is_group():
        for c in layer:
            set_all_visible(c, vis)

groups = {l.name: l for l in psd if l.is_group()}

for cn, en in name_map.items():
    grp = groups.get(cn)
    if grp is None:
        print(f"[跳过] 未找到分组 {cn}")
        continue
    for l in psd:
        set_all_visible(l, False)
    set_all_visible(grp, True)
    img = psd.composite(force=True)
    bbox = img.getbbox()
    cropped = img.crop(bbox)
    out = os.path.join(outdir, f"{en}.png")
    cropped.save(out)
    w, h = cropped.size
    print(f"{cn}->{en}: bbox={bbox} cropped={w}x{h} ppu400->{w/400:.2f}x{h/400:.2f}单位 -> {out}")
