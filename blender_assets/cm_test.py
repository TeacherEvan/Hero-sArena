"""Test: color management fix - set view_transform to 'Raw' or 'Standard'"""
import bpy, os

out = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/cm_test"
os.makedirs(out, exist_ok=True)

def render_test(name, view_transform, look, use_filter):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)
    for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)
    for l in list(bpy.data.lights): bpy.data.lights.remove(l)

    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'; cd.ortho_scale = 2.0
    co = bpy.data.objects.new('cam', cd)
    co.location = (0, 0, 4); co.rotation_euler = (1.570796, 0, 0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co

    world = bpy.data.worlds.new('world')
    bpy.context.scene.world = world
    world.use_nodes = True
    for n in world.node_tree.nodes: world.node_tree.nodes.remove(n)
    bn = world.node_tree.nodes.new(type='ShaderNodeBackground')
    bn.inputs['Color'].default_value = (0.0, 0.0, 0.0, 1.0)
    bn.inputs['Strength'].default_value = 1.0
    on = world.node_tree.nodes.new(type='ShaderNodeOutputWorld')
    world.node_tree.links.new(bn.outputs['Background'], on.inputs['Surface'])

    ld = bpy.data.lights.new('light', type='POINT')
    ld.energy = 10.0
    lo = bpy.data.objects.new('light', ld)
    lo.location = (0, 0, 3)
    bpy.context.collection.objects.link(lo)

    mat = bpy.data.materials.new('mat')
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    for n in nodes: nodes.remove(n)

    e = nodes.new(type='ShaderNodeEmission')
    e.inputs['Color'].default_value = (1.0, 0.1, 0.1, 1.0)  # Red
    e.inputs['Strength'].default_value = 3.0
    on_m = nodes.new(type='ShaderNodeOutputMaterial')
    mat.node_tree.links.new(e.outputs['Emission'], on_m.inputs['Surface'])

    bpy.ops.mesh.primitive_circle_add(vertices=24, radius=0.8, fill_type='TRIFAN')
    obj = bpy.context.active_object
    obj.location.z = 0.01
    obj.data.materials.clear()
    obj.data.materials.append(mat)

    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE_NEXT'
    s.render.resolution_x = 512; s.render.resolution_y = 512
    s.render.film_transparent = False
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    s.render.image_settings.view_transform = view_transform
    s.render.image_settings.look = look
    s.render.image_settings.filter_size = use_filter

    # Also try color_space_settings on the world and material for good measure
    try:
        world.color_space = 'Linear'
    except:
        pass
    
    s.render.filepath = os.path.join(out, f'{name}.png')
    print(f"Rendering {name} (view={view_transform}, look={look}, filter={use_filter})...", end=" ", flush=True)
    bpy.ops.render.render(write_still=True)
    print(f"→ {os.path.getsize(os.path.join(out, name+'.png'))}B")

# Default (Filmic/AgX)
render_test('default_filmic', 'Filmic', 'None', 0.5)

# Standard
render_test('standard', 'Standard', 'None', 0.5)

# Raw
render_test('raw', 'Raw', 'None', 0.5)

# Raw + no filter
render_test('raw_nofilter', 'Raw', 'None', 0.0)

# Aces
render_test('aces', 'ACES', 'None', 0.5)

# Pink color to be extra visible
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)
for l in list(bpy.data.lights): bpy.data.lights.remove(l)

cd = bpy.data.cameras.new('cam')
cd.type = 'ORTHO'; cd.ortho_scale = 2.0
co = bpy.data.objects.new('cam', cd)
co.location = (0, 0, 4); co.rotation_euler = (1.570796, 0, 0)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

world = bpy.data.worlds.new('world')
bpy.context.scene.world = world

ld = bpy.data.lights.new('light', type='POINT')
ld.energy = 10.0
lo = bpy.data.objects.new('light', ld)
lo.location = (0, 0, 3)
bpy.context.collection.objects.link(lo)

mat = bpy.data.materials.new('mat')
mat.use_nodes = True
nodes = mat.node_tree.nodes
for n in nodes: nodes.remove(n)

e = nodes.new(type='ShaderNodeEmission')
e.inputs['Color'].default_value = (1.0, 0.0, 0.5, 1.0)  # Pink/magenta
e.inputs['Strength'].default_value = 5.0
on_m = nodes.new(type='ShaderNodeOutputMaterial')
mat.node_tree.links.new(e.outputs['Emission'], on_m.inputs['Surface'])

bpy.ops.mesh.primitive_circle_add(vertices=24, radius=0.8, fill_type='TRIFAN')
obj = bpy.context.active_object
obj.location.z = 0.01
obj.data.materials.clear()
obj.data.materials.append(mat)

s = bpy.context.scene
s.render.engine = 'BLENDER_EEVEE_NEXT'
s.render.resolution_x = 512; s.render.resolution_y = 512
s.render.film_transparent = False
s.render.image_settings.file_format = 'PNG'
s.render.image_settings.color_mode = 'RGBA'
s.render.image_settings.view_transform = 'Raw'
s.render.image_settings.look = 'None'
s.render.image_settings.filter_size = 0.0
s.render.filepath = os.path.join(out, 'raw_pink.png')
print(f"Rendering raw_pink (pink, emission 5.0, raw, no filter)...", end=" ", flush=True)
bpy.ops.render.render(write_still=True)
print(f"→ {os.path.getsize(os.path.join(out, 'raw_pink.png'))}B")

print("\nChecking results...")
PYEOF
