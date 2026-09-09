# -*- coding: utf-8 -*-
"""v2:统一比例缩放 + 运动量重采样。

相对 v1 的两个变化,都是 v1 的实测缺陷逼出来的:

1) 统一比例(治"不同形态大小不一致")
   v1 让每套动画各自"填满画布高度",于是矮的角色被放得更大 ——
   实测 scale:青年待机 0.4122 / 青年行走 0.3868 / 老年待机 0.3577 / 老年行走 0.3340。
   四套单独看都正常,并排就发现老年跟青年差不多高,失去了年龄感。
   正解:全形态共用一个 scale(由 --scale 指定),让世界坐标里的身高差
   完全由源素材的实际像素身高决定。

2) 运动量重采样(治"走路中间卡顿一下")
   即梦生成的行走节奏并不均匀 —— 实测源素材相邻帧变化量在 0.29x 到 4.50x
   之间摆动(相对中位数)。播放时就是"走两步顿一下,然后猛地跨一大步"。
   正解:把运动看成一条曲线,按**累积运动量**等距重采样。
   运动快的地方帧被跳过,慢的地方帧被保留 —— 于是每帧的运动量趋于相等。
   实测老年行走:节奏变异系数 cv 0.642 -> 0.105,最猛一跳 3.69x -> 1.19x。
   用最近邻取帧(不做插帧),所以每一帧都是源素材里真实存在的画面,不会糊。
   ⚠️ 必须检查"重复帧数"= 0:如果相邻两帧取到了同一张源帧,那才是真卡顿。
"""
import os
import sys
import glob
import json
import argparse

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RAW_DIR = os.path.join(ROOT, 'tools', 'ai_video', 'raw')
OUT_DIR = os.path.join(ROOT, 'tools', 'ai_video', 'final')

OUT_W, OUT_H = 260, 512   # 宽度 260:统一 scale 后青年待机的臂展需要 243px
FEET_MARGIN = 2

SPECK_MAX_AREA = 200
FEATHER = 2.0
TEMPORAL_WINDOW = 3

# 细长结构抢救判据(拐杖/飘带/武器杆): 长边>=60px 且长宽比>=3.0
# 实测(源分辨率)被中值删掉的块里,拐杖类长宽比 5.8-42.0 且长边>=153px,
# 而轮廓抖动块长宽比只有 1.5-3.0 —— 分界干净。judge 用长宽比而非面积:
# 拐杖杆 501x40=7245px 面积并不小,按面积过滤会漏。
CANE_MIN_LEN = 150
CANE_MIN_ELONG = 5.0

# 亮度归一化增益上限(1.15 = 最多 ±15%%)。夹住是为了不让某个异常帧被拉爆。
LUMA_MAX_GAIN = 1.15


def soften(alpha):
    """给二值 alpha 补一圈羽化边(framepacker 导出的 alpha 只有 0 和 255)。"""
    op = alpha > 0.5
    if not op.any():
        return alpha
    d_in = ndimage.distance_transform_edt(op)
    return np.where(op, np.clip(d_in / FEATHER, 0.0, 1.0), 0.0)


def content_box(alpha, thr=0.15):
    op = alpha > thr
    if not op.any():
        return None
    ys, xs = np.where(op)
    return ys.min(), ys.max(), xs.min(), xs.max()


def resample_by_motion(masks, K):
    """按累积运动量把 n 帧重采样到 K 帧。返回选中的源帧下标列表。

    masks: 每帧的二值剪影(bool 数组列表)
    """
    n = len(masks)
    steps = [float(np.abs(masks[i].astype(np.float32) - masks[i + 1]).sum())
             for i in range(n - 1)]
    # 末帧到首帧也算一步(循环动画),否则接缝处的运动量会被忽略
    steps.append(float(np.abs(masks[-1].astype(np.float32) - masks[0]).sum()))
    cum = np.concatenate([[0.0], np.cumsum(steps)])
    total = cum[-1]
    if total <= 0:
        return list(range(min(K, n)))
    picked = []
    for t in np.linspace(0.0, total, K + 1)[:-1]:
        j = min(int(np.searchsorted(cum, t, side='right')) - 1, n - 1)
        j = max(j, 0)
        seg = cum[j + 1] - cum[j]
        frac = (t - cum[j]) / seg if seg > 0 else 0.0
        picked.append(j if frac < 0.5 else (j + 1) % n)
    return picked


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('name', help='raw 子目录名')
    ap.add_argument('--out', default=None)
    ap.add_argument('--prefix', default='walk')
    ap.add_argument('--glob', default='*.png')
    ap.add_argument('--start', type=int, default=0)
    ap.add_argument('--count', type=int, default=0)
    ap.add_argument('--no-feather', action='store_true')
    ap.add_argument('--resample', type=int, default=0,
                    help='按累积运动量重采样到 K 帧(0=不做)')
    ap.add_argument('--scale', type=float, default=0.0,
                    help='强制 scale(全形态用同一个值才能保住身高比例);0=自动填满画布')
    ap.add_argument('--no-luma', action='store_true',
                    help='关闭亮度归一化')
    ap.add_argument('--no-median', action='store_true',
                    help='强制关闭时间中值(重采样时已自动关闭)')
    ap.add_argument('--pingpong', action='store_true',
                    help='正放一遍再倒放一遍(去掉两端重复帧)。用于**单向漂移**素材:'
                         '角色全程缓慢前倾/转身,首尾形态天生不一致,任何切窗都会在接缝处跳。'
                         '老年待机实测:70帧全窗接缝跳变 5.19x base,而"头宽差0"的窗口接缝更差'
                         '(10.2-10.7x) —— 接缝闭合与头宽闭合互斥,选窗无解。'
                         '乒乓后接缝退化成一个普通相邻帧步长(2.09x),头宽必然回到起点。'
                         '代价:动作会"倒着走一遍",只适合待机/呼吸这种无方向性的动作,'
                         '**绝对不要用在行走上**(会变成前后横移)。')
    args = ap.parse_args()

    src = os.path.join(RAW_DIR, args.name)
    files = sorted(glob.glob(os.path.join(src, args.glob)))
    if not files:
        print('[错误] %s/%s 没有文件' % (src, args.glob))
        return 1
    if args.start or args.count:
        end = args.start + args.count if args.count else len(files)
        n0 = len(files)
        files = files[args.start:end]
        print('截取: 原 %d -> [%d,%d) 共 %d 帧' % (n0, args.start, end, len(files)))

    out_name = args.out or args.name
    dst = os.path.join(OUT_DIR, out_name)
    os.makedirs(dst, exist_ok=True)
    print('源: %s  (%d 帧)' % (src, len(files)))

    alphas = []
    rgbs = []
    for p in files:
        A = np.asarray(Image.open(p).convert('RGBA'))
        alphas.append(A[..., 3].astype(np.float64) / 255.0)
        rgbs.append(A[..., :3].astype(np.float64))

    binary_cnt = sum(1 for a in alphas if ((a > 0.02) & (a < 0.98)).mean() < 0.001)
    print('二值 alpha 帧数: %d/%d%s' % (binary_cnt, len(alphas),
                                    '  -> 补羽化边' if binary_cnt else ''))

    # 乒乓要在重采样之前:先把序列变成真正闭环,再让重采样在闭环上均匀取点。
    # 顺序反了的话重采样会按单向序列算运动曲线,拼接后节奏还是偏的。
    if args.pingpong and len(alphas) >= 5:
        n0 = len(alphas)
        # 折返处跳 2 帧,不是 1 帧。原因:折返点两侧是**镜像对称**的,
        # 后面的 3 帧时间中值会把这两帧磨成几乎一样 —— 实测 SKIP=1 时
        # 源帧层面最静步长还有 0.681x(健康),但成片掉到 0.067x,肉眼是定格。
        # 跳 2 帧拉开对称性,中值就磨不平了。
        skip = 2
        order = list(range(n0)) + list(range(n0 - 1 - skip, skip - 1, -1))
        alphas = [alphas[i] for i in order]
        rgbs = [rgbs[i] for i in order]
        print('乒乓拼接: %d -> %d 帧 (正放+倒放,折返处跳 %d 帧)' % (n0, len(alphas), skip))

    # 重采样要在羽化之前做:羽化只是外观处理,采样依据应该是原始剪影
    if args.resample > 0 and len(alphas) > args.resample:
        K = args.resample
        masks = [a > 0.5 for a in alphas]
        picked = resample_by_motion(masks, K)
        dup = sum(1 for i in range(K) if picked[i] == picked[(i + 1) % K])
        print('重采样: %d -> %d 帧(累积运动量最近邻)' % (len(alphas), K))
        print('  选中源帧下标: %s' % picked)
        print('  相邻重复帧: %d  %s' % (dup, '<-- 必须是0,否则会真卡顿' if dup else '(OK)'))
        alphas = [alphas[i] for i in picked]
        rgbs = [rgbs[i] for i in picked]

    if not args.no_feather and binary_cnt:
        alphas = [soften(a) for a in alphas]
        print('已补羽化边 (FEATHER=%.1fpx)' % FEATHER)

    # ⚠️ 时间中值和"细长快速运动的物体"根本冲突,但也不能整个关掉。
    #    冲突: 中值规则是"3帧里至少2帧有,才保留"。老年行走的拐杖杆
    #      (501x40px,长宽比12.5)摆动时同一位置只在1帧出现 —— 实测被删块在
    #      两个邻帧的重叠都是 **0px** —— 于是整根被判成噪声抹掉。拐杖区损失率
    #      在 5.87%~70.54% 之间剧烈波动,表现就是"拐杖间歇性消失一部分"。
    #      重采样放大这个问题:23->12 帧使帧间运动量翻倍,位移超过拐杖自身宽度。
    #      (v3 的 31->14 压缩率低,所以当时没暴露。)
    #    但整个关掉也不行:实测全关后孤立突跳 205 -> 12284px,散布全身、碎成
    #      20-98 块 —— 那是轮廓边缘逐帧抖动,中值原本在压它;腿部接缝 IoU 也从
    #      0.9430 掉到 0.8961。
    #    正解: 只保护"细长且邻帧无重叠"的结构,其余照常滤波。
    do_median = (TEMPORAL_WINDOW > 0 and len(alphas) >= TEMPORAL_WINDOW
                 and not args.no_median)
    if do_median:
        n = len(alphas)
        h = TEMPORAL_WINDOW // 2
        orig = list(alphas)
        filt = [np.median(np.stack([orig[(i + k) % n] for k in range(-h, h + 1)]), axis=0)
                for i in range(n)]
        # 把中值"删掉"的部分挑出来,凡是细长结构就还回去(拐杖、飘带、武器杆)。
        # 判据用长宽比而非面积:拐杖杆 501x40 面积不小,按面积过滤会漏。
        rescued = 0
        for i in range(n):
            lost = (orig[i] > 0.5) & ~(filt[i] > 0.5)
            if not lost.any():
                continue
            lb, nb = ndimage.label(lost)
            for k in range(1, nb + 1):
                m = lb == k
                ys, xs = np.where(m)
                hh, ww = ys.max() - ys.min() + 1, xs.max() - xs.min() + 1
                elong = max(hh, ww) / max(min(hh, ww), 1)
                if max(hh, ww) >= CANE_MIN_LEN and elong >= CANE_MIN_ELONG:
                    filt[i][m] = orig[i][m]
                    rescued += int(m.sum())
        alphas = filt
        del orig
        print('时间中值: %d 帧循环' % TEMPORAL_WINDOW)
        if rescued:
            print('  已抢救细长结构(拐杖等) %d px  [长边>=%d 且长宽比>=%.1f]'
                  % (rescued, CANE_MIN_LEN, CANE_MIN_ELONG))

    for i, a in enumerate(alphas):
        op = a > 0.5
        lb, nb = ndimage.label(op)
        if nb > 1:
            sz = ndimage.sum(op, lb, range(1, nb + 1))
            mid = int(np.argmax(sz)) + 1
            drop = op & (lb != mid)
            if drop.any():
                alphas[i][drop] = 0.0

    boxes = [content_box(a) for a in alphas]
    keep = [(a, r, bx) for a, r, bx in zip(alphas, rgbs, boxes) if bx is not None]
    if not keep:
        print('[错误] 全空')
        return 1
    alphas = [k[0] for k in keep]
    rgbs = [k[1] for k in keep]
    b = np.array([k[2] for k in keep])

    # 亮度归一化: 把每帧前景平均亮度拉到全序列中位数。
    # 为什么需要:即梦生成的视频**开头有淡入**,而且整段亮度还会缓慢起伏。
    #   实测老年待机源素材:帧0 亮度 87.80 是全序列最低(中位 92.41,低 4.60),
    #   帧1 90.83、帧2 91.72 —— 前3帧是淡入;此外帧6-9 和 64-69 偏亮 +0.7~1.3,
    #   中段 20-50 偏暗,整体是"两头亮中间暗"的起伏。
    #   乒乓正好把帧0放在循环接缝上,于是每轮循环闪一下 ——
    #   成片实测接缝亮度跳变 4.047,是中位跳变 0.171 的 **23.7 倍**。
    # 只按 alpha>0.9 的实心区算均值:边缘羽化像素的 RGB 被背景污染,会带偏统计。
    if not args.no_luma:
        lums = []
        for a, r in zip(alphas, rgbs):
            m = a > 0.9
            lums.append(float(r[m].mean()) if m.any() else 0.0)
        tgt = float(np.median(lums))
        adj = []
        for i in range(len(rgbs)):
            if lums[i] <= 1e-6:
                continue
            g = tgt / lums[i]
            g = min(max(g, 1.0 / LUMA_MAX_GAIN), LUMA_MAX_GAIN)   # 夹住,防止极端帧被拉爆
            rgbs[i] = np.clip(rgbs[i] * g, 0.0, 255.0)
            adj.append(abs(g - 1.0))
        print('亮度归一化: 目标 %.2f  原跨度 %.2f  最大增益 %+.2f%%'
              % (tgt, max(lums) - min(lums), 100.0 * max(adj) if adj else 0.0))

    PAD = 2
    gy0 = max(0, int(b[:, 0].min()) - PAD)
    gy1 = int(b[:, 1].max()) + PAD
    gx0 = max(0, int(b[:, 2].min()) - PAD)
    gx1 = int(b[:, 3].max()) + PAD
    gh, gw = gy1 - gy0 + 1, gx1 - gx0 + 1

    fit_scale = min((OUT_H - FEET_MARGIN) / gh, (OUT_W - 4) / gw)
    if args.scale > 0:
        scale = args.scale
        if scale > fit_scale:
            print('[警告] 指定 scale=%.4f 超过画布能装下的 %.4f,已改用后者(会破坏比例一致)'
                  % (scale, fit_scale))
            scale = fit_scale
    else:
        scale = fit_scale

    nw = max(1, int(round(gw * scale)))
    nh = max(1, int(round(gh * scale)))
    px = (OUT_W - nw) // 2
    py = (OUT_H - FEET_MARGIN) - nh
    print('统一裁切框 y%d-%d x%d-%d (%dx%d)' % (gy0, gy1, gx0, gx1, gw, gh))
    print('scale=%.4f (自动填满会是 %.4f)  输出角色 %dx%d px' % (scale, fit_scale, nw, nh))
    print('脚底钉 y=%d,头顶在 y=%d' % (OUT_H - FEET_MARGIN, py))

    for i, (a, rgb) in enumerate(zip(alphas, rgbs)):
        rgba = np.dstack([rgb, a * 255.0]).astype(np.uint8)
        crop = rgba[gy0:gy1 + 1, gx0:gx1 + 1]
        im = Image.fromarray(crop, 'RGBA').resize((nw, nh), Image.LANCZOS)
        arr = np.array(im)
        op = arr[..., 3] > 128
        if op.any():
            lb, nb = ndimage.label(op)
            if nb > 1:
                sz = ndimage.sum(op, lb, range(1, nb + 1))
                mid = int(np.argmax(sz)) + 1
                for k in range(1, nb + 1):
                    if k != mid and sz[k - 1] < SPECK_MAX_AREA:
                        arr[..., 3][lb == k] = 0
                im = Image.fromarray(arr, 'RGBA')
        canvas = Image.new('RGBA', (OUT_W, OUT_H), (0, 0, 0, 0))
        canvas.alpha_composite(im, (px, py))
        canvas.save(os.path.join(dst, '%s_%03d.png' % (args.prefix, i)))

    with open(os.path.join(dst, '_align.json'), 'w', encoding='utf-8') as f:
        json.dump({'source': src, 'frames': len(alphas), 'canvas': [OUT_W, OUT_H],
                   'scale': scale, 'fit_scale': fit_scale,
                   'crop_box': [gx0, gy0, gx1, gy1],
                   'char_px': [nw, nh], 'paste': [px, py],
                   'feet_row': OUT_H - FEET_MARGIN, 'feather': FEATHER,
                   'temporal_window': TEMPORAL_WINDOW,
                   'resample_target': args.resample,
                   'pingpong': bool(args.pingpong)},
                  f, ensure_ascii=False, indent=2)
    print('\n[完成] %d 帧 -> %s' % (len(alphas), dst))
    return 0


if __name__ == '__main__':
    sys.exit(main())
