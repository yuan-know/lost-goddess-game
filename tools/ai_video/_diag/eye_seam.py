# -*- coding: utf-8 -*-
"""诊断眼区在**循环接缝处**是否闭合。

用户 2026-09-08 反馈:新版44帧青年行走"结束的时候眼珠还是会转动很快",
但原视频眼珠不快。这说明问题不在抽帧(我取的是连续窗口[16:60],没跳帧),
而在**接缝**:末帧眼珠位置和首帧不一致,循环回去时一帧内跳完 → 看着"猛转一下"。

之前 scan_yw3.py 只评了两项:
  legIoU  —— 腿部接缝闭合
  eyeMed  —— 眼区**逐帧**变化中位数(衡量"平时"眼珠动得快不快)
**漏了最关键的第三项:眼区接缝跳变。** 这就是本次 bug 的根因。

判据: eye_seam / eye_med。=1 表示接缝处眼珠移动量和平时一帧差不多(闭合好);
      >>1 表示接缝处一帧要跳完平时好几帧的量,肉眼就是"猛转一下"。
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
A, G = [], []
for p in files:
    im = np.asarray(Image.open(p).convert('RGBA'))
    A.append(im[..., 3].astype(np.float32) / 255.0)
    G.append(im[..., :3].astype(np.float32).mean(axis=2))
A = np.stack(A)
G = np.stack(G)
n = len(A)

u = A.max(axis=0) > 0.5
r_ = np.where(u.any(axis=1))[0]
c_ = np.where(u.any(axis=0))[0]
top, bot, left, right = r_[0], r_[-1], c_[0], c_[-1]
H = bot - top + 1

# 眼区:头顶下 3%~9%,横向中间 40%
ey0, ey1 = top + int(H * 0.03), top + int(H * 0.09)
ex0 = left + int((right - left) * 0.30)
ex1 = left + int((right - left) * 0.70)
eye = G[:, ey0:ey1, ex0:ex1]

print('=== %s : %d 帧,眼区 %dx%d ===' % (NAME, n, ey1 - ey0, ex1 - ex0))

# 1) 眼珠"朝向"代理量:眼区暗像素(瞳孔)的水平质心。
#    瞳孔比眼白/皮肤暗,取最暗的 15% 像素算质心,就能追出眼珠左右移动。
cx = []
for i in range(n):
    e = eye[i]
    thr = np.percentile(e, 15)
    m = e <= thr
    xs = np.where(m)[1]
    cx.append(xs.mean() if len(xs) else np.nan)
cx = np.array(cx)
print('\n--- 1) 瞳孔水平质心(越大越靠右) ---')
print('  全序列 min %.1f  max %.1f  跨度 %.1f px' % (np.nanmin(cx), np.nanmax(cx), np.nanmax(cx) - np.nanmin(cx)))
print('  首帧 %.1f  末帧 %.1f' % (cx[0], cx[-1]))
print('  逐帧序列(每5帧): %s' % np.round(cx[::5], 1).tolist())

# 2) 逐帧眼区变化
ed = np.array([np.abs(eye[i + 1] - eye[i]).mean() for i in range(n - 1)])
print('\n--- 2) 眼区逐帧变化 ---')
print('  中位 %.2f  最大 %.2f (帧%d→%d)' % (np.median(ed), ed.max(), ed.argmax(), ed.argmax() + 1))

# 3) 核心:每个候选窗口的**眼区接缝跳变**
print('\n--- 3) 眼区接缝跳变 seam/med (关键!>2 就看得出猛转) ---')
print('%-4s %-9s %-8s %-8s %-7s %s'
      % ('L', 'window', 'eyeSeam', 'eyeMed', 'ratio', '判定'))
rows = []
for L in (22, 23, 24, 44, 45, 46, 66, 79):
    for s in range(0, n - L + 1):
        e = s + L - 1
        seam = float(np.abs(eye[s] - eye[e]).mean())
        med = float(np.median([np.abs(eye[i + 1] - eye[i]).mean() for i in range(s, e)]))
        ratio = seam / max(med, 1e-6)
        # 瞳孔质心闭合度
        dcx = abs(cx[e] - cx[s]) if not (np.isnan(cx[e]) or np.isnan(cx[s])) else 99
        rows.append((ratio, seam, med, dcx, L, s, e))

# 打印当前实装的那个窗口 + 每个 L 的最优
cur = [r for r in rows if r[4] == 44 and r[5] == 16]
if cur:
    r = cur[0]
    print('%-4d [%2d:%2d]   %7.2f  %7.2f  %6.2f  ← **当前实装的 V2,就是它在跳**'
          % (r[4], r[5], r[6] + 1, r[1], r[2], r[0]))
print()
for L in (22, 23, 24, 44, 45, 46, 66, 79):
    sub = sorted([r for r in rows if r[4] == L])[:3]
    for r in sub:
        tag = '好' if r[0] < 1.5 else ('勉强' if r[0] < 2.5 else '会猛转')
        print('%-4d [%2d:%2d]   %7.2f  %7.2f  %6.2f  %s  瞳孔质心差 %.1fpx'
              % (r[4], r[5], r[6] + 1, r[1], r[2], r[0], tag, r[3]))
    print()
