// View-local uniforms: disabled unless an architectural material is registered.
uniform bool occlusion_enabled = false;
uniform vec3 occlusion_focus = vec3(0.0);
uniform vec3 occlusion_camera = vec3(0.0);
uniform vec3 occlusion_right = vec3(1.0,0.0,0.0);
uniform vec3 occlusion_up = vec3(0.0,1.0,0.0);
uniform vec3 occlusion_toward_camera = vec3(0.0,0.0,1.0);
uniform float occlusion_radius = 2.2;
uniform float occlusion_strength = 1.0;
varying vec3 occlusion_world;
float occlusion_threshold(vec2 pixel) {
    ivec2 cell = ivec2(mod(floor(pixel / 2.0), 4.0));
    const float bayer[16] = float[16](0.0,8.0,2.0,10.0,12.0,4.0,14.0,6.0,3.0,11.0,1.0,9.0,15.0,7.0,13.0,5.0);
    return (bayer[cell.y * 4 + cell.x] + 0.5) / 16.0;
}
float occlusion_amount(vec3 point, vec3 render_camera) {
    // Preserve shadow passes and other cameras; the controller belongs to one view.
    if (!occlusion_enabled || distance(render_camera, occlusion_camera) > 0.05) return 0.0;
    vec3 delta = point - occlusion_focus;
    float depth = dot(delta, occlusion_toward_camera);
    float camera_depth = dot(occlusion_camera - occlusion_focus, occlusion_toward_camera);
    float in_front = smoothstep(0.08, 0.35, depth) * (1.0 - smoothstep(camera_depth - 0.1, camera_depth, depth));
    vec2 offset = vec2(dot(delta, occlusion_right), dot(delta, occlusion_up));
    float opening = 1.0 - smoothstep(occlusion_radius * 0.45, occlusion_radius, length(offset));
    return opening * in_front * occlusion_strength;
}
