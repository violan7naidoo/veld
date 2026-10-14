// Grass tufts: crossed quads with an alpha-cut blade texture, swaying in the wind.
// The instance colour (set per tuft from the terrain colour under it) tints the blades.
shader_type spatial;
render_mode cull_disabled, depth_prepass_alpha, diffuse_burley;

uniform sampler2D blade : source_color, filter_linear_mipmap;
uniform float wind_strength = 0.14;
uniform float wind_speed = 1.3;
uniform float fade_start = 140.0;
uniform float fade_end = 190.0;
uniform vec3 bend_a = vec3(0.0, -1000.0, 0.0);
uniform vec3 bend_b = vec3(0.0, -1000.0, 0.0);

varying float fade;

void vertex() {
    vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float top = 1.0 - UV.y;
    float phase = wp.x * 0.31 + wp.z * 0.23;
    float gust = sin(TIME * wind_speed * 0.37 + wp.x * 0.02) * 0.5 + 0.5;
    float sway = sin(TIME * wind_speed + phase) * wind_strength * (0.4 + gust) * top * top;
    VERTEX.x += sway;
    VERTEX.z += sway * 0.6;
    // bend away from whoever is standing in the grass
    vec3 base = MODEL_MATRIX[3].xyz;
    vec2 push = vec2(0.0);
    vec2 da = base.xz - bend_a.xz;
    float la = length(da);
    if (la < 1.4 && abs(base.y - bend_a.y) < 2.5) push += (da / max(la, 0.05)) * (1.4 - la) * 0.55;
    vec2 db = base.xz - bend_b.xz;
    float lb = length(db);
    if (lb < 1.8 && abs(base.y - bend_b.y) < 2.5) push += (db / max(lb, 0.05)) * (1.8 - lb) * 0.5;
    if (length(push) > 0.001) {
        vec3 wv = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
        wv.xz += push * top;
        wv.y -= length(push) * top * 0.3;
        VERTEX = (inverse(MODEL_MATRIX) * vec4(wv, 1.0)).xyz;
    }
    // light the tufts as if they were the ground, so they do not go black when side-on to the sun
    NORMAL = vec3(0.0, 1.0, 0.0);
    float dist = length((VIEW_MATRIX * vec4(wp, 1.0)).xyz);
    fade = 1.0 - smoothstep(fade_start, fade_end, dist);
}

void fragment() {
    vec4 c = texture(blade, UV);
    ALBEDO = c.rgb * COLOR.rgb * 1.15;
    ALPHA = c.a * fade;
    ALPHA_SCISSOR_THRESHOLD = 0.5;
    ROUGHNESS = 0.9;
    SPECULAR = 0.1;
}
