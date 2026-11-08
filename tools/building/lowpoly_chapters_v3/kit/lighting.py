"""Low-poly lamps with real construction, separate emitters and light anchors.

Blender Z is up. Wall fixtures face -Y. A wall lantern's origin is the centre
of its mounting plate; a banker lamp's origin is the table surface; a pendant
origin is its ceiling attachment. Lighting may be disabled independently.
"""
import math
import bpy

from .core import group, box, mesh, cylinder, ico, beam, lathe


def _light(name, parent, location, kind='POINT', energy=12.0,
           color=(1.0, .72, .40), size=.10, size_y=None):
    data = bpy.data.lights.new(name+'_Data', type=kind)
    data.energy = max(0.0, float(energy))
    data.color = color
    if kind == 'POINT':
        data.shadow_soft_size = size
    elif kind == 'AREA':
        data.shape = 'RECTANGLE'
        data.size = size
        data.size_y = size if size_y is None else size_y
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj['kind'] = 'generated_fixture_light'
    return obj


def _unshadowed_emitter(obj):
    # An opaque glowing core must not seal the point light inside itself.
    if hasattr(obj, 'visible_shadow'):
        obj.visible_shadow = False
    obj['casts_shadow'] = False
    return obj


def wall_lantern(name, mats, height=.65, parent=None, light=True,
                 light_energy=12.0):
    """Six-sided gold wall lantern, origin at the wall mounting plate centre."""
    if height <= 0:
        raise ValueError('Lantern height must be positive')
    root = group(name, parent=parent)
    root['kind'] = 'wall_lantern'
    root['fixture_height'] = height
    s = height/.65
    gold = mats['gold']
    box(name+'_Mount', (0, -.028*s, .05*s), (.115*s, .055*s, .34*s),
        gold, bevel=.012*s, parent=root)
    box(name+'_Mount_inset', (0, -.062*s, .05*s), (.070*s, .018*s, .25*s),
        mats['wood_dark'], bevel=.007*s, parent=root)
    arm = [(0, -.06*s, .18*s), (0, -.145*s, .285*s),
           (0, -.30*s, .285*s), (0, -.30*s, .205*s)]
    for j in range(len(arm)-1):
        beam(name+'_Bracket_'+str(j), arm[j], arm[j+1], .026*s, .026*s,
             gold, parent=root, bevel=.004*s)
    cy = -.30*s
    body = [(.066*s, -.235*s), (.118*s, -.145*s),
            (.118*s, .095*s), (.073*s, .185*s)]
    core_profile = [(0, body[0][1])]
    core_profile += [(r*.91, z) for r, z in body]
    core_profile += [(0, body[-1][1])]
    core = lathe(name+'_Emitting_core', core_profile, 6, mats['emission'],
                 parent=root, location=(0, cy, 0))
    _unshadowed_emitter(core)
    for j in range(6):
        angle = 2*math.pi*j/6 + math.pi/8
        for k in range(len(body)-1):
            r0, z0 = body[k]
            r1, z1 = body[k+1]
            beam(name+'_Rib_'+str(j)+'_'+str(k),
                 (r0*math.cos(angle), cy+r0*math.sin(angle), z0),
                 (r1*math.cos(angle), cy+r1*math.sin(angle), z1),
                 .016*s, .016*s, gold, parent=root, bevel=.002*s)
        for k in (1, 2):
            r, z = body[k]
            following = 2*math.pi*(j+1)/6 + math.pi/8
            beam(name+'_Ring_'+str(k)+'_'+str(j),
                 (r*math.cos(angle), cy+r*math.sin(angle), z),
                 (r*math.cos(following), cy+r*math.sin(following), z),
                 .018*s, .018*s, gold, parent=root, bevel=.002*s)
    lathe(name+'_Top_cap', [(0, .174*s), (.082*s, .174*s),
                           (.142*s, .205*s), (.142*s, .227*s),
                           (.045*s, .320*s), (.022*s, .325*s),
                           (0, .325*s)],
          6, gold, parent=root, location=(0, cy, 0))
    lathe(name+'_Bottom_cap', [(0, -.31*s), (.026*s, -.285*s),
                              (.080*s, -.25*s), (.088*s, -.215*s),
                              (0, -.215*s)],
          6, gold, parent=root, location=(0, cy, 0))
    ico(name+'_Finial', (0, cy, -.31*s), .025*s, gold,
        subdivisions=1, parent=root)
    anchor = group(name+'_Light_anchor', (0, cy, -.025*s), parent=root)
    anchor['kind'] = 'light_anchor'
    anchor['light_type'] = 'point'
    anchor['energy_watts'] = float(light_energy)
    if light:
        _light(name+'_Point', root, anchor.location, energy=light_energy,
               size=.075*s)
    return root


def _banker_shade(name, mats, width, radius_y, radius_z, bottom, cy, parent):
    """Watertight faceted half-cylinder shell, open along its underside.

    Outer and inner end caps are included, so this is a hood, not a flat
    double-sided plane. Cream faces point into the actual hollow interior.
    """
    segments = 6
    thick = min(.015, radius_y*.12, width*.04)
    vertices = []
    rings = []
    settings = [(-width/2, radius_y, radius_z),
                (width/2, radius_y, radius_z),
                (-width/2+thick, radius_y-thick, radius_z-thick),
                (width/2-thick, radius_y-thick, radius_z-thick)]
    for x, ry, rz in settings:
        ring = []
        for j in range(segments+1):
            angle = math.pi*j/segments
            ring.append(len(vertices))
            vertices.append((x, cy+ry*math.cos(angle),
                             bottom+rz*math.sin(angle)))
        rings.append(ring)
    ol, or_, il, ir = rings
    faces, inner_faces = [], []
    for j in range(segments):
        faces.append((ol[j], ol[j+1], or_[j+1], or_[j]))
    faces.append(tuple(reversed(ol)))
    faces.append(tuple(or_))
    for j in range(segments):
        inner_faces.append(len(faces))
        faces.append((il[j], ir[j], ir[j+1], il[j+1]))
    inner_faces.append(len(faces))
    faces.append(tuple(il))
    inner_faces.append(len(faces))
    faces.append(tuple(reversed(ir)))
    # Four bottom rim faces join the outer and inner rectangular apertures.
    outer = [ol[-1], or_[-1], or_[0], ol[0]]
    inner = [il[-1], ir[-1], ir[0], il[0]]
    for i in range(4):
        j = (i+1) % 4
        faces.append((outer[i], inner[i], inner[j], outer[j]))
    obj = mesh(name, vertices, faces, mats['green'], parent=parent)
    obj.data.materials.append(mats['cream'])
    for index in inner_faces:
        obj.data.polygons[index].material_index = 1
    return obj


def banker_lamp(name, mats, height=.55, width=.43, parent=None,
                light=True, light_energy=8.0):
    """Green angular banker lamp; returned Empty rests at the table surface."""
    if min(height, width) <= 0:
        raise ValueError('Lamp dimensions must be positive')
    root = group(name, parent=parent)
    root['kind'] = 'banker_lamp'
    s = height/.55
    gold = mats['gold']
    lathe(name+'_Base', [(0, 0), (.11*s, 0), (.135*s, .018*s),
                        (.135*s, .035*s), (.105*s, .055*s),
                        (.045*s, .072*s), (0, .072*s)],
          12, gold, parent=root)
    cylinder(name+'_Stem', (0, .025*s, .215*s), .024*s, .285*s,
             gold, vertices=8, parent=root)
    lathe(name+'_Stem_collar', [(0, .067*s), (.038*s, .067*s),
                               (.038*s, .095*s), (.029*s, .115*s),
                               (.025*s, .115*s), (0, .115*s)],
          8, gold, parent=root, location=(0, .025*s, 0))
    cy, bottom = -.025*s, .405*s
    for side in (-1, 1):
        beam(name+'_Shade_support_'+str(side),
             (0, .025*s, .335*s), (side*(width/2+.018*s), .025*s, .435*s),
             .017*s, .020*s, gold, parent=root, bevel=.003*s)
        knob = cylinder(name+'_Shade_pivot_'+str(side),
                        (side*(width/2+.012*s), cy, bottom+.045*s),
                        .022*s, .040*s, gold, vertices=8, parent=root)
        knob.rotation_euler[1] = math.pi/2
    _banker_shade(name+'_Green_shade', mats, width, .128*s, .145*s,
                  bottom, cy, root)
    beam(name+'_Shade_front_rim', (-width/2, cy-.128*s, bottom),
         (width/2, cy-.128*s, bottom), .011*s, .011*s,
         gold, parent=root, bevel=.002*s)
    tube = cylinder(name+'_Warm_tube', (0, cy, bottom+.035*s),
                    .015*s, width*.64, mats['emission'], vertices=8, parent=root)
    tube.rotation_euler[1] = math.pi/2
    _unshadowed_emitter(tube)
    switch = cylinder(name+'_Switch', (0, -.085*s, .064*s),
                      .013*s, .020*s, mats['wood_dark'], vertices=8, parent=root)
    switch.rotation_euler[0] = .35
    anchor = group(name+'_Light_anchor', (0, cy, bottom-.008*s), parent=root)
    anchor['kind'] = 'light_anchor'
    anchor['light_type'] = 'area'
    anchor['energy_watts'] = float(light_energy)
    if light:
        _light(name+'_Downlight', root, anchor.location, kind='AREA',
               energy=light_energy, size=width*.64, size_y=.11*s,
               color=(1.0, .79, .50))
    return root


def _pendant_shade(name, mats, outer, thickness, sides, parent):
    """Closed shell formed by outer/inner lathe rings and annular end rims."""
    vertices, rings = [], []
    # All rings run from upper opening down to the lower opening.
    inner = [(max(.005, r-thickness), z) for r, z in outer]
    for profile in (outer, inner):
        profile_rings = []
        for radius, z in profile:
            ring = []
            for j in range(sides):
                theta = 2*math.pi*j/sides + math.pi/8
                ring.append(len(vertices))
                vertices.append((radius*math.cos(theta), radius*math.sin(theta), z))
            profile_rings.append(ring)
        rings.append(profile_rings)
    outside, inside = rings
    faces, inner_faces = [], []
    for k in range(len(outer)-1):
        for j in range(sides):
            l = (j+1) % sides
            faces.append((outside[k][j], outside[k+1][j],
                          outside[k+1][l], outside[k][l]))
            inner_faces.append(len(faces))
            faces.append((inside[k][j], inside[k][l],
                          inside[k+1][l], inside[k+1][j]))
    for j in range(sides):
        l = (j+1) % sides
        faces.append((outside[0][j], outside[0][l], inside[0][l], inside[0][j]))
        faces.append((outside[-1][j], inside[-1][j], inside[-1][l], outside[-1][l]))
    obj = mesh(name, vertices, faces, mats['green'], parent=parent)
    obj.data.materials.append(mats['cream'])
    for index in inner_faces:
        obj.data.polygons[index].material_index = 1
    return obj


def pendant_lamp(name, mats, drop=.8, radius=.27, parent=None,
                 light=True, light_energy=10.0):
    """Faceted hanging shade. Local Z=0 is the ceiling mounting position."""
    if drop <= .15 or radius <= .06:
        raise ValueError('Pendant needs drop>.15 and radius>.06')
    root = group(name, parent=parent)
    root['kind'] = 'pendant_lamp'
    cylinder(name+'_Ceiling_cap', (0, 0, -.025), .095, .05,
             mats['gold'], vertices=8, parent=root)
    cylinder(name+'_Cord', (0, 0, -drop/2), .008, drop,
             mats['wood_dark'], vertices=6, parent=root)
    profile = [(radius*.19, -drop+.08), (radius*.34, -drop+.035),
               (radius*.82, -drop-.115), (radius, -drop-.16),
               (radius, -drop-.18)]
    _pendant_shade(name+'_Shade', mats, profile, .012, 8, root)
    bulb = ico(name+'_Bulb', (0, 0, -drop-.105), .052,
               mats['emission'], subdivisions=1, parent=root)
    _unshadowed_emitter(bulb)
    anchor = group(name+'_Light_anchor', (0, 0, -drop-.19), parent=root)
    anchor['kind'] = 'light_anchor'
    anchor['light_type'] = 'point'
    anchor['energy_watts'] = float(light_energy)
    if light:
        _light(name+'_Light', root, anchor.location, energy=light_energy,
               size=.065, color=(1.0, .78, .46))
    return root
