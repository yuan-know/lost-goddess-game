# -*- coding: utf-8 -*-
"""
eye_track_ball.py —— 追踪序列帧里眼球的 (x, y)

为什么不用「找最暗的团」或「找最亮的点」：
  · 眼窝内壁和眼球一样暗，最暗团会锁到整个眼窝（质心几乎不动）
  · 眼睑边缘比球面高光更亮，找最亮点会锁到眼睑
真正可靠的做法是【帧间差分】：只有眼球在动，所以
  「当前帧比中位帧更暗的地方」 = 眼球现在所在的位置。
对这个差取质心，就是球心。

用法:
  python eye_track_ball.py --src <帧目录> [--out <json/npy 路径>]
"""
import argparse
import glob
import json
import os
import re

import numpy as np
from PIL import Image

# 眼球活动窗口（源图 1470x630 像素坐标），从帧间 std 图上读出来的
ENV = (640, 210, 890, 440)      # x0,y0,x1,y1
ALPHA_MIN = 200


def frame_no(p):
    return int(re.search(r"(\d+)\.png$", os.path.basename(p)).group(1))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    fs = sorted(glob.glob(os.path.join(args.src, "*.png")), key=frame_no)
    print(f"帧数 {len(fs)}")

    A = np.stack([np.asarray(Image.open(f).convert("RGBA")).astype(np.float32) for f in fs])
    L = A[..., :3].mean(axis=3)
    AL = A[..., 3]
    M = np.median(L, axis=0)

    vis = (AL > ALPHA_MIN).all(axis=0)
    x0, y0, x1, y1 = ENV
    env = np.zeros_like(vis)
    env[y0:y1, x0:x1] = True
    env &= vis

    neg = np.where(env, np.clip(M - L, 0, None), 0)
    yy, xx = np.mgrid[0:L.shape[1], 0:L.shape[2]]

    xs, ys, en = [], [], []
    for i in range(len(fs)):
        w = neg[i]
        s = w.sum()
        if s < 1:
            xs.append(np.nan); ys.append(np.nan); en.append(0.0); continue
        xs.append(float((w * xx).sum() / s))
        ys.append(float((w * yy).sum() / s))
        en.append(float(s))
    xs = np.array(xs); ys = np.array(ys)

    print(f"差分能量 中位={np.median(en):.0f}")
    print(f"球心 x 中位={np.median(xs):.1f} 范围={xs.min():.1f}~{xs.max():.1f} "
          f"(幅度 {xs.max()-xs.min():.1f}) std={xs.std():.2f}")
    print(f"球心 y 中位={np.median(ys):.1f} 范围={ys.min():.1f}~{ys.max():.1f} "
          f"(幅度 {ys.max()-ys.min():.1f}) std={ys.std():.2f}")
    print(f"corr(x,y) = {np.corrcoef(xs, ys)[0,1]:.3f}")

    # 残差检查：有没有「跑偏」的帧
    def sm(a, k=11):
        p = np.pad(a, k // 2, mode="edge")
        return np.array([np.median(p[i:i + k]) for i in range(len(a))])
    rx = xs - sm(xs); ry = ys - sm(ys)
    print(f"x 残差 max={np.abs(rx).max():.2f} rms={np.sqrt((rx**2).mean()):.2f}  "
          f"y 残差 max={np.abs(ry).max():.2f} rms={np.sqrt((ry**2).mean()):.2f}")
    badx = [i + 1 for i in range(len(fs)) if abs(rx[i]) > 3.0]
    bady = [i + 1 for i in range(len(fs)) if abs(ry[i]) > 3.0]
    print(f"x 异常帧(残差>3px): {badx if badx else '无'}")
    print(f"y 异常帧(残差>3px): {bady if bady else '无'}")

    out = args.out or os.path.join(args.src, "_ball_xy")
    np.save(out + ".npy", np.stack([xs, ys]))
    json.dump({"frames": [os.path.basename(f) for f in fs],
               "x": [float(v) for v in xs], "y": [float(v) for v in ys],
               "bad_x": badx, "bad_y": bady},
              open(out + ".json", "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"[out] {out}.npy / .json")


if __name__ == "__main__":
    main()
