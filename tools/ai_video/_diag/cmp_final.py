# -*- coding: utf-8 -*-
"""对比两版成片(final 目录):接缝IoU / 腿接缝 / 身高 / 眼区安静度。

成片验证必须重新量一遍 —— 源帧上算的 IoU 不等于成片的:
中值滤波、连通块过滤、缩放都会改形状(见 memory 里"中年 IoU 0.9786→0.62"的教训)。
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


def load(name):
    fs = sorted(glob.glob(os.path.join(HERE, 'final', name, '*.png')))
    A, L = [], []
    for p in fs:
        im = np.asarray(Image.open(p).convert('RGBA'))
        A.append(im[..., 3].astype(np.float32) / 255.0)
        L.append(im[..., :3].astype(np.float32).mean(axis=2))
    return np.stack(A), np.stack(L), len(fs)


def iou(a, b):
    x, y = a > 0.5, b > 0.5
    return (x & y).sum() / max((x | y).sum(), 1)


def report(name):
    A, L, n = load(name)
    u = A.max(axis=0) > 0.5
    r = np.where(u.any(axis=1))[0]
    c = np.where(u.any(axis=0))[0]
    top, bot, lf, rt = r[0], r[-1], c[0], c[-1]
    H = bot - top + 1
    legsl = slice(top + int(H * 0.70), bot + 1)

    seam = iou(A[-1], A[0])
    seam_leg = iou(A[-1][legsl], A[0][legsl])

    # 眼区
    ey0, ey1 = top + int(H * 0.03), top + int(H * 0.09)
    ex0 = lf + int((rt - lf) * 0.30)
    ex1 = lf + int((rt - lf) * 0.70)
    eye = L[:, ey0:ey1, ex0:ex1]
    eyed = np.array([np.abs(eye[(i + 1) % n] - eye[i]).mean() for i in range(n)])
    torso = L[:, top + int(H * 0.20): top + int(H * 0.50), lf:rt + 1]
    td = np.array([np.abs(torso[(i + 1) % n] - torso[i]).mean() for i in range(n)])

    # 每帧身高 / 节奏
    hs = []
    for i in range(n):
        m = A[i] > 0.5
        rr = np.where(m.any(axis=1))[0]
        hs.append(rr[-1] - rr[0] + 1)
    st = np.array([np.abs(A[i] - A[(i + 1) % n]).sum() for i in range(n)])

    # 腿展开量波峰 = 步数
    span = []
    for i in range(n):
        m = A[i] > 0.5
        band = m[bot - int(H * 0.15): bot + 1]
        cc = np.where(band.any(axis=0))[0]
        span.append((cc[-1] - cc[0] + 1) if len(cc) else 0)
    span = np.array(span, float)
    peaks = [i for i in range(n)
             if span[i] >= span[(i - 1) % n] and span[i] > span[(i + 1) % n]
             and span[i] > span.mean()]

    print('=== %-16s %2d 帧 ===' % (name, n))
    print('  接缝 IoU     %.4f      腿接缝 IoU  %.4f' % (seam, seam_leg))
    print('  身高         %d px (min %d max %d)' % (int(np.median(hs)), min(hs), max(hs)))
    print('  节奏 cv      %.3f       最猛一跳 %.2fx' % (st.std() / st.mean(), st.max() / np.median(st)))
    print('  眼区变化中位 %.2f       眼/躯干比 %.3f  ← 越小眼珠越安静'
          % (np.median(eyed), np.median(eyed) / max(np.median(td), 1e-6)))
    print('  步数         %d  (波峰帧 %s)' % (len(peaks), peaks))
    return n, len(peaks)


if __name__ == '__main__':
    names = sys.argv[1:] or ['young_walk_v4', 'young_walk_v5']
    for nm in names:
        report(nm)
        print()
