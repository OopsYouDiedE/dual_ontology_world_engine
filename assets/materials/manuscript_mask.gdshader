shader_type canvas_item;
render_mode unshaded;
// Material IDs are rendered independently of page color: R gold, G ink, B paint.
uniform bool miniature = false;
uniform bool ink_only = false;
uniform bool allow_gold = true;
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    float dark = 1.0 - smoothstep(0.22, 0.48, max(c.r, max(c.g, c.b)));
    float gold = smoothstep(0.12, 0.30, c.r - c.b) * smoothstep(0.08, 0.22, c.g - c.b);
    gold *= float(allow_gold);
    gold *= smoothstep(0.3, 0.55, c.r);
    if (miniature) {
        // Only the authored frame is gilded. Yellow scenery remains pigment.
        float edge = min(min(UV.x, UV.y), min(1.0 - UV.x, 1.0 - UV.y));
        gold *= 1.0 - step(0.065, edge);
    }
    COLOR = ink_only ? vec4(0.0, dark, 0.0, 1.0) : vec4(gold, 0.0, 1.0 - gold, c.a);
}
