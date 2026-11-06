// Adaptation of J Hell's MIT shader / CC0 scale solution demo.
// Source: https://godotshaders.com/shader/simple-cheap-stylized-tree-shader/
// Explicit alpha cutout follows the author's discussion; original kept separately.
shader_type spatial;
render_mode depth_draw_opaque, cull_disabled;
uniform sampler2D foliage_texture;
uniform vec3 foliage_colour : source_color = vec3(0.192157,0.454902,0.34902);
uniform float scale = 3.697;
uniform int alpha_channel = 1;
void vertex() {
	NORMAL = -NORMAL;
	vec2 offset = (UV-vec2(0.5))*scale;
	VERTEX += (inverse(MODELVIEW_MATRIX)*vec4(offset,0.0,0.0)).xyz;
}
void fragment() {
	ALBEDO = foliage_colour;
	ROUGHNESS = 0.9;
	vec3 mask = texture(foliage_texture,UV).rgb;
	ALPHA = alpha_channel == 0 ? mask.r : mask.g;
	ALPHA_SCISSOR_THRESHOLD = 0.35;
}
