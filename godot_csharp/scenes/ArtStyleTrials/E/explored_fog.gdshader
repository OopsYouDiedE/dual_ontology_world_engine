shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled;
uniform vec3 mist_color : source_color=vec3(0.35,0.40,0.41);
uniform float opacity : hint_range(0.0,1.0)=0.34;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p);vec2 f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
void fragment(){
	float cloud=noise(UV*7.0+vec2(TIME*0.012,0.0))*0.65+noise(UV*17.0-vec2(0.0,TIME*0.009))*0.35;
	ALBEDO=mist_color;
	ALPHA=opacity*(0.60+0.40*cloud);
}
