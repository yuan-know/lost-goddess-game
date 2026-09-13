# -*- coding: utf-8 -*-
"""
eye_build_eyelid.py —— 把眼睑开合序列帧转成「纯眼睑覆盖层」

素材：video-frames-*.zip（480x480，眼睛内容 bbox 约 399x270）
     序列是「闭合 → 张开 → 闭合」的一次眨眼，最开在中间。

为什么要做「差异抠图」：
  这批帧是整只眼睛的完整渲染（含外壳和眼球）。直接叠到分层版上会把
  外壳、眼球全盖住。所以只保留「相对最开帧发生变化」的区域 ——
  那正好就是落下来的眼睑（以及被它盖住的眼球部分）。
  其余地方透明，底下的分层眼睛（眼球位置由旋钮控制）就露出来了。

对齐：
  480 帧里的眼睛 bbox 和分层版 `ec_eye_frame_back` 的 bbox 是同一只眼睛，
  按宽度缩放并中心对齐即可。

输出：
  <out>/ec_eyelid_00.png ... ec_eyelid_NN.png   （全画布 3400x1200）
  再交给 eye_console_pack.py（LAYERS 里 group="lid"）裁成统一框。

⚠ 2026-09-13 补的一步：裁到「眼窝开口」
  只做差异抠图是不够的。AI 视频帧与帧之间铜壳本身就在抖，
  阈值 22 + 外扩 4 会把**整只眼睛的下半部（含铜壳/下眼窝）**都判成"变化"，
  实测产出的覆盖层有 84k px 落在眼窝开口之外，是整只眼睛的完整渲染。
  这样播放眼睑动画时等于把视频自己的铜色一遍遍刷在分层版铜框上
  →「黄金瞳部分区域光影色彩随帧改变」。
  所以最后必须用 `--opening <ec_eye_mask.png> --opening-at <x,y>` 把 alpha
  裁进眼窝开口：开口内正常落眼睑，开口外 alpha 归 0，铜框纹丝不动。
  （也可对已产出的帧单独跑 tools/eye_eyelid_clip_opening.py）

用法：
  python eye_build_eyelid.py --frames docs/eye_console/_lid \
      --out docs/eye_console/_lid_src --count 20 \
      --opening Assets/_Project/Resources/Closeups/eye_console_layered/ec_eye_mask.png \
      --opening-at 1545,250
"""
import argparse
import glob
import os
import re

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

CANVAS = (3400, 1200)
# 分层版里眼睛（eye_frame_back）的 bbox，作为对齐基准
TARGET_BOX = (1474, 149, 1926, 456)


def frame_no(p):
    return int(re.search(r"(\d+)\.png$", os.path.basename(p)).group(1))


def alpha_bbox(a, t=16):
    m = a[..., 3] > t
    if not m.any():
        return None
    ys, xs = np.nonzero(m)
    return (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--frames", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--count", type=int, default=20)
    ap.add_argument("--thresh", type=float, default=22.0, help="与最开帧的差异阈值")
    ap.add_argument("--grow", type=int, default=4, help="差异掩码外扩像素")
    ap.add_argument("--erode", type=int, default=3,
                    help="alpha 向内腐蚀像素。源帧边缘有绿色残留"
                         "（AI 视频通病：alpha 1~200 的边缘像素 RGB≈(69,113,62)，G-R 达 +44），"
                         "腐蚀后边界落在不透明的铜色区里，露出的边缘由底下的分层眼睛补上，不留缝")
    ap.add_argument("--opening", default="",
                    help="眼窝开口遮罩 ec_eye_mask.png（全画布图层），传了就按它裁 alpha")
    ap.add_argument("--opening-at", default="",
                    help="上面那张遮罩在图上的左上角 x,y（例如 1545,250）")
    ap.add_argument("--opening-feather", type=float, default=1.0)
    args = ap.parse_args()

    opening = None
    if args.opening:
        m = np.asarray(Image.open(args.opening).convert("RGBA")).astype(np.float32)
        ox, oy = (int(v) for v in args.opening_at.split(","))
        full = np.zeros(CANVAS[::-1], np.float32)
        ma = m[..., 3]
        full[oy:oy + ma.shape[0], ox:ox + ma.shape[1]] = ma
        soft = np.clip(full / 255.0, 0, 1)
        if args.opening_feather > 0:
            soft = np.asarray(Image.fromarray((soft * 255).astype(np.uint8), "L")
                              .filter(ImageFilter.GaussianBlur(args.opening_feather))
                              ).astype(np.float32) / 255.0
        opening = np.clip(soft, 0, 1)
        print(f"眼窝开口遮罩：{args.opening} @ ({ox},{oy})，"
              f"覆盖 {int((opening > 0.02).sum())} px")

    fs = sorted(glob.glob(os.path.join(args.frames, "*.png")), key=frame_no)
    A = [np.asarray(Image.open(f).convert("RGBA")).astype(np.float32) for f in fs]

    # ── 用 alpha 高度找最开帧（峰值）─────────────────────────────────
    h = np.array([alpha_bbox(a)[3] - alpha_bbox(a)[1] for a in A])
    peak = int(np.argmax(h))
    print(f"共 {len(fs)} 帧；最开帧 = f{peak+1}（alpha 高 {h[peak]}，首帧 {h[0]}，末帧 {h[-1]}）")

    # 闭合段：从最开帧往后到末尾（张开→闭合）。
    # 用均匀索引采样：源视频本身就是等时间抽帧，眼睑下落也接近匀速，
    # 实测 00/05/10/15/19 的递进很自然。
    # （试过按 alpha 高度重采样，但高度只有 14 级分辨率，会出现重复帧。）
    seg = list(range(peak, len(fs)))
    print(f"取闭合段 f{seg[0]+1}..f{seg[-1]+1}（{len(seg)} 帧）→ 均匀重采样 {args.count} 帧")
    idxs = [seg[i] for i in np.linspace(0, len(seg) - 1, args.count).round().astype(int)]

    # ── 缩放 + 对齐 ────────────────────────────────────────────────
    ref_box = alpha_bbox(A[peak])
    tw, th = TARGET_BOX[2] - TARGET_BOX[0], TARGET_BOX[3] - TARGET_BOX[1]
    sw, sh = ref_box[2] - ref_box[0], ref_box[3] - ref_box[1]
    sx, sy = tw / sw, th / sh
    print(f"对齐：源眼睛 {sw}x{sh} → 目标 {tw}x{th}，缩放 x{sx:.4f} y{sy:.4f}")
    ref_center = ((ref_box[0] + ref_box[2]) / 2, (ref_box[1] + ref_box[3]) / 2)
    tgt_center = ((TARGET_BOX[0] + TARGET_BOX[2]) / 2, (TARGET_BOX[1] + TARGET_BOX[3]) / 2)

    def to_canvas(a):
        im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")
        nw, nh = int(round(im.width * sx)), int(round(im.height * sy))
        im = im.resize((nw, nh), Image.Resampling.LANCZOS)
        # 源图里 ref_center 缩放后应落在 tgt_center
        px = int(round(tgt_center[0] - ref_center[0] * sx))
        py = int(round(tgt_center[1] - ref_center[1] * sy))
        out = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
        out.alpha_composite(im, (px, py))
        return out

    ref_c = to_canvas(A[peak])
    ref = np.asarray(ref_c).astype(np.float32)

    os.makedirs(args.out, exist_ok=True)
    for old in glob.glob(os.path.join(args.out, "ec_eyelid_*.png")):
        os.remove(old)

    print("\n逐帧生成覆盖层：")
    for k, i in enumerate(idxs):
        raw = np.asarray(to_canvas(A[i])).astype(np.float32)

        # ① 先算差异 —— 必须用「原始 RGB」。
        #    （之前把颜色外扩放在这一步之前，结果连基准帧自己都产生了虚假差异，
        #      全开那一帧也长出一圈覆盖层。）
        diff = np.abs(raw[..., :3] - ref[..., :3]).max(axis=2)
        both = (raw[..., 3] > 16) | (ref[..., 3] > 16)
        m = (diff > args.thresh) & both
        if args.grow > 0:
            m = ndimage.binary_dilation(m, iterations=args.grow)
        m = ndimage.binary_fill_holes(m)
        soft = np.asarray(Image.fromarray((m * 255).astype(np.uint8), "L")
                          .filter(ImageFilter.GaussianBlur(1.5))).astype(np.float32) / 255.0

        # ② 再处理颜色：源帧外缘一圈 RGB 是绿的（AI 视频通病），羽化会把绿
        #    带到半透明边缘上。所以从「不透明 且 不偏绿」的像素向外做颜色外扩。
        #    注意判据要排除绿色本身 —— 否则最近的「不透明像素」也是绿的。
        cur = raw.copy()
        g = cur[..., 1] - np.maximum(cur[..., 0], cur[..., 2])
        good = (cur[..., 3] > 200) & (g <= 6)
        if good.any() and not good.all():
            ind = ndimage.distance_transform_edt(~good, return_distances=False, return_indices=True)
            cur[..., :3] = cur[..., :3][tuple(ind)]

        # ③ alpha 向内腐蚀：切掉源帧外缘的残留
        if args.erode > 0:
            core = ndimage.binary_erosion(raw[..., 3] > 200, iterations=args.erode)
            core = ndimage.binary_dilation(core, iterations=max(0, args.erode - 1))
        else:
            core = raw[..., 3] > 200
        aclean = np.asarray(Image.fromarray((core * 255).astype(np.uint8), "L")
                            .filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255.0

        out = cur.copy()
        out[..., 3] = np.clip(aclean * soft, 0, 1) * 255.0

        # ④ 兜底：把绿色通道夹到 max(R,B)。
        #    源帧内部个别地方也带绿调，颜色外扩救不到；直接压掉绿通道，
        #    视觉上变成中性铜灰，比露出绿块好得多。
        rb = np.maximum(out[..., 0], out[..., 2])
        out[..., 1] = np.minimum(out[..., 1], rb)

        # ⑤ 裁到眼窝开口（关键，见文件头说明）。
        #    没有这一步，覆盖层会把整只眼睛的下半部（含铜壳）都盖住，
        #    播放时铜框的光影会随帧抖。
        if opening is not None:
            out[..., 3] = out[..., 3] * opening
        p = os.path.join(args.out, f"ec_eyelid_{k:02d}.png")
        Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA").save(p)
        cov = float((out[..., 3] > 16).sum())
        print(f"  ec_eyelid_{k:02d}  ← f{i+1:02d}   覆盖 {cov:8.0f} px")
    print(f"\n[out] {args.out}")


if __name__ == "__main__":
    main()
