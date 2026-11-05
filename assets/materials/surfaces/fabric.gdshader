shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
#include "res://rendering/shaders/common/surface_noise.gdshaderinc"
#include "res://rendering/shaders/common/surface_metrics.gdshaderinc"
uniform vec4 pigment : source_color = vec4(0.20,0.34,0.29,1.0);
uniform float surface_roughness : hint_range(0.4,1.0) = 0.94;
uniform float weave_density : hint_range(10.0,600.0) = 220.0;
varying vec3 local_position;
#include "res://rendering/shaders/common/occlusion_dither.gdshaderinc"
void vertex(){
 occlusion_world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;local_position=material_coordinates_meters(VERTEX, MODEL_MATRIX);}
void fragment(){
 if(occlusion_amount(occlusion_world, CAMERA_POSITION_WORLD) > occlusion_threshold(FRAGCOORD.xy)) discard;
    vec3 p=local_position/surface_view_scale(local_position);
    float weave=filtered_wave(p.x*weave_density)*filtered_wave((p.y+p.z)*weave_density);
    float wash=filtered_material_noise(local_position/SURFACE_DETAIL_METERS)-0.5;
    ALBEDO=pigment.rgb*(1.0+wash*0.045+weave*0.018);
    ROUGHNESS=clamp(surface_roughness+wash*0.04,0.4,1.0);
}
