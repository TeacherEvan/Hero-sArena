import bpy

mat = bpy.data.materials.new("test")
mat.use_nodes = True
nodes = mat.node_tree.nodes
links = mat.node_tree.links

for node in nodes:
    nodes.remove(node)

output = nodes.new(type='ShaderNodeOutputMaterial')
output.location = (200, 0)

bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
bsdf.location = (0, 0)

print("=== BSDF inputs ===")
for inp in bsdf.inputs:
    print(f"  '{inp.name}' (type: {inp.type}, default: {inp.default_value})")

print()
print("=== Output inputs ===")
for inp in output.inputs:
    print(f"  '{inp.name}' (type: {inp.type})")

# Try to set emission
print()
print("=== Testing emission ===")
try:
    bsdf.inputs['Emission'].default_value = (1.0, 0.0, 0.0, 1.0)
    bsdf.inputs['Emission Strength'].default_value = 1.0
    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    print("Emission set OK via 'Emission' key")
except KeyError as e:
    print(f"KeyError on 'Emission': {e}")
    # Try alternate names
    for name in ['Emission Color', 'EmissionColor', 'emission', 'Color']:
        if name in bsdf.inputs:
            print(f"  Found: '{name}'")
            try:
                bsdf.inputs[name].default_value = (1.0, 0.0, 0.0, 1.0)
                print(f"  Set OK")
            except Exception as e2:
                print(f"  Error: {e2}")
