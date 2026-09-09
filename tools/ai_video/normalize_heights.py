# -*- coding: utf-8 -*-
"""把 AIAnimations 下六套定稿帧统一缩放到目标像素身高。

为什么需要:六套素材来自不同 AI 视频,源构图/镜头距离不一致,
pipeline 用统一 scale 出片后,成片角色身高仍然乱:
  青年待机 5.01 / 青年行走 4.50(行走反而矮 0.5 单位)
  中年待机 5.03(比青年还高) / 中年行走 4.71
  老年待机 4.84 / 老年行走 4.79
年龄顺序和"同形态待机=行走"全不满足。

做法(不改 pipeline,直接后处理 Unity 帧;final/ 目录保留作备份可重拷):
  1. 每套算所有帧角色 alpha 包围盒身高的中位数
  2. 缩放因子 k = 目标身高 / 中位身高
  3. 逐帧裁出角色 bbox → LANCZOS 缩放 k 倍 → 贴回 260×512 画布
  4. 脚底逐帧对齐到该帧原脚底 row(pipeline 已把脚底钉在固定 y,保持它),
     水平对齐该帧原中心 —— 只改缩放,不破坏锚点和走路上下起伏。

目标身高(px @ PPU100 = 世界单位×100),比例取自人物设定源身高
青年1.00 / 中年0.96 / 老年0.92(青年最高、老年拄拐最矮):
  青年 495px(4.95 单位) / 中年 475(4.75) / 老年 455(4.55)
"""
import os
import glob
import numpy as np
from PIL import Image

try:
    sys_stdout = __import__('sys').stdout
    __import__('sys').stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

HERE = os.path.dirname(os.path.abspath(__file__))
BASE = os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Art', 'AIAnimations')

CANVAS_W, CANVAS_H = 260, 512
ALPHA_THR = 10

# 目录 -> 目标像素身高(青年最高 / 中年 / 老年最矮;同形态待机行走等高)
TARGETS = {
    'YoungIdle': 495, 'YoungWalkV6': 495,
    'MiddleIdle': 475, 'MiddleWalkV2': 475,
    'OldIdle': 455, 'OldWalk': 455,
}


def bbox(im):
    a = np.asarray(im)[..., 3]
    rows = np.where((a > ALPHA_THR).any(axis=1))[0]
    cols = np.where((a > ALPHA_THR).any(axis=0))[0]
    if len(rows) == 0:
        return None
    return rows[0], rows[-1], cols[0], cols[-1]


def main():
    for d, tgt in TARGETS.items():
        fs = sorted(glob.glob(os.path.join(BASE, d, '*.png')))
        if not fs:
            print('[跳过] %s 无帧' % d)
            continue

        boxes = []
        for p in fs:
            b = bbox(Image.open(p).convert('RGBA'))
            if b:
                boxes.append((p, b))
        hs = np.array([b[1] - b[0] + 1 for _, b in boxes])
        h_med = float(np.median(hs))
        k = tgt / h_med

        for p, (t, b, l, r) in boxes:
            im = Image.open(p).convert('RGBA')
            crop = im.crop((l, t, r + 1, b + 1))
            w, h = crop.size
            nw, nh = max(1, round(w * k)), max(1, round(h * k))
            crop = crop.resize((nw, nh), Image.LANCZOS)

            canvas = Image.new('RGBA', (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
            cx = (l + r) / 2.0
            px = int(round(cx - nw / 2.0))   # 水平中心对齐原中心
            py = int(b - nh + 1)             # 脚底对齐该帧原脚底 row
            canvas.alpha_composite(crop, (px, py))
            canvas.save(p)

        print('%-14s 原中位身高 %4.0fpx -> 目标 %dpx  (k=%.3f, %d 帧)' % (
            d, h_med, tgt, k, len(boxes)))


if __name__ == '__main__':
    main()
