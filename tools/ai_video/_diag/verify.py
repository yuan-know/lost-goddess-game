# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
def load(d,pat):
    fs=sorted(glob.glob(os.path.join('final',d,pat)))
    return np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
def iou(a,b):
    x=a>0.5;y=b>0.5;return (x&y).sum()/max((x|y).sum(),1)
def stats(d,pat,legfrac=0.70):
    A=load(d,pat); n=len(A)
    u=A.max(axis=0); rs=np.where((u>0.5).any(axis=1))[0]; top,bot=rs[0],rs[-1]; H=bot-top
    legsl=slice(top+int(H*legfrac),bot+1)
    st=np.array([np.abs(A[i]-A[(i+1)%n]).sum() for i in range(n)])
    m=np.median(st)
    HW=[]
    for a in A:
        mm=a>0.5; r=np.where(mm.any(axis=1))[0]; t=int(r[0])
        band=mm[t:t+79]; w=0
        for rr in range(band.shape[0]):
            cc=np.where(band[rr])[0]
            if len(cc): w=max(w,int(cc[-1]-cc[0]+1))
        HW.append(w)
    HW=np.array(HW)
    dup=sum(1 for i in range(n) if st[i]<m*0.02)
    return dict(n=n,cv=st.std()/st.mean(),worst=st.max()/m,still=st.min()/m,
                seam=iou(A[-1],A[0]),legseam=iou(A[-1][legsl],A[0][legsl]),
                dHW=abs(int(HW[-1])-int(HW[0])),HWspan=int(HW.max()-HW.min()),dup=dup)
print('%-14s %3s %6s %6s %6s %7s %7s %5s %6s %3s'%('版本','帧','cv','最猛','最静','接缝','腿接缝','头宽差','头宽跨','重'))
for d,pat,lbl in [('old_walk_v3','walk_*.png','走 v3 旧'),('old_walk_v4','walk_*.png','走 v4 中值'),('old_walk_v5','walk_*.png','走 v5 无中值'),('old_walk_v6','walk_*.png','走 v6 宽判据'),('old_walk_v7','walk_*.png','走 v7 严判据'),
                  ('old_idle_v2','idle_*.png','待 v2 旧'),('old_idle_v3','idle_*.png','待 v3 废'),('old_idle_v4','idle_*.png','待 v4'),('old_idle_v5','idle_*.png','待 v5'),('old_idle_v6','idle_*.png','待 v6 新')]:
    s=stats(d,pat)
    print('%-14s %3d %6.3f %6.2f %6.2f %7.4f %7.4f %5d %6d %3d'%(
        lbl,s['n'],s['cv'],s['worst'],s['still'],s['seam'],s['legseam'],s['dHW'],s['HWspan'],s['dup']))
