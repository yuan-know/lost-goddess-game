# -*- coding: utf-8 -*-
"""
eye_frames_analyze.py —— 追踪序列帧里眼球的 (x,y)，找出「跑偏」的帧

原理：只有眼球在动，其余（铜框/眼窝）逐帧不动。
所以取一帧的眼球当模板，在每一帧里做归一化互相关(NCC)找最佳匹配位置，
匹配峰值坐标就是该帧眼球的位置。偏离轨迹的帧就是要清掉的。

用法:
  python eye_frames_analyze.py --src <帧目录> [--template-frame 1] [--out <json>]
"""
import argparse
import glob
import json
import os
import re

import numpy as np
from PIL import Image
from numpy.lib.stride_tricks import sliding_window_view

# 眼球活动区域（源图 1470x630 像素坐标）
SEARCH = (580, 170, 930, 450)      # x0,y0,x1,y1
TEMPLATE_BOX = (665, 235, 815, 385)  # 150x150，主要覆盖眼球
DS = 3                             # 下采样倍数，纯为提速


def frame_no(p):
    m = re.search(r"(\d+)\.png$", os.path.basename(p))
    return int(m.group(1))


def load_lum(path):
    a = np.asarray(Image.open(path).convert("RGBA")).astype(np.float32)
    return a[..., :3].mean(axis=2), a[..., 3]


def downsample(a, k):
    h, w = a.shape[0] // k * k, a.shape[1] // k * k
    return a[:h, :w].reshape(h // k, k, w // k, k).mean(axis=(1, 3))


def ncc_map(img, tmpl):
    """返回 (H-tH+1, W-tW+1) 的 NCC 图。"""
    th, tw = tmpl.shape
    if img.shape[0] < th or img.shape[1] < tw:
        return None
    win = sliding_window_view(img, (th, tw))                  # (H', W', th, tw)
    wm = win.mean(axis=(2, 3), keepdims=True)
    wc = win - wm
    wden = np.sqrt((wc ** 2).sum(axis=(2, 3)))
    t = tmpl - tmpl.mean()
    tden = np.sqrt((t ** 2).sum())
    num = (wc * t).sum(axis=(2, 3))
    den = np.maximum(wden * tden, 1e-6)
    return num / den


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--template-frame", type=int, default=1)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    fs = sorted(glob.glob(os.path.join(args.src, "*.png")), key=frame_no)
    print(f"帧数 {len(fs)}")

    # 模板
    lum0, _ = load_lum(fs[args.template_frame - 1])
    x0, y0, x1, y1 = TEMPLATE_BOX
    tmpl = downsample(lum0[y0:y1, x0:x1], DS)
    sx0, sy0, sx1, sy1 = SEARCH
    search0 = downsample(lum0[sy0:sy1, sx0:sx1], DS)

    # 模板在搜索区里的偏移（用于把峰值换回源图坐标）
    off_x = (x0 - sx0) // DS
    off_y = (y0 - sy0) // DS

    pts = []
    for f in fs:
        lum, _ = load_lum(f)
        img = downsample(lum[sy0:sy1, sx0:sx1], DS)
        m = ncc_map(img, tmpl)
        if m is None:
            pts.append((np.nan, np.nan, np.nan))
            continue
        iy, ix = np.unravel_index(int(np.argmax(m)), m.shape)
        # 峰值对应模板左上角在搜索区的位置 → 换算成「模板中心」在源图的坐标
        cx = sx0 + (ix + off_x) * DS + (x1 - x0) / 2.0
        cy = sy0 + (iy + off_y) * DS + (y1 - y0) / 2.0
        pts.append((cx, cy, float(m[iy, ix])))

    xs = np.array([p[0] for p in pts])
    ys = np.array([p[1] for p in pts])
    sc = np.array([p[2] for p in pts])

    print(f"匹配得分 min={sc.min():.3f} 中位={np.median(sc):.3f}")
    print(f"眼球 x: 中位={np.median(xs):.1f} 范围={xs.min():.1f}~{xs.max():.1f} "
          f"极差={xs.max()-xs.min():.1f} std={xs.std():.2f}")
    print(f"眼球 y: 中位={np.median(ys):.1f} 范围={ys.min():.1f}~{ys.max():.1f} "
          f"极差={ys.max()-ys.min():.1f} std={ys.std():.2f}")

    # 用中位滤波后的轨迹做参考，找偏离点
    def medfilt(a, k=9):
        pad = np.pad(a, k // 2, mode="edge")
        return np.array([np.median(pad[i:i + k]) for i in range(len(a))])

    refx = medfilt(xs, 9)
    devx = xs - refx
    thr = max(4.0, 3.0 * np.median(np.abs(devx)))
    bad = [i + 1 for i in range(len(fs)) if abs(devx[i]) > thr]
    print(f"\n水平偏离中位轨迹 > {thr:.1f}px 的帧（共 {len(bad)} 帧）:")
    print(" ", bad)

    print("\n逐帧 (每 3 帧):")
    for i in range(0, len(fs), 3):
        flag = "  <== 偏" if abs(devx[i]) > thr else ""
        print(f"  f{i+1:03d}  x={xs[i]:6.1f} y={ys[i]:6.1f}  dx={devx[i]:+5.1f}  ncc={sc[i]:.3f}{flag}")

    out = args.out or os.path.join(args.src, "_track.json")
    json.dump({
        "frames": [os.path.basename(f) for f in fs],
        "x": [float(v) for v in xs], "y": [float(v) for v in ys],
        "ncc": [float(v) for v in sc], "devx": [float(v) for v in devx],
        "bad_frames": bad, "thr": float(thr),
    }, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"\n[out] {out}")


if __name__ == "__main__":
    main()
