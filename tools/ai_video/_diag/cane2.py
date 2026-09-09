# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
from scipy import ndimage
fs=sorted(glob.glob(os.path.join('raw','old_walk_v1','walk_*.png')))
picked=[0,2,4,7,8,11,13,16,18,19,20,21]
sel=[14+p for p in picked]
A=[np.array(Image.open(fs[i]).convert('RGBA'))[:,:,3].astype(np.float64)/255.0 for i in sel]
n=len(A)
FEATHER=2.0
def soften(al):
    op=al>0.5
    d=ndimage.distance_transform_edt(op)
    return np.where(op,np.clip(d/FEATHER,0.0,1.0),0.0)
S=[soften(a) for a in A]
h=1
M=[np.median(np.stack([S[(i+k)%n] for k in range(-h,h+1)]),axis=0) for i in range(n)]
print('时间中值前后的前景面积对比 (羽化后 alpha>0.5 计数):')
print('%-4s %-9s %-9s %-8s %-7s'%('帧','中值前','中值后','被删掉','占比'))
tot_lost=0
for i in range(n):
    b=int((S[i]>0.5).sum()); a2=int((M[i]>0.5).sum())
    lost=b-a2; tot_lost+=max(lost,0)
    print(' %2d  %8d  %8d  %+7d  %6.2f%%'%(i,b,a2,-lost,100.0*lost/max(b,1)))
print()
print('总共被中值删掉 %d px'%tot_lost)
print()
# where is the loss? spatially
print('被删区域的位置分布 (取损失最大的3帧,看删掉的像素落在画面哪里):')
losses=[(int((S[i]>0.5).sum())-int((M[i]>0.5).sum()),i) for i in range(n)]
losses.sort(reverse=True)
for lost,i in losses[:3]:
    d=(S[i]>0.5)&~(M[i]>0.5)
    if not d.any(): continue
    ys,xs=np.where(d)
    lb,nb=ndimage.label(d)
    sz=ndimage.sum(d,lb,range(1,nb+1))
    print('  帧%2d 删了%5d px  行%4d-%4d 列%4d-%4d  分成%d块 最大块%d'%(
        i,lost,ys.min(),ys.max(),xs.min(),xs.max(),nb,int(sz.max()) if nb else 0))
    # is it in the lower-right (cane region)?
    H,W=d.shape
    print('       画面 %dx%d;删除区域重心 行%.0f 列%.0f'%(H,W,ys.mean(),xs.mean()))
