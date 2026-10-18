#!/usr/bin/env python3
"""
Generate crisp, high-resolution, cel-shaded vector character sprites for Hero's Arena.
All sprites are rendered at 256x256 with transparent background, facing Right (+X / 0 radians).
"""

import os, math
from PIL import Image, ImageDraw

OUTPUT_HEROES = "res/heroes"
OUTPUT_ENEMIES = "res/enemies"
CANVAS_SIZE = 256
CENTER = (128, 128)

def rotate_point(pt, angle_rad, origin=CENTER):
    ox, oy = origin
    px, py = pt
    s = math.sin(angle_rad)
    c = math.cos(angle_rad)
    qx = ox + c * (px - ox) - s * (py - oy)
    qy = oy + s * (px - ox) + c * (py - oy)
    return (qx, qy)

def draw_polygon(draw, points, fill, outline=(15, 20, 28, 255), width=3):
    draw.polygon(points, fill=fill)
    if outline and width > 0:
        draw.line(points + [points[0]], fill=outline, width=width)

def draw_circle(draw, center, radius, fill, outline=(15, 20, 28, 255), width=3):
    cx, cy = center
    bbox = [cx - radius, cy - radius, cx + radius, cy + radius]
    draw.ellipse(bbox, fill=fill, outline=outline, width=width)

def draw_glow_circle(img, center, radius, color_rgb, max_alpha=180):
    overlay = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    cx, cy = center
    steps = 10
    for i in range(steps, 0, -1):
        r = radius * (1.0 + (i / steps) * 0.8)
        alpha = int(max_alpha * (1.0 - (i / steps))**1.5)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(color_rgb[0], color_rgb[1], color_rgb[2], alpha))
    img.alpha_composite(overlay)

# ── HERO GENERATORS ──────────────────────────────────────────────────────────

def generate_atlas():
    # Heavy kinetic juggernaut: broad hexagonal chassis, forward slam gauntlets, glowing amber power core
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Hydraulic shoulder plates / pauldrons
    draw_polygon(draw, [(cx-40, cy-65), (cx+25, cy-70), (cx+15, cy-40), (cx-50, cy-35)], fill=(45, 52, 68), width=3)
    draw_polygon(draw, [(cx-40, cy+65), (cx+25, cy+70), (cx+15, cy+40), (cx-50, cy+35)], fill=(45, 52, 68), width=3)
    
    # Kinetic slam gauntlets (forward-facing on right)
    draw_polygon(draw, [(cx+20, cy-55), (cx+70, cy-48), (cx+75, cy-25), (cx+25, cy-30)], fill=(227, 199, 106), width=3)
    draw_polygon(draw, [(cx+20, cy+55), (cx+70, cy+48), (cx+75, cy+25), (cx+25, cy+30)], fill=(227, 199, 106), width=3)
    
    # Heavy titanium main chassis (hexagonal)
    hull = [(cx-55, cy-45), (cx+10, cy-50), (cx+50, 0), (cx+10, cy+50), (cx-55, cy+45), (cx-70, 0)]
    draw_polygon(draw, hull, fill=(30, 36, 48), width=4)
    
    # Interior armor bevel
    inner = [(cx-45, cy-35), (cx+5, cy-38), (cx+38, 0), (cx+5, cy+38), (cx-45, cy+35), (cx-55, 0)]
    draw_polygon(draw, inner, fill=(50, 60, 80), width=2)
    
    # Kinetic Core (Amber gold reactor)
    draw_glow_circle(img, (cx-5, cy), 22, (255, 170, 34), 160)
    draw_circle(draw, (cx-5, cy), 16, fill=(255, 185, 50), outline=(227, 199, 106), width=3)
    draw_circle(draw, (cx-5, cy), 7, fill=(255, 240, 180), outline=None)
    
    # Forward visor slit
    draw.line([(cx+25, cy-12), (cx+35, 0), (cx+25, cy+12)], fill=(255, 200, 60), width=4)
    return img

def generate_zephyr():
    # Aerodynamic sweeping delta-wing chassis, forward razor windblades, cyan slipstream vents
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Twin energy katanas / forward wing blades
    draw_polygon(draw, [(cx-10, cy-50), (cx+78, cy-18), (cx+90, cy-22), (cx+15, cy-60)], fill=(56, 189, 248), width=2)
    draw_polygon(draw, [(cx-10, cy+50), (cx+78, cy+18), (cx+90, cy+22), (cx+15, cy+60)], fill=(56, 189, 248), width=2)
    
    # Delta sweeping wings
    wing_pts = [(cx-75, cy-60), (cx-20, cy-45), (cx+50, 0), (cx-20, cy+45), (cx-75, cy+60), (cx-60, 0)]
    draw_polygon(draw, wing_pts, fill=(20, 28, 42), width=3)
    
    # Silver fuselage center
    fuse = [(cx-55, cy-22), (cx+40, 0), (cx-55, cy+22), (cx-65, 0)]
    draw_polygon(draw, fuse, fill=(200, 215, 230), width=2)
    
    # Slipstream cyan thruster core
    draw_glow_circle(img, (cx-10, cy), 18, (56, 189, 248), 180)
    draw_circle(draw, (cx-10, cy), 12, fill=(34, 211, 238), outline=(255, 255, 255), width=2)
    
    # Forward aerodynamic cockpit visor
    draw_polygon(draw, [(cx+10, cy-6), (cx+32, 0), (cx+10, cy+6)], fill=(255, 255, 255), outline=(56, 189, 248), width=2)
    return img

def generate_synapse():
    # Psionic telekinetic savant: crystalline floating chassis with 3 orbiting focusing shards
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # 3 Floating psionic focusing shards
    shards = [
        [(cx+65, cy-35), (cx+85, cy-25), (cx+60, cy-15)],
        [(cx+65, cy+35), (cx+85, cy+25), (cx+60, cy+15)],
        [(cx-75, cy), (cx-60, cy-20), (cx-60, cy+20)]
    ]
    for s in shards:
        draw_polygon(draw, s, fill=(192, 132, 252), outline=(217, 70, 239), width=2)
        
    # Central crystalline chassis
    poly = [(cx-45, cy-40), (cx+15, cy-45), (cx+55, 0), (cx+15, cy+40), (cx-45, cy+45), (cx-30, 0)]
    draw_polygon(draw, poly, fill=(30, 20, 45), width=3)
    
    # Inner psychic geometry
    draw_polygon(draw, [(cx-25, cy-25), (cx+10, cy-28), (cx+35, 0), (cx+10, cy+28), (cx-25, cy+25)], fill=(75, 45, 105), width=2)
    
    # Amethyst third-eye psychic core
    draw_glow_circle(img, (cx+5, cy), 24, (217, 70, 239), 180)
    draw_circle(draw, (cx+5, cy), 15, fill=(232, 121, 249), outline=(255, 255, 255), width=2)
    draw_circle(draw, (cx+5, cy), 6, fill=(255, 255, 255), outline=None)
    
    # Forward psychic focusing beam emitter
    draw.line([(cx+35, cy-8), (cx+50, 0), (cx+35, cy+8)], fill=(45, 212, 191), width=3)
    return img

def generate_volt():
    # Electro-kinetic storm runner: sharp chevron chassis, twin Tesla coil prongs, electric spark nodes
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Twin Tesla prongs (pointing right)
    draw_polygon(draw, [(cx+10, cy-35), (cx+85, cy-32), (cx+75, cy-22), (cx+10, cy-25)], fill=(250, 204, 21), width=2)
    draw_polygon(draw, [(cx+10, cy+35), (cx+85, cy+32), (cx+75, cy+22), (cx+10, cy+25)], fill=(250, 204, 21), width=2)
    
    # Main chevron chassis
    pts = [(cx-70, cy-50), (cx-30, cy-30), (cx+40, 0), (cx-30, cy+30), (cx-70, cy+50), (cx-45, 0)]
    draw_polygon(draw, pts, fill=(24, 24, 27), width=3)
    
    # Capacitor plating
    draw_polygon(draw, [(cx-40, cy-25), (cx+20, 0), (cx-40, cy+25), (cx-30, 0)], fill=(63, 63, 70), width=2)
    
    # Lightning reactor core
    draw_glow_circle(img, (cx-5, cy), 20, (250, 204, 21), 190)
    draw_circle(draw, (cx-5, cy), 13, fill=(254, 240, 138), outline=(250, 204, 21), width=2)
    
    # Arcing electrodes
    draw.line([(cx+40, cy-15), (cx+60, 0), (cx+40, cy+15)], fill=(56, 189, 248), width=3)
    return img

def generate_ignis():
    # Pyromancer warlord: armored furnace chassis, twin flame exhausts, molten combustion hearth
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Rear volcanic exhaust vents
    draw_polygon(draw, [(cx-75, cy-45), (cx-45, cy-35), (cx-45, cy-15), (cx-75, cy-25)], fill=(249, 115, 22), width=2)
    draw_polygon(draw, [(cx-75, cy+45), (cx-45, cy+35), (cx-45, cy+15), (cx-75, cy+25)], fill=(249, 115, 22), width=2)
    
    # Heavy furnace hull
    hull = [(cx-60, cy-40), (cx+15, cy-45), (cx+55, 0), (cx+15, cy+40), (cx-60, cy+40), (cx-40, 0)]
    draw_polygon(draw, hull, fill=(28, 25, 23), width=4)
    
    # Heat-shield armor plates (crimson)
    draw_polygon(draw, [(cx-35, cy-30), (cx+10, cy-32), (cx+35, 0), (cx+10, cy+32), (cx-35, cy+30)], fill=(153, 27, 27), width=2)
    
    # Molten combustion hearth (blazing orange/red glow)
    draw_glow_circle(img, (cx, cy), 24, (249, 115, 22), 200)
    draw_circle(draw, (cx, cy), 16, fill=(239, 68, 68), outline=(254, 215, 170), width=2)
    draw_circle(draw, (cx, cy), 8, fill=(255, 237, 213), outline=None)
    
    # Forward flame thrower muzzle
    draw_polygon(draw, [(cx+40, cy-15), (cx+68, cy-8), (cx+68, cy+8), (cx+40, cy+15)], fill=(120, 113, 108), width=2)
    return img

def generate_toxis():
    # Venom alchemist: bio-carapace chassis with pressurized fluid conduits and venom injector stinger
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Dual acid fluid canisters
    draw_circle(draw, (cx-35, cy-40), 16, fill=(20, 83, 45), outline=(132, 204, 22), width=2)
    draw_circle(draw, (cx-35, cy+40), 16, fill=(20, 83, 45), outline=(132, 204, 22), width=2)
    draw_glow_circle(img, (cx-35, cy-40), 12, (132, 204, 22), 140)
    draw_glow_circle(img, (cx-35, cy+40), 12, (132, 204, 22), 140)
    
    # Chitinous bio-carapace hull
    hull = [(cx-55, cy-30), (cx, cy-45), (cx+50, 0), (cx, cy+45), (cx-55, cy+30), (cx-40, 0)]
    draw_polygon(draw, hull, fill=(15, 23, 18), width=3)
    
    # Inner bio-ribs
    draw_polygon(draw, [(cx-30, cy-22), (cx+10, cy-28), (cx+30, 0), (cx+10, cy+28), (cx-30, cy+22)], fill=(34, 197, 94), width=2)
    
    # Acid stinger / injector needle (forward)
    draw_polygon(draw, [(cx+35, cy-8), (cx+85, 0), (cx+35, cy+8)], fill=(163, 230, 53), outline=(15, 20, 28), width=2)
    
    # Toxic glowing core
    draw_glow_circle(img, (cx, cy), 18, (132, 204, 22), 180)
    draw_circle(draw, (cx, cy), 11, fill=(190, 242, 100), outline=(255, 255, 255), width=2)
    return img

def generate_havoc():
    # Demolitions expert: heavy reinforced quad-tread walker, twin mortar barrels, hazard stripes
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Tread cowlings / tracks
    draw_polygon(draw, [(cx-65, cy-65), (cx+30, cy-65), (cx+40, cy-45), (cx-55, cy-45)], fill=(30, 41, 59), width=3)
    draw_polygon(draw, [(cx-65, cy+65), (cx+30, cy+65), (cx+40, cy+45), (cx-55, cy+45)], fill=(30, 41, 59), width=3)
    
    # Twin heavy cannon barrels (forward)
    draw_polygon(draw, [(cx+25, cy-25), (cx+85, cy-25), (cx+85, cy-12), (cx+25, cy-12)], fill=(71, 85, 105), width=3)
    draw_polygon(draw, [(cx+25, cy+12), (cx+85, cy+12), (cx+85, cy+25), (cx+25, cy+25)], fill=(71, 85, 105), width=3)
    
    # Heavy armored bunker hull
    hull = [(cx-55, cy-42), (cx+25, cy-42), (cx+48, 0), (cx+25, cy+42), (cx-55, cy+42), (cx-65, 0)]
    draw_polygon(draw, hull, fill=(234, 88, 12), width=4)
    
    # Hazard stripe panel
    draw_polygon(draw, [(cx-35, cy-28), (cx+15, cy-28), (cx+25, 0), (cx+15, cy+28), (cx-35, cy+28)], fill=(234, 179, 8), width=2)
    draw.line([(cx-25, cy-28), (cx-10, cy+28)], fill=(15, 23, 42), width=5)
    draw.line([(cx-5, cy-28), (cx+10, cy+28)], fill=(15, 23, 42), width=5)
    
    # Armored blast viewport
    draw_polygon(draw, [(cx+15, cy-10), (cx+30, 0), (cx+15, cy+10)], fill=(15, 23, 42), width=2)
    return img

# ── ENEMY GENERATORS ─────────────────────────────────────────────────────────

def generate_drone():
    # Swarm scout: agile triangular dart drone with glowing red mono-eye
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Triangular scout body pointing right
    hull = [(cx-65, cy-45), (cx+60, 0), (cx-65, cy+45), (cx-40, 0)]
    draw_polygon(draw, hull, fill=(55, 65, 81), width=3)
    
    # Wing thruster fins
    draw_polygon(draw, [(cx-45, cy-30), (cx+20, 0), (cx-45, cy+30)], fill=(75, 85, 99), width=2)
    
    # Central scanning red mono-eye
    draw_glow_circle(img, (cx+10, cy), 16, (239, 68, 68), 200)
    draw_circle(draw, (cx+10, cy), 10, fill=(220, 38, 38), outline=(255, 255, 255), width=2)
    draw_circle(draw, (cx+10, cy), 4, fill=(255, 255, 255), outline=None)
    return img

def generate_brute():
    # Massive armored ram: heavy pentagonal siege armor with battering-ram cowlings
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Heavy battering horns / cowlings (forward)
    draw_polygon(draw, [(cx-10, cy-75), (cx+80, cy-50), (cx+60, cy-25), (cx-10, cy-45)], fill=(180, 83, 9), width=3)
    draw_polygon(draw, [(cx-10, cy+75), (cx+80, cy+50), (cx+60, cy+25), (cx-10, cy+45)], fill=(180, 83, 9), width=3)
    
    # Massive pentagonal chassis
    hull = [(cx-70, cy-55), (cx+20, cy-65), (cx+65, 0), (cx+20, cy+65), (cx-70, cy+55), (cx-60, 0)]
    draw_polygon(draw, hull, fill=(68, 20, 15), width=4)
    
    # Plated reinforced core
    draw_polygon(draw, [(cx-45, cy-35), (cx+15, cy-40), (cx+35, 0), (cx+15, cy+40), (cx-45, cy+35)], fill=(120, 40, 30), width=2)
    
    # Amber optic sensors
    draw_glow_circle(img, (cx+15, cy), 18, (245, 158, 11), 180)
    draw_circle(draw, (cx+15, cy), 12, fill=(251, 191, 36), outline=(255, 255, 255), width=2)
    return img

def generate_sprinter():
    # Charger: raptor-like aerodynamic wedge with forward-pointing twin talons
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Forward talons/mandibles
    draw_polygon(draw, [(cx+20, cy-25), (cx+88, cy-15), (cx+60, cy-5), (cx+20, cy-12)], fill=(248, 113, 113), width=2)
    draw_polygon(draw, [(cx+20, cy+25), (cx+88, cy+15), (cx+60, cy+5), (cx+20, cy+12)], fill=(248, 113, 113), width=2)
    
    # Sleek dart chassis
    hull = [(cx-75, cy-35), (cx+40, 0), (cx-75, cy+35), (cx-55, 0)]
    draw_polygon(draw, hull, fill=(24, 24, 27), width=3)
    
    # Speed streak accents
    draw_polygon(draw, [(cx-50, cy-18), (cx+15, 0), (cx-50, cy+18)], fill=(239, 68, 68), width=2)
    
    # Glowing yellow optic slit
    draw_glow_circle(img, (cx+10, cy), 14, (250, 204, 21), 180)
    draw.line([(cx+5, cy-8), (cx+25, 0), (cx+5, cy+8)], fill=(254, 240, 138), width=3)
    return img

def generate_artillery():
    # Long-range bombardment crawler: heavy platform with oversized elevated mortar cannon
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # 6 Outrigger stabilizer legs
    for angle in [-60, -100, -140, 60, 100, 140]:
        rad = math.radians(angle)
        ex = cx + math.cos(rad) * 75
        ey = cy + math.sin(rad) * 75
        draw.line([(cx, cy), (ex, ey)], fill=(51, 65, 85), width=6)
        draw_circle(draw, (ex, ey), 8, fill=(30, 41, 59), width=2)
        
    # Main platform chassis
    hull = [(cx-50, cy-45), (cx+30, cy-45), (cx+50, 0), (cx+30, cy+45), (cx-50, cy+45), (cx-60, 0)]
    draw_polygon(draw, hull, fill=(30, 41, 59), width=3)
    
    # Heavy mortar barrel (facing right)
    draw_polygon(draw, [(cx-15, cy-22), (cx+78, cy-22), (cx+78, cy+22), (cx-15, cy+22)], fill=(71, 85, 105), width=3)
    draw_circle(draw, (cx+78, cy), 16, fill=(15, 23, 42), outline=(249, 115, 22), width=3)
    draw_glow_circle(img, (cx+78, cy), 14, (249, 115, 22), 180)
    return img

def generate_shielder():
    # Protector: heavy bulwark chassis projecting a translucent forward curved energy barrier
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Curved forward energy barrier arc
    draw_glow_circle(img, (cx+60, cy), 35, (59, 130, 246), 140)
    draw.arc([cx+20, cy-75, cx+90, cy+75], start=-90, end=90, fill=(96, 165, 250), width=6)
    
    # Bulwark hexagonal chassis
    hull = [(cx-60, cy-50), (cx+15, cy-55), (cx+50, 0), (cx+15, cy+55), (cx-60, cy+50), (cx-45, 0)]
    draw_polygon(draw, hull, fill=(23, 37, 84), width=3)
    
    # Shield generator emitter core
    draw_polygon(draw, [(cx-35, cy-30), (cx+20, 0), (cx-35, cy+30)], fill=(30, 58, 138), width=2)
    draw_glow_circle(img, (cx+5, cy), 22, (59, 130, 246), 200)
    draw_circle(draw, (cx+5, cy), 14, fill=(96, 165, 250), outline=(255, 255, 255), width=2)
    return img

def generate_healer():
    # Support medic: cross/caduceus bio-mech chassis with glowing circular restoration emitter
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Radiant restoration aura ring
    draw_glow_circle(img, (cx, cy), 40, (16, 185, 129), 130)
    
    # Medical cross chassis
    arm_w = 22
    span = 65
    draw.rectangle([cx-arm_w, cy-span, cx+arm_w, cy+span], fill=(6, 78, 59), outline=(15, 20, 28), width=3)
    draw.rectangle([cx-span, cy-arm_w, cx+span, cy+arm_w], fill=(6, 78, 59), outline=(15, 20, 28), width=3)
    
    # Inner white/mint cross
    iw = 12
    ispan = 50
    draw.rectangle([cx-iw, cy-ispan, cx+iw, cy+ispan], fill=(209, 250, 229))
    draw.rectangle([cx-ispan, cy-iw, cx+ispan, cy+iw], fill=(209, 250, 229))
    
    # Radiant emerald core
    draw_glow_circle(img, (cx, cy), 20, (16, 185, 129), 220)
    draw_circle(draw, (cx, cy), 13, fill=(52, 211, 153), outline=(255, 255, 255), width=2)
    return img

def generate_exploder():
    # Volatile bomb: swollen pressurized spheroid with bright glowing cracked fissure seams
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Volatile explosion warning aura
    draw_glow_circle(img, (cx, cy), 45, (220, 38, 38), 160)
    
    # Bulbous pressurized spheroid
    draw_circle(draw, (cx, cy), 58, fill=(69, 10, 10), outline=(15, 20, 28), width=4)
    
    # Glowing surface fracture fissures
    fissures = [
        [(cx-40, cy-25), (cx-10, cy-5), (cx+30, cy-35)],
        [(cx-35, cy+30), (cx+5, cy+10), (cx+40, cy+25)],
        [(cx-10, cy-45), (cx+5, cy-15), (cx-5, cy+25), (cx+15, cy+45)],
        [(cx+10, cy-15), (cx+45, 0), (cx+25, cy+15)]
    ]
    for f in fissures:
        draw.line(f, fill=(251, 146, 60), width=4)
        draw.line(f, fill=(255, 237, 213), width=2)
        
    # Critical volatile core
    draw_glow_circle(img, (cx, cy), 22, (239, 68, 68), 220)
    draw_circle(draw, (cx, cy), 14, fill=(254, 202, 202), outline=(239, 68, 68), width=2)
    return img

def generate_burrower():
    # Segmented drill-head with rotating earth-boring cutters and amber subterranean eyes
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Body segments (tapering to rear on left)
    for seg_x, seg_r in [(-60, 22), (-35, 30), (-5, 38)]:
        draw_circle(draw, (cx + seg_x, cy), seg_r, fill=(120, 53, 15), outline=(15, 20, 28), width=3)
        
    # Armored conical drill head (pointing right)
    drill = [(cx-5, cy-45), (cx+75, 0), (cx-5, cy+45)]
    draw_polygon(draw, drill, fill=(180, 83, 9), width=3)
    
    # Rotating cutter spiral grooves
    draw.line([(cx+10, cy-32), (cx+45, 0), (cx+10, cy+32)], fill=(245, 158, 11), width=4)
    draw.line([(cx+30, cy-20), (cx+60, 0), (cx+30, cy+20)], fill=(254, 240, 138), width=3)
    
    # Twin subterranean optic sensors
    draw_circle(draw, (cx+15, cy-16), 6, fill=(250, 204, 21), outline=(15, 20, 28), width=1)
    draw_circle(draw, (cx+15, cy+16), 6, fill=(250, 204, 21), outline=(15, 20, 28), width=1)
    return img

def generate_parasite():
    # Tiny multi-legged leech chassis with gripping mandibles and venom siphon
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # 6 Spider/leech barbed legs
    for angle in [-45, -90, -135, 45, 90, 135]:
        rad = math.radians(angle)
        lx = cx + math.cos(rad) * 65
        ly = cy + math.sin(rad) * 65
        draw.line([(cx, cy), (lx, ly)], fill=(88, 28, 135), width=4)
        
    # Forward venom mandibles
    draw_polygon(draw, [(cx+10, cy-22), (cx+60, cy-10), (cx+25, cy-5)], fill=(168, 85, 247), width=2)
    draw_polygon(draw, [(cx+10, cy+22), (cx+60, cy+10), (cx+25, cy+5)], fill=(168, 85, 247), width=2)
    
    # Bio-pod body
    draw_circle(draw, (cx-10, cy), 35, fill=(59, 7, 100), outline=(15, 20, 28), width=3)
    draw_glow_circle(img, (cx-5, cy), 20, (168, 85, 247), 180)
    draw_circle(draw, (cx-5, cy), 12, fill=(216, 180, 254), outline=(147, 51, 234), width=2)
    return img

def generate_apex():
    # The Colossal Boss: multi-tiered dreadnought mech with layered armor plates and blood-red command core
    img = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = CENTER
    
    # Massive dreadnought command aura
    draw_glow_circle(img, (cx, cy), 80, (185, 28, 28), 170)
    
    # 4 Orbiting heavy weapon sponsons
    for ox, oy in [(-50, -85), (35, -85), (-50, 85), (35, 85)]:
        draw_polygon(draw, [(cx+ox-25, cy+oy-18), (cx+ox+25, cy+oy-18), (cx+ox+35, cy+oy+18), (cx+ox-15, cy+oy+18)], fill=(39, 39, 42), width=3)
        draw_circle(draw, (cx+ox+15, cy+oy), 8, fill=(239, 68, 68), width=2)
        
    # Outer titan armor hull (wide dreadnought silhouette)
    outer_hull = [
        (cx-95, cy-70), (cx-30, cy-80), (cx+45, cy-60), (cx+95, 0),
        (cx+45, cy+60), (cx-30, cy+80), (cx-95, cy+70), (cx-75, 0)
    ]
    draw_polygon(draw, outer_hull, fill=(9, 9, 11), width=5)
    
    # Mid-layer reinforced blast plates
    mid_hull = [
        (cx-65, cy-50), (cx+25, cy-45), (cx+70, 0),
        (cx+25, cy+45), (cx-65, cy+50), (cx-50, 0)
    ]
    draw_polygon(draw, mid_hull, fill=(39, 39, 42), width=3)
    
    # Inner blood-red command reactor
    draw_glow_circle(img, (cx, cy), 35, (220, 38, 38), 240)
    draw_circle(draw, (cx, cy), 24, fill=(185, 28, 28), outline=(254, 202, 202), width=3)
    draw_circle(draw, (cx, cy), 12, fill=(255, 255, 255), outline=None)
    
    # Forward heavy prow ram
    draw_polygon(draw, [(cx+50, cy-25), (cx+98, 0), (cx+50, cy+25)], fill=(113, 113, 122), width=3)
    return img

# ── RUNNER ───────────────────────────────────────────────────────────────────

def main():
    os.makedirs(OUTPUT_HEROES, exist_ok=True)
    os.makedirs(OUTPUT_ENEMIES, exist_ok=True)
    
    heroes = {
        "atlas": generate_atlas,
        "zephyr": generate_zephyr,
        "synapse": generate_synapse,
        "volt": generate_volt,
        "ignis": generate_ignis,
        "toxis": generate_toxis,
        "havoc": generate_havoc
    }
    
    enemies = {
        "drone": generate_drone,
        "brute": generate_brute,
        "sprinter": generate_sprinter,
        "artillery": generate_artillery,
        "shielder": generate_shielder,
        "healer": generate_healer,
        "exploder": generate_exploder,
        "burrower": generate_burrower,
        "parasite": generate_parasite,
        "apex": generate_apex
    }
    
    print("Generating 7 Playable Heroes...")
    for name, gen in heroes.items():
        img = gen()
        path = os.path.join(OUTPUT_HEROES, f"{name}.png")
        img.save(path, "PNG")
        print(f"  ✓ {path} ({img.size[0]}x{img.size[1]})")
        
    print("\nGenerating 10 Enemies...")
    for name, gen in enemies.items():
        img = gen()
        path = os.path.join(OUTPUT_ENEMIES, f"{name}.png")
        img.save(path, "PNG")
        print(f"  ✓ {path} ({img.size[0]}x{img.size[1]})")
        
    print("\nAll 17 character visual assets generated successfully!")

if __name__ == "__main__":
    main()
