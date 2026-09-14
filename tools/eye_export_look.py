# -*- coding: utf-8 -*-
"""
eye_export_look.py —— 把眼球上下转动的原始序列帧导出成 Unity 用的素材（v2）

── 为什么不能按帧号直接切 ────────────────────────────────────────────
AI 生成的视频里眼睛转动是**有快慢的**（缓动：两端慢、中间快），而抽帧是
等时间间隔的。所以「球心 y」对「帧号」是一条曲线，中段每帧能跳 5~12px。
如果旋钮直接映射帧号，转动时就会看到一下一下的跳变。

旋钮是按**位置**驱动的，所以这里按**位置**重采样：
    目标 y 均匀铺满整个行程 → 每帧去挑 y 最接近的那张源帧
这样球心位置对旋钮角度就是线性的，跳变消失。

── 为什么把上行段和下行段合并 ────────────────────────────────────────
同一批渲染里，「球往上走」和「球往下走」经过同一个 y 时画面几乎一致
（实测像素差 0.2~0.9/255）。两段合起来 y 采样密度翻倍：
121 帧覆盖 66.6px，最大空档从 10.8px 降到 2.96px。

用法:
  python eye_export_look.py --frames <帧目录> --track <_ball_xy.npy> \
      --src <原合成图> --out <Resources/Closeups/eye_console> \
      --preview <预览目录> [--count 64] [--cover 1.06]
"""
import argparse
import glob
import json
import os
import re

import numpy as np
from PIL import Image

# 帧图里的眼睛包围盒（1470x630）
FRAME_EYE_BOX = (477, 143, 1001, 492)
CROP_MARGIN = 7
# 原合成图里的眼睛包围盒（来自 docs/eye_console/eye_console_measure.json）
SRC_EYE_BOX = (1477, 152, 1922, 452)
SRC_CANVAS = (3400, 1200)
CONTAINER = (1920, 678)
SRC_EYE_BAND_Y = (140, 470)


def frame_no(p):
    return int(re.search(r"(\d+)\.png$", os.path.basename(p)).group(1))


def src_to_container(p):
    return ((p[0] / SRC_CANVAS[0] - 0.5) * CONTAINER[0],
            (0.5 - p[1] / SRC_CANVAS[1]) * CONTAINER[1])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--frames", required=True)
    ap.add_argument("--track", required=True)
    ap.add_argument("--src", default=None)
    ap.add_argument("--out", required=True)
    ap.add_argument("--preview", default=None)
    ap.add_argument("--count", type=int, default=64)
    ap.add_argument("--cover", type=float, default=1.06)
    args = ap.parse_args()

    fs = sorted(glob.glob(os.path.join(args.frames, "*.png")), key=frame_no)
    d = np.load(args.track)
    bx, by = d[0], d[1]
    if len(fs) != len(by):
        raise SystemExit(f"帧数({len(fs)})和轨迹({len(by)})对不上，检查 --frames/--track")

    y_top, y_bot = float(by.min()), float(by.max())
    print(f"源帧 {len(fs)} 张，球心 y 范围 {y_top:.1f} ~ {y_bot:.1f}（行程 {y_bot-y_top:.1f}px）")

    # ── 按位置重采样：目标 y 均匀铺满，挑一条单调不重复的源帧子序列 ──────
    order = np.argsort(by)                      # 按球心 y 排序（合并了上行+下行）
    ys_sorted = by[order]
    targets = np.linspace(y_top, y_bot, args.count)

    # 贪心：对每个目标位置，往后推进到「再下一张更远」为止，然后取当前这张并前进一格。
    # 这样保证源帧索引严格递增 —— 不会出现连续两帧是同一张图（那会看到一顿）。
    picked, gaps, k = [], [], 0
    n_src = len(ys_sorted)
    for t in targets:
        while k + 1 < n_src and abs(ys_sorted[k + 1] - t) <= abs(ys_sorted[k] - t):
            k += 1
        picked.append(int(order[k]))
        gaps.append(abs(float(ys_sorted[k]) - t))
        k = min(k + 1, n_src - 1)
    gaps = np.array(gaps)
    print(f"重采样为 {args.count} 帧（位置步长 {(y_bot-y_top)/(args.count-1):.2f}px）")
    print(f"  与目标位置的最大偏差 {gaps.max():.2f}px，平均 {gaps.mean():.2f}px")
    print(f"  用到的源帧 {len(set(picked))}/{args.count} 张（无重复）")
    dy = np.diff(by[picked])
    print(f"  相邻帧 Δy: 最大 {dy.max():.2f}px（原始按帧号切时最大 10.76px）")

    # ── 裁剪 + 缩放 ──────────────────────────────────────────────────
    x0, y0, x1, y1 = FRAME_EYE_BOX
    cx0, cy0, cx1, cy1 = x0 - CROP_MARGIN, y0 - CROP_MARGIN, x1 + CROP_MARGIN, y1 + CROP_MARGIN
    cw, ch = cx1 - cx0, cy1 - cy0
    scale = ((SRC_EYE_BOX[2] - SRC_EYE_BOX[0]) * CONTAINER[0] / SRC_CANVAS[0]) / (x1 - x0) * args.cover
    ow, oh = int(round(cw * scale)), int(round(ch * scale))
    print(f"  裁剪 {cw}x{ch} → 缩放 {scale:.4f}（含 cover x{args.cover}）→ 输出 {ow}x{oh}")

    os.makedirs(args.out, exist_ok=True)
    frames_out = []
    for k, i in enumerate(picked):
        im = Image.open(fs[i]).convert("RGBA").crop((cx0, cy0, cx1, cy1)).resize((ow, oh), Image.Resampling.LANCZOS)
        name = f"ec_eyelook_{k:02d}"
        im.save(os.path.join(args.out, name + ".png"))
        frames_out.append(name)

    # ── 挖掉眼睛的背景 ───────────────────────────────────────────────
    bg_name = None
    if args.src and os.path.exists(args.src):
        a = np.asarray(Image.open(args.src).convert("RGBA")).copy()
        y0b, y1b = SRC_EYE_BAND_Y
        a[y0b:y1b, :, 3] = 0
        bg_name = "ec_bg_noeye"
        Image.fromarray(a, "RGBA").save(os.path.join(args.out, bg_name + ".png"))

    # ── manifest ─────────────────────────────────────────────────────
    eye_cx = (SRC_EYE_BOX[0] + SRC_EYE_BOX[2]) / 2.0
    eye_cy = (SRC_EYE_BOX[1] + SRC_EYE_BOX[3]) / 2.0
    anchor = src_to_container((eye_cx, eye_cy))
    crop_cx = (cx0 + cx1) / 2.0
    ball_dx = [float(bx[i] - crop_cx) * scale for i in picked]

    mf = {
        "count": len(frames_out),
        "sprite_size": [ow, oh],
        "anchor_container": [round(anchor[0], 3), round(anchor[1], 3)],
        "scale": round(scale, 6),
        "crop_box": [cx0, cy0, cx1, cy1],
        "src_frames": [int(i + 1) for i in picked],
        "frames": frames_out,
        "bg": bg_name,
        "ball_dx": [round(v, 3) for v in ball_dx],
        "ball_dx_ref": round(float(np.median(ball_dx)), 3),
        "ball_y": [round(float(by[i]), 3) for i in picked],
        "note": "帧 0 = 眼球最上，末帧 = 最下（按位置均匀重采样）。"
                "ball_dx = 球心相对裁剪框中心的水平偏移（容器像素），拉直用。",
    }
    json.dump(mf, open(os.path.join(args.out, "eyelook_manifest.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=2)
    print(f"  球心 y: 首帧 {mf['ball_y'][0]:.1f} → 末帧 {mf['ball_y'][-1]:.1f}")

    # ── 预览 ─────────────────────────────────────────────────────────
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)
        n = len(frames_out)
        cols, pad = 8, 4
        rows = (n + cols - 1) // cols
        sheet = Image.new("RGB", (ow * cols + pad * (cols + 1), oh * rows + pad * (rows + 1)), (225, 225, 225))
        for k in range(n):
            im = Image.open(os.path.join(args.out, frames_out[k] + ".png")).convert("RGBA")
            im = Image.alpha_composite(Image.new("RGBA", im.size, (255, 255, 255, 255)), im).convert("RGB")
            v = np.clip(np.asarray(im).astype(np.float32) / 255, 0, 1) ** 0.52 * 1.15
            im = Image.fromarray((np.clip(v, 0, 1) * 255).astype(np.uint8), "RGB")
            sheet.paste(im, ((k % cols) * (ow + pad) + pad, (k // cols) * (oh + pad) + pad))
        sheet.save(os.path.join(args.preview, "眼睛上下_胶片条.png"))

        gs = []
        for k in range(n):
            im = Image.open(os.path.join(args.out, frames_out[k] + ".png")).convert("RGBA")
            bgc = Image.new("RGB", (ow, oh), (26, 24, 22))
            bgc.paste(im, (0, 0), im)
            gs.append(bgc.convert("P", palette=Image.ADAPTIVE, colors=128))
        gs[0].save(os.path.join(args.preview, "眼球上下_整段播放.gif"),
                   save_all=True, append_images=gs[1:], duration=55, loop=0, optimize=True)
        print(f"[preview] 眼睛上下_胶片条.png + 眼球上下_整段播放.gif")

    print(f"\n[out] -> {args.out}  ({len(frames_out)} 帧" + (f" + {bg_name}" if bg_name else "") + ")")


if __name__ == "__main__":
    main()
