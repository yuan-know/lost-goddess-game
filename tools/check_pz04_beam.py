# -*- coding: utf-8 -*-
"""
check_pz04_beam.py —— 千眼回廊 PZ_04「躲光束」(双光束版) 几何 / 节奏 / 可玩性离线校验

与 Assets/_Project/Scripts/Content/EyeCorridor/EyeCorridorLaserPuzzle.cs 里的常量一一对应。
改完 C# 常量后跑一次:
    1) 打印两条光束的几何、每个暴露段的冲刺时间、复位后出生点的安全窗口
    2) 用「到安全区就停、等窗口再冲」的策略做纯离线模拟,看能否通关、多久、被抓几次
    3) 画图:背景 + 拱门遮罩 + 8 个安全区 + 两片光束在若干时刻的位置

产出: docs/千眼回廊/新玩法_双光束与安全区.png
"""
import os, math
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, 'Assets/_Project/Resources/Scenes/ChaseCorridor')
OUT = os.path.join(ROOT, 'docs/千眼回廊/新玩法_双光束与安全区.png')
FONT = os.path.join(ROOT, 'Assets/_Project/Resources/Fonts/LXGWWenKai-Regular.ttf')

# ── 与 C# 对齐的常量 ──────────────────────────────────────────────────────
CORRIDOR_HALF = 17.0
GROUND_Y = -5.76
SPAWN_X = -16.45
EXIT_X = 16.45

EYE_X, EYE_Y = -0.075, 4.40          # 铜眼(光束顶点)
BEAM_ORIGIN_Y = 4.30

GATE_MIN = [-16.280, -12.080, -7.700, -3.420, 0.830, 5.170, 9.420, 13.830]
GATE_MAX = [-13.890, -9.510, -5.220, -0.940, 3.410, 7.670, 12.030, 16.340]

BEAM_SPEED = 11.0                     # 地面光带移动速度(单位/秒)
BEAM_HALF_W = 3.75                    # 危险带半宽(总宽 7.5 ≈ 三个拱门)
BEAM_TRACK_MIN, BEAM_TRACK_MAX = -17.0, 17.0
BEAM_COUNT = 2                        # ★ 两片光区
BEAM_PHASE_STEP = 0.5                 # 相邻光束相位差(0.5 = 镜像对开)
BEAM_RESET_X = 9.3                    # 复位时两束所在位置(±)

PLAYER_SPEED = 3.0
DT = 1.0 / 60.0
PX_PER_UNIT = 100.0


def span():
    return BEAM_TRACK_MAX - BEAM_TRACK_MIN


def period():
    return 2.0 * span() / BEAM_SPEED


def tri_value(ph):
    ph = ph % 1.0
    return ph * 2.0 if ph < 0.5 else (1.0 - ph) * 2.0


def tri_x(ph):
    return BEAM_TRACK_MIN + tri_value(ph) * span()


def tri_dir(ph):
    return 1.0 if (ph % 1.0) < 0.5 else -1.0


def start_phase_from_x(x):
    """复位时想让光束落在 x 处、朝左走 → 对应相位。"""
    u = (x - BEAM_TRACK_MIN) / span()
    return 1.0 - u * 0.5


def beam_positions(ph):
    return [(tri_x(ph + i * BEAM_PHASE_STEP), tri_dir(ph + i * BEAM_PHASE_STEP))
            for i in range(BEAM_COUNT)]


def px(world_x):
    return (world_x + CORRIDOR_HALF) * PX_PER_UNIT


def py(world_y):
    return (6.0 - world_y) * PX_PER_UNIT


def in_gate(x):
    for a, b in zip(GATE_MIN, GATE_MAX):
        if a <= x <= b:
            return True
    return False


def lit(x, beams):
    if in_gate(x):
        return False
    return any(abs(x - bx) <= BEAM_HALF_W for bx, _ in beams)


# ── 模拟 ──────────────────────────────────────────────────────────────────
class Sim:
    """到安全区中心就停;判断「整段冲刺期间两束光都不覆盖我」才出发。"""

    def __init__(self, margin=0.0):
        self.margin = margin
        self.ph = start_phase_from_x(BEAM_RESET_X)
        self.reset()

    def reset(self):
        self.player = SPAWN_X
        self.caught = 0
        self.moving = False
        self.target = SPAWN_X
        self.best = SPAWN_X

    def beams(self):
        return beam_positions(self.ph)

    def step(self, dt):
        self.ph = (self.ph + dt / period()) % 1.0
        beams = self.beams()

        if self.moving:
            d = self.target - self.player
            step = min(abs(d), PLAYER_SPEED * dt)
            self.player += math.copysign(step, d)
            if abs(self.target - self.player) < 1e-3:
                self.player = self.target
                self.moving = False
        else:
            nxt = self.next_gate_center()
            if nxt is not None and self.can_dash(nxt):
                self.target = nxt
                self.moving = True

        if self.player > self.best:
            self.best = self.player

        if lit(self.player, beams):
            self.caught += 1
            self.reset()

    def next_gate_center(self):
        for a, b in zip(GATE_MIN, GATE_MAX):
            c = (a + b) * 0.5
            if c > self.player + 0.05:
                return c
        return EXIT_X          # 最后一尊安全区之后 = 直接冲出口

    def can_dash(self, target):
        total = (target - self.player) / PLAYER_SPEED
        n = max(2, int(total / DT) + 1)
        ph = self.ph
        for k in range(n + 1):
            tt = min(total, k * DT)
            p = self.player + tt * PLAYER_SPEED
            b = beam_positions((ph + tt / period()) % 1.0)
            if self.margin > 0:
                if any(abs(p - bx) <= BEAM_HALF_W + self.margin for bx, _ in b) and not in_gate(p):
                    return False
            else:
                if lit(p, b):
                    return False
        return True

    def run(self, max_seconds=300.0):
        t = 0.0
        while t < max_seconds:
            self.step(DT)
            t += DT
            if self.player >= EXIT_X - 1e-6:
                return t, self.caught
        return None, self.caught


# ── 出图 ──────────────────────────────────────────────────────────────────
def draw(shots=5):
    full = Image.open(os.path.join(BASE, 'bg_full.png')).convert('RGBA')
    mask = Image.open(os.path.join(BASE, 'bg_gate_shadow.png')).convert('RGBA')
    canvas = Image.alpha_composite(full, mask)
    layer = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    vx, vy = px(EYE_X), py(BEAM_ORIGIN_Y)
    gY = py(GROUND_Y)

    # ① 每片光束「扫过一遍」的包络(很淡的大三角)
    for bi, col in enumerate([(120, 180, 255), (255, 190, 120)]):
        off = bi * BEAM_PHASE_STEP
        lo = min(tri_x((off + k / 40.0) % 1.0) for k in range(41))
        hi = max(tri_x((off + k / 40.0) % 1.0) for k in range(41))
        d.polygon([(vx, vy), (px(lo - BEAM_HALF_W), gY), (px(hi + BEAM_HALF_W), gY)],
                  fill=(col[0], col[1], col[2], 20))

    # ② 若干时刻的两片光束本体
    ph0 = start_phase_from_x(BEAM_RESET_X)
    for k in range(shots):
        ph = (ph0 + k / float(shots) * 1.0) % 1.0
        for bi, (bx, _) in enumerate(beam_positions(ph)):
            col = (150, 200, 255) if bi == 0 else (255, 200, 140)
            a = int(110 - 14 * k)
            d.polygon([(vx, vy), (px(bx - BEAM_HALF_W), gY), (px(bx + BEAM_HALF_W), gY)],
                      fill=(col[0], col[1], col[2], max(26, a)))

    # ③ 地面读数条: 绿=安全区, 蓝/橙=当前两片光束落点(复位状态)
    band_y = gY + 10
    d.rectangle([0, band_y, canvas.width, band_y + 44], fill=(0, 0, 0, 150))
    for a, b in zip(GATE_MIN, GATE_MAX):
        d.rectangle([px(a), band_y, px(b), band_y + 44], fill=(70, 235, 140, 235))
    for bi, (bx, _) in enumerate(beam_positions(ph0)):
        col = (90, 160, 255) if bi == 0 else (255, 175, 90)
        d.rectangle([px(bx - BEAM_HALF_W), band_y, px(bx + BEAM_HALF_W), band_y + 44],
                    fill=col + (170,), outline=(255, 255, 255, 220))
    for a, b in zip(GATE_MIN, GATE_MAX):
        d.line([(px((a + b) * 0.5), gY - 8), (px((a + b) * 0.5), band_y)], fill=(70, 235, 140, 200), width=2)

    # ④ 铜眼 / 出生 / 出口
    d.ellipse([vx - 15, vy - 15, vx + 15, vy + 15], fill=(255, 210, 90, 255))
    d.line([(px(SPAWN_X), gY - 80), (px(SPAWN_X), band_y + 44)], fill=(255, 90, 80, 255), width=6)
    d.line([(px(EXIT_X), gY - 80), (px(EXIT_X), band_y + 44)], fill=(120, 255, 140, 255), width=6)

    out = Image.alpha_composite(canvas, layer).convert('RGB')
    d2 = ImageDraw.Draw(out)
    try:
        f1 = ImageFont.truetype(FONT, 46)
        f2 = ImageFont.truetype(FONT, 34)
    except Exception:
        f1 = f2 = ImageFont.load_default()
    d2.text((34, 20), '双光束:铜眼同时射出两片光区(镜像对开) · 拱门暗影 = 安全点',
            font=f1, fill=(240, 228, 196), stroke_width=3, stroke_fill=(0, 0, 0))
    d2.text((34, 80), '红=出生(暴露)  绿=出口  |  每片总宽 %.1f ≈ 三个拱门  速度 %.1f 单位/秒  '
            '复位时两束在 ±%.1f,出生点有 %.1f 秒反应窗口'
            % (BEAM_HALF_W * 2, BEAM_SPEED, BEAM_RESET_X,
               next(t for t in [x * DT for x in range(4000)]
                    if any(abs(SPAWN_X - bx) <= BEAM_HALF_W
                           for bx, _ in beam_positions((ph0 + t / period()) % 1.0)))),
            font=f2, fill=(225, 225, 225), stroke_width=3, stroke_fill=(0, 0, 0))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out = out.resize((out.width // 2, out.height // 2), Image.LANCZOS)
    out.save(OUT)
    print('\n-> %s %s' % (OUT, out.size))


# ── 主 ────────────────────────────────────────────────────────────────────
def main():
    print('=' * 72)
    print('千眼回廊 PZ_04 · 双光束几何')
    print('=' * 72)
    print('走廊 ±%.1f   出生 %.2f   出口 %.2f   玩家 %.1f 单位/秒' %
          (CORRIDOR_HALF, SPAWN_X, EXIT_X, PLAYER_SPEED))
    print('光束 %d 片  每片总宽 %.2f(半宽 %.2f)  地面速度 %.1f  → 单程 %.2f 秒 / 一个来回 %.2f 秒'
          % (BEAM_COUNT, BEAM_HALF_W * 2, BEAM_HALF_W, BEAM_SPEED,
             span() / BEAM_SPEED, period()))
    print('相位差 %.2f → %s' % (BEAM_PHASE_STEP,
          '两束始终左右镜像对开' if abs(BEAM_PHASE_STEP - 0.5) < 1e-6 else '两束错开 %.0f%% 周期' % (BEAM_PHASE_STEP * 100)))

    ph0 = start_phase_from_x(BEAM_RESET_X)
    print()
    print('复位状态: 光束落点 ' + '   '.join('%.2f%s' % (x, '→' if dr > 0 else '←') for x, dr in beam_positions(ph0)))

    t_safe, t = 0.0, 0.0
    while t < 60.0:
        if any(abs(SPAWN_X - bx) <= BEAM_HALF_W for bx, _ in beam_positions((ph0 + t / period()) % 1.0)):
            break
        t += DT
    t_safe = t
    print('出生点 %.2f 落在安全区内? %s   光束 %.2f 秒后扫到出生点(= 玩家的反应窗口)'
          % (SPAWN_X, in_gate(SPAWN_X), t_safe))

    print()
    print('暴露段(相邻安全区之间要冲过去的距离 / 冲刺耗时):')
    for i in range(len(GATE_MIN) - 1):
        g = GATE_MIN[i + 1] - GATE_MAX[i]
        print('  第 %d 段   %.2f 单位  →  %.2f 秒' % (i + 1, g, g / PLAYER_SPEED))

    print()
    print('=' * 72)
    print('离线模拟(到安全区就停、等窗口够宽再冲)')
    print('=' * 72)
    s = Sim(0.0)
    win, caught = s.run()
    if win:
        print('  完美预判(0 余量):✓ 通关 %.1f 秒,被抓 %d 次' % (win, caught))
    else:
        print('  完美预判(0 余量):✗ 未通关,最远只到 x=%.2f(进度 %.0f%%),被抓 %d 次'
              % (s.best, (s.best - SPAWN_X) / (EXIT_X - SPAWN_X) * 100, caught))
    for m in (0.3, 0.6, 0.9):
        s2 = Sim(m)
        w2, c2 = s2.run()
        print('  留 %.1f 单位余量: %s' % (m, ('✓ 通关 %.1f 秒,被抓 %d 次' % (w2, c2)) if w2
                                       else ('✗ 未通关(最远 x=%.2f),被抓 %d 次' % (s2.best, c2))))
    draw()


if __name__ == '__main__':
    main()
