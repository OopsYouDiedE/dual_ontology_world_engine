shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
#include "res://assets/shaders/common/surface_noise.gdshaderinc"
#include "res://assets/shaders/common/surface_metrics.gdshaderinc"
uniform vec4 pigment : source_color = vec4(0.55,0.53,0.47,1.0);
uniform float surface_roughness : hint_range(0.2,1.0) = 0.85;
uniform float variation : hint_range(0.0,0.3) = 0.09;
varying vec3 local_position;
#include "res://assets/shaders/common/occlusion_dither.gdshaderinc"
void vertex(){
 occlusion_world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;local_position=material_coordinates_meters(VERTEX, MODEL_MATRIX);}
void fragment(){
 if(occlusion_amount(occlusion_world, CAMERA_POSITION_WORLD) > occlusion_threshold(FRAGCOORD.xy)) discard;
    vec3 p=local_position/surface_view_scale(local_position);
    float grain=filtered_material_noise(p/SURFACE_DETAIL_METERS)-0.5;
    float wash=filtered_material_noise(local_position/SURFACE_COARSE_METERS)-0.5;
    float micro=(filtered_material_noise(p/SURFACE_MICRO_METERS)-0.5)*surface_detail_visibility(local_position,SURFACE_MICRO_METERS);
    ALBEDO=pigment.rgb*(1.0+wash*0.03+grain*min(variation,0.06)+micro*0.02);
    ROUGHNESS=clamp(surface_roughness+grain*0.05,0.2,1.0);
}
