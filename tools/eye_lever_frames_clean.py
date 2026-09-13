# -*- coding: utf-8 -*-
"""
eye_lever_frames_clean.py
=========================
把「最右侧拉杆旋转」的 AI 视频序列帧（1470x630 RGBA）处理成眼睛控制台
特写的「最底层控制台图层」序列帧。

做三件事：
  1. 腐蚀掉控制台轮廓边缘的蓝色污染
     （AI 视频生成的色度溢出：轮廓最外 1~2px 的 B-R 明显偏正，青色）。
  2. 按仿射矩阵把每帧对齐到 3400x1200 源图画布，使控制台与旧
     ec_bg.png 的位置、大小完全一致 —— 两个旋钮的单独图层一个都不用挪。
  3. 按统一 bbox 裁切（保证所有帧尺寸一致）、落盘到 Resources。

对齐矩阵怎么来
--------------
    直接量「底板矩形」：帧图上底板 bbox 与 ec_bg 上底板 bbox 的
    左上/右下角一一对应，解出 sx / sy / ox / oy。
    测 bbox 时避开拉杆所在列，否则会被转动的手柄带偏。
    这样得到的是「等比消除 AI 视频的非等比拉伸」的解 —— 实测帧图底板
    长宽比 2.790、ec_bg 2.919，视频把画面纵向拉长了约 4.6%，必须压回去。

用法:
  python tools/eye_lever_frames_clean.py \
      --src <视频抽帧目录> --bg <ec_bg.png> --out <Resources/.../eye_console_layered>
"""
import argparse
import glob
import json
import os

import cv2
import numpy as np
from scipy import ndimage

# ── 源画布（与 EyeConsoleCloseup.cs 一致）───────────────────────────
CANVAS_W, CANVAS_H = 3400, 1200

# 帧目录里参与动画的单调区间：0 = 拉杆静止位(与 ec_bg 一致)，20 = 拉满
FRAME_FIRST, FRAME_LAST = 0, 20

# 蓝色污染判定：B - R 超过该值、且距离轮廓外边界不超过 EDGE_BAND 像素
BLUE_BR_MIN = 6
EDGE_BAND = 4
# 轮廓腐蚀像素数（把最外圈污染整条切掉）
ERODE_PX = 2

# 量底板矩形时避开的拉杆列（帧坐标）
LEVER_COL = (925, 1085)
# ec_bg 上对应的拉杆列（画布坐标）
LEVER_COL_BG = (1930, 2130)


# ─────────────────────────────────────────────────────────────────────
def plate_box(alpha, col_band, row_band):
    """量底板矩形：竖直范围用 col_band 内的列，水平范围用 row_band 内的行。"""
    m = alpha > 128
    ys = np.where(m[:, col_band[0]:col_band[1]].any(axis=1))[0]
    xs = np.where(m[row_band[0]:row_band[1], :].any(axis=0))[0]
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def estimate_transform(bg_path, frame_path):
    bg = cv2.imread(bg_path, cv2.IMREAD_UNCHANGED)
    fr = cv2.imread(frame_path, cv2.IMREAD_UNCHANGED)
    # 帧图上底板落在 x≈325..1042 / y≈269..526；避开拉杆列取竖直范围
    fb = plate_box(fr[:, :, 3], (340, 900), (400, 470))
    # ec_bg 上底板落在 x≈1214..2189 / y≈658..992
    bb = plate_box(bg[:, :, 3], (1280, 1900), (800, 900))
    sx = (bb[2] - bb[0]) / (fb[2] - fb[0])
    sy = (bb[3] - bb[1]) / (fb[3] - fb[1])
    ox = bb[0] - fb[0] * sx
    oy = bb[1] - fb[1] * sy
    M = np.array([[sx, 0, ox], [0, sy, oy], [0, 0, 1]], np.float64)
    print(f"[align] 帧底板 {fb}   ec_bg 底板 {bb}")
    print(f"[align] sx={sx:.5f} sy={sy:.5f} offset=({ox:.2f},{oy:.2f})")
    return M


# ─────────────────────────────────────────────────────────────────────
def clean_frame(im, log_prefix=""):
    """腐蚀掉轮廓边缘的蓝色污染，返回清理后的 RGBA。"""
    a = im[:, :, 3]
    b = im[:, :, 0].astype(np.int16)
    r = im[:, :, 2].astype(np.int16)

    solid = a > 8
    # 到透明区的距离：最外圈 = 1
    dist = ndimage.distance_transform_edt(solid)
    polluted = solid & ((b - r) > BLUE_BR_MIN) & (dist <= EDGE_BAND)
    n_pol = int(polluted.sum())

    out = im.copy()
    if n_pol:
        # 只从「干净且不透明」的像素取样，避免把外部的黑底拉进来
        source = solid & ~polluted
        idx = ndimage.distance_transform_edt(
            ~source, return_distances=False, return_indices=True)
        sel = polluted
        out[sel, :3] = im[idx[0][sel], idx[1][sel], :3]

    # 轮廓腐蚀：最外 1~2px 就是污染所在的那一圈，整条切掉
    kern = np.ones((3, 3), np.uint8)
    out[:, :, 3] = cv2.erode((a > 128).astype(np.uint8), kern,
                             iterations=ERODE_PX) * 255
    print(f"{log_prefix}污染像素={n_pol:5d}   腐蚀 {ERODE_PX}px → "
          f"alpha={int((out[:, :, 3] > 0).sum())}")
    return out


# ─────────────────────────────────────────────────────────────────────
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True, help="AI 序列帧目录 (action_001..png)")
    ap.add_argument("--bg", required=True, help="参考底图 ec_bg.png")
    ap.add_argument("--out", required=True, help="输出目录（Resources 下）")
    ap.add_argument("--prefix", default="ec_base")
    ap.add_argument("--preview", default="", help="预览目录（可选，合成灰底 jpg）")
    ap.add_argument("--rect", default="", help="强制裁切框 x0,y0,x1,y1（可选）")
    args = ap.parse_args()

    files = sorted(glob.glob(os.path.join(args.src, "action_*.png")))
    if not files:
        raise SystemExit("没找到 action_*.png")
    print(f"[src] {len(files)} 帧：{os.path.basename(files[0])} .. "
          f"{os.path.basename(files[-1])}")

    M = estimate_transform(args.bg, files[0])

    use = files[FRAME_FIRST:FRAME_LAST + 1]
    print(f"[use] 取单调区间 第 {FRAME_FIRST} ~ {FRAME_LAST} 帧，共 {len(use)} 帧"
          f"（0=静止位，末帧=拉满）")

    aligned = []
    for i, f in enumerate(use):
        im = cv2.imread(f, cv2.IMREAD_UNCHANGED)
        im = clean_frame(im, log_prefix=f"  [{i:02d}] ")
        aligned.append(cv2.warpAffine(
            im, M[:2].astype(np.float32), (CANVAS_W, CANVAS_H),
            flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT,
            borderValue=(0, 0, 0, 0)))

    if args.rect:
        rect = tuple(int(v) for v in args.rect.split(","))
    else:
        union = np.max([w[:, :, 3] for w in aligned], axis=0)
        ys, xs = np.where(union > 8)
        rect = (int(xs.min()) - 2, int(ys.min()) - 2,
                int(xs.max()) + 3, int(ys.max()) + 3)
    print(f"[bbox] 裁切框 {rect}  ({rect[2]-rect[0]}x{rect[3]-rect[1]})")

    os.makedirs(args.out, exist_ok=True)
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)

    names = []
    for i, w in enumerate(aligned):
        crop = w[rect[1]:rect[3], rect[0]:rect[2]]
        name = f"{args.prefix}_{i:02d}"
        cv2.imwrite(os.path.join(args.out, name + ".png"), crop)
        names.append(name)
        if args.preview:
            flat = np.zeros_like(crop)
            flat[:, :, :3] = 40
            flat[:, :, 3] = 255
            al = crop[:, :, 3:4].astype(np.float32) / 255.0
            comp = (crop[:, :, :3] * al + flat[:, :, :3] * (1 - al)).astype(np.uint8)
            cv2.imwrite(os.path.join(args.preview, name + ".jpg"), comp,
                        [cv2.IMWRITE_JPEG_QUALITY, 88])

    meta = {
        "canvas": [CANVAS_W, CANVAS_H],
        "rect": list(rect),
        "frames": names,
        "frame_source": [FRAME_FIRST, FRAME_LAST],
        "transform": [[float(v) for v in row] for row in M],
        "erode_px": ERODE_PX,
        "blue_br_min": BLUE_BR_MIN,
        "edge_band": EDGE_BAND,
    }
    with open(os.path.join(args.out, "ec_base_manifest.json"), "w",
              encoding="utf-8") as fh:
        json.dump(meta, fh, indent=2, ensure_ascii=False)
    print(f"[out] {len(names)} 帧 → {args.out}")


if __name__ == "__main__":
    main()
