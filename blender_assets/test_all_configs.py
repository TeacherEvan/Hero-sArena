import bpy, math, zlib, struct

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
for pos, e in [((3, 3, 5), 50), ((-3, 0, 3), 30), ((0, -3, 4), 20)]:
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

# RGB without transparency
for fmt, alpha, film in [("PNG", "RGB", False), ("PNG", "RGBA", False), ("PNG", "RGBA", True)]:
    bpy.context.scene.render.image_settings.file_format = "PNG"
    bpy.context.scene.render.image_settings.color_mode = alpha
    bpy.context.scene.render.film_transparent = film
    name = f"test_{fmt}_{'alpha' if alpha == 'RGBA' else 'rgb'}_film{'T' if film else 'F'}.png"
    bpy.context.scene.render.filepath = f"/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/{name}"
    print(f"Rendering {name} (format={fmt} color={alpha} film={film})...")
    bpy.ops.render.render(write_still=True)

print("All renders done. Now analyzing...")

# Analyze all
for name in ["test_PNG_RGB_filmF.png", "test_PNG_RGBA_filmF.png", "test_PNG_RGBA_filmT.png"]:
    path = f"/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/{name}"
    with open(path, 'rb') as f:
        sig = f.read(8)
        idat = []
        w = h = 0; ct = 0
        while True:
            length = struct.unpack('>I', f.read(4))[0]
            ctype = f.read(4)
            data = f.read(length)
            f.read(4)
            if ctype == b'IHDR':
                w = struct.unpack('>I', data[0:4])[0]
                h = struct.unpack('>I', data[4:8])[0]
                ct = data[9]
            elif ctype == b'IDAT':
                idat.append(data)
            elif ctype == b'IEND':
                break
        raw = zlib.decompress(b''.join(idat))
    
    bpp = 4 if ct == 6 else 3
    row_bytes = 1 + w * bpp
    vis_px = 0
    max_r = max_g = max_b = max_a = cnt_vis = 0
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        for x in range(w):
            idx = x * bpp
            if idx + bpp - 1 < len(cur):
                r, g, b = cur[idx], cur[idx+1], cur[idx+2]
                a = cur[idx+3] if bpp == 4 else 255
                if a > 5:
                    vis_px += 1
                    max_r = max(max_r, r); max_g = max(max_g, g)
                    max_b = max(max_b, b); max_a = max(max_a, a)
                    cnt_vis += 1
                if r > 0 or g > 0 or b > 0:
                    if cnt_vis < 1:
                        print(f"  {name} y={y} x={x}: RGB=({r},{g},{b}) A={a}")

    avg_r = max_r // max(1, cnt_vis) if cnt_vis else 0
    avg_g = max_g // max(1, cnt_vis) if cnt_vis else 0
    avg_b = max_b // max(1, cnt_vis) if cnt_vis else 0
    print(f"  {name}: vis_px={vis_px}/{w*h} ({100*vis_px/max(1,w*h):.1f}%) max_alpha={max_a} avg_visible=({avg_r},{avg_g},{avg_b})")
    print()
