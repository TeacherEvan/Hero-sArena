import bpy

scene = bpy.context.scene
scene.render.resolution_x = 128
scene.render.resolution_y = 128
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = True
scene.render.engine = "BLENDER_EEVEE"

# Test ShaderNodeOutputMaterial sockets
mat = bpy.data.materials.new("test")
mat.use_nodes = True
nodes = mat.node_tree.nodes
links = mat.node_tree.links
nodes.clear()

bsdf = nodes.new("ShaderNodeBsdfPrincipled")
output = nodes.new("ShaderNodeOutputMaterial")

print("OutputMaterial inputs:", [i.name for i in output.inputs])

# Test emission
emit = nodes.new("ShaderNodeEmission")
emit.inputs["Color"].default_value = (1.0, 0.0, 0.0, 1.0)
emit.inputs["Strength"].default_value = 1.0

print("Emission outputs:", [o.name for o in emit.outputs])
print("Output inputs after:", [i.name for i in output.inputs])

# Link emission to surface
try:
    links.new(emit.outputs["Emission"], output.inputs["Surface"])
    print("Link Emission->Surface OK")
except Exception as e:
    print(f"Link failed: {e}")

# Try BSDF
try:
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    print("Link BSDF->Surface OK")
except Exception as e:
    print(f"Link BSDF failed: {e}")

# Check if there's a separate Emission input
for inp in output.inputs:
    print(f"  Input: {inp.name} ({inp.type})")
