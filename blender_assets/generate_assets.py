#!/usr/bin/env python3
"""
Hero's Arena — Blender Asset Generation (Phase 1)
===================================================
Procedurally generates top-down 2D sprite assets for the Godot arena shooter.
Uses Blender 5.2 headless mode. Outputs PNGs with transparency.

Entities covered:
  - 4 Heroes: Atlas, Zephyr, Synapse, Volt
  - 10 Enemies: Drone, Brute, Sprinter, Artillery, Shielder, Healer, 
               Exploder, Burrower, Parasite, Apex
  - Projectiles (standard + ability variants)
  - Environment: arena floor tile, destructible objects (crate, barrel, wall)
  - Decals: blood splat, scorch mark, crater mark, acid pool, explosion scorch
  - VFX: hit flash, explosion burst, smoke puff, spark particles

Run:  blender --background --python generate_assets.py -- --output-dir ./assets
"""

import bpy
import bmesh
import math
import os
import sys
import random
from mathutils import Vector, Color, Matrix

# ── Configuration ─────────────────────────────────────────────────────────────
RESOLUTION = 512  # render resolution (square)
OUTPUT_DIR = None  # set from CLI args

# Color palettes per entity (top-down view, flat-shaded)
PALETTE = {
    # Heroes
    "atlas":  {"body": (0.85, 0.35, 0.20), "accent": (0.60, 0.15, 0.10), "metal": (0.70, 0.60, 0.50), "eye": (1.0, 1.0, 1.0),   "outline": (0.25, 0.10, 0.05)},
    "zephyr": {"body": (0.20, 0.60, 0.85), "accent": (0.10, 0.40, 0.70), "metal": (0.75, 0.80, 0.90), "eye": (1.0, 1.0, 1.0),   "outline": (0.05, 0.20, 0.35)},
    "synapse":{"body": (0.70, 0.25, 0.70), "accent": (0.50, 0.10, 0.50), "metal": (0.50, 0.70, 0.50), "eye": (0.30, 1.0, 0.30), "outline": (0.30, 0.10, 0.30)},
    "volt":   {"body": (0.90, 0.75, 0.10), "accent": (0.70, 0.55, 0.00), "metal": (0.95, 0.90, 0.70), "eye": (1.0, 1.0, 0.20), "outline": (0.40, 0.30, 0.00)},
    
    # Enemies
    "drone":    {"body": (0.50, 0.50, 0.55), "accent": (0.35, 0.35, 0.40), "metal": (0.60, 0.60, 0.65), "eye": (1.0, 0.2, 0.2),   "outline": (0.15, 0.15, 0.20)},
    "brute":    {"body": (0.55, 0.15, 0.10), "accent": (0.35, 0.08, 0.05), "metal": (0.45, 0.35, 0.30), "eye": (1.0, 0.8, 0.2),   "outline": (0.15, 0.05, 0.03)},
    "sprinter": {"body": (0.85, 0.30, 0.15), "accent": (0.60, 0.15, 0.05), "metal": (0.70, 0.60, 0.50), "eye": (1.0, 1.0, 0.3),   "outline": (0.20, 0.08, 0.03)},
    "artillery":{"body": (0.30, 0.30, 0.35), "accent": (0.20, 0.20, 0.25), "metal": (0.50, 0.50, 0.55), "eye": (1.0, 0.5, 0.1),   "outline": (0.10, 0.10, 0.12)},
    "shielder": {"body": (0.25, 0.25, 0.60), "accent": (0.15, 0.15, 0.40), "metal": (0.55, 0.60, 0.80), "eye": (1.0, 0.9, 0.3),   "outline": (0.08, 0.08, 0.25)},
    "healer":   {"body": (0.20, 0.60, 0.25), "accent": (0.10, 0.40, 0.15), "metal": (0.50, 0.75, 0.55), "eye": (1.0, 1.0, 0.5),   "outline": (0.08, 0.20, 0.10)},
    "exploder": {"body": (0.85, 0.15, 0.10), "accent": (0.60, 0.05, 0.02), "metal": (0.50, 0.30, 0.25), "eye": (1.0, 0.4, 0.1),   "outline": (0.20, 0.03, 0.02)},
    "burrower": {"body": (0.45, 0.35, 0.15), "accent": (0.25, 0.18, 0.08), "metal": (0.55, 0.45, 0.30), "eye": (0.8, 0.6, 0.1),   "outline": (0.12, 0.08, 0.03)},
    "parasite": {"body": (0.40, 0.15, 0.40), "accent": (0.25, 0.08, 0.25), "metal": (0.50, 0.30, 0.50), "eye": (1.0, 0.1, 0.8),   "outline": (0.12, 0.04, 0.12)},
    "apex":     {"body": (0.15, 0.10, 0.15), "accent": (0.08, 0.05, 0.08), "metal": (0.60, 0.15, 0.15), "eye": (1.0, 0.1, 0.1),   "outline": (0.05, 0.03, 0.05)},
    
    # Destructibles
    "crate":    {"body": (0.50, 0.30, 0.15), "accent": (0.35, 0.20, 0.10), "metal": (0.40, 0.25, 0.12), "eye": (0.0, 0.0, 0.0),    "outline": (0.15, 0.08, 0.03)},
    "barrel":   {"body": (0.30, 0.30, 0.35), "accent": (0.22, 0.22, 0.27), "metal": (0.45, 0.45, 0.50), "eye": (0.0, 0.0, 0.0),    "outline": (0.10, 0.10, 0.12)},
    "wall":     {"body": (0.55, 0.50, 0.45), "accent": (0.40, 0.35, 0.30), "metal": (0.50, 0.45, 0.40), "eye": (0.0, 0.0, 0.0),    "outline": (0.15, 0.12, 0.10)},
    
    # Floor
    "floor":    {"body": (0.25, 0.25, 0.28), "accent": (0.20, 0.20, 0.22), "metal": (0.0, 0.0, 0.0),    "eye": (0.0, 0.0, 0.0),    "outline": (0.0, 0.0, 0.0)},
}

# Entity sizes (radius in Blender units; sprite will be rendered at orthographic scale)
SIZES = {
    "atlas": 1.0, "zephyr": 0.9, "synapse": 0.95, "volt": 0.85,
    "drone": 0.4, "brute": 1.2, "sprinter": 0.6, "artillery": 1.0,
    "shielder": 1.0, "healer": 0.7, "exploder": 0.6, "burrower": 0.7,
    "parasite": 0.3, "apex": 1.8,
    "crate": 0.6, "barrel": 0.5, "wall": (0.8, 1.5),
}

ASPECT = {
    "atlas": (1.0, 1.0), "zephyr": (1.0, 1.0), "synapse": (1.0, 1.0), "volt": (1.0, 1.0),
    "drone": (1.0, 1.0), "brute": (1.0, 1.1), "sprinter": (1.2, 0.8), "artillery": (1.0, 1.2),
    "shielder": (1.0, 1.0), "healer": (1.0, 1.1), "exploder": (1.0, 1.0), "burrower": (1.0, 1.3),
    "parasite": (1.0, 1.0), "apex": (1.0, 1.0),
    "crate": (1.0, 1.0), "barrel": (0.7, 1.0), "wall": (0.5, 1.0),
}


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def clear_scene():
    """Remove everything from the scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    # Remove all materials
    for mat in bpy.data.materials:
        bpy.data.materials.remove(mat)


def setup_camera_light():
    """Set up orthographic camera and lighting for top-down sprite rendering."""
    # Remove existing camera/lights
    for obj in bpy.context.scene.objects:
        if obj.type in ('CAMERA', 'LIGHT'):
            bpy.data.objects.remove(obj, do_unlink=True)
    
    # Orthographic camera looking down
    cam_data = bpy.data.cameras.new("sprite_cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 3.0
    cam_obj = bpy.data.objects.new("SpriteCam", cam_data)
    cam_obj.location = (0, 0, 10)
    cam_obj.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj
    
    # Apply resolution
    bpy.context.scene.render.resolution_x = RESOLUTION
    bpy.context.scene.render.resolution_y = RESOLUTION
    
    # Key light
    key_data = bpy.data.lights.new("KeyLight", type='POINT')
    key_data.energy = 50
    key_data.color = (1.0, 1.0, 1.0)
    key_obj = bpy.data.objects.new("KeyLight", key_data)
    key_obj.location = (2, 3, 8)
    bpy.context.collection.objects.link(key_obj)
    
    # Fill light
    fill_data = bpy.data.lights.new("FillLight", type='POINT')
    fill_data.energy = 20
    fill_data.color = (0.8, 0.8, 0.9)
    fill_obj = bpy.data.objects.new("FillLight", fill_data)
    fill_obj.location = (-2, -2, 6)
    bpy.context.collection.objects.link(fill_obj)
    
    # Rim/back light
    rim_data = bpy.data.lights.new("RimLight", type='POINT')
    rim_data.energy = 15
    rim_data.color = (0.5, 0.6, 0.8)
    rim_obj = bpy.data.objects.new("RimLight", rim_data)
    rim_obj.location = (0, -4, 7)
    bpy.context.collection.objects.link(rim_obj)


def create_circle_mesh(radius, subdivisions=32):
    """Create a circle mesh (Z-up, viewed from above)."""
    bpy.ops.mesh.primitive_circle_add(
        vertices=subdivisions,
        radius=radius,
        fill_type='NOTHING'
    )
    return bpy.context.active_object


def create_disk_mesh(radius, subdivisions=32):
    """Create a filled disk mesh."""
    bpy.ops.mesh.primitive_circle_add(
        vertices=subdivisions,
        radius=radius,
        fill_type='TRIFAN'
    )
    return bpy.context.active_object


def create_rect_mesh(w, h, subdivisions=4):
    """Create a rectangle plane."""
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.scale = (w / 2, h / 2, 0.01)
    bpy.ops.object.shade_flat()
    return obj


def create_round_rect_mesh(w, h, radius, subdivisions=8):
    """Create a rounded rectangle using a cylinder scaled to the right aspect."""
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=subdivisions * 4,
        radius=1.0,
        depth=0.02
    )
    obj = bpy.context.active_object
    obj.scale = (w / 2, h / 2, 1.0)
    bpy.ops.object.shade_flat()
    return obj


def assign_material(obj, color, name_prefix, metallic=0.0, roughness=0.7, emission=0.0):
    """Assign a flat material with the given color."""
    mat_name = f"{name_prefix}_mat"
    mat = bpy.data.materials.get(mat_name)
    if mat is None:
        mat = bpy.data.materials.new(mat_name)
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        
        # Clear default nodes
        for node in nodes:
            nodes.remove(node)
        
        # Output node
        output = nodes.new(type='ShaderNodeOutputMaterial')
        output.location = (200, 0)
        
        # Principled BSDF
        bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
        bsdf.location = (0, 0)
        bsdf.inputs['Base Color'].default_value = (*color, 1.0)
        bsdf.inputs['Metallic'].default_value = metallic
        bsdf.inputs['Roughness'].default_value = roughness
        bsdf.inputs['Emission Color'].default_value = (*color, emission)
        bsdf.inputs['Emission Strength'].default_value = emission
        
        links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)
    
    return mat


def render_sprite(filepath):
    """Render the current scene to a PNG."""
    bpy.context.scene.render.filepath = filepath
    bpy.context.scene.render.image_settings.file_format = 'PNG'
    bpy.context.scene.render.image_settings.color_mode = 'RGBA'
    bpy.context.scene.render.film_transparent = True
    bpy.ops.render.render(write_still=True)


def add_outline(obj, color, width=0.03):
    """Create a slightly larger duplicate behind the object as an outline."""
    outline_obj = obj.copy()
    outline_obj.data = obj.data.copy()
    outline_obj.location = obj.location.copy()
    outline_obj.rotation_euler = obj.rotation_euler.copy()
    outline_obj.scale = tuple(s * (1 + width / max(abs(s) for s in obj.scale[:2]) * 2) if max(abs(s) for s in obj.scale[:2]) > 0 else s for s in obj.scale)
    
    # Simpler: just scale up slightly
    scale_factor = 1.05
    outline_obj.scale = (obj.scale.x * scale_factor, obj.scale.y * scale_factor, obj.scale.z)
    
    # Move behind (lower Z)
    outline_obj.location.z = obj.location.z - 0.01
    bpy.context.collection.objects.link(outline_obj)
    
    assign_material(outline_obj, color, "outline")
    
    return outline_obj


def create_hero(name, palette_key, extra_details_fn=None):
    """Create a hero sprite. Returns the main object."""
    palette = PALETTE[palette_key]
    aspect = ASPECT.get(palette_key, (1.0, 1.0))
    size = SIZES.get(palette_key, 1.0)
    w = size * aspect[0]
    h = size * aspect[1]
    
    clear_scene()
    setup_camera_light()
    
    # Outline (behind)
    body = create_round_rect_mesh(w, h, min(w, h) * 0.15)
    body.location.z = 0.01
    assign_material(body, palette["outline"], f"{palette_key}_outline")
    
    # Main body
    body_mesh = create_round_rect_mesh(w, h, min(w, h) * 0.15)
    body_mesh.location.z = 0.02
    assign_material(body_mesh, palette["body"], palette_key)
    
    # Accent stripe/band
    accent = create_rect_mesh(w * 0.7, h * 0.15, 2)
    accent.location = (0, 0, 0.03)
    accent.rotation_euler = (0, 0, 0)
    assign_material(accent, palette["accent"], f"{palette_key}_accent")
    
    # Metal detail (shoulder pad / chest piece)
    metal = create_circle_mesh(min(w, h) * 0.3)
    metal.location = (0, 0, 0.04)
    assign_material(metal, palette["metal"], f"{palette_key}_metal", metallic=0.6, roughness=0.3)
    
    # Eyes (two small dots)
    eye_size = min(w, h) * 0.08
    for dx in (-eye_size * 2, eye_size * 2):
        eye = create_disk_mesh(eye_size, 12)
        eye.location = (dx, 0, 0.05)
        assign_material(eye, palette["eye"], f"{palette_key}_eye", emission=0.3)
    
    # Extra class-specific details
    if extra_details_fn:
        extra_details_fn(palette_key, palette, w, h)
    
    # Render
    filepath = os.path.join(OUTPUT_DIR, "heroes", f"{palette_key}.png")
    ensure_dir(os.path.dirname(filepath))
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    return body_mesh


def hero_atlas_details(pk, pal, w, h):
    """Atlas: add gauntlet details."""
    # Shoulder pads
    for sx in (-1, 1):
        pad = create_round_rect_mesh(w * 0.2, h * 0.15, w * 0.05)
        pad.location = (sx * w * 0.35, 0, 0.035)
        assign_material(pad, pal["metal"], "atlas_pad", metallic=0.5, roughness=0.4)
    
    # Belt/waist detail
    belt = create_rect_mesh(w * 0.8, h * 0.06, 2)
    belt.location = (0, 0, 0.03)
    assign_material(belt, (0.4, 0.2, 0.1), "atlas_belt")


def hero_zephyr_details(pk, pal, w, h):
    """Zephyr: add wing/glide details."""
    for sx in (-1, 1):
        wing = create_rect_mesh(w * 0.4, h * 0.12, 2)
        wing.location = (sx * w * 0.3, 0, 0.03)
        wing.rotation_euler = (0, 0, sx * 0.3)
        assign_material(wing, pal["accent"], "zephyr_wing")


def hero_synapse_details(pk, pal, w, h):
    """Synapse: add psychic ring."""
    ring = create_circle_mesh(min(w, h) * 0.55)
    ring.location = (0, 0, 0.025)
    ring.scale = (1.1, 1.1, 1.0)
    assign_material(ring, (0.3, 1.0, 0.3), "synapse_ring", emission=0.15, roughness=0.2)


def hero_volt_details(pk, pal, w, h):
    """Volt: add electric arcs (small protrusions)."""
    for angle in range(0, 360, 45):
        rad = math.radians(angle)
        spike = create_disk_mesh(min(w, h) * 0.06, 6)
        spike.location = (math.cos(rad) * w * 0.55, math.sin(rad) * h * 0.55, 0.04)
        spike.rotation_euler = (0, 0, rad)
        assign_material(spike, (1.0, 0.9, 0.2), "volt_spike", emission=0.4, roughness=0.1)


def create_enemy(name, palette_key, extra_details_fn=None):
    """Create an enemy sprite."""
    palette = PALETTE[palette_key]
    aspect = ASPECT.get(palette_key, (1.0, 1.0))
    size = SIZES.get(palette_key, 0.6)
    w = size * aspect[0]
    h = size * aspect[1]
    
    clear_scene()
    setup_camera_light()
    
    # Body shape varies by enemy type
    body = None
    
    if palette_key in ("drone", "parasite"):
        # Circular body
        body = create_disk_mesh(min(w, h), 24)
        body.location.z = 0.02
        assign_material(body, palette["body"], palette_key)
    elif palette_key == "brute":
        # Wide body
        body = create_round_rect_mesh(w, h, min(w, h) * 0.2)
        body.location.z = 0.02
        assign_material(body, palette["body"], palette_key)
    elif palette_key == "sprinter":
        # Elongated diamond-ish
        body = create_round_rect_mesh(w * 1.1, h * 0.7, min(w, h) * 0.1)
        body.location.z = 0.02
        assign_material(body, palette["body"], palette_key)
    elif palette_key == "artillery":
        # Tall body with barrel
        body = create_round_rect_mesh(w * 0.8, h, min(w, h) * 0.15)
        body.location.z = 0.02
        assign_material(body, palette["body"], palette_key)
    elif palette_key == "shielder":
        # Round with shield bump
        body = create_disk_mesh(min(w, h), 24)
        body.location.z = 0.02
        assign_material(body, palette["body"], palette_key)
    elif palette_key == "healer":
        # Rounded body
        body = create_round_rect_mesh(w, h, min(w, h) * 0.2)
        body.location.z = 0.02
        assign_material(body, palette["body"], palette_key)
    elif palette_key == "exploder":
        # Round with spikes
        body = create_disk_mesh(min(w, h), 16)
        body.location.z = 0.02
        assign_material(body, palette["body"], palette_key)
    elif palette_key == "burrower":
        # Elongated oval
        body = create_round_rect_mesh(w * 0.9, h * 1.2, min(w, h) * 0.15)
        body.location.z = 0.02
        assign_material(body, palette["body"], palette_key)
    elif palette_key == "apex":
        # Large round with armor plates
        body = create_disk_mesh(min(w, h), 24)
        body.location.z = 0.02
        assign_material(body, palette["body"], palette_key)
    
    # Accent details
    accent = create_round_rect_mesh(w * 0.6, h * 0.12, min(w, h) * 0.03)
    accent.location = (0, 0, 0.03)
    assign_material(accent, palette["accent"], f"{palette_key}_accent")
    
    # Eyes
    eye_size = min(w, h) * 0.07
    for dx in (-eye_size * 1.5, eye_size * 1.5):
        eye = create_disk_mesh(eye_size, 12)
        eye.location = (dx, 0, 0.04)
        assign_material(eye, palette["eye"], f"{palette_key}_eye", emission=0.2)
    
    # Extra details
    if extra_details_fn:
        extra_details_fn(palette_key, palette, w, h)
    
    # Render
    filepath = os.path.join(OUTPUT_DIR, "enemies", f"{palette_key}.png")
    ensure_dir(os.path.dirname(filepath))
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")


def enemy_drone_details(pk, pal, w, h):
    """Drone: add rotor rings."""
    for angle in range(0, 360, 90):
        rad = math.radians(angle)
        rotor = create_circle_mesh(min(w, h) * 0.3, 16)
        rotor.location = (math.cos(rad) * w * 0.4, math.sin(rad) * h * 0.4, 0.03)
        rotor.rotation_euler = (0, 0, rad)
        assign_material(rotor, pal["metal"], "drone_rotor", metallic=0.7, roughness=0.2)


def enemy_brute_details(pk, pal, w, h):
    """Brute: add armor plates."""
    for sx, sy in [(-1, -1), (1, -1), (-1, 1), (1, 1)]:
        plate = create_round_rect_mesh(w * 0.22, h * 0.18, w * 0.04)
        plate.location = (sx * w * 0.3, sy * h * 0.25, 0.035)
        assign_material(plate, pal["metal"], "brute_plate", metallic=0.4, roughness=0.6)


def enemy_sprinter_details(pk, pal, w, h):
    """Sprinter: add speed lines."""
    for i in range(3):
        stripe = create_rect_mesh(w * 0.15, h * 0.03, 1)
        stripe.location = (0, (i - 1) * h * 0.15, 0.03)
        stripe.rotation_euler = (0, 0, 0.2 * (i - 1))
        assign_material(stripe, pal["accent"], "sprinter_stripe")


def enemy_artillery_details(pk, pal, w, h):
    """Artillery: add barrel."""
    barrel = create_round_rect_mesh(w * 0.15, h * 0.4, w * 0.05)
    barrel.location = (0, h * 0.35, 0.03)
    assign_material(barrel, pal["metal"], "artillery_barrel", metallic=0.6, roughness=0.4)
    
    # Mortar base
    base = create_round_rect_mesh(w * 0.6, h * 0.15, w * 0.05)
    base.location = (0, -h * 0.2, 0.03)
    assign_material(base, pal["accent"], "artillery_base")


def enemy_shielder_details(pk, pal, w, h):
    """Shielder: add shield arc."""
    shield = create_disk_mesh(min(w, h) * 0.7, 20)
    shield.location = (0, 0, 0.015)
    shield.scale = (1.3, 1.3, 1.0)
    assign_material(shield, (0.3, 0.3, 0.7), "shielder_shield", metallic=0.3, roughness=0.5, emission=0.05)


def enemy_healer_details(pk, pal, w, h):
    """Healer: add halo/aura ring."""
    halo = create_circle_mesh(min(w, h) * 0.65, 20)
    halo.location = (0, 0, 0.015)
    halo.scale = (1.2, 1.2, 1.0)
    assign_material(halo, (0.3, 1.0, 0.3), "healer_halo", emission=0.15, roughness=0.3)


def enemy_exploder_details(pk, pal, w, h):
    """Exploder: add spikes."""
    spike_count = 8
    for i in range(spike_count):
        angle = (i / spike_count) * math.pi * 2
        spike = create_disk_mesh(min(w, h) * 0.08, 6)
        spike.location = (math.cos(angle) * w * 0.55, math.sin(angle) * h * 0.55, 0.03)
        spike.rotation_euler = (0, 0, angle)
        assign_material(spike, pal["accent"], "exploder_spike", emission=0.2, roughness=0.4)


def enemy_burrower_details(pk, pal, w, h):
    """Burrower: add segments."""
    for i in range(3):
        seg = create_round_rect_mesh(w * 0.5, h * 0.15, w * 0.05)
        seg.location = (0, (i - 1) * h * 0.22, 0.03)
        assign_material(seg, pal["accent"], f"burrower_seg_{i}")


def enemy_parasite_details(pk, pal, w, h):
    """Parasite: add tentacles."""
    for angle in range(0, 360, 60):
        rad = math.radians(angle)
        tent = create_disk_mesh(min(w, h) * 0.04, 6)
        tent.location = (math.cos(rad) * w * 0.5, math.sin(rad) * h * 0.5, 0.015)
        tent.rotation_euler = (0, 0, rad)
        assign_material(tent, pal["accent"], "parasite_tent")


def enemy_apex_details(pk, pal, w, h):
    """Apex boss: add crown/horns and armor."""
    # Horns
    for angle in (45, 135, 225, 315):
        rad = math.radians(angle)
        horn = create_round_rect_mesh(w * 0.1, h * 0.25, w * 0.03)
        horn.location = (math.cos(rad) * w * 0.5, math.sin(rad) * h * 0.5, 0.04)
        horn.rotation_euler = (0, 0, rad)
        assign_material(horn, pal["metal"], "apex_horn", metallic=0.5, roughness=0.4)
    
    # Armor plates
    for sx, sy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
        plate = create_round_rect_mesh(w * 0.2, h * 0.2, w * 0.03)
        plate.location = (sx * w * 0.35, sy * h * 0.35, 0.035)
        assign_material(plate, pal["metal"], "apex_plate", metallic=0.4, roughness=0.5)


def create_projectile(name, palette_key="volt", is_ability=False):
    """Create a projectile sprite."""
    palette = PALETTE.get(palette_key, PALETTE["volt"])
    
    clear_scene()
    setup_camera_light()
    
    if is_ability:
        # Larger, more detailed projectile
        body = create_disk_mesh(0.3, 20)
        body.location.z = 0.02
        assign_material(body, palette["body"], f"{name}_body", emission=0.3, roughness=0.2)
        
        # Glow ring
        ring = create_circle_mesh(0.35, 20)
        ring.location.z = 0.015
        ring.scale = (1.0, 1.0, 1.0)
        ring.rotation_euler = (0, 0, 0)
        assign_material(ring, palette["accent"], f"{name}_glow", emission=0.2, roughness=0.1)
        
        # Core
        core = create_disk_mesh(0.12, 12)
        core.location.z = 0.03
        assign_material(core, (1.0, 1.0, 1.0), f"{name}_core", emission=0.8, roughness=0.0)
    else:
        # Standard small projectile
        body = create_disk_mesh(0.15, 16)
        body.location.z = 0.02
        assign_material(body, palette["body"], f"{name}_body", emission=0.2, roughness=0.3)
    
    # Trail (small streak behind)
    trail = create_rect_mesh(0.1, 0.03, 1)
    trail.location = (-0.1, 0, 0.015)
    assign_material(trail, palette["body"], f"{name}_trail", emission=0.1, roughness=0.5)
    
    filepath = os.path.join(OUTPUT_DIR, "projectiles", f"{name}.png")
    ensure_dir(os.path.dirname(filepath))
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")


def create_decal(name, decal_type):
    """Create a decal sprite (blood, scorch, crater, acid, explosion)."""
    clear_scene()
    setup_camera_light()
    
    palette = PALETTE.get("floor", PALETTE["atlas"])
    
    if decal_type == "blood":
        # Irregular splatter
        for _ in range(5):
            ang = random.uniform(0, math.pi * 2)
            dist = random.uniform(0.1, 0.4)
            blob = create_disk_mesh(random.uniform(0.05, 0.12), 8)
            blob.location = (math.cos(ang) * dist, math.sin(ang) * dist, 0.01)
            assign_material(blob, (0.6, 0.05, 0.05), f"{name}_splat", roughness=0.9)
        
        # Central pool
        pool = create_disk_mesh(0.2, 16)
        pool.location.z = 0.01
        assign_material(pool, (0.5, 0.03, 0.03), f"{name}_pool", roughness=0.95)
    
    elif decal_type == "scorch":
        # Burn mark - dark circle with ash
        outer = create_disk_mesh(0.35, 20)
        outer.location.z = 0.01
        assign_material(outer, (0.1, 0.1, 0.1), f"{name}_outer", roughness=0.9)
        
        inner = create_disk_mesh(0.2, 16)
        inner.location.z = 0.015
        assign_material(inner, (0.15, 0.12, 0.08), f"{name}_inner", roughness=0.85)
        
        # Char marks
        for _ in range(3):
            mark = create_disk_mesh(random.uniform(0.03, 0.06), 6)
            mark.location = (random.uniform(-0.2, 0.2), random.uniform(-0.2, 0.2), 0.02)
            assign_material(mark, (0.05, 0.05, 0.05), f"{name}_char", roughness=0.9)
    
    elif decal_type == "crater":
        # Crater - ring with depth
        outer_ring = create_circle_mesh(0.4, 20)
        outer_ring.location.z = 0.005
        assign_material(outer_ring, (0.3, 0.25, 0.2), f"{name}_rim", roughness=0.9)
        
        inner = create_disk_mesh(0.3, 16)
        inner.location.z = 0.01
        assign_material(inner, (0.15, 0.12, 0.1), f"{name}_floor", roughness=0.95)
        
        # Back rim (visible inside crater)
        inner_rim = create_circle_mesh(0.25, 16)
        inner_rim.location = (0, 0, 0.01)
        inner_rim.scale = (1.0, 1.0, 1.0)
        assign_material(inner_rim, (0.2, 0.18, 0.15), f"{name}_inner_rim", roughness=0.9)
        
        # Debris
        for _ in range(4):
            rock = create_disk_mesh(random.uniform(0.02, 0.05), 6)
            ang = random.uniform(0, math.pi * 2)
            dist = random.uniform(0.35, 0.45)
            rock.location = (math.cos(ang) * dist, math.sin(ang) * dist, 0.015 + random.uniform(0, 0.01))
            assign_material(rock, (0.35, 0.30, 0.25), f"{name}_debris", roughness=0.9)
    
    elif decal_type == "acid":
        # Acid pool - green with bubbling
        pool = create_disk_mesh(0.35, 20)
        pool.location.z = 0.01
        assign_material(pool, (0.1, 0.5, 0.1), f"{name}_pool", roughness=0.3, emission=0.1)
        
        # Surface ripples
        for i in range(3):
            ripple = create_circle_mesh(0.2 + i * 0.05, 16)
            ripple.location.z = 0.012
            ripple.scale = (1.0 - i * 0.1, 1.0 - i * 0.1, 1.0)
            assign_material(ripple, (0.15, 0.6, 0.15), f"{name}_ripple_{i}", emission=0.05, roughness=0.2)
    
    elif decal_type == "explosion":
        # Explosion scorch - bright center fading out
        center = create_disk_mesh(0.15, 16)
        center.location.z = 0.02
        assign_material(center, (0.9, 0.8, 0.5), f"{name}_center", emission=0.5, roughness=0.3)
        
        mid = create_disk_mesh(0.3, 16)
        mid.location.z = 0.015
        assign_material(mid, (0.5, 0.3, 0.05), f"{name}_mid", emission=0.2, roughness=0.5)
        
        outer = create_disk_mesh(0.4, 20)
        outer.location.z = 0.01
        assign_material(outer, (0.15, 0.10, 0.02), f"{name}_outer", roughness=0.8)
    
    filepath = os.path.join(OUTPUT_DIR, "decals", f"{name}.png")
    ensure_dir(os.path.dirname(filepath))
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")


def create_environment():
    """Create environment sprites: floor tile, destructibles."""
    ensure_dir(os.path.join(OUTPUT_DIR, "environment"))
    
    # ── Arena floor tile ──
    clear_scene()
    setup_camera_light()
    
    # Large floor plane
    floor = create_rect_mesh(3.0, 3.0, 2)
    floor.location.z = 0.0
    floor.rotation_euler = (math.pi / 2, 0, 0)  # lay flat (though we render top-down so already flat)
    floor.rotation_euler = (0, 0, 0)
    floor.location = (0, 0, -0.01)  # below everything
    
    # Tile pattern via grid of smaller squares
    tile_size = 0.3
    tile_gap = 0.01
    for tx in range(-5, 6):
        for ty in range(-5, 6):
            is_odd = (tx + ty) % 2 == 0
            tile = create_rect_mesh(tile_size, tile_size, 1)
            tile.location = (tx * (tile_size + tile_gap), ty * (tile_size + tile_gap), 0.0)
            color = (0.22, 0.22, 0.24) if is_odd else (0.20, 0.20, 0.22)
            assign_material(tile, color, "floor_tile", roughness=0.9)
    
    # Grid border lines (thin lines)
    for i in range(-5, 6):
        h_line = create_rect_mesh(3.0, 0.005, 1)
        h_line.location = (0, i * 0.3, -0.02)
        assign_material(h_line, (0.15, 0.15, 0.16), "floor_grid_h", roughness=0.9)
        
        v_line = create_rect_mesh(0.005, 3.0, 1)
        v_line.location = (i * 0.3, 0, -0.02)
        assign_material(v_line, (0.15, 0.15, 0.16), "floor_grid_v", roughness=0.9)
    
    filepath = os.path.join(OUTPUT_DIR, "environment", "arena_floor.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Crate ──
    clear_scene()
    setup_camera_light()
    palette_crate = PALETTE["crate"]
    
    crate = create_round_rect_mesh(0.8, 0.8, 0.1, 8)
    crate.location.z = 0.02
    assign_material(crate, palette_crate["body"], "crate_body", roughness=0.8)
    
    # Wood planks (cross lines)
    for i in range(3):
        plank = create_rect_mesh(0.7, 0.02, 1)
        plank.location = (0, (i - 1) * 0.25, 0.03)
        assign_material(plank, palette_crate["accent"], "crate_plank")
    
    plank2 = create_rect_mesh(0.02, 0.7, 1)
    plank2.location = (0, 0, 0.03)
    assign_material(plank2, palette_crate["accent"], "crate_plank_v")
    
    # Corner brackets
    for sx, sy in [(-1, -1), (1, -1), (-1, 1), (1, 1)]:
        bracket = create_round_rect_mesh(0.08, 0.08, 0.02, 4)
        bracket.location = (sx * 0.35, sy * 0.35, 0.03)
        assign_material(bracket, palette_crate["metal"], "crate_bracket", metallic=0.3, roughness=0.6)
    
    filepath = os.path.join(OUTPUT_DIR, "environment", "crate.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Barrel ──
    clear_scene()
    setup_camera_light()
    palette_barrel = PALETTE["barrel"]
    
    barrel = create_round_rect_mesh(0.6, 0.8, 0.05, 8)
    barrel.location.z = 0.02
    assign_material(barrel, palette_barrel["body"], "barrel_body", roughness=0.7)
    
    # Metal bands
    for i in range(3):
        band = create_rect_mesh(0.65, 0.03, 1)
        band.location = (0, (i - 1) * 0.25, 0.03)
        assign_material(band, palette_barrel["metal"], "barrel_band", metallic=0.5, roughness=0.4)
    
    # Bung hole
    hole = create_disk_mesh(0.04, 8)
    hole.location = (0, 0.35, 0.03)
    assign_material(hole, (0.05, 0.05, 0.05), "barrel_hole")
    
    filepath = os.path.join(OUTPUT_DIR, "environment", "barrel.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Wall segment ──
    clear_scene()
    setup_camera_light()
    palette_wall = PALETTE["wall"]
    
    wall = create_rect_mesh(0.8, 1.2, 2)
    wall.location.z = 0.02
    assign_material(wall, palette_wall["body"], "wall_body", roughness=0.85)
    
    # Brick pattern
    for row in range(3):
        for col in range(2):
            brick = create_rect_mesh(0.32, 0.32, 1)
            offset_x = 0.08 if row % 2 == 0 else -0.08
            brick.location = (-0.3 + col * 0.4 + offset_x, row * 0.35 - 0.35, 0.03)
            is_mortar = (row + col) % 2 == 0
            color = (0.45, 0.40, 0.35) if is_mortar else (0.50, 0.45, 0.40)
            assign_material(brick, color, "wall_brick")
    
    # Crack
    crack = create_rect_mesh(0.005, 0.3, 1)
    crack.location = (0.1, -0.1, 0.035)
    crack.rotation_euler = (0, 0, 0.2)
    assign_material(crack, (0.2, 0.2, 0.2), "wall_crack")
    
    # Second crack segment
    crack2 = create_rect_mesh(0.005, 0.2, 1)
    crack2.location = (0.15, 0.05, 0.035)
    crack2.rotation_euler = (0, 0, -0.3)
    assign_material(crack2, (0.2, 0.2, 0.2), "wall_crack2")
    
    filepath = os.path.join(OUTPUT_DIR, "environment", "wall.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")


def create_vfx():
    """Create VFX sprites: hit flash, explosion, smoke, sparks."""
    ensure_dir(os.path.join(OUTPUT_DIR, "vfx"))
    
    # ── Hit flash ──
    clear_scene()
    setup_camera_light()
    
    flash = create_disk_mesh(0.5, 24)
    flash.location.z = 0.02
    assign_material(flash, (1.0, 0.95, 0.9), "hitflash", emission=0.8, roughness=0.1)
    
    # Inner bright core
    core = create_disk_mesh(0.2, 16)
    core.location.z = 0.03
    assign_material(core, (1.0, 1.0, 1.0), "hitflash_core", emission=1.0, roughness=0.0)
    
    filepath = os.path.join(OUTPUT_DIR, "vfx", "hitflash.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Explosion ──
    clear_scene()
    setup_camera_light()
    
    # Outer fire
    outer = create_disk_mesh(0.5, 24)
    outer.location.z = 0.01
    assign_material(outer, (1.0, 0.4, 0.05), "explosion_outer", emission=0.6, roughness=0.3)
    
    # Mid
    mid = create_disk_mesh(0.3, 20)
    mid.location.z = 0.02
    assign_material(mid, (1.0, 0.7, 0.1), "explosion_mid", emission=0.8, roughness=0.2)
    
    # Inner white hot
    inner = create_disk_mesh(0.12, 12)
    inner.location.z = 0.03
    assign_material(inner, (1.0, 1.0, 0.9), "explosion_inner", emission=1.0, roughness=0.0)
    
    filepath = os.path.join(OUTPUT_DIR, "vfx", "explosion.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Smoke puff ──
    clear_scene()
    setup_camera_light()
    
    smoke = create_disk_mesh(0.4, 20)
    smoke.location.z = 0.01
    assign_material(smoke, (0.3, 0.3, 0.35), "smoke_outer", roughness=0.95)
    
    smoke2 = create_disk_mesh(0.3, 16)
    smoke2.location = (0.05, 0.05, 0.015)
    assign_material(smoke2, (0.25, 0.25, 0.3), "smoke_inner", roughness=0.95)
    
    smoke3 = create_disk_mesh(0.15, 12)
    smoke3.location = (-0.03, -0.02, 0.02)
    assign_material(smoke3, (0.2, 0.2, 0.25), "smoke_core", roughness=0.95)
    
    filepath = os.path.join(OUTPUT_DIR, "vfx", "smoke.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Sparks ──
    clear_scene()
    setup_camera_light()
    
    spark_count = 8
    for i in range(spark_count):
        angle = (i / spark_count) * math.pi * 2 + random.uniform(-0.2, 0.2)
        dist = random.uniform(0.1, 0.45)
        spark = create_disk_mesh(random.uniform(0.01, 0.025), 4)
        spark.location = (math.cos(angle) * dist, math.sin(angle) * dist, 0.02)
        spark.rotation_euler = (0, 0, angle)
        spark_color = random.choice([(1.0, 0.8, 0.1), (1.0, 0.5, 0.1), (1.0, 1.0, 0.9)])
        assign_material(spark, spark_color, f"spark_{i}", emission=0.8, roughness=0.1)
    
    # Center spark (brightest)
    center_spark = create_disk_mesh(0.04, 6)
    center_spark.location.z = 0.03
    assign_material(center_spark, (1.0, 1.0, 0.9), "spark_center", emission=1.0, roughness=0.0)
    
    filepath = os.path.join(OUTPUT_DIR, "vfx", "sparks.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Shockwave ring ──
    clear_scene()
    setup_camera_light()
    
    ring = create_circle_mesh(0.45, 24)
    ring.location.z = 0.02
    assign_material(ring, (0.8, 0.8, 1.0), "shockwave", emission=0.4, roughness=0.1)
    
    inner_ring = create_circle_mesh(0.35, 20)
    inner_ring.location.z = 0.025
    inner_ring.scale = (0.9, 0.9, 1.0)
    assign_material(inner_ring, (1.0, 0.9, 1.0), "shockwave_inner", emission=0.6, roughness=0.0)
    
    filepath = os.path.join(OUTPUT_DIR, "vfx", "shockwave.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")


def create_ui_elements():
    """Create UI sprites: health bar background, powerup icons, etc."""
    ensure_dir(os.path.join(OUTPUT_DIR, "ui"))
    
    # ── Health bar background ──
    clear_scene()
    setup_camera_light()
    
    bg = create_round_rect_mesh(2.0, 0.2, 0.05, 4)
    bg.location.z = 0.01
    assign_material(bg, (0.15, 0.15, 0.15), "healthbar_bg", roughness=0.8)
    
    filepath = os.path.join(OUTPUT_DIR, "ui", "healthbar_bg.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Health bar fill ──
    clear_scene()
    setup_camera_light()
    
    fill = create_round_rect_mesh(1.9, 0.15, 0.03, 4)
    fill.location.z = 0.01
    assign_material(fill, (0.8, 0.1, 0.1), "healthbar_fill", emission=0.1, roughness=0.6)
    
    filepath = os.path.join(OUTPUT_DIR, "ui", "healthbar_fill.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Powerup icon base ──
    clear_scene()
    setup_camera_light()
    
    icon = create_round_rect_mesh(0.5, 0.5, 0.08, 8)
    icon.location.z = 0.02
    assign_material(icon, (0.1, 0.1, 0.1), "powerup_bg", roughness=0.5)
    
    ring = create_circle_mesh(0.4, 16)
    ring.location = (0, 0, 0.025)
    assign_material(ring, (0.8, 0.7, 0.2), "powerup_ring", emission=0.2, metallic=0.4, roughness=0.3)
    
    filepath = os.path.join(OUTPUT_DIR, "ui", "powerup_icon_bg.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Wave announcement bg ──
    clear_scene()
    setup_camera_light()
    
    banner = create_round_rect_mesh(4.0, 1.0, 0.2, 8)
    banner.location.z = 0.01
    assign_material(banner, (0.05, 0.05, 0.08), "wave_banner", roughness=0.6)
    
    # Border
    border = create_round_rect_mesh(4.0, 1.0, 0.2, 8)
    border.location = (0, 0, 0.0)
    border.scale = (1.05, 1.05, 1.0)
    assign_material(border, (0.8, 0.7, 0.2), "wave_banner_border", emission=0.1, metallic=0.3, roughness=0.4)
    
    filepath = os.path.join(OUTPUT_DIR, "ui", "wave_banner.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")
    
    # ── Main menu background ──
    clear_scene()
    setup_camera_light()
    
    mg_bg = create_rect_mesh(6.0, 6.0, 1)
    mg_bg.location.z = -0.01
    assign_material(mg_bg, (0.08, 0.08, 0.10), "mainmenu_bg", roughness=0.9)
    
    # Radial gradient effect via multiple rings
    for i, r in enumerate([5.5, 4.5, 3.5, 2.5]):
        ring = create_circle_mesh(r, 32)
        ring.location.z = 0.0
        ring.rotation_euler = (math.pi / 2, 0, 0)
        alpha = 0.05 * (4 - i)
        assign_material(ring, (0.1 + i * 0.02, 0.08 + i * 0.01, 0.12 + i * 0.02), f"mainmenu_ring_{i}", roughness=0.9, emission=alpha)
    
    filepath = os.path.join(OUTPUT_DIR, "ui", "mainmenu_bg.png")
    render_sprite(filepath)
    print(f"  Rendered: {filepath}")


# ── Entity builder maps ───────────────────────────────────────────────────────
HERO_BUILDERS = {
    "atlas":  hero_atlas_details,
    "zephyr": hero_zephyr_details,
    "synapse": hero_synapse_details,
    "volt":   hero_volt_details,
}

ENEMY_BUILDERS = {
    "drone":    enemy_drone_details,
    "brute":    enemy_brute_details,
    "sprinter": enemy_sprinter_details,
    "artillery": enemy_artillery_details,
    "shielder": enemy_shielder_details,
    "healer":   enemy_healer_details,
    "exploder": enemy_exploder_details,
    "burrower": enemy_burrower_details,
    "parasite": enemy_parasite_details,
    "apex":     enemy_apex_details,
}

PROJECTILE_TYPES = {
    "standard": {"palette": "volt"},
    "kinetic":  {"palette": "atlas"},
    "energy":   {"palette": "zephyr"},
    "lightning": {"palette": "volt"},
    "acid":     {"palette": "healer"},
    "fire":     {"palette": "exploder"},
    "explosive": {"palette": "apex"},
    "synapse_bolt": {"palette": "synapse", "ability": True},
    "zephyr_wind":  {"palette": "zephyr", "ability": True},
    "atlas_slam":   {"palette": "atlas", "ability": True},
    "volt_chain":   {"palette": "volt", "ability": True},
}


# ── Main ──────────────────────────────────────────────────────────────────────
def main():
    global OUTPUT_DIR
    
    # Parse CLI args
    args = sys.argv
    if "--output-dir" in args:
        idx = args.index("--output-dir")
        OUTPUT_DIR = args[idx + 1]
    else:
        # Default: relative to this script
        script_dir = os.path.dirname(os.path.abspath(__file__))
        OUTPUT_DIR = os.path.join(script_dir, "generated_assets")
    
    ensure_dir(OUTPUT_DIR)
    print(f"Output directory: {OUTPUT_DIR}")
    print(f"Resolution: {RESOLUTION}x{RESOLUTION}")
    print()
    
    print("=" * 60)
    print("HERO'S ARENA — Blender Asset Generation (Phase 1)")
    print("=" * 60)
    
    # ── Heroes ──
    print("\n--- Heroes ---")
    for hero_name, builder_fn in HERO_BUILDERS.items():
        print(f"  Generating {hero_name}...")
        create_hero(hero_name, hero_name, builder_fn)
    
    # ── Enemies ──
    print("\n--- Enemies ---")
    for enemy_name, builder_fn in ENEMY_BUILDERS.items():
        print(f"  Generating {enemy_name}...")
        create_enemy(enemy_name, enemy_name, builder_fn)
    
    # ── Projectiles ──
    print("\n--- Projectiles ---")
    for proj_name, config in PROJECTILE_TYPES.items():
        print(f"  Generating {proj_name}...")
        create_projectile(proj_name, config["palette"], config.get("ability", False))
    
    # ── Decals ──
    print("\n--- Decals ---")
    decal_types = ["blood", "scorch", "crater", "acid", "explosion"]
    for decal_type in decal_types:
        print(f"  Generating decal: {decal_type}...")
        create_decal(decal_type, decal_type)
    
    # ── Environment ──
    print("\n--- Environment ---")
    create_environment()
    
    # ── VFX ──
    print("\n--- VFX ---")
    create_vfx()
    
    # ── UI ──
    print("\n--- UI ---")
    create_ui_elements()
    
    print()
    print("=" * 60)
    print("Asset generation complete!")
    print(f"Output: {OUTPUT_DIR}/")
    print("=" * 60)


if __name__ == "__main__":
    main()
