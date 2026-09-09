# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image

def alphas(d, pat, sub='final'):
    fs = sorted(glob.glob(os.path.join(sub, d, pat)))
    return fs, [np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs]

def body_rows(A):
    """returns top,bot rows of the union silhouette"""
    u = np.zeros_like(A[0]); 
    for a in A: u = np.maximum(u, a)
    rs = np.where((u>0.5).any(axis=1))[0]
    return rs[0], rs[-1]

def head_metrics(A):
    top,bot = body_rows(A)
    H = bot-top
    # head band = top 14% of body
    h0,h1 = top, top+int(H*0.14)
    out=[]
    for a in A:
        m = a[h0:h1+1] > 0.5
        area = m.sum()
        cols = np.where(m.any(axis=0))[0]
        w = (cols[-1]-cols[0]+1) if len(cols) else 0
        rows = np.where(m.any(axis=1))[0]
        # top row of head within full frame
        tr = (h0+rows[0]) if len(rows) else -1
        out.append((area, w, tr))
    return out, (h0,h1)

def leg_signal(A):
    """horizontal spread of the leg band (bottom 30% of body) -> gait signal"""
    top,bot = body_rows(A)
    H = bot-top
    l0 = top+int(H*0.70)
    sig=[]
    for a in A:
        m = a[l0:bot+1] > 0.5
        cols = np.where(m.any(axis=0))[0]
        sig.append(float(cols[-1]-cols[0]+1) if len(cols) else 0.0)
    return np.array(sig), (l0,bot)

def autocorr_period(sig):
    s = sig - sig.mean()
    n = len(s)
    best=[]
    for lag in range(3, n//2+1):
        a=s[:n-lag]; b=s[lag:]
        d=(np.linalg.norm(a)*np.linalg.norm(b))
        if d<=0: continue
        best.append((float(np.dot(a,b)/d), lag))
    best.sort(reverse=True)
    return best[:6]

print('#'*72)
print('## SOURCE old_walk_v1  gait period')
fs,A = alphas('old_walk_v1','walk_*.png', sub='raw')
print('source frames:', len(A))
sig,(l0,bot) = leg_signal(A)
print('leg band rows %d-%d' % (l0,bot))
print('leg spread per frame:')
for i in range(0,len(sig),14):
    print('  %2d: %s' % (i, ' '.join('%4.0f'%v for v in sig[i:i+14])))
print('autocorr top lags (corr, lag):')
for c,l in autocorr_period(sig): print('   lag %2d  corr %+.3f' % (l,c))
