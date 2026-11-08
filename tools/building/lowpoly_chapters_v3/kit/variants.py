"""Additional reusable opening families; independent of room composition.

All dimensions are metres. X is width, Z is height, front is -Y. Window clear
contours are CCW in XZ and are recorded for a future wall-opening generator.
Frames are closed annular meshes with real holes; grids are separate solids.
Door leaves are opaque solids under independent, correctly positioned hinges.
"""
import json
import math

from .core import group, box, mesh, beam, cylinder
from . import openings as O


def _root(name, kind, points, parent=None):
    root = group(name, parent=parent)
    root['kind'] = kind
    root['front_axis'] = '-Y'
    root['opening_profile_json'] = json.dumps(points)
    root['clear_width'] = max(p[0] for p in points) - min(p[0] for p in points)
    root['clear_height'] = max(p[1] for p in points) - min(p[1] for p in points)
    return root


def _pane(name, points, mats, root):
    pane = mesh(name + '_Glass', [(x, .035, z) for x, z in points],
                [tuple(range(len(points)))], mats['glass'], parent=root)
    if pane.data.polygons[0].normal.y > 0:
        for polygon in pane.data.polygons:
            polygon.flip()
        pane.data.update()
    return pane


def _window_frame(name, points, mats, root, frame_width):
    O._frame_band(name + '_Deep_frame', points, O._outset(points, frame_width),
                  -.11, .10, mats['wood_dark'], root, .007)
    O._frame_band(name + '_Raised_moulding', O._outset(points, frame_width * .64),
                  O._outset(points, frame_width + .024), -.16, -.098,
                  mats['wood'], root, .005)
    O._frame_band(name + '_Inner_bead', points, O._outset(points, .026),
                  -.145, -.102, mats['wood_light'], root, .003)


def rectangular_window(name, mats, width=1.8, height=2.35, cols=4, rows=5,
                       frame_width=.13, sill=True, glass=True, parent=None):
    """Grid window: one hollow rectangle, true solid bars and optional pane."""
    if min(width, height) <= .2 or frame_width <= 0:
        raise ValueError('Window width/height must exceed .2m and frame_width > 0')
    cols, rows = max(1, int(cols)), max(1, int(rows))
    frame_width = min(frame_width, width * .22)
    points = [(-width / 2, 0), (width / 2, 0),
              (width / 2, height), (-width / 2, height)]
    root = _root(name, 'rectangular_window', points, parent)
    root['cols'], root['rows'] = cols, rows
    _window_frame(name, points, mats, root, frame_width)
    if glass:
        _pane(name, points, mats, root)
    bar = min(.038, width / (cols * 10), height / (rows * 10))
    for i in range(1, cols):
        box(name + '_Mullion_%02d' % i, (-width / 2 + width * i / cols, -.065, height / 2),
            (bar, .075, height), mats['wood'], .003, root)
    for j in range(1, rows):
        box(name + '_Transom_%02d' % j, (0, -.067, height * j / rows),
            (width, .082, bar), mats['wood'], .003, root)
    if sill:
        box(name + '_Sill', (0, -.115, -.075),
            (width + 2 * frame_width + .18, .39, .12), mats['wood_light'], .014, root)
        box(name + '_Sill_apron', (0, -.055, -.17),
            (width + 2 * frame_width - .02, .18, .10), mats['wood'], .009, root)
    return root


def round_window(name, mats, radius=.95, segments=12, frame_width=.13,
                 grid='cross', glass=True, parent=None):
    """Eight/twelve-sided circular ring; cross or radial bars are real beams.

    Local bottom is Z=0; circle centre is (0, 0, radius). ``radius`` is the
    circumradius of the clear polygon. The sampled contour is shared with
    wall cutters, so the opening follows the actual faceted ring exactly.
    """
    segments = int(segments)
    if radius <= .12 or frame_width <= 0 or segments not in (8, 12):
        raise ValueError('Round window needs radius>.12, frame_width>0, segments=8 or 12')
    if grid not in ('cross', 'radial'):
        raise ValueError("grid must be 'cross' or 'radial'")
    points = [(radius * math.cos(2 * math.pi * i / segments),
               radius + radius * math.sin(2 * math.pi * i / segments))
              for i in range(segments)]
    root = _root(name, 'round_window', points, parent)
    root['radius'], root['segments'], root['grid'] = radius, segments, grid
    _window_frame(name, points, mats, root, min(frame_width, radius * .28))
    if glass:
        _pane(name, points, mats, root)
    count = 4 if grid == 'cross' else segments
    for i in range(count):
        angle = 2 * math.pi * i / count
        beam(name + '_Ray_%02d' % i, (0, -.067, radius),
             (radius * math.cos(angle), -.067, radius + radius * math.sin(angle)),
             .040, .076, mats['wood_light'], root, .003)
    # A small faceted centre boss hides the beam junction cleanly.
    boss = cylinder(name + '_Centre_boss', (0, -.08, radius), .055, .10,
                    mats['wood'], vertices=8, parent=root)
    boss.rotation_euler[0] = math.pi / 2
    return root


def _clip_x(points, boundary, keep_left):
    """Sutherland-Hodgman clipping against one vertical half-plane."""
    result = []
    inside = lambda p: p[0] <= boundary + 1e-9 if keep_left else p[0] >= boundary - 1e-9
    previous = points[-1]
    previous_inside = inside(previous)
    for current in points:
        current_inside = inside(current)
        if current_inside != previous_inside:
            t = (boundary - previous[0]) / (current[0] - previous[0])
            result.append((boundary, previous[1] + t * (current[1] - previous[1])))
        if current_inside:
            result.append(current)
        previous, previous_inside = current, current_inside
    # Exact arch apex intersections can duplicate a sampled point.
    clean = []
    for point in result:
        if not clean or math.dist(point, clean[-1]) > 1e-7:
            clean.append(point)
    if len(clean) > 1 and math.dist(clean[0], clean[-1]) < 1e-7:
        clean.pop()
    return clean


def _xz_solid(name, points, front, back, mat, parent):
    n = len(points)
    vertices = [(x, front, z) for x, z in points] + [(x, back, z) for x, z in points]
    faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))]
    faces += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    return mesh(name, vertices, faces, mat, parent=parent, bevel=.005)


def arched_closed_door(name, mats, width=2.15, height=2.95, rise=.78,
                       double=True, segments=12, frame_width=.14, parent=None):
    """Opaque arched single/double door with true leaf hinge origins.

    1. Sample the clear arch and construct a closed annular jamb.
    2. Inset its polygon by the perimeter gap. For a double door, clip at
       x=+-gap/2 to create two convex leaf silhouettes with an actual seam.
    3. Extrude each entire silhouette; no transparent arch or view to another
       room is present. Parent outline beads, panels and handles to each hinge.
    """
    if width <= .55 or height <= 1.2 or not .12 < rise < height * .70:
        raise ValueError('Invalid arched door dimensions')
    points = O.arch_points(width, height, rise, segments)
    root = _root(name, 'arched_closed_double_door' if double else 'arched_closed_door', points, parent)
    root['closed_angle'], root['arch_rise'] = 0.0, float(rise)
    root['leaf_count'], root['arc_segments'] = 2 if double else 1, int(segments)
    frame_width = min(float(frame_width), width * .16)
    O._frame_band(name + '_Arch_jamb', points, O._outset(points, frame_width),
                  -.13, .14, mats['wood_dark'], root, .008)
    O._frame_band(name + '_Arch_moulding', O._outset(points, frame_width * .70),
                  O._outset(points, frame_width + .035), -.19, -.115,
                  mats['wood'], root, .006)
    O._frame_band(name + '_Arch_inner_bead', points, O._outset(points, .028),
                  -.165, -.118, mats['wood_light'], root, .004)
    gap = .009
    perimeter = O._outset(points, -gap)
    spring = height - rise
    count = 2 if double else 1
    for number in range(count):
        left_hinged = number == 0
        direction = 1 if left_hinged else -1
        hinge_x = -width / 2 + gap if left_hinged else width / 2 - gap
        hinge_z = .006
        hinge = group(name + ('_Left_hinge' if left_hinged else '_Right_hinge'),
                      (hinge_x, 0, hinge_z), parent=root)
        hinge['kind'] = 'door_hinge'
        hinge['open_angle_degrees'] = -95.0 * direction
        outline = perimeter
        if double:
            outline = _clip_x(perimeter, -gap / 2 if left_hinged else gap / 2, left_hinged)
        local = [(x - hinge_x, z - hinge_z) for x, z in outline]
        _xz_solid(name + '_Leaf_%d' % number, local, -.04, .07, mats['wood_dark'], hinge)
        inset_outer = O._outset(local, -.035)
        inset_inner = O._outset(local, -.065)
        O._frame_band(name + '_Leaf_outline_%d' % number, inset_inner, inset_outer,
                      -.091, -.037, mats['wood_light'], hinge, .003)
        min_x, max_x = min(p[0] for p in local), max(p[0] for p in local)
        leaf_width = max_x - min_x
        cx = (min_x + max_x) / 2
        panel_width = leaf_width - .20
        for level, (bottom, top) in enumerate(((.17, spring * .42),
                                               (spring * .42 + .10, spring - .15))):
            if panel_width > .12 and top - bottom > .12:
                O._raised_panel(name + '_Panel_%d_%d' % (number, level), mats,
                                 cx, (bottom + top) / 2 - hinge_z,
                                 panel_width, top - bottom, hinge)
        handle_x = (max_x - .085) if left_hinged else (min_x + .085)
        handle_z = min(1.05, spring * .53)
        box(name + '_Handle_plate_%d' % number, (handle_x, -.13, handle_z),
            (.05, .025, .20), mats['gold'], .006, hinge)
        neck = cylinder(name + '_Handle_neck_%d' % number,
                        (handle_x, -.16, handle_z + .02), .022, .06,
                        mats['gold'], vertices=8, parent=hinge)
        neck.rotation_euler[0] = math.pi / 2
        beam(name + '_Handle_lever_%d' % number,
             (handle_x, -.20, handle_z + .02),
             (handle_x - direction * .10, -.20, handle_z + .02),
             .023, .026, mats['gold'], hinge, .004)
        for j, z in enumerate((.23, spring * .80)):
            cylinder(name + '_Hinge_barrel_%d_%d' % (number, j), (0, -.075, z),
                     .02, .14, mats['gold'], vertices=8, parent=hinge)
        group(name + '_Handle_anchor_%d' % number,
              (handle_x, -.21, handle_z), parent=hinge)
    # A small separate cap accents the arch without changing its clear contour.
    box(name + '_Keystone', (0, -.21, height + frame_width * .48),
        (.18, .12, frame_width * 1.15), mats['wood_light'], .008, root)
    group(name + '_Stand_front', (0, -.68, 0), parent=root)
    group(name + '_Passage_center', (0, 0, 0), parent=root)
    return root
