shader_type canvas_item;
render_mode unshaded;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform bool reverse = false;

void fragment() {
	vec2 uv = UV;
	if (reverse) uv.x = 1.0 - uv.x;
	float edge = 1.0 - progress * 1.15;
	float curl = 0.13 * sin(progress * 3.14159265);
	float distance_from_edge = uv.x - edge;
	if (distance_from_edge > curl) {
		COLOR = vec4(0.0);
	} else if (distance_from_edge > 0.0) {
		float arc = distance_from_edge / max(curl, 0.001);
		float shade = 0.63 + 0.37 * sqrt(max(0.0, 1.0 - arc * arc));
		COLOR = vec4(vec3(0.918, 0.878, 0.769) * shade, 1.0 - smoothstep(0.94, 1.0, arc));
	} else {
		vec2 sample_uv = UV;
		sample_uv.y += sin(UV.y * 3.14159265) * exp(distance_from_edge * 30.0) * curl * 0.10;
		vec4 page = texture(TEXTURE, sample_uv);
		page.rgb *= 1.0 - exp(distance_from_edge * 38.0) * sin(progress * 3.14159265) * 0.28;
		COLOR = page;
	}
}
