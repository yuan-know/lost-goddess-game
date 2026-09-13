"""渲染两种「齿轮 5 个圆窗看不清」的修法，供选择。
A. 把圆窗内的透明圈填成暖白珐琅盘（深色小图标变成清晰剪影）—— 改素材 PNG
B. 圆窗保持透明，但在齿轮背后垫一层「提亮的砖墙」，让窗口透出可见纹理 —— 只改代码
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\d6"
SW, SH, CW, CH = 1920, 1080, 1920, 678
CONT_CY = SH / 2.0
GEAR_SIZE = 520.0
GEAR_OFF = -180.0
GEAR_CY = (CONT_CY - CH / 2.0) + GEAR_OFF

# 齿轮素材里 5 个圆窗的圆心（500x500 图内，正五边形，半径 105）
WIN_SRC = [(250.0, 145.0), (350.0, 217.6), (312.0, 335.0), (188.0, 335.0), (150.0, 217.6)]
WIN_R_SRC = 42.0


def load(n):
    return Image.open(os.path.join(D6, n)).convert("RGBA")


def fit(img, w, h):
    return img.resize((int(round(w)), int(round(h))), Image.LANCZOS)


def paste_center(canvas, img, cx, cy, rot=0.0):
    if rot:
        img = img.rotate(rot, resample=Image.BICUBIC, expand=True)
    w, h = img.size
    canvas.alpha_composite(img, (int(round(cx - w / 2)), int(round((SH - cy) - h / 2))))


def base_scene():
    c = Image.new("RGBA", (SW, SH), (13, 11, 10, 255))
    c.alpha_composite(Image.new("RGBA", (SW, SH), (0, 0, 0, int(0.7 * 255))))
    paste_center(c, fit(load("d6_bg_aperture.png"), CW, CH), SW / 2, CONT_CY)
    paste_center(c, fit(load("d6_prop_dial.png"), CW, CH), SW / 2, CONT_CY)
    return c


def gear_filled(inner_rgb=(228, 222, 208), grad=True):
    """A 方案：把 5 个圆窗的透明部分填成暖白珐琅盘。"""
    g = load("d6_gear_wheel.png").copy()
    a = np.array(g)
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    transparent = a[:, :, 3] <= 8
    for (wx, wy) in WIN_SRC:
        d = np.sqrt((xx - wx) ** 2 + (yy - wy) ** 2)
        inside = (d <= WIN_R_SRC) & transparent
        if not inside.any():
            continue
        if grad:
            # 盘面中心亮、边缘略暗，做出珐琅/玻璃的层次
            t = np.clip(d / WIN_R_SRC, 0, 1)
            shade = 1.0 - 0.30 * t
            for c in range(3):
                plane = np.zeros_like(a[:, :, c], dtype=float)
                plane[...] = inner_rgb[c] * shade
                a[:, :, c] = np.where(inside, plane, a[:, :, c])
        else:
            for c in range(3):
                a[:, :, c] = np.where(inside, inner_rgb[c], a[:, :, c])
        a[:, :, 3] = np.where(inside, 255, a[:, :, 3])
    return Image.fromarray(a)


def gear_orig():
    return load("d6_gear_wheel.png")


def add_backdrop(canvas_before, gear, boost=3.4):
    """B 方案：在齿轮之前，先垫一层提亮的砖墙（按齿轮轮廓裁剪）。"""
    scale = SH / CH
    bg_cover = fit(load("d6_bg_aperture.png"), CW * scale, CH * scale)
    layer = Image.new("RGBA", (SW, SH), (0, 0, 0, 0))
    paste_center(layer, bg_cover, SW / 2, CONT_CY)
    arr = np.array(layer).astype(float)
    arr[:, :, :3] = np.clip(arr[:, :, :3] * boost + 26, 0, 255)
    layer = Image.fromarray(arr.astype(np.uint8))
    # 按齿轮 alpha 裁剪
    g = Image.new("RGBA", (SW, SH), (0, 0, 0, 0))
    paste_center(g, gear, SW / 2, GEAR_CY)
    mask = (np.array(g.getchannel("A")) > 8).astype(np.uint8) * 255
    mask = np.array(Image.fromarray(mask).filter(ImageFilter.GaussianBlur(0.6)))
    layer.putalpha(Image.fromarray(
        (np.array(layer.getchannel("A")).astype(int) * mask // 255).astype(np.uint8)))
    out = canvas_before.copy()
    out.alpha_composite(layer)
    return out


os.makedirs(OUT, exist_ok=True)

# 现状
c0 = base_scene()
paste_center(c0, fit(gear_orig(), GEAR_SIZE, GEAR_SIZE), SW / 2, GEAR_CY)
paste_center(c0, fit(load("d6_knob.png"), 160, 160), SW - 100, (CONT_CY - CH / 2.0) + 100)
c0.convert("RGB").save(os.path.join(OUT, "修法对比_0_现状.png"), quality=92)

# A：珐琅盘
gA = gear_filled()
cA = base_scene()
paste_center(cA, fit(gA, GEAR_SIZE, GEAR_SIZE), SW / 2, GEAR_CY)
paste_center(cA, fit(load("d6_knob.png"), 160, 160), SW - 100, (CONT_CY - CH / 2.0) + 100)
cA.convert("RGB").save(os.path.join(OUT, "修法对比_A_窗口填珐琅盘.png"), quality=92)

# B：窗口透明 + 齿轮之前垫一层提亮砖墙
cB = add_backdrop(base_scene(), gear_orig())
paste_center(cB, fit(gear_orig(), GEAR_SIZE, GEAR_SIZE), SW / 2, GEAR_CY)
paste_center(cB, fit(load("d6_knob.png"), 160, 160), SW - 100, (CONT_CY - CH / 2.0) + 100)
cB.convert("RGB").save(os.path.join(OUT, "修法对比_B_窗口透视提亮砖墙.png"), quality=92)

# 三种的齿轮局部放大并排
crop = (700, 1080 - 300, 1220, 1080)
tiles = []
for img, name in ((c0, "0 现状"), (cA, "A 珐琅盘"), (cB, "B 透视砖墙")):
    t = img.convert("RGB").crop(crop).resize((520 * 2, 300 * 2), Image.LANCZOS)
    tiles.append((name, t))
W = sum(t.size[0] for _, t in tiles) + 20 * (len(tiles) - 1)
strip = Image.new("RGB", (W, tiles[0][1].size[1]), (0, 0, 0))
x = 0
for _, t in tiles:
    strip.paste(t, (x, 0))
    x += t.size[0] + 20
strip.save(os.path.join(OUT, "修法对比_齿轮局部_0_A_B.png"), quality=92)

# 逐窗口亮度
for label, img in (("现状", c0), ("A珐琅盘", cA), ("B透视砖墙", cB)):
    a = np.array(img.convert("RGB")).astype(float)
    vals = []
    sx = GEAR_SIZE / 500.0
    for (wx, wy) in WIN_SRC:
        px = SW / 2 + (wx - 249.5) * sx
        py = GEAR_CY - (wy - 249.5) * sx
        row = int(round(SH - py))
        col = int(round(px))
        if 0 <= row < SH:
            vals.append(a[max(0, row - 10):row + 10, col - 10:col + 10].mean())
    print(f"{label:8s} 可见窗口平均亮度 {np.mean(vals):6.1f}   各窗口 {[round(v,1) for v in vals]}")

print("\nwrote 修法对比_0_现状.png / _A_窗口填珐琅盘.png / _B_窗口透视提亮砖墙.png / _齿轮局部_0_A_B.png")
