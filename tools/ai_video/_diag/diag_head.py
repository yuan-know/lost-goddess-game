# -*- coding: utf-8 -*-
import numpy as np, glob, os
from PIL import Image

def alphas(sub,d,pat):
    fs=sorted(glob.glob(os.path.join(sub,d,pat)))
    return fs, np.stack([np.array(Image.open(f).convert('RGBA'))[:,:,3].astype(np.float32)/255.0 for f in fs])

for sub,d,pat,label in [('final','old_idle_v2','idle_*.png','FINAL old_idle_v2'),
                        ('raw','old_idle_v1','idle_*.png','SOURCE old_idle_v1')]:
    fs,A=alphas(sub,d,pat); n=len(A)
    u=A.max(axis=0); rs=np.where((u>0.5).any(axis=1))[0]; top,bot=rs[0],rs[-1]; H=bot-top
    h1=top+int(H*0.16)
    print('='*72); print(label,' n=%d  body rows %d-%d  head band %d-%d'%(n,top,bot,top,h1))
    recs=[]
    for i,a in enumerate(A):
        m=a[top:h1+1]>0.5
        area=int(m.sum())
        cols=np.where(m.any(axis=0))[0]; w=int(cols[-1]-cols[0]+1) if len(cols) else 0
        rows=np.where(m.any(axis=1))[0]; tr=int(top+rows[0]) if len(rows) else -1
        # full-body top row and total height
        mf=a>0.5
        frows=np.where(mf.any(axis=1))[0]
        recs.append((area,w,tr,int(frows[0]),int(frows[-1])))
    ar=np.array([r[0] for r in recs]); wd=np.array([r[1] for r in recs]); tr=np.array([r[2] for r in recs])
    ftop=np.array([r[3] for r in recs])
    print(' head area : med %.0f  min %d(f%d)  max %d(f%d)  span %d (%.1f%%)'%(
        np.median(ar),ar.min(),ar.argmin(),ar.max(),ar.argmax(),ar.max()-ar.min(),100.0*(ar.max()-ar.min())/np.median(ar)))
    print(' head width: med %.0f  min %d(f%d)  max %d(f%d)  span %d'%(
        np.median(wd),wd.min(),wd.argmin(),wd.max(),wd.argmax(),wd.max()-wd.min()))
    print(' head toprow: min %d max %d span %d'%(tr.min(),tr.max(),tr.max()-tr.min()))
    print(' body toprow: min %d max %d span %d'%(ftop.min(),ftop.max(),ftop.max()-ftop.min()))
    print(' 首尾对比  frame0 vs frame%d :'%(n-1))
    print('   area  %d vs %d  (diff %+d, %+.1f%%)'%(recs[0][0],recs[-1][0],recs[-1][0]-recs[0][0],100.0*(recs[-1][0]-recs[0][0])/recs[0][0]))
    print('   width %d vs %d  (diff %+d)'%(recs[0][1],recs[-1][1],recs[-1][1]-recs[0][1]))
    print('   headtop %d vs %d (diff %+d)'%(recs[0][2],recs[-1][2],recs[-1][2]-recs[0][2]))
    print('   bodytop %d vs %d (diff %+d)'%(recs[0][3],recs[-1][3],recs[-1][3]-recs[0][3]))
    print(' 末尾10帧 area/width/headtop:')
    for i in range(max(0,n-10),n):
        print('   f%02d  area %5d  w %3d  htop %3d  btop %3d'%(i,recs[i][0],recs[i][1],recs[i][2],recs[i][3]))
    print(' 开头10帧:')
    for i in range(min(10,n)):
        print('   f%02d  area %5d  w %3d  htop %3d  btop %3d'%(i,recs[i][0],recs[i][1],recs[i][2],recs[i][3]))
