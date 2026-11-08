"""Reusable outdoor low-poly props: solids, paths and faceted flat colours.

No component contains a room layout, chapter action or neighboring map. Local
Z=0 is the placement surface, except dock/water where the datum is deck/sea.
"""
import math
import random

import bpy
from . import core as C, furniture as F, lighting as L, variants as V, decor as D


def outdoor_materials(base):
    mats=dict(base)
    colors={'leaf_sun':'#718047','leaf_mid':'#52643B','leaf_dark':'#35472D',
            'soil':'#53432D','water_0':'#315C5A','water_1':'#386B65',
            'water_2':'#477B70','water_3':'#2B5657','rope':'#A88658'}
    for key,color in colors.items():
        name='LP_Outdoor_'+key
        mats[key]=bpy.data.materials.get(name) or C.solid_material(name,color,.42 if key.startswith('water') else .82)
    return mats


def stone_wall(name,mats,length=4,height=1.05,thickness=.32,pier_spacing=3.0,parent=None):
    """Mortared block wall with actual block seams, coping and square piers."""
    root=C.group(name,parent=parent)
    C.box(name+'_Core',(0,0,height/2),(length,thickness*.90,height),mats['stone_dark'],0,root)
    rows=max(1,round(height/.32));rh=height/rows
    for row in range(rows):
        start=-length/2-(.5 if row%2 else 0)
        for col in range(math.ceil(length)+2):
            a=max(-length/2,start+col);b=min(length/2,start+col+1)
            if b-a<.03:continue
            C.box(name+'_Block_%d_%d'%(row,col),((a+b)/2,0,(row+.5)*rh),
                  (b-a-.012,thickness,rh-.012),mats['stone'] if (row+col)%4 else mats['cream'],.012,root)
    C.box(name+'_Coping',(0,0,height+.045),(length+.04,thickness+.12,.09),mats['cream'],.012,root)
    spans=max(1,math.ceil(length/pier_spacing))
    for i in range(spans+1):
        x=-length/2+length*i/spans
        C.box(name+'_Pier_%d'%i,(x,0,(height+.12)/2),(.43,.43,height+.12),mats['stone'],.012,root)
        C.box(name+'_Pier_cap_%d'%i,(x,0,height+.14),(.55,.55,.16),mats['cream'],.018,root)
    return root


def courtyard_gate(name,mats,width=2.20,height=2.70,rise=.65,parent=None):
    root=V.arched_closed_door(name,mats,width=width,height=height,rise=rise,double=True,parent=parent)
    for obj in root.children_recursive:
        if obj.type!='MESH':continue
        if '_Arch_jamb' in obj.name or '_Arch_moulding' in obj.name or '_Arch_inner_bead' in obj.name:
            obj.data.materials.clear();obj.data.materials.append(mats['stone'] if '_jamb' in obj.name else mats['cream'])
    for sign in (-1,1):
        C.box(name+'_Stone_Pier_'+str(sign),(sign*(width/2+.30),.04,height/2),(.44,.48,height),mats['stone'],.015,root)
        C.box(name+'_Stone_Cap_'+str(sign),(sign*(width/2+.30),.04,height+.08),(.60,.62,.18),mats['cream'],.015,root)
    root['transition_mode']='teleport'
    return root


def tree(name,mats,height=3.8,crown_radius=1.15,seed=7,parent=None):
    root=C.group(name,parent=parent);rng=random.Random(seed)
    C.lathe(name+'_Trunk',[(.18,0),(.13,height*.18),(.10,height*.48),(.055,height*.76)],7,mats['wood'],root)
    for i in range(5):
        angle=2*math.pi*i/5+.4
        end=(crown_radius*.78*math.cos(angle),crown_radius*.78*math.sin(angle),height*(.58+.06*(i%3)))
        C.beam(name+'_Branch_'+str(i),(0,0,height*.35),end,.09,.11,mats['wood'],root,.004)
    for i in range(10):
        angle=2*math.pi*i/7
        radius=crown_radius*(.62 if i<7 else .28)
        z=height*(.62 if i<7 else .81)+rng.uniform(-.08,.10)
        location=(radius*math.cos(angle),radius*math.sin(angle),z)
        leaf=C.ico(name+'_Crown_%02d'%i,location,crown_radius*(.60 if i<7 else .67),
                    mats[('leaf_mid','leaf_sun','leaf_dark')[i%3]],1,root)
        leaf.scale=(1.0,rng.uniform(.82,1.10),rng.uniform(.75,1.0))
        leaf.rotation_euler=(rng.uniform(0,.4),rng.uniform(0,.4),angle)
    root['kind']='faceted_tree';root['seed']=seed
    return root


def planter_bed(name,mats,width=2.0,depth=.85,height=.42,shrubs=True,parent=None):
    root=C.group(name,parent=parent)
    C.box(name+'_Soil',(0,0,height-.075),(width-.14,depth-.14,.08),mats['soil'],0,root)
    for x in (-width/2+.055,width/2-.055):
        C.box(name+'_End',(x,0,height/2),(.11,depth,height),mats['stone'],.012,root)
    for y in (-depth/2+.055,depth/2-.055):
        C.box(name+'_Side',(0,y,height/2),(width-.12,.11,height),mats['stone'],.012,root)
    if shrubs:
        count=max(3,round(width/.30))
        for i in range(count):
            leaf=C.ico(name+'_Shrub_%02d'%i,(-width*.35+width*.70*i/(count-1),0,height+.12),
                        min(.30,depth*.40),mats['leaf_mid'] if i%2 else mats['leaf_sun'],1,root)
            leaf.scale=(1,.85,1.0+(i%3)*.15)
    return root


def fountain(name,mats,radius=1.05,height=1.40,parent=None):
    root=C.group(name,parent=parent)
    C.cylinder(name+'_Octagonal_Foot',(0,0,.06),radius+ .16,.12,mats['stone_dark'],8,root)
    C.lathe(name+'_Basin',[(0,.12),(radius,.12),(radius,.56),(radius-.11,.62),
                          (radius-.20,.62),(radius-.22,.24),(0,.24)],8,mats['stone'],root)
    C.cylinder(name+'_Water',(0,0,.34),radius-.22,.028,mats['water_2'],8,root)
    C.lathe(name+'_Column',[(.24,.26),(.29,.32),(.19,.42),(.12,height*.70),
                           (.26,height*.81),(.31,height*.93),(.17,height),(0,height)],8,mats['cream'],root)
    for i in range(4):
        angle=i*math.pi/2+math.pi/4
        points=[]
        for j in range(7):
            t=j/6;r=(radius-.30)*t
            points.append((r*math.cos(angle),r*math.sin(angle),height-.04+.20*t-(height-.34)*t*t))
        for j in range(len(points)-1):
            C.beam(name+'_Water_Arc_%d_%d'%(i,j),points[j],points[j+1],.035,.035,mats['water_2'],root,0)
    root['water']='Stylized opaque solid streams; no fluid simulation or texture.'
    return root


def scholarly_statue(name,mats,height=2.8,parent=None):
    """Faceted generic robed scholar; no named person or narrative identity."""
    root=C.group(name,parent=parent);base=height*.30;body=height-base
    for z,w,h in ((.08,.92,.16),(.20,.76,.14),(base/2+.12,.64,base-.30),(base-.04,.83,.12)):
        C.box(name+'_Plinth',(0,0,z),(w,w,h),mats['stone'],.014,root)
    C.lathe(name+'_Robe',[(.32,base),(.34,base+.10),(.27,base+body*.42),
                         (.20,base+body*.70),(.27,base+body*.76),(.14,base+body*.84)],7,mats['cream'],root)
    head=C.ico(name+'_Head',(0,-.025,base+body*.91),body*.12,mats['cream'],1,root)
    head.scale=(.86,.90,1.05)
    C.beam(name+'_Left_Arm',(-.22,0,base+body*.73),(-.23,-.22,base+body*.48),.14,.15,mats['cream'],root,.01)
    C.beam(name+'_Right_Arm',(.22,0,base+body*.72),(.10,-.25,base+body*.56),.14,.15,mats['cream'],root,.01)
    book=C.box(name+'_Stone_Book',(-.04,-.28,base+body*.57),(.35,.09,.30),mats['stone'],.008,root)
    book.rotation_euler[1]=-.18
    C.ico(name+'_Nose',(0,-.20,base+body*.92),body*.035,mats['cream'],1,root)
    beard=C.ico(name+'_Beard',(0,-.13,base+body*.84),body*.085,mats['cream'],1,root)
    beard.scale=(.65,.55,1.05)
    root['identity']='generic_scholarly_statue_no_story_identity'
    return root


def _ring(name,center,radius,normal,mats,parent,segments=16):
    # Two explicit in-plane basis vectors; no dependent curve modifiers.
    if normal=='X':u,v=(0,1,0),(0,0,1)
    elif normal=='Y':u,v=(1,0,0),(0,0,1)
    else:u,v=(1,0,0),(0,.70710678,.70710678)
    pts=[tuple(center[k]+radius*(u[k]*math.cos(i*2*math.pi/segments)+v[k]*math.sin(i*2*math.pi/segments)) for k in range(3)) for i in range(segments)]
    for i,p in enumerate(pts):C.beam(name+'_%02d'%i,p,pts[(i+1)%segments],.018,.026,mats['gold'],parent,.002)


def instrument_table(name,mats,width=1.40,depth=.72,height=.88,instrument='armillary',parent=None):
    root=C.group(name,parent=parent)
    F.table(name+'_Table',mats,width=width,depth=depth,height=height,parent=root)
    cx=-width*.16
    C.lathe(name+'_Instrument_Base',[(0,0),(.17,0),(.19,.035),(.11,.07),(.045,.11),(.045,.20),(0,.20)],8,mats['gold'],root,(cx,0,height))
    if instrument=='armillary':
        center=(cx,0,height+.48)
        for normal in ('X','Y','T'):_ring(name+'_Armillary_'+normal,center,.29,normal,mats,root)
        C.beam(name+'_Axis',(cx-.11,0,height+.12),(cx+.11,0,height+.84),.025,.025,mats['gold'],root)
        C.ico(name+'_Globe',center,.085,mats['stone'],1,root)
    elif instrument=='sextant':
        bottom=(cx-.25,-.03,height+.16)
        pts=[(cx-.25+.55*math.cos(math.pi/6+i*math.pi/3/12),-.03,
              height+.16+.55*math.sin(math.pi/6+i*math.pi/3/12)) for i in range(13)]
        for i in range(len(pts)-1):C.beam(name+'_Sextant_Arc',pts[i],pts[i+1],.03,.04,mats['gold'],root)
        for p in (pts[0],pts[-1],pts[6]):C.beam(name+'_Sextant_Arm',bottom,p,.035,.035,mats['gold'],root)
    else:raise ValueError('instrument must be armillary or sextant')
    dial=C.cylinder(name+'_Compass',(width*.30,-.03,height+.035),.12,.04,mats['gold'],12,root)
    C.cylinder(name+'_Compass_Face',(width*.30,-.03,height+.058),.092,.008,mats['cream'],12,root)
    C.beam(name+'_Compass_Needle',(width*.30-.065,-.03,height+.065),(width*.30+.065,-.03,height+.065),.009,.009,mats['wood_dark'],root)
    C.group(name+'_Measurement_anchor',(cx,-.1,height+.40),parent=root)
    root['kind']='outdoor_instrument';root['instrument']=instrument
    return root


def lamp_post(name,mats,height=2.80,energy=22,parent=None):
    root=C.group(name,parent=parent)
    C.lathe(name+'_Pole',[(.20,0),(.21,.12),(.12,.22),(.07,.65),(.055,height-.35)],8,mats['wood_dark'],root)
    for z,r in ((.12,.21),(.65,.10),(height-.39,.10)):
        C.cylinder(name+'_Gold_Collar',(0,0,z),r,.065,mats['gold'],8,root)
    lantern=L.wall_lantern(name+'_Lantern',mats,height=.76,parent=root,light_energy=energy)
    lantern.location=(0,.18,height-.37)
    return root


def oath_platform(name,mats,width=4.2,depth=2.4,height=.60,parent=None):
    root=C.group(name,parent=parent)
    for i in range(3):
        extra=(2-i)*.30;h=height/3
        C.box(name+'_Stone_Step_%d'%i,(0,-extra/2,(i+.5)*h),
              (width+extra,depth+extra,h),mats['stone'],.012,root)
    C.box(name+'_Top_Trim',(0,0,height-.022),(width+.035,depth+.035,.044),mats['cream'],.008,root)
    root['surface_height']=height
    return root


def barrel(name,mats,radius=.38,height=.85,parent=None):
    root=C.group(name,parent=parent)
    profile=[(0,0),(radius*.82,0),(radius*.92,height*.13),(radius,height*.48),
             (radius*.92,height*.87),(radius*.82,height),(0,height)]
    C.lathe(name+'_Staves',profile,12,mats['wood'],root)
    for level in (.15,.50,.85):
        z=height*level;r=radius*(.94 if level!=.50 else 1.015)
        C.lathe(name+'_Band_'+str(level),[(r-.015,z-.025),(r+.012,z-.025),
                 (r+.012,z+.025),(r-.015,z+.025)],12,mats['gold'],root)
    C.cylinder(name+'_Lid',(0,0,height+.008),radius*.80,.025,mats['wood_light'],12,root)
    return root


def bollard(name,mats,height=.55,parent=None):
    root=C.group(name,parent=parent)
    C.cylinder(name+'_Foot',(0,0,.035),.25,.07,mats['gold'],8,root)
    C.lathe(name+'_Post',[(.15,.07),(.12,height-.10),(.20,height-.08),(.20,height),(0,height)],8,mats['gold'],root)
    return root


def rope_coil(name,mats,radius=.38,parent=None):
    root=C.group(name,parent=parent);points=[]
    for i in range(97):
        t=i/96;angle=t*math.pi*6;r=radius*(.36+.64*t)
        points.append((r*math.cos(angle),r*math.sin(angle),.035))
    for i in range(len(points)-1):C.beam(name+'_Segment_%02d'%i,points[i],points[i+1],.035,.035,mats['rope'],root,0)
    return root


def dock(name,mats,width=2.30,length=3.20,parent=None):
    """Deck datum is Z=0; posts physically extend below the water line."""
    root=C.group(name,parent=parent);count=max(4,round(length/.28))
    for i in range(count):
        y=-length/2+(i+.5)*length/count
        C.box(name+'_Deck_Plank_%02d'%i,(0,y,-.055),(width,length/count-.012,.11),mats['wood_light'] if i%3 else mats['wood'],.008,root)
    for x in (-width*.38,width*.38):
        C.box(name+'_Stringer',(x,0,-.20),(.13,length+.10,.22),mats['wood_dark'],.008,root)
    for x in (-width*.44,width*.44):
        for y in (-length*.43,length*.43):
            C.cylinder(name+'_Pile',(x,y,-.13),.105,1.18,mats['wood'],8,root)
            C.cylinder(name+'_Post_Cap',(x,y,.47),.135,.09,mats['wood_light'],8,root)
    for x in (-width*.38,width*.38):
        C.beam(name+'_Brace',(x,-length*.40,-.61),(x,0,-.16),.10,.12,mats['wood'],root)
        C.beam(name+'_Brace',(x,length*.40,-.61),(x,0,-.16),.10,.12,mats['wood'],root)
    root['finished_deck_z']=0.0
    return root


def water_patch(name,mats,width=10,depth=4.2,seed=3,parent=None):
    """Closed coloured triangle mesh, without textures or a water shader."""
    root=C.group(name,parent=parent);rng=random.Random(seed);nx,ny=8,5
    vertices=[]
    for j in range(ny+1):
        for i in range(nx+1):
            x=-width/2+width*i/nx;y=-depth/2+depth*j/ny
            if 0<i<nx:x+=rng.uniform(-.18,.18)
            if 0<j<ny:y+=rng.uniform(-.16,.16)
            vertices.append((x,y,rng.uniform(-.012,.012)))
    faces=[]
    for j in range(ny):
        for i in range(nx):
            a=j*(nx+1)+i;b=a+1;c=a+nx+2;d=a+nx+1
            faces.extend([(a,b,c),(a,c,d)] if (i+j)%2 else [(a,b,d),(b,c,d)])
    top_count=len(faces)
    perimeter=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,ny+1)]
    perimeter+=list(range(ny*(nx+1)+nx-1,ny*(nx+1)-1,-1))+[j*(nx+1) for j in range(ny-1,0,-1)]
    bottom=[]
    for index in perimeter:
        x,y,z=vertices[index];bottom.append(len(vertices));vertices.append((x,y,-.17))
    for i,index in enumerate(perimeter):
        j=(i+1)%len(perimeter);faces.append((index,bottom[i],bottom[j],perimeter[j]))
    faces.append(tuple(reversed(bottom)))
    obj=C.mesh(name+'_Faceted_Water',vertices,faces,mats['water_0'],root)
    for i in range(1,4):obj.data.materials.append(mats['water_'+str(i)])
    for i,face in enumerate(obj.data.polygons):face.material_index=rng.randrange(4) if i<top_count else 0
    root['kind']='faceted_water';root['water_rendering']='Opaque mesh facets, four constant colours.'
    return root


def rowboat(name,mats,length=2.60,width=1.05,height=.55,parent=None):
    root=C.group(name,parent=parent)
    shape=[(-width/2,length*.34),(-width*.43,-length*.25),(0,-length/2),
           (width*.43,-length*.25),(width/2,length*.34),(width*.30,length/2),(-width*.30,length/2)]
    n=len(shape);verts=[]
    for scale,z in ((.45,0),(1,height),(.86,height),(.40,.10)):
        verts.extend((x*scale,y*scale,z) for x,y in shape)
    faces=[tuple(reversed(range(n))),tuple(range(3*n,4*n))]
    for ring in range(3):
        for i in range(n):
            j=(i+1)%n;faces.append((ring*n+i,ring*n+j,(ring+1)*n+j,(ring+1)*n+i))
    obj=C.mesh(name+'_Hollow_Hull',verts,faces,mats['wood'],root,.006)
    obj.data.materials.append(mats['wood_light'])
    for i,face in enumerate(obj.data.polygons):
        if i==1 or i>=2+n:face.material_index=1
    for i,(x,y) in enumerate(shape):
        xx,yy=shape[(i+1)%n]
        C.beam(name+'_Gunwale_%02d'%i,(x,y,height),(xx,yy,height),.045,.055,mats['wood_light'],root,.004)
    for y in (-length*.13,length*.22):
        C.box(name+'_Seat',(0,y,height*.64),(width*.78,.18,.055),mats['wood_light'],.008,root)
    return root
