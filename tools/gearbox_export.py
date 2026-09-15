# -*- coding: utf-8 -*-
"""
gearbox_export.py —— 齿轮箱(PZ_04z 精密操作)分层素材导出
源: C:/Users/yuan/Desktop/齿轮 (3400x1200 PSD, 无扩展名)

产出(Assets/_Project/Resources/Closeups/GearboxPuzzle/):
  gearbox_base.png       齿轮箱整体层,可动件区域填暗色(防旋转重影)
  gearbox_valve.png      S 形弯管阀件(蒸汽喷口)
  gearbox_gear_small.png 小锥齿轮
  gearbox_gear_big.png   大齿轮
  gearbox_shaft.png      顶部竖轴
  scar_offset_line.png   刻痕「偏之一线,灼身千里」(黑底转 alpha,叠加用)

⚠ PSD 第 1 层「人生三阶,起落有时」是美术误留的污染刻痕层,
  2026-09-15 用户要求整层弃用,不导出(变量仍解包但不用)。

前四个共享同一裁剪框 RECT(像素级同位),运行时摆放见 GearboxProjectorPuzzle.cs 常量。

同时:
  - 手写 .meta(Sprite / PPU100 / 自定义 pivot / 固定 GUID)
  - 出网格核对图 + 离线合成预览(docs/gearbox/)
"""
import json
import os
import shutil
import numpy as np
from PIL import Image, ImageDraw
from psd_tools import PSDImage

PY = r"C:/Users/yuan/lost-goddess-game"
SRC = r"C:/Users/yuan/Desktop/齿轮"
OUT = os.path.join(PY, "Assets/_Project/Resources/Closeups/GearboxPuzzle")
DOCS = os.path.join(PY, "docs/gearbox")
os.makedirs(OUT, exist_ok=True)
os.makedirs(DOCS, exist_ok=True)

# 共享裁剪框(覆盖箱体 1334-2131 / 133-1098 + 管子到 1098 + 边距)
RECT = (1310, 120, 2145, 1108)   # x0,y0,x1,y1
RW, RH = RECT[2] - RECT[0], RECT[3] - RECT[1]

# 关键点(全画布像素)
BOX_X0, BOX_X1 = 1334, 2131
BOX_BOT = 1015.0                 # 箱体木框底边(实测 solidw 685->227 在 y1015-1020)
ROOT_PX = ((BOX_X0 + BOX_X1) / 2.0, BOX_BOT)          # 底座旋转轴心 = 箱底中心
VALVE_C = (1691, 720)
GEAR_S_C = (1550, 522)
GEAR_B_C = (1787, 350)
SHAFT_C = (1781, 704)
# 蒸汽喷口 = 阀件那根小支管的**左开口**(2026-09-15 放大 4 倍网格实测;
# 旧值 (1612,648) 偏高 40px、偏右 29px,导致蒸汽喷在管子上方)
VALVE_SPOUT = (1583, 688)
# 箱右缘凸出的滚花黄铜旋钮:实测金属包围盒 x2050..2132 y545..665,
# 左右各留边 -> 独立裁切框。它原本烤在"木箱底"图层里,不抠出来就没法做推拉交互。
KNOB_RECT = (2042, 532, 2140, 678)     # x0,y0,x1,y1
KNOB_PIVOT_PX = (2042.0, 605.0)        # 裁切框左缘中点:推拉/缩放的锚点(往框里缩)
KNOB_PX = (2092, 605)                  # 旋钮视觉中心(热区中心)

def pivot_norm(pt):
    """全画布像素点 -> RECT 内 Unity 归一化 pivot(y 翻转)"""
    return (round((pt[0] - RECT[0]) / RW, 5), round(1 - (pt[1] - RECT[1]) / RH, 5))

def pivot_in_rect(pt, rect):
    """任意裁切框内的归一化 pivot(y 翻转)。"""
    return (round((pt[0] - rect[0]) / (rect[2] - rect[0]), 5),
            round(1 - (pt[1] - rect[1]) / (rect[3] - rect[1]), 5))

PIVOTS = {
    "gearbox_base":       pivot_norm(ROOT_PX),
    "gearbox_knob":       pivot_in_rect(KNOB_PIVOT_PX, KNOB_RECT),
    "gearbox_valve":      pivot_norm(VALVE_C),
    "gearbox_gear_small": pivot_norm(GEAR_S_C),
    "gearbox_gear_big":   pivot_norm(GEAR_B_C),
    "gearbox_shaft":      pivot_norm(SHAFT_C),
}

# 固定 GUID(手写 meta 用,与场景/代码无直接引用,但保证稳定)
# ⚠ GUID 必须是 **32 位十六进制**(0-9a-f)。写成 k9e7u4r9… 这种带非十六进制字母的,
#   Unity 会判定 "does not have a valid GUID" 并**整个忽略素材** → Resources.Load 返回 null,
#   UI Image 就渲染成纯白方块(2026-09-15 齿轮箱旋钮/蒸汽表就是这么白了的)。
#   这里的值 = 磁盘现状,重跑脚本不会改坏已有引用。
GUIDS = {
    "gearbox_base.png":      "b1a7e4c2d3f54a0198c6e2d1a4f70a01",
    "gearbox_gear_big.png":  "b1a7e4c2d3f54a0198c6e2d1a4f70a04",
    "gearbox_gear_small.png": "b1a7e4c2d3f54a0198c6e2d1a4f70a03",
    "gearbox_knob.png":      "b1a7e4c2d3f54a0198c6e2d1a4f70a08",
    "gearbox_shaft.png":     "b1a7e4c2d3f54a0198c6e2d1a4f70a05",
    "gearbox_valve.png":     "b1a7e4c2d3f54a0198c6e2d1a4f70a02",
    "scar_offset_line.png":  "b1a7e4c2d3f54a0198c6e2d1a4f70a07",
    "steam_sheet.png":       "b1a7e4c2d3f54a0198c6e2d1a4f70a09",
}

META_TPL = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {maxsize}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: {align}
  spritePivot: {{x: {px}, y: {py}}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {maxsize}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Standalone
    maxTextureSize: {maxsize}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

def main():
    import re as _re
    for _k, _v in GUIDS.items():
        assert _re.fullmatch(r"[0-9a-fA-F]{32}", _v), (
            "GUIDS[%s] 不是合法的 32 位十六进制 GUID: %s —— Unity 会忽略该素材!" % (_k, _v))
    psd = PSDImage.open(SRC)
    layers = {}
    def walk(L):
        for c in L:
            if c.is_group():
                walk(c)
            else:
                im = c.topil().convert("RGBA")
                a = np.asarray(im)
                if (a[..., 3] > 4).any():
                    layers[len(layers) + 1] = np.asarray(im).copy()
    walk(psd)
    assert len(layers) == 6, "expect 6 layers, got %d" % len(layers)
    motto, box, valve, gear_s, gear_b, shaft = (layers[i] for i in range(1, 7))
    del motto   # 污染层「人生三阶,起落有时」——弃用,不导出

    # ---- 1) 干净底座:把可动件区域填成箱内暗色 -------------------------------
    box_f = box.astype(np.float32)
    parts = np.zeros(box.shape[:2], np.uint8)
    for lay in (valve, gear_s, gear_b, shaft):
        parts |= (lay[..., 3] > 8).astype(np.uint8)
    # 横向膨胀 5px、纵向 3px(齿轮齿沿横向伸出更多)
    parts_d = parts.copy()
    for dx in range(-5, 6):
        for dy in range(-3, 4):
            if abs(dx) + abs(dy) > 7:
                continue
            parts_d |= np.roll(np.roll(parts, dx, axis=1), dy, axis=0)
    fill_mask = (parts_d > 0) & (box[..., 3] > 128)
    # 填充色:箱内背板暗棕,按部位周围实测中值 + 轻噪声
    rng = np.random.default_rng(7)
    for lay, cpt in ((valve, VALVE_C), (gear_s, GEAR_S_C), (gear_b, GEAR_B_C), (shaft, SHAFT_C)):
        m = (lay[..., 3] > 8).astype(np.uint8)
        md = m.copy()
        for dx in range(-14, 15, 2):
            md |= np.roll(m, dx, axis=1)
        ring = (md > 0) & (m == 0) & (box[..., 3] > 128)
        ys, xs = np.where(ring)
        if len(ys) > 50:
            col = np.median(box[ys, xs, :3], axis=0)
        else:
            col = np.array([34, 27, 20])
        sub = fill_mask & (md > 0)
        ys, xs = np.where(sub)
        noise = rng.normal(0, 5, len(ys))
        for k in range(3):
            box_f[ys, xs, k] = np.clip(col[k] + noise, 5, 90)
        print("fill %-12s ring median %s  px %d" % (cpt, col.astype(int), len(ys)))
    # ---- 1b) 把右缘旋钮从底图里抹掉(它要变成可推拉的独立层)------------------
    #   只抹裁切框右缘以右的部分,保住木框自己的轮廓线;填充色取环带中值。
    kx0, ky0, kx1, ky1 = KNOB_RECT
    fill_x0 = 2050                      # 木框外轮廓线大约在 2044-2050,别抹掉它
    m = np.zeros(box.shape[:2], bool)
    m[ky0:ky1, fill_x0:kx1] = True
    ring = np.zeros_like(m)
    ring[ky0 - 14:ky0, fill_x0 - 6:kx1 + 6] = True
    ring[ky1:ky1 + 14, fill_x0 - 6:kx1 + 6] = True
    ring = ring & (box[..., 3] > 128) & (~m)
    ys, xs = np.where(ring)
    kcol = np.median(box[ys, xs, :3], axis=0) if len(ys) > 50 else np.array([96, 66, 38])
    # 填充:不要用平色(推拉时露出的那块会像"补丁")。改成把左侧木框的**竖向木纹**
    # 横向平铺过来 —— 木框这条横梁的木纹本来就是竖的,平铺后接缝看不出来。
    src_x0, src_w = fill_x0 - 56, 56
    strip = box[ky0:ky1, src_x0:src_x0 + src_w, :3].astype(np.float32)
    for i in range(ky1 - ky0):
        row = strip[i]
        tiled = np.concatenate([row] * (int(np.ceil((kx1 - fill_x0) / src_w)) + 1), axis=0)[:kx1 - fill_x0]
        box_f[ky0 + i, fill_x0:kx1, :3] = tiled
    ys, xs = np.where(m & (box[..., 3] > 128))
    kn = np.random.default_rng(11).normal(0, 3, len(ys))
    for c in range(3):
        box_f[ys, xs, c] = np.clip(box_f[ys, xs, c] + kn, 5, 235)
    print("knob fill 木纹平铺 x%d..%d  行 %d  (环带中值 %s)" % (fill_x0, kx1, ky1 - ky0, kcol.astype(int)))

    box_clean = box_f.astype(np.uint8)
    box_clean[..., 3] = box[..., 3]
    box_im = Image.fromarray(box_clean, "RGBA").crop(RECT)

    # ---- 2) 共享 RECT 导出 --------------------------------------------------
    exports = {
        "gearbox_base": box_im,
        "gearbox_valve": Image.fromarray(valve, "RGBA").crop(RECT),
        "gearbox_gear_small": Image.fromarray(gear_s, "RGBA").crop(RECT),
        "gearbox_gear_big": Image.fromarray(gear_b, "RGBA").crop(RECT),
        "gearbox_shaft": Image.fromarray(shaft, "RGBA").crop(RECT),
    }
    motto_bbox = None   # 污染层已弃用
    exports.pop("gearbox_motto", None)

    # ---- 3) 刻痕:黑底 -> alpha(max 通道),叠加显示 ---------------------------
    scar_src = Image.open(os.path.join(PY, "docs/gearbox/../../..",
                                       "Desktop_scar_placeholder.png")) \
        if False else Image.open(r"C:/Users/yuan/Desktop/差之一线.png").convert("RGB")
    s = np.asarray(scar_src).astype(np.float32)
    alpha = np.clip(s.max(axis=2) * 1.35, 0, 255).astype(np.uint8)   # 黑底 -> alpha
    # ⚠ 源图文字 RGB 只有 (74,21,20) —— 直接叠到压暗后的房间(≈10,10,12)上
    #   几乎看不见(2026-09-15 实测)。按最大通道归一到 235,保住色相变成"血色发光",
    #   刻痕才真的像"烙"在画面上。
    mx = s.max(axis=2, keepdims=True)
    gain = np.where(mx > 1e-3, 235.0 / np.maximum(mx, 1e-3), 1.0)
    gain = np.clip(gain, 1.0, 12.0)
    s = np.clip(s * gain, 0, 255)
    scar = np.dstack([s.astype(np.uint8), alpha])
    exports["scar_offset_line"] = Image.fromarray(scar, "RGBA")

    # ---- 3b) 右缘旋钮独立层 -------------------------------------------------
    comp_rgba = psd.composite().convert("RGBA")
    exports["gearbox_knob"] = comp_rgba.crop(KNOB_RECT)

    # ---- 3c) 蒸汽帧表(UI 特写用)------------------------------------------
    #  真实特写是 ScreenSpaceOverlay,世界空间的 SpriteRenderer 会被 70% 黑底压住
    #  看不见 → 烟雾必须变成 UI Image 逐帧换 sprite。但 52 张 smoke_*.png 在
    #  Art/ 下,不在 Resources 里,Resources.Load 拿不到 → 打一张帧表出来。
    #  0.6 缩放到 288x122,13 列 x 4 行 = 3744x488(塞得进 4096 上限)。
    smoke_dir = os.path.join(PY, "Assets/_Project/Art/AIAnimations/SmokeSpray")
    frames = sorted(f for f in os.listdir(smoke_dir) if f.endswith(".png"))
    if frames:
        fw, fh = 288, 122
        cols, rows = 13, (len(frames) + 12) // 13
        sheet = Image.new("RGBA", (fw * cols, fh * rows), (0, 0, 0, 0))
        for i, fn in enumerate(frames):
            fr = Image.open(os.path.join(smoke_dir, fn)).convert("RGBA").resize((fw, fh), Image.LANCZOS)
            sheet.alpha_composite(fr, ((i % cols) * fw, (i // cols) * fh))
        sp = os.path.join(OUT, "steam_sheet.png")
        sheet.save(sp)
        meta = META_TPL.format(guid="t5m1s7e3a9n22x7741z8q4w6r5y37u06", maxsize=4096,
                               align=0, px=0.5, py=0.5)
        with open(sp + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(meta)
        print("saved steam_sheet", sheet.size, "frames", len(frames), "grid %dx%d" % (cols, rows))

    for name, im in exports.items():
        p = os.path.join(OUT, name + ".png")
        im.save(p)
        maxsize = 4096 if max(im.size) > 2048 else 2048
        pv = PIVOTS.get(name, (0.5, 0.5))
        # ⚠ SpriteAlignment 枚举:Center=0, Custom=9。自定义 pivot 必须 9,
        #   否则 Unity 无视 spritePivot 按中心摆(2026-09-15 踩坑)。
        meta = META_TPL.format(guid=GUIDS[name], maxsize=maxsize,
                               align=9 if name in PIVOTS else 0, px=pv[0], py=pv[1])
        with open(os.path.join(OUT, name + ".png.meta"), "w", encoding="utf-8", newline="\n") as f:
            f.write(meta)
        print("saved", name, im.size, "pivot", pv)

    # ---- 4) 网格核对图 ------------------------------------------------------
    comp = psd.composite().convert("RGBA")
    g = comp.copy()
    d = ImageDraw.Draw(g)
    for x in range(0, g.width, 100):
        d.line([(x, 0), (x, g.height)], fill=(0, 200, 255, 90), width=1)
        d.text((x + 3, 4), str(x), fill=(0, 255, 255, 255))
    for y in range(0, g.height, 100):
        d.line([(0, y), (g.width, y)], fill=(0, 200, 255, 90), width=1)
        d.text((4, y + 3), str(y), fill=(0, 255, 255, 255))
    for name, pt, col in (("ROOT", ROOT_PX, (255, 80, 80)), ("VALVE", VALVE_C, (80, 255, 80)),
                          ("GEAR_S", GEAR_S_C, (255, 255, 80)), ("GEAR_B", GEAR_B_C, (255, 160, 80)),
                          ("SHAFT", SHAFT_C, (80, 160, 255)), ("SPOUT", VALVE_SPOUT, (255, 80, 255)),
                          ("KNOB", KNOB_PX, (80, 255, 255))):
        x, y = pt
        d.ellipse([x - 9, y - 9, x + 9, y + 9], outline=col, width=3)
        d.text((x + 12, y - 6), name, fill=col)
    g.save(os.path.join(DOCS, "measure_grid.png"))

    # ---- 5) 离线合成预览(复刻运行时摆放数学,1920x1080) ----------------------
    # root(=ROOT_PX) 投到屏幕 (960, 960);图像点(x,y) -> (960+x-1732.5, 960-(1015-y))
    RX, RY = 960, 960
    canvas = Image.new("RGBA", (1920, 1080), (12, 11, 13, 255))
    def paste_layer(im, pt_img, angle=0.0):
        pv = PIVOTS.get(im_name[0], (0.5, 0.5))
        w, h = im.size
        pv_off = (pv[0] * w, (1 - pv[1]) * h)
        if angle:
            im = im.rotate(angle, resample=Image.BICUBIC, center=pv_off)
        sx = RX + (pt_img[0] - ROOT_PX[0])
        sy = RY - (ROOT_PX[1] - pt_img[1])
        canvas.alpha_composite(im, (int(sx - pv_off[0]), int(sy - pv_off[1])))
    # 底座 + 各件(先 0° 静止)
    for im_name in [("gearbox_base", ROOT_PX, 0), ("gearbox_valve", VALVE_C, 0),
                    ("gearbox_gear_small", GEAR_S_C, 0), ("gearbox_gear_big", GEAR_B_C, 0),
                    ("gearbox_shaft", SHAFT_C, 0)]:
        paste_layer(exports[im_name[0]], im_name[1], im_name[2])
    canvas.save(os.path.join(DOCS, "preview_rest.png"))

    # 15° 上倾 + 齿轮联动(离线示意:整体绕 ROOT 旋转 15°,齿轮各自再转)
    canvas2 = Image.new("RGBA", (1920, 1080), (12, 11, 13, 255))
    stack = Image.new("RGBA", (3400, 1200), (0, 0, 0, 0))
    def stack_layer(im, pt_img, angle=0.0):
        pv = PIVOTS.get(im_name[0], (0.5, 0.5))
        w, h = im.size
        pv_off = (pv[0] * w, (1 - pv[1]) * h)
        if angle:
            im = im.rotate(angle, resample=Image.BICUBIC, center=pv_off)
        sx = int(pt_img[0] - pv_off[0])
        sy = int(pt_img[1] - pv_off[1])
        stack.alpha_composite(im, (sx, sy))
    for im_name in [("gearbox_base", ROOT_PX, 0), ("gearbox_valve", VALVE_C, 0),
                    ("gearbox_gear_small", GEAR_S_C, -40), ("gearbox_gear_big", GEAR_B_C, 28),
                    ("gearbox_shaft", SHAFT_C, 0)]:
        stack_layer(exports[im_name[0]], im_name[1], im_name[2])
    stack = stack.rotate(15, resample=Image.BICUBIC, center=ROOT_PX)
    canvas2.alpha_composite(stack, (int(RX - ROOT_PX[0]), int(RY - ROOT_PX[1])))
    canvas2.save(os.path.join(DOCS, "preview_tilt15.png"))

    # ---- 6) measure json ----------------------------------------------------
    meas = {
        "rect": RECT, "rect_size": [RW, RH],
        "root_px": ROOT_PX, "valve": VALVE_C, "gear_small": GEAR_S_C,
        "gear_big": GEAR_B_C, "shaft": SHAFT_C, "valve_spout": VALVE_SPOUT,
        "knob": KNOB_PX,
        "knob_rect": KNOB_RECT, "knob_pivot_px": KNOB_PIVOT_PX,
        "pivots": PIVOTS, "guids": GUIDS,
    }
    with open(os.path.join(DOCS, "gearbox_measure.json"), "w", encoding="utf-8") as f:
        json.dump(meas, f, ensure_ascii=False, indent=2)
    print("done")

if __name__ == "__main__":
    main()
