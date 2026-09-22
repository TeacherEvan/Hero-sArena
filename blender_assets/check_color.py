import struct, zlib, os, glob
from collections import Counter

def has_color(path, min_red=200):
    with open(path,'rb') as f:
        data=f.read()
    pos=8; w=h=0; all_idat=b''
    while pos<len(data):
        ln=struct.unpack('>I',data[pos:pos+4])[0]
        ct=data[pos+4:pos+8]; cd=data[pos+8:pos+8+ln]
        if ct==b'*IHDR' or ct==b'IHDR':
            w=struct.unpack('>I',cd[0:4])[0]; h=struct.unpack('>I',cd[4:8])[0]
        if ct==b'IDAT': all_idat+=cd
        if ct==b'IEND': break
        pos+=12+ln
    try:
        raw=zlib.decompress(all_idat)
    except:
        return None, 0, 0
    bpp=4; rb=1+w*bpp; rows=[]
    for y in range(h):
        off=y*rb; ft=raw[off]; cur=bytearray(raw[off+1:off+rb])
        if ft==1:
            for i in range(bpp,len(cur)): cur[i]=(cur[i]+cur[i-bpp])&0xFF
        rows.append(cur)
    total=0; red=0; nonblack=0
    for y in range(0,h,4):
        for x in range(0,w,4):
            i=x*bpp; r,g,b,a=rows[y][i],rows[y][i+1],rows[y][i+2],rows[y][i+3]
            total+=1
            if r>min_red and g<80 and b<80 and a>0: red+=1
            if a>0 and not(r<10 and g<10 and b<10): nonblack+=1
    return red, nonblack, total

files = sorted(glob.glob("/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/color_test/*.png"))
print(f"{'Filename':<45} {'red_px':>8} {'nonblack':>9} {'pct_red':>8} {'pct_nb':>8}")
print("-"*82)
for f in files:
    red, nb, tot = has_color(f)
    if red is None:
        print(f"{os.path.basename(f):<45}  DECOMPRESS ERROR")
        continue
    pct_red = 100*red/tot if tot else 0
    pct_nb = 100*nb/tot if tot else 0
    flag = " <<<" if red > 100 else ""
    print(f"{os.path.basename(f):<45} {red:>8} {nb:>9} {pct_red:>7.2f}% {pct_nb:>7.2f}%{flag}")
