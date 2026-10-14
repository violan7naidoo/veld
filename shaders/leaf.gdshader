// Leafy canopies and shrubs: a sphere with holes cut by an alpha texture, gently moving.
shader_type spatial;
render_mode cull_disabled, depth_prepass_alpha, diffuse_burley;

uniform sampler2D leaf : source_color, filter_linear_mipmap, repeat_enable;
uniform float sway = 0.05;
uniform float tile = 2.5;
uniform float cutoff = 0.42;

void vertex() {
    vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float t = TIME * 0.9 + wp.x * 0.15 + wp.z * 0.11;
    VERTEX += vec3(sin(t), sin(t * 1.3 + 1.0) * 0.4, cos(t * 0.8)) * sway;
}

void fragment() {
    vec4 c = texture(leaf, UV * tile);
    ALBEDO = c.rgb * COLOR.rgb;
    ALPHA = c.a;
    ALPHA_SCISSOR_THRESHOLD = cutoff;
    ROUGHNESS = 0.85;
    SPECULAR = 0.15;
    // soften the lighting of thin foliage: part of the light goes through
    BACKLIGHT = vec3(0.25, 0.3, 0.12);
}
