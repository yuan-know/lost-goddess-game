"""按 GearDialCloseup 运行时的规则，离线合成 1920x1080 特写预览（不开 Unity）。
用来核验：齿轮到底落在哪、它的开口后面是什么、旋钮多大。
"""
import os

import numpy as np
from PIL import Image

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\d6"

SW, SH = 1920, 1080          # 参考分辨率
CW, CH = 1920, 678           # 容器
CONT_CX, CONT_CY = SW / 2.0, SH / 2.0      # 容器中心(Unity 左下原点)

# 运行时参数(抄 GearDialCloseup)
GEAR_SIZE = 520.0
GEAR_OFFSET_Y = -180.0
GEAR_ROT = 0.0
KNOB_SIZE = 160.0
KNOB_MR, KNOB_MB = 100.0, 100.0
KNOB_ROT = 0.0


def load(name):
    return Image.open(os.path.join(D6, name)).convert("RGBA")


def fit(img, w, h):
    """preserveAspect=False → 直接拉伸"""
    return img.resize((int(round(w)), int(round(h))), Image.LANCZOS)


def paste_center(canvas, img, cx_u, cy_u, rot_deg=0.0):
    """img 以自身中心为轴心，放到 Unity 坐标 (cx_u, cy_u)（左下原点）。"""
    if rot_deg:
        img = img.rotate(rot_deg, resample=Image.BICUBIC, expand=True)  # PIL 逆时针为正
    w, h = img.size
    left = int(round(cx_u - w / 2.0))
    top = int(round((SH - cy_u) - h / 2.0))      # 转成左上原点
    canvas.alpha_composite(img, (left, top))


def build(clip_gear_to_band=False, gear_offset_y=None, path="preview.png"):
    off_y = GEAR_OFFSET_Y if gear_offset_y is None else gear_offset_y
    canvas = Image.new("RGBA", (SW, SH), (13, 11, 10, 255))     # 相机底色
    dim = Image.new("RGBA", (SW, SH), (0, 0, 0, int(0.7 * 255)))  # CloseupView 半透明黑底
    canvas.alpha_composite(dim)

    band_img = Image.new("RGBA", (SW, SH), (0, 0, 0, 0))
    paste_center(band_img, fit(load("d6_bg_aperture.png"), CW, CH), CONT_CX, CONT_CY)
    paste_center(band_img, fit(load("d6_prop_dial.png"), CW, CH), CONT_CX, CONT_CY)

    if clip_gear_to_band:
        # 容器加 RectMask2D 后的效果：只保留中间条带
        m = Image.new("L", (SW, SH), 0)
        m.paste(255, (0, int(SH - (CONT_CY + CH / 2)), SW, int(SH - (CONT_CY - CH / 2))))
        band_img.putalpha(Image.fromarray(
            (np.array(band_img.getchannel("A")) * (np.array(m) / 255.0)).astype(np.uint8)))

    canvas.alpha_composite(band_img)

    # 齿轮：中心 = 容器底边 + 锚点偏移
    gear_cy = (CONT_CY - CH / 2.0) + off_y
    gear = fit(load("d6_gear_wheel.png"), GEAR_SIZE, GEAR_SIZE)
    if clip_gear_to_band:
        g = Image.new("RGBA", (SW, SH), (0, 0, 0, 0))
        paste_center(g, gear, CONT_CX, gear_cy, GEAR_ROT)
        m = Image.new("L", (SW, SH), 0)
        m.paste(255, (0, int(SH - (CONT_CY + CH / 2)), SW, int(SH - (CONT_CY - CH / 2))))
        g.putalpha(Image.fromarray(
            (np.array(g.getchannel("A")) * (np.array(m) / 255.0)).astype(np.uint8)))
        canvas.alpha_composite(g)
    else:
        paste_center(canvas, gear, CONT_CX, gear_cy, GEAR_ROT)

    knob_cx = SW - KNOB_MR
    knob_cy = (CONT_CY - CH / 2.0) + KNOB_MB
    paste_center(canvas, fit(load("d6_knob.png"), KNOB_SIZE, KNOB_SIZE), knob_cx, knob_cy, KNOB_ROT)

    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, path)
    canvas.convert("RGB").save(p, quality=92)
    print("wrote", p)
    print(f"  容器条带 y(Unity) {CONT_CY-CH/2:.0f}..{CONT_CY+CH/2:.0f}")
    print(f"  齿轮中心 y(Unity) {gear_cy:.0f}   齿轮 y {gear_cy-GEAR_SIZE/2:.0f}..{gear_cy+GEAR_SIZE/2:.0f}")
    print(f"  旋钮中心 (x={knob_cx:.0f}, y={knob_cy:.0f})")
    return p


build(False, None, "预览_当前布局.png")
build(True, None, "预览_齿轮按条带裁剪.png")
build(False, 190.0, "预览_齿轮上移至条带内.png")
