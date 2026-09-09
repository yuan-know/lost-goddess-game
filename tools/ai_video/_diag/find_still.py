# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs=sorted(glob.glob(os.path.join('final','old_idle_v4','idle_*.png')))
A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
n=len(A)
st=np.array([float(np.abs(A[i]-A[(i+1)%n]).sum()) for i in range(n)])
m=np.median(st)
print('n=%d 中位 %.0f'%(n,m))
print('比率最小的8对:')
for i in np.argsort(st)[:8]:
    print('  %2d->%2d  abs %8.1f  ratio %.3f'%(i,(i+1)%n,st[i],st[i]/m))
print()
print('逐帧比率:')
for i in range(0,n,13):
    print(' %3d: %s'%(i,' '.join('%5.2f'%(v/m) for v in st[i:i+13])))
# picked list from build log
picked=[0,2,4,7,11,14,16,19,20,21,23,26,28,31,35,39,43,48,51,55,56,57,59,60,63,65,69,73,75,78,79,81,82,83,87,90,95,99,103,107,110,112,115,117,118,119,122,124,127,131,134,136]
order=list(range(70))+list(range(68,0,-1))
srcf=[order[p] for p in picked]
print()
print('每个输出帧对应的源帧:')
for i in range(0,n,13):
    print(' %3d: %s'%(i,' '.join('%3d'%v for v in srcf[i:i+13])))
print()
print('最静那几对的源帧:')
for i in np.argsort(st)[:6]:
    print('  out %2d->%2d = src %3d->%3d  (源帧间隔 %d)'%(i,(i+1)%n,srcf[i],srcf[(i+1)%n],abs(srcf[(i+1)%n]-srcf[i])))
