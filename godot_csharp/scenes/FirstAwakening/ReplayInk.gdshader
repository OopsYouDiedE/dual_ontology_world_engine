shader_type canvas_item;
render_mode unshaded;

// Preserve native colour and pixel detail with a modest saturation reduction.
uniform float saturation : hint_range(0.0, 1.0) = 0.8;
void fragment() {
	vec4 original = texture(TEXTURE, UV);
	float luminance = dot(original.rgb, vec3(0.2126, 0.7152, 0.0722));
	COLOR = vec4(mix(vec3(luminance), original.rgb, saturation), original.a);
}
