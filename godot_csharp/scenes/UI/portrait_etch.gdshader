shader_type canvas_item;
render_mode unshaded;

void fragment() {
	vec4 base = texture(TEXTURE, UV) * COLOR;
	float value = dot(base.rgb, vec3(0.299, 0.587, 0.114));
	base.rgb = mix(base.rgb, vec3(value), 0.12);
	float hatch = smoothstep(0.76, 0.94, fract((FRAGCOORD.x + FRAGCOORD.y) / 5.0));
	base.rgb *= 1.0 - hatch * (1.0 - value) * 0.075;
	base.a *= 1.0 - smoothstep(0.91, 1.0, UV.y);
	COLOR = base;
}
