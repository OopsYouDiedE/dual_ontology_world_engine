shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, fog_disabled;

uniform sampler2D scene_depth : hint_depth_texture, repeat_disable, filter_nearest;
uniform vec4 ink : source_color = vec4(0.12, 0.09, 0.07, 1.0);
uniform float width_pixels : hint_range(0.5, 2.0) = 1.0;
uniform float strength : hint_range(0.0, 1.0) = 0.38;
uniform float minimum_depth_step = 0.045;
uniform float relative_depth_step = 0.002;

void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float distance_at(vec2 uv, mat4 inverse_projection) {
    vec2 bounded_uv = clamp(uv, vec2(0.0), vec2(1.0));
    float depth = texture(scene_depth, bounded_uv).r;
    #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    vec3 ndc = vec3(bounded_uv, depth) * 2.0 - 1.0;
    #else
    vec3 ndc = vec3(bounded_uv * 2.0 - 1.0, depth);
    #endif
    vec4 view = inverse_projection * vec4(ndc, 1.0);
    return -view.z / view.w;
}

void fragment() {
    vec2 pixel = width_pixels / VIEWPORT_SIZE;
    float center = distance_at(SCREEN_UV, INV_PROJECTION_MATRIX);
    float left = distance_at(SCREEN_UV - vec2(pixel.x, 0.0), INV_PROJECTION_MATRIX);
    float right = distance_at(SCREEN_UV + vec2(pixel.x, 0.0), INV_PROJECTION_MATRIX);
    float up = distance_at(SCREEN_UV - vec2(0.0, pixel.y), INV_PROJECTION_MATRIX);
    float down = distance_at(SCREEN_UV + vec2(0.0, pixel.y), INV_PROJECTION_MATRIX);
    // Opposite-sample differences suppress a flat floor's continuous depth slope.
    float discontinuity = max(abs(left + right - 2.0 * center), abs(up + down - 2.0 * center));
    float threshold = max(minimum_depth_step, center * relative_depth_step);
    float edge = smoothstep(threshold, threshold * 2.0, discontinuity);
    ALBEDO = ink.rgb;
    ALPHA = edge * strength * ink.a;
}
