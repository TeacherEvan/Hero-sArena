import bpy
import math

# Clear
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
for m in bpy.data.materials:
    bpy.data.materials.remove(m)

# Red cube
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.5))
cube = bpy.context.active_object
mat = bpy.data.materials.new("red")
mat.use_nodes = True
nodes = mat.node_tree.nodes
for n in nodes:
    nodes.remove(n)
out = nodes.new("ShaderNodeOutputMaterial")
bsdf = nodes.new("ShaderNodeBsdfPrincipled")
bsdf.inputs['Base Color'].default_value = (1.0, 0.0, 0.0, 1.0)
mat.node_tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
cube.data.materials.append(mat)

# Lights
for pos, e in [((3,3,5), 50), ((-3,0,3), 30)]:
    ld = bpy.data.lights.new("l", type='POINT')
    ld.energy = e
    lo = bpy.data.objects.new("l", ld)
    lo.location = pos
    bpy.context.collection.objects.link(lo)

# Camera - try exact top-down
cd = bpy.data.cameras.new("cam")
cd.type = 'ORTHO'
cd.ortho_scale = 3.0
co = bpy.data.objects.new("cam", cd)
co.location = (0, 0, 5)
co.rotation_euler = (math.radians(90), 0, 0)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

# Render settings - overrides
bpy.context.scene.render.resolution_x = 256
bpy.context.scene.render.resolution_y = 256
bpy.context.scene.render.image_settings.file_format = 'PNG'
bpy.context.scene.render.image_settings.color_mode = 'RGBA'
bpy.context.scene.render.film_transparent = True
bpy.context.scene.render.filepath = '/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/test_cube2.png'

print("About to render...")
bpy.ops.render.render(write_still=True)
print("Done")

# Also verify render settings took effect
print("Resolution:", bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y)
print("File path:", bpy.context.scene.render.filepath)
print("Film transparent:", bpy.context.scene.render.film_transparent)
