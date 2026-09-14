# -*- coding: utf-8 -*-
"""
beam_spill_audit.py —— 审计「没被光锥照到的地方,地面亮条还亮不亮」

背景:`GroundBarSprite()` 原本是 256×128 贴图但 ppu=Hh(128) ⇒ 精灵实际 **2×1 世界单位**,
而 SyncBeamVisual 用 `localScale=(barW=7.95, 1.30)` 直接当世界尺寸用 ⇒ 实机渲染 **15.9 单位宽**
(危险带只有 7.5 单位) ⇒ 每侧多铺 420px 的地面亮光,玩家在出生点(-16.45、光带在 ±9.3)
脚下也是亮的 —— 用户报的"非光照区域也有泛光"。

本脚本在同一相位(复位态 ±9.3)下渲染三张:底图、修复前(15.9 宽 + 0.28 柔降)、
修复后(7.95 宽 + 平台对齐危险带),并在出生点与危险带内各取一个读数。

产出: docs/千眼回廊/地面泛光溢出_修前修后.png
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

import beam_look_preview as B

HALF   = B.BEAM_HALF_W            # 3.75 危险带半宽
BLEED  = 1.06                     # BarBleed
SPAWN  = -16.45                   # 出生点
PROBE_OUT = B.RESET_X - HALF - 0.9  # ≈4.65:危险带**外** 0.9 单位,用来量"非光照区域的泛光"


def barrier(canvas, bx, bar_half, alpha, tint, span):
    """把地面亮条贴到 canvas(0..1 float32)。bar_half = 亮条世界半宽, span = 平台外的柔降比例。"""
    bar = B.ground_bar(alpha, span=span)
    bh, bw = bar.shape
    gy = B.py(B.GROUND_Y)
    y0 = int(gy - bh + 6)
    xa = int(B.px(bx - bar_half))
    xb = int(B.px(bx + bar_half))
    h, w = canvas.shape[:2]
    for r in range(bh):
        yy = y0 + r
        if not (0 <= yy < h):
            continue
        n = max(1, xb - xa)
        cols = np.linspace(0, bw - 1, n).astype(int)
        seg = bar[r, cols]
        a2, b2 = max(0, xa), min(w, xb)
        if b2 <= a2:
            continue
        canvas[yy, a2:b2] += seg[a2 - xa:b2 - xa, None] * tint
    return canvas


def shot(bg, bar_half, span, title, sub):
    c = bg.astype(np.float32).copy() / 255.0
    c = B.render(c, [B.RESET_X, -B.RESET_X], 0.11, B.tex_alpha_new, 0.50, 0.0, 0.0)  # 只要光锥
    for bx in (B.RESET_X, -B.RESET_X):
        c = barrier(c, bx, bar_half, 0.22, B.TINT, span)
    c = np.clip(c, 0, 1) * 255.0
    im = Image.fromarray(c.astype(np.uint8))
    d = ImageDraw.Draw(im)
    try:
        f1 = ImageFont.truetype(B.FONT, 62)
        f2 = ImageFont.truetype(B.FONT, 40)
        f3 = ImageFont.truetype(B.FONT, 34)
    except Exception:
        f1 = f2 = f3 = ImageFont.load_default()
    d.text((40, 24), title, font=f1, fill=(255, 240, 200), stroke_width=4, stroke_fill=(0, 0, 0))
    d.text((40, 104), sub, font=f2, fill=(225, 225, 225), stroke_width=3, stroke_fill=(0, 0, 0))
    # 危险带两端画竖线,一眼看出亮条有没有越界
    for bx in (B.RESET_X, -B.RESET_X):
        for ex in (bx - HALF, bx + HALF):
            xx = int(B.px(ex))
            d.line([(xx, int(B.py(1.2))), (xx, int(B.py(-5.9)))], fill=(120, 200, 255), width=5)
    xx = int(B.px(SPAWN))
    d.line([(xx, int(B.py(1.2))), (xx, int(B.py(-5.9)))], fill=(255, 120, 120), width=5)
    d.text((xx + 10, int(B.py(0.9))), '出生点', font=f3, fill=(255, 150, 150),
           stroke_width=3, stroke_fill=(0, 0, 0))
    # 采样点:危险带外 0.9 单位(黄)。★线只画到地面之上,别盖住要采样的那一行
    xp = int(B.px(PROBE_OUT))
    d.line([(xp, int(B.py(-2.4))), (xp, int(B.py(-4.6)))], fill=(255, 220, 60), width=5)
    d.text((xp - 250, int(B.py(-2.1))), '采样点(带外)', font=f3, fill=(255, 230, 120),
           stroke_width=3, stroke_fill=(0, 0, 0))
    return im


def main():
    bg = np.asarray(Image.open(os.path.join(B.BASE, 'bg_full.png')).convert('RGB'))
    mask = np.asarray(Image.open(os.path.join(B.BASE, 'bg_gate_shadow.png')).convert('RGBA')
                      .resize((B.BG_W, B.BG_H)))[:, :, 3].astype(np.float32) / 255.0
    bg = (bg.astype(np.float32) * (1.0 - (mask * 0.392)[:, :, None]))

    old = shot(bg, HALF * BLEED * 2, 0.28, '修复前:亮条实机 15.9 单位 (精灵被放成 2 倍宽) + 柔降 0.28',
               '危险带 7.5 单位,蓝线以内才是会死的地方 —— 蓝线以外还铺着 4.2 单位/侧的地面亮光')
    new = shot(bg, HALF * BLEED, 1.0 / BLEED, '修复后:亮条 7.95 单位,平台边缘严格压在蓝线上',
               '亮条不再越界;平台以外只剩 22px 柔降,没有"非光照区域的泛光"')

    # 读数:危险带正中心 vs 危险带**外** 0.9 单位处的地面亮度
    PROBE_OUT = B.RESET_X - HALF - 0.9          # 约 4.65,在危险带外
    yrow = int(B.py(B.GROUND_Y + 0.25))
    def rd(im, wx):
        return int(np.asarray(im)[yrow, int(B.px(wx))].mean())
    print('地面亮度  危险带中心 x=+%.2f : 修前 %3d → 修后 %3d' % (B.RESET_X, rd(old, B.RESET_X), rd(new, B.RESET_X)))
    print('地面亮度  危险带外   x=+%.2f : 修前 %3d → 修后 %3d  ← 这一格才是"非光照区域的泛光"'
          % (PROBE_OUT, rd(old, PROBE_OUT), rd(new, PROBE_OUT)))

    W = 3400
    sheet = Image.new('RGB', (W, 1200 * 2 + 40), (14, 14, 18))
    sheet.paste(old.resize((W, 1200), Image.LANCZOS), (0, 0))
    sheet.paste(new.resize((W, 1200), Image.LANCZOS), (0, 1240))
    sheet = sheet.resize((W // 3, (1200 * 2 + 40) // 3), Image.LANCZOS)

    out = os.path.join(B.ROOT, 'docs/千眼回廊/地面泛光溢出_修前修后.png')
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.save(out)
    print('->', out, sheet.size)


if __name__ == '__main__':
    main()
