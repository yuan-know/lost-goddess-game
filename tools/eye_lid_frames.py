# -*- coding: utf-8 -*-
"""
黄铜眼「眼睑开合」序列帧处理器  (tools/eye_lid_frames.py)

输入
----
tools/_ai_eye_lid_frames/action_001.png ... action_071.png   (AI 视频抽帧, 480x480 RGBA)
    一整只黄铜眼(外壳 + 眼睑 + 眼球)的开合循环, 实测开合度(瞳孔区亮度)曲线:
      帧 1~17  全闭(瞳孔区被眼睑盖住 -> 亮)   ~124..130
      帧 19~29 睁开中                         130 -> 38
      帧 29~51 全开(瞳孔区是深色眼球 -> 暗)   ~40
      帧 53~71 再闭合                          38 -> 131
    => 取帧 1..39 作为「闭 -> 睁」驱动段, 按开合度均匀抽帧。

输出
----
Assets/_Project/Resources/Scenes/ChaseCorridor/EyeLid/eye_lid_XX.png  (525x380, 与
    bg_eye_shell.png 同一裁剪框/同一 anchor, PPU100)

    每张 = 该帧的「眼睑层」: 原始帧 但按亮度分割 alpha ——
      亮像素(黄铜眼睑 / 外壳) -> 不透明(遮住下层瞳孔)
      暗像素(眼窝 / 眼球)      -> 透明(露出下层 shell + 可动的瞳孔球)
    所以闭眼时整只眼被眼睑盖住(瞳孔看不见), 睁眼时中间透出瞳孔(仍能跟随光柱滚动)。

为什么这样切
------------
1. 原始帧是「一整只眼」, 直接播会盖掉瞳孔跟随光柱的滚动 —— 必须抠出开口。
2. 逐列梯度法识别「眼睑内缘」在这批 AI 帧上不稳(眼睑内侧有大片暗色凹陷/污渍,
   梯度跃变会被误当成内缘; 全开帧眼窝很深, 跃变后亮度甚至不到 100)。
   实测「瞳孔区亮度」能干净地区分闭/开(闭 ~130 / 开 ~40, 差 3 倍), 于是改用
   亮度 ramp 做 alpha: 物理上也自洽 —— 亮的就是抬起来的眼睑, 暗的就是露出来的眼窝。
3. 瞳孔滚到眼角时仍会被开口外的亮像素压住(与 bg_eye_lid 同一目的)。

几何口径(实测)
------------
shell(525x380) alpha bbox x 40..485  y 40..340
帧(480x480)  帧1   alpha bbox x 42..441  y 106..375
=> 纯缩放 s = 445/399 = 1.11529, 无旋转;
   crop_x = 40 + (frame_x - 42) * s
   crop_y = 40 + (frame_y - 106) * s
帧内眼睛中心 (241.5, 240.5) 恰好映射到裁剪框中心 (262.5, 190) = shell 的几何中心。

用法
----
python tools/eye_lid_frames.py --analyze      # 只打印开合度曲线
python tools/eye_lid_frames.py --keep 20      # 抽 20 帧导出
python tools/eye_lid_frames.py --preview      # 额外输出合成预览图(需 docs/千眼回廊)
"""

import os
import sys
import glob
import numpy as np
from PIL import Image

ROOT  = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC   = os.path.join(ROOT, 'tools', '_ai_eye_lid_frames')
BASE  = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Scenes', 'ChaseCorridor')
OUT   = os.path.join(BASE, 'EyeLid')
DOCS  = os.path.join(ROOT, 'docs', '千眼回廊')

# ── 几何(实测, 见文件头) ──
S        = 445.0 / 399.0        # 帧 -> 裁剪框 缩放
FX0, FY0 = 42.0, 106.0          # 帧基准 bbox 左上(取全闭帧)
CX0, CY0 = 40.0, 40.0           # shell bbox 左上
CROP_W, CROP_H = 525, 380

# ── 亮度分割阈值(帧坐标系 0..255) ──
#   实测: 眼窝/眼球 30~70, 眼睑黄铜 130~215, 过渡带 70~130
LID_LO, LID_HI = 78.0, 128.0

# ── 开合度采样 ──
DRIVE_FIRST, DRIVE_LAST = 1, 39   # 「闭 -> 睁」驱动段(1-based, 含端点)


def frame_to_crop(fx, fy):
    return CX0 + (fx - FX0) * S, CY0 + (fy - FY0) * S


def openness(gray):
    """开合度 0..1: 用「瞳孔可能出现区」的亮度反推(闭=亮=1, 开=暗=0)"""
    cx, cy, r = 232.5, 226.6, 34.0        # 帧坐标系
    y0, y1 = int(cy - r), int(cy + r) + 1
    x0, x1 = int(cx - r), int(cx + r) + 1
    sub = gray[y0:y1, x0:x1]
    yy, xx = np.mgrid[y0:y1, x0:x1]
    disk = ((xx - cx) ** 2 + (yy - cy) ** 2) <= r * r
    return float(sub[disk].mean())


def lid_alpha(gray):
    """亮度 ramp: 暗=透明(露出瞳孔), 亮=不透明(眼睑遮住)"""
    t = np.clip((gray - LID_LO) / (LID_HI - LID_LO), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)        # smoothstep


def resample_to_crop(rgba):
    """帧(480x480) -> 裁剪框(525x380), 与 shell 同格(BaseCenter 对齐)"""
    pil = Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8), 'RGBA')
    nw, nh = int(round(480 * S)), int(round(480 * S))
    pil = pil.resize((nw, nh), Image.BILINEAR)
    ox = int(round(CX0 - FX0 * S))
    oy = int(round(CY0 - FY0 * S))
    canvas = Image.new('RGBA', (CROP_W, CROP_H), (0, 0, 0, 0))
    canvas.alpha_composite(pil, (ox, oy))
    return np.asarray(canvas, dtype=np.float32)


META_TPL = os.path.join(BASE, 'bg_eye_lid.png.meta')


def write_meta(png_path, name):
    """照抄 bg_eye_lid.png.meta 的导入设置(textureType=8 Sprite / alignment=7 / PPU100),
    guid 由文件名 md5 派生 → 可复现, 不随重跑变化。"""
    import hashlib
    import re
    with open(META_TPL, 'r', encoding='utf-8') as f:
        tpl = f.read()
    g = hashlib.md5(('LostGoddess/EyeLid/' + name).encode('utf-8')).hexdigest()
    out = re.sub(r'^guid: .*$', 'guid: ' + g, tpl, count=1, flags=re.M)
    with open(png_path + '.meta', 'w', encoding='utf-8', newline='\n') as f:
        f.write(out)


def build(frame_path):
    a = np.asarray(Image.open(frame_path).convert('RGBA'), dtype=np.float32)
    gray = np.asarray(Image.open(frame_path).convert('L'), dtype=np.float32)
    lid = a.copy()
    lid[..., 3] = a[..., 3] * lid_alpha(gray)
    return lid, openness(gray)


def main():
    fs = sorted(glob.glob(os.path.join(SRC, 'action_*.png')))
    if not fs:
        print('缺序列帧: %s' % SRC); return 1

    analyze = '--analyze' in sys.argv
    preview = '--preview' in sys.argv
    keep = 20
    if '--keep' in sys.argv:
        keep = int(sys.argv[sys.argv.index('--keep') + 1])

    o = np.array([openness(np.asarray(Image.open(f).convert('L'), dtype=np.float32)) for f in fs])
    lo, hi = o.min(), o.max()
    print('序列帧 %d 张   瞳孔区亮度 %.1f .. %.1f' % (len(fs), lo, hi))
    print('\n开合度 e=(亮度-min)/(max-min)  (1=闭 0=开):')
    for i in range(0, len(fs), 2):
        e = (o[i] - lo) / max(1e-6, hi - lo)
        print('  %3d  %.3f  %s' % (i + 1, e, '#' * int(round(e * 40))))
    if analyze:
        return 0

    os.makedirs(OUT, exist_ok=True)
    # 「闭->睁」段 1..39 里, 按开合度均匀抽帧(闭=1.0 一侧抽第 0 张, 开=0.0 一侧抽最后一张)
    seg = list(range(DRIVE_FIRST - 1, DRIVE_LAST))
    es = np.array([(o[i] - lo) / max(1e-6, hi - lo) for i in seg])
    targets = np.linspace(1.0, 0.0, keep)
    picked, taken = [], set()
    for t in targets:
        order = np.argsort(np.abs(es - t))
        for k in order:
            if k not in taken:
                taken.add(int(k)); picked.append(seg[int(k)]); break
    # 输出顺序 = 开合度 闭(1.0) -> 开(0.0), 即索引 0 = 最闭
    print('\n抽帧: %s' % ', '.join('第%d帧' % (i + 1) for i in picked))
    print('导出 %d 张 -> %s' % (len(picked), OUT))

    made = []
    for n, i in enumerate(picked):
        lid, op = build(fs[i])
        crop = resample_to_crop(lid)
        name = 'eye_lid_%02d.png' % n
        dst = os.path.join(OUT, name)
        Image.fromarray(np.clip(crop, 0, 255).astype(np.uint8), 'RGBA').save(dst)
        write_meta(dst, name)
        made.append((name, os.path.basename(fs[i]), op))
        print('  %s <- %s  瞳孔区亮度 %.1f' % (name, os.path.basename(fs[i]), op))

    if preview:
        os.makedirs(DOCS, exist_ok=True)
        shell = Image.open(os.path.join(BASE, 'bg_eye_shell.png')).convert('RGBA')
        cols, cw, ch = 5, CROP_W // 3, CROP_H // 3
        rows = (len(made) + cols - 1) // cols
        sheet = Image.new('RGB', (cols * cw, rows * ch), (24, 24, 30))
        for n, (name, _, _) in enumerate(made):
            lid = Image.open(os.path.join(OUT, name)).convert('RGBA')
            comp = shell.copy(); comp.alpha_composite(lid)
            comp = comp.resize((cw, ch))
            bg = Image.new('RGB', (cw, ch), (24, 24, 30)); bg.paste(comp, (0, 0), comp)
            sheet.paste(bg, ((n % cols) * cw, (n // cols) * ch))
        sheet.save(os.path.join(DOCS, '黄铜眼_眼睑开合序列帧.png'))
        print('预览图 -> docs/千眼回廊/黄铜眼_眼睑开合序列帧.png')
    return 0


if __name__ == '__main__':
    sys.exit(main())
