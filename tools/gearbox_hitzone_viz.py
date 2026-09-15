# -*- coding: utf-8 -*-
"""
gearbox_hitzone_viz.py —— 把 HitZone() 的交互区域画在齿轮箱面板上

映射(与 C# 一致):
  u = (x - 287.5) / 287.5      v = (310 - y) / 310
  x = 287.5 + u * 287.5        y = 310 - v * 310        (v 向上为正)

★ 热区来源(2026-09-15 八修,不靠肉眼估):
  旧素材有独立零件图层(gearbox_shaft / gear_big / gear_small / valve),位置写在
  docs/gearbox/gearbox_measure.json。量各层 alpha bbox → 画布坐标 → 用"齿轮箱整体"
  在旧(813×969)与新(571×616)两套素材里的 bbox 做线性映射 → 零件在新面板里的 u/v。
  交叉验证:霍夫圆检出的大齿轮圆心 (0.117,0.584) ≈ 图层 bbox 算出的 (0.142,0.537) ✓

产出 docs/gearbox/_hitzones.png
"""
import os
import math
from PIL import Image, ImageDraw, ImageFont

D = r"C:/Users/yuan/lost-goddess-game/Assets/_Project/Resources/Closeups/GearboxPuzzle"
DOCS = r"C:/Users/yuan/lost-goddess-game/docs/gearbox"

# ══ 1 号:大传动轴 → 通关 ════════════════════════════════════════════
HIT_GEAR = (0.142, 0.537, 0.502, 0.259)       # 椭圆 中心u,v 半轴u,v(图层 gear_big)
HIT_SHAFT = (-0.045, -0.994, 0.287, 0.409)    # 矩形 u0,v0,u1,v1(图层 gear_shaft)

# ══ 2 号:其他零件 → 失败(椭圆 中心u,v 半轴u,v)═══════════════════════
# 带「图层」注释的两个是实测值,其余是按图上零件位置量的
FAIL = [
    ("F1 左上齿轮组", -0.660, 0.740, 0.270, 0.210),
    ("F2 左中大齿轮(图层 gear_small)", -0.430, 0.190, 0.400, 0.190),
    ("F3 左下大齿轮", -0.450, -0.780, 0.350, 0.220),
    ("F4 阀件(图层 valve)", -0.100, -0.240, 0.170, 0.240),
    ("F5 右中齿轮", 0.590, 0.200, 0.190, 0.170),
    ("F6 右下齿轮组", 0.550, -0.450, 0.250, 0.380),
    ("F7 右缘旋钮", 1.060, 0.200, 0.170, 0.180),
]

SCALE = 2
LEGEND_H = 300          # 底部图例带高度(画在面板外,不遮挡内容)
COL_HIT = (255, 55, 55)
COL_FAIL = (70, 155, 255)
FONT = FONT_S = None
for p in (r"C:/Windows/Fonts/msyh.ttc", r"C:/Windows/Fonts/simhei.ttf"):
    if os.path.exists(p):
        try:
            FONT = ImageFont.truetype(p, 30)
            FONT_S = ImageFont.truetype(p, 20)
            break
        except Exception:
            pass
if FONT is None:
    FONT = FONT_S = ImageFont.load_default()


def main():
    st = Image.open(os.path.join(D, "gearbox_static.png")).convert("RGBA")
    W, H = st.size
    CX, CY = W / 2.0, H / 2.0

    def PX(u, v):
        return ((CX + u * CX) * SCALE, (CY - v * CY) * SCALE)

    def rect_px(u0, v0, u1, v1):
        x0, y0 = PX(u0, v1)
        x1, y1 = PX(u1, v0)
        return [x0, y0, x1, y1]

    def ell_px(cu, cv, ru, rv):
        x0, y0 = PX(cu - ru, cv + rv)
        x1, y1 = PX(cu + ru, cv - rv)
        return [x0, y0, x1, y1]

    big = st.resize((W * SCALE, H * SCALE), Image.LANCZOS)
    ov = Image.new("RGBA", big.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)

    # 2 号兜底:整块面板
    d.rectangle(rect_px(-1, -1, 1, 1), fill=COL_FAIL + (46,), outline=COL_FAIL + (150,), width=2)
    # 2 号:各零件失败区
    for (name, cu, cv, ru, rv) in FAIL:
        d.ellipse(ell_px(cu, cv, ru, rv), fill=COL_FAIL + (74,), outline=COL_FAIL + (235,), width=3)
        x, y = PX(cu, cv)
        tag = name.split()[0]
        d.text((x - 12, y - 12), tag, font=FONT_S, fill=(255, 255, 255, 255),
               stroke_width=3, stroke_fill=(0, 0, 0, 235))

    # 1 号
    d.ellipse(ell_px(*HIT_GEAR), fill=COL_HIT + (92,), outline=COL_HIT + (255,), width=4)
    d.rectangle(rect_px(*HIT_SHAFT), fill=COL_HIT + (92,), outline=COL_HIT + (255,), width=4)

    out = Image.alpha_composite(big, ov)

    gd = ImageDraw.Draw(out)
    for i in range(-10, 11):
        u = i / 10.0
        xx = PX(u, 0)[0]
        if 0 <= xx < out.width:
            gd.line([(xx, 0), (xx, out.height)], fill=(255, 255, 0, 55), width=1)
            if i % 2 == 0 and i:
                gd.text((xx + 3, 4), "u=%+.1f" % u, font=FONT_S,
                        fill=(255, 240, 120, 235), stroke_width=3, stroke_fill=(0, 0, 0, 220))
    for j in range(-10, 11):
        v = j / 10.0
        yy = PX(0, v)[1]
        if 0 <= yy < out.height:
            gd.line([(0, yy), (out.width, yy)], fill=(255, 255, 0, 55), width=1)
            if j % 2 == 0 and j:
                gd.text((4, yy + 3), "v=%+.1f" % v, font=FONT_S,
                        fill=(255, 240, 120, 235), stroke_width=3, stroke_fill=(0, 0, 0, 220))

    x, y = PX(0.142, 0.60)
    gd.text((x - 12, y - 18), "1", font=FONT, fill=(255, 255, 255, 255),
            stroke_width=4, stroke_fill=(0, 0, 0, 240))
    x, y = PX(0.10, -0.40)
    gd.text((x - 12, y - 18), "1", font=FONT, fill=(255, 255, 255, 255),
            stroke_width=4, stroke_fill=(0, 0, 0, 240))

    rows = [("红 1 = 大传动轴(大齿轮椭圆 + 竖轴矩形)→ 通关", COL_HIT),
            ("蓝 F = 各零件失败区 → 失败", COL_FAIL),
            ("蓝底 = 面板兜底(面板内其余位置一律失败)", (150, 150, 160))]
    for (name, cu, cv, ru, rv) in FAIL:
        rows.append(("  %s  u%+.3f v%+.3f 半轴%.2f/%.2f" % (name, cu, cv, ru, rv),
                     (190, 190, 190)))

    # ── 底部图例带(画在面板**外**,不遮挡任何内容)──────────────────
    lh = 34
    total_h = out.height + LEGEND_H
    canvas = Image.new("RGBA", (out.width, total_h), (16, 16, 20, 255))
    canvas.alpha_composite(out, (0, 0))
    gd2 = ImageDraw.Draw(canvas)
    gd2.line([(0, out.height), (out.width, out.height)], fill=(90, 90, 100, 255), width=2)
    for i, (txt, col) in enumerate(rows):
        yy = out.height + 12 + i * lh
        if yy + lh > total_h:
            break
        gd2.rectangle([20, yy + 5, 44, yy + 23], fill=col + (205,),
                      outline=col + (255,), width=2)
        gd2.text((54, yy), txt, font=FONT_S, fill=(255, 255, 255, 255))

    os.makedirs(DOCS, exist_ok=True)
    canvas.convert("RGB").save(os.path.join(DOCS, "_hitzones.png"))
    print("saved _hitzones.png (%dx%d)  面板区 %dx%d + 图例 %d"
          % (canvas.width, canvas.height, out.width, out.height, LEGEND_H))
    print()
    print("C# HitZone() 判定:")
    print("  float gu = (u - %.3ff) / %.3ff, gv = (v - %.3ff) / %.3ff;"
          % (HIT_GEAR[0], HIT_GEAR[2], HIT_GEAR[1], HIT_GEAR[3]))
    print("  if (gu * gu + gv * gv <= 1f) return 1;")
    print("  if (u >= %.3ff && u <= %.3ff && v >= %.3ff && v <= %.3ff) return 1;"
          % (HIT_SHAFT[0], HIT_SHAFT[2], HIT_SHAFT[1], HIT_SHAFT[3]))
    for (name, cu, cv, ru, rv) in FAIL:
        print("  if (InEllipse(u, v, %+.3ff, %+.3ff, %.3ff, %.3ff)) return 2;   // %s"
              % (cu, cv, ru, rv, name))


if __name__ == "__main__":
    main()
