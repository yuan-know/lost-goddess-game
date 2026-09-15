# -*- coding: utf-8 -*-
"""gearbox_measure_frames.py —— 量序列帧与静态五层合成图的对齐参数(2026-09-15 三修)。

背景:序列帧不是"内部机构层",而是**整块齿轮箱面板**(木框+全部齿轮+右缘旋钮),
所以它必须和静态五层合成图(gearbox_base/gear_big/gear_small/shaft/valve)的
**alpha 并集**逐边重合。

★★ 画质事故复盘(必读,两次踩坑):
  ① 一修把**整帧 1470×630 缩到 340×146** —— 齿轮箱从 441×508 缩到 100×117,
     Unity 再放大到屏幕 526×609 → **净放大 5.2 倍 → 糊**。用户原话
     "明显和齿轮箱图层不是一个量级的精细度"。
  ② 二修改成"帧图只有 340×146"的坐标系 —— 尺寸对了但**素材还是缩过的**,画质没救。

  ★ 三修正解:**只裁齿轮箱区域(441×508),不缩放**,原生像素打进帧表。
    素材 441→屏幕 529 = 只放大 **1.20 倍**,和静态图层(813→518,是缩小)同量级。
    161 帧装不进一张 4096 图集 → 拆 **4 张**(每张 41 帧,9 列×5 行 = 3969×2540)。

输出 docs/gearbox/gearbox_spin_align.json
复核:tools/gearbox_screen_sim.py(屏幕像素级复刻)→ docs/gearbox/_screen_*.png
"""
import json
import os
import numpy as np
from PIL import Image

ROOT = r"C:/Users/yuan/lost-goddess-game"
D = os.path.join(ROOT, "Assets/_Project/Resources/Closeups/GearboxPuzzle")
DOCS = os.path.join(ROOT, "docs/gearbox")

# 835 包在画布上的位置:gearbox_base 的 pivot (0.50599, 0.09413) 落在画布 (1732.5, 1015)
PACK_ORIGIN_CANVAS = (1310.0, 120.0)

CANVAS_CX, CANVAS_CY = 1700.0, 600.0
K = 0.635


def alpha_bbox(im, thr=200):
    a = np.asarray(im.convert("RGBA")).astype(np.int16)
    ys, xs = np.where(a[..., 3] > thr)
    if len(xs) == 0:
        return None
    return [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]


def main():
    meas = json.load(open(os.path.join(DOCS, "gearbox_spin.json"), encoding="utf-8"))
    FW, FH = meas["frame_w"], meas["frame_h"]
    COLS, PER_SHEET, N = meas["cols"], meas["per_sheet"], meas["frame_count"]
    cx0, cy0 = meas["crop_box"][0], meas["crop_box"][1]
    print("帧表: %d 张 x (每张 %d 帧, %d 列), 单格 %dx%d, 裁切框 (%d,%d)" % (
        meas["n_sheets"], PER_SHEET, COLS, FW, FH, cx0, cy0))

    # ① 静态五层合成图(alpha 并集)
    st = Image.new("RGBA", (835, 988), (0, 0, 0, 0))
    for n in ["gearbox_base", "gearbox_gear_big", "gearbox_gear_small",
              "gearbox_shaft", "gearbox_valve"]:
        st.alpha_composite(Image.open(os.path.join(D, n + ".png")).convert("RGBA"))
    bb_st = alpha_bbox(st)
    bb_st_canvas = [PACK_ORIGIN_CANVAS[0] + bb_st[0], PACK_ORIGIN_CANVAS[1] + bb_st[1],
                    PACK_ORIGIN_CANVAS[0] + bb_st[2], PACK_ORIGIN_CANVAS[1] + bb_st[3]]
    print("静态合成 alpha bbox 画布 x %.0f..%.0f y %.0f..%.0f (%dx%d)" % (
        bb_st_canvas[0], bb_st_canvas[2], bb_st_canvas[1], bb_st_canvas[3],
        bb_st_canvas[2] - bb_st_canvas[0], bb_st_canvas[3] - bb_st_canvas[1]))

    # ② 序列帧 union bbox(★ 从**新图集**量,只取有效前缀帧)
    u = np.zeros((FH, FW), bool)
    idx = 0
    for s in range(meas["n_sheets"]):
        path = os.path.join(D, meas["sheets"][s])
        sheet = Image.open(path).convert("RGBA")
        a = np.asarray(sheet).astype(np.int16)
        for i in range(PER_SHEET):
            if idx >= N:
                break
            c, r = i % COLS, i // COLS
            cell = a[r * FH:(r + 1) * FH, c * FW:(c + 1) * FW]
            if cell.shape[0] != FH or cell.shape[1] != FW:
                break
            u |= (cell[..., 3] > 200)
            idx += 1
    ys2, xs2 = np.where(u)
    bb_fr = [int(xs2.min()), int(ys2.min()), int(xs2.max()) + 1, int(ys2.max()) + 1]
    print("帧 union(帧内)      x %d..%d y %d..%d (%dx%d)" % (
        bb_fr[0], bb_fr[2], bb_fr[1], bb_fr[3], bb_fr[2] - bb_fr[0], bb_fr[3] - bb_fr[1]))

    # ③ 统一缩放
    sx = (bb_st_canvas[2] - bb_st_canvas[0]) / (bb_fr[2] - bb_fr[0])
    sy = (bb_st_canvas[3] - bb_st_canvas[1]) / (bb_fr[3] - bb_fr[1])
    S = (sx + sy) / 2.0
    print("缩放 S: x %.6f  y %.6f  取 %.6f" % (sx, sy, S))

    # ④ 帧图左上角/中心落画布何处
    ox = bb_st_canvas[0] - bb_fr[0] * S
    oy = bb_st_canvas[1] - bb_fr[1] * S
    ccx = ox + FW * S / 2.0
    ccy = oy + FH * S / 2.0
    anchored = ((ccx - CANVAS_CX) * K, (CANVAS_CY - ccy) * K)
    size_delta = (FW * S * K, FH * S * K)
    print("帧图中心 → 画布 (%.2f, %.2f)" % (ccx, ccy))
    print("容器 anchoredPosition = (%.3f, %.3f)" % anchored)
    print("容器 sizeDelta        = (%.0f, %.0f)" % size_delta)
    print("★ 素材 %d → 屏幕 %.0f = 放大 %.2f 倍" % (FW, size_delta[0], size_delta[0] / FW))

    out = {
        "_note": "序列帧对齐(2026-09-15 三修)。★ 只裁齿轮箱 441×508,不缩放,原生分辨率。",
        "static_composite": {
            "alpha_bbox_in_pack": bb_st,
            "pack_origin_canvas": list(PACK_ORIGIN_CANVAS),
            "alpha_bbox_canvas": bb_st_canvas,
        },
        "spin_frame": {
            "frame_size": [FW, FH], "cols": COLS, "per_sheet": PER_SHEET,
            "n_sheets": meas["n_sheets"], "frame_count": N,
            "crop_box": meas["crop_box"],
            "alpha_bbox_union": bb_fr,
        },
        "fit": {
            "scale_x": sx, "scale_y": sy, "scale_unified": S,
            "frame_origin_canvas": [ox, oy],
            "frame_center_canvas": [ccx, ccy],
            "container_anchored_pos": list(anchored),
            "size_delta_container_px": list(size_delta),
            "material_to_screen_upscale": size_delta[0] / FW,
        },
    }
    os.makedirs(DOCS, exist_ok=True)
    with open(os.path.join(DOCS, "gearbox_spin_align.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
    print("saved gearbox_spin_align.json")


if __name__ == "__main__":
    main()
