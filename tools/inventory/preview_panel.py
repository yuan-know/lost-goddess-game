# -*- coding: utf-8 -*-
"""离线预览:新版背包底图 + 现有道具放进 4x4 格子。

★ 本脚本与 C# 的 InventoryDebugBootstrap 用**同一套**排版算法,所以这张图就是
  Unity 里会看到的样子(Unity 只是多了 letterbox 黑边与 dim 遮罩)。

算法:
  k        = 显示高度 / 908         (底图等比缩放系数)
  格子占用框 = (TileW*k*fill, TileH*k*fill)
  道具按 **内容框**(alpha 外框,不是整张 500x500)等比 contain 进占用框,
  再把内容框中心对齐到格子中心 —— 所以自带留白大的图标不会被"看起来变小"。
"""
import os
import sys
import json
import numpy as np
from PIL import Image, ImageDraw

sys.stdout.reconfigure(encoding='utf-8')

ROOT = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources"
PANEL = os.path.join(ROOT, r"UI\Inventory\bag_panel_v2.png")
DOCS = r"C:\Users\yuan\lost-goddess-game\docs\inventory"
JSONP = r"C:\Users\yuan\lost-goddess-game\tools\inventory\panel_grid.json"

G = json.load(open(JSONP, encoding="utf-8"))
PANEL_W, PANEL_H = G["panel_w"], G["panel_h"]
SLOT_CX, SLOT_CY = G["slot_cx"], G["slot_cy"]      # 逐格实测中心(排版真源)
TILE_W, TILE_H = G["tile_w"], G["tile_h"]

BAND_W, BAND_H = 1920, 644
PANEL_H_RATIO = 0.94

# (itemId, 图标路径, 中文名)  —— 顺序 = 填格顺序
ITEMS = [
    ("crowbar",         "UI/Icons/crowbar_v2",      "铁撬棍"),
    ("wrench",          "UI/Icons/wrench",          "铸铁扳手"),
    ("hex_wrench",      "UI/Icons/hex_wrench",      "六角扳手"),
    ("pottery_key",     "UI/Icons/pottery_key",     "陶罐钥匙"),
    ("focus_lens",      "UI/Icons/focus_lens_v2",   "聚焦透镜"),
    ("brass_base",      "UI/Icons/brass_base_v2",   "黄铜底座"),
    ("gear_1",          "UI/Icons/gear_small_v2",   "小齿轮"),
    ("gear_2",          "UI/Icons/gear_big_v2",     "大齿轮"),
    ("light_projector", "Props/Prologue/prop_projector", "光幕投影仪"),
]
EXTRA = [
    ("lantern",   "UI/Icons/hand_lamp", "提灯*"),
    ("magnifier", "UI/Icons/magnifier", "放大镜*"),
]


def load(rel):
    return Image.open(os.path.join(ROOT, rel.replace("/", os.sep) + ".png")).convert("RGBA")


def content_bbox(im):
    a = np.asarray(im)[..., 3]
    ys, xs = np.where(a > 16)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def fit_contain(cw, ch, bw, bh):
    s = min(bw / cw, bh / ch)
    return cw * s, ch * s


def layout(fill, items):
    """返回 (panel_img_scaled, [ (贴图, 左上角屏幕坐标) ... ])"""
    panel = Image.open(PANEL).convert("RGBA")
    k = (BAND_H * PANEL_H_RATIO) / PANEL_H
    pw, ph = int(round(PANEL_W * k)), int(round(BAND_H * PANEL_H_RATIO))
    panel = panel.resize((pw, ph), Image.LANCZOS)

    ox, oy = (BAND_W - pw) // 2, (BAND_H - ph) // 2
    placed = []
    for i, (item, rel, cn) in enumerate(items):
        icon = load(rel)
        x0, y0, x1, y1 = content_bbox(icon)
        cw, ch = x1 - x0, y1 - y0
        bw, bh = TILE_W * k * fill, TILE_H * k * fill
        dw, dh = fit_contain(cw, ch, bw, bh)
        s = dw / cw
        icon = icon.resize((max(1, int(round(icon.width * s))),
                            max(1, int(round(icon.height * s)))), Image.LANCZOS)
        ccx, ccy = (x0 + x1) / 2.0 * s, (y0 + y1) / 2.0 * s   # 内容中心在缩放后贴图里的位置
        tx = ox + SLOT_CX[i] * k
        ty = oy + SLOT_CY[i] * k
        px = tx - ccx
        py = ty - ccy
        placed.append((icon, px, py, tx, ty))
    return panel, (ox, oy, pw, ph, k), placed


def render(fill, items, draw_marks=False, bg=(8, 6, 10)):
    canvas = Image.new("RGBA", (BAND_W, BAND_H), bg + (255,))
    panel, (ox, oy, pw, ph, k), placed = layout(fill, items)
    canvas.alpha_composite(panel, (ox, oy))
    for icon, px, py, tx, ty in placed:
        canvas.alpha_composite(icon, (int(round(px)), int(round(py))))
    if draw_marks:
        d = ImageDraw.Draw(canvas)
        for i in range(16):
            tx = ox + SLOT_CX[i] * k
            ty = oy + SLOT_CY[i] * k
            hw, hh = TILE_W * k / 2, TILE_H * k / 2
            d.rectangle([tx - hw, ty - hh, tx + hw, ty + hh], outline=(255, 40, 40, 255))
            d.line([tx - 8, ty, tx + 8, ty], fill=(0, 200, 255, 255))
            d.line([tx, ty - 8, tx, ty + 8], fill=(0, 200, 255, 255))
    return canvas.convert("RGB")


if __name__ == "__main__":
    os.makedirs(DOCS, exist_ok=True)
    items = list(ITEMS)
    if "--extra" in sys.argv:
        items += EXTRA
    outs = []
    for fill in (0.80, 0.86, 0.92):
        im = render(fill, items, draw_marks="--marks" in sys.argv)
        p = os.path.join(DOCS, f"preview_v2_fill{int(fill*100)}.png")
        im.save(p)
        print(f"fill={fill:.2f} -> {p}")
        outs.append(im)
    W = BAND_W
    sheet = Image.new("RGB", (W, BAND_H * len(outs) + 16 * (len(outs) - 1)), (0, 0, 0))
    y = 0
    for im in outs:
        sheet.paste(im, (0, y)); y += BAND_H + 16
    sheet.save(os.path.join(DOCS, "preview_v2_compare.png"))
    print("compare ->", os.path.join(DOCS, "preview_v2_compare.png"))
