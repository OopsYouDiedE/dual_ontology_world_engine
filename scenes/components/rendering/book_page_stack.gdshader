shader_type spatial;
render_mode cull_back, diffuse_burley;
varying vec3 paper_coord;
varying float sheet_tint;
varying float edge_face;
void vertex() {
    paper_coord = VERTEX;
    sheet_tint = COLOR.r;
    edge_face = COLOR.g;
}
void fragment() {
    float fiber = sin(paper_coord.x * 201.0 + paper_coord.z * 89.0) * 0.012;
    ALBEDO = mix(vec3(0.88, 0.80, 0.64), vec3(0.94, 0.88, 0.73), edge_face) * sheet_tint + fiber;
    ROUGHNESS = 0.96;
    SPECULAR = 0.08;
}

