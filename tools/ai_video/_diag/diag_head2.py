# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image

def run(sub,d,pat,label,band):
    fs=sorted(glob.glob(os.path.join(sub,d,pat)))
    A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
    n=len(A)
    print('='*74); print('%s n=%d  (跟随头顶的相对band: 头顶下方 0..%d px)'%(label,n,band))
    AR=[];WD=[];HH=[]
    for a in A:
        m=a>0.5
        r=np.where(m.any(axis=1))[0]; t=int(r[0])
        sub_m=m[t:t+band+1]
        AR.append(int(sub_m.sum()))
        c=np.where(sub_m.any(axis=0))[0]
        WD.append(int(c[-1]-c[0]+1) if len(c) else 0)
        # max head width scan
        widths=[]
        for rr in range(sub_m.shape[0]):
            cc=np.where(sub_m[rr])[0]
            widths.append(int(cc[-1]-cc[0]+1) if len(cc) else 0)
        HH.append(max(widths))
    AR=np.array(AR);WD=np.array(WD);HH=np.array(HH)
    for nm,v in [('头区面积',AR),('头区包围宽',WD),('头最大宽',HH)]:
        x=np.arange(n); k=np.polyfit(x,v,1)[0]
        print(' %-10s med %7.1f  min %6d(f%02d)  max %6d(f%02d)  span %.2f%%  趋势 %+0.4f/帧'%(
            nm,np.median(v),v.min(),v.argmin(),v.max(),v.argmax(),100.0*(v.max()-v.min())/np.median(v),k))
    print(' 首尾: 面积 %d vs %d (%+.2f%%)   最大宽 %d vs %d (%+d px)'%(
        AR[0],AR[-1],100.0*(AR[-1]-AR[0])/AR[0],HH[0],HH[-1],HH[-1]-HH[0]))
    return AR,HH

run('raw','old_idle_v1','idle_*.png','SOURCE old_idle_v1',190)
run('final','old_idle_v2','idle_*.png','FINAL old_idle_v2',78)
