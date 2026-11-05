shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never;
uniform float darkness : hint_range(0.0,1.0)=0.66;
void fragment(){ ALBEDO=vec3(0.025,0.037,0.043); ALPHA=darkness; }
