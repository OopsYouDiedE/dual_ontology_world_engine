shader_type spatial;
render_mode unshaded, fog_disabled, cull_disabled, depth_draw_never, depth_test_disabled;
uniform sampler2D screen_image : hint_screen_texture, filter_nearest;
uniform sampler2D depth_image : hint_depth_texture, filter_nearest;
uniform sampler2D normals_image : hint_normal_roughness_texture, filter_nearest;
uniform vec4 ink : source_color = vec4(0.075,0.085,0.09,1.0);
uniform float line_pixels = 1.25;
float view_depth(vec2 uv, mat4 inverse_projection) {
	float d = texture(depth_image,uv).r;
	vec4 p = inverse_projection * vec4(uv*2.0-1.0,d,1.0);
	return -p.z/p.w;
}
void vertex() { POSITION=vec4(VERTEX.xy,1.0,1.0); }
void fragment() {
	vec2 pixel=line_pixels/vec2(textureSize(screen_image,0));
	float center=view_depth(SCREEN_UV,INV_PROJECTION_MATRIX);
	vec3 normal=texture(normals_image,SCREEN_UV).xyz*2.0-1.0;
	float edge=0.0;
	for(int i=0;i<4;i++) {
		vec2 offset=i==0?vec2(pixel.x,0):i==1?vec2(-pixel.x,0):i==2?vec2(0,pixel.y):vec2(0,-pixel.y);
		vec2 uv=clamp(SCREEN_UV+offset,vec2(0.001),vec2(0.999));
		float neighbor=view_depth(uv,INV_PROJECTION_MATRIX);
		vec3 other=texture(normals_image,uv).xyz*2.0-1.0;
		float silhouette=step(max(0.08,center*0.012),neighbor-center);
		float crease=step(0.25,1.0-dot(normal,other))*step(abs(neighbor-center),max(0.15,center*0.02));
		edge=max(edge,max(silhouette,crease));
	}
	ALBEDO=mix(texture(screen_image,SCREEN_UV).rgb,ink.rgb,edge*0.8);
}
