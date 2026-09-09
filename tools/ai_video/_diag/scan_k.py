# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs = sorted(glob.glob(os.path.join('raw','old_walk_v1','walk_*.png')))
A = np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
n=len(A)
u=A.max(axis=0); rs=np.where((u>0.5).any(axis=1))[0]; top,bot=rs[0],rs[-1]; H=bot-top
legsl=slice(top+int(H*0.70),bot+1)
def iou(a,b):
    x=a>0.5;y=b>0.5;return (x&y).sum()/max((x|y).sum(),1)

def resample(masks,K):
    m=len(masks)
    steps=[float(np.abs(masks[i]-masks[i+1]).sum()) for i in range(m-1)]
    steps.append(float(np.abs(masks[-1]-masks[0]).sum()))
    cum=np.concatenate([[0.0],np.cumsum(steps)]); total=cum[-1]
    if total<=0: return list(range(min(K,m)))
    picked=[]
    for t in np.linspace(0.0,total,K+1)[:-1]:
        j=min(int(np.searchsorted(cum,t,side='right'))-1,m-1); j=max(j,0)
        seg=cum[j+1]-cum[j]; frac=(t-cum[j])/seg if seg>0 else 0.0
        picked.append(j if frac<0.5 else (j+1)%m)
    return picked

print('%-20s %-3s %-4s %-7s %-7s %-6s %-6s' % ('window','K','dup','legIoU','fulIoU','cv','worst'))
best=[]
for (s,L) in [(14,22),(13,23),(11,23),(10,22),(14,21),(14,23),(13,22)]:
    W=A[s:s+L]
    for K in range(10,23):
        p=resample(W,K)
        dup=sum(1 for i in range(K) if p[i]==p[(i+1)%K])
        F=[W[i] for i in p]
        st=np.array([np.abs(F[i]-F[(i+1)%K]).sum() for i in range(K)])
        m=np.median(st); cv=st.std()/st.mean(); worst=st.max()/m; stillest=st.min()/m
        li=iou(F[-1],F[0]); fi=iou(F[-1],F[0])
        li=iou(F[-1][legsl],F[0][legsl])
        best.append((dup, -li, cv, s,L,K,fi,worst,stillest,p))
best.sort(key=lambda r:(r[0], r[2], r[1]))
for r in best[:18]:
    dup,nli,cv,s,L,K,fi,worst,still,p=r
    print('walk_%03d..%03d(L=%2d) %2d %3d  %.4f  %.4f  %.3f  %.2f  still %.2f' % (s,s+L-1,L,K,dup,-nli,fi,cv,worst,still))
