# -*- coding: utf-8 -*-
"""烟雾喷出序列帧:绿幕溢出抑制(去绿)+ 薄雾透明化。

问题(实测本批 34 帧 action_001..034):
  帧已是 RGBA 480x204,**alpha 是二值** 0/255(无软边),背景全透明、RGB=0。
  但**不透明像素内部**残留大量绿色:
    - 全序列 50% 的上不透明像素绿色度 g-max(r,b) <= 0(干净)
    - p90=9 / p99=24 / p99.9=37 / max≈55 —— 绿色集中在**薄雾区**
    - 纯绿幕只剩在最薄的像素里,中位 RGB(82,150,103),绿色度≈47
    - 浓密核心是干净的中性冷白(220,222,224),绿色度≈-2
  即:烟是白烟,但薄的地方透出了绿幕底色,而那部分 alpha 被(错误地)标成 255。

为什么"逐列平均绿色度"能定位缺陷:
  x=40 左缘 8.2 / x=100-380 主体 0.4-2.5 / x=400-460 右缘 2.5-8.8 / x=479 12.5
  左缘是烟头(刚喷出的薄雾),右缘是烟尾 —— **两端薄、中间厚**,绿都长在薄的地方。

做法(四步,每步对症一个缺陷):
  1. **溢出抑制 + 亮度补偿**:ex = clip(g-max(r,b),0)。把绿色通道压掉,
     同时把压掉的量按 K 分给 r/b —— 否则薄雾区会变成一块死灰(实测 v1 硬压
     g'=max(r,b) 时右缘 (82,147,104)->(82,104,104),亮度掉 16%)。
  1b.**去青(关键修正)**:只压到 g=max(r,b) 是不够的 —— 喷出位置那片像素是
     **b > r**(实测 (82,147,104)),压完变成 g==b>r = **(101,123,123) 青灰**,
     肉眼看仍然是"偏绿的色块"(用户 2026-09-11 反馈)。真正的解是按溢出比例
     **向亮度去饱和**:rgb += (lum - rgb) * s,s=ex/S0。
       色饱和度 spread 被压成原来的 (1-s) 倍 —— 溢出越重越接近中性灰。
       实测 (101,123,123) -> (114,117,117),r 与 g/b 的差 22 -> 3,青感消失。
       浓核心 ex=0 -> s=0 -> 完全不动。
  2. **薄雾透明化 + 趋白(混合比例估计)**:把观测像素建模成 C = a*Cf + (1-a)*BG,
     并假设烟雾是消色(白)的 → 每对通道的差都等于 (1-a)*ΔBG:
        1-a = 平均[ (g-r)/(BGg-BGr), (g-b)/(BGg-BGb) ]
     两路取平均抗噪。这样"薄雾"就自动变成半透明,而不是变成一块灰斑。
     这是**物理重建**,比拍脑袋的 ex/常数 准:
       右缘 (82,147,104) -> 1-a≈0.94 -> alpha≈6%(几乎全透,正确 —— 那里本来就是背景)
       轻溢出 (190,199,195) -> 1-a≈0.11 -> alpha≈89%(只轻微透)
       浓核心 (220,222,224) -> 1-a≈0    -> alpha=100%
  2b.**颜色趋白**:只降 alpha 会把薄雾留成一块**暗灰块**(用户 2026-09-11 反馈
     "喷出位置还有偏绿色块")。物理上薄白烟的真色**就是白的**,只是不透明。
     所以按混入比例把颜色往浓核心的实测中性色 (206,209,211) 拉:
       色 = 色*(1-w) + 中性*w, w = clip(1-a, 0, 1)
     薄雾于是变成"亮白但透明",在深色背景上自然淡出,不再是一块灰。
  2c.**透明度空间平滑(sigma=2)**:源视频有压缩块,再叠上逐像素估计的噪声,
     喷出位置的 alpha 会呈**方块状**。对 (1-a) 做高斯平滑后块状消失。
  3. **1px 羽化**:源 alpha 是二值,深色背景上锯齿明显;给轮廓 1px 斜坡。
  4. **绿色碎片清除**:源帧的主体周围散落着一些**孤立小块**,实测它们**原图就是偏绿的**
     (绿色度 9.6-27),属于绿幕残留,不只是压缩噪声。逐个看:
       action_011 block2 (159,178,169) 绿9.6 / block3 (166,194,175) 绿18.6
       action_034 block2 (150,183,166) 绿17.1 / 若干 1-5px 点 绿 9-27
     所以判据是**"小 且 原图偏绿"**,而不是单看面积 —— 免得把真正的中性小烟丝误删。
       非主体块 且 面积 < SPECK_MAX 且 (面积 < SPECK_TINY 或 原图平均绿色度 > GREEN_CUT) -> 删
     主体块永远保留(早期帧整团烟就是主体,不会被误删)。

⚠️ 不做反预乘求 Cf。试过(v3):1-a 大时除以接近 0 的 alpha 会把噪声放大成怪色
   (实测右缘反预乘出 (94,129,116) 仍偏绿且 alpha 只有 0.14)。
   亮度补偿已能给出稳定的去绿颜色,透明度交给第 2 步。
⚠️ BG 不做迭代精修。试过:高 1-a 像素迭代会把 BG 越修越亮(110,158,127),
   因为不透明掩膜里根本没有"纯"背景,精修会漂。改用**确定性**取值:
   全序列最绿的 300 个不透明像素的中位色(实测 (82,150,103))。

用法:
    python tools/ai_video/despill_smoke.py                      # 处理全部
    python tools/ai_video/despill_smoke.py --src <目录> --out <目录>
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

# 纯绿幕基准色。实测全序列最绿的 300 个不透明像素中位色 = (82,150,103)。
# 只用来算通道差 ΔBG,不做迭代精修(理由见文件头)。
BG = np.array([82.0, 150.0, 103.0])

# 纯绿幕的绿色度(g - max(r,b)) = 150 - max(82,103) = 47。用于把溢出量归一化成
# 0..1 的"背景混入强度",同时也是去青时去饱和的强度系数。
S0 = 47.0

# 亮度补偿比例。ex 的一半补给 r/b。实测 0.45 时右缘 (82,147,104) -> (101,123,123),
# 亮度从 111 回到 116(纯硬压是 104);0.5 以上薄雾区开始发灰白,反而失真。
K = 0.45

# 浓核心实测中性色(亮 10% 像素的均值),薄雾趋白时拉到这个色。
SMOKE_NEUTRAL = np.array([206.0, 209.0, 211.0])

# 容差参数。
#   FADE_POW: alpha 保留系数 = 1 - (1-a)^FADE_POW。0.5 比线性(1.0)更陡,
#     实测能把喷出位置的暗灰块压到接近背景;再小(0.4)烟尾开始断开。
#   WHITE_GAIN: 趋白的强度增益。1.15 让中等污染的像素也大致转白。
#   SMOOTH_SIGMA: (1-a) 的高斯平滑半径,压掉源视频压缩块造成的方块感。
FADE_POW = 0.5
WHITE_GAIN = 1.15
SMOOTH_SIGMA = 2.0

# 绿色碎片清除阈值。
#   SPECK_TINY 以下的无条件是噪声(实测 1-8px)。
#   SPECK_MAX 以下**且原图平均绿色度 > GREEN_CUT** 的判为绿幕残留块(实测 44-166px)。
SPECK_TINY = 30
SPECK_MAX = 400
GREEN_CUT = 5.0

# 是否给二值 alpha 做 1px 羽化。二值边在深色背景上会有明显锯齿;
# 1px 斜坡能混掉,又不至于把细烟丝(宽 2-4px)糊掉。
FEATHER = True

# ⚠️ 喷出源渐变(2026-09-11 用户三次反馈,最终定稿):
#   喷嘴处必须**完全透明(alpha=0)**,否则和背景有一条分界线;
#   然后在 EMIT_FADE_PX 内快速渐变到不透明。
#   ⚠️ 但距离不能长 —— v1 用 90px 从 0 起步,喷嘴附近一大块全透明,用户不满。
#   所以:距离短(50px)+ 幂次 <1(前段爬得快),全透明的部分只有边缘几像素。
EMIT_FADE_PX = 40
EMIT_FADE_POW = 0.6

# ⚠️ 淡出时颜色要向白提亮。只降 alpha 的话,半透明烟叠在深色背景上是一层
# **灰膜**(200*a + 40*(1-a)),用户看成"渐变区和实区之间一条黑色的分界线"。
# 薄烟在深底上应该是**发白的雾**(散射光),所以 alpha 越低颜色越往白推。
EMIT_BRIGHTEN = 0.0   # v6 关掉:密度 alpha 后烟雾已半透,推白反而造成"渐变区和烟体两团色"的分界

# ⚠️ 烟灰段提升(用户第 6 次反馈"渐变区和后半段非渐变区中间有一条黑边"):
# 源视频里喷嘴出口那一段烟本身是**暗灰**(r 197->156,带绿污染,去绿后亮度
# 回不来)。曾用"空间斜坡提亮"(EMIT_BRIGHTEN_PX=140)盖它 —— 结果斜坡终点
# (x≈339)出现亮度台阶(后段 190 vs 渐变区 205+),那条台阶就是用户看到的黑线。
# 正解:**按像素暗度提升,不带任何空间边界** —— 暗的像素抬向白、亮的不动,
# 过渡天然连续,任何位置都不会有线。
SHADOW_TARGET = 205.0   # 提升目标亮度(≈烟体亮度,提到它就不会有反差)
SHADOW_REF = 140.0      # 低于此亮度的像素按最大力度提升
SHADOW_LIFT = 0.8       # 力度(1.0 会把烟灰段抬得比烟体还亮,反而像亮带)

# ⚠️⚠️ 密度->alpha 重映射(2026-09-11 用户第 7 次反馈黑线后,回头重看 4x 放大图
# 才发现的**总根因**):源视频的 alpha 是**二值 0/255**,烟雾的半透明感全部烘焙
# 在 RGB 亮度里(灰 = 半透的白烟叠深底)。管线一直保持 alpha≈255 + 把 RGB 提白,
# 渲染出来就是一块**不透明的白色剪纸**:轮廓硬边、内部看不到背景、喷嘴渐变只有
# 40px 一条 —— 剪纸的边界就是用户一直说的"黑线",修亮度/修 alpha 台阶都没用。
# 正解:**alpha 从密度(亮度)推导** —— 亮=密=实,暗=疏=透,RGB 保持白。
# 烟雾恢复半透,内部渐变连续,任何交界都不再有线。
DENSITY_LO = 120.0      # 亮度低于此 = 全透
DENSITY_HI = 215.0      # 亮度高于此 = 最浓
DENSITY_GAMMA = 1.1     # >1 让中低密度更透一点
DENSITY_MAX = 0.96      # 最浓处也不完全 255,保留一丝透气感
DENSITY_SMOOTH = 1.5    # 密度场平滑:压掉源视频压缩块,也让轮廓软化

# ⚠️ 末尾消散淡出(2026-09-11 用户反馈):烟播到最后一帧后不要"啪一下消失",
# 追加若干帧让整团烟**淡出 + 轻微上飘放大**,像自然散掉。
# 速度取中:18 帧 @24fps = 0.75s(太快像闪退,太慢像赖着不走)。
FPS = 24                     # 源视频速率,算消散时长用
FADE_OUT_FRAMES = 18
DISSIPATE_RISE = 16      # 消散期间整体上飘(像素)
DISSIPATE_DRIFT = 10     # 同时往左漂一点(像素)
DISSIPATE_GROW = 0.10    # 同时放大的比例
DISSIPATE_BRIGHTEN = 1.0  # 消散时同样向白提亮,避免变成灰膜

OUT_W, OUT_H = 480, 204     # 与源一致,方便直接替换


def mixed_fraction(rgb):
    """估计背景混入比例 (1-a)。假设烟雾消色,每对通道差 = (1-a)*ΔBG。"""
    dgr = max(1e-6, BG[1] - BG[0])      # 150-82 = 68
    dgb = max(1e-6, BG[1] - BG[2])      # 150-103 = 47
    f1 = (rgb[..., 1] - rgb[..., 0]) / dgr
    f2 = (rgb[..., 1] - rgb[..., 2]) / dgb
    return np.clip((f1 + f2) * 0.5, 0.0, 0.96)


def despill(rgb, alpha):
    """返回 (去绿去青后 rgb, 背景混入比例)。保证绿色度 <= 0 且大幅削弱青感。"""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    m = np.maximum(r, b)
    ex = np.clip(g - m, 0.0, None)                 # 绿色溢出量

    # 1) 压绿 + 亮度补偿(把压掉的绿按 K 分给 r/b)
    r2 = np.clip(r + ex * K, 0, 255)
    b2 = np.clip(b + ex * K, 0, 255)
    g2 = np.minimum(g, np.maximum(r2, b2))         # 保证绿色度 <= 0

    # 2) 去青:按溢出比例向亮度去饱和(spread 压成 (1-s) 倍)
    s = np.clip(ex / S0, 0.0, 1.0)
    lum = 0.299 * r2 + 0.587 * g2 + 0.114 * b2
    r3 = r2 + (lum - r2) * s
    g3 = g2 + (lum - g2) * s
    b3 = b2 + (lum - b2) * s
    out = np.dstack([r3, g3, b3])
    return out, mixed_fraction(out)


def bleed_rgb(out):
    """把无效像素(a<10)的 RGB 用最近有效像素的颜色填掉(只填 RGB,不动 alpha)。

    ⚠️ 黑边真凶(2026-09-11 用户第三次反馈"还是有黑边"):
    源视频解码出来的 alpha=0 像素 RGB 是**纯黑 (0,0,0)**(实测单帧 45275 个)。
    我的 Python 预览用最近邻采样,看不出问题;但 Unity 用**双线性过滤**采样,
    会把透明像素的黑 RGB 混进烟雾边缘的低透明度区 —— 叠在亮背景(黄铜)上
    就是一圈/一条暗色描边。这就是"黑边在 GIF 里看不见、在 Unity 里看得见"的原因。
    填掉 RGB 渲染结果不变(黑*a=0),但过滤后不再渗黑。alphaIsTransparency=1
    理论上 Unity 导入时也会填,但在自己素材里做一遍是确定性的,不依赖导入器行为。
    """
    a = out[..., 3]
    bad = a < 10.0
    if not bad.any() or not (a >= 10).any():
        return out
    ind = ndimage.distance_transform_edt(bad, return_distances=False,
                                         return_indices=True)
    out[..., :3][bad] = out[..., :3][ind[0][bad], ind[1][bad]]
    return out


def process(path):
    im = np.asarray(Image.open(path).convert('RGBA')).astype(np.float64)
    rgb, alpha = im[..., :3], im[..., 3]

    rgb2, frac = despill(rgb, alpha)

    # 2c) (1-a) 空间平滑:压掉源视频压缩块造成的方块感
    if SMOOTH_SIGMA > 0:
        frac = ndimage.gaussian_filter(frac, SMOOTH_SIGMA)

    # 2b0) 密度场:用**去绿后、提白前**的亮度。源是二值 alpha,烟雾的半透感
    #      全烘焙在这个亮度里(灰=半透白烟叠深底) —— 它就是烟的"密度图"。
    #      必须在 shadow lift / 趋白**之前**取,否则提亮把密度抹平,又是一块剪纸。
    lum_src = 0.299 * rgb2[..., 0] + 0.587 * rgb2[..., 1] + 0.114 * rgb2[..., 2]
    lum_src = ndimage.gaussian_filter(lum_src, DENSITY_SMOOTH)

    # 2b) 颜色趋白:薄白烟的真色是白的,只是不透明
    w = np.clip(frac * WHITE_GAIN, 0.0, 1.0)[..., None]
    rgb3 = rgb2 * (1.0 - w) + SMOKE_NEUTRAL[None, None, :] * w
    # ⚠️ 混合本身会重新引入最多 (209-206) 的绿度:SMOKE_NEUTRAL 是 b>g>r,
    # 而 r 被拉到 206 就会让 g 反超 max(r,b)。最后必须再钳一次。
    rgb3[..., 1] = np.minimum(rgb3[..., 1],
                              np.maximum(rgb3[..., 0], rgb3[..., 2]))

    # 2b2) 烟灰段提升:按**像素暗度**向白抬(无空间边界 → 任何位置都不出现
    #      亮度台阶/分界线)。源里喷嘴出口那段暗灰烟(r~160)被抬到 ≈205,
    #      与烟体(≈200)融合;亮部(>205)不动。
    lum = 0.299 * rgb3[..., 0] + 0.587 * rgb3[..., 1] + 0.114 * rgb3[..., 2]
    lift = np.clip((SHADOW_TARGET - lum) / (SHADOW_TARGET - SHADOW_REF),
                   0.0, 1.0) * SHADOW_LIFT
    rgb3 = rgb3 + (255.0 - rgb3) * lift[..., None]
    rgb3[..., 1] = np.minimum(rgb3[..., 1],
                              np.maximum(rgb3[..., 0], rgb3[..., 2]))

    # 2) **密度->alpha 重映射**(总根因修复):源 alpha 是二值 0/255,若保持
    #    alpha≈255,渲染出来是不透明的白色剪纸(硬边 + 渐变交界必有线)。
    #    改为从密度推导:亮=密=实,暗=疏=透,RGB 保持白。烟雾恢复半透。
    dn = np.clip((lum_src - DENSITY_LO) / (DENSITY_HI - DENSITY_LO), 0.0, 1.0)
    a2 = np.power(dn, DENSITY_GAMMA) * 255.0 * DENSITY_MAX
    # 绿污染薄雾区仍按混入比例再压一档(两路取较小,保守)
    a2 = np.minimum(a2, alpha * (1.0 - np.power(frac, FADE_POW)))

    # 2d) 喷出源渐变:喷嘴处**完全透明(0)**,在 EMIT_FADE_PX 内快速渐变到不透明。
    #     幂次 <1 让前段爬得快,全透明的部分只有边缘几像素,不会挖出一大块洞。
    #     渐变区颜色向白提亮(薄雾叠深底是灰膜)。烟灰段由 2b2 的暗度提升处理,
    #     这里**不再用空间斜坡提亮**(斜坡终点会产生亮度台阶 = 用户看到的黑线)。
    emit_ramp = np.ones(rgb2.shape[1], np.float64)
    if EMIT_FADE_PX > 0:
        w = rgb2.shape[1]
        t = np.clip((w - 1 - np.arange(w)) / float(EMIT_FADE_PX), 0.0, 1.0)
        s = t * t * (3.0 - 2.0 * t)                     # smoothstep
        emit_ramp = np.power(s, EMIT_FADE_POW)
        a2 = a2 * emit_ramp[None, :]
        rgb3 = rgb3 + (255.0 - rgb3) * ((1.0 - emit_ramp) * EMIT_BRIGHTEN)[None, :, None]

    # 1px 羽化:只沿轮廓做斜坡,内部保持不透明
    if FEATHER:
        mask = (a2 > 127).astype(np.float64)
        if mask.any():
            soft = ndimage.gaussian_filter(mask, sigma=0.62)
            soft = np.clip(soft * 1.12, 0, 1)      # 微增益,避免内部被压半档
            # 只在"硬掩膜边界附近"用软值覆盖,已半透明的取两者较小
            edge_zone = (soft > 0.02) & (soft < 0.995)
            a2 = np.where(edge_zone, np.minimum(a2, soft * 255.0), a2)

    out = np.dstack([rgb3, a2])

    # 绿色碎片清除:小 且 原图偏绿 的孤立块
    op = out[..., 3] > 40
    lb, nb = ndimage.label(op)
    if nb > 1:
        orig_green = rgb[..., 1] - np.maximum(rgb[..., 0], rgb[..., 2])
        szs = ndimage.sum(op, lb, range(1, nb + 1))
        main = int(np.argmax(szs)) + 1
        for k in range(1, nb + 1):
            if k == main:
                continue
            m = lb == k
            area = float(szs[k - 1])
            if area < SPECK_TINY:
                out[..., 3][m] = 0.0
            elif area < SPECK_MAX and float(orig_green[m].mean()) > GREEN_CUT:
                out[..., 3][m] = 0.0

    # 黑边修复:最后一步,把透明像素的黑 RGB 全部填成最近的烟色
    bleed_rgb(out)

    return np.clip(out, 0, 255).astype(np.uint8)


def make_dissipate(last_rgba, t):
    """末尾消散帧。t: 0 = 刚播完(完全不透明), 1 = 完全散掉。
    整体淡出(smoothstep)+ 轻微上飘左漂 + 微放大,像自然散开。"""
    h, w = last_rgba.shape[:2]
    s = 1.0 + DISSIPATE_GROW * t
    nw, nh = max(1, int(round(w * s))), max(1, int(round(h * s)))
    img = Image.fromarray(last_rgba, 'RGBA').resize((nw, nh), Image.LANCZOS)
    a = np.asarray(img).astype(np.float64)
    a = a[:h, :w] if (a.shape[0] >= h and a.shape[1] >= w) else np.pad(
        a, ((0, h - a.shape[0]), (0, w - a.shape[1]), (0, 0)))
    s2 = t * t * (3.0 - 2.0 * t)
    f = 1.0 - s2                                  # 剩余不透明度
    a[..., 3] *= f
    # 消散时向白提亮:散掉的烟应该越来越"白雾",而不是越来越灰
    a[..., :3] += (255.0 - a[..., :3]) * ((1.0 - f) * DISSIPATE_BRIGHTEN)
    # 预乘平移:直接 shift RGBA 会把 cval=0(黑)混进移动边缘的部分透明像素,
    # 又是一圈黑边。改成平移预乘色(rgb*a)和平移 alpha,再反预乘 —— 数学上
    # 等价于"加色混合的平移",边缘颜色永远是烟色,不会渗黑。
    pm = a.copy()
    pm[..., :3] *= (a[..., 3:4] / 255.0)
    shift = (-DISSIPATE_RISE * t, -DISSIPATE_DRIFT * t, 0)
    pm2 = ndimage.shift(pm, shift, order=1, mode='constant', cval=0.0)
    a2s = ndimage.shift(a[..., 3], shift[:2], order=1, mode='constant', cval=0.0)
    out = pm2
    out[..., 3] = a2s
    valid = out[..., 3] > 0.5
    out[..., :3][valid] = (out[..., :3][valid]
                           / (out[..., 3][valid, None] / 255.0))
    out[..., :3] = np.clip(out[..., :3], 0.0, 255.0)
    # 边框渐隐:消散时整体上飘,烟会顶到精灵矩形上边缘(实测 top alpha 到 125),
    # 被矩形硬截断 = 一条横切边。给四周 12px 做 alpha 渐隐,把截断藏进淡出里。
    # (消散帧本来就在整体淡出,边缘多淡一点看不出来;播放段帧不受影响 ——
    #  源烟顶离上边框只有 ~3px,这个渐隐只作用于消散帧。)
    fade_px = 12
    ry = np.minimum(np.arange(h), np.arange(h)[::-1]).astype(np.float64) / fade_px
    rx = np.minimum(np.arange(w), np.arange(w)[::-1]).astype(np.float64) / fade_px
    border = np.clip(np.minimum(ry[:, None], rx[None, :]), 0.0, 1.0)
    out[..., 3] *= border
    # 最后再兜底填一遍黑(覆盖 resize/shift 造成的所有无效色)
    bleed_rgb(out)
    return np.clip(out, 0, 255).astype(np.uint8)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', default=r'C:\Users\yuan\Downloads\smoke_extract')
    ap.add_argument('--out', default=os.path.join(ROOT, 'Assets', '_Project', 'Art',
                                                 'AIAnimations', 'SmokeSpray'))
    ap.add_argument('--prefix', default='smoke')
    ap.add_argument('--report', default=None, help='对比拼图输出路径')
    ap.add_argument('--dry', action='store_true', help='只统计不写盘')
    args = ap.parse_args()

    files = sorted(glob.glob(os.path.join(args.src, '*.png')))
    if not files:
        print(f'[错误] {args.src} 下没有 PNG')
        return 1
    print(f'源 {args.src}: {len(files)} 帧')

    if not args.dry:
        os.makedirs(args.out, exist_ok=True)
        # 清掉旧帧:帧数变化(加了消散尾帧)时,残留的旧编号文件会混进动画
        for old in glob.glob(os.path.join(args.out, f'{args.prefix}_*.png')):
            os.remove(old)

    # 源绿统计 / 处理后统计
    src_green = dst_green = 0
    src_cyan = dst_cyan = 0
    written = 0
    worst = (0, '', 0, 0)
    outs = []                      # 先攒着,末尾还要追加消散帧
    for i, p in enumerate(files):
        a0 = np.asarray(Image.open(p).convert('RGBA')).astype(np.int16)
        op0 = a0[..., 3] > 128
        if op0.any():
            src_green += int(((a0[..., 1] - np.maximum(a0[..., 0], a0[..., 2])) > 5)[op0].sum())
            src_cyan += int(((a0[..., 1] - a0[..., 0]) > 10)[op0].sum())

        out = process(p)
        op = out[..., 3] > 40
        if op.any():
            gi = out[..., 1].astype(np.int16)
            ri = out[..., 0].astype(np.int16)
            bi = out[..., 2].astype(np.int16)
            dst_green += int(((gi - np.maximum(ri, bi)) > 0)[op].sum())
            cyan_mask = ((gi - ri) > 10) & op
            dst_cyan += int(cyan_mask.sum())
            c = int((gi - ri)[op].max())
            if c > worst[2]:
                worst = (i, os.path.basename(p), c, int((gi - np.minimum(ri, bi))[op].max()))
        outs.append(out)
        written += 1

    # --- 末尾消散淡出:以最后一帧为底,淡出 + 上飘 + 微放大 ---
    if FADE_OUT_FRAMES > 0 and outs:
        last = outs[-1]
        for k in range(1, FADE_OUT_FRAMES + 1):
            t = k / float(FADE_OUT_FRAMES)
            outs.append(make_dissipate(last, t))
            written += 1
        print(f'末尾消散:追加 {FADE_OUT_FRAMES} 帧(共 {FADE_OUT_FRAMES/FPS:.2f}s @24fps)')

    if not args.dry:
        for i, out in enumerate(outs):
            Image.fromarray(out, 'RGBA').save(
                os.path.join(args.out, f'{args.prefix}_{i:03d}.png'))

    print(f'\n[完成] {written} 帧 -> {args.out}')
    print(f'绿像素(绿色度>5)  处理前 {src_green} -> 处理后(>0) {dst_green}')
    print(f'青像素(g-r>10)    处理前 {src_cyan} -> 处理后 {dst_cyan}')
    print(f'最偏青的帧: {worst[1]}  max(g-r)={worst[2]}  max色差={worst[3]}')
    print('  (max(g-r) 越接近 0 越好;色差 = max-min,越小越中性)')

    # 自检
    print('\n=== 自检 ===')
    chk = sorted(glob.glob(os.path.join(args.out, '*.png'))) or files
    for p in [chk[0], chk[len(chk) // 2], chk[-1]]:
        im = np.asarray(Image.open(p).convert('RGBA')).astype(np.int16)
        a = im[..., 3]
        op = a > 40
        gr = im[..., 1] - np.maximum(im[..., 0], im[..., 2])
        print(f'  {os.path.basename(p)}: 不透明 {int(op.sum()):>6d}px  '
              f'最大绿色度 {int(gr[op].max()) if op.any() else "-":>3}  '
              f'半透明边 {int(((a > 0) & (a < 250)).sum()):>6d}px  '
              f'连通块 {ndimage.label(op)[1]}')
    print('  最大绿色度必须 <= 0;半透明边应有几千px(羽化);连通块应为1(无碎屑)')

    if args.report and not args.dry:
        cols = 6
        tw, th = OUT_W // 2, OUT_H // 2
        rows = (len(chk) + cols - 1) // cols
        sheet = Image.new('RGB', (cols * tw, rows * th), (255, 0, 255))
        for j, p in enumerate(chk):
            t = Image.open(p).convert('RGBA').resize((tw, th), Image.LANCZOS)
            sheet.paste(t, ((j % cols) * tw, (j // cols) * th), t)
        sheet.save(args.report)
        print(f'\n目视拼图(洋红底): {args.report}')

    return 0


if __name__ == '__main__':
    sys.exit(main())
