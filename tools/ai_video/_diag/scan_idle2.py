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
def iou(i,j):
    x=A[i]>0.5;y=A[j]>0.5;return (x&y).sum()/max((x|y).sum(),1)
# baseline: median inter-frame abs change over whole source
allst=[absdiff(i,i+1) for i in range(n-1)]
base=np.median(allst)
print('源素材相邻帧变化量中位数(1188px高): %.0f'%base)
rows=[]
for L in range(34,71):
    for s in range(0,n-L+1):
        e=s+L-1
        seam_abs=absdiff(e,s)
        rows.append((seam_abs/base, abs(HW[e]-HW[s]), iou(e,s), L,s,e, HT[s:e+1].max()-HT[s:e+1].min()))
# primary: seam jump must be near base (<=1.6x). secondary: head width closure.
ok=[r for r in rows if r[0]<=1.8 and r[6]>=12]
ok.sort(key=lambda r:(r[1], r[0]))
print()
print('%-20s %-3s %-8s %-7s %-8s %-6s'%('window','L','接缝/base','头宽差','seamIoU','呼吸'))
seen=set(); c=0
for r in ok:
    sj,dhw,io,L,s,e,amp=r
    k=(s//4,L//4)
    if k in seen: continue
    seen.add(k)
    print('idle_%03d..%03d      %2d  %6.2fx  %5.1f   %.4f  %5.1f'%(s,e,L,sj,dhw,io,amp))
    c+=1
    if c>=16: break
print()
for lbl,s,e in [('当前v2 全70',0,69),('v3 已废',20,63)]:
    print('%s: 接缝 %.2fx base, 头宽差 %.0f, IoU %.4f'%(lbl,absdiff(e,s)/base,abs(HW[e]-HW[s]),iou(e,s)))
