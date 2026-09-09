# -*- coding: utf-8 -*-
"""诊断 raw/young_walk_v3(新版青年行走):淡入 / 步态周期 / 形状漂移 / 眼珠区运动。

用法(在 tools/ai_video 下跑):  python _diag/diag_yw3.py [raw子目录名]

四项诊断都是被具体 bug 逼出来的,见 memory 2026-09-07 记录:
  1) 淡入   —— AI 视频开头几帧偏暗,循环时会闪
  2) 步态周期 —— 用**腿部区域自相似矩阵**,不用"腿展开量自相关"(后者读数是噪声)
  3) 形状漂移 —— 身高/头宽单向变化 = 镜头推拉或角色转身,首尾天生不闭合
  4) 眼珠区   —— 本次新增:用户反馈旧版"眼珠转动太快",量化头部上半区的逐帧变化
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
SRC = os.path.join(HERE, 'raw', NAME)

files = sorted(glob.glob(os.path.join(SRC, '*.png')))
if not files:
    print('[错误] %s 下没有 png' % SRC)
    sys.exit(1)

A, RGB = [], []
for p in files:
    im = np.asarray(Image.open(p).convert('RGBA'))
    A.append(im[..., 3].astype(np.float32) / 255.0)
    RGB.append(im[..., :3].astype(np.float32))
A = np.stack(A)
n = len(A)
print('=== %s : %d 帧 %dx%d ===' % (NAME, n, A.shape[2], A.shape[1]))

# 全序列并集包围盒,拿到身体纵向范围
u = A.max(axis=0) > 0.5
rows = np.where(u.any(axis=1))[0]
cols = np.where(u.any(axis=0))[0]
top, bot, left, right = rows[0], rows[-1], cols[0], cols[-1]
H = bot - top + 1
print('并集包围盒: y %d-%d (高 %d)  x %d-%d (宽 %d)' % (top, bot, H, left, right, right - left + 1))

# ---------- 1) 淡入 / 亮度起伏 ----------
lum = np.array([RGB[i][A[i] > 0.9].mean() if (A[i] > 0.9).any() else 0.0 for i in range(n)])
med = float(np.median(lum))
print('\n--- 1) 亮度(实心区均值) ---')
print('中位 %.2f  跨度 %.2f  最暗帧 #%d=%.2f  最亮帧 #%d=%.2f'
      % (med, lum.max() - lum.min(), int(lum.argmin()), lum.min(), int(lum.argmax()), lum.max()))
fade = [i for i in range(min(12, n)) if lum[i] < med - 1.0]
print('前 12 帧里低于中位 1.0 的(疑似淡入): %s' % (fade if fade else '无'))
print('  前 10 帧亮度: %s' % np.round(lum[:10], 2).tolist())

# ---------- 2) 步态周期(腿部自相似矩阵) ----------
leg = A[:, top + int(H * 0.70): bot + 1, :].reshape(n, -1)
D = np.zeros((n, n), np.float64)
for i in range(n):
    D[i] = np.abs(leg - leg[i]).sum(axis=1)
D /= D.max()

print('\n--- 2) 步态周期(腿部自相似) ---')
# 按 lag 平均距离:真周期处应出现明显低谷
lagmean = np.zeros(n)
for lag in range(1, n):
    lagmean[lag] = np.mean([D[i, i + lag] for i in range(n - lag)])
cands = []
for lag in range(4, n - 3):
    if lagmean[lag] < lagmean[lag - 1] and lagmean[lag] < lagmean[lag + 1]:
        cands.append((lagmean[lag], lag))
cands.sort()
print('lag 平均距离的局部低谷(越小越像一个整周期),前 6 个:')
for v, lag in cands[:6]:
    print('  lag=%2d  平均距离 %.4f' % (lag, v))

# ---------- 3) 形状漂移 ----------
print('\n--- 3) 形状漂移(身高 / 头宽) ---')
hs, hw = [], []
for i in range(n):
    m = A[i] > 0.5
    r = np.where(m.any(axis=1))[0]
    if len(r) == 0:
        hs.append(0); hw.append(0); continue
    t, b = r[0], r[-1]
    hs.append(b - t + 1)
    band = m[t: t + max(1, int((b - t + 1) * 0.10))]
    c = np.where(band.any(axis=0))[0]
    hw.append((c[-1] - c[0] + 1) if len(c) else 0)
hs, hw = np.array(hs, float), np.array(hw, float)
print('身高 首%.0f 末%.0f  变化 %+.2f%%   (min %.0f max %.0f)'
      % (hs[0], hs[-1], 100 * (hs[-1] / hs[0] - 1), hs.min(), hs.max()))
print('头宽 首%.0f 末%.0f  变化 %+.2f%%   (min %.0f max %.0f)'
      % (hw[0], hw[-1], 100 * (hw[-1] / hw[0] - 1), hw.min(), hw.max()))

# ---------- 4) 眼珠区运动(本次新增) ----------
# 头部上 12% 的中间横向 60%,基本就是脸/眼睛区域。
# 眼珠是小面积高对比结构,alpha 上看不见 —— 必须看 RGB 的逐帧变化。
print('\n--- 4) 头部/眼珠区逐帧变化(用户反馈旧版眼珠转太快) ---')
hy0, hy1 = top, top + max(1, int(H * 0.12))
hx0 = left + int((right - left) * 0.20)
hx1 = left + int((right - left) * 0.80)
face = np.stack([RGB[i][hy0:hy1, hx0:hx1].mean(axis=2) for i in range(n)])
fd = np.array([np.abs(face[i + 1] - face[i]).mean() for i in range(n - 1)])
body = np.stack([RGB[i][top:bot + 1, left:right + 1].mean(axis=2) for i in range(n)])
bd = np.array([np.abs(body[i + 1] - body[i]).mean() for i in range(n - 1)])
print('脸区 %dx%d  逐帧变化 中位 %.3f  最大 %.3f (在帧 %d->%d)'
      % (hy1 - hy0, hx1 - hx0, np.median(fd), fd.max(), int(fd.argmax()), int(fd.argmax()) + 1))
print('全身逐帧变化 中位 %.3f' % np.median(bd))
print('脸/全身 变化比 %.3f  (比值越高说明脸部动得比身体还猛 = 眼珠乱转)'
      % (np.median(fd) / max(np.median(bd), 1e-6)))
print('  脸区逐帧变化序列: %s' % np.round(fd, 2).tolist())
