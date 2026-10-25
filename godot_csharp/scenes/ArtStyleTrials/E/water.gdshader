shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
void fragment(){
 float ripple=sin(UV.x*65.0+TIME*1.2+sin(UV.y*31.0))*sin(UV.y*48.0-TIME*0.8);
 ALBEDO=vec3(0.12,0.29,0.25)+ripple*0.015;
 ROUGHNESS=0.22; METALLIC=0.15;
 NORMAL_MAP=normalize(vec3(cos(UV.x*65.0+TIME)*0.10,sin(UV.y*48.0-TIME)*0.08,1.0))*0.5+0.5;
}
