shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
#include "res://assets/shaders/common/surface_metrics.gdshaderinc"

uniform int wood_tone = 0;
varying vec3 world_point;

float plank_hash(vec2 q) {
	return fract(sin(dot(q, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	world_point = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	// Existing E planks are 14 cm wide and 1.25 m long.
	vec2 q = world_point.xz / vec2(1.25, 0.14);
	float row = floor(q.y);
	q.x += mod(row, 3.0) * 0.344;
	vec2 cell = floor(q);
	vec2 edge = min(fract(q), 1.0 - fract(q)) * vec2(1.25, 0.14);
	float joint = surface_joint(min(edge.x, edge.y), 0.0025);
	float variation = 0.92 + 0.16 * plank_hash(cell);
	float detail_scale = surface_view_scale(world_point);
	float grain = surface_filtered_wave(world_point.z / (SURFACE_DETAIL_METERS * detail_scale) * 6.2831853 + sin(world_point.x * 9.0) * 2.0);
	float micro = surface_filtered_wave(world_point.z / (SURFACE_MICRO_METERS * detail_scale) * 6.2831853 + world_point.x * 3.0);
	vec3 oak = vec3(0.245, 0.145, 0.078);
	// Keep the existing E wood palette until the ambiguous DV honey label is specified.
	vec3 wood = oak * variation * (1.0 + grain * 0.04 + micro * 0.012);
	ALBEDO = mix(wood, vec3(0.07, 0.045, 0.025), joint);
	ROUGHNESS = 0.44 + (plank_hash(cell) - 0.5) * 0.08;
}
