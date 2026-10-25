shader_type spatial;
uniform vec2 rug_size=vec2(2.9,1.5);
varying vec3 pos;
void vertex(){pos=VERTEX;}
void fragment(){
 vec2 p=pos.xz;
 vec2 edge=rug_size*0.5-abs(p);
 float d=min(edge.x,edge.y);
 float border=(1.0-smoothstep(0.16,0.18,d))*smoothstep(0.07,0.09,d);
 float vine=sin((p.x+p.y)*43.0+sin((p.x-p.y)*22.0));
 float ornament=border*smoothstep(0.2,0.6,vine)*0.65;
 float weave=sin(p.x*310.0)*sin(p.y*310.0)*0.012;
 ALBEDO=mix(vec3(0.055,0.15,0.11),vec3(0.38,0.29,0.13),ornament)+weave;
 ROUGHNESS=0.94;
}
