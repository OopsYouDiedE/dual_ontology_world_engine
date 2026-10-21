shader_type canvas_item;
render_mode unshaded;
uniform float wind = 0.0;
uniform float intensity = 1.0;
varying vec4 tint;
void vertex() { tint = COLOR; }
void fragment() {
	vec2 uv = UV;
	vec2 flame = vec2(0.51, 0.445);
	float mask = exp(-pow((UV.x - flame.x) / 0.045, 2.0) - pow((UV.y - flame.y) / 0.065, 2.0));
	uv.x -= wind * 0.018 * mask;
	vec4 base = texture(TEXTURE, uv) * tint;
	base.rgb *= 1.0 + (intensity - 1.0) * mask * 3.0;
	COLOR = base;
}
