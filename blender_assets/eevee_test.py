"""Quick test: does EEVEE actually render objects in Blender 5.2 headless?"""
import bpy, os

out = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/eevee_test"
os.makedirs(out, exist_ok=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)

# Camera
cd = bpy.data.cameras.new('cam')
cd.type = 'ORTHO'; cd.ortho_scale = 2.0
co = bpy.data.objects.new('cam', cd)
co.location = (0, 0, 4); co.rotation_euler = (1.570796, 0, 0)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

# World — try colored background
world = bpy.data.worlds.new('world')
bpy.context.scene.world = world
world.use_nodes = True
for n in world.node_tree.nodes: world.node_tree.nodes.remove(n)
bg = world.node_tree.nodes.new(type='ShaderNodeBackground')
bg.inputs['Color'].default_value = (0.1, 0.1, 0.1, 1.0)
bg.inputs['Strength'].default_value = 1.0
ow = world.node_tree.nodes.new(type='ShaderNodeOutputWorld')
world.node_tree.links.new(bg.outputs['Background'], ow.inputs['Surface'])

# Light
ld = bpy.data.lights.new('light', type='POINT')
ld.energy = 50.0  # Very bright
lo = bpy.data.objects.new('light', ld)
lo.location = (0, 0, 3)
bpy.context.collection.objects.link(lo)

# Material — Diffuse (should work with light)
mat = bpy.data.materials.new('diffuse_red')
mat.use_nodes = True
for n in mat.node_tree.nodes: mat.node_tree.nodes.remove(n)
bsdf = mat.node_tree.nodes.new(type='ShaderNodeBsdfDiffuse')
bsdf.inputs['Color'].default_value = (1.0, 0.0, 0.0, 1.0)
bsdf.inputs['Roughness'].default_value = 0.5
out_m = mat.node_tree.nodes.new(type='ShaderNodeOutputMaterial')
mat.node_tree.links.new(bsdf.outputs['BSDF'], out_m.inputs['Surface'])

# Red circle
bpy.ops.mesh.primitive_circle_add(vertices=32, radius=0.8, fill_type='TRIFAN')
obj = bpy.context.active_object
obj.location.z = 0.01
obj.data.materials.clear()
obj.data.materials.append(mat)

# Render
s = bpy.context.scene
s.render.engine = 'BLENDER_EEVEE'
s.render.resolution_x = 512; s.render.resolution_y = 512
s.render.film_transparent = False
s.render.image_settings.file_format = 'PNG'
s.render.image_settings.color_mode = 'RGBA'
s.render.filepath = os.path.join(out, 'eevee_diffuse.png')
bpy.ops.render.render(write_still=True)
print(f"EEVEE diffuse: {os.path.getsize(os.path.join(out, 'eevee_diffuse.png'))}B")

# Now try CYCLES
s.render.engine = 'CYCLES'
s.cycles.samples = 64
s.render.filepath = os.path.join(out, 'cycles_diffuse.png')
bpy.ops.render.render(write_still=True)
print(f"CYCLES diffuse: {os.path.getsize(os.path.join(out, 'cycles_diffuse.png'))}B")

print("Done!")
