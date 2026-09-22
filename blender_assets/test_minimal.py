import bpy
import math

# Minimal test: a red cube on the ground, camera looking down
clear_test = True
if clear_test:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in bpy.data.materials:
        bpy.data.materials.remove(m)

# Cube
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

# Camera
cd = bpy.data.cameras.new("cam")
cd.type = 'ORTHO'
cd.ortho_scale = 3.0
co = bpy.data.objects.new("cam", cd)
co.location = (0, 0, 5)
co.rotation_euler = (math.radians(90), 0, 0)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

# Render settings
bpy.context.scene.render.resolution_x = 256
bpy.context.scene.render.resolution_y = 256
bpy.context.scene.render.image_settings.file_format = 'PNG'
bpy.context.scene.render.image_settings.color_mode = 'RGBA'
bpy.context.scene.render.film_transparent = True
bpy.context.scene.render.filepath = '/tmp/test_cube.png'

print("Objects:", [o.name for o in bpy.context.scene.objects])
print("Camera:", co.name, co.location, co.rotation_euler)
print("Cube:", cube.name, cube.location, "bounds:", cube.bound_box)

bpy.ops.render.render(write_still=True)
print("Rendered to /tmp/test_cube.png")
