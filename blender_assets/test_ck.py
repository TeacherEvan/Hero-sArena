import struct, zlib

def chromakey(path_in, path_out, bg_r, bg_g, bg_b, threshold=30):
    """Replace background-colored pixels with alpha=0, make everything else opaque."""
    with open(path_in, 'rb') as f:
        sig = f.read(8)
        idat = []
        w = h = 0
        ct = 0
        chunk_sizes = []
        while True:
            length = struct.unpack('>I', f.read(4))[0]
            ctype = f.read(4)
            data = f.read(length)
            crc = f.read(4)
            chunk_sizes.append((ctype, length))
            if ctype == b'IHDR':
                w = struct.unpack('>I', data[0:4])[0]
                h = struct.unpack('>I', data[4:8])[0]
                ct = data[9]
            elif ctype == b'IEND':
                break
            elif ctype == b'IDAT':
                idat.append(data)
            else:
                # Copy non-IDAT chunks as-is for output
                pass
    
    raw = zlib.decompress(b''.join(idat))
    bpp = 4
    row_bytes = 1 + w * bpp
    
    # Decompress all rows
    rows = []
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        rows.append(bytes(cur))
    
    # Chroma-key: build new pixel data
    new_rows = []
    transparent = 0
    opaque = 0
    for y in range(h):
        cur = bytearray(rows[y])
        new_row = bytearray()
        new_row.append(0)  # filter byte: None
        for x in range(w):
            idx = x * bpp
            r, g, b, a = cur[idx], cur[idx+1], cur[idx+2], cur[idx+3]
            dr = abs(r - bg_r)
            dg = abs(g - bg_g)
            db = abs(b - bg_b)
            if dr <= threshold and dg <= threshold and db <= threshold:
                # Background → transparent
                new_row.extend([r, g, b, 0])
                transparent += 1
            else:
                # Object → opaque
                new_row.extend([r, g, b, 255])
                opaque += 1
        new_rows.append(bytes(new_row))
    
    # Re-compress and write PNG
    new_raw = b''.join(bytes([0]) + row for row in new_rows)
    compressed = zlib.compress(new_raw)
    
    with open(path_out, 'wb') as f:
        # PNG signature
        f.write(b'\x89PNG\r\n\x1a\n')
        
        def write_chunk(ctype, data):
            f.write(struct.pack('>I', len(data)))
            f.write(ctype)
            f.write(data)
            import binascii
            crc = binascii.crc32(ctype + data) & 0xFFFFFFFF
            f.write(struct.pack('>I', crc))
        
        # IHDR
        ihdr_data = struct.pack('>IIBBBBB', w, h, 8, ct, 0, 0, 0)
        write_chunk(b'IHDR', ihdr_data)
        
        # IDAT
        write_chunk(b'IDAT', compressed)
        
        # IEND
        write_chunk(b'IEND', b'')
    
    print(f'  Chroma-keyed: {w}x{h}  opaque={opaque}  transparent={transparent}')
    return opaque, transparent

print("=== Chroma-key test: bg_green (green bg, red cube) ===")
o, t = chromakey(
    '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/bg_green.png',
    '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/bg_green_ck.png',
    110, 197, 81, threshold=40
)

print("\n=== Chroma-key test: bg_magenta (magenta bg, red cube) ===")
o, t = chromakey(
    '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/bg_magenta.png',
    '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/bg_magenta_ck.png',
    201, 116, 201, threshold=40
)
PYEOF
