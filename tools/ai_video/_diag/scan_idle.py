# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs=sorted(glob.glob(os.path.join('raw','old_idle_v1','idle_*.png')))
A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
n=len(A)
HW=[];HT=[];TH=[]
for a in A:
    m=a>0.5
    r=np.where(m.any(axis=1))[0]; t=int(r[0]); b=int(r[-1])
    HT.append(t); TH.append(b-t+1)
    band=m[t:t+191]
    w=0
    for rr in range(band.shape[0]):
        cc=np.where(band[rr])[0]
        if len(cc): w=max(w,int(cc[-1]-cc[0]+1))
    HW.append(w)
HW=np.array(HW,float);HT=np.array(HT,float);TH=np.array(TH,float)
def iou(a,b):
    x=a>0.5;y=b>0.5;return (x&y).sum()/max((x|y).sum(),1)
rows=[]
for L in range(30,71):
    for s in range(0,n-L+1):
        e=s+L-1
        dhw=abs(HW[e]-HW[s]); dht=abs(HT[e]-HT[s]); dth=abs(TH[e]-TH[s])
        seam=iou(A[e],A[s])
        # breathing amplitude preserved? head-top span within window
        amp=HT[s:e+1].max()-HT[s:e+1].min()
        rows.append((dhw,dht,seam,L,s,e,amp,dth))
# rank: head-width closure first, then seam IoU, keep decent length + breathing amplitude
rows.sort(key=lambda r:(r[0], r[1], -r[2]))
print('%-20s %-4s %-6s %-6s %-7s %-6s'%('window','L','dHeadW','dTop','seamIoU','呼吸幅度'))
seen=set()
cnt=0
for r in rows:
    dhw,dht,seam,L,s,e,amp,dth=r
    if amp < 12: continue          # need breathing motion preserved
    if L < 40: continue
    k=(s//3,L//3)
    if k in seen: continue
    seen.add(k)
    print('idle_%03d..%03d      %2d  %5.1f  %5.1f  %.4f  %5.1f'%(s,e,L,dhw,dht,seam,amp))
    cnt+=1
    if cnt>=15: break
print()
print('--- 当前(全部70帧) ---')
print('dHeadW %.1f  dTop %.1f  seamIoU %.4f  呼吸幅度 %.1f'%(abs(HW[69]-HW[0]),abs(HT[69]-HT[0]),iou(A[69],A[0]),HT.max()-HT.min()))
