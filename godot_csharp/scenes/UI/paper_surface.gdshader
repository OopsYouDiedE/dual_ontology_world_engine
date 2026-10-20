shader_type canvas_item;
render_mode unshaded;

void fragment() {
	vec2 p = FRAGCOORD.xy;
	float grain = fract(sin(dot(floor(p), vec2(12.9898, 78.233))) * 43758.5453);
	float edge = pow(abs(UV.x - 0.5) * 2.0, 8.0) + pow(abs(UV.y - 0.5) * 2.0, 8.0);
	COLOR = vec4(vec3(0.918, 0.878, 0.769) * (0.985 + grain * 0.025 - edge * 0.035), 1.0);
}
