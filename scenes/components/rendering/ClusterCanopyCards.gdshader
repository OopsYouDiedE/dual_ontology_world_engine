shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;
uniform vec4 base_color : source_color = vec4(0.24, 0.34, 0.12, 1.0);
uniform vec4 tip_color : source_color = vec4(0.48, 0.55, 0.23, 1.0);
// Each fixed mesh card is one mathematical leaf. No external image dependency.
void fragment() {
 vec2 p = UV * 2.0 - 1.0;
 float half_width = pow(max(0.0, 1.0 - p.y * p.y), 0.72) * (0.80 - 0.16 * p.y);
 float leaf = half_width - abs(p.x);
 ALPHA = step(0.0, leaf);
 ALPHA_SCISSOR_THRESHOLD = 0.5;
 if (!FRONT_FACING) NORMAL = -NORMAL;
 ALBEDO = mix(base_color.rgb, tip_color.rgb, COLOR.r);
 ROUGHNESS = 0.88;
 SPECULAR = 0.18;
}
