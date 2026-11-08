"""Straight flights with explicit N-riser / (N-1)-tread semantics.

``start`` is the lower landing's nose at its finished floor elevation;
``end`` is the upper landing's nose at its finished elevation.  A flight of
10 risers over 1.5 m and 2.7 m of run therefore has 9 treads, each 0.30 m
deep and 0.15 m higher than the last.  Its last tread is at 1.35 m.  The
separately assembled upper landing supplies the tenth riser, not a tenth
tread.  Its front fascia must extend down by at least one riser height.

Geometry is local to the returned group; input points are in parent space.
No railings or platforms are implicit.  Pure ``flight_spec`` is usable
without Blender to inspect endpoint, tread and railing-anchor calculations.
"""
import json
import math

from .paths import point3

EPS = 1e-8


def _profile_mesh(profile, width):
    """A closed solid with a concave stair side profile and sloping soffit."""
    n = len(profile)
    vertices = [(x, -width / 2, z) for x, z in profile]
    vertices += [(x, width / 2, z) for x, z in profile]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
    faces += [(i, (i + 1) % n, (i + 1) % n + n, i + n)
              for i in range(n)]
    return vertices, faces


def flight_spec(start, end, width, risers=10):
    """Return deterministic dimensions and exact anchors, without bpy.

    ``rise`` is the whole flight's height; ``riser_height`` is one rise.
    Anchor coordinates are supplied both in root-local and parent space.
    ``railing_baselines`` follows first tread nose to upper landing nose;
    the lower landing nose is separately available as ``lower_landing``.
    Left/right are defined while walking uphill, viewed from above.
    """
    a, b = point3(start), point3(end)
    width = float(width)
    if isinstance(risers, bool) or int(risers) != risers or int(risers) < 2:
        raise ValueError('A flight needs an integer riser count of at least two')
    n = int(risers)
    if not math.isfinite(width) or width <= EPS:
        raise ValueError('Stair width must be finite and positive')
    dx, dy, rise = (b[i] - a[i] for i in range(3))
    run = math.hypot(dx, dy)
    if run <= EPS or rise <= EPS:
        raise ValueError('start/end must describe an ascending flight with horizontal run')
    tread_count = n - 1
    riser, tread = rise / n, run / tread_count
    cap = min(.055, riser * .4)
    waist = max(.24, riser + cap + .075)
    angle = math.atan2(dy, dx)
    ux, uy = dx / run, dy / run

    def parent_point(p):
        x, y, z = p
        return [a[0] + ux * x - uy * y,
                a[1] + uy * x + ux * y, a[2] + z]

    # Nine horizontal stone tops for ten risers.  The terminal solid stays
    # below the last tread; upper-platform geometry owns the final riser.
    profile = [(0.0, -waist), (0.0, riser - cap)]
    anchors = []
    for i in range(tread_count):
        x0, x1, z = i * tread, (i + 1) * tread, (i + 1) * riser
        profile.append((x1, z - cap))
        if i + 1 < tread_count:
            profile.append((x1, z + riser - cap))
        local = {'nose': [x0, 0.0, z], 'center': [(x0 + x1) / 2, 0.0, z],
                 'back': [x1, 0.0, z], 'left': [x0, width / 2, z],
                 'right': [x0, -width / 2, z]}
        anchors.append({'index': i + 1, 'local': local,
                        'parent': {key: parent_point(value) for key, value in local.items()}})
    profile.append((run, rise - waist))

    rails = {}
    for side, lateral in [('left', width / 2), ('right', -width / 2)]:
        local = [[0.0, lateral, riser], [run, lateral, rise]]
        rails[side] = {'local': local, 'parent': [parent_point(p) for p in local]}
    return {'start': list(a), 'end': list(b), 'width': width,
            'yaw': angle, 'risers': n, 'treads': tread_count,
            'run': run, 'rise': rise, 'riser_height': riser, 'tread': tread,
            'tread_cap': cap, 'waist': waist, 'support_profile': profile,
            'step_top_anchors': anchors, 'railing_baselines': rails,
            'lower_landing': {'local': [0.0, 0.0, 0.0], 'parent': list(a)},
            'upper_landing': {'local': [run, 0.0, rise], 'parent': list(b)},
            'upper_landing_min_fascia_height': riser,
            'last_riser_owner': 'separate_upper_landing'}


def straight_flight(name, start, end, width, mats, risers=10, parent=None):
    """Build a closed wood support and N-1 bevelled solid stone treads.

    Example: ``straight_flight('Main', (0, 0, 0), (0, 2.7, 1.5),
    3.6, mats, risers=10)``.  Assemble a landing beginning at Y=2.7,
    finished Z=1.5, and thickness >=0.15 separately.  Both connected
    branches may then begin on that landing.  JSON custom properties on
    the returned root expose dimensions and tread/rail anchors for assembly.
    """
    from .core import group, mesh, box

    spec = flight_spec(start, end, width, risers)
    root = group(name, spec['start'], spec['yaw'], parent)
    vertices, faces = _profile_mesh(spec['support_profile'], spec['width'])
    support = mesh(name + '_Closed_Wood_Support', vertices, faces,
                   mats['wood_dark'], root, bevel=min(.006, spec['riser_height'] * .04))
    support['role'] = 'closed_stair_support'
    support['static_geometry'] = True
    bevel = min(.010, spec['riser_height'] * .10, spec['tread'] * .04,
                spec['width'] * .005)
    stone = mats.get('stair_tread', mats['stone'])
    for anchor in spec['step_top_anchors']:
        x, y, z = anchor['local']['center']
        obj = box(name + '_Stone_Tread_%02d' % anchor['index'],
                  (x, y, z - spec['tread_cap'] / 2),
                  (spec['tread'], spec['width'], spec['tread_cap']),
                  stone, bevel=bevel, parent=root)
        obj['tread_index'] = anchor['index']
        obj['finished_tread_z'] = z
        obj['static_geometry'] = True
    root['generator'] = 'straight_flight_exact_v1'
    root['static_geometry'] = True
    for key in ('width', 'risers', 'treads', 'run', 'rise', 'riser_height', 'tread'):
        root[key] = spec[key]
    root['steps_total'] = spec['treads']
    root['finished_top'] = spec['end'][2]
    root['last_riser_owner'] = spec['last_riser_owner']
    root['upper_landing_min_fascia_height'] = spec['riser_height']
    root['anchor_space'] = 'local = returned root; parent = supplied start/end space'
    root['path_nodes'] = json.dumps([spec['start'], spec['end']])
    root['step_top_anchors'] = json.dumps(spec['step_top_anchors'])
    root['railing_baselines'] = json.dumps(spec['railing_baselines'])
    root['landing_anchors'] = json.dumps({key: spec[key] for key in ('lower_landing', 'upper_landing')})
    return root
