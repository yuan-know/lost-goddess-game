# -*- coding: utf-8 -*-
"""
千眼回廊 · 黄铜眼观感预览渲染器  (tools/render_eye_corridor_preview.py)

不启动 Unity, 直接把 bg_full + 黄铜眼三层(shell/pupil/lid) + 石像阴影 + 光柱
按运行时公式离屏合成, 用来快速核对"眼珠转动 / 眼睑遮挡 / 闭眼"的画面。

用法:
    python tools/render_eye_corridor_preview.py
输出: docs/千眼回廊/观感预览_*.png

参数与 EyeCorridorLaserPuzzle 的默认值保持一致(改了 C# 常量记得同步这里)。
"""

import os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
D = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Scenes', 'ChaseCorridor')
OUT = os.path.join(ROOT, 'docs', '千眼回廊')

# ── 与 C# 对齐 ──
EYE_X, EYE_Y = 1437, 112          # bg_eye_shell 左上角在 bg 里的像素位置
PUPIL_CX, PUPIL_CY = 252.5, 174.5  # 瞳孔中心在裁剪框(525×380)内的位置
SWING_L, SWING_R = 1.105, 1.355    # PupilSwingLeft / PupilSwingRight
SQUASH = 0.10                      # PupilEdgeSquash
TRACK_MIN, TRACK_MAX = -15.6, 15.6
GROUND_PY = 1176.0                 # 地面(GroundY = -5.76)在 bg 里的像素行
STATUE_XS = (-12.96, -8.56, -4.30, 0.0, 4.34, 8.61, 12.95)
STATUE_SHADOW = True               # 是否画石像阴影安全区


def _load(n):
    return np.asarray(Image.open(os.path.join(D, n)).convert('RGBA'), dtype=np.float32)


BG = _load('bg_full.png')
SHELL = _load('bg_eye_shell.png')
PUPIL = _load('bg_eye_pupil.png')
LID = _load('bg_eye_lid.png')
H, W = BG.shape[:2]


def over(dst, src, ox, oy):
    x0, y0 = max(0, ox), max(0, oy)
    x1, y1 = min(W, ox + src.shape[1]), min(H, oy + src.shape[0])
    if x1 <= x0 or y1 <= y0:
        return
    s = src[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
    a = s[..., 3:4] / 255.0
    dst[y0:y1, x0:x1, :3] = dst[y0:y1, x0:x1, :3] * (1 - a) + s[..., :3] * a
    dst[y0:y1, x0:x1, 3:4] = np.maximum(dst[y0:y1, x0:x1, 3:4], s[..., 3:4])


def add(dst, alpha, rgb, ox, oy):
    x0, y0 = max(0, ox), max(0, oy)
    x1, y1 = min(W, ox + alpha.shape[1]), min(H, oy + alpha.shape[0])
    if x1 <= x0 or y1 <= y0:
        return
    a = alpha[y0 - oy:y1 - oy, x0 - ox:x1 - ox][..., None]
    dst[y0:y1, x0:x1, :3] += a * np.array(rgb, dtype=np.float32)


def _prop(im, sx=1.0, sy=1.0, lit=1.0):
    nw, nh = int(round(525 * sx)), int(round(380 * sy))
    q = np.asarray(Image.fromarray(np.clip(im, 0, 255).astype(np.uint8), 'RGBA')
                   .resize((nw, nh), Image.LANCZOS), dtype=np.float32)
    q[..., :3] *= lit
    return q


def pupil_offset_percent(bx):
    """返回 (dx_px, 被眼睑切掉的百分比)"""
    from scipy import ndimage as ndi  # noqa
    t = (bx - TRACK_MIN) / (TRACK_MAX - TRACK_MIN)
    t2 = t * 2 - 1
    dx = (t2 * SWING_L if t2 < 0 else t2 * SWING_R) * 100.0
    pa = PUPIL[..., 3] > 16
    m = (LID[..., 3] < 8) if False else None
    # lid 抠空的区域 = 眼窝开口
    m = (LID[..., 3] < 8)
    moved = np.roll(pa, int(round(dx)), axis=1)
    cut = 1.0 - (moved & m).sum() / max(1, pa.sum())
    return dx, cut * 100.0


def render(bx, out_name, closed=False):
    img = BG.copy()
    t = (bx - TRACK_MIN) / (TRACK_MAX - TRACK_MIN)
    t2 = t * 2 - 1
    if closed:
        dx = 0.0
        p_sx, p_sy, lit = 1.18, 0.26, 0.42
    else:
        dx = (t2 * SWING_L if t2 < 0 else t2 * SWING_R) * 100.0
        p_sx, p_sy, lit = 1.0 - SQUASH * abs(t2), 1.0, 1.0

    over(img, SHELL, EYE_X, EYE_Y)
    ox = int(round(EYE_X + PUPIL_CX - PUPIL_CX * p_sx + dx))
    oy = int(round(EYE_Y + PUPIL_CY - PUPIL_CY * p_sy))
    over(img, _prop(PUPIL, p_sx, p_sy, lit), ox, oy)
    over(img, LID, EYE_X, EYE_Y)

    pcx = EYE_X + PUPIL_CX + dx
    pcy = EYE_Y + PUPIL_CY

    if STATUE_SHADOW:
        for xw in STATUE_XS:
            yy = np.arange(80); xx = np.arange(270)
            X, Y = np.meshgrid(xx, yy)
            dd = np.sqrt(((X - 135) / 135.0) ** 2 + ((Y - 40) / 40.0) ** 2)
            ov = np.zeros((80, 270, 4), dtype=np.float32)
            ov[..., 3] = np.clip(1 - dd, 0, 1) ** 1.4 * 0.55 * 255
            over(img, ov, int((xw + 17) * 100 - 135), int(GROUND_PY) - 40)

    bxp = (bx + 17) * 100.0
    k = 0.35 if closed else 1.0
    yy = np.arange(int(pcy) - 2, int(GROUND_PY) + 1); xx = np.arange(W)
    X, Y = np.meshgrid(xx, yy)
    tt = (Y - pcy) / max(1.0, GROUND_PY - pcy)
    cx = pcx + (bxp - pcx) * tt
    for ht, hb, al in ((20, 225, 0.20 * k), (7, 88, 0.52 * k)):
        hw = ht + (hb - ht) * tt
        m = np.clip(1 - np.abs(X - cx) / np.maximum(hw, 1e-3), 0, 1) ** 1.6
        m *= 0.55 + 0.45 * (1 - tt)
        add(img, (m * al * 255).astype(np.float32), (0.84, 0.91, 1.0), 0, int(pcy) - 2)

    yy2 = np.arange(0, 140); xx2 = np.arange(0, 2 * int(2.2 * 100 * 1.6))
    X2, Y2 = np.meshgrid(xx2, yy2)
    d2 = np.sqrt(((X2 - len(xx2) / 2) / (len(xx2) / 2)) ** 2 + ((Y2 - 70) / 70) ** 2)
    add(img, (np.clip(1 - d2, 0, 1) ** 1.3 * 0.44 * k * 255).astype(np.float32),
        (1.0, 0.96, 0.82), int(bxp - len(xx2) / 2), int(GROUND_PY) - 70)

    o = os.path.join(OUT, out_name)
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGBA').convert('RGB') \
         .resize((1200, 424), Image.LANCZOS).save(o)
    _, cut = pupil_offset_percent(bx)
    print('%-34s 光柱 %+6.1f  瞳孔偏移 %+7.1fpx  被眼睑切掉 %4.1f%%' % (out_name, bx, dx, cut))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    render(TRACK_MIN, '观感预览_光柱到最左_眼珠被眼角压住.png')
    render(TRACK_MAX, '观感预览_光柱到最右_眼珠被眼角压住.png')
    render(0.0, '观感预览_光柱扫到正中_瞳孔居中.png')
    render(0.0, '观感预览_躲入阴影闭眼.png', closed=True)
