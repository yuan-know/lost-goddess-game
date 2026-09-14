# -*- coding: utf-8 -*-
"""
beam_look_preview.py —— 离线预演「光锥观感」:旧版(过曝亮芯) vs 新版(散光柔片)

复刻 EyeCorridorLaserPuzzle 的加色渲染数学(Legacy Particles/Additive):
    add = 2 * tintRGB * vtxAlpha * tintAlpha * texAlpha
其中 tintRGB = color.rgb*0.5、tintAlpha = BeamAlpha、texAlpha 由程序化贴图给(见 ConeTex)、
vtxAlpha 是 mesh 顶点色(顶点靠铜眼那头压暗)。

产出: docs/千眼回廊/光束观感对照_旧vs新.png
"""
import os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, 'Assets/_Project/Resources/Scenes/ChaseCorridor')
OUT = os.path.join(ROOT, 'docs/千眼回廊/光束观感对照_旧vs新.png')
FONT = os.path.join(ROOT, 'Assets/_Project/Resources/Fonts/LXGWWenKai-Regular.ttf')

PPU = 100.0
HALF = 17.0
EYE_X, EYE_Y = -0.075, 4.30
GROUND_Y = -5.76
BEAM_HALF_W = 3.75
RESET_X = 9.3

BG_W, BG_H = 3400, 1200

# 光束色 —— 必须和 EyeCorridorLaserPuzzle.SyncBeamVisual() 里的 SetMatColor 保持同步。
# 2026-09-14 用户要求「把光调成白色」:暖黄 (1, 0.95, 0.84) → 纯白 (1, 1, 1)。
# 要预演别的色相,改这里即可(或 `import beam_look_preview as p; p.TINT = np.array([...])`)。
TINT = np.array([1.0, 1.0, 1.0])

# ── 铜眼光晕(EyeCorridorLaserPuzzle 的 _eyeGlow)──
# GlowSprite 是 256×256 + ppu=S/2(128) ⇒ 精灵 2×2 单位,pivot 居中 ⇒ **精灵半径正好 1 单位**,
# 所以 localScale = s 时光晕半径就是 s 个世界单位(localScale 2.2 ⇒ 直径 4.4 单位 = 440px)。
# 加色量:2 * iColor * _TintColor(0.5) * texA = alpha * texA,texA = (1-d)^3(d 归一化到半径)。
EYE_GLOW_ALPHA = 0.30
EYE_GLOW_SCALE = 2.2
EYE_GLOW_TINT  = np.array([1.0, 0.93, 0.72])


def px(wx):
    return (wx + HALF) * PPU


def py(wy):
    return (6.0 - wy) * PPU


def hash01(x, y):
    h = (x * 374761393 + y * 668265263) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    h ^= h >> 16
    return (h & 0x7fffffff) / float(0x7fffffff)


def vnoise(fx, fy):
    x0, y0 = int(math.floor(fx)), int(math.floor(fy))
    tx, ty = fx - x0, fy - y0
    tx = tx * tx * (3 - 2 * tx)
    ty = ty * ty * (3 - 2 * ty)
    a, b = hash01(x0, y0), hash01(x0 + 1, y0)
    c, d = hash01(x0, y0 + 1), hash01(x0 + 1, y0 + 1)
    return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty


def smoothstep(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0))) if e1 != e0 else 0.0
    return t * t * (3 - 2 * t)


def smoothstep_np(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def _vnoise_np(fx, fy):
    x0 = np.floor(fx).astype(np.int64)
    y0 = np.floor(fy).astype(np.int64)
    tx = fx - x0
    ty = fy - y0
    tx = tx * tx * (3 - 2 * tx)
    ty = ty * ty * (3 - 2 * ty)
    def h(x, y):
        v = (x * 374761393 + y * 668265263).astype(np.int64)
        v = ((v ^ (v >> 13)) * 1274126177).astype(np.int64)
        v = v ^ (v >> 16)
        return (v & 0x7fffffff) / float(0x7fffffff)
    a, b = h(x0, y0), h(x0 + 1, y0)
    c, d = h(x0, y0 + 1), h(x0 + 1, y0 + 1)
    l1 = a + (b - a) * tx
    l2 = c + (d - c) * tx
    return l1 + (l2 - l1) * ty


def tex_alpha_new(u, v):
    """新版 ConeTex:纯柔边(无平台) → 一层光雾,不是一块板。"""
    u = np.asarray(u, dtype=np.float64)
    e = np.minimum(u, 1.0 - u) * 2.0
    lat = smoothstep_np(0.0, 1.0, e)
    n = _vnoise_np(u * 11, v * 17) * 0.65 + _vnoise_np(u * 29, v * 37) * 0.35
    return np.clip(lat * (0.86 + 0.14 * n), 0.0, 1.0)


def ground_bar(alpha, height=1.3, span=1.0 / 1.06):
    """地面亮条:中间平台 + 两端极窄柔降(危险边界的读数)。

    ★span 必须跟 C# 的 GroundBarSprite() 一致:平台(alpha=1)边界严格落在
      ±BeamGroundHalfWidth = 危险判定线上,平台以外只剩 BarBleed(1.06) 那点余量做柔降。
      2026-09-14 之前这里是 0.28(C# 的 BeamBarEdge=0.14×2),配合当时精灵被放成
      2 倍宽(实机 15.9 单位)刚好让平台盖住危险带 —— 但那 4.2 单位/侧的地面亮光是
      "非光照区域的泛光"。现在平台与危险带严格对齐,不再外溢。
    """
    h = 90
    fx = np.linspace(-1.0, 1.0, 400)[None, :]
    e = np.minimum(1.0 - fx, 1.0 + fx)          # 0=两端 1=中间
    lat = smoothstep_np(0.0, 1.0, np.clip(e / span, 0.0, 1.0))
    fy = np.linspace(-1.0, 1.0, h)[:, None]
    vert = (1.0 - np.clip(np.abs(fy), 0, 1)) ** 1.4
    return np.clip(lat * vert, 0, 1) * alpha


def tex_alpha_old(u, v):
    """旧版 ConeTex:越靠顶点越实 + 中心亮芯。"""
    u = np.asarray(u, dtype=np.float64)
    core = smoothstep_np(0.55, 1.0, v)
    lat = np.maximum(0.0, 1.0 - np.abs(u - 0.5) * 2.0)
    lat = lat * lat * (0.35 + 0.65 * core)
    return np.clip(lat * (0.55 + 0.45 * v), 0.0, 1.0)


def render(canvas, beams, alpha, tex_fn, vtx_apex, core_alpha=0.0, bar_alpha=0.0):
    """把光锥以加色方式叠到 canvas(float32, 0..1) 上。"""
    vx, vy = px(EYE_X), py(EYE_Y)
    gy = py(GROUND_Y)
    h, w = canvas.shape[:2]
    ys = np.arange(vy, gy + 1)
    t = (ys - vy) / (gy - vy)                      # 0=顶点 1=底边
    tint = TINT
    for bx in beams:
        x1 = px(bx - BEAM_HALF_W)
        x2 = px(bx + BEAM_HALF_W)
        for k, y in enumerate(ys):
            tt = t[k]
            left = vx + (x1 - vx) * tt
            right = vx + (x2 - vx) * tt
            if right - left < 1:
                continue
            x0 = int(max(0, math.floor(left)))
            x1i = int(min(w - 1, math.ceil(right)))
            if x1i <= x0:
                continue
            xs = np.arange(x0, x1i + 1)
            u = (xs - left) / (right - left)
            v = 1.0 - tt                            # 贴图 v:顶点=1 底边=0
            ta = tex_fn(u, v)
            va = vtx_apex + (1.0 - vtx_apex) * tt    # 顶点色:顶点压暗
            add = 2.0 * alpha * ta * va
            idx = int(round(y))
            if 0 <= idx < h:
                canvas[idx, x0:x1i + 1] += add[:, None] * tint
        if core_alpha > 0:                          # 旧版那层高亮内核(收窄 34%)
            k2 = 0.34
            cx = px(bx)
            for k, y in enumerate(ys):
                tt = t[k]
                a1 = vx + (x1 - vx) * tt
                a2 = vx + (x2 - vx) * tt
                left = a1 + (cx - a1) * (1.0 - k2)
                right = a2 - (a2 - cx) * k2
                if right - left < 1:
                    continue
                x0 = int(max(0, math.floor(left)))
                x1i = int(min(w - 1, math.ceil(right)))
                if x1i <= x0:
                    continue
                xs = np.arange(x0, x1i + 1)
                u = (xs - left) / (right - left)
                ta = tex_fn(u, 1.0 - tt)
                va = vtx_apex + (1.0 - vtx_apex) * tt
                idx = int(round(y))
                if 0 <= idx < h:
                    canvas[idx, x0:x1i + 1] += (2.0 * core_alpha * ta * va)[:, None] * np.array([1.0, 0.99, 1.0])
        if bar_alpha > 0:                           # 地面亮条
            bar = ground_bar(bar_alpha)             # h×400, alpha 已含
            bh, bw = bar.shape
            y0 = int(gy - bh + 6)
            xa = int(px(bx - BEAM_HALF_W * 1.06))
            xb = int(px(bx + BEAM_HALF_W * 1.06))
            for r in range(bh):
                yy = y0 + r
                if not (0 <= yy < h):
                    continue
                cols = np.linspace(0, bw - 1, max(1, xb - xa)).astype(int)
                seg = bar[r, cols]
                xa2 = max(0, xa); xb2 = min(w, xb)
                if xb2 <= xa2:
                    continue
                canvas[yy, xa2:xb2] += seg[xa2 - xa:xb2 - xa, None] * tint
    return canvas


def panel(bg, title, sub, alpha, tex_fn, vtx_apex, core, beams, bar=0.0):
    c = bg.astype(np.float32).copy()
    c /= 255.0
    c = render(c, beams, alpha, tex_fn, vtx_apex, core, bar)
    c = np.clip(c, 0, 1) * 255.0
    im = Image.fromarray(c.astype(np.uint8))
    d = ImageDraw.Draw(im)
    try:
        f1 = ImageFont.truetype(FONT, 62)
        f2 = ImageFont.truetype(FONT, 40)
    except Exception:
        f1 = f2 = ImageFont.load_default()
    d.text((40, 24), title, font=f1, fill=(255, 240, 200), stroke_width=4, stroke_fill=(0, 0, 0))
    d.text((40, 104), sub, font=f2, fill=(225, 225, 225), stroke_width=3, stroke_fill=(0, 0, 0))
    return im


def main():
    bg = np.asarray(Image.open(os.path.join(BASE, 'bg_full.png')).convert('RGB'))
    mask = np.asarray(Image.open(os.path.join(BASE, 'bg_gate_shadow.png')).convert('RGBA')
                      .resize((BG_W, BG_H)))[:, :, 3].astype(np.float32) / 255.0
    bg = (bg.astype(np.float32) * (1.0 - (mask * 0.392)[:, :, None]))   # 叠上拱门暗影,和实机一致

    beams = [RESET_X, -RESET_X]

    old = panel(bg, '旧观感(你看到的)',
                '光锥 alpha 0.30 + 一层收窄 34%% 的亮芯 alpha 0.68 → 中心过曝,像一束细激光',
                0.30, tex_alpha_old, 1.0, 0.68, beams)
    new = panel(bg, '新观感(已改)',
                '光雾 alpha 0.11(纯柔边、带颗粒、顶点压暗) + 地面亮条 alpha 0.22 → 一片散光,边界靠地面读',
                0.11, tex_alpha_new, 0.50, 0.0, beams, bar=0.22)

    # 局部放大:看边缘柔不柔
    box = (int(px(-6.0)), int(py(2.2)), int(px(2.2)), int(py(-4.2)))
    zcrop = 2
    zo = old.crop(box); zn = new.crop(box)
    zw, zh = zo.size
    zo = zo.resize((zw * zcrop, zh * zcrop), Image.LANCZOS)
    zn = zn.resize((zw * zcrop, zh * zcrop), Image.LANCZOS)

    W = 3400
    sheet = Image.new('RGB', (W, 1200 * 2 + 40), (14, 14, 18))
    sheet.paste(old.resize((W, 1200), Image.LANCZOS), (0, 0))
    sheet.paste(new.resize((W, 1200), Image.LANCZOS), (0, 1240))
    sheet = sheet.resize((W // 3, (1200 * 2 + 40) // 3), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sheet.save(OUT)
    print('->', OUT, sheet.size)

    OUT2 = OUT.replace('.png', '_局部放大.png')
    sh2 = Image.new('RGB', (zo.width * 2 + 24, zo.height), (14, 14, 18))
    sh2.paste(zo, (0, 0)); sh2.paste(zn, (zo.width + 24, 0))
    d = ImageDraw.Draw(sh2)
    try:
        f = ImageFont.truetype(FONT, 34)
    except Exception:
        f = ImageFont.load_default()
    d.text((12, 12), '旧:中心烧白 + 硬边', font=f, fill=(255, 235, 200), stroke_width=3, stroke_fill=(0, 0, 0))
    d.text((zo.width + 36, 12), '新:柔边散光,读得出边界', font=f, fill=(210, 240, 255), stroke_width=3, stroke_fill=(0, 0, 0))
    sh2.save(OUT2)
    print('->', OUT2, sh2.size)


if __name__ == '__main__':
    main()
