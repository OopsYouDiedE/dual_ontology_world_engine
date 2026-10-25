shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
uniform vec4 pigment : source_color = vec4(0.4,0.3,0.2,1.0);
uniform float surface_kind = 0.0;
uniform float surface_roughness = 0.8;
uniform float surface_metallic = 0.0;
varying vec3 local_pos;
float hash(vec3 p){return fract(sin(dot(p,vec3(127.1,311.7,74.7)))*43758.5453);}
float noise(vec3 p){vec3 i=floor(p);vec3 f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x),mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x),f.y),mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x),mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x),f.y),f.z);}
void vertex(){local_pos=VERTEX;}
void fragment(){
 vec3 p=local_pos;
 float wash=noise(p*7.0)*0.65+noise(p*19.0)*0.35;
 float bristle=noise(p*vec3(75.0,12.0,8.0));
 float variation=(wash-0.5)*0.055+(bristle-0.5)*0.035;
 if(surface_kind>0.5 && surface_kind<1.5){
  float grain=sin(p.x*115.0+noise(p*vec3(3.0,5.0,12.0))*14.0);
  variation+=grain*0.008;
 }
 if(surface_kind>2.5 && surface_kind<3.5){variation+=sin(p.x*260.0)*sin(p.z*260.0+p.y*200.0)*0.009;}
 vec3 warm=vec3(1.02,1.005,0.98);
 vec3 cool=vec3(0.98,0.995,1.02);
 ALBEDO=clamp(pigment.rgb*mix(cool,warm,wash)*(1.0+variation),vec3(0.0),vec3(1.0));
 ROUGHNESS=clamp(surface_roughness+(wash-0.5)*0.12,0.12,1.0);
 METALLIC=surface_metallic;
}
