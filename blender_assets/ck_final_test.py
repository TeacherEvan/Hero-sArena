import struct, zlib, os, sys, math, bpy

BG_R, BG_G, BG_B = 0.0, 170.0/255.0, 0.0   # Blender color-space (0-1)
BG_THRESHOLD = 0.12   # ~30 in 0-255 space

def chromakey_png(inpath, outpath):
    """Replace bg-colored pixels with alpha=0, everything else alpha=255.
    Uses Euclidean distance in RGB space for anti-aliased edge handling."""
    with open(inpath, 'rb') as f:
        sig = f.read(8)
        assert sig == b'\x89PNG\r\n\x1a\n'
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

    idat = b''.join(d for ct2, d in chunks if ct2 == b'IDAT')
    raw = zlib.decompress(idat)

    bpp = 4
    row_bytes = 1 + w * bpp
    rows = []
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        elif ft == 2 and y > 0:
            prev = rows[y-1]
            for i in range(len(cur)):
                cur[i] = (cur[i] + prev[i]) & 0xFF
        elif ft == 3 and y > 0:
            prev = rows[y-1]
            for i in range(len(cur)):
                left = cur[i-bpp] if i >= bpp else 0
                cur[i] = (cur[i] + (left + prev[i]) // 2) & 0xFF
        elif ft == 4 and y > 0:
            prev = rows[y-1]
            for i in range(len(cur)):
                left = cur[i-bpp] if i >= bpp else 0
                up = prev[i]
                up_left = prev[i-bpp] if i >= bpp else 0
                p = left + up - up_left
                pl = abs(p - left)
                pu = abs(p - up)
                pul = abs(p - up_left)
                if pl <= pu and pl <= pul:
                    pred = left
                elif pu <= pul:
                    pred = up
                else:
                    pred = up_left
                cur[i] = (cur[i] + pred) & 0xFF
        rows.append(bytes(cur))

    new_rows = []
    op = tr = 0
    for y in range(h):
        cur = rows[y]
        nr = bytearray([0])  # filter=None
        for x in range(w):
            idx = x * bpp
            r8 = cur[idx]
            g8 = cur[idx+1]
            b8 = cur[idx+2]
            # Convert to 0-1 space for comparison
            r = r8 / 255.0
            g = g8 / 255.0
            b = b8 / 255.0
            dist = math.sqrt((r - BG_R)**2 + (g - BG_G)**2 + (b - BG_B)**2)
            if dist <= BG_THRESHOLD:
                nr.extend([r8, g8, b8, 0])
                tr += 1
            else:
                nr.extend([r8, g8, b8, 255])
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

    pct = 100.0 * op / (op+tr)
    return op, tr, pct


# ── Scene setup ──────────────────────────────────────────────
def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for w in list(bpy.data.worlds):
        bpy.data.worlds.remove(w)

def setup():
    clear()
    # Camera
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'
    cd.ortho_scale = 3.0
    co = bpy.data.objects.new('cam', cd)
    co.location = (0, 0, 5)
    co.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co
    # Lights
    for pos, e in [((3,3,5),100),((-3,0,3),50),((0,-4,5),40)]:
        ld = bpy.data.lights.new('l', type='POINT')
        ld.energy = e
        lo = bpy.data.objects.new('l', ld)
        lo.location = pos
        bpy.context.collection.objects.link(lo)
    # World
    world = bpy.data.worlds.new('bg')
    bpy.context.scene.world = world
    world.use_nodes = True
    wn = world.node_tree.nodes
    for n in wn: wn.remove(n)
    bg_n = wn.new(type='ShaderNodeBackground')
    bg_n.inputs['Color'].default_value = (BG_R, BG_G, BG_B, 1.0)
    bg_n.inputs['Strength'].default_value = 1.0
    out_n = wn.new(type='ShaderNodeOutputWorld')
    bg_n.location = (-200,0); out_n.location = (200,0)
    world.node_tree.links.new(bg_n.outputs['Background'], out_n.inputs['Surface'])
    # Render settings
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

def circle(r, v=32, fill=True):
    bpy.ops.mesh.primitive_circle_add(vertices=v, radius=r, fill_type='TRIFAN' if fill else 'NOTHING')
    o = bpy.context.active_object
    o.location.z = 0
    return o

def rect(w, h):
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0,0,0))
    o = bpy.context.active_object
    o.scale = (w/2, h/2, 1.0)
    return o

def rrect(w, h, rad, v=8):
    bpy.ops.mesh.primitive_cylinder_add(vertices=v*4, radius=1.0, depth=0.02)
    o = bpy.context.active_object
    o.scale = (w/2, h/2, 1.0)
    return o

def place(o, x, y, z=0):
    o.location = (x, y, z)


# ── Test ─────────────────────────────────────────────────────
setup()

# Red circle
c = circle(1.0, 32)
c.data.materials.append(mat('red', 1.0, 0.2, 0.2))
place(c, 0, 0, 0)

raw = '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/ck2_raw.png'
final = '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/ck2_final.png'
render(raw)
print(f'Rendered raw: {raw}')

op, tr, pct = chromakey_png(raw, final)
print(f'Chroma-key: {op} opaque, {tr} transparent ({pct:.1f}% opaque)')

# Verify
import zlib
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
    raw2 = zlib.decompress(b''.join(idat))

bpp = 4
row_bytes = 1 + w * bpp
vis = bg = other = 0
for y in range(h):
    off = y * row_bytes
    ft = raw2[off]
    cur = bytearray(raw2[off+1:off+row_bytes])
    if ft == 1:
        for i in range(bpp, len(cur)):
            cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
    for x in range(0, w, 4):
        idx = x * bpp
        if idx + 3 < len(cur):
            a = cur[idx+3]
            r,g,b = cur[idx], cur[idx+1], cur[idx+2]
            if a == 0:
                bg += 1
            elif r > 150:
                vis += 1
            else:
                other += 1

print(f'\nVerification: {w}x{h}')
print(f'  Alpha=0 (bg): {bg}')
print(f'  Visible red (A>0, R>150): {vis}')
print(f'  Other visible: {other}')
print(f'  Total sampled: {bg+vis+other}')
