shader_type spatial;
uniform bool grid_enabled = true;
uniform vec3 dark_grass : source_color = vec3(0.25,0.34,0.18);
uniform vec3 light_grass : source_color = vec3(0.40,0.48,0.27);
varying vec3 world_point;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p);vec2 f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1)),f.x),f.y);}
void vertex(){world_point=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
void fragment(){
 vec2 p=world_point.xz;
 vec3 color=mix(dark_grass,light_grass,noise(p*0.19)*0.65+noise(p*1.7)*0.35);
 float fine_weight=1.0-smoothstep(0.02,0.09,max(length(dFdx(p)),length(dFdy(p))));
 color*=1.0+0.055*(noise(p*32.0)-0.5)*fine_weight;
 vec2 edge=abs(fract(p-0.5)-0.5);
 vec2 aa=max(fwidth(p),vec2(0.001));
 vec2 lines=vec2(1.0)-smoothstep(vec2(0.012),vec2(0.012)+aa,edge);
 if(grid_enabled)color=mix(color,vec3(0.62,0.71,0.49),max(lines.x,lines.y)*0.32);
 ALBEDO=color;ROUGHNESS=0.96;
}
