# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs=sorted(glob.glob(os.path.join('raw','old_idle_v1','idle_*.png')))
A=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
n=len(A)
print('SOURCE old_idle_v1 per-frame bbox (n=%d)'%n)
H=[];W=[];T=[];B=[];SH=[]
for a in A:
    m=a>0.5
    r=np.where(m.any(axis=1))[0]; c=np.where(m.any(axis=0))[0]
    t,b=int(r[0]),int(r[-1]); l,rr=int(c[0]),int(c[-1])
    T.append(t);B.append(b);H.append(b-t+1);W.append(rr-l+1)
    # shoulder width at 22% down the body
    row=t+int((b-t)*0.22)
    cc=np.where(m[row])[0]
    SH.append(int(cc[-1]-cc[0]+1) if len(cc) else 0)
H=np.array(H);W=np.array(W);T=np.array(T);B=np.array(B);SH=np.array(SH)
def tr(name,v):
    x=np.arange(len(v)); k=np.polyfit(x,v,1)[0]
    print(' %-12s med %6.1f  min %5d(f%02d)  max %5d(f%02d)  span %4d (%.2f%%)  线性趋势 %+0.3f px/帧 (全程 %+0.1f)'%(
        name,np.median(v),v.min(),v.argmin(),v.max(),v.argmax(),v.max()-v.min(),100.0*(v.max()-v.min())/np.median(v),k,k*len(v)))
tr('总高',H); tr('总宽',W); tr('顶行',T); tr('底行',B); tr('肩宽',SH)
print()
print(' 每10帧的 顶行/底行/总高/肩宽:')
for i in range(0,n,5):
    print('   f%02d  top %4d  bot %4d  H %4d  shoulder %3d'%(i,T[i],B[i],H[i],SH[i]))
