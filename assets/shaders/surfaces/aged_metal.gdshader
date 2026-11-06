shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
#include "res://assets/shaders/common/surface_noise.gdshaderinc"
uniform vec4 pigment : source_color = vec4(0.66,0.53,0.31,1.0);
uniform float surface_roughness : hint_range(0.08,1.0) = 0.38;
uniform float surface_metallic : hint_range(0.0,1.0) = 0.85;
uniform float patina : hint_range(0.0,0.5) = 0.08;
varying vec3 local_position;
#include "res://assets/shaders/common/occlusion_dither.gdshaderinc"
void vertex(){
 occlusion_world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;local_position=material_coordinates_meters(VERTEX, MODEL_MATRIX);}
void fragment(){
 if(occlusion_amount(occlusion_world, CAMERA_POSITION_WORLD) > occlusion_threshold(FRAGCOORD.xy)) discard;
    float age=filtered_material_noise(local_position*12.0);
    ALBEDO=pigment.rgb*(1.0-age*patina);
    METALLIC=surface_metallic*(1.0-age*patina);
    ROUGHNESS=clamp(surface_roughness+age*patina,0.08,1.0);
}
