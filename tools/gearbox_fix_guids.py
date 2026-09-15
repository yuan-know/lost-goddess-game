# -*- coding: utf-8 -*-
"""
gearbox_fix_guids.py —— 把 GearboxPuzzle 素材 meta 里的**非法 GUID** 换成合法 32 位十六进制

背景(2026-09-15):
  旧 tools/gearbox_export.py 的 GUIDS 字典里写成了
    gearbox_base: "b1a7e4c2d3f54a0198c6e2d1a4f70a01"
  这些串含 i/o/k/m/s/z/y/q/w… 等**非十六进制**字母。Unity 读到会判定
  "does not have a valid GUID" 并**整个忽略该素材**(表现为 Resources.Load 返回 null、
  UI Image 渲染成白色方块)。

本脚本:
  · 只改 .meta 的 guid 行,PNG 与其它字段(mipmap/PPU/pivot/alignment)一字不动;
  · 每张图分配**稳定**的新 GUID(硬编码在本文件里,重跑幂等);
  · 打印新旧对照,便于核对。

用法: python tools/gearbox_fix_guids.py
"""
import os
import re

D = r"C:/Users/yuan/lost-goddess-game/Assets/_Project/Resources/Closeups/GearboxPuzzle"

# 稳定新 GUID(random hex 定稿;重跑不会变)
NEW = {
    "gearbox_base.png":       "3a7c1e9d5b246f80a3c7e1d9b5f20436",
    "gearbox_gear_big.png":   "9f2b6d4a8c130e57b9f2d4a6c8013e57",
    "gearbox_gear_small.png": "5c8e3f7b1d902a46c8e3f7b1d902a46c",
    "gearbox_knob.png":       "b1a7e4c2d3f54a0198c6e2d1a4f70a08",   # 本来就是合法十六进制,保留
    "gearbox_shaft.png":      "7d4a9c2e6f130b58d4a9c2e6f130b58d",
    "gearbox_valve.png":      "e1f6a3c9d27b04581f6a3c9d27b04581",
    "scar_offset_line.png":   "b1a7e4c2d3f54a0198c6e2d1a4f70a07",   # 合法,保留
    "steam_sheet.png":        "b1a7e4c2d3f54a0198c6e2d1a4f70a09",   # 合法,保留
    "gearspin_sheet.png":     "c7e2a5b18d340f6a92e1c4b7d0a63f25",
}


def main():
    for key, g in NEW.items():
        assert re.fullmatch(r"[0-9a-f]{32}", g), "非法 GUID: %s = %s" % (key, g)

    for name, newg in NEW.items():
        meta = os.path.join(D, name + ".meta")
        if not os.path.exists(meta):
            print("skip (no meta):", name)
            continue
        with open(meta, encoding="utf-8") as f:
            txt = f.read()
        m = re.search(r"^guid: (\S+)$", txt, re.M)
        old = m.group(1) if m else "(none)"
        if old == newg:
            print("%-26s ok   %s" % (name, newg))
            continue
        txt = re.sub(r"^guid: \S+$", "guid: " + newg, txt, count=1, flags=re.M)
        with open(meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(txt)
        valid = "合法" if re.fullmatch(r"[0-9a-f]{32}", old) else "★非法"
        print("%-26s %s  %s -> %s" % (name, valid, old, newg))


if __name__ == "__main__":
    main()
