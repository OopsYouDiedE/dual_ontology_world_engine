shader_type spatial;
render_mode diffuse_lambert, specular_disabled;
uniform vec4 base_color : source_color = vec4(0.6,0.65,0.45,1.0);
uniform int surface_kind = 0;
void fragment() {
	ALBEDO = base_color.rgb * COLOR.rgb;
	ROUGHNESS = 1.0;
}
void light() {
	float ndl = max(dot(NORMAL,LIGHT),0.0);
	float bands = floor(ndl*3.0+0.5)/3.0;
	DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * ATTENUATION * bands / PI;
}
