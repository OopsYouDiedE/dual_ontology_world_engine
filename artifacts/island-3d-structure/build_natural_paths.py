"""Build terrain-following paths without changing the terrain or its objects.

This module is loaded inside Blender.  ``build_natural_surface`` accepts the
result of natural_paths.route_on_natural and returns a dict containing Blender
``objects``, JSON-compatible ``stairs`` / ``route_meta`` and explicit ``blocked``
records.  It creates one mesh by concatenating closed pavement / tread solids;
it does not use operators, selection, object joining, modifiers, or terrain edits.

Top / bottom vertices and exposed risers have explicit indices.  No
surface_vertex_count is set: joined mixed geometry does not have that layout.
Terrain clearance is checked by finite samples, not an analytic all-point proof.
"""
import bisect
import json
import math

import bpy


_LIFT = .12
_EMBED = .05
_RISE = .20
_TREAD = .25
_STAIR_WIDTH = 2.0
_EPS = 1e-8


class _Blocked(ValueError):
    def __init__(self, reason, **measurements):
        super().__init__(reason)
        self.reason = reason
        self.measurements = measurements


def _distance(a, b):
    return math.hypot(b[0] - a[0], b[1] - a[1])


def _lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def _cross(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _intersects(a, b, c, d):
    """Include non-adjacent touching edges: a strip touching itself is unsafe."""
    ab_c, ab_d, cd_a, cd_b = _cross(a, b, c), _cross(a, b, d), _cross(c, d, a), _cross(c, d, b)
    if ab_c * ab_d < -1e-14 and cd_a * cd_b < -1e-14:
        return True
    def on(p, q, r, cross):
        return (abs(cross) <= 1e-8 and
                min(p[0], q[0]) - _EPS <= r[0] <= max(p[0], q[0]) + _EPS and
                min(p[1], q[1]) - _EPS <= r[1] <= max(p[1], q[1]) + _EPS)
    return on(a, b, c, ab_c) or on(a, b, d, ab_d) or on(c, d, a, cd_a) or on(c, d, b, cd_b)


class _Ground:
    def __init__(self, fn):
        self.fn = fn
        self.cache = {}

    def __call__(self, x, y):
        key = (round(x, 9), round(y, 9))
        if key not in self.cache:
            try:
                z = float(self.fn(float(x), float(y)))
            except Exception as exc:
                raise _Blocked("ground_fn could not sample a path footprint", xy=[x, y],
                               error=str(exc)) from exc
            if not math.isfinite(z):
                raise _Blocked("ground_fn returned a non-finite height", xy=[x, y])
            self.cache[key] = z
        return self.cache[key]


class _Sweep:
    """A single continuous miter strip, parameterized by centerline distance.

    Offset edges interpolate between ORIGINAL path vertices.  Recomputing a
    miter at each densified station would make the inside edge reverse near a
    corner; the continuous original strip avoids that overlap.
    """
    def __init__(self, points, width):
        self.xy = []
        for p in points:
            q = (float(p[0]), float(p[1]))
            if not all(math.isfinite(v) for v in q):
                raise _Blocked("Non-finite path coordinate")
            if not self.xy or _distance(q, self.xy[-1]) > _EPS:
                self.xy.append(q)
        if len(self.xy) < 2:
            raise _Blocked("A surface needs two distinct path points")
        self.width = width
        self.s = [0.0]
        tangents, normals = [], []
        for a, b in zip(self.xy, self.xy[1:]):
            length = _distance(a, b)
            dx, dy = (b[0] - a[0]) / length, (b[1] - a[1]) / length
            tangents.append((dx, dy))
            normals.append((-dy, dx))
            self.s.append(self.s[-1] + length)
        self.length = self.s[-1]
        self.left, self.right = [], []
        for i, point in enumerate(self.xy):
            if i == 0:
                nx, ny = normals[0]
            elif i == len(self.xy) - 1:
                nx, ny = normals[-1]
            else:
                u, v = normals[i - 1], normals[i]
                nx, ny = u[0] + v[0], u[1] + v[1]
                length = math.hypot(nx, ny)
                if length < _EPS:
                    raise _Blocked("Path reverses direction at a corner", corner_index=i)
                nx, ny = nx / length, ny / length
                denominator = nx * u[0] + ny * u[1]
                if denominator < .34:
                    raise _Blocked("Corner is too sharp for a non-overlapping miter strip",
                                   corner_index=i, miter_ratio=1 / max(denominator, _EPS))
                nx, ny = nx / denominator, ny / denominator
            offset = (nx * width / 2, ny * width / 2)
            self.left.append((point[0] + offset[0], point[1] + offset[1]))
            self.right.append((point[0] - offset[0], point[1] - offset[1]))
        self.min_side_ratio = float('inf')
        for i, direction in enumerate(tangents):
            center_length = self.s[i + 1] - self.s[i]
            for side in (self.left, self.right):
                dx, dy = side[i + 1][0] - side[i][0], side[i + 1][1] - side[i][1]
                if dx * direction[0] + dy * direction[1] <= _EPS:
                    raise _Blocked("Inside offset edge folds back at a corner", edge_index=i)
                self.min_side_ratio = min(self.min_side_ratio, math.hypot(dx, dy) / center_length)
        perimeter = self.left + list(reversed(self.right))
        count = len(perimeter)
        for i in range(count):
            for j in range(i + 1, count):
                if j == i + 1 or (i == 0 and j == count - 1):
                    continue
                if _intersects(perimeter[i], perimeter[(i + 1) % count],
                               perimeter[j], perimeter[(j + 1) % count]):
                    raise _Blocked("Offset strip intersects itself", boundary_edges=[i, j])

    def at(self, s):
        s = min(self.length, max(0.0, s))
        index = min(len(self.s) - 2, max(0, bisect.bisect_right(self.s, s) - 1))
        t = (s - self.s[index]) / (self.s[index + 1] - self.s[index])
        return (_lerp(self.xy[index], self.xy[index + 1], t),
                _lerp(self.left[index], self.left[index + 1], t),
                _lerp(self.right[index], self.right[index + 1], t))

    def rows(self, start, end, step):
        anchors = [start] + [s for s in self.s[1:-1] if start + _EPS < s < end - _EPS] + [end]
        result = []
        for a, b in zip(anchors, anchors[1:]):
            aa, bb = self.at(a), self.at(b)
            length = max(b - a, _distance(aa[1], bb[1]), _distance(aa[2], bb[2]))
            n = max(1, math.ceil(length / step))
            result.extend(a + (b - a) * i / n for i in range(n))
        result.append(end)
        return [(s, *self.at(s)) for s in result]


class _Geometry:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.materials = []
        self.surface_indices = []
        self.bottom_indices = []
        self.surface_faces = []
        self.support_faces = []
        self.riser_faces = []

    def vertex(self, xy, z, role=None):
        i = len(self.vertices)
        self.vertices.append((xy[0], xy[1], float(z)))
        if role == 'top':
            self.surface_indices.append(i)
        elif role == 'bottom':
            self.bottom_indices.append(i)
        return i

    def face(self, vertices, kind):
        index = len(self.faces)
        self.faces.append(tuple(vertices))
        self.materials.append(kind)
        if kind in ('walk', 'stairs'):
            self.surface_faces.append(index)
        elif kind == 'riser':
            self.riser_faces.append(index)
        else:
            self.support_faces.append(index)
        return index


def _grid(sweep, rows, ground, spacing):
    max_width = max(_distance(row[2], row[3]) for row in rows)
    columns = max(1, math.ceil(max_width / spacing))
    xy = [[_lerp(row[2], row[3], j / columns) for j in range(columns + 1)] for row in rows]
    z = [[ground(*p) for p in row] for row in xy]
    return xy, z


def _sample_cells(xy, z, ground):
    """Both possible diagonals are checked; the mesh uses explicit triangles."""
    minimum_clearance = float('inf')
    maximum_base_gap = -float('inf')
    minimum_ground = min(min(row) for row in z)
    maximum_ground = max(max(row) for row in z)
    for i in range(len(xy) - 1):
        for j in range(len(xy[0]) - 1):
            points = [xy[i][j], xy[i][j + 1], xy[i + 1][j + 1], xy[i + 1][j]]
            heights = [z[i][j], z[i][j + 1], z[i + 1][j + 1], z[i + 1][j]]
            for triangle in ((0, 1, 2), (0, 2, 3), (0, 1, 3), (1, 2, 3)):
                for weights in ((1/3, 1/3, 1/3), (.5, .5, 0), (0, .5, .5), (.5, 0, .5)):
                    p = tuple(sum(points[k][axis] * w for k, w in zip(triangle, weights)) for axis in (0, 1))
                    raw = ground(*p)
                    interpolated = sum(heights[k] * w for k, w in zip(triangle, weights))
                    minimum_ground = min(minimum_ground, raw)
                    maximum_ground = max(maximum_ground, raw)
                    minimum_clearance = min(minimum_clearance, interpolated + _LIFT - raw)
                    maximum_base_gap = max(maximum_base_gap, interpolated - _EMBED - raw)
    return minimum_ground, maximum_ground, minimum_clearance, maximum_base_gap


def _walk(geometry, points, width, ground):
    sweep = _Sweep(points, width)
    step = 1.0
    for refinement in range(6):
        rows = sweep.rows(0, sweep.length, step)
        xy, z = _grid(sweep, rows, ground, step)
        low, high, clearance, base_gap = _sample_cells(xy, z, ground)
        if clearance >= .03 - _EPS and base_gap <= -.005 + _EPS:
            break
        step /= 2
    else:
        raise _Blocked("Thin pavement cannot follow the sampled terrain without cutting or floating",
                       min_top_clearance=clearance, max_bottom_gap=base_gap,
                       last_sample_spacing_m=step * 2)
    tops = [[geometry.vertex(p, h + _LIFT, 'top') for p, h in zip(row, heights)]
            for row, heights in zip(xy, z)]
    bottoms = [[geometry.vertex(p, h - _EMBED, 'bottom') for p, h in zip(row, heights)]
               for row, heights in zip(xy, z)]
    for i in range(len(xy) - 1):
        for j in range(len(xy[0]) - 1):
            a, b, c, d = tops[i][j], tops[i][j + 1], tops[i + 1][j + 1], tops[i + 1][j]
            geometry.face((a, b, c), 'walk')
            geometry.face((a, c, d), 'walk')
            a, b, c, d = bottoms[i][j], bottoms[i][j + 1], bottoms[i + 1][j + 1], bottoms[i + 1][j]
            geometry.face((a, c, b), 'support')
            geometry.face((a, d, c), 'support')
        geometry.face((bottoms[i][0], tops[i][0], tops[i + 1][0], bottoms[i + 1][0]), 'support')
        geometry.face((bottoms[i][-1], bottoms[i + 1][-1], tops[i + 1][-1], tops[i][-1]), 'support')
    for j in range(len(xy[0]) - 1):
        geometry.face((bottoms[0][j], bottoms[0][j + 1], tops[0][j + 1], tops[0][j]), 'support')
        geometry.face((bottoms[-1][j], tops[-1][j], tops[-1][j + 1], bottoms[-1][j + 1]), 'support')
    maximum_grade = 0.0
    for i in range(len(xy) - 1):
        for j in range(len(xy[0])):
            length = _distance(xy[i][j], xy[i + 1][j])
            maximum_grade = max(maximum_grade, abs(z[i + 1][j] - z[i][j]) / max(length, _EPS))
    return dict(length_m=sweep.length, width_m=width, sample_step_m=step,
                sample_count=sum(map(len, xy)), min_ground=low, max_ground=high,
                min_sampled_top_clearance=clearance, max_sampled_bottom_gap=base_gap,
                max_sampled_grade_percent=100 * maximum_grade)


def _stair_plan(points, ground):
    sweep = _Sweep(points, _STAIR_WIDTH)
    maximum_count = math.floor(sweep.length * sweep.min_side_ratio / _TREAD + _EPS)
    if maximum_count < 1:
        raise _Blocked("Stair route is too short for a 0.25m tread on both sides", length_m=sweep.length)
    count = min(maximum_count, max(1, math.ceil(sweep.length / .65)))
    last_failure = {}
    while True:
        boundaries = [sweep.length * i / count for i in range(count + 1)]
        steps = []
        for start, end in zip(boundaries, boundaries[1:]):
            rows = sweep.rows(start, end, .125)
            xy, z = _grid(sweep, rows, ground, .125)
            low, high, _, bottom_gap = _sample_cells(xy, z, ground)
            left_depth = sum(_distance(a[0], b[0]) for a, b in zip(xy, xy[1:]))
            right_depth = sum(_distance(a[-1], b[-1]) for a, b in zip(xy, xy[1:]))
            steps.append(dict(start_s=start, end_s=end, rows=rows, xy=xy, z=z,
                              min_ground=low, max_ground=high, height=high + _LIFT,
                              tread=min(left_depth, right_depth), left_tread=left_depth,
                              right_tread=right_depth, max_bottom_gap=bottom_gap))
        # Least upward correction which bounds neighbouring risers.  It never
        # lowers a tread into the original ground.  Endpoint limits below stop
        # this correction from inventing an inaccessible elevated stair flight.
        for i in range(1, len(steps)):
            steps[i]['height'] = max(steps[i]['height'], steps[i - 1]['height'] - _RISE)
        for i in range(len(steps) - 2, -1, -1):
            steps[i]['height'] = max(steps[i]['height'], steps[i + 1]['height'] - _RISE)
        entry_heights = [h + _LIFT for h in steps[0]['z'][0]]
        exit_heights = [h + _LIFT for h in steps[-1]['z'][-1]]
        entry = max(abs(steps[0]['height'] - h) for h in entry_heights)
        exit_ = max(abs(steps[-1]['height'] - h) for h in exit_heights)
        rise = max([0.0] + [abs(b['height'] - a['height']) for a, b in zip(steps, steps[1:])])
        depth = min(s['tread'] for s in steps)
        bottom_gap = max(s['max_bottom_gap'] for s in steps)
        if depth >= _TREAD - _EPS and max(entry, exit_, rise) <= _RISE + _EPS and bottom_gap <= .01:
            return sweep, steps, dict(entry_max_rise=entry, exit_max_rise=exit_,
                                      max_rise=rise, min_tread=depth,
                                      sample_step_m=.125, refinement_count=count)
        last_failure = dict(entry_max_rise=entry, exit_max_rise=exit_, max_rise=rise,
                            min_tread=depth, max_bottom_gap=bottom_gap,
                            tread_count=count, minimum_tread_m=_TREAD, maximum_riser_m=_RISE)
        if count == maximum_count or depth < _TREAD - _EPS or bottom_gap > .01:
            break
        count = min(maximum_count, max(count + 1, math.ceil(count * 1.2)))
    raise _Blocked("No sampled stair partition satisfies tread, riser and endpoint limits", **last_failure)


def _stairs(geometry, points, ground):
    sweep, plans, limits = _stair_plan(points, ground)
    metadata = []
    for number, plan in enumerate(plans):
        xy, z, height = plan['xy'], plan['z'], plan['height']
        top_vertices = [[geometry.vertex(p, height, 'top') for p in row] for row in xy]
        bottom_vertices = [[geometry.vertex(p, h - _EMBED, 'bottom') for p, h in zip(row, heights)]
                           for row, heights in zip(xy, z)]
        top_faces, bottom_faces, risers = [], [], []
        for i in range(len(xy) - 1):
            for j in range(len(xy[0]) - 1):
                top_faces.append(geometry.face((top_vertices[i][j], top_vertices[i][j + 1],
                                                top_vertices[i + 1][j + 1], top_vertices[i + 1][j]), 'stairs'))
                bottom_faces.append(geometry.face((bottom_vertices[i][j], bottom_vertices[i + 1][j],
                                                   bottom_vertices[i + 1][j + 1], bottom_vertices[i][j + 1]), 'support'))
        cap_splits = []
        for end, row_index, neighbour in (
                ('start', 0, plans[number - 1]['height'] if number else None),
                ('end', -1, plans[number + 1]['height'] if number + 1 < len(plans) else None)):
            adjacent = ([neighbour] * len(xy[0]) if neighbour is not None
                        else [h + _LIFT for h in z[row_index]])
            split = [min(height, h) for h in adjacent]
            middle = [(geometry.vertex(p, h) if h < height - _EPS else top)
                      for p, h, top in zip(xy[row_index], split, top_vertices[row_index])]
            cap_splits.append((split, middle))
            for j in range(len(xy[0]) - 1):
                base_a, base_b = bottom_vertices[row_index][j:j + 2]
                mid_a, mid_b = middle[j:j + 2]
                top_a, top_b = top_vertices[row_index][j:j + 2]
                if end == 'start':
                    geometry.face((base_a, base_b, mid_b, mid_a), 'support')
                    vertices = (mid_a, mid_b, top_b, top_a)
                else:
                    geometry.face((base_a, mid_a, mid_b, base_b), 'support')
                    vertices = (mid_a, top_a, top_b, mid_b)
                if height > min(split[j], split[j + 1]) + _EPS:
                    # At an endpoint a neighbour is the actual sloping pavement
                    # edge; remove repeated vertices from a triangular riser.
                    cleaned = tuple(dict.fromkeys(vertices))
                    if len(cleaned) >= 3:
                        risers.append(geometry.face(cleaned, 'riser'))
        # Closed side walls include cap split vertices, so the exposed riser
        # edges meet them without a topological T-junction.
        for i in range(len(xy) - 1):
            for side in (0, -1):
                polygon = [bottom_vertices[i][side]]
                if i == 0 and cap_splits[0][0][side] < height - _EPS:
                    polygon.append(cap_splits[0][1][side])
                polygon += [top_vertices[i][side], top_vertices[i + 1][side]]
                if i == len(xy) - 2 and cap_splits[1][0][side] < height - _EPS:
                    polygon.append(cap_splits[1][1][side])
                polygon.append(bottom_vertices[i + 1][side])
                geometry.face(polygon if side == 0 else list(reversed(polygon)), 'support')
        center_start = sweep.at(plan['start_s'])[0]
        prior_height = plans[number - 1]['height'] if number else ground(*center_start) + _LIFT
        metadata.append(dict(index=number, start_s=plan['start_s'], end_s=plan['end_s'],
                             height=height, rise=height - prior_height,
                             tread=plan['tread'], left_tread=plan['left_tread'], right_tread=plan['right_tread'],
                             min_sampled_ground=plan['min_ground'], max_sampled_ground=plan['max_ground'],
                             min_ground_clearance=height - plan['max_ground'],
                             max_ground_clearance=height - plan['min_ground'],
                             start_xy=[list(p) for p in xy[0]], end_xy=[list(p) for p in xy[-1]],
                             top_vertex_indices=[v for row in top_vertices for v in row],
                             bottom_vertex_indices=[v for row in bottom_vertices for v in row],
                             top_face_indices=top_faces, bottom_face_indices=bottom_faces,
                             riser_face_indices=risers))
    return dict(width_m=_STAIR_WIDTH, length_m=sweep.length, tread_count=len(plans),
                steps=metadata, **limits)


def _json(value):
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False)


def build_natural_surface(name, route_result, width, ground_fn, materials, collection):
    """Return ``{objects, stairs, route_meta, blocked}`` in a Blender context.

    ``materials`` is a dict with walk/stairs/support (optional riser), or a list
    in that order.  ``collection`` is a bpy Collection; None uses scene.collection.
    Blocked segments create no substitute geometry.  Valid segments are joined
    in one mesh named ``name`` by direct data construction.  Stair solids touch
    at identical footprint boundaries; their closed contact faces remain inside
    the joined object.  This preserves the auditability of each actual tread.
    """
    width = float(width)
    if not math.isfinite(width) or width <= 0:
        raise ValueError('width must be positive and finite')
    if not callable(ground_fn):
        raise TypeError('ground_fn must be callable')
    if not isinstance(route_result, dict):
        raise TypeError('route_result must be a routing result dict')
    if isinstance(materials, dict):
        material_map = dict(materials)
    else:
        supplied = list(materials or [])
        material_map = {key: supplied[min(i, len(supplied) - 1)] if supplied else None
                        for i, key in enumerate(('walk', 'stairs', 'support'))}
    material_map.setdefault('stairs', material_map.get('walk'))
    material_map.setdefault('support', material_map.get('stairs'))
    material_map.setdefault('riser', material_map.get('stairs'))
    source = route_result.get('anchors', route_result.get('points', []))
    segments = route_result.get('segments') or [dict(kind=route_result.get('kind', 'walk'),
                                                    points=route_result.get('points', []))]
    ground = _Ground(ground_fn)
    geometry = _Geometry()
    stair_metadata, blocked, constructed = [], [], []
    for index, segment in enumerate(segments):
        kind = segment.get('kind', 'blocked')
        points = segment.get('points', [])
        if kind == 'blocked':
            blocked.append(dict(segment_index=index, kind=kind, source_points=points,
                                reason='Route planner marked this segment blocked'))
            continue
        if kind not in ('walk', 'footstairs'):
            blocked.append(dict(segment_index=index, kind=kind, source_points=points,
                                reason='Unsupported route geometry kind'))
            continue
        # Roll back a failing fragment before constructing any Blender data.
        snapshot = {key: len(value) for key, value in vars(geometry).items()}
        try:
            if kind == 'walk':
                meta = _walk(geometry, points, width, ground)
                limit=route_result.get('max_grade_percent')
                if limit is not None and meta['max_sampled_grade_percent']>limit+.01:
                    raise _Blocked('Actual walking surface exceeds the requested grade',
                        actual_grade_percent=meta['max_sampled_grade_percent'],limit_percent=limit)
            else:
                meta = _stairs(geometry, points, ground)
                meta.update(segment_index=index, leg=segment.get('leg'), kind='footstairs')
                stair_metadata.append(meta)
            constructed.append(dict(segment_index=index, kind=kind, leg=segment.get('leg'),
                                    source_points=points, statistics={k: v for k, v in meta.items() if k != 'steps'}))
        except _Blocked as exc:
            for key, length in snapshot.items():
                del getattr(geometry, key)[length:]
            blocked.append(dict(segment_index=index, kind=kind, source_points=points,
                                reason=exc.reason, measurements=exc.measurements))
    route_meta = dict(source_kind=route_result.get('kind'), kind='blocked' if blocked else
                      'footstairs' if stair_metadata else 'walk', terrain_edited=False,
                      requested_width_m=width, stair_width_m=_STAIR_WIDTH, pavement_lift_m=_LIFT,
                      bottom_embed_m=_EMBED, max_riser_m=_RISE, min_tread_m=_TREAD,
                      source_nodes=source, source_statistics=route_result.get('statistics', {}),
                      requested_max_grade_percent=route_result.get('max_grade_percent'),
                      warnings=route_result.get('warnings', []), constructed_segments=constructed,
                      blocked=blocked, ground_samples=len(ground.cache),
                      geometry_contact='Closed tread solids touch at shared footprint boundaries',
                      verification='Finite samples; terrain itself was not modified')
    objects = []
    if geometry.faces:
        mesh = bpy.data.meshes.new(str(name) + '_NaturalSurfaceMesh')
        mesh.from_pydata(geometry.vertices, [], geometry.faces)
        mesh.update()
        obj = bpy.data.objects.new(str(name), mesh)
        (collection if collection is not None else bpy.context.scene.collection).objects.link(obj)
        slots = []
        kind_slots = {}
        for key in ('walk', 'stairs', 'support', 'riser'):
            material = material_map.get(key)
            if material is not None:
                if material not in slots:
                    slots.append(material)
                    mesh.materials.append(material)
                kind_slots[key] = slots.index(material)
            else:
                kind_slots[key] = 0
        for polygon, kind in zip(mesh.polygons, geometry.materials):
            polygon.material_index = kind_slots[kind]
        obj['kind'] = route_meta['kind']
        obj['natural_surface'] = True
        obj['terrain_edited'] = False
        obj['source_nodes'] = _json(source)
        obj['route_meta'] = _json(route_meta)
        obj['stairs_meta'] = _json(stair_metadata)
        obj['surface_indices'] = geometry.surface_indices
        obj['bottom_indices'] = geometry.bottom_indices
        obj['surface_face_indices'] = geometry.surface_faces
        obj['riser_face_indices'] = geometry.riser_faces
        obj['support_face_indices'] = geometry.support_faces
        obj['material_kinds'] = _json(kind_slots)
        objects.append(obj)
        for meta in stair_metadata:
            meta['object_name'] = obj.name
        obj['stairs_meta'] = _json(stair_metadata)
    return dict(objects=objects, stairs=stair_metadata, route_meta=route_meta, blocked=blocked)
