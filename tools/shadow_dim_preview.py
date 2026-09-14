# -*- coding: utf-8 -*-
"""
shadow_dim_preview.py —— 离线预演「角色躲进拱门暗影时的调光」

遮罩 bg_gate_shadow.png 是半透明黑(alpha≈100/255 ⇒ 场景被压到 ~0.61)。
角色画在遮罩**之上**,不跟着变暗就会"浮"在暗影里。
EyeCorridorLaserPuzzle.ShadowDim 默认 0.60,让角色和暗影的亮度对齐。

产出: docs/千眼回廊/角色暗影调光_对照.png
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, 'Assets/_Project/Resources/Scenes/ChaseCorridor')
PLAYER = os.path.join(ROOT, 'Assets/_Project/Art/AIAnimations/YoungIdle/idle_000.png')
OUT = os.path.join(ROOT, 'docs/千眼回廊/角色暗影调光_对照.png')
FONT = os.path.join(ROOT, 'Assets/_Project/Resources/Fonts/LXGWWenKai-Regular.ttf')

PPU = 100.0
HALF = 17.0
GROUND_Y = -5.76
SPRITE_PX_PER_UNIT = 100.0          # AISpriteImporter 的 PPU
MASK_DARKEN = 100.0 / 255.0         # 遮罩 alpha
GATE_CENTER = -2.18                 # 第 4 个拱门安全区中心(离两端够远,便于取景)
SHADOW_DIM = 0.60
PLAYER_TINT = (0.68, 0.72, 0.78)    # PlayerBuilder 给角色叠的冷调


def px(wx):
    return (wx + HALF) * PPU


def py(wy):
    return (6.0 - wy) * PPU


def place_player(canvas, sprite, world_x, dim):
    a = np.asarray(sprite).astype(np.float32)
    al = a[:, :, 3]                                          # 0..255
    ys, xs = np.where(al > 24)
    if not len(xs):
        return canvas
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    content_h = (y1 - y0 + 1) / SPRITE_PX_PER_UNIT          # 世界高(单位)
    content_w = (x1 - x0 + 1) / SPRITE_PX_PER_UNIT
    # 世界 -> 像素:脚底在 groundY,水平居中在 world_x
    cx = px(world_x)
    feet = py(GROUND_Y)
    dst_w = int(round(content_w * PPU))
    dst_h = int(round(content_h * PPU))
    sub = Image.fromarray(a[y0:y1 + 1, x0:x1 + 1].astype(np.uint8), 'RGBA')
    sub = sub.resize((dst_w, dst_h), Image.LANCZOS)

    # PlayerBuilder 的冷调 × 暗影调光系数(alpha 不动)
    s = np.asarray(sub).astype(np.float32)
    s[:, :, 0] *= PLAYER_TINT[0] * dim
    s[:, :, 1] *= PLAYER_TINT[1] * dim
    s[:, :, 2] *= PLAYER_TINT[2] * dim
    sub = Image.fromarray(np.clip(s, 0, 255).astype(np.uint8), 'RGBA')

    canvas.alpha_composite(sub, (int(cx - dst_w / 2), int(feet - dst_h)))
    return canvas


def main():
    bg = Image.open(os.path.join(BASE, 'bg_full.png')).convert('RGBA')
    mask = Image.open(os.path.join(BASE, 'bg_gate_shadow.png')).convert('RGBA')
    scene = Image.alpha_composite(bg, mask)
    sp = Image.open(PLAYER).convert('RGBA')

    titles = []
    panels = []
    for title, dim in [('不进暗影:角色 100% 亮度', 1.0),
                       ('躲进暗影:角色 ×%.2f' % SHADOW_DIM, SHADOW_DIM)]:
        c = scene.copy()
        place_player(c, sp, GATE_CENTER, dim)
        panels.append(c)
        titles.append(title)

    box = (int(px(GATE_CENTER - 3.6)), int(py(2.8)), int(px(GATE_CENTER + 3.6)), int(py(-6.4)))
    crops = [p.crop(box) for p in panels]
    w, h = crops[0].size
    z = 1.45
    crops = [c.resize((int(w * z), int(h * z)), Image.LANCZOS) for c in crops]

    pad, top = 20, 66
    sheet = Image.new('RGB', (crops[0].width * 2 + pad * 3, crops[0].height + top + pad), (14, 14, 18))
    for i, c in enumerate(crops):
        sheet.paste(c, (pad + i * (crops[0].width + pad), top))
    d = ImageDraw.Draw(sheet)
    try:
        f = ImageFont.truetype(FONT, 40)
    except Exception:
        f = ImageFont.load_default()
    d.text((pad, 14), titles[0], font=f, fill=(255, 240, 200))
    d.text((pad * 2 + crops[0].width, 14), titles[1], font=f, fill=(150, 220, 255))
    d.line([(pad + crops[0].width + pad // 2, 0), (pad + crops[0].width + pad // 2, sheet.height)],
           fill=(90, 90, 100), width=2)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sheet.save(OUT)
    print('->', OUT, sheet.size)


if __name__ == '__main__':
    main()
