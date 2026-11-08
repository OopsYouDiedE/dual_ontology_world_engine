"""Independent chapter public rooms, assembled from concept-fitted prefabs.

Door transforms are visual layout only. Opening a door is intended to switch
rooms through the host; no adjacent-room alignment or story state is authored.
"""
import math
from .scene_common import RoomBuilder
from . import core as C, furniture as F, openings as O, variants as V
from . import architecture as A, special_props as P, public_props as Q
from . import hall_architecture as H


def _default_openings(b,large=False):
    w,d,h=b.width,b.depth,b.height
    doors=[dict(wall='rear',center=-w*.34,width=1.35 if large else 1.15,height=2.5 if large else 2.3)]
    windows=[dict(wall='rear',center=w*.19,width=min(2.0,w*.28),height=min(2.6,h-1.15),bottom=.9,shape='arch'),
             dict(wall='left',center=d*.56,width=min(1.8,d*.26),height=min(2.5,h-1.2),bottom=.95,shape='arch')]
    return doors,windows


def _reading_table(b,x,y,width=2.2,depth=1.1,chairs=2):
    b.table((x,y,0),width=width,depth=depth,height=.8)
    if chairs:
        b.chair((x,y-depth/2-.55,0),math.pi)
        if chairs>1:b.chair((x,y+depth/2+.55,0),0)
    b.lamp((x-width*.3,y,.8))
    b.add('open_book',F.open_book,(x+.15,y-.1,.8),0,dict(width=.43,depth=.30),False)


def _board(b,pos,width=3,height=1.55):
    return b.add('chalkboard',P.chalkboard,pos,0,dict(width=width,height=height))


def _dining(b):
    w,d,h=b.width,b.depth,b.height
    b.shell(doors=[dict(wall='rear',center=x,width=1.4,height=2.6) for x in (-w*.38,w*.38)],
      windows=[dict(wall='rear',center=0,width=3.1,height=3.2,bottom=1.1,shape='arch'),
               *[dict(wall='left',center=y,width=2.0,height=2.8,bottom=1.15,shape='arch') for y in (d*.32,d*.70)]],tile=.95)
    for x in (-3.35,3.35):
        for y in (3.0,8.2):
            b.table((x,y,0),width=4.6,depth=1.25,height=.82)
            for side in (-1,1):
                for seat in range(6):
                    sx=x-1.85+seat*.74
                    b.chair((sx,y+side*1.14,0),math.pi if side<0 else 0)
                    b.add('plate',P.vessel,(sx,y+side*.43,.82),0,dict(radius=.15,height=.035,kind='plate'),False)
                    b.add('cup',P.vessel,(sx+.20,y+side*.28,.82),0,dict(radius=.055,height=.10,kind='cup'),False)
            b.lamp((x,y,h-.25),kind='pendant')
    b.add('dish_rack',Q.dish_rack,(w/2-.48,d-2.0,0),-math.pi/2,dict(width=2.0,height=2.0))
    b.plant((-w/2+.6,d-.65,0),width=.8,height=1)


def _lecture(b):
    w,d,h=b.width,b.depth,b.height
    b.shell(doors=[dict(wall='rear',center=x,width=1.2,height=2.5) for x in (-w*.38,w*.38)],
        windows=[dict(wall='left',center=y,width=1.7,height=2.8,bottom=1,shape='arch') for y in (d*.25,d*.60)],tile=.88)
    C.box(b.id+'_Teaching_Dais',(0,d-1.4,.10),(w*.64,2.5,.2),b.mats['stone'],.025,b.static)
    C.box(b.id+'_Dais_Step',(0,d-2.8,.05),(w*.42,.35,.1),b.mats['stone'],.015,b.static)
    _board(b,(0,d-.19,1.28),width=4.0,height=1.8)
    b.add('lectern',P.lectern,(1.2,d-1.7,.2),0,dict(width=.88,height=1.2))
    for x in (-w*.26,w*.26):
        for row,y in enumerate((1.7,3.45,5.2)):
            b.table((x,y,0),width=2.3,depth=.75,height=.78)
            for dx in (-.60,.60):b.chair((x+dx,y-.78,0),math.pi)
            if row==1:b.lamp((x-.8,y,.78))
            b.add('open_book',F.open_book,(x+.35,y-.06,.78),0,dict(width=.35),False)
    for x in (-2.55,2.55):b.shelf((x,d-.31,.2),width=.95,height=1.85,fill=.55)
    b.add('chandelier',H.chandelier,(0,d*.48,h-.05),0,dict(radius=.8,drop=.9,arms=6,energy=65),False)


def _common_hall(b,meeting=False):
    doors,windows=_default_openings(b,True)
    doors.append(dict(wall='rear',center=b.width*.36,width=1.35,height=2.5))
    windows=[dict(wall='left',center=b.depth*.40,width=2,height=2.8,bottom=1,shape='arch')]
    b.shell(doors=doors,windows=windows,tile=.9)
    for x,kind in ((-1.5,'record'),(0,'audio'),(1.5,'monitor')):
        b.add('record_station',P.record_station,(x,b.depth-.48,0),0,dict(width=1.05,height=1.65,kind=kind))
    b.add('records_pillar',Q.records_pillar,(0,b.depth*.46,0),0,dict(radius=.63,height=1.65))
    b.add('lectern',P.lectern,(0,b.depth*.46-1.03,0),0,dict(width=.7,height=1.08))
    for side in (-1,1):b.bench((side*(b.width/2-.70),b.depth*.40,0),math.pi/2 if side<0 else -math.pi/2,width=2.4)
    _reading_table(b,-b.width*.29,b.depth*.72,width=1.5,chairs=1)
    b.plant((b.width*.35,b.depth*.73,0),width=.7,height=.88)
    b.add('chandelier',H.chandelier,(0,b.depth*.48,b.height-.05),0,dict(radius=.8,drop=.85,arms=6,energy=65),False)
    if meeting:
        _board(b,(0,b.depth-.18,2.12),width=2.7,height=.9)
        b.add('painting',F.painting,(-b.width/2+.16,b.depth*.73,1.8),math.pi/2,
              dict(width=1.1,height=.8,style='city'),False)


def _kitchen(b,pantry=False):
    doors,windows=_default_openings(b)
    b.shell(doors=doors,windows=windows,tile=.66)
    w,d=b.width,b.depth
    if pantry:
        for x in (-w*.23,w*.16):
            b.add('dish_rack',Q.dish_rack,(x,d-.36,0),0,dict(width=1.7,height=2.1))
        b.add('sink',P.sink,(-w/2+.42,d*.45,0),math.pi/2,dict(width=1.4))
    else:
        b.add('stove',P.stove,(-w/2+.45,d*.40,0),math.pi/2,dict(width=2.2,depth=.8,height=.92))
        # Faceted extraction hood; independent reusable prop from residential kit.
        from .residential_props import kitchen_hood
        b.add('kitchen_hood',kitchen_hood,(-w/2+.45,d*.40,2.2),math.pi/2,dict(width=2.25))
        b.add('sink',P.sink,(w*.18,d-.42,0),0,dict(width=2.1))
        b.add('cabinet',P.cabinet,(w/2-.4,d*.70,0),-math.pi/2,dict(width=1.8,depth=.55,height=2.1))
    b.add('work_counter',P.work_counter,(0,d*.46,0),0,dict(width=2.7,depth=1.1,height=.9))
    for x in (-.7,.6):
        b.add('pot',P.vessel,(x,d*.46,.90),0,dict(radius=.23,height=.3,kind='pot'),False)
    b.add('bottles',P.bottles,(.4,d*.46+.26,.90),0,dict(count=4,spacing=.16),False)
    b.lamp((0,d*.43,b.height-.07),kind='pendant')


def _archive(b):
    doors,windows=_default_openings(b,True)
    b.shell(doors=doors,windows=windows,floor='mosaic',tile=.8)
    w,d=b.width,b.depth
    b.add('flat_file',P.cabinet,(.55,d-.35,0),0,dict(width=2.8,depth=.6,height=1.2,drawers=6))
    b.shelf((2.7,d-.28,0),width=1.25,height=2.9,fill=.72)
    b.shelf((-w/2+.26,d*.55,0),math.pi/2,width=2.7,height=2.9,fill=.7)
    _reading_table(b,0,d*.43,width=2.9,depth=1.45)
    b.add('balance',Q.balance,(.7,d*.43+.1,.8),0,dict(width=.68,height=.7))
    b.add('magnifier',Q.magnifier,(-.8,d*.43,.8),0,dict(height=.6))
    b.add('work_counter',P.work_counter,(w/2-.45,d*.65,0),-math.pi/2,dict(width=1.6,depth=.7,height=.9))
    b.add('stamp_press',Q.stamp_press,(w/2-.50,d*.65,.9),-math.pi/2,dict(width=.6,height=.7))
    b.add('books',F.books,(.2,d-.35,1.2),0,dict(width=.42,depth=.32,height=.21),False)
    b.lamp((0,d*.54,b.height-.1),kind='pendant')


def _clinic(b,pharmacy=False):
    doors=[dict(wall='rear',center=0,width=1.3,height=2.4)]
    windows=[dict(wall='left',center=y,width=1.6,height=2.35,bottom=.9,shape='arch') for y in (b.depth*.30,b.depth*.7)]
    b.shell(doors=doors,windows=windows,tile=.7)
    w,d=b.width,b.depth
    for x in (-w*.30,w*.30):
        b.add('medicine_cabinet',Q.medicine_cabinet,(x,d-.3,0),0,dict(width=1.6,height=2.4))
    if pharmacy:
        _reading_table(b,0,d*.45,width=2.8,depth=1.3)
        b.add('balance',Q.balance,(.6,d*.45,.8),0,dict(width=.65,height=.6))
        b.add('bottles',P.bottles,(0,d*.45+.32,.8),0,dict(count=7,spacing=.18),False)
        for x in (-w/2+.4,w/2-.4):
            b.add('cabinet',P.cabinet,(x,d*.45,0),math.pi/2 if x<0 else -math.pi/2,
                  dict(width=2.0,depth=.55,height=1.2,drawers=4))
    else:
        for x in (-w*.30,w*.30):
            for y in (2.0,4.95):
                b.add('medical_bed',P.bed,(x,y,0),0,dict(width=1.12,length=2.0,height=.58,medical=True))
                b.add('bedside_cabinet',P.cabinet,(x-.86,y+.45,0),0,dict(width=.5,depth=.48,height=.65,drawers=2))
            b.add('privacy_screen',P.screen,(x,3.55,0),0,dict(width=1.9,height=1.7))
        _reading_table(b,0,1.6,width=1.5,depth=.7,chairs=1)
    b.add('sink',P.sink,(w/2-.38,d*.75,0),-math.pi/2,dict(width=1.3,depth=.6,height=.87))
    for x in (-w*.25,w*.25):b.lamp((x,d*.48,b.height-.10),kind='pendant')


def _greenhouse(b):
    w,d,h=b.width,b.depth,b.height
    doors=[dict(wall='rear',center=0,width=1.3,height=2.45)]
    windows=[dict(wall='rear',center=x,width=1.7,height=2.55,bottom=.8,shape='arch') for x in (-2.4,2.4)]
    windows.extend(dict(wall='left',center=y,width=1.65,height=2.55,bottom=.8,shape='arch') for y in (1.4,3.8,6.2))
    b.shell(doors=doors,windows=windows,tile=.78)
    b.add('greenhouse_frame',Q.greenhouse_frame,(0,0,0),0,
          dict(width=w,depth=d,eave=3.6,rise=1.0,bays=4),False)
    for x in (-2.25,2.25):
        b.add('planting_bed',Q.planting_bed,(x,3.2,0),0,dict(width=1.65,depth=4.4,height=.62,seed=9 if x<0 else 14))
    b.add('work_counter',P.work_counter,(-2.3,d-.44,0),0,dict(width=2.1,depth=.7,height=.88))
    b.add('watering_can',Q.watering_can,(-2.4,d-.55,.88),0,dict(radius=.20,height=.38))
    b.add('medicine_cabinet',Q.medicine_cabinet,(2.45,d-.32,0),0,dict(width=1.6,height=2.0))


def _resonance(b,quiet=False):
    doors,windows=_default_openings(b,True)
    if quiet:windows=[]
    b.shell(doors=doors,windows=windows,tile=.86)
    w,d,h=b.width,b.depth,b.height
    if quiet:
        # Alternating raised brown panels give a visible acoustic chamber.
        for i in range(7):
            x=-w*.4+i*w*.8/6
            C.box(b.id+'_Acoustic_Block',(x,d-.20,1.9),(.45,.18,2.8),b.mats['wood_dark'],.025,b.static)
        b.bench((0,d*.55,0),width=2.4)
        b.add('record_station',P.record_station,(-w*.29,d*.40,0),0,dict(width=.85,height=1.25,kind='audio'))
        b.add('lectern',P.lectern,(w*.25,d*.5,0),0,dict(width=.6,height=1.05))
    else:
        C.cylinder(b.id+'_Octagonal_Dais',(0,d*.44,.12),2.0,.24,b.mats['stone'],8,b.static)
        b.add('armillary',Q.armillary,(0,d*.44,.24),0,dict(radius=1.12,height=2.7))
        b.add('resonance_tubes',Q.resonance_tubes,(.5,d-.43,0),0,dict(width=3.6,height=3.05,count=11))
        from .residential_props import upright_piano
        b.add('upright_piano',upright_piano,(-w/2+.4,d*.60,0),math.pi/2,dict(width=1.7))
        b.bench((w/2-.65,d*.48,0),-math.pi/2,width=2.4)
        _reading_table(b,w*.28,1.5,width=1.6,depth=.8,chairs=1)
    for x in (-w*.27,w*.27):b.lamp((x,d-.03,2.6),kind='wall')


def _supply(b,rations=False):
    doors,windows=_default_openings(b)
    b.shell(doors=doors,windows=windows,tile=.78)
    w,d=b.width,b.depth
    b.add('reception_desk',F.reception_desk,(0,d*.46,0),0,
          dict(width=3.1,depth=1.1,height=1.0,panels=4,details=False))
    b.chair((0,d*.46+1.1,0),0)
    b.add('open_book',F.open_book,(-.8,d*.46-.30,1.0),0,dict(width=.45),False)
    b.lamp((1.05,d*.46-.30,1.0))
    b.add('balance',Q.balance,(.1,d*.46-.32,1.0),0,dict(width=.6,height=.6))
    for x in (-w*.27,w*.26):
        b.add('cabinet',P.cabinet,(x,d-.35,0),0,dict(width=1.85,depth=.6,height=2.25))
        b.add('crates',P.crates,(x,1.25,0),0,dict(width=.9,depth=.72,height=.65,stack=2))
    if rations:b.add('sacks',P.sacks,(w*.27,d*.65,0),0,dict(count=4),False)
    else:b.bench((-w/2+.6,d*.5,0),math.pi/2,width=2.0)


def _utility(b,variant):
    from . import industrial_props as I
    if variant=='washroom':
        b.shell(doors=[dict(wall='rear',center=-b.width*.31,width=1.2,height=2.3)],
                windows=[dict(wall='rear',center=b.width*.21,width=1.8,height=1.15,bottom=2,shape='rect')],tile=.65)
        for x in (-1.7,0,1.7):b.add('sink',P.sink,(x,b.depth-.5,0),0,dict(width=1.35,depth=.65,height=.9))
        b.add('water_tank',I.reaction_vessel,(-b.width/2+.7,b.depth*.5,0),0,dict(radius=.5,height=2.25))
        b.add('rack',P.rack,(b.width*.28,1.7,0),0,dict(width=1.5,height=1.6))
        b.bench((0,1.8,0),width=1.8)
    else:
        b.shell(doors=[dict(wall='rear',center=0,width=1.3,height=2.35)],windows=[],tile=.7)
        b.add('pipe_run',I.pipe_run,(-b.width/2+.28,0,0),0,
          dict(nodes=[(0,.5,1.0),(0,b.depth*.55,1.0),(0,b.depth*.55,2.4),(0,b.depth-.3,2.4)],radius=.09),False)
        b.add('valve_station',I.valve_station,(b.width*.26,b.depth*.6,0),0,dict(width=1.3,height=1.5))
        if variant=='service_passage':
            b.add('closed_grille',Q.grille,(-b.width/2+.22,b.depth*.48,0),math.pi/2,dict(width=1.2,height=2.0))
            b.add('crates',P.crates,(b.width*.26,1.4,0),0,dict(width=.75,depth=.65,height=.5,stack=1))
        else:
            b.add('control_console',I.control_console,(0,b.depth*.32,0),0,dict(width=1.8,depth=.8,height=1.2))
            b.add('schematic',I.schematic,(-b.width/2+.2,b.depth*.58,1.5),math.pi/2,dict(width=1.8,height=1.0),False)
    b.lamp((0,b.depth*.5,b.height-.1),kind='pendant')


def build_public(spec,mats):
    b=RoomBuilder(spec,mats)
    variant=spec['variant']
    if variant=='dining':_dining(b)
    elif variant=='lecture':_lecture(b)
    elif variant in ('public_hall','meeting_hall'):_common_hall(b,variant=='meeting_hall')
    elif variant in ('kitchen','pantry'):_kitchen(b,variant=='pantry')
    elif variant=='document_workshop':_archive(b)
    elif variant in ('clinic','pharmacy_safe'):_clinic(b,variant=='pharmacy_safe')
    elif variant=='greenhouse':_greenhouse(b)
    elif variant in ('resonance','quiet_room'):_resonance(b,variant=='quiet_room')
    elif variant in ('logistics','ration_room'):_supply(b,variant=='ration_room')
    elif variant in ('washroom','service_passage','valve_safe'):_utility(b,variant)
    elif variant=='corridor':
        from .residential_scenes import build_residential
        copy=dict(spec,variant='corridor')
        return build_residential(copy,mats)
    else:raise ValueError('Unknown public room variant: '+variant)
    b.root['door_behavior']='host_teleport_between_independent_rooms'
    return b.finish(lighting={'with_sun':variant not in ('quiet_room','service_passage','valve_safe'),
                             'roof_helper':variant!='greenhouse'})
