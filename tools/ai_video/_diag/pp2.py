# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs=sorted(glob.glob(os.path.join('raw','old_idle_v1','idle_*.png')))
A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
n=len(A)
def ad(i,j): return float(np.abs(A[i]-A[j]).sum())
def resample(seq,K):
    m=len(seq)
    steps=[ad(seq[i],seq[i+1]) for i in range(m-1)]; steps.append(ad(seq[-1],seq[0]))
    cum=np.concatenate([[0.0],np.cumsum(steps)]); total=cum[-1]
    out=[]
    for t in np.linspace(0.0,total,K+1)[:-1]:
        j=min(int(np.searchsorted(cum,t,side='right'))-1,m-1); j=max(j,0)
        seg=cum[j+1]-cum[j]; frac=(t-cum[j])/seg if seg>0 else 0.0
        out.append(seq[j] if frac<0.5 else seq[(j+1)%m])
    return out
def ev(idx):
    K=len(idx)
    st=np.array([ad(idx[i],idx[(i+1)%K]) for i in range(K)])
    m=np.median(st)
    dup=sum(1 for i in range(K) if idx[i]==idx[(i+1)%K])
    return dup,st.std()/st.mean(),st.max()/m,st.min()/m
print('折返跳帧 SKIP = 折返处两侧各跳过多少源帧')
print('%-6s %-4s %-4s %-6s %-6s %-7s'%('SKIP','K','重复','cv','最猛','最静'))
res=[]
for skip in [1,2,3,4]:
    for K in [40,44,48,52,56]:
        # forward 0..n-1, then backward n-1-skip .. skip
        seq=list(range(n))+list(range(n-1-skip, skip-1, -1))
        idx=resample(seq,K)
        dup,cv,worst,still=ev(idx)
        res.append((still,dup,cv,worst,skip,K))
        print('%-6d %-4d %-4d %.3f  %.2f   %.3f'%(skip,K,dup,cv,worst,still))
print()
best=[r for r in res if r[1]==0]
best.sort(key=lambda r:(-min(r[0],0.45), r[2]))
print('推荐(最静>=0.45优先,再看cv):')
for r in best[:5]:
    print('  SKIP=%d K=%d  cv %.3f  最猛 %.2f  最静 %.3f'%(r[4],r[5],r[2],r[3],r[0]))
