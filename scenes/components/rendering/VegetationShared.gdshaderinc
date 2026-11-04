#include "res://scenes/components/rendering/VegetationWind.gdshaderinc"
uniform vec4 base_color : source_color = vec4(.22,.40,.12,1);
uniform vec4 tip_color : source_color = vec4(.46,.59,.20,1);
uniform float roughness_value = .82;
uniform float transmission : hint_range(0.0,1.0) = .18;
uniform float canopy_softness : hint_range(0.0,1.0) = .35;

void vegetation_deform(inout vec3 vertex_position,inout vec3 vertex_normal,vec4 vertex_weights,mat4 model,mat3 model_normal){
 vec3 p=(model*vec4(vertex_position,1)).xyz;
 vec3 anchor=model[3].xyz;
 vec3 n=normalize(model_normal*vertex_normal);
 vec3 tangent=normalize(cross(abs(n.y)>.95?vec3(1,0,0):vec3(0,1,0),n));
 vec3 bitangent=cross(n,tangent);
 vec3 shift=vegetation_offset(p,anchor,vertex_weights);
 vec4 wt=vertex_weights;vec4 wb=vertex_weights;
 wt.r=pow(max(sqrt(max(vertex_weights.r,0.0))+tangent.y*.015/plant_height,0.0),2.0);
 wb.r=pow(max(sqrt(max(vertex_weights.r,0.0))+bitangent.y*.015/plant_height,0.0),2.0);
 vec3 dt=tangent*.015+vegetation_offset(p+tangent*.015,anchor,wt)-shift;
 vec3 db=bitangent*.015+vegetation_offset(p+bitangent*.015,anchor,wb)-shift;
 vec3 deformed_normal=normalize(cross(dt,db));
 vertex_normal=normalize(inverse(model_normal)*deformed_normal);
 vertex_position=(inverse(model)*vec4(p+shift,1)).xyz;
}
void vegetation_surface(vec2 uv,vec4 weights,mat4 view,inout vec3 normal,out vec3 albedo,out float rough,out vec3 back){
 vec3 color=mix(base_color.rgb,tip_color.rgb,clamp(uv.y,0.0,1.0));
 albedo=color*(.91+.16*weights.a);
 rough=roughness_value;
 vec3 up=normalize((view*vec4(0,1,0,0)).xyz);
 normal=normalize(mix(normal,up,canopy_softness));
 back=color*transmission;
}
