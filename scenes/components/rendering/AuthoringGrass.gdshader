shader_type spatial;
render_mode cull_disabled;
uniform vec3 root_color : source_color = vec3(0.30,0.40,0.19);
uniform vec3 tip_color : source_color = vec3(0.48,0.56,0.28);
uniform vec2 wind_direction = vec2(0.8,0.6);
uniform float wind_strength = 0.055;
uniform float fade_start = 70.0;
uniform float fade_end = 100.0;
varying float blade_height;
varying float variation;
void vertex(){
 vec3 root=MODEL_MATRIX[3].xyz;
 blade_height=UV.y;
 variation=fract(sin(dot(root.xz,vec2(12.9898,78.233)))*43758.5453);
 float gust=0.55+0.3*sin(dot(root.xz,vec2(0.37,0.21))-TIME*1.4)+0.15*sin(dot(root.xz,vec2(1.1,-0.7))-TIME*2.2);
 vec2 wind=normalize(wind_direction+vec2(0.0001))*wind_strength*gust*blade_height*blade_height;
 VERTEX+=(inverse(MODEL_MATRIX)*vec4(wind.x,0.0,wind.y,0.0)).xyz;
 float camera_distance=PROJECTION_MATRIX[3][3]>0.5?length((VIEW_MATRIX*vec4(root,1.0)).xy):distance(CAMERA_POSITION_WORLD,root);
 float fade=1.0-smoothstep(fade_start,fade_end,camera_distance);
 VERTEX*=fade;
 NORMAL=vec3(0,1,0);
}
void fragment(){NORMAL=normalize((VIEW_MATRIX*vec4(0,1,0,0)).xyz);ALBEDO=mix(root_color,tip_color,blade_height)*(0.91+variation*0.13);ROUGHNESS=0.95;}
