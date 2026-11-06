shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
uniform vec4 base_color : source_color = vec4(0.56,0.64,0.30,1.0);
void fragment() {
	ALBEDO = base_color.rgb * COLOR.rgb;
	ROUGHNESS = 0.92;
	SPECULAR = 0.12;
	AO = mix(0.74,1.0,COLOR.a);
	AO_LIGHT_AFFECT = 0.25;
	BACKLIGHT = ALBEDO * 0.065;
}
