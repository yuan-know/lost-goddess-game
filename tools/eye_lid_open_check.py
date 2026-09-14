# -*- coding: utf-8 -*-
"""
眼睑「开合映射」验证图  (tools/eye_lid_open_check.py)

目的
----
按 EyeCorridorLaserPuzzle.SyncBeamVisual() 里**实际的取帧公式**离线复刻一遍,
确认 _eyeOpen(1=睁 / 0=闭) 与眼睑帧号的对应关系没写反。

运行时公式:
    fi = round(_eyeOpen * (N - 1))          N = 序列帧张数
    _eyeOpen = 1 -> 末帧(最开)   _eyeOpen = 0 -> 第 0 帧(最闭)

输出
----
docs/千眼回廊/黄铜眼_开合映射验证.png
  上半: shell + pupil + lid(按公式取帧) 的合成,逐档 _eyeOpen
  下半: 每档对应的帧号 + 该帧在瞳孔区的「不透明占比」(应该随 _eyeOpen 单调下降)

用法
----
python tools/eye_lid_open_check.py
"""

import os
import glob
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Scenes', 'ChaseCorridor')
LIDDIR = os.path.join(BASE, 'EyeLid')
DOCS = os.path.join(ROOT, 'docs', '千眼回廊')

CROP_W, CROP_H = 525, 380
# 瞳孔区(裁剪框坐标,与 eye_lid_frames.py 的口径一致)
PCI = (252.5, 174.5, 37.9)

STEPS = [1.00, 0.75, 0.50, 0.25, 0.00]

# 光柱淡出参数(必须与 EyeCorridorLaserPuzzle 保持一致)
BEAM_CUT_LO, BEAM_CUT_HI = 0.15, 0.85
CLOSED_BEAM_ALPHA = 0.0


def beam_k(eye_open):
    """运行时 beamK = Lerp(ClosedBeamAlpha, 1, smoothstep(inverseLerp(lo, hi, eyeOpen)))"""
    t = min(max((eye_open - BEAM_CUT_LO) / (BEAM_CUT_HI - BEAM_CUT_LO), 0.0), 1.0)
    return CLOSED_BEAM_ALPHA + (1.0 - CLOSED_BEAM_ALPHA) * (t * t * (3.0 - 2.0 * t))


def load(name, sub=False):
    p = os.path.join(LIDDIR, name) if sub else os.path.join(BASE, name)
    return Image.open(p).convert('RGBA')


def compose(shell, pupil, lid):
    c = Image.new('RGBA', shell.size, (0, 0, 0, 0))
    for layer in (shell, pupil, lid):
        if layer is not None:
            c.alpha_composite(layer)
    return c


def main():
    frames = sorted(glob.glob(os.path.join(LIDDIR, 'eye_lid_*.png')))
    if not frames:
        print('缺 EyeLid 序列帧'); return 1
    N = len(frames)
    shell, pupil = load('bg_eye_shell.png'), load('bg_eye_pupil.png')

    cx, cy, r = PCI
    Y, X = np.mgrid[0:CROP_H, 0:CROP_W]
    disk = ((X - cx) ** 2 + (Y - cy) ** 2) <= r * r

    cw, ch = CROP_W // 2, CROP_H // 2
    pad, top, lab = 16, 52, 26
    W = pad + len(STEPS) * (cw + pad)
    H = top + ch + lab + pad
    sheet = Image.new('RGB', (W, H), (26, 26, 32))
    d = ImageDraw.Draw(sheet)
    d.text((pad, 12), 'lid frame = round(_eyeOpen x %d)   [top: composite | bottom: frame no. / cover%% / beamK]' % (N - 1),
           fill=(235, 225, 205))

    print('_eyeOpen   frame     pupil-area opaque%%   beamK(beam alpha)')
    for i, e in enumerate(STEPS):
        fi = int(min(max(round(e * (N - 1)), 0), N - 1))
        lid = load(os.path.basename(frames[fi]), sub=True)
        comp = compose(shell, pupil, lid)
        a = np.asarray(lid)[..., 3].astype(np.float32)[disk]
        cov = float((a > 128).mean())
        bk = beam_k(e)
        x0 = pad + i * (cw + pad)
        bg = Image.new('RGB', (cw, ch), (18, 18, 22))
        small = comp.resize((cw, ch), Image.LANCZOS)
        bg.paste(small, (0, 0), small)
        sheet.paste(bg, (x0, top))
        d.text((x0, top - 16), '_eyeOpen = %.2f' % e, fill=(220, 210, 190))
        d.text((x0, top + ch + 4), 'frame %02d/%d  cover %.0f%%' % (fi, N - 1, cov * 100),
               fill=(190, 215, 190))
        d.text((x0, top + ch + 16),
               'beamK %.2f  %s' % (bk, 'HIDDEN' if bk < 0.02 else ('visible' if bk > 0.98 else 'fading')),
               fill=(235, 190, 140) if bk < 0.02 else (190, 205, 225))
        print('  %.2f     %02d/%d     %6.1f%%          %.2f' % (e, fi, N - 1, cov * 100, bk))

    os.makedirs(DOCS, exist_ok=True)
    out = os.path.join(DOCS, '黄铜眼_开合映射验证.png')
    sheet.save(out)
    print('\n-> %s' % out)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
