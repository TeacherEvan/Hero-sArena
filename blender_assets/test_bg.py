import bpy, math, zlib, struct

def decode_check(path, label):
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
    
    ct = None
    with open(path, 'rb') as f:
        f.read(8)
        while True:
            length = struct.unpack('>I', f.read(4))[0]
            ctype = f.read(4)
            data = f.read(length)
            f.read(4)
            if ctype == b'IHDR':
                ct = data[9]
            elif ctype == b'IEND':
                break
    
    bpp = 4 if ct == 6 else 3
    row_bytes = 1 + w * bpp
    vis = 0
    samples = []
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        for x in range(0, w, 4):
            idx = x * bpp
            if idx + bpp - 1 < len(cur):
                r, g, b = cur[idx], cur[idx+1], cur[idx+2]
                if r > 5 or g > 5 or b > 5:
                    vis += 1
                    if len(samples) < 5:
                        a = cur[idx+3] if bpp == 4 else 255
                        samples.append(f'y={y:3d} x={x:3d} RGBA=({r:3d},{g:3d},{b:3d},{a:3d})')
    pct = 100.0 * vis / (w * h // 4) if w * h > 0 else 0
    print(f'{label}: {w}x{h} vis={vis} ({pct:.1f}%) samples:')
    for s in samples:
        print(f'  {s}')

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)

# Red cube
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.5))
cube = bpy.context.active_object
mat = bpy.data.materials.new("red_plain")
mat.diffuse_color = (1.0, 0.0, 0.0, 1.0)
cube.data.materials.append(mat)

# Lights
for pos, e in [((3, 3, 5), 100), ((-3, 0, 3), 60), ((0, -4, 5), 40), ((0, 4, 5), 40)]:
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

# Try different world background colors with film_transparent=False
for bg_name, bg_color in [
    ("green", (0.0, 1.0, 0.0)),
    ("magenta", (1.0, 0.0, 1.0)),
]:
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    
    world = bpy.data.worlds.new(f"World_{bg_name}")
    scene.world = world
    world.use_nodes = True
    wnodes = world.node_tree.nodes
    for n in wnodes:
        wnodes.remove(n)
    bg_node = wnodes.new(type="ShaderNodeBackground")
    bg_node.inputs["Color"].default_value = (*bg_color, 1.0)
    bg_node.inputs["Strength"].default_value = 1.0
    out_node = wnodes.new(type="ShaderNodeOutputWorld")
    bg_node.location = (-200, 0)
    out_node.location = (200, 0)
    world.node_tree.links.new(bg_node.outputs["Background"], out_node.inputs["Surface"])
    
    name = f"/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/bg_{bg_name}.png"
    scene.render.filepath = name
    print(f"Rendering {name}...")
    bpy.ops.render.render(write_still=True)

print("Done. Analyzing...")
for bg in ["green", "magenta"]:
    decode_check(f"/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/bg_{bg}.png", f"bg_{bg}")
