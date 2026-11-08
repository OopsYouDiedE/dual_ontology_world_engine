"""Faceted architectural openings, expressed in metres.

Local X is width, Z is height, and the room-facing side is -Y. ``width``
and ``height`` describe the CLEAR wall opening, whose bottom is local Z=0.
The frame extends outside this contour so it overlaps the surrounding wall.
``arch_points`` is also the wall cutter's profile contract.
"""
import math

from .core import group, box, mesh, cylinder, beam


def arch_points(width, height, rise=None, segments=10):
    """CCW (X,Z) clear-opening polygon; top is a faceted elliptical arch."""
    width, height = float(width), float(height)
    rise = width / 2 if rise is None else float(rise)
    segments = max(4, int(segments))
    if width <= 0 or height <= 0 or not 0 < rise < height:
        raise ValueError('An arch needs width>0 and 0<rise<height')
    half = width / 2
    spring = height - rise
    points = [(-half, 0.0), (half, 0.0)]
    for j in range(segments + 1):
        angle = math.pi * j / segments
        points.append((half * math.cos(angle), spring + rise * math.sin(angle)))
    return points


def _outset(points, distance):
    """Miter offset of a convex CCW contour, preserving its sample indices."""
    result = []
    for i, (x, z) in enumerate(points):
        px, pz = points[(i - 1) % len(points)]
        nx, nz = points[(i + 1) % len(points)]
        e0 = (x - px, z - pz)
        e1 = (nx - x, nz - z)
        l0, l1 = math.hypot(*e0), math.hypot(*e1)
        if min(l0, l1) < 1e-9:
            raise ValueError('Repeated contour vertex')
        n0 = (e0[1] / l0, -e0[0] / l0)
        n1 = (e1[1] / l1, -e1[0] / l1)
        denominator = 1 + n0[0] * n1[0] + n0[1] * n1[1]
        if denominator < 1e-5:
            raise ValueError('Contour has an excessively sharp corner')
        k = distance / denominator
        result.append((x + k * (n0[0] + n1[0]),
                       z + k * (n0[1] + n1[1])))
    return result


def _frame_band(name, inner, outer, front, back, mat, parent, bevel=0.006):
    """Closed annular extrusion with actual empty space through its centre."""
    n = len(inner)
    if n != len(outer):
        raise ValueError('Frame contours require matching samples')
    vertices = ([(x, front, z) for x, z in outer] +
                [(x, back, z) for x, z in outer] +
                [(x, front, z) for x, z in inner] +
                [(x, back, z) for x, z in inner])
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces += [(i, j, 2*n+j, 2*n+i),
                  (n+j, n+i, 3*n+i, 3*n+j),
                  (i, n+i, n+j, j),
                  (2*n+i, 2*n+j, 3*n+j, 3*n+i)]
    return mesh(name, vertices, faces, mat, parent=parent, bevel=bevel)


def arched_window(name, mats, width=1.4, height=3.0, rise=None,
                  cols=4, rows=5, parent=None, segments=10,
                  frame_width=0.12, glass=True):
    """Detailed fixed window; place the returned Empty at the wall opening."""
    points = arch_points(width, height, rise, segments)
    rise = width/2 if rise is None else float(rise)
    spring = height-rise
    cols, rows = max(1, int(cols)), max(1, int(rows))
    root = group(name, parent=parent)
    root['kind'] = 'arched_window'
    root['clear_width'] = width
    root['clear_height'] = height
    root['arch_rise'] = rise
    root['arc_segments'] = int(segments)
    frame_width = min(float(frame_width), width*0.22)
    outside = _outset(points, frame_width)
    _frame_band(name+'_Deep_frame', points, outside, -.11, .095,
                mats['wood_dark'], root, .008)
    _frame_band(name+'_Raised_moulding', _outset(points, frame_width*.68),
                _outset(points, frame_width+.025), -.155, -.095,
                mats['wood'], root, .006)
    _frame_band(name+'_Inner_bead', points, _outset(points, .024),
                -.145, -.10, mats['wood_light'], root, .004)

    if glass:
        # CCW in XZ faces the room (-Y); material handles the sun's shadow rays.
        pane = mesh(name+'_Glass', [(x, .035, z) for x, z in points],
                    [tuple(range(len(points)))], mats.get('glass', mats['cream']),
                    parent=root)
        # Recalculating an open sheet has no unambiguous outward direction.
        # Enforce its authored room-facing normal after the shared mesh helper.
        if pane.data.polygons[0].normal.y > 0:
            for polygon in pane.data.polygons:
                polygon.flip()
            pane.data.update()

    bar = min(.035, width / (cols*11))
    depth = .068
    y = -.065
    for i in range(1, cols):
        x = -width/2 + width*i/cols
        box(name+'_Mullion_'+str(i), (x, y, spring/2),
            (bar, depth, spring), mats['wood'], bevel=.004, parent=root)
    for j in range(1, rows+1):
        z = spring*j/rows
        box(name+'_Transom_'+str(j), (0, y-.002, z),
            (width, depth+.008, bar), mats['wood'], bevel=.004, parent=root)
    # The straight grid ends at the spring line; the arched light has fan rays.
    for j in range(1, max(3, cols+1)):
        angle = math.pi*j/max(3, cols+1)
        beam(name+'_Fan_ray_'+str(j), (0, y, spring),
             (width/2*math.cos(angle), y, spring+rise*math.sin(angle)),
             bar*.84, depth, mats['wood_light'], parent=root, bevel=.003)

    box(name+'_Sill', (0, -.115, -.075),
        (width+2*frame_width+.18, .39, .12), mats['wood_light'],
        bevel=.016, parent=root)
    box(name+'_Sill_apron', (0, -.055, -.17),
        (width+2*frame_width-.02, .18, .10), mats['wood'],
        bevel=.010, parent=root)
    return root


def _raised_panel(name, mats, x, z, width, height, parent):
    """Opaque inset centre and four projecting, bevelled moulding strips."""
    moulding = min(.034, width*.11, height*.11)
    box(name+'_Field', (x, -.058, z), (width, .026, height),
        mats['wood'], bevel=.007, parent=parent)
    for sign in (-1, 1):
        box(name+'_Upright_'+str(sign),
            (x+sign*(width-moulding)/2, -.084, z),
            (moulding, .035, height), mats['wood_light'],
            bevel=.004, parent=parent)
        box(name+'_Rail_'+str(sign),
            (x, -.084, z+sign*(height-moulding)/2),
            (max(.005, width-2*moulding), .035, moulding),
            mats['wood_light'], bevel=.004, parent=parent)


def closed_door(name, mats, width=1.25, height=2.65, double=False,
                transom=False, parent=None):
    """Opaque panel door. Hinges are separate Empty nodes, closed initially.

    Width/height are the wall opening. A fixed transom, when requested, takes
    its height from the upper part of that opening. The root's bottom is Z=0.
    """
    if width <= .35 or height <= .8:
        raise ValueError('Door opening is too small')
    root = group(name, parent=parent)
    root['kind'] = 'closed_double_door' if double else 'closed_door'
    root['clear_width'], root['clear_height'] = width, height
    root['closed_angle'] = 0.0
    fw = min(.14, width*.11)
    for side in (-1, 1):
        box(name+'_Jamb_'+str(side), (side*(width+fw)/2, -.04, height/2),
            (fw, .25, height), mats['wood_dark'], bevel=.012, parent=root)
        box(name+'_Jamb_moulding_'+str(side),
            (side*(width+fw)/2, -.18, height/2),
            (fw*.76, .055, height+.025), mats['wood_light'],
            bevel=.008, parent=root)
    box(name+'_Lintel', (0, -.04, height+fw/2),
        (width+2*fw, .27, fw), mats['wood_dark'], bevel=.012, parent=root)
    box(name+'_Cornice', (0, -.095, height+fw+.05),
        (width+2*fw+.15, .36, .10), mats['wood_light'], bevel=.012, parent=root)

    transom_h = min(.50, height*.18) if transom else 0.0
    leaf_h = height-transom_h-(.075 if transom else 0)-.012
    if transom:
        z0 = leaf_h+.075
        box(name+'_Fixed_transom_back', (0, .035, (z0+height)/2),
            (width, .055, height-z0), mats['wood_dark'], bevel=.002, parent=root)
        n = max(3, round(width/.23))
        gap = .032
        cell_w = (width-(n+1)*gap)/n
        for i in range(n):
            cx = -width/2+gap+cell_w/2+i*(cell_w+gap)
            box(name+'_Transom_inset_'+str(i), (cx, -.007, (z0+height)/2),
                (cell_w, .018, max(.02, height-z0-.07)), mats['cream'],
                bevel=.003, parent=root)
        box(name+'_Transom_crossrail', (0, -.075, leaf_h+.038),
            (width+.02, .16, .075), mats['wood_light'],
            bevel=.008, parent=root)

    count = 2 if double else 1
    gap = .009
    leaf_w = (width-(count+1)*gap)/count
    for number in range(count):
        left_hinged = number == 0
        direction = 1 if left_hinged else -1
        hinge_x = -width/2+gap if left_hinged else width/2-gap
        leaf = group(name+('_Left_hinge' if left_hinged else '_Right_hinge'),
                     (hinge_x, 0, .006), parent=root)
        leaf['kind'] = 'door_hinge'
        leaf['open_angle_degrees'] = -95*direction
        cx = direction*leaf_w/2
        box(name+'_Leaf_'+str(number), (cx, .005, leaf_h/2),
            (leaf_w, .11, leaf_h), mats['wood_dark'], bevel=.008, parent=leaf)
        rail = min(.095, leaf_w*.13)
        for edge in (-1, 1):
            box(name+'_Leaf_stile_'+str(number)+'_'+str(edge),
                (cx+edge*(leaf_w-rail)/2, -.073, leaf_h/2),
                (rail, .06, leaf_h), mats['wood'], bevel=.007, parent=leaf)
        for z in (rail/2, leaf_h*.34, leaf_h-rail/2):
            box(name+'_Leaf_crossrail_'+str(number)+'_'+str(round(z, 3)),
                (cx, -.073, z), (leaf_w-2*rail, .06, rail),
                mats['wood'], bevel=.007, parent=leaf)
        inner_w = leaf_w-2*rail-.025
        lower_bottom, lower_top = rail+.02, leaf_h*.34-rail/2-.02
        upper_bottom, upper_top = leaf_h*.34+rail/2+.02, leaf_h-rail-.02
        for index, (a, b) in enumerate(((lower_bottom, lower_top),
                                       (upper_bottom, upper_top))):
            _raised_panel(name+'_Panel_'+str(number)+'_'+str(index), mats,
                          cx, (a+b)/2, inner_w, b-a, leaf)
        handle_x = direction*(leaf_w-rail*.70)
        handle_z = min(1.03, leaf_h*.47)
        box(name+'_Handle_plate_'+str(number), (handle_x, -.119, handle_z),
            (.048, .025, .19), mats['gold'], bevel=.008, parent=leaf)
        knob = cylinder(name+'_Handle_neck_'+str(number),
                        (handle_x, -.153, handle_z+.025), .022, .055,
                        mats['gold'], vertices=8, parent=leaf)
        knob.rotation_euler[0] = math.pi/2
        beam(name+'_Handle_lever_'+str(number),
             (handle_x, -.19, handle_z+.025),
             (handle_x-direction*.105, -.19, handle_z+.025),
             .022, .025, mats['gold'], parent=leaf, bevel=.005)
        for level in (.22, .78):
            cylinder(name+'_Hinge_barrel_'+str(number)+'_'+str(level),
                     (0, -.075, leaf_h*level), .018, .13, mats['gold'],
                     vertices=8, parent=leaf)
        group(name+'_Handle_anchor_'+str(number),
              (handle_x, -.20, handle_z), parent=leaf)
    group(name+'_Stand_front', (0, -.65, 0), parent=root)
    group(name+'_Passage_center', (0, 0, 0), parent=root)
    return root
