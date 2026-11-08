"""Independent courtyard, lane and harbor art-support scenes.

Six existing chapter-2 art-region IDs are preserved as provenance. Visual
arrangements do not claim six independently verified story events. Closed
gates are teleport presentation assets; adjacent scenes are never generated.
"""
import math

from . import core as C, flooring as T, furniture as F, special_props as S
from . import outdoor_props as P, lighting as L
from .scene_common import RoomBuilder


def _floor(b,width=None,depth=None,y=None,z=0,name='Outdoor_Stone_Paving'):
    width=width or b.width;depth=depth or b.depth
    root=T.tiled_floor(b.id+'_'+name,b.mats,width=width,depth=depth,tile=.90,
                       cut=.11,border=.36,seed=11,parent=b.static)
    C.place(root,(0,b.depth/2 if y is None else y,z-.04))
    return root


def _wall(b,center,length,height=1.05,yaw=0):
    root=P.stone_wall(b.id+'_Boundary_'+str(len(b.static.children)),b.mats,
                      length=length,height=height,parent=b.static)
    C.place(root,center,yaw)
    return root


def _rear_gate(b,x=0,z=0,width=2.2,height=2.7,wall_height=1.1):
    # The stone gate frame occupies width+0.9; adjoining wall stops at it.
    extent=width/2+.45
    for a,c in ((-b.width/2,x-extent),(x+extent,b.width/2)):
        if c-a>.08:_wall(b,((a+c)/2,b.depth,z),c-a,wall_height)
    gate=b.add('courtyard_gate',P.courtyard_gate,(x,b.depth,z),0,
               dict(width=width,height=height,rise=.65),name=b.id+'_Closed_Teleport_Gate')
    gate['transition_mode']='teleport';gate['destination_scene_hint']='unassigned'
    b.ports.append(dict(id=gate['instance_id'],wall='rear',position=list(gate.location),transition='teleport'))
    return gate


def _tree(b,pos,height=3.7,radius=1.12,seed=7):
    return b.add('faceted_tree',P.tree,pos,0,dict(height=height,crown_radius=radius,seed=seed),False)


def _bed(b,pos,width=2.0,depth=.85,shrubs=True):
    return b.add('stone_planter_bed',P.planter_bed,pos,0,
                 dict(width=width,depth=depth,height=.42,shrubs=shrubs),False)


def _lamp(b,pos,height=2.8):
    return b.add('outdoor_lamp',P.lamp_post,pos,0,dict(height=height,energy=20),False)


def _instrument(b,pos,kind='armillary',rotation=0):
    return b.add('outdoor_instrument',P.instrument_table,pos,rotation,
                 dict(width=1.40,depth=.72,height=.88,instrument=kind),True,
                 anchors={'Measurement':{'position':[0,-.1,1.28],'pose':'stand'}},
                 boxes=[dict(name='TableTop',center=[0,0,.84],size=[1.40,.72,.08])])


def _courtyard(b,variant):
    w,d=b.width,b.depth
    _floor(b)
    _wall(b,(-w/2,d/2,0),d,1.10,math.pi/2)
    _wall(b,(w/2,d/2,0),d,.82,math.pi/2)
    gate_x=w*.28 if variant=='oath_court' else 0
    _rear_gate(b,gate_x,width=1.8 if variant=='oath_court' else 2.2)
    _lamp(b,(-w/2+.42,.6,0));_lamp(b,(w/2-.42,d-.55,0))
    if variant=='instrument_terrace':
        platform=b.add('raised_terrace',P.oath_platform,(0,d-2.1,0),0,
                        dict(width=5.4,depth=2.65,height=.60),False)
        _instrument(b,(-.85,d-2.1,.60));_instrument(b,(1.05,d-2.1,.60),'sextant')
        b.add('fountain',P.fountain,(-w*.23,d*.32,0),0,dict(radius=.78,height=1.1),False)
        b.bench((w*.28,d*.33,0),-math.pi/2,width=1.8)
        _bed(b,(-w*.34,d*.73,0),1.3,1.3,False)
        _tree(b,(-w*.34,d*.73,.36),height=3.75,radius=.95,seed=5)
        _bed(b,(w*.34,1.25,0),1.5,.90)
    elif variant=='oath_court':
        b.add('oath_platform',P.oath_platform,(0,d-2.05,0),0,
              dict(width=4.0,depth=2.35,height=.60),False)
        b.add('lectern',S.lectern,(0,d-1.9,.60),0,dict(width=.86,depth=.62,height=1.18))
        b.add('scholarly_statue',P.scholarly_statue,(-w*.33,d*.73,0),0,dict(height=2.9),False)
        b.add('fountain',P.fountain,(0,d*.35,0),0,dict(radius=1.02,height=1.40),False)
        for sign in (-1,1):
            b.bench((sign*(w/2-.72),d*.43,0),math.pi/2 if sign<0 else -math.pi/2,width=2.0)
        _instrument(b,(w*.28,d*.20,0))
        _bed(b,(-w*.34,d-.7,0),1.4,1.1,False)
        _tree(b,(-w*.34,d-.7,.34),height=3.75,radius=1.08,seed=7)
        _bed(b,(w*.30,d*.72,0),1.3,.8)
    elif variant=='statue_garden':
        b.add('scholarly_statue',P.scholarly_statue,(-w*.21,d*.62,0),0,dict(height=2.8),False)
        b.add('fountain',P.fountain,(w*.20,d*.31,0),0,dict(radius=.82,height=1.12),False)
        b.bench((-w*.34,d*.25,0),math.pi/2,width=1.8)
        b.bench((w*.34,d*.65,0),-math.pi/2,width=1.8)
        _tree(b,(-w*.36,d*.82,0),height=3.8,radius=1.10,seed=9)
        _tree(b,(w*.34,d*.86,0),height=3.4,radius=.9,seed=13)
        _bed(b,(0,d*.16,0),2.0,.75)
    else:
        b.add('fountain',P.fountain,(0,d*.45,0),0,dict(radius=1.08,height=1.42),False)
        b.add('scholarly_statue',P.scholarly_statue,(-w*.29,d*.73,0),0,dict(height=2.6),False)
        for sign in (-1,1):
            b.bench((sign*(w/2-.68),d*.38,0),math.pi/2 if sign<0 else -math.pi/2,width=1.9)
        _instrument(b,(w*.24,d*.21,0),'sextant')
        for sign,seed in ((-1,5),(1,12)):
            _bed(b,(sign*w*.32,d*.88,0),1.65,1.0,False)
            _tree(b,(sign*w*.32,d*.88,.35),height=3.65,radius=1.02,seed=seed)


def _alley(b):
    w,d=b.width,b.depth
    # A broad upper landing owns the top of two exact 0.2m steps.
    lower_depth=d-2.2
    _floor(b,depth=lower_depth,y=lower_depth/2)
    C.box(b.id+'_Upper_Landing_Foundation',(0,d-1.1,-.10),
          (w,2.2,.40),b.mats['stone_dark'],.015,b.static)
    _floor(b,depth=2.2,y=d-1.1,z=.40,name='Upper_Landing')
    for i in range(2):
        y=lower_depth-.50+i*.25
        C.box(b.id+'_Landing_Step_%d'%i,(0,y+.125,(i+1)*.20/2),
              (w-.10,.25,(i+1)*.20),b.mats['stone'],.009,b.static)
    _wall(b,(-w/2,d/2,0),d,2.25,math.pi/2)
    _wall(b,(w/2,d/2,0),d,.70,math.pi/2)
    _rear_gate(b,z=.40,width=2.15,height=2.6,wall_height=2.25)
    b.bench((-w/2+.65,d*.40,0),math.pi/2,width=1.75)
    b.bench((w/2-.65,d*.66,0),-math.pi/2,width=1.6)
    _bed(b,(-w/2+.75,1.0,0),1.2,1.2)
    _bed(b,(-w/2+.74,d*.66,0),1.25,1.2,False)
    _tree(b,(-w/2+.74,d*.66,.35),height=3.7,radius=.95,seed=9)
    _lamp(b,(-w/2+.42,d*.23,0));_lamp(b,(w/2-.45,d-.55,.40))


def _harbor(b):
    w,d=b.width,b.depth;quay_y=4.2
    # One island base; water is a separate noninteractive visual prefab.
    outline=[(-w/2+.6,0),(w/2-.6,0),(w/2,.6),(w/2,d),(-w/2,d),(-w/2,.6)]
    C.polyprism(b.id+'_Island_Base',outline,-.72,-.34,b.mats['stone_dark'],b.static)
    _floor(b,depth=d-quay_y,y=(d+quay_y)/2)
    b.add('faceted_water',P.water_patch,(0,quay_y/2,-.33),0,
          dict(width=w-.12,depth=quay_y-.05,seed=13),False)
    _wall(b,(-w/2,(quay_y+d)/2,0),d-quay_y,1.15,math.pi/2)
    _wall(b,(w/2,(quay_y+d)/2,0),d-quay_y,.72,math.pi/2)
    _rear_gate(b,x=-.75,width=2.2,height=2.6,wall_height=1.35)
    C.box(b.id+'_Quay_Timber_Edge',(0,quay_y-.04,-.12),(w,.18,.28),b.mats['wood'],.012,b.static)
    for x in (-4.0,-2.0,0,2.0,4.0):
        C.beam(b.id+'_Quay_Pile',(x,quay_y,-.69),(x,quay_y,-.12),.16,.18,b.mats['wood_dark'],b.static)
        C.ico(b.id+'_Edge_Bolt',(x,quay_y-.14,-.10),.035,b.mats['gold'],1,b.static)
    b.add('wooden_dock',P.dock,(.35,quay_y-1.60,0),0,dict(width=2.3,length=3.20),False)
    boat=b.add('rowboat',P.rowboat,(2.55,1.65,-.38),-.18,
                dict(length=2.65,width=1.05,height=.55),False)
    # Visible plank links quay/dock to the small boat; no room graph is implied.
    gang=C.group(b.id+'_Gangplank',parent=b.static)
    for i in range(5):
        x=1.40+(i+.5)*.18
        board=C.box(b.id+'_Gangplank_Board_%d'%i,(x,1.85,.015+.18*(i+.5)/5),(.17,.48,.06),b.mats['wood_light'],.006,gang)
        board.rotation_euler[1]=-math.atan(.18/.90)
    for y in (1.64,2.06):
        C.beam(b.id+'_Gangplank_Stringer',(1.40,y,-.025),(2.30,y,.155),.045,.065,b.mats['wood_dark'],gang,.004)
    b.add('cargo_crates',S.crates,(-3.85,6.55,0),0,dict(width=.90,depth=.70,height=.72,stack=2))
    b.add('cargo_crates',S.crates,(-2.85,5.50,0),.12,dict(width=.72,depth=.62,height=.65,stack=1))
    b.add('barrel',P.barrel,(-2.65,7.00,0),0,dict(radius=.37,height=.88))
    b.add('barrel',P.barrel,(-3.10,6.15,0),0,dict(radius=.30,height=.70))
    _instrument(b,(2.4,6.65,0),'sextant')
    b.add('rope_coil',P.rope_coil,(.75,5.0,0),0,dict(radius=.36),False)
    for x in (-3.85,-1.55,2.85,4.25):
        b.add('mooring_bollard',P.bollard,(x,quay_y+.10,0),0,dict(height=.53),False)
    _lamp(b,(-4.35,7.4,0));_lamp(b,(4.25,4.65,0))


def build_outdoor(spec,mats):
    b=RoomBuilder(spec,P.outdoor_materials(mats));b.mats=P.outdoor_materials(b.mats)
    variant=spec.get('variant','courtyard')
    if variant=='harbor':_harbor(b)
    elif variant=='alley':_alley(b)
    else:_courtyard(b,variant)
    b.root['scene_scope']='Independent outdoor art-support plot; no adjacent scene topology.'
    b.root['scope_evidence']=spec.get('scope_evidence','art_support_scene')
    b.root['source_region_id']=spec.get('source_region_id',spec['id'])
    camera=dict(location=[b.width*1.03,-b.depth*1.12,max(b.width,b.depth)*1.05],
                 target=[0,b.depth*.48,1.0],projection='ORTHO',margin=1.12)
    if variant=='alley':
        camera=dict(location=[8,-12,13],target=[0,b.depth*.52,1.1],projection='ORTHO',margin=1.11)
    result=b.finish(camera=camera,lighting={'style':'warm_outdoor','outdoor':True,
                                           'roof_helper':False,'sun_direction':[.45,-.7,-1]})
    result['bounds_blender'][0][2]=-.80 if variant=='harbor' else -.32
    result['provenance_status']=spec.get('provenance_status','existing_art_region_support')
    return result


build=build_outdoor
