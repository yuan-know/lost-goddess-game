# -*- coding: utf-8 -*-
"""把透明背景立绘合成到纯绿幕上,做成 AI 视频(即梦/可灵)的首帧图。

为什么要这一步:
  立绘是 RGBA 透明背景。直接喂给 image-to-video,模型会自己脑补一个背景,
  输出的视频没法抠图 —— 后面"抠干净→序列帧→接回 Unity"整条管线就断了。
  先合到纯绿幕上,模型才会把绿幕当背景延续下去。

绿幕安全性已实测:young/middle/old 三张立绘的不透明像素里,
  满足 g > r+25 且 g > b+25 的"偏绿冲突像素"= 0 个,不会被色度键误抠。

关键产出不只是 PNG,还有同名 .json —— 记录人物在画布上的精确位置
  (脚底行号 feet_y / 缩放 scale / 水平中心 center_x)。
  后处理脚本靠这份元数据把 AI 每帧的脚底"钉"回同一行,治脚底抖。
  ⚠️ 别手动改 PNG 尺寸或裁剪,否则 json 失效。

用法:
    python tools/make_greenscreen_plate.py            # 三形态全出
    python tools/make_greenscreen_plate.py young      # 只出青年
"""
import sys, io, os, json
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
import numpy as np
from PIL import Image

ROOT = r"C:\Users\yuan\lost-goddess-game"
SRC_DIR = os.path.join(ROOT, r"Assets\_Project\Resources\Characters")
OUT_DIR = os.path.join(ROOT, r"tools\ai_video\plates")

# 画布 9:16 竖版。即梦等工具对竖版支持最好,人物是竖长构图,横版会浪费一半像素。
CANVAS_W, CANVAS_H = 1080, 1920

# 纯绿幕。用标准键控绿(0,255,0)而不是影视绿(0,177,64):
#   AI 视频会给背景加轻微明暗渐变,纯饱和绿留的容差最大。
GREEN = (0, 255, 0)

# 人物占画布高度的比例。留 headroom/footroom 很重要:
#   贴边会让模型在生成时把头顶或脚底裁出画面,一裁脚底对齐就废了。
FIGURE_H_RATIO = 0.82
# 脚底距画布底部的比例。垫高一点,给"脚下阴影/地面"留空间,
#   也避免模型把脚糊进画布边缘。
FEET_MARGIN_RATIO = 0.09


def make_plate(name):
    src_path = os.path.join(SRC_DIR, f"{name}.png")
    if not os.path.exists(src_path):
        print(f"[跳过] 找不到 {src_path}")
        return None

    im = Image.open(src_path).convert("RGBA")
    bbox = im.getbbox()          # 只取有内容的区域,原图四周的空白不参与缩放计算
    if bbox is None:
        print(f"[跳过] {name}.png 全透明")
        return None
    fig = im.crop(bbox)
    fw, fh = fig.size

    target_h = int(CANVAS_H * FIGURE_H_RATIO)
    scale = target_h / fh
    target_w = max(1, int(round(fw * scale)))
    fig = fig.resize((target_w, target_h), Image.LANCZOS)

    canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H), GREEN + (255,))
    paste_x = (CANVAS_W - target_w) // 2
    feet_y = CANVAS_H - int(CANVAS_H * FEET_MARGIN_RATIO)
    paste_y = feet_y - target_h
    canvas.alpha_composite(fig, (paste_x, paste_y))

    os.makedirs(OUT_DIR, exist_ok=True)
    out_png = os.path.join(OUT_DIR, f"{name}_plate.png")
    canvas.convert("RGB").save(out_png, quality=98)

    # 元数据:后处理脚本据此把每帧脚底钉回 feet_y,并还原原始比例。
    meta = {
        "name": name,
        "canvas": [CANVAS_W, CANVAS_H],
        "green": list(GREEN),
        "figure_box": [paste_x, paste_y, paste_x + target_w, feet_y],
        "feet_y": feet_y,
        "center_x": paste_x + target_w // 2,
        "scale": round(scale, 6),
        "source_bbox": list(bbox),
        "source_size": list(im.size),
    }
    with open(os.path.join(OUT_DIR, f"{name}_plate.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=2)

    print(f"[完成] {name}: 人物 {target_w}x{target_h} (scale {scale:.3f}), "
          f"脚底 y={feet_y}, 中心 x={meta['center_x']}")
    print(f"        {out_png}")
    return meta


def main():
    names = sys.argv[1:] or ["young", "middle", "old"]
    made = [m for m in (make_plate(n) for n in names) if m]
    if made:
        print(f"\n共生成 {len(made)} 张绿幕首帧 → {OUT_DIR}")
        print("即梦设置:图生视频 / 固定机位(关掉智能运镜) / 3秒")


if __name__ == "__main__":
    main()
