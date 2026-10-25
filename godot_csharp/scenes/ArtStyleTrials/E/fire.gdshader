shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;
uniform float activity=1.0;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
void fragment(){
	vec2 uv=UV;
	float t=TIME;
	float wave=sin(uv.y*15.0-t*5.0)*0.08+sin(uv.y*25.0+t*3.0)*0.04;
	float width=uv.y*0.38;
	float flame=1.0-smoothstep(width-0.03,width+0.03,abs(uv.x-0.5+wave));
	float broken=0.85+0.15*sin(uv.y*22.0-t*4.0);
	ALBEDO=mix(vec3(1.0,0.18,0.025),vec3(1.0,0.84,0.28),uv.y*0.8);
	EMISSION=ALBEDO*1.4*activity;
	ALPHA=flame*broken*activity;
}
