# -*- coding: utf-8 -*-
"""
eye_console_pack.py —— 眼睛控制台素材打包：全画布大图 → 按 alpha 包围盒裁剪的小图 + manifest.json

为什么需要它
------------
美术按最不容易出错的方式出图：每层都是 3400x1200 全画布透明 PNG。
但那样一张 160x160 的旋钮帽也要占一张全画布纹理，RGBA32 下 ~5.9MB/张，
Unity 默认 maxTextureSize=2048 还会把它降采样，十几个图层叠起来很费显存。

本脚本把每层裁到自己的 alpha 包围盒（+padding），体积通常能小 20~30 倍，
并生成 manifest.json 告诉运行时「这张小图原本贴在源图的哪个位置、
它的旋转轴/位移基准点在哪」，运行时据此算出 pivot / sizeDelta / anchoredPosition。

裁剪后的图仍然全部 < 2048，不会再被 Unity 降采样。

用法
----
  # 1) 美术把全画布图丢进源目录（建议放 Resources 外面，别打进包）
  #    Assets/_Project/ArtSource/eye_console/ec_*.png
  # 2) 跑本脚本
  python eye_console_pack.py \
      --src  Assets/_Project/ArtSource/eye_console \
      --out  Assets/_Project/Resources/Closeups/eye_console

  产出：out 目录下同名的裁剪小图 + manifest.json

约定
----
  · 源图尺寸必须 3400x1200（和 EyeConsoleCloseup.cs 的 kSrcW/kSrcH 一致）
  · 眼睑帧 ec_eyelid_0..N.png 会共用同一个裁剪框（取各帧并集），否则切帧时会抖
  · padding 区域的 RGB 用边缘像素外扩、alpha 置 0，避免 bilinear/压缩在边缘拉出黑边
"""
import argparse
import glob
import json
import os
import re

import numpy as np
from PIL import Image

SRC_W, SRC_H = 3400, 1200
ALPHA_THRESH = 8

# ── 图层表 ────────────────────────────────────────────────────────────
# key                : 和 EyeConsoleCloseup.cs 里的 key 一一对应
# file               : 源文件名（不含扩展名）；输出沿用同名
# pivot (源图像素)    : 该图层的「旋转轴 / 位移基准点」
#                      · 旋钮 = 圆柱侧面轮廓中心（不是顶面圆心，斜视图下顶面会画圈）
#                      · 眼球 / 眼睑 = 球心
#                      · 拉杆 = 球头静止位
#                      · 静态层填图中心即可，填错也不影响（它不动）
LAYERS = [
    # ── 黄金瞳（美术 PSD 分层）─────────────────────────────────────
    ("eye_frame_back",   "ec_eye_frame_back",   (1699.5, 302.0), None),
    ("eye_mask",         "ec_eye_mask",         (1700.0, 299.0), None),
    ("eyeball",          "ec_eyeball",          (1689.5, 286.5), None),
    ("eye_frame_front",  "ec_eye_frame_front",  (1699.5, 302.0), None),
    # ── 三个控件（美术按钮图）─────────────────────────────────────
    #    旋钮拆成「外壳 + 顶面」两层，pivot 必须相同：外壳不动，
    #    顶面绕这个点自转（3/4 视角的圆柱整张转会「躺倒」）。
    ("knob_left_shell",  "ec_knob_left_shell",  (1390.2, 758.6), None),
    ("knob_left_top",    "ec_knob_left_top",    (1390.2, 758.6), None),
    ("knob_mid_shell",   "ec_knob_mid_shell",   (1712.8, 785.3), None),
    ("knob_mid_top",     "ec_knob_mid_top",     (1712.8, 785.3), None),
    ("lever_handle",     "ec_lever_handle",     (2040.6, 678.3), None),
    # ── 眼睑开合（多帧，group="lid" → 共用并集裁剪框，避免切帧抖动）──
    ("eyelid",           "ec_eyelid",           (1699.5, 302.0), "lid"),
]


def alpha_bbox(arr, thresh=ALPHA_THRESH):
    """返回 (x0,y0,x1,y1) 闭区间；全透明返回 None。"""
    m = arr[..., 3] > thresh
    ys, xs = np.nonzero(m)
    if len(ys) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def crop_with_pad(arr, box, pad):
    """按 box 裁剪并向外扩 pad。padding 的 RGB 用边缘外扩、alpha 置 0，防边缘黑边。"""
    x0, y0, x1, y1 = box
    h, w = arr.shape[:2]
    cx0, cy0 = max(0, x0 - pad), max(0, y0 - pad)
    cx1, cy1 = min(w - 1, x1 + pad), min(h - 1, y1 + pad)
    sub = arr[cy0:cy1 + 1, cx0:cx1 + 1].copy()

    # 补上被画布边界截掉的 padding（外扩）
    top, left = cy0 - (y0 - pad), cx0 - (x0 - pad)
    bottom = (y1 + pad) - cy1
    right = (x1 + pad) - cx1
    if top or bottom or left or right:
        rgb = np.pad(sub[..., :3], ((top, bottom), (left, right), (0, 0)), mode="edge")
        a = np.pad(sub[..., 3], ((top, bottom), (left, right)), mode="constant", constant_values=0)
        sub = np.dstack([rgb, a])
    else:
        # 正常情况：把 padding 环的 alpha 清 0，RGB 保留边缘色
        sub[:pad, :, 3] = 0
        sub[-pad:, :, 3] = 0
        sub[:, :pad, 3] = 0
        sub[:, -pad:, 3] = 0
    return sub, (cx0, cy0, cx1 + 1, cy1 + 1)   # rect 用 [x0,y0,x1,y1) 半开区间


def frame_index(path):
    m = re.search(r"_(\d+)\.png$", os.path.basename(path))
    return int(m.group(1)) if m else -1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True, help="全画布素材目录")
    ap.add_argument("--out", required=True, help="输出目录（Resources/Closeups/eye_console）")
    ap.add_argument("--pad", type=int, default=3, help="裁剪外扩像素，默认 3")
    ap.add_argument("--only", default=None, help="只处理某个 key（调试用）")
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    entries = []
    total_src = total_dst = 0
    rows = []

    for key, stem, pivot, group in LAYERS:
        if args.only and key != args.only:
            continue

        if group == "lid":
            files = sorted(glob.glob(os.path.join(args.src, stem + "_*.png")), key=frame_index)
        else:
            p = os.path.join(args.src, stem + ".png")
            files = [p] if os.path.exists(p) else []

        if not files:
            print(f"[skip] {key:22s} 没找到 {stem}.png")
            continue

        # 先算并集包围盒（眼睑各帧必须共用同一个框，否则切帧会抖）
        # 注意：file 和 array 必须成对保留 —— 中间若有帧被跳过，分开存两个 list 会错位。
        # 全透明的帧（比如「全开」帧就是空的）不能丢，否则帧序会错位导致闭眼进度对不上，
        # 它只是没有自己的 bbox，跟着并集框一起裁就行。
        pairs = []
        for f in files:
            im = Image.open(f).convert("RGBA")
            if im.size != (SRC_W, SRC_H):
                raise SystemExit(f"{f} 尺寸是 {im.size}，必须是 {SRC_W}x{SRC_H}")
            pairs.append((f, np.asarray(im), alpha_bbox(np.asarray(im))))

        valid = [p[2] for p in pairs if p[2] is not None]
        if not valid:
            print(f"[warn] {key}: 所有帧都是全透明，跳过")
            continue

        box = (min(b[0] for b in valid), min(b[1] for b in valid),
               max(b[2] for b in valid), max(b[3] for b in valid))

        # 每帧都按同一个 box 裁
        out_names = []
        for f, a, _ in pairs:
            sub, rect = crop_with_pad(a, box, args.pad)
            name = os.path.splitext(os.path.basename(f))[0]
            Image.fromarray(sub, "RGBA").save(os.path.join(args.out, name + ".png"))
            out_names.append(name)
            total_src += a.shape[0] * a.shape[1] * 4
            total_dst += sub.shape[0] * sub.shape[1] * 4

        # 检查 pivot 是否落在裁剪框内
        px, py = pivot
        inside = rect[0] <= px < rect[2] and rect[1] <= py < rect[3]
        flag = "" if inside else "  <-- 注意:pivot 不在裁剪框内!"
        rows.append((key, f"{rect[2]-rect[0]}x{rect[3]-rect[1]}", f"({px},{py})", inside))

        e = {
            "key": key,
            "file": out_names[0],
            "rect": [rect[0], rect[1], rect[2], rect[3]],
            "pivot": [px, py],
        }
        if len(out_names) > 1:
            e["frames"] = out_names
        entries.append(e)
        print(f"[ok]   {key:22s} {len(out_names):>2} 帧  "
              f"裁剪 {(rect[2]-rect[0])}x{rect[3]-rect[1]}  pivot=({px},{py}){flag}")

    manifest = {
        "canvas": [SRC_W, SRC_H],
        "container": [1920, 678],
        "layers": entries,
    }
    mpath = os.path.join(args.out, "manifest.json")
    with open(mpath, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)

    print(f"\n[out] {mpath}  共 {len(entries)} 个图层条目")
    if total_src:
        print(f"[省]  纹理像素 {total_src/1e6:.1f}M → {total_dst/1e6:.1f}M "
              f"(压到 {total_dst/total_src*100:.1f}%，RGBA32 约 "
              f"{total_src/1048576:.0f}MB → {total_dst/1048576:.0f}MB)")
    bad = [r[0] for r in rows if not r[3]]
    if bad:
        print(f"[!!]  以下图层的 pivot 不在自己的裁剪框内，运行时旋转会偏: {bad}")


if __name__ == "__main__":
    main()
