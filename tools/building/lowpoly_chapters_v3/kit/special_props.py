"""Independent solid-colour chapter props; no scene layout or story logic."""
import math
import random
from .core import group,box,cylinder,lathe,beam,ico,mesh,polyprism
from . import furniture as F


def bed(name,mats,width=1.15,length=2.15,height=.52,medical=False,parent=None):
    root=group(name,parent=parent)
    mat=mats['stone_dark'] if medical else mats['wood']
    for x in (-width*.43,width*.43):
        for y in (-length*.43,length*.43):
            box(name+'_Leg',(x,y,.25),(.09,.09,.5),mat,.012,root)
    box(name+'_Frame',(0,0,height-.13),(width,length,.16),mat,.02,root)
    box(name+'_Mattress',(0,0,height-.015),(width*.94,length*.97,.15),mats['cream'],.035,root)
    box(name+'_Blanket',(0,-length*.11,height+.075),(width*.96,length*.63,.045),mats['book_blue'] if medical else mats['green'],.02,root)
    box(name+'_Fold',(0,length*.19,height+.103),(width*.97,.16,.03),mats['stone'] if medical else mats['book_green'],.006,root)
    box(name+'_Pillow',(0,length*.34,height+.105),(width*.69,.42,.12),mats['cream'],.05,root)
    for y,h in ((length*.47,1.02),(-length*.47,.67)):
        box(name+'_HeadFoot',(0,y,h*.5),(width+.07,.09,h),mat,.018,root)
        box(name+'_Inset',(0,y-.055,h*.65),(width*.8,.035,h*.37),mats['wood_light'] if not medical else mats['cream'],.008,root)
    root['kind']='bed'
    root['surface_height']=height+.06
    return root


def cabinet(name,mats,width=1.2,depth=.5,height=1.9,drawers=0,parent=None):
    root=group(name,parent=parent)
    box(name+'_Body',(0,0,height/2),(width,depth,height),mats['wood_dark'],.012,root)
    box(name+'_Top',(0,0,height-.025),(width+.08,depth+.05,.07),mats['wood_light'],.012,root)
    box(name+'_Plinth',(0,0,.07),(width+.05,depth+.04,.14),mats['wood'],.012,root)
    count=drawers if drawers else 2
    for i in range(count):
        if drawers:
            wh=(height-.23)/count
            pos=(0,-depth/2-.018,.15+wh*(i+.5));size=(width-.10,.045,wh-.035)
        else:
            pos=(-width/4+i*width/2,-depth/2-.018,height*.53);size=(width*.47,.045,height-.24)
        box(name+'_Front_%02d'%i,pos,size,mats['wood'],.012,root)
        x,y,z=pos
        box(name+'_Inset_%02d'%i,(x,y-.027,z),(size[0]-.10,.018,size[2]-.12),mats['wood_light'],.006,root)
        hx=x if drawers else x+(.18*width if i==0 else -.18*width)
        ico(name+'_Handle',(hx,y-.07,z),.028,mats['gold'],1,root)
    root['kind']='cabinet'
    return root


def chest(name,mats,width=.9,depth=.52,height=.48,parent=None):
    root=group(name,parent=parent)
    box(name+'_Case',(0,0,height*.45),(width,depth,height*.9),mats['wood'],.02,root)
    box(name+'_Lid',(0,0,height-.03),(width+.035,depth+.035,.075),mats['wood_light'],.015,root)
    for x in (-width*.32,width*.32):
        box(name+'_Band',(x,-depth/2-.008,height*.47),(.055,.024,height*.92),mats['stone_dark'],.004,root)
        box(name+'_TopBand',(x,0,height+.014),(.055,depth+.035,.018),mats['stone_dark'],.002,root)
    box(name+'_Lock',(0,-depth/2-.028,height*.67),(.09,.035,.12),mats['gold'],.008,root)
    return root


def vessel(name,mats,radius=.12,height=.3,kind='bottle',parent=None):
    root=group(name,parent=parent)
    if kind=='bottle':
        profile=[(0,0),(radius*.8,0),(radius,.04),(radius,height*.66),(radius*.42,height*.8),(radius*.42,height*.95)]
        lathe(name+'_Bottle',profile,8,mats['book_green'],root)
        cylinder(name+'_Cork',(0,0,height*.97),radius*.47,height*.08,mats['wood_light'],8,root)
        box(name+'_Label',(0,-radius-.001,height*.42),(radius*1.1,.008,height*.24),mats['cream'],0,root)
    elif kind=='plate':
        lathe(name+'_Dish',[(0,0),(radius*.7,0),(radius,height*.3),(radius,height),
            (radius*.77,height*.9),(radius*.58,height*.35),(0,height*.35)],12,mats['cream'],root)
    elif kind=='cup':
        lathe(name+'_Cup',[(radius*.7,0),(radius,height),(radius*.83,height),(radius*.6,height*.12),(0,height*.12)],10,mats['cream'],root)
        for p,q in [((radius*.85,0,height*.8),(radius*1.5,0,height*.8)),((radius*1.5,0,height*.8),(radius*1.5,0,height*.25)),((radius*1.5,0,height*.25),(radius*.75,0,height*.25))]:
            beam(name+'_Handle',p,q,.025,.025,mats['cream'],root)
    else:
        lathe(name+'_Pot',[(0,0),(radius*.65,0),(radius,height*.7),(radius*.92,height),(radius*.78,height),(radius*.72,height*.25),(0,height*.25)],10,mats['stone_dark'],root)
        for x in (-radius*1.12,radius*1.12):
            box(name+'_Grip',(x,0,height*.7),(radius*.48,.06,.08),mats['gold'],.012,root)
    return root


def sink(name,mats,width=1.3,depth=.65,height=.88,parent=None):
    root=group(name,parent=parent)
    base=cabinet(name+'_Base',mats,width,depth,height-.08,parent=root)
    # Four raised rim strips leave an actual recessed basin.
    box(name+'_Bottom',(0,0,height-.12),(width-.12,depth-.12,.04),mats['stone'],.012,root)
    for x in (-width/2+.045,width/2-.045):
        box(name+'_RimX',(x,0,height-.04),(.09,depth,.12),mats['cream'],.016,root)
    for y in (-depth/2+.045,depth/2-.045):
        box(name+'_RimY',(0,y,height-.04),(width-.15,.09,.12),mats['cream'],.016,root)
    beam(name+'_TapStem',(0,depth*.34,height),(0,depth*.34,height+.28),.036,.036,mats['gold'],root)
    beam(name+'_TapNeck',(0,depth*.34,height+.28),(0,0,height+.28),.036,.036,mats['gold'],root)
    cylinder(name+'_Drain',(0,0,height-.094),.04,.012,mats['stone_dark'],10,root)
    return root


def stove(name,mats,width=1.4,depth=.72,height=.9,parent=None):
    root=group(name,parent=parent)
    box(name+'_Iron',(0,0,height*.49),(width,depth,height*.98),mats['stone_dark'],.02,root)
    box(name+'_Top',(0,0,height),(width+.05,depth+.05,.055),mats['stone'],.012,root)
    for x in (-width*.27,width*.27):
        for y in (-depth*.23,depth*.23):
            cylinder(name+'_Burner',(x,y,height+.04),min(width*.14,depth*.19),.024,mats['wood_dark'],10,root)
    for x in (-width*.28,width*.28):
        box(name+'_Oven',(x,-depth/2-.016,height*.43),(width*.43,.04,height*.48),mats['wood_dark'],.008,root)
        beam(name+'_Bar',(x-width*.12,-depth/2-.07,height*.57),(x+width*.12,-depth/2-.07,height*.57),.025,.025,mats['gold'],root)
        knob=cylinder(name+'_Knob',(x,-depth/2-.04,height*.82),.035,.035,mats['gold'],8,root)
        knob.rotation_euler[0]=math.pi/2
    return root


def work_counter(name,mats,width=2,depth=.7,height=.9,parent=None):
    root=cabinet(name,mats,width=width,depth=depth,height=height-.05,drawers=0,parent=parent)
    box(name+'_Worktop',(0,0,height-.025),(width+.08,depth+.06,.07),mats['stone'],.012,root)
    return root


def tub(name,mats,width=1.6,depth=.8,height=.6,parent=None):
    root=group(name,parent=parent)
    box(name+'_Bottom',(0,0,.2),(width-.12,depth-.12,.10),mats['cream'],.04,root)
    for x in (-width/2+.07,width/2-.07):
        box(name+'_End',(x,0,height*.6),(.14,depth,height*.8),mats['cream'],.035,root)
    for y in (-depth/2+.07,depth/2-.07):
        box(name+'_Side',(0,y,height*.6),(width-.14,.14,height*.8),mats['cream'],.035,root)
    box(name+'_Water',(0,0,height*.55),(width-.30,depth-.30,.015),mats['glass'],.006,root)
    for x in (-width*.32,width*.32):
        for y in (-depth*.28,depth*.28):box(name+'_Foot',(x,y,.09),(.10,.10,.18),mats['gold'],.015,root)
    return root


def washstand(name,mats,width=.8,depth=.54,height=.84,parent=None):
    return sink(name,mats,width,depth,height,parent)


def screen(name,mats,width=1.8,height=1.8,parent=None):
    root=group(name,parent=parent)
    for i in range(3):
        panel=group(name+'_Panel_'+str(i),((i-1)*width/3,0,0),rotation=(-1 if i%2 else 1)*.14,parent=root)
        box(name+'_Fabric',(0,0,height*.55),(width/3-.05,.035,height*.84),mats['cream'],.008,panel)
        for x in (-width/6,width/6):
            box(name+'_Post',(x,0,height/2),(.045,.055,height),mats['wood'],.006,panel)
        for z in (.18,height):box(name+'_Rail',(0,0,z),(width/3,.055,.05),mats['wood'],.006,panel)
    return root


def rack(name,mats,width=1.5,height=1.65,parent=None):
    root=group(name,parent=parent)
    for x in (-width/2,width/2):
        beam(name+'_Upright',(x,0,0),(x,0,height),.06,.06,mats['wood'],root)
        box(name+'_Foot',(x,0,.045),(.24,.6,.09),mats['wood_dark'],.01,root)
    beam(name+'_Rail',(-width/2,0,height),(width/2,0,height),.055,.055,mats['wood'],root)
    for i in range(4):
        x=-width*.35+i*width*.23
        box(name+'_Towel',(x,0,height-.45),(width*.17,.04,.83),mats['cream'] if i%2 else mats['book_blue'],.008,root)
    return root


def chalkboard(name,mats,width=3.3,height=1.7,parent=None):
    root=group(name,parent=parent)
    box(name+'_Slate',(0,0,height/2),(width,.08,height),mats['green'],.012,root)
    for x in (-width/2,width/2):box(name+'_Stile',(x,-.025,height/2),(.10,.13,height+.12),mats['wood_light'],.01,root)
    for z in (0,height):box(name+'_Rail',(0,-.025,z),(width+.1,.13,.10),mats['wood_light'],.01,root)
    box(name+'_Tray',(0,-.15,-.04),(width,.28,.065),mats['wood'],.008,root)
    # Deliberately abstract visual marks: actual poem text is owned by DV.
    for row in range(5):
        for col in range(5-row%2):
            x=-width*.37+col*width*.15
            box(name+'_ChalkMark',(x,-.046,height*.77-row*height*.13),(width*.09,.005,.014),mats['cream'],0,root)
    root['text_binding']='host_owned_no_story_text_embedded'
    return root


def lectern(name,mats,width=.8,depth=.55,height=1.12,parent=None):
    root=group(name,parent=parent)
    box(name+'_Foot',(0,0,.07),(width*.9,depth*.95,.14),mats['wood_dark'],.015,root)
    box(name+'_Pedestal',(0,0,height*.43),(width*.68,depth*.68,height*.78),mats['wood'],.015,root)
    top=box(name+'_Slope',(0,0,height-.06),(width,depth,.08),mats['wood_light'],.015,root)
    top.rotation_euler[0]=math.radians(14)
    box(name+'_BookStop',(0,-depth*.43,height-.105),(width*.9,.045,.045),mats['gold'],.006,root)
    return root


def chessboard(name,mats,size=.62,parent=None):
    root=group(name,parent=parent)
    box(name+'_Base',(0,0,.018),(size+.06,size+.06,.036),mats['wood_dark'],.008,root)
    for row in range(8):
        for col in range(8):
            box(name+'_Square_%d_%d'%(row,col),(-size/2+(col+.5)*size/8,-size/2+(row+.5)*size/8,.04),
                (size/8-.001,size/8-.001,.012),mats['cream'] if (row+col)%2 else mats['wood'],0,root)
    for color,rows in ((mats['cream'],(0,1)),(mats['wood_dark'],(6,7))):
        for row in rows:
            for col in range(8):
                h=size*(.11 if row in (0,7) else .08)
                pos=(-size/2+(col+.5)*size/8,-size/2+(row+.5)*size/8,.048)
                lathe(name+'_Piece',[(size*.026,0),(size*.028,h*.13),(size*.016,h*.32),(size*.012,h*.6),(size*.022,h*.78),(0,h)],6,color,root,pos)
    return root


def record_station(name,mats,width=.9,height=1.45,kind='record',parent=None):
    root=group(name,parent=parent)
    cabinet(name+'_Case',mats,width=width,depth=.38,height=height*.74,drawers=3,parent=root)
    box(name+'_Top',(0,0,height*.76),(width+.06,.45,.06),mats['wood_light'],.012,root)
    if kind=='audio':
        base=(0,0,height*.85)
        box(name+'_Recorder',base,(width*.72,.30,.18),mats['stone_dark'],.016,root)
        for x in (-width*.2,width*.2):
            reel=cylinder(name+'_Reel',(x,-.17,height*.88),width*.13,.026,mats['gold'],10,root)
            reel.rotation_euler[0]=math.pi/2
            for a in (0,2.09,4.19):
                ico(name+'_ReelHole',(x+math.cos(a)*width*.075,-.19,height*.88+math.sin(a)*width*.075),width*.025,mats['wood_dark'],1,root)
    elif kind=='monitor':
        box(name+'_Panel',(0,.07,height*.91),(width*.75,.11,height*.29),mats['stone_dark'],.012,root)
        for i in range(3):ico(name+'_Signal',(-width*.22+i*width*.22,-.002,height*.96),.04,mats['green'] if i<2 else mats['gold'],1,root)
    else:
        volume=F.open_book(name+'_Ledger',mats,width=width*.65,depth=.30,parent=root)
        volume.location=(0,-.03,height*.8)
        beam(name+'_Pen',(.23,-.1,height*.82),(.29,.12,height*.84),.012,.012,mats['gold'],root)
    return root


def bottles(name,mats,count=6,spacing=.17,parent=None):
    root=group(name,parent=parent)
    for i in range(count):
        obj=vessel(name+'_%02d'%i,mats,radius=.055,height=.17+(i%3)*.035,parent=root)
        obj.location=((i-(count-1)/2)*spacing,0,0)
    return root


def crates(name,mats,width=.9,depth=.7,height=.75,stack=2,parent=None):
    root=group(name,parent=parent)
    for row in range(stack):
        z=row*(height+.02)
        box(name+'_Box',(0,0,z+height/2),(width,depth,height),mats['wood_light'],.008,root)
        for x in (-width*.35,width*.35):
            box(name+'_Strap',(x,-depth/2-.012,z+height/2),(.06,.03,height),mats['wood'],.004,root)
        for h in (.1,height-.1):box(name+'_Cross',(0,-depth/2-.03,z+h),(width,.04,.075),mats['wood'],.004,root)
        beam(name+'_Diagonal',(-width*.4,-depth/2-.06,z+.12),(width*.4,-depth/2-.06,z+height-.12),.06,.03,mats['wood'],root,.003)
    return root


def sacks(name,mats,count=3,parent=None):
    root=group(name,parent=parent)
    for i in range(count):
        x=(i%2)*.48-.24;y=(i//2)*.42
        obj=ico(name+'_Sack',(x,y,.32),.38,mats['stone'],2,root)
        obj.scale=(.72,.65,1)
        cylinder(name+'_Tie',(x,y,.66),.07,.08,mats['wood'],8,root)
    return root
