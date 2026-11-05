// Adapted from Malido, Stylized Multimesh Grass Shader (CC0-1.0).
// https://godotshaders.com/shader/stylized-multimesh-grass-shader/
// Project changes: real tapered blades, INSTANCE_CUSTOM variation, no player state,
// no instance uniforms, and subdued E-palette lighting for Compatibility rendering.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_disabled;

uniform vec4 top_color : source_color = vec4(0.46, 0.53, 0.29, 1.0);
uniform vec4 bottom_color : source_color = vec4(0.19, 0.25, 0.12, 1.0);
uniform sampler2D wind_noise : repeat_enable, filter_linear;
uniform vec2 wind_direction = vec2(0.8, 0.45);
uniform float wind_strength = 0.045;
uniform float wind_noise_size = 0.075;
uniform float wind_noise_speed = 0.035;
varying float individual_tint;

void vertex() {
	vec3 world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec2 direction = normalize(wind_direction + vec2(0.0001));
	float gust = texture(wind_noise, world_position.xz * wind_noise_size + direction * TIME * wind_noise_speed).r;
	float height_weight = 1.0 - UV.y;
	vec3 wind_world = vec3(direction.x, -0.1, direction.y) * (gust * 0.75 + sin(TIME * 1.6 + world_position.x * 0.7 + world_position.z) * 0.25);
	VERTEX += (inverse(MODEL_MATRIX) * vec4(wind_world * wind_strength * height_weight * height_weight, 0.0)).xyz;
	NORMAL = normalize(mix(NORMAL, vec3(0.0, 1.0, 0.0), 0.68));
	individual_tint = INSTANCE_CUSTOM.r;
}

void fragment() {
	if (!FRONT_FACING) { NORMAL = -NORMAL; }
	vec3 gradient = mix(bottom_color.rgb, top_color.rgb, pow(1.0 - UV.y, 0.65));
	ALBEDO = gradient * mix(0.83, 1.12, individual_tint);
	ROUGHNESS = 0.94;
	SPECULAR = 0.0;
}
