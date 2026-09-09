# -*- coding: utf-8 -*-
"""把两版青年行走的**头部特写**拼成对比长图,交给视觉模型看眼珠。

纯数值指标(眼区逐帧变化)会被"头整体在晃"污染 —— 头晃动时整个眼区像素都在变,
读数跟眼珠转动分不开。所以这里直接出图人眼/视觉模型看。
"""
import os
import sys
import glob

import numpy as np
from PIL import Image

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def head_strip(name, every, out, start=0, count=0, cols=10):
    src = os.path.join(HERE, 'raw', name)
    files = sorted(glob.glob(os.path.join(src, '*.png')))
    if start or count:
        files = files[start:(start + count if count else None)]
    files = files[::every]

    crops = []
    for p in files:
        im = Image.open(p).convert('RGBA')
        a = np.asarray(im)[..., 3] > 128
        rows = np.where(a.any(axis=1))[0]
        cs = np.where(a.any(axis=0))[0]
        if len(rows) == 0:
            continue
        t, b, l, r = rows[0], rows[-1], cs[0], cs[-1]
        h = b - t + 1
        # 头部 = 顶部 14%,横向以头部实际跨度为准
        hy1 = t + int(h * 0.14)
        band = a[t:hy1]
        hc = np.where(band.any(axis=0))[0]
        hl, hr = (hc[0], hc[-1]) if len(hc) else (l, r)
        pad = 10
        crop = im.crop((max(0, hl - pad), max(0, t - pad), hr + pad, hy1 + pad))
        crop = crop.resize((200, int(200 * crop.height / crop.width)), Image.LANCZOS)
        crops.append(crop)

    if not crops:
        print('[错误] %s 没裁到头' % name)
        return
    cw = max(c.width for c in crops)
    ch = max(c.height for c in crops)
    rows_n = (len(crops) + cols - 1) // cols
    sheet = Image.new('RGB', (cw * cols, ch * rows_n), (235, 235, 235))
    for i, c in enumerate(crops):
        sheet.paste(c, ((i % cols) * cw, (i // cols) * ch), c)
    sheet.save(out)
    print('%s: %d 个头部裁片 (每 %d 帧取 1) -> %s  %dx%d'
          % (name, len(crops), every, out, sheet.width, sheet.height))


if __name__ == '__main__':
    # 旧版实装窗口是 v2 的前 26 帧,每 2 帧取一个 = 13 片
    head_strip('young_walk_v2', 2, os.path.join(HERE, '_diag', 'head_old.png'), 0, 26)
    # 新版 79 帧,每 3 帧取一个 ≈ 27 片
    head_strip('young_walk_v3', 3, os.path.join(HERE, '_diag', 'head_new.png'))
