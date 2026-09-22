#!/usr/bin/env python3
"""
Hero's Arena — PIL Sprite Generator (Phase 1)
==============================================
Generates top-down 2D sprite assets using Pillow.
No Blender needed — deterministic, fast, pixel-perfect alpha.

Run:
  python3 generate_pil.py [--output-dir ./assets]
"""

import os, sys, math, random
from PIL import Image, ImageDraw

# ── Config ───────────────────────────────────────────────
RES = 512
OUTPUT_DIR = None
random.seed(42)  # deterministic output

# ── Color palettes (RGB tuples) ──────────────────────────
P = {
    "atlas":  dict(body=(217,89,46),    acc=(153,38,20),   met=(178,153,127),
                   eye=(255,255,255), out=(51,20,10)),
    "zephyr": dict(body=(46,140,209),   acc=(20,97,166),   met=(191,204,230),
                   eye=(255,255,255), out=(10,46,82)),
    "synapse":dict(body=(173,56,173),   acc=(122,20,122),  met=(127,183,127),
                   eye=(64,255,64),   out=(71,20,71)),
    "volt":   dict(body=(230,191,20),   acc=(173,133,0),   met=(242,230,178),
                   eye=(255,255,46),  out=(97,71,0)),
    "drone":    dict(body=(127,127,140), acc=(89,89,102),  met=(153,153,165),
                    eye=(255,46,46),   out=(30,30,46)),
    "brute":    dict(body=(140,33,20),   acc=(89,18,10),   met=(114,89,76),
                    eye=(255,199,46),  out=(30,10,5)),
    "sprinter": dict(body=(217,71,30),   acc=(148,30,10),  met=(178,153,127),
                    eye=(255,255,64),  out=(46,15,5)),
    "artillery":dict(body=(71,71,84),    acc=(46,46,58),   met=(127,127,140),
                    eye=(255,114,20),  out=(20,20,26)),
    "shielder": dict(body=(56,56,148),   acc=(30,30,97),   met=(140,153,204),
                    eye=(255,224,71),  out=(15,15,56)),
    "healer":   dict(body=(46,148,56),   acc=(20,97,30),   met=(127,191,140),
                    eye=(255,255,114), out=(15,46,20)),
    "exploder": dict(body=(217,30,20),   acc=(148,10,5),   met=(127,76,64),
                    eye=(255,89,20),   out=(46,5,2)),
    "burrower": dict(body=(114,84,30),   acc=(58,40,15),   met=(140,114,76),
                    eye=(191,140,20),  out=(25,15,5)),
    "parasite": dict(body=(97,30,97),    acc=(58,15,58),   met=(127,76,127),
                    eye=(255,20,191),  out=(25,7,25)),
    "apex":     dict(body=(30,20,30),    acc=(15,7,15),    met=(148,30,30),
                    eye=(255,20,20),   out=(10,5,7)),
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


# ── Drawing helpers ──────────────────────────────────────
def draw_ellipse(d, cx, cy, rx, ry, fill, outline=None, width=0):
    bbox = [cx-rx, cy-ry, cx+rx, cy+ry]
    if outline:
        d.ellipse(bbox, fill=None, outline=outline, width=width)
    else:
        d.ellipse(bbox, fill=fill)

def draw_circle(d, cx, cy, r, fill, outline=None, width=0):
    draw_ellipse(d, cx, cy, r, r, fill, outline, width)

def draw_round_rect(d, cx, cy, w, h, r, fill, outline=None, width=0):
    bbox = [cx-w/2, cy-h/2, cx+w/2, cy+h/2]
    if outline:
        d.rounded_rectangle(bbox, radius=r, fill=None, outline=outline, width=width)
    else:
        d.rounded_rectangle(bbox, radius=r, fill=fill)

def draw_rect(d, cx, cy, w, h, fill, outline=None, width=0):
    bbox = [cx-w/2, cy-h/2, cx+w/2, cy+h/2]
    if outline:
        d.rectangle(bbox, fill=None, outline=outline, width=width)
    else:
        d.rectangle(bbox, fill=fill)

def draw_line(d, x1, y1, x2, y2, fill, width=1):
    d.line([(x1,y1),(x2,y2)], fill=fill, width=width)


# ── Sprite builders ──────────────────────────────────────

def new_canvas():
    """Create a blank RGBA canvas."""
    return Image.new('RGBA', (RES, RES), (0,0,0,0)), ImageDraw.Draw(Image.new('RGBA', (RES, RES), (0,0,0,0)))


def make_canvas():
    img = Image.new('RGBA', (RES, RES), (0,0,0,0))
    return img, ImageDraw.Draw(img)


def body_ellipse(p, w, h, label="body"):
    """Main body as ellipse."""
    cx, cy = RES/2, RES/2
    rx = (w/2) * (RES/2.4)
    ry = (h/2) * (RES/2.4)
    img, d = make_canvas()
    draw_ellipse(d, cx, cy, rx, ry, p['body'], outline=p['out'], width=max(2,int(RES*0.006)))
    return img

def body_rect(p, w, h, label="body"):
    """Main body as rounded rectangle."""
    cx, cy = RES/2, RES/2
    rw = (w/2) * (RES/2.4) * 2
    rh = (h/2) * (RES/2.4) * 2
    rr = min(rw, rh) * 0.12
    img, d = make_canvas()
    draw_round_rect(d, cx, cy, rw, rh, rr, p['body'], outline=p['out'], width=max(2,int(RES*0.006)))
    return img

def outline_layer(p, w, h):
    """Outer outline/border."""
    cx, cy = RES/2, RES/2
    rw = (w/2) * (RES/2.4) * 2 * 1.06
    rh = (h/2) * (RES/2.4) * 2 * 1.06
    rr = min(rw, rh) * 0.08
    img, d = make_canvas()
    draw_round_rect(d, cx, cy, rw, rh, rr, p['out'])
    return img

def accent_stripe(p, w, h):
    """Center accent stripe."""
    cx, cy = RES/2, RES/2
    rw = (w/2) * (RES/2.4) * 2 * 0.58
    rh = (h/2) * (RES/2.4) * 2 * 0.08
    rr = min(rw, rh) * 0.2
    img, d = make_canvas()
    draw_round_rect(d, cx, cy, rw, rh, rr, p['acc'])
    return img


def composite(base, *layers):
    """Composite layers onto base (bottom to top)."""
    result = base.copy()
    for layer, offset in layers:
        result = Image.alpha_composite(result, layer)
    return result


def save_sprite(img, subdir, name):
    path = os.path.join(OUTPUT_DIR, subdir)
    os.makedirs(path, exist_ok=True)
    out = os.path.join(path, f"{name}.png")
    img.save(out, 'PNG')
    return out


# ── HERO BUILDERS ────────────────────────────────────────
def build_hero(name):
    p = P[name]
    w = h = SIZE[name]
    cx, cy = RES/2, RES/2
    scale = RES / 2.4  # body fits within ~half the canvas

    layers = []

    # Outline
    layers.append((outline_layer(p, w, h), (0,0)))

    # Body
    layers.append((body_ellipse(p, w, h), (0,0)))

    # Accent stripe
    layers.append((accent_stripe(p, w, h), (0,0)))

    # Eyes
    es = w * scale * 0.07  # eye size
    ex = w * scale * 0.22  # eye offset from center
    eye_l = Image.new('RGBA', (RES, RES), (0,0,0,0))
    draw_circle(ImageDraw.Draw(eye_l), cx - ex, cy, es, p['eye'])
    layers.append((eye_l, (0,0)))

    eye_r = Image.new('RGBA', (RES, RES), (0,0,0,0))
    draw_circle(ImageDraw.Draw(eye_r), cx + ex, cy, es, p['eye'])
    layers.append((eye_r, (0,0)))

    # Hero-specific details
    if name == 'atlas':
        # Diamond chest emblem
        ew = w * scale * 0.22
        eh = w * scale * 0.22 * 0.3
        emblem = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(emblem)
        # Diamond shape
        pts = [(cx, cy-eh), (cx+ew, cy), (cx, cy+eh), (cx-ew, cy)]
        d.polygon(pts, fill=p['met'])
        layers.append((emblem, (0,0)))

    elif name == 'zephyr':
        # Wing blades
        for sx in (-1, 1):
            wing = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(wing)
            ww = w * scale * 0.32
            wh = h * scale * 0.1
            wx = cx + sx * w * scale * 0.28
            draw_round_rect(d, wx, cy, ww, wh, min(ww,wh)*0.15, p['acc'])
            layers.append((wing, (0,0)))

    elif name == 'synapse':
        # Outer glow ring
        for i, (r_mult, alpha) in enumerate([(1.2, 60), (1.05, 40)]):
            ring = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(ring)
            cr = w * scale * 0.45 * r_mult
            draw_circle(d, cx, cy, cr, (*p['met'], alpha))
            layers.append((ring, (0,0)))

    elif name == 'volt':
        # Lightning spikes
        for ang_deg in range(0, 360, 45):
            ang = math.radians(ang_deg)
            sp = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(sp)
            sr = w * scale * 0.05
            sx = cx + math.cos(ang) * w * scale * 0.55
            sy = cy + math.sin(ang) * w * scale * 0.55
            draw_circle(d, sx, sy, sr, (255, 217, 25))
            layers.append((sp, (0,0)))

    # Composite all layers
    result = Image.new('RGBA', (RES, RES), (0,0,0,0))
    for layer, offset in layers:
        result = Image.alpha_composite(result, layer)

    save_sprite(result, 'heroes', name)
    return result


# ── ENEMY BUILDERS ───────────────────────────────────────
def build_enemy(name):
    p = P[name]
    w = SIZE[name] * ASPECT[name][0]
    h = SIZE[name] * ASPECT[name][1]
    cx, cy = RES/2, RES/2
    scale = RES / 2.4

    layers = []

    # Outline
    layers.append((outline_layer(p, w, h), (0,0)))

    # Body shape by type
    body_img = Image.new('RGBA', (RES, RES), (0,0,0,0))
    d = ImageDraw.Draw(body_img)
    rw = w * scale
    rh = h * scale

    if name in ('drone', 'parasite', 'shielder', 'exploder', 'apex'):
        draw_ellipse(d, cx, cy, rw/2, rh/2, p['body'], outline=p['out'], width=max(2,int(RES*0.006)))
    elif name in ('brute', 'healer'):
        draw_round_rect(d, cx, cy, rw, rh, min(rw,rh)*0.14, p['body'], outline=p['out'], width=max(2,int(RES*0.006)))
    elif name == 'sprinter':
        draw_round_rect(d, cx, cy, rw*1.1, rh*0.7, min(rw,rh)*0.08, p['body'], outline=p['out'], width=max(2,int(RES*0.006)))
    elif name == 'artillery':
        draw_round_rect(d, cx, cy, rw*0.8, rh, min(rw,rh)*0.1, p['body'], outline=p['out'], width=max(2,int(RES*0.006)))
    elif name == 'burrower':
        draw_round_rect(d, cx, cy, rw*0.85, rh*1.2, min(rw,rh)*0.1, p['body'], outline=p['out'], width=max(2,int(RES*0.006)))
    else:
        draw_ellipse(d, cx, cy, rw/2, rh/2, p['body'], outline=p['out'], width=max(2,int(RES*0.006)))
    layers.append((body_img, (0,0)))


    # Accent stripe
    layers.append((accent_stripe(p, w, h), (0,0)))

    # Eyes
    es = min(w,h) * scale * 0.075
    eye_off = es * 1.6
    for ex in (-eye_off, eye_off):
        eye = Image.new('RGBA', (RES, RES), (0,0,0,0))
        draw_circle(ImageDraw.Draw(eye), cx + ex, cy, es, p['eye'])
        layers.append((eye, (0,0)))

    # Enemy-specific details
    if name == 'drone':
        for ang_deg in (0, 90, 180, 270):
            ang = math.radians(ang_deg)
            rt = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(rt)
            rr = min(w,h) * scale * 0.22
            rx = cx + math.cos(ang) * w * scale * 0.4
            ry = cy + math.sin(ang) * h * scale * 0.4
            draw_ellipse(d, rx, ry, rr, rr*0.3, p['met'])
            layers.append((rt, (0,0)))

    elif name == 'brute':
        for sx, sy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
            pt = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(pt)
            pw = w * scale * 0.18
            ph = h * scale * 0.14
            px = cx + sx * w * scale * 0.25
            py = cy + sy * h * scale * 0.2
            draw_round_rect(d, px, py, pw, ph, min(pw,ph)*0.15, p['met'])
            layers.append((pt, (0,0)))

    elif name == 'sprinter':
        for i in range(3):
            sl = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(sl)
            sw = w * scale * 0.1
            sh = h * scale * 0.022
            sy = cy + (i-1) * h * scale * 0.1
            draw_rect(d, cx, sy, sw, sh, p['acc'])
            layers.append((sl, (0,0)))

    elif name == 'artillery':
        # Barrel
        bt = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(bt)
        bw = w * scale * 0.1
        bh = h * scale * 0.3
        bx, by = cx, cy + h * scale * 0.25
        draw_round_rect(d, bx, by, bw, bh, min(bw,bh)*0.3, p['met'])
        layers.append((bt, (0,0)))
        # Base ring
        bs = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(bs)
        bsw = w * scale * 0.48
        bsh = h * scale * 0.08
        draw_round_rect(d, cx, cy - h * scale * 0.15, bsw, bsh, min(bsw,bsh)*0.3, p['acc'])
        layers.append((bs, (0,0)))

    elif name == 'shielder':
        # Shield arc (semi-transparent)
        sh = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(sh)
        sr = min(w,h) * scale * 0.58
        draw_circle(d, cx, cy - h * scale * 0.1, sr, (64,64,166,80))
        draw_circle(d, cx, cy - h * scale * 0.1, sr * 1.35, (64,64,166,30))
        layers.append((sh, (0,0)))

    elif name == 'healer':
        # Healing halo
        for i, (r_mult, alpha) in enumerate([(1.25, 50), (1.05, 80)]):
            hl = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(hl)
            hr = min(w,h) * scale * 0.47 * r_mult
            draw_circle(d, cx, cy, hr, (*p['met'], alpha))
            layers.append((hl, (0,0)))

    elif name == 'exploder':
        for i in range(8):
            ang = (i/8) * math.pi * 2
            sp = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(sp)
            sr = min(w,h) * scale * 0.06
            sx = cx + math.cos(ang) * w * scale * 0.48
            sy = cy + math.sin(ang) * h * scale * 0.48
            draw_circle(d, sx, sy, sr, p['acc'])
            layers.append((sp, (0,0)))

    elif name == 'burrower':
        for i in range(3):
            sg = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(sg)
            sw = w * scale * 0.38
            sh = h * scale * 0.12
            sy = cy + (i-1) * h * scale * 0.2
            draw_round_rect(d, cx, sy, sw, sh, min(sw,sh)*0.15, p['acc'])
            layers.append((sg, (0,0)))

    elif name == 'parasite':
        for ang_deg in range(0, 360, 45):
            ang = math.radians(ang_deg)
            tn = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(tn)
            tr = min(w,h) * scale * 0.025
            tx = cx + math.cos(ang) * w * scale * 0.46
            ty = cy + math.sin(ang) * h * scale * 0.46
            draw_circle(d, tx, ty, tr, p['acc'])
            layers.append((tn, (0,0)))

    elif name == 'apex':
        # Horns
        for ang_deg in (45, 135, 225, 315):
            ang = math.radians(ang_deg)
            hn = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(hn)
            hw = w * scale * 0.07
            hh = h * scale * 0.18
            hx = cx + math.cos(ang) * w * scale * 0.42
            hy = cy + math.sin(ang) * h * scale * 0.42
            draw_round_rect(d, hx, hy, hw, hh, min(hw,hh)*0.25, p['met'])
            layers.append((hn, (0,0)))
        # Flanker plates
        for sx, sy in [(-1,0),(1,0),(0,-1),(0,1)]:
            pt = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(pt)
            pw = w * scale * 0.14
            ph = h * scale * 0.14
            px = cx + sx * w * scale * 0.28
            py = cy + sy * h * scale * 0.28
            draw_round_rect(d, px, py, pw, ph, min(pw,ph)*0.1, p['met'])
            layers.append((pt, (0,0)))

    # Composite
    result = Image.new('RGBA', (RES, RES), (0,0,0,0))
    for layer, offset in layers:
        result = Image.alpha_composite(result, layer)

    save_sprite(result, 'enemies', name)
    return result


# ── PROJECTILE BUILDERS ──────────────────────────────────
def build_projectile(name, palette, ability):
    p = P[palette]
    cx, cy = RES/2, RES/2

    layers = []

    if ability:
        # Glowing orb with aura
        gl = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(gl)
        gr = 0.3 * RES/2.4
        draw_circle(d, cx, cy, gr, (*p['acc'], 80))
        layers.append((gl, (0,0)))

        bd = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(bd)
        br = 0.18 * RES/2.4
        draw_circle(d, cx, cy, br, p['body'])
        layers.append((bd, (0,0)))

        co = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(co)
        cr = 0.06 * RES/2.4
        draw_circle(d, cx, cy, cr, (255,255,255))
        layers.append((co, (0,0)))
    else:
        # Small projectile with trail
        bd = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(bd)
        br = 0.12 * RES/2.4
        draw_circle(d, cx, cy, br, p['body'])
        layers.append((bd, (0,0)))

        tr = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(tr)
        tw = 0.06 * RES/2.4
        th = 0.02 * RES/2.4
        tx = cx - 0.07 * RES/2.4
        draw_rect(d, tx, cy, tw, th, (*p['body'], 89))
        layers.append((tr, (0,0)))

    result = Image.new('RGBA', (RES, RES), (0,0,0,0))
    for layer, offset in layers:
        result = Image.alpha_composite(result, layer)

    save_sprite(result, 'projectiles', name)
    return result


# ── DECAL BUILDERS ───────────────────────────────────────
def build_decal(name, dtype):
    cx, cy = RES/2, RES/2
    scale = RES / 2.4

    layers = []

    if dtype == 'blood':
        pl = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(pl)
        pr = 0.14 * scale
        draw_circle(d, cx, cy, pr, (115, 5, 5))
        layers.append((pl, (0,0)))
        for _ in range(5):
            a = random.uniform(0, math.pi*2)
            dist = random.uniform(0.08, 0.28) * scale
            bl = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(bl)
            br = random.uniform(0.025, 0.065) * scale
            bx = cx + math.cos(a) * dist
            by = cy + math.sin(a) * dist
            draw_circle(d, bx, by, br, (140, 8, 8))
            layers.append((bl, (0,0)))

    elif dtype == 'scorch':
        o = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(o)
        or_ = 0.28 * scale
        draw_circle(d, cx, cy, or_, (10, 10, 7, 180))
        layers.append((o, (0,0)))
        i = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(i)
        ir = 0.14 * scale
        draw_circle(d, cx, cy, ir, (20, 15, 10, 200))
        layers.append((i, (0,0)))
        for _ in range(4):
            m = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(m)
            mr = random.uniform(0.015, 0.035) * scale
            mx = cx + random.uniform(-0.13, 0.13) * scale
            my = cy + random.uniform(-0.13, 0.13) * scale
            draw_circle(d, mx, my, mr, (5, 5, 5, 200))
            layers.append((m, (0,0)))

    elif dtype == 'crater':
        rm = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(rm)
        rm_r = 0.32 * scale
        draw_circle(d, cx, cy, rm_r, (64, 48, 33, 220))
        layers.append((rm, (0,0)))
        fl = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(fl)
        fl_r = 0.22 * scale
        draw_circle(d, cx, cy, fl_r, (20, 15, 13, 220))
        layers.append((fl, (0,0)))
        for _ in range(5):
            rk = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(rk)
            rk_r = random.uniform(0.01, 0.03) * scale
            ang = random.uniform(0, math.pi*2)
            dist = random.uniform(0.28, 0.36) * scale
            rx = cx + math.cos(ang) * dist
            ry = cy + math.sin(ang) * dist
            draw_circle(d, rx, ry, rk_r, (76, 58, 46, 220))
            layers.append((rk, (0,0)))

    elif dtype == 'acid':
        pl = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(pl)
        pr = 0.28 * scale
        draw_circle(d, cx, cy, pr, (13, 96, 13, 200))
        layers.append((pl, (0,0)))
        for i in range(3):
            rp = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(rp)
            rr = (0.14 + i * 0.035) * scale
            draw_circle(d, cx, cy, rr, (18, 115, 18, 150 - i*30))
            layers.append((rp, (0,0)))

    elif dtype == 'explosion':
        o = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(o)
        o_r = 0.32 * scale
        draw_circle(d, cx, cy, o_r, (20, 10, 2, 200))
        layers.append((o, (0,0)))
        m = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(m)
        m_r = 0.18 * scale
        draw_circle(d, cx, cy, m_r, (89, 46, 5, 220))
        layers.append((m, (0,0)))
        ce = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(ce)
        ce_r = 0.06 * scale
        draw_circle(d, cx, cy, ce_r, (178, 153, 76, 255))
        layers.append((ce, (0,0)))

    result = Image.new('RGBA', (RES, RES), (0,0,0,0))
    for layer, offset in layers:
        result = Image.alpha_composite(result, layer)
    save_sprite(result, 'decals', name)
    return result


# ── VFX BUILDERS ─────────────────────────────────────────
def build_vfx(name, vtype):
    cx, cy = RES/2, RES/2
    scale = RES / 2.4
    layers = []

    if vtype == 'hitflash':
        o = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(o)
        or_ = 0.4 * scale
        draw_circle(d, cx, cy, or_, (255, 224, 184))
        layers.append((o, (0,0)))
        i = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(i)
        ir = 0.14 * scale
        draw_circle(d, cx, cy, ir, (255, 255, 255))
        layers.append((i, (0,0)))

    elif vtype == 'explosion':
        o = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(o)
        or_ = 0.4 * scale
        draw_circle(d, cx, cy, or_, (255, 81, 5, 200))
        layers.append((o, (0,0)))
        m = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(m)
        m_r = 0.22 * scale
        draw_circle(d, cx, cy, m_r, (255, 158, 15, 220))
        layers.append((m, (0,0)))
        ce = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(ce)
        ce_r = 0.06 * scale
        draw_circle(d, cx, cy, ce_r, (255, 255, 209))
        layers.append((ce, (0,0)))

    elif vtype == 'smoke':
        for si, (sz, al) in enumerate([(0.32, 71), (0.22, 56), (0.10, 46)]):
            pu = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(pu)
            ox = random.uniform(-0.02, 0.02) * scale
            oy = random.uniform(-0.02, 0.02) * scale
            draw_ellipse(d, cx+ox, cy+oy, sz*scale, sz*scale*0.9, (71, 62, 60, al))
            layers.append((pu, (0,0)))

    elif vtype == 'sparks':
        for i in range(10):
            an = random.uniform(0, math.pi*2)
            dist = random.uniform(0.05, 0.38) * scale
            sp = Image.new('RGBA', (RES, RES), (0,0,0,0))
            d = ImageDraw.Draw(sp)
            sr = random.uniform(0.006, 0.015) * scale
            sx = cx + math.cos(an) * dist
            sy = cy + math.sin(an) * dist
            col = random.choice([(255,191,20), (255,114,20), (255,242,217)])
            draw_circle(d, sx, sy, sr, col)
            layers.append((sp, (0,0)))
        cs = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(cs)
        draw_circle(d, cx, cy, 0.02*scale, (255,255,230))
        layers.append((cs, (0,0)))

    elif vtype == 'shockwave':
        o = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(o)
        or_ = 0.38 * scale
        draw_circle(d, cx, cy, or_, (153, 153, 255, 200))
        layers.append((o, (0,0)))
        i = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(i)
        ir = 0.25 * scale
        draw_circle(d, cx, cy, ir, (255, 209, 255, 220))
        layers.append((i, (0,0)))

    result = Image.new('RGBA', (RES, RES), (0,0,0,0))
    for layer, offset in layers:
        result = Image.alpha_composite(result, layer)
    save_sprite(result, 'vfx', name)
    return result


# ── ENVIRONMENT BUILDERS ─────────────────────────────────
def build_environment():
    os.makedirs(os.path.join(OUTPUT_DIR, 'environment'), exist_ok=True)
    scale = RES / 2.4

    # Arena floor tile
    def arena_floor():
        img = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(img)
        # Base (transparent — no fill)
        # tiles are drawn directly, grid lines below
        # Checkerboard tiles
        ts = 0.24 * scale
        gap = 0.012 * scale
        for tx in range(-5, 6):
            for ty in range(-5, 6):
                odd = (tx + ty) % 2 == 0
                c = (42, 42, 47) if odd else (38, 38, 44)
                draw_rect(d, tx*(ts+gap), ty*(ts+gap), ts, ts, c)
        # Grid lines
        grid_c = (20, 20, 23)
        for i in range(-5, 6):
            pos = i * 0.24 * scale
            draw_rect(d, pos, 0, 0.004*scale, 2.52*scale, grid_c)  # vertical
            draw_rect(d, 0, pos, 2.52*scale, 0.004*scale, grid_c)  # horizontal
        save_sprite(img, 'environment', 'arena_floor')

    arena_floor()

    # Crate
    def crate():
        img = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(img)
        draw_round_rect(d, RES/2, RES/2, 0.68*scale, 0.68*scale, 0.06*scale, (127, 76, 38))
        for i in range(3):
            draw_rect(d, RES/2, (i-1)*0.18*scale, 0.58*scale, 0.016*scale, (89, 51, 25))
        for sx, sy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
            draw_round_rect(d, sx*0.28*scale, sy*0.28*scale, 0.05*scale, 0.05*scale, 0.01*scale, (102, 64, 30))
        save_sprite(img, 'environment', 'crate')

    crate()

    # Barrel
    def barrel():
        img = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(img)
        draw_round_rect(d, RES/2, RES/2, 0.48*scale, 0.68*scale, 0.03*scale, (71, 71, 84))
        for i in range(3):
            draw_rect(d, RES/2, (i-1)*0.19*scale, 0.52*scale, 0.018*scale, (107, 107, 120))
        draw_circle(d, RES/2, 0.28*scale, 0.025*scale, (7, 7, 7))
        save_sprite(img, 'environment', 'barrel')

    barrel()

    # Wall
    def wall():
        img = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(img)
        draw_rect(d, RES/2, RES/2, 0.68*scale, 1.02*scale, (133, 120, 107))
        for row in range(3):
            for col in range(2):
                ox = 0.06*scale if row % 2 == 0 else -0.06*scale
                lx = -0.26*scale + col*0.36*scale + ox
                ly = row*0.3*scale - 0.3*scale
                lt = (row + col) % 2 == 0
                c = (115, 102, 89) if lt else (127, 117, 102)
                draw_rect(d, lx, ly, 0.26*scale, 0.26*scale, c)
        # Cracks
        draw_rect(d, 0.06*scale, -0.06*scale, 0.003*scale, 0.22*scale, (36, 36, 36))
        draw_rect(d, 0.10*scale, 0.04*scale, 0.003*scale, 0.14*scale, (36, 36, 36))
        save_sprite(img, 'environment', 'wall')

    wall()


# ── UI BUILDERS ──────────────────────────────────────────
def build_ui():
    os.makedirs(os.path.join(OUTPUT_DIR, 'ui'), exist_ok=True)
    scale = RES / 2.4

    def healthbar_bg():
        img = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(img)
        draw_round_rect(d, RES/2, RES/2, 1.7*scale, 0.15*scale, 0.022*scale, (28, 28, 28))
        save_sprite(img, 'ui', 'healthbar_bg')

    healthbar_bg()

    def healthbar_fill():
        img = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(img)
        draw_round_rect(d, RES/2, RES/2, 1.6*scale, 0.09*scale, 0.012*scale, (184, 15, 15))
        save_sprite(img, 'ui', 'healthbar_fill')

    healthbar_fill()

    def powerup_icon():
        img = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(img)
        draw_round_rect(d, RES/2, RES/2, 0.40*scale, 0.40*scale, 0.045*scale, (15, 15, 15))
        draw_circle(d, RES/2, RES/2, 0.34*scale, (178, 153, 30))
        save_sprite(img, 'ui', 'powerup_icon_bg')

    powerup_icon()

    def wave_banner():
        img = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(img)
        draw_round_rect(d, RES/2, RES/2, 3.4*scale, 0.8*scale, 0.1*scale, (7, 7, 12))
        draw_round_rect(d, RES/2, RES/2, 3.48*scale, 0.86*scale, 0.1*scale, (178, 153, 30))
        save_sprite(img, 'ui', 'wave_banner')

    wave_banner()

    def mainmenu_bg():
        img = Image.new('RGBA', (RES, RES), (0,0,0,0))
        d = ImageDraw.Draw(img)
        # Concentric rings on transparent bg — pixel radii
        radii = [220, 165, 110, 55]
        alphas = [80, 50, 35, 20]
        hues  = [(28,22,42), (35,27,50), (42,32,58), (48,38,65)]
        for i in range(4):
            draw_ellipse(d, RES/2, RES/2, radii[i], radii[i], (*hues[i], alphas[i]))
        save_sprite(img, 'ui', 'mainmenu_bg')

    mainmenu_bg()


# ── MAIN ─────────────────────────────────────────────────
def main():
    global OUTPUT_DIR
    args = sys.argv
    if '--output-dir' in args:
        idx = args.index('--output-dir')
        OUTPUT_DIR = args[idx + 1]
    else:
        OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'generated_pil')

    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"Hero's Arena — PIL Sprite Generator")
    print(f"Output: {OUTPUT_DIR}/")
    print(f"Res: {RES}×{RES} RGBA")
    print()

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

    # Count
    cnt = 0
    for root, _, files in os.walk(OUTPUT_DIR):
        for f in files:
            if f.endswith('.png'):
                cnt += 1
    print(); print(f"Done! {cnt} sprites → {OUTPUT_DIR}/")


if __name__ == '__main__':
    main()
