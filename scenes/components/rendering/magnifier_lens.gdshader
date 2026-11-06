shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, specular_schlick_ggx, fog_disabled;

// Edge annulus adapted from Serj, MIT (2020).
// See res://scenes/components/rendering/vendor/serj_magnifier/LICENSE and SOURCE.md.
// One entrance-surface composite for the real double-convex optic. Godot's
// screen copy contains opaque geometry only, not flame/other transparent glass.
// https://docs.godotengine.org/en/4.5/tutorials/shaders/screen-reading_shaders.html
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;

// Set from the imported LensGlass surface's vertices and world transform.
uniform vec3 lens_center_world = vec3(0.0);
uniform vec3 lens_axis_world = vec3(0.0, 1.0, 0.0);
uniform float lens_radius_world = 1.0;
uniform float lens_thickness_world = 0.1;
uniform float depth_reject_margin_world = 0.01;

uniform float magnification : hint_range(1.0, 2.0) = 1.65;
uniform float refractive_index : hint_range(1.0, 1.8) = 1.52;
uniform float edge_distortion_strength : hint_range(0.0, 0.5) = 0.18;
uniform float distortion_size : hint_range(0.05, 0.9) = 0.32;
uniform float smoothing_outer : hint_range(0.01, 0.2) = 0.04;
uniform float smoothing_inner : hint_range(0.1, 1.0) = 0.75;
uniform float chromatic_spread : hint_range(0.0, 0.02) = 0.004;
uniform float glass_roughness : hint_range(0.02, 0.4) = 0.18;
uniform vec4 transmission_tint : source_color = vec4(0.985, 0.995, 0.988, 1.0);

varying vec3 surface_world;
varying vec3 surface_normal_world;

vec2 project_view_uv(vec3 point, mat4 projection) {
    vec4 clip = projection * vec4(point, 1.0);
    return clip.xy / max(clip.w, 0.00001) * 0.5 + 0.5;
}

vec3 opaque_view_position(vec2 uv, mat4 inverse_projection) {
    float depth = textureLod(depth_texture, uv, 0.0).r;
    // Godot 4.3+ reverse Z; Compatibility has a different NDC Z interval.
    #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    vec3 ndc = vec3(uv, depth) * 2.0 - 1.0;
    #else
    vec3 ndc = vec3(uv * 2.0 - 1.0, depth);
    #endif
    vec4 point = inverse_projection * vec4(ndc, 1.0);
    return point.xyz / max(point.w, 0.00001);
}

bool behind_entrance(vec2 uv, vec2 pixel_size, vec3 plane_point_view,
                     vec3 plane_normal_view, mat4 inverse_projection) {
    // Do not stretch the border texel into the lens when a sample leaves frame.
    vec2 guard = pixel_size * 1.5;
    if (any(lessThan(uv, guard)) || any(greaterThan(uv, vec2(1.0) - guard))) {
        return false;
    }
    vec3 background = opaque_view_position(uv, inverse_projection);
    float background_distance = length(background);
    vec3 ray = background / max(background_distance, 0.00001);
    float denominator = dot(ray, plane_normal_view);
    if (abs(denominator) < 0.00001) {
        return false;
    }
    // Evaluate the lens plane on the candidate ray, rather than comparing
    // unrelated screen pixels' raw depth values. Reject nearer opaque objects.
    float entrance_distance = dot(plane_point_view, plane_normal_view) / denominator;
    return entrance_distance > 0.0 && background_distance >= entrance_distance - depth_reject_margin_world;
}

void vertex() {
    surface_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    surface_normal_world = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

void fragment() {
    vec3 axis = normalize(lens_axis_world);
    vec3 world_normal = normalize(surface_normal_world);
    vec3 relative = surface_world - lens_center_world;
    float axial_height = dot(relative, axis);
    // The imported optic is closed. Its back shell must not apply a second
    // screen-space magnification, nor paint over the already composited front.
    if (dot(world_normal, axis) <= 0.015 || axial_height < -lens_thickness_world * 0.02) {
        discard;
    }
    vec3 radial = relative - axis * axial_height;
    float radius_fraction = clamp(length(radial) / max(lens_radius_world, 0.00001), 0.0, 1.0);
    float edge_join = 1.0 - smoothstep(0.965, 1.0, radius_fraction);
    vec3 center_view = (VIEW_MATRIX * vec4(lens_center_world, 1.0)).xyz;
    vec2 center_uv = project_view_uv(center_view, PROJECTION_MATRIX);
    vec2 radial_uv = SCREEN_UV - center_uv;

    // Serj's inner/outer annulus, evaluated in the physical optic plane.
    // World-space radius preserves the ellipse under the oblique camera.
    float outer_circle = 1.0 - smoothstep(1.0 - smoothing_outer, 1.0, radius_fraction);
    float smoothing = mix(1.0 - distortion_size, 1.0, smoothing_inner);
    float inner_circle = smoothstep(1.0 - distortion_size, smoothing, radius_fraction);
    float difference = outer_circle * inner_circle;
    vec2 magnified_uv = center_uv + radial_uv / magnification;
    vec2 distortion_offset = radial_uv * difference * edge_distortion_strength;
    vec2 displacement = (magnified_uv - distortion_offset - SCREEN_UV) * edge_join;

    vec2 pixel_size = 1.0 / VIEWPORT_SIZE;
    vec3 entrance_view = (VIEW_MATRIX * vec4(lens_center_world + axis * axial_height, 1.0)).xyz;
    vec3 axis_view = normalize(mat3(VIEW_MATRIX) * axis);
    vec2 sample_uv = SCREEN_UV + displacement;
    // Pull an unsafe ray back toward the current pixel, never toward a fixed
    // screen coordinate. This prevents foreground book/brass silhouettes leaking in.
    for (int attempt = 0; attempt < 4; attempt++) {
        if (behind_entrance(sample_uv, pixel_size, entrance_view, axis_view, INV_PROJECTION_MATRIX)) {
            break;
        }
        sample_uv = mix(SCREEN_UV, sample_uv, 0.5);
    }
    if (!behind_entrance(sample_uv, pixel_size, entrance_view, axis_view, INV_PROJECTION_MATRIX)) {
        sample_uv = SCREEN_UV;
    }
    vec2 dispersion = (sample_uv - SCREEN_UV) * chromatic_spread * radius_fraction * radius_fraction;
    vec2 red_uv = sample_uv + dispersion;
    vec2 blue_uv = sample_uv - dispersion;
    if (!behind_entrance(red_uv, pixel_size, entrance_view, axis_view, INV_PROJECTION_MATRIX)) {
        red_uv = sample_uv;
    }
    if (!behind_entrance(blue_uv, pixel_size, entrance_view, axis_view, INV_PROJECTION_MATRIX)) {
        blue_uv = sample_uv;
    }
    vec3 transmitted = vec3(textureLod(screen_texture, red_uv, 0.0).r,
                            textureLod(screen_texture, sample_uv, 0.0).g,
                            textureLod(screen_texture, blue_uv, 0.0).b);

    vec3 normal_view = normalize(NORMAL);
    float facing = clamp(dot(normal_view, VIEW), 0.0, 1.0);
    float f0 = pow((refractive_index - 1.0) / (refractive_index + 1.0), 2.0);
    float fresnel = f0 + (1.0 - f0) * pow(1.0 - facing, 5.0);
    NORMAL = normal_view;
    ALBEDO = vec3(0.0);
    EMISSION = transmitted * transmission_tint.rgb * (1.0 - fresnel);
    ROUGHNESS = glass_roughness;
    METALLIC = 0.0;
    SPECULAR = clamp(f0 / 0.08, 0.0, 1.0) * 0.025;
    // The captured background is already the transmitted layer. Compositing it
    // with alpha < 1 would double the unmagnified background into a ghost image.
    ALPHA = 1.0;
}
