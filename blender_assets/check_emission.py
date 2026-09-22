import struct, zlib, os
from collections import Counter

def analyze(path):
    with open(path,'rb') as f:
        data=f.read()
    pos=8; w=h=0; all_idat=b''
    while pos<len(data):
        ln=struct.unpack('>I',data[pos:pos+4])[0]
        ct=data[pos+4:pos+8]
        cd=data[pos+8:pos+8+ln]
        if ct==b'IHDR':
            w=struct.unpack('>I',cd[0:4])[0]
            h=struct.unpack('>I',cd[4:8])[0]
        elif ct==b'IDAT':
            all_idat+=cd
        elif ct==b'IEND':
            break
        pos+=12+ln
    raw=zlib.decompress(all_idat)
    bpp=4; rb=1+w*bpp; rows=[]
    for y in range(h):
        off=y*rb; ft=raw[off]; cur=bytearray(raw[off+1:off+rb])
        if ft==1:
            for i in range(bpp,len(cur)): cur[i]=(cur[i]+cur[i-bpp])&0xFF
        rows.append(cur)
    c=Counter()
    for y in range(0,h,2):
        for x in range(0,w,2):
            i=x*bpp; r,g,b,a=rows[y][i],rows[y][i+1],rows[y][i+2],rows[y][i+3]
            c[(r,g,b,a)]+=1
    print(f"{os.path.basename(path)}: {w}x{h}  {os.path.getsize(path)}B")
    total=sum(x[1] for x in c.most_common(8))
    for (r,g,b,a),cnt in c.most_common(8):
        print(f"  RGBA({r:3d},{g:3d},{b:3d},{a:3d})  {cnt:7d}  ({100*cnt/total:5.1f}%)")
    red=sum(cnt for (r,g,b,a),cnt in c.items() if r>200 and g<60 and b<60)
    print(f"  RED pixels (R>200,G<60,B<60): {red}")
    print()

for p in [
    "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/emission_test/principled.png",
    "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/emission_test/emission.png",
]:
    analyze(p)
