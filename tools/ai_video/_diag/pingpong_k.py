# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs=sorted(glob.glob(os.path.join('raw','old_idle_v1','idle_*.png')))
A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
n=len(A)
def resample_idx(seq,K):
    m=len(seq)
    steps=[float(np.abs(A[seq[i]]-A[seq[i+1]]).sum()) for i in range(m-1)]
    steps.append(float(np.abs(A[seq[-1]]-A[seq[0]]).sum()))
    cum=np.concatenate([[0.0],np.cumsum(steps)]); total=cum[-1]
    picked=[]
    for t in np.linspace(0.0,total,K+1)[:-1]:
        j=min(int(np.searchsorted(cum,t,side='right'))-1,m-1); j=max(j,0)
        seg=cum[j+1]-cum[j]; frac=(t-cum[j])/seg if seg>0 else 0.0
        picked.append(seq[j] if frac<0.5 else seq[(j+1)%m])
    return picked
def ev(idx):
    K=len(idx)
    st=np.array([float(np.abs(A[idx[i]]-A[idx[(i+1)%K]]).sum()) for i in range(K)])
    m=np.median(st)
    dup=sum(1 for i in range(K) if idx[i]==idx[(i+1)%K])
    return dup, st.std()/st.mean(), st.max()/m, st.min()/m
print('%-18s %-4s %-4s %-6s %-6s %-6s'%('window(乒乓)','K','重复','cv','最猛','最静'))
best=[]
for (s,e) in [(10,69),(15,69),(3,69),(0,69)]:
    seq=list(range(s,e+1))+list(range(e-1,s,-1))
    for K in [40,44,48,52,56,60,64,70]:
        if K>len(seq): continue
        idx=resample_idx(seq,K)
        dup,cv,worst,still=ev(idx)
        best.append((dup,cv,s,e,K,worst,still,idx))
best.sort(key=lambda r:(r[0],r[1]))
for r in best[:14]:
    dup,cv,s,e,K,worst,still,idx=r
    print('idle_%03d..%03d      %3d  %3d  %.3f  %.2f  %.2f'%(s,e,K,dup,cv,worst,still))
