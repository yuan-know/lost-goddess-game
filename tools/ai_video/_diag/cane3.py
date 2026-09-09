# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
from scipy import ndimage
fs=sorted(glob.glob(os.path.join('raw','old_walk_v1','walk_*.png')))
picked=[0,2,4,7,8,11,13,16,18,19,20,21]
sel=[14+p for p in picked]
A=[np.array(Image.open(fs[i]).convert('RGBA'))[:,:,3].astype(np.float64)/255.0 for i in sel]
n=len(A); FEATHER=2.0
def soften(al):
    op=al>0.5; d=ndimage.distance_transform_edt(op)
    return np.where(op,np.clip(d/FEATHER,0.0,1.0),0.0)
S=[soften(a) for a in A]
M=[np.median(np.stack([S[(i+k)%n] for k in range(-1,2)]),axis=0) for i in range(n)]
i=10
d=(S[i]>0.5)&~(M[i]>0.5)
lb,nb=ndimage.label(d)
sz=ndimage.sum(d,lb,range(1,nb+1))
big=int(np.argmax(sz))+1
blob=(lb==big)
ys,xs=np.where(blob)
print('帧10 最大被删块: %d px'%blob.sum())
print('  行 %d-%d (高 %d)   列 %d-%d (宽 %d)'%(ys.min(),ys.max(),ys.max()-ys.min()+1,xs.min(),xs.max(),xs.max()-xs.min()+1))
print('  长宽比 %.1f  填充率 %.3f'%((ys.max()-ys.min()+1)/max(xs.max()-xs.min()+1,1),
      blob.sum()/((ys.max()-ys.min()+1)*(xs.max()-xs.min()+1))))
print()
# is this blob a THIN structure? measure via erosion survival
er=ndimage.binary_erosion(blob,iterations=2)
print('  腐蚀2次后剩 %d px (%.1f%%) -> %s'%(er.sum(),100.0*er.sum()/blob.sum(),
      '细长结构(拐杖/边缘)' if er.sum()<blob.sum()*0.3 else '实心块(身体部位)'))
print()
# compare: is the blob present in neighbours?
for k in [-1,1]:
    j=(i+k)%n
    ov=(blob&(S[j]>0.5)).sum()
    print('  该块在邻帧%2d 的重叠: %d px (%.1f%%) -> %s'%(j,ov,100.0*ov/blob.sum(),
          '邻帧也有,不该删' if ov>blob.sum()*0.5 else '邻帧没有'))
print()
print('结论判据: 中值滤波的规则是"3帧里至少2帧有,才保留"。')
print('拐杖细长+摆动快 => 同一位置只在1帧出现 => 被判为噪声抹掉。')
print()
# quantify cane region specifically: lower-left quadrant, thin structures
print('各帧在"拐杖区"(行700-1250, 列120-320)的面积变化:')
print('%-4s %-9s %-9s %-8s'%('帧','中值前','中值后','损失%'))
for i in range(n):
    reg=(slice(700,1250),slice(120,320))
    b=int((S[i][reg]>0.5).sum()); a2=int((M[i][reg]>0.5).sum())
    print(' %2d  %8d  %8d  %6.2f%%'%(i,b,a2,100.0*(b-a2)/max(b,1)))
