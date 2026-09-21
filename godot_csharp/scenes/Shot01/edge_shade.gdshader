shader_type canvas_item;

void fragment() {
	float radius = length((UV - vec2(0.5, 0.52)) * vec2(1.0, 1.15));
	COLOR = vec4(0.055, 0.045, 0.04, smoothstep(0.35, 0.72, radius) * 0.32);
}
