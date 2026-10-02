"""Read-only audit of the CURRENTLY LOADED, saved island mesh.

Integration: section = audit_saved_geometry(report, ground_z)
             out['site_geometry'] = section
             out['issues'].extend(section['issues'])

No build source is executed, objects changed, scene regenerated, or blend saved.
The caller must load the NEW saved blend and its matching validation.json.
Foundation/road collision uses actual top mesh convex footprints and actual
road surface triangles. Ground contact uses the saved terrain triangle BVH.
Half-metre footprint samples are finite checks, not an all-point terrain proof.
"""
import math

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ALLOWED_BASE_PAIRS = {
    frozenset(('中央厅', '中央厅_门廊_' + str(i))) for i in range(4)
} | {frozenset(('铁工院_动力总站', '铁工院_烟囱_' + str(i))) for i in range(2)}
BODY_OBJECTS = {'中央厅': '中央厅_鼓座', '海崖信号塔': '海崖信号塔_铁架'}


def _points(ob):
    # All current island objects are parentless. Hidden collections need not
    # have evaluated matrix_world; basis is the actual transform in that case.
    matrix = ob.matrix_basis if ob.parent is None else ob.matrix_world
    return [tuple(float(q) for q in (matrix @ v.co)) for v in ob.data.vertices]


def _cross(a, b, c):
    return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])


def _hull(points):
    points = sorted(set((float(p[0]), float(p[1])) for p in points))
    if len(points) < 3:
        return points
    def half(seq):
        result = []
        for p in seq:
            while len(result) >= 2 and _cross(result[-2], result[-1], p) <= 1e-9:
                result.pop()
            result.append(p)
        return result
    return half(points)[:-1] + half(reversed(points))[:-1]


def _bounds(poly):
    return (min(p[0] for p in poly), min(p[1] for p in poly),
            max(p[0] for p in poly), max(p[1] for p in poly))


def _bounds_overlap(a, b, tolerance=0.0):
    return not (a[2] < b[0]-tolerance or b[2] < a[0]-tolerance or
                a[3] < b[1]-tolerance or b[3] < a[1]-tolerance)


def _inside(p, poly, tolerance=0.0):
    # CCW convex polygon. Negative tolerance requires positive interior.
    return all(_cross(a, b, p) >= -tolerance*math.hypot(b[0]-a[0], b[1]-a[1])
               for a, b in zip(poly, poly[1:]+poly[:1]))


def _sat(a, b, tolerance=1e-5):
    """Positive projected-area intersection; boundary touching is allowed."""
    depth = float('inf')
    for poly in (a, b):
        for p, q in zip(poly, poly[1:]+poly[:1]):
            dx, dy = q[0]-p[0], q[1]-p[1]
            length = math.hypot(dx, dy)
            if length < 1e-9:
                continue
            axis = (-dy/length, dx/length)
            aa = [v[0]*axis[0]+v[1]*axis[1] for v in a]
            bb = [v[0]*axis[0]+v[1]*axis[1] for v in b]
            overlap = min(max(aa), max(bb))-max(min(aa), min(bb))
            if overlap <= tolerance:
                return None
            depth = min(depth, overlap)
    return depth


def _footprint_samples(poly, direction=(1.0, 0.0), spacing=.5):
    """Inclusive boundary and rotated raster with <=spacing in both axes."""
    dx, dy = direction
    local = [(x*dx+y*dy, -x*dy+y*dx) for x, y in poly]
    a, b, c, d = _bounds(local)
    nx, ny = max(1, math.ceil((c-a)/spacing)), max(1, math.ceil((d-b)/spacing))
    seen = set()
    for i in range(nx+1):
        u = a+(c-a)*i/nx
        for j in range(ny+1):
            v = b+(d-b)*j/ny
            p = (u*dx-v*dy, u*dy+v*dx)
            if _inside(p, poly, 1e-6):
                seen.add(p)
    for p, q in zip(poly, poly[1:]+poly[:1]):
        count = max(1, math.ceil(math.dist(p, q)/spacing))
        seen.update((p[0]+(q[0]-p[0])*i/count, p[1]+(q[1]-p[1])*i/count)
                    for i in range(count+1))
    return sorted(seen)


def _index_list(ob, key, limit, issues):
    if key not in ob:
        issues.append('Missing '+key+': '+ob.name)
        return []
    try:
        values = [int(v) for v in ob[key]]
    except (TypeError, ValueError):
        issues.append('Invalid '+key+': '+ob.name)
        return []
    if len(values) != len(set(values)) or any(v < 0 or v >= limit for v in values):
        issues.append('Out-of-range/duplicate '+key+': '+ob.name)
        return []
    return values


def _ground_sampler():
    ob = bpy.data.objects.get('岛屿地形')
    if ob is None or ob.type != 'MESH':
        raise ValueError('Saved island terrain mesh is missing')
    points = _points(ob)
    ob.data.calc_loop_triangles()
    tree = BVHTree.FromPolygons(points, [tuple(t.vertices) for t in ob.data.loop_triangles],
                                all_triangles=True)
    def ground_z(x, y):
        hit = tree.ray_cast(Vector((x, y, 1000)), Vector((0, 0, -1)), 2000)[0]
        return float(hit.z) if hit is not None else None
    return ground_z


def audit_saved_geometry(report, ground_z=None, *, tolerance_m=.001,
                         sample_spacing_m=.5, max_base_exposure_m=1.5,
                         require_full_road_approach=False, boundary_tolerance_m=.003):
    """Return JSON-compatible details and issues; never trust report['passed'].

    ``ground_z`` may be supplied by the independent terrain audit. If omitted,
    a BVH of the loaded terrain is built here. ``require_full_road_approach``
    adds complete first-edge road coverage to the pass gate; without it missing
    samples remain visible and available road samples still gate riser height.
    Road BVH boundary misses may use a real projected triangle nearest point
    within 3mm and interpolate its actual Z, never a natural-height substitute.
    Parentless objects are audited in matrix_basis; parented objects fail closed.
    """
    ground_z = ground_z or _ground_sampler()
    out = dict(method='Saved raw world mesh vertices; terrain BVH; convex SAT; actual road surface loop triangles',
               blend=bpy.data.filepath, tolerance_m=tolerance_m,
               footprint_sample_spacing_m=sample_spacing_m,
               road_boundary_xy_tolerance_m=boundary_tolerance_m,
               foundations={}, buildings={}, entries={}, road_footprints={},
               allowed_base_overlaps=[], unexpected_base_overlaps=[], issues=[],
               limitations=[
                   'Foundation bottom contact is checked at every actual bottom vertex; faces may interpolate differently from ground between vertices.',
                   'Terrain clearance/exposure is a finite inclusive footprint raster and boundary check at at most 0.5m spacing.',
                   'Road collision is positive-area 2D SAT against the actual convex foundation footprints; it does not assert wall/roof geometry is convex.',
                   'Main wall feet are checked on the registered body mesh, before modifiers; signal tower wireframe feet are therefore checked on its source frame.',
               ])
    issues = out['issues']
    cache = {}
    support_top_trees = {}
    def g(x, y):
        key = (round(x, 7), round(y, 7))
        if key not in cache:
            value = ground_z(x, y)
            cache[key] = None if value is None else float(value)
        return cache[key]
    def mesh(name):
        ob = bpy.data.objects.get(name)
        if ob is None or ob.type != 'MESH':
            issues.append('Missing saved mesh: '+name)
            return None
        if ob.parent is not None:
            issues.append('Parented mesh requires explicit transform audit: '+name)
        return ob
    def support(name, expected_top=None, direction=(1.0, 0.0)):
        ob = mesh(name)
        if ob is None:
            return None
        pts = _points(ob)
        n = int(ob.get('support_bottom_vertex_count', 0))
        if n < 3 or len(pts) != 2*n:
            issues.append('Invalid support bottom/top index layout: '+name)
            return None
        bottom, top = pts[:n], pts[n:]
        poly = _hull(top)
        if len(poly) < 3:
            issues.append('Degenerate support footprint: '+name)
            return None
        z = sum(p[2] for p in top)/n
        plane_error = max(abs(p[2]-z) for p in top)
        pair_error = max(math.hypot(a[0]-b[0], a[1]-b[1]) for a, b in zip(bottom, top))
        ob.data.calc_loop_triangles()
        top_triangles = [tuple(t.vertices) for t in ob.data.loop_triangles if all(v >= n for v in t.vertices)]
        edge_counts = {}
        for face in ob.data.polygons:
            vv = list(face.vertices)
            for a, b in zip(vv, vv[1:]+vv[:1]):
                edge = tuple(sorted((a, b)))
                edge_counts[edge] = edge_counts.get(edge, 0)+1
        boundary_edges = sum(v == 1 for v in edge_counts.values())
        nonmanifold_edges = sum(v > 2 for v in edge_counts.values())
        if not top_triangles or boundary_edges or nonmanifold_edges:
            issues.append('Support top is missing or solid is not closed manifold: '+name)
        else:
            support_top_trees[name] = BVHTree.FromPolygons(pts, top_triangles, all_triangles=True)
        contacts, missing = [], 0
        for p in bottom:
            h = g(*p[:2])
            if h is None:
                missing += 1
            else:
                contacts.append((abs(p[2]-(h-.05)), p, h))
        worst = max(contacts, default=(float('inf'), None, None), key=lambda a:a[0])
        samples = _footprint_samples(poly, direction, sample_spacing_m)
        clearances = [(z-h, p, h) for p in samples if (h := g(*p)) is not None]
        missing += len(samples)-len(clearances)
        low = min(clearances, default=(float('-inf'), None, None), key=lambda a:a[0])
        high = max(clearances, default=(float('inf'), None, None), key=lambda a:a[0])
        result = dict(object=name, bottom_vertex_count=n, actual_vertex_count=len(pts),
                      actual_top_z=z, top_planarity_error_m=plane_error,
                      actual_top_triangle_count=len(top_triangles),
                      boundary_edges=boundary_edges, nonmanifold_edges=nonmanifold_edges,
                      paired_xy_error_m=pair_error, max_bottom_ground_minus_005_error_m=worst[0],
                      bottom_worst_xyz=worst[1], bottom_worst_ground_z=worst[2],
                      footprint_sample_count=len(samples), missing_ground_samples=missing,
                      min_top_ground_clearance_m=low[0], max_top_ground_exposure_m=high[0],
                      min_clearance_xy=low[1], max_exposure_xy=high[1],
                      expected_top_z=expected_top,
                      expected_top_error_m=abs(z-expected_top) if expected_top is not None else None)
        if worst[0] > tolerance_m or missing:
            issues.append('Support bottom does not follow saved ground minus 0.05m: '+name)
        if plane_error > tolerance_m or pair_error > tolerance_m:
            issues.append('Support top is not a paired horizontal footprint: '+name)
        if expected_top is not None and abs(z-expected_top) > tolerance_m:
            issues.append('Support top differs from registered floor: '+name)
        if abs(float(ob.get('support_top_z', z))-z) > tolerance_m:
            issues.append('Support top property differs from saved vertices: '+name)
        if low[0] < .025-tolerance_m:
            issues.append('Support/step top enters natural terrain: '+name)
        if ob.get('terrain_edited') is not False:
            issues.append('Support lacks terrain_edited=false declaration: '+name)
        return result, poly, pts, ob

    bases = {}
    for name, data in report.get('buildings', {}).items():
        expected_floor = float(data.get('floor_z', data['ground_z']))
        base_names = data.get('support_objects', [])
        if len(base_names) != 1:
            issues.append('Building must register its one actual fitted base: '+name)
            continue
        result = support(base_names[0], expected_floor)
        if result is None:
            continue
        stats, poly, pts, ob = result
        out['foundations'][name] = stats
        bases[name] = (poly, _bounds(poly), stats)
        if stats['max_top_ground_exposure_m'] > max_base_exposure_m+tolerance_m:
            issues.append('Building base exposure exceeds 1.5m: '+name)
        body = mesh(BODY_OBJECTS.get(name, name))
        if body is None:
            continue
        body_pts = _points(body)
        actual_floor = min(p[2] for p in body_pts)
        feet = [p for p in body_pts if p[2] <= actual_floor+tolerance_m]
        outside = [p for p in feet if not _inside(p[:2], poly, tolerance_m)]
        top_tree = support_top_trees.get(base_names[0])
        surface_errors, surface_misses = [], 0
        for p in feet:
            hit = None if top_tree is None else top_tree.ray_cast(
                Vector((p[0], p[1], 1000)), Vector((0, 0, -1)), 2000)[0]
            if hit is None:
                surface_misses += 1
            else:
                surface_errors.append(abs(float(hit.z)-p[2]))
        record = dict(body_object=body.name, actual_wall_floor_z=actual_floor,
                      wall_floor_to_support_error_m=abs(actual_floor-stats['actual_top_z']),
                      wall_floor_to_report_error_m=abs(actual_floor-expected_floor),
                      actual_foot_vertex_count=len(feet), unsupported_foot_vertex_count=len(outside),
                      actual_support_surface_misses=surface_misses,
                      max_foot_to_actual_support_surface_error_m=max(surface_errors, default=None),
                      first_unsupported_foot=outside[0] if outside else None)
        out['buildings'][name] = record
        if (record['wall_floor_to_support_error_m'] > tolerance_m or outside or surface_misses
                or max(surface_errors, default=float('inf')) > tolerance_m):
            issues.append('Building wall feet are not carried by its actual base: '+name)
        if record['wall_floor_to_report_error_m'] > tolerance_m:
            issues.append('Building actual wall floor differs from report: '+name)
        if body.get('site_owner') != name:
            issues.append('Building body lacks correct site_owner: '+name)

    names = list(bases)
    for i, name in enumerate(names):
        pa, ba, sa = bases[name]
        for other in names[i+1:]:
            pb, bb, sb = bases[other]
            if not _bounds_overlap(ba, bb):
                continue
            depth = _sat(pa, pb)
            if depth is None:
                continue
            record = dict(a=name, b=other, minimum_projected_overlap_m=depth,
                          floor_difference_m=abs(sa['actual_top_z']-sb['actual_top_z']))
            if frozenset((name, other)) in ALLOWED_BASE_PAIRS:
                out['allowed_base_overlaps'].append(record)
            else:
                out['unexpected_base_overlaps'].append(record)
                issues.append('Unexpected actual base footprint overlap: '+name+' / '+other)

    # Actual road surfaces: require both vertex and face index metadata, and
    # inspect every indexed surface vertex AND every projected top triangle.
    road_trees = []
    # Global 40m spatial buckets prevent a long island ring from testing every
    # one of its triangles against every campus building. This is only a broad
    # phase; actual projected triangle/base SAT remains the collision decision.
    base_buckets = {}
    for name, (poly, bounds, _) in bases.items():
        for by in range(math.floor(bounds[1]/40), math.floor(bounds[3]/40)+1):
            for bx in range(math.floor(bounds[0]/40), math.floor(bounds[2]/40)+1):
                base_buckets.setdefault((bx, by), set()).add(name)
    def nearby_bases(bounds):
        found = set()
        for by in range(math.floor(bounds[1]/40), math.floor(bounds[3]/40)+1):
            for bx in range(math.floor(bounds[0]/40), math.floor(bounds[2]/40)+1):
                found.update(base_buckets.get((bx, by), ()))
        return found
    for route_name, route in report.get('routes', {}).items():
        records = []
        geometry = route.get('geometry', {})
        for object_name in geometry.get('objects', []):
            ob = mesh(object_name)
            if ob is None:
                continue
            pts = _points(ob)
            surface = _index_list(ob, 'surface_indices', len(pts), issues)
            face_indices = _index_list(ob, 'surface_face_indices', len(ob.data.polygons), issues)
            si, fi = set(surface), set(face_indices)
            ob.data.calc_loop_triangles()
            triangles = [tuple(t.vertices) for t in ob.data.loop_triangles if t.polygon_index in fi]
            if not surface or not triangles:
                issues.append('Road has no auditable actual top surface: '+object_name)
                continue
            if any(not set(tri) <= si for tri in triangles):
                issues.append('Road surface face references a vertex outside surface_indices: '+object_name)
            surface_poly = _hull([pts[v] for v in surface])
            object_bounds = _bounds(surface_poly)
            hits = {}
            def hit_record(name):
                return hits.setdefault(name, dict(surface_vertices_inside=0,
                    first_vertex_xyz=None, surface_triangles_intersecting=0,
                    max_projected_overlap_m=0.0, first_triangle=None))
            for v in surface:
                p = pts[v]
                for name in base_buckets.get((math.floor(p[0]/40), math.floor(p[1]/40)), ()):
                    if _inside(p[:2], bases[name][0], -1e-5):
                        hit = hit_record(name)
                        hit['surface_vertices_inside'] += 1
                        if hit['first_vertex_xyz'] is None:
                            hit['first_vertex_xyz'] = p
            for tri in triangles:
                projected = [(pts[v][0], pts[v][1]) for v in tri]
                if abs(_cross(*projected)) <= 1e-9:
                    continue
                bounds = _bounds(projected)
                for name in nearby_bases(bounds):
                    poly, bb, _ = bases[name]
                    if not _bounds_overlap(bounds, bb):
                        continue
                    depth = _sat(_hull(projected), poly)
                    if depth is not None:
                        hit = hit_record(name)
                        hit['surface_triangles_intersecting'] += 1
                        hit['max_projected_overlap_m'] = max(hit['max_projected_overlap_m'], depth)
                        if hit['first_triangle'] is None:
                            hit['first_triangle'] = dict(vertex_indices=list(tri), actual_xy=projected)
            for name in hits:
                issues.append('Actual road surface crosses building base: '+object_name+' / '+name)
            tree = BVHTree.FromPolygons(pts, triangles, all_triangles=True)
            projected_tree = BVHTree.FromPolygons([(p[0], p[1], 0) for p in pts],
                                                  triangles, all_triangles=True)
            road_trees.append((object_name, object_bounds, tree, projected_tree, triangles, pts))
            records.append(dict(object=object_name, surface_vertex_count=len(surface),
                                surface_triangle_count=len(triangles), building_collisions=hits))
        out['road_footprints'][route_name] = records

    def road_top(x, y):
        heights = []
        for name, bounds, tree, projected_tree, triangles, pts in road_trees:
            if not _bounds_overlap((x, y, x, y), bounds, boundary_tolerance_m):
                continue
            p = tree.ray_cast(Vector((x, y, 1000)), Vector((0, 0, -1)), 2000)[0]
            if p is not None:
                heights.append((float(p.z), name, 0.0))
                continue
            near, normal, index, distance = projected_tree.find_nearest(
                Vector((x, y, 0)), boundary_tolerance_m)
            if near is None:
                continue
            a, b, c = (pts[v] for v in triangles[index])
            denominator = _cross(a, b, c)
            if abs(denominator) <= 1e-9:
                continue
            w0, w1 = _cross(b, c, near)/denominator, _cross(c, a, near)/denominator
            actual_z = w0*a[2]+w1*b[2]+(1-w0-w1)*c[2]
            heights.append((actual_z, name, float(distance)))
        return max(heights) if heights else None

    excluded = {'中央厅'} | {n for n in report.get('buildings', {}) if '烟囱_' in n or '线圈_' in n}
    for name in excluded & set(report.get('entries', {})):
        issues.append('Composed body/industrial attachment has independent entrance: '+name)
    expected_entries = set(report.get('buildings', {}))-excluded
    for name in expected_entries-set(report.get('entries', {})):
        issues.append('Building has no registered stair entrance: '+name)
    for name, entry in report.get('entries', {}).items():
        if name not in bases or name not in out['buildings']:
            issues.append('Entrance owner lacks an audited wall/base: '+name)
            continue
        start, door = entry['start_xy'], entry['door_xy']
        length = math.dist(start, door)
        if length <= tolerance_m:
            issues.append('Entrance has zero actual plan run: '+name)
            continue
        direction = ((start[0]-door[0])/length, (start[1]-door[1])/length)
        dx, dy = direction
        step_stats, intervals, step_polys, first_edges = [], [], [], []
        for step_name in entry.get('step_objects', []):
            result = support(step_name, direction=direction)
            if result is None:
                continue
            stats, poly, pts, ob = result
            projected = [(p[0]*dx+p[1]*dy, -p[0]*dy+p[1]*dx) for p in poly]
            u0, v0, u1, v1 = _bounds(projected)
            tread, width = u1-u0, v1-v0
            stats.update(actual_tread_m=tread, actual_width_m=width,
                         tread_property_error_m=abs(tread-float(ob.get('stair_tread_m', -1))))
            if abs(width-2) > tolerance_m or tread < .32-tolerance_m:
                issues.append('Entrance actual tread is too short or not 2m wide: '+step_name)
            if stats['tread_property_error_m'] > tolerance_m:
                issues.append('Entrance tread property disagrees with actual geometry: '+step_name)
            others = []
            for other, (other_poly, bb, _) in bases.items():
                if other == name or frozenset((name, other)) in ALLOWED_BASE_PAIRS:
                    continue
                if _bounds_overlap(_bounds(poly), bb) and _sat(poly, other_poly) is not None:
                    others.append(other)
            if others:
                issues.append('Entrance actual tread hits other building base: '+step_name+' / '+', '.join(others))
            stats['other_base_collisions'] = others
            step_stats.append(stats)
            intervals.append((u0, v0, u1, v1))
            step_polys.append(poly)
            edge_points = [p for p in poly if p[0]*dx+p[1]*dy >= u1-tolerance_m]
            first_edges.append((min(edge_points, key=lambda p:-p[0]*dy+p[1]*dx),
                                max(edge_points, key=lambda p:-p[0]*dy+p[1]*dx)))
        if not step_stats or len(step_stats) != len(entry.get('step_objects', [])):
            issues.append('Entrance has missing/unusable actual steps: '+name)
            continue
        heights = [s['actual_top_z'] for s in step_stats]
        rises = [b-a for a, b in zip(heights, heights[1:])]
        gap_errors = [abs(intervals[i][0]-intervals[i+1][2]) for i in range(len(intervals)-1)]
        alignment = [max(abs(intervals[i][1]-intervals[i+1][1]), abs(intervals[i][3]-intervals[i+1][3]))
                     for i in range(len(intervals)-1)]
        descending = bool(entry.get('descending', False))
        rise_direction = -1 if descending else 1
        if any(r*rise_direction <= 0 or abs(r) > .20+tolerance_m for r in rises):
            issues.append('Entrance actual intermediate riser exceeds 0.20m or reverses: '+name)
        if max(gap_errors+alignment, default=0) > tolerance_m:
            issues.append('Entrance actual adjacent treads gap/overlap or shift sideways: '+name)
        start_u, start_v = start[0]*dx+start[1]*dy, -start[0]*dy+start[1]*dx
        door_u = door[0]*dx+door[1]*dy
        endpoint_error = max(abs(intervals[0][2]-start_u), abs(intervals[-1][0]-door_u),
                             abs(intervals[0][1]-(start_v-1)), abs(intervals[0][3]-(start_v+1)))
        if endpoint_error > tolerance_m:
            issues.append('Entrance saved footprint differs from registered endpoints: '+name)
        first_ground, first_road, missing_road, boundary_fallbacks = [], [], [], []
        count = max(1, math.ceil(2/sample_spacing_m))
        edge_a, edge_b = first_edges[0]
        for j in range(count+1):
            # Query the actual saved first-step edge, not the higher-precision
            # JSON endpoint that may lie just outside a float32 pavement cap.
            p = (edge_a[0]+(edge_b[0]-edge_a[0])*j/count,
                 edge_a[1]+(edge_b[1]-edge_a[1])*j/count)
            h = g(*p)
            if h is None:
                issues.append('Entrance first edge has no saved terrain: '+name)
            else:
                first_ground.append(abs(heights[0]-(h+.12)))
            road = road_top(*p)
            if road is None:
                missing_road.append(p)
            else:
                first_road.append(abs(heights[0]-road[0]))
                if road[2] > 0:
                    boundary_fallbacks.append(dict(xy=p, actual_road=road[1],
                                                   xy_distance_m=road[2], actual_z=road[0]))
        if max(first_ground+first_road, default=float('inf')) > .20+tolerance_m:
            issues.append('Entrance first riser exceeds 0.20m across its actual 2m edge: '+name)
        if require_full_road_approach and missing_road:
            issues.append('Entrance first edge is not fully carried by an actual road: '+name)
        floor = out['buildings'][name]['actual_wall_floor_z']
        last = abs(heights[-1]-floor)
        if last > tolerance_m:
            issues.append('Entrance final actual top does not meet actual wall floor: '+name)
        if _sat(step_polys[-1], bases[name][0]) is None:
            issues.append('Entrance final actual tread does not reach its own base: '+name)
        # Compare each metadata riser with independent vertex-derived height.
        centre_h = g(*start)
        first_actual = heights[0]-(centre_h+.12) if centre_h is not None else None
        if descending:
            # A descending entrance begins with a landing on the native
            # approach; its nominal riser describes the following drop.
            # Verify that landing independently rather than requiring it to
            # rise by the metadata's positive riser magnitude.
            if first_actual is None or abs(first_actual) > tolerance_m:
                issues.append('Descending entrance first landing misses actual native approach: '+name)
            measured_rises = [abs(r) for r in rises]
            property_rises = ([abs(rises[0])] + measured_rises) if rises else [0.0]
        else:
            measured_rises = ([abs(first_actual)] if first_actual is not None else []) + [abs(r) for r in rises]
            property_rises = measured_rises
        riser_meta_error = max((abs(r-float(entry.get('riser_m', -1))) for r in measured_rises), default=0)
        for stats, rise in zip(step_stats, property_rises):
            ob = bpy.data.objects[stats['object']]
            if abs(rise-float(ob.get('stair_riser_m', -1))) > tolerance_m:
                issues.append('Entrance riser property disagrees with actual geometry: '+ob.name)
        if riser_meta_error > tolerance_m:
            issues.append('Entrance registered riser disagrees with actual heights: '+name)
        out['entries'][name] = dict(actual_step_count=len(step_stats), steps=step_stats,
                                   descending=descending,first_landing_native_error_m=abs(first_actual) if descending and first_actual is not None else None,
                                   actual_intermediate_rises_m=rises,
                                   max_intermediate_rise_m=max((abs(r) for r in rises), default=0),
                                   max_adjacent_xy_gap_or_shift_m=max(gap_errors+alignment, default=0),
                                   endpoint_footprint_error_m=endpoint_error,
                                   first_edge_ground_plus_012_max_rise_m=max(first_ground, default=None),
                                   first_edge_actual_road_max_rise_m=max(first_road, default=None),
                                   actual_first_edge_xy=[edge_a, edge_b],
                                   first_edge_road_boundary_fallbacks=boundary_fallbacks,
                                   first_edge_missing_road_samples=missing_road,
                                   last_step_to_actual_floor_error_m=last,
                                   riser_metadata_error_m=riser_meta_error)
    out['counts'] = dict(buildings=len(out['buildings']), foundations=len(out['foundations']),
                         entries=len(out['entries']), steps=sum(v['actual_step_count'] for v in out['entries'].values()),
                         road_routes=len(out['road_footprints']),
                         road_objects=sum(len(v) for v in out['road_footprints'].values()),
                         allowed_base_overlaps=len(out['allowed_base_overlaps']),
                         unexpected_base_overlaps=len(out['unexpected_base_overlaps']),
                         ground_bvh_queries=len(cache))
    out['passed'] = not issues
    return out
