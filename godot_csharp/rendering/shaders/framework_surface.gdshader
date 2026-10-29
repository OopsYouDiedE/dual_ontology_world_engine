shader_type spatial;
render_mode diffuse_burley;
uniform vec4 base_color : source_color = vec4(0.5,0.5,0.5,1.0);
uniform bool floor_surface = false;
uniform float surface_roughness : hint_range(0.0,1.0) = 0.87;
uniform float texture_scale_m = 1.0;
varying vec3 world_position;
varying vec3 world_normal;
void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz;
	world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
	vec2 uv = abs(world_normal.y)>0.6 ? world_position.xz : (abs(world_normal.x)>0.6 ? world_position.zy : world_position.xy);
	vec2 cell = floor_surface ? vec2(0.6) : vec2(1.2,0.6);
	cell *= max(texture_scale_m,0.05);
	if (!floor_surface) uv.x += mod(floor(uv.y/cell.y),2.0)*0.6;
	vec2 p = abs(fract(uv/cell)-0.5)*cell;
	vec2 edge = cell*0.5-p;
	float gap = min(edge.x,edge.y);
	float aa = max(fwidth(gap),0.001);
	float line = 1.0-smoothstep(0.006-aa,0.006+aa,gap);
	ALBEDO = base_color.rgb * (1.0-line*(floor_surface ? 0.24 : 0.10));
	ROUGHNESS = surface_roughness;
}
