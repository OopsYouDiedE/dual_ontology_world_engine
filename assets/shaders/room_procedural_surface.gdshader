shader_type spatial;
render_mode diffuse_burley;

// Bind separately to imported wall and rug surface slots.
uniform vec4 base_color : source_color = vec4(0.74, 0.69, 0.57, 1.0);
uniform vec4 accent_color : source_color = vec4(0.45, 0.53, 0.37, 1.0);
uniform vec4 border_color : source_color = vec4(0.55, 0.41, 0.23, 1.0);
uniform bool rug_surface = false;
uniform float roughness_value : hint_range(0.0, 1.0) = 0.9;
uniform float grain_per_meter = 80.0;
uniform vec2 rug_repeat = vec2(12.0, 16.0);
varying vec3 surface_position;

void vertex() {
    surface_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
void fragment() {
    vec3 col = base_color.rgb;
    float grain = sin(surface_position.x * grain_per_meter) *
                  sin(surface_position.y * grain_per_meter) *
                  sin(surface_position.z * grain_per_meter);
    if (rug_surface) {
        vec2 e = min(UV, vec2(1.0) - UV);
        float edge = min(e.x, e.y);
        float border = step(0.035, edge) * (1.0 - step(0.12, edge));
        vec2 cell = abs(fract(UV * rug_repeat) - 0.5);
        float distance = abs(cell.x + cell.y - 0.29);
        float aa = max(fwidth(distance), 0.001);
        float motif = (1.0 - smoothstep(0.025-aa, 0.025+aa, distance)) * step(0.16, edge);
        col = mix(col, border_color.rgb, border);
        col = mix(col, accent_color.rgb, motif);
        grain = sin(UV.x * 2048.0) * sin(UV.y * 2048.0);
    }
    ALBEDO = col * (1.0 + grain * 0.025);
    ROUGHNESS = roughness_value;
}
