shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled;

// Night sky stained red by a distant eruption: gradient, ash clouds, stars, flashes.
// Drawn on a far quad; UV.y = 0 is the top edge.
uniform vec2 volcano_uv = vec2(0.42, 0.52);
uniform float horizon_v = 0.66;
uniform float flash = 0.0;
uniform vec4 zenith : source_color = vec4(0.008, 0.012, 0.035, 1.0);
uniform vec4 middle : source_color = vec4(0.07, 0.02, 0.045, 1.0);
uniform vec4 horizon : source_color = vec4(0.42, 0.10, 0.05, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
	float value = 0.0;
	float amplitude = 0.5;
	for (int octave = 0; octave < 5; octave++) {
		value += noise(p) * amplitude;
		p = p * 2.03 + vec2(17.0, 9.0);
		amplitude *= 0.5;
	}
	return value;
}

void fragment() {
	vec2 uv = UV;
	float height = clamp((horizon_v - uv.y) / horizon_v, 0.0, 1.0);
	vec3 color = mix(horizon.rgb, middle.rgb, smoothstep(0.0, 0.10, height));
	color = mix(color, zenith.rgb, smoothstep(0.10, 0.55, height));
	vec2 offset = (uv - volcano_uv) * vec2(2.4, 1.0);
	float glow = exp(-dot(offset, offset) * 26.0);
	float pulse = 0.86 + 0.10 * sin(TIME * 0.83) + 0.04 * sin(TIME * 3.7);
	color += vec3(1.0, 0.34, 0.11) * glow * 0.75 * pulse;
	// Ash drifts slowly to the right and is lit from beneath by the crater.
	float cloud_noise = fbm(uv * vec2(3.2, 7.0) + vec2(TIME * 0.0045, TIME * 0.0012));
	float cloud = smoothstep(0.46, 0.80, cloud_noise) * smoothstep(0.02, 0.22, height) * (1.0 - smoothstep(0.75, 1.0, height));
	vec3 cloud_color = mix(vec3(0.045, 0.025, 0.04), vec3(0.58, 0.17, 0.08), clamp(glow * 1.8 + (1.0 - height) * 0.35, 0.0, 1.0));
	color = mix(color, cloud_color, cloud * 0.8);
	vec2 star_uv = uv * vec2(460.0, 190.0);
	float star_seed = hash(floor(star_uv));
	float star = step(0.986, star_seed) * smoothstep(0.38, 0.0, length(fract(star_uv) - 0.5));
	star *= smoothstep(0.38, 0.82, height) * (1.0 - cloud) * (0.55 + 0.45 * sin(TIME * 1.7 + star_seed * 70.0));
	color += vec3(0.92, 0.88, 1.0) * star * 0.85;
	color += vec3(1.0, 0.78, 0.58) * flash * (glow * 2.6 + cloud * 0.4) + vec3(0.18, 0.07, 0.06) * flash;
	ALBEDO = color;
}
