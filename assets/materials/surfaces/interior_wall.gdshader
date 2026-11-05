shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
#include "res://rendering/shaders/common/surface_metrics.gdshaderinc"
uniform sampler2D wallpaper : source_color, repeat_enable, filter_linear_mipmap_anisotropic;
uniform bool use_wallpaper=false;
uniform vec4 pigment : source_color=vec4(.776,.784,.714,1.0);
uniform float finish_roughness=.9;
varying vec3 world;
varying vec3 world_normal;
#include "res://rendering/shaders/common/occlusion_dither.gdshaderinc"
void vertex(){
 occlusion_world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
 world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
 world_normal=normalize(MODEL_NORMAL_MATRIX*NORMAL);
}
void fragment(){
 if(occlusion_amount(occlusion_world, CAMERA_POSITION_WORLD) > occlusion_threshold(FRAGCOORD.xy)) discard;
 vec3 color=pigment.rgb;
 if(use_wallpaper){
  vec3 weights=pow(abs(world_normal),vec3(4.0));
  weights/=max(dot(weights,vec3(1.0)),.0001);
  color=texture(wallpaper,world.zy*.65).rgb*weights.x
       +texture(wallpaper,world.xz*.65).rgb*weights.y
       +texture(wallpaper,world.xy*.65).rgb*weights.z;
 }
 float scale=surface_view_scale(world);
 float detail=surface_filtered_wave((world.x+world.z)/(SURFACE_DETAIL_METERS*scale)*6.2831853+world.y*7.0);
 float micro=surface_filtered_wave(world.y/(SURFACE_MICRO_METERS*scale)*6.2831853);
 ALBEDO=color*(1.0+.014*detail+.006*micro);
 ROUGHNESS=clamp(finish_roughness+.012*micro,0.0,1.0);
}
