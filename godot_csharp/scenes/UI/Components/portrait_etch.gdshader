shader_type canvas_item;
render_mode unshaded;
uniform float saturation : hint_range(0.0, 1.0) = 0.62;
uniform float etching : hint_range(0.0, 0.3) = 0.075;
void fragment() {
    vec4 portrait = texture(TEXTURE, UV);
    float luminance = dot(portrait.rgb, vec3(0.299, 0.587, 0.114));
    vec3 pigment = mix(vec3(luminance) * vec3(1.04, 1.0, 0.94), portrait.rgb, saturation);
    float strokes = step(0.58, fract((FRAGCOORD.x + FRAGCOORD.y * 1.6) / 4.0));
    float shadow = (1.0 - smoothstep(0.14, 0.72, luminance));
    pigment *= 1.0 - etching * strokes * shadow;
    pigment = (pigment - 0.5) * 1.07 + 0.49;
    COLOR = vec4(pigment, portrait.a) * COLOR;
}
