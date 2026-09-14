"""端到端验证:按 C# 的 SlotToAngle 把齿轮转到 5 个槽位的角度,
裁出「正上方孔位」的图案,确认每个槽位正上方显示的就是该道具的图标。"""
import os
import numpy as np
from PIL import Image, ImageDraw

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\d6"
SW, SH, CW, CH = 1920, 1080, 1920, 678
GEAR_CY = (SH / 2.0 - CH / 2.0) + (-180.0)
K = [2, 3, 4, 1, 0]                      # 孔0..4 -> 槽位
NAMES = ["Button 按钮", "Dial 表盘", "Gramophone 唱片机",
         "LongCompass 长罗盘", "SmallCompass 小罗盘"]


def load(n):
    return Image.open(os.path.join(D6, n)).convert("RGBA")


def fit(img, w, h):
    return img.resize((int(round(w)), int(round(h))), Image.LANCZOS)


def paste(canvas, img, cx, cy, rot=0.0):
    if rot:
        img = img.rotate(rot, resample=Image.BICUBIC, expand=True)
    w, h = img.size
    canvas.alpha_composite(img, (int(round(cx - w / 2)), int(round((SH - cy) - h / 2))))


def scene(gear_img, rot):
    c = Image.new("RGBA", (SW, SH), (13, 11, 10, 255))
    c.alpha_composite(Image.new("RGBA", (SW, SH), (0, 0, 0, int(0.7 * 255))))
    paste(c, fit(load("d6_bg_aperture.png"), CW, CH), SW / 2, SH / 2)
    paste(c, fit(load("d6_prop_dial.png"), CW, CH), SW / 2, SH / 2)
    paste(c, fit(gear_img, 520, 520), SW / 2, GEAR_CY, rot)
    paste(c, fit(load("d6_knob.png"), 160, 160), SW - 100, (SH / 2 - CH / 2) + 100)
    return c


def slot_to_angle(slot):
    w = K.index(slot)
    return (72 * ((5 - w) % 5)) % 360


gear = load("d6_gear_wheel.png")
tiles = []
for slot in range(5):
    ang = slot_to_angle(slot)
    sc = scene(gear, -ang)          # Unity z = -angle(PIL rotate 正值=逆时针 → -ang 即顺时针)
    # 正上方孔位:齿轮中心 (960,21) + 半径 109 向上 → Unity y≈130 → 图像行≈950
    crop = sc.convert("RGB").crop((900, 890, 1020, 1010)).resize((240, 240), Image.LANCZOS)
    d = ImageDraw.Draw(crop)
    d.rectangle([0, 0, 239, 239], outline=(255, 210, 120), width=3)
    d.text((6, 4), f"槽{slot} {NAMES[slot]}", fill=(255, 230, 160))
    d.text((6, 222), f"齿轮 {ang:.0f}°", fill=(180, 220, 255))
    tiles.append(crop)

W = sum(t.size[0] for t in tiles) + 14 * (len(tiles) - 1)
strip = Image.new("RGB", (W, tiles[0].size[1]), (0, 0, 0))
x = 0
for t in tiles:
    strip.paste(t, (x, 0))
    x += t.size[0] + 14
os.makedirs(OUT, exist_ok=True)
strip.save(os.path.join(OUT, "验证_对应关系_5槽位正上方孔.png"), quality=93)
print("wrote 验证_对应关系_5槽位正上方孔.png", strip.size)
print("槽位->角度:", {s: round(slot_to_angle(s)) for s in range(5)})
