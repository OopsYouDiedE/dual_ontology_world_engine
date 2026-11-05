shader_type spatial;
render_mode cull_disabled, diffuse_burley;
uniform vec4 pigment : source_color = vec4(0.31,0.37,0.38,1.0);
varying vec3 world_point;
void vertex() { world_point = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz; }
void fragment() {
	vec2 t = world_point.xz / vec2(0.32,0.44);
	t.y += mod(floor(t.x),2.0)*0.5;
	vec2 f = fract(t);
	vec2 aa = max(fwidth(t),vec2(0.008));
	vec2 edge = min(f,1.0-f);
	float seam = 1.0-min(smoothstep(0.01,0.01+aa.x,edge.x),smoothstep(0.015,0.015+aa.y,edge.y));
	float variation = fract(sin(dot(floor(t),vec2(12.9898,78.233)))*43758.5453);
	ALBEDO = pigment.rgb*(0.91+variation*0.13)*(1.0-seam*0.17);
	ROUGHNESS = 0.89;
}
