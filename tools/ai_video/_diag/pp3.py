# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
srcA=np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float64)/255.0
               for f in sorted(glob.glob(os.path.join('raw','old_idle_v1','idle_*.png')))])
n=len(srcA)
# per-frame head-top row in SOURCE (this is what gets crushed by downscale)
tops=[]
for a in srcA:
    m=a>0.5; r=np.where(m.any(axis=1))[0]; tops.append(int(r[0]))
tops=np.array(tops)
print('源头顶行逐帧(每10个):')
for i in range(0,n,10):
    print('  %2d: %s'%(i,' '.join('%4d'%v for v in tops[i:i+10])))
print()
# vertical speed = |d tops|. turnaround must be where speed is HIGH
spd=np.abs(np.diff(tops,append=tops[-1]))
print('头顶行速度(px/帧):')
for i in range(0,n,10):
    print('  %2d: %s'%(i,' '.join('%4d'%v for v in spd[i:i+10])))
print()
print('速度最高的帧(适合做折返点):', [int(i) for i in np.argsort(spd)[-12:][::-1]])
print('速度为0的帧(绝对不能做折返点):', [int(i) for i in np.where(spd==0)[0]])
print()
print('注: 折返点=窗口两端。两端速度都要 >=2 px/帧,缩到 0.4093 后才有 ~1px 位移')
cands=[]
for s in range(0,n-30):
    for e in range(s+30,n):
        if spd[s]>=2 and spd[e]>=2:
            cands.append((min(spd[s],spd[e]), e-s+1, s, e, tops[s:e+1].max()-tops[s:e+1].min()))
cands.sort(key=lambda r:(-r[0],-r[4]))
print()
print('%-18s %-4s %-9s %-7s'%('window','L','两端最小速度','呼吸幅度'))
seen=set(); c=0
for r in cands:
    mn,L,s,e,amp=r
    k=(s//5,L//8)
    if k in seen: continue
    seen.add(k)
    print('idle_%03d..%03d      %2d   %4d      %4d'%(s,e,L,mn,amp))
    c+=1
    if c>=12: break
