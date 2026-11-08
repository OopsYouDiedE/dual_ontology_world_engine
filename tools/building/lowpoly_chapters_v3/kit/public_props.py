"""Concept-fitted public-room objects, independent of room assembly."""
import math
from . import core as C, special_props as P, furniture as F


def ring_band(name,mats,radius=1.0,width=.07,depth=.045,segments=24,parent=None):
    root=C.group(name,parent=parent)
    vertices=[]
    for r,z in ((radius-width/2,-depth/2),(radius+width/2,-depth/2),
                (radius-width/2,depth/2),(radius+width/2,depth/2)):
        vertices.extend((r*math.cos(2*math.pi*i/segments),r*math.sin(2*math.pi*i/segments),z) for i in range(segments))
    faces=[]
    for i in range(segments):
        j=(i+1)%segments
        faces.extend([(i,j,segments+j,segments+i),(2*segments+i,3*segments+i,3*segments+j,2*segments+j),
                      (i,2*segments+i,2*segments+j,j),(segments+i,segments+j,3*segments+j,3*segments+i)])
    C.mesh(name+'_Band',vertices,faces,mats['gold'],root,.003)
    return root


def armillary(name,mats,radius=1.12,height=2.7,parent=None):
    root=C.group(name,parent=parent)
    C.lathe(name+'_Pedestal',[(.65,0),(.68,.08),(.55,.22),(.35,.65),(.45,.78)],8,mats['wood'],root)
    center=height-radius
    for i,angle in enumerate((0,math.pi/2,.65)):
        ring=ring_band(name+'_Ring_'+str(i),mats,radius*(1-i*.09),.085,.055,24,root)
        ring.location=(0,0,center)
        ring.rotation_euler=(angle,.45 if i==2 else 0,.3 if i==2 else 0)
    C.beam(name+'_Axis',(-radius,0,center),(radius,0,center),.065,.065,mats['gold'],root)
    C.ico(name+'_Core',(0,0,center),radius*.22,mats['gold'],1,root)
    for x in (-radius*.9,radius*.9):
        C.beam(name+'_Stanchion',(x*1.15,0,0),(x,0,center),.09,.09,mats['gold'],root)
        C.box(name+'_Foot',(x*1.15,0,.06),(.3,.35,.12),mats['wood_dark'],.02,root)
    return root


def resonance_tubes(name,mats,width=3.6,height=2.9,count=11,parent=None):
    root=C.group(name,parent=parent)
    P.cabinet(name+'_Base',mats,width=width,depth=.55,height=.68,parent=root)
    for i in range(count):
        x=-width*.43+i*width*.86/(count-1)
        h=height*(.37+.56*i/(count-1))
        C.lathe(name+'_Pipe_%02d'%i,[(.09,0),(.09,h),(.073,h),(.073,.12),(0,.12)],10,mats['gold'],root,(x,0,.66))
        C.box(name+'_Mouth_%02d'%i,(x,-.092,.84),(.10,.015,.10),mats['wood_dark'],0,root)
    C.box(name+'_Support',(0,.02,1.2),(width,.12,.08),mats['wood_light'],.012,root)
    return root


def balance(name,mats,width=.66,height=.68,parent=None):
    root=C.group(name,parent=parent)
    C.box(name+'_Base',(0,0,.035),(.24,.22,.07),mats['wood'],.012,root)
    C.beam(name+'_Stem',(0,0,.07),(0,0,height),.025,.025,mats['gold'],root)
    C.beam(name+'_Cross',(-width/2,0,height-.08),(width/2,0,height-.08),.025,.025,mats['gold'],root)
    for x in (-width*.43,width*.43):
        for y in (-.09,.09):
            C.beam(name+'_Wire',(x,0,height-.08),(x,y,height*.4),.008,.008,mats['gold'],root,0)
        pan=P.vessel(name+'_Pan',mats,radius=.13,height=.04,kind='plate',parent=root)
        pan.location=(x,0,height*.4)
    return root


def magnifier(name,mats,height=.55,parent=None):
    root=C.group(name,parent=parent)
    C.box(name+'_Base',(0,0,.025),(.2,.2,.05),mats['gold'],.01,root)
    C.beam(name+'_Stand',(0,0,.05),(0,0,height-.1),.018,.018,mats['gold'],root)
    ring=ring_band(name+'_Lens',mats,.12,.025,.02,16,root)
    ring.location=(0,-.07,height-.08);ring.rotation_euler[0]=1.0
    return root


def stamp_press(name,mats,width=.5,height=.65,parent=None):
    root=C.group(name,parent=parent)
    C.box(name+'_Base',(0,0,.05),(width,.45,.10),mats['wood'],.012,root)
    for x in (-width*.36,width*.36):C.box(name+'_Upright',(x,.06,height*.5),(.07,.08,height*.8),mats['gold'],.008,root)
    C.box(name+'_Cross',(0,.06,height*.9),(width*.85,.10,.09),mats['gold'],.01,root)
    C.beam(name+'_Screw',(0,.06,.17),(0,.06,height),.045,.045,mats['stone_dark'],root)
    C.box(name+'_PressurePlate',(0,.02,.19),(width*.62,.28,.05),mats['gold'],.008,root)
    C.beam(name+'_Lever',(-width*.6,.06,height),(width*.45,.06,height),.028,.028,mats['gold'],root)
    return root


def records_pillar(name,mats,radius=.66,height=1.75,parent=None):
    root=C.group(name,parent=parent)
    C.lathe(name+'_Stone',[(radius+.12,0),(radius+.12,.1),(radius,.17),(radius,height-.13),
                          (radius+.1,height-.10),(radius+.1,height),(radius*.86,height+.1)],8,mats['stone'],root)
    for side in range(8):
        a=2*math.pi*side/8+math.pi/8
        g=C.group(name+'_Face_'+str(side),(math.sin(a)*radius*.94,-math.cos(a)*radius*.94,.30),a,parent=root)
        P.cabinet(name+'_Drawers_'+str(side),mats,width=radius*.55,depth=.05,height=height-.5,drawers=4,parent=g)
    return root


def medicine_cabinet(name,mats,width=1.5,height=2.2,parent=None):
    root=C.group(name,parent=parent)
    P.cabinet(name+'_Base',mats,width=width,depth=.48,height=.66,parent=root)
    C.box(name+'_Back',(0,.2,height*.63),(width,.08,height*.72),mats['wood_dark'],.008,root)
    for x in (-width/2+.04,width/2-.04):C.box(name+'_Side',(x,0,height*.64),(.08,.48,height*.73),mats['wood'],.008,root)
    for i in range(4):
        z=.69+i*(height-.73)/3
        C.box(name+'_Shelf',(0,0,z),(width,.5,.05),mats['wood_light'],.008,root)
        if i<3:
            bs=P.bottles(name+'_Bottles_'+str(i),mats,count=6,spacing=(width-.25)/6,parent=root)
            bs.location=(0,-.07,z+.03)
    C.box(name+'_Crown',(0,0,height+.05),(width+.12,.55,.11),mats['wood_light'],.01,root)
    return root


def dish_rack(name,mats,width=1.7,height=1.7,parent=None):
    root=C.group(name,parent=parent)
    P.work_counter(name+'_Base',mats,width=width,depth=.6,height=.86,parent=root)
    for x in (-width/2+.05,width/2-.05):C.box(name+'_Post',(x,.22,height*.75),(.08,.08,height*.5),mats['wood'],.008,root)
    for z in (1.18,1.65):
        C.box(name+'_Shelf',(0,.10,z),(width,.4,.06),mats['wood'],.008,root)
        for i in range(5):
            v=P.vessel(name+'_Cup',mats,radius=.075,height=.13,kind='cup',parent=root)
            v.location=(-width*.36+i*width*.18,.07,z+.03)
    for i in range(6):
        v=P.vessel(name+'_Plate',mats,radius=.19,height=.035,kind='plate',parent=root)
        v.location=(-width*.25,-.08,.86+i*.025)
    return root


def planting_bed(name,mats,width=1.8,depth=3.2,height=.65,seed=8,parent=None):
    root=C.group(name,parent=parent)
    C.box(name+'_Box',(0,0,height/2),(width,depth,height),mats['wood'],.02,root)
    C.box(name+'_Soil',(0,0,height+.008),(width-.12,depth-.12,.03),mats['wood_dark'],.008,root)
    for x in (-width/2,width/2):C.box(name+'_RimX',(x,0,height+.04),(.10,depth+.10,.10),mats['wood_light'],.01,root)
    for y in (-depth/2,depth/2):C.box(name+'_RimY',(0,y,height+.04),(width,.10,.10),mats['wood_light'],.01,root)
    for i,y in enumerate((-depth*.32,0,depth*.32)):
        for j,x in enumerate((-width*.26,width*.26)):
            p=F.planter(name+'_Plant_%d_%d'%(i,j),mats,width=.59,height=.72,flower_count=3,seed=seed+i*2+j,parent=root)
            p.location=(x,y,height-.25)
    return root


def greenhouse_frame(name,mats,width=8,depth=8,eave=3.5,rise=1.25,bays=4,parent=None):
    root=C.group(name,parent=parent)
    for i in range(bays+1):
        y=depth*i/bays
        p=[(-width/2,y,0),(-width/2,y,eave),(0,y,eave+rise),(width/2,y,eave),(width/2,y,0)]
        for a,b in zip(p,p[1:]):C.beam(name+'_Rib_%02d'%i,a,b,.10,.10,mats['wood_dark'],root)
    for x,z in ((-width/2,eave),(width/2,eave),(0,eave+rise)):
        C.beam(name+'_Longitudinal',(x,0,z),(x,depth,z),.09,.09,mats['gold'],root)
    return root


def watering_can(name,mats,radius=.18,height=.35,parent=None):
    root=C.group(name,parent=parent)
    C.lathe(name+'_Can',[(0,0),(radius,0),(radius,height*.8),(radius*.8,height)],8,mats['gold'],root)
    C.beam(name+'_Spout',(radius*.8,0,height*.2),(radius*2.8,0,height*.9),.055,.055,mats['gold'],root)
    C.beam(name+'_HandleA',(-radius*.7,0,height*.7),(-radius*1.5,0,height*1.2),.035,.035,mats['gold'],root)
    C.beam(name+'_HandleB',(-radius*1.5,0,height*1.2),(0,0,height*1.3),.035,.035,mats['gold'],root)
    return root


def grille(name,mats,width=1.1,height=1.8,parent=None):
    root=C.group(name,parent=parent)
    for x in (-width/2,width/2):C.box(name+'_FrameX',(x,0,height/2),(.08,.10,height),mats['wood'],.01,root)
    for z in (0,height):C.box(name+'_FrameZ',(0,0,z),(width,.10,.08),mats['wood'],.01,root)
    for i in range(7):
        x=-width*.42+i*width*.84/6
        C.beam(name+'_Bar',(x,0,.05),(x,0,height-.05),.024,.024,mats['gold'],root)
    C.box(name+'_OpaqueBacking',(0,.08,height/2),(width,.08,height),mats['wood_dark'],0,root)
    return root
