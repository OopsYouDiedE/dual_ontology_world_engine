shader_type spatial;
uniform vec4 base_color : source_color=vec4(0.65,0.65,0.57,1.0);
uniform vec4 accent_color : source_color=vec4(0.20,0.30,0.27,1.0);
uniform float tile_size=0.40;
uniform bool diamond_insets=true;
varying vec3 world;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
void fragment(){
	vec2 q=world.xz/tile_size;
	vec2 f=fract(q);
	float edge=min(min(f.x,1.0-f.x),min(f.y,1.0-f.y));
	float seam=1.0-smoothstep(0.012,0.024,edge);
	float corner=min(f.x,1.0-f.x)+min(f.y,1.0-f.y);
	vec3 color=base_color.rgb;
	if(diamond_insets) color=mix(accent_color.rgb,color,smoothstep(0.18,0.20,corner));
	color*=0.98+0.02*sin(world.x*23.0+sin(world.z*18.0));
	ALBEDO=mix(color,vec3(0.28,0.29,0.26),seam*0.55);
	ROUGHNESS=0.78;
}
