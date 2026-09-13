# -*- coding: utf-8 -*-
"""
eye_console_preview_composite.py
================================
离线模拟 EyeConsoleCloseup.cs 的图层摆放，把「底层控制台序列帧 + 旋钮图层
+ 眼睑序列帧 + 眼球」按运行时同样规则合成为 1920x678 的容器图，
用来在不开 Unity 的情况下核对：对齐、裁切、层序、拉杆/眼睑联动。

用法:
  python tools/eye_console_preview_composite.py --res <Resources/Closeups/eye_console_layered> \
      --base-manifest <ec_base_manifest.json> --out <预览目录>
"""
import argparse
import json
import os

import cv2
import numpy as np

CONT_W, CONT_H = 1920.0, 678.0
SRC_W, SRC_H = 3400.0, 1200.0
CENTER = np.array([SRC_W * 0.5, SRC_H * 0.5])


def src_to_cont(p):
    return np.array([(p[0] / SRC_W - 0.5) * CONT_W,
                     (0.5 - p[1] / SRC_H) * CONT_H])


def place(canvas, img, rect, pivot, rot_deg=0.0):
    """按 manifest 的 rect/pivot 把 img 摆到容器画布上（容器中心=原点，y 向上）。"""
    x0, y0, x1, y1 = rect
    w_px, h_px = x1 - x0, y1 - y0
    sx, sy = CONT_W / SRC_W, CONT_H / SRC_H
    w, h = w_px * sx, h_px * sy
    piv = ((pivot[0] - x0) / w_px, 1.0 - (pivot[1] - y0) / h_px)
    ap = src_to_cont(pivot) - src_to_cont(CENTER)
    # 目标矩形（画布像素、左上原点）
    left = ap[0] - piv[0] * w + CONT_W / 2
    top = CONT_H / 2 - (ap[1] + (1 - piv[1]) * h)

    if abs(rot_deg) > 1e-6:
        M = cv2.getRotationMatrix2D((piv[0] * w, piv[1] * h), -rot_deg, 1.0)
        M[0, 2] += left
        M[1, 2] += top
        src = cv2.resize(img, (max(1, int(round(w))), max(1, int(round(h)))),
                         interpolation=cv2.INTER_AREA if w < img.shape[1] else cv2.INTER_LINEAR)
        warp = cv2.warpAffine(src, M, (int(CONT_W), int(CONT_H)),
                              flags=cv2.INTER_LINEAR,
                              borderMode=cv2.BORDER_CONSTANT,
                              borderValue=(0, 0, 0, 0))
    else:
        wi, hi = int(round(w)), int(round(h))
        l, t = int(round(left)), int(round(top))
        src = cv2.resize(img, (wi, hi),
                         interpolation=cv2.INTER_AREA if w < img.shape[1] else cv2.INTER_LINEAR)
        warp = np.zeros((int(CONT_H), int(CONT_W), 4), np.uint8)
        x0c, y0c = max(0, l), max(0, t)
        x1c, y1c = min(int(CONT_W), l + wi), min(int(CONT_H), t + hi)
        if x1c > x0c and y1c > y0c:
            warp[y0c:y1c, x0c:x1c] = src[y0c - t:y1c - t, x0c - l:x1c - l]

    a = warp[:, :, 3:4].astype(np.float32) / 255.0
    canvas[:, :, :3] = (warp[:, :, :3] * a + canvas[:, :, :3] * (1 - a)).astype(np.uint8)
    canvas[:, :, 3] = np.maximum(canvas[:, :, 3],
                                 (a[:, :, 0] * 255).astype(np.uint8))
    return canvas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--res", required=True)
    ap.add_argument("--base-manifest", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--states", default="0,0.25,0.5,0.75,1.0")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    mf = json.load(open(os.path.join(args.res, "manifest.json"), encoding="utf-8"))
    bm = json.load(open(args.base_manifest, encoding="utf-8"))
    layers = {L["key"]: L for L in mf["layers"]}

    def load(name):
        im = cv2.imread(os.path.join(args.res, name + ".png"), cv2.IMREAD_UNCHANGED)
        if im is None:
            raise SystemExit("缺 " + name)
        return im

    def find(key):
        for L in mf["layers"]:
            if L["key"] == key:
                return L
        raise SystemExit("manifest 缺 " + key)

    base_frames = bm["frames"]
    lid = find("eyelid")
    lid_frames = lid["frames"]
    eye = find("eyeball")
    eng = find("eye_frame_back")

    outs = []
    for t in [float(v) for v in args.states.split(",")]:
        c = np.zeros((int(CONT_H), int(CONT_W), 4), np.uint8)
        c[:, :, :3] = 26
        c[:, :, 3] = 255

        # 0) 最底层：拉杆旋转序列帧
        bi = int(round(t * (len(base_frames) - 1)))
        place(c, load(base_frames[bi]), bm["rect"],
              [(bm["rect"][0] + bm["rect"][2]) / 2, (bm["rect"][1] + bm["rect"][3]) / 2])

        # 1) 铜框 + 眼球
        place(c, load(eng["file"]), eng["rect"], eng["pivot"])
        place(c, load(eye["file"]), eye["rect"], eye["pivot"])
        front = find("eye_frame_front")
        place(c, load(front["file"]), front["rect"], front["pivot"])

        # 2) 眼睑（与拉杆同一 leverT）
        li = int(round(t * (len(lid_frames) - 1)))
        place(c, load(lid_frames[li]), lid["rect"], lid["pivot"])

        # 3) 旋钮
        for k, ang in (("knob_left_top", -40), ("knob_mid_top", 30)):
            sh = find(k.replace("_top", "_shell"))
            place(c, load(sh["file"]), sh["rect"], sh["pivot"])
            L = find(k)
            place(c, load(L["file"]), L["rect"], L["pivot"], rot_deg=ang)

        flat = c[:, :, :3].copy()
        cv2.imwrite(os.path.join(args.out, "composite_t%02d.png" % int(t * 100)), flat)
        outs.append(flat)

    rows = [np.concatenate(outs[i:i + 1], axis=0) for i in range(len(outs))]
    cv2.imwrite(os.path.join(args.out, "composite_all.jpg"),
                np.concatenate(rows, axis=0), [cv2.IMWRITE_JPEG_QUALITY, 92])
    print("ok ->", args.out)


if __name__ == "__main__":
    main()
