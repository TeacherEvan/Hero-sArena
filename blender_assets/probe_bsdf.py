import bpy

mat = bpy.data.materials.new("probe")
mat.use_nodes = True
nodes = mat.node_tree.nodes
links = mat.node_tree.links

for n in list(nodes):
    nodes.remove(n)

out = nodes.new("ShaderNodeOutputMaterial")
out.location = (300, 0)

bsdf = nodes.new("ShaderNodeBsdfPrincipled")
bsdf.location = (0, 0)

print("=== All BSDF inputs (name | type | default) ===")
for inp in bsdf.inputs:
    dv = inp.default_value
    dv_str = repr(dv) if not isinstance(dv, (list, tuple)) else f"len={len(dv)}"
    print(f"  '{inp.name}'  type={inp.type}  default={dv_str}")

print()
print("=== All BSDF outputs ===")
for o in bsdf.outputs:
    print(f"  '{o.name}'")
