# -*- coding: utf-8 -*-
"""按 SSM 周期扫窗,并把"抽搐"作为一等评分项。

背景(2026-09-08 二次翻车):
  我用"腿展开量波峰间距"算周期,得出 35 帧,做出来用户反馈**抽搐**。
  而 diag_ssm.py 给的真周期是 **lag≈22**(帧0-45 最佳匹配集中在 20-24)。
  35 帧 = 1.6 个周期 → 循环时腿停在半个摆动上 = 抽搐。
  记忆 2026-09-07 第 3 条坑写得很清楚:"腿展开量的自相关不可靠,读数是噪声,
  必须用腿部区域自相似矩阵" —— 我明知故犯了一次。

本脚本的评分与旧 scan 的区别:
  旧 scan 主要看 legIoU(接缝闭合),但**闭合好 ≠ 不抽搐**。
  抽搐的直接来源是**节奏不匀**:某两帧之间几乎不动(定格),下一对突然跳很大。
  所以这里把三项分开报,不合成一个分:
    legIoU / fullIoU  接缝闭合(循环回去那一下顺不顺)
    cv                步长标准差/均值(整体匀不匀)
    minR / maxR       最静步长、最大步长 / 中位步长(**抽搐看这两个**)
  经验阈值(来自已验证 OK 的六套):minR > 0.35 且 maxR < 2.5 才算不抽搐。
  之前 35 帧那版是 minR 0.25 / maxR 4.03 —— 数据早就报警了,我没当回事。

用法:
  python _diag/scan_cycle.py young_walk_v4            # 默认扫 22 和 44
  python _diag/scan_cycle.py young_walk_v4 20 26      # 指定长度范围
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
NAME = sys.argv[1] if len(sys.argv) > 1 else 'young_walk_v4'

if len(sys.argv) > 3:
    LENS = list(range(int(sys.argv[2]), int(sys.argv[3]) + 1))
else:
    # 单周期(22)与双周期(44)各扫一段容差
    LENS = [20, 21, 22, 23, 24, 42, 43, 44, 45, 46]

files = sorted(glob.glob(os.path.join(HERE, 'raw', NAME, '*.png')))
A = []
for p in files:
    im = np.asarray(Image.open(p).convert('RGBA'))
    A.append(im[..., 3].astype(np.float32) / 255.0)
A = np.stack(A)
n = len(A)

u = A.max(axis=0) > 0.5
rows = np.where(u.any(axis=1))[0]
top, bot = rows[0], rows[-1]
H = bot - top + 1
legs = slice(top + int(H * 0.70), bot + 1)


def iou(a, b):
    a = a > 0.5
    b = b > 0.5
    un = (a | b).sum()
    return (a & b).sum() / un if un else 1.0


print('=== %s : %d 帧 ===' % (NAME, n))
print('腿部区域 y[%d:%d] (身高 %d)' % (legs.start, legs.stop, H))
print()
print('%-11s %7s %7s %6s %6s %6s  %s' % (
    'window', 'legIoU', 'fullIoU', 'cv', 'minR', 'maxR', '抽搐判定'))

rows_out = []
for L in LENS:
    for s in range(0, n - L + 1):
        e = s + L
        W = A[s:e]
        # 循环步长:包含末帧→首帧那一步,这才是真实播放时的步长序列
        st = np.array([np.abs(W[i] - W[(i + 1) % L]).sum() for i in range(L)])
        med = np.median(st)
        if med <= 0:
            continue
        minR = st.min() / med
        maxR = st.max() / med
        cv = st.std() / st.mean()
        li = iou(W[-1][legs], W[0][legs])
        fi = iou(W[-1], W[0])

        smooth = (minR > 0.35) and (maxR < 2.5)
        verdict = 'OK' if (smooth and li > 0.95) else (
            '抽搐' if not smooth else '接缝差')
        rows_out.append(dict(L=L, s=s, e=e, li=li, fi=fi, cv=cv,
                             minR=minR, maxR=maxR, smooth=smooth,
                             verdict=verdict))

# 先按"不抽搐"分组,再按接缝闭合排序 —— 抽搐是硬否决项,不参与加权
ok = [r for r in rows_out if r['smooth']]
bad = [r for r in rows_out if not r['smooth']]
ok.sort(key=lambda r: -(r['li'] * 0.6 + r['fi'] * 0.4))

print('--- 不抽搐的窗口(minR>0.35 且 maxR<2.5),按接缝闭合排序 ---')
if not ok:
    print('  【无】所有窗口都抽搐 —— 说明源素材节奏本身有定格/突跳,')
    print('        需要按累积运动量重采样(--resample)来匀化节奏。')
for r in ok[:15]:
    print('[%2d:%2d]%4s %7.4f %7.4f %6.3f %6.2f %6.2f  %s' % (
        r['s'], r['e'], 'L%d' % r['L'], r['li'], r['fi'], r['cv'],
        r['minR'], r['maxR'], r['verdict']))

print()
print('--- 抽搐最轻的 8 个(供参考,仍不建议直接用) ---')
bad.sort(key=lambda r: (abs(r['maxR'] - 1) + abs(1 - r['minR'])))
for r in bad[:8]:
    print('[%2d:%2d]%4s %7.4f %7.4f %6.3f %6.2f %6.2f  %s' % (
        r['s'], r['e'], 'L%d' % r['L'], r['li'], r['fi'], r['cv'],
        r['minR'], r['maxR'], r['verdict']))
