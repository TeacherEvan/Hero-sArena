import bpy, math, zlib, struct, os

# Quick standalone test: render + chroma-key + verify
out_dir = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/ck_debug"
os.makedirs(out_dir, exist_ok=True)

# --- Render ---
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)

bpy.ops.mesh.primitive_circle_add(vertices=32, radius=1.0, fill_type="TRIFAN")
obj = bpy.context.active_object
obj.location = (0, 0, 0)
mat = bpy.data.materials.new("red")
mat.diffuse_color = (1.0, 0.2, 0.2, 1.0)
obj.data.materials.append(mat)

for pos, e in [((3,3,5), 100), ((-3,0,3), 50), ((0,-4,5), 40)]:
    ld = bpy.data.lights.new("l", type="POINT")
    ld.energy = e
    lo = bpy.data.objects.new("l", ld)
    lo.location = pos
    bpy.context.collection.objects.link(lo)

cd = bpy.data.cameras.new("cam")
cd.type = "ORTHO"
cd.ortho_scale = 3.0
co = bpy.data.objects.new("cam", cd)
co.location = (0, 0, 5)
co.rotation_euler = (math.radians(90), 0, 0)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

scene = bpy.context.scene
scene.render.resolution_x = 256
scene.render.resolution_y = 256
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False

world = bpy.data.worlds.new("bg")
scene.world = world
world.use_nodes = True
wn = world.node_tree.nodes
for n in wn: wn.remove(n)
bg_n = wn.new(type="ShaderNodeBackground")
bg_n.inputs["Color"].default_value = (0.0, 0.66, 0.0, 1.0)  # green
bg_n.inputs["Strength"].default_value = 1.0
out_n = wn.new(type="ShaderNodeOutputWorld")
bg_n.location = (-200, 0)
out_n.location = (200, 0)
world.node_tree.links.new(bg_n.outputs["Background"], out_n.inputs["Surface"])

raw_path = f"{out_dir}/test_raw.png"
scene.render.filepath = raw_path
bpy.ops.render.render(write_still=True)
print(f"Rendered {raw_path}")

# --- Analyze raw ---
def analyze(path, label):
    with open(path, 'rb') as f:
        sig = f.read(8)
        idat = []
        w = h = 0
        while True:
            length = struct.unpack('>I', f.read(4))[0]
            ctype = f.read(4)
            data = f.read(length)
            f.read(4)
            if ctype == b'IHDR':
                w = struct.unpack('>I', data[0:4])[0]
                h = struct.unpack('>I', data[4:8])[0]
            elif ctype == b'IDAT':
                idat.append(data)
            elif ctype == b'IEND':
                break
        raw = zlib.decompress(b''.join(idat))
    
    bpp = 4
    row_bytes = 1 + w * bpp
    
    # Collect distinct colors (sampled)
    colors = {}
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        for x in range(0, w, 8):
            idx = x * bpp
            if idx + 3 < len(cur):
                r, g, b, a = cur[idx], cur[idx+1], cur[idx+2], cur[idx+3]
                key = (r, g, b, a)
                colors[key] = colors.get(key, 0) + 1
    
    sorted_colors = sorted(colors.items(), key=lambda x: -x[1])[:10]
    print(f"\n{label} ({w}x{h}):")
    print(f"  Distinct colors (top 10 by frequency):")
    for (r,g,b,a), cnt in sorted_colors:
        pct = 100.0 * cnt / (w * h // 8)
        print(f"    RGBA({r:3d},{g:3d},{b:3d},{a:3d})  {pct:5.1f}%  count={cnt}")
    
    # Check for pure green
    green_count = sum(c for (r,g,b,a), c in colors.items() if r < 30 and 140 < g < 200 and b < 30)
    print(f"  Green-ish pixels (R<30, 140<G<200, B<30): {green_count}")

analyze(raw_path, "RAW render")
analyze(f"{out_dir}/test_raw.png", "RAW again")

# --- Chroma-key ---
def chromakey(inpath, outpath, bg_color, threshold=30):
    bg_r, bg_g, bg_b = bg_color
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
    rows = []
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        rows.append(bytes(cur))
    
    new_rows = []
    op = tr = 0
    for y in range(h):
        cur = rows[y]
        nr = bytearray([0])
        for x in range(w):
            idx = x * bpp
            r, g, b = cur[idx], cur[idx+1], cur[idx+2]
            if abs(int(r)-bg_r) <= threshold and abs(int(g)-bg_g) <= threshold and abs(int(b)-bg_b) <= threshold:
                nr.extend([r, g, b, 0])
                tr += 1
            else:
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
    
    print(f"\nChroma-key: opaque={op} transparent={tr} total={op+tr} (expected {w*h})")
    return op, tr

print("\n--- Running chroma-key ---")
op, tr = chromakey(raw_path, f"{out_dir}/test_ck.png", (0, 170, 0), threshold=30)

# --- Analyze result ---
analyze(f"{out_dir}/test_ck.png", "AFTER chroma-key")
