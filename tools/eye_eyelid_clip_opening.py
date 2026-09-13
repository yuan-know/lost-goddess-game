# -*- coding: utf-8 -*-
"""
eye_eyelid_clip_opening.py —— 把眼睑覆盖层裁进「眼窝开口」范围内

问题（2026-09-13 诊断）
----------------------
`ec_eyelid_*.png` 不是薄薄一层眼睑，而是**整只眼睛的完整渲染**：
它的 alpha 是一个实心的大杏仁，把整只眼睛（含铜壳/下眼窝）都盖住了。
实测眼睑层的足迹 104024 px，其中 **83983 px（81%）在眼窝开口之外**，
全落在铜框和外壳上。

于是播放眼睑动画时，等于把 AI 视频自己的铜色 + 逐帧抖动的光影，
一遍遍刷在分层版的铜框上 —— 外部看到的就是「黄金瞳部分区域光影色彩改变」。
单独播放这些 PNG 不会有这个问题，因为那时你看到的就是那一帧的完整渲染，
没有"下面那层铜"可以对比。

修法
----
把 alpha 裁到 `ec_eye_mask`（分层版的眼窝开口）范围内，向外放 2px 再羽化 1.5px，
让边缘落在铜框内缘上：
  · 开口内 —— 眼睑照常落下/闭合，动画完全保留（眼睑本来就只该出现在这里）；
  · 开口外 —— alpha 归 0，铜框再也不会被视频帧覆盖。
实测铜框区（开口外）色差从「均值 10~22、每帧 6k~18k 像素」降到 **恒为 0**。

用法
----
  python tools/eye_eyelid_clip_opening.py \
      --dir Assets/_Project/Resources/Closeups/eye_console_layered [--dry-run]
"""
import argparse
import json
import os

import cv2
import numpy as np

DILATE = 0       # 开口额外向外放多少像素（0 = 就用美术遮罩自己的边界）
FEATHER = 1.0    # 对遮罩 alpha 做的高斯羽化（会把边界温和地外扩 ~1px）


def build_opening_soft(dirpath, lid_rect, mask_rect, mask_alpha):
    """把 eye_mask 摆进眼睑层的裁切框坐标系，返回 [0,1] 软掩码。

    直接用美术遮罩的 alpha 本身 —— 它自带抗锯齿（211 级），
    边界落在铜框内缘，不需要我们再造一个边界。
    """
    W = int(round(lid_rect[2] - lid_rect[0]))
    H = int(round(lid_rect[3] - lid_rect[1]))
    full = np.zeros((H, W), np.float32)
    ox = int(round(mask_rect[0] - lid_rect[0]))
    oy = int(round(mask_rect[1] - lid_rect[1]))
    mh, mw = mask_alpha.shape
    x0, y0 = max(0, ox), max(0, oy)
    x1, y1 = min(W, ox + mw), min(H, oy + mh)
    if x1 > x0 and y1 > y0:
        full[y0:y1, x0:x1] = mask_alpha[y0 - oy:y1 - oy, x0 - ox:x1 - ox]

    soft = np.clip(full.astype(np.float32) / 255.0, 0.0, 1.0)
    if DILATE > 0:
        k = 2 * DILATE + 1
        soft = cv2.dilate(soft, np.ones((k, k), np.float32))
    if FEATHER > 0:
        soft = cv2.GaussianBlur(soft, (0, 0), FEATHER)
    return np.clip(soft, 0.0, 1.0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--mask", default="ec_eye_mask")
    ap.add_argument("--prefix", default="ec_eyelid_")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    mf = json.load(open(os.path.join(args.dir, "manifest.json"), encoding="utf-8"))
    layers = {L["key"]: L for L in mf["layers"]}
    lid = layers["eyelid"]
    mask_layer = layers["eye_mask"]

    mask_alpha = cv2.imread(os.path.join(args.dir, args.mask + ".png"),
                            cv2.IMREAD_UNCHANGED)[:, :, 3]
    soft = build_opening_soft(args.dir, lid["rect"], mask_layer["rect"], mask_alpha)

    names = sorted(f for f in os.listdir(args.dir)
                   if f.startswith(args.prefix) and f.endswith(".png"))
    print("眼窝开口软掩码：有效 %d px，占眼睑裁切框 %.1f%%"
          % (int((soft > 0.02).sum()), 100 * (soft > 0.02).mean()))
    print("%-18s %12s %12s %12s" % ("file", "裁前 a>0", "裁后 a>0", "裁掉"))
    for n in names:
        p = os.path.join(args.dir, n)
        im = cv2.imread(p, cv2.IMREAD_UNCHANGED)
        before = int((im[:, :, 3] > 0).sum())
        out = im.copy()
        out[:, :, 3] = np.round(im[:, :, 3].astype(np.float32) * soft).astype(np.uint8)
        after = int((out[:, :, 3] > 0).sum())
        print("%-18s %12d %12d %12d" % (n, before, after, before - after))
        if not args.dry_run:
            cv2.imwrite(p, out)
    print("[done]" + (" (dry-run)" if args.dry_run else " 已写回 " + args.dir))


if __name__ == "__main__":
    main()
