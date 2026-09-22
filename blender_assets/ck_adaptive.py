import struct, zlib, os, math, bpy

def chromakey_adaptive(inpath, outpath, sample_step=4):
    """
    Render-independent chroma-key: auto-discover bg color from the
    most frequent RGB in the raw render, then key everything similar to it.
    """
    with open(inpath, 'rb') as f:
        sig = f.read(8)
        chunks = []
        w = h = 0
        ct = 0
        while True:
            length = struct.unpack('>I', f.read(4))[0]
            ctype = f.read(4)
            data = f.read(length)
            f.read(4)
            chunks.append((ctype, data))
            if ctype == b'IHDR':
                w = struct.unpack('>I', data[0:4])[0]
                h = struct.unpack('>I', data[4:8])[0]
                ct = data[9]
            elif ctype == b'IEND':
                break

    idat = b''.join(d for c, d in chunks if c == b'IDAT')
    raw = zlib.decompress(idat)
    bpp = 4
    row_bytes = 1 + w * bpp

    # Decode rows (sub filter only — enough for sprites)
    rows = []
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        rows.append(bytes(cur))

    # Sample colors, skip alpha=0 (don't count those as bg)
    color_counts = {}
    for y in range(0, h, sample_step):
        cur = rows[y]
        for x in range(0, w, sample_step):
            idx = x * bpp
            r, g, b, a = cur[idx], cur[idx+1], cur[idx+2], cur[idx+3]
            if a > 10:  # skip transparent pixels (shouldn't exist, but just in case)
                key = (r, g, b)
                color_counts[key] = color_counts.get(key, 0) + 1

    if not color_counts:
        # All transparent — just copy
        import shutil
        shutil.copy(inpath, outpath)
        return 0, w*h, 0.0

    # Most common color = background
    bg_color = max(color_counts, key=color_counts.get)
    bg_count = color_counts[bg_color]
    total_samples = sum(color_counts.values())
    print(f"  Detected bg color: RGB{bg_color} ({100*bg_count/total_samples:.1f}% of samples)")

    # Chromakey: distance-based in 0-255 space
    br, bg_c, bb = bg_color
    new_rows = []
    op = tr = 0
    threshold = 35  # in 0-255 space
    for y in range(h):
        cur = rows[y]
        nr = bytearray([0])
        for x in range(w):
            idx = x * bpp
            r, g, b = cur[idx], cur[idx+1], cur[idx+2]
            dist = math.sqrt((r-br)**2 + (g-bg_c)**2 + (b-bb)**2)
            if dist <= threshold:
                nr.extend([r, g, b, 0])
                tr += 1
            else:
                # Boost alpha for anti-aliased edges toward opaque
                nr.extend([r, g, b, 255])
                op += 1
        new_rows.append(bytes(nr))

    new_raw = b''.join(nr for nr in new_rows)
    compressed = zlib.compress(new_raw)

    with open(outpath, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n')
        import binascii
        def wc(ctype, data):
            f.write(struct.pack('>I', len(data)))
            f.write(ctype)
            f.write(data)
            f.write(struct.pack('>I', binascii.crc32(ctype + data) & 0xFFFFFFFF))
        ihdr = struct.pack('>IIBBBBB', w, h, 8, ct, 0, 0, 0)
        wc(b'IHDR', ihdr)
        wc(b'IDAT', compressed)
        wc(b'IEND', b'')

    pct = 100.0 * op / (op+tr) if (op+tr) > 0 else 0
    return op, tr, pct


def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for w in list(bpy.data.worlds):
        bpy.data.worlds.remove(w)

def setup():
    clear()
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'
    cd.ortho_scale = 3.0
    co = bpy.data.objects.new('cam', cd)
    co.location = (0, 0, 5)
    co.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co
    for pos, e in [((3,3,5),100),((-3,0,3),50),((0,-4,5),40)]:
        ld = bpy.data.lights.new('l', type='POINT')
        ld.energy = e
        lo = bpy.data.objects.new('l', ld)
        lo.location = pos
        bpy.context.collection.objects.link(lo)
    world = bpy.data.worlds.new('bg')
    bpy.context.scene.world = world
    world.use_nodes = True
    wn = world.node_tree.nodes
    for n in wn: wn.remove(n)
    bg_n = wn.new(type='ShaderNodeBackground')
    bg_n.inputs['Color'].default_value = (0.0, 0.66, 0.0, 1.0)
    bg_n.inputs['Strength'].default_value = 1.0
    out_n = wn.new(type='ShaderNodeOutputWorld')
    bg_n.location = (-200,0); out_n.location = (200,0)
    world.node_tree.links.new(bg_n.outputs['Background'], out_n.inputs['Surface'])
    s = bpy.context.scene
    s.render.resolution_x = 256
    s.render.resolution_y = 256
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    s.render.film_transparent = False

def mat(name, r, g, b):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (r, g, b, 1.0)
    return m

def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

def circle(r, v=32):
    bpy.ops.mesh.primitive_circle_add(vertices=v, radius=r, fill_type='TRIFAN')
    o = bpy.context.active_object
    o.location.z = 0
    return o

# ── Test ──
setup()
c = circle(1.0, 32)
c.data.materials.append(mat('red', 1.0, 0.2, 0.2))
c.location = (0, 0, 0)

raw = '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/ck3_raw.png'
final = '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/ck3_final.png'
render(raw)
print('Rendered raw image')

op, tr, pct = chromakey_adaptive(raw, final)
print(f'Chroma-key: {op} opaque, {tr} transparent ({pct:.1f}% opaque)')

# Verify
with open(final, 'rb') as f:
    f.read(8)
    idat = []
    w = h = 0
    while True:
        length = struct.unpack('>I', f.read(4))[0]
        ct = f.read(4)
        data = f.read(length)
        f.read(4)
        if ct == b'IHDR':
            w = struct.unpack('>I', data[0:4])[0]
            h = struct.unpack('>I', data[4:8])[0]
        elif ct == b'IDAT':
            idat.append(data)
        elif ct == b'IEND':
            break
    r2 = zlib.decompress(b''.join(idat))

rbpp = 4
rb = 1 + w * rbpp
vis_a = vis_r = vis_g = vis_b = 0
for y in range(h):
    off = y * rb
    ft = r2[off]
    cur = bytearray(r2[off+1:off+rb])
    if ft == 1:
        for i in range(rbpp, len(cur)):
            cur[i] = (cur[i] + cur[i-rbpp]) & 0xFF
    for x in range(0, w, 4):
        idx = x * rbpp
        if idx + 3 < len(cur):
            if cur[idx+3] > 200:
                vis_a += 1
                vis_r = max(vis_r, cur[idx])
                vis_g = max(vis_g, cur[idx+1])
                vis_b = max(vis_b, cur[idx+2])

print(f'Verification ({w}x{h}):')
print(f'  Fully opaque pixels (A>200): {vis_a}')
print(f'  Max RGB of opaque: ({vis_r},{vis_g},{vis_b})')
print(f'  (If vis_a > 0 and max RGB has R>150, the circle is visible with correct alpha)')
