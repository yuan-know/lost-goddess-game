# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
from scipy import ndimage
fs=sorted(glob.glob(os.path.join('raw','old_walk_v1','walk_*.png')))
# the 12 picked source frames within window 14..36
picked=[0,2,4,7,8,11,13,16,18,19,20,21]
sel=[14+p for p in picked]
print('检查源素材里被选中的12帧的连通块结构 (原始 alpha,未经任何处理):')
print('%-6s %-4s %s'%('源帧','块数','各块面积(降序,前6)'))
for i in sel:
    a=np.array(Image.open(fs[i]).convert('RGBA'))[:,:,3].astype(np.float64)/255.0
    op=a>0.5
    lb,nb=ndimage.label(op)
    if nb==0: print('  %3d   0   (空)'%i); continue
    sz=ndimage.sum(op,lb,range(1,nb+1))
    order=np.argsort(sz)[::-1]
    print('  %3d  %3d   %s'%(i,nb,' '.join('%d'%sz[o] for o in order[:6])))
