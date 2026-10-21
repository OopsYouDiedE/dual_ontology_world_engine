shader_type canvas_item;
render_mode unshaded;

// The current desk is the front of the departing leaf. Transparent pixels
// uncover the already-rendered game below it; no destination image is sampled.
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform sampler2D paper_texture : source_color, filter_linear, repeat_disable;
uniform vec4 paper_tint : source_color = vec4(1.0);
varying vec4 surface_modulate;

void vertex() {
	surface_modulate = COLOR;
}

void fragment() {
	vec4 result = texture(TEXTURE, UV);
	float p = clamp(progress, 0.0, 1.0);
	if (p >= 0.9999) {
		result = vec4(0.0);
	} else if (p > 0.0001) {
		float lift = sin(p * 3.14159265);
		float curl_width = max(0.0001, 0.19 * lift);
		// A modest bow and diagonal lift begin at the outer lower corner.
		float edge = mix(1.055, -0.23, p)
			+ lift * (0.085 * (0.5 - UV.y) + 0.025 * sin(UV.y * 3.14159265));
		float distance = UV.x - edge;
		if (distance > 0.0) {
			float shadow_width = 0.025 + 0.025 * lift;
			float shadow = (1.0 - smoothstep(0.0, shadow_width, distance)) * 0.24 * lift;
			result = vec4(vec3(0.10, 0.075, 0.045), shadow);
		} else if (distance > -curl_width) {
			float bend = clamp((distance + curl_width) / curl_width, 0.0, 1.0);
			float arc = asin(bend) / 1.57079633;
			// Paper grain stays distributed across the visible back, including
			// the final sliver after the virtual fold has moved offscreen.
			vec3 paper = texture(paper_texture, vec2(1.0 - UV.x, UV.y)).rgb;
			vec3 back = paper * paper_tint.rgb;
			float shade = 0.77 + 0.23 * sin(arc * 3.14159265);
			back *= shade;
			float rim = pow(bend, 16.0) * 0.10;
			result = vec4(back + rim, 1.0);
			// A thin contact shade connects the lifted back to the unturned face.
			float root_blend = smoothstep(0.0, 0.11, bend);
			vec4 front = texture(TEXTURE, UV);
			front.rgb *= 1.0 - 0.18 * root_blend;
			result = mix(front, result, root_blend);
		}
	}
	COLOR = result * surface_modulate;
}
