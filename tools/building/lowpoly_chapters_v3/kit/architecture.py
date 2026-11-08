"""Walnut architectural kit: real wall openings, stairs and balustrades.

Wall openings are geometric voids, not dark rectangles.  A conforming planar
strip decomposition generates watertight wall shells without Boolean context
operators.  Dimensions match openings.arch_points: width/height are the clear
opening; an arch has spring=height-rise and ten sampled half-ellipse segments.
"""
import bisect
import collections
import json
import math

from .core import group, box, mesh, beam, lathe, cylinder
from .paths import clean_polyline, offset_polyline, point3, sample_path, polyline_lengths, point_at_distance

EPS = 1e-7


def _opening(spec, length, wall_height):
    o = dict(spec)
    o['width'] = float(o['width'])
    o['height'] = float(o['height'])
    o['bottom'] = float(o.get('bottom', 0))
    o['center'] = float(o['center'])
    o['shape'] = o.get('shape', 'rect')
    if o['shape'] not in ('rect', 'arch'):
        raise ValueError('Opening shape must be rect or arch')
    if min(o['width'], o['height']) <= 0 or o['bottom'] < -EPS:
        raise ValueError('Opening dimensions must be positive and above wall base')
    if o['center']-o['width']/2 < -EPS or o['center']+o['width']/2 > length+EPS:
        raise ValueError('Opening lies outside its wall segment')
    if o['bottom']+o['height'] > wall_height+EPS:
        raise ValueError('Opening is taller than its wall')
    if o['shape'] == 'arch':
        o['rise'] = float(o.get('rise') if o.get('rise') is not None else o['width']/2)
        if not 0 < o['rise'] <= o['height']:
            raise ValueError('Arch rise must be positive and at most its opening height')
        count = max(4, int(o.get('segments', 10)))
        o['_roof'] = [(o['center']+o['width']/2*math.cos(math.pi*(1-i/count)),
                       o['bottom']+o['height']-o['rise']+o['rise']*math.sin(math.pi*(1-i/count)))
                      for i in range(count+1)]
    else:
        o['_roof'] = [(o['center']-o['width']/2, o['bottom']+o['height']),
                      (o['center']+o['width']/2, o['bottom']+o['height'])]
    return o


def _roof(o, x):
    points = o['_roof']
    index = min(len(points)-2, max(0, bisect.bisect_right([p[0] for p in points], x)-1))
    a, b = points[index:index+2]
    t = max(0.0, min(1.0, (x-a[0])/(b[0]-a[0])))
    return a[1]+t*(b[1]-a[1])


def _wall_shell(name, length, height, thickness, openings, mat, parent, base_slope=0):
    xs = sorted(set([0.0, length]+[round(p[0], 10) for o in openings for p in o['_roof']]))
    strips = []
    levels = collections.defaultdict(set)
    for x0, x1 in zip(xs, xs[1:]):
        if x1-x0 < EPS:
            continue
        xm = (x0+x1)/2
        holes = sorted((o for o in openings if o['_roof'][0][0] < xm < o['_roof'][-1][0]), key=lambda o: o['bottom'])
        cursor0 = cursor1 = 0.0
        gaps = []
        for o in holes:
            bottom = o['bottom']
            top0, top1 = _roof(o, x0), _roof(o, x1)
            if bottom < max(cursor0, cursor1)-EPS:
                raise ValueError('Overlapping wall openings need separate valid intervals: '+name)
            if bottom > min(cursor0, cursor1)+EPS:
                gaps.append((cursor0, cursor1, bottom, bottom))
            cursor0, cursor1 = top0, top1
        if height > min(cursor0, cursor1)+EPS:
            gaps.append((cursor0, cursor1, height, height))
        for lo0, lo1, hi0, hi1 in gaps:
            levels[x0].update((lo0, hi0))
            levels[x1].update((lo1, hi1))
        strips.append((x0, x1, gaps))

    vertices, front_faces, index_map = [], [], {}
    def vertex(x, z):
        key = (round(x, 8), round(z, 8))
        if key not in index_map:
            index_map[key] = len(vertices)
            vertices.append((x, -thickness/2, z+base_slope*x))
        return index_map[key]
    for x0, x1, gaps in strips:
        for lo0, lo1, hi0, hi1 in gaps:
            boundary = [(x0, lo0), (x1, lo1)]
            boundary += [(x1, z) for z in sorted(levels[x1]) if lo1+EPS < z < hi1-EPS]
            boundary += [(x1, hi1), (x0, hi0)]
            boundary += [(x0, z) for z in sorted(levels[x0], reverse=True) if lo0+EPS < z < hi0-EPS]
            clean = []
            for p in boundary:
                if not clean or math.dist(p, clean[-1]) > EPS:
                    clean.append(p)
            if len(clean) > 1 and math.dist(clean[0], clean[-1]) < EPS:
                clean.pop()
            if len(clean) < 3:
                continue
            center = vertex((x0+x1)/2, (lo0+lo1+hi0+hi1)/4)
            ids = [vertex(*p) for p in clean]
            for i in range(len(ids)):
                a, b = clean[i], clean[(i+1) % len(ids)]
                area = (a[0]-vertices[center][0])*(b[1]-(lo0+lo1+hi0+hi1)/4)-(b[0]-vertices[center][0])*(a[1]-(lo0+lo1+hi0+hi1)/4)
                if abs(area) > EPS:
                    front_faces.append((center, ids[i], ids[(i+1) % len(ids)]))
    if not front_faces:
        raise ValueError('Wall has no remaining solid area: '+name)
    edge_counts = collections.Counter()
    edge_directions = {}
    for face in front_faces:
        for a, b in zip(face, face[1:]+face[:1]):
            key = tuple(sorted((a, b)))
            edge_counts[key] += 1
            edge_directions[key] = (a, b)
    count = len(vertices)
    vertices += [(x, thickness/2, z) for x, _, z in vertices]
    faces = list(front_faces)+[tuple(i+count for i in reversed(face)) for face in front_faces]
    for edge, uses in edge_counts.items():
        if uses == 1:
            a, b = edge_directions[edge]
            faces.append((a, a+count, b+count, b))
        elif uses != 2:
            raise ValueError('Invalid wall surface adjacency: '+name)
    return mesh(name, vertices, faces, mat, parent)


def _free_spans(length, openings, low, high, pad=.065):
    cuts = []
    for o in openings:
        bottom, top = o['bottom'], o['bottom']+o['height']
        if high <= bottom or low >= top:
            continue
        half = o['width']/2
        if o['shape'] == 'arch':
            spring = top-o['rise']
            if low > spring:
                q = min(1.0, (low-spring)/o['rise'])
                half *= math.sqrt(max(0.0, 1-q*q))
        cuts.append((max(0.0, o['center']-half-pad), min(length, o['center']+half+pad)))
    cursor, spans = 0.0, []
    for a, b in sorted(cuts):
        if a > cursor+EPS:
            spans.append((cursor, a))
        cursor = max(cursor, b)
    if cursor < length-EPS:
        spans.append((cursor, length))
    return spans


def _rail_band(name, length, z, band_height, depth, thickness, openings, mat, parent):
    for j, (a, b) in enumerate(_free_spans(length, openings, z-band_height/2, z+band_height/2)):
        if b-a > .025:
            box(name+'_'+str(j), ((a+b)/2, -thickness/2-depth/2+.002, z), (b-a, depth, band_height), mat, bevel=.006, parent=parent)


def _panels(name, length, bottom, top, thickness, openings, mats, parent, target=1.0):
    height = top-bottom
    if height < .18:
        return
    for span_index, (a, b) in enumerate(_free_spans(length, openings, bottom, top, pad=.12)):
        span = b-a-.09
        if span < .20:
            continue
        n = max(1, round(span/target))
        gap, border = .055, .035
        width = (span-(n-1)*gap)/n
        for j in range(n):
            center = a+.045+j*(width+gap)+width/2
            prefix = name+'_%d_%d' % (span_index, j)
            face_y = -thickness/2-.014
            box(prefix+'_Recess', (center, face_y, (bottom+top)/2), (width, .026, height), mats['wood_dark'], bevel=.006, parent=parent)
            for side in (-1, 1):
                box(prefix+'_Stile_'+str(side), (center+side*(width-border)/2, face_y-.022, (bottom+top)/2), (border, .036, height), mats['wood_light'], bevel=.005, parent=parent)
                box(prefix+'_Rail_'+str(side), (center, face_y-.022, (bottom+top)/2+side*(height-border)/2), (width, .036, border), mats['wood_light'], bevel=.005, parent=parent)
            if width > .48 and height > .48:
                # Broad central field leaves a shallow shadowed reveal around it.
                box(prefix+'_Field', (center, face_y-.004, (bottom+top)/2), (width-.12, .025, height-.12), mats['wood'], bevel=.010, parent=parent)


def wall_path(name, points, height, thickness, mats, openings=(), panel_height=1.05, parent=None):
    """Build a paneled wall path, with interior to the right of each segment.

    openings: {segment, center, width, height, bottom=0, shape='rect'|'arch',
               rise=width/2, segments=10}.  No frames or infills are created.
    Dimensions are metres; center is measured from that segment's first node.
    The result has a JSON opening_anchors property for placing window assets.
    """
    pts = sample_path(points) if any(isinstance(p, dict) for p in points) else clean_polyline(points)
    height, thickness = float(height), float(thickness)
    if not all(math.isfinite(v) for v in (height, thickness)) or min(height, thickness) <= 0:
        raise ValueError('Wall height and thickness must be positive')
    for opening in openings:
        if not 0 <= int(opening.get('segment', 0)) < len(pts)-1:
            raise ValueError('Opening refers to an absent wall segment')
    root = group(name, parent=parent)
    root['generator'] = 'wall_path_v1'
    root['path_nodes'] = json.dumps(pts)
    anchors = []
    for i, (a, b) in enumerate(zip(pts, pts[1:])):
        dx, dy, dz = (b[j]-a[j] for j in range(3))
        length = math.hypot(dx, dy)
        if length < EPS:
            raise ValueError('Wall segments cannot be vertical')
        angle = math.atan2(dy, dx)
        segment = group(name+'_Segment_%02d' % i, location=a, rotation=angle, parent=root)
        ops = [_opening(o, length, height) for o in openings if int(o.get('segment', 0)) == i]
        _wall_shell(name+'_Solid_%02d' % i, length, height, thickness, ops, mats['wood'], segment, dz/length)
        if abs(dz) < EPS:
            _rail_band(name+'_Skirting_%02d' % i, length, .085, .17, .085, thickness, ops, mats['wood_dark'], segment)
            _rail_band(name+'_SkirtingBead_%02d' % i, length, .18, .035, .10, thickness, ops, mats['wood_light'], segment)
            _rail_band(name+'_Dado_%02d' % i, length, panel_height, .09, .12, thickness, ops, mats['wood'], segment)
            _rail_band(name+'_DadoHighlight_%02d' % i, length, panel_height+.055, .025, .145, thickness, ops, mats['wood_light'], segment)
            _panels(name+'_LowerPanel_%02d' % i, length, .235, panel_height-.11, thickness, ops, mats, segment)
            _panels(name+'_UpperPanel_%02d' % i, length, panel_height+.16, height-.35, thickness, ops, mats, segment, target=1.25)
            for layer, (z, h, depth, mat) in enumerate([
                (height-.25, .055, .10, mats['wood_light']),
                (height-.16, .12, .145, mats['wood']),
                (height-.065, .075, .205, mats['wood_light']),
                (height-.012, .04, .245, mats['wood']),
            ]):
                _rail_band(name+'_Cornice_%02d_%d' % (i, layer), length, z, h, depth, thickness, ops, mat, segment)
        for j, o in enumerate(ops):
            local_x = o['center']
            anchors.append({'segment': i, 'opening': j,
                            'location': [a[0]+local_x*dx/length, a[1]+local_x*dy/length, a[2]+local_x*dz/length+o['bottom']],
                            'rotation': angle, 'front_offset': -thickness/2,
                            'width': o['width'], 'height': o['height'], 'shape': o['shape'],
                            'rise': o.get('rise')})
    # Stately pilasters at architectural nodes, not every sample of a curve.
    closed = len(pts) > 3 and math.dist(pts[0], pts[-1]) < EPS
    pillar_points = pts[:-1] if closed else pts
    for i, p in enumerate(pillar_points):
        if i == 0 and not closed:
            direction = (pts[1][0]-p[0], pts[1][1]-p[1])
        elif i == len(pillar_points)-1 and not closed:
            direction = (p[0]-pts[i-1][0], p[1]-pts[i-1][1])
        else:
            previous = pillar_points[(i-1) % len(pillar_points)]
            following = pillar_points[(i+1) % len(pillar_points)]
            before = (p[0]-previous[0], p[1]-previous[1])
            after = (following[0]-p[0], following[1]-p[1])
            a1, a2 = math.atan2(before[1], before[0]), math.atan2(after[1], after[0])
            turn = math.atan2(math.sin(a2-a1), math.cos(a2-a1))
            if abs(turn) < math.radians(14):
                continue
            direction = (math.cos(a1)+math.cos(a2), math.sin(a1)+math.sin(a2))
        pillar = group(name+'_Pilaster_%02d' % i, p, math.atan2(direction[1], direction[0]), root)
        y = -thickness/2-.055
        box('Pilaster_Shaft', (0, y, height/2), (.245, .145, max(.1, height-.16)), mats['wood'], bevel=.016, parent=pillar)
        box('Pilaster_Foot', (0, y-.01, .13), (.35, .22, .26), mats['wood_dark'], bevel=.016, parent=pillar)
        box('Pilaster_BaseBand', (0, y-.015, .30), (.30, .205, .075), mats['wood_light'], bevel=.008, parent=pillar)
        box('Pilaster_Capital', (0, y-.025, height-.19), (.34, .235, .15), mats['wood'], bevel=.012, parent=pillar)
        box('Pilaster_Abacus', (0, y-.035, height-.065), (.43, .285, .10), mats['wood_light'], bevel=.012, parent=pillar)
    root['opening_anchors'] = json.dumps(anchors)
    return root


def _profile_prism(name, profile, half_width, mat, parent):
    """Extrude a simple XZ stair profile through Y, including real solid sides."""
    clean = []
    for p in profile:
        if not clean or math.dist(p, clean[-1]) > EPS:
            clean.append(p)
    n = len(clean)
    vertices = [(x, -half_width, z) for x, z in clean]+[(x, half_width, z) for x, z in clean]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, (i+1) % n+n, i+n) for i in range(n)]
    return mesh(name, vertices, faces, mat, parent)


def staircase(name, points, width, mats, step_rise=.18, landing_depth=None, parent=None):
    """Create flights and explicitly horizontal landings from a 3-D node path.

    A sloping segment has ceil(abs(dz)/step_rise) equal risers.  The path's
    Z coordinates are finished tread elevations.  Horizontal segments become
    platforms; landing_depth, when given, is their minimum longitudinal size.
    No rails are implicit: use offset_polyline and railing for either edge.
    """
    pts = clean_polyline(points)
    if width <= 0 or step_rise <= 0:
        raise ValueError('Stair width and riser height must be positive')
    root = group(name, parent=parent)
    root['generator'] = 'staircase_v1'
    root['path_nodes'] = json.dumps(pts)
    total_steps = 0
    cap = .055
    nosing = .025
    for index, (a, b) in enumerate(zip(pts, pts[1:])):
        dx, dy, dz = (b[j]-a[j] for j in range(3))
        run = math.hypot(dx, dy)
        if run < EPS:
            raise ValueError('Stair segments need a horizontal run')
        flight = group(name+'_Flight_%02d' % index, a, math.atan2(dy, dx), root)
        if abs(dz) < EPS:
            length = max(run, float(landing_depth)) if landing_depth is not None else run
            box('Landing_Solid', (run/2, 0, -.15), (length, width, .30), mats['wood'], bevel=.012, parent=flight)
            box('Landing_Surface', (run/2, 0, -cap/2), (length+.04, width+.07, cap), mats.get('stair_tread', mats['stone']), bevel=.012, parent=flight)
            for side in (-1, 1):
                box('Landing_Edge_'+str(side), (run/2, side*(width/2+.018), -.10), (length+.03, .065, .14), mats['wood_light'], bevel=.008, parent=flight)
            continue
        count = max(1, math.ceil(abs(dz)/step_rise))
        riser, depth = dz/count, run/count
        if depth < .12:
            raise ValueError('Stair treads are too shallow; increase run or riser height')
        waist = max(.24, abs(riser)+.07)
        profile = [(0, -cap)]
        for j in range(count):
            level = (j+1)*riser-cap
            profile.extend([(j*depth, level), ((j+1)*depth, level)])
        profile.extend([(run, dz-waist), (0, -waist)])
        _profile_prism('Stair_Solid_Sides', profile, width/2, mats['wood_dark'], flight)
        for j in range(count):
            z = (j+1)*riser
            box('Tread_%02d' % j, ((j+.5)*depth-nosing/2, 0, z-cap/2), (depth+nosing, width+.07, cap), mats.get('stair_tread', mats['stone']), bevel=.010, parent=flight)
            # A separate narrow bullnose highlight makes every riser legible.
            box('Nosing_%02d' % j, (j*depth-nosing/2, 0, z-.018), (.034, width+.09, .035), mats.get('stair_nosing', mats['stone']), bevel=.007, parent=flight)
        for side in (-1, 1):
            beam('Stringer_'+str(side), (0, side*(width/2+.022), -.12), (run, side*(width/2+.022), dz-.12), .07, .16, mats['wood'], parent=flight, bevel=.008)
        total_steps += count
    root['steps_total'] = total_steps
    root['width'] = float(width)
    root['finished_top'] = max(p[2] for p in pts)
    return root


def _handrail(name, points, width, depth, z_offset, mat, parent):
    left, right = offset_polyline(points, -width/2), offset_polyline(points, width/2)
    vertices = []
    for l, r in zip(left, right):
        vertices += [(r[0], r[1], r[2]+z_offset-depth/2),
                     (l[0], l[1], l[2]+z_offset-depth/2),
                     (l[0], l[1], l[2]+z_offset+depth/2),
                     (r[0], r[1], r[2]+z_offset+depth/2)]
    faces = [(3, 2, 1, 0), tuple(range(len(vertices)-4, len(vertices)))]
    for i in range(len(points)-1):
        for j in range(4):
            faces.append((4*i+j, 4*i+(j+1) % 4, 4*(i+1)+(j+1) % 4, 4*(i+1)+j))
    return mesh(name, vertices, faces, mat, parent, bevel=.006)


def railing(name, points, mats, height=.88, spacing=.22, parent=None, end_posts=(True, True)):
    """Follow a 3-D polyline with miter-limited rails and vertical balusters.

    Sloping balusters embed into a solid stringer instead of floating above
    stair treads.  Significant plan/grade corners receive square newel posts.
    Set either end_posts flag false when joining an existing newel, so adjoining
    railing runs do not create coincident posts.
    """
    pts = clean_polyline(points)
    if height <= .35 or spacing <= 0:
        raise ValueError('Railing needs positive spacing and a useful height')
    root = group(name, parent=parent)
    root['generator'] = 'railing_v1'
    root['path_nodes'] = json.dumps(pts)
    _handrail('Mitered_Handrail', pts, .105, .065, height, mats['wood'], root)
    _handrail('Handrail_Lower_Bead', pts, .075, .028, height-.052, mats['wood_light'], root)
    newel_indices = [0] if end_posts[0] else []
    for i in range(1, len(pts)-1):
        u = tuple(pts[i][j]-pts[i-1][j] for j in range(3))
        v = tuple(pts[i+1][j]-pts[i][j] for j in range(3))
        lu, lv = math.sqrt(sum(q*q for q in u)), math.sqrt(sum(q*q for q in v))
        angle = math.acos(max(-1.0, min(1.0, sum(x*y for x, y in zip(u, v))/(lu*lv))))
        if angle > math.radians(14):
            newel_indices.append(i)
    if end_posts[1]:
        newel_indices.append(len(pts)-1)
    lengths = polyline_lengths(pts)
    total_length = lengths[-1]
    intervals = max(1, math.ceil(total_length/spacing))
    balusters = 0
    # Allocate along the entire arc, rather than independently per sample
    # segment.  Dense Bezier samples must not make all balusters disappear.
    for j in range(1, intervals):
        distance = total_length*j/intervals
        if any(abs(distance-lengths[i]) < .10 for i in newel_indices):
            continue
        index = min(len(pts)-2, max(0, bisect.bisect_right(lengths, distance)-1))
        base = point_at_distance(pts, distance, lengths)
        embed = .19 if abs(pts[index+1][2]-pts[index][2]) > EPS else .025
        h = height+embed-.075
        profile = [(0, 0), (.037, 0), (.037, .09*h), (.022, .16*h),
                   (.019, .31*h), (.036, .41*h), (.024, .49*h),
                   (.024, .76*h), (.033, .82*h), (.023, .89*h),
                   (.023, h), (0, h)]
        lathe('Baluster_%03d' % j, profile, 6, mats['wood_light'], parent=root,
              location=(base[0], base[1], base[2]-embed))
        balusters += 1
    for i in newel_indices:
        p = pts[i]
        post = group('Newel_%02d' % i, p, parent=root)
        box('Newel_Shaft', (0, 0, height/2-.025), (.14, .14, height+.05), mats['wood'], bevel=.012, parent=post)
        box('Newel_Base', (0, 0, .075), (.205, .205, .15), mats['wood_dark'], bevel=.012, parent=post)
        box('Newel_Collar', (0, 0, height-.04), (.19, .19, .08), mats['wood_light'], bevel=.008, parent=post)
        box('Newel_Cap', (0, 0, height+.035), (.225, .225, .07), mats['wood'], bevel=.010, parent=post)
        lathe('Newel_Finial', [(0, 0), (.065, 0), (.09, .045), (.075, .11), (.035, .15), (0, .15)], 8, mats['wood_light'], parent=post, location=(0, 0, height+.07))
    root['balusters_total'] = balusters
    return root
