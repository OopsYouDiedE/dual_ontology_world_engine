shader_type spatial;
render_mode diffuse_burley;

uniform sampler2D paper_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D paper_normal_texture : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D paper_roughness_texture : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform bool has_paper_normal = false;
uniform bool has_paper_roughness = false;
uniform float paper_normal_strength : hint_range(0.0, 1.0) = 0.08;
uniform vec4 edge_tint : source_color = vec4(0.68, 0.63, 0.54, 1.0);
uniform float sheet_density : hint_range(120.0, 600.0) = 380.0;
uniform float relief_strength : hint_range(0.0, 0.08) = 0.025;

varying vec3 local_point;
varying vec3 local_normal;
varying vec3 view_up;

float paper_hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float paper_noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(paper_hash(cell), paper_hash(cell + vec2(1.0, 0.0)), f.x),
		mix(paper_hash(cell + vec2(0.0, 1.0)), paper_hash(cell + vec2(1.0)), f.x),
		f.y);
}

// Integral of a periodic narrow separator. Pixel averaging keeps distant
// sheets soft instead of turning them into black lines or shimmering moire.
float stripe_integral(float phase, float width) {
	return floor(phase) * width + min(fract(phase), width);
}

float filtered_separator(float phase, float width, float footprint) {
	float p = phase + width * 0.5;
	return clamp((stripe_integral(p + footprint * 0.5, width)
		- stripe_integral(p - footprint * 0.5, width)) / footprint, 0.0, 1.0);
}

void vertex() {
	local_point = VERTEX;
	local_normal = NORMAL;
	view_up = normalize((MODELVIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
}

void fragment() {
	// Preserve the existing BoxMesh UV / paper texture contract.
	vec3 paper = texture(paper_texture, UV * vec2(1.5, 0.4)).rgb;
	float side = 1.0 - smoothstep(0.70, 0.99, abs(normalize(local_normal).y));
	float along = local_point.x + local_point.z * 0.731;
	float waviness = (paper_noise(local_point.xz * 1.45) - 0.5) * 0.15
		+ (paper_noise(local_point.xz * vec2(7.0, 3.0)) - 0.5) * 0.045;
	// The monotonic warp varies sheet spacing without crossing sheet layers.
	float phase = local_point.y * sheet_density
		+ sin(local_point.y * 79.0) * 0.19
		+ sin(local_point.y * 173.0 + 1.7) * 0.12 + waviness;
	float footprint = max(fwidth(phase), 0.002);
	float separator = filtered_separator(phase, 0.08, footprint) * 0.45
		+ filtered_separator(phase, 0.18, footprint) * 0.55;
	// Broad, low-contrast compression groups remain visible when individual
	// sheets are smaller than a pixel. They are paper variation, not dark rules.
	float bundle = paper_noise(vec2(along * 0.65, local_point.y * 51.0 + 13.0));
	float wear = paper_noise(local_point.xz * 0.75 + vec2(local_point.y * 3.0));
	vec2 fiber_coords = vec2(along * 37.0, local_point.y * 270.0 + 7.0);
	float fiber_filter = 1.0 - smoothstep(0.35, 1.4,
		max(fwidth(fiber_coords.x), fwidth(fiber_coords.y)));
	float fiber = (paper_noise(fiber_coords) - 0.5) * fiber_filter;
	vec3 aged_paper = mix(paper, edge_tint.rgb, 0.56);
	aged_paper *= mix(vec3(0.95, 0.935, 0.90), vec3(1.015), wear);
	float variation = 1.0 + (bundle - 0.5) * 0.075 + fiber * 0.045;
	ALBEDO = aged_paper * variation * (1.0 - separator * side * 0.14);

	// Microscopic relief changes the real lighting; the mesh silhouette,
	// page height, UI plane and book contact remain exactly where they were.
	float resolved = 1.0 - smoothstep(0.18, 0.8, footprint);
	float relief = sin(phase * 6.2831853) * resolved * relief_strength;
	vec3 normal = normalize(NORMAL);
	if (has_paper_normal) {
		vec2 xy = (texture(paper_normal_texture, UV * vec2(1.5, 0.4)).rg * 2.0 - 1.0) * paper_normal_strength;
		vec3 detail = normalize(vec3(xy, sqrt(max(1.0 - dot(xy, xy), 0.001))));
		normal = normalize(normalize(TANGENT) * detail.x + normalize(BINORMAL) * detail.y + normal * detail.z);
	}
	vec3 up_tangent = view_up - normal * dot(view_up, normal);
	NORMAL = normalize(normal + up_tangent * (relief + fiber * 0.006) * side);
	float mapped_roughness = has_paper_roughness ? texture(paper_roughness_texture, UV * vec2(1.5, 0.4)).r : 0.965;
	ROUGHNESS = clamp(mapped_roughness + fiber * 0.015, 0.04, 1.0);
	METALLIC = 0.0;
	SPECULAR = 0.08;
}
