shader_type spatial;
render_mode cull_back;
uniform vec4 base_color : source_color = vec4(0.24, 0.34, 0.12, 1.0);
uniform vec4 tip_color : source_color = vec4(0.48, 0.55, 0.23, 1.0);
uniform float vertex_variation : hint_range(0.0,1.0) = 1.0;
// Static first comparison. No time-dependent displacement or fake camera facing.
void fragment() {
 float variation = mix(0.4, COLOR.r, vertex_variation);
 ALBEDO = mix(base_color.rgb, tip_color.rgb, variation);
 ROUGHNESS = 0.86;
 SPECULAR = 0.22;
}
