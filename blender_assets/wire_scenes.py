#!/usr/bin/env python3
"""Create decal and destructible scenes + fix entity node types."""

import os

base = "/home/leandi-duplessis/github/workspaces/Hero-sArena"

# ── Decal scene (single sprite, texture set at runtime by type) ──
# The DecalInstance.cs is a Sprite2D. We create one scene with a Sprite2D
# that has no texture — the texture gets assigned in code based on decal type.
decal_tscn = """[gd_scene load_steps=2 format=3 uid="uid://decal"]
[ext_resource type="Script" path="res://scripts/vfx/DecalInstance.cs" id="1"]

[sub_resource type="AtlasTexture" id="1"]
path = "res://decals/blood.png"

[node name="Decal" type="Sprite2D"]
script = ExtResource("1")
texture = SubResource("1")
"""

with open(os.path.join(base, "scenes", "vfx", "Decal.tscn"), 'w') as f:
    f.write(decal_tscn)
print("✓ scenes/vfx/Decal.tscn")

# ── Destructible scene (crate) ──
destructible_tscn = """[gd_scene load_steps=4 format=3 uid="uid://destructible"]
[ext_resource type="Script" path="res://scripts/maps/DestructibleObject.cs" id="1"]
[sub_resource type="RectangleShape2D" id="1"]
size = Vector2(64, 64)

[sub_resource type="AtlasTexture" id="2"]
path = "res://environment/crate.png"

[node name="Destructible" type="Node2D"]
script = ExtResource("1")

[node name="Sprite2D" type="Sprite2D" parent="."]
texture = SubResource("2")

[node name="CollisionShape2D" type="CollisionShape2D" parent="."]
shape = SubResource("1")
"""

with open(os.path.join(base, "scenes", "maps", "Destructible.tscn"), 'w') as f:
    f.write(destructible_tscn)
print("✓ scenes/maps/Destructible.tscn")

# ── Fix entity scenes: Node2D → CharacterBody2D ──
import re

entity_dirs = [
    os.path.join(base, "scenes", "heroes"),
    os.path.join(base, "scenes", "enemies"),
]

fixed = 0
for d in entity_dirs:
    for fname in os.listdir(d):
        if not fname.endswith('.tscn'):
            continue
        path = os.path.join(d, fname)
        with open(path) as f:
            content = f.read()
        
        # Fix root node type from Node2D to CharacterBody2D
        old = f'[node name="{os.path.splitext(fname)[0]}" type="Node2D"]'
        new = f'[node name="{os.path.splitext(fname)[0]}" type="CharacterBody2D"]'
        
        if old in content:
            content = content.replace(old, new)
            with open(path, 'w') as f:
                f.write(content)
            print(f"✓ Fixed {fname}: Node2D → CharacterBody2D")
            fixed += 1
        else:
            # Already fixed or different format
            pass

print(f"\n{fixed} entity scenes fixed")
