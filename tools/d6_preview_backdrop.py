"""1) 看道具图层的 alpha 分布（判断它是不是"只有图标不透明"的图层）
2) 试一版修法：把背景砖墙复制一份、按齿轮轮廓裁剪后垫在齿轮背后，
   让齿轮的透明圆窗能看到背景（且不影响齿轮以外的黑边）。"""
import os
import numpy as np
from PIL import Image, ImageDraw

D6 = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Closeups\d6"
OUT = r"C:\Users\yuan\lost-goddess-game\docs\d6"
SW, SH, CW, CH = 1920, 1080, 1920, 678
CONT_CY = SH / 2.0

# 道具图层 alpha 统计
print("=== 道具图层 alpha ===")
for n in ["d6_prop_button.png", "d6_prop_dial.png", "d6_prop_gramophone.png",
          "d6_prop_longcompass.png", "d6_prop_smallcompass_base.png",
          "d6_prop_smallcompass_needle.png"]:
    a = np.array(Image.open(os.path.join(D6, n)).convert("RGBA"))
    al = a[:, :, 3]
    solid = al > 8
    ys, xs = np.nonzero(solid)
    print(f"  {n:38s} 不透明 {solid.mean()*100:5.1f}%   "
          f"bbox x[{xs.min()}..{xs.max()}] y[{ys.min()}..{ys.max()}]")


def load(n):
    return Image.open(os.path.join(D6, n)).convert("RGBA")


def fit(img, w, h):
    return img.resize((int(round(w)), int(round(h))), Image.LANCZOS)


def paste_center(canvas, img, cx, cy, rot=0.0):
    if rot:
        img = img.rotate(rot, resample=Image.BICUBIC, expand=True)
    w, h = img.size
    canvas.alpha_composite(img, (int(round(cx - w / 2)), int(round((SH - cy) - h / 2))))


gear = fit(load("d6_gear_wheel.png"), 520, 520)
gear_cy = (CONT_CY - CH / 2.0) + (-180.0)          # 21

canvas = Image.new("RGBA", (SW, SH), (13, 11, 10, 255))
canvas.alpha_composite(Image.new("RGBA", (SW, SH), (0, 0, 0, int(0.7 * 255))))
paste_center(canvas, fit(load("d6_bg_aperture.png"), CW, CH), SW / 2, CONT_CY)
paste_center(canvas, fit(load("d6_prop_dial.png"), CW, CH), SW / 2, CONT_CY)

# ── 修法：背景砖墙「延伸」到齿轮背后，但按齿轮轮廓裁剪 ──
# 背景整幅铺到 1920x1080（保持 3400:1200 的比例放大到覆盖整个屏幕高度），
# 这样齿轮区域的砖纹和条带里的砖纹是连续的（条带本来就占满宽度）。
sx = SW / 3400.0
bg_big = fit(load("d6_bg_aperture.png"), 3400 * sx, 1200 * sx)      # 1920x678? 不，这是整幅
# 真正要的是：以条带为基准，把背景向上/下延展。这里用「条带同宽、按同比例放大到覆盖全屏」
scale = SH / CH
bg_cover = fit(load("d6_bg_aperture.png"), CW * scale, CH * scale)
print(f"\n背景延伸到全屏: {bg_cover.size}")

backdrop = Image.new("RGBA", (SW, SH), (0, 0, 0, 0))
paste_center(backdrop, bg_cover, SW / 2, CONT_CY)

# 用齿轮 alpha 当蒙版，只保留齿轮形状范围内的背景
mask = Image.new("L", (SW, SH), 0)
paste_center(mask.convert("RGBA").convert("RGBA"), Image.new("RGBA", (1, 1)), 0, 0)  # noop
g_layer = Image.new("RGBA", (SW, SH), (0, 0, 0, 0))
paste_center(g_layer, gear, SW / 2, gear_cy)
g_alpha = np.array(g_layer.getchannel("A"))
backdrop.putalpha(Image.fromarray((np.array(backdrop.getchannel("A")) *
                                   (g_alpha > 8).astype(np.uint8)).astype(np.uint8)))
canvas.alpha_composite(backdrop)
paste_center(canvas, gear, SW / 2, gear_cy)
paste_center(canvas, fit(load("d6_knob.png"), 160, 160), SW - 100, (CONT_CY - CH / 2.0) + 100)

os.makedirs(OUT, exist_ok=True)
p = os.path.join(OUT, "预览_齿轮背后垫背景砖墙.png")
canvas.convert("RGB").save(p, quality=92)
print("wrote", p)

# 顶部/底部黑边是否被波及：统计齿轮左右 300px 外的底栏亮度
arr = np.array(canvas.convert("RGB"))
bottom = arr[SH - 150:SH - 30, :, :]
print(f"底部黑边平均亮度 {bottom.mean():.1f}（若明显 >15 说明黑边被砖墙污染）")
