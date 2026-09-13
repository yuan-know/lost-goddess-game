# -*- coding: utf-8 -*-
"""
eye_console_layered_assets.py —— 从美术的 PSD + 按钮图导出「分层版」控制台素材

输入:
  · 黄金瞳.psd         —— 组里有 3 层：合成参考 / 空眼窝(无球) / 单独眼球
  · 三个按钮.png       —— 左旋钮 / 中旋钮 / 拉杆，三个独立块，3400x1200 定位

输出（全部 3400x1200 全画布透明 PNG，交给 eye_console_pack.py 再裁）:
  ec_eye_frame_back.png     眼窝后层（铜框 + 眼窝内壁，不含球）
  ec_eyeball.png            眼球（必须画在眼睑前层下面）
  ec_eye_frame_front.png    眼睑/眼眶前缘（压在眼球上面，遮住球边缘）
  ec_eye_mask.png           眼窝开口遮罩（给眼球平移裁剪用）
  ec_knob_left_shell.png    左旋钮外壳（挖掉顶面）
  ec_knob_left_top.png      左旋钮顶面（可单独旋转）
  ec_knob_mid_shell.png     中旋钮外壳
  ec_knob_mid_top.png       中旋钮顶面
  ec_lever_handle.png       拉杆（球头 + 杆）

为什么旋钮要拆成「外壳 + 顶面」:
  旋钮是 3/4 视角的圆柱。整张图做 2D 旋转的话，转到 ±135° 会看起来「躺倒」
  （圆柱的侧壁甩到一边）。正确做法是外壳不动、只让顶面绕自己的圆心转 ——
  顶面在 3D 里本来就是个圆，投影成椭圆，绕中心转是对的。

用法:
  python eye_console_layered_assets.py --psd <黄金瞳.psd> --buttons <三个按钮.png> \
      --out docs/eye_console/_layered_src
"""
import argparse
import os

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

CANVAS = (3400, 1200)

# ── 三个按钮在 3400x1200 里的位置（实测连通域） ────────────────────────
BTN_LEFT = (1109, 410, 1328, 647)      # 左旋钮
BTN_MID = (1602, 420, 1826, 670)       # 中旋钮
BTN_LEVER = (2150, 320, 2280, 690)     # 拉杆
PAD = 24

# 旋钮顶面椭圆的圆心与半轴（源图像素）。圆心 = 2D 旋转轴。
# 左旋钮的圆心是拿「旋转后轮廓 IoU 最高」+ 实机尺寸目视双重校准过的。
KNOB_TOP = {
    "left": (1218, 508, 82, 70),
    "mid":  (1714, 524, 84, 72),
}
TOP_FEATHER = 8

# PSD 空眼窝的「实际开口」：球应该出现在这条杏仁形孔里。
# 数值是在眼窝层网格图上量出来的：
#   左尖 (1521,309) 右尖 (1880,306) 上缘 y≈249 下缘 y≈361
#   → 中心 (1700,305)，半轴 (179.5,56)；在 x=1600 处半高 41 → power≈0.84
# 取略小一点点，让裁剪正好发生在内缘上（球被眼睑挡住，而不是在眼窝中间被切断）。
EYE_OPENING = (1700.0, 299.0, 150.0, 43.0, 0.90)  # cx,cy,rx,ry,power


def find_psd_layers(psd_path):
    """返回 (空眼窝层, 眼球层)。判据：眼球层的可见面积远小于眼窝层。"""
    from psd_tools import PSDImage
    psd = PSDImage.open(psd_path)
    out = []
    for grp in psd:
        for l in (grp if grp.is_group() else [grp]):
            if l.is_group():
                continue
            im = l.composite()
            if im is None:
                continue
            a = np.asarray(im.convert("RGBA"))
            n = int((a[..., 3] > 16).sum())
            if n > 0:
                out.append((l.name, im.convert("RGBA"), n))
    if len(out) < 2:
        raise SystemExit("PSD 里可见图层不足 2 个，检查结构")
    out.sort(key=lambda t: -t[2])
    socket_im = out[0][1]      # 面积最大 = 整只眼睛（含球）
    ball_im = out[-1][1]       # 面积最小 = 眼球
    # 面积最大的那张其实含球，真正的「空眼窝」是次大的那张
    if len(out) >= 3:
        socket_im = out[1][1]
    print("PSD 可见图层（按面积降序）:")
    for name, _, n in out:
        print("   %-14s %7d px" % (name, n))
    return socket_im, ball_im


def extract_blocks(btn_path, boxes):
    """把按钮图按连通域拆成独立块（每块输出全画布图）。"""
    im = Image.open(btn_path).convert("RGBA")
    a = np.asarray(im)
    lab, n = ndimage.label(a[..., 3] > 16, structure=np.ones((3, 3)))
    res = {}
    for key, (x0, y0, x1, y1) in boxes.items():
        sl = (slice(y0, y1 + 1), slice(x0, x1 + 1))
        ids = np.unique(lab[sl])
        ids = ids[ids > 0]
        if len(ids) == 0:
            raise SystemExit(f"按钮图里 {key} 区域没内容: {boxes[key]}")
        m = np.isin(lab, ids)
        out = np.zeros_like(a)
        out[m] = a[m]
        res[key] = Image.fromarray(out, "RGBA")
        print(f"  {key:10s} 取到 {int(m.sum()):7d} px  bbox={boxes[key]}")
    return res


def split_knob(im, cx, cy, rx, ry):
    """按椭圆把旋钮拆成 外壳 / 顶面 两张全画布图。"""
    w, h = im.size
    yy, xx = np.mgrid[0:h, 0:w]
    m = (((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2) <= 1.0
    mm = np.zeros((h, w), np.uint8)
    mm[m] = 255
    soft = np.asarray(Image.fromarray(mm, "L").filter(ImageFilter.GaussianBlur(TOP_FEATHER))).astype(np.float32) / 255.0
    a = np.asarray(im).astype(np.float32)
    top = a.copy();  top[..., 3] = a[..., 3] * soft
    shell = a.copy(); shell[..., 3] = a[..., 3] * (1.0 - soft)
    return (Image.fromarray(top.astype(np.uint8), "RGBA"),
            Image.fromarray(shell.astype(np.uint8), "RGBA"))


def clamp_green(im):
    """把绿色通道夹到 max(R,B)。

    素材边缘/内部有绿色残留（G 同时高于 R 和 B）。铜色本身 R>G>B，
    永远不会触发这个判据，所以这个操作只动真正的绿色像素，不会改坏画面。
    """
    a = np.asarray(im).astype(np.float32)
    g = a[..., 1]
    rb = np.maximum(a[..., 0], a[..., 2])
    hit = int((g > rb).sum())
    if hit:
        a[..., 1] = np.minimum(g, rb)
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--psd", required=True)
    ap.add_argument("--buttons", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    print("== 读 PSD ==")
    socket_im, ball_im = find_psd_layers(args.psd)

    print("\n== 拆按钮 ==")
    blocks = extract_blocks(args.buttons,
                            {"knob_left": BTN_LEFT, "knob_mid": BTN_MID, "lever": BTN_LEVER})

    print("\n== 旋钮拆外壳/顶面 ==")
    out = {}
    for key in ("left", "mid"):
        cx, cy, rx, ry = KNOB_TOP[key]
        top, shell = split_knob(blocks["knob_" + key], cx, cy, rx, ry)
        out[f"knob_{key}_top"] = top
        out[f"knob_{key}_shell"] = shell
        print(f"  旋钮{key}: 轴心=({cx},{cy}) 顶面半轴=({rx},{ry})")

    # ── 眼窝开口遮罩 + 眼眶前缘 ────────────────────────────────────────
    # layer1 是「空眼窝」：它既包含后方暗内壁，也包含应该压在球上面的
    # 上/下眼睑与外框。必须拆成：
    #   frame_back  = 原空眼窝全层（球放在它上面）
    #   frame_front = 原空眼窝在开口外的部分（球放在它下面）
    # 这样球的层级才是：后内壁 → 球 → 眼睑/前缘。
    print("\n== 生成眼窝开口遮罩 + 眼眶前缘 ==")
    cx, cy, rx, ry, power = EYE_OPENING
    yy, xx = np.mgrid[0:CANVAS[1], 0:CANVAS[0]]
    dx = np.abs(xx - cx)
    t = np.clip(1.0 - (dx / rx) ** 2, 0.0, 1.0)
    opening = (dx <= rx) & (np.abs(yy - cy) <= ry * (t ** power))
    soft = np.asarray(Image.fromarray((opening.astype(np.uint8) * 255), "L")
                      .filter(ImageFilter.GaussianBlur(2))).astype(np.float32) / 255.0
    ma = np.zeros((CANVAS[1], CANVAS[0], 4), np.uint8)
    ma[..., :3] = 255
    ma[..., 3] = np.clip(soft * 255, 0, 255).astype(np.uint8)
    out["eye_mask"] = Image.fromarray(ma, "RGBA")

    socket_a = np.asarray(socket_im).astype(np.float32)
    front_a = socket_a.copy()
    front_a[..., 3] = socket_a[..., 3] * (1.0 - soft)
    out["eye_frame_front"] = Image.fromarray(front_a.astype(np.uint8), "RGBA")
    print(f"  开口杏仁: 中心({cx:.1f},{cy:.1f}) 半轴({rx:.1f},{ry:.1f}) "
          f"面积 {int(opening.sum())} px；前缘 alpha 仅保留开口外")

    # ── 落盘 ────────────────────────────────────────────────────────
    files = {
        "ec_eye_frame_back": socket_im,
        "ec_eyeball": ball_im,
        "ec_eye_frame_front": out["eye_frame_front"],
        "ec_eye_mask": out["eye_mask"],
        "ec_knob_left_shell": out["knob_left_shell"],
        "ec_knob_left_top": out["knob_left_top"],
        "ec_knob_mid_shell": out["knob_mid_shell"],
        "ec_knob_mid_top": out["knob_mid_top"],
        "ec_lever_handle": blocks["lever"],
    }
    print("\n== 落盘 ==")
    for name, im in files.items():
        p = os.path.join(args.out, name + ".png")
        im = clamp_green(im)
        im.save(p)
        aa = np.asarray(im)[..., 3] > 16
        print(f"  {name:22s} {os.path.getsize(p):>8} B  可见 {int(aa.sum()):7d} px")

    print(f"\n[out] {args.out}")


if __name__ == "__main__":
    main()
