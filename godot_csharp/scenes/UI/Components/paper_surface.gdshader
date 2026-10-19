shader_type canvas_item;
render_mode unshaded;
uniform float texture_strength : hint_range(0.0, 1.0) = 1.0;
void fragment() {
    vec4 paper = texture(TEXTURE, UV) * COLOR;
    float grain = fract(sin(dot(floor(FRAGCOORD.xy), vec2(12.9898, 78.233))) * 43758.5453);
    vec2 edge = min(UV, 1.0 - UV);
    float age = 1.0 - smoothstep(0.0, 0.12, min(edge.x, edge.y));
    paper.rgb *= 1.0 - texture_strength * (age * 0.065 + grain * 0.018);
    COLOR = paper;
}
