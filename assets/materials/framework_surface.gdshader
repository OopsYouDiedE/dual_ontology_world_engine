shader_type spatial;
render_mode diffuse_burley, depth_draw_opaque;
uniform vec4 base_color : source_color = vec4(0.5,0.5,0.5,1.0);
uniform bool floor_surface = false;
uniform float surface_roughness : hint_range(0.0,1.0) = 0.87;
uniform float texture_scale_m = 1.0;

// Camera occlusion uniforms (Task 4.07, 4.08)
uniform bool occlusion_enabled = false;
uniform float occlusion_strength : hint_range(0.0, 1.0) = 0.0;
uniform vec3 occlusion_focus = vec3(0.0);
uniform vec3 occlusion_camera = vec3(0.0);
uniform vec3 occlusion_right = vec3(1.0, 0.0, 0.0);
uniform vec3 occlusion_up = vec3(0.0, 1.0, 0.0);
uniform vec3 occlusion_toward_camera = vec3(0.0, 0.0, 1.0);
uniform float occlusion_radius = 2.2;

varying vec3 world_position;
varying vec3 world_normal;
void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz;
	world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
	vec2 uv = abs(world_normal.y)>0.6 ? world_position.xz : (abs(world_normal.x)>0.6 ? world_position.zy : world_position.xy);
	vec2 cell = floor_surface ? vec2(0.6) : vec2(1.2,0.6);
	cell *= max(texture_scale_m,0.05);
	if (!floor_surface) uv.x += mod(floor(uv.y/cell.y),2.0)*0.6;
	vec2 p = abs(fract(uv/cell)-0.5)*cell;
	vec2 edge = cell*0.5-p;
	float gap = min(edge.x,edge.y);
	float aa = max(fwidth(gap),0.001);
	float line = 1.0-smoothstep(0.006-aa,0.006+aa,gap);
	ALBEDO = base_color.rgb * (1.0-line*(floor_surface ? 0.24 : 0.10));
	ROUGHNESS = surface_roughness;

	// Camera occlusion fade: hides foreground walls and ceilings between camera and character
	if (occlusion_enabled && !floor_surface) {
		vec3 seg = occlusion_focus - occlusion_camera;
		float seg_len_sq = max(dot(seg, seg), 0.0001);
		float t = clamp(dot(world_position - occlusion_camera, seg) / seg_len_sq, 0.0, 1.0);
		vec3 closest = occlusion_camera + t * seg;
		float dist = length(world_position - closest);
		if (t > 0.05 && t < 0.95 && dist < occlusion_radius) {
			float fade = smoothstep(occlusion_radius * 0.3, occlusion_radius, dist);
			ALPHA = mix(1.0, fade, occlusion_strength);
		} else {
			ALPHA = 1.0;
		}
	} else {
		ALPHA = 1.0;
	}
}
