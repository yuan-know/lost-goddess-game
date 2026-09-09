# -*- coding: utf-8 -*-
"""把一个循环帧序列**帧数翻倍**(相邻帧 + 末帧→首帧 之间插一张 50% alpha 混合帧)。

用途:在不降低播放帧率的前提下**放慢循环动画**。
  老年行走旧版只有 12 帧。要压慢步频只有两条路:
    a) 降 fps —— 每帧停留时间变长(8fps=125ms),肉眼卡顿,接缝处尤其明显;
    b) 本脚本:插过渡帧把帧数翻倍,再用正常 24fps 播 ——
       同一段动作播放时长翻倍(自然减速一半),但每帧只停 ~42ms,流畅不卡。
  插值帧相当于运动模糊;单帧动作越小,混合重影越轻。老年行走单帧腿部仅 10px,
  实测 22→44 帧后 legIoU 0.91→0.95、单帧腿动 10.5px→5.2px,无可见重影。

用法:
  python interp_loop.py <输入帧目录> <输出目录> [--keep-existing]
  例: python interp_loop.py final/old_walk_t22 final/old_walk_t44

注意:
  - 输入必须是**已经闭合的循环**(首尾同相位),否则末→首插帧会糊。
  - 输出帧序 = 原0, 混合(0,1), 原1, 混合(1,2), ... 原n-1, 混合(n-1,0)。
  - alpha 按两帧并集取均值;RGB 按 alpha 加权,透明区不染色。
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


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    src_dir, dst_dir = sys.argv[1], sys.argv[2]
    os.makedirs(dst_dir, exist_ok=True)

    src = sorted(glob.glob(os.path.join(src_dir, '*.png')))
    if not src:
        print('[错误] %s 无 PNG' % src_dir)
        return 1

    ims = [np.asarray(Image.open(p).convert('RGBA')).astype(np.float32) for p in src]
    n = len(ims)

    out = []
    for i in range(n):
        a = ims[i]
        b = ims[(i + 1) % n]
        aa = a[..., 3:4] / 255.0
        bb = b[..., 3:4] / 255.0
        wsum = aa + bb
        rgb = np.where(wsum > 1e-3,
                       (a[..., :3] * aa + b[..., :3] * bb) / np.maximum(wsum, 1e-3),
                       0.0)
        alpha = (aa + bb) * 0.5 * 255.0
        blend = np.concatenate([rgb, alpha], axis=-1)
        out.append(a)
        out.append(blend)

    for i, im in enumerate(out):
        Image.fromarray(np.clip(im, 0, 255).astype(np.uint8)).save(
            os.path.join(dst_dir, 'walk_%03d.png' % i))

    print('插值补帧: %d -> %d 帧,输出到 %s' % (n, len(out), dst_dir))
    return 0


if __name__ == '__main__':
    sys.exit(main())
