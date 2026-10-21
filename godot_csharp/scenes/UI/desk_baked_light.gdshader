shader_type canvas_item;
render_mode unshaded;

// Baked glass/brass reflections retain the reference's coherent lighting.
// Only the flame and its incident illumination move; the lamp body is rigid.
uniform float wind = 0.0;
uniform float intensity = 1.0;
uniform vec2 flame_center = vec2(0.026, 0.145);
varying vec4 tint;

void vertex() { tint = COLOR; }

void fragment() {
	vec2 uv = UV;
	vec2 flame_delta = (uv - flame_center) / vec2(0.028, 0.085);
	float flame_mask = exp(-dot(flame_delta, flame_delta) * 2.5);
	float flame_top = clamp((flame_center.y + 0.07 - uv.y) / 0.14, 0.0, 1.0);
	uv.x -= wind * 0.005 * flame_mask * flame_top;
	vec4 base = texture(TEXTURE, uv) * tint;
	vec2 light_origin = flame_center + vec2(wind * 0.012, 0.0);
	float light_pool = exp(-length((UV - light_origin) * vec2(1.1, 0.82)) * 2.8);
	float reflection = smoothstep(0.3, 0.85, max(base.r, max(base.g, base.b)));
	float response = light_pool * (0.45 + reflection * 0.55);
	base.rgb *= 1.0 + (intensity - 1.0) * response * 1.7;
	base.rgb += vec3(1.0, 0.68, 0.32) * flame_mask * max(0.0, intensity - 0.96) * 0.28;
	COLOR = base;
}
