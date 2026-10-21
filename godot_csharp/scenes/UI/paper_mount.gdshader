shader_type canvas_item;

// Set to Slip.size in _ready and on resized. Coordinates are Control pixels,
// independent of the segmented UVs emitted by StyleBoxTexture nine-slicing.
uniform vec2 paper_size = vec2(440.0, 620.0);
uniform float notch_depth : hint_range(0.0, 3.0) = 1.8;
uniform float aged_edge_width : hint_range(0.0, 12.0) = 5.0;
uniform float aged_edge_strength : hint_range(0.0, 1.0) = 0.45;

varying vec2 paper_point;
varying vec4 vertex_tint;

float edge_hash(float value) {
	return fract(sin(value * 127.1) * 43758.5453);
}

float edge_noise(float value) {
	float cell = floor(value);
	float f = fract(value);
	f = f * f * (3.0 - 2.0 * f);
	return mix(edge_hash(cell), edge_hash(cell + 1.0), f);
}

float edge_notch(float along, float seed) {
	return 0.15 + notch_depth * (
		0.58 * edge_noise(along * 0.18 + seed)
		+ 0.22 * edge_noise(along * 0.73 + seed * 1.7)
		+ 0.20 * pow(edge_noise(along * 0.038 + seed * 2.3), 5.0));
}

void vertex() {
	paper_point = VERTEX;
	vertex_tint = COLOR;
}

void fragment() {
	// Sample the original style-box texture without moving any texels.
	vec4 paper = texture(TEXTURE, UV) * vertex_tint;
	vec4 inward = vec4(paper_point.x, paper_size.x - paper_point.x,
		paper_point.y, paper_size.y - paper_point.y);
	inward -= vec4(edge_notch(paper_point.y, 17.0),
		edge_notch(paper_point.y, 43.0),
		edge_notch(paper_point.x, 71.0),
		edge_notch(paper_point.x, 103.0));
	float edge_distance = min(min(inward.x, inward.y), min(inward.z, inward.w));
	float aa = max(fwidth(edge_distance), 0.65);
	float mask = smoothstep(-aa * 0.5, aa * 0.5, edge_distance);
	float age = 1.0 - smoothstep(0.0, max(aged_edge_width, 0.001), max(edge_distance, 0.0));
	paper.rgb *= mix(vec3(1.0), vec3(0.82, 0.76, 0.66), age * aged_edge_strength);
	COLOR = vec4(paper.rgb, paper.a * mask);
}
