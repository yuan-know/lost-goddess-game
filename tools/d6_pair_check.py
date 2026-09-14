"""孔图案 vs 道具图案 一一配对验证:每个孔旁边放我判断的道具本体图。"""
import os
from PIL import Image, ImageDraw

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\d6"

WINS = [(250.74, 146.72, "孔0 正上"), (344.26, 212.13, "孔1 右上"),
        (314.43, 328.76, "孔2 右下"), (182.27, 330.94, "孔3 左下"),
        (155.27, 211.85, "孔4 左上")]
PROPS = {"Button": ("d6_prop_button.png", (1354, 254, 2045, 945)),
         "Dial": ("d6_prop_dial.png", (1363, 264, 2036, 935)),
         "Gramophone": ("d6_prop_gramophone.png", (1402, 289, 1997, 910)),
         "LongCompass": ("d6_prop_longcompass.png", (1400, 299, 1999, 900)),
         "SmallCompass": ("d6_prop_smallcompass_base.png", (1436, 267, 1963, 884))}
# 我判断的配对(待验证)
PAIR = [("孔0 正上", "Gramophone", (250.74, 146.72)),
        ("孔1 右上", "LongCompass", (344.26, 212.13)),
        ("孔2 右下", "SmallCompass", (314.43, 328.76)),
        ("孔3 左下", "Dial", (182.27, 330.94)),
        ("孔4 左上", "Button", (155.27, 211.85))]


def load(n):
    return Image.open(os.path.join(D6, n)).convert("RGBA")


def on_bg(img, rgb=(46, 42, 38)):
    bg = Image.new("RGBA", img.size, rgb + (255,))
    bg.alpha_composite(img)
    return bg.convert("RGB")


gear = load("d6_gear_wheel.png")
CELL = 300
rows = []
for (wtag, pname, (wx, wy)) in PAIR:
    r = 46
    a = on_bg(gear.crop((int(wx - r), int(wy - r), int(wx + r), int(wy + r))))
    a = a.resize((CELL, CELL), Image.LANCZOS)
    da = ImageDraw.Draw(a)
    da.rectangle([0, 0, CELL - 1, CELL - 1], outline=(255, 210, 120), width=3)
    da.text((8, 6), wtag, fill=(255, 230, 160))
    bimg, bbox = PROPS[pname]
    b = load(bimg).crop(bbox)
    b = on_bg(b).resize((CELL, CELL), Image.LANCZOS)
    db = ImageDraw.Draw(b)
    db.rectangle([0, 0, CELL - 1, CELL - 1], outline=(140, 200, 255), width=3)
    db.text((8, 6), pname, fill=(170, 215, 255))
    row = Image.new("RGB", (CELL * 2 + 10, CELL), (0, 0, 0))
    row.paste(a, (0, 0))
    row.paste(b, (CELL + 10, 0))
    rows.append(row)

W = sum(r.size[0] for r in rows) + 16 * (len(rows) - 1)
strip = Image.new("RGB", (W, rows[0].size[1]), (0, 0, 0))
x = 0
for r in rows:
    strip.paste(r, (x, 0))
    x += r.size[0] + 16
os.makedirs(OUT, exist_ok=True)
strip.save(os.path.join(OUT, "配对验证_孔vs道具.png"), quality=93)
print("wrote 配对验证_孔vs道具.png", strip.size)
