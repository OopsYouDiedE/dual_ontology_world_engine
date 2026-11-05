shader_type spatial;
render_mode cull_disabled;
uniform vec4 pigment : source_color = vec4(0.27,0.32,0.32,1.0);
void fragment(){
 vec2 t = UV / vec2(0.44,0.34);
 float row=floor(t.y);
 t.x+=mod(row,2.0)*0.5;
 vec2 f=fract(t);
 vec2 aa=max(fwidth(t),vec2(0.012));
 float seamx=1.0-smoothstep(0.018,0.018+aa.x,min(f.x,1.0-f.x));
 float seamy=1.0-smoothstep(0.028,0.028+aa.y,min(f.y,1.0-f.y));
 float variation=fract(sin(dot(floor(t),vec2(12.9898,78.233)))*43758.5453);
 ALBEDO=pigment.rgb*(0.86+variation*0.23)*(1.0-0.25*max(seamx,seamy));
 ROUGHNESS=0.87;
}
