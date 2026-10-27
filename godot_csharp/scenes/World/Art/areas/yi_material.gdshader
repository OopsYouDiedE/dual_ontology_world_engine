shader_type spatial;
render_mode diffuse_burley,specular_schlick_ggx;
#include "res://rendering/shaders/common/surface_metrics.gdshaderinc"
uniform int kind=0;
uniform vec2 dimensions=vec2(1.0);
varying vec3 p;
float hash(vec2 q){return fract(sin(dot(q,vec2(127.1,311.7)))*43758.5453);}
// All ornament repeats are measured in metres, not fitted to mesh UV counts.
float rosette(vec2 v,float radius){
 float angle=atan(v.y,v.x);
 float rim=radius*(.70+.20*cos(angle*8.0));
 float petal_distance=abs(length(v)-rim);
 float petal_aa=max(fwidth(petal_distance),.001);
 float petal=1.0-smoothstep(.003-petal_aa,.009+petal_aa,petal_distance);
 float eye_distance=length(v);
 float eye_aa=max(fwidth(eye_distance),.001);
 float eye=1.0-smoothstep(.006-eye_aa,.013+eye_aa,eye_distance);
 return max(petal,eye);
}
void vertex(){p=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
void fragment(){
 float detail_scale=surface_view_scale(p);
 float grain=surface_filtered_wave(p.x/(SURFACE_DETAIL_METERS*detail_scale)*6.2831853+sin(p.z*9.0)*2.0)*0.025;
 if(kind==0){
  vec2 cell=vec2(floor((p.x+mod(floor(p.z/.14),3.0)*.43)/1.25),floor(p.z/.14));
  vec2 f=fract(vec2((p.x+mod(cell.y,3.0)*.43)/1.25,p.z/.14));
  float edge=min(min(f.x,1.0-f.x)*1.25,min(f.y,1.0-f.y)*.14);
  float seam=surface_joint(edge,.0025);
  float variation=0.92+hash(cell)*0.16;
  ALBEDO=mix(vec3(.07,.045,.025),vec3(.245,.145,.078)*variation*(1.0+grain),1.0-seam);
  ROUGHNESS=.44+(hash(cell)-.5)*.08;
 }else if(kind==1){
  vec2 q=UV*dimensions;
  vec2 d=min(q,dimensions-q);
  float edge=min(d.x,d.y);
  float line_distance=min(abs(edge-.055),abs(edge-.12));
  float line_aa=max(fwidth(line_distance),.001);
  float line=1.0-smoothstep(.008-line_aa,.018+line_aa,line_distance);
  vec2 tile=fract(q/.20)-.5;
  float bud_distance=abs(tile.x)+abs(tile.y);
  float bud_aa=max(fwidth(bud_distance),.001);
  float bud=1.0-smoothstep(.09-bud_aa,.17+bud_aa,bud_distance);
  float edge_aa=max(fwidth(edge),.001);
  float band=smoothstep(.14-edge_aa,.14+edge_aa,edge)*(1.0-smoothstep(.28-edge_aa,.28+edge_aa,edge));
  vec2 leaf=mod(q,.20)-.10;
  float ornament=max(bud*.65,rosette(leaf,.065))*band;
  vec2 center=(UV-.5)*dimensions;
  float diamond_distance=abs(abs(center.x)+abs(center.y)-min(dimensions.x,dimensions.y)*.29);
  float diamond_aa=max(fwidth(diamond_distance),.001);
  float diamond=1.0-smoothstep(.012-diamond_aa,.026+diamond_aa,diamond_distance);
  vec2 field=mod(q+vec2(mod(floor(q.y/.34),2.0)*.17,0.0),.34)-.17;
  float flowers=rosette(field,.075)*smoothstep(.29-edge_aa,.34+edge_aa,edge);
  float vine_distance=abs(field.x-.045*sin(field.y*30.0));
  float vine_aa=max(fwidth(vine_distance),.001);
  float vines=(1.0-smoothstep(.003-vine_aa,.009+vine_aa,vine_distance))*smoothstep(.29-edge_aa,.34+edge_aa,edge);
  float weave=surface_filtered_wave(q.x/(.0035*detail_scale)*6.2831853)*surface_filtered_wave(q.y/(.0035*detail_scale)*6.2831853)*.018;
  ALBEDO=mix(vec3(.025,.10,.085),vec3(.23,.22,.105),max(flowers*.60,vines*.24));
  ALBEDO=mix(ALBEDO,vec3(.48,.32,.12),max(line,max(ornament,diamond)))+weave;
  ROUGHNESS=.96;
 }else{
  vec2 q=vec2(p.x+p.z,p.y);
  float stripe=surface_joint(abs(fract(q.x/.24)-.5)*.24,.006);
  vec2 t=fract(q/vec2(.24,.32))-.5;
  float bud_distance=abs(t.x)*.6+abs(t.y);
  float bud_aa=max(fwidth(bud_distance),.001);
  float bud=1.0-smoothstep(.045-bud_aa,.075+bud_aa,bud_distance);
  ALBEDO=mix(vec3(.025,.085,.075),vec3(.46,.32,.13),max(stripe*.45,bud));
  ROUGHNESS=.88;
 }
}
