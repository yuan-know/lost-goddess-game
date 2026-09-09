# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
from scipy import ndimage
def caneland(d):
    fs=sorted(glob.glob(os.path.join('final',d,'walk_*.png')))
    A=[np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float64)/255.0 for f in fs]
    n=len(A)
    # find cane region in OUTPUT coords: thin vertical structures in lower half
    res=[]
    for a in A:
        op=a>0.5
        # erode heavily -> body core; cane is what disappears
        core=ndimage.binary_erosion(op,iterations=6)
        thin=op&~ndimage.binary_dilation(core,iterations=6)
        # restrict to lower 55% and check for elongated blobs
        H=op.shape[0]
        low=np.zeros_like(op); low[int(H*0.42):,:]=True
        cand=thin&low
        lb,nb=ndimage.label(cand)
        best=0
        if nb:
            for k in range(1,nb+1):
                m=lb==k
                ys,xs=np.where(m)
                hh=ys.max()-ys.min()+1; ww=xs.max()-xs.min()+1
                if hh>=60 and hh/max(ww,1)>=3.0:
                    best=max(best,int(m.sum()))
        res.append(best)
    return n,res
for d,lbl in [('old_walk_v4','v4 有中值'),('old_walk_v5','v5 无中值'),('old_walk_v6','v6 宽'),('old_walk_v7','v7 严')]:
    n,res=caneland(d)
    arr=np.array(res)
    print('%-12s n=%d  细长竖结构(拐杖)面积: %s'%(lbl,n,' '.join('%4d'%v for v in res)))
    nz=(arr>0).sum()
    print('             检出帧 %d/%d   中位 %.0f  min %d  max %d'%(nz,n,np.median(arr),arr.min(),arr.max()))
