# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
for d in ['old_idle_v5']:
    fs=sorted(glob.glob(os.path.join('final',d,'idle_*.png')))
    A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
    n=len(A)
    st=np.array([float(np.abs(A[i]-A[(i+1)%n]).sum()) for i in range(n)])
    m=np.median(st)
    worst=list(np.argsort(st)[:6])
    print(d,'中位 %.0f'%m)
    for i in worst:
        j=(i+1)%n
        diff=np.abs(A[i]-A[j])
        npx=(diff>0.01).sum()
        print('  out %2d->%2d ratio %.3f  abs %.1f  有差异像素 %d  最大差 %.3f'%(i,j,st[i]/m,st[i],npx,diff.max()))
# check RAW at those source frames
srcA=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0
               for f in sorted(glob.glob(os.path.join('raw','old_idle_v1','idle_*.png')))])
picked=[0,2,4,7,11,14,16,19,20,21,23,26,28,31,35,39,43,47,51,55,56,57,59,60,63,65,69,72,74,77,78,80,81,82,85,89,94,98,102,106,109,111,114,116,117,118,121,123,126,129,133,135]
n0=70; skip=2
order=list(range(n0))+list(range(n0-1-skip, skip-1, -1))
srcf=[order[p] for p in picked]
print()
print('源帧序列(前后各13):')
print(' 头:',srcf[:13]); print(' 尾:',srcf[-13:])
print()
print('最静那几对在**源素材**里的距离:')
for i in [25,26,0,51,50,1]:
    a,b=srcf[i],srcf[(i+1)%len(srcf)]
    d=float(np.abs(srcA[a]-srcA[b]).sum())
    print('  out %2d->%2d = src %2d->%2d   源距离 %8.0f'%(i,(i+1)%len(srcf),a,b,d))
sm=np.median([float(np.abs(srcA[i]-srcA[i+1]).sum()) for i in range(n0-1)])
print(' 源相邻中位 %.0f'%sm)
