shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
uniform vec4 base_color : source_color = vec4(0.6,0.65,0.45,1.0);
uniform int surface_kind = 0;
varying vec3 local_position;
void vertex() { local_position = VERTEX; }
void fragment() {
	vec3 p = local_position;
	float wash = sin(p.x*2.9 + sin(p.z*3.1))*sin(p.y*3.0+p.z*2.1);
	float detail = 0.0;
	if (surface_kind == 1) {
		vec2 tile = p.xz / 0.5;
		vec2 edge = min(fract(tile), 1.0-fract(tile));
		detail = (1.0-smoothstep(0.012,0.03,min(edge.x,edge.y)))*0.20;
	} else if (surface_kind == 2) {
		detail = pow(0.5+0.5*sin(p.y*95.0+sin(p.x*9.0+p.z*8.0)*3.0),8.0)*0.09;
	}
	ALBEDO = base_color.rgb * (1.0+wash*0.055-detail);
	ROUGHNESS = 0.88;
	SPECULAR = 0.18;
}
