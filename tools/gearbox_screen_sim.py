# -*- coding: utf-8 -*-
"""
gearbox_screen_sim.py —— 严格复刻 Unity **屏幕**最终画面(1920×1080)

为什么需要它:预览图以前只画 1920×678 的"容器",但 Unity 实际输出是 1920×1080,
容器只占中间条带,外面还有信箱黑边 + CloseupView 的 70% 暗底。只有按屏幕坐标
合成,才能看出"尺寸对不对"。

严格复刻的链路:
  CloseupView: Canvas(ScreenSpaceOverlay, sortingOrder 1050) + CanvasScaler(ScaleWithScreenSize, 1920×1080)
    → 1920×1080 屏幕上画布像素 == 屏幕像素,无缩放
    → Content 挂点 anchor=(0.5,0.5) anchoredPosition=(0,0)  → 屏幕中心 (960,540)
  GearboxSpin root: anchorMin=anchorMax=(0.5,0.5), pivot=(0.5,0.5), sizeDelta=(1920,678)
    → 居中于 (960,540) → 占屏幕 y 201..879
  LetterboxOverlay: BarHeightPct=0.1863 → 上下各 201.2px 黑边
  DimBackground: 全屏 (0,0,0,0.7)

  每个 Image: anchoredPosition p(局部), pivot pv, sizeDelta s
    屏幕像素 = (960 + ax, 540 - ay)
    贴图左上角 = (960 + ax - pv.x*s.x, 540 - ay - (1-pv.y)*s.y)
    贴图尺寸 = s(Image.sizeDelta;preserveAspect 时按 sprite 宽高比收进 s 的框)

★★ 2026-09-15 四修:常驻图改为**序列帧首帧**(gearbox_static.png),
   旧的五层静态拼装(base/gear_big/gear_small/shaft/valve)+ knob 已从 C# 删除,
   本模拟器同步删除,免得预览图和实机不符。

产出: docs/gearbox/_screen_rest.png / _screen_solved.png / _screen_scar.png
"""
import os
from PIL import Image

D = r"C:/Users/yuan/lost-goddess-game/Assets/_Project/Resources/Closeups/GearboxPuzzle"
DOCS = r"C:/Users/yuan/lost-goddess-game/docs/gearbox"

SCREEN_W, SCREEN_H = 1920, 1080
BAR_PCT = 0.1863
CONTAINER_W, CONTAINER_H = 1920, 678
ROOT_CX, ROOT_CY = SCREEN_W / 2.0, SCREEN_H / 2.0

# ── 与 GearboxSpinCloseup.cs 同源(四修)─────────────────────────────
K = 0.635
CANVAS_W, CANVAS_H = 3400, 1200
CANVAS_CX, CANVAS_CY = 1700, 600

STATIC_TEX = "gearbox_static.png"
SPIN_ANCHORED = (22.0, -4.0)          # 容器局部
STATIC_SIZE = (566.0, 610.0)          # 容器像素
SPIN_SIZE = (566.0, 610.0)

# ★ 蒸汽:2026-09-15 用户亲手拖/滚定稿值(从 Console [SteamLayout] 抄回)
STEAM_ANCHORED = (-48.5, 0.6)
STEAM_SIZE = (249.0, 105.5)
STEAM_ROT = -8.9
STEAM_PIVOT = (1.0, 1.0)              # 右上角 = 喷出源
STEAM_SRC = (288, 122)
STEAM_COLS, STEAM_ROWS = 13, 4

# 图集:4 张,每张 41 帧,7 列 × 6 行,单格 575×620
SPIN_SHEETS = ["gearspin_sheet.png", "gearspin_sheet_2.png",
               "gearspin_sheet_3.png", "gearspin_sheet_4.png"]
SPIN_COLS, SPIN_PER_SHEET, SPIN_FRAMES = 7, 41, 161
SPIN_SRC = (575, 620)


def spin_frame(i):
    """按帧序取单帧(跨 4 张图集)。"""
    s = i // SPIN_PER_SHEET
    j = i % SPIN_PER_SHEET
    sheet = Image.open(os.path.join(D, SPIN_SHEETS[s])).convert("RGBA")
    c, r = j % SPIN_COLS, j // SPIN_COLS
    return sheet.crop((c * SPIN_SRC[0], r * SPIN_SRC[1],
                       (c + 1) * SPIN_SRC[0], (r + 1) * SPIN_SRC[1]))


def steam_frame(i):
    """取蒸汽单帧(13 列 × 4 行)。"""
    sheet = Image.open(os.path.join(D, "steam_sheet.png")).convert("RGBA")
    c, r = i % STEAM_COLS, i // STEAM_COLS
    return sheet.crop((c * STEAM_SRC[0], r * STEAM_SRC[1],
                       (c + 1) * STEAM_SRC[0], (r + 1) * STEAM_SRC[1]))


def build(with_spin=False, with_scar=False, frame=0, with_steam=False, steam_frame_i=30):
    screen = Image.new("RGBA", (SCREEN_W, SCREEN_H), (20, 18, 22, 255))
    # CloseupView.DimBackground (0,0,0,0.7)
    screen.alpha_composite(Image.new("RGBA", (SCREEN_W, SCREEN_H), (0, 0, 0, 178)))

    c = Image.new("RGBA", (CONTAINER_W, CONTAINER_H), (0, 0, 0, 0))

    def paste_local(img, anchored, pivot, size):
        """anchored = 容器局部坐标(原点=容器中心,y 向上)"""
        iw, ih = img.size
        scale = min(size[0] / iw, size[1] / ih)
        tw, th = iw * scale, ih * scale
        cx = CONTAINER_W / 2.0 + anchored[0]
        cy = CONTAINER_H / 2.0 - anchored[1]
        c.alpha_composite(img.resize((int(round(tw)), int(round(th))), Image.LANCZOS),
                          (int(round(cx - pivot[0] * tw)),
                           int(round(cy - (1 - pivot[1]) * th))))

    def paste_rotated(img, anchored, pivot, size, rot_deg):
        """绕 pivot 旋转后再贴。Unity Z 正角逆时针 ≈ PIL rotate 正角逆时针(视觉一致)。"""
        tw, th = int(round(size[0])), int(round(size[1]))
        im = img.resize((tw, th), Image.LANCZOS)
        px, py = pivot[0] * tw, (1 - pivot[1]) * th     # pivot 在贴图内(左上原点)
        pad = int(max(tw, th) * 1.5)
        big = Image.new("RGBA", (tw + 2 * pad, th + 2 * pad), (0, 0, 0, 0))
        big.paste(im, (pad, pad))
        rp = (pad + px, pad + py)
        rbig = big.rotate(-rot_deg, resample=Image.BICUBIC, center=rp, expand=False)
        cx = CONTAINER_W / 2.0 + anchored[0]
        cy = CONTAINER_H / 2.0 - anchored[1]
        c.alpha_composite(rbig, (int(round(cx - rp[0])), int(round(cy - rp[1]))))

    if with_spin:
        # 动画态:序列帧(与常驻图同尺寸同位置)
        paste_local(spin_frame(frame), SPIN_ANCHORED, (0.5, 0.5), SPIN_SIZE)
    else:
        # 常驻态:序列帧**首帧**导出的 gearbox_static.png
        st = Image.open(os.path.join(D, STATIC_TEX)).convert("RGBA")
        paste_local(st, SPIN_ANCHORED, (0.5, 0.5), STATIC_SIZE)

    if with_steam:
        paste_rotated(steam_frame(steam_frame_i), STEAM_ANCHORED, STEAM_PIVOT,
                      STEAM_SIZE, STEAM_ROT)

    if with_scar:
        scar = Image.open(os.path.join(D, "scar_offset_line.png")).convert("RGBA")
        tw = int(round(CANVAS_W * K))
        th = int(round(CANVAS_H * K))
        sc = scar.resize((tw, th), Image.LANCZOS)
        sc = sc.rotate(-2.5, resample=Image.BICUBIC, expand=True)
        c.alpha_composite(sc, (int(round(CONTAINER_W / 2.0 - sc.width / 2)),
                               int(round(CONTAINER_H / 2.0 + 25 - sc.height / 2))))

    screen.alpha_composite(c, (0, (SCREEN_H - CONTAINER_H) // 2))
    return screen


def main():
    os.makedirs(DOCS, exist_ok=True)
    build(False).convert("RGB").save(os.path.join(DOCS, "_screen_rest.png"))
    build(True, frame=0).convert("RGB").save(os.path.join(DOCS, "_screen_solved.png"))
    build(True, frame=80).convert("RGB").save(os.path.join(DOCS, "_screen_solved_mid.png"))
    build(True, frame=160).convert("RGB").save(os.path.join(DOCS, "_screen_solved_last.png"))
    build(False, with_scar=True).convert("RGB").save(os.path.join(DOCS, "_screen_scar.png"))
    # ★ 蒸汽定稿态:常驻循环中(用户 2026-09-15 亲手摆位)
    build(False, with_steam=True, steam_frame_i=30).convert("RGB").save(
        os.path.join(DOCS, "_screen_steam.png"))
    build(True, frame=80, with_steam=True, steam_frame_i=30).convert("RGB").save(
        os.path.join(DOCS, "_screen_steam_spin.png"))
    print("saved _screen_{rest,solved,solved_mid,solved_last,scar,steam,steam_spin}.png")


if __name__ == "__main__":
    main()
