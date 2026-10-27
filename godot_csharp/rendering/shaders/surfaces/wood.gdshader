shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
#include "res://rendering/shaders/common/surface_noise.gdshaderinc"
#include "res://rendering/shaders/common/surface_metrics.gdshaderinc"
uniform vec4 pigment : source_color = vec4(0.31,0.21,0.14,1.0);
uniform float surface_roughness : hint_range(0.1,1.0) = 0.55;
uniform float grain_density : hint_range(2.0,800.0) = 104.719755;
uniform vec3 grain_direction = vec3(1.0,0.0,0.0);
varying vec3 local_position;
#include "res://rendering/shaders/common/occlusion_dither.gdshaderinc"
void vertex(){
 occlusion_world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;local_position=material_coordinates_meters(VERTEX, MODEL_MATRIX);}
void fragment(){
 if(occlusion_amount(occlusion_world, CAMERA_POSITION_WORLD) > occlusion_threshold(FRAGCOORD.xy)) discard;
    vec3 p=local_position/surface_view_scale(local_position);
    float wash=filtered_material_noise(local_position/SURFACE_COARSE_METERS);
    float phase=dot(p,normalize(grain_direction+vec3(0.00001)))*grain_density+wash*5.0;
    float grain=filtered_wave(phase);
    float micro=(filtered_material_noise(p/SURFACE_MICRO_METERS)-0.5)*surface_detail_visibility(local_position,SURFACE_MICRO_METERS);
    ALBEDO=pigment.rgb*(0.98+wash*0.04+grain*0.03+micro*0.02);
    ROUGHNESS=clamp(surface_roughness+grain*0.025,0.1,1.0);
}
