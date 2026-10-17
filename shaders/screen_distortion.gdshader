shader_type canvas_item;

// Screen-space refraction rings + karma vignette for ScreenDistortionFX.
//
// This shader runs on a full-viewport ColorRect inside a CanvasLayer that
// draws after the world but before the HUD (see Main.tscn node order). It
// samples the already-rendered frame through hint_screen_texture and bends
// SCREEN_UV outward around up to eight ripple centers, making explosions
// look like they physically warp the air. u_vignette mixes the edges toward
// the karma color as the Collateral Karma threat level rises.
//
// Every ripple is a separate vec4 uniform (x, y = UV center; z = amplitude;
// w = ring radius in UV units) so the C# side never has to marshal uniform
// arrays — 8 SetShaderParameter calls per frame, all cheap.

uniform vec4 u_ripple0 = vec4(0.0);
uniform vec4 u_ripple1 = vec4(0.0);
uniform vec4 u_ripple2 = vec4(0.0);
uniform vec4 u_ripple3 = vec4(0.0);
uniform vec4 u_ripple4 = vec4(0.0);
uniform vec4 u_ripple5 = vec4(0.0);
uniform vec4 u_ripple6 = vec4(0.0);
uniform vec4 u_ripple7 = vec4(0.0);

uniform float u_vignette : hint_range(0.0, 1.0) = 0.0;
uniform vec4 u_vignette_color : source_color = vec4(0.42, 0.03, 0.06, 1.0);
uniform float u_aspect = 1.7778;
uniform float u_ripple_thickness : hint_range(0.01, 0.5) = 0.12;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;

// Returns (warped UV, sheen strength) for one ripple.
vec3 apply_ripple(vec2 uv, vec4 r) {
    if (r.z <= 0.0001) {
        return vec3(uv, 0.0);
    }
    vec2 delta = uv - r.xy;
    delta.x *= u_aspect;
    float dist = length(delta);
    float band = smoothstep(r.w - u_ripple_thickness, r.w, dist)
               * (1.0 - smoothstep(r.w, r.w + u_ripple_thickness, dist));
    vec2 dir = dist > 0.0001 ? delta / dist : vec2(0.0);
    float falloff = 1.0 - smoothstep(0.15, 0.95, r.w);
    vec2 offset = dir * band * r.z * 0.05 * falloff;
    offset.x /= u_aspect;
    return vec3(uv - offset, band * r.z * falloff);
}

void fragment() {
    vec2 uv = SCREEN_UV;
    float sheen = 0.0;
    vec3 p;

    p = apply_ripple(uv, u_ripple0); uv = p.xy; sheen += p.z;
    p = apply_ripple(uv, u_ripple1); uv = p.xy; sheen += p.z;
    p = apply_ripple(uv, u_ripple2); uv = p.xy; sheen += p.z;
    p = apply_ripple(uv, u_ripple3); uv = p.xy; sheen += p.z;
    p = apply_ripple(uv, u_ripple4); uv = p.xy; sheen += p.z;
    p = apply_ripple(uv, u_ripple5); uv = p.xy; sheen += p.z;
    p = apply_ripple(uv, u_ripple6); uv = p.xy; sheen += p.z;
    p = apply_ripple(uv, u_ripple7); uv = p.xy; sheen += p.z;

    vec3 col = textureLod(screen_texture, clamp(uv, vec2(0.0), vec2(1.0)), 0.0).rgb;
    col += sheen * vec3(0.20, 0.18, 0.14);

    float d = length((SCREEN_UV - vec2(0.5)) * vec2(u_aspect, 1.0));
    float vig = smoothstep(0.32, 0.92, d) * u_vignette;
    vig *= 1.0 + 0.12 * sin(TIME * 2.1);
    col = mix(col, u_vignette_color.rgb, clamp(vig, 0.0, 0.9));

    COLOR = vec4(col, 1.0);
}
