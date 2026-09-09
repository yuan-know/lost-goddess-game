# -*- coding: utf-8 -*-
"""把**已经抠好透明**的序列帧(如 framepacker 导出的)对齐、缩放到 Unity 用的画布。

与 matte_frames.py 的分工:
  matte_frames.py  处理**带绿背景的原始帧** —— 它要负责抠像
  align_cutout.py  处理**已透明的帧** —— 只做对齐/缩放/边缘修整,不碰抠像

为什么不能把透明帧直接丢进 Unity(实测数据,青年行走 51 帧):
  1. 脚底行跨度 47px、最右跨度 167px —— 逐帧位置在飘,直接用会明显上下左右抖。
     解法同 matte_frames:全序列**统一裁切框 + 统一缩放**,绝不逐帧对齐
     (逐帧按各自 bbox 对齐反而会把肢端噪声翻译成整体平移,那才是抖动真凶)
  2. alpha 是**纯二值**的(实测半透明像素占比 0.00%,只有 0 和 255) ——
     没有羽化边,放大后就是硬锯齿。所以要人工补一圈软边。
"""
import os, sys, glob, json, argparse
try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RAW_DIR = os.path.join(ROOT, 'tools', 'ai_video', 'raw')
OUT_DIR = os.path.join(ROOT, 'tools', 'ai_video', 'final')

OUT_W, OUT_H = 180, 512
FEET_MARGIN, TOP_MARGIN = 2, 3

# 碎屑清理:小于此面积且与主体分离的连通块删掉(LANCZOS 会在肢端造碎块)
SPECK_MAX_AREA = 200

# 二值 alpha 补软边:向内 FEATHER px 做线性过渡。
# 1.0 太窄看不出效果,3 会啃掉细节;实测 1.5-2 最好。
FEATHER = 2.0

# 时间中值窗口,消除单帧突跳。行走动作变化快,所以只用 3(5 会削弱迈步幅度)。
TEMPORAL_WINDOW = 3


def soften(alpha):
    """给二值 alpha 补一圈羽化边。用距离变换实现:离轮廓越近越透明。"""
    op = alpha > 0.5
    if not op.any():
        return alpha
    # 内部到边界的距离。边界处=0,越深越大。
    d_in = ndimage.distance_transform_edt(op)
    # 只改最外 FEATHER 圈,内部保持完全不透明
    ramp = np.clip(d_in / FEATHER, 0.0, 1.0)
    return np.where(op, ramp, 0.0)


def content_box(alpha):
    op = alpha > 0.15
    if not op.any():
        return None
    ys, xs = np.where(op)
    return ys.min(), ys.max(), xs.min(), xs.max()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('name', help='raw 子目录名')
    ap.add_argument('--out', default=None)
    ap.add_argument('--prefix', default='walk')
    ap.add_argument('--glob', default='*.png')
    ap.add_argument('--start', type=int, default=0)
    ap.add_argument('--count', type=int, default=0)
    ap.add_argument('--no-feather', action='store_true')
    args = ap.parse_args()

    src = os.path.join(RAW_DIR, args.name)
    files = sorted(glob.glob(os.path.join(src, args.glob)))
    if not files:
        print(f'[错误] {src}/{args.glob} 没有文件')
        return 1
    if args.start or args.count:
        end = args.start + args.count if args.count else len(files)
        n0 = len(files)
        files = files[args.start:end]
        print(f'截取: 原 {n0} -> 取 [{args.start},{end}) 共 {len(files)} 帧')

    out_name = args.out or args.name
    dst = os.path.join(OUT_DIR, out_name)
    os.makedirs(dst, exist_ok=True)
    print(f'源: {src}  ({len(files)} 帧)')

    alphas, rgbs = [], []
    binary_cnt = 0
    for p in files:
        A = np.asarray(Image.open(p).convert('RGBA'))
        a = A[..., 3].astype(np.float64) / 255.0
        semi = ((a > 0.02) & (a < 0.98)).mean()
        if semi < 0.001:
            binary_cnt += 1
        rgbs.append(A[..., :3].astype(np.float64))
        alphas.append(a)
    print(f'二值 alpha 帧数: {binary_cnt}/{len(files)}'
          + ('  -> 需要补羽化边' if binary_cnt else ''))

    # 1) 补软边(二值时才有意义)
    if not args.no_feather and binary_cnt:
        alphas = [soften(a) for a in alphas]
        print(f'已补羽化边 (FEATHER={FEATHER}px)')

    # 2) 时间中值,消单帧突跳
    if len(alphas) >= TEMPORAL_WINDOW:
        n = len(alphas)
        h = TEMPORAL_WINDOW // 2
        orig = list(alphas)
        alphas = [np.median(np.stack([orig[(i + k) % n] for k in range(-h, h + 1)]), axis=0)
                  for i in range(n)]
        del orig
        print(f'时间中值: {TEMPORAL_WINDOW} 帧循环')

    # 3) 只留最大连通块(去水印/散点)
    for i, a in enumerate(alphas):
        op = a > 0.5
        lb, nb = ndimage.label(op)
        if nb > 1:
            sz = ndimage.sum(op, lb, range(1, nb + 1))
            main_id = int(np.argmax(sz)) + 1
            drop = op & (lb != main_id)
            if drop.any():
                a[drop] = 0.0

    boxes = [content_box(a) for a in alphas]
    keep = [(a, r, b) for a, r, b in zip(alphas, rgbs, boxes) if b is not None]
    if not keep:
        print('[错误] 全空')
        return 1
    alphas = [k[0] for k in keep]; rgbs = [k[1] for k in keep]
    b = np.array([k[2] for k in keep])

    # 4) 统一裁切框 + 统一缩放(抖动的正解,理由见文件头)
    PAD = 2
    gy0 = max(0, int(b[:, 0].min()) - PAD); gy1 = int(b[:, 1].max()) + PAD
    gx0 = max(0, int(b[:, 2].min()) - PAD); gx1 = int(b[:, 3].max()) + PAD
    gh, gw = gy1 - gy0 + 1, gx1 - gx0 + 1
    scale = min((OUT_H - FEET_MARGIN - TOP_MARGIN) / gh, (OUT_W - 4) / gw)
    nw = max(1, int(round(gw * scale))); nh = max(1, int(round(gh * scale)))
    px = (OUT_W - nw) // 2; py = (OUT_H - FEET_MARGIN) - nh
    print(f'统一裁切框 y{gy0}-{gy1} x{gx0}-{gx1} ({gw}x{gh})  scale={scale:.4f}')
    print(f'脚底钉在 y={OUT_H - FEET_MARGIN}')

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
        canvas.save(os.path.join(dst, f'{args.prefix}_{i:03d}.png'))

    with open(os.path.join(dst, '_align.json'), 'w', encoding='utf-8') as f:
        json.dump({'source': src, 'frames': len(alphas), 'canvas': [OUT_W, OUT_H],
                   'scale': scale, 'crop_box': [gx0, gy0, gx1, gy1],
                   'feet_row': OUT_H - FEET_MARGIN, 'feather': FEATHER,
                   'temporal_window': TEMPORAL_WINDOW}, f, ensure_ascii=False, indent=2)
    print(f'\n[完成] {len(alphas)} 帧 -> {dst}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
