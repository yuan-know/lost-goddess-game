# -*- coding: utf-8 -*-
"""
run_pipeline.py —— 一条命令跑完 骨架 -> 姿态 -> 骨骼曲线

用法:
  python tools/pose/run_pipeline.py --video "C:/path/ref.mp4" --form young \
      --clip young_Idle --loop --dur 4.0

跑完去 Unity:菜单 `失落的女神 ▸ 动作捕捉 ▸ 导入姿态 Clip (JSON)`,选生成的
tools/pose/out/clip_<form>_<clip>.json。

--mirror-phase 什么时候加:
  · Idle  -> 不加。左右肢基本同相,反相会让站着的人诡异地拧起来。
  · Walk  -> 加。步态本身左右差半个周期,遮挡侧必须反相才对。
"""
import argparse
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

# Windows 控制台默认 GBK,中文/▸ 之类字符会 UnicodeEncodeError。
# 自己的 stdout 转 UTF-8,子进程通过环境变量同样处理。
try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass
os.environ['PYTHONIOENCODING'] = 'utf-8:replace'


def run(args, desc):
    print('\n=== %s ===' % desc)
    r = subprocess.run([sys.executable] + args)
    if r.returncode != 0:
        raise SystemExit('[run_pipeline] 这步失败,已中止: %s' % desc)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--video', required=True)
    ap.add_argument('--form', default='young', choices=['young', 'middle', 'old'])
    ap.add_argument('--clip', required=True, help='clip 名,如 young_Idle')
    ap.add_argument('--loop', action='store_true')
    ap.add_argument('--dur', type=float, default=None)
    ap.add_argument('--mirror-phase', action='store_true')
    ap.add_argument('--decimate', type=int, default=2)
    ap.add_argument('--model', default='heavy')
    ap.add_argument('--preview', action='store_true')
    a = ap.parse_args()

    pose_name = a.clip.lower()

    run([os.path.join(HERE, 'extract_skeleton.py'), a.form], '1/3 解析骨架 rest pose')

    step2 = [os.path.join(HERE, 'extract_pose.py'), a.video, pose_name, '--model', a.model]
    if a.preview:
        step2.append('--preview')
    run(step2, '2/3 视频 -> 关节角(MediaPipe)')

    step3 = [os.path.join(HERE, 'retarget.py'), a.form, pose_name, a.clip,
             '--decimate', str(a.decimate)]
    if a.loop:
        step3.append('--loop')
    if a.mirror_phase:
        step3.append('--mirror-phase')
    if a.dur:
        step3 += ['--dur', str(a.dur)]
    run(step3, '3/3 关节角 -> 骨骼局部角曲线')

    print('\n完成。下一步在 Unity:')
    print('  菜单: 失落的女神 > 动作捕捉 > 导入姿态 Clip (JSON)')
    print('  选 tools/pose/out/clip_%s_%s.json' % (a.form, a.clip))


if __name__ == '__main__':
    main()
