# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
for d in ['old_idle_v2','old_idle_v3']:
    fs=sorted(glob.glob(os.path.join('final',d,'idle_*.png')))
    A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
    n=len(A)
    st=np.array([np.abs(A[i]-A[(i+1)%n]).sum() for i in range(n)])
    m=np.median(st)
    print('='*70); print('%s n=%d  绝对变化量: 中位 %.0f  min %.0f  max %.0f'%(d,n,m,st.min(),st.max()))
    print(' 逐帧比率:')
    for i in range(0,n,10):
        print('  %3d: %s'%(i,' '.join('%6.2f'%(v/m) for v in st[i:i+10])))
    print(' 绝对值最大5个:')
    for i in np.argsort(st)[-5:][::-1]:
        print('   %2d->%2d  abs %.0f  ratio %.2f'%(i,(i+1)%n,st[i],st[i]/m))
