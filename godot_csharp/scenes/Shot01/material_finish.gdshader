shader_type canvas_item;

void fragment() {
	vec4 surface = COLOR;
	float value = dot(surface.rgb, vec3(0.299, 0.587, 0.114));
	COLOR = vec4(mix(vec3(value), surface.rgb, 0.68) * vec3(0.88, 0.86, 0.82), surface.a);
}
