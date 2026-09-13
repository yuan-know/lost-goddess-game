"""核验：用当前磁盘上的 d6 素材按运行时规则合成，输出齿轮局部放大图。"""
import os
import numpy as np
from PIL import Image

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\d6"
SW, SH, CW, CH = 1920, 1080, 1920, 678
CONT_CY = SH / 2.0
GEAR_CY = (CONT_CY - CH / 2.0) + (-180.0)


def load(n):
    return Image.open(os.path.join(D6, n)).convert("RGBA")


def fit(img, w, h):
    return img.resize((int(round(w)), int(round(h))), Image.LANCZOS)


def paste_center(canvas, img, cx, cy, rot=0.0):
    if rot:
        img = img.rotate(rot, resample=Image.BICUBIC, expand=True)
    w, h = img.size
    canvas.alpha_composite(img, (int(round(cx - w / 2)), int(round((SH - cy) - h / 2))))


c = Image.new("RGBA", (SW, SH), (13, 11, 10, 255))
c.alpha_composite(Image.new("RGBA", (SW, SH), (0, 0, 0, int(0.7 * 255))))
paste_center(c, fit(load("d6_bg_aperture.png"), CW, CH), SW / 2, CONT_CY)
paste_center(c, fit(load("d6_prop_dial.png"), CW, CH), SW / 2, CONT_CY)
paste_center(c, fit(load("d6_gear_wheel.png"), 520, 520), SW / 2, GEAR_CY)
paste_center(c, fit(load("d6_knob.png"), 160, 160), SW - 100, (CONT_CY - CH / 2.0) + 100)

os.makedirs(OUT, exist_ok=True)
c.convert("RGB").save(os.path.join(OUT, "核验_改后全屏.png"), quality=92)
crop = c.convert("RGB").crop((690, 1080 - 300, 1230, 1080)).resize((540 * 2, 300 * 2), Image.LANCZOS)
crop.save(os.path.join(OUT, "核验_改后齿轮局部.png"), quality=92)
print("wrote 核验_改后全屏.png / 核验_改后齿轮局部.png")

# 顺带看两种旋转档位下的样子
for deg in (72, 144):
    cc = Image.new("RGBA", (SW, SH), (13, 11, 10, 255))
    cc.alpha_composite(Image.new("RGBA", (SW, SH), (0, 0, 0, int(0.7 * 255))))
    paste_center(cc, fit(load("d6_bg_aperture.png"), CW, CH), SW / 2, CONT_CY)
    paste_center(cc, fit(load("d6_prop_dial.png"), CW, CH), SW / 2, CONT_CY)
    paste_center(cc, fit(load("d6_gear_wheel.png"), 520, 520), SW / 2, GEAR_CY, rot=-deg)
    paste_center(cc, fit(load("d6_knob.png"), 160, 160), SW - 100, (CONT_CY - CH / 2.0) + 100)
    cc.convert("RGB").crop((690, 1080 - 300, 1230, 1080)).resize((540 * 2, 300 * 2), Image.LANCZOS) \
        .save(os.path.join(OUT, f"核验_改后齿轮局部_转{-deg}度.png"), quality=92)
print("wrote 旋转档位核验图")
