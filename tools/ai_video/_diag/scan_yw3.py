# -*- coding: utf-8 -*-
"""为新版青年行走(raw/young_walk_v3, 79帧)选窗。

比旧的 scan_walk.py 多一项:**眼区稳定性**。用户反馈上一版"眼珠转动太快",
所以选窗时不能只看接缝闭合和节奏均匀,还要挑眼睛动得最少的那一段。

三项评分:
  legIoU  —— 循环接缝处腿部形状吻合度(闭合性),越高越好
  cv      —— 帧间运动量的变异系数(节奏均匀度),越低越好
  eye     —— 窗口内眼区逐帧变化的中位数,越低越好(眼珠越安静)

诊断已确认源素材步态周期 = **22 帧**(lag 平均距离低谷 0.1805),
所以窗口长度优先取 22 前后。取整周期是硬要求 —— 见 memory:
"重采样只能修节奏均匀度,修不了周期不闭合"。
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
NAME = sys.argv[1] if len(sys.argv) > 1 else 'young_walk_v3'

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
rows_ = np.where(u.any(axis=1))[0]
cols_ = np.where(u.any(axis=0))[0]
top, bot, left, right = rows_[0], rows_[-1], cols_[0], cols_[-1]
H = bot - top + 1
legsl = slice(top + int(H * 0.70), bot + 1)

# 眼区:头顶下 3%~9%,横向中间 40%
ey0, ey1 = top + int(H * 0.03), top + int(H * 0.09)
ex0 = left + int((right - left) * 0.30)
ex1 = left + int((right - left) * 0.70)
eye = RGB[:, ey0:ey1, ex0:ex1]
# 逐帧眼区变化(循环意义)
eyed = np.array([np.abs(eye[(i + 1) % n] - eye[i]).mean() for i in range(n)])


def iou(a, b):
    x, y = a > 0.5, b > 0.5
    return (x & y).sum() / max((x | y).sum(), 1)


rows = []
for L in (20, 21, 22, 23, 24):
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
        eye_max = float(eyed[s:e + 1].max())
        # 综合分:腿闭合权重最大,眼睛安静次之,节奏第三
        score = play_leg * 2.0 - eye_med / 30.0 - cv * 0.3
        rows.append((score, play_leg, play_full, cv, worst, eye_med, eye_max, L, s, e))

rows.sort(reverse=True)
print('=== %s (%d 帧) 选窗结果  按综合分排序 ===' % (NAME, n))
print('%-6s %-7s %-7s %-6s %-6s %-7s %-7s %-3s %s'
      % ('score', 'legIoU', 'fullIoU', 'cv', 'worst', 'eyeMed', 'eyeMax', 'L', 'window'))
for r in rows[:15]:
    print('%.4f %.4f  %.4f  %.3f  %.2f   %6.2f  %6.2f  %2d  [%d:%d]'
          % (r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8], r[9] + 1))

print('\n--- 眼睛最安静的 5 个窗口(不看腿闭合) ---')
rows2 = sorted(rows, key=lambda r: r[5])
for r in rows2[:5]:
    print('eyeMed %6.2f  legIoU %.4f  cv %.3f  L=%d  [%d:%d]'
          % (r[5], r[1], r[3], r[7], r[8], r[9] + 1))

print('\n--- 腿闭合最好的 5 个窗口 ---')
rows3 = sorted(rows, key=lambda r: -r[1])
for r in rows3[:5]:
    print('legIoU %.4f  eyeMed %6.2f  cv %.3f  L=%d  [%d:%d]'
          % (r[1], r[5], r[3], r[7], r[8], r[9] + 1))
