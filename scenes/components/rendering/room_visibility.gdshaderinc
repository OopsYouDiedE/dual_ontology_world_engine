uniform int manuscript_room_count = 0;
uniform vec4 manuscript_rooms[32];
uniform int manuscript_shell_count = 0;
uniform vec4 manuscript_shells[32];
uniform bool manuscript_static_shell = false;
uniform int manuscript_slice_count = 0;
uniform vec4 manuscript_slices[16];
uniform vec2 manuscript_axes[16];
uniform vec2 manuscript_slice_heights[16];
// One context-owned texel is shared by this map's surfaces. Isolated execution
// viewports retain their own texture; no global observer state is shared.
uniform sampler2D manuscript_eye_data : filter_nearest, repeat_disable;
#define manuscript_eye texelFetch(manuscript_eye_data, ivec2(0), 0).xyz
uniform float manuscript_floor = 0.0;
uniform bool manuscript_wall = false;
uniform vec2 manuscript_wall_axis = vec2(0.0);
uniform bool manuscript_structure = false;
uniform bool manuscript_doorway = false;
uniform vec3 manuscript_doorway_anchor = vec3(0.0);
uniform float manuscript_wall_margin = 0.34;
uniform float manuscript_cutaway_height = 1.15;
uniform bool manuscript_ceiling = false;
uniform bool manuscript_deduction = false;
uniform bool manuscript_cap = false;
uniform bool manuscript_boundary_cap = false;
uniform vec3 manuscript_section_normal = vec3(0.0);
uniform bool manuscript_slice_floor = false;
uniform vec3 manuscript_object_min = vec3(0.0);
uniform vec3 manuscript_object_max = vec3(0.0);
uniform float manuscript_storey_base = -1000000.0;

float manuscript_aperture(vec3 point, int i) {
	vec4 aperture = manuscript_slices[i];
	vec2 n = manuscript_axes[i];
	vec2 d = point.xz - aperture.xy;
	float depth = dot(d, n);
	if (depth < 0.0 || depth > aperture.w) return 0.0;
	vec2 tangent = vec2(-n.y, n.x);
	float eye_depth = max(0.001, dot(aperture.xy - manuscript_eye.xz, n));
	float aperture_t = eye_depth / max(0.001, eye_depth + depth);
	float aperture_y = mix(manuscript_eye.y, point.y, aperture_t);
	if (aperture_y < manuscript_slice_heights[i].x - 0.05 || aperture_y > manuscript_slice_heights[i].y + 0.05) return 0.0;
	float ray_offset = dot(aperture.xy - manuscript_eye.xz, tangent) * depth / eye_depth;
	float width = min(aperture.z * 1.6, aperture.z * (1.0 + depth / eye_depth));
	float lateral = abs(dot(d, tangent) - ray_offset);
	return (1.0 - smoothstep(0.0, aperture.w, depth)) * (1.0 - smoothstep(width * 0.65, width, lateral));
}

float manuscript_known(vec3 point) {
	float margin = manuscript_structure ? manuscript_wall_margin : 0.02;
	// Wall thickness belongs across the wall, never beyond both of its ends.
	// Expanding both axes exposes sliced neighbouring walls as floating shelves.
	vec2 margins = vec2(margin);
	if (manuscript_wall) margins = abs(manuscript_wall_axis.x) > 0.7 ? vec2(0.0, margin) : vec2(margin, 0.0);
	// Section vertices lie exactly on the boundary; allow only numerical noise.
	if (manuscript_boundary_cap) margins += vec2(0.0001);
	float visibility = 0.0;
	int region_count = manuscript_static_shell ? manuscript_shell_count : manuscript_room_count;
	if (manuscript_boundary_cap) {
		bool inside_a = false;
		bool inside_b = false;
		vec2 a = (point - manuscript_section_normal * 0.002).xz;
		vec2 b = (point + manuscript_section_normal * 0.002).xz;
		for (int i = 0; i < region_count; i++) {
			vec4 r = manuscript_static_shell ? manuscript_shells[i] : manuscript_rooms[i];
			inside_a = inside_a || (a.x >= r.x-margins.x && a.y >= r.y-margins.y && a.x <= r.z+margins.x && a.y <= r.w+margins.y);
			inside_b = inside_b || (b.x >= r.x-margins.x && b.y >= r.y-margins.y && b.x <= r.z+margins.x && b.y <= r.w+margins.y);
		}
		// A semantic split inside one continuous shell is not an exposed cut.
		if (inside_a && inside_b) return 0.0;
	}
	for (int i = 0; i < region_count; i++) {
		vec4 r = manuscript_static_shell ? manuscript_shells[i] : manuscript_rooms[i];
		if (manuscript_wall) {
			float overlap_x = min(manuscript_object_max.x,r.z)-max(manuscript_object_min.x,r.x);
			float overlap_z = min(manuscript_object_max.z,r.w)-max(manuscript_object_min.z,r.y);
			// Thickness expansion may retain this room's boundary wall, but
			// must never admit a neighbouring wall that stops before the room.
			if (!((overlap_x > 0.001 && overlap_z >= -0.002) || (overlap_z > 0.001 && overlap_x >= -0.002))) continue;
		}
		if (manuscript_doorway && manuscript_doorway_anchor.x >= r.x-margin && manuscript_doorway_anchor.z >= r.y-margin && manuscript_doorway_anchor.x <= r.z+margin && manuscript_doorway_anchor.z <= r.w+margin) return 1.0;
		if (point.x >= r.x-margins.x && point.z >= r.y-margins.y && point.x <= r.z+margins.x && point.z <= r.w+margins.y) return 1.0;
	}
	// Unknown walls and frames remain whole and hidden. Cutting their boxes
	// with a view cone exposes floating internal faces, not a readable doorway.
	if (manuscript_wall || manuscript_structure || manuscript_ceiling) return 0.0;
	for (int i = 0; i < manuscript_slice_count; i++) {
		float fade = manuscript_aperture(point,i);
		if (!manuscript_slice_floor) {
			// All eight corners must fit before showing any part of a solid.
			fade = 1.0;
			for (int corner = 0; corner < 8; corner++) {
				vec3 p = vec3((corner & 1) == 0 ? manuscript_object_min.x : manuscript_object_max.x,
					(corner & 2) == 0 ? manuscript_object_min.y : manuscript_object_max.y,
					(corner & 4) == 0 ? manuscript_object_min.z : manuscript_object_max.z);
				fade = min(fade,manuscript_aperture(p,i));
			}
		}
		visibility = max(visibility,fade);
	}
	return visibility;
}

bool manuscript_cut(vec3 point) {
	if (manuscript_ceiling) return true;
	if ((!manuscript_wall && !manuscript_doorway) || point.y <= manuscript_floor + manuscript_cutaway_height) return false;
	// A rotating leaf and its frame belong to the original wall plane.
	// Clipping by the leaf's current point leaves a floating door in cut walls.
	vec3 boundary = manuscript_doorway ? manuscript_doorway_anchor : point;
	int region_count = manuscript_static_shell ? manuscript_shell_count : manuscript_room_count;
	for (int i = 0; i < region_count; i++) {
		vec4 r = manuscript_static_shell ? manuscript_shells[i] : manuscript_rooms[i];
		if (abs(manuscript_wall_axis.x) > 0.7 && boundary.x > r.x - manuscript_wall_margin && boundary.x < r.z + manuscript_wall_margin && abs(boundary.z - r.w) < manuscript_wall_margin) return true;
		if (abs(manuscript_wall_axis.y) > 0.7 && boundary.z > r.y - manuscript_wall_margin && boundary.z < r.w + manuscript_wall_margin && abs(boundary.x - r.z) < manuscript_wall_margin) return true;
	}
	return false;
}
