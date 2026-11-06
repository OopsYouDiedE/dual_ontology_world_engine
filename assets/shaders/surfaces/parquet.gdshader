shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
#include "res://assets/shaders/common/surface_metrics.gdshaderinc"
varying vec3 world;
uniform float wood_value=1.0;
uniform float finish_roughness=.53;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
void fragment(){
 vec2 p=mat2(vec2(0.7071,-0.7071),vec2(0.7071,0.7071))*world.xz/0.16;
 vec2 cell=floor(p); vec2 f=fract(p);
 float band=mod(cell.x+cell.y,8.0);
 bool horizontal=band<4.0;
 vec2 along=horizontal?vec2(band+f.x,f.y):vec2(band-4.0+f.y,f.x);
 vec2 id=horizontal?cell-vec2(band,0.0):cell-vec2(0.0,band-4.0);
 float rand=hash(id);
 float distance_edge=min(min(along.x,4.0-along.x),min(along.y,1.0-along.y));
 float seam=surface_joint(distance_edge*0.16,0.0029);
 float detail_scale=surface_view_scale(world);
 float grain=surface_filtered_wave((along.y*0.16/(SURFACE_DETAIL_METERS*detail_scale))*6.2831853+sin(along.x*1.8+rand*9.0)*2.0);
 float brush=surface_filtered_wave((along.y*0.16/(SURFACE_MICRO_METERS*detail_scale))*6.2831853+along.x*2.0);
 vec3 wood=vec3(0.308,0.164,0.080)*(0.92+rand*0.16);
 ALBEDO=mix(wood*(1.0+grain*0.045+brush*0.012),vec3(0.10,0.068,0.042),seam)*wood_value;
 ROUGHNESS=finish_roughness+(rand-0.5)*0.08;
}
