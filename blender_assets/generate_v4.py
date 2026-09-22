#!/usr/bin/env python3
"""
Hero's Arena — Blender Asset Generator — FINAL v4
==================================================
Proven pipeline:
  1. Black void background (no nodes = pure black)
  2. Principled BSDF materials (render color correctly)
  3. Render @ 512×512 with film_transparent=False
  4. Post: adaptive chroma-key turns black→transparent

Run:
  blender --background --python generate_v4.py -- --output-dir ./assets
"""

import bpy, os, sys, math, struct, zlib, random
from collections import Counter

# ── Config ───────────────────────────────────────────────
RES = 512
OUTPUT_DIR = None
KEY_THRESHOLD = 45.0   # RGB distance (0-255 space) to consider "background"

BG_COLOR = (0.0, 0.0, 0.0)  # pure black void

# ── Color palettes ───────────────────────────────────────
P = {
    "atlas":  dict(body=(0.85,0.35,0.18), acc=(0.60,0.15,0.08), met=(0.70,0.60,0.50),
                   eye=(1,1,1), out=(0.20,0.08,0.04)),
    "zephyr": dict(body=(0.18,0.55,0.82), acc=(0.08,0.38,0.65), met=(0.75,0.80,0.90),
                   eye=(1,1,1), out=(0.04,0.18,0.32)),
    "synapse":dict(body=(0.68,0.22,0.68), acc=(0.48,0.08,0.48), met=(0.50,0.72,0.50),
                   eye=(0.25,1,0.25), out=(0.28,0.08,0.28)),
    "volt":   dict(body=(0.90,0.75,0.08), acc=(0.68,0.52,0.0),  met=(0.95,0.90,0.70),
                   eye=(1,1,0.18), out=(0.38,0.28,0.0)),
    "drone":    dict(body=(0.50,0.50,0.55), acc=(0.35,0.35,0.40), met=(0.60,0.60,0.65),
                    eye=(1,0.18,0.18), out=(0.12,0.12,0.18)),
    "brute":    dict(body=(0.55,0.13,0.08), acc=(0.35,0.07,0.04), met=(0.45,0.35,0.30),
                    eye=(1,0.78,0.18), out=(0.12,0.04,0.02)),
    "sprinter": dict(body=(0.85,0.28,0.12), acc=(0.58,0.12,0.04), met=(0.70,0.60,0.50),
                    eye=(1,1,0.25), out=(0.18,0.06,0.02)),
    "artillery":dict(body=(0.28,0.28,0.33), acc=(0.18,0.18,0.23), met=(0.50,0.50,0.55),
                    eye=(1,0.45,0.08), out=(0.08,0.08,0.10)),
    "shielder": dict(body=(0.22,0.22,0.58), acc=(0.12,0.12,0.38), met=(0.55,0.60,0.80),
                    eye=(1,0.88,0.28), out=(0.06,0.06,0.22)),
    "healer":   dict(body=(0.18,0.58,0.22), acc=(0.08,0.38,0.12), met=(0.50,0.75,0.55),
                    eye=(1,1,0.45), out=(0.06,0.18,0.08)),
    "exploder": dict(body=(0.85,0.12,0.08), acc=(0.58,0.04,0.02), met=(0.50,0.30,0.25),
                    eye=(1,0.35,0.08), out=(0.18,0.02,0.01)),
    "burrower": dict(body=(0.45,0.33,0.12), acc=(0.23,0.16,0.06), met=(0.55,0.45,0.30),
                    eye=(0.75,0.55,0.08), out=(0.10,0.06,0.02)),
    "parasite": dict(body=(0.38,0.12,0.38), acc=(0.23,0.06,0.23), met=(0.50,0.30,0.50),
                    eye=(1,0.08,0.75), out=(0.10,0.03,0.10)),
    "apex":     dict(body=(0.12,0.08,0.12), acc=(0.06,0.03,0.06), met=(0.58,0.12,0.12),
                    eye=(1,0.08,0.08), out=(0.04,0.02,0.04)),
}

ASPECT = {
    "atlas":(1,1),"zephyr":(1,1),"synapse":(1,1),"volt":(1,1),
    "drone":(1,1),"brute":(1,1.05),"sprinter":(1.15,0.75),"artillery":(0.9,1.1),
    "shielder":(1,1),"healer":(1,1.05),"exploder":(1,1),"burrower":(0.9,1.25),
    "parasite":(1,1),"apex":(1,1),
}

SIZE = {
    "atlas":1.0,"zephyr":0.85,"synapse":0.9,"volt":0.8,
    "drone":0.38,"brute":1.15,"sprinter":0.55,"artillery":0.9,
    "shielder":0.95,"healer":0.65,"exploder":0.55,"burrower":0.65,
    "parasite":0.28,"apex":1.7,
}

PROJ_SPECS = [
    ('standard','volt',False),
    ('kinetic','atlas',False),
    ('energy','zephyr',False),
    ('lightning','volt',False),
    ('acid','healer',False),
    ('fire','exploder',False),
    ('explosive','apex',False),
    ('synapse_bolt','synapse',True),
    ('zephyr_wind','zephyr',True),
    ('atlas_slam','atlas',True),
    ('volt_chain','volt',True),
]


# ── Chroma-key (proven: works with black bg) ────────────
def chromakey(inpath, outpath):
    """
    Read raw PNG, detect dominant background color from alpha=0 pixels,
    replace all pixels close to that color (RGB distance) with alpha=0.
    """
    with open(inpath, 'rb') as f:
        f.read(8)  # PNG signature
        idat = b''
        w = h = 0
        while True:
            ln = struct.unpack('>I', f.read(4))[0]
            ct = f.read(4)
            d = f.read(ln)
            f.read(4)  # CRC
            if ct == b'IHDR':
                w = struct.unpack('>I', d[0:4])[0]
                h = struct.unpack('>I', d[4:8])[0]
            elif ct == b'IEND':
                break
            else:
                idat += d

    raw = zlib.decompress(idat)
    bpp = 4
    row_bytes = 1 + w * bpp  # filter byte + pixels

    # Decode rows
    rows = []
    for y in range(h):
        off = y * row_bytes
        ft = raw[off]  # filter type
        cur = bytearray(raw[off+1:off+row_bytes])
        if ft == 1:  # sub
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        rows.append(bytes(cur))

    # Sample background from center edge region (where bg dominates)
    bg_cc = Counter()
    for y in range(0, h, 2):       # every other row
        cur = rows[y]
        for x in range(0, w, 2):
            i = x * bpp
            r, g, b, a = cur[i], cur[i+1], cur[i+2], cur[i+3]
            if a == 0:
                bg_cc[(r, g, b)] += 1

    if not bg_cc:
        # No alpha=0 pixels found — copy as-is (shouldn't happen with black bg)
        import shutil; shutil.copy(inpath, outpath)
        return 0, 0, 0.0

    # Dominant background color
    (br, bg_c, bb), _ = bg_cc.most_common(1)[0]
    print(f"    bg detected: RGB({br},{bg_c},{bb})", end="")

    # Chroma-key: replace pixels close to bg with alpha=0
    new_raw_parts = []
    opaque = transparent = 0
    th_sq = KEY_THRESHOLD * KEY_THRESHOLD

    for y in range(h):
        cur = rows[y]
        nr = bytearray([0])  # filter byte = None
        for x in range(w):
            i = x * bpp
            r, g, b = cur[i], cur[i+1], cur[i+2]
            # RGB distance squared
            dist_sq = (r-br)*(r-br) + (g-bg_c)*(g-bg_c) + (b-bb)*(b-bb)
            if dist_sq <= th_sq:
                nr.extend([r, g, b, 0])
                transparent += 1
            else:
                nr.extend([r, g, b, 255])
                opaque += 1
        new_raw_parts.append(bytes(nr))

    new_raw = b''.join(new_raw_parts)
    comp = zlib.compress(new_raw)

    # Write new PNG
    import binascii
    with open(outpath, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n')

        def write_chunk(ctype, data):
            f.write(struct.pack('>I', len(data)))
            f.write(ctype)
            f.write(data)
            f.write(struct.pack('>I', binascii.crc32(ctype + data) & 0xFFFFFFFF))

        write_chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
        write_chunk(b'IDAT', comp)
        write_chunk(b'IEND', b'')

    total = opaque + transparent
    pct = 100.0 * opaque / total if total > 0 else 0.0
    print(f"  → {opaque}/{total} opaque ({pct:.1f}%)")
    return opaque, transparent, pct


# ── Blender scene helpers ────────────────────────────────
_mat_cache = {}

def _mat(r, g, b, name="m"):
    """Get or create a Principled BSDF material."""
    key = (r, g, b, name)
    if key in _mat_cache:
        return _mat_cache[key]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nodes = m.node_tree.nodes
    for n in nodes:
        nodes.remove(n)
    bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = (r, g, b, 1.0)
    bsdf.inputs['Roughness'].default_value = 0.85
    out_n = nodes.new(type='ShaderNodeOutputMaterial')
    m.node_tree.links.new(bsdf.outputs['BSDF'], out_n.inputs['Surface'])
    _mat_cache[key] = m
    return m


def clear_scene():
    """Remove all objects, materials, and world from the scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    _mat_cache.clear()
    for w in list(bpy.data.worlds):
        bpy.data.worlds.remove(w)


def setup_scene():
    """Camera + black void world + render settings."""
    # Camera — orthographic, looking down
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'
    cd.ortho_scale = 3.0
    co = bpy.data.objects.new('cam', cd)
    co.location = (0, 0, 5.0)
    co.rotation_euler = (math.pi / 2, 0.0, 0.0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co

    # World = pure black void (no background node)
    world = bpy.data.worlds.new('void')
    bpy.context.scene.world = world
    world.use_nodes = True
    for n in world.node_tree.nodes:
        world.node_tree.nodes.remove(n)
    # No background node = pure black with no emitted light

    # Render settings
    s = bpy.context.scene
    s.render.resolution_x = RES
    s.render.resolution_y = RES
    s.render.resolution_percentage = 100
    s.render.film_transparent = False
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    s.render.image_settings.compression = 15


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def make_mat(r, g, b, label=""):
    return _mat(r, g, b, label)


def set_mesh_mat(mesh_obj, mat):
    mesh_obj.data.materials.clear()
    mesh_obj.data.materials.append(mat)


def circ(radius, vertices=28, fill='TRIFAN'):
    bpy.ops.mesh.primitive_circle_add(vertices=vertices, radius=radius, fill_type=fill)
    obj = bpy.context.active_object
    obj.location.z = 0.003
    return obj


def rrect(w, h, rounded=0.08, vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices * 4, radius=1.0, depth=0.02)
    obj = bpy.context.active_object
    obj.scale = (w / 2, h / 2, 1.0)
    obj.location.z = 0.003
    return obj


def plane(w, h):
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    obj = bpy.context.active_object
    obj.scale = (w / 2, h / 2, 1.0)
    obj.location.z = 0.003
    return obj


def put(obj, x, y, z=0.003):
    obj.location = (x, y, z)


def eye(p, x, y, r, label="eye"):
    o = circ(r, 14)
    put(o, x, y)
    set_mesh_mat(o, make_mat(p['eye'][0], p['eye'][1], p['eye'][2], label))
    return o


def body(p, w, h, shape='round', label="body"):
    if shape == 'round':
        o = circ(min(w, h) * 0.88, 28)
    elif shape == 'rect':
        o = rrect(w, h, min(w, h) * 0.12)
    elif shape == 'ellipse':
        o = rrect(w * 0.88, h * 1.15, min(w, h) * 0.1)
    elif shape == 'wide':
        o = rrect(w * 1.1, h * 0.7, min(w, h) * 0.08)
    elif shape == 'tall':
        o = rrect(w * 0.85, h * 1.2, min(w, h) * 0.1)
    else:
        o = circ(min(w, h) * 0.88, 28)
    put(o, 0, 0)
    set_mesh_mat(o, make_mat(p['body'][0], p['body'][1], p['body'][2], label))
    return o


def outline(p, w, h, label="out"):
    o = rrect(w * 1.06, h * 1.06, min(w, h) * 0.08)
    put(o, 0, 0, -0.004)
    set_mesh_mat(o, make_mat(p['out'][0], p['out'][1], p['out'][2], label))
    return o


def accent(p, w, h, label="acc"):
    o = rrect(w * 0.58, h * 0.08, min(w, h) * 0.02)
    put(o, 0, 0, 0.009)
    set_mesh_mat(o, make_mat(p['acc'][0], p['acc'][1], p['acc'][2], label))
    return o


# ── Generic render-and-key helper ────────────────────────
def do_render(name, subdir, build_fn):
    """Build scene with build_fn(), render, chroma-key, clean up."""
    _mat_cache.clear()  # fresh material cache for each sprite
    path = os.path.join(OUTPUT_DIR, subdir)
    os.makedirs(path, exist_ok=True)
    raw_path = os.path.join(path, f"_raw_{name}.png")
    final_path = os.path.join(path, f"{name}.png")

    clear_scene()
    setup_scene()
    build_fn()
    render(raw_path)
    op, tr, pct = chromakey(raw_path, final_path)
    os.remove(raw_path)
    print(f"  ✓ {subdir}/{name}.png")
    return op, tr, pct


# ── HERO BUILDERS ────────────────────────────────────────
def build_hero(name):
    p = P[name]
    w = h = SIZE[name]

    def build():
        outline(p, w, h)
        body(p, w, h)
        accent(p, w, h)

        # Eyes
        es = w * 0.07
        eye(p, -w * 0.22, 0, es, f"{name}_eye_l")
        eye(p, w * 0.22, 0, es, f"{name}_eye_r")

        # Hero-specific details
        if name == 'atlas':
            # Diamond chest emblem
            e = circ(w * 0.22, 8)
            put(e, 0, 0, 0.012)
            set_mesh_mat(e, make_mat(p['met'][0], p['met'][1], p['met'][2], 'emblem'))
            e.scale = (1.0, 0.3, 1.0)

        elif name == 'zephyr':
            # Wing blades
            for sx in (-1, 1):
                wl = rrect(w * 0.32, h * 0.1, w * 0.025)
                put(wl, sx * w * 0.28, 0, 0.01)
                set_mesh_mat(wl, make_mat(p['acc'][0], p['acc'][1], p['acc'][2], 'wing'))

        elif name == 'synapse':
            # Outer glow ring
            hl = circ(w * 0.58, 22)
            put(hl, 0, 0, 0.002)
            set_mesh_mat(hl, make_mat(p['met'][0] * 0.5, p['met'][1] * 0.5, p['met'][2] * 0.5, 'halo_outer'))
            hl.scale.x = 1.15; hl.scale.y = 1.15; hl.scale.z = 1.0
            # Inner glow
            il = circ(w * 0.42, 16)
            put(il, 0, 0, 0.003)
            set_mesh_mat(il, make_mat(p['met'][0], p['met'][1], p['met'][2], 'halo_inner'))
            il.scale.x = 1.1; il.scale.y = 1.1; il.scale.z = 1.0

        elif name == 'volt':
            # Lightning spikes around body
            for ang_deg in range(0, 360, 45):
                ang = math.radians(ang_deg)
                sp = circ(w * 0.05, 5)
                put(sp, math.cos(ang) * w * 0.55, math.sin(ang) * w * 0.55, 0.015)
                set_mesh_mat(sp, make_mat(1.0, 0.85, 0.1, f'spike_{ang_deg}'))

    do_render(name, 'heroes', build)


# ── ENEMY BUILDERS ───────────────────────────────────────
def build_enemy(name):
    p = P[name]
    w = SIZE[name] * ASPECT[name][0]
    h = SIZE[name] * ASPECT[name][1]

    def build():
        outline(p, w, h)

        # Body shape by enemy type
        if name in ('drone', 'parasite', 'shielder', 'exploder', 'apex'):
            body(p, w, h, 'round')
        elif name in ('brute', 'healer'):
            body(p, w, h, 'rect')
        elif name == 'sprinter':
            body(p, w, h, 'wide')
        elif name == 'artillery':
            body(p, w, h, 'tall')
        elif name == 'burrower':
            body(p, w, h, 'tall')
        else:
            body(p, w, h, 'round')

        accent(p, w, h)

        # Eyes
        es = min(w, h) * 0.075
        eye(p, -es * 1.6, 0, es, f"{name}_eye_l")
        eye(p, es * 1.6, 0, es, f"{name}_eye_r")

        # Enemy-specific details
        if name == 'drone':
            # Rotors at corners
            for ang_deg in (0, 90, 180, 270):
                ang = math.radians(ang_deg)
                rt = circ(min(w, h) * 0.22, 14)
                put(rt, math.cos(ang) * w * 0.4, math.sin(ang) * h * 0.4, 0.015)
                set_mesh_mat(rt, make_mat(p['met'][0], p['met'][1], p['met'][2], 'rotor'))

        elif name == 'brute':
            # Shoulder plates
            for sx, sy in [(-1, -1), (1, -1), (-1, 1), (1, 1)]:
                pt = rrect(w * 0.18, h * 0.14, w * 0.025)
                put(pt, sx * w * 0.25, sy * h * 0.2, 0.01)
                set_mesh_mat(pt, make_mat(p['met'][0], p['met'][1], p['met'][2], 'plate'))

        elif name == 'sprinter':
            # Speed lines
            for i in range(3):
                sl = plane(w * 0.1, h * 0.022)
                put(sl, 0, (i - 1) * h * 0.1, 0.01)
                set_mesh_mat(sl, make_mat(p['acc'][0], p['acc'][1], p['acc'][2], 'stripe'))

        elif name == 'artillery':
            # Cannon barrel
            bt = rrect(w * 0.1, h * 0.3, w * 0.03)
            put(bt, 0, h * 0.25, 0.012)
            set_mesh_mat(bt, make_mat(p['met'][0], p['met'][1], p['met'][2], 'barrel'))
            # Base ring
            bs = rrect(w * 0.48, h * 0.08, w * 0.03)
            put(bs, 0, -h * 0.15, 0.01)
            set_mesh_mat(bs, make_mat(p['acc'][0], p['acc'][1], p['acc'][2], 'base'))

        elif name == 'shielder':
            # Shield arc in front
            sh = circ(min(w, h) * 0.58, 18)
            put(sh, 0, -h * 0.1, 0.001)
            set_mesh_mat(sh, make_mat(0.25, 0.25, 0.65, 'shield'))
            sh.scale.x = 1.35; sh.scale.y = 1.35; sh.scale.z = 1.0

        elif name == 'healer':
            # Healing halo
            hl = circ(min(w, h) * 0.55, 16)
            put(hl, 0, 0, 0.003)
            set_mesh_mat(hl, make_mat(p['met'][0], p['met'][1], p['met'][2], 'halo'))
            hl.scale.x = 1.2; hl.scale.y = 1.2; hl.scale.z = 1.0

        elif name == 'exploder':
            # Spikes
            for i in range(8):
                ang = (i / 8) * math.pi * 2
                sp = circ(min(w, h) * 0.06, 5)
                put(sp, math.cos(ang) * w * 0.48, math.sin(ang) * h * 0.48, 0.012)
                set_mesh_mat(sp, make_mat(p['acc'][0], p['acc'][1], p['acc'][2], f'spike_{i}'))

        elif name == 'burrower':
            # Segment lines
            for i in range(3):
                sg = rrect(w * 0.38, h * 0.12, w * 0.03)
                put(sg, 0, (i - 1) * h * 0.2, 0.01)
                set_mesh_mat(sg, make_mat(p['acc'][0], p['acc'][1], p['acc'][2], 'seg'))

        elif name == 'parasite':
            # Tentacles
            for ang_deg in range(0, 360, 45):
                ang = math.radians(ang_deg)
                tn = circ(min(w, h) * 0.025, 5)
                put(tn, math.cos(ang) * w * 0.46, math.sin(ang) * h * 0.46, 0.006)
                set_mesh_mat(tn, make_mat(p['acc'][0], p['acc'][1], p['acc'][2], 'tent'))

        elif name == 'apex':
            # Horns
            for ang_deg in (45, 135, 225, 315):
                ang = math.radians(ang_deg)
                hn = rrect(w * 0.07, h * 0.18, w * 0.018)
                put(hn, math.cos(ang) * w * 0.42, math.sin(ang) * h * 0.42, 0.015)
                set_mesh_mat(hn, make_mat(p['met'][0], p['met'][1], p['met'][2], 'horn'))
            # Flanker plates
            for sx, sy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
                pt = rrect(w * 0.14, h * 0.14, w * 0.012)
                put(pt, sx * w * 0.28, sy * h * 0.28, 0.012)
                set_mesh_mat(pt, make_mat(p['met'][0], p['met'][1], p['met'][2], 'flank'))

    do_render(name, 'enemies', build)


# ── PROJECTILE BUILDERS ──────────────────────────────────
def build_projectile(name, palette, ability):
    p = P[palette]

    def build():
        if ability:
            # Glowing orb
            gl = circ(0.3, 22)
            put(gl, 0, 0, 0.003)
            set_mesh_mat(gl, make_mat(p['acc'][0], p['acc'][1], p['acc'][2], 'glow'))
            bd = circ(0.18, 18)
            put(bd, 0, 0, 0.008)
            set_mesh_mat(bd, make_mat(p['body'][0], p['body'][1], p['body'][2], 'body'))
            co = circ(0.06, 12)
            put(co, 0, 0, 0.012)
            set_mesh_mat(co, make_mat(1.0, 1.0, 1.0, 'core'))
        else:
            # Small filled projectile with trail
            bd = circ(0.12, 14)
            put(bd, 0, 0, 0.007)
            set_mesh_mat(bd, make_mat(p['body'][0], p['body'][1], p['body'][2], 'body'))
            # Trail line behind
            tr = plane(0.06, 0.02)
            put(tr, -0.07, 0, 0.003)
            set_mesh_mat(tr, make_mat(p['body'][0] * 0.35, p['body'][1] * 0.35, p['body'][2] * 0.35, 'trail'))

    do_render(name, 'projectiles', build)


# ── DECAL BUILDERS ───────────────────────────────────────
def build_decal(name, dtype):
    def build():
        if dtype == 'blood':
            # Central pool + scattered splats
            pl = circ(0.14, 14)
            put(pl, 0, 0, 0.003)
            set_mesh_mat(pl, make_mat(0.45, 0.02, 0.02, 'pool'))
            for _ in range(5):
                a = random.uniform(0, math.pi * 2)
                d = random.uniform(0.08, 0.28)
                bl = circ(random.uniform(0.025, 0.065), 7)
                put(bl, math.cos(a) * d, math.sin(a) * d, 0.002)
                set_mesh_mat(bl, make_mat(0.55, 0.03, 0.03, 'splat'))

        elif dtype == 'scorch':
            # Burnt circle
            o = circ(0.28, 18)
            put(o, 0, 0, 0.001)
            set_mesh_mat(o, make_mat(0.04, 0.04, 0.03, 'outer'))
            i = circ(0.14, 14)
            put(i, 0, 0, 0.004)
            set_mesh_mat(i, make_mat(0.08, 0.06, 0.04, 'inner'))
            for _ in range(4):
                m = circ(random.uniform(0.015, 0.035), 5)
                put(m, random.uniform(-0.13, 0.13), random.uniform(-0.13, 0.13), 0.006)
                set_mesh_mat(m, make_mat(0.02, 0.02, 0.02, 'char'))

        elif dtype == 'crater':
            # Impact crater
            rm = circ(0.32, 22)
            put(rm, 0, 0, 0.001)
            set_mesh_mat(rm, make_mat(0.25, 0.19, 0.13, 'rim'))
            fl = circ(0.22, 18)
            put(fl, 0, 0, 0.003)
            set_mesh_mat(fl, make_mat(0.08, 0.06, 0.05, 'floor'))
            for _ in range(5):
                rk = circ(random.uniform(0.01, 0.03), 5)
                an = random.uniform(0, math.pi * 2)
                di = random.uniform(0.28, 0.36)
                put(rk, math.cos(an) * di, math.sin(an) * di, random.uniform(0.003, 0.006))
                set_mesh_mat(rk, make_mat(0.28, 0.23, 0.18, 'debris'))

        elif dtype == 'acid':
            # Acid pool with ripples
            pl = circ(0.28, 16)
            put(pl, 0, 0, 0.001)
            set_mesh_mat(pl, make_mat(0.05, 0.38, 0.05, 'pool'))
            for i in range(3):
                rp = circ(0.14 + i * 0.035, 14)
                put(rp, 0, 0, 0.003)
                set_mesh_mat(rp, make_mat(0.07, 0.45, 0.07, f'ripple_{i}'))

        elif dtype == 'explosion':
            # Scorch mark from explosion
            o = circ(0.32, 22)
            put(o, 0, 0, 0.001)
            set_mesh_mat(o, make_mat(0.08, 0.04, 0.01, 'outer'))
            m = circ(0.18, 16)
            put(m, 0, 0, 0.004)
            set_mesh_mat(m, make_mat(0.35, 0.18, 0.02, 'mid'))
            ce = circ(0.06, 10)
            put(ce, 0, 0, 0.007)
            set_mesh_mat(ce, make_mat(0.70, 0.60, 0.30, 'center'))

    do_render(name, 'decals', build)


# ── VFX BUILDERS ─────────────────────────────────────────
def build_vfx(name, vtype):
    def build():
        if vtype == 'hitflash':
            o = circ(0.4, 22)
            put(o, 0, 0, 0.003)
            set_mesh_mat(o, make_mat(1.0, 0.88, 0.72, 'outer'))
            i = circ(0.14, 12)
            put(i, 0, 0, 0.007)
            set_mesh_mat(i, make_mat(1.0, 1.0, 1.0, 'inner'))

        elif vtype == 'explosion':
            o = circ(0.4, 22)
            put(o, 0, 0, 0.001)
            set_mesh_mat(o, make_mat(1.0, 0.32, 0.02, 'outer'))
            m = circ(0.22, 18)
            put(m, 0, 0, 0.005)
            set_mesh_mat(m, make_mat(1.0, 0.62, 0.06, 'mid'))
            ce = circ(0.06, 10)
            put(ce, 0, 0, 0.009)
            set_mesh_mat(ce, make_mat(1.0, 1.0, 0.82, 'center'))

        elif vtype == 'smoke':
            for si, (sz, al) in enumerate([(0.32, 0.28), (0.22, 0.22), (0.10, 0.18)]):
                pu = circ(sz, 14)
                put(pu, random.uniform(-0.02, 0.02), random.uniform(-0.02, 0.02), 0.002 + si * 0.001)
                set_mesh_mat(pu, make_mat(al, al * 0.88, al * 0.85, f'puff_{si}'))

        elif vtype == 'sparks':
            for i in range(10):
                an = random.uniform(0, math.pi * 2)
                di = random.uniform(0.05, 0.38)
                sp = circ(random.uniform(0.006, 0.015), 4)
                put(sp, math.cos(an) * di, math.sin(an) * di, random.uniform(0.008, 0.02))
                col = random.choice([(1.0, 0.75, 0.08), (1.0, 0.45, 0.08), (1.0, 0.95, 0.85)])
                set_mesh_mat(sp, make_mat(col[0], col[1], col[2], f'spark_{i}'))
            # Center flash
            cs = circ(0.02, 5)
            put(cs, 0, 0, 0.015)
            set_mesh_mat(cs, make_mat(1.0, 1.0, 0.9, 'core'))

        elif vtype == 'shockwave':
            o = circ(0.38, 22)
            put(o, 0, 0, 0.003)
            set_mesh_mat(o, make_mat(0.6, 0.6, 1.0, 'outer'))
            i = circ(0.25, 18)
            put(i, 0, 0, 0.006)
            set_mesh_mat(i, make_mat(1.0, 0.82, 1.0, 'inner'))

    do_render(name, 'vfx', build)


# ── ENVIRONMENT BUILDERS ─────────────────────────────────
def build_environment():
    os.makedirs(os.path.join(OUTPUT_DIR, 'environment'), exist_ok=True)

    # Arena floor tile
    def arena_floor():
        fl = plane(2.6, 2.6)
        put(fl, 0, 0, -0.01)
        set_mesh_mat(fl, make_mat(0.18, 0.18, 0.20, 'base'))
        ts = 0.24; gap = 0.012
        for tx in range(-5, 6):
            for ty in range(-5, 6):
                t = plane(ts, ts)
                odd = (tx + ty) % 2 == 0
                c = (0.165, 0.165, 0.185) if odd else (0.15, 0.15, 0.17)
                put(t, tx * (ts + gap), ty * (ts + gap), 0.0)
                set_mesh_mat(t, make_mat(*c, 'tile'))
        # Grid lines
        for i in range(-5, 6):
            # Vertical grid line
            vl = plane(0.004, 2.52)
            put(vl, i * 0.24, 0, -0.02)
            set_mesh_mat(vl, make_mat(0.08, 0.08, 0.09, f'vline_{i}'))
            # Horizontal grid line
            hl = plane(2.52, 0.004)
            put(hl, 0, i * 0.24, -0.02)
            set_mesh_mat(hl, make_mat(0.08, 0.08, 0.09, f'hline_{i}'))
    do_render('arena_floor', 'environment', arena_floor)

    # Destructible crate
    def crate():
        c = rrect(0.68, 0.68, 0.06, 8)
        put(c, 0, 0, 0.003)
        set_mesh_mat(c, make_mat(0.50, 0.30, 0.15, 'body'))
        for i in range(3):
            pl = plane(0.58, 0.016)
            put(pl, 0, (i - 1) * 0.18, 0.009)
            set_mesh_mat(pl, make_mat(0.35, 0.20, 0.10, 'plank'))
        # Corner brackets
        for sx, sy in [(-1, -1), (1, -1), (-1, 1), (1, 1)]:
            br = rrect(0.05, 0.05, 0.01, 5)
            put(br, sx * 0.28, sy * 0.28, 0.008)
            set_mesh_mat(br, make_mat(0.40, 0.25, 0.12, 'bracket'))
    do_render('crate', 'environment', crate)

    # Destructible barrel
    def barrel():
        b = rrect(0.48, 0.68, 0.03, 8)
        put(b, 0, 0, 0.003)
        set_mesh_mat(b, make_mat(0.28, 0.28, 0.33, 'body'))
        for i in range(3):
            bn = plane(0.52, 0.018)
            put(bn, 0, (i - 1) * 0.19, 0.009)
            set_mesh_mat(bn, make_mat(0.42, 0.42, 0.47, 'band'))
        # Top hole
        hl = circ(0.025, 7)
        put(hl, 0, 0.28, 0.008)
        set_mesh_mat(hl, make_mat(0.03, 0.03, 0.03, 'hole'))
    do_render('barrel', 'environment', barrel)

    # Wall segment
    def wall():
        w = plane(0.68, 1.02)
        put(w, 0, 0, 0.003)
        set_mesh_mat(w, make_mat(0.52, 0.47, 0.42, 'base'))
        # Bricks
        for row in range(3):
            for col in range(2):
                br = plane(0.26, 0.26)
                ox = 0.06 if row % 2 == 0 else -0.06
                lx = -0.26 + col * 0.36 + ox
                ly = row * 0.3 - 0.3
                lt = (row + col) % 2 == 0
                c = (0.45, 0.40, 0.35) if lt else (0.50, 0.45, 0.40)
                put(br, lx, ly, 0.008)
                set_mesh_mat(br, make_mat(*c, 'brick'))
        # Cracks
        cr = plane(0.003, 0.22)
        put(cr, 0.06, -0.06, 0.012)
        cr.rotation_euler = (0, 0, 0.1)
        set_mesh_mat(cr, make_mat(0.14, 0.14, 0.14, 'crack'))
        cr2 = plane(0.003, 0.14)
        put(cr2, 0.10, 0.04, 0.012)
        cr2.rotation_euler = (0, 0, -0.15)
        set_mesh_mat(cr2, make_mat(0.14, 0.14, 0.14, 'crack2'))
    do_render('wall', 'environment', wall)


# ── UI BUILDERS ──────────────────────────────────────────
def build_ui():
    os.makedirs(os.path.join(OUTPUT_DIR, 'ui'), exist_ok=True)

    def healthbar_bg():
        bg = rrect(1.7, 0.15, 0.022, 6)
        put(bg, 0, 0, 0.002)
        set_mesh_mat(bg, make_mat(0.11, 0.11, 0.11, 'bg'))
    do_render('healthbar_bg', 'ui', healthbar_bg)

    def healthbar_fill():
        f = rrect(1.6, 0.09, 0.012, 6)
        put(f, 0, 0, 0.006)
        set_mesh_mat(f, make_mat(0.72, 0.06, 0.06, 'fill'))
    do_render('healthbar_fill', 'ui', healthbar_fill)

    def powerup_icon():
        ic = rrect(0.40, 0.40, 0.045, 7)
        put(ic, 0, 0, 0.002)
        set_mesh_mat(ic, make_mat(0.06, 0.06, 0.06, 'bg'))
        rn = circ(0.34, 16)
        put(rn, 0, 0, 0.005)
        set_mesh_mat(rn, make_mat(0.70, 0.60, 0.12, 'ring'))
    do_render('powerup_icon_bg', 'ui', powerup_icon)

    def wave_banner():
        bg = rrect(3.4, 0.8, 0.1, 5)
        put(bg, 0, 0, 0.001)
        set_mesh_mat(bg, make_mat(0.03, 0.03, 0.05, 'bg'))
        bd = rrect(3.48, 0.86, 0.1, 5)
        put(bd, 0, 0, -0.002)
        set_mesh_mat(bd, make_mat(0.70, 0.60, 0.12, 'border'))
    do_render('wave_banner', 'ui', wave_banner)

    def mainmenu_bg():
        bg = plane(5.2, 5.2)
        put(bg, 0, 0, -0.01)
        set_mesh_mat(bg, make_mat(0.05, 0.05, 0.07, 'bg'))
        # Concentric rings
        for i, rad in enumerate([4.8, 3.8, 2.8, 1.8]):
            rn = circ(rad, 32)
            put(rn, 0, 0, -0.003)
            inten = (4 - i) * 0.025
            set_mesh_mat(rn, make_mat(0.07 + i * 0.008, 0.05 + i * 0.006, 0.09 + i * 0.008, f'ring_{i}'))
    do_render('mainmenu_bg', 'ui', mainmenu_bg)


# ── MAIN ─────────────────────────────────────────────────
def main():
    global OUTPUT_DIR
    args = sys.argv
    if '--output-dir' in args:
        idx = args.index('--output-dir')
        OUTPUT_DIR = args[idx + 1]
    else:
        OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'generated_v4')

    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"Hero's Arena — Blender Asset Generator v4")
    print(f"Output: {OUTPUT_DIR}/")
    print(f"Res: {RES}×{RES}  BG: pure black void  Key threshold: {KEY_THRESHOLD}")
    print()

    total = 0

    print("=" * 52); print("HEROES (4)"); print("=" * 52)
    for n in ['atlas', 'zephyr', 'synapse', 'volt']:
        build_hero(n)

    print(); print("=" * 52); print("ENEMIES (10)"); print("=" * 52)
    for n in ['drone', 'brute', 'sprinter', 'artillery', 'shielder',
              'healer', 'exploder', 'burrower', 'parasite', 'apex']:
        build_enemy(n)

    print(); print("=" * 52); print("PROJECTILES (11)"); print("=" * 52)
    for nm, pl, ab in PROJ_SPECS:
        build_projectile(nm, pl, ab)

    print(); print("=" * 52); print("DECALS (5)"); print("=" * 52)
    for d in ['blood', 'scorch', 'crater', 'acid', 'explosion']:
        build_decal(d, d)

    print(); print("=" * 52); print("VFX (5)"); print("=" * 52)
    for nm, vt in [('hitflash', 'hitflash'), ('explosion', 'explosion'),
                   ('smoke', 'smoke'), ('sparks', 'sparks'), ('shockwave', 'shockwave')]:
        build_vfx(nm, vt)

    print(); print("=" * 52); print("ENVIRONMENT (4)"); print("=" * 52)
    build_environment()

    print(); print("=" * 52); print("UI (5)"); print("=" * 52)
    build_ui()

    # Final count
    cnt = 0
    for root, _, files in os.walk(OUTPUT_DIR):
        for f in files:
            if f.endswith('.png') and not f.startswith('_raw_'):
                cnt += 1
    print(); print(f"Done! {cnt} sprites → {OUTPUT_DIR}/")


if __name__ == '__main__':
    main()
