shader_type spatial;
render_mode diffuse_burley, specular_disabled;
#include "res://assets/shaders/common/surface_noise.gdshaderinc"
#include "res://assets/shaders/common/surface_metrics.gdshaderinc"

uniform vec4 stone_color : source_color = vec4(0.54, 0.53, 0.47, 1.0);
varying vec3 world_point;

float hash21(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	world_point = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	// World metre coordinates make slab size stable across differently sized routes.
	vec2 metre = world_point.xz / vec2(0.48, 0.36);
	vec2 row = floor(metre);
	metre.x += mod(row.y, 2.0) * 0.5;
	vec2 cell = floor(metre);
	vec2 edge = min(fract(metre),1.0-fract(metre))*vec2(0.48,0.36);
	float mortar = surface_joint(min(edge.x,edge.y),0.009);
	float variation = hash21(cell) * 0.16 - 0.08;
	vec3 p=world_point/surface_view_scale(world_point);
	float grain=filtered_material_noise(p/SURFACE_DETAIL_METERS)-0.5;
	float micro=(filtered_material_noise(p/SURFACE_MICRO_METERS)-0.5)*surface_detail_visibility(world_point,SURFACE_MICRO_METERS);
	vec3 slab = stone_color.rgb * (0.96 + variation + grain*0.04 + micro*0.02);
	ALBEDO = mix(slab, slab * 0.53, mortar);
	ROUGHNESS = 0.95;
	SPECULAR = 0.0;
}
