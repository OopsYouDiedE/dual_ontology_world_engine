shader_type spatial;
#include "res://rendering/shaders/common/surface_metrics.gdshaderinc"
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
	float seam=surface_joint(edge*tile_size,0.006);
	float corner=min(f.x,1.0-f.x)+min(f.y,1.0-f.y);
	vec3 color=base_color.rgb;
	float inset_edge=corner*tile_size;
	float inset_aa=max(fwidth(inset_edge),0.001);
	if(diamond_insets) color=mix(accent_color.rgb,color,smoothstep(0.076-inset_aa,0.076+inset_aa,inset_edge));
	color*=1.0+0.025*surface_filtered_wave(world.x/(SURFACE_DETAIL_METERS*surface_view_scale(world))*6.2831853+sin(world.z*8.0));
	ALBEDO=mix(color,vec3(0.28,0.29,0.26),seam*0.55);
	ROUGHNESS=0.78;
}
