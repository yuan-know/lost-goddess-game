# -*- coding: utf-8 -*-
"""
extract_pose.py —— 参考视频 -> MediaPipe 骨骼角度序列

只取"角度",不取像素。所以 [[2026-08-22-animation-approach-decision]] 里记的
AI 视频三个坑(白边/脚底抖/角色漂形)全都不存在 —— 输出驱动的是项目现有骨骼。

产物: tools/pose/out/pose_<name>.json
    {
      "source": "...mp4", "fps": 24.0, "frameCount": 97,
      "detected": 97, "meanVisibility": 0.909,
      "joints": ["Lupper","Lfore",...],
      "worldAngles": [[f0 各关节世界角], [f1 ...], ...],   # 度
      "visibility":  [[f0 各关节可见度], ...]
    }

坐标约定: MediaPipe 图像 y 轴朝下,这里统一翻成 y 朝上(与 Unity / psb 一致),
所以输出的世界角可以直接和 skeleton_*.json 的 worldZ 相减。

用法:
  python tools/pose/extract_pose.py <video> <outName> [--model heavy|full|lite] [--preview]
例:
  python tools/pose/extract_pose.py "C:/Users/yuan/Downloads/角色待机循环视频生成.mp4" young_idle_ref
"""
import argparse
import io
import json
import math
import os

import cv2
import numpy as np

os.environ.setdefault('GLOG_minloglevel', '2')  # 压掉 mediapipe 的 C++ 日志

import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

OUT_DIR = 'tools/pose/out'
MODEL_DIR = 'tools/pose/models'

# MediaPipe Pose 33 点里我们需要的那些
LM = {
    'nose': 0,
    'lsh': 11, 'rsh': 12,
    'lel': 13, 'rel': 14,
    'lwr': 15, 'rwr': 16,
    'lhip': 23, 'rhip': 24,
    'lkn': 25, 'rkn': 26,
    'lank': 27, 'rank': 28,
    'lheel': 29, 'rheel': 30,
    'ltoe': 31, 'rtoe': 32,
}

# 关节定义: 名字 -> (起点, 终点)。世界角 = atan2 从起点指向终点的向量。
# 命名用解剖学左右(MediaPipe 的 left/right 是被拍者自己的左右)。
JOINTS = [
    ('Lupper', 'lsh', 'lel'),    # 左大臂
    ('Lfore', 'lel', 'lwr'),     # 左小臂
    ('Rupper', 'rsh', 'rel'),    # 右大臂
    ('Rfore', 'rel', 'rwr'),     # 右小臂
    ('Lthigh', 'lhip', 'lkn'),   # 左大腿
    ('Lshin', 'lkn', 'lank'),    # 左小腿
    ('Rthigh', 'rhip', 'rkn'),   # 右大腿
    ('Rshin', 'rkn', 'rank'),    # 右小腿
    ('Lfoot', 'lank', 'ltoe'),   # 左脚(踝关节 —— 生成器缺的次级运动之一)
    ('Rfoot', 'rank', 'rtoe'),
    ('Spine', 'hipC', 'shC'),    # 躯干: 髋中点 -> 肩中点
    ('Neck', 'shC', 'nose'),     # 头颈
]

# 关节 -> 参与计算的可见度取哪些点
JOINT_VIS = {name: (a, b) for name, a, b in JOINTS}


def synth_points(p):
    """补出 MediaPipe 没有的中点(髋中点/肩中点)"""
    p['hipC'] = ((p['lhip'][0] + p['rhip'][0]) / 2.0, (p['lhip'][1] + p['rhip'][1]) / 2.0)
    p['shC'] = ((p['lsh'][0] + p['rsh'][0]) / 2.0, (p['lsh'][1] + p['rsh'][1]) / 2.0)
    return p


def world_angle(a, b):
    return math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))


def extract(video, model='heavy', preview=None):
    model_path = os.path.join(MODEL_DIR, 'pose_landmarker_%s.task' % model)
    if not os.path.exists(model_path):
        raise SystemExit('[extract_pose] 缺模型 %s' % model_path)

    cap = cv2.VideoCapture(video)
    if not cap.isOpened():
        raise SystemExit('[extract_pose] 打不开视频 %s' % video)
    fps = cap.get(cv2.CAP_PROP_FPS) or 24.0
    w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

    opts = vision.PoseLandmarkerOptions(
        base_options=mp_python.BaseOptions(model_asset_path=model_path),
        running_mode=vision.RunningMode.VIDEO,
        num_poses=1,
        min_pose_detection_confidence=0.3,
        min_pose_presence_confidence=0.3,
        min_tracking_confidence=0.3,
    )

    names = [j[0] for j in JOINTS]
    angles, vis_rows = [], []
    writer = None
    n = 0

    with vision.PoseLandmarker.create_from_options(opts) as landmarker:
        while True:
            ok, frame = cap.read()
            if not ok:
                break
            img = mp.Image(image_format=mp.ImageFormat.SRGB,
                           data=cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
            res = landmarker.detect_for_video(img, int(n * 1000 / fps))
            n += 1

            if not res.pose_landmarks:
                angles.append([None] * len(JOINTS))
                vis_rows.append([0.0] * len(JOINTS))
                continue

            lm = res.pose_landmarks[0]
            # 归一化坐标 -> 按画幅比例还原纵横比,并把 y 翻成朝上
            pts = {k: (lm[i].x * w, -lm[i].y * h) for k, i in LM.items()}
            visd = {k: lm[i].visibility for k, i in LM.items()}
            visd['hipC'] = (visd['lhip'] + visd['rhip']) / 2.0
            visd['shC'] = (visd['lsh'] + visd['rsh']) / 2.0
            pts = synth_points(pts)

            angles.append([round(world_angle(pts[a], pts[b]), 4) for _, a, b in JOINTS])
            vis_rows.append([round(min(visd[a], visd[b]), 4) for _, a, b in JOINTS])

            if preview:
                if writer is None:
                    writer = cv2.VideoWriter(preview, cv2.VideoWriter_fourcc(*'mp4v'), fps, (w, h))
                for _, a, b in JOINTS:
                    pa = (int(pts[a][0]), int(-pts[a][1]))
                    pb = (int(pts[b][0]), int(-pts[b][1]))
                    cv2.line(frame, pa, pb, (0, 255, 0), 3)
                    cv2.circle(frame, pa, 5, (0, 0, 255), -1)
                    cv2.circle(frame, pb, 5, (0, 0, 255), -1)
                writer.write(frame)

    cap.release()
    if writer is not None:
        writer.release()

    va = np.array([[v for v in row] for row in vis_rows], dtype=float)
    detected = sum(1 for row in angles if row[0] is not None)
    return {
        'source': video.replace('\\', '/'),
        'fps': fps,
        'width': w, 'height': h,
        'frameCount': n,
        'detected': detected,
        'meanVisibility': round(float(va[va > 0].mean()) if (va > 0).any() else 0.0, 4),
        'model': model,
        'joints': names,
        'worldAngles': angles,
        'visibility': vis_rows,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('video')
    ap.add_argument('outName')
    ap.add_argument('--model', default='heavy', choices=['heavy', 'full', 'lite'])
    ap.add_argument('--preview', action='store_true', help='额外导一段画了骨架的校验视频')
    a = ap.parse_args()

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    preview = os.path.join(OUT_DIR, 'preview_%s.mp4' % a.outName) if a.preview else None

    data = extract(a.video, a.model, preview)
    out = os.path.join(OUT_DIR, 'pose_%s.json' % a.outName)
    io.open(out, 'w', encoding='utf-8').write(json.dumps(data, indent=1, ensure_ascii=False))

    print('[extract_pose] %s' % out)
    print('  frames=%d detected=%d (%.0f%%) meanVis=%.3f fps=%.1f'
          % (data['frameCount'], data['detected'],
             100.0 * data['detected'] / max(1, data['frameCount']),
             data['meanVisibility'], data['fps']))
    arr = np.array([[x for x in row] for row in data['worldAngles'] if row[0] is not None], dtype=float)
    print('  per-joint world angle range:')
    for i, nm in enumerate(data['joints']):
        c = arr[:, i]
        print('    %-7s min=%8.1f max=%8.1f range=%6.1f' % (nm, c.min(), c.max(), c.max() - c.min()))
    if preview:
        print('  preview -> %s' % preview)


if __name__ == '__main__':
    main()
