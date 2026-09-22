"""Quick test: Principled BSDF vs Emission in headless Blender 5.2"""
import bpy, os

out = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/emission_test"
os.makedirs(out, exist_ok=True)

def render_scene(name, mat_type):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)
    for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)

    # Camera
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'; cd.ortho_scale = 2.0
    co = bpy.data.objects.new('cam', cd)
    co.location = (0, 0, 3); co.rotation_euler = (1.570796, 0, 0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co

    # Black void world
    world = bpy.data.worlds.new('void')
    bpy.context.scene.world = world
    world.use_nodes = True
    for n in world.node_tree.nodes: world.node_tree.nodes.remove(n)

    if mat_type == 'principled':
        # Principled BSDF — needs light to show color
        mat = bpy.data.materials.new('p_mat')
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        for n in nodes: nodes.remove(n)
        bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
        bsdf.inputs['Base Color'].default_value = (1.0, 0.1, 0.1, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.8
        on = nodes.new(type='ShaderNodeOutputMaterial')
        mat.node_tree.links.new(bsdf.outputs['BSDF'], on.inputs['Surface'])
    elif mat_type == 'emission':
        # Emission — always shows color regardless of lighting
        mat = bpy.data.materials.new('e_mat')
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        for n in nodes: nodes.remove(n)
        emit = nodes.new(type='ShaderNodeEmission')
        emit.inputs['Color'].default_value = (1.0, 0.1, 0.1, 1.0)
        emit.inputs['Strength'].default_value = 1.0
        on = nodes.new(type='ShaderNodeOutputMaterial')
        mat.node_tree.links.new(emit.outputs['Emission'], on.inputs['Surface'])

    # Red circle
    bpy.ops.mesh.primitive_circle_add(vertices=24, radius=0.8, fill_type='TRIFAN')
    obj = bpy.context.active_object
    obj.location.z = 0.01
    obj.data.materials.clear()
    obj.data.materials.append(mat)

    s = bpy.context.scene
    s.render.resolution_x = 512; s.render.resolution_y = 512
    s.render.film_transparent = False
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    s.render.filepath = os.path.join(out, f'{name}.png')
    bpy.ops.render.render(write_still=True)
    print(f"Rendered {name}.png")

render_scene('principled', 'principled')
render_scene('emission', 'emission')

print(f"\nFiles:")
for f in os.listdir(out):
    sz = os.path.getsize(os.path.join(out, f))
    print(f"  {f}: {sz} bytes")
