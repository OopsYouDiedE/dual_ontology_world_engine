// Stable object-space variation. Derivative attenuation avoids distant shimmer.
vec3 material_coordinates_meters(vec3 position, mat4 model_to_world) {
    // Keep grain attached to the object while accounting for instance/parent scale.
    vec3 scale = vec3(length(model_to_world[0].xyz),
                      length(model_to_world[1].xyz),
                      length(model_to_world[2].xyz));
    return position * scale;
}
float material_hash(vec3 p) {
    return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}
float material_noise(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(material_hash(i), material_hash(i + vec3(1,0,0)), f.x),
                   mix(material_hash(i + vec3(0,1,0)), material_hash(i + vec3(1,1,0)), f.x), f.y),
               mix(mix(material_hash(i + vec3(0,0,1)), material_hash(i + vec3(1,0,1)), f.x),
                   mix(material_hash(i + vec3(0,1,1)), material_hash(i + vec3(1,1,1)), f.x), f.y), f.z);
}
float filtered_wave(float phase) {
    return sin(phase) * smoothstep(2.0, 4.0, 6.28318530718 / max(fwidth(phase), 0.000001));
}
float filtered_material_noise(vec3 p) {
    float footprint = max(length(dFdx(p)), length(dFdy(p)));
    return mix(material_noise(p), 0.5, smoothstep(0.5, 1.0, footprint));
}
