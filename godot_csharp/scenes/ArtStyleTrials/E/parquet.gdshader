shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
varying vec3 world;
uniform float wood_value=1.0;
uniform float finish_roughness=.53;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
void fragment(){
 vec2 p=mat2(vec2(0.7071,-0.7071),vec2(0.7071,0.7071))*world.xz/0.16;
 vec2 cell=floor(p); vec2 f=fract(p);
 float band=mod(cell.x+cell.y,8.0);
 bool horizontal=band<4.0;
 vec2 along=horizontal?vec2(band+f.x,f.y):vec2(band-4.0+f.y,f.x);
 vec2 id=horizontal?cell-vec2(band,0.0):cell-vec2(0.0,band-4.0);
 float rand=hash(id);
 float distance_edge=min(min(along.x,4.0-along.x),min(along.y,1.0-along.y));
 float aa=max(fwidth(distance_edge),0.015);
 float seam=smoothstep(0.018-aa,0.018+aa,distance_edge);
 float grain=sin(along.y*55.0+sin(along.x*1.8+rand*9.0)*2.0);
 float brush=sin(along.y*130.0+along.x*9.0)*0.008;
 vec3 wood=mix(vec3(0.255,0.123,0.055),vec3(0.37,0.205,0.105),rand);
 ALBEDO=mix(vec3(0.10,0.068,0.042),wood*(1.0+grain*0.055+brush),seam)*wood_value;
 ROUGHNESS=finish_roughness+rand*0.12;
}
