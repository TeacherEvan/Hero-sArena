import struct, zlib, os, glob
from collections import Counter

def check(path):
    with open(path,'rb') as f:
        data=f.read()
    pos=8; w=h=0; idat=b''
    while pos<len(data):
        ln=struct.unpack('>I',data[pos:pos+4])[0]
        ct=data[pos+4:pos+8]; cd=data[pos+8:pos+8+ln]
        if ct==b'IHDR': w,h=struct.unpack('>II',cd[:8])[:2]
        elif ct==b'IDAT': idat+=cd
        elif ct==b'IEND': break
        pos+=12+ln
    raw=zlib.decompress(idat)
    bpp=4; rb=1+w*bpp; rows=[]
    for y in range(h):
        off=y*rb; ft=raw[off]; cur=bytearray(raw[off+1:off+rb])
        if ft==1:
            for i in range(bpp,len(cur)): cur[i]=(cur[i]+cur[i-bpp])&0xFF
        rows.append(cur)
    c=Counter()
    for y in range(0,h,2):
        cur=rows[y]
        for x in range(0,w,2):
            i=x*bpp; r,g,b,a=cur[i],cur[i+1],cur[i+2],cur[i+3]
            c[(r,g,b,a)]+=1
    s=os.path.basename(path)
    # Find red-ish pixels
    red=sum(cnt for (r,g,b,a),cnt in c.items() if r>150 and g<80 and b<80 and a>0)
    nonblack=sum(cnt for (r,g,b,a),cnt in c.items() if a>0 and not(r<10 and g<10 and b<10))
    total=sum(c.values())
    print(f"{s:35s} red={red:6d} ({100*red/total:4.1f}%)  nonblack={nonblack:6d} ({100*nonblack/total:4.1f}%)")
    # Show top colors if anything interesting
    if red > 0 or nonblack > 1000:
        print(f"  Top colors:")
        for (r,g,b,a),cnt in c.most_common(5):
            print(f"    RGBA({r:3d},{g:3d},{b:3d},{a:3d})  {cnt:7d}  {100*cnt/total:5.1f}%")

for f in sorted(glob.glob('/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/v2_color/*.png')):
    check(f)
