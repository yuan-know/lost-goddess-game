# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs=sorted(glob.glob(os.path.join('final','old_idle_v5','idle_*.png')))
A=[np.array(Image.open(f).convert('RGBA')) for f in fs]
def topline(a):
    m=a[:,:,3]>127
    r=np.where(m.any(axis=1))[0]
    return int(r[0])
def cy(a):
    al=a[:,:,3].astype(np.float64)/255.0
    ys=np.arange(al.shape[0])[:,None]
    return float((al*ys).sum()/al.sum())
print('out帧  头顶行  alpha重心y')
for i in [24,25,26,27,28, 50,51,0,1,2]:
    print('  %2d    %4d    %7.3f'%(i,topline(A[i]),cy(A[i])))
print()
print('折返处头顶行是否真的不动: 25/26/27 =',topline(A[25]),topline(A[26]),topline(A[27]))
print('重心y  25/26/27 = %.3f %.3f %.3f'%(cy(A[25]),cy(A[26]),cy(A[27])))
print()
# how much does the whole sequence move?
tops=[topline(a) for a in A]; cys=[cy(a) for a in A]
print('全序列 头顶行 %d..%d (跨度%d)   重心y %.2f..%.2f (跨度%.2f)'%(
    min(tops),max(tops),max(tops)-min(tops),min(cys),max(cys),max(cys)-min(cys)))
