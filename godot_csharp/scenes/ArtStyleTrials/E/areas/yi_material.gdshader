shader_type spatial;
render_mode diffuse_burley,specular_schlick_ggx;
uniform int kind=0;
uniform vec2 dimensions=vec2(1.0);
varying vec3 p;
float hash(vec2 q){return fract(sin(dot(q,vec2(127.1,311.7)))*43758.5453);}
// All ornament repeats are measured in metres, not fitted to mesh UV counts.
float rosette(vec2 v,float radius){
 float angle=atan(v.y,v.x);
 float rim=radius*(.70+.20*cos(angle*8.0));
 float petal=1.0-smoothstep(.003,.009,abs(length(v)-rim));
 float eye=1.0-smoothstep(.006,.013,length(v));
 return max(petal,eye);
}
void vertex(){p=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
void fragment(){
 float grain=sin(p.x*180.0+sin(p.z*9.0)*2.0)*0.025;
 if(kind==0){
  vec2 cell=vec2(floor((p.x+mod(floor(p.z/.14),3.0)*.43)/1.25),floor(p.z/.14));
  vec2 f=fract(vec2((p.x+mod(cell.y,3.0)*.43)/1.25,p.z/.14));
  float edge=min(min(f.x,1.0-f.x)*1.25,min(f.y,1.0-f.y)*.14);
  float seam=smoothstep(.001,.004,edge);
  ALBEDO=mix(vec3(.07,.045,.025),mix(vec3(.17,.09,.045),vec3(.32,.205,.12),hash(cell))*(1.0+grain),seam);
  ROUGHNESS=.38+hash(cell)*.12;
 }else if(kind==1){
  vec2 q=UV*dimensions;
  vec2 d=min(q,dimensions-q);
  float edge=min(d.x,d.y);
  float line=1.0-smoothstep(.008,.018,min(abs(edge-.055),abs(edge-.12)));
  vec2 tile=fract(q/.20)-.5;
  float bud=1.0-smoothstep(.09,.17,abs(tile.x)+abs(tile.y));
  float band=step(.14,edge)*(1.0-step(.28,edge));
  vec2 leaf=mod(q,.20)-.10;
  float ornament=max(bud*.65,rosette(leaf,.065))*band;
  vec2 center=(UV-.5)*dimensions;
  float diamond=1.0-smoothstep(.012,.026,abs(abs(center.x)+abs(center.y)-min(dimensions.x,dimensions.y)*.29));
  vec2 field=mod(q+vec2(mod(floor(q.y/.34),2.0)*.17,0.0),.34)-.17;
  float flowers=rosette(field,.075)*smoothstep(.29,.34,edge);
  float vines=(1.0-smoothstep(.003,.009,abs(field.x-.045*sin(field.y*30.0))))*smoothstep(.29,.34,edge);
  float weave=sin(q.x*1800.0)*sin(q.y*1800.0)*.018;
  ALBEDO=mix(vec3(.025,.10,.085),vec3(.23,.22,.105),max(flowers*.60,vines*.24));
  ALBEDO=mix(ALBEDO,vec3(.48,.32,.12),max(line,max(ornament,diamond)))+weave;
  ROUGHNESS=.96;
 }else{
  vec2 q=vec2(p.x+p.z,p.y);
  float stripe=1.0-smoothstep(.003,.008,abs(fract(q.x/.24)-.5)*.24);
  vec2 t=fract(q/vec2(.24,.32))-.5;
  float bud=1.0-smoothstep(.045,.075,abs(t.x)*.6+abs(t.y));
  ALBEDO=mix(vec3(.025,.085,.075),vec3(.46,.32,.13),max(stripe*.45,bud));
  ROUGHNESS=.88;
 }
}
