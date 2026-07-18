"""
将「未命名作品(4).psd」拆分为三份 PSB:young.psb / middle.psb / old.psb
每份三个形态图层组都在,但只有目标形态那一组的图层可见 —— Unity PSD Importer 默认
"Include Hidden Layers = false",于是每份 PSB 只会导入目标形态的部件 Sprite。
"""
import sys
import io
from pathlib import Path
from psd_tools import PSDImage

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

SRC = Path(r"C:\Users\yuan\Documents\xwechat_files\wxid_x3vxwi7harzc22_7b87\msg\file\2026-07\未命名作品(4).psd")
DST_DIR = Path(r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Art\Characters")
DST_DIR.mkdir(parents=True, exist_ok=True)

FORMS = {
    "青年": "young.psb",
    "中年": "middle.psb",
    "老年": "old.psb",
}
ALL_FORM_NAMES = set(FORMS.keys())


def set_visibility(psd: PSDImage, keep_form: str):
    """顶层组名 in {青年,中年,老年}:目标组连同其所有子图层全部可见,
       其它两组连同子图层全部隐藏。非形态组的顶层(如背景)保持原样。"""
    def show_all(layer, on: bool):
        layer.visible = on
        if layer.is_group():
            for c in layer:
                show_all(c, on)

    for l in psd:
        if not l.is_group():
            continue
        if l.name in ALL_FORM_NAMES:
            show_all(l, l.name == keep_form)


def main():
    for form_name, out_name in FORMS.items():
        print(f"\n=== 生成 {out_name} (只让「{form_name}」组可见) ===")
        psd = PSDImage.open(SRC)
        set_visibility(psd, form_name)
        out_path = DST_DIR / out_name
        psd.save(str(out_path))
        # 可选:核对结果
        chk = PSDImage.open(out_path)
        for l in chk:
            if l.is_group() and l.name in ALL_FORM_NAMES:
                print(f"  [{'V' if l.visible else '_'}] {l.name}")
        print(f"  已写出: {out_path}")


if __name__ == "__main__":
    main()
