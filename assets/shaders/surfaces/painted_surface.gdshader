shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
uniform vec4 pigment : source_color = vec4(0.4,0.3,0.2,1.0);
uniform float surface_roughness = 0.8;
uniform float surface_metallic = 0.0;
varying vec3 local_pos;
#include "res://assets/shaders/common/surface_noise.gdshaderinc"
#include "res://assets/shaders/common/surface_metrics.gdshaderinc"
#include "res://assets/shaders/common/occlusion_dither.gdshaderinc"
void vertex(){
 occlusion_world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;local_pos=material_coordinates_meters(VERTEX, MODEL_MATRIX);}
void fragment(){
 if(occlusion_amount(occlusion_world, CAMERA_POSITION_WORLD) > occlusion_threshold(FRAGCOORD.xy)) discard;
 vec3 p=local_pos/surface_view_scale(local_pos);
 float wash=filtered_material_noise(local_pos/SURFACE_COARSE_METERS)*0.65+filtered_material_noise(p/SURFACE_DETAIL_METERS)*0.35;
 float bristle=mix(0.5,filtered_material_noise(p/SURFACE_MICRO_METERS),surface_detail_visibility(local_pos,SURFACE_MICRO_METERS));
 float variation=(wash-0.5)*0.055+(bristle-0.5)*0.035;
 vec3 warm=vec3(1.02,1.005,0.98);
 vec3 cool=vec3(0.98,0.995,1.02);
 ALBEDO=clamp(pigment.rgb*mix(cool,warm,wash)*(1.0+variation),vec3(0.0),vec3(1.0));
 ROUGHNESS=clamp(surface_roughness+(wash-0.5)*0.12,0.12,1.0);
 METALLIC=surface_metallic;
}
