shader_type canvas_item;
void fragment() {
	vec4 base = COLOR;
	float inside = 1.0 - smoothstep(0.86, 0.99, length((UV - vec2(0.29, 0.60)) / vec2(0.185, 0.26)));
	float glass = smoothstep(0.65, 0.9, dot(base.rgb, vec3(0.299, 0.587, 0.114))) * inside;
	base.a *= 1.0 - glass * 0.88;
	COLOR = base;
}
