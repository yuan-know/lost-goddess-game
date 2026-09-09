# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs=sorted(glob.glob(os.path.join('raw','old_idle_v1','idle_*.png')))
A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
n=len(A)
HW=[];HT=[]
for a in A:
    m=a>0.5
    r=np.where(m.any(axis=1))[0]; t=int(r[0]); HT.append(t)
    band=m[t:t+191]; w=0
    for rr in range(band.shape[0]):
        cc=np.where(band[rr])[0]
        if len(cc): w=max(w,int(cc[-1]-cc[0]+1))
    HW.append(w)
HW=np.array(HW,float);HT=np.array(HT,float)
def absdiff(i,j): return float(np.abs(A[i]-A[j]).sum())
allst=[absdiff(i,i+1) for i in range(n-1)]
base=np.median(allst)
rows=[]
for L in range(34,71):
    for s in range(0,n-L+1):
        e=s+L-1
        rows.append((absdiff(e,s)/base, abs(HW[e]-HW[s]), L,s,e, HT[s:e+1].max()-HT[s:e+1].min()))
print('全部窗口的接缝跳变分布: min %.2fx  p10 %.2fx  中位 %.2fx  max %.2fx'%(
    min(r[0] for r in rows), np.percentile([r[0] for r in rows],10),
    np.median([r[0] for r in rows]), max(r[0] for r in rows)))
print()
# combined score: normalize both, weight seam more
cand=[r for r in rows if r[5]>=12 and r[2]>=38]
sj=np.array([r[0] for r in cand]); dh=np.array([r[1] for r in cand])
score = sj/sj.max()*1.0 + dh/max(dh.max(),1)*0.7
order=np.argsort(score)
print('%-20s %-3s %-9s %-7s %-6s'%('window','L','接缝/base','头宽差','呼吸'))
seen=set(); c=0
for idx in order:
    r=cand[idx]; s,e,L=r[3],r[4],r[2]
    k=(s//4,L//5)
    if k in seen: continue
    seen.add(k)
    print('idle_%03d..%03d      %2d  %6.2fx   %5.1f   %5.1f'%(s,e,L,r[0],r[1],r[5]))
    c+=1
    if c>=14: break
