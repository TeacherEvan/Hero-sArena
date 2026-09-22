"""Find what actually renders color in Blender 5.2 headless"""
import bpy, os

out = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/color_test"
os.makedirs(out, exist_ok=True)

def render_test(name, world_setup, light_setup, mat_type, strength=None):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)
    for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)
    for l in list(bpy.data.lights): bpy.data.lights.remove(l)
    for o in list(bpy.data.objects):
        if o.type == 'LIGHT':
            bpy.data.objects.remove(o)

    # Camera
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'; cd.ortho_scale = 2.0
    co = bpy.data.objects.new('cam', cd)
    co.location = (0, 0, 5); co.rotation_euler = (1.570796, 0, 0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co

    # World
    world = bpy.data.worlds.new('world')
    bpy.context.scene.world = world
    world.use_nodes = True
    for n in world.node_tree.nodes: world.node_tree.nodes.remove(n)

    if world_setup == 'black_void':
        pass  # no nodes = pure black
    elif world_setup == 'black_bg':
        bn = world.node_tree.nodes.new(type='ShaderNodeBackground')
        bn.inputs['Color'].default_value = (0.0, 0.0, 0.0, 1.0)
        bn.inputs['Strength'].default_value = 1.0
        on = world.node_tree.nodes.new(type='ShaderNodeOutputWorld')
        world.node_tree.links.new(bn.outputs['Background'], on.inputs['Surface'])

    # Light
    if light_setup == 'none':
        pass
    elif light_setup == 'point':
        ld = bpy.data.lights.new('light', type='POINT')
        ld.energy = 10.0
        lo = bpy.data.objects.new('light', ld)
        lo.location = (0, 0, 3)
        bpy.context.collection.objects.link(lo)
    elif light_setup == 'sun':
        ld = bpy.data.lights.new('light', type='SUN')
        ld.energy = 5.0
        lo = bpy.data.objects.new('light', ld)
        lo.rotation_euler = (0.5, 0, 0)
        bpy.context.collection.objects.link(lo)

    # Material
    mat = bpy.data.materials.new('mat')
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    for n in nodes: nodes.remove(n)

    if mat_type == 'emission':
        e = nodes.new(type='ShaderNodeEmission')
        e.inputs['Color'].default_value = (1.0, 0.1, 0.1, 1.0)
        e.inputs['Strength'].default_value = strength or 1.0
        on = nodes.new(type='ShaderNodeOutputMaterial')
        mat.node_tree.links.new(e.outputs['Emission'], on.inputs['Surface'])
    elif mat_type == 'diffuse':
        md = nodes.new(type='ShaderNodeBsdfDiffuse')
        md.inputs['Color'].default_value = (1.0, 0.1, 0.1, 1.0)
        md.inputs['Roughness'].default_value = 0.5
        on = nodes.new(type='ShaderNodeOutputMaterial')
        mat.node_tree.links.new(md.outputs['BSDF'], on.inputs['Surface'])
    elif mat_type == 'principled':
        bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
        bsdf.inputs['Base Color'].default_value = (1.0, 0.1, 0.1, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.5
        on = nodes.new(type='ShaderNodeOutputMaterial')
        mat.node_tree.links.new(bsdf.outputs['BSDF'], on.inputs['Surface'])

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
    print(f"  ✓ {name}.png ({os.path.getsize(os.path.join(out, name+'.png'))}B)")

print("Testing color rendering in Blender 5.2 headless...")
for ws in ['black_void', 'black_bg']:
    for ls in ['none', 'point', 'sun']:
        for mt in ['emission', 'diffuse', 'principled']:
            if mt == 'emission':
                for st in [1.0, 5.0, 20.0]:
                    render_test(f"{ws}_{ls}_{mt}_{st}", ws, ls, mt, st)
            else:
                render_test(f"{ws}_{ls}_{mt}", ws, ls, mt)

print("\nDone!")
