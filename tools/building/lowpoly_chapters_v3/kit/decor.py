"""Pedestals, coat stands and decks used by room assembly."""
import math
from .core import group, box, cylinder, beam, ico, mesh


def pedestal(name,mats,height=.8,width=.48,parent=None):
    root=group(name,parent=parent)
    for suffix,z,w,h,mat in [
        ('Foot',.06,width*1.13,.12,'wood_light'),
        ('Plinth',.17,width,.16,'wood'),
        ('Shaft',height*.53,width*.78,height-.29,'wood'),
        ('Neck',height-.13,width*.9,.10,'wood_dark'),
        ('Capital',height-.055,width*1.15,.11,'wood_light')]:
        box(name+'_'+suffix,(0,0,z),(w,w,h),mats[mat],.015,root)
    box(name+'_Recess',(0,-width*.397,height*.52),(width*.48,.02,height*.48),mats['wood_dark'],.008,root)
    box(name+'_Panel',(0,-width*.412,height*.52),(width*.39,.025,height*.41),mats['wood'],.008,root)
    return root


def coat_stand(name,mats,height=1.95,parent=None):
    root=group(name,parent=parent)
    cylinder(name+'_Base',(0,0,.07),.32,.14,mats['wood_dark'],8,root)
    cylinder(name+'_Base_cap',(0,0,.16),.24,.08,mats['wood'],8,root)
    cylinder(name+'_Column',(0,0,height*.5),.047,height-.24,mats['wood_light'],8,root)
    ico(name+'_Finial',(0,0,height),.085,mats['wood_light'],1,root)
    for level in [.68,.85]:
        z=height*level
        for i in range(4):
            a=i*math.pi/2+.3
            x,y=.29*math.cos(a),.29*math.sin(a)
            beam(name+'_Arm',(0,0,z),(x,y,z+.10),.038,.038,mats['wood_light'],root)
            ico(name+'_Hook',(x,y,z+.13),.055,mats['gold'],1,root)
    # Lowpoly coat, three angular vertical folds with thickness.
    from .core import polyprism
    coat=polyprism(name+'_Hanging_coat',[(-.17,-.12),(.05,-.16),(.17,-.08),(.11,-.02),(-.1,-.01)],.45,1.46,mats['book_red'],root)
    coat.location=(.10,.08,0)
    return root


def platform(name,mats,width=2,depth=1.5,height=1.8,thickness=.18,parent=None):
    root=group(name,parent=parent)
    box(name+'_Timber_fascia',(0,0,height-thickness/2),(width,depth,thickness),mats['wood'],.018,root)
    box(name+'_Stone_top',(0,0,height+.025),(width-.035,depth-.035,.05),mats['stone'],.012,root)
    return root


def library_rail(name,mats,length=4,height=3.2,parent=None):
    root=group(name,parent=parent)
    beam(name+'_Rail',(-length/2,-.36,height),(length/2,-.36,height),.05,.05,mats['gold'],root)
    for x in [-length/2,0,length/2]:
        beam(name+'_Mount',(x,0,height),(x,-.36,height),.06,.06,mats['gold'],root)
        cylinder(name+'_End_cap',(x,-.36,height),.075,.08,mats['gold'],8,root)
    return root
