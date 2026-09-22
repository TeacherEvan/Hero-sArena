import struct, zlib, os
from collections import Counter

path = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/min_test/test.png"
print(f"File: {path}")
print(f"Size: {os.path.getsize(path)} bytes")
print(f"PNG sig: {open(path,'rb').read(8).hex()}")

with open(path, 'rb') as f:
    data = f.read()

pos = 8
while pos < len(data):
    ln = struct.unpack('>I', data[pos:pos+4])[0]
    ct = data[pos+4:pos+8].decode('ascii', errors='replace')
    cd = data[pos+8:pos+8+ln]
    print(f"  chunk: {ct}  {ln}B  at offset {pos}")
    if ct == 'IHDR':
        w = struct.unpack('>I', cd[0:4])[0]
        h = struct.unpack('>I', cd[4:8])[0]
        bd = cd[8]; ct2 = cd[9]
        print(f"    {w}x{h}  bit_depth={bd}  color_type={ct2}")
    elif ct == 'IDAT':
        print(f"    IDAT DATA first 40 bytes: {cd[:40].hex()}")
    elif ct == 'IEND':
        break
    pos += 12 + ln

# Try to find ALL IDAT chunks (not just first)
all_idat = b''
pos = 8
while pos < len(data):
    ln = struct.unpack('>I', data[pos:pos+4])[0]
    ct = data[pos+4:pos+8]
    cd = data[pos+8:pos+8+ln]
    if ct == b'IDAT':
        all_idat += cd
    pos += 12 + ln

print(f"\nTotal IDAT data: {len(all_idat)} bytes")
print(f"First 20 bytes: {all_idat[:20].hex()}")
print(f"Last 20 bytes: {all_idat[-20:].hex()}")

# Try decompress
try:
    raw = zlib.decompress(all_idat)
    print(f"Decompressed OK: {len(raw)} bytes")
    bpp = 4; rb = 1 + w * bpp
    rows = []
    for y in range(h):
        off = y * rb; ft = raw[off]; cur = bytearray(raw[off+1:off+rb])
        if ft == 1:
            for i in range(bpp, len(cur)): cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        rows.append(cur)
    c = Counter()
    for y in range(0, h, 2):
        for x in range(0, w, 2):
            i = x * bpp; r,g,b,a = rows[y][i],rows[y][i+1],rows[y][i+2],rows[y][i+3]
            c[(r,g,b,a)] += 1
    top = c.most_common(8)
    tot = sum(x[1] for x in top)
    print(f"\nTop 8 RGBA:")
    for (r,g,b,a),cnt in top:
        print(f"  RGBA({r:3d},{g:3d},{b:3d},{a:3d})  {cnt:7d}  ({100*cnt/tot:5.1f}%)")
except Exception as e:
    print(f"Decompress FAILED: {e}")
PYEOF