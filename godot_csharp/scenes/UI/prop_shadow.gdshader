shader_type canvas_item;
render_mode unshaded;
uniform vec2 displacement = vec2(0.02, 0.04);
uniform float softness = 0.012;
void fragment() {
	float alpha = 0.0;
	for (int y = -2; y <= 2; y++) {
		for (int x = -2; x <= 2; x++) {
			vec2 uv = UV - displacement + vec2(float(x), float(y)) * softness;
			if (all(greaterThanEqual(uv, vec2(0.0))) && all(lessThanEqual(uv, vec2(1.0)))) alpha += texture(TEXTURE, uv).a / 25.0;
		}
	}
	COLOR = vec4(0.055, 0.05, 0.045, alpha * 0.45);
}
