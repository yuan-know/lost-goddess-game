# -*- coding: utf-8 -*-
"""数步数(腿部展开量波峰) + 眼睛小区域运动量。

步数不能只靠自相似 lag —— lag 只告诉你"多少帧后姿势重现",
但**一个步态周期含 2 步**(左脚接触 + 右脚接触),而"姿势重现"的 lag
在左右腿造型对称时会读成半个周期。必须靠腿展开量波峰数才能定死步数。
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


def run(name, start=0, count=0):
    src = os.path.join(HERE, 'raw', name)
    files = sorted(glob.glob(os.path.join(src, '*.png')))
    if start or count:
        files = files[start:(start + count if count else None)]
    A, RGB = [], []
    for p in files:
        im = np.asarray(Image.open(p).convert('RGBA'))
        A.append(im[..., 3].astype(np.float32) / 255.0)
        RGB.append(im[..., :3].astype(np.float32))
    A = np.stack(A)
    n = len(A)
    u = A.max(axis=0) > 0.5
    rows = np.where(u.any(axis=1))[0]
    cols = np.where(u.any(axis=0))[0]
    top, bot, left, right = rows[0], rows[-1], cols[0], cols[-1]
    H = bot - top + 1

    print('\n=== %s  (%d 帧, 取 [%d:%d]) ===' % (name, n, start, start + n))

    # 腿展开量 = 最下 15% 区域的水平跨度。双腿分开时跨度最大。
    span = []
    for i in range(n):
        m = A[i] > 0.5
        band = m[bot - int(H * 0.15): bot + 1]
        c = np.where(band.any(axis=0))[0]
        span.append((c[-1] - c[0] + 1) if len(c) else 0)
    span = np.array(span, float)
    # 循环意义上的局部极大
    peaks = [i for i in range(n)
             if span[i] >= span[(i - 1) % n] and span[i] > span[(i + 1) % n]
             and span[i] > span.mean()]
    print('腿展开量: 均值 %.0f  min %.0f  max %.0f' % (span.mean(), span.min(), span.max()))
    print('波峰帧(=接触相,每个波峰 1 步): %s  → %d 步' % (peaks, len(peaks)))
    print('  展开量序列: %s' % span.astype(int).tolist())

    # 眼睛区:头顶往下 3%~9%,横向中间 40% —— 比之前 12% 的"脸区"更聚焦
    ey0 = top + int(H * 0.03)
    ey1 = top + int(H * 0.09)
    ex0 = left + int((right - left) * 0.30)
    ex1 = left + int((right - left) * 0.70)
    eye = np.stack([RGB[i][ey0:ey1, ex0:ex1].mean(axis=2) for i in range(n)])
    ed = np.array([np.abs(eye[(i + 1) % n] - eye[i]).mean() for i in range(n)])
    torso = np.stack([RGB[i][top + int(H * 0.20): top + int(H * 0.50),
                            left:right + 1].mean(axis=2) for i in range(n)])
    td = np.array([np.abs(torso[(i + 1) % n] - torso[i]).mean() for i in range(n)])
    print('眼区 %dx%d  逐帧变化 中位 %.2f 最大 %.2f' % (ey1 - ey0, ex1 - ex0,
                                                np.median(ed), ed.max()))
    print('躯干区   逐帧变化 中位 %.2f' % np.median(td))
    print('眼/躯干 比 %.3f   ← 越大说明脸部相对身体动得越猛' % (np.median(ed) / max(np.median(td), 1e-6)))
    return span, ed


if __name__ == '__main__':
    # 旧版实装窗口:v4 = young_walk_v2 的 26 帧
    run('young_walk_v2', 0, 26)
    run('young_walk_v3', 0, 0)
