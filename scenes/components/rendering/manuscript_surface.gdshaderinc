// Explicit linear RGB material coverage, independent of the color artwork.
uniform sampler2D front_material_mask : filter_linear, repeat_disable;
uniform sampler2D back_material_mask : filter_linear, repeat_disable;
uniform bool has_material_masks = false;

vec3 manuscript_coverage(vec2 uv, bool back) {
    if (!has_material_masks) return vec3(0.0);
    return clamp(back ? texture(back_material_mask, uv).rgb : texture(front_material_mask, uv).rgb, vec3(0.0), vec3(1.0));
}

vec3 manuscript_normal(vec2 uv, bool back, vec3 fiber, vec3 coverage) {
    vec2 step_uv = vec2(1.0 / 580.0, 1.0 / 820.0);
    vec3 left = manuscript_coverage(uv - vec2(step_uv.x, 0.0), back);
    vec3 right = manuscript_coverage(uv + vec2(step_uv.x, 0.0), back);
    vec3 top = manuscript_coverage(uv - vec2(0.0, step_uv.y), back);
    vec3 bottom = manuscript_coverage(uv + vec2(0.0, step_uv.y), back);
    // Gold raised, ink pressed very slightly into paper, paint has its own grain.
    vec3 relief = vec3(0.13, -0.025, 0.018);
    vec2 slope = vec2(dot(right - left, relief), dot(bottom - top, relief));
    float hammer = sin(uv.x * 1457.0 + sin(uv.y * 913.0)) * sin(uv.y * 1633.0);
    vec3 gold_normal = normalize(vec3(vec2(hammer, sin(uv.y * 1277.0)) * 0.035, 1.0));
    vec3 pigment_normal = normalize(vec3(fiber.xy * 0.45, 1.0));
    vec3 result = mix(fiber, pigment_normal, coverage.b);
    result = mix(result, normalize(vec3(fiber.xy * 0.7, 1.0)), coverage.g);
    result = mix(result, gold_normal, coverage.r);
    return normalize(result + vec3(-slope, 0.0));
}
