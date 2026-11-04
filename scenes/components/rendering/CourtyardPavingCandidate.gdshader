shader_type spatial;
render_mode cull_back;
uniform vec4 tint : source_color = vec4(0.65,0.60,0.47,1.0);
varying vec3 world_position;
void vertex(){ world_position=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment(){
 vec2 p=world_position.xz/vec2(0.8,0.6);
 p.x+=mod(floor(p.y),2.0)*0.5;
 vec2 id=floor(p), f=fract(p);
 float variation=fract(sin(dot(id,vec2(127.1,311.7)))*43758.5453);
 vec2 edge=min(f,1.0-f);
 float distance_to_edge=min(edge.x,edge.y);
 float aa=max(fwidth(p.x),fwidth(p.y));
 float tile=smoothstep(0.006,0.006+aa,distance_to_edge);
 ALBEDO=mix(tint.rgb*0.78,tint.rgb*(0.96+0.08*variation),tile);
 ROUGHNESS=0.72;
 SPECULAR=0.28;
}
