"""Reusable solid-mesh residential props derived from the reviewed concepts.

All origins are at the support surface, fronts face -Y, and dimensions are
metres.  These generators contain no room placement or plot state.  Paper
marks and game pieces are visual placeholders; story data remains host-owned.
"""
import math
from . import core as C, furniture as F, special_props as S, flooring as T


def paneled_bed(name,mats,width=1.35,length=2.25,height=.56,parent=None):
    root=S.bed(name,mats,width,length,height,parent=parent)
    for y,top in ((length*.47,1.28),(-length*.47,.82)):
        for x in (-width*.5,width*.5):
            C.box(name+'_Post',(x,y,top*.5),(.12,.13,top),mats['wood'],.012,root)
            C.box(name+'_Cap',(x,y,top+.025),(.18,.18,.06),mats['wood_light'],.01,root)
        C.box(name+'_Crown',(0,y,top-.06),(width,.13,.09),mats['wood_light'],.01,root)
    # A planar fold and thin foot drape keep the blanket an actual mesh.
    C.box(name+'_FootDrape',(0,-length*.39,height-.04),(width*.92,.045,.24),mats['green'],.01,root)
    return root


def framed_mirror(name,mats,width=.65,height=.95,parent=None):
    root=C.group(name,parent=parent)
    cut=min(width,height)*.15
    pts=[(-width/2,0),(width/2,0),(width/2,height-cut),(width/2-cut,height),
         (-width/2+cut,height),(-width/2,height-cut)]
    # Extrude in XY then rotate once to create a closed vertical glass panel.
    panel=C.polyprism(name+'_Frame',pts,-.022,.022,mats['gold'],root)
    panel.rotation_euler[0]=math.pi/2
    inset=[(x*.86,(z-height*.5)*.88+height*.5) for x,z in pts]
    glass=C.polyprism(name+'_Glass',inset,.026,.030,mats['grout'],root)
    glass.rotation_euler[0]=math.pi/2
    # A restrained diagonal highlight is geometry, not a photographed mirror.
    C.beam(name+'_Highlight',(-width*.22,-.034,height*.25),(width*.2,-.034,height*.68),.012,.007,mats['stone'],root,0)
    return root


def pitcher(name,mats,radius=.11,height=.30,parent=None):
    root=C.group(name,parent=parent)
    C.lathe(name+'_Body',[(0,0),(radius*.55,0),(radius*.85,height*.12),(radius,height*.46),
       (radius*.55,height*.76),(radius*.55,height),(radius*.43,height),
       (radius*.42,height*.79),(radius*.76,height*.46),(radius*.53,height*.15),(0,height*.15)],10,mats['cream'],root)
    for a,b in [((radius*.55,0,height*.83),(radius*1.55,0,height*.78)),
                ((radius*1.55,0,height*.78),(radius*1.5,0,height*.25)),
                ((radius*1.5,0,height*.25),(radius*.85,0,height*.28))]:
        C.beam(name+'_Handle',a,b,.025,.028,mats['cream'],root)
    return root


def wash_table(name,mats,width=.98,depth=.60,height=.86,mirror=True,parent=None):
    root=C.group(name,parent=parent)
    S.cabinet(name+'_Case',mats,width,depth,height-.05,parent=root)
    C.box(name+'_Top',(0,0,height-.025),(width+.09,depth+.06,.07),mats['wood_light'],.012,root)
    bowl=S.vessel(name+'_Bowl',mats,radius=width*.24,height=.12,kind='plate',parent=root)
    bowl.location=(-width*.14,-.04,height+.014)
    jug=pitcher(name+'_Jug',mats,parent=root);jug.location=(width*.29,.08,height+.014)
    for x in (-width*.46,width*.46):
        C.box(name+'_SplashSide',(x,depth*.29,height+.08),(.035,depth*.34,.17),mats['wood'],.008,root)
    if mirror:
        for x in (-width*.25,width*.25):
            C.box(name+'_MirrorSupport',(x,depth*.50,height+.25),(.035,.05,.54),mats['wood'],.005,root)
        m=framed_mirror(name+'_Mirror',mats,width=width*.65,height=.88,parent=root)
        m.location=(0,depth*.5,height+.30)
    return root


def rug(name,mats,width=2.1,depth=1.4,parent=None):
    root=C.group(name,parent=parent)
    C.box(name+'_Border',(0,0,.009),(width,depth,.018),mats['stone'],.004,root)
    C.box(name+'_Field',(0,0,.02),(width-.10,depth-.10,.015),mats['book_green'],.002,root)
    return root


def slate_table(name,mats,width=2.3,depth=1.3,height=.80,parent=None):
    root=F.table(name,mats,width=width,depth=depth,height=height,parent=parent)
    C.box(name+'_Inset',(0,0,height+.006),(width-.17,depth-.17,.014),mats['stone_dark'],.006,root)
    for y in (-depth*.44,depth*.44):C.box(name+'_FineBorder',(0,y,height+.015),(width*.90,.018,.012),mats['gold'],.002,root)
    return root


def folding_desk(name,mats,width=1.2,depth=.65,height=.78,parent=None):
    root=C.group(name,parent=parent)
    C.box(name+'_Top',(0,0,height-.035),(width,depth,.07),mats['wood_light'],.012,root)
    C.box(name+'_WallRail',(0,depth*.44,height-.13),(width,.065,.15),mats['wood'],.01,root)
    for x in (-width*.32,width*.32):
        C.beam(name+'_FoldingBracket',(x,depth*.43,height-.46),(x,-depth*.35,height-.075),.05,.05,mats['gold'],root)
        C.cylinder(name+'_Hinge',(x,depth*.43,height-.10),.055,.10,mats['gold'],8,root)
    return root


def go_board(name,mats,size=.72,lines=19,parent=None):
    """19 intersecting grid lines per axis; black/white lens-shaped Go stones."""
    if int(lines)!=19: raise ValueError('The approved Go board uses 19 lines.')
    root=C.group(name,parent=parent)
    C.box(name+'_Board',(0,0,.025),(size,size,.05),mats['wood_light'],.008,root)
    border=size*.065;span=size-2*border;step=span/(lines-1);batch=T.Batch()
    for i in range(lines):
        t=-span/2+i*step;thick=size*.0023
        batch.prism([(-span/2,t-thick),(span/2,t-thick),(span/2,t+thick),(-span/2,t+thick)],.050,.052)
        batch.prism([(t-thick,-span/2),(t+thick,-span/2),(t+thick,span/2),(t-thick,span/2)],.050,.052)
    batch.finish(name+'_19x19Grid',mats['wood_dark'],root)
    for r in (3,9,15):
        for c in (3,9,15):C.cylinder(name+'_Star',(-span/2+c*step,-span/2+r*step,.053),size*.005,.003,mats['wood_dark'],8,root)
    # Non-authoritative sample stones: no claim of a recorded puzzle position.
    for j,(r,c) in enumerate(((3,3),(15,15),(3,15),(15,3),(9,9),(8,9),(9,8),(10,10),(5,6),(5,7),(6,6),(7,7))):
        C.lathe(name+'_Stone',[(0,0),(step*.38,0),(step*.46,.007),(step*.39,.016),(0,.023)],10,
                mats['wood_dark'] if j%2==0 else mats['cream'],root,(-span/2+c*step,-span/2+r*step,.054))
    for x,mat in ((-size*.72,mats['wood_dark']),(size*.72,mats['cream'])):
        C.lathe(name+'_Bowl',[(0,0),(.10,0),(.13,.09),(.12,.12),(.10,.12),(.08,.04),(0,.04)],10,mats['wood'],root,(x,0,0))
        C.ico(name+'_Reserve',(x,0,.09),.068,mat,1,root)
    root['grid_lines']=19
    root['story_position']='visual_placeholder_not_authored_evidence'
    return root


def music_stand(name,mats,width=.52,height=1.25,parent=None):
    root=C.group(name,parent=parent)
    for a in (0,2.094,4.189):C.beam(name+'_Foot',(0,0,.08),(.24*math.cos(a),.24*math.sin(a),.04),.045,.045,mats['wood'],root)
    C.beam(name+'_Stem',(0,0,.06),(0,0,height-.20),.045,.045,mats['gold'],root)
    tray=C.group(name+'_Tray',(0,0,height-.18),parent=root)
    tray.rotation_euler[0]=math.radians(20)
    C.box(name+'_Back',(0,0,.18),(width,.045,.38),mats['wood'],.008,tray)
    C.box(name+'_Score',(0,-.027,.19),(width*.84,.009,.30),mats['cream'],.002,tray)
    C.box(name+'_Rest',(0,-.05,.015),(width+.03,.13,.035),mats['wood_light'],.006,tray)
    for row in range(5):C.box(name+'_Staff',(0,-.034,.09+row*.045),(width*.65,.005,.007),mats['stone_dark'],0,tray)
    return root


def keyboard(name,mats,width=1.7,depth=.70,height=1.45,parent=None):
    root=C.group(name,parent=parent)
    S.cabinet(name+'_Case',mats,width,depth*.65,height,parent=root)
    C.box(name+'_KeyboardBed',(0,-depth*.36,.82),(width+.08,depth*.45,.11),mats['wood_light'],.012,root)
    n=28;kw=(width-.18)/n
    for i in range(n):
        x=-width/2+.09+(i+.5)*kw
        C.box(name+'_IvoryKey',(x,-depth*.40,.89),(kw-.002,depth*.33,.025),mats['cream'],.001,root)
        if i%7 not in (2,6) and i<n-1:
            C.box(name+'_BlackKey',(x+kw*.5,-depth*.32,.916),(kw*.55,depth*.17,.035),mats['wood_dark'],.001,root)
    for x in (-width*.44,width*.44):C.box(name+'_FrontLeg',(x,-depth*.42,.39),(.09,.09,.78),mats['wood'],.008,root)
    score=music_stand(name+'_ScoreDesk',mats,width=.58,height=.43,parent=root)
    score.location=(0,-depth*.15,.95)
    for x in (-.14,0,.14):C.box(name+'_Pedal',(x,-depth*.39,.075),(.055,.20,.028),mats['gold'],.005,root)
    return root


def cooker_hood(name,mats,width=1.6,depth=.78,height=1.65,parent=None):
    root=C.group(name,parent=parent)
    # Origin is the hood's low rim. Truncated square pyramid + rigid flue.
    verts=[(-width/2,-depth/2,0),(width/2,-depth/2,0),(width/2,depth/2,0),(-width/2,depth/2,0),
      (-width*.19,-depth*.18,.57),(width*.19,-depth*.18,.57),(width*.19,depth*.18,.57),(-width*.19,depth*.18,.57)]
    C.mesh(name+'_Canopy',verts,[(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7),(3,2,1,0)],mats['stone_dark'],root,.008)
    C.box(name+'_Rim',(0,0,.035),(width+.06,depth+.05,.09),mats['stone_dark'],.01,root)
    C.box(name+'_Flue',(0,depth*.04,(.57+height)/2),(width*.36,depth*.36,height-.57),mats['stone_dark'],.01,root)
    for x in (-width*.4,width*.4):C.beam(name+'_Bracket',(x,depth*.43,-.48),(x,-depth*.30,0),.065,.065,mats['wood_dark'],root)
    return root


def crockery_shelf(name,mats,width=1.65,parent=None):
    root=C.group(name,parent=parent)
    for z in (0,.48):
        C.box(name+'_Shelf',(0,0,z),(width,.34,.07),mats['wood_light'],.009,root)
        for x in (-width*.36,width*.36):C.beam(name+'_Bracket',(x,.13,z-.23),(x,-.12,z-.045),.05,.05,mats['wood'],root)
    for i in range(4):
        if i<2:
            p=pitcher(name+'_Jug',mats,radius=.105,height=.30,parent=root);p.location=(-width*.32+i*width*.21,0,.52)
        for j in range(2 if i%2 else 3):
            p=S.vessel(name+'_Bowl',mats,radius=.12,height=.065,kind='plate',parent=root);p.location=(-width*.33+i*width*.22,-.02,.04+j*.035)
    return root


def linen_cabinet(name,mats,width=.85,height=1.9,parent=None):
    root=C.group(name,parent=parent);depth=.45
    S.cabinet(name+'_Lower',mats,width,depth,height*.43,parent=root)
    C.box(name+'_Back',(0,depth*.45,height*.7),(width,.06,height*.60),mats['wood_dark'],.005,root)
    for x in (-width*.47,width*.47):C.box(name+'_Side',(x,0,height*.7),(.06,depth,height*.60),mats['wood'],.007,root)
    for z in (height*.48,height*.72,height):C.box(name+'_Shelf',(0,0,z),(width+.04,depth+.03,.055),mats['wood_light'],.007,root)
    for row in (0,1):
        for j in range(3):C.box(name+'_FoldedLinen',(0,-.015,height*(.48+row*.24)+.07+j*.063),(width*.76,depth*.73,.057),mats['cream'],.013,root)
    return root


def speaking_tube(name,mats,height=1.8,parent=None):
    root=C.group(name,parent=parent)
    C.beam(name+'_Pipe',(0,.07,0),(0,.07,height),.065,.065,mats['gold'],root)
    for z in (.18,height-.16):C.box(name+'_WallClamp',(0,.12,z),(.15,.10,.08),mats['wood_dark'],.006,root)
    bell=C.lathe(name+'_Mouth',[(.035,0),(.04,.10),(.13,.22),(.12,.25),(.09,.23),(.025,.10)],8,mats['gold'],root)
    bell.rotation_euler[0]=math.pi/2;bell.location=(0,.06,.45)
    return root


def pressure_panel(name,mats,width=.65,height=.75,parent=None):
    root=C.group(name,parent=parent)
    C.box(name+'_Mount',(0,0,height/2),(width,.11,height),mats['wood_dark'],.012,root)
    for z,rad,mat in ((height*.65,.18,mats['gold']),(height*.65,.145,mats['cream'])):
        c=C.cylinder(name+'_Gauge',(0,-.078,z),rad,.035,mat,12,root);c.rotation_euler[0]=math.pi/2
    C.beam(name+'_Needle',(0,-.106,height*.65),(.08,-.106,height*.71),.012,.010,mats['wood_dark'],root,0)
    C.beam(name+'_Lever',(-.13,-.11,.16),(.13,-.11,.16),.05,.05,mats['gold'],root)
    root['state_binding']='host_owned_no_pressure_reading_embedded'
    return root


def vent_grille(name,mats,width=.75,height=.42,parent=None):
    root=C.group(name,parent=parent)
    C.box(name+'_Recess',(0,0,height/2),(width,.035,height),mats['wood_dark'],.008,root)
    for i in range(7):C.box(name+'_Louvre',(0,-.03,(i+.5)*height/7),(width*.91,.045,.023),mats['stone_dark'],.003,root)
    for x in (-width*.48,width*.48):C.box(name+'_Stile',(x,-.03,height/2),(.04,.06,height),mats['wood_light'],.005,root)
    return root


def desk_bundle(name,mats,mode='manuscripts',seed=7,parent=None):
    """Small character-specific solids; no names, document text or puzzle facts."""
    root=C.group(name,parent=parent)
    if mode in ('manuscripts','legal_records','notebook','rubbings','sketchbook','score_sheets','botany_notebook'):
        n=4 if mode in ('manuscripts','legal_records','rubbings') else 1
        for i in range(n):
            p=C.box(name+'_Paper',(.013*(i%2),.011*i,.006+i*.009),(.38,.27,.012),mats['cream'],.002,root)
            p.rotation_euler[2]=-.06+i*.04
        C.beam(name+'_Pen',(.24,-.1,.025),(.27,.14,.025),.012,.012,mats['gold'] if mode=='legal_records' else mats['wood_dark'],root,0)
        if mode=='manuscripts':C.box(name+'_BlueChalk',(-.25,.02,.014),(.08,.022,.022),mats['book_blue'],.003,root)
        if mode=='rubbings':
            C.lathe(name+'_Magnifier',[(.066,0),(.080,0),(.080,.018),(.066,.018),(.066,0)],12,mats['gold'],root,(.24,.02,.023))
            C.cylinder(name+'_Lens',(.24,.02,.032),.065,.008,mats['glass'],12,root)
            C.beam(name+'_MagnifierHandle',(.24,-.055,.031),(.24,-.21,.031),.028,.028,mats['wood_dark'],root)
    elif mode in ('drawing_rolls','ruler'):
        for i in range(3):
            p=C.cylinder(name+'_PaperRoll',(-.14+i*.14,0,.04),.039,.41,mats['cream'],10,root);p.rotation_euler[0]=math.pi/2
        C.box(name+'_Ruler',(.1,-.27,.012),(.52,.04,.022),mats['wood_light'],.002,root)
        C.lathe(name+'_PlumbBob',[(0,0),(.04,.06),(.04,.10),(0,.13)],8,mats['gold'],root,(-.24,-.20,0))
    elif mode in ('cloth_bag','cloth_kit','medical_bag'):
        C.box(name+'_Bag',(0,0,.13),(.40,.22,.26),mats['wood_dark'] if mode=='medical_bag' else mats['stone'],.045,root)
        C.box(name+'_Flap',(0,-.12,.18),(.39,.03,.17),mats['wood'] if mode=='medical_bag' else mats['cream'],.025,root)
        for x in (-.14,.14):C.box(name+'_Buckle',(x,-.145,.12),(.045,.025,.065),mats['gold'],.004,root)
        for a,b in [((-.10,0,.26),(-.10,0,.36)),((-.10,0,.36),(.10,0,.36)),((.10,0,.36),(.10,0,.26))]:C.beam(name+'_Handle',a,b,.025,.025,mats['wood_dark'],root)
    elif mode in ('bandage_rolls','bedside_bandages','oil_cloth','shawl'):
        C.box(name+'_Cloth',(0,0,.025),(.45,.31,.05),mats['cream'] if mode!='oil_cloth' else mats['stone_dark'],.02,root)
        for i in range(3):C.cylinder(name+'_Roll',(-.14+i*.14,0,.09),.05,.10,mats['cream'],10,root)
    elif mode in ('mechanical_tools','tool_roll','gear_board'):
        C.box(name+'_ToolMat',(0,0,.008),(.52,.36,.015),mats['wood_light'],.008,root)
        for i in range(3):
            x=-.18+i*.17;C.beam(name+'_Tool',(x,-.11,.027),(x,.10,.027),.022,.026,mats['stone_dark'],root)
            C.box(name+'_ToolHead',(x,.12,.033),(.10,.05,.035),mats['gold'],.003,root)
        if mode=='gear_board':
            for i in range(3):C.cylinder(name+'_Gear',(-.15+i*.15,.0,.045),.055,.025,mats['gold'],8,root)
    elif mode in ('bowls','cooking_tools'):
        for i in range(2):
            p=S.vessel(name+'_Bowl',mats,radius=.12,height=.09,kind='plate',parent=root);p.location=(-.15+i*.3,0,0)
        C.beam(name+'_Spoon',(-.25,-.2,.035),(.10,-.2,.035),.02,.02,mats['wood_light'],root)
    else: raise ValueError('Unknown residential accessory bundle: '+mode)
    root['authored_text']='none_visual_only'
    return root


def easel(name,mats,width=.70,height=1.65,parent=None):
    root=C.group(name,parent=parent)
    for x in (-width*.38,width*.38):C.beam(name+'_Leg',(x,-.17,0),(x*.45,.06,height),.055,.055,mats['wood_light'],root)
    C.beam(name+'_BackLeg',(0,.53,0),(0,.06,height*.86),.055,.055,mats['wood'],root)
    C.box(name+'_Rest',(0,-.07,height*.43),(width+.12,.18,.06),mats['wood'],.008,root)
    art=F.painting(name+'_Canvas',mats,width=width,height=height*.48,style='landscape',parent=root)
    art.location=(0,-.06,height*.46)
    return root


def stool(name,mats,width=.62,depth=.40,height=.45,parent=None):
    root=C.group(name,parent=parent)
    for x in (-width*.40,width*.40):
        for y in (-depth*.36,depth*.36):C.box(name+'_Leg',(x,y,height*.46),(.065,.065,height*.92),mats['wood'],.008,root)
    C.box(name+'_Seat',(0,0,height-.035),(width,depth,.07),mats['wood_light'],.009,root)
    C.box(name+'_Cushion',(0,0,height+.018),(width*.94,depth*.91,.045),mats['green'],.014,root)
    return root


def seal_door_trim(name,mats,width=1.3,height=2.45,parent=None):
    """Hardware around an existing opaque door; it never opens a neighboring room."""
    root=C.group(name,parent=parent)
    for x in (-width*.46,width*.46):C.box(name+'_SealRail',(x,-.06,height*.5),(.055,.045,height-.10),mats['stone_dark'],.005,root)
    for z in (.07,height-.07):C.box(name+'_Seal',(0,-.06,z),(width-.1,.045,.055),mats['stone_dark'],.005,root)
    for z in (height*.24,height*.5,height*.76):C.box(name+'_Lock',(width*.32,-.115,z),(.12,.08,.16),mats['gold'],.008,root)
    return root


def kitchen_hood(name,mats,width=2.25,parent=None,depth=.80,height=1.65):
    return cooker_hood(name,mats,width=width,depth=depth,height=height,parent=parent)


def upright_piano(name,mats,width=1.7,parent=None,depth=.70,height=1.45):
    return keyboard(name,mats,width=width,depth=depth,height=height,parent=parent)


__all__=['paneled_bed','framed_mirror','pitcher','wash_table','rug','slate_table','folding_desk','go_board',
 'music_stand','keyboard','cooker_hood','crockery_shelf','linen_cabinet','speaking_tube',
 'pressure_panel','vent_grille','desk_bundle','easel','stool','seal_door_trim',
 'kitchen_hood','upright_piano']
