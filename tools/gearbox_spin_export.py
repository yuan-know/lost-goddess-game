# -*- coding: utf-8 -*-
"""
gearbox_spin_export.py —— 齿轮箱「内部旋转」序列帧去污 + 导出(2026-09-15 v3)

★ 本版变更(用户 2026-09-15 18:4x 新素材 + 新要求):
  源换成 `C:/Users/yuan/Downloads/video-frames-20260915-184323.zip`
  (161 张 action_001.png … action_161.png,每张 1470×630 RGBA;齿轮箱占 572×617)
  用户原话:"我重新做了一版齿轮箱转动的序列帧图…同样先把边缘蓝色像素块腐蚀干净
           然后接入进去,这次直接使用这套序列帧图的**初始帧**作为齿轮箱的常驻图,
           这样就不会出现动画播放前后的形变了"

  → **不再用 5 张静态图层拼装**(那套五层是旧美术,和新序列帧形状对不上,才会形变);
    改成 **首帧(action_001)当常驻图**,动画播完停末帧也不形变(帧本身几何稳定)。

★ 承接上一版的两条硬经验:
  ① 去污:判色 + 从"外部透明域"测地线生长(见 pollution_mask)。
  ② 画质:**只裁齿轮箱区域,不缩放** —— 素材→屏幕放大 <1.3 倍才不糊。
     161 帧装不进一张 4096 图集 → 拆多张(每张 PER_SHEET 帧)。
  ③ GUID **必须 32 位十六进制**,否则 Unity 静默忽略整张图集(白块)。

产出(Assets/_Project/Resources/Closeups/GearboxPuzzle/):
  gearspin_sheet{,_2,_3,_4}.png  旋转帧表(Multiple sprite)
  gearbox_static.png             首帧(常驻图,Single sprite)
  docs/gearbox/gearbox_spin.json 参数给 C# 读

用法: python tools/gearbox_spin_export.py
"""
import os
import glob
import json
import shutil
import zipfile
import numpy as np
from PIL import Image
from scipy import ndimage

PY = r"C:/Users/yuan/lost-goddess-game"
ZIP = r"C:/Users/yuan/Downloads/video-frames-20260915-184323.zip"
WORK = os.path.join(os.environ.get("TEMP", "/tmp"), "gbframes2")
OUT = os.path.join(PY, "Assets/_Project/Resources/Closeups/GearboxPuzzle")
DOCS = os.path.join(PY, "docs/gearbox")
os.makedirs(OUT, exist_ok=True)
os.makedirs(DOCS, exist_ok=True)

COLS = 9          # 占位,实际由 fit_grid() 按 4096 上限 + 帧尺寸自动定
PER_SHEET = 41
N_FRAMES = 161
MAX_TEX = 4096


def fit_grid(fw, fh, per_sheet):
    """在 MAX_TEX 单边上限内给 per_sheet 帧挑最紧凑的 (cols, rows)。
    返回 (cols, rows) —— 保证 cols*fw <= MAX_TEX 且 rows*fh <= MAX_TEX。"""
    best = None
    for cols in range(1, per_sheet + 1):
        rows = (per_sheet + cols - 1) // cols
        w, h = cols * fw, rows * fh
        if w <= MAX_TEX and h <= MAX_TEX:
            area = w * h
            if best is None or area < best[2]:
                best = (cols, rows, area)
    assert best is not None, "单帧 %dx%d 太大,放不进 %d 图集" % (fw, fh, MAX_TEX)
    return best[0], best[1]

# 固定 GUID(**必须 32 位十六进制!**)
# ⚠ 踩坑史:曾写 30 位 → Unity 报 "cannot be extracted by the YAML Parser" → 白块。
GUID_PREFIX = "c7e2a5b18d340f6a92e1c4b7d0a63f"
GUID_SUFFIX = {0: "25", 1: "26", 2: "27", 3: "28", 4: "29", 5: "30"}
GUID_STATIC = "d4f81a6c2b930e57a1f8c4d6b2e90a13"   # 常驻图(首帧)


def guid_for(i):
    g = GUID_PREFIX + GUID_SUFFIX[i]
    assert len(g) == 32, "GUID 必须 32 位,当前 %d: %s" % (len(g), g)
    return g


def unzip_frames():
    if os.path.isdir(WORK) and len(glob.glob(os.path.join(WORK, "*.png"))) >= N_FRAMES:
        return sorted(glob.glob(os.path.join(WORK, "*.png")))
    os.makedirs(WORK, exist_ok=True)
    with zipfile.ZipFile(ZIP) as z:
        z.extractall(WORK)
    fs = sorted(glob.glob(os.path.join(WORK, "**", "*.png"), recursive=True))
    if len(fs) > 0 and os.path.dirname(fs[0]) != WORK:
        for f in fs:
            shutil.copy2(f, os.path.join(WORK, os.path.basename(f)))
        fs = sorted(glob.glob(os.path.join(WORK, "*.png")))
    return fs


def pollution_mask(arr):
    """污染像素 mask:判色 + 从"外部透明域"测地线生长 —— 只吃从画布外伸进来的青蓝块。
    实测该批素材污染色 ≈ (30..45, 57..65, 72..90)(深青蓝,b 比 r 高 20+),alpha=255。"""
    rgb = arr[..., :3].astype(np.int16)
    a = arr[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    cand = (b > r + 18) & (b > g + 4) & (r < 110) & (b < 150) & (a > 10)
    if not cand.any():
        return cand
    trans = a <= 10
    lab, _ = ndimage.label(trans, np.ones((3, 3), int))
    ids = set(np.unique(np.concatenate([
        lab[0, :], lab[-1, :], lab[:, 0], lab[:, -1]])))
    ids.discard(0)
    if not ids:
        return cand
    outside = np.isin(lab, list(ids))
    seed = ndimage.binary_dilation(outside, np.ones((5, 5), bool)) & cand
    if not seed.any():
        return np.zeros_like(cand)
    cur = seed.copy()
    for _ in range(200):
        nxt = ndimage.binary_dilation(cur, np.ones((3, 3), bool)) & cand
        if int(nxt.sum()) == int(cur.sum()):
            break
        cur = nxt
    return cur


def clean_one(path, report):
    im = Image.open(path).convert("RGBA")
    arr = np.array(im)
    m = pollution_mask(arr)
    report["blue_px"].append(int(m.sum()))
    if not m.any():
        report["killed"].append(0)
        return im
    m_out = ndimage.binary_dilation(m, np.ones((3, 3), bool))
    rgb = arr[..., :3].astype(np.int16)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    halo = m_out & (~m) & (arr[..., 3] > 10) & ((b > r + 6) | ((r + g + b) < 130))
    kill = m | halo
    arr = arr.copy()
    arr[kill, 3] = 0
    arr[kill, :3] = 0
    report["killed"].append(int(kill.sum()))
    return Image.fromarray(arr, "RGBA")


# ── meta 写手 ────────────────────────────────────────────────────────────
META_HEAD = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
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
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: {sprite_mode}
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
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
    maxTextureSize: 4096
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
"""


def write_single_meta(png_path, guid):
    """Single sprite meta(常驻图用)。"""
    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META_HEAD.format(guid=guid, sprite_mode=1))
        f.write("  spriteSheet:\n")
        f.write("    sprites: []\n")
        f.write("    outline: []\n")
        f.write("    customData: \n")
        f.write("    physicsShape: []\n")
        f.write("    bones: []\n")
        f.write("    internalID: 0\n")
        f.write("    spriteID: \n")
        f.write("  userData: \n")
        f.write("  assetBundleName: \n")
        f.write("  assetBundleVariant: \n")


def write_multiple_meta(png_path, guid, cw, ch, cols, rows, names):
    """Multiple sprite meta(帧表用)。⚠ 必须逐格列出,否则 Unity 只认整张。"""
    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META_HEAD.format(guid=guid, sprite_mode=2))
        f.write("  spriteSheet:\n")
        f.write("    sprites: []\n")
        for i, nm in enumerate(names):
            c, r = i % cols, i // cols
            y = (rows - r - 1) * ch           # Unity rect y 从图左下角起算
            f.write("    - serializedVersion: 2\n")
            f.write("      name: %s\n" % nm)
            f.write("      rect:\n")
            f.write("        serializedVersion: 2\n")
            f.write("        x: %d\n" % (c * cw))
            f.write("        y: %d\n" % y)
            f.write("        width: %d\n" % cw)
            f.write("        height: %d\n" % ch)
            f.write("      alignment: 0\n")
            f.write("      pivot: {x: 0.5, y: 0.5}\n")
            f.write("      border: {x: 0, y: 0, z: 0, w: 0}\n")
            f.write("      customData: \n")
            f.write("      outline: []\n")
            f.write("      physicsShape: []\n")
            f.write("      tessellationOrder: 0\n")
            f.write("      bones: []\n")
            f.write("      spriteID: \n")
            f.write("      internalID: 0\n")
            f.write("      spriteBone: []\n")
            f.write("      spriteID: \n")
            f.write("      internalID: 0\n")
        f.write("    outline: []\n")
        f.write("    customData: \n")
        f.write("    physicsShape: []\n")
        f.write("    bones: []\n")
        f.write("    internalID: 0\n")
        f.write("    spriteID: \n")
        f.write("  userData: \n")
        f.write("  assetBundleName: \n")
        f.write("  assetBundleVariant: \n")


def main():
    fs = unzip_frames()
    assert len(fs) >= 100, "frame count = %d" % len(fs)
    n = min(len(fs), N_FRAMES)
    print("frames:", len(fs), "size", Image.open(fs[0]).size)

    report = {"blue_px": [], "killed": []}
    cleaned = [clean_one(fs[i], report) for i in range(n)]
    print("blue px  min/med/max  %d / %d / %d" % (
        int(np.min(report["blue_px"])), int(np.median(report["blue_px"])),
        int(np.max(report["blue_px"]))))
    print("killed   min/med/max  %d / %d / %d" % (
        int(np.min(report["killed"])), int(np.median(report["killed"])),
        int(np.max(report["killed"]))))

    # 并集 bbox(去污后)→ 定裁切框
    ux0, uy0, ux1, uy1 = 10 ** 9, 10 ** 9, -1, -1
    for im in cleaned:
        bb = im.split()[3].getbbox()
        if bb:
            ux0 = min(ux0, bb[0]); uy0 = min(uy0, bb[1])
            ux1 = max(ux1, bb[2]); uy1 = max(uy1, bb[3])
    print("union alpha bbox", (ux0, uy0, ux1, uy1), "=", ux1 - ux0, "x", uy1 - uy0)

    # ★ 裁切框:并集 bbox 外扩 2px,再取整成偶数(避免半像素)
    cx0, cy0 = max(0, ux0 - 2), max(0, uy0 - 2)
    FW = (ux1 - cx0) + 2
    FH = (uy1 - cy0) + 2
    print("crop box: (%d,%d) %dx%d" % (cx0, cy0, FW, FH))

    # ── ① 常驻图 = 首帧(action_001)裁切后原样 ─────────────────────────
    static_im = cleaned[0].crop((cx0, cy0, cx0 + FW, cy0 + FH))
    sp = os.path.join(OUT, "gearbox_static.png")
    static_im.save(sp)
    write_single_meta(sp, GUID_STATIC)
    print("saved gearbox_static.png (首帧, %dx%d)" % (FW, FH))

    # ── ② 旋转帧表:多张 ──────────────────────────────────────────────
    cols, rows = fit_grid(FW, FH, PER_SHEET)
    print("grid fit: %d 列 × %d 行 = %dx%d (≤%d ✓)" % (
        cols, rows, cols * FW, rows * FH, MAX_TEX))
    n_sheets = (n + PER_SHEET - 1) // PER_SHEET
    sheet_names = ["gearspin_sheet"] + ["gearspin_sheet_%d" % (k + 1) for k in range(1, n_sheets)]
    for si in range(n_sheets):
        chunk = cleaned[si * PER_SHEET:(si + 1) * PER_SHEET]
        sheet = Image.new("RGBA", (FW * cols, FH * rows), (0, 0, 0, 0))
        names = []
        for j, im in enumerate(chunk):
            fr = im.crop((cx0, cy0, cx0 + FW, cy0 + FH))   # ★ 只裁,不缩放
            sheet.alpha_composite(fr, ((j % cols) * FW, (j // cols) * FH))
            names.append("gearspin_%03d" % (si * PER_SHEET + j))
        sp = os.path.join(OUT, sheet_names[si] + ".png")
        sheet.save(sp)
        write_multiple_meta(sp, guid_for(si), FW, FH, cols, rows, names)
        print("saved %s %s grid %dx%d frames %d guid %s" % (
            sheet_names[si], sheet.size, cols, rows, len(chunk), guid_for(si)))

    meas = {
        "src_zip": os.path.basename(ZIP),
        "sheets": [s + ".png" for s in sheet_names],
        "static": "gearbox_static.png",
        "static_guid": GUID_STATIC,
        "sheet_size": [FW * cols, FH * rows],
        "frame_w": FW, "frame_h": FH,
        "cols": cols, "rows": rows,
        "per_sheet": PER_SHEET, "n_sheets": n_sheets,
        "frame_count": n,
        "src_size": [1470, 630],
        "crop_box": [cx0, cy0, FW, FH],
        "union_alpha_bbox": [ux0, uy0, ux1, uy1],
        "blue_px_median": int(np.median(report["blue_px"])),
        "guids": [guid_for(k) for k in range(n_sheets)],
        "grid_cells": cols * rows,
        "blank_tail": cols * rows - PER_SHEET,
    }
    with open(os.path.join(DOCS, "gearbox_spin.json"), "w", encoding="utf-8") as f:
        json.dump(meas, f, ensure_ascii=False, indent=2)
    print("done")


if __name__ == "__main__":
    main()
