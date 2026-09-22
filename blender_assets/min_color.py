"""Minimal Blender 5.2 color test: colored world background + emission object"""
import bpy, os

out = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/min_color"
os.makedirs(out, exist_ok=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)
for l in list(bpy.data.lights): bpy.data.lights.remove(l)

# Camera
cd = bpy.data.cameras.new('cam')
cd.type = 'ORTHO'; cd.ortho_scale = 2.0
co = bpy.data.objects.new('cam', cd)
co.location = (0, 0, 4); co.rotation_euler = (1.570796, 0, 0)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

# World with colored background
world = bpy.data.worlds.new('world')
bpy.context.scene.world = world

# Try 1: World nodes with Background
world.use_nodes = True
wn = world.node_tree.nodes
for n in wn: wn.remove(n)
bg = wn.new(type='ShaderNodeBackground')
bg.inputs['Color'].default_value = (0.0, 0.0, 0.0, 1.0)
bg.inputs['Strength'].default_value = 1.0
out_w = wn.new(type='ShaderNodeOutputWorld')
world.node_tree.links.new(bg.outputs['Background'], out_w.inputs['Surface'])

# Point light
ld = bpy.data.lights.new('light', type='POINT')
ld.energy = 10.0
lo = bpy.data.objects.new('light', ld)
lo.location = (0, 0, 3)
bpy.context.collection.objects.link(lo)

# Emission object
mat = bpy.data.materials.new('mat')
mat.use_nodes = True
mn = mat.node_tree.nodes
for n in mn: mn.remove(n)
emit = mn.new(type='ShaderNodeEmission')
emit.inputs['Color'].default_value = (1.0, 0.0, 0.0, 1.0)  # pure red
emit.inputs['Strength'].default_value = 10.0
out_m = mn.new(type='ShaderNodeOutputMaterial')
mat.node_tree.links.new(emit.outputs['Emission'], out_m.inputs['Surface'])

bpy.ops.mesh.primitive_circle_add(vertices=32, radius=0.8, fill_type='TRIFAN')
obj = bpy.context.active_object
obj.location.z = 0.01
obj.data.materials.clear()
obj.data.materials.append(mat)

# Render settings - try various color management configs
s = bpy.context.scene
s.render.engine = 'BLENDER_EEVEE'  # Try old EEVEE first
s.render.resolution_x = 512; s.render.resolution_y = 512
s.render.film_transparent = False
s.render.image_settings.file_format = 'PNG'
s.render.image_settings.color_mode = 'RGBA'

# Try different view transforms
for vt in ['Raw', 'Standard', 'AgX', 'Filmic', 'None']:
    try:
        s.render.image_settings.view_transform = vt
    except:
        pass
    s.render.filepath = os.path.join(out, f'eevee_{vt}.png')
    try:
        bpy.ops.render.render(write_still=True)
    except Exception as e:
        print(f"  {vt}: ERROR {e}")

# Try CYCLES  
s.render.engine = 'CYCLES'
s.cycles.samples = 128
for vt in ['Raw', 'Standard', 'Filmic']:
    try:
        s.render.image_settings.view_transform = vt
    except:
        pass
    s.render.filepath = os.path.join(out, f'cycles_{vt}.png')
    try:
        bpy.ops.render.render(write_still=True)
        print(f"  cycles_{vt}: OK {os.path.getsize(os.path.join(out, f'cycles_{vt}.png'))}B")
    except Exception as e:
        print(f"  cycles_{vt}: ERROR {e}")

print("\nChecking results...")
PYEOF
