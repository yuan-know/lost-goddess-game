# -*- coding: utf-8 -*-
"""
beam_white_preview.py —— 离线预演「光束改纯白」前后的观感对照

复用 beam_look_preview 的加色渲染数学(Legacy Particles/Additive):
    add = 2 * tintRGB * vtxAlpha * tintAlpha * texAlpha
其余参数(光雾 alpha 0.11 / 顶点压暗 0.50 / 地面亮条 0.22 / 两片镜像复位点 ±9.3)全部不变,
只把 TINT 从暖黄 (1, 0.95, 0.84) 换成纯白 (1, 1, 1)。

产出: docs/千眼回廊/光束颜色_暖黄vs纯白.png
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

import beam_look_preview as B


def main():
    bg = np.asarray(Image.open(os.path.join(B.BASE, 'bg_full.png')).convert('RGB'))
    mask = np.asarray(Image.open(os.path.join(B.BASE, 'bg_gate_shadow.png')).convert('RGBA')
                      .resize((B.BG_W, B.BG_H)))[:, :, 3].astype(np.float32) / 255.0
    bg = (bg.astype(np.float32) * (1.0 - (mask * 0.392)[:, :, None]))

    beams = [B.RESET_X, -B.RESET_X]          # 复位相位:两片镜像落在 ±9.3

    def shot(title, sub, tint):
        B.TINT = np.array(tint, dtype=np.float32)
        c = bg.astype(np.float32).copy() / 255.0
        c = B.render(c, beams, 0.11, B.tex_alpha_new, 0.50, 0.0, 0.22)
        c = np.clip(c, 0, 1) * 255.0
        im = Image.fromarray(c.astype(np.uint8))
        d = ImageDraw.Draw(im)
        try:
            f1 = ImageFont.truetype(B.FONT, 62)
            f2 = ImageFont.truetype(B.FONT, 40)
        except Exception:
            f1 = f2 = ImageFont.load_default()
        d.text((40, 24), title, font=f1, fill=(255, 240, 200), stroke_width=4, stroke_fill=(0, 0, 0))
        d.text((40, 104), sub, font=f2, fill=(225, 225, 225), stroke_width=3, stroke_fill=(0, 0, 0))
        return im

    warm = shot('改前:暖黄光 (1, 0.95, 0.84)',
                '光雾 alpha 0.11 / 顶点压暗 0.50 / 地面亮条 0.22 —— 色相偏奶油黄',
                (1.0, 0.95, 0.84))
    white = shot('改后:纯白 (1, 1, 1)',
                 '其余参数一字未动 —— 只把色相拉到中性白,颗粒与柔边照旧',
                 (1.0, 1.0, 1.0))

    # 采样:在 y=-1.0 这条高度上,取光锥此时的实际中心(顶点→底边线性插值,别拍脑袋写 x)
    gy_px, vy_px = B.py(B.GROUND_Y), B.py(B.EYE_Y)
    y_px = B.py(-1.0)
    tt = (y_px - vy_px) / (gy_px - vy_px)
    bx_c = B.EYE_X + (B.RESET_X - B.EYE_X) * tt
    xs, ys = int(B.px(bx_c)), int(y_px)
    def rd(im):
        return tuple(int(v) for v in np.asarray(im)[ys, xs])
    print('光锥中心(世界 x=%.2f) RGB  改前 =' % bx_c, rd(warm), ' 改后 =', rd(white))
    rw, ww = np.asarray(warm)[ys, xs].astype(float), np.asarray(white)[ys, xs].astype(float)
    assert not np.allclose(rw, ww), '两次采样一模一样 → TINT 没起作用'
    # 中性和度:纯白光的 R/G/B 加色增量应等比(改前蓝通道明显落后)
    print('加色比例 改前 R:G:B = %.3f:%.3f:%.3f' % (rw[0] / rw[0], rw[1] / rw[0], rw[2] / rw[0]),
          '  改后 R:G:B = %.3f:%.3f:%.3f' % (ww[0] / ww[0], ww[1] / ww[0], ww[2] / ww[0]))

    W = 3400
    sheet = Image.new('RGB', (W, 1200 * 2 + 40), (14, 14, 18))
    sheet.paste(warm.resize((W, 1200), Image.LANCZOS), (0, 0))
    sheet.paste(white.resize((W, 1200), Image.LANCZOS), (0, 1240))
    sheet = sheet.resize((W // 3, (1200 * 2 + 40) // 3), Image.LANCZOS)

    out = os.path.join(B.ROOT, 'docs/千眼回廊/光束颜色_暖黄vs纯白.png')
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.save(out)
    print('->', out, sheet.size)


if __name__ == '__main__':
    main()
