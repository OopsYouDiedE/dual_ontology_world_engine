shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
#include "res://rendering/shaders/common/surface_noise.gdshaderinc"
#include "res://rendering/shaders/common/surface_metrics.gdshaderinc"
uniform vec4 pigment : source_color = vec4(0.69,0.66,0.55,1.0);
uniform float surface_roughness = 0.91;
varying vec3 world_point;
void vertex() { world_point = (MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
	vec3 p = world_point / surface_view_scale(world_point);
	float coarse = filtered_material_noise(world_point/SURFACE_COARSE_METERS);
	float fine = filtered_material_noise(p/SURFACE_DETAIL_METERS);
	float micro = (filtered_material_noise(p/SURFACE_MICRO_METERS)-0.5)*surface_detail_visibility(world_point,SURFACE_MICRO_METERS);
	float foot = 1.0-smoothstep(0.15,0.85,world_point.y);
	ALBEDO = pigment.rgb*(0.97+coarse*0.035+fine*0.025+micro*0.02)*mix(1.0,0.88,foot);
	ROUGHNESS = surface_roughness;
}
