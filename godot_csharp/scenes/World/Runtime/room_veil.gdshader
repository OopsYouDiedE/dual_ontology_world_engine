shader_type canvas_item;
render_mode unshaded;
uniform vec4 room_rect = vec4(0.0, 0.0, 1.0, 1.0);
uniform vec2 pixel_size = vec2(0.001);
void fragment() {
    vec2 outside = max(room_rect.xy - SCREEN_UV, SCREEN_UV - room_rect.zw);
    float distance_px = length(max(outside, vec2(0.0)) / pixel_size);
    float curtain = smoothstep(3.0, 36.0, distance_px);
    vec2 room_uv = (SCREEN_UV - room_rect.xy) / max(room_rect.zw - room_rect.xy, pixel_size);
    float vignette = smoothstep(0.24, 0.78, length(room_uv - 0.5)) * 0.16;
    COLOR = vec4(0.055, 0.063, 0.078, max(curtain, vignette));
}
