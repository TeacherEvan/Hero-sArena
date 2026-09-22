import struct, zlib, bpy, math

def raw_alpha_check(path, label):
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
    max_a = 0
    max_r = max_g = max_b = 0
    
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        for x in range(0, w, 2):
            idx = x * bpp
            if idx + bpp - 1 < len(cur):
                r, g, b = cur[idx], cur[idx+1], cur[idx+2]
                a = cur[idx+3] if bpp == 4 else 255
                max_r = max(max_r, r)
                max_g = max(max_g, g)
                max_b = max(max_b, b)
                max_a = max(max_a, a)
                if a > 0:
                    vis_px += 1
    
    print(f'{label}: {w}x{h}  non_bg_sampled={vis_px}  max_a={max_a}  max_rgb=({max_r},{max_g},{max_b})')

# Render test with opaque background
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)

bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.5))
cube = bpy.context.active_object
mat = bpy.data.materials.new('red')
mat.use_nodes = True
nodes = mat.node_tree.nodes
for n in nodes:
    nodes.remove(n)
out = nodes.new('ShaderNodeOutputMaterial')
bsdf = nodes.new('ShaderNodeBsdfPrincipled')
bsdf.inputs['Base Color'].default_value = (1.0, 0.0, 0.0, 1.0)
bsdf.inputs['Alpha'].default_value = 1.0
mat.node_tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
cube.data.materials.append(mat)

for pos, e in [((3, 3, 5), 100), ((-3, 0, 3), 60), ((0, -3, 4), 40), ((0, 3, 4), 40)]:
    ld = bpy.data.lights.new('l', type='POINT')
    ld.energy = e
    lo = bpy.data.objects.new('l', ld)
    lo.location = pos
    bpy.context.collection.objects.link(lo)

cd = bpy.data.cameras.new('cam')
cd.type = 'ORTHO'
cd.ortho_scale = 3.0
co = bpy.data.objects.new('cam', cd)
co.location = (0, 0, 5)
co.rotation_euler = (math.radians(90), 0, 0)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

scene = bpy.context.scene
scene.render.resolution_x = 256
scene.render.resolution_y = 256
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = False

# World background
world = bpy.data.worlds.new('bg')
scene.world = world
world.use_nodes = True
wn = world.node_tree.nodes
for n in wn:
    wn.remove(n)
bg_n = wn.new(type='ShaderNodeBackground')
bg_n.inputs['Color'].default_value = (0.3, 0.3, 0.3, 1.0)
bg_n.inputs['Strength'].default_value = 1.0
out_n = wn.new(type='ShaderNodeOutputWorld')
bg_n.location = (-200, 0)
out_n.location = (200, 0)
world.node_tree.links.new(bg_n.outputs['Background'], out_n.inputs['Surface'])

scene.render.filepath = '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/test_opaque_bg.png'
bpy.ops.render.render(write_still=True)
print('Rendered test_opaque_bg.png')
raw_alpha_check('/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/test_opaque_bg.png', 'test_opaque_bg')
