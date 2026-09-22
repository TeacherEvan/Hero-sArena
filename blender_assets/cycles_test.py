"""Test: does Blender 5.2 headless render color with CYCLES engine?"""
import bpy, os

out = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/cycles_test"
os.makedirs(out, exist_ok=True)

def render(name, engine, eevee_settings=None):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)
    for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)
    for l in list(bpy.data.lights): bpy.data.lights.remove(l)

    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'; cd.ortho_scale = 2.0
    co = bpy.data.objects.new('cam', cd)
    co.location = (0, 0, 5); co.rotation_euler = (1.570796, 0, 0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co

    world = bpy.data.worlds.new('world')
    bpy.context.scene.world = world
    world.use_nodes = True
    for n in world.node_tree.nodes: world.node_tree.nodes.remove(n)

    # Point light
    ld = bpy.data.lights.new('light', type='POINT')
    ld.energy = 10.0
    lo = bpy.data.objects.new('light', ld)
    lo.location = (0, 0, 3)
    bpy.context.collection.objects.link(lo)

    mat = bpy.data.materials.new('mat')
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    for n in nodes: nodes.remove(n)

    # Emission material  
    e = nodes.new(type='ShaderNodeEmission')
    e.inputs['Color'].default_value = (1.0, 0.1, 0.1, 1.0)
    e.inputs['Strength'].default_value = 3.0
    on = nodes.new(type='ShaderNodeOutputMaterial')
    mat.node_tree.links.new(e.outputs['Emission'], on.inputs['Surface'])

    bpy.ops.mesh.primitive_circle_add(vertices=24, radius=0.8, fill_type='TRIFAN')
    obj = bpy.context.active_object
    obj.location.z = 0.01
    obj.data.materials.clear()
    obj.data.materials.append(mat)

    s = bpy.context.scene
    s.render.engine = engine
    if engine == 'CYCLES':
        s.cycles.samples = 64
        s.cycles.use_denoising = False
    s.render.resolution_x = 512; s.render.resolution_y = 512
    s.render.film_transparent = False
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    s.render.filepath = os.path.join(out, f'{name}.png')
    
    print(f"Rendering {name} ({engine})...", end=" ", flush=True)
    bpy.ops.render.render(write_still=True)
    sz = os.path.getsize(os.path.join(out, f'{name}.png'))
    print(f"→ {sz}B")

# Test EEVEE
render('eevee', 'BLENDER_EEVEE_NEXT')

# Test CYCLES
render('cycles', 'CYCLES')

# Test EEVEE with flat shading
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)
for l in list(bpy.data.lights): bpy.data.lights.remove(l)

cd = bpy.data.cameras.new('cam')
cd.type = 'ORTHO'; cd.ortho_scale = 2.0
co = bpy.data.objects.new('cam', cd)
co.location = (0, 0, 5); co.rotation_euler = (1.570796, 0, 0)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

world = bpy.data.worlds.new('world')
bpy.context.scene.world = world
world.use_nodes = True
for n in world.node_tree.nodes: world.node_tree.nodes.remove(n)

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
e.inputs['Color'].default_value = (1.0, 0.1, 0.1, 1.0)
e.inputs['Strength'].default_value = 5.0
on = nodes.new(type='ShaderNodeOutputMaterial')
mat.node_tree.links.new(e.outputs['Emission'], on.inputs['Surface'])

bpy.ops.mesh.primitive_circle_add(vertices=24, radius=0.8, fill_type='TRIFAN')
obj = bpy.context.active_object
obj.location.z = 0.01
obj.data.materials.clear()
obj.data.materials.append(mat)

# Flat shading
for f in obj.data.polygons:
    f.use_smooth = False

s = bpy.context.scene
s.render.engine = 'BLENDER_EEVEE_NEXT'
s.render.resolution_x = 512; s.render.resolution_y = 512
s.render.film_transparent = False
s.render.image_settings.file_format = 'PNG'
s.render.image_settings.color_mode = 'RGBA'
s.render.filepath = os.path.join(out, 'eevee_flat.png')
print(f"Rendering eevee_flat...", end=" ", flush=True)
bpy.ops.render.render(write_still=True)
print(f"→ {os.path.getsize(os.path.join(out, 'eevee_flat.png'))}B")

print("\nDone!")
