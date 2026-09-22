import struct, zlib, os
from collections import Counter
import math

def check_pixels(path):
    with open(path,'rb') as f:
        data=f.read()
    pos=8; w=h=0; all_idat=b''
    while pos<len(data):
        ln=struct.unpack('>I',data[pos:pos+4])[0]
        ct=data[pos+4:pos+8]; cd=data[pos+8:pos+8+ln]
        if ct==b'IHDR': w=struct.unpack('>I',cd[0:4])[0]; h=struct.unpack('>I',cd[4:8])[0]
        elif ct==b'IDAT': all_idat+=cd
        elif ct==b'IEND': break
        pos+=12+ln
    raw=zlib.decompress(all_idat)
    bpp=4; rb=1+w*bpp; rows=[]
    for y in range(h):
        off=y*rb; ft=raw[off]; cur=bytearray(raw[off+1:off+rb])
        if ft==1:
            for i in range(bpp,len(cur)): cur[i]=(cur[i]+cur[i-bpp])&0xFF
        rows.append(bytes(cur))
    
    # Collect all DIFFERENT non-black pixels
    colored = Counter()
    for y in range(h):
        cur=rows[y]
        for x in range(w):
            i=x*bpp; r,g,b,a=cur[i],cur[i+1],cur[i+2],cur[i+3]
            if not (r<10 and g<10 and b<10):
                colored[(r,g,b,a)] += 1
    
    print(f"\n{path}")
    print(f"  {w}x{h}  Total colored (non-black) pixels: {sum(colored.values())}")
    print(f"  Unique colored values: {len(colored)}")
    print(f"\n  All colored pixel values (sorted by count):")
    for (r,g,b,a),cnt in colored.most_common(30):
        print(f"    RGBA({r:3d},{g:3d},{b:3d},{a:3d})  {cnt:7d}  {100*cnt/(w*h):5.2f}%")

for p in [
    "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/color_test/black_void_none_emission_20.0.png",
    "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/color_test/black_void_point_diffuse.png",
    "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/color_test/black_void_sun_principled.png",
]:
    check_pixels(p)
