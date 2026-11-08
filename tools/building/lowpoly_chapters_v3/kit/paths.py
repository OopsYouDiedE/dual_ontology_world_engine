"""Deterministic path utilities shared by walls, stairs and balustrades.

Coordinates are local to the eventual parent, with Z up.  Positive offsets
are to the RIGHT of a directed path when viewed from above.  This module has
no Blender dependency and can also be used to prepare JSON scene recipes.
"""
import bisect
import math

EPS = 1e-9


def point3(value):
    if isinstance(value, dict):
        value = value.get('position', value.get('pos', value.get('co')))
    if value is None or len(value) < 2:
        raise ValueError('A path point needs at least x and y coordinates')
    p = (float(value[0]), float(value[1]), float(value[2]) if len(value) > 2 else 0.0)
    if not all(math.isfinite(v) for v in p):
        raise ValueError('Path coordinates must be finite')
    return p


def _sub(a, b):
    return tuple(x-y for x, y in zip(a, b))


def _length(v):
    return math.sqrt(sum(x*x for x in v))


def _unit(v):
    length = _length(v)
    if length < EPS:
        raise ValueError('Cannot normalize a zero-length path direction')
    return tuple(x/length for x in v)


def _cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def clean_polyline(points, closed=False):
    result = []
    for value in points:
        p = point3(value)
        if not result or _length(_sub(p, result[-1])) > EPS:
            result.append(p)
    if closed and len(result) > 1 and _length(_sub(result[0], result[-1])) < EPS:
        result.pop()
    if len(result) < (3 if closed else 2):
        raise ValueError('A path requires distinct points')
    return result


def sample_cubic_bezier(p0, p1, p2, p3, segments=12):
    """Return segments+1 points, including both exact end points."""
    controls = [point3(p) for p in (p0, p1, p2, p3)]
    segments = max(1, int(segments))
    result = []
    for i in range(segments+1):
        t = i/segments
        u = 1-t
        weights = (u*u*u, 3*u*u*t, 3*u*t*t, t*t*t)
        result.append(tuple(sum(weights[j]*controls[j][axis] for j in range(4)) for axis in range(3)))
    return result


sample_bezier = sample_cubic_bezier


def sample_path(nodes, curve_segments=12, closed=False):
    """Sample nodes, with optional ABSOLUTE handle_in/handle_out coordinates.

    A plain coordinate node produces a straight segment.  A node may instead
    be {'position':[x,y,z], 'handle_out':[...], 'handle_in':[...]}.  Missing
    handles use the node position.  Closed output does not repeat its start.
    """
    if isinstance(nodes, dict):
        closed = bool(nodes.get('closed', closed))
        nodes = nodes['nodes']
    if len(nodes) < 2:
        raise ValueError('A sampled path needs at least two nodes')
    result = []
    pair_count = len(nodes) if closed else len(nodes)-1
    for i in range(pair_count):
        a, b = nodes[i], nodes[(i+1) % len(nodes)]
        p0, p3 = point3(a), point3(b)
        h0 = a.get('handle_out') if isinstance(a, dict) else None
        h1 = b.get('handle_in') if isinstance(b, dict) else None
        segment = sample_cubic_bezier(p0, h0 or p0, h1 or p3, p3, curve_segments) if h0 is not None or h1 is not None else [p0, p3]
        result.extend(segment if not result else segment[1:])
    return clean_polyline(result, closed=closed)


def polyline_lengths(points, closed=False, planar=False):
    pts = clean_polyline(points, closed)
    if closed:
        pts = pts+[pts[0]]
    cumulative = [0.0]
    for a, b in zip(pts, pts[1:]):
        delta = _sub(b, a)
        length = math.hypot(delta[0], delta[1]) if planar else _length(delta)
        cumulative.append(cumulative[-1]+length)
    return cumulative


def arc_length(points, closed=False, planar=False):
    return polyline_lengths(points, closed, planar)[-1]


def _lookup(points, distance, closed=False, cumulative=None):
    pts = clean_polyline(points, closed)
    lengths = cumulative if cumulative is not None else polyline_lengths(pts, closed)
    if closed:
        pts = pts+[pts[0]]
    total = lengths[-1]
    d = float(distance) % total if closed else max(0.0, min(float(distance), total))
    i = min(len(pts)-2, max(0, bisect.bisect_right(lengths, d)-1))
    length = lengths[i+1]-lengths[i]
    t = (d-lengths[i])/length if length > EPS else 0.0
    position = tuple(a+(b-a)*t for a, b in zip(pts[i], pts[i+1]))
    return position, _unit(_sub(pts[i+1], pts[i]))


def point_at_distance(points, distance, cumulative=None, closed=False):
    return _lookup(points, distance, closed, cumulative)[0]


def frame_at_distance(points, distance, closed=False):
    """Return (position, tangent, right, up), an orthonormal 3-D frame."""
    position, tangent = _lookup(points, distance, closed)
    horizontal = math.hypot(tangent[0], tangent[1])
    right = (tangent[1]/horizontal, -tangent[0]/horizontal, 0.0) if horizontal > EPS else (1.0, 0.0, 0.0)
    up = _unit(_cross(right, tangent))
    return position, tangent, right, up


def resample_polyline(points, spacing=None, count=None, closed=False, include_end=True):
    """Resample by true 3-D arc length; keep the end of an open path.

    Exactly one of spacing/count may be supplied.  Spacing is a maximum:
    the total is split into equal intervals no longer than that value.
    A closed path has no duplicate first/last point.
    """
    pts = clean_polyline(points, closed)
    total = arc_length(pts, closed)
    if spacing is not None and count is not None:
        raise ValueError('Specify spacing OR count')
    if count is not None:
        count = max(3 if closed else 2, int(count))
        intervals = count if closed else count-1
    else:
        spacing = .2 if spacing is None else float(spacing)
        if spacing <= 0:
            raise ValueError('Path sample spacing must be positive')
        intervals = max(1, math.ceil(total/spacing))
        count = intervals if closed else intervals+1
    if not closed and not include_end:
        count -= 1
    lengths = polyline_lengths(pts, closed)
    return [point_at_distance(pts, i*total/intervals, lengths, closed) for i in range(count)]


resample = resample_polyline


def offset_polyline(points, distance, miter_limit=2.5, closed=False):
    """Horizontal right-side offset with bounded miter joins; preserve Z.

    At a near reversal, the miter is clamped instead of producing a spike.
    For very tight curves, callers must keep offset below the bend radius.
    """
    pts = clean_polyline(points, closed)
    normals = []
    for a, b in zip(pts, pts[1:]+([pts[0]] if closed else [])):
        dx, dy = b[0]-a[0], b[1]-a[1]
        length = math.hypot(dx, dy)
        if length < EPS:
            raise ValueError('Horizontal offset cannot follow a vertical segment')
        normals.append((dy/length, -dx/length))
    result = []
    for i, p in enumerate(pts):
        if not closed and i == 0:
            vx, vy = normals[0]
        elif not closed and i == len(pts)-1:
            vx, vy = normals[-1]
        else:
            a, b = normals[(i-1) % len(normals)], normals[i % len(normals)]
            vx, vy = a[0]+b[0], a[1]+b[1]
            length = math.hypot(vx, vy)
            if length < EPS:
                vx, vy = b
            else:
                vx, vy = vx/length, vy/length
                denominator = vx*a[0]+vy*a[1]
                factor = min(float(miter_limit), 1/max(EPS, denominator))
                vx, vy = vx*factor, vy*factor
        result.append((p[0]+distance*vx, p[1]+distance*vy, p[2]))
    return result


offset_path = offset_polyline
