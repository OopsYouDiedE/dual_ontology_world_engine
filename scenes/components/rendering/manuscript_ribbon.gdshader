shader_type spatial;
render_mode cull_disabled;
void fragment() {
    float border = step(0.08, UV.x) * step(UV.x, 0.92);
    vec2 motif = abs(fract(UV * vec2(2.0, 9.0)) - 0.5);
    float diamond = 1.0 - smoothstep(0.17, 0.22, motif.x + motif.y);
    float gold = max(1.0 - border, diamond);
    ALBEDO = mix(vec3(0.13, 0.035, 0.025), vec3(0.61, 0.36, 0.09), gold);
    float warp = sin(UV.x * 450.0) * sin(UV.y * 1600.0);
    NORMAL_MAP = normalize(vec3(warp * 0.13, sin(UV.y * 1600.0) * 0.08, 1.0)) * 0.5 + 0.5;
    ROUGHNESS = mix(0.87, 0.46, gold);
    METALLIC = gold * 0.55;
}
