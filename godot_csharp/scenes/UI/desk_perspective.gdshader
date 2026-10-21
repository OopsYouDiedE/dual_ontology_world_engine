shader_type canvas_item;
render_mode unshaded;
uniform vec2 tilt = vec2(0.0);
uniform float aspect = 1.2903226;
varying vec4 tint;
void vertex() { tint = COLOR; }

void fragment() {
	float cy = cos(tilt.x), sy = sin(tilt.x);
	float cp = cos(tilt.y), sp = sin(tilt.y);
	vec3 u = vec3(cy, 0.0, -sy);
	vec3 v = vec3(sy * sp, cp, cy * sp);
	vec3 n = cross(u, v);
	vec3 origin = vec3(0.0, 0.0, 2.4);
	vec3 ray = vec3((UV.x - 0.5) * aspect, 0.5 - UV.y, -2.4);
	vec3 hit = origin - ray * dot(n, origin) / dot(n, ray);
	vec2 source = vec2(dot(hit, u) / aspect + 0.5, 0.5 - dot(hit, v));
	// Overscan the tabletop so tilting never stretches the source's edge pixels.
	source = vec2(0.5) + (source - vec2(0.5)) * 0.92;
	COLOR = texture(TEXTURE, clamp(source, vec2(0.001), vec2(0.999))) * tint;
}
