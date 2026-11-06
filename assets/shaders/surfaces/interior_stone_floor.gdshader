shader_type spatial;
render_mode diffuse_burley, specular_disabled;
#include "res://assets/shaders/common/surface_noise.gdshaderinc"
#include "res://assets/shaders/common/surface_metrics.gdshaderinc"

// Quarry blocks, flagstones, slate slabs, field stone and fine gravel.
uniform int stone_kind = 0;
varying vec3 world_point;

float stone_hash(vec2 q) {
	return fract(sin(dot(q, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	world_point = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 metre = world_point.xz;
	vec2 size = vec2(0.40, 0.40);
	vec3 base = vec3(0.47, 0.47, 0.42);
	if (stone_kind == 1) { size = vec2(0.48, 0.36); base = vec3(0.49, 0.48, 0.42); }
	if (stone_kind == 2) { size = vec2(0.62, 0.31); base = vec3(0.34, 0.37, 0.38); }
	if (stone_kind == 3) { size = vec2(0.44, 0.37); base = vec3(0.46, 0.45, 0.41); }
	if (stone_kind == 4) { size = vec2(0.08, 0.07); base = vec3(0.43, 0.42, 0.37); }
	vec2 q = metre / size;
	float row = floor(q.y);
	if (stone_kind == 1 || stone_kind == 2 || stone_kind == 3) q.x += mod(row, 2.0) * 0.5;
	vec2 cell = floor(q);
	vec2 local = fract(q);
	vec2 edge = min(local, 1.0 - local) * size;
	float irregular = (stone_hash(cell + vec2(3.0, 7.0)) - 0.5) * 0.010;
	float joint_width = stone_kind == 4 ? 0.001 : 0.006;
	float pattern_visibility = surface_detail_visibility(world_point, min(size.x, size.y));
	float joint = surface_joint(min(edge.x, edge.y) + irregular * (stone_kind == 3 ? 1.0 : 0.0), joint_width) * pattern_visibility;
	float variation = (stone_hash(cell) - 0.5) * 0.16 * pattern_visibility;
	vec3 detail_position = world_point / surface_view_scale(world_point);
	float detail = filtered_material_noise(detail_position / SURFACE_DETAIL_METERS) - 0.5;
	float micro = (filtered_material_noise(detail_position / SURFACE_MICRO_METERS) - 0.5) * surface_detail_visibility(world_point, SURFACE_MICRO_METERS);
	float coarse = (filtered_material_noise(world_point / SURFACE_COARSE_METERS) - 0.5) * 0.025;
	vec3 color = base * (1.0 + variation + coarse + detail * 0.05 + micro * 0.02);
	ALBEDO = mix(color, color * (stone_kind == 4 ? 0.80 : 0.56), joint);
	ROUGHNESS = 0.91;
	SPECULAR = 0.0;
}
