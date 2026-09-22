import bpy, math

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)

# Red cube with diffuse material
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.5))
cube = bpy.context.active_object
mat = bpy.data.materials.new("red_plain")
mat.diffuse_color = (1.0, 0.0, 0.0, 1.0)
cube.data.materials.append(mat)

# Lights
for pos, e in [((3, 3, 5), 50), ((-3, 0, 3), 30)]:
    ld = bpy.data.lights.new("l", type="POINT")
    ld.energy = e
    lo = bpy.data.objects.new("l", ld)
    lo.location = pos
    bpy.context.collection.objects.link(lo)

# Camera
cd = bpy.data.cameras.new("cam")
cd.type = "ORTHO"
cd.ortho_scale = 3.0
co = bpy.data.objects.new("cam", cd)
co.location = (0, 0, 5)
co.rotation_euler = (math.radians(90), 0, 0)
bpy.context.collection.objects.link(co)
bpy.context.scene.camera = co

bpy.context.scene.render.resolution_x = 256
bpy.context.scene.render.resolution_y = 256
bpy.context.scene.render.image_settings.file_format = "PNG"
bpy.context.scene.render.image_settings.color_mode = "RGB"   # NO alpha
bpy.context.scene.render.film_transparent = False             # opaque background
bpy.context.scene.render.filepath = "/home/leandi-duplessis/github/workspaces/Hero-sArena/blender_assets/test_rgb.png"

print("Rendering test_rgb.png (RGB, no transparency)...")
bpy.ops.render.render(write_still=True)
print("Done.")
