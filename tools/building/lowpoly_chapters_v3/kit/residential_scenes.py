"""Independent residential-room assembly; all mesh algorithms live in prop kits.

Room doors are closed teleport portals.  Layouts intentionally do not try to
line up with the source campus floorplan.  Source IDs are traceability only.
"""
import math
from .scene_common import RoomBuilder
from . import furniture as F, special_props as S, residential_props as P


def _paint(b,pos,rotation=0,width=1.25,height=.9,style='landscape'):
    return b.add('painting',F.painting,pos,rotation,
                 dict(width=width,height=height,style=style,seed=b.spec.get('seed',7)),False)


def _bundle(b,mode,pos,rotation=0):
    return b.add('residential_accessories',P.desk_bundle,pos,rotation,
                 dict(mode=mode,seed=b.spec.get('seed',7)))


def _dorm(b):
    s=b.spec;w,d,h=b.width,b.depth,b.height;sid=s['id']
    upper=s.get('class_type')=='upper'
    doorx=w/2-1.05
    b.shell(doors=[dict(wall='rear',center=doorx,width=1.15,height=2.4,to='corridor_'+s['wing'])],
            windows=[dict(wall='left',center=2.5,width=1.7,height=2.25,bottom=.95,shape='arch')])
    # Full-size furniture is spaced around a clear central approach.
    bedx=.45
    b.add('bed',P.paneled_bed,(bedx,d-1.52,0),params=dict(width=1.4 if upper else 1.28,length=2.25,height=.56),
          anchors={'sleep_surface':[0,0,.65]})
    b.add('wardrobe',S.cabinet,(-w/2+1.05,d-.43,0),params=dict(width=1.42,depth=.58,height=2.35))
    b.add('nightstand',S.cabinet,(bedx-1.11,d-.91,0),params=dict(width=.58,depth=.48,height=.61,drawers=2))
    b.lamp((bedx-1.11,d-.91,.61))
    deskpos=(-w/2+.48,2.5,0);yaw=math.pi/2
    deskfactory=P.folding_desk if sid=='R13' else F.table
    b.add('folding_desk' if sid=='R13' else 'table',deskfactory,deskpos,yaw,
          dict(width=1.65,depth=.76,height=.80))
    b.chair((-w/2+1.35,2.5,0),-math.pi/2)
    b.lamp((-w/2+.48,3.05,.80),yaw)
    b.add('wash_table',P.wash_table,(w/2-.95,2.60,0),0,
          dict(width=1.0,depth=.59,height=.86,mirror=True))
    _paint(b,(bedx,d-.18,1.65),width=1.35,height=.95)
    b.lamp((doorx-.85,d-.18,2.55),kind='wall')
    b.lamp((-w/2+.18,4.12,2.5),math.pi/2,kind='wall')
    b.add('rug',P.rug,(.10,2.25,0),params=dict(width=2.2 if upper else 1.9,depth=1.45),interactive=False)
    if sid=='R05':
        b.shelf((-w/2+.29,4.65,0),math.pi/2,width=1.28,height=1.83,fill=0,seed=s['seed'])
    elif upper:
        b.bench((-1.2,.85,0),math.pi/2,width=1.55)
        b.table((-.25,1.05,0),width=.65,depth=.60,height=.51)
    elif sid!='R09':
        b.add('chest',S.chest,(1.63,.95,0),params=dict(width=.85,depth=.52,height=.48))
    # Personal props are source-backed types, without readable invented clues.
    primary={
      'R01':'manuscripts','R02':'rubbings','R03':'tool_roll','R04':'legal_records',
      'R05':'notebook','R06':'mechanical_tools','R07':'bowls','R08':'score_sheets',
      'R09':'botany_notebook','R10':'notebook','R11':'bedside_bandages','R12':'sketchbook',
      'R13':'notebook','R14':'cloth_kit','R15':'drawing_rolls','R16':'medical_bag'}[sid]
    _bundle(b,primary,(-w/2+.48,2.25,.805),yaw)
    if sid=='R02':
        b.add('drying_rack',S.rack,(1.9,1.0,0),-.2,dict(width=1.0,height=1.4))
    elif sid=='R03':
        b.add('vent_grille',P.vent_grille,(-w/2+.18,4.75,.40),yaw,dict(width=.75,height=.42),False)
        _bundle(b,'cloth_bag',(1.63,.95,.49))
    elif sid=='R06':
        _bundle(b,'gear_board',(1.63,.95,.49))
        _bundle(b,'oil_cloth',(bedx-1.11,d-.91,.62))
    elif sid=='R08':
        b.add('music_stand',P.music_stand,(1.8,1.45,0),-.32,dict(width=.55,height=1.32))
        _bundle(b,'shawl',(1.63,.95,.49))
    elif sid=='R09':
        b.add('planting_counter',S.work_counter,(1.4,1.20,0),params=dict(width=1.25,depth=.58,height=.80))
        for x in (1.02,1.4,1.78):
            b.add('seedling_pot',F.planter,(x,1.2,.8),params=dict(width=.22,height=.31,flower_count=0,seed=s['seed']),interactive=False)
    elif sid=='R10':
        b.table((.10,1.85,0),width=1.50,depth=1.05,height=.73)
        b.add('go_board',P.go_board,(.10,1.85,.735),params=dict(size=.72,lines=19))
        b.chair((.1,.85,0),math.pi)
    elif sid=='R11':
        b.add('foot_support',P.stool,(bedx,d-2.9,0),params=dict(width=.70,depth=.45,height=.28))
    elif sid=='R12':
        b.add('easel',P.easel,(1.7,1.55,0),-.25,dict(width=.73,height=1.7))
    elif sid=='R13':
        b.add('bottle_rack',P.crockery_shelf,(-w/2+.18,4.9,1.3),yaw,dict(width=.95),False)
        b.add('bottles',S.bottles,(-w/2+.48,2.1,.81),yaw,dict(count=4,spacing=.14))
        b.add('three_part_door_lock',P.seal_door_trim,(doorx,d-.14,0),params=dict(width=1.15,height=2.4),interactive=False)
    elif sid=='R16':
        _bundle(b,'bandage_rolls',(bedx-1.11,d-.91,.62))
    return b.finish(lighting=dict(with_sun=True,sun_direction=[1,.08,-.78]))


def _kitchen(b):
    w,d,h=b.width,b.depth,b.height
    combined=b.spec['variant']=='kitchen_lounge'
    b.shell(doors=[dict(wall='left',center=1.18,width=1.15,height=2.4)],
            windows=[dict(wall='rear',center=2.10,width=1.32,height=1.84,bottom=1.10,shape='arch')])
    stove_x=-w/2+1.13
    b.add('stove',S.stove,(stove_x,d-.48,0),params=dict(width=1.40,depth=.76,height=.91))
    b.add('cooker_hood',P.cooker_hood,(stove_x,d-.48,1.75),params=dict(width=1.65,depth=.80,height=1.62),interactive=False)
    b.add('work_counter',S.work_counter,(0,d-.50,0),params=dict(width=1.65,depth=.76,height=.91))
    b.add('sink',S.sink,(2.05,d-.50,0),params=dict(width=1.30,depth=.76,height=.91))
    b.add('crockery_shelf',P.crockery_shelf,(0,d-.13,1.78),params=dict(width=1.68),interactive=False)
    b.add('cooking_pot',S.vessel,(stove_x-.3,d-.60,.955),params=dict(radius=.19,height=.30,kind='pot'))
    b.add('cooking_pot',S.vessel,(stove_x+.35,d-.52,.955),params=dict(radius=.14,height=.20,kind='pot'))
    _bundle(b,'cooking_tools',(0,d-.52,.95))
    b.table((0,2.07,0),width=2.1 if combined else 1.75,depth=1.0,height=.79)
    for x in (-.6,.6):b.chair((x,1.23,0),math.pi)
    if combined:
        b.bench((w/2-.41,2.50,0),-math.pi/2,width=1.9)
        b.add('linen_cabinet',P.linen_cabinet,(-w/2+.50,3.30,0),math.pi/2,dict(width=.90,height=1.85))
        _bundle(b,'bandage_rolls',(.55,2.07,.80))
    else:
        b.add('cabinet',S.cabinet,(w/2-.80,1.70,0),0,dict(width=1.1,depth=.58,height=1.20))
    if b.id=='A-P':b.add('vent_grille',P.vent_grille,(0,d-.16,2.9),params=dict(width=.90,height=.38),interactive=False)
    if b.id=='B-P':
        b.add('cloth_rack',S.rack,(-w/2+.35,2.60,0),math.pi/2,dict(width=.65,height=1.5),False)
    b.lamp((.70,d-.20,2.7),kind='wall')
    b.lamp((0,2.6,3.45),kind='pendant')
    b.add('pitcher',P.pitcher,(-.60,2.07,.80),params=dict(radius=.12,height=.31))
    return b.finish(lighting=dict(with_sun=True,sun_direction=[.22,-1,-.78]))


def _bath(b):
    w,d,h=b.width,b.depth,b.height
    wash=b.spec['variant']=='wash_lounge'
    b.shell(doors=[dict(wall='rear',center=w/2-.95,width=1.12,height=2.4)],
            windows=[dict(wall='rear',center=-1.0,width=2.40,height=.72,bottom=2.52,shape='rect',cols=5,rows=2)])
    for x in (-1.55,-.15):
        b.add('wash_table',P.wash_table,(x,d-.48,0),params=dict(width=1.12,depth=.62,height=.86,mirror=True))
    b.add('linen_cabinet',P.linen_cabinet,(-w/2+.43,2.35,0),math.pi/2,dict(width=1.00,height=1.95))
    if wash:
        b.add('cleaning_counter',S.work_counter,(.25,2.20,0),params=dict(width=2.05,depth=.78,height=.84))
        _bundle(b,'bandage_rolls',(.25,2.20,.85))
        b.add('cloth_rack',S.rack,(w/2-.45,2.5,0),-math.pi/2,dict(width=1.30,height=1.65),False)
        b.bench((.0,.72,0),math.pi,width=1.7)
    else:
        b.add('bathtub',S.tub,(-.55,2.03,0),params=dict(width=1.9,depth=.90,height=.63))
        b.add('privacy_screen',S.screen,(1.55,2.7,0),math.pi/2,dict(width=1.8,height=1.75))
        b.add('stool',P.stool,(1.1,1.05,0),params=dict(width=.53,depth=.42,height=.40))
        b.add('cloth_rack',S.rack,(w/2-.43,3.0,0),-math.pi/2,dict(width=1.05,height=1.60),False)
    for x in (-2.35,.70):b.lamp((x,d-.18,2.25),kind='wall')
    b.lamp((-.15,2.6,3.42),kind='pendant')
    return b.finish(lighting=dict(with_sun=True,sun_direction=[.24,-1,-.78]))


def _lounge(b):
    w,d,h=b.width,b.depth,b.height
    b.shell(doors=[dict(wall='rear',center=w/2-1.10,width=1.2,height=2.4)],
            windows=[dict(wall='left',center=3.55,width=1.85,height=2.18,bottom=.95,shape='arch')])
    b.bench((-1.0,d-.55,0),width=2.6)
    _paint(b,(-1.0,d-.17,1.53),width=2.25,height=1.18)
    b.add('discussion_table',P.slate_table,(.1,2.65,0),params=dict(width=2.6,depth=1.35,height=.80))
    for y,rot in ((1.54,math.pi),(3.76,0)):
        for x in (-.65,.85):b.chair((x,y,0),rot)
    b.add('sideboard',S.cabinet,(-w/2+.43,1.45,0),math.pi/2,dict(width=1.45,depth=.65,height=1.05))
    b.lamp((-w/2+.43,1.45,1.05),math.pi/2)
    b.add('pitcher',P.pitcher,(.20,2.72,.815),params=dict(radius=.13,height=.30))
    for x in (-.45,.75):b.add('cup',S.vessel,(x,2.60,.815),params=dict(radius=.075,height=.12,kind='cup'))
    if b.id=='B-L':
        b.add('speaking_tube',P.speaking_tube,(1.25,d-.16,.8),params=dict(height=2.0),interactive=False)
    else:
        b.shelf((w/2-.98,3.25,0),0,width=1.5,height=2.25,fill=.55,seed=b.spec['seed'])
    b.lamp((.4,d-.18,2.86),kind='wall')
    b.plant((w/2-.75,.82,0),width=.62,height=.88,seed=b.spec['seed'])
    return b.finish(lighting=dict(with_sun=True,sun_direction=[1,.12,-.85]))


def _music(b):
    w,d,h=b.width,b.depth,b.height
    b.shell(doors=[dict(wall='rear',center=w/2-1.0,width=1.15,height=2.4)],
            windows=[dict(wall='left',center=2.25,width=1.75,height=2.35,bottom=.90,shape='arch')])
    b.add('keyboard_instrument',P.keyboard,(-.65,d-.46,0),params=dict(width=1.85,depth=.75,height=1.5))
    b.add('piano_bench',P.stool,(-.65,d-1.5,0),params=dict(width=1.15,depth=.42,height=.5))
    b.add('music_stand',P.music_stand,(-1.2,2.45,0),-.15,dict(width=.60,height=1.35))
    b.chair((.0,1.75,0),-.3,arms=True)
    b.shelf((-w/2+.30,4.5,0),math.pi/2,width=1.55,height=2.35,fill=.55,seed=b.spec['seed'])
    b.add('cabinet',S.cabinet,(w/2-.82,3.1,0),0,dict(width=1.2,depth=.60,height=1.15))
    _bundle(b,'score_sheets',(w/2-.82,3.1,1.16),0)
    _paint(b,(-.65,d-.16,2.05),width=1.2,height=.8)
    b.lamp((.75,d-.18,2.72),kind='wall')
    b.add('rug',P.rug,(-.55,2.2,0),params=dict(width=2.2,depth=1.8),interactive=False)
    return b.finish(lighting=dict(with_sun=True,sun_direction=[1,.08,-.85]))


def _corridor(b):
    w,d,h=b.width,b.depth,b.height;wing=b.spec.get('wing',b.spec.get('zone','A'))
    if wing not in ('A','B','C','D'):wing='A'
    first={'A':1,'B':5,'C':9,'D':13}[wing]
    targets=['R%02d'%(first+i) for i in range(4)]+[wing+'-P',{'A':'A-W','B':'B-M','C':'C-W','D':'D-W'}[wing]]
    if not b.id.startswith('corridor_'):targets=list(b.spec.get('door_targets',[]))+['']*6
    if w<d:
        centers=[1.25+i*(d-2.50)/5 for i in range(6)]
        b.shell(doors=[dict(wall='left',center=y,width=1.06,height=2.45,to=to) for y,to in zip(centers,targets)],
          windows=[dict(wall='rear',center=0,width=min(2.45,w-.8),height=2.45,bottom=.85,shape='arch')],tile=.82)
        for i in range(5):
            y=(centers[i]+centers[i+1])/2
            if i%2==0:b.lamp((-w/2+.18,y,2.65),math.pi/2,kind='wall')
            else:_paint(b,(-w/2+.17,y,1.65),math.pi/2,width=1.0,height=.85)
        b.bench((w/2-.50,d*.35,0),-math.pi/2,width=1.70)
        b.bench((w/2-.50,d*.65,0),-math.pi/2,width=1.70)
        for x in (-w*.30,w*.30):b.plant((x,d-.65,0),width=.62,height=.87,seed=b.spec.get('seed',7))
        b.add('speaking_tube',P.speaking_tube,(-w/2+.16,d-.50,.80),math.pi/2,dict(height=2.15),False)
        b.root['portal_names']='host_owned; no readable invented door signs'
        return b.finish(lighting=dict(with_sun=True,sun_direction=[.12,-1,-.62]))
    centers=[-7.6,-5.05,-2.5,.10,3.0,5.85]
    doors=[dict(wall='rear',center=x,width=1.13,height=2.45,to=to) for x,to in zip(centers,targets)]
    b.shell(doors=doors,windows=[dict(wall='left',center=d*.5,width=2.15,height=2.60,bottom=.68,shape='arch')],tile=.82)
    for x in (-6.325,-1.2,4.42,7.45):b.lamp((x,d-.18,2.62),kind='wall')
    b.bench((1.55,d-.50,0),width=1.45)
    b.bench((-3.78,d-.50,0),width=1.38)
    for x in (-3.78,1.55):_paint(b,(x,d-.17,1.70),width=1.35,height=.88)
    for x in (-8.50,7.95):b.plant((x,d-.55,0),width=.65,height=.95,seed=b.spec['seed'])
    b.add('speaking_tube',P.speaking_tube,(8.48,d-.16,.8),params=dict(height=2.20),interactive=False)
    # Plates are intentionally blank; display names and destinations live in metadata.
    b.root['portal_names']='host_owned; no readable invented door signs'
    camera=dict(location=[15,-20,17],target=[0,d*.5,1.50],projection='ORTHO',margin=1.07)
    return b.finish(camera=camera,lighting=dict(with_sun=True,sun_direction=[1,.03,-.55]))


def _airlock(b):
    from . import industrial_props as I
    w,d,h=b.width,b.depth,b.height
    doors=[dict(wall='rear',center=-1.25,width=1.30,height=2.50),
           dict(wall='left',center=1.45,width=1.30,height=2.50)]
    b.shell(doors=doors,windows=[])
    b.add('seal_door_trim',P.seal_door_trim,(-1.25,d-.14,0),params=dict(width=1.30,height=2.50),interactive=False)
    b.add('seal_door_trim',P.seal_door_trim,(-w/2+.14,1.45,0),math.pi/2,dict(width=1.30,height=2.50),False)
    b.add('pressure_gauge',I.gauge,(1.1,d-.17,2.05),params=dict(radius=.25,needle_degrees=42))
    b.add('interlock_handle',I.interlock_handle,(1.1,d-.22,1.18),params=dict(radius=.26))
    b.add('indicator_board',I.indicator_board,(2.15,d-.18,1.10),params=dict(width=.38,height=1.25,kind='warning'))
    b.add('linen_cabinet',P.linen_cabinet,(w/2-.70,2.45,0),0,dict(width=.85,height=1.85))
    b.bench((-.25,.8,0),math.pi,width=1.65)
    b.lamp((0,d-.18,2.85),kind='wall')
    b.lamp((-.15,2.65,3.40),kind='pendant')
    return b.finish(lighting=dict(with_sun=False))


BUILDERS={'dorm':_dorm,'kitchen':_kitchen,'kitchen_lounge':_kitchen,
          'bath':_bath,'wash_lounge':_bath,'lounge':_lounge,
          'music':_music,'corridor':_corridor,'airlock':_airlock}


def build_residential(spec,mats):
    """Build one complete Blender scene tree and tagged reusable prefabs."""
    variant=spec['variant']
    if variant not in BUILDERS:raise ValueError('Unknown residential variant '+variant)
    b=RoomBuilder(spec,mats)
    b.root['design_process']='source audit > reviewed concepts > independent props > assembly'
    b.root['door_behavior']='teleport'
    b.root['concept_refs']=';'.join(spec.get('concept_refs',[]))
    return BUILDERS[variant](b)


__all__=['build_residential']
