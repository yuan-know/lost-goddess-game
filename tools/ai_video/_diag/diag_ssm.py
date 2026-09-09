# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image

def alphas(d, pat, sub='raw'):
    fs = sorted(glob.glob(os.path.join(sub, d, pat)))
    return fs, np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])

fs, A = alphas('old_walk_v1','walk_*.png')
n = len(A)
u = A.max(axis=0); rs = np.where((u>0.5).any(axis=1))[0]; top,bot = rs[0],rs[-1]
H = bot-top
leg = A[:, top+int(H*0.70):bot+1, :]
legf = leg.reshape(n,-1)
# also align horizontally per-frame? no: walk is in place (global crop). keep absolute.
D = np.zeros((n,n))
for i in range(n):
    D[i] = np.abs(legf - legf[i]).sum(axis=1)
D /= D.max()

print('leg-region self-similarity: for each frame, its most-similar NON-adjacent frame')
for i in range(n):
    cand = [(D[i,j], j) for j in range(n) if min(abs(i-j), n-abs(i-j)) > 3]
    v,j = min(cand)
    print('  %2d -> best match %2d (dist %.4f)  lag %d' % (i,j,v, abs(i-j)))
