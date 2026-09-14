# -*- coding: utf-8 -*-
"""
eye_make_opening_mask.py —— 从眼球序列帧里提取「眼窝开口」遮罩

用途:
  左右转动不用新素材也能做 —— 把眼睛拆成两层:
    底层: 整帧(铜框 + 眼窝 + 球)
    遮罩层: 用本脚本产出的开口遮罩,里面放同一帧但横向偏移
  铜框在遮罩外(不动),只有眼窝里的球左右移动。开口内部本来就是暗色、
  纹理均匀,所以接缝看不出来。

做法:
  青铜框是亮的,开口是被它围住的暗区。于是
    亮 = luma > T 且 alpha 有效
    从画布边界洪水填充「非亮」区域,填不到的就是被围住的开口
  再向内腐蚀几像素(把接缝藏进暗部),输出纯白遮罩。

用法:
  python eye_make_opening_mask.py --frames <帧目录> --out <输出目录> [--erode 6]
"""
import argparse
import glob
import json
import os
import re

import numpy as np
from PIL import Image
from scipy import ndimage

# 帧图里眼睛的包围盒（1470x630 坐标系），用于裁剪
EYE_BOX = (477, 143, 1001, 492)
OUT_SIZE = (273, 185)          # 和 ec_eyelook_*.png 一致
CROP_BOX = (470, 136, 1008, 499)


def frame_no(p):
    return int(re.search(r"(\d+)\.png$", os.path.basename(p)).group(1))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--frames", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--cx", type=float, default=134)
    ap.add_argument("--cy", type=float, default=88)
    ap.add_argument("--rx", type=float, default=94)
    ap.add_argument("--ry", type=float, default=37)
    args = ap.parse_args()

    # 亮度阈值分不开「眼窝开口」和「青铜框」——整只眼睛亮度都低于 55，
    # 只有高光处才亮。梯度法也因纹理噪声而漏。所以这里用人工标定的椭圆：
    # 从提亮网格图上量出来的开口范围（sprite 坐标系）。
    W, H = OUT_SIZE
    cx, cy, rx, ry = args.cx, args.cy, args.rx, args.ry
    yy, xx = np.mgrid[0:H, 0:W]
    m = (((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2) <= 1.0
    print(f"开口椭圆(遮罩图坐标) 中心({cx},{cy}) 半轴({rx},{ry})  面积 {int(m.sum())} "
          f"({m.mean()*100:.1f}% of {W}x{H})")

    rgba = np.zeros((H, W, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.where(m, 255, 0).astype(np.uint8)
    os.makedirs(args.out, exist_ok=True)
    p2 = os.path.join(args.out, "ec_eye_opening_mask.png")
    Image.fromarray(rgba, "RGBA").save(p2)
    print(f"[out] {p2}")

    fs = sorted(glob.glob(os.path.join(args.frames, "*.png")), key=frame_no)
    # 预览:遮罩位置 + 左右偏移效果(底层整帧 + 遮罩内偏移的副本)
    mf = json.load(open(os.path.join(args.out, "eyelook_manifest.json"), encoding="utf-8"))
    cb = tuple(mf["crop_box"])
    mid = fs[mf["src_frames"][len(mf["src_frames"]) // 2] - 1]
    base = Image.open(mid).convert("RGBA").crop(cb).resize(OUT_SIZE, Image.Resampling.LANCZOS)

    tiles = []
    for shift in (0, -20, 20):
        c = Image.new("RGBA", OUT_SIZE, (0, 0, 0, 0))
        c.alpha_composite(base)
        shifted = Image.new("RGBA", OUT_SIZE, (0, 0, 0, 0))
        shifted.alpha_composite(base, (shift, 0))
        arr = np.asarray(shifted).copy()
        arr[..., 3] = (arr[..., 3].astype(np.float32) * (np.asarray(rgba)[..., 3] / 255.0)).astype(np.uint8)
        c.alpha_composite(Image.fromarray(arr, "RGBA"))
        bgc = Image.new("RGB", OUT_SIZE, (26, 24, 22))
        bgc.paste(c, (0, 0), c)
        tiles.append(bgc)

    # 遮罩位置示意
    ov = base.copy()
    mm = np.asarray(rgba).copy()
    mm[..., :3] = np.array([255, 60, 60], np.uint8)
    mm[..., 3] = (np.asarray(rgba)[..., 3] * 0.4).astype(np.uint8)
    ov.alpha_composite(Image.fromarray(mm, "RGBA"))
    bgc = Image.new("RGB", OUT_SIZE, (26, 24, 22))
    bgc.paste(ov, (0, 0), ov)
    tiles.append(bgc)

    Z = 2
    sheet = Image.new("RGB", (W * Z * 2 + 12, H * Z * 2 + 12), (40, 40, 40))
    labels = ["mask on base", "shift -20", "shift 0", "shift +20"]
    from PIL import ImageDraw
    for i, t in enumerate([tiles[3], tiles[1], tiles[0], tiles[2]]):
        im = t.resize((W * Z, H * Z), Image.Resampling.LANCZOS)
        d = ImageDraw.Draw(im)
        d.rectangle([0, 0, 170, 22], fill=(0, 0, 0))
        d.text((5, 6), labels[i], fill=(255, 230, 120))
        sheet.paste(im, ((i % 2) * (W * Z + 6) + 4, (i // 2) * (H * Z + 6) + 4))
    pv = os.path.join(args.out, "_mask_preview.png")
    sheet.save(pv)
    print(f"[preview] {pv}")


if __name__ == "__main__":
    main()
