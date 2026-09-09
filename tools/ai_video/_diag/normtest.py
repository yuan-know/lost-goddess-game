# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs=sorted(glob.glob(os.path.join('raw','old_idle_v1','idle_*.png')))
imgs=[Image.open(f).convert('RGBA') for f in fs]
A=np.stack([np.array(im)[:,:,3].astype(np.float64)/255.0 for im in imgs])
n=len(A)
def headw(al):
    m=al>0.5; r=np.where(m.any(axis=1))[0]; t=int(r[0])
    band=m[t:t+191]; w=0
    for rr in range(band.shape[0]):
        cc=np.where(band[rr])[0]
        if len(cc): w=max(w,int(cc[-1]-cc[0]+1))
    return w,t
hw=np.array([headw(a)[0] for a in A],float)
x=np.arange(n)
k,b=np.polyfit(x,hw,1)
print('头宽线性拟合: %.4f*i + %.1f   全程漂移 %+.1f px (%.2f%%)'%(k,b,k*n,100*k*n/np.median(hw)))
trend=k*x+b
resid=hw-trend
print('去掉线性趋势后残差: std %.2f px  范围 %.1f..%.1f'%(resid.std(),resid.min(),resid.max()))
print()
print('若按趋势反向缩放每帧(补偿系数 = trend[0]/trend[i]):')
comp=trend[0]/trend
print('  系数范围 %.5f .. %.5f  (最大缩放 %.2f%%)'%(comp.min(),comp.max(),100*(comp.max()-comp.min())))
print('  首尾补偿后头宽: %.1f vs %.1f'%(hw[0]*comp[0], hw[-1]*comp[-1]))
print()
print('身高同时也在变,一起看:')
H=[]
for a in A:
    m=a>0.5; r=np.where(m.any(axis=1))[0]; H.append(int(r[-1]-r[0]+1))
H=np.array(H,float)
kh,bh=np.polyfit(x,H,1)
print('  身高趋势 %+.3f px/帧 (全程 %+.1f, %.2f%%)'%(kh,kh*n,100*kh*n/np.median(H)))
print('  -> 头宽变大 %+.2f%% 而身高变小 %.2f%%,方向相反'%(100*k*n/np.median(hw),100*kh*n/np.median(H)))
print('  -> 不是镜头推拉(那会同向),是角色在转身/前倾。单一缩放系数补不了。')
