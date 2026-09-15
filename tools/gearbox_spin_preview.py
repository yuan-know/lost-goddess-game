# -*- coding: utf-8 -*-
"""
gearbox_spin_preview.py —— GearboxSpinCloseup 离线版面预览

复刻 GearboxSpinCloseup.cs 的摆放数学,把静态版面(不含序列帧)合成到
1920×678 容器上,用于**编译前**核对各零件的相对位置 / 是否出框。

坐标系(与 C# 一致):
  k = 0.635
  画布像素 (x,y) → 容器像素 (RX + (x-CX)*k, RY + (CY-y)*k),其中 CX,CY=1700,600 / RX,RY=960,339
  图层 spritePivot.y = 归一化"从底往上";贴图在容器里的左上角 =
      (anchorX - pivotX*w, anchorY - (1-pivotY)*h)   ← y 向下为正,故用 (1-pivotY)

产出: docs/gearbox/_spin_preview_rest.png / _spin_preview_solved.png
"""
import os
from PIL import Image, ImageDraw

D = r"C:/Users/yuan/lost-goddess-game/Assets/_Project/Resources/Closeups/GearboxPuzzle"
DOCS = r"C:/Users/yuan/lost-goddess-game/docs/gearbox"

K = 0.635
CX, CY = 1700.0, 600.0       # 画布中心
RX, RY = 960.0, 339.0        # 容器中心
CW, CH = 1920, 678

RECT_X0, RECT_Y0 = 1310.0, 120.0
RECT_W, RECT_H = 835.0, 988.0

ROOT = (1732.5, 1015.0)
VALVE = (1691.0, 720.0)
GEAR_S = (1550.0, 522.0)
GEAR_B = (1787.0, 350.0)
SHAFT = (1781.0, 704.0)
KNOB_PIVOT = (2042.0, 605.0)

# ★ 序列帧对齐参数(2026-09-15 实测标定,与 GearboxSpinCloseup.cs 的常量同源)
#   序列帧 = **整块齿轮箱面板**(含木框),要和 gearbox_base 的木箱逐边重合。
#   实测:静态木箱 canvas x 1326..2139 y 134..1095 / 帧内木箱 x 541..960 y 70..570
#   → 统一缩放 ≈1.9312;帧图中心落在画布 (1700.6, 607.1)。
SPIN_FIT = 1.93117
SPIN_CENTER = (1700.6, 607.1)


def pivot_of(ax, ay):
    return ((ax - RECT_X0) / RECT_W, 1.0 - (ay - RECT_Y0) / RECT_H)


def canvas_to_container(x, y):
    """画布像素 → 容器内 **top-down 像素**坐标(左上角为原点),供 PIL 贴图。
    Unity 的 anchoredPosition.y 向上为正、容器中心为原点:
        anchored_y = (CY - y) * K
        top_down_y = CH/2 - anchored_y = CH/2 - (CY - y)*K
    ⚠ 早期版本漏了这步符号换算,导致图层整体贴反/出框。"""
    return (RX + (x - CX) * K, CH / 2.0 - (CY - y) * K)


def paste(canvas, im, ax, ay, pivot):
    """把**画布像素尺寸**的贴图按 k 缩到 UI 尺寸再贴。
    ⚠ Unity 侧 Image.sizeDelta = LayerSize(=裁切尺寸×k),贴图会被缩到那个尺寸,
      所以预览也必须先 resize,否则会按 1:1 原图像素尺寸贴 → 尺寸/位置全错。
    pivot = Unity 归一化轴心(y 从底往上);top-down 下轴心距顶边 = (1-pivot.y)*h。"""
    w, h = im.size
    w, h = int(round(w * K)), int(round(h * K))
    im = im.resize((w, h), Image.LANCZOS)
    px = pivot[0] * w
    py = (1.0 - pivot[1]) * h          # 从贴图顶部往下量
    cx, cy = canvas_to_container(ax, ay)
    canvas.alpha_composite(im, (int(round(cx - px)), int(round(cy - py))))


def build(with_spin=False):
    canvas = Image.new("RGBA", (CW, CH), (14, 13, 15, 255))
    # 木箱底
    base = Image.open(os.path.join(D, "gearbox_base.png")).convert("RGBA")
    paste(canvas, base, ROOT[0], ROOT[1], pivot_of(*ROOT))
    if with_spin:
        # 旋转序列帧:源 1470×630 整块面板图。
        # ★ 它不是"内部机构层",所以**不能**按 1470*K 摆(那样只有实际一半大、还偏下);
        #   必须乘 SPIN_FIT,并把中心放在 SPIN_CENTER,才能和 gearbox_base 的木箱重合。
        sheet = Image.open(os.path.join(D, "gearspin_sheet.png")).convert("RGBA")
        fw, fh = 340, 146
        fr = sheet.crop((0, 0, fw, fh))
        w = int(round(1470 * K * SPIN_FIT))
        h = int(round(630 * K * SPIN_FIT))
        fr = fr.resize((w, h), Image.LANCZOS)
        cx, cy = canvas_to_container(SPIN_CENTER[0], SPIN_CENTER[1])
        canvas.alpha_composite(fr, (int(round(cx - w / 2)), int(round(cy - h / 2))))
    else:
        for name, pt in (("gearbox_gear_small", GEAR_S), ("gearbox_gear_big", GEAR_B),
                         ("gearbox_shaft", SHAFT), ("gearbox_valve", VALVE)):
            im = Image.open(os.path.join(D, name + ".png")).convert("RGBA")
            paste(canvas, im, pt[0], pt[1], pivot_of(*pt))
        kn = Image.open(os.path.join(D, "gearbox_knob.png")).convert("RGBA")
        # 旋钮 Image.sizeDelta = 98*K × 146*K;paste() 会按 K 缩
        paste(canvas, kn, KNOB_PIVOT[0], KNOB_PIVOT[1], (0.0, 0.5))

    # 画 1920×678 容器边框 + 热区(调试用,可选)
    d = ImageDraw.Draw(canvas)
    d.rectangle([0, 0, CW - 1, CH - 1], outline=(90, 90, 90, 255), width=1)
    for pt, r, col, tag in ((SHAFT, 118, (255, 90, 90), "SHAFT+GEAR_B 热区"),
                            (GEAR_B, 132, (255, 140, 60), None),
                            (GEAR_S, 112, (90, 200, 255), "GEAR_S"),
                            (VALVE, 96, (120, 255, 120), "VALVE")):
        cx, cy = canvas_to_container(pt[0], pt[1])
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col, width=2)
    return canvas


def build_alignment_overlay():
    """★ 核对用:静态面板(半透明绿)叠在序列帧面板(正常)上。
    两者木框应当**逐边重合** —— 错位一眼可见。"""
    canvas = Image.new("RGBA", (CW, CH), (14, 13, 15, 255))
    # 序列帧(第 1 帧)= 完整面板
    sheet = Image.open(os.path.join(D, "gearspin_sheet.png")).convert("RGBA")
    fr = sheet.crop((0, 0, 340, 146))
    w = int(round(1470 * K * SPIN_FIT)); h = int(round(630 * K * SPIN_FIT))
    fr = fr.resize((w, h), Image.LANCZOS)
    cx, cy = canvas_to_container(SPIN_CENTER[0], SPIN_CENTER[1])
    canvas.alpha_composite(fr, (int(round(cx - w / 2)), int(round(cy - h / 2))))

    # 静态 gearbox_base 染色成绿色半透明
    base = Image.open(os.path.join(D, "gearbox_base.png")).convert("RGBA")
    base = _tint(base, (60, 255, 90), 0.45)
    paste(canvas, base, ROOT[0], ROOT[1], pivot_of(*ROOT))
    return canvas


def _tint(im, rgb, alpha_mul):
    a = im.split()[3].point(lambda v: int(v * alpha_mul))
    solid = Image.new("RGBA", im.size, rgb + (0,))
    solid.putalpha(a)
    return solid


def main():
    os.makedirs(DOCS, exist_ok=True)
    build(False).convert("RGB").save(os.path.join(DOCS, "_spin_preview_rest.png"))
    build(True).convert("RGB").save(os.path.join(DOCS, "_spin_preview_solved.png"))
    build_alignment_overlay().convert("RGB").save(os.path.join(DOCS, "_spin_align_check.png"))
    print("saved _spin_preview_rest.png / _spin_preview_solved.png / _spin_align_check.png")


if __name__ == "__main__":
    main()
