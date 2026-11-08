"""Deterministic, solid-colour low-poly furniture for the room kit.

Every public generator returns an Empty.  Its footprint is centred on X/Y,
its bottom is Z=0 and its front faces -Y.  All coordinates are local to that
Empty, so the caller can place, rotate or scale a whole asset.  Dimensions
are metres.  No texture, external model, font or runtime dependency is used.
"""
import math
import random

from .core import group, box, mesh, cylinder, ico, beam, lathe, polyprism


_BOOK_COLOURS = ("book_red", "book_blue", "book_green", "wood_dark", "wood_light")


def _positive(**values):
    for key, value in values.items():
        if not math.isfinite(value) or value <= 0:
            raise ValueError("%s must be a finite positive number" % key)


def _root(name, kind, parent, **dimensions):
    _positive(**dimensions)
    obj = group(name, parent=parent)
    obj["asset_type"] = kind
    obj["front_axis"] = "-Y"
    for key, value in dimensions.items():
        obj[key] = value
    return obj


def _boxes(name, specs, mat, parent, bevel=0):
    """Several disconnected cuboids share one mesh and one material."""
    vertices, faces = [], []
    cube_faces = ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                  (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7))
    for (cx, cy, cz), (width, depth, height) in specs:
        x, y, z = width / 2, depth / 2, height / 2
        start = len(vertices)
        vertices += [(cx + sx*x, cy + sy*y, cz + sz*z)
                     for sx, sy, sz in ((-1, -1, -1), (1, -1, -1),
                                       (1, 1, -1), (-1, 1, -1),
                                       (-1, -1, 1), (1, -1, 1),
                                       (1, 1, 1), (-1, 1, 1))]
        faces += [tuple(start + j for j in face) for face in cube_faces]
    return mesh(name, vertices, faces, mat, parent, bevel)


def _rounded_rect(width, depth, cut):
    """Eight straight edges: a chamfered rectangle, not a curved bevel."""
    a, b = width / 2, depth / 2
    c = min(cut, a*.6, b*.6)
    return [(-a+c, -b), (a-c, -b), (a, -b+c), (a, b-c),
            (a-c, b), (-a+c, b), (-a, b-c), (-a, -b+c)]


def _ring_solid(name, rings, mat, parent):
    """Closed solid from equal-sized XY rings at successive heights."""
    n = len(rings[0][1])
    vertices = [(x, y, z) for z, ring in rings for x, y in ring]
    faces = [tuple(reversed(range(n)))]
    for level in range(len(rings)-1):
        a, b = level*n, (level+1)*n
        faces += [(a+j, a+(j+1) % n, b+(j+1) % n, b+j) for j in range(n)]
    faces.append(tuple(range((len(rings)-1)*n, len(rings)*n)))
    return mesh(name, vertices, faces, mat, parent)


def _post(name, x, y, levels, mat, parent):
    rings = [(z, [(x-w/2, y-w/2), (x+w/2, y-w/2),
                  (x+w/2, y+w/2), (x-w/2, y+w/2)]) for z, w in levels]
    return _ring_solid(name, rings, mat, parent)


def _cushion(name, width, depth, bottom, height, mat, parent):
    cut = min(width, depth)*.08
    rings = [(bottom, _rounded_rect(width*.95, depth*.95, cut)),
             (bottom+height*.25, _rounded_rect(width, depth, cut)),
             (bottom+height*.78, _rounded_rect(width*.98, depth*.98, cut)),
             (bottom+height, _rounded_rect(width*.91, depth*.91, cut))]
    return _ring_solid(name, rings, mat, parent)


def _panel(name, x, z, width, height, face_y, mats, parent):
    """Front-facing recessed-looking field and a proud four-piece frame."""
    border = min(width, height)*.065
    box(name+"_Field", (x, face_y-.004, z), (width, .012, height),
        mats["wood_dark"], .004, parent)
    specs = [((x-width/2+border/2, face_y-.019, z), (border, .033, height)),
             ((x+width/2-border/2, face_y-.019, z), (border, .033, height)),
             ((x, face_y-.019, z-height/2+border/2), (width, .033, border)),
             ((x, face_y-.019, z+height/2-border/2), (width, .033, border))]
    _boxes(name+"_Moulding", specs, mats["wood"], parent, .004)


def book(name, mats, *, width=.075, depth=.23, height=.31,
         colour="book_red", parent=None):
    """One upright volume.  Width is its spine thickness, front is -Y."""
    root = _root(name, "book", parent, width=width, depth=depth, height=height)
    cover = min(width*.13, .008)
    spine = min(depth*.055, .014)
    material = mats[colour]
    specs = [((-width/2+cover/2, 0, height/2), (cover, depth, height)),
             ((width/2-cover/2, 0, height/2), (cover, depth, height)),
             ((0, -depth/2+spine/2, height/2), (width, spine, height))]
    _boxes(name+"_Binding", specs, material, root, min(cover*.15, .001))
    box(name+"_Pages", (0, spine*.35, height/2),
        (width-2*cover, depth-spine*1.55, height*.94), mats["cream"], 0, root)
    bands = [((0, -depth/2-.0007, height*t), (width*.88, .002, height*.025))
             for t in (.17, .78, .85)]
    _boxes(name+"_SpineBands", bands, mats["gold"], root)
    box(name+"_SpineLabel", (0, -depth/2-.001, height*.56),
        (width*.55, .002, height*.105), mats["cream"], 0, root)
    return root


def _flat_book(name, mats, width, depth, height, colour, parent):
    root = _root(name, "flat_book", parent, width=width, depth=depth, height=height)
    cover = height*.16
    specs = [((0, 0, cover/2), (width, depth, cover)),
             ((0, 0, height-cover/2), (width, depth, cover)),
             ((0, -depth/2+cover/2, height/2), (width, cover, height))]
    _boxes(name+"_Binding", specs, mats[colour], root, min(.002, cover*.2))
    box(name+"_Pages", (0, cover*.25, height/2),
        (width*.96, depth-cover*1.5, height-2*cover), mats["cream"], 0, root)
    _boxes(name+"_Bands", [((width*t, -depth/2-.001, height/2),
                            (width*.023, .002, height*.8)) for t in (-.30, .30)],
           mats["gold"], root)
    return root


def books(name, mats, *, width=.34, depth=.25, height=.17,
          count=3, seed=7, parent=None):
    """A small horizontal stack, useful on tables and reception counters."""
    root = _root(name, "books", parent, width=width, depth=depth, height=height)
    rng = random.Random(seed)
    count = max(1, int(count))
    weights = [rng.uniform(.8, 1.2) for _ in range(count)]
    total, bottom = sum(weights), 0
    for i, weight in enumerate(weights):
        h = height*weight/total
        factor = 1-i*.035
        child = _flat_book(name+"_%02d" % i, mats, width*factor,
                           depth*factor, h, _BOOK_COLOURS[i % len(_BOOK_COLOURS)], root)
        child.location = (rng.uniform(-.018, .018)*width,
                          rng.uniform(-.025, .025)*depth, bottom)
        child.rotation_euler[2] = rng.uniform(-.10, .10)
        bottom += h
    return root


def open_book(name, mats, *, width=.45, depth=.30, height=.055, parent=None):
    """A closed-volume folded page block with geometric line marks."""
    root = _root(name, "open_book", parent, width=width, depth=depth, height=height)
    # Each half has two planar facets; covers and page edges are closed solids.
    for side in (-1, 1):
        xs = [side*width*.5, side*width*.25, 0]
        zs = [height*.64, height, height*.45]
        section = list(zip(xs, zs)) + [(x, z-height*.22) for x, z in reversed(list(zip(xs, zs)))]
        verts = [(x, y, z) for y in (-depth*.47, depth*.47) for x, z in section]
        n = len(section)
        faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
        faces += [(j, (j+1) % n, (j+1) % n+n, j+n) for j in range(n)]
        mesh(name+"_PageBlock_%d" % side, verts, faces, mats["cream"], root)
        # Explicit X/Z cover strips keep the fold and underside closed.
        csec = [(0, 0), (side*width*.52, height*.20),
                (side*width*.52, height*.32), (0, height*.12)]
        cv = [(x, y, z) for y in (-depth/2, depth/2) for x, z in csec]
        cf = [(3, 2, 1, 0), (4, 5, 6, 7)]
        cf += [(j, (j+1) % 4, (j+1) % 4+4, j+4) for j in range(4)]
        mesh(name+"_Cover_%d" % side, cv, cf, mats["book_red"], root)
        def page_height(x):
            t = abs(x)/width
            if t <= .25:
                return height*(.45+(.55*t/.25))
            return height*(1-.36*(t-.25)/.25)
        for j in range(6):
            y = depth*(-.31+j*.105)
            # Split each mark at the page ridge so it follows both facets.
            row_xs = [side*width*.44, side*width*.25, side*width*(.065+.012*(j % 2))]
            for k in range(2):
                a, b = row_xs[k:k+2]
                thickness = depth*.009
                points = [(a, y, page_height(a)+.0008),
                          (b, y, page_height(b)+.0008),
                          (b, y+thickness, page_height(b)+.0008),
                          (a, y+thickness, page_height(a)+.0008)]
                # Closed paper-thin strokes remain visible from either side.
                vv = points+[(x, yy, z-.0004) for x, yy, z in points]
                ff = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1),
                      (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
                mesh(name+"_Line_%d_%d_%d" % (side, j, k), vv, ff,
                     mats["stone_dark"], root)
    return root


def bookshelf(name, mats, *, width=2.0, depth=.42, height=2.55,
              shelves=4, bays=2, fill=.78, seed=7, parent=None):
    """Panelled bookcase with actual shelves, bindings and page blocks."""
    root = _root(name, "bookshelf", parent, width=width, depth=depth, height=height)
    if width < .4 or depth < .18 or height < .65:
        raise ValueError("bookshelf needs width >= .4, depth >= .18 and height >= .65")
    rng = random.Random(seed)
    shelves, bays = max(1, int(shelves)), max(1, int(bays))
    fill = max(0, min(float(fill), 1))
    side = min(.095, width*.065)
    divider = side*.68
    inner_width = width-2*side-(bays-1)*divider
    bay_width = inner_width/bays
    if bay_width < .15:
        raise ValueError("too many bays for bookshelf width")
    base, crown = height*.075, height*.075
    usable = height-base-crown
    pitch = usable/shelves
    if pitch < .10:
        raise ValueError("too many shelves for bookshelf height")
    box(name+"_Back", (0, depth/2-.018, height/2),
        (width-.02, .036, height-.08), mats["wood_dark"], .004, root)
    box(name+"_Base", (0, -.005, base*.45), (width, depth, base*.9), mats["wood"], .012, root)
    box(name+"_BaseLip", (0, -.012, base*.87),
        (width*1.025, depth*1.05, base*.24), mats["wood_light"], .009, root)
    box(name+"_Cornice", (0, 0, height-crown*.38),
        (width*1.035, depth*1.10, crown*.76), mats["wood"], .014, root)
    box(name+"_CorniceEdge", (0, -.008, height-crown*.78),
        (width*1.01, depth*1.04, crown*.20), mats["wood_light"], .006, root)
    for i in range(bays+1):
        x = -width/2+side/2 if i == 0 else (width/2-side/2 if i == bays else
              -width/2+side+i*bay_width+(i-.5)*divider)
        post_width = side if i in (0, bays) else divider
        box(name+"_Stile_%d" % i, (x, 0, (height-crown+base)/2),
            (post_width, depth, usable), mats["wood"], .008, root)
        box(name+"_StileFace_%d" % i, (x, -depth/2-.008, height/2),
            (post_width*.65, .022, height-base-crown*.8), mats["wood_light"], .005, root)
    for bay in range(bays):
        cx = -width/2+side+bay_width/2+bay*(bay_width+divider)
        for level in range(shelves):
            z = base+level*pitch
            shelf_t = min(.045, pitch*.12)
            box(name+"_Shelf_%d_%d" % (bay, level), (cx, -.006, z+shelf_t/2),
                (bay_width, depth-.025, shelf_t), mats["wood"], .004, root)
            box(name+"_ShelfEdge_%d_%d" % (bay, level),
                (cx, -depth/2-.006, z+shelf_t*.6),
                (bay_width, .023, shelf_t*.65), mats["wood_light"], .002, root)
            left = cx-bay_width/2+.02
            used = 0.0
            target = (bay_width-.04)*fill
            available_height = pitch-shelf_t-.02
            book_depth = min(depth*.69, available_height*.78)
            index = 0
            while used < target-.025:
                bw = min(rng.uniform(.045, .085)*height/2.55, target-used)
                if bw < .018:
                    break
                bh = available_height*rng.uniform(.63, .94)
                volume = book(name+"_Book_%d_%d_%02d" % (bay, level, index), mats,
                              width=bw, depth=book_depth*rng.uniform(.88, 1), height=bh,
                              colour=rng.choice(_BOOK_COLOURS), parent=root)
                volume.location = (left+used+bw/2,
                                   -depth/2+book_depth/2+.016+rng.uniform(0, .012), z+shelf_t)
                used += bw+.003
                index += 1
                if index % 4 == 0 and used < target*.80:
                    used += rng.uniform(.015, .035)
            free = bay_width-.05-used
            if fill > 0 and free > .17 and level % 2 == bay % 2:
                stack = books(name+"_ShelfStack_%d_%d" % (bay, level), mats,
                              width=min(free-.015, .27), depth=book_depth*.92,
                              height=available_height*.34, count=2,
                              seed=seed+bay*29+level*11, parent=root)
                stack.location = (cx+bay_width/2-free/2-.01,
                                  -depth/2+book_depth/2+.018, z+shelf_t)
    return root


def table(name, mats, *, width=1.6, depth=.85, height=.78,
          shape="rect", parent=None):
    """Writing table with turned-square legs, or an octagonal pedestal table."""
    root = _root(name, "table", parent, width=width, depth=depth, height=height)
    top_t = height*.075
    if shape in ("round", "octagon"):
        points = [(width/2*math.cos(2*math.pi*i/8+math.pi/8),
                   depth/2*math.sin(2*math.pi*i/8+math.pi/8)) for i in range(8)]
        polyprism(name+"_Top", points, height-top_t, height, mats["wood_light"], root)
        polyprism(name+"_TopUndercut", [(x*.96, y*.96) for x, y in points],
                  height-top_t*1.45, height-top_t, mats["wood"], root)
        radius = min(width, depth)
        profile = [(radius*.17, 0), (radius*.19, .08), (radius*.11, .14),
                   (radius*.07, height*.38), (radius*.09, height*.58),
                   (radius*.12, height*.66), (radius*.10, height-top_t*1.45)]
        lathe(name+"_Pedestal", profile, 8, mats["wood"], root)
        for i in range(4):
            angle = i*math.pi/2+math.pi/4
            end = (math.cos(angle)*width*.34, math.sin(angle)*depth*.34, .045)
            beam(name+"_Foot_%d" % i, (0, 0, .10), end,
                 radius*.095, .075, mats["wood"], root, .006)
        return root
    if shape != "rect":
        raise ValueError("table shape must be rect, round or octagon")
    box(name+"_Top", (0, 0, height-top_t/2), (width, depth, top_t),
        mats["wood_light"], min(.024, top_t*.3), root)
    box(name+"_TopUndercut", (0, 0, height-top_t*1.2),
        (width*.96, depth*.95, top_t*.45), mats["wood_dark"], .006, root)
    leg_width = min(width, depth)*.11
    inset = leg_width*.85
    for i, x in enumerate((-width/2+inset, width/2-inset)):
        for j, y in enumerate((-depth/2+inset, depth/2-inset)):
            levels = [(0, leg_width*.91), (.045, leg_width*.91),
                      (.065, leg_width*.64), (height*.48, leg_width*.53),
                      (height*.62, leg_width*.88), (height*.68, leg_width*.88),
                      (height*.72, leg_width*.72), (height-top_t, leg_width*.95)]
            _post(name+"_Leg_%d_%d" % (i, j), x, y, levels, mats["wood"], root)
    apron_z = height-top_t-height*.075
    _boxes(name+"_Aprons", [((0, y, apron_z), (width-inset, .045, height*.15))
                            for y in (-depth/2+inset, depth/2-inset)] +
           [((x, 0, apron_z), (.045, depth-inset, height*.15))
            for x in (-width/2+inset, width/2-inset)], mats["wood"], root, .005)
    box(name+"_FrontDrawer", (0, -depth/2+inset-.028, apron_z),
        (width*.37, .018, height*.11), mats["wood_light"], .005, root)
    for x in (-width*.105, width*.105):
        knob = ico(name+"_DrawerKnob", (x, -depth/2+inset-.049, apron_z),
                   height*.012, mats["gold"], 1, root)
        knob.scale = (1, .7, 1)
    return root


def chair(name, mats, *, width=.52, depth=.53, height=.98,
          seat_height=.44, arms=False, parent=None):
    """Slatted walnut chair with a faceted upholstered seat."""
    root = _root(name, "chair", parent, width=width, depth=depth, height=height,
                 seat_height=seat_height)
    if seat_height >= height*.78:
        raise ValueError("chair back needs height above seat")
    leg = min(width, depth)*.11
    xleg, yf, yb = width*.39, -depth*.37, depth*.36
    for x in (-xleg, xleg):
        _post(name+"_FrontLeg", x, yf, [(0, leg*.70), (.045, leg*.85),
               (seat_height-.09, leg), (seat_height-.02, leg)], mats["wood"], root)
        _post(name+"_BackPost", x, yb, [(0, leg*.75), (seat_height, leg),
               (height-.07, leg*.9), (height, leg*1.04)], mats["wood"], root)
    box(name+"_SeatFrame", (0, 0, seat_height-.08),
        (width*.97, depth*.96, .075), mats["wood"], .012, root)
    _cushion(name+"_Seat", width*.87, depth*.87, seat_height-.05, .05,
             mats["green"], root)
    box(name+"_BackTopRail", (0, yb, height-.035),
        (width*.98, leg*1.12, .07), mats["wood_light"], .010, root)
    box(name+"_BackLowerRail", (0, yb, seat_height+.13),
        (width*.86, leg*.85, .055), mats["wood"], .005, root)
    for j, x in enumerate((-width*.22, 0, width*.22)):
        box(name+"_BackSlat_%d" % j,
            (x, yb, (height-.07+seat_height+.155)/2),
            (leg*.65, leg*.64, height-.225-seat_height), mats["wood"], .005, root)
    # Stretcher bars make the otherwise slender silhouette structurally legible.
    for x in (-xleg, xleg):
        beam(name+"_Stretcher", (x, yf, seat_height*.40),
             (x, yb, seat_height*.40), leg*.5, leg*.5, mats["wood"], root, .003)
    if arms:
        arm_z = seat_height+.20
        for x in (-xleg, xleg):
            beam(name+"_ArmSupport", (x, yf, seat_height), (x, yf, arm_z),
                 leg*.65, leg*.65, mats["wood"], root, .004)
            box(name+"_Arm", (x, -.01, arm_z),
                (leg*1.55, depth*.86, .045), mats["wood_light"], .009, root)
    return root


def bench(name, mats, *, width=1.80, depth=.60, height=1.02,
          seat_height=.45, parent=None):
    """Hall bench with seven slats, tall end posts and a green seat pad."""
    root = _root(name, "bench", parent, width=width, depth=depth, height=height,
                 seat_height=seat_height)
    if seat_height >= height*.75:
        raise ValueError("bench back needs height above seat")
    leg = min(.085, width*.055, depth*.15)
    lx, yf, yb = width/2-leg*.90, -depth*.34, depth*.37
    for x in (-lx, lx):
        _post(name+"_FrontLeg", x, yf, [(0, leg*.85), (.045, leg*.85),
               (.075, leg*.65), (seat_height+.22, leg*.85)], mats["wood"], root)
        _post(name+"_RearLeg", x, yb, [(0, leg*.85), (seat_height, leg),
               (height-.06, leg), (height, leg*1.25)], mats["wood"], root)
        box(name+"_ArmRest", (x, 0, seat_height+.22),
            (leg*1.38, depth*.93, .055), mats["wood_light"], .009, root)
    box(name+"_SeatBase", (0, 0, seat_height-.10),
        (width*.96, depth*.92, .10), mats["wood"], .012, root)
    _cushion(name+"_LongCushion", width*.88, depth*.77, seat_height-.065,
             .065, mats["green"], root)
    box(name+"_BackTopRail", (0, yb, height-.065),
        (width, .075, .09), mats["wood_light"], .012, root)
    box(name+"_BackBottomRail", (0, yb, seat_height+.12),
        (width*.93, .055, .06), mats["wood"], .005, root)
    slat_bottom, slat_top = seat_height+.15, height-.11
    count = max(3, round(width/.20))
    for i in range(count):
        x = -width*.39+width*.78*i/(count-1)
        box(name+"_Slat_%02d" % i, (x, yb, (slat_top+slat_bottom)/2),
            (.045, .042, slat_top-slat_bottom), mats["wood"], .004, root)
    return root


def reception_desk(name, mats, *, width=2.35, depth=.95, height=1.08,
                   panels=3, details=True, parent=None):
    """A hollow U-shaped reception counter with a lower writing surface."""
    root = _root(name, "reception_desk", parent, width=width, depth=depth, height=height)
    panels = max(1, int(panels))
    wall = min(.09, depth*.12)
    front_y = -depth/2
    case_h = height-.09
    specs = [((0, front_y+wall/2, case_h/2), (width, wall, case_h)),
             ((-width/2+wall/2, 0, case_h/2), (wall, depth, case_h)),
             ((width/2-wall/2, 0, case_h/2), (wall, depth, case_h))]
    _boxes(name+"_Carcass", specs, mats["wood"], root, .010)
    # Keep the three moulding pieces in one object.
    plinth = [((0, front_y+.012, .065), (width+.06, wall+.07, .13))] + [
        ((x, 0, .065), (wall+.055, depth+.035, .13)) for x in (-width/2+.015, width/2-.015)]
    _boxes(name+"_Plinth", plinth, mats["wood_dark"], root, .009)
    box(name+"_WritingSurface", (0, .035, height-.27),
        (width-wall*2, depth-wall-.04, .055), mats["wood_light"], .008, root)
    top_depth = min(.30, depth*.32)
    # A single concave U polygon avoids overlapping coplanar top surfaces.
    a = width/2+.05
    y0, y1 = front_y-.035, depth/2+.035
    side_t, inner_front = top_depth*.72, y0+top_depth
    outline = [(-a, y0), (a, y0), (a, y1), (a-side_t, y1),
               (a-side_t, inner_front), (-a+side_t, inner_front),
               (-a+side_t, y1), (-a, y1)]
    top = polyprism(name+"_CounterTop", outline, height-.09, height,
                    mats["wood_light"], root)
    mod = top.modifiers.new('Counter_edge_highlight', 'BEVEL')
    mod.width, mod.segments = .016, 1
    mod.use_clamp_overlap = True
    field_h, field_z = height-.31, height/2-.015
    panel_pitch = (width-.17)/panels
    for i in range(panels):
        x = -width/2+.085+(i+.5)*panel_pitch
        _panel(name+"_FrontPanel_%d" % i, x, field_z,
               panel_pitch-.065, field_h, front_y, mats, root)
    for sign in (-1, 1):
        side = group(name+"_SideDetail_%d" % sign,
                     location=(sign*width/2, 0, 0), rotation=sign*math.pi/2, parent=root)
        _panel(name+"_SidePanel_%d" % sign, 0, field_z,
               depth-.18, field_h, 0, mats, side)
    if details:
        # Solid triangular sign stand; the sloping brass face contains no text.
        sw, sd, sh = min(.32, width*.16), .12, .13
        x0, y0, z0 = -width*.13, front_y+top_depth*.44-.035, height
        verts = [(x0+x, y0+y, z0+z) for x in (-sw/2, sw/2)
                 for y, z in ((-sd/2, 0), (sd/2, 0), (sd/2, sh))]
        faces = [(2, 1, 0), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)]
        mesh(name+"_BrassSign", verts, faces, mats["gold"], root, .003)
        bx, by = width*.30, front_y+top_depth*.46-.035
        cylinder(name+"_BellBase", (bx, by, height+.008), .055, .016,
                 mats["wood_dark"], 8, root)
        lathe(name+"_ServiceBell", [(.047, 0), (.047, .015), (.039, .035),
              (.023, .047), (.006, .051), (.006, .062)], 8, mats["gold"], root,
              location=(bx, by, height+.016))
    return root


def _leaf(name, start, end, width, mat, parent):
    """A six-vertex closed blade: a raised ridge and a thin underside."""
    sx, sy, sz = start
    ex, ey, ez = end
    dx, dy = ex-sx, ey-sy
    length_xy = math.hypot(dx, dy)
    tx, ty = ((-dy/length_xy, dx/length_xy) if length_xy > 1e-8 else (1, 0))
    cx, cy, cz = (sx+ex)*.5, (sy+ey)*.5, (sz+ez)*.5
    verts = [start, end, (cx+tx*width/2, cy+ty*width/2, cz),
             (cx-tx*width/2, cy-ty*width/2, cz),
             (cx, cy, cz+width*.14), (cx, cy, cz-width*.04)]
    faces = [(0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4),
             (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)]
    return mesh(name, verts, faces, mat, parent)


def planter(name, mats, *, width=.52, height=.66, flower_count=7,
            seed=7, parent=None):
    """Eight-sided hollow pot, soil, solid leaves and faceted cream flowers.

    width is the approximate canopy diameter; the pot diameter is .62*width.
    The pot is .44*height high and all foliage stays near the specified height.
    """
    root = _root(name, "planter", parent, width=width, height=height)
    rng = random.Random(seed)
    pot_h, radius = height*.44, width*.31
    profile = [(radius*.60, 0), (radius*.67, pot_h*.07),
               (radius*.91, pot_h*.78), (radius*.94, pot_h*.84),
               (radius, pot_h*.86), (radius, pot_h),
               (radius*.82, pot_h), (radius*.78, pot_h*.81),
               (radius*.56, pot_h*.17), (0, pot_h*.17)]
    lathe(name+"_Pot", profile, 8, mats["stone"], root)
    cylinder(name+"_Soil", (0, 0, pot_h*.86), radius*.78, height*.018,
             mats["wood_dark"], 8, root)
    for i in range(10):
        theta = 2*math.pi*i/10+rng.uniform(-.15, .15)
        outer = width*rng.uniform(.38, .48)
        start = (math.cos(theta)*width*.05, math.sin(theta)*width*.05, pot_h*.92)
        end = (math.cos(theta)*outer, math.sin(theta)*outer,
               height*rng.uniform(.48, .68))
        _leaf(name+"_Leaf_%02d" % i, start, end, width*rng.uniform(.15, .23),
              mats["leaf" if i % 3 else "green"], root)
    count = max(0, int(flower_count))
    for i in range(count):
        if i == 0:
            x, y, flower_z = 0, 0, height*.89
        else:
            angle = 2*math.pi*(i-1)/max(1, count-1)+.20
            rr = width*(.23 if count <= 8 else .29)
            x, y = math.cos(angle)*rr, math.sin(angle)*rr
            flower_z = height*(.72+.06*math.sin(angle*2+.4))
        flower_radius = min(width*.115, height*.09)
        beam(name+"_Stem_%02d" % i, (x*.35, y*.35, pot_h*.85),
             (x, y, flower_z), width*.012, width*.012, mats["leaf"], root, 0)
        # Each head has a faceted centre and six individually modelled lobes.
        centre = ico(name+"_FlowerHeart_%02d" % i,
                     (x, y, flower_z+flower_radius*.15), flower_radius*.65,
                     mats["cream"], 1, root)
        centre.scale = (1, 1, .75)
        for petal in range(6):
            theta = petal*math.pi/3+i*.37
            rr = flower_radius*.65
            obj = ico(name+"_Petal_%02d_%d" % (i, petal),
                      (x+math.cos(theta)*rr, y+math.sin(theta)*rr,
                       flower_z+rng.uniform(-.05, .08)*flower_radius),
                      flower_radius*.62, mats["cream"], 1, root)
            obj.scale = (1, 1, .70)
        ico(name+"_Pollen_%02d" % i, (x, y, flower_z+flower_radius*.60),
             flower_radius*.25, mats["wall"], 1, root)
    return root


def _art_polygon(name, points, face_y, mat, parent):
    # Tiny thickness yields a true closed mesh and reliable culled back faces.
    n = len(points)
    vertices = [(x, face_y+y, z) for y in (0, .0008) for x, z in points]
    faces = [tuple(range(n)), tuple(reversed(range(n, 2*n)))]
    faces += [(j, j+n, (j+1) % n+n, (j+1) % n) for j in range(n)]
    return mesh(name, vertices, faces, mat, parent)


def painting(name, mats, *, width=1.05, height=.85, depth=.065,
             style="city", seed=7, parent=None):
    """A gold frame containing layered solid polygon art, with no image plane.

    Origin is centred beneath the frame; place on a wall at the desired bottom
    elevation.  style is 'city' or 'landscape'.  Its visible face points -Y.
    """
    root = _root(name, "painting", parent, width=width, height=height, depth=depth)
    if style not in ("city", "landscape"):
        raise ValueError("painting style must be city or landscape")
    rng = random.Random(seed)
    border = min(width, height)*.075
    box(name+"_Back", (0, depth*.21, height/2),
        (width, depth*.55, height), mats["wood_dark"], .006, root)
    frame = [((-width/2+border/2, -depth*.12, height/2), (border, depth, height)),
             ((width/2-border/2, -depth*.12, height/2), (border, depth, height)),
             ((0, -depth*.12, border/2), (width, depth, border)),
             ((0, -depth*.12, height-border/2), (width, depth, border))]
    _boxes(name+"_GoldFrame", frame, mats["gold"], root, border*.13)
    inner_w, inner_h = width-2*border, height-2*border
    a, low, high = inner_w/2, border, height-border
    _art_polygon(name+"_Sky", [(-a, low), (a, low), (a, high), (-a, high)],
                 -depth*.08, mats["cream"], root)
    sun_x, sun_z, sr = inner_w*.25, low+inner_h*.75, inner_h*.09
    sun = [(sun_x+sr*math.cos(2*math.pi*i/12),
            sun_z+sr*math.sin(2*math.pi*i/12)) for i in range(12)]
    _art_polygon(name+"_Sun", sun, -depth*.08-.0015, mats["wood_light"], root)
    mountains = [(-a, low), (a, low), (a, low+inner_h*.38),
                 (inner_w*.29, low+inner_h*.53), (inner_w*.12, low+inner_h*.43),
                 (-inner_w*.10, low+inner_h*.63), (-inner_w*.31, low+inner_h*.38),
                 (-a, low+inner_h*.48)]
    _art_polygon(name+"_DistantHills", mountains, -depth*.08-.003,
                 mats["grout"], root)
    if style == "city":
        # Silhouette buildings are individual closed, constant-colour polygons.
        for i in range(7):
            x0 = -a+i*inner_w/7
            bw = inner_w/7*1.01
            bh = inner_h*rng.uniform(.22, .43)
            polygon = [(x0, low), (x0+bw, low), (x0+bw, low+bh),
                       (x0+bw*.5, low+bh+inner_h*.08), (x0, low+bh)]
            _art_polygon(name+"_House_%d" % i, polygon,
                         -depth*.08-.005-i*.00012, mats["stone_dark"], root)
        tower_x, tower_w = inner_w*.06, inner_w*.15
        tower = [(tower_x-tower_w/2, low), (tower_x+tower_w/2, low),
                 (tower_x+tower_w/2, low+inner_h*.60),
                 (tower_x+tower_w*.32, low+inner_h*.60),
                 (tower_x+tower_w*.27, low+inner_h*.77),
                 (tower_x, low+inner_h*.91),
                 (tower_x-tower_w*.27, low+inner_h*.77),
                 (tower_x-tower_w*.32, low+inner_h*.60),
                 (tower_x-tower_w/2, low+inner_h*.60)]
        _art_polygon(name+"_Tower", tower, -depth*.08-.007,
                     mats["wood_dark"], root)
        fore = [(-a, low), (a, low), (a, low+inner_h*.12),
                (inner_w*.20, low+inner_h*.17), (-inner_w*.18, low+inner_h*.12),
                (-a, low+inner_h*.20)]
        _art_polygon(name+"_Foreground", fore, -depth*.08-.009,
                     mats["wood_dark"], root)
    else:
        near = [(-a, low), (a, low), (a, low+inner_h*.25),
                (inner_w*.33, low+inner_h*.35), (inner_w*.10, low+inner_h*.24),
                (-inner_w*.15, low+inner_h*.42), (-a, low+inner_h*.21)]
        _art_polygon(name+"_NearHills", near, -depth*.08-.006, mats["green"], root)
        for i, x in enumerate((-inner_w*.34, -inner_w*.24, inner_w*.34)):
            tree_h = inner_h*(.35 if i == 0 else .26)
            tree_w = inner_w*.13
            z = low+inner_h*.09
            polygon = [(x-tree_w*.13, z), (x+tree_w*.13, z),
                       (x+tree_w*.13, z+tree_h*.23), (x+tree_w*.50, z+tree_h*.23),
                       (x+tree_w*.32, z+tree_h*.52), (x+tree_w*.42, z+tree_h*.52),
                       (x, z+tree_h), (x-tree_w*.42, z+tree_h*.52),
                       (x-tree_w*.32, z+tree_h*.52), (x-tree_w*.50, z+tree_h*.23),
                       (x-tree_w*.13, z+tree_h*.23)]
            _art_polygon(name+"_Tree_%d" % i, polygon, -depth*.08-.008-i*.0003,
                         mats["wood_dark"], root)
    return root


def ladder(name, mats, *, width=.46, depth=.30, height=2.20,
           rungs=7, parent=None):
    """A slightly inclined library ladder; bottom sits on Z=0."""
    root = _root(name, "ladder", parent, width=width, depth=depth, height=height)
    rail = min(width*.105, .06)
    bottom_y, top_y = -depth*.43, depth*.43
    for side in (-1, 1):
        x = side*(width/2-rail/2)
        beam(name+"_Rail_%d" % side, (x, bottom_y, rail*.45),
             (x, top_y, height-rail*.45), rail, rail, mats["wood"], root, .006)
        box(name+"_Shoe_%d" % side, (x, bottom_y, rail*.38),
            (rail*1.2, rail*1.6, rail*.76), mats["gold"], .003, root)
    rungs = max(2, int(rungs))
    for i in range(rungs):
        t = .10+.78*i/(rungs-1)
        y, z = bottom_y+(top_y-bottom_y)*t, height*t
        box(name+"_Rung_%02d" % i, (0, y, z),
            (width-rail, rail*1.5, rail*.68), mats["wood_light"], .004, root)
    return root


__all__ = ["book", "books", "open_book", "bookshelf", "table", "chair", "bench",
           "reception_desk", "planter", "painting", "ladder"]
