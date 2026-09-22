#!/usr/bin/env python3
"""
Hero's Arena — Blender Asset Generation (Phase 1) — v2
=========================================================
Renders top-down 2D sprite assets, then chroma-keys the background
to transparent alpha in post-processing.

Pipeline per sprite:
  1. Build mesh with diffuse material (no nodes — proven stable in Blender 5.2 headless)
  2. Render with solid background color (film_transparent=False)
  3. Post-process: pixels similar to bg color → alpha=0, everything else → alpha=255

Run:  blender --background --python generate_assets_v2.py -- --output-dir ./assets
"""

import bpy
import os
import math
import struct
import zlib
import sys

# ── Configuration ─────────────────────────────────────────────────────────────
RESOLUTION = 512
OUTPUT_DIR = None
BG_COLOR = (0, 170, 0)       # solid green background for chroma-keying
BG_THRESHOLD = 40            # max RGB distance from bg color to consider "background"

# ── Post-processing: chroma-key ───────────────────────────────────────────────
def chroma_key_png(inpath, outpath, bg_r, bg_g, bg_b, threshold=40):
    """Replace background-colored pixels with alpha=0, everything else alpha=255."""
    with open(inpath, 'rb') as f:
        sig = f.read(8)
        assert sig == b'\x89PNG\r\n\x1a\n', "Not a valid PNG"
        chunks = []
        w = h = 0
        ct = 0
        while True:
            length = struct.unpack('>I', f.read(4))[0]
            ctype = f.read(4)
            data = f.read(length)
            f.read(4)  # crc
            chunks.append((ctype, data))
            if ctype == b'IHDR':
                w = struct.unpack('>I', data[0:4])[0]
                h = struct.unpack('>I', data[4:8])[0]
                ct = data[9]
            elif ctype == b'IEND':
                break

    idat_data = b''.join(d for ct2, d in chunks if ct2 == b'IDAT')
    raw = zlib.decompress(idat_data)

    bpp = 4  # RGBA
    row_bytes = 1 + w * bpp

    # Decode all rows (handle sub/up/average/paeth filters)
    rows = []
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:  # Sub
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        elif ft == 2 and y > 0:  # Up
            prev = rows[y-1]
            for i in range(len(cur)):
                cur[i] = (cur[i] + prev[i]) & 0xFF
        elif ft == 3 and y > 0:  # Average
            prev = rows[y-1]
            for i in range(len(cur)):
                left = cur[i-bpp] if i >= bpp else 0
                cur[i] = (cur[i] + (left + prev[i]) // 2) & 0xFF
        elif ft == 4 and y > 0:  # Paeth
            prev = rows[y-1]
            for i in range(len(cur)):
                left = cur[i-bpp] if i >= bpp else 0
                up = prev[i]
                up_left = prev[i-bpp] if i >= bpp else 0
                p = left + up - up_left
                pl = abs(p - left)
                pu = abs(p - up)
                pul = abs(p - up_left)
                if pl <= pu and pl <= pul:
                    pred = left
                elif pu <= pul:
                    pred = up
                else:
                    pred = up_left
                cur[i] = (cur[i] + pred) & 0xFF
        rows.append(bytes(cur))

    # Build new rows with corrected alpha
    new_rows = []
    opaque = transparent = 0
    for y in range(h):
        cur = rows[y]
        nr = bytearray()
        nr.append(0)  # filter byte: None
        for x in range(w):
            idx = x * bpp
            r, g, b = cur[idx], cur[idx+1], cur[idx+2]
            dr = abs(int(r) - bg_r)
            dg = abs(int(g) - bg_g)
            db = abs(int(b) - bg_b)
            if dr <= threshold and dg <= threshold and db <= threshold:
                nr.extend([r, g, b, 0])
                transparent += 1
            else:
                nr.extend([r, g, b, 255])
                opaque += 1
        new_rows.append(bytes(nr))

    # Re-compress and write PNG
    new_raw = b''.join(nr for nr in new_rows)
    compressed = zlib.compress(new_raw)

    with open(outpath, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n')

        def write_chunk(ctype, data):
            import binascii
            f.write(struct.pack('>I', len(data)))
            f.write(ctype)
            f.write(data)
            crc = binascii.crc32(ctype + data) & 0xFFFFFFFF
            f.write(struct.pack('>I', crc))

        ihdr = struct.pack('>IIBBBBB', w, h, 8, ct, 0, 0, 0)
        write_chunk(b'IHDR', ihdr)
        write_chunk(b'IDAT', compressed)
        write_chunk(b'IEND', b'')

    pct = 100.0 * opaque / (opaque + transparent) if (opaque + transparent) > 0 else 0
    print(f"    chroma-key: {opaque}/{opaque+transparent} opaque ({pct:.1f}%)")
    return opaque, transparent


# ── Blender setup ─────────────────────────────────────────────────────────────

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for w in list(bpy.data.worlds):
        bpy.data.worlds.remove(w)


def setup_camera_lights():
    # Remove old cameras/lights
    for obj in list(bpy.context.scene.objects):
        if obj.type in ('CAMERA', 'LIGHT'):
            bpy.data.objects.remove(obj, do_unlink=True)

    # Orthographic camera looking down
    cd = bpy.data.cameras.new("sprite_cam")
    cd.type = 'ORTHO'
    cd.ortho_scale = 3.0
    co = bpy.data.objects.new("SpriteCam", cd)
    co.location = (0, 0, 5)
    co.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co

    # Lights — 3-point style for flat top-down lighting
    for pos, energy in [((3, 3, 6), 120), ((-3, -2, 4), 60), ((0, 4, 5), 50), ((0, -4, 5), 40)]:
        ld = bpy.data.lights.new("light", type='POINT')
        ld.energy = energy
        lo = bpy.data.objects.new("light", ld)
        lo.location = pos
        bpy.context.collection.objects.link(lo)


def setup_world(bg_color):
    """Set world background to a solid color."""
    world = bpy.data.worlds.new("bg_world")
    bpy.context.scene.world = world
    world.use_nodes = True
    nodes = world.node_tree.nodes
    for n in nodes:
        nodes.remove(n)
    bg_node = nodes.new(type='ShaderNodeBackground')
    bg_node.inputs['Color'].default_value = (*bg_color, 1.0)
    bg_node.inputs['Strength'].default_value = 1.0
    out_node = nodes.new(type='ShaderNodeOutputWorld')
    bg_node.location = (-200, 0)
    out_node.location = (200, 0)
    world.node_tree.links.new(bg_node.outputs['Background'], out_node.inputs['Surface'])


def set_render_settings(color_mode='RGBA'):
    scene = bpy.context.scene
    scene.render.resolution_x = RESOLUTION
    scene.render.resolution_y = RESOLUTION
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = color_mode
    scene.render.film_transparent = False  # always render with bg


def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


# ── Mesh builders ─────────────────────────────────────────────────────────────

def make_circle(radius, verts=32, filled=True):
    bpy.ops.mesh.primitive_circle_add(
        vertices=verts, radius=radius,
        fill_type='TRIFAN' if filled else 'NOTHING'
    )
    obj = bpy.context.active_object
    obj.location.z = 0
    return obj


def make_rect(w, h):
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.scale = (w / 2, h / 2, 1.0)
    return obj


def make_round_rect(w, h, radius, verts=8):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts * 4, radius=1.0, depth=0.02
    )
    obj = bpy.context.active_object
    obj.scale = (w / 2, h / 2, 1.0)
    return obj


def make_material(r, g, b, name="mat", specular=False):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (r, g, b, 1.0)
    if specular:
        mat.specular_color = (1.0, 1.0, 1.0)
        mat.specular_hardness = 60
    return mat


def assign_material(obj, r, g, b, name="mat"):
    mat = make_material(r, g, b, name)
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)
    return mat


def set_location(obj, x, y, z=0):
    obj.location = (x, y, z)


def set_scale(obj, sx, sy, sz=1.0):
    obj.scale = (sx, sy, sz)


# ── Sprite builders ───────────────────────────────────────────────────────────

def render_and_key(name, out_subdir):
    """Render current scene to PNG then chroma-key."""
    out_dir = os.path.join(OUTPUT_DIR, out_subdir)
    os.makedirs(out_dir, exist_ok=True)
    raw_path = os.path.join(out_dir, f"_raw_{name}.png")
    final_path = os.path.join(out_dir, f"{name}.png")
    render_to(raw_path)
    chroma_key_png(raw_path, final_path,
                   BG_COLOR[0], BG_COLOR[1], BG_COLOR[2], BG_THRESHOLD)
    os.remove(raw_path)  # clean up raw file
    print(f"  ✓ {out_subdir}/{name}.png")


def build_hero(name, palette_key, detail_fn=None):
    pal = PALETTE[palette_key]
    aspect = ASPECT.get(palette_key, (1, 1))
    size = SIZES.get(palette_key, 1.0)
    w = size * aspect[0]
    h = size * aspect[1]
    r, g, b = pal['body']

    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    # Outline
    outline = make_round_rect(w * 1.06, h * 1.06, min(w, h) * 0.1)
    assign_material(outline, pal['outline'][0], pal['outline'][1], pal['outline'][2],
                    f"{name}_outline")
    set_location(outline, 0, 0, -0.01)

    # Body
    body = make_round_rect(w, h, min(w, h) * 0.12)
    assign_material(body, r, g, b, name)
    set_location(body, 0, 0, 0.01)

    # Accent band
    accent = make_rect(w * 0.7, h * 0.12)
    assign_material(accent, pal['accent'][0], pal['accent'][1], pal['accent'][2],
                    f"{name}_accent")
    set_location(accent, 0, 0, 0.02)

    # Chest emblem
    emblem = make_circle(min(w, h) * 0.3, 20, filled=True)
    assign_material(emblem, pal['metal'][0], pal['metal'][1], pal['metal'][2],
                    f"{name}_metal")
    set_location(emblem, 0, 0, 0.03)

    # Eyes
    es = min(w, h) * 0.08
    for dx in (-es * 2.2, es * 2.2):
        eye = make_circle(es, 12, filled=True)
        assign_material(eye, pal['eye'][0], pal['eye'][1], pal['eye'][2],
                        f"{name}_eye")
        set_location(eye, dx, 0, 0.04)

    if detail_fn:
        detail_fn(name, pal, w, h)

    render_and_key(name, "heroes")


def build_enemy(name, palette_key, detail_fn=None):
    pal = PALETTE[palette_key]
    aspect = ASPECT.get(palette_key, (1, 1))
    size = SIZES.get(palette_key, 0.6)
    w = size * aspect[0]
    h = size * aspect[1]
    r, g, b = pal['body']

    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    # Body shape depends on enemy type
    body = None
    if name in ('drone', 'parasite'):
        body = make_circle(min(w, h), 24)
    elif name == 'brute':
        body = make_round_rect(w, h, min(w, h) * 0.18)
    elif name == 'sprinter':
        body = make_round_rect(w * 1.15, h * 0.65, min(w, h) * 0.08)
    elif name == 'artillery':
        body = make_round_rect(w * 0.75, h, min(w, h) * 0.12)
    elif name == 'shielder':
        body = make_circle(min(w, h), 24)
    elif name == 'healer':
        body = make_round_rect(w, h, min(w, h) * 0.18)
    elif name == 'exploder':
        body = make_circle(min(w, h), 16)
    elif name == 'burrower':
        body = make_round_rect(w * 0.85, h * 1.25, min(w, h) * 0.12)
    elif name == 'apex':
        body = make_circle(min(w, h), 24)
    else:
        body = make_circle(min(w, h), 24)

    assign_material(body, r, g, b, name)
    set_location(body, 0, 0, 0.01)

    # Accent band
    accent = make_round_rect(w * 0.55, h * 0.1, min(w, h) * 0.02)
    assign_material(accent, pal['accent'][0], pal['accent'][1], pal['accent'][2],
                    f"{name}_accent")
    set_location(accent, 0, 0, 0.02)

    # Eyes
    es = min(w, h) * 0.07
    for dx in (-es * 1.8, es * 1.8):
        eye = make_circle(es, 12, filled=True)
        assign_material(eye, pal['eye'][0], pal['eye'][1], pal['eye'][2],
                        f"{name}_eye")
        set_location(eye, dx, 0, 0.03)

    if detail_fn:
        detail_fn(name, pal, w, h)

    render_and_key(name, "enemies")


def build_projectile(name, palette_key, ability=False):
    pal = PALETTE.get(palette_key, PALETTE['volt'])
    r, g, b = pal['body']
    ar, ag, ab = pal['accent']

    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    if ability:
        # Larger, glowing projectile
        glow = make_circle(0.35, 24)
        assign_material(glow, ar, ag, ab, f"{name}_glow")
        set_location(glow, 0, 0, 0.01)

        body = make_circle(0.22, 20)
        assign_material(body, r, g, b, name)
        set_location(body, 0, 0, 0.02)

        core = make_circle(0.08, 12)
        assign_material(core, 1.0, 1.0, 1.0, f"{name}_core")
        set_location(core, 0, 0, 0.03)
    else:
        body = make_circle(0.15, 16)
        assign_material(body, r, g, b, name)
        set_location(body, 0, 0, 0.02)

        # Trail
        trail = make_rect(0.1, 0.03)
        assign_material(trail, r * 0.5, g * 0.5, b * 0.5, f"{name}_trail")
        set_location(trail, -0.1, 0, 0.01)

    render_and_key(name, "projectiles")


def build_decal(name, decal_type):
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    if decal_type == 'blood':
        for _ in range(5):
            ang = _rand_angle()
            dist = _rand(0.1, 0.35)
            blob = make_circle(_rand(0.04, 0.1), 8)
            assign_material(blob, 0.55, 0.03, 0.03, f"{name}_splat")
            set_location(blob, math.cos(ang) * dist, math.sin(ang) * dist, 0.01)

        pool = make_circle(0.18, 16)
        assign_material(pool, 0.45, 0.02, 0.02, f"{name}_pool")
        set_location(pool, 0, 0, 0.015)

    elif decal_type == 'scorch':
        outer = make_circle(0.32, 20)
        assign_material(outer, 0.08, 0.08, 0.06, f"{name}_outer")
        set_location(outer, 0, 0, 0.01)

        inner = make_circle(0.18, 16)
        assign_material(inner, 0.12, 0.10, 0.06, f"{name}_inner")
        set_location(inner, 0, 0, 0.015)

        for _ in range(4):
            mark = make_circle(_rand(0.02, 0.05), 6)
            assign_material(mark, 0.03, 0.03, 0.03, f"{name}_char")
            set_location(mark, _rand(-0.2, 0.2), _rand(-0.2, 0.2), 0.02)

    elif decal_type == 'crater':
        rim = make_circle(0.38, 24)
        assign_material(rim, 0.28, 0.22, 0.16, f"{name}_rim")
        set_location(rim, 0, 0, 0.005)

        floor = make_circle(0.28, 20)
        assign_material(floor, 0.12, 0.10, 0.08, f"{name}_floor")
        set_location(floor, 0, 0, 0.01)

        for _ in range(5):
            rock = make_circle(_rand(0.02, 0.05), 6)
            ang = _rand_angle()
            dist = _rand(0.35, 0.42)
            assign_material(rock, 0.32, 0.27, 0.22, f"{name}_debris")
            set_location(rock, math.cos(ang) * dist, math.sin(ang) * dist, 0.012)

    elif decal_type == 'acid':
        pool = make_circle(0.32, 20)
        assign_material(pool, 0.08, 0.45, 0.08, f"{name}_pool")
        set_location(pool, 0, 0, 0.01)

        for i in range(3):
            r = 0.18 + i * 0.05
            ripple = make_circle(r, 16)
            assign_material(ripple, 0.12, 0.55, 0.12, f"{name}_ripple_{i}")
            set_location(ripple, 0, 0, 0.008)

    elif decal_type == 'explosion':
        outer = make_circle(0.38, 24)
        assign_material(outer, 0.12, 0.08, 0.02, f"{name}_outer")
        set_location(outer, 0, 0, 0.01)

        mid = make_circle(0.25, 20)
        assign_material(mid, 0.45, 0.25, 0.05, f"{name}_mid")
        set_location(mid, 0, 0, 0.015)

        center = make_circle(0.1, 14)
        assign_material(center, 0.85, 0.75, 0.4, f"{name}_center")
        set_location(center, 0, 0, 0.02)

    render_and_key(name, "decals")


def build_vfx(name, vfx_type):
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    if vfx_type == 'hitflash':
        outer = make_circle(0.45, 24)
        assign_material(outer, 1.0, 0.9, 0.8, f"{name}_outer")
        set_location(outer, 0, 0, 0.01)

        inner = make_circle(0.18, 14)
        assign_material(inner, 1.0, 1.0, 1.0, f"{name}_inner")
        set_location(inner, 0, 0, 0.02)

    elif vfx_type == 'explosion':
        outer = make_circle(0.45, 24)
        assign_material(outer, 1.0, 0.4, 0.05, f"{name}_outer")
        set_location(outer, 0, 0, 0.01)

        mid = make_circle(0.28, 20)
        assign_material(mid, 1.0, 0.7, 0.1, f"{name}_mid")
        set_location(mid, 0, 0, 0.015)

        inner = make_circle(0.1, 12)
        assign_material(inner, 1.0, 1.0, 0.9, f"{name}_inner")
        set_location(inner, 0, 0, 0.02)

    elif vfx_type == 'smoke':
        for size, alpha in [(0.38, 0.35), (0.28, 0.28), (0.14, 0.22)]:
            puff = make_circle(size, 16)
            r = g = b = alpha * 0.8
            assign_material(puff, r, g, b, f"{name}_puff")
            # Offset each puff slightly
            off_x = _rand(-0.04, 0.04)
            off_y = _rand(-0.04, 0.04)
            set_location(puff, off_x, off_y, 0.01)

    elif vfx_type == 'sparks':
        for i in range(10):
            ang = _rand_angle()
            dist = _rand(0.08, 0.42)
            spark = make_circle(_rand(0.01, 0.025), 5)
            sr, sg, sb = _pick([ (1.0, 0.8, 0.1), (1.0, 0.5, 0.1), (1.0, 1.0, 0.9) ])
            assign_material(spark, sr, sg, sb, f"{name}_spark")
            set_location(spark, math.cos(ang) * dist, math.sin(ang) * dist, 0.02)

        core = make_circle(0.03, 6)
        assign_material(core, 1.0, 1.0, 0.9, f"{name}_core")
        set_location(core, 0, 0, 0.03)

    elif vfx_type == 'shockwave':
        outer = make_circle(0.42, 24)
        assign_material(outer, 0.7, 0.7, 1.0, f"{name}_outer")
        set_location(outer, 0, 0, 0.01)

        inner = make_circle(0.3, 20)
        assign_material(inner, 1.0, 0.9, 1.0, f"{name}_inner")
        set_location(inner, 0, 0, 0.015)

    render_and_key(name, "vfx")


def build_environment():
    """Arena floor tile, crate, barrel, wall."""
    os.makedirs(os.path.join(OUTPUT_DIR, "environment"), exist_ok=True)

    # ── Arena floor ──
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    # Large floor
    floor = make_rect(2.8, 2.8)
    assign_material(floor, 0.22, 0.22, 0.24, "floor_base")
    set_location(floor, 0, 0, -0.01)

    # Tile pattern
    ts = 0.28
    gap = 0.015
    for tx in range(-5, 6):
        for ty in range(-5, 6):
            tile = make_rect(ts, ts)
            odd = (tx + ty) % 2 == 0
            c = (0.20, 0.20, 0.22) if odd else (0.18, 0.18, 0.20)
            assign_material(tile, *c, "floor_tile")
            set_location(tile, tx * (ts + gap), ty * (ts + gap), 0.0)

    # Grid lines
    for i in range(-5, 6):
        hline = make_rect(2.8, 0.008)
        assign_material(hline, 0.12, 0.12, 0.13, "floor_grid_h")
        set_location(hline, 0, i * 0.28, -0.02)

        vline = make_rect(0.008, 2.8)
        assign_material(vline, 0.12, 0.12, 0.13, "floor_grid_v")
        set_location(vline, i * 0.28, 0, -0.02)

    render_and_key("arena_floor", "environment")

    # ── Crate ──
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    crate = make_round_rect(0.75, 0.75, 0.08, 8)
    assign_material(crate, 0.50, 0.30, 0.15, "crate_body")
    set_location(crate, 0, 0, 0.01)

    for i in range(3):
        plank = make_rect(0.65, 0.02)
        assign_material(plank, 0.35, 0.20, 0.10, "crate_plank")
        set_location(plank, 0, (i - 1) * 0.22, 0.02)

    for sx, sy in [(-1, -1), (1, -1), (-1, 1), (1, 1)]:
        bracket = make_round_rect(0.07, 0.07, 0.015)
        assign_material(bracket, 0.40, 0.25, 0.12, "crate_bracket")
        set_location(bracket, sx * 0.32, sy * 0.32, 0.02)

    render_and_key("crate", "environment")

    # ── Barrel ──
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    barrel = make_round_rect(0.55, 0.75, 0.04, 8)
    assign_material(barrel, 0.30, 0.30, 0.35, "barrel_body")
    set_location(barrel, 0, 0, 0.01)

    for i in range(3):
        band = make_rect(0.6, 0.025)
        assign_material(band, 0.45, 0.45, 0.50, "barrel_band")
        set_location(band, 0, (i - 1) * 0.22, 0.02)

    hole = make_circle(0.035, 8)
    assign_material(hole, 0.05, 0.05, 0.05, "barrel_hole")
    set_location(hole, 0, 0.32, 0.02)

    render_and_key("barrel", "environment")

    # ── Wall ──
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    wall = make_rect(0.75, 1.1)
    assign_material(wall, 0.55, 0.50, 0.45, "wall_body")
    set_location(wall, 0, 0, 0.01)

    for row in range(3):
        for col in range(2):
            brick = make_rect(0.3, 0.3)
            off_x = 0.08 if row % 2 == 0 else -0.08
            lx = -0.28 + col * 0.38 + off_x
            ly = row * 0.33 - 0.33
            is_light = (row + col) % 2 == 0
            c = (0.48, 0.43, 0.38) if is_light else (0.52, 0.47, 0.42)
            assign_material(brick, *c, "wall_brick")
            set_location(brick, lx, ly, 0.02)

    # Crack
    crack1 = make_rect(0.005, 0.28)
    assign_material(crack1, 0.18, 0.18, 0.18, "wall_crack")
    set_location(crack1, 0.08, -0.08, 0.03)
    crack1.rotation_euler = (0, 0, 0.15)

    crack2 = make_rect(0.005, 0.18)
    assign_material(crack2, 0.18, 0.18, 0.18, "wall_crack2")
    set_location(crack2, 0.14, 0.06, 0.03)
    crack2.rotation_euler = (0, 0, -0.2)

    render_and_key("wall", "environment")


def build_ui():
    """Health bar, powerup icon, wave banner, main menu bg."""
    os.makedirs(os.path.join(OUTPUT_DIR, "ui"), exist_ok=True)

    # ── Health bar bg ──
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    bg = make_round_rect(1.9, 0.18, 0.03)
    assign_material(bg, 0.15, 0.15, 0.15, "hb_bg")
    set_location(bg, 0, 0, 0.01)

    render_and_key("healthbar_bg", "ui")

    # ── Health bar fill ──
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    fill = make_round_rect(1.8, 0.13, 0.02)
    assign_material(fill, 0.8, 0.1, 0.1, "hb_fill")
    set_location(fill, 0, 0, 0.02)

    render_and_key("healthbar_fill", "ui")

    # ── Powerup icon bg ──
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    icon = make_round_rect(0.45, 0.45, 0.06)
    assign_material(icon, 0.1, 0.1, 0.1, "pu_bg")
    set_location(icon, 0, 0, 0.01)

    ring = make_circle(0.38, 18)
    assign_material(ring, 0.8, 0.7, 0.2, "pu_ring")
    set_location(ring, 0, 0, 0.02)

    render_and_key("powerup_icon_bg", "ui")

    # ── Wave banner ──
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    banner = make_round_rect(3.8, 0.9, 0.15)
    assign_material(banner, 0.05, 0.05, 0.08, "wave_bg")
    set_location(banner, 0, 0, 0.01)

    border = make_round_rect(3.85, 0.95, 0.15)
    assign_material(border, 0.8, 0.7, 0.2, "wave_border")
    set_location(border, 0, 0, 0.0)

    render_and_key("wave_banner", "ui")

    # ── Main menu bg ──
    clear_scene()
    setup_camera_lights()
    setup_world(BG_COLOR)
    set_render_settings()

    bg_panel = make_rect(5.6, 5.6)
    assign_material(bg_panel, 0.08, 0.08, 0.10, "mm_bg")
    set_location(bg_panel, 0, 0, -0.01)

    for i, r in enumerate([5.2, 4.2, 3.2, 2.2]):
        ring = make_circle(r, 36)
        intensity = 0.04 * (4 - i)
        assign_material(ring, 0.1 + i * 0.015, 0.08 + i * 0.01, 0.12 + i * 0.015,
                        f"mm_ring_{i}")
        set_location(ring, 0, 0, -0.005)

    render_and_key("mainmenu_bg", "ui")


# ── Helpers ───────────────────────────────────────────────────────────────────

def _rand(a, b):
    r = bpy.context
    # Use Blender's random via a simple approach
    import random
    return random.uniform(a, b)


def _rand_angle():
    import random
    return random.uniform(0, math.pi * 2)


def _pick(items):
    import random
    return random.choice(items)


# ── Enemy detail builders ─────────────────────────────────────────────────────

def _edrone(name, pal, w, h):
    for ang in range(0, 360, 90):
        rad = math.radians(ang)
        rotor = make_circle(min(w, h) * 0.28, 14)
        assign_material(rotor, pal['metal'][0], pal['metal'][1], pal['metal'][2],
                        f"{name}_rotor")
        set_location(rotor, math.cos(rad) * w * 0.38, math.sin(rad) * h * 0.38, 0.025)


def _ebrute(name, pal, w, h):
    for sx, sy in [(-1, -1), (1, -1), (-1, 1), (1, 1)]:
        plate = make_round_rect(w * 0.2, h * 0.16, w * 0.03)
        assign_material(plate, pal['metal'][0], pal['metal'][1], pal['metal'][2],
                        f"{name}_plate")
        set_location(plate, sx * w * 0.28, sy * h * 0.22, 0.025)


def _esprinter(name, pal, w, h):
    for i in range(3):
        stripe = make_rect(w * 0.12, h * 0.025)
        assign_material(stripe, pal['accent'][0], pal['accent'][1], pal['accent'][2],
                        f"{name}_stripe")
        set_location(stripe, 0, (i - 1) * h * 0.12, 0.02)


def _eartillery(name, pal, w, h):
    barrel = make_round_rect(w * 0.12, h * 0.35, w * 0.04)
    assign_material(barrel, pal['metal'][0], pal['metal'][1], pal['metal'][2],
                    f"{name}_barrel")
    set_location(barrel, 0, h * 0.3, 0.02)

    base = make_round_rect(w * 0.5, h * 0.12, w * 0.04)
    assign_material(base, pal['accent'][0], pal['accent'][1], pal['accent'][2],
                    f"{name}_base")
    set_location(base, 0, -h * 0.18, 0.02)


def _eshielder(name, pal, w, h):
    shield = make_circle(min(w, h) * 0.65, 20)
    assign_material(shield, 0.25, 0.25, 0.65, f"{name}_shield")
    set_location(shield, 0, 0, 0.005)
    shield.scale = (1.3, 1.3, 1.0)


def _ehealer(name, pal, w, h):
    halo = make_circle(min(w, h) * 0.6, 18)
    assign_material(halo, 0.25, 0.85, 0.25, f"{name}_halo")
    set_location(halo, 0, 0, 0.005)
    halo.scale = (1.2, 1.2, 1.0)


def _eexploder(name, pal, w, h):
    for i in range(8):
        ang = (i / 8) * math.pi * 2
        spike = make_circle(min(w, h) * 0.07, 5)
        assign_material(spike, pal['accent'][0], pal['accent'][1], pal['accent'][2],
                        f"{name}_spike")
        set_location(spike, math.cos(ang) * w * 0.52, math.sin(ang) * h * 0.52, 0.02)


def _eburrower(name, pal, w, h):
    for i in range(3):
        seg = make_round_rect(w * 0.45, h * 0.14, w * 0.04)
        assign_material(seg, pal['accent'][0], pal['accent'][1], pal['accent'][2],
                        f"{name}_seg")
        set_location(seg, 0, (i - 1) * h * 0.2, 0.02)


def _eparasite(name, pal, w, h):
    for ang in range(0, 360, 60):
        rad = math.radians(ang)
        tent = make_circle(min(w, h) * 0.035, 5)
        assign_material(tent, pal['accent'][0], pal['accent'][1], pal['accent'][2],
                        f"{name}_tent")
        set_location(tent, math.cos(rad) * w * 0.48, math.sin(rad) * h * 0.48, 0.01)


def _eapex(name, pal, w, h):
    for ang_deg in (45, 135, 225, 315):
        rad = math.radians(ang_deg)
        horn = make_round_rect(w * 0.08, h * 0.22, w * 0.025)
        assign_material(horn, pal['metal'][0], pal['metal'][1], pal['metal'][2],
                        f"{name}_horn")
        set_location(horn, math.cos(rad) * w * 0.48, math.sin(rad) * h * 0.48, 0.03)

    for sx, sy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
        plate = make_round_rect(w * 0.18, h * 0.18, w * 0.02)
        assign_material(plate, pal['metal'][0], pal['metal'][1], pal['metal'][2],
                        f"{name}_plate")
        set_location(plate, sx * w * 0.32, sy * h * 0.32, 0.025)


# ── Palettes & specs ──────────────────────────────────────────────────────────

PALETTE = {
    "atlas":  {"body": (0.85, 0.35, 0.18), "accent": (0.60, 0.15, 0.08),
               "metal": (0.70, 0.60, 0.50), "eye": (1.0, 1.0, 1.0),
               "outline": (0.20, 0.08, 0.04)},
    "zephyr": {"body": (0.18, 0.55, 0.82), "accent": (0.08, 0.38, 0.65),
               "metal": (0.75, 0.80, 0.90), "eye": (1.0, 1.0, 1.0),
               "outline": (0.04, 0.18, 0.32)},
    "synapse":{"body": (0.68, 0.22, 0.68), "accent": (0.48, 0.08, 0.48),
               "metal": (0.50, 0.72, 0.50), "eye": (0.25, 1.0, 0.25),
               "outline": (0.28, 0.08, 0.28)},
    "volt":   {"body": (0.90, 0.75, 0.08), "accent": (0.68, 0.52, 0.0),
               "metal": (0.95, 0.90, 0.70), "eye": (1.0, 1.0, 0.18),
               "outline": (0.38, 0.28, 0.0)},
    "drone":    {"body": (0.50, 0.50, 0.55), "accent": (0.35, 0.35, 0.40),
                 "metal": (0.60, 0.60, 0.65), "eye": (1.0, 0.18, 0.18),
                 "outline": (0.12, 0.12, 0.18)},
    "brute":    {"body": (0.55, 0.13, 0.08), "accent": (0.35, 0.07, 0.04),
                 "metal": (0.45, 0.35, 0.30), "eye": (1.0, 0.78, 0.18),
                 "outline": (0.12, 0.04, 0.02)},
    "sprinter": {"body": (0.85, 0.28, 0.12), "accent": (0.58, 0.12, 0.04),
                 "metal": (0.70, 0.60, 0.50), "eye": (1.0, 1.0, 0.25),
                 "outline": (0.18, 0.06, 0.02)},
    "artillery":{"body": (0.28, 0.28, 0.33), "accent": (0.18, 0.18, 0.23),
                 "metal": (0.50, 0.50, 0.55), "eye": (1.0, 0.45, 0.08),
                 "outline": (0.08, 0.08, 0.10)},
    "shielder": {"body": (0.22, 0.22, 0.58), "accent": (0.12, 0.12, 0.38),
                 "metal": (0.55, 0.60, 0.80), "eye": (1.0, 0.88, 0.28),
                 "outline": (0.06, 0.06, 0.22)},
    "healer":   {"body": (0.18, 0.58, 0.22), "accent": (0.08, 0.38, 0.12),
                 "metal": (0.50, 0.75, 0.55), "eye": (1.0, 1.0, 0.45),
                 "outline": (0.06, 0.18, 0.08)},
    "exploder": {"body": (0.85, 0.12, 0.08), "accent": (0.58, 0.04, 0.02),
                 "metal": (0.50, 0.30, 0.25), "eye": (1.0, 0.35, 0.08),
                 "outline": (0.18, 0.02, 0.01)},
    "burrower": {"body": (0.45, 0.33, 0.12), "accent": (0.23, 0.16, 0.06),
                 "metal": (0.55, 0.45, 0.30), "eye": (0.75, 0.55, 0.08),
                 "outline": (0.10, 0.06, 0.02)},
    "parasite": {"body": (0.38, 0.12, 0.38), "accent": (0.23, 0.06, 0.23),
                 "metal": (0.50, 0.30, 0.50), "eye": (1.0, 0.08, 0.75),
                 "outline": (0.10, 0.03, 0.10)},
    "apex":     {"body": (0.12, 0.08, 0.12), "accent": (0.06, 0.03, 0.06),
                 "metal": (0.58, 0.12, 0.12), "eye": (1.0, 0.08, 0.08),
                 "outline": (0.04, 0.02, 0.04)},
    "crate":    {"body": (0.50, 0.30, 0.15), "accent": (0.35, 0.20, 0.10),
                 "metal": (0.40, 0.25, 0.12), "eye": (0, 0, 0),
                 "outline": (0.12, 0.06, 0.03)},
    "barrel":   {"body": (0.30, 0.30, 0.35), "accent": (0.22, 0.22, 0.27),
                 "metal": (0.45, 0.45, 0.50), "eye": (0, 0, 0),
                 "outline": (0.08, 0.08, 0.10)},
    "wall":     {"body": (0.55, 0.50, 0.45), "accent": (0.40, 0.35, 0.30),
                 "metal": (0.50, 0.45, 0.40), "eye": (0, 0, 0),
                 "outline": (0.12, 0.10, 0.08)},
}

ASPECT = {
    "atlas": (1, 1),    "zephyr": (1, 1),   "synapse": (1, 1),  "volt": (1, 1),
    "drone": (1, 1),    "brute": (1, 1.05), "sprinter": (1.15, 0.75),
    "artillery": (0.9, 1.1), "shielder": (1, 1),
    "healer": (1, 1.05), "exploder": (1, 1), "burrower": (0.9, 1.25),
    "parasite": (1, 1), "apex": (1, 1),
    "crate": (1, 1),    "barrel": (0.65, 1), "wall": (0.45, 1),
}

SIZES = {
    "atlas": 1.0,     "zephyr": 0.85,   "synapse": 0.9,   "volt": 0.8,
    "drone": 0.38,    "brute": 1.15,    "sprinter": 0.55, "artillery": 0.9,
    "shielder": 0.95, "healer": 0.65,   "exploder": 0.55, "burrower": 0.65,
    "parasite": 0.28, "apex": 1.7,
    "crate": 0.6,     "barrel": 0.5,    "wall": 0.75,
}

HERO_DETAILS = {
    "atlas":  _edrone,  # placeholder — will override below
    "zephyr": None,
    "synapse": None,
    "volt":   None,
}

ENEMY_DETAILS = {
    "drone":    _edrone,
    "brute":    _ebrute,
    "sprinter": _esprinter,
    "artillery": _eartillery,
    "shielder": _eshielder,
    "healer":   _ehealer,
    "exploder": _eexploder,
    "burrower": _eburrower,
    "parasite": _eparasite,
    "apex":     _eapex,
}


# ── Main ───────────────────────────────────────────────────────────────────────

def main():
    global OUTPUT_DIR

    args = sys.argv
    if "--output-dir" in args:
        idx = args.index("--output-dir")
        OUTPUT_DIR = args[idx + 1]
    else:
        script_dir = os.path.dirname(os.path.abspath(__file__))
        OUTPUT_DIR = os.path.join(script_dir, "generated_assets_v2")

    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"Output: {OUTPUT_DIR}/")
    print(f"Resolution: {RESOLUTION}x{RESOLUTION}")
    print(f"BG color: {BG_COLOR}  threshold: {BG_THRESHOLD}")
    print()

    total_start = bpy.context.scene.frame_current  # dummy reference

    # ── Heroes ──
    print("─" * 50)
    print("HEROES")
    print("─" * 50)
    for hero_name in ["atlas", "zephyr", "synapse", "volt"]:
        print(f"  {hero_name}...")
        build_hero(hero_name, hero_name)

    # ── Enemies ──
    print()
    print("─" * 50)
    print("ENEMIES")
    print("─" * 50)
    for enemy_name in ["drone", "brute", "sprinter", "artillery",
                       "shielder", "healer", "exploder", "burrower",
                       "parasite", "apex"]:
        print(f"  {enemy_name}...")
        build_enemy(enemy_name, enemy_name, ENEMY_DETAILS.get(enemy_name))

    # ── Projectiles ──
    print()
    print("─" * 50)
    print("PROJECTILES")
    print("─" * 50)
    proj_specs = [
        ("standard", "volt", False),
        ("kinetic", "atlas", False),
        ("energy", "zephyr", False),
        ("lightning", "volt", False),
        ("acid", "healer", False),
        ("fire", "exploder", False),
        ("explosive", "apex", False),
        ("synapse_bolt", "synapse", True),
        ("zephyr_wind", "zephyr", True),
        ("atlas_slam", "atlas", True),
        ("volt_chain", "volt", True),
    ]
    for pname, palette, is_ability in proj_specs:
        print(f"  {pname}...")
        build_projectile(pname, palette, is_ability)

    # ── Decals ──
    print()
    print("─" * 50)
    print("DECALS")
    print("─" * 50)
    for dtype in ["blood", "scorch", "crater", "acid", "explosion"]:
        print(f"  {dtype}...")
        build_decal(dtype, dtype)

    # ── VFX ──
    print()
    print("─" * 50)
    print("VFX")
    print("─" * 50)
    for vname, vtype in [
        ("hitflash", "hitflash"),
        ("explosion", "explosion"),
        ("smoke", "smoke"),
        ("sparks", "sparks"),
        ("shockwave", "shockwave"),
    ]:
        print(f"  {vname}...")
        build_vfx(vname, vtype)

    # ── Environment ──
    print()
    print("─" * 50)
    print("ENVIRONMENT")
    print("─" * 50)
    build_environment()

    # ── UI ──
    print()
    print("─" * 50)
    print("UI")
    print("─" * 50)
    build_ui()

    # ── Summary ──
    print()
    print("=" * 60)
    count = 0
    for root, dirs, files in os.walk(OUTPUT_DIR):
        for f in files:
            if f.endswith('.png') and not f.startswith('_raw_'):
                count += 1
    print(f"Done! {count} PNG sprites in {OUTPUT_DIR}/")
    print("=" * 60)


if __name__ == "__main__":
    main()
