import struct, zlib, os

def check_everything(path):
    with open(path,'rb') as f:
        data=f.read()
    pos=8; w=h=0; all_idat=b''
    n_c = 0
    while pos<len(data):
        ln=struct.unpack('>I',data[pos:pos+4])[0]
        ct=data[pos+4:pos+8]; cd=data[pos+8:pos+8+ln]
        n_c+=1
        if ct==b'IHDR':
            w=struct.unpack('>I',cd[0:4])[0]; h=struct.unpack('>I',cd[4:8])[0]
            print(f"  IHDR: {w}x{h} bit_depth={cd[8]} color_type={cd[9]}")
        elif ct==b'IDAT':
            all_idat+=cd
            print(f"  IDAT chunk #{n_c}: {len(cd)}B")
        elif ct==b'IEND':
            print(f"  IEND at chunk #{n_c}")
            break
        else:
            print(f"  {ct.decode('ascii',errors='replace')} chunk: {cd[:30].hex()}... ({len(cd)}B)")
        pos+=12+ln
    
    print(f"\n  Total IDAT: {len(all_idat)}B  Chunks: {n_c}")
    
    try:
        raw=zlib.decompress(all_idat)
        print(f"  Decompressed: {len(raw)}B  (expected ~{w*h*4+w*h} for RGBA+filters)")
    except Exception as e:
        print(f"  Decompress FAILED: {e}")
        return
    
    bpp=4; rb=1+w*bpp; rows=[]
    for y in range(h):
        off=y*rb; ft=raw[off]; cur=bytearray(raw[off+1:off+rb])
        if ft==1:
            for i in range(bpp,len(cur)): cur[i]=(cur[i]+cur[i-bpp])&0xFF
        rows.append(bytes(cur))
    
    # Full histogram
    from collections import Counter
    c=Counter()
    nonblack=0; nonzero_alpha=0
    for y in range(h):
        cur=rows[y]
        for x in range(w):
            i=x*bpp; r,g,b,a=cur[i],cur[i+1],cur[i+2],cur[i+3]
            c[(r,g,b,a)]+=1
            if a>0: nonzero_alpha+=1
            if not(r<10 and g<10 and b<10): nonblack+=1
    
    print(f"\n  Total pixels: {w*h}")
    print(f"  Non-black pixels: {nonblack}")
    print(f"  Pixels with alpha>0: {nonzero_alpha}")
    print(f"\n  Top 10 RGBA values:")
    for (r,g,b,a),cnt in c.most_common(10):
        print(f"    RGBA({r:3d},{g:3d},{b:3d},{a:3d})  {cnt:8d}  {100*cnt/(w*h):5.1f}%")

print("=== black_void_none_emission_20.0.png ===")
check_everything("/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/color_test/black_void_none_emission_20.0.png")
