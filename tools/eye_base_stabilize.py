# -*- coding: utf-8 -*-
"""
eye_base_stabilize.py —— 消掉底层控制台序列帧的「面板亮度漂移」

问题（2026-09-13）
------------------
`ec_base_00..20.png` 是 AI 视频抽帧。AI 每帧把整块控制台重新渲一遍，
于是**面板本身的亮度/对比度随帧漂移**：
    帧0 面板平均亮度 56.40 → 帧20 61.84   (+9.6%)
    面板区 50% 的像素在 21 帧之间亮度极差 > 6
拉杆一动，下方控制台整体变亮/变暗 —— 用户报的「操控拉杆时控制台亮度改变」。

走过弯路，记下来别再踩
----------------------
  · **逐像素阈值冻结面板**（「与参考帧差异 > 阈值就换成本帧」）：
    拉杆和面板的对比度本来就低（静止杆区 帧0 亮度 43.3，帧20 同位置 48.8，
    55% 的像素差 < 15），阈值低了吃面板、高了在拉杆原位留一条残影，
    怎么调都能看见重影。**这条路在光度上不可分，直接放弃。**
  · **时序中位数 / 掩码迭代中位数当"无杆参考"**：
    AI 视频里拉杆在第 7~20 帧几乎是"原地变长"（球心 cx 恒定、cy 一直降），
    杆的竖直段覆盖同一批像素的时间超过一半 → 中位数取到的是杆不是面板，仍是残影。
  · **低频平场矫正**（大窗口中值滤波求低频比）：比全局配平还差一点，没必要。
  · 结论：**别指望把"拉杆"和"面板"在像素上分开——它们光度太接近。**
    要处理的是**整帧的亮度漂移**，不是某个区域。

修法（两步）
------------
1. **逐帧光度配平**：每帧按通道线性拟合「参考帧面板 ≈ a·本帧面板 + b」
   （在面板掩码上最小二乘），整帧按 (a,b) 拉回来。
   这一步就把主要症状干掉了：亮度极差 5.72 → 2.88，>6 的像素 39.5% → 5.3%。
2. **冻结拉杆活动区之外**：拉杆只在一个固定区域里动，那以外**每帧应该一模一样**。
   用「各帧与参考帧差异的最大值」求出活动区 U（模糊后阈值 + 形态学清理 + 外扩羽化），
   U 之外一律换成参考帧像素 —— 那 88% 的面板就是**逐像素恒定**（极差 0.00）。
   U 之内保持各帧原样（拉杆就在那儿，不能冻结）。

顺带：帧 0 配平到自己是恒等变换，所以**静止位（leverT=0）的画面一个像素都不变**。

用法
----
  python tools/eye_base_stabilize.py --dir <Resources/.../eye_console_layered> [--dry-run]
"""
import argparse
import json
import os

import cv2
import numpy as np

LEVER_COLS = (760, 945)   # 拟合光度模型时排除的列（拉杆所在列）
ACT_THRESH = 12.0         # 活动区判据（对已配平的帧做 3px 模糊后的差异最大值）
ACT_OPEN = 5              # 形态学开运算核（去零星噪点）
ACT_CLOSE = 11            # 闭运算核（补内部小洞）
ACT_DILATE = 9            # 外扩（给羽化留余量）
ACT_FEATHER = 3.0         # 羽化 sigma


def fit_photometric(stack, mask, ref_index=0):
    """每帧按通道拟合 ref ≈ a*x + b，返回配平后的 stack 和系数。"""
    ref = stack[ref_index, :, :, :3]
    out = stack.copy()
    coefs = []
    for k in range(stack.shape[0]):
        a = np.ones(3, np.float32)
        b = np.zeros(3, np.float32)
        for c in range(3):
            a[c], b[c] = np.polyfit(stack[k, :, :, c][mask], ref[:, :, c][mask], 1)
        out[k, :, :, :3] = np.clip(stack[k, :, :, :3] * a + b, 0, 255)
        coefs.append((a.copy(), b.copy()))
    return out, coefs


def activity_soft_mask(stack, ref_index=0):
    """拉杆活动区 U：各帧与参考帧差异的最大值 → 模糊 → 阈值 → 形态学 → 羽化。"""
    H, W = stack.shape[1:3]
    d_max = np.zeros((H, W), np.float32)
    for k in range(stack.shape[0]):
        if k == ref_index:
            continue
        d = np.abs(stack[k, :, :, :3] - stack[ref_index, :, :, :3]).max(2).astype(np.float32)
        d_max = np.maximum(d_max, cv2.GaussianBlur(d, (0, 0), 3.0))
    u = (d_max > ACT_THRESH).astype(np.uint8)
    u = cv2.morphologyEx(u, cv2.MORPH_OPEN,
                         cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (ACT_OPEN, ACT_OPEN)))
    u = cv2.morphologyEx(u, cv2.MORPH_CLOSE,
                         cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (ACT_CLOSE, ACT_CLOSE)))
    u = cv2.dilate(u, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (ACT_DILATE, ACT_DILATE)))
    return cv2.GaussianBlur(u.astype(np.float32), (0, 0), ACT_FEATHER), u


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--manifest", default="ec_base_manifest.json")
    ap.add_argument("--ref", type=int, default=0, help="参考帧（默认 0 = 静止位）")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    bm = json.load(open(os.path.join(args.dir, args.manifest), encoding="utf-8"))
    names = bm["frames"]
    stack = np.stack([cv2.imread(os.path.join(args.dir, n + ".png"),
                                 cv2.IMREAD_UNCHANGED) for n in names]).astype(np.float32)
    print("载入 %d 帧 %s" % (len(names), stack.shape[1:]))

    opaque = (stack[:, :, :, 3] >= 200).all(axis=0)
    fit_mask = opaque.copy()
    fit_mask[:, LEVER_COLS[0]:LEVER_COLS[1]] = False

    # ① 光度配平
    bal, coefs = fit_photometric(stack, fit_mask, args.ref)
    # ② 活动区掩码（在配平后的帧上求）
    soft, hard = activity_soft_mask(bal, args.ref)
    print("拉杆活动区：硬掩码 %d px (%.1f%%)，羽化后 >2%% 的 %d px"
          % (int(hard.sum()), 100 * hard.mean(), int((soft > 0.02).sum())))

    um = soft[:, :, None]
    ref = bal[args.ref]
    out = bal.copy()
    for k in range(stack.shape[0]):
        out[k, :, :, :3] = ref[:, :, :3] * (1 - um) + bal[k, :, :, :3] * um

    # ── 报告 ──
    def stat(A):
        L = A[:, :, :, :3].mean(3)
        r = L[:, opaque].max(0) - L[:, opaque].min(0)
        return (L[0][opaque].mean(), L[-1][opaque].mean(), r.mean(), 100 * (r > 6).mean())

    print("%-22s %10s %10s %12s %10s" % ("", "帧0 亮度", "帧末亮度", "极差均值", ">6 占比"))
    for tag, A in (("原版", stack), ("①配平后", bal), ("②配平+冻结", out)):
        m0, m1, rm, pc = stat(A)
        print("%-22s %10.2f %10.2f %12.2f %9.1f%%" % (tag, m0, m1, rm, pc))
    frz = opaque & (hard == 0)
    L = out[:, :, :, :3].mean(3)
    rf = L[:, frz].max(0) - L[:, frz].min(0)
    print("冻结区（活动区外 %d px）逐像素亮度极差：均值 %.4f  最大 %d"
          % (int(frz.sum()), rf.mean(), int(rf.max())))

    if args.dry_run:
        print("[dry-run] 未写盘")
        return
    for n, img in zip(names, out):
        cv2.imwrite(os.path.join(args.dir, n + ".png"),
                    np.clip(np.round(img), 0, 255).astype(np.uint8))
    print("[done] 已写回", args.dir)


if __name__ == "__main__":
    main()
