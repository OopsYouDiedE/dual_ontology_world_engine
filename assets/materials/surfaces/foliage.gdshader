shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
uniform vec4 pigment : source_color = vec4(0.32,0.41,0.29,1.0);
uniform float wind_strength : hint_range(0.0,0.05) = 0.012;
uniform float wind_speed : hint_range(0.0,2.0) = 0.65;
void vertex(){
    float phase=dot(MODEL_MATRIX[3].xz,vec2(0.7,0.4));
    float bend=clamp(VERTEX.y,0.0,1.0);
    VERTEX.x+=sin(TIME*wind_speed+phase+VERTEX.y*1.7)*wind_strength*bend;
    VERTEX.z+=cos(TIME*wind_speed*0.7+phase)*wind_strength*0.5*bend;
}
void fragment(){
    ALBEDO=pigment.rgb;
    ROUGHNESS=0.88;
    METALLIC=0.0;
}
