# -*- coding: utf-8 -*-
"""扫描 old.png:找是否有"通顶竖线"——某一列从图顶到图底几乎都有不透明像素。
   人物只占中下部,若某列从最顶行就开始有像素,就是异常竖线。"""
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from PIL import Image
import numpy as np

p = r"C:\Users\yuan\lost-goddess-game\Assets\_Project\Resources\Characters\old.png"
a = np.array(Image.open(p).convert("RGBA"))
alpha = a[..., 3]
H, W = alpha.shape
print(f"图尺寸 {W}x{H}")

# 每列不透明像素(alpha>16)的数量 与 最高(最小行号)出现位置
opaque = alpha > 16
col_count = opaque.sum(axis=0)
# 找最顶部有像素的行(人物头顶)
rows_any = np.where(opaque.any(axis=1))[0]
print(f"人物最顶行={rows_any.min()} 最底行={rows_any.max()} (顶部pad应约8)")

# 找"从很靠顶部就开始有像素"的列——正常只有头部那几列靠顶
top_pixel_row = np.array([np.argmax(opaque[:, x]) if opaque[:, x].any() else H for x in range(W)])
# 统计:哪些列的最顶像素在头顶附近(异常竖线会让很多列都通到顶)
head_top = rows_any.min()
suspicious = np.where((top_pixel_row <= head_top + 30) & (col_count > H*0.7))[0]
print(f"疑似通顶竖线的列(靠顶且纵向覆盖>70%): {suspicious.tolist()[:50]}")
print(f"各列不透明数量: min={col_count.min()} max={col_count.max()} 中位={int(np.median(col_count))}")
# 打印覆盖率最高的10列
top10 = np.argsort(col_count)[-10:]
for x in sorted(top10):
    print(f"  列x={x}: 不透明={col_count[x]}/{H} ({col_count[x]/H*100:.0f}%) 最顶像素行={top_pixel_row[x]}")
