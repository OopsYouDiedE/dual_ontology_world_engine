shader_type canvas_item;
render_mode unshaded;

// Coordinates are local to source crop [462,498,338,266]. This excludes
// the photographed pillar from the floor. Hidden ornament is not invented.
const vec2 column[24] = vec2[24](
    vec2(158, 0), vec2(179, 0), vec2(183, 15), vec2(195, 24),
    vec2(197, 44), vec2(192, 58), vec2(194, 78), vec2(216, 76),
    vec2(227, 93), vec2(220, 118), vec2(205, 123), vec2(216, 137),
    vec2(218, 154), vec2(201, 169), vec2(181, 178), vec2(148, 177),
    vec2(126, 165), vec2(116, 149), vec2(122, 129), vec2(111, 115),
    vec2(108, 85), vec2(141, 73), vec2(141, 25), vec2(154, 15)
);

void fragment() {
    vec2 p = UV * vec2(338.0, 266.0);
    vec2 ellipse = (p - vec2(169.0, 133.0)) / vec2(168.0, 131.0);
    bool blocked = false;
    int previous = 23;
    for (int current = 0; current < 24; current++) {
        vec2 a = column[current];
        vec2 b = column[previous];
        if ((a.y > p.y) != (b.y > p.y)) {
            if (p.x < (b.x-a.x) * (p.y-a.y) / (b.y-a.y) + a.x) {
                blocked = !blocked;
            }
        }
        previous = current;
    }
    COLOR = texture(TEXTURE, UV);
    if (dot(ellipse, ellipse) > 1.0 || blocked) { COLOR.a = 0.0; }
}
