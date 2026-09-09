# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs=sorted(glob.glob(os.path.join('raw','old_idle_v1','idle_*.png')))
A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
n=len(A)
def absdiff(i,j): return float(np.abs(A[i]-A[j]).sum())
base=np.median([absdiff(i,i+1) for i in range(n-1)])
HW=[]
for a in A:
    m=a>0.5; r=np.where(m.any(axis=1))[0]; t=int(r[0])
    band=m[t:t+191]; w=0
    for rr in range(band.shape[0]):
        cc=np.where(band[rr])[0]
        if len(cc): w=max(w,int(cc[-1]-cc[0]+1))
    HW.append(w)
HW=np.array(HW)
print('乒乓方案评估 (正放 s..e 再倒放 e-1..s+1)')
print('%-18s %-4s %-9s %-8s %-7s %-6s'%('window','总帧','接缝跳变','最猛/中位','cv','呼吸'))
for (s,e) in [(0,69),(3,69),(19,69),(20,63),(15,69),(10,69)]:
    seq=list(range(s,e+1))+list(range(e-1,s,-1))
    N=len(seq)
    st=np.array([absdiff(seq[i],seq[(i+1)%N]) for i in range(N)])
    m=np.median(st)
    print('idle_%03d..%03d      %3d  %6.2fx   %6.2fx  %.3f  %4d'%(
        s,e,N,st[-1]/base,st.max()/m,st.std()/st.mean(),HW[s:e+1].max()-HW[s:e+1].min()))
print()
print('说明: 乒乓的接缝 = seq[-1]->seq[0] = 帧(s+1)->帧(s), 天然是一个正常相邻帧步长')
