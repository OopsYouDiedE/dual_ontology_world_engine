shader_type canvas_item;

void fragment() {
	vec4 sample_color = COLOR;
	float value = dot(sample_color.rgb, vec3(0.299, 0.587, 0.114));
	// Keep the material grain, but prevent black grout from dominating the room.
	vec3 stone = mix(vec3(0.34, 0.31, 0.27), vec3(0.68, 0.63, 0.55), value);
	COLOR = vec4(stone, sample_color.a);
}
