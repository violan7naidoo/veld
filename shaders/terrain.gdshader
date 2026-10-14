// Terrain: the mesh's vertex colour gives the base tint (veld, road, rail, town, rock), this shader adds
// tiling detail (soil or rock by slope), a normal map, and large-scale variation so the tiling is not visible.
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D soil_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D soil_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D macro_noise : filter_linear_mipmap, repeat_enable;

uniform float near_tile = 0.45;   // texture repeats per metre, close up
uniform float far_tile = 0.035;   // second layer at a much larger scale
uniform float rock_slope_start = 0.16;
uniform float rock_slope_end = 0.38;
uniform float detail_strength = 1.0;
uniform float wetness : hint_range(0.0, 1.0) = 0.0;
// aerial photo draped over the ground: rect = x0, z0, x1, z1 in metres; mix 0 = off
uniform sampler2D ortho : source_color, filter_linear_mipmap_anisotropic;
uniform vec4 ortho_rect = vec4(0.0, 0.0, 0.0, 0.0);
uniform float ortho_mix : hint_range(0.0, 1.0) = 0.0;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

vec3 triplanar(sampler2D tex, vec3 p, vec3 n, float scale) {
    vec3 w = pow(abs(n), vec3(4.0));
    w /= (w.x + w.y + w.z);
    vec3 x = texture(tex, p.zy * scale).rgb;
    vec3 y = texture(tex, p.xz * scale).rgb;
    vec3 z = texture(tex, p.xy * scale).rgb;
    return x * w.x + y * w.y + z * w.z;
}

void fragment() {
    vec2 uv = world_pos.xz;
    float macro = texture(macro_noise, uv * 0.0025).r;
    float macro2 = texture(macro_noise, uv * 0.018 + vec2(0.37, 0.11)).r;
    float slope = 1.0 - clamp(world_normal.y, 0.0, 1.0);
    float rock_w = smoothstep(rock_slope_start, rock_slope_end, slope + (macro2 - 0.5) * 0.08);

    // two scales of detail, both roughly mid-grey, so they multiply the tint without darkening it
    vec3 soil = texture(soil_albedo, uv * near_tile).rgb * texture(soil_albedo, uv * far_tile).rgb * 4.0;
    vec3 rock = triplanar(rock_albedo, world_pos, world_normal, near_tile * 0.6) * triplanar(rock_albedo, world_pos, world_normal, far_tile) * 4.0;
    vec3 detail = mix(soil, rock, rock_w);
    detail = mix(vec3(1.0), detail, detail_strength);

    vec3 tint = COLOR.rgb;
    tint *= mix(0.82, 1.12, macro) * mix(0.93, 1.05, macro2);
    // a little dry-grass yellow in the low patches, redder soil where it is bare
    tint = mix(tint, tint * vec3(1.05, 1.02, 0.85), smoothstep(0.55, 0.8, macro) * (1.0 - rock_w) * 0.6);

    ALBEDO = tint * detail * mix(1.0, 0.7, wetness);
    if (ortho_mix > 0.001 && world_pos.x > ortho_rect.x && world_pos.z > ortho_rect.y && world_pos.x < ortho_rect.z && world_pos.z < ortho_rect.w) {
        vec2 ouv = (world_pos.xz - ortho_rect.xy) / (ortho_rect.zw - ortho_rect.xy);
        vec3 oc = texture(ortho, ouv).rgb * mix(vec3(1.0), detail, 0.35) * mix(1.0, 0.7, wetness);
        ALBEDO = mix(ALBEDO, oc, ortho_mix);
    }

    vec3 nm_soil = texture(soil_normal, uv * near_tile).rgb;
    vec3 nm_rock = triplanar(rock_normal, world_pos, world_normal, near_tile * 0.6);
    NORMAL_MAP = mix(nm_soil, nm_rock, rock_w);
    NORMAL_MAP_DEPTH = mix(0.5, 1.2, rock_w);
    ROUGHNESS = mix(mix(0.95, 0.8, rock_w), 0.25, wetness);
    SPECULAR = mix(0.12, 0.5, wetness);
}
