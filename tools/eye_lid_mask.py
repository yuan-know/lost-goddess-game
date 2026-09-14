# -*- coding: utf-8 -*-
"""
黄金瞳「眼睑遮挡」层生成器  (tools/eye_lid_mask.py)

用途
----
从 Resources/Scenes/ChaseCorridor/bg_eye_shell.png 里**自动识别眼窝开口**
( = 真正的眼睑内缘, 不是最外圈的金属包边 ), 生成一张遮挡层:

    bg_eye_lid.png = bg_eye_shell.png 但「眼窝开口内」alpha 归零

运行时把 lid 叠在瞳孔之上(sortingOrder: shell < pupil < lid),
瞳孔滚到眼角时超出开口的部分就会被眼睑像素压住 —— 这就是"眼珠转到极限被眼睑遮挡"。

为什么不能用最外圈
------------------
用户明确: 黄金瞳 PSD 里最外面还包裹了一层金属包边, 那不是真正的眼睑。
本脚本用「逐列梯度法」找的是眼窝(深色区) 与 眼睑亮带 之间的跃变位置,
金环在更外层 → 瞳孔根本够不到, 不会误当成遮挡边界。

算法
----
1. 每列 x: 从瞳孔中心行 y 向上/向下扫描, 找平滑后亮度**最大正梯度**的位置
   (眼窝暗 → 眼睑亮的跃变点), 并要求跃变后亮度确实 >100 (排除噪声)。
2. 邻域中值(±15px)平滑 + 线性插值补洞 → 上缘曲线 yup(x) / 下缘曲线 ybot(x)。
3. 两端 30px 内按 sqrt 收口, 让开口自然收成眼角尖形。
4. 填充 yup..ybot → 软边(1.2px 高斯) mask 。

验证方法
--------
跑完会打印 "瞳孔位移 → 遮挡比例" 表。左移 1.10 / 右移 1.30 单位约 15%/10%,
与 EyeCorridorLaserPuzzle.PupilSwingLeft/Right 的默认值对应。

依赖: PIL, numpy, scipy  (用 hermes venv 的 python 跑)
"""

import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Scenes', 'ChaseCorridor')

# ── 与 EyeCorridorLaserPuzzle 保持一致的口径 ──
CROP_W, CROP_H = 525, 380          # bg_eye_shell.png 尺寸(= PSD 裁剪框 1437,112-1962,492)
PUPIL_CX, PUPIL_CY = 252.5, 174.5  # 瞳孔中心在裁剪框内的位置
XL, XR = 116, 414                  # 眼窝开口左右端(裁剪框内 px)
CAP = 30.0                         # 两端收口宽度
WIN = 15                           # 中值平滑窗口半径


def edge_row(L, x, y_from, y_to):
    """沿 y 扫描, 返回 (跃变像素 y, 梯度值, 跃变后亮度)"""
    step = 1 if y_to > y_from else -1
    ys = np.arange(y_from, y_to + step, step)
    if x < 0 or x >= L.shape[1]:
        return y_from, 0.0, 0.0
    vs = np.convolve(L[ys, x].astype(np.float64), np.ones(5) / 5, mode='same')
    g = np.diff(vs)
    i = int(np.argmax(g))
    return int(ys[i]), float(g[i]), float(vs[max(0, i - 4):i + 5].max())


def extract_curves(shell):
    L = shell[..., :3].mean(axis=2)
    cy = int(round(PUPIL_CY))
    raw_up, raw_dn = {}, {}
    for x in range(40, 486):
        yu, gu, au = edge_row(L, x, cy, 40)
        if gu > 12 and au > 100:
            raw_up[x] = yu
        yd, gd, ad = edge_row(L, x, cy, 360)
        if gd > 12 and ad > 100:
            raw_dn[x] = yd

    def smooth(raw):
        xs = np.arange(XL, XR + 1)
        ys = np.full(len(xs), np.nan)
        for i, x in enumerate(xs):
            v = [raw[u] for u in range(x - WIN, x + WIN + 1) if u in raw]
            if v:
                ys[i] = np.median(v)
        good = ~np.isnan(ys)
        return np.interp(xs, xs[good], ys[good])

    return smooth(raw_up), smooth(raw_dn), len(raw_up), len(raw_dn)


def build_mask(shell):
    yup, ybot, nu, nd = extract_curves(shell)
    print('  有效列: 上缘 %d  下缘 %d' % (nu, nd))
    xs = np.arange(XL, XR + 1)
    mid = (yup + ybot) / 2.0
    half = np.maximum((ybot - yup) / 2.0, 0.0)
    s = np.clip(np.minimum(xs - XL, XR - xs) / CAP, 0, 1)
    shrink = np.sqrt(np.clip(1.0 - (1.0 - s) ** 2, 0, 1))
    yt = mid - half * shrink
    yb = mid + half * shrink

    mask = np.zeros(shell.shape[:2], dtype=np.float32)
    for i, x in enumerate(xs):
        y0, y1 = int(round(yt[i])), int(round(yb[i]))
        if y1 > y0:
            mask[y0:y1 + 1, x] = 1.0
    return ndi.gaussian_filter(mask, 1.2, mode='nearest')


def occlusion_table(mask, pupil):
    pa = pupil[..., 3] > 16
    tot = float(pa.sum())

    def occl(dx):
        a = np.roll(pa, int(round(dx)), axis=1)
        return 1.0 - (a & (mask > 0.5)).sum() / max(1.0, tot)

    print('  位移(px/单位)   左移遮挡%   右移遮挡%')
    for off in range(90, 161, 10):
        print('    %3d / %.2f      %6.1f     %6.1f' % (off, off / 100.0,
                                                       occl(-off) * 100, occl(+off) * 100))


def main():
    shell_p = os.path.join(DIR, 'bg_eye_shell.png')
    if not os.path.exists(shell_p):
        print('缺 %s —— 先用 PSD 导出外壳/瞳孔两层。' % shell_p)
        return 1
    shell = np.asarray(Image.open(shell_p).convert('RGBA'), dtype=np.float32)
    pupil = np.asarray(Image.open(os.path.join(DIR, 'bg_eye_pupil.png')).convert('RGBA'), dtype=np.float32)

    print('识别眼窝开口 (真正的眼睑内缘)…')
    mask = build_mask(shell)
    cov = (mask > 0.5)
    ys, xs = np.where(cov)
    print('  开口 bbox x %d..%d  y %d..%d  面积 %d px (%.1f%% 画布)' % (
        xs.min(), xs.max(), ys.min(), ys.max(), cov.sum(), cov.sum() / cov.size * 100))

    lid = shell.copy()
    lid[..., 3] = shell[..., 3] * (1.0 - mask)
    out = os.path.join(DIR, 'bg_eye_lid.png')
    Image.fromarray(np.clip(lid, 0, 255).astype(np.uint8), 'RGBA').save(out)
    print('  已写出 %s  (alpha>16 像素 %d, 原 shell %d)' % (
        out, int((lid[..., 3] > 16).sum()), int((shell[..., 3] > 16).sum())))

    print('遮挡比例自检:')
    occlusion_table(mask, pupil)
    return 0


if __name__ == '__main__':
    sys.exit(main())
