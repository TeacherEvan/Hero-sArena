#!/usr/bin/env python3
"""
Hero's Arena — Blender Asset Generation (Phase 1) — FINAL
==========================================================
Proven pipeline:
  1. Black background, NO lights, no AA → clean raw render
  2. Adaptive chroma-key: detect bg color, key everything similar to it

Every sprite gets a flat unlit look (consistent with Godot 2D games).
Run:  blender --background --python generate_final.py -- --output-dir ./assets
"""

import bpy, os, sys, math, struct, zlib, random

# ── Config ───────────────────────────────────────────────
BG_COLOR = (0.0, 0.0, 0.0)   # pure black — no AA halos with no lights
RES = 512
OUTPUT_DIR = None
_KEY_THRESHOLD = 25.0  # RGB distance in 0-255 space

# ── Color palettes (0-1 space, will be *255 for materials) ──
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

# ── Chroma-key (adaptive, proven) ────────────────────────
def chromakey(inpath, outpath):
    with open(inpath, 'rb') as f:
        f.read(8)
        chunks = []
        w = h = 0
        while True:
            ln = struct.unpack('>I', f.read(4))[0]
            ct = f.read(4)
            d = f.read(ln)
            f.read(4)
            chunks.append((ct, d))
            if ct == b'IHDR':
                w = struct.unpack('>I', d[0:4])[0]
                h = struct.unpack('>I', d[4:8])[0]
            elif ct == b'IEND':
                break

    idat = b''.join(d for c, d in chunks if c == b'IDAT')
    raw = zlib.decompress(idat)
    bpp = 4
    rb = 1 + w * bpp

    # Decode
    rows = []
    for y in range(h):
        off = y * rb
        ft = raw[off]
        cur = bytearray(raw[off+1:off+rb])
        if ft == 1:
            for i in range(bpp, len(cur)):
                cur[i] = (cur[i] + cur[i-bpp]) & 0xFF
        rows.append(bytes(cur))

    # Sample bg color (most common non-zero-alpha color)
    cc = {}
    for y in range(0, h, 4):
        cur = rows[y]
        for x in range(0, w, 4):
            idx = x * bpp
            r,g,b,a = cur[idx], cur[idx+1], cur[idx+2], cur[idx+3]
            if a > 10:
                k = (r,g,b)
                cc[k] = cc.get(k, 0) + 1

    if not cc:
        import shutil; shutil.copy(inpath, outpath)
        return 0, w*h, 0.0

    br, bg_c, bb = max(cc, key=cc.get)
    print(f"    bg detected: RGB({br},{bg_c},{bb})")

    new_rows = []
    op = tr = 0
    th = _KEY_THRESHOLD
    for y in range(h):
        cur = rows[y]
        nr = bytearray([0])
        for x in range(w):
            idx = x * bpp
            r,g,b = cur[idx], cur[idx+1], cur[idx+2]
            if math.sqrt((r-br)**2 + (g-bg_c)**2 + (b-bb)**2) <= th:
                nr.extend([r,g,b,0]); tr += 1
            else:
                nr.extend([r,g,b,255]); op += 1
        new_rows.append(bytes(nr))

    new_raw = b''.join(nr for nr in new_rows)
    comp = zlib.compress(new_raw)

    with open(outpath, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n')
        import binascii
        def wc(ctype, data):
            f.write(struct.pack('>I', len(data)))
            f.write(ctype); f.write(data)
            f.write(struct.pack('>I', binascii.crc32(ctype+data) & 0xFFFFFFFF))
        wc(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
        wc(b'IDAT', comp)
        wc(b'IEND', b'')

    return op, tr, 100.0*op/(op+tr)


# ── Scene helpers ─────────────────────────────────────────
def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)
    for w in list(bpy.data.worlds): bpy.data.worlds.remove(w)

def setup_scene():
    clear()
    # Camera
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'; cd.ortho_scale = 3.0
    co = bpy.data.objects.new('cam', cd)
    co.location = (0,0,5); co.rotation_euler = (math.pi/2, 0, 0)
    bpy.context.collection.objects.link(co)
    bpy.context.scene.camera = co
    # No lights — flat unlit look
    # World = black
    world = bpy.data.worlds.new('bg')
    bpy.context.scene.world = world
    world.use_nodes = True
    wn = world.node_tree.nodes
    for n in wn: wn.remove(n)
    bn = wn.new(type='ShaderNodeBackground')
    bn.inputs['Color'].default_value = (*BG_COLOR, 1.0)
    bn.inputs['Strength'].default_value = 1.0
    on = wn.new(type='ShaderNodeOutputWorld')
    bn.location = (-200,0); on.location = (200,0)
    world.node_tree.links.new(bn.outputs['Background'], on.inputs['Surface'])
    # Render settings
    s = bpy.context.scene
    s.render.resolution_x = RES; s.render.resolution_y = RES
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    s.render.film_transparent = False

def mat(r,g,b,name="m"):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (r,g,b,1.0)
    return m

def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

def circ(r, v=32):
    bpy.ops.mesh.primitive_circle_add(vertices=v, radius=r, fill_type='TRIFAN')
    o = bpy.context.active_object; o.location.z = 0; return o

def rrect(w,h,rad,v=8):
    bpy.ops.mesh.primitive_cylinder_add(vertices=v*4,radius=1.0,depth=0.02)
    o = bpy.context.active_object; o.scale = (w/2,h/2,1.0); return o

def plane(w,h):
    bpy.ops.mesh.primitive_plane_add(size=1)
    o = bpy.context.active_object; o.scale = (w/2,h/2,1.0); return o

def put(o,x,y,z=0):
    o.location = (x,y,z)


def key_render(name, subdir, fn):
    """Call fn() to build the scene, render, chroma-key, clean up."""
    os.makedirs(os.path.join(OUTPUT_DIR, subdir), exist_ok=True)
    raw = os.path.join(OUTPUT_DIR, subdir, f"_raw_{name}.png")
    final = os.path.join(OUTPUT_DIR, subdir, f"{name}.png")
    fn()
    render(raw)
    op, tr, pct = chromakey(raw, final)
    os.remove(raw)
    print(f"  ✓ {subdir}/{name}.png  ({pct:.0f}% opaque)")


# ── MESH BUILDERS ─────────────────────────────────────────

def mk_body(p, w, h, shape='round'):
    if shape == 'round':
        o = circ(min(w,h)*0.9, 28)
    elif shape == 'rect':
        o = rrect(w, h, min(w,h)*0.12)
    elif shape == 'ellipse':
        o = rrect(w*0.9, h*1.2, min(w,h)*0.1)
    elif shape == 'diamond':
        o = circ(min(w,h)*0.85, 20)
        o.rotation_euler = (0,0,0)
    put(o, 0, 0, 0.002)
    mat(p['body'][0],p['body'][1],p['body'][2],p.get('name','body')).name
    o.data.materials.clear()
    o.data.materials.append(mat(p['body'][0],p['body'][1],p['body'][2],'body'))
    return o

def mk_outline(p, w, h):
    o = rrect(w*1.06, h*1.06, min(w,h)*0.08)
    put(o, 0, 0, -0.005)
    o.data.materials.clear()
    o.data.materials.append(mat(p['out'][0],p['out'][1],p['out'][2],'outline'))
    return o

def mk_accent(p, w, h):
    o = rrect(w*0.6, h*0.1, min(w,h)*0.02)
    put(o, 0, 0, 0.008)
    o.data.materials.clear()
    o.data.materials.append(mat(p['acc'][0],p['acc'][1],p['acc'][2],'accent'))
    return o

def mk_eye(p, x, y, r):
    o = circ(r, 14)
    put(o, x, y, 0.01)
    o.data.materials.clear()
    o.data.materials.append(mat(p['eye'][0],p['eye'][1],p['eye'][2],'eye'))
    return o


# ── HERO BUILDERS ─────────────────────────────────────────

def hero_atlas():
    p = P['atlas']; w = h = SIZE['atlas']

    def build():
        mk_outline(p, w, h)
        mk_body(p, w, h)
        mk_accent(p, w, h)
        es = w * 0.07
        mk_eye(p, -w*0.22, 0, es)
        mk_eye(p,  w*0.22, 0, es)
        e = circ(w*0.25, 8)
        put(e, 0, 0, 0.015)
        e.data.materials.clear()
        e.data.materials.append(mat(p['met'][0], p['met'][1], p['met'][2], 'emblem'))
        e.scale.x = 1.0; e.scale.y = 0.35; e.scale.z = 1.0
    key_render('atlas', 'heroes', build)

# Simpler approach for all heroes — inline builders
def build_hero(name):
    p = P[name]
    w = h = SIZE[name]

    def build():
        mk_outline(p, w, h)
        mk_body(p, w, h)
        mk_accent(p, w, h)
        # Eyes
        es = w * 0.07
        mk_eye(p, -w*0.22, 0, es)
        mk_eye(p,  w*0.22, 0, es)
        # Class detail
        if name == 'atlas':
            e = circ(w*0.25, 8)
            put(e, 0, 0, 0.015)
            e.data.materials.clear()
            e.data.materials.append(mat(p['met'][0],p['met'][1],p['met'][2],'emblem'))
            e.scale = (1.0, 0.35, 1.0)
        elif name == 'zephyr':
            for sx in (-1,1):
                wl = rrect(w*0.35, h*0.1, w*0.03)
                put(wl, sx*w*0.3, 0, 0.01)
                wl.data.materials.clear()
                wl.data.materials.append(mat(p['acc'][0],p['acc'][1],p['acc'][2],'wing'))
        elif name == 'synapse':
            hl = circ(w*0.55, 20)
            put(hl, 0, 0, 0.008)
            hl.data.materials.clear()
            hl.data.materials.append(mat(p['met'][0],p['met'][1],p['met'][2],'halo'))
            hl.scale = (1.1,1.1,1.0)
        elif name == 'volt':
            for ang in range(0,360,60):
                rad = math.radians(ang)
                sp = circ(w*0.06, 5)
                put(sp, math.cos(rad)*w*0.52, math.sin(rad)*w*0.52, 0.015)
                sp.data.materials.clear()
                sp.data.materials.append(mat(1.0,0.9,0.2,f'spike_{ang}'))
    key_render(name, 'heroes', build)


# ── ENEMY BUILDERS ────────────────────────────────────────

def build_enemy(name):
    p = P[name]
    w = SIZE[name] * ASPECT[name][0]
    h = SIZE[name] * ASPECT[name][1]

    def build():
        mk_outline(p, w, h)

        # Body shape by type
        if name in ('drone','parasite','shielder','exploder','apex'):
            o = circ(min(w,h)*0.88, 24)
        elif name in ('brute','healer'):
            o = rrect(w, h, min(w,h)*0.15)
        elif name == 'sprinter':
            o = rrect(w*1.12, h*0.68, min(w,h)*0.07)
        elif name == 'artillery':
            o = rrect(w*0.78, h, min(w,h)*0.1)
        elif name == 'burrower':
            o = rrect(w*0.85, h*1.22, min(w,h)*0.1)
        else:
            o = circ(min(w,h)*0.88, 24)
        put(o, 0, 0, 0.005)
        o.data.materials.clear()
        o.data.materials.append(mat(p['body'][0],p['body'][1],p['body'][2],'body'))

        mk_accent(p, w, h)

        # Eyes
        es = min(w,h)*0.07
        mk_eye(p, -es*1.8, 0, es)
        mk_eye(p,  es*1.8, 0, es)

        # Enemy-specific details
        if name == 'drone':
            for ang in range(0,360,90):
                ra = math.radians(ang)
                rt = circ(min(w,h)*0.26, 14)
                put(rt, math.cos(ra)*w*0.38, math.sin(ra)*h*0.38, 0.015)
                rt.data.materials.clear()
                rt.data.materials.append(mat(p['met'][0],p['met'][1],p['met'][2],'rotor'))
        elif name == 'brute':
            for sx,sy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
                pt = rrect(w*0.2, h*0.15, w*0.03)
                put(pt, sx*w*0.28, sy*h*0.22, 0.015)
                pt.data.materials.clear()
                pt.data.materials.append(mat(p['met'][0],p['met'][1],p['met'][2],'plate'))
        elif name == 'sprinter':
            for i in range(3):
                st = plane(w*0.12, h*0.025)
                put(st, 0, (i-1)*h*0.12, 0.012)
                st.data.materials.clear()
                st.data.materials.append(mat(p['acc'][0],p['acc'][1],p['acc'][2],'stripe'))
        elif name == 'artillery':
            bt = rrect(w*0.12, h*0.32, w*0.035)
            put(bt, 0, h*0.28, 0.015)
            bt.data.materials.clear()
            bt.data.materials.append(mat(p['met'][0],p['met'][1],p['met'][2],'barrel'))
            bs = rrect(w*0.5, h*0.1, w*0.035)
            put(bs, 0, -h*0.16, 0.015)
            bs.data.materials.clear()
            bs.data.materials.append(mat(p['acc'][0],p['acc'][1],p['acc'][2],'base'))
        elif name == 'shielder':
            sh = circ(min(w,h)*0.62, 20)
            put(sh, 0, 0, 0.002)
            sh.data.materials.clear()
            sh.data.materials.append(mat(0.25,0.25,0.65,'shield'))
            sh.scale = (1.3,1.3,1.0)
        elif name == 'healer':
            hl = circ(min(w,h)*0.58, 18)
            put(hl, 0, 0, 0.004)
            hl.data.materials.clear()
            hl.data.materials.append(mat(p['met'][0],p['met'][1],p['met'][2],'halo'))
            hl.scale = (1.2,1.2,1.0)
        elif name == 'exploder':
            for i in range(8):
                ang = (i/8)*math.pi*2
                sp = circ(min(w,h)*0.065, 5)
                put(sp, math.cos(ang)*w*0.5, math.sin(ang)*h*0.5, 0.015)
                sp.data.materials.clear()
                sp.data.materials.append(mat(p['acc'][0],p['acc'][1],p['acc'][2],'spike'))
        elif name == 'burrower':
            for i in range(3):
                sg = rrect(w*0.42, h*0.13, w*0.035)
                put(sg, 0, (i-1)*h*0.2, 0.013)
                sg.data.materials.clear()
                sg.data.materials.append(mat(p['acc'][0],p['acc'][1],p['acc'][2],'seg'))
        elif name == 'parasite':
            for ang in range(0,360,60):
                ra = math.radians(ang)
                tn = circ(min(w,h)*0.03, 5)
                put(tn, math.cos(ra)*w*0.48, math.sin(ra)*h*0.48, 0.008)
                tn.data.materials.clear()
                tn.data.materials.append(mat(p['acc'][0],p['acc'][1],p['acc'][2],'tent'))
        elif name == 'apex':
            for ad in (45,135,225,315):
                ra = math.radians(ad)
                hn = rrect(w*0.08, h*0.2, w*0.02)
                put(hn, math.cos(ra)*w*0.45, math.sin(ra)*h*0.45, 0.018)
                hn.data.materials.clear()
                hn.data.materials.append(mat(p['met'][0],p['met'][1],p['met'][2],'horn'))
            for sx,sy in [(-1,0),(1,0),(0,-1),(0,1)]:
                pt = rrect(w*0.16, h*0.16, w*0.015)
                put(pt, sx*w*0.3, sy*h*0.3, 0.018)
                pt.data.materials.clear()
                pt.data.materials.append(mat(p['met'][0],p['met'][1],p['met'][2],'armplate'))
    key_render(name, 'enemies', build)


# ── PROJECTILE BUILDERS ────────────────────────────────────

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

def build_projectile(name, palette, ability):
    p = P[palette]

    def build():
        if ability:
            gl = circ(0.32, 24)
            put(gl, 0, 0, 0.005)
            gl.data.materials.clear()
            gl.data.materials.append(mat(p['acc'][0],p['acc'][1],p['acc'][2],'glow'))
            bd = circ(0.2, 20)
            put(bd, 0, 0, 0.01)
            bd.data.materials.clear()
            bd.data.materials.append(mat(p['body'][0],p['body'][1],p['body'][2],'body'))
            co = circ(0.07, 12)
            put(co, 0, 0, 0.015)
            co.data.materials.clear()
            co.data.materials.append(mat(1,1,1,'core'))
        else:
            bd = circ(0.14, 16)
            put(bd, 0, 0, 0.008)
            bd.data.materials.clear()
            bd.data.materials.append(mat(p['body'][0],p['body'][1],p['body'][2],'body'))
            tr = plane(0.08, 0.025)
            put(tr, -0.08, 0, 0.004)
            tr.data.materials.clear()
            tr.data.materials.append(mat(p['body'][0]*0.4, p['body'][1]*0.4, p['body'][2]*0.4,'trail'))
    key_render(name, 'projectiles', build)


# ── DECAL BUILDERS ─────────────────────────────────────────

def build_decal(name, dtype):
    def build():
        if dtype == 'blood':
            for _ in range(5):
                a = random.uniform(0, math.pi*2)
                d = random.uniform(0.08, 0.3)
                bl = circ(random.uniform(0.03,0.08), 8)
                put(bl, math.cos(a)*d, math.sin(a)*d, 0.003)
                bl.data.materials.clear()
                bl.data.materials.append(mat(0.55,0.03,0.03,'splat'))
            pl = circ(0.15, 16)
            put(pl, 0, 0, 0.005)
            pl.data.materials.clear()
            pl.data.materials.append(mat(0.45,0.02,0.02,'pool'))
        elif dtype == 'scorch':
            o = circ(0.3, 20)
            put(o, 0, 0, 0.002)
            o.data.materials.clear()
            o.data.materials.append(mat(0.06,0.06,0.04,'outer'))
            i = circ(0.16, 16)
            put(i, 0, 0, 0.006)
            i.data.materials.clear()
            i.data.materials.append(mat(0.10,0.08,0.05,'inner'))
            for _ in range(4):
                m = circ(random.uniform(0.02,0.045), 6)
                put(m, random.uniform(-0.15,0.15), random.uniform(-0.15,0.15), 0.008)
                m.data.materials.clear()
                m.data.materials.append(mat(0.03,0.03,0.03,'char'))
        elif dtype == 'crater':
            rm = circ(0.35, 24)
            put(rm, 0, 0, 0.001)
            rm.data.materials.clear()
            rm.data.materials.append(mat(0.26,0.20,0.14,'rim'))
            fl = circ(0.25, 20)
            put(fl, 0, 0, 0.004)
            fl.data.materials.clear()
            fl.data.materials.append(mat(0.10,0.08,0.06,'floor'))
            for _ in range(5):
                rk = circ(random.uniform(0.015,0.04), 6)
                an = random.uniform(0,math.pi*2)
                di = random.uniform(0.3,0.4)
                put(rk, math.cos(an)*di, math.sin(an)*di, random.uniform(0.004,0.008))
                rk.data.materials.clear()
                rk.data.materials.append(mat(0.30,0.25,0.20,'debris'))
        elif dtype == 'acid':
            pl = circ(0.3, 20)
            put(pl, 0, 0, 0.002)
            pl.data.materials.clear()
            pl.data.materials.append(mat(0.06,0.40,0.06,'pool'))
            for i in range(3):
                rp = circ(0.15+i*0.04, 16)
                put(rp, 0, 0, 0.005)
                rp.data.materials.clear()
                rp.data.materials.append(mat(0.08,0.48,0.08,f'ripple_{i}'))
        elif dtype == 'explosion':
            o = circ(0.35, 24)
            put(o, 0, 0, 0.002)
            o.data.materials.clear()
            o.data.materials.append(mat(0.10,0.06,0.01,'outer'))
            m = circ(0.22, 20)
            put(m, 0, 0, 0.006)
            m.data.materials.clear()
            m.data.materials.append(mat(0.40,0.22,0.03,'mid'))
            ce = circ(0.08, 12)
            put(ce, 0, 0, 0.01)
            ce.data.materials.clear()
            ce.data.materials.append(mat(0.80,0.70,0.35,'center'))
    key_render(name, 'decals', build)


# ── VFX BUILDERS ───────────────────────────────────────────

def build_vfx(name, vtype):
    def build():
        if vtype == 'hitflash':
            o = circ(0.42, 24)
            put(o, 0, 0, 0.004)
            o.data.materials.clear()
            o.data.materials.append(mat(1.0,0.9,0.75,'outer'))
            i = circ(0.16, 14)
            put(i, 0, 0, 0.008)
            i.data.materials.clear()
            i.data.materials.append(mat(1,1,1,'inner'))
        elif vtype == 'explosion':
            o = circ(0.42, 24)
            put(o, 0, 0, 0.002)
            o.data.materials.clear()
            o.data.materials.append(mat(1.0,0.35,0.03,'outer'))
            m = circ(0.25, 20)
            put(m, 0, 0, 0.006)
            m.data.materials.clear()
            m.data.materials.append(mat(1.0,0.65,0.08,'mid'))
            ce = circ(0.08, 12)
            put(ce, 0, 0, 0.01)
            ce.data.materials.clear()
            ce.data.materials.append(mat(1,1,0.85,'center'))
        elif vtype == 'smoke':
            for si, (sz, al) in enumerate([(0.35,0.30),(0.25,0.25),(0.12,0.20)]):
                pu = circ(sz, 16)
                put(pu, random.uniform(-0.03,0.03), random.uniform(-0.03,0.03), 0.003+si*0.002)
                pu.data.materials.clear()
                pu.data.materials.append(mat(al,al*0.9,al*0.9,f'puff_{si}'))
        elif vtype == 'sparks':
            for i in range(10):
                an = random.uniform(0,math.pi*2)
                di = random.uniform(0.06,0.4)
                sp = circ(random.uniform(0.008,0.02), 5)
                put(sp, math.cos(an)*di, math.sin(an)*di, random.uniform(0.01,0.025))
                sp.data.materials.clear()
                col = random.choice([(1,0.8,0.1),(1,0.5,0.1),(1,1,0.9)])
                sp.data.materials.append(mat(col[0],col[1],col[2],f'spark_{i}'))
            cs = circ(0.025, 6)
            put(cs, 0, 0, 0.02)
            cs.data.materials.clear()
            cs.data.materials.append(mat(1,1,0.9,'core'))
        elif vtype == 'shockwave':
            o = circ(0.4, 24)
            put(o, 0, 0, 0.004)
            o.data.materials.clear()
            o.data.materials.append(mat(0.65,0.65,1.0,'outer'))
            i = circ(0.28, 20)
            put(i, 0, 0, 0.007)
            i.data.materials.clear()
            i.data.materials.append(mat(1.0,0.85,1.0,'inner'))
    key_render(name, 'vfx', build)


# ── ENVIRONMENT BUILDERS ───────────────────────────────────

def build_environment():
    os.makedirs(os.path.join(OUTPUT_DIR,'environment'), exist_ok=True)

    # Arena floor
    def floor():
        fl = plane(2.8, 2.8)
        put(fl, 0, 0, -0.01)
        fl.data.materials.clear()
        fl.data.materials.append(mat(0.20,0.20,0.22,'base'))
        ts = 0.26; gap = 0.015
        for tx in range(-5,6):
            for ty in range(-5,6):
                t = plane(ts, ts)
                odd = (tx+ty)%2 == 0
                c = (0.18,0.18,0.20) if odd else (0.16,0.16,0.18)
                put(t, tx*(ts+gap), ty*(ts+gap), 0.0)
                t.data.materials.clear()
                t.data.materials.append(mat(*c,'tile'))
        for i in range(-5,6):
            hl = plane(2.8, 0.006)
            put(hl, 0, i*0.26, -0.02)
            hl.data.materials.clear()
            hl.data.materials.append(mat(0.10,0.10,0.11,'hline'))
            vl = plane(0.006, 2.8)
            put(vl, i*0.26, 0, -0.02)
            vl.data.materials.clear()
            vl.data.materials.append(mat(0.10,0.10,0.11,'vline'))
    key_render('arena_floor','environment', floor)

    # Crate
    def crate():
        c = rrect(0.72, 0.72, 0.07, 8)
        put(c, 0, 0, 0.004)
        c.data.materials.clear()
        c.data.materials.append(mat(0.50,0.30,0.15,'body'))
        for i in range(3):
            pl = plane(0.62, 0.018)
            put(pl, 0, (i-1)*0.2, 0.01)
            pl.data.materials.clear()
            pl.data.materials.append(mat(0.35,0.20,0.10,'plank'))
        for sx,sy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
            br = rrect(0.06,0.06,0.012)
            put(br, sx*0.3, sy*0.3, 0.01)
            br.data.materials.clear()
            br.data.materials.append(mat(0.40,0.25,0.12,'bracket'))
    key_render('crate','environment', crate)

    # Barrel
    def barrel():
        b = rrect(0.52, 0.72, 0.035, 8)
        put(b, 0, 0, 0.004)
        b.data.materials.clear()
        b.data.materials.append(mat(0.30,0.30,0.35,'body'))
        for i in range(3):
            bn = plane(0.56, 0.022)
            put(bn, 0, (i-1)*0.2, 0.01)
            bn.data.materials.clear()
            bn.data.materials.append(mat(0.45,0.45,0.50,'band'))
        hl = circ(0.03, 8)
        put(hl, 0, 0.3, 0.01)
        hl.data.materials.clear()
        hl.data.materials.append(mat(0.04,0.04,0.04,'hole'))
    key_render('barrel','environment', barrel)

    # Wall
    def wall():
        w = plane(0.72, 1.08)
        put(w, 0, 0, 0.004)
        w.data.materials.clear()
        w.data.materials.append(mat(0.55,0.50,0.45,'base'))
        for row in range(3):
            for col in range(2):
                br = plane(0.28, 0.28)
                ox = 0.07 if row%2==0 else -0.07
                lx = -0.26 + col*0.36 + ox
                ly = row*0.32 - 0.32
                lt = (row+col)%2==0
                c = (0.48,0.43,0.38) if lt else (0.52,0.47,0.42)
                put(br, lx, ly, 0.01)
                br.data.materials.clear()
                br.data.materials.append(mat(*c,'brick'))
        cr = plane(0.004, 0.25)
        put(cr, 0.07, -0.05, 0.015)
        cr.rotation_euler = (0,0,0.12)
        cr.data.materials.clear()
        cr.data.materials.append(mat(0.16,0.16,0.16,'crack'))
        cr2 = plane(0.004, 0.16)
        put(cr2, 0.12, 0.04, 0.015)
        cr2.rotation_euler = (0,0,-0.18)
        cr2.data.materials.clear()
        cr2.data.materials.append(mat(0.16,0.16,0.16,'crack2'))
    key_render('wall','environment', wall)


# ── UI BUILDERS ────────────────────────────────────────────

def build_ui():
    os.makedirs(os.path.join(OUTPUT_DIR,'ui'), exist_ok=True)

    def hb_bg():
        bg = rrect(1.8, 0.16, 0.025)
        put(bg, 0, 0, 0.003)
        bg.data.materials.clear()
        bg.data.materials.append(mat(0.12,0.12,0.12,'bg'))
    key_render('healthbar_bg','ui', hb_bg)

    def hb_fill():
        f = rrect(1.7, 0.11, 0.015)
        put(f, 0, 0, 0.007)
        f.data.materials.clear()
        f.data.materials.append(mat(0.75,0.08,0.08,'fill'))
    key_render('healthbar_fill','ui', hb_fill)

    def pu_icon():
        ic = rrect(0.42, 0.42, 0.05)
        put(ic, 0, 0, 0.003)
        ic.data.materials.clear()
        ic.data.materials.append(mat(0.08,0.08,0.08,'bg'))
        rn = circ(0.36, 18)
        put(rn, 0, 0, 0.007)
        rn.data.materials.clear()
        rn.data.materials.append(mat(0.75,0.65,0.15,'ring'))
    key_render('powerup_icon_bg','ui', pu_icon)

    def wave_banner():
        bg = rrect(3.6, 0.85, 0.12)
        put(bg, 0, 0, 0.002)
        bg.data.materials.clear()
        bg.data.materials.append(mat(0.04,0.04,0.06,'bg'))
        bd = rrect(3.65, 0.9, 0.12)
        put(bd, 0, 0, 0.0)
        bd.data.materials.clear()
        bd.data.materials.append(mat(0.75,0.65,0.15,'border'))
    key_render('wave_banner','ui', wave_banner)

    def mainmenu():
        bg = plane(5.4, 5.4)
        put(bg, 0, 0, -0.01)
        bg.data.materials.clear()
        bg.data.materials.append(mat(0.06,0.06,0.08,'bg'))
        for i, rad in enumerate([5.0,4.0,3.0,2.0]):
            rn = circ(rad, 36)
            put(rn, 0, 0, -0.004)
            rn.data.materials.clear()
            inten = 0.03*(4-i)
            rn.data.materials.append(mat(0.08+i*0.01,0.06+i*0.008,0.10+i*0.01,inten and f'r{i}'))
    key_render('mainmenu_bg','ui', mainmenu)


# ── MAIN ───────────────────────────────────────────────────

def main():
    global OUTPUT_DIR
    args = sys.argv
    if '--output-dir' in args:
        OUTPUT_DIR = args[args.index('--output-dir')+1]
    else:
        OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'generated_final')

    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"Output: {OUTPUT_DIR}/")
    print(f"Res: {RES}x{RES}  BG: RGB({BG_COLOR[0]*255:.0f},{BG_COLOR[1]*255:.0f},{BG_COLOR[2]*255:.0f})  key_threshold: {_KEY_THRESHOLD}")
    print()

    total = 0

    print("="*50); print("HEROES"); print("="*50)
    for n in ['atlas','zephyr','synapse','volt']:
        build_hero(n)

    print(); print("="*50); print("ENEMIES"); print("="*50)
    for n in ['drone','brute','sprinter','artillery','shielder','healer',
              'exploder','burrower','parasite','apex']:
        build_enemy(n)

    print(); print("="*50); print("PROJECTILES"); print("="*50)
    for nm,pl,ab in PROJ_SPECS:
        build_projectile(nm,pl,ab)

    print(); print("="*50); print("DECALS"); print("="*50)
    for d in ['blood','scorch','crater','acid','explosion']:
        build_decal(d,d)

    print(); print("="*50); print("VFX"); print("="*50)
    for nm,vt in [('hitflash','hitflash'),('explosion','explosion'),
                  ('smoke','smoke'),('sparks','sparks'),('shockwave','shockwave')]:
        build_vfx(nm,vt)

    print(); print("="*50); print("ENVIRONMENT"); print("="*50)
    build_environment()

    print(); print("="*50); print("UI"); print("="*50)
    build_ui()

    # Count
    cnt = 0
    for root,_,files in os.walk(OUTPUT_DIR):
        for f in files:
            if f.endswith('.png') and not f.startswith('_raw_'):
                cnt += 1
    print(); print(f"Done! {cnt} sprites → {OUTPUT_DIR}/")


if __name__ == '__main__':
    main()
