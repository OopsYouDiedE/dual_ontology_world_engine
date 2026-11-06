// Shared metre-scale detail layers. Architectural patterns keep their dimensions.
const float SURFACE_COARSE_METERS = 0.65;
const float SURFACE_DETAIL_METERS = 0.06;
const float SURFACE_MICRO_METERS = 0.012;

float surface_pixel_footprint(vec3 metre_position) {
	return max(length(dFdx(metre_position)), length(dFdy(metre_position)));
}
float surface_detail_visibility(vec3 metre_position, float period_m) {
	float period_pixels = period_m / max(surface_pixel_footprint(metre_position), 0.000001);
	return smoothstep(2.0, 4.0, period_pixels);
}
float surface_filtered_wave(float phase) {
	float period_pixels = 6.28318530718 / max(fwidth(phase), 0.000001);
	return sin(phase) * smoothstep(2.0, 4.0, period_pixels);
}
float surface_joint(float edge_distance_m, float joint_width_m) {
	float aa = max(fwidth(edge_distance_m), 0.0003);
	float half_width = max(joint_width_m, 0.0) * 0.5;
	return 1.0 - smoothstep(half_width - aa, half_width + aa, edge_distance_m);
}
// Micro/surface detail can grow by up to 2x in distant or zoomed-out views.
// Derivatives support both orthographic zoom and perspective camera distance.
float surface_view_scale(vec3 metre_position) {
	float pixels_per_detail = SURFACE_DETAIL_METERS / max(surface_pixel_footprint(metre_position), 0.000001);
	return mix(2.0, 1.0, smoothstep(2.0, 6.0, pixels_per_detail));
}
