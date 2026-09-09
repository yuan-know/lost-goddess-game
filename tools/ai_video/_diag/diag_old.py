import numpy as np, glob, os, sys
from PIL import Image

def load(d, pat):
    fs = sorted(glob.glob(os.path.join('final', d, pat)))
    return fs, [np.array(Image.open(f))[:,:,3].astype(np.float32)/255.0 for f in fs]

def report(d, pat, name):
    fs, A = load(d, pat)
    n = len(A)
    print('='*70)
    print('%s : %d frames' % (name, n))
    # inter-frame change, circular
    steps = [np.abs(A[i]-A[(i+1)%n]).sum() for i in range(n)]
    med = np.median(steps)
    print('  变化量中位数 %.0f' % med)
    print('  逐帧比率(i->i+1, 最后一项是 %d->0 接缝):' % (n-1))
    for i in range(0, n, 10):
        chunk = steps[i:i+10]
        print('   %3d: %s' % (i, ' '.join('%5.2f'%(s/med) for s in chunk)))
    # seam IoU
    def iou(a,b):
        x=a>0.5; y=b>0.5
        return (x&y).sum()/max((x|y).sum(),1)
    print('  接缝 IoU (%d vs 0): %.4f' % (n-1, iou(A[-1],A[0])))
    print('  相邻 IoU 最低的几对:')
    ious = [(iou(A[i],A[(i+1)%n]), i) for i in range(n)]
    for v,i in sorted(ious)[:5]:
        print('    %2d->%2d  IoU %.4f  ratio %.2f' % (i,(i+1)%n,v,steps[i]/med))
    # duplicate detection
    dups = [(i, steps[i]) for i in range(n) if steps[i] < med*0.15]
    if dups: print('  疑似重复/几乎不动:', [(i, round(s/med,3)) for i,s in dups])
    return fs, A, steps, med

fs_i, AI, si, mi = report('old_idle_v2','idle_*.png','老年待机')
fs_w, AW, sw, mw = report('old_walk_v3','walk_*.png','老年行走')
