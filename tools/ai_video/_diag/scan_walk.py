# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image
fs = sorted(glob.glob(os.path.join('raw','old_walk_v1','walk_*.png')))
A = np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])
n=len(A)
u=A.max(axis=0); rs=np.where((u>0.5).any(axis=1))[0]; top,bot=rs[0],rs[-1]; H=bot-top
legsl = slice(top+int(H*0.70), bot+1)
def iou(a,b):
    x=a>0.5;y=b>0.5;return (x&y).sum()/max((x|y).sum(),1)
def leg_iou(i,j): return iou(A[i][legsl],A[j][legsl])
def full_iou(i,j): return iou(A[i],A[j])

# candidate: window [s, s+L-1], loop seam = last frame -> s. quality = similarity of A[s+L] region to A[s]
rows=[]
for L in range(16,29):
    for s in range(0, n-L):
        e = s+L-1
        seam_leg = leg_iou(e+1 if e+1<n else s, s)  # frame after window should look like start
        # true seam when played: e -> s
        play_leg = leg_iou(e, s)
        play_full = full_iou(e, s)
        # pacing of the window
        st=[np.abs(A[i]-A[i+1]).sum() for i in range(s,e)]
        st.append(np.abs(A[e]-A[s]).sum())
        st=np.array(st); m=np.median(st)
        cv = st.std()/st.mean()
        worst = st.max()/m
        rows.append((play_leg, play_full, cv, worst, L, s, e))
rows.sort(reverse=True)
print('%-6s %-6s %-6s %-6s %-4s %s' % ('legIoU','fulIoU','cv','worst','L','window'))
for r in rows[:20]:
    print('%.4f %.4f %.3f  %.2f  %2d  walk_%03d..%03d' % (r[0],r[1],r[2],r[3],r[4],r[5],r[6]))
print()
print('--- current window walk_013..043 (0-idx 13, L=31) for reference ---')
s,e=13,43
print('legIoU %.4f fullIoU %.4f' % (leg_iou(e,s), full_iou(e,s)))
