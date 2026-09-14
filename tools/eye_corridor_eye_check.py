# -*- coding: utf-8 -*-
"""
黄金瞳 离屏验证图  (tools/eye_corridor_eye_check.py)

干两件事:
  A. 眼睛状态图: 按运行时的层序(shell -> pupil -> lid)离屏合成
     3 种开合(闭 / 半开 / 全开) x 3 种瞳孔位置(左极限 / 正中 / 右极限) ——
     检查眼珠滚到眼角时是否被眼睑压住。
  B. 场景贴合图: 把眼睛按「目标世界尺寸/位置」缩放着画到 bg_full 上,
     和最初那版 bg_brass_eye 的 alpha 包围盒并排对比 —— 检查尺寸/位置有没有还原。

输出(tools 的父级 docs/千眼回廊/):
  黄铜眼_状态与遮挡检查.png
  黄铜眼_场景位置核对.png
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
PIVOT_X, PIVOT_Y = 262.5, 380.0        # 素材 pivot(图底边中心) 在裁剪框内的像素位置
CONTENT = (40, 40, 485, 340)           # 眼睛内容 bbox(裁剪框内 px)
CONTENT_CX, CONTENT_CY = 262.5, 190.0  # 内容中心
PUPIL_CX, PUPIL_CY = 252.5, 174.5      # 瞳孔默认中心
PUPIL_OFF = (PUPIL_CX - PIVOT_X, PIVOT_Y - PUPIL_CY)   # (-10, +205.5) 瞳孔中心相对 pivot

# 与 EyeCorridorLaserPuzzle 对齐的口径
SWING_L, SWING_R = 1.10, 1.30
EYE_TARGET_W = 0.87                     # 最初 bg_brass_eye 实测内容宽(世界单位)
EYE_CENTER = (-0.075, 4.40)             # 最初实测内容中心(世界坐标)


def load(name, sub=False):
    p = os.path.join(LIDDIR if sub else BASE, name)
    return Image.open(p).convert('RGBA')


def paste_at(canvas, img, center_in_img, center_on_canvas):
    """把 img 里 center_in_img(px) 这个点, 对齐到画布上的 center_on_canvas(px)"""
    x = int(round(center_on_canvas[0] - center_in_img[0]))
    y = int(round(center_on_canvas[1] - center_in_img[1]))
    canvas.alpha_composite(img, (x, y))


def compose(shell, pupil, lid, pupil_dx_px=0.0, sx=1.0, sy=1.0):
    """运行时层序合成: shell -> pupil(以瞳孔中心为支点缩放+平移) -> lid"""
    c = shell.copy()
    if pupil is not None:
        pw, ph = max(1, int(round(CROP_W * sx))), max(1, int(round(CROP_H * sy)))
        p = pupil.resize((pw, ph), Image.LANCZOS)
        pivot_in_img = (PIVOT_X * sx, PIVOT_Y * sy)
        pupil_center = (PUPIL_CX + pupil_dx_px, PUPIL_CY)
        pivot_on_canvas = (pupil_center[0] + PUPIL_OFF[0] * sx,
                           pupil_center[1] + PUPIL_OFF[1] * sy)
        paste_at(c, p, pivot_in_img, pivot_on_canvas)
    if lid is not None:
        c.alpha_composite(lid)
    return c


def state_sheet():
    shell = load('bg_eye_shell.png')
    pupil = load('bg_eye_pupil.png')
    frames = sorted(glob.glob(os.path.join(LIDDIR, 'eye_lid_*.png')))
    if not frames:
        print('缺 EyeLid 序列帧'); return None
    picks = [frames[0], frames[len(frames) // 2], frames[-1]]
    labels = ['闭(第0帧)', '半开', '全开(末帧)']

    cols = [('瞳孔左极限(-1.10)', -SWING_L * 100), ('瞳孔正中', 0.0), ('瞳孔右极限(+1.30)', SWING_R * 100)]
    cw, ch = CROP_W // 2, CROP_H // 2
    pad, top = 16, 34
    W = pad + len(cols) * (cw + pad)
    H = top + len(picks) * (ch + pad + 22)
    sheet = Image.new('RGB', (W, H), (26, 26, 32))
    d = ImageDraw.Draw(sheet)
    for j, (cl, _) in enumerate(cols):
        d.text((pad + j * (cw + pad), 12), cl, fill=(230, 220, 200))
    for i, fp in enumerate(picks):
        lid = load(os.path.basename(fp), sub=True)
        y0 = top + i * (ch + pad + 22)
        d.text((pad, y0), labels[i], fill=(210, 200, 180))
        for j, (_, dx) in enumerate(cols):
            comp = compose(shell, pupil, lid, pupil_dx_px=dx)
            comp = comp.resize((cw, ch), Image.LANCZOS)
            bg = Image.new('RGB', (cw, ch), (18, 18, 22)); bg.paste(comp, (0, 0), comp)
            sheet.paste(bg, (pad + j * (cw + pad), y0 + 16))
    out = os.path.join(DOCS, '黄铜眼_状态与遮挡检查.png')
    sheet.save(out)
    return out


def scene_sheet():
    full = Image.open(os.path.join(BASE, 'bg_full.png')).convert('RGBA')
    shell = load('bg_eye_shell.png')
    pupil = load('bg_eye_pupil.png')
    frames = sorted(glob.glob(os.path.join(LIDDIR, 'eye_lid_*.png')))
    eye = compose(shell, pupil, load(os.path.basename(frames[-1]), sub=True))

    # 只取「眼睛内容」再按目标世界尺寸摆放(等比, 与运行时 EyeScale 一致)
    content = eye.crop(CONTENT)
    cw_px = CONTENT[2] - CONTENT[0]
    ch_px = CONTENT[3] - CONTENT[1]
    k = (EYE_TARGET_W * 100.0) / cw_px
    nw = int(round(cw_px * k))
    nh = int(round(ch_px * k))
    content = content.resize((nw, nh), Image.LANCZOS)

    # 世界 -> bg_full 像素
    cx_px = (EYE_CENTER[0] + 17.0) * 100.0
    cy_px = (6.0 - EYE_CENTER[1]) * 100.0
    brass = Image.open(os.path.join(BASE, 'bg_brass_eye.png')).convert('RGBA')
    canvas = full.copy()
    paste_at(canvas, content, (nw / 2.0, nh / 2.0), (cx_px, cy_px))

    # 数值自检:贴合后「眼睛(含可见内容)」在 bg_full 里的 bbox, 与最初 bg_brass_eye 的 bbox 比
    brass_a = np.asarray(brass)[..., 3]
    ys, xs = np.where(brass_a > 24)
    tgt = (xs.min(), xs.max(), ys.min(), ys.max())
    a = np.abs(np.asarray(canvas)[..., :3].astype(np.int16) -
               np.asarray(full)[..., :3].astype(np.int16)).max(axis=2)
    ys2, xs2 = np.where(a > 8)
    got = (xs2.min(), xs2.max(), ys2.min(), ys2.max()) if len(xs2) else None
    print('  目标 bbox(最初 bg_brass_eye) x %d..%d y %d..%d   宽 %d 高 %d' % (
        tgt[0], tgt[1], tgt[2], tgt[3], tgt[1] - tgt[0], tgt[3] - tgt[2]))
    if got:
        print('  贴合后 bbox                  x %d..%d y %d..%d   宽 %d 高 %d' % (
            got[0], got[1], got[2], got[3], got[1] - got[0], got[3] - got[2]))
        print('  中心偏移 (%.1f, %.1f) px   宽差 %+d px  高差 %+d px' % (
            (got[0] + got[1]) / 2 - (tgt[0] + tgt[1]) / 2,
            (got[2] + got[3]) / 2 - (tgt[2] + tgt[3]) / 2,
            (got[1] - got[0]) - (tgt[1] - tgt[0]), (got[3] - got[2]) - (tgt[3] - tgt[2])))

    # 对照板: 原始 / 贴合后 / 最初 bg_brass_eye 的 bbox 参考
    box = (1560, 90, 1830, 225)
    tiles = []
    ref = full.copy(); ref.alpha_composite(brass)
    for label, img in [('① 原背景图(无黄金瞳图层)', full), ('② 分层版按目标尺寸贴合', canvas),
                       ('③ 最初 bg_brass_eye 图层', ref)]:
        t = img.crop(box)
        bg = Image.new('RGB', t.size, (18, 18, 22)); bg.paste(t, (0, 0), t)
        tiles.append((label, bg))
    w, h = tiles[0][1].size
    sc = 2
    sheet = Image.new('RGB', (w * sc * 3 + 40, h * sc + 40), (26, 26, 32))
    d = ImageDraw.Draw(sheet)
    for i, (label, t) in enumerate(tiles):
        t = t.resize((w * sc, h * sc), Image.NEAREST)
        x = 10 + i * (w * sc + 10)
        sheet.paste(t, (x, 30))
        d.text((x, 10), label, fill=(230, 220, 200))
    out = os.path.join(DOCS, '黄铜眼_场景位置核对.png')
    sheet.save(out)
    return out


if __name__ == '__main__':
    os.makedirs(DOCS, exist_ok=True)
    a = state_sheet(); print('->', a)
    b = scene_sheet(); print('->', b)
