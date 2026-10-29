shader_type spatial;
render_mode diffuse_burley;
uniform vec4 base_color : source_color = vec4(0.52,0.46,0.34,1.0);
uniform vec4 secondary_color : source_color = vec4(0.64,0.58,0.44,1.0);
uniform int surface_kind = 0; // 0 plaster, 1 wood, 2 stone tiles, 3 metal, 4 textile
uniform float roughness_value = 0.85;
uniform float metallic_value = 0.0;
varying vec3 world_pos;
varying vec3 world_norm;
varying vec3 local_pos;
float hash(vec3 p) {return fract(sin(dot(p,vec3(12.9898,78.233,37.719)))*43758.5453);}
float noise(vec3 p) {
 vec3 i=floor(p);vec3 f=fract(p);f=f*f*(3.0-2.0*f);
 return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x),mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x),f.y),mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x),mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x),f.y),f.z);
}
void vertex(){world_pos=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;world_norm=normalize(MODEL_NORMAL_MATRIX*NORMAL);local_pos=VERTEX;}
void fragment(){
 float n=noise(world_pos*3.0);vec3 c=mix(base_color.rgb,secondary_color.rgb,n*.35);
 float rough=roughness_value;
 if(surface_kind==1){
  float grain=noise(local_pos*vec3(3.0,7.0,70.0));
  float rings=0.5+0.5*sin(local_pos.z*150.0+grain*9.0);
  c=mix(base_color.rgb,secondary_color.rgb,grain*.65+rings*.2);rough+=grain*.08;
 }else if(surface_kind==2){
  vec2 uv=abs(world_norm.y)>.6?world_pos.xz:(abs(world_norm.x)>.6?world_pos.zy:world_pos.xy);
  vec2 cell=floor(uv/.6);vec2 p=min(fract(uv/.6),1.0-fract(uv/.6))*.6;
  float dist=min(p.x,p.y);float aa=max(fwidth(dist),.0008);
  float grout=1.0-smoothstep(.003-aa,.003+aa,dist);
  c=mix(base_color.rgb,secondary_color.rgb,hash(vec3(cell,0))*.65);
  c=mix(c,base_color.rgb*.55,grout);rough=mix(rough,.95,grout);
 }else if(surface_kind==4){
  float weave=sin(local_pos.x*500.0)*sin(local_pos.z*500.0);
  c*=1.0+weave*.025;
 }
 ALBEDO=c;ROUGHNESS=clamp(rough,0.05,1.0);METALLIC=metallic_value;
}
