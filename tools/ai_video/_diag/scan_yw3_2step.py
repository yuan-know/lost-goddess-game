# -*- coding: utf-8 -*-
"""找 2 步(≈44帧)的最佳窗口。

23 帧只有 1 步,跟旧版"26 帧 2 步"比不公平 ——
其实旧版 26 帧也只有 2 个波峰=2 步,源素材 51 帧的周期 lag=13 也是每 13 帧 1 步。
所以新版 79 帧,lag=22,每 22 帧 1 步 —— 2 步要 44 帧。

但 44 帧的动画,步频按真人 1.8 步/秒算 = 2/ (44/fps)=1.8 → fps≈40,
每帧 25ms,远低于 80ms 阈值,肉眼看不出跳跃。帧数多反而好——动画更流畅。

不过跟旧版比帧数量级变了,需要重新考虑:
旧版 26 帧@18fps=1.44s/2 步=1.38 步/秒。
新版如果 44 帧@18fps=2.44s/2 步=0.82 步/秒 —— 太慢。
要 1.38 步/秒得 fps = 44*1.38/2 = 30fps,每帧 33ms,完全没问题。

结论:取 44 帧 2 步窗口, fps 调到 ~30 保持 1.38 步/秒。
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

ey0, ey1 = top + int(H * 0.03), top + int(H * 0.09)
ex0 = left + int((right - left) * 0.30)
ex1 = left + int((right - left) * 0.70)
eye = RGB[:, ey0:ey1, ex0:ex1]
eyed = np.array([np.abs(eye[(i + 1) % n] - eye[i]).mean() for i in range(n)])


def iou(a, b):
    x, y = a > 0.5, b > 0.5
    return (x & y).sum() / max((x | y).sum(), 1)


# 算腿展开量
span = []
for i in range(n):
    m = A[i] > 0.5
    band = m[bot - int(H * 0.15): bot + 1]
    c = np.where(band.any(axis=0))[0]
    span.append((c[-1] - c[0] + 1) if len(c) else 0)
span = np.array(span, float)
peaks = [i for i in range(n)
         if span[i] >= span[(i - 1) % n] and span[i] > span[(i + 1) % n]
         and span[i] > span.mean()]
print('全序列波峰(步数): %s' % peaks)

print('\n=== 2 步(44帧)窗口 ===')
rows = []
for L in (33, 34, 35, 36, 37):
    for s in range(0, n - L + 1):
        e = s + L - 1
        play_leg = iou(A[e][legsl], A[s][legsl])
        play_full = iou(A[e], A[s])
        st = [np.abs(A[i] - A[i + 1]).sum() for i in range(s, e)]
        st.append(np.abs(A[e] - A[s]).sum())
        st = np.array(st)
        cv = st.std() / st.mean()
        eye_med = float(np.median(eyed[s:e + 1]))
        # 数窗口内波峰
        pk = [p for p in peaks if s <= p <= e]
        score = play_leg * 2.0 - eye_med / 30.0 - cv * 0.3
        rows.append((score, play_leg, play_full, cv, eye_med, len(pk), L, s, e))

rows.sort(reverse=True)
print('%-6s %-7s %-7s %-6s %-7s %-3s %-3s %s'
      % ('score', 'legIoU', 'fullIoU', 'cv', 'eyeMed', 'stp', 'L', 'window'))
for r in rows[:15]:
    print('%.4f %.4f  %.4f  %.3f  %6.2f  %1d   %2d  [%d:%d]'
          % (r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8] + 1))
