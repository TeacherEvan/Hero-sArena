"""Clean Blender 5.2 color test: EEVEE + CYCLES + various color mgmt"""
import bpy, os

out = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/v2_color"
os.makedirs(out, exist_ok=True)

def setup_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)
    for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)
    for l in list(bpy.data.lights): bpy.data.lights.remove(l)
    for o in list(bpy.data.objects):
        if o.type == 'LIGHT': bpy.data.objects.remove(o)
    # Camera
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'; cd.ortho_scale = 2.0
    co = bpy.data.objects.new('cam', cd)
    co.location = (0, 0, 4); co.rotation_euler = (1.570796, 0, 0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co
    # World background - black
    world = bpy.data.worlds.new('world')
    bpy.context.scene.world = world
    world.use_nodes = True
    for n in world.node_tree.nodes: world.node_tree.nodes.remove(n)
    bg = world.node_tree.nodes.new(type='ShaderNodeBackground')
    bg.inputs['Color'].default_value = (0.0, 0.0, 0.0, 1.0)
    bg.inputs['Strength'].default_value = 1.0
    ow = world.node_tree.nodes.new(type='ShaderNodeOutputWorld')
    world.node_tree.links.new(bg.outputs['Background'], ow.inputs['Surface'])
    # Light
    ld = bpy.data.lights.new('light', type='POINT')
    ld.energy = 10.0
    lo = bpy.data.objects.new('light', ld)
    lo.location = (0, 0, 3)
    bpy.context.collection.objects.link(lo)
    # Red emission circle
    mat = bpy.data.materials.new('mat')
    mat.use_nodes = True
    for n in mat.node_tree.nodes: mat.node_tree.nodes.remove(n)
    em = mat.node_tree.nodes.new(type='ShaderNodeEmission')
    em.inputs['Color'].default_value = (1.0, 0.0, 0.0, 1.0)
    em.inputs['Strength'].default_value = 10.0
    om = mat.node_tree.nodes.new(type='ShaderNodeOutputMaterial')
    mat.node_tree.links.new(em.outputs['Emission'], om.inputs['Surface'])
    bpy.ops.mesh.primitive_circle_add(vertices=32, radius=0.8, fill_type='TRIFAN')
    obj = bpy.context.active_object
    obj.location.z = 0.01
    obj.data.materials.clear()
    obj.data.materials.append(mat)

def render(name, engine, vt, look=None):
    setup_scene()
    s = bpy.context.scene
    s.render.engine = engine
    s.render.resolution_x = 512; s.render.resolution_y = 512
    s.render.film_transparent = False
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    try:
        s.render.image_settings.view_transform = vt
    except: pass
    try:
        s.render.image_settings.look = look or 'None'
    except: pass
    s.render.filepath = os.path.join(out, f'{name}.png')
    bpy.ops.render.render(write_still=True)
    sz = os.path.getsize(os.path.join(out, f'{name}.png'))
    print(f"  {name}: {sz}B")

print("Blender 5.2 color test:")
print("EEVEE:")
for vt in ['Raw', 'Standard', 'AgX', 'Filmic']:
    render(f'eevee_{vt}', 'BLENDER_EEVEE', vt)

print("CYCLES:")
for vt in ['Raw', 'Standard', 'Filmic']:
    render(f'cycles_{vt}', 'CYCLES', vt, 'None')
    # need to set cycles samples
    bpy.context.scene.cycles.samples = 64

print("\nChecking which ones have RED pixels...")
PYEOF
