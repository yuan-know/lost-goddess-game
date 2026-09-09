# -*- coding: utf-8 -*-
"""
retarget.py —— MediaPipe 关节角 -> Unity AnimationClip 曲线数据

管线第三步(extract_skeleton -> extract_pose -> 本步 -> Unity 侧导入)。

核心思路: 骨骼要写的**局部角** = 视频测出的世界角 - 该骨骼 rest 的世界角。
父子骨都各自减自己的 rest,所以父骨的旋转不会被重复累加到子骨上
(局部角本身就是"相对父骨当前朝向"的量)。

产物: tools/pose/out/clip_<form>_<name>.json —— Unity 侧直接读它建 clip:
    {
      "form":"young", "clipName":"young_Idle", "loop":true,
      "frameRate":24.0, "duration":4.0,
      "curves":[ {"path":"bone_1/bone_6","property":"localEulerAnglesRaw.z",
                  "keys":[{"t":0.0,"v":-2.31}, ...]}, ... ]
    }

三件必须做的处理(不做就出问题):
  1. **遮挡关节镜像** —— 参考视频里侧身站,右臂 visibility 只 0.35,角度不可信。
     低于阈值的关节改用对侧关节的镜像相位驱动(idle 用同相,walk 用半周期反相)。
  2. **循环闭合** —— 首尾帧差最多 2.7°,直接循环会"抽一下"。把整条曲线做
     线性去漂移,强制末帧 == 首帧。
  3. **切线连续** —— 这正是 [[2026-08-22-animation-approach-decision]] 记的老 bug 根因:
     旧生成器 `new Keyframe(t,v,0f,0f)` 把切线全归零,每帧顿一下。这里改成
     中心差分自动切线,速度不再在关键帧处归零。

用法:
  python tools/pose/retarget.py young pose_young_idle_ref young_Idle --loop --dur 4.0
"""
import argparse
import io
import json
import os

import numpy as np

OUT_DIR = 'tools/pose/out'

# 关节 -> 骨骼路径。young 的 9 根骨语义(FK 验证过,见 skeleton_young.json):
#   bone_2/3 = 左大臂/左小臂     bone_4/5 = 右大臂/右小臂
#   bone_6/7 = 左大腿/左小腿     bone_8/9 = 右大腿/右小腿
#   bone_1   = 根骨(兼头颈/躯干)
JOINT_TO_BONE = {
    'young': {
        'Lupper': 'bone_1/bone_2',
        'Lfore':  'bone_1/bone_2/bone_3',
        'Rupper': 'bone_1/bone_4',
        'Rfore':  'bone_1/bone_4/bone_5',
        'Lthigh': 'bone_1/bone_6',
        'Lshin':  'bone_1/bone_6/bone_7',
        'Rthigh': 'bone_1/bone_8',
        'Rshin':  'bone_1/bone_8/bone_9',
        'Spine':  'bone_1',
    },
}

# 遮挡时用哪个关节镜像补(对侧同名部位)
MIRROR = {
    'Rupper': 'Lupper', 'Rfore': 'Lfore', 'Rthigh': 'Lthigh', 'Rshin': 'Lshin',
    'Lupper': 'Rupper', 'Lfore': 'Rfore', 'Lthigh': 'Rthigh', 'Lshin': 'Rshin',
}

VIS_THRESHOLD = 0.6   # 低于此值认为该关节不可信
SMOOTH_WIN = 5        # 滑动平均窗口(奇数)


def unwrap_deg(a):
    """去掉 ±180 跳变,保证曲线连续(否则插值会绕大圈)"""
    return np.degrees(np.unwrap(np.radians(np.asarray(a, dtype=float))))


def smooth(a, win=SMOOTH_WIN):
    """循环边界的滑动平均 —— 用 wrap 模式,免得首尾被压平破坏循环"""
    a = np.asarray(a, dtype=float)
    if win <= 1 or len(a) < win:
        return a
    k = np.ones(win) / win
    pad = win // 2
    ext = np.concatenate([a[-pad:], a, a[:pad]])
    return np.convolve(ext, k, mode='valid')


def close_loop(a):
    """线性去漂移,强制首尾相等 —— 循环动画不抽帧的前提"""
    a = np.asarray(a, dtype=float)
    n = len(a)
    if n < 2:
        return a
    drift = a[-1] - a[0]
    return a - drift * (np.arange(n) / (n - 1.0))


def phase_shift(a, frac):
    """整体相位平移 frac 个周期(用于给镜像肢体做半周期反相)"""
    a = np.asarray(a, dtype=float)
    n = len(a)
    return np.roll(a, int(round(frac * n)))


def auto_tangent_keys(times, values):
    """中心差分切线。旧生成器把切线设 0 导致每帧顿一下,这里给真实速度。"""
    times = np.asarray(times, dtype=float)
    values = np.asarray(values, dtype=float)
    n = len(times)
    keys = []
    for i in range(n):
        if n == 1:
            slope = 0.0
        elif i == 0:
            slope = (values[1] - values[0]) / (times[1] - times[0])
        elif i == n - 1:
            slope = (values[-1] - values[-2]) / (times[-1] - times[-2])
        else:
            slope = (values[i + 1] - values[i - 1]) / (times[i + 1] - times[i - 1])
        keys.append({'t': round(float(times[i]), 5),
                     'v': round(float(values[i]), 4),
                     'inT': round(float(slope), 4),
                     'outT': round(float(slope), 4)})
    return keys


def build(form, pose_name, clip_name, loop, duration, mirror_phase, decimate):
    sk = json.load(io.open(os.path.join(OUT_DIR, 'skeleton_%s.json' % form), encoding='utf-8'))
    rest = {b['path']: b['worldZ'] for b in sk['bones']}

    pose = json.load(io.open(os.path.join(OUT_DIR, 'pose_%s.json' % pose_name), encoding='utf-8'))
    names = pose['joints']
    ang = np.array([[(v if v is not None else np.nan) for v in row]
                    for row in pose['worldAngles']], dtype=float)
    vis = np.array(pose['visibility'], dtype=float)

    # 丢检帧用前后插值补上
    for c in range(ang.shape[1]):
        col = ang[:, c]
        bad = np.isnan(col)
        if bad.any() and not bad.all():
            col[bad] = np.interp(np.flatnonzero(bad), np.flatnonzero(~bad), col[~bad])

    joint_ang = {}
    mean_vis = {}
    for i, n in enumerate(names):
        joint_ang[n] = unwrap_deg(ang[:, i])
        mean_vis[n] = float(vis[:, i].mean())

    mapping = JOINT_TO_BONE[form]
    notes = []

    # --- 处理 1: 遮挡关节用对侧镜像 ---
    for j in list(mapping.keys()):
        if j not in joint_ang:
            continue
        if mean_vis.get(j, 1.0) < VIS_THRESHOLD:
            src = MIRROR.get(j)
            if src and mean_vis.get(src, 0) >= VIS_THRESHOLD:
                base = joint_ang[src]
                # 取对侧的"相对自身均值的摆动量",再叠到本侧 rest 上
                swing = base - base.mean()
                if mirror_phase:
                    swing = phase_shift(swing, 0.5)
                joint_ang[j] = joint_ang[j].mean() + swing
                notes.append('%s vis=%.2f 过低 -> 用 %s 镜像%s'
                             % (j, mean_vis[j], src, '(反相)' if mirror_phase else ''))

    n_src = ang.shape[0]
    step = max(1, int(decimate))
    idx = list(range(0, n_src, step))
    if idx[-1] != n_src - 1:
        idx.append(n_src - 1)
    times = np.linspace(0.0, duration, len(idx))

    curves = []
    for joint, path in mapping.items():
        if joint not in joint_ang or path not in rest:
            continue
        series = smooth(joint_ang[joint])
        local = series - rest[path]          # 世界角 -> 局部角
        # rest worldZ 可能是 251° 这类未归一化的值(psb 里 bone_4 就是),
        # 直接相减会得到 -352° 这种绕大圈的角 —— 平移回 (-180,180] 的等价角。
        local = local - 360.0 * np.round(local.mean() / 360.0)
        if loop:
            local = close_loop(local)        # 处理 2
        sampled = local[idx]
        curves.append({
            'path': path,
            'property': 'localEulerAnglesRaw.z',
            'joint': joint,
            'meanVisibility': round(mean_vis.get(joint, 0.0), 3),
            'keys': auto_tangent_keys(times, sampled),   # 处理 3
        })

    return {
        'form': form,
        'clipName': clip_name,
        'loop': bool(loop),
        'frameRate': pose['fps'],
        'duration': duration,
        'sourceVideo': pose['source'],
        'sourceFrames': n_src,
        'keyCount': len(idx),
        'notes': notes,
        'curves': curves,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('form', choices=['young', 'middle', 'old'])
    ap.add_argument('poseName', help='pose_<name>.json 的 <name>')
    ap.add_argument('clipName', help='生成的 clip 名,如 young_Idle')
    ap.add_argument('--loop', action='store_true')
    ap.add_argument('--dur', type=float, default=None, help='clip 时长(秒),默认按源视频')
    ap.add_argument('--mirror-phase', action='store_true',
                    help='镜像补齐时反相半周期(walk 用;idle 不要)')
    ap.add_argument('--decimate', type=int, default=2,
                    help='每 N 帧取一个关键帧(默认2,24fps->12关键帧/秒)')
    a = ap.parse_args()

    pose = json.load(io.open(os.path.join(OUT_DIR, 'pose_%s.json' % a.poseName), encoding='utf-8'))
    dur = a.dur if a.dur else pose['frameCount'] / pose['fps']

    data = build(a.form, a.poseName, a.clipName, a.loop, dur, a.mirror_phase, a.decimate)
    out = os.path.join(OUT_DIR, 'clip_%s_%s.json' % (a.form, a.clipName))
    io.open(out, 'w', encoding='utf-8').write(json.dumps(data, indent=1, ensure_ascii=False))

    print('[retarget] %s' % out)
    print('  clip=%s loop=%s dur=%.2fs keys=%d curves=%d'
          % (data['clipName'], data['loop'], data['duration'], data['keyCount'], len(data['curves'])))
    for note in data['notes']:
        print('  ! %s' % note)
    print('  per-curve local-angle range (deg):')
    for c in data['curves']:
        vs = [k['v'] for k in c['keys']]
        print('    %-22s %-7s min=%7.2f max=%7.2f range=%6.2f loopGap=%.3f'
              % (c['path'], c['joint'], min(vs), max(vs), max(vs) - min(vs),
                 abs(vs[0] - vs[-1])))


if __name__ == '__main__':
    main()
