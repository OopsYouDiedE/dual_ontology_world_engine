// Shared original world-space wind. All dimensions are metres.
uniform vec2 wind_direction = vec2(0.85, 0.35);
uniform float wind_strength : hint_range(0.0, 2.0) = 0.45;
uniform float wind_clock = -1.0;
uniform float plant_height = 1.0;
uniform float main_bend = 0.18;
uniform float branch_bend = 0.10;
uniform float leaf_flutter = 0.025;
float vegetation_clock(){return wind_clock < 0.0 ? TIME : wind_clock;}
vec3 vegetation_offset(vec3 p, vec3 anchor, vec4 weights){
 float t=vegetation_clock();
 vec2 direction=normalize(wind_direction+vec2(0.0001));
 float wave=sin(dot(anchor.xz,vec2(0.21,0.14))-t*0.9);
 float gust=0.65+0.28*wave+0.12*sin(dot(anchor.xz,vec2(0.63,-0.31))-t*1.7);
 float branch=sin(t*1.9+dot(anchor.xz,vec2(.41,.27))+dot(p.xz,vec2(.34,.21)));
 float flutter=sin(t*6.4+weights.a*19.0+dot(p.xz,vec2(1.7,.8)));
 float bend=main_bend*weights.r*gust+branch_bend*weights.g*branch;
 vec2 move=direction*bend+vec2(-direction.y,direction.x)*leaf_flutter*weights.b*flutter;
 float drop=-0.06*weights.r*wind_strength*wind_strength;
 return vec3(move.x*wind_strength,drop,move.y*wind_strength);
}
