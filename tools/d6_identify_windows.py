"""精准识别齿轮 5 个孔里的图案,并与 5 个道具本体图案对照。
输出两张对照条图,供确认「孔位 j ↔ 道具」的对应关系。"""
import os
import numpy as np
from PIL import Image, ImageDraw

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\d6"

# 齿轮 5 个孔心(500 图内,顺时针: 0=正上 1=右上 2=右下 3=左下 4=左上)
WINS = [(250.74, 146.72, "孔0 正上"),
        (344.26, 212.13, "孔1 右上"),
        (314.43, 328.76, "孔2 右下"),
        (182.27, 330.94, "孔3 左下"),
        (155.27, 211.85, "孔4 左上")]

# 5 个道具本体的图标 bbox(3400x1200 图内,来自 alpha 量测)
PROPS = [("Button 按钮", "d6_prop_button.png", (1354, 254, 2045, 945)),
         ("Dial 表盘", "d6_prop_dial.png", (1363, 264, 2036, 935)),
         ("Gramophone 唱片机", "d6_prop_gramophone.png", (1402, 289, 1997, 910)),
         ("LongCompass 长罗盘", "d6_prop_longcompass.png", (1400, 299, 1999, 900)),
         ("SmallCompass 小罗盘", "d6_prop_smallcompass_base.png", (1436, 267, 1963, 884))]


def load(n):
    return Image.open(os.path.join(D6, n)).convert("RGBA")


def on_bg(img, rgb=(46, 42, 38)):
    bg = Image.new("RGBA", img.size, rgb + (255,))
    bg.alpha_composite(img)
    return bg.convert("RGB")


# ── 1. 齿轮 5 孔放大条 ──
gear = load("d6_gear_wheel.png")
tiles = []
for (wx, wy, tag) in WINS:
    r = 46
    crop = gear.crop((int(wx - r), int(wy - r), int(wx + r), int(wy + r)))
    big = on_bg(crop).resize((92 * 4, 92 * 4), Image.LANCZOS)
    d = ImageDraw.Draw(big)
    d.rectangle([0, 0, big.size[0] - 1, big.size[1] - 1], outline=(255, 210, 120), width=3)
    d.text((8, 6), tag, fill=(255, 230, 160))
    tiles.append(big)
W = sum(t.size[0] for t in tiles) + 14 * (len(tiles) - 1)
strip = Image.new("RGB", (W, tiles[0].size[1]), (0, 0, 0))
x = 0
for t in tiles:
    strip.paste(t, (x, 0))
    x += t.size[0] + 14
os.makedirs(OUT, exist_ok=True)
strip.save(os.path.join(OUT, "识别_齿轮5孔图案.png"), quality=93)
print("wrote 识别_齿轮5孔图案.png", strip.size)

# ── 2. 道具本体图案条 ──
tiles2 = []
for (name, fn, box) in PROPS:
    im = on_bg(load(fn).crop(box))
    im = im.resize((300, int(300 * im.size[1] / im.size[0])), Image.LANCZOS)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, im.size[0] - 1, im.size[1] - 1], outline=(140, 200, 255), width=3)
    d.text((8, 6), name, fill=(170, 215, 255))
    tiles2.append(im)
H = max(t.size[1] for t in tiles2)
W2 = sum(t.size[0] for t in tiles2) + 14 * (len(tiles2) - 1)
strip2 = Image.new("RGB", (W2, H), (0, 0, 0))
x = 0
for t in tiles2:
    strip2.paste(t, (x, 0))
    x += t.size[0] + 14
strip2.save(os.path.join(OUT, "识别_5个道具图案.png"), quality=93)
print("wrote 识别_5个道具图案.png", strip2.size)
