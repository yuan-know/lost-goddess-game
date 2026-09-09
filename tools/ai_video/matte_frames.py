# -*- coding: utf-8 -*-
"""把 AI 视频原始帧抠成干净的透明 PNG 序列。

为什么不能用色度键(旧 make_greenscreen_plate 的假设已失效):
  我们送进即梦的是纯绿 (0,255,0),但它吐回来的背景是 **灰绿 (110,138,109)** ——
  明度 43%、饱和度极低。整帧满足 g>r+40 的像素数 = 0。
  旧判定 g>r+25 且 g>b+25 只是勉强擦边通过,靠运气,所以出现:
    - 左臂与身体之间的封闭空隙留下成片绿色(6927px,泛洪到不了那里)
    - 轮廓脏边(边缘混了背景色又没还原)
    - 每帧 357 个连通块 / 297 个内部空洞(散点噪声)

改用 **色距 + 拓扑**,因为实测这两件事都成立:
    背景极均匀:四角与中值色距仅 2.3
    角色是棕调 (100,85,68):与背景色距中位数 111
  信噪比 111:2.3,比色度键稳得多。

六个措施,每个对症一个缺陷:
  1. 逐帧从边框环估背景色 —— 它在 R108-111/G135-139 之间漂,固定阈值必失效
  2. 双阈值软 alpha (NEAR..FAR) —— 给边缘一个平滑斜坡,治锯齿
  3. 只保留最大连通块 —— 治 357 个散点噪声,并顺手干掉即梦水印
     (已逐帧验证 97/97 帧水印都与角色分离;不用硬编码矩形,因为水印左边界
      距角色最右只有 1px 余量,写死矩形会啃掉手指)
  4. 封闭空隙回收 —— 面积达标且内部确实是背景色的洞,强制打透明。治左臂空隙那片绿
  5. 反预乘还原边缘真色 Cf=(C-(1-a)*BG)/a —— 治绿边/脏边
  6. 全序列统一缩放 + 脚底钉同一行 —— 旧产物逐帧各自缩放,把角色"呼吸"的
     5px 起伏当噪声抹平了,还引入尺寸脉动

用法:
    python tools/ai_video/matte_frames.py young_idle_v2
    python tools/ai_video/matte_frames.py young_idle_v2 --out young_idle_v3 --debug
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

# 色距阈值。NEAR 以下算纯背景,FAR 以上算纯前景,中间线性过渡成软 alpha。
# 实测:背景自身色距 ~2.8,角色核心 ~110。
#
# NEAR=10 的依据(下半身粗糙的真因):
#   即梦生成的背景**并不均匀** —— 画面底部的背景噪声远高于顶部:
#   纯背景像素中色距>6 的占比,顶部 0.2% 而底部 13.8%(p99 甚至到 120)。
#   NEAR=6 时这些噪声被当成"羽化边"赋了半透明 alpha,每帧约 19000px,
#   在腿部形成大片若隐若现的杂点 = 肉眼看到的"下半身粗糙"。
#   实测 NEAR=10 把误判从 19326px/帧 降到 5991px/帧(-69%),
#   而角色核心被啃掉 0px(核心最小色距远大于10),是无损的收益。
NEAR = 10.0
FAR = 30.0

# 措施7:软边距离守卫。只有**紧邻角色轮廓**的像素才允许半透明;
# 离轮廓超过这个距离的弱信号一律判为背景。
# 依据:实测下腿带的"软边"像素有 72% 离轮廓 19px 以上 —— 那不是羽化边,
# 而是背景噪声。真正的羽化边只有 3-6px 宽(实测 10-90% 上升距离)。
SOFT_EDGE_MAX_DIST = 8

# 封闭空隙回收:洞面积 >= 这个值、且洞内像素平均色距 < HOLE_BG_DIST 才打透明。
# 面积门槛防止把角色身上的高光小点误穿;色距门槛确保洞里真的是背景而不是暗部衣褶。
HOLE_MIN_AREA = 120
HOLE_BG_DIST = 14.0

# 措施8:绿色度判据(治裆部残留)。
#
# 为什么色距(欧氏距离)在裆部失效:
#   裆部是窄深 V 形凹口,两腿暗部把凹口里的背景压暗了(环境光遮蔽)。
#   实测凹口尖端的背景 RGB 是 (127,143,123) 而非纯背景 (110,137,109),
#   色距达 20-60 —— 被 d>20 的实体判据当成了前景,所以怎么调 NEAR 都没用。
#
# 正解:改用**绿色度** g - max(r,b),这是个色相量,与明暗无关:
#   凹口尖端 g-r = +15.7(偏绿=背景),而裤子 g-r = -5.0(非绿=前景)。
#   即梦背景虽被压暗,但**色相始终是绿的**,这正是可靠的抓手。
#
# ⚠️ 此判据**并非零重叠**,别信"安全余量极大"。我最初按全序列聚合统计得出
#    "前景 p99=-1 / 背景 p1=+25 零重叠",那个结论是错的 —— 聚合掩盖了逐帧差异。
#    逐帧实测:角色核心绿色度 max 中位 19、**最高 25**,而背景 p1 最低 22 ——
#    真实重叠。绝大多数帧核心 p99 只有 0-2,但帧 1/51 顶到 6-8,
#    正好卡在阈值上,于是这两帧在角色暗部(亮度~62)被打出 1500-2100px 的小洞,
#    表现为播放时的"麻麻卡卡"。
#    提高阈值治不好:调到 18 仍残留 275px 误杀,而凹口清理能力掉 86%。
#    真正的解是下面的 TEMPORAL_MEDIAN —— 从时间维度消掉孤立帧的突跳。
GREEN_BG = 6.0     # g-max(r,b) > 此值 判为背景

# 缩放后碎屑清理阈值。LANCZOS 在指尖等细小肢端会产生 50-100px 的孤立碎块
# (源分辨率上不存在)。小于此面积且不与主体连通的一律删除。
# 取 200:实测碎屑约 55px,而真实肢端(手掌等)都与主体连通不受影响。
SPECK_MAX_AREA = 200

# 措施9:alpha 的**循环时间中值滤波**(治"麻麻卡卡")。
#
# 为什么需要它:上面所有判据都是**逐帧独立**做的,任何一个判据只要在某一帧
#   踩到分布边缘,就会在那一帧单独打出一片洞/一片噪点。单帧静态看几乎正常
#   (帧1 的锯齿指标 1.854 vs 中位 1.591,毫无异常),但连续播放时它一帧闪进
#   一帧闪出,肉眼就是"麻麻卡卡"。这类缺陷**在空间维度是测不出来的**。
#
# 判定"孤立突跳"的方法:第 i 帧与前后帧都差很多(|Δa|>0.4),但前后帧彼此
#   却很接近(|a[i+1]-a[i-1]|<0.25) —— 说明这不是真实运动,而是单帧抖动。
#   实测 v8:成片突跳(>=3px 连通块)最大 446px/帧,集中在帧 1(446)和帧 51(365),
#   其余帧中位数 0。
#
# 3 帧中值恰好能吃掉"只出现在一帧"的异常,而保留连续 2 帧以上的真实运动。
#   实测效果:成片突跳 446 -> **0**;顺带把质心y帧间最大跳变 0.981 -> 0.151px
#   (比修复前的 v5 的 0.492 还稳)。
# 呼吸动作未被抹平:质心y 跨度 2.16 -> 1.57px,头顶行/脚底行跨度仍是 1px。
# 改动很局部:受影响像素中位仅 50px/帧。
# 用环形索引是因为这是循环动画,首尾帧互为邻居。
TEMPORAL_MEDIAN = True

# 中值窗口(必须是奇数)。
# 3 = 只吃掉"孤立单帧"异常,对真实运动几乎无损 —— 治帧1/51 那种突跳的正确档位。
# 5 = 还能吃掉"连续两帧"的异常,但也开始削弱真实运动。
#
# ⚠️ 加大窗口对**机械臂**基本无效,别指望它:
#   机械臂的"麻卡"不是孤立突跳(臂区突跳实测已经是 0),而是即梦生成时
#   义肢轮廓的**持续性帧间漂移** —— 臂区相邻帧剪影 IoU 中位 0.9915,
#   而躯干是 0.9996,差 20 倍。中值治不了持续性漂移。
#   实测 3->5 帧:臂区 IoU 仅 0.9954->0.9961,锯齿 0.512->0.502,肉眼无差。
#   治本只能重新生成素材(提示词约束义肢可见且结构一致)。
TEMPORAL_WINDOW = 3

# 输出画布。与旧产物保持一致,保证能直接替换 Unity 里已导入的 83 张图。
OUT_W, OUT_H = 260, 512
FEET_MARGIN = 2      # 脚底距画布底部留几px,避免采样时贴边
TOP_MARGIN = 3


def estimate_bg(rgb):
    """从边框环取中值当背景色。用中值而非均值:水印/角色万一探到边框也不会拉偏。"""
    h, w, _ = rgb.shape
    k = 6
    ring = np.concatenate([
        rgb[:k].reshape(-1, 3),
        rgb[-k:].reshape(-1, 3),
        rgb[:, :k].reshape(-1, 3),
        rgb[:, -k:].reshape(-1, 3),
    ])
    return np.median(ring, axis=0)


def build_alpha(rgb, bg):
    """色距 -> 软 alpha,再用拓扑清理。返回 (alpha float 0..1, dist)"""
    dist = np.sqrt(((rgb - bg) ** 2).sum(axis=2))

    # 绿色度:与明暗无关的色相量,用来识别被暗部压暗的背景(裆部凹口)。
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    greenness = g - np.maximum(r, b)
    is_bg_hue = greenness > GREEN_BG

    # 措施2:双阈值软斜坡
    alpha = np.clip((dist - NEAR) / (FAR - NEAR), 0.0, 1.0)
    # 色相判定为背景的,直接压到透明 —— 这是裆部残留的正解。
    alpha[is_bg_hue] = 0.0

    # 措施3:只保留最大连通块。先闭运算把角色内部因暗部造成的细缝连起来,
    # 否则一条头发丝的断裂会把身体劈成两块,取最大块就丢半个人。
    # 实体判据必须**同时**满足"色距够大"和"色相不是背景绿",否则裆部凹口里
    # 被压暗的背景(色距>20 但偏绿)会被误当成实体,后续再怎么调都盖不住。
    solid = ndimage.binary_closing((dist > 20.0) & ~is_bg_hue, np.ones((3, 3)))
    lbl, n = ndimage.label(solid)
    if n == 0:
        return np.zeros_like(alpha), dist
    sizes = ndimage.sum(solid, lbl, range(1, n + 1))
    keep = lbl == (int(np.argmax(sizes)) + 1)

    # 措施7:软边距离守卫(治下半身粗糙)。
    # 旧做法是"主块膨胀3次"当守卫,但那只挡住了离轮廓>3px 的噪声,
    # 而实测背景噪声离轮廓可达 19-60px,照样漏进来变成半透明杂点。
    # 改为按**到实体轮廓的真实距离**来卡:
    #   - 距离 <= SOFT_EDGE_MAX_DIST 的,保留软 alpha(真正的羽化边只有 3-6px 宽)
    #   - 超出的,一律判为背景(alpha=0),但**实体像素本身不受影响**
    dist_to_solid = ndimage.distance_transform_edt(~keep)
    near_edge = dist_to_solid <= SOFT_EDGE_MAX_DIST
    # keep 本身(实体)必须无条件保留,只对"非实体的弱信号"做距离筛选
    alpha = alpha * (keep | near_edge)

    # 措施4:封闭空隙回收 —— 这是左臂空隙那片绿的正解。
    filled = ndimage.binary_fill_holes(keep)
    holes = filled & ~keep
    if holes.any():
        hl, hn = ndimage.label(holes)
        for i in range(1, hn + 1):
            hm = hl == i
            area = int(hm.sum())
            if area < HOLE_MIN_AREA:
                continue                      # 太小,可能是角色身上的高光,别动
            if dist[hm].mean() >= HOLE_BG_DIST:
                continue                      # 洞里不是背景色(暗部衣褶),别动
            # 确认是背景:强制透明。腐蚀一圈保留边界羽化,不切出硬口子。
            core = ndimage.binary_erosion(hm, np.ones((3, 3)), iterations=1)
            alpha[core] = 0.0
            edge = hm & ~core
            alpha[edge] = np.minimum(alpha[edge], 0.5)

    return alpha, dist


def unpremultiply(rgb, alpha, bg):
    """措施5:反预乘还原边缘真色。

    半透明边缘像素观测值是 C = a*Cf + (1-a)*BG。既然 BG 已知,就能解出 Cf。
    不做这一步,轮廓上会留一圈背景色 —— 也就是你看到的"脏边/绿边"。
    """
    a = alpha[..., None]
    safe = np.maximum(a, 1e-3)
    fg = (rgb - (1.0 - a) * bg[None, None, :]) / safe
    # a 很小时上式数值不稳,直接沿用观测色(反正几乎全透明,看不见)
    fg = np.where(a < 0.05, rgb, fg)
    return np.clip(fg, 0, 255)


def content_box(alpha, thr=0.35):
    m = alpha > thr
    if not m.any():
        return None
    ys, xs = np.nonzero(m)
    return int(ys.min()), int(ys.max()), int(xs.min()), int(xs.max())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('name', help='raw 子目录名,如 young_idle_v2')
    ap.add_argument('--out', default=None, help='final 子目录名,默认同 name')
    ap.add_argument('--prefix', default='idle', help='输出文件名前缀')
    ap.add_argument('--debug', action='store_true', help='额外输出一张拼图便于目视')
    # 为什么需要截取窗口:Unity 里的 Young_Idle_AI.anim 是按 GUID 引用 **83 张** 精灵的。
    # 多出来的帧会拿到新 GUID,clip 引用不到 —— 所以帧数必须对齐才能原地替换。
    # 顺带好处:选一段首末更接近的窗口,循环接缝更顺(实测 9-91 的 IoU 0.981 > 全 97 帧的 0.969)。
    ap.add_argument('--start', type=int, default=0, help='起始帧(含)')
    ap.add_argument('--count', type=int, default=0, help='取多少帧,0=全部')
    # 全形态必须共用同一个 scale,否则并排看身高比例会失真(v1 的教训)
    ap.add_argument('--scale', type=float, default=0.0, help='强制 scale;0=自动填满')
    args = ap.parse_args()

    src = os.path.join(RAW_DIR, args.name)
    files = sorted(glob.glob(os.path.join(src, '*.png')))
    if not files:
        print(f'[错误] {src} 下没有 PNG')
        return 1

    if args.start or args.count:
        n0 = len(files)
        end = args.start + args.count if args.count else len(files)
        files = files[args.start:end]
        print(f'截取窗口: 原 {n0} 帧 -> 取 [{args.start}, {end}) 共 {len(files)} 帧')
        if not files:
            print('[错误] 窗口是空的')
            return 1

    out_name = args.out or args.name
    dst = os.path.join(OUT_DIR, out_name)
    os.makedirs(dst, exist_ok=True)

    print(f'源: {src}  ({len(files)} 帧)')

    # --- 第一遍:抠像 + 量测。先不写盘,因为统一缩放需要先知道全序列的尺寸极值。
    mattes = []
    bgs = []
    for i, p in enumerate(files):
        rgb = np.asarray(Image.open(p).convert('RGB')).astype(np.float64)
        bg = estimate_bg(rgb)
        alpha, dist = build_alpha(rgb, bg)
        fg = unpremultiply(rgb, alpha, bg)
        if content_box(alpha) is None:
            print(f'  [跳过] 帧 {i} 抠出来是空的')
            continue
        # 存 rgb 是为了在时间中值之后**用滤波后的 alpha 重做反预乘**。
        # 否则:中值把某帧误杀的洞恢复成不透明,但那里的 fg 还是按旧 alpha(≈0)
        # 算出来的 —— 除以接近 0 的 alpha 得到的颜色是垃圾,会露出绿溢出。
        # 实测不重做时,帧1 的偏绿像素 149 -> 511px(静态绿边,不闪,但可避免)。
        mattes.append((fg, alpha, rgb))
        bgs.append(bg)

    if not mattes:
        print('[错误] 全部帧都抠空了')
        return 1

    # 措施9:alpha 循环时间中值(详见文件头 TEMPORAL_MEDIAN 注释)。
    # 必须在算包围盒之前做,否则裁切框会带上待滤掉的噪声。
    if TEMPORAL_MEDIAN and len(mattes) >= TEMPORAL_WINDOW:
        n = len(mattes)
        half = TEMPORAL_WINDOW // 2
        # 必须先把所有原始 alpha 存下来:窗口要用到 i 前后各 half 帧的**原值**,
        # 一旦就地覆写,后面的帧就会读到已滤波的结果(级联滤波,等效窗口被放大)。
        # alpha 是单通道 float,83 帧约 700MB 内存峰值 —— 可接受;
        # 若日后帧数翻倍,改成只缓存 TEMPORAL_WINDOW 帧的环形缓冲。
        orig = [m[1] for m in mattes]
        for i in range(n):
            stack = np.stack([orig[(i + k) % n] for k in range(-half, half + 1)])
            med = np.median(stack, axis=0)
            # 用滤波后的 alpha 重做反预乘(理由见上面存 rgb 处的注释)
            mattes[i] = (unpremultiply(mattes[i][2], med, bgs[i]), med)
        del orig
        print(f'时间中值: 已对 alpha 做 {TEMPORAL_WINDOW} 帧循环中值'
              f'(消除单帧突跳),并重做反预乘')
    else:
        mattes = [(fg, a) for fg, a, _ in mattes]

    boxes = [content_box(a) for _, a in mattes]
    if any(bx is None for bx in boxes):
        keep = [(m, bx) for m, bx in zip(mattes, boxes) if bx is not None]
        mattes = [m for m, _ in keep]
        boxes = [bx for _, bx in keep]
        print(f'中值后有帧变空,已剔除,剩 {len(mattes)} 帧')

    b = np.array(boxes)
    # 措施6:全序列 **统一裁切框 + 统一缩放**。
    #
    # 为什么不能逐帧按自己的包围盒对齐(这是抖动的真凶):
    #   源视频是固定机位的(脚底行全序列只变 1px),但它的**包围盒**会晃 7px ——
    #   因为包围盒由最外缘的噪声决定(指尖、发梢、羽化边的一两个像素)。
    #   逐帧按自己的 bbox 对齐,等于把"指尖噪声"翻译成了"整个人平移",
    #   再被 int(round()) 量化成 1-2px 的整像素跳变 = 肉眼可见的抖动。
    #
    # 正解:全序列共用一个裁切框(取所有帧包围盒的并集 + 边距)。
    #   每帧做**完全相同**的裁切与缩放,不做任何逐帧位置补偿。
    #   角色真实的呼吸/摆动仍然存在(内容在固定画框内移动),
    #   而对齐噪声被彻底消除 —— 因为根本不再逐帧对齐。
    PAD = 2
    gy0 = max(0, int(b[:, 0].min()) - PAD)
    gy1 = int(b[:, 1].max()) + PAD
    gx0 = max(0, int(b[:, 2].min()) - PAD)
    gx1 = int(b[:, 3].max()) + PAD
    gh = gy1 - gy0 + 1
    gw = gx1 - gx0 + 1
    avail_h = OUT_H - FEET_MARGIN - TOP_MARGIN
    fit_scale = min(avail_h / gh, (OUT_W - 4) / gw)
    scale = args.scale if args.scale > 0 else fit_scale
    if args.scale > 0 and args.scale > fit_scale:
        print(f'[警告] scale {args.scale:.4f} 超过可容纳的 {fit_scale:.4f},改用后者')
        scale = fit_scale
    print(f'统一裁切框: y {gy0}-{gy1} x {gx0}-{gx1} ({gw}x{gh})  '
          f'= 全帧包围盒并集+{PAD}px')

    bgarr = np.array(bgs)
    print(f'背景色: R{bgarr[:,0].min():.0f}-{bgarr[:,0].max():.0f} '
          f'G{bgarr[:,1].min():.0f}-{bgarr[:,1].max():.0f} '
          f'B{bgarr[:,2].min():.0f}-{bgarr[:,2].max():.0f}  (逐帧自适应)')
    print(f'角色包围盒: 高 {(b[:,1]-b[:,0]).min()}-{(b[:,1]-b[:,0]).max()} '
          f'宽 {(b[:,3]-b[:,2]).min()}-{(b[:,3]-b[:,2]).max()}')
    print(f'统一缩放 scale={scale:.4f},脚底钉在 y={OUT_H - FEET_MARGIN}')

    # --- 第二遍:统一变换 + 写盘
    # 所有帧共用同一个裁切框和同一个粘贴位置,不做逐帧对齐 -> 零对齐抖动。
    nw = max(1, int(round(gw * scale)))
    nh = max(1, int(round(gh * scale)))
    paste_x = (OUT_W - nw) // 2
    paste_y = (OUT_H - FEET_MARGIN) - nh
    written = 0
    for i, (fg, alpha) in enumerate(mattes):
        rgba = np.dstack([fg, alpha * 255.0]).astype(np.uint8)
        # 先裁切到统一框(每帧完全相同的区域),再缩放
        crop = rgba[gy0:gy1 + 1, gx0:gx1 + 1]
        im = Image.fromarray(crop, 'RGBA').resize((nw, nh), Image.LANCZOS)

        # 缩放后清理碎屑:LANCZOS 重采样会在细小肢端(指尖等)留下几十像素的
        # 孤立碎块 —— 源分辨率上并不存在(实测源图无额外连通块)。
        # 只删"不与主体连通且很小"的块,主体和真实肢端都不受影响。
        arr = np.array(im)
        op = arr[..., 3] > 128
        if op.any():
            lb, nb = ndimage.label(op)
            if nb > 1:
                szs = ndimage.sum(op, lb, range(1, nb + 1))
                main_id = int(np.argmax(szs)) + 1
                for k in range(1, nb + 1):
                    if k != main_id and szs[k - 1] < SPECK_MAX_AREA:
                        arr[..., 3][lb == k] = 0
                im = Image.fromarray(arr, 'RGBA')

        canvas = Image.new('RGBA', (OUT_W, OUT_H), (0, 0, 0, 0))
        canvas.alpha_composite(im, (paste_x, paste_y))

        canvas.save(os.path.join(dst, f'{args.prefix}_{i:03d}.png'))
        written += 1

    print(f'\n[完成] {written} 帧 -> {dst}')

    # --- 自检:把关键指标打出来,别让"跑完了"冒充"抠干净了"
    checks = sorted(glob.glob(os.path.join(dst, '*.png')))
    probe = [checks[0], checks[len(checks) // 2], checks[-1]]
    print('\n=== 自检 ===')
    for p in probe:
        im = np.asarray(Image.open(p).convert('RGBA')).astype(np.int16)
        a = im[..., 3]
        r, g, bl = im[..., 0], im[..., 1], im[..., 2]
        op = a > 128
        gm = (g > r + 10) & (g > bl + 10) & op
        # 区分"边缘羽化像素偏绿"(无害,半透明,看不见)和"内部成片绿"(真缺陷)。
        # 只报后者,否则数字会虚高,让人误判还没修好。
        interior = ndimage.binary_erosion(op, iterations=4)
        inner_green = int((gm & interior).sum())
        lbl, n = ndimage.label(op)
        holes = ndimage.binary_fill_holes(op) & ~op
        soft = int(((a > 0) & (a < 255)).sum())
        print(f'  {os.path.basename(p)}: 内部残留绿 {inner_green:>4d}px  连通块 {n:>2d}  '
              f'封闭透明区 {int(holes.sum()):>4d}px  软边 {soft:>5d}px')
    print('  内部残留绿: 应接近0 —— 这是"空隙里成片绿"的直接指标')
    print('  连通块: 应为1 —— >1 说明有散点噪声或水印没除掉')
    print('  封闭透明区: 应有几百px —— 这是左臂与身体的空隙,透明才对')
    print('  软边: 应有几千px —— 说明边缘是羽化的,不是硬锯齿')

    meta = {
        'source': args.name,
        'frames': written,
        'canvas': [OUT_W, OUT_H],
        'scale': round(float(scale), 6),
        'feet_row': OUT_H - FEET_MARGIN,
        'crop_box': [gx0, gy0, gx1, gy1],
        'near_far': [NEAR, FAR],
        'method': 'bg-distance + largest-CC + hole-reclaim + unpremultiply',
    }
    with open(os.path.join(dst, '_matte.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, ensure_ascii=False, indent=2)

    if args.debug:
        cols = 8
        sel = checks[::max(1, len(checks) // 16)][:16]
        tw, th = OUT_W // 2, OUT_H // 2
        rows = (len(sel) + cols - 1) // cols
        # 洋红底:绿残留和脏边在洋红上最刺眼,一眼能看出来
        sheet = Image.new('RGB', (cols * tw, rows * th), (255, 0, 255))
        for j, p in enumerate(sel):
            t = Image.open(p).convert('RGBA').resize((tw, th), Image.LANCZOS)
            sheet.paste(t, ((j % cols) * tw, (j // cols) * th), t)
        dbg = os.path.join(os.path.dirname(dst), f'_{out_name}_check.png')
        sheet.save(dbg)
        print(f'\n目视拼图(洋红底,绿残留会很刺眼): {dbg}')

    return 0


if __name__ == '__main__':
    sys.exit(main())
