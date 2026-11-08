"""Reusable double-height wall, gallery and faceted chandelier components.

All heights are finished surface elevations, in Blender Z-up metres.
Room assembly lives in hall_scene.py; nothing here knows the hall layout.
"""
import math
from . import architecture as A, lighting as L
from .core import group, box, beam, lathe, cylinder


def two_register_wall(name, points, mats, height=8.0, gallery_z=3.0,
                      thickness=.3, lower_openings=(), upper_openings=(), parent=None):
    root = group(name, parent=parent)
    A.wall_path(name+'_Lower', points, gallery_z, thickness, mats,
                openings=lower_openings, panel_height=.98, parent=root)
    # A separate upper register makes the height legible without stretching
    # the original 3m mouldings to eight metres.
    upper_mats = dict(mats)
    upper_mats['wood_dark'] = mats['wall']
    upper_mats['wood'] = mats['wall']
    upper_mats['wood_light'] = mats['wood']
    upper_points = [(p[0], p[1], p[2]+gallery_z) for p in points]
    A.wall_path(name+'_Upper', upper_points, height-gallery_z, thickness,
                upper_mats, openings=upper_openings, panel_height=.95, parent=root)
    root['generator'] = 'two_register_wall_v2'
    root['height'] = height
    root['gallery_elevation'] = gallery_z
    return root


def gallery_deck(name, bounds, z, mats, thickness=.3, parent=None, front_trim=True):
    """Rectangular deck; top is exactly z, never z plus a finishing layer."""
    x0, x1, y0, y1 = bounds
    root = group(name, parent=parent)
    box(name+'_Structure', ((x0+x1)/2, (y0+y1)/2, z-thickness/2-.025),
        (x1-x0, y1-y0, thickness-.05), mats['wood_dark'], .012, root)
    box(name+'_Stone_surface', ((x0+x1)/2, (y0+y1)/2, z-.0275),
        (x1-x0, y1-y0, .055), mats['stone'], .012, root)
    if front_trim:
        box(name+'_Front_fascia', ((x0+x1)/2, y0-.008, z-.16),
            (x1-x0, .11, .25), mats['wood'], .012, root)
        box(name+'_Front_bead', ((x0+x1)/2, y0-.072, z-.07),
            (x1-x0, .05, .055), mats['wood_light'], .006, root)
    root['bounds_xy'] = list(bounds)
    root['finished_elevation'] = z
    return root


def square_pier(name, location, height, mats, width=.38, parent=None):
    root = group(name, location, parent=parent)
    box(name+'_Shaft', (0,0,height/2), (width,width,height), mats['wood'], .018, root)
    box(name+'_Foot', (0,0,.12), (width+.13,width+.13,.24), mats['wood_dark'], .014, root)
    box(name+'_Foot_band', (0,0,.29), (width+.06,width+.06,.08), mats['wood_light'], .01, root)
    box(name+'_Capital', (0,0,height-.12), (width+.15,width+.15,.24), mats['wood_light'], .014, root)
    return root


def chandelier(name, mats, radius=.95, drop=1.65, arms=8, energy=360, parent=None):
    """Low-poly ring chandelier; base is the ceiling attachment point."""
    root = group(name, parent=parent)
    root['kind'] = 'chandelier'
    gold = mats['gold']
    cylinder(name+'_Ceiling_rose', (0,0,-.05), .20, .10, gold, 8, root)
    beam(name+'_Stem',(0,0,-.1),(0,0,-drop), .035,.035,gold,parent=root)
    z = -drop
    for i in range(arms):
        a,b = 2*math.pi*i/arms,2*math.pi*(i+1)/arms
        p,q=(radius*math.cos(a),radius*math.sin(a),z),(radius*math.cos(b),radius*math.sin(b),z)
        beam(name+'_Ring_'+str(i),p,q,.055,.075,gold,parent=root)
        if i%2==0:
            beam(name+'_Spoke_'+str(i),(0,0,z+.7),p,.022,.028,gold,parent=root)
        cylinder(name+'_Cup_'+str(i),(p[0],p[1],z+.04),.105,.07,gold,6,root)
        emitter=lathe(name+'_Glow_'+str(i),[(0,0),(.08,.03),(.11,.17),(.075,.31),(0,.36)],
                      6,mats['emission'],root,(p[0],p[1],z+.07))
        L._unshadowed_emitter(emitter)
    L._light(name+'_Point',root,(0,0,z-.12),energy=energy,size=.55)
    root['fixture_energy_watts'] = energy
    return root
