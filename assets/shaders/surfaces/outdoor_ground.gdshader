shader_type spatial;
render_mode diffuse_burley, specular_disabled;
uniform vec4 grass_color : source_color = vec4(0.27, 0.32, 0.18, 1.0);
uniform vec4 soil_color : source_color = vec4(0.24, 0.20, 0.14, 1.0);
uniform vec4 stone_color : source_color = vec4(0.46, 0.45, 0.37, 1.0);
uniform int route_count = 0;
uniform vec4 routes[48];
uniform float route_widths[48];
varying vec3 world_point;

float hash21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise21(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f*f*(3.0-2.0*f);
	return mix(mix(hash21(i),hash21(i+vec2(1,0)),f.x),mix(hash21(i+vec2(0,1)),hash21(i+vec2(1,1)),f.x),f.y);
}
float route_distance(vec2 p, vec4 edge) {
	vec2 ab = edge.zw-edge.xy;
	float t = clamp(dot(p-edge.xy,ab)/max(dot(ab,ab),0.001),0.0,1.0);
	return length(p-edge.xy-ab*t);
}
void vertex() { world_point = (MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
	vec2 p = world_point.xz;
	float broad = noise21(p*0.25)*0.65+noise21(p*0.63)*0.35;
	float grain = noise21(p*31.0);
	float distance_to_path = 10000.0;
	for(int i=0;i<48;i++) {
		if(i>=route_count) break;
		distance_to_path = min(distance_to_path,route_distance(p,routes[i])-route_widths[i]*0.5);
	}
	float ragged = (noise21(p*4.0)-0.5)*0.15;
	float turf = smoothstep(-0.06,0.30,distance_to_path+ragged);
	vec3 lawn = grass_color.rgb*mix(0.77,1.17,broad);
	lawn = mix(lawn,soil_color.rgb,smoothstep(0.66,0.86,noise21(p*0.72))*0.23);
	vec3 packed = soil_color.rgb*mix(0.80,1.15,grain);
	packed = mix(packed,stone_color.rgb,smoothstep(0.73,0.88,hash21(floor(p*24.0)))*0.23);
	ALBEDO = mix(packed,lawn,turf)*(0.94+grain*0.12);
	ROUGHNESS = 0.97;
	SPECULAR = 0.0;
}
