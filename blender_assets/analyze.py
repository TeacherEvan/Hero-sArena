import struct, zlib, os
from collections import Counter

def parse_png(path):
    with open(path, 'rb') as f:
        data = f.read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    pos = 8
    w = h = 0
    all_idat = b''
    while pos < len(data):
        ln = struct.unpack('>I', data[pos:pos+4])[0]
        ct = data[pos+4:pos+8]
        cd = data[pos+8:pos+8+ln]
        if ct == b'IHDR':
            w = struct.unpack('>I', cd[0:4])[0]
            h = struct.unpack('>I', cd[4:8])[0]
        elif ct == b'IDAT':
            all_idat += cd
        elif ct == b'IEND':
            break
        pos += 12 + ln
    
    raw = zlib.decompress(all_idat)
    bpp = 4; rb = 1 + w * bpp
    rows = []
    for y in range(h):
        off = y * rb; ft = raw[off]; cur = bytearray(raw[off+1:off+rb])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        rows.append(bytes(cur))
    return w, h, rows

def analyze(path):
    w, h, rows = parse_png(path)
    c = Counter()
    for y in range(0, h, 2):
        cur = rows[y]
        for x in range(0, w, 2):
            i = x * 4
            r,g,b,a = cur[i],cur[i+1],cur[i+2],cur[i+3]
            c[(r,g,b,a)] += 1
    top = c.most_common(12)
    total = sum(x[1] for x in top)
    print(f"{os.path.basename(path)}: {w}x{h}  {os.path.getsize(path)}B")
    for (r,g,b,a),cnt in top:
        print(f"  RGBA({r:3d},{g:3d},{b:3d},{a:3d})  {cnt:7d}  ({100*cnt/total:5.1f}%)")
    # Count key color ranges
    black_bg = sum(c for (r,g,b,a),c in c.items() if r<10 and g<10 and b<10 and a==0)
    colored_opaque = sum(c for (r,g,b,a),c in c.items() if a>0 and not (r>240 and g>240 and b>240))
    print(f"  → Black bg (a=0): {black_bg}  Colored opaque: {colored_opaque}")
    print()

for p in [
    "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/generated_v4/heroes/_raw_atlas.png",
]:
    if os.path.exists(p):
        analyze(p)
    else:
        print(f"{p}: NOT FOUND\n")
PYEOF