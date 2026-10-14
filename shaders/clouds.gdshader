// A high cloud layer drawn on a big plane: two scrolling noise layers, coloured by the sun.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, fog_disabled;

uniform sampler2D noise : filter_linear_mipmap, repeat_enable;
uniform float coverage : hint_range(0.0, 1.0) = 0.56;
uniform float softness : hint_range(0.01, 0.6) = 0.28;
uniform vec3 lit_color : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 shade_color : source_color = vec3(0.7, 0.72, 0.78);
uniform float opacity : hint_range(0.0, 1.0) = 0.85;
uniform float speed = 0.0025;
uniform float scale = 3.2;

void fragment() {
    vec2 drift = vec2(TIME * speed, TIME * speed * 0.35);
    vec2 uv = UV * scale + drift;
    float n = texture(noise, uv).r * 0.62 + texture(noise, uv * 2.7 + drift * 1.5 + 0.3).g * 0.38;
    float a = smoothstep(coverage, coverage + softness, n);
    float thick = smoothstep(coverage + softness * 0.5, coverage + softness * 1.8, n);
    vec3 col = mix(lit_color, shade_color, thick * 0.8);
    float edge = length(UV - 0.5) * 2.0;
    ALBEDO = col;
    ALPHA = a * opacity * (1.0 - smoothstep(0.5, 0.95, edge));
}
