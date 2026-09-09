# -*- coding: utf-8 -*-
"""为新版中年行走(raw/middle_walk_v2, 89帧)选窗。

诊断结论:步态周期 = 24 帧(lag 低谷 0.1179,腿展开量波峰在 13/38/60/83)。
旧版 middle_walk_v1 有 lag63/lag40/lag22 三个候选周期,是 memory 里
"中年那套有两个候选周期,取40帧后重采样导致腿IoU 0.9786→0.62" 的根源。
**新素材周期干净单一,这个老问题消失了。**

同时评眼区安静度 —— 用户 2026-09-08 反馈青年旧版"眼珠转太快",中年同源同批,
先量一下免得实装后才发现同样问题。
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
NAME = sys.argv[1] if len(sys.argv) > 1 else 'middle_walk_v2'
LENS = [int(x) for x in (sys.argv[2].split(',') if len(sys.argv) > 2 else ['23', '24', '25'])]

files = sorted(glob.glob(os.path.join(HERE, 'raw', NAME, '*.png')))
A, RGB = [], []
for p in files:
    im = np.asarray(Image.open(p).convert('RGBA'))
    A.append(im[..., 3].astype(np.float32) / 255.0)
    RGB.append(im[..., :3].astype(np.float32).mean(axis=2))
A = np.stack(A)
RGB = np.stack(RGB)
n = len(A)

u = A.max(axis=0) > 0.5
r_ = np.where(u.any(axis=1))[0]
c_ = np.where(u.any(axis=0))[0]
top, bot, left, right = r_[0], r_[-1], c_[0], c_[-1]
H = bot - top + 1
legsl = slice(top + int(H * 0.70), bot + 1)

ey0, ey1 = top + int(H * 0.03), top + int(H * 0.09)
ex0 = left + int((right - left) * 0.30)
ex1 = left + int((right - left) * 0.70)
eye = RGB[:, ey0:ey1, ex0:ex1]
eyed = np.array([np.abs(eye[(i + 1) % n] - eye[i]).mean() for i in range(n)])

# 腿展开量波峰 = 步数标记
span = []
for i in range(n):
    m = A[i] > 0.5
    band = m[bot - int(H * 0.15): bot + 1]
    cc = np.where(band.any(axis=0))[0]
    span.append((cc[-1] - cc[0] + 1) if len(cc) else 0)
span = np.array(span, float)
# 只认"显著"波峰:>= 最大值的 95%,避免平台段被数成多步
thr = span.max() * 0.95
peaks = []
for i in range(n):
    if span[i] >= thr and (not peaks or i - peaks[-1] > 8):
        peaks.append(i)
print('=== %s (%d 帧) 显著波峰(步): %s ===' % (NAME, n, peaks))


def iou(a, b):
    x, y = a > 0.5, b > 0.5
    return (x & y).sum() / max((x | y).sum(), 1)


rows = []
for L in LENS:
    for s in range(0, n - L + 1):
        e = s + L - 1
        play_leg = iou(A[e][legsl], A[s][legsl])
        play_full = iou(A[e], A[s])
        st = [np.abs(A[i] - A[i + 1]).sum() for i in range(s, e)]
        st.append(np.abs(A[e] - A[s]).sum())
        st = np.array(st)
        cv = st.std() / st.mean()
        worst = st.max() / np.median(st)
        eye_med = float(np.median(eyed[s:e + 1]))
        nstep = len([p for p in peaks if s <= p <= e])
        score = play_leg * 2.0 - eye_med / 30.0 - cv * 0.3
        rows.append((score, play_leg, play_full, cv, worst, eye_med, nstep, L, s, e))

rows.sort(reverse=True)
print('%-6s %-7s %-7s %-6s %-6s %-7s %-3s %-3s %s'
      % ('score', 'legIoU', 'fullIoU', 'cv', 'worst', 'eyeMed', 'stp', 'L', 'window'))
for r in rows[:15]:
    print('%.4f %.4f  %.4f  %.3f  %.2f   %6.2f  %1d   %2d  [%d:%d]'
          % (r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8], r[9] + 1))
