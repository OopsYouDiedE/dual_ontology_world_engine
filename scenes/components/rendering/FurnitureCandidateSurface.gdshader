shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
uniform vec4 pigment : source_color = vec4(0.5,0.35,0.22,1.0);
uniform float roughness_value = 0.78;
uniform float metallic_value = 0.0;
uniform int surface_kind = 0; // 0 plain, 1 wood, 2 cloth
uniform int grain_axis = 1;
uniform float detail_strength = 1.0;
varying vec3 local_position;
void vertex() { local_position = VERTEX; }
void fragment() {
	vec3 p = local_position;
	float along = grain_axis == 0 ? p.x : (grain_axis == 2 ? p.z : p.y);
	float across = grain_axis == 0 ? p.z : p.x;
	float grain = sin(across*72.0+sin(along*2.0+across*9.0)*0.9);
	float wash = sin(across*10.0+along*0.8)*0.012;
	float variation = surface_kind == 1 ? grain*0.018+wash : 0.0;
	// Fine cloth weave is filtered by screen derivatives to avoid distant shimmer.
	vec2 cloth_uv = p.xz*95.0;
	float cloth_filter = 1.0-smoothstep(0.12,0.5,max(length(dFdx(cloth_uv)),length(dFdy(cloth_uv))));
	if (surface_kind == 2) variation = sin(cloth_uv.x)*sin(cloth_uv.y)*0.012*cloth_filter;
	ALBEDO = pigment.rgb*(1.0+variation*detail_strength);
	ROUGHNESS = clamp(roughness_value+variation*0.4,0.1,1.0);
	METALLIC = metallic_value;
	SPECULAR = 0.25;
}
