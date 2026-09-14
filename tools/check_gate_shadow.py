# -*- coding: utf-8 -*-
"""
回廊遮罩层 与 bg_full 的对齐核对。

遮罩图 3400x1200 与 bg_full 同格(PPU100 / BottomCenter), 直接 alpha 叠加即可。
本脚本:
  1. 把遮罩叠到 bg_full 上, 出「原图 / 叠加后」对照(带拱门编号与中心竖线)
  2. 打印 8 个拱门(安全点)的世界 X 区间 —— 这是新玩法的安全区真值

输出: docs/千眼回廊/遮罩对齐核对.png
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, 'Assets/_Project/Resources/Scenes/ChaseCorridor')
FONT = os.path.join(ROOT, 'Assets/_Project/Resources/Fonts/LXGWWenKai-Regular.ttf')
OUT = os.path.join(ROOT, 'docs/千眼回廊/遮罩对齐核对.png')

PPU = 100.0
W0, H0 = 3400.0, 1200.0          # 画布像素
HALF_W = W0 / PPU / 2            # 17 世界单位


def px2world_x(px):
    return px / PPU - HALF_W


def world2px_x(wx):
    return (wx + HALF_W) * PPU


def gates():
    """按列扫描 alpha, 返回 8 个拱门的 (x0, x1) 像素区间。"""
    a = np.asarray(Image.open(os.path.join(BASE, 'bg_gate_shadow.png')).convert('RGBA'))
    colhas = (a[:, :, 3] > 24).any(axis=0)
    segs, s = [], None
    for i, v in enumerate(colhas):
        if v and s is None:
            s = i
        if not v and s is not None:
            segs.append((s, i - 1))
            s = None
    if s is not None:
        segs.append((s, len(colhas) - 1))
    return segs


def main():
    full = Image.open(os.path.join(BASE, 'bg_full.png')).convert('RGBA')
    mask = Image.open(os.path.join(BASE, 'bg_gate_shadow.png')).convert('RGBA')
    comp = Image.alpha_composite(full, mask)

    segs = gates()
    print('遮罩拱门(安全点)实测 —— px 与 世界X:')
    print('  #   px区间        世界X区间                中心      宽')
    sp = []
    for i, (x0, x1) in enumerate(segs):
        w0, w1 = px2world_x(x0), px2world_x(x1)
        c, w = (w0 + w1) / 2, w1 - w0
        sp.append((c, w))
        print('  %d  %4d..%-4d   %+7.3f .. %+7.3f   %+7.3f   %.3f' % (i + 1, x0, x1, w0, w1, c, w))
    if len(sp) > 1:
        d = [sp[i + 1][0] - sp[i][0] for i in range(len(sp) - 1)]
        print('  间距: %s  (均值 %.3f)' % (['%.3f' % x for x in d], sum(d) / len(d)))

    # 石像位置(旧玩法实测, 用来确认"安全点在石像之间")
    statues = [-12.96, -8.56, -4.30, 0.00, 4.34, 8.61, 12.95]
    print('\n对照 —— 石像 X: %s' % ['%.2f' % s for s in statues])
    for i, c in enumerate(statues):
        near = min(sp, key=lambda t: abs(t[0] - c))[0]
        print('  石像 %+6.2f  →  最近的拱门中心 %+7.3f (相距 %.2f)' % (c, near, abs(near - c)))

    # ── 出图 ──
    sc = 0.56
    w, h = int(W0 * sc), int(H0 * sc)
    pad, gap = 16, 14
    top = 40
    sheet = Image.new('RGB', (w + pad * 2, top + h * 2 + gap + 34), (14, 13, 17))
    d = ImageDraw.Draw(sheet)
    f = ImageFont.truetype(FONT, 22)
    f2 = ImageFont.truetype(FONT, 16)

    for k, (img, label) in enumerate([(full, 'bg_full(原图)'), (comp, 'bg_full + bg_gate_shadow(叠加后)')]):
        y = top + k * (h + gap)
        d.text((pad, y - 26), label, font=f, fill=(232, 216, 180))
        small = img.convert('RGB').resize((w, h), Image.LANCZOS)
        ds = ImageDraw.Draw(small)
        for i, (x0, x1) in enumerate(segs):
            cx = (x0 + x1) / 2 * sc
            ds.line([(cx, 0), (cx, h)], fill=(90, 230, 140), width=2)
            ds.text((cx - 6, 6), str(i + 1), font=f2, fill=(90, 230, 140))
            ds.line([(x0 * sc, 18), (x1 * sc, 18)], fill=(90, 230, 140), width=3)
        # 石像位置(红) —— 用来确认安全点落在"石像之间"
        for sx in statues:
            ds.line([(world2px_x(sx) * sc, 0), (world2px_x(sx) * sc, h)], fill=(230, 110, 90), width=2)
        sheet.paste(small, (pad, y))

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sheet.save(OUT)
    print('\n->', OUT, sheet.size)


if __name__ == '__main__':
    main()
