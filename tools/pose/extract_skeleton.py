# -*- coding: utf-8 -*-
"""
extract_skeleton.py —— 从 .psb.meta 的 characterData 解析骨架 rest pose

产物: tools/pose/out/skeleton_<form>.json
    {
      "form": "young",
      "bones": [
        {"name":"bone_1","parent":-1,"parentName":null,
         "localPos":[x,y],          # 相对父骨的局部坐标(psb 像素)
         "localZ":  81.93,          # 相对父骨的局部旋转(度) = Unity localEulerAngles.z 的 rest 值
         "worldPos":[x,y],          # FK 算出的世界坐标
         "worldZ":  81.93,          # FK 算出的世界角(度)
         "tip":    [x,y],           # 骨骼末端世界坐标
         "length": 181.4},
        ...
      ]
    }

worldZ 是把 MediaPipe 关键点角度对回骨骼的锚 —— 视频测出的世界角减去 rest worldZ,
就是该骨骼要写进 clip 的局部角增量。

用法: python tools/pose/extract_skeleton.py            # 三个形态全导
      python tools/pose/extract_skeleton.py young      # 只导青年
"""
import io
import json
import math
import os
import re
import sys

PSB_DIR = 'Assets/_Project/Art/Characters'
OUT_DIR = 'tools/pose/out'

# characterData.bones 里每根骨头的字段块。position/rotation 都是相对父骨的局部值,
# rotation 只有 z/w 非零(2D 骨骼绕 z 轴),所以 z 角 = 2*atan2(z, w)。
BONE_RE = re.compile(
    r'- name: (\S+)\s+'
    r'guid: \S+\s+'
    r'position: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: [-\d.e+]+\}\s+'
    r'rotation: \{x: [-\d.e+]+, y: -?([-\d.e+]+), z: ([-\d.e+]+), w: ([-\d.e+]+)\}\s+'
    r'length: ([-\d.e+]+)\s+'
    r'parentId: (-?\d+)'
)


def quat_z_to_deg(z, w):
    """2D 骨骼四元数 -> 绕 z 轴角度(度)"""
    return math.degrees(2.0 * math.atan2(z, w))


def parse_character_data(form):
    path = os.path.join(PSB_DIR, form + '.psb.meta')
    if not os.path.exists(path):
        raise SystemExit('[extract_skeleton] 找不到 ' + path)

    txt = io.open(path, encoding='utf-8', errors='replace').read()
    i = txt.find('characterData')
    if i < 0:
        raise SystemExit('[extract_skeleton] %s 里没有 characterData(未做 2D 绑骨?)' % form)

    # characterData 下面依次是 bones / characterGroups / parts,只取 bones 段
    seg = txt[i:]
    for stop in ('\n    characterGroups', '\n    parts'):
        j = seg.find(stop)
        if j > 0:
            seg = seg[:j]
            break

    bones = []
    for m in BONE_RE.finditer(seg):
        name, x, y, _qy, qz, qw, length, pid = m.groups()
        bones.append({
            'name': name,
            'parent': int(pid),
            'localPos': [float(x), float(y)],
            'localZ': round(quat_z_to_deg(float(qz), float(qw)), 4),
            'length': round(float(length), 3),
        })
    if not bones:
        raise SystemExit('[extract_skeleton] %s 的 characterData 解析出 0 根骨头' % form)
    return bones


def forward_kinematics(bones):
    """把局部 pos/rot 累乘成世界 pos/角,顺带算骨骼末端 tip。

    父骨一定在列表里排在子骨之前(parentId 是索引),所以顺序遍历即可。
    """
    for idx, b in enumerate(bones):
        pid = b['parent']
        if pid < 0:
            wx, wy = b['localPos']
            wa = b['localZ']
            b['parentName'] = None
        else:
            p = bones[pid]
            px, py = p['worldPos']
            pa = p['worldZ']
            r = math.radians(pa)
            lx, ly = b['localPos']
            wx = px + lx * math.cos(r) - ly * math.sin(r)
            wy = py + lx * math.sin(r) + ly * math.cos(r)
            wa = pa + b['localZ']
            b['parentName'] = p['name']

        b['worldPos'] = [round(wx, 3), round(wy, 3)]
        b['worldZ'] = round(wa, 4)
        r = math.radians(wa)
        b['tip'] = [round(wx + b['length'] * math.cos(r), 3),
                    round(wy + b['length'] * math.sin(r), 3)]
    return bones


def bone_path(bones, idx):
    """Unity clip 里的曲线路径,如 bone_1/bone_6/bone_7"""
    parts = []
    while idx >= 0:
        parts.append(bones[idx]['name'])
        idx = bones[idx]['parent']
    return '/'.join(reversed(parts))


def run(form):
    bones = forward_kinematics(parse_character_data(form))
    for i, b in enumerate(bones):
        b['path'] = bone_path(bones, i)

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    out = os.path.join(OUT_DIR, 'skeleton_%s.json' % form)
    io.open(out, 'w', encoding='utf-8').write(
        json.dumps({'form': form, 'bones': bones}, indent=1, ensure_ascii=False))

    print('[%s] %d bones -> %s' % (form, len(bones), out))
    for b in bones:
        print('  %-8s parent=%-8s worldZ=%8.2f len=%7.1f  %s'
              % (b['name'], b['parentName'] or 'ROOT', b['worldZ'], b['length'], b['path']))
    return bones


if __name__ == '__main__':
    forms = sys.argv[1:] or ['young', 'middle', 'old']
    for f in forms:
        run(f)
        print()
