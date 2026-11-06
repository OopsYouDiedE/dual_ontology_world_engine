shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
uniform vec4 pigment : source_color = vec4(0.5,0.6,0.5,1.0);
uniform float surface_roughness : hint_range(0.0,1.0) = 0.8;
uniform float surface_metallic : hint_range(0.0,1.0) = 0.0;
uniform bool wood = false;
varying vec3 local_position;
void vertex() { local_position = VERTEX; }
void fragment() {
    // Broad, low-contrast painted variation survives pixel reduction; no photo micro-noise.
    float wash = sin(local_position.x*2.3+local_position.y*1.7)*sin(local_position.z*2.1);
    float grain = wood ? sin(local_position.x*5.0+sin(local_position.z*2.0)*0.5)*0.035 : 0.0;
    ALBEDO = pigment.rgb*(1.0+wash*0.025+grain);
    ROUGHNESS = surface_roughness;
    METALLIC = surface_metallic;
}
