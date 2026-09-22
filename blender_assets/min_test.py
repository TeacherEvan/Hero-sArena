"""Minimal Blender 5.2 test: does a Principled BSDF material render with color?"""
import bpy, os

out = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/min_test"
os.makedirs(out, exist_ok=True)

# Clean
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

# World = pure black, NO background shader (film_transparent will be False)
world = bpy.data.worlds.new('bg')
bpy.context.scene.world = world
world.use_nodes = True
for n in world.node_tree.nodes: world.node_tree.nodes.remove(n)
# No background node = pure black void

# Material with Principled BSDF
mat = bpy.data.materials.new('red')
mat.use_nodes = True
nodes = mat.node_tree.nodes
for n in nodes: nodes.remove(n)
bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
bsdf.inputs['Base Color'].default_value = (1.0, 0.1, 0.1, 1.0)
bsdf.inputs['Roughness'].default_value = 0.8
out_n = nodes.new(type='ShaderNodeOutputMaterial')
mat.node_tree.links.new(bsdf.outputs['BSDF'], out_n.inputs['Surface'])

# Red circle
bpy.ops.mesh.primitive_circle_add(vertices=24, radius=0.8, fill_type='TRIFAN')
obj = bpy.context.active_object
obj.location.z = 0.01
obj.data.materials.clear()
obj.data.materials.append(mat)

# Render settings
s = bpy.context.scene
s.render.resolution_x = 512
s.render.resolution_y = 512
s.render.film_transparent = False
s.render.image_settings.file_format = 'PNG'
s.render.image_settings.color_mode = 'RGBA'
s.render.filepath = os.path.join(out, 'test.png')
bpy.ops.render.render(write_still=True)
print(f"Rendered: {os.path.join(out, 'test.png')}")
print(f"Size: {os.path.getsize(os.path.join(out, 'test.png'))} bytes")
