import bpy

# Check lights.new signature
print("Lights type:", type(bpy.data.lights))
print("Lights dir:", [x for x in dir(bpy.types.BlendDataLights) if not x.startswith('_')])

# Try creating a point light
data = bpy.data.lights.new(name="test_pt", type='POINT')
print("Created point light:", data.name, "type:", data.type)

# Try area light
data2 = bpy.data.lights.new(name="test_area", type='AREA')
print("Created area light:", data2.name, "type:", data2.type)

# Try spot
data3 = bpy.data.lights.new(name="test_spot", type='SPOT')
print("Created spot light:", data3.name, "type:", data3.type)
