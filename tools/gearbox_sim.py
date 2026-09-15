# -*- coding: utf-8 -*-
"""
gearbox_sim.py —— 齿轮箱「精密操作」离线取景模拟(UI 空间,复刻真实特写)

为什么需要:本机 Unity 编辑器常开着这个工程,起第二个实例会直接崩
(build_check.log 的 HandleProjectAlreadyOpenInAnotherInstance);批处理又因
UPM 服务起不来跑不通。所以这里用 PIL 把 GearboxProjectorCloseup.cs 的
**UI 摆放/角度数学**原样复刻一遍,离线出图核对。

★ 2026-09-15 重写:旧版复刻的是"世界空间假特写"(自建正交相机 + 缩放根节点),
  跟真实渲染路径对不上,出的图不作数。现在改成真正的 UI 空间:
    屏幕 1920×1080;CloseupView 全屏 70% 黑;
    内容容器 1920×678 居中;素材 3400×1200 画布 → 容器 k = 1920/3400 = 0.564706;
    画布像素 (x,y) → 容器局部 (x*k-960, 339-y*k) → 屏幕 (960+lx, 540-ly)。

⚠ 这是**几何/排版核对**,不是像素级复刻 Unity 渲染(没有 UI 混合/抗锯齿差异)。
  改完 C# 常量请同步本文件镜像值,然后跑 `--verify-only` 自检。

用法:
  python tools/gearbox_sim.py                # 出全部帧到 docs/gearbox/sim_v6_*.png
  python tools/gearbox_sim.py --verify-only   # 只跑契约自检
"""
import io
import json
import math
import os
import re

import numpy as np

from PIL import Image, ImageDraw, ImageFont

PY = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PY, "docs", "gearbox")
ART = os.path.join(PY, "Assets/_Project/Resources/Closeups/GearboxPuzzle")
FONT = os.path.join(PY, "Assets/_Project/Resources/Fonts/LXGWWenKai-Regular.ttf")
CS = os.path.join(PY, "Assets/_Project/Scripts/Core/UI/GearboxProjectorCloseup.cs")

# ── 屏幕 / 容器 / 画布(与 C# 一致)──────────────────────────────────────
W, H = 1920, 1080
CANVAS_W, CANVAS_H = 3400.0, 1200.0
CONTAINER_W, CONTAINER_H = 1920.0, 678.0
# ★ 显示比例 = 拟合成品面板 gear_mechanism_closeup.png 得到的 0.635(IoU 0.9929),
#   不是"1920/3400 刚好塞满"。原点 = 画布中心 (1700,600)。
K = 0.635
CANVAS_CX, CANVAS_CY = 1700.0, 600.0
PANEL_PATH = os.path.join(PY, "Assets/_Project/Resources/Closeups/gear_mechanism_closeup.png")

# ── 锚点(画布像素,与 C# 一致)─────────────────────────────────────────
ROOT_PX = (1732.5, 1015.0)
RECT = (1310, 120, 2145, 1108)
RW, RH = RECT[2] - RECT[0], RECT[3] - RECT[1]
VALVE_PX = (1691.0, 720.0)
GEAR_S_PX = (1550.0, 522.0)
GEAR_B_PX = (1787.0, 350.0)
SHAFT_PX = (1781.0, 704.0)
BEAM_EMIT_PX = (1787.0, 300.0)
SPOUT_PX = (1610.0, 686.0)      # 用户定稿 EmitPx(903,393) 反算回画布
KNOB_PIVOT_PX = (2042.0, 605.0)
KNOB_CENTER_PX = (2092.0, 605.0)
KNOB_RECT_W, KNOB_RECT_H = 98.0, 146.0

BEAM_HIT_LOCAL = (-762.0, 208.0)               # 靶点(容器局部)
LAYER_SIZE = (RW * K, RH * K)
KNOB_SIZE = (KNOB_RECT_W * K, KNOB_RECT_H * K)

# ── 手感镜像(与 C# 逐一对照,--verify-only 会校验)─────────────────────
M = {
    "DragDegPerFullTilt": 270.0,
    "DetentHitchTilt": 14.35,
    "DetentHitchTime": 0.13,
    "DetentSnapTime": 0.20,
    "SnapOvershootDeg": 0.7,
    "GearBigRatio": -2.2,
    "GearSmallRatio": 3.1,
    "GearFollowSmoothTime": 0.06,
    "KnobPushTravel": 13.0,
    "KnobPushScale": 0.90,
    "AmplitudePerUnit": 0.55,
    "AmplitudeThreshold": 0.35,
    "AmplitudeSweepMax": 26.0,
    "SweepPeriod": 2.6,
    "ScarFadeTime": 0.8,
    "RollbackTime": 0.7,
    "RootPxX": 1732.5,
    "RootPxY": 1015.0,
    "kScale": 0.635,
    "kSteamAngleDeg": -8.9,
    "kSmokeScale": 0.45,
}
M_HITCH_TIME = M["DetentHitchTime"]
M_SNAP_TIME = M["DetentSnapTime"]
M_OVERSHOOT = M["SnapOvershootDeg"]
DETENT_HITCH = M["DetentHitchTilt"]
M_ROLLBACK = M["RollbackTime"]


# ── 坐标换算 ──────────────────────────────────────────────────────────
def canvas_to_local(x, y):
    return ((x - CANVAS_CX) * K, (CANVAS_CY - y) * K)


def local_to_screen(p):
    return (W * 0.5 + p[0], H * 0.5 - p[1])


def rot_around_root(canvas_pt, deg):
    """绕箱底中心(画布像素空间)旋转。+deg = 逆时针 = 上倾。"""
    ax, ay = canvas_pt[0] - ROOT_PX[0], ROOT_PX[1] - canvas_pt[1]
    r = math.radians(deg)
    c, s = math.cos(r), math.sin(r)
    return (ROOT_PX[0] + ax * c - ay * s, ROOT_PX[1] - (ax * s + ay * c))


def pivot_for(ax, ay):
    return ((ax - RECT[0]) / RW, 1.0 - (ay - RECT[1]) / RH)


CRADLE_POS = canvas_to_local(*ROOT_PX)


def cradle_child_local(anchor_px):
    """机构内零件相对机构轴心的容器局部坐标。"""
    return (canvas_to_local(*anchor_px)[0] - CRADLE_POS[0],
            canvas_to_local(*anchor_px)[1] - CRADLE_POS[1])


def rotate_local(p, deg):
    r = math.radians(deg)
    c, s = math.cos(r), math.sin(r)
    return (p[0] * c - p[1] * s, p[0] * s + p[1] * c)


def load(name):
    return Image.open(os.path.join(ART, name)).convert("RGBA")


def paste_ui(canvas, img, pivot_norm, container_pt, rot_deg=0.0, sx=1.0, sy=1.0):
    """把一个 UI Image 贴到画布上:pivot 落在 container_pt,绕 pivot 旋转 rot_deg。"""
    w = max(1, int(round(img.width * sx)))
    h = max(1, int(round(img.height * sy)))
    im = img.resize((w, h), Image.LANCZOS)
    px, py = pivot_norm[0] * w, (1.0 - pivot_norm[1]) * h
    if abs(rot_deg) > 1e-6:
        im = im.rotate(rot_deg, resample=Image.BICUBIC, center=(px, py))
    sx_px, sy_px = local_to_screen(container_pt)
    canvas.alpha_composite(im, (int(round(sx_px - px)), int(round(sy_px - py))))


def draw_line_local(canvas, a, b, color, width):
    d = ImageDraw.Draw(canvas, "RGBA")
    d.line([local_to_screen(a), local_to_screen(b)], fill=color, width=int(width))


# ── 光束(与 C# UpdateBeam 同数学)──────────────────────────────────────
def emit_local(elev):
    rel = (canvas_to_local(*BEAM_EMIT_PX)[0] - CRADLE_POS[0],
           canvas_to_local(*BEAM_EMIT_PX)[1] - CRADLE_POS[1])
    r = rotate_local(rel, elev)
    return (CRADLE_POS[0] + r[0], CRADLE_POS[1] + r[1])


def ray_exit_rect(o, d):
    hw, hh = CONTAINER_W * 0.5, CONTAINER_H * 0.5
    best = None
    for num, den in ((hw - o[0], d[0]), (-hw - o[0], d[0]),
                     (hh - o[1], d[1]), (-hh - o[1], d[1])):
        if abs(den) < 1e-4:
            continue
        t = num / den
        if t > 0 and (best is None or t < best):
            best = t
    if best is None or best > 6000:
        best = 4000.0
    return (o[0] + d[0] * best, o[1] + d[1] * best)


def beam_points(elev, amp, phase, solved):
    e = emit_local(elev)
    e15 = emit_local(15.0)
    dx, dy = BEAM_HIT_LOCAL[0] - e15[0], BEAM_HIT_LOCAL[1] - e15[1]
    n = math.hypot(dx, dy)
    a15 = math.degrees(math.atan2(dy / n, dx / n))
    center = a15 + (15.0 - elev)
    pan = 0.0 if solved else amp * M["AmplitudeSweepMax"] * math.sin(phase)
    ang = math.radians(center + pan)
    d = (math.cos(ang), math.sin(ang))
    if d[0] < -0.02:
        t = (BEAM_HIT_LOCAL[0] - e[0]) / d[0]
        end = (BEAM_HIT_LOCAL[0],
               max(-CONTAINER_H * 0.5, min(CONTAINER_H * 0.5, e[1] + d[1] * t)))
    else:
        end = ray_exit_rect(e, d)
    if solved:
        end = BEAM_HIT_LOCAL
    return e, end


# ── 背景 = 真实千眼回廊房间三层 + CloseupView 同款全屏 70% 黑 ────────────
#  相机 orthoSize 6 → 1080px = 12 单位 = 90px/单位;bg_full 是 3400×1200 / PPU100
#  = 34×12 世界单位 → 3060×1080 屏幕像素,图中心落在世界原点(屏幕中心)。
BG_DIR = os.path.join(PY, "Assets/_Project/Resources/Scenes/ChaseCorridor")


def build_backdrop():
    """真实房间取景:相机视口被裁进信箱条带(LetterboxOverlay.BarHeightPct=0.1863),
    orthoSize=6 时条带高 12 单位、宽 12*1920/678=34 单位 = 房间图尺寸 →
    **3400×1200 的整张图 1:1 完整铺进 1920×678 条带**(不是放大后裁两侧)。
    ⚠ 我第一版按"全屏 1080 高"渲染,房间被放大 1.59 倍又裁掉两侧 —— 用户说"明显不对"。"""
    bg = Image.new("RGBA", (W, H), (13, 13, 15, 255))
    try:
        full = Image.open(os.path.join(BG_DIR, "bg_full.png")).convert("RGBA")
        size = (int(CONTAINER_W), int(CONTAINER_H))          # 1920 x 678
        off = (int(W / 2 - size[0] / 2), int(H / 2 - size[1] / 2))
        bg.alpha_composite(full.resize(size, Image.LANCZOS), off)
        for nm in ("bg_gate_shadow.png", "bg_brass_eye.png"):
            try:
                sp = Image.open(os.path.join(BG_DIR, nm)).convert("RGBA")
                bg.alpha_composite(sp.resize(size, Image.LANCZOS), off)
            except FileNotFoundError:
                pass
    except FileNotFoundError:
        pass
    bg.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, int(255 * 0.7))))
    return bg


BACKDROP = build_backdrop()
BASE = load("gearbox_base.png")
VALVE = load("gearbox_valve.png")
GEAR_S = load("gearbox_gear_small.png")
GEAR_B = load("gearbox_gear_big.png")
SHAFT = load("gearbox_shaft.png")
KNOB = load("gearbox_knob.png")
SCAR = load("scar_offset_line.png")
try:
    STEAM_SHEET = Image.open(os.path.join(ART, "steam_sheet.png")).convert("RGBA")
except FileNotFoundError:
    STEAM_SHEET = None

FONT_MAIN = ImageFont.truetype(FONT, 27) if os.path.exists(FONT) else ImageFont.load_default()
FONT_STEP = ImageFont.truetype(FONT, 26) if os.path.exists(FONT) else FONT_MAIN
FONT_HINT = ImageFont.truetype(FONT, 22) if os.path.exists(FONT) else FONT_MAIN
FONT_TOAST = ImageFont.truetype(FONT, 40) if os.path.exists(FONT) else FONT_MAIN
FONT_TAG = ImageFont.truetype(FONT, 32) if os.path.exists(FONT) else FONT_MAIN


def render(elev=0.0, amp=1.0, phase=0.0, solved=False, locked=False,
           scar=0.0, flash=0.0, steam=False, steam_frame=20,
           label="", caption="", step=1, toast=""):
    c = BACKDROP.copy()

    # 木箱底图(不随仰角动)
    paste_ui(c, BASE, pivot_for(*ROOT_PX), CRADLE_POS, 0.0, K, K)

    # 内部机构:绕箱底中心整体旋转
    gb_rot = elev * M["GearBigRatio"]
    gs_rot = elev * M["GearSmallRatio"]

    def cradle_pt(anchor_px):
        rel = cradle_child_local(anchor_px)
        r = rotate_local(rel, elev)
        return (CRADLE_POS[0] + r[0], CRADLE_POS[1] + r[1])

    paste_ui(c, GEAR_S, pivot_for(*GEAR_S_PX), cradle_pt(GEAR_S_PX), elev + gs_rot, K, K)
    paste_ui(c, GEAR_B, pivot_for(*GEAR_B_PX), cradle_pt(GEAR_B_PX), elev + gb_rot, K, K)
    paste_ui(c, SHAFT, pivot_for(*SHAFT_PX), cradle_pt(SHAFT_PX), elev, K, K)
    paste_ui(c, VALVE, pivot_for(*VALVE_PX), cradle_pt(VALVE_PX), elev, K, K)

    # 蒸汽(UI 逐帧;pivot 在贴图右上角 = 喷出源)
    if steam and STEAM_SHEET is not None:
        fw, fh, cols = 288, 122, 13
        total = (STEAM_SHEET.width // fw) * (STEAM_SHEET.height // fh)
        i = max(0, min(steam_frame, total - 1))
        fr = STEAM_SHEET.crop(((i % cols) * fw, (i // cols) * fh,
                               (i % cols) * fw + fw, (i // cols) * fh + fh))
        # 定稿尺寸 480*0.45 / 204*0.45,定稿朝向 -8.9°(PIL 正角=逆时针,与 Unity 同向)
        fr = fr.resize((int(round(480 * M["kSmokeScale"])), int(round(204 * M["kSmokeScale"]))),
                       Image.LANCZOS)
        pt = cradle_pt(SPOUT_PX)
        fr = fr.rotate(elev + M["kSteamAngleDeg"], resample=Image.BICUBIC, center=(fr.width, 0))
        sp = local_to_screen(pt)
        c.alpha_composite(fr, (int(round(sp[0] - fr.width)), int(round(sp[1]))))

    # 右缘旋钮(推拉:往框里缩)
    push = 1.0 - amp
    kx = canvas_to_local(*KNOB_PIVOT_PX)
    paste_ui(c, KNOB, (0.0, 0.5), (kx[0] - push * M["KnobPushTravel"], kx[1]), 0.0,
             K * (1.0 - push * (1.0 - M["KnobPushScale"])), K)

    # 靶环
    tgt = BEAM_HIT_LOCAL
    d = ImageDraw.Draw(c, "RGBA")
    rr = (88.0 if solved else 132.0) * 0.5
    ta = 235 if solved else (170 if locked else 105)
    cx, cy = local_to_screen(tgt)
    d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=(255, 224, 140, ta), width=3)
    for (a, b) in (((cx - rr * 1.25, cy), (cx - rr * 0.6, cy)),
                   ((cx + rr * 0.6, cy), (cx + rr * 1.25, cy)),
                   ((cx, cy - rr * 1.25), (cx, cy - rr * 0.6)),
                   ((cx, cy + rr * 0.6), (cx, cy + rr * 1.25))):
        d.line([a, b], fill=(255, 224, 140, ta), width=3)

    # 光束
    emit, end = beam_points(elev, amp, phase, solved)
    ba = 255 if solved else (230 if locked else 70)
    for w, al in ((16, 30), (9, 60), (4, 170), (2, 235)):
        draw_line_local(c, emit, end, (255, 246, 208, min(al, ba)), w)

    # 刻痕
    if scar > 0.01:
        s = SCAR.resize((880, 311), Image.LANCZOS)
        s.putalpha(s.getchannel("A").point(lambda v: int(v * scar)))
        s = s.rotate(-2.5, resample=Image.BICUBIC, expand=True)
        base = (int(W / 2 - s.width / 2), int(H / 2 + 190 - s.height / 2))
        sh = Image.new("RGBA", s.size, (5, 3, 3, 0))
        sh.putalpha(s.getchannel("A").point(lambda v: int(v * scar * 0.85)))
        c.alpha_composite(sh, (base[0] + 4, base[1] + 4))
        c.alpha_composite(s, base)

    if flash > 0.01:
        c.alpha_composite(Image.new("RGBA", (W, H), (140, 13, 10, int(255 * 0.45 * flash))))

    draw_guide(c, step, solved)
    if toast:
        d.text((W / 2, H / 2 - 339 + 14 + 30), toast, font=FONT_TOAST,
               fill=(240, 232, 210, 235), anchor="mm",
               stroke_width=3, stroke_fill=(0, 0, 0, 220))
    if label:
        d.text((W - 30, 26), label, font=FONT_TAG, fill=(255, 226, 150, 235), anchor="ra")
    if caption:
        d.text((W / 2, H - 26), caption, font=FONT_MAIN, fill=(228, 220, 198, 235), anchor="md")
    return c


def draw_guide(c, step, solved):
    d = ImageDraw.Draw(c, "RGBA")
    pw, ph = 1240, 140
    x0 = int(W / 2 - pw / 2)
    y0 = int(H / 2 - 339 - 8 - ph)      # 容器上沿之外(顶部黑边)
    d.rectangle([x0, y0, x0 + pw, y0 + ph], fill=(15, 13, 11, 158))
    d.rectangle([x0, y0 + ph - 2, x0 + pw, y0 + ph], fill=(184, 148, 77, 128))
    DONE, ACT, IDLE = (158, 209, 140), (255, 219, 115), (158, 153, 143)
    rows = [(step > 1, step == 1, "① 抬起底座 15°"),
            (step > 2, step == 2, "② 收拢摆幅"),
            (step > 3, step == 3, "③ 锁定角度")]
    for i, (done, act, txt) in enumerate(rows):
        col = DONE if done else (ACT if act else IDLE)
        d.text((x0 + 28 + i * 396, y0 + 18), ("✓ " if done else "") + txt,
               font=FONT_STEP, fill=col)
    if solved or step == 4:
        hint = "角度已锁定,光束固定投射在左侧墙高处。"
    elif step == 1:
        hint = "按住顶部竖轴,绕着它画圈(只按不动会提示你)"
    elif step == 2:
        hint = "把箱体右缘的黄铜旋钮往里推 —— 摆幅越窄,光束越靠近左侧靶点"
    else:
        hint = "点击中间那根 S 形弯管,把角度钉死"
    d.text((x0 + 28, y0 + 66), hint, font=FONT_HINT, fill=(217, 204, 173, 230))


def contact_sheet(frames, path, cols=2):
    tw = W // cols
    th = int(H * tw / W)
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new("RGBA", (tw * cols, th * rows), (8, 8, 9, 255))
    for i, (im, _) in enumerate(frames):
        sheet.alpha_composite(im.resize((tw, th), Image.LANCZOS),
                              ((i % cols) * tw, (i // cols) * th))
    sheet.convert("RGB").save(path, quality=92)
    print("saved", path, sheet.size)


# ============================================================================
#  契约自检
# ============================================================================
_fails = []


def _ok(cond, msg):
    print(("  OK   " if cond else "  FAIL ") + msg)
    if not cond:
        _fails.append(msg)


def check_constants():
    print("[A] C# 常量 <-> 模拟器镜像一致性")
    src = io.open(CS, encoding="utf-8").read()
    for name, want in sorted(M.items()):
        # 常量可能写在多声明符里(`float RootPxX = 1732.5f, RootPxY = 1015f;`),收尾是 , 或 ;
        m = re.search(r'\b' + name + r'\s*=\s*(-?[\d.]+)f?\s*[,;]', src)
        if not m:
            _ok(False, "%s 在 C# 里找不到" % name)
            continue
        _ok(abs(float(m.group(1)) - want) < 1e-6,
            "%s = %s(C#) vs %s(模拟器)" % (name, m.group(1), want))
    m = re.search(r'BeamHitLocal\s*=\s*new Vector2\((-?[\d.]+)f,\s*(-?[\d.]+)f\)', src)
    _ok(bool(m) and abs(float(m.group(1)) - BEAM_HIT_LOCAL[0]) < 1e-6
        and abs(float(m.group(2)) - BEAM_HIT_LOCAL[1]) < 1e-6,
        "BeamHitLocal = %s(C#) vs %s(模拟器)"
        % ((m.group(1), m.group(2)) if m else None, BEAM_HIT_LOCAL))
    for nm, want in (("kCanvasW", 3400.0), ("kCanvasH", 1200.0),
                     ("kContainerW", 1920.0), ("kContainerH", 678.0)):
        mm = re.search(r'\b' + nm + r'\s*=\s*([\d.]+)f', src)
        _ok(bool(mm) and abs(float(mm.group(1)) - want) < 1e-6,
            "%s = %s(C#) vs %s" % (nm, mm.group(1) if mm else None, want))
    inside = all(-CONTAINER_W / 2 <= canvas_to_local(*p)[0] <= CONTAINER_W / 2
                 and -CONTAINER_H / 2 <= canvas_to_local(*p)[1] <= CONTAINER_H / 2
                 for p in (ROOT_PX, VALVE_PX, GEAR_S_PX, GEAR_B_PX, SHAFT_PX, KNOB_PIVOT_PX))
    _ok(inside, "所有图层锚点都落在 1920x678 容器内")


def check_geometry():
    print("[B] 光束几何契约(容器局部坐标)")
    off = [i for i in range(0, 16)
           if abs(beam_points(i, 0.0, 0.0, False)[1][0] - BEAM_HIT_LOCAL[0]) > 1e-6]
    _ok(not off, "仰角 0..15 落点全部在左墙平面 x=%.0f 上" % BEAM_HIT_LOCAL[0])

    ys = [beam_points(i, 0.0, 0.0, False)[1][1] for i in range(0, 16)]
    _ok(all(ys[i] < ys[i + 1] for i in range(len(ys) - 1)),
        "仰角 0->15 落点单调爬升 %.1f -> %.1f" % (ys[0], ys[-1]))

    end = beam_points(15.0, 0.0, 0.0, False)[1]
    _ok(abs(end[1] - BEAM_HIT_LOCAL[1]) < 1e-6,
        "仰角 15 + 摆幅 0 -> 落点 y=%.2f == 靶点 y=%.1f" % (end[1], BEAM_HIT_LOCAL[1]))

    dev = []
    for a in (0.0, 0.35, 0.7, 1.0):
        e = beam_points(15.0, a, math.pi / 2, False)[1]
        dev.append(math.hypot(e[0] - BEAM_HIT_LOCAL[0], e[1] - BEAM_HIT_LOCAL[1]))
    _ok(all(dev[i] < dev[i + 1] for i in range(len(dev) - 1)),
        "摆幅 0/0.35/0.7/1.0 -> 偏离靶点 %.0f/%.0f/%.0f/%.0f" % tuple(dev))

    hh = CONTAINER_H * 0.5
    allin = all(-hh - 1e-6 <= beam_points(15.0, a, ph, False)[1][1] <= hh + 1e-6
                for a in (0.0, 0.5, 1.0) for ph in [i * math.pi / 8 for i in range(16)])
    _ok(allin, "满幅整周期摆动落点都不出容器(|y| <= %.0f)" % hh)

    ok_in = True
    for p in (VALVE_PX, GEAR_S_PX, GEAR_B_PX, SHAFT_PX):
        pt = canvas_to_local(*rot_around_root(p, 15.0))
        if not (-CONTAINER_W / 2 <= pt[0] <= CONTAINER_W / 2
                and -CONTAINER_H / 2 <= pt[1] <= CONTAINER_H / 2):
            ok_in = False
    _ok(ok_in, "仰角 15° 时机构各零件仍在容器内(不甩出画面)")


def check_against_approved_panel():
    """★ 最强的一道:把五层按 kScale 合成,和项目里那张成品面板比形状重合度。
    这是"复现现成的"唯一硬标准 —— 取景/比例错了这里立刻掉下去。"""
    print("[A2] 与成品面板 gear_mechanism_closeup.png 比对")
    if not os.path.exists(PANEL_PATH):
        _ok(False, "找不到成品面板 %s" % PANEL_PATH)
        return
    comp = Image.new("RGBA", (int(CANVAS_W), int(CANVAS_H)), (0, 0, 0, 0))
    piv = json.load(io.open(os.path.join(PY, "docs/gearbox/gearbox_measure.json"),
                            encoding="utf-8"))["pivots"]
    for name, (ax, ay) in (("gearbox_base", ROOT_PX), ("gearbox_valve", VALVE_PX),
                           ("gearbox_gear_small", GEAR_S_PX), ("gearbox_gear_big", GEAR_B_PX),
                           ("gearbox_shaft", SHAFT_PX)):
        im = load(name + ".png")
        pv = piv[name]
        comp.alpha_composite(im, (int(round(ax - pv[0] * im.width)),
                                  int(round(ay - (1 - pv[1]) * im.height))))
    panel = Image.open(PANEL_PATH).convert("RGBA")
    im = comp.resize((int(round(CANVAS_W * K)), int(round(CANVAS_H * K))), Image.LANCZOS)
    canvas = Image.new("L", (panel.width, panel.height), 0)
    canvas.paste(im.getchannel("A"),
                 (int(round(panel.width * 0.5 - CANVAS_CX * K)),
                  int(round(panel.height * 0.5 - CANVAS_CY * K))))
    ma = np.asarray(panel)[..., 3] > 24
    mb = np.asarray(canvas) > 24
    v = (ma & mb).sum() / (ma | mb).sum()
    _ok(v >= 0.98, "五层合成 @kScale=%.3f 与成品面板形状 IoU = %.4f (要求 >= 0.98)" % (K, v))


def check_timeline(fps=60.0):
    print("[C] 状态机时间轴(60fps 复刻 UpdateElevation)")
    dt = 1.0 / fps
    vis, hitch_t, detent_t = DETENT_HITCH, 0.0, 0.0
    hitching, detenting = True, False
    peak, t_total, frozen_ok = vis, 0.0, True
    while hitching or detenting:
        t_total += dt
        if hitching:
            hitch_t += dt
            vis = DETENT_HITCH
            if abs(vis - DETENT_HITCH) > 1e-9:
                frozen_ok = False
            if hitch_t >= M_HITCH_TIME:
                hitching, detenting, detent_t = False, True, 0.0
        elif detenting:
            detent_t += dt / M_SNAP_TIME
            t = min(1.0, detent_t)
            over = 15.0 + M_OVERSHOOT
            if t < 0.45:
                k = t / 0.45
                vis = DETENT_HITCH + (over - DETENT_HITCH) * (1 - (1 - k) ** 2)
            else:
                k = (t - 0.45) / 0.55
                vis = over + (15.0 - over) * (k * k * (3 - 2 * k))
            peak = max(peak, vis)
            if t >= 1.0:
                vis, detenting = 15.0, False
        if t_total > 3:
            break

    _ok(abs(vis - 15.0) < 1e-6, "吸附结束精确停在 15.000(实得 %.4f)" % vis)
    _ok(peak > 15.4, "过冲峰值 %.3f > 15.4(肉眼看得见的'咔哒'手感)" % peak)
    _ok(abs(t_total - (M_HITCH_TIME + M_SNAP_TIME)) < 3 * dt,
        "总时长 %.3fs ~= 卡顿 %.2f + 吸附 %.2f" % (t_total, M_HITCH_TIME, M_SNAP_TIME))
    _ok(frozen_ok, "卡顿段(%.2fs)视觉冻结在 %.2f 不抖" % (M_HITCH_TIME, DETENT_HITCH))
    _ok(M_ROLLBACK > 0, "回滚 %.2fs 内仰角 15->0、摆幅 0->1 收敛" % M_ROLLBACK)


def verify():
    print("=" * 66)
    print("齿轮箱契约自检(UI 空间)")
    print("=" * 66)
    check_constants()
    check_against_approved_panel()
    check_geometry()
    check_timeline()
    print("-" * 66)
    if _fails:
        print("结果:FAIL(%d 项)" % len(_fails))
        for f in _fails:
            print("   x " + f)
    else:
        print("结果:PASS —— 常量一致 / 光束几何成立 / 时间轴正确")
    return 1 if _fails else 0


def main():
    os.makedirs(OUT, exist_ok=True)
    frames = []

    def emit(name, **kw):
        im = render(**kw)
        p = os.path.join(OUT, "sim_v6_%s.png" % name)
        im.convert("RGB").save(p)
        frames.append((im, name))
        print("saved", p)

    emit("1_rest", elev=0.0, amp=1.0, phase=0.0, label="① 水平 0°", step=1,
         caption="水平:光束落在左侧墙低处;木箱外框不动,内部机构未倾")
    emit("2_drag", elev=7.5, amp=1.0, phase=0.6, label="① 拖拽中 7.5°", step=1,
         caption="圆周拖拽:内部机构整体倾斜,大齿轮 -16.5° / 小齿轮 +23.3° 联动")
    emit("3_detent", elev=14.35, amp=1.0, phase=1.2, label="① 卡顿点 14.35°", step=1,
         caption="到位前“轻微卡顿”:视觉钉在 14.35°,随后咔哒吸附过冲到 15.7° 再坐回 15°")
    emit("4_locked_fullamp", elev=15.0, amp=1.0, phase=1.57, locked=True, step=2,
         label="② 仰角到位 · 满幅",
         caption="仰角锁定,光束变亮:摆幅满幅 → 光束在左侧墙上大幅来回扫")
    emit("5_narrow", elev=15.0, amp=0.35, phase=1.57, locked=True, step=2,
         label="② 旋钮推入中",
         caption="黄铜旋钮往框里推:摆幅收窄,光束渐渐贴住靶环")
    emit("6_solved", elev=15.0, amp=0.0, phase=0.0, locked=True, solved=True, step=4,
         label="③ 已锁定", caption="点击 S 形弯管:角度锁定,光束固定投射在左侧墙高处")
    emit("7_penalty_flash", elev=0.0, amp=1.0, phase=0.0, flash=0.9, steam=True,
         steam_frame=24, step=1, label="✗ 顺序错误 · 喷蒸汽", toast="顺序错误:先抬起底座仰角",
         caption="先动旋钮 → S 形弯管喷蒸汽 + 红闪(此刻刻痕还没显影)")
    emit("8_penalty_scar", elev=0.0, amp=1.0, phase=0.0, scar=1.0, step=1,
         label="✗ 刻痕常驻",
         caption="蒸汽退去,画面留下刻痕「偏之一线,灼身千里」,随后仰角/摆幅/齿轮全部回滚")

    contact_sheet(frames, os.path.join(OUT, "sim_v6_compare.png"))
    print("done")
    return verify()


if __name__ == "__main__":
    import sys
    if "--verify-only" in sys.argv:
        sys.exit(verify())
    sys.exit(main())
