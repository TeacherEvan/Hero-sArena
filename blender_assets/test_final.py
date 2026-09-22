import bpy, math, zlib, struct

# ============================================================
# STEP 1: Render with diffuse material + solid background
# ============================================================
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

# Lights
for pos, e in [((3,3,5), 100), ((-3,0,3), 50), ((0,-4,5), 40)]:
    ld = bpy.data.lights.new("l", type="POINT")
    ld.energy = e
    lo = bpy.data.objects.new("l", ld)
    lo.location = pos
    bpy.context.collection.objects.link(lo)

# Camera
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

# Green background
world = bpy.data.worlds.new("bg")
scene.world = world
world.use_nodes = True
wn = world.node_tree.nodes
for n in wn: wn.remove(n)
bg_n = wn.new(type="ShaderNodeBackground")
bg_n.inputs["Color"].default_value = (0.0, 1.0, 0.0, 1.0)  # pure green
bg_n.inputs["Strength"].default_value = 1.0
out_n = wn.new(type="ShaderNodeOutputWorld")
bg_n.location = (-200, 0)
out_n.location = (200, 0)
world.node_tree.links.new(bg_n.outputs["Background"], out_n.inputs["Surface"])

scene.render.filepath = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/ck_test.png"
bpy.ops.render.render(write_still=True)
print("Rendered ck_test.png")


# ============================================================
# STEP 2: Post-process — chroma-key green to alpha
# ============================================================
def chromakey(inpath, outpath, bg_r, bg_g, bg_b, threshold=25):
    with open(inpath, 'rb') as f:
        sig = f.read(8)
        chunks = []
        w = h = 0
        ct = 0
        while True:
            length = struct.unpack('>I', f.read(4))[0]
            ctype = f.read(4)
            data = f.read(length)
            crc = f.read(4)
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
        elif ft == 2:
            # Up filter — use previous row
            if y > 0:
                prev = rows[y-1]
                for i in range(len(cur)):
                    cur[i] = (cur[i] + prev[i]) & 0xFF
        elif ft == 3:
            if y > 0:
                prev = rows[y-1]
                for i in range(len(cur)):
                    left = cur[i-bpp] if i >= bpp else 0
                    cur[i] = (cur[i] + (left + prev[i]) // 2) & 0xFF
        elif ft == 4:
            if y > 0:
                prev = rows[y-1]
                for i in range(len(cur)):
                    left = cur[i-bpp] if i >= bpp else 0
                    up = prev[i]
                    up_left = prev[i-bpp] if i >= bpp else 0
                    p = left + up - up_left
                    p_left = abs(p - left)
                    p_up = abs(p - up)
                    p_ul = abs(p - up_left)
                    if p_left <= p_up and p_left <= p_ul:
                        pred = left
                    elif p_up <= p_ul:
                        pred = up
                    else:
                        pred = up_left
                    cur[i] = (cur[i] + pred) & 0xFF
        rows.append(bytes(cur))
    
    # Build new rows with alpha
    new_rows = []
    opaque = transparent = 0
    for y in range(h):
        cur = rows[y]
        nr = bytearray()
        nr.append(0)  # filter byte: None
        for x in range(w):
            idx = x * bpp
            r, g, b = cur[idx], cur[idx+1], cur[idx+2]
            dr = abs(int(r) - bg_r)
            dg = abs(int(g) - bg_g)
            db = abs(int(b) - bg_b)
            if dr <= threshold and dg <= threshold and db <= threshold:
                nr.extend([r, g, b, 0])
                transparent += 1
            else:
                nr.extend([r, g, b, 255])
                opaque += 1
        new_rows.append(bytes(nr))
    
    new_raw = b''.join(nr for nr in new_rows)
    compressed = zlib.compress(new_raw)
    
    with open(outpath, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n')
        
        def wc(ctype, data):
            import binascii
            f.write(struct.pack('>I', len(data)))
            f.write(ctype)
            f.write(data)
            crc = binascii.crc32(ctype + data) & 0xFFFFFFFF
            f.write(struct.pack('>I', crc))
        
        ihdr = struct.pack('>IIBBBBB', w, h, 8, ct, 0, 0, 0)
        wc(b'IHDR', ihdr)
        wc(b'IDAT', compressed)
        wc(b'IEND', b'')
    
    print(f"  Chroma-keyed: {w}x{h}  opaque={opaque}  transparent={transparent}")
    return opaque, transparent

print("\nProcessing chroma-key...")
o, t = chromakey(
    "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/ck_test.png",
    "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/ck_test_out.png",
    bg_r=0, bg_g=255, bg_b=0, threshold=30
)

print(f"\nResult: {o} opaque pixels, {t} transparent pixels")
print(f"Total: {o + t} (should be {256*256} for 256x256)")
PYEOF
