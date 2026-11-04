shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx, depth_draw_opaque;

// Material categories and physical dimensions are supplied by DV surfaces.
// No joints are drawn unless the content explicitly declares a module size.
uniform vec4 base_color : source_color = vec4(0.5, 0.5, 0.5, 1.0);
uniform int surface_kind = 0; // stone, plaster, wood, metal
uniform bool floor_surface = false;
uniform float surface_roughness : hint_range(0.0, 1.0) = 0.78;
uniform float texture_scale_m = 1.0;
uniform vec2 module_size_m = vec2(0.0);
uniform float joint_width_m = 0.003;
uniform float detail_scale_m = 0.025;
uniform float normal_strength = 0.35;
uniform float grain_direction = 0.0;

uniform bool room_front_wall_hidden = false;
uniform bool occlusion_enabled = false;
uniform bool occlusion_alpha = false;
uniform bool overhead_cutaway = false;
uniform float cutaway_height = 1000.0;
uniform float occlusion_strength : hint_range(0.0, 1.0) = 0.0;
uniform vec3 occlusion_focus = vec3(0.0);
uniform vec3 occlusion_camera = vec3(0.0);
uniform vec3 occlusion_right = vec3(1.0, 0.0, 0.0);
uniform vec3 occlusion_up = vec3(0.0, 1.0, 0.0);
uniform vec3 occlusion_toward_camera = vec3(0.0, 0.0, 1.0);
uniform float occlusion_radius = 2.2;

varying vec3 world_position;
varying vec3 material_position;
varying vec3 material_normal;

float hash2(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}
float noise2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), u.x),
		mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0)), u.x), u.y);
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// Preserve metre scale while the texture follows moving door leaves.
	material_position = VERTEX * vec3(length(MODEL_MATRIX[0].xyz), length(MODEL_MATRIX[1].xyz), length(MODEL_MATRIX[2].xyz));
	material_normal = NORMAL;
}

void fragment() {
	if (room_front_wall_hidden && !floor_surface) discard;
	if (overhead_cutaway && !floor_surface && world_position.y > cutaway_height) discard;
	vec3 n = abs(material_normal);
	vec2 uv = n.y > max(n.x, n.z) ? material_position.xz : (n.x > n.z ? material_position.zy : material_position.xy);
	float detail = max(detail_scale_m * max(texture_scale_m, 0.05), 0.002);
	float broad = noise2(uv / 0.48);
	float fine = noise2(uv / detail);
	float micro = noise2(uv / (detail * 0.24));
	// Filter subpixel relief instead of turning it into sparkling grain.
	float detail_visibility = 1.0 - smoothstep(0.35, 1.4, length(fwidth(uv / detail)));
	float variation = (broad - 0.5) * 0.035 + (fine - 0.5) * 0.025 * detail_visibility;
	float relief = ((fine - 0.5) * 0.0006 + (micro - 0.5) * 0.00015) * detail_visibility;
	float roughness_variation = (fine - 0.5) * 0.07 * detail_visibility;
	float metallic = 0.0;
	if (surface_kind == 1) {
		// Lime/plaster has broad hand-worked variation and fine pores, no grid.
		variation = (broad - 0.5) * 0.025 + (fine - 0.5) * 0.012 * detail_visibility;
		relief *= 0.75;
		roughness_variation *= 0.5;
	} else if (surface_kind == 2) {
		float angle = radians(grain_direction);
		vec2 wood_uv = mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle))) * uv;
		float warp = noise2(wood_uv * vec2(1.8, 0.32));
		float grain_spacing = max(detail * 0.15, 0.002);
		float bands = sin((wood_uv.x + (warp - 0.5) * 0.035) * 6.283185 / grain_spacing);
		float grain_visibility = 1.0 - smoothstep(0.3, 1.8, length(fwidth(wood_uv * vec2(1.0 / grain_spacing, 1.0))));
		float grain = bands * grain_visibility;
		variation = (noise2(wood_uv * vec2(7.0, 0.5)) - 0.5) * 0.08 + grain * 0.018;
		relief = grain * 0.00008;
		roughness_variation = grain * 0.025;
	} else if (surface_kind == 3) {
		metallic = 0.85;
		variation *= 0.25;
		relief *= 0.2;
	}
	float joint = 0.0;
	float unit_variation = 0.0;
	if (module_size_m.x > 0.01 && module_size_m.y > 0.01) {
		vec2 cell = floor(uv / module_size_m);
		vec2 edge = min(fract(uv / module_size_m), 1.0 - fract(uv / module_size_m)) * module_size_m;
		float gap = min(edge.x, edge.y);
		float aa = max(fwidth(gap), 0.0005);
		joint = 1.0 - smoothstep(joint_width_m * 0.5 - aa, joint_width_m * 0.5 + aa, gap);
		unit_variation = (hash2(cell) - 0.5) * 0.04;
		relief -= joint * 0.0007;
	}
	ALBEDO = base_color.rgb * (1.0 + variation + unit_variation) * (1.0 - joint * 0.12);
	ROUGHNESS = clamp(surface_roughness + roughness_variation + joint * 0.06, 0.12, 0.98);
	METALLIC = metallic;
	SPECULAR = surface_kind == 2 ? 0.32 : 0.38;
	// Derivative bump does not require authored UV tangents on structural meshes.
	vec3 dpdx = dFdx(VERTEX);
	vec3 dpdy = dFdy(VERTEX);
	vec3 r1 = cross(dpdy, NORMAL);
	vec3 r2 = cross(NORMAL, dpdx);
	float bump_det = dot(dpdx, r1);
	vec3 gradient = (dFdx(relief) * r1 + dFdy(relief) * r2) * sign(bump_det) / max(abs(bump_det), 0.00001);
	NORMAL = normalize(NORMAL - gradient * normal_strength);

	float opacity = 1.0;
	if (occlusion_enabled && !floor_surface) {
		vec3 segment = occlusion_focus - occlusion_camera;
		float t = clamp(dot(world_position - occlusion_camera, segment) / max(dot(segment, segment), 0.0001), 0.0, 1.0);
		float distance_to_ray = length(world_position - (occlusion_camera + t * segment));
		if (t > 0.05 && t < 0.95 && distance_to_ray < occlusion_radius)
			opacity = mix(1.0, smoothstep(occlusion_radius * 0.3, occlusion_radius, distance_to_ray), occlusion_strength);
	}
	// Ordered 4x4 coverage is stable in screen space and represents intermediate opacity.
	vec2 pixel = mod(floor(FRAGCOORD.xy), 4.0);
	float threshold = (mod(pixel.x, 2.0) * 8.0 + mod(pixel.y, 2.0) * 4.0 +
		mod(floor(pixel.x / 2.0) + floor(pixel.y / 2.0), 2.0) * 2.0 + floor(pixel.y / 2.0) + 0.5) / 16.0;
	if (occlusion_alpha && opacity < 0.999) discard; // BASE_ALPHA_CUT
	if (!occlusion_alpha && opacity < threshold) discard;
	// ALPHA_VARIANT
}
