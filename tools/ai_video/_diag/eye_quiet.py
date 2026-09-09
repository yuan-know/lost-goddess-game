# -*- coding: utf-8 -*-
"""找眼珠最安静的连续区间,并检查是不是 fps 被我调快了。

用户 2026-09-08 第二次反馈:新版44帧"**结束的时候**眼珠还是转很快",
且强调"原视频眼珠转动是不快的"。

两个可能原因,都要查:
  A) 窗口里**局部**有眼珠猛动的帧段。当前窗口 [16:60] 含帧49→50 的 40.18 尖峰
     (全序列最大值,是中位 22.10 的 1.8 倍),位置在 33/44 = 75% 处 —— 正好是"结束时"。
  B) **fps 被调快了**。为了凑"1.38 步/秒"我把 44 帧配了 30fps。
     但源素材每步 22 帧,若原视频是 24fps,则原生步频只有 24/22 = 1.09 步/秒。
     我配 30fps = 比原视频快 1.25 倍,**眼珠自然跟着快 1.25 倍**。
     旧素材每步 13 帧,配 18fps 才是 1.38 —— 新素材帧更密,不该照抄旧步频。
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
legsl = slice(top + int(H * 0.70), bot + 1)

ey0, ey1 = top + int(H * 0.03), top + int(H * 0.09)
ex0 = left + int((right - left) * 0.30)
ex1 = left + int((right - left) * 0.70)
eye = G[:, ey0:ey1, ex0:ex1]

# 逐帧眼区变化(非循环,只看相邻)
ed = np.array([np.abs(eye[i + 1] - eye[i]).mean() for i in range(n - 1)])
med = np.median(ed)

print('=== %s : %d 帧 ===' % (NAME, n))
print('眼区逐帧变化 中位 %.2f' % med)
print('\n--- 每帧眼区变化(标 ! 的是 >1.5x 中位 = 眼珠猛动) ---')
for i in range(0, n - 1, 1):
    bar = '#' * int(ed[i] / 2)
    flag = ' !!!' if ed[i] > med * 1.5 else (' !' if ed[i] > med * 1.25 else '')
    if ed[i] > med * 1.25 or i % 10 == 0:
        print('  %2d→%2d  %5.2f %-22s%s' % (i, i + 1, ed[i], bar, flag))

hot = [i for i in range(n - 1) if ed[i] > med * 1.5]
print('\n猛动帧(>1.5x): %s' % hot)

# 找"最安静的连续区间":窗口内眼区变化最大值最小
print('\n--- 按"窗口内眼珠最大跳动"排序(这才是用户感知的指标) ---')


def iou(a, b):
    x, y = a > 0.5, b > 0.5
    return (x & y).sum() / max((x | y).sum(), 1)


rows = []
for L in (22, 23, 24, 44, 45, 46):
    for s in range(0, n - L + 1):
        e = s + L - 1
        inner = ed[s:e]                        # 窗口内部相邻帧
        seam = float(np.abs(eye[s] - eye[e]).mean())
        all_steps = np.append(inner, seam)     # 加上接缝这一"帧"
        eye_max = float(all_steps.max())
        eye_med = float(np.median(all_steps))
        leg = iou(A[e][legsl], A[s][legsl])
        rows.append((eye_max, eye_med, seam, leg, L, s, e))

print('%-4s %-9s %-8s %-8s %-7s %-8s %s'
      % ('L', 'window', 'eyeMAX', 'eyeMed', 'seam', 'legIoU', '判定'))
cur = [r for r in rows if r[4] == 44 and r[5] == 16][0]
print('%-4d [%2d:%2d]   %7.2f  %7.2f  %6.2f  %.4f  ← 当前实装 V2'
      % (cur[4], cur[5], cur[6] + 1, cur[0], cur[1], cur[2], cur[3]))
print()
for L in (22, 23, 24, 44, 45, 46):
    sub = sorted([r for r in rows if r[4] == L])[:3]
    for r in sub:
        tag = '眼睛安静' if r[0] < med * 1.35 else '有跳动'
        print('%-4d [%2d:%2d]   %7.2f  %7.2f  %6.2f  %.4f  %s'
              % (r[4], r[5], r[6] + 1, r[0], r[1], r[2], r[3], tag))
    print()
