"""ASTRA independent editable architectural sketch generator, Blender 4.2.
Run prepare_terrain.py with system Python first, then blender -b -t 6 -P this_file.
This is an artifact authoring source, never a game runtime or production asset path.
"""
import bpy, math, json, os, numpy as np
from pathlib import Path
from mathutils import Vector
H=Path(__file__).resolve().parent
OUT=H
Z=np.load(H/'terrain_elevation.npy')
LAT=39.67325; LON=-31.1155
SCALE_XY=.5
def geo(x,y): return LON+x/(.5*111320*math.cos(math.radians(LAT))), LAT+y/(.5*111320)
def dem(x,y):
    lon,lat=geo(x,y)
    px=(lon+180)/360*8192*256-3387*256
    py=(1-np.arcsinh(np.tan(np.radians(lat)))/np.pi)/2*8192*256-3109*256
    ix,iy=int(px),int(py);u,v=px-ix,py-iy
    if ix<0 or iy<0 or ix>=Z.shape[1]-1 or iy>=Z.shape[0]-1:return 0.
    return float((Z[iy,ix]*(1-u)+Z[iy,ix+1]*u)*(1-v)+(Z[iy+1,ix]*(1-u)+Z[iy+1,ix+1]*u)*v)

bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for mat in bpy.data.materials: bpy.data.materials.remove(mat)
COLORS={'stone':(.55,.52,.44,1),'wall':(.86,.81,.67,1),'trim':(.95,.88,.72,1),'roof':(.19,.28,.26,1),'labroof':(.28,.31,.33,1),'wood':(.32,.20,.12,1),'floor':(.68,.58,.43,1),'glass':(.22,.48,.51,1),'dark':(.11,.15,.16,1),'soil':(.37,.32,.23,1),'terrain':(.34,.43,.25,1),'road':(.64,.55,.39,1),'cargo':(.72,.42,.20,1),'research':(.36,.62,.63,1),'water':(.16,.34,.43,1),'label':(.05,.06,.06,1),'rooms':(.85,.70,.44,1),'original':(.65,.35,.32,1),'suiteA':(.77,.86,.77,1),'suiteB':(.80,.81,.94,1),'suiteC':(.94,.83,.67,1),'suiteD':(.92,.75,.74,1)}
MATS={}
for name,color in COLORS.items():
    m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True
    m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color
    m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.82
    MATS[name]=m
DATA={}; RECORDS=[]; PADS=[]; LABELS=[]
def mesh(v,f,mat='wall',group='architecture'):
    key=(group,mat); data=DATA.setdefault(key,[[],[]]);n=len(data[0]);data[0].extend(v);data[1].extend([[i+n for i in q] for q in f])
def box(x,y,z,w,d,h,mat='wall',group='architecture',angle=0):
    c,s=math.cos(angle),math.sin(angle)
    v=[]
    for zz in [z,z+h]:
        for xx,yy in [(-w/2,-d/2),(w/2,-d/2),(w/2,d/2),(-w/2,d/2)]:v.append((x+c*xx-s*yy,y+s*xx+c*yy,zz))
    mesh(v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,group)
def slab_with_holes(x,y,z,w,d,holes,group):
    # Split a rectangular slab into cells at opening edges; do not cap staircase voids.
    x0,x1=x-w/2,x+w/2;y0,y1=y-d/2,y+d/2
    xs=sorted(set([x0,x1]+[max(x0,min(x1,h[i])) for h in holes for i in [0,1]]))
    ys=sorted(set([y0,y1]+[max(y0,min(y1,h[i])) for h in holes for i in [2,3]]))
    for a,b in zip(xs,xs[1:]):
        for c,e in zip(ys,ys[1:]):
            if b-a<.01 or e-c<.01:continue
            mx,my=(a+b)/2,(c+e)/2
            if any(h[0]<mx<h[1] and h[2]<my<h[3] for h in holes):continue
            box(mx,my,z-.28,b-a,e-c,.28,'floor',group)
def beam(a,b,width=.25,depth=.25,mat='wood',group='architecture'):
    a,b=Vector(a),Vector(b);axis=(b-a).normalized();up=Vector((0,0,1))
    if abs(axis.dot(up))>.98:up=Vector((1,0,0))
    u=axis.cross(up).normalized()*width/2;v=axis.cross(u).normalized()*depth/2
    vs=[tuple(p+du*u+dv*v) for p in [a,b] for du,dv in [(-1,-1),(1,-1),(1,1),(-1,1)]]
    mesh(vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,group)
def roof(x,y,z,w,d,rise=2.3,mat='roof',group='roofs',ang=0):
    c,s=math.cos(ang),math.sin(ang)
    raw=[(-w/2,-d/2,0),(w/2,-d/2,0),(w/2,d/2,0),(-w/2,d/2,0),(-w/2,0,rise),(w/2,0,rise)]
    v=[(x+c*a-s*b,y+s*a+c*b,z+h) for a,b,h in raw]
    mesh(v,[(0,1,5,4),(4,5,2,3),(0,4,3),(1,2,5)],mat,group)
def transform(x,y,cx,cy,a):return cx+x*math.cos(a)-y*math.sin(a),cy+x*math.sin(a)+y*math.cos(a)
def wall_segment(cx,cy,z,L,h,ang,group,door=False,windows=True):
    # Each elevation is assembled around open apertures, not textured solid walls.
    n=max(1,round(L/4.0));module=L/n;t=.42
    for i in range(n):
        off=-L/2+(i+.5)*module;x,y=transform(off,0,cx,cy,ang)
        isdoor=door and i==n//2
        opening=1.6 if isdoor else (1.9 if h>5 else 1.6); sill=0 if isdoor else (1.2 if h>5 else 1.05);head=3.8 if h>5 else 2.75
        if not windows and not isdoor:box(x,y,z,module,t,h,'wall',group,ang);continue
        for edge in [-1,1]:
            xx,yy=transform(edge*(module+opening)/4,0,x,y,ang)
            box(xx,yy,z,(module-opening)/2,t,h,'wall',group,ang)
        if sill:box(x,y,z,opening,t,sill,'wall',group,ang)
        box(x,y,z+head,opening,t,h-head,'wall',group,ang)
        box(x,y,z+head-.08,opening+.18,.57,.14,'trim',group,ang)
        if not isdoor:
            # Deliberately no glazing plane: the aperture can be inspected in GLB.
            box(x,y,z+sill-.07,opening+.18,.6,.15,'trim',group,ang)
            box(x,y,z+sill, .07,.12,head-sill,'wood',group,ang)
            box(x,y,z+1.9,opening,.12,.07,'wood',group,ang)
def stairs(x,y,z,height,width=2.0,run=5.8,ang=0,group='stairs'):
    if height>4.8 and run<9:
        # Two flights and a real intermediate landing keep ordinary riser/tread proportions.
        n=max(14,round(height/.175/2));fw=(width-.2)/2;flight=run-1.5
        for i in range(n):
            yy=-run/2+(i+.5)*flight/n
            xx,sy=transform(-width/4,yy,x,y,ang);box(xx,sy,z,fw,flight/n+.01,(i+1)*height/(2*n),'stone',group,ang)
            xx,sy=transform(width/4,-run/2+flight-(i+.5)*flight/n,x,y,ang);box(xx,sy,z+height/2,fw,flight/n+.01,(i+1)*height/(2*n),'stone',group,ang)
        xx,yy=transform(0,run/2-.75,x,y,ang);box(xx,yy,z+height/2-.2,width,1.5,.2,'stone',group,ang)
        return
    n=max(12,round(height/.175));step=run/n
    for i in range(n):
        xx,yy=transform(0,-run/2+(i+.5)*step,x,y,ang)
        box(xx,yy,z,width,step+.01,(i+1)*height/n,'stone',group,ang)
    for sign in [-1,1]:
        a=transform(sign*(width/2+.07),-run/2,x,y,ang);b=transform(sign*(width/2+.07),run/2,x,y,ang)
        beam((*a,z+.9),(*b,z+height+.9),.08,.08,'wood',group)
def pad(cx,cy,w,d,base,ang,name):
    PADS.append((cx,cy,w,d,base-.32,ang,name))
    # Retaining walls follow original-ground bottom and a level platform top.
    for axis in [0,1]:
        length=w if axis==0 else d;n=max(2,int(length/3))
        for side in [-1,1]:
            for i in range(n):
                q=-length/2+(i+.5)*length/n
                lx,ly=(q,side*d/2) if axis==0 else (side*w/2,q)
                x,y=transform(lx,ly,cx,cy,ang);g=dem(x,y);top=base-.3
                if top>g:box(x,y,g-.3,length/n+.06,.55,top-g+.3,'stone','retaining',ang+axis*math.pi/2)
    box(cx,cy,base-.35,w,d,.35,'stone','platforms',ang)
def label(text,x,y,z,size=2,group='labels'):
    LABELS.append((text,x,y,z,size,group))
def building(name,cx,cy,w,d,floors=1,h=3.6,base=None,ang=0,kind='support',pier=False,roomgrid=True):
    if base is None:base=dem(cx,cy)+1.2
    gvals=[dem(*transform(i*w/2,j*d/2,cx,cy,ang)) for i in [-1,0,1] for j in [-1,0,1]]
    RECORDS.append({'id':name,'type':kind,'x_m':cx,'y_m':cy,'source_lon_lat':geo(cx,cy),'finished_floor_m':base,'raw_ground_range_m':[min(gvals),max(gvals)],'floors':floors,'floor_height_m':h,'footprint_m2':w*d,'gross_floor_area_m2':w*d*floors,'width_m':w,'depth_m':d,'rotation_deg':math.degrees(ang),'raised_piers':pier})
    if not pier and kind not in ['main4','tower','interface']:pad(cx,cy,w+1.0,d+1.0,base,ang,name)
    elif pier:
        for xx in np.linspace(-w/2+.45,w/2-.45,max(3,int(w/3)+1)):
            for yy in [-d/2+.5,d/2-.5]:
                x,y=transform(xx,yy,cx,cy,ang);g=dem(x,y)
                box(x,y,g-.3,.6,.6,max(.4,base-g+.3),'stone',kind+'_piers',ang)
        for yy in [-d/2+.5,d/2-.5]:
            a=transform(-w/2+.5,yy,cx,cy,ang);b=transform(w/2-.5,yy,cx,cy,ang)
            beam((*a,base-.2),(*b,base-.2),.35,.3,'wood',kind+'_piers')
        # Short corner knees create a visible continuous bracing system.
        for xx in [-w/2+.5,w/2-.5]:
            for yy in [-d/2+.5,d/2-.5]:
                p=transform(xx,yy,cx,cy,ang);q=transform(xx+(-1 if xx>0 else 1)*1.1,yy,cx,cy,ang)
                beam((*p,base-.9),(*q,base-.2),.16,.16,'wood',kind+'_piers')
    for f in range(floors):
        group=f'{kind}_{name}_L{f+1}';z=base+f*h
        hole=None
        if f>0:hole=(w/2-4.1,w/2-1.9,-3.2,3.2)
        if kind=='main4':hole=(-4.2,4.2,-3.9,-.1)
        if kind=='tower':hole=(-1.5,1.5,-3.5,3.5)
        if hole:
            hx0,hx1,hy0,hy1=hole
            for x0,x1,y0,y1 in [(-w/2,hx0,-d/2,d/2),(hx1,w/2,-d/2,d/2),(hx0,hx1,-d/2,hy0),(hx0,hx1,hy1,d/2)]:
                xx,yy=transform((x0+x1)/2,(y0+y1)/2,cx,cy,ang);box(xx,yy,z-.22,x1-x0,y1-y0,.22,'floor',group,ang)
        else:box(cx,cy,z-.22,w,d,.22,'floor',group,ang)
        for off,a,L in [((0,-d/2),ang,w),((0,d/2),ang,w),((-w/2,0),ang+math.pi/2,d),((w/2,0),ang+math.pi/2,d)]:
            x,y=transform(*off,cx,cy,ang);wall_segment(x,y,z,L,h-.22,a,group,door=(off[1]==-d/2),windows=True)
        # Regular internal supports continue vertically, with visible beams.
        for xx in np.arange(-w/2+5,w/2-2,5):
            for yy in np.arange(-d/2+5,d/2-2,5):
                x,y=transform(xx,yy,cx,cy,ang);box(x,y,z,.3,.3,h-.22,'wood',group,ang)
        if roomgrid:
            for xx in np.arange(-w/2+5,w/2-2,5):
                x,y=transform(xx,0,cx,cy,ang);wall_segment(x,y,z,d*.65,h-.22,ang+math.pi/2,group,door=True,windows=False)
        if floors>1 and f<floors-1:
            sx,sy=transform(w/2-3,0,cx,cy,ang);stairs(sx,sy,z,h,width=1.8,run=min(d-2,6),ang=ang,group=group)
    roof(cx,cy,base+floors*h,w+1.2,d+1.2,2 if d<12 else 3,'labroof' if kind=='lab' else 'roof',kind+'_roofs',ang)
    if pier:
        sx,sy=transform(0,-d/2-1.5,cx,cy,ang);ground=dem(sx,sy)
        if base>ground:stairs(sx,sy,ground,base-ground,1.5,2.7,ang,kind+'_stairs')
    label(name,cx,cy,base+floors*h+3,2,kind+'_labels')

# MAIN: offset courtyard wings, not a rigid campus square.  All levels retain brief semantics.
MAIN_BASE=49.0
main_blocks=[('south',30,48,76,12),('west',-2,72,12,36),('east',62,69,12,30),('north',30,84,52,12),('link',30,66,12,24)]
for name,x,y,w,d in main_blocks:
    pad(x,y,w,d,MAIN_BASE,0,'main_'+name)
    levels=3 if name in ['south','north','link'] else 2
    RECORDS.append({'id':'main_'+name,'type':'main','x_m':x,'y_m':y,'source_lon_lat':geo(x,y),'finished_floor_m':MAIN_BASE,'floors':levels,'footprint_m2':w*d,'gross_floor_area_m2':w*d*levels,'level_functions':['1F dining/kitchen/grain','2F four suites/lecture/lounge','3F reception/guest/library'][:levels]})
    for f in range(levels):
        z=MAIN_BASE+[0,5.8,11.4][f];height=[5.8,5.6,5.6][f];group=f'main_L{f+1}'
        holes=[] if f==0 else [(23,31,44.1,47.9),(-3.8,-.2,78,86)]
        slab_with_holes(x,y,z,w,d,holes,group)
        for xx,yy,L,a in [(x,y-d/2,w,0),(x,y+d/2,w,0),(x-w/2,y,d,math.pi/2),(x+w/2,y,d,math.pi/2)]:wall_segment(xx,yy,z,L,height-.28,a,group,door=True)
        for xx in np.arange(x-w/2+5,x+w/2-1,5):
            for yy in np.arange(y-d/2+4,y+d/2-1,5):
                if not any(h[0]<xx<h[1] and h[2]<yy<h[3] for h in holes):box(xx,yy,z,.35,.35,height-.28,'wood',group)
        for xx in np.arange(x-w/2+5,x+w/2-1,5):box(xx,y,z+height-.7,.25,d,.35,'wood',group)
    if name=='north':
        roof(7.5,84,MAIN_BASE+17,7,13.2,2.8,'roof','main_roofs')
        roof(42.5,84,MAIN_BASE+17,27,13.2,2.8,'roof','main_roofs')
    elif name=='link':
        roof(30,58.25,MAIN_BASE+17,13.2,8.5,1.8,'roof','main_roofs')
        roof(30,74.75,MAIN_BASE+17,13.2,6.5,1.4,'roof','main_roofs')
    else:roof(x,y,MAIN_BASE+([17] if levels==3 else [11.4])[0],w+1.2,d+1.2,2.8,'roof','main_roofs')

# True floor-by-floor room divisions, openings and stairs. Diagram indices are not story room IDs.
for f in [1,2,3]:
    z=MAIN_BASE+[0,5.8,11.4][f-1];hh=[5.8,5.6,5.6][f-1];gr=f'main_L{f}'
    if f<3:
        stairs(27,46,z,hh,3.4,7.5,math.pi/2,gr)
        stairs(-2,82,z,hh,3.0,7.5,0,gr)
    else:stairs(20,82,z,hh,3.0,7.5,math.pi/2,gr)
    if f==2:
        n=0
        for idx,(start,end,north) in enumerate([(-8,16,False),(44,68,False),(4,30,True),(30,56,True)]):
            key='ABCD'[idx];mid=(start+end)/2;yr=87 if north else 45;yd=84 if north else 48;yc=83 if north else 49;ys=81 if north else 51;yp=80 if north else 52
            box(mid,yr,z+.015,end-start,6,.035,'suite'+key,gr)
            # Four 4.5x6m rooms, their own corridor, a separate lounge, wet/service strip.
            for i in range(4):
                xx=start+i*4.5
                wall_segment(xx+2.25,yd,z,4.5,hh-.28,0,gr,door=True,windows=False)
                if i:wall_segment(xx,yr,z,6,hh-.28,math.pi/2,gr,windows=False)
                n+=1;box(xx+2.25,yr,z,.95,2.1,.48,'rooms',gr)
                label('S%02d'%n,xx+2.25,yr-1.6,z+.075,.65,'main_plan_labels')
            wall_segment(start+18,yr,z,6,hh-.28,math.pi/2,gr,windows=False)
            wall_segment((start+18+end)/2,yd,z,end-start-18,hh-.28,0,gr,door=True,windows=False)
            wall_segment(mid,yp,z,end-start,hh-.28,0,gr,door=True,windows=False)
            for xx in [start,end]:wall_segment(xx,85 if north else 47,z,10,hh-.28,math.pi/2,gr,windows=False)
            # Bath/pantry separation and an entrance vestibule between public and private corridors.
            wall_segment(start+5,ys,z,2,hh-.28,math.pi/2,gr,windows=False)
            wall_segment(end-5,ys,z,2,hh-.28,math.pi/2,gr,windows=False)
            for xx in [start+2.5,end-2.5]:
                wall_segment(xx,82 if north else 50,z,5,hh-.28,0,gr,door=True,windows=False)
                box(xx,ys,z,1.6,.55,.9,'stone',gr)
            label(key+'区活动厅',(start+18+end)/2,yr,z+.075,.6,'main_plan_labels')
            label(key+'区入口',mid,ys,z+.075,.65,'main_plan_labels')
            label('盥洗',start+2.5,ys,z+.95,.6,'main_plan_labels')
            label('备餐',end-2.5,ys,z+.95,.6,'main_plan_labels')
        for x,y,text in [(-2,66,'共用讲堂'),(62,67,'共用起居厅'),(30,58,'公共交通'),(4,53,'公共回廊'),(43,79,'公共回廊')]:label(text,x,y,z+.075,1.0,'main_plan_labels')
    elif f==1:
        for x,y,L,a in [(8,48,12,math.pi/2),(-2,72,12,0),(30,84,12,math.pi/2),(48,48,12,math.pi/2)]:wall_segment(x,y,z,L,hh-.28,a,gr,door=True,windows=False)
        for x in [-2,4,10,16,22]:
            box(x,46,z+.7,2.6,1.2,.12,'wood',gr)
        for text,x,y in [('宴会 / 食堂',6,48),('厨房',-2,62),('粮仓 / 收货',-2,81),('娱乐 / 会客',58,48),('礼仪门厅',30,84),('后勤副厅',46,48)]:label(text,x,y,z+.34,1.0,'main_L1_labels')
    else:
        # Eight guest rooms, two sleepers each: 16 guest places. No fictional identities.
        for i in range(8):
            xx=-8+i*9.5
            if i:wall_segment(xx,47,z,10,hh-.28,math.pi/2,gr,door=True,windows=False)
            for dx in [-1.1,1.1]:box(xx+4.75+dx,46,z,1,2,.5,'rooms',gr)
        label('八间客房 / 十六席',30,48,z+.34,1.3,'main_L3_labels')
        label('藏书 / 迎宾',25,84,z+.34,1.3,'main_L3_labels')
        for xx in [8,14,20,26,32]:box(xx,85,z,1,5,2.5,'wood',gr)

# Local fourth floor archive with continuous walls, separate clock core.
building('Register archive',20,84,18,10,1,4.8,MAIN_BASE+17,kind='main4',roomgrid=False)
building('Clock tower',30,67,7,8,1,10.8,MAIN_BASE+17,kind='tower',roomgrid=False)
# Clock structural walls are continuous through the main stack; leave a passage beside it.
for xx in [27,33]:box(xx,67,MAIN_BASE,.45,8,17,'stone','main_core')
for f in range(5):stairs(30,67,MAIN_BASE+f*5.6,5.6,2.8,7.0,0,'tower_stairs')
for y in [62.9,71.1]:
    bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=1.65,depth=.12,location=(30,y,MAIN_BASE+24.3),rotation=(math.pi/2,0,0))
    ob=bpy.context.object;ob.name='clock dial';ob.data.materials.append(MATS['trim'])
    beam((30,y-.1,MAIN_BASE+24.3),(30,y-.1,MAIN_BASE+25.5),.1,.1,'dark','clock')
    beam((30,y-.1,MAIN_BASE+24.3),(31,y-.1,MAIN_BASE+24.3),.1,.1,'dark','clock')

# Basement sketches use separate hidden-in-exterior collections, not terrain replacement.
for name,x,y,w,d,z,desc in [('B1_service',30,48,60,12,44.8,'B1 SERVICE / CISTERN SEPARATE'),('B1_north',30,84,40,12,44.8,'B1 SERVICE JUNCTION'),('B2_plant',8,48,26,12,40.6,'B2 PLANT / FOUR ANCHORS')]:
    box(x,y,z-.3,w,d,.3,'stone',name)
    for xx,yy,L,a in [(x,y-d/2,w,0),(x,y+d/2,w,0),(x-w/2,y,d,math.pi/2),(x+w/2,y,d,math.pi/2)]:wall_segment(xx,yy,z,L,3.5,a,name,door=True,windows=False)
    RECORDS.append({'id':name,'type':'basement','gross_floor_area_m2':w*d,'finished_floor_m':z,'floors':1,'x_m':x,'y_m':y,'source_lon_lat':geo(x,y)})
    label(desc,x,y,z+.4,1,name+'_labels')
box(52,48,44.8,12,9,2.8,'water','B1_service')
for xx in [0,8]:
    for yy in [45,51]:box(xx,yy,40.6,2.2,2.2,2,'stone','B2_plant')

# Courtyard ground/slab and ceremonial entry: no black voids or floating entrance.
for x,w in [(14,20),(46,20)]:
    pad(x,66,w,24,MAIN_BASE,0,'courtyard')
    box(x,66,MAIN_BASE+.02,w,24,.06,'road','courtyard')
pad(30,96,18,12,MAIN_BASE,0,'north_forecourt')
stairs(30,96,MAIN_BASE,dem(30,102)-MAIN_BASE,4.2,12,0,'entrance_stairs')
box(30,90.5,MAIN_BASE-.2,10,1,.2,'stone','entrance_stairs')
# North ceremonial porch faces the caldera; southern side remains service-oriented.
for xx in [25,35]:box(xx,92,MAIN_BASE,.55,.55,5.2,'stone','main_portico')
roof(30,91.8,MAIN_BASE+5.2,12,4.5,1.4,'roof','main_portico')
label('正门 / 火山口方向',30,94,MAIN_BASE+.1,1,'main_L1_labels')

# Rotate only the independent main-house design to the DEM-derived caldera target.
MAIN_BEARING=math.atan2(230-30,2060-66)
MAIN_ANGLE=-MAIN_BEARING
def mainpoint(x,y):return transform(x-30,y-66,30,66,MAIN_ANGLE)
for values in DATA.values():
    values[0][:]=[(*mainpoint(v[0],v[1]),v[2]) for v in values[0]]
for ob in list(bpy.context.scene.objects):
    ob.location.x,ob.location.y=mainpoint(ob.location.x,ob.location.y)
    ob.rotation_euler.z+=MAIN_ANGLE
PADS[:]=[( *mainpoint(cx,cy),w,d,z,a+MAIN_ANGLE,n) for cx,cy,w,d,z,a,n in PADS]
for r in RECORDS:
    r['x_m'],r['y_m']=mainpoint(r['x_m'],r['y_m']);r['source_lon_lat']=geo(r['x_m'],r['y_m']);r['main_axis_bearing_degrees']=math.degrees(MAIN_BEARING)
LABELS[:]=[(t,*mainpoint(x,y),z,s,g) for t,x,y,z,s,g in LABELS]

# Four research pavilions share a hillside contour belt but are deliberately staggered.
LABS=[('R1',-65,70,-.18),('R2',113,75,.16),('R3',-127,38,-.30),('R4',171,27,.24)]
for name,x,y,a in LABS:
    building(name,x,y,26,20,3,4.2,dem(x,y)+1.1,a,'lab',False,True)

# Service court and three workshops/warehouses, offset instead of grid-aligned.
for name,x,y,w,d,a in [('Supply hall',-45,-8,38,18,-.16),('Laundry repair',-91,1,30,16,.10),('Fuel equipment',-115,36,26,14,-.24),('Cold dry stores',-3,-12,28,16,.07)]:building(name,x,y,w,d,1,4.2,None,a,'support',False,True)

# Raised single-storey staff houses, 16 x four-place modules = 64 design places.
RESIDENTIAL_USED=[]
def residential_site(tx,ty,w,d,region):
    """Keep human-scale modules; choose lower-gradient ground instead of taller stilts."""
    xmin,xmax,ymin,ymax=region;choices=[]
    for x in np.arange(xmin,xmax,4.5):
        for y in np.arange(ymin,ymax,4.5):
            x=float(x)+1.1*math.sin(float(y));y=float(y)+.8*math.sin(float(x))
            rad=.5*math.hypot(w,d)
            if any(math.hypot(x-xx,y-yy)<rad+rr+2.5 for xx,yy,rr in RESIDENTIAL_USED):continue
            g=dem(x,y)
            if g<6 or g>32:continue
            gx=dem(x+2,y)-dem(x-2,y);gy=dem(x,y+2)-dem(x,y-2)
            a=math.atan2(-gx,gy)
            vals=[dem(*transform(s*w/2,t*d/2,x,y,a)) for s in [-1,0,1] for t in [-1,0,1]]
            span=max(vals)-min(vals)
            score=span*span*18+math.hypot(x-tx,y-ty)*.12
            choices.append((score,x,y,a,max(vals),span))
    if not choices:raise RuntimeError('No non-overlapping residential footprint candidate')
    _,x,y,a,g,span=min(choices)
    RESIDENTIAL_USED.append((x,y,.5*math.hypot(w,d)))
    return x,y,a,g+.8

staff_positions=[(-146,-40),(-122,-50),(-98,-60),(-72,-65),(-156,-69),(-130,-78),(-102,-89),(-76,-96),(-166,-98),(-137,-107),(-108,-117),(-80,-128),(-177,-126),(-148,-138),(-117,-147),(-88,-155)]
for i,(x,y) in enumerate(staff_positions):
    w,d=11,8;x,y,a,base=residential_site(x,y,w,d,(-210,-60,-156,-36))
    building('Staff_%02d'%(i+1),x,y,w,d,1,3.2,base,a,'staff',True,True)
for name,x,y,w,d in [('Staff common west',-193,-55,10,12),('Staff common east',-185,-74,10,12),('Staff wash stores',-185,-100,18,10)]:
    x,y,a,base=residential_site(x,y,w,d,(-220,-60,-156,-36));building(name,x,y,w,d,1,3.6,base,a,'staff',True,True)

# Village planning modules: 25 x 4-person design capacity = 100, not world population.
village_positions=[(47,-65),(70,-71),(96,-64),(122,-76),(151,-65),
 (55,-88),(82,-97),(112,-94),(140,-101),(167,-91),
 (42,-112),(69,-120),(99,-118),(126,-126),(157,-121),
 (55,-142),(83,-146),(113,-151),(142,-146),(180,-138),
 (184,-67),(205,-55),(224,-72),(213,-112),(230,-141)]
for i,(x,y) in enumerate(village_positions):
    w,d=11,9;x,y,a,base=residential_site(x,y,w,d,(20,250,-156,-40))
    building('Village_%02d'%(i+1),x,y,w,d,1,3.2,base,a,'village',True,True)
for name,x,y,w,d in [('Village gathering A',193,-92,11,14),('Village gathering B',213,-92,11,14),('Fishing workshop A',206,-145,12,12),('Fishing workshop B',230,-145,12,12),('Village stores A',24,-138,9,12),('Village stores B',25,-114,9,12)]:
    x,y,a,base=residential_site(x,y,w,d,(10,260,-156,-35));building(name,x,y,w,d,1,3.5,base,a,'village',True,True)

# Mountain monastery: distinct terraces around two irregular courts near the 110m contour.
MON=[('M common hall',-62,227,30,14,-.20,2),('M lodging west',-94,218,26,12,-.36,2),('M lodging east',-22,230,24,12,.18,2),('M work stores',-80,252,22,12,-.12,2),('M upper court',-38,255,26,14,.25,2)]
for name,x,y,w,d,a,f in MON:building(name,x,y,w,d,f,3.7,dem(x,y)+.8,a,'monastery',False,True)

# Terrain-level routes: separate colour + explicit unresolved vertical bridge interfaces.
def route(name,pts,width,mat):
    for a,b in zip(pts,pts[1:]):
        length=math.dist(a,b);n=max(1,int(length/3))
        for i in range(n):
            t0=i/n;t1=(i+1)/n;ang=math.atan2(b[1]-a[1],b[0]-a[0]);nx=-math.sin(ang)*width/2;ny=math.cos(ang)*width/2
            vs=[]
            for t,sign in [(t0,-1),(t1,-1),(t1,1),(t0,1)]:
                x=a[0]*(1-t)+b[0]*t+sign*nx;y=a[1]*(1-t)+b[1]*t+sign*ny;vs.append((x,y,dem(x,y)+.18))
            mesh(vs,[(0,1,2,3)],mat,'routes')
route('Freight',[(251,-157),(228,-54),(159,-28),(50,-27),(-42,-24),(-100,12),(-107,45)],4.5,'cargo')
route('Residential',[(-190,-144),(-153,-113),(-113,-84),(-72,-49),(-26,-31),(14,12),(30,35)],2.2,'road')
route('Guest ceremonial approach',[(30,35),(83,42),(91,80),(64,104),mainpoint(30,102)],2.6,'road')
route('Village footway',[(42,-160),(88,-138),(126,-111),(162,-82),(164,-41),(72,4),(37,35)],2.0,'road')
route('Research',[(-127,38),(-94,50),(-65,70),(-35,105),(36,108),(113,75),(153,54),(171,27)],2.4,'research')
route('Monastery supply',[(-107,45),(-148,80),(-150,130),(-121,161),(-83,177),(-25,193),(-8,209),(-49,227)],3.0,'cargo')

# Bridges only where their interfaces are physically declared; riser towers show unresolved grade changes.
BRIDGES=[]
for name,a,b in [('R1',(-8,65),(-52,70)),('R2',(68,65),(100,75)),('R3',(-8,84),(-114,38)),('R4',(56,84),(158,27))]:
    a=mainpoint(*a)
    lab=next(r for r in RECORDS if r['id']==name);z0=MAIN_BASE+5.8;z1=lab['finished_floor_m']+4.2
    bridge_z=max(z0,max(dem(a[0]*(1-t)+b[0]*t,a[1]*(1-t)+b[1]*t) for t in np.linspace(0,1,30))+1.5)
    length=math.dist(a,b);ang=math.atan2(b[1]-a[1],b[0]-a[0]);mx=(a[0]+b[0])/2;my=(a[1]+b[1])/2
    # level main-side gallery, with a separate stair tower at pavilion interface.
    box(mx,my,bridge_z-.25,length,2.5,.25,'floor','bridges',ang)
    for side in [-1,1]:
        xx,yy=transform(0,side*1.25,mx,my,ang);wall_segment(xx,yy,bridge_z,length,2.8,ang,'bridges',windows=True)
    roof(mx,my,bridge_z+2.8,length+1,3.4,.7,'roof','bridges_roof',ang)
    if bridge_z-z0>.2:
        building(name+'_main_interface',a[0],a[1],5,7,1,bridge_z-z0+3.2,z0,ang,'interface',False,False)
        stairs(a[0],a[1],z0,bridge_z-z0,2.8,6.0,ang,'interfaces_stairs')
    # Explicit tower volume; no assertion of solved accessible/transport connection.
    towerbase=min(bridge_z,z1);building(name+'_interface',b[0],b[1],5,7,1,abs(z1-bridge_z)+3.2,towerbase,ang,'interface',False,False)
    if abs(z1-bridge_z)>.2:stairs(b[0],b[1],towerbase,abs(z1-bridge_z),2.8,5.8,ang,'interfaces_stairs')
    BRIDGES.append({'pavilion':name,'main_2F_z':z0,'bridge_z':bridge_z,'pavilion_2F_z':z1,'height_difference_m':z1-z0,'status':'separate stair interfaces avoid terrain intersection; cargo transport and egress unverified'})

# Record local earthworks, retain raw DEM as separate source mesh. No island-wide flattening.
def altered(x,y):
    raw=dem(x,y);out=raw
    for cx,cy,w,d,base,a,name in PADS:
        dx=x-cx;dy=y-cy;u=dx*math.cos(a)+dy*math.sin(a);v=-dx*math.sin(a)+dy*math.cos(a)
        gap=max(abs(u)-w/2,abs(v)-d/2)
        if gap<=0:return base
        if gap<1.5:out=out*(gap/1.5)+base*(1-gap/1.5)
    return out
def terrainmesh(bounds,step,group,modified=False):
    xmin,xmax,ymin,ymax=bounds;xs=np.arange(xmin,xmax+step/2,step);ys=np.arange(ymin,ymax+step/2,step)
    v=[];f=[]
    for y in ys:
        for x in xs:v.append((float(x),float(y),altered(x,y) if modified else dem(x,y)))
    n=len(xs)
    for j in range(len(ys)-1):
        for i in range(n-1):
            q=j*n+i
            # Marine zero cells are not a fabricated flat extension of the island.
            if max(v[k][2] for k in [q,q+1,q+n,q+n+1])<=.5:continue
            f.extend([(q,q+1,q+n+1),(q,q+n+1,q+n)])
    mesh(v,f,'terrain',group)
terrainmesh((-1150,1100,-260,3100),18,'terrain_island')
terrainmesh((-235,270,-195,305),1.5,'terrain_local',True)
terrainmesh((-235,270,-195,305),4,'terrain_original',False)
box(0,1000,-2,4000,6500,1,'water','ocean')

# Consolidate by functional layer/material for fast render and inspectable GLB.
for (group,mat),(verts,faces) in DATA.items():
    if not verts:continue
    me=bpy.data.meshes.new(group+'_'+mat);me.from_pydata(verts,[],faces);me.materials.append(MATS[mat]);me.update()
    ob=bpy.data.objects.new(group+'_'+mat,me);bpy.context.scene.collection.objects.link(ob);ob['layer']=group
font=bpy.data.fonts.load('C:/Windows/Fonts/simhei.ttf')
for text,x,y,z,size,group in LABELS:
    cu=bpy.data.curves.new(text,'FONT');cu.body=text;cu.font=font;cu.size=size;cu.align_x='CENTER';cu.align_y='CENTER';cu.extrude=.005
    ob=bpy.data.objects.new(text,cu);bpy.context.scene.collection.objects.link(ob);ob.location=(x,y,z);ob['layer']=group;cu.materials.append(MATS['label'])

scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.world.color=(.7,.76,.82)
scene.render.film_transparent=False
scene.view_settings.view_transform='AgX'
bpy.ops.object.light_add(type='SUN',location=(-100,-100,250));sun=bpy.context.object;sun.rotation_euler=(.35,-.45,-.45);sun.data.energy=2.7;sun.data.angle=.14
bpy.ops.object.light_add(type='AREA',location=(0,-200,200));bpy.context.object.data.energy=90000;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=250
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.clip_end=12000
def visible(layer):
    for ob in scene.objects:
        if ob.type in ['CAMERA','LIGHT']:continue
        gr=ob.get('layer','clock')
        hide=not layer(gr)
        ob.hide_render=hide;ob.hide_set(hide)
def camera(pos,target,scale):cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
def render(name,pos,target,scale,filter=None):
    visible(filter or (lambda g:g!='terrain_original' and g!='terrain_island' and not ('labels' in g or g.startswith('B'))))
    camera(pos,target,scale);scene.render.filepath=str(OUT/name);bpy.ops.render.render(write_still=True)

earth=[]
for cx,cy,w,d,z,a,name in PADS:
    gs=[dem(*transform(xx,yy,cx,cy,a)) for xx in np.linspace(-w/2,w/2,7) for yy in np.linspace(-d/2,d/2,7)]
    earth.append({'pad':name,'platform_z_m':z,'raw_min_m':min(gs),'raw_max_m':max(gs),'max_cut_m':max(0,max(gs)-z),'max_fill_m':max(0,z-min(gs))})
meta={'title':'Astra independent terrain-following campus study','date':'2026-10-24','transform':{'horizontal_scale':.5,'vertical_scale':1,'origin_lon_lat':[LON,LAT],'formula':'x=.5*(lon-lon0)*111320*cos(lat0); y=.5*(lat-lat0)*111320; z=raw Terrarium DEM'},'terrain_source':'../terrain-source/manifest.json','capacity':{'students':16,'staff_places':64,'guest_places':16,'village_design_places':100,'status':'scenario A geometric allocation; not established world population','unplaced_higher_scenario':{'additional_staff':32,'additional_guests':16,'additional_village':50},'monastery_population':'not assigned'},'buildings':RECORDS,'earthworks':earth,'bridge_interfaces':BRIDGES,'limitations':['Architecture/structure sketch, not engineering verification','Floor slabs at stairs are not all boolean-cut; stair footprint and flights are shown, detailed openings unresolved','Terrarium raster is not local survey; scaled terrain doubles original slope','Local terrace cuts/fills explicitly replace rendered terrain only; original DEM source retained unchanged','B1 tunnels to four labs are topology requirements, not excavated geometric corridors in this model','No source/story room IDs invented; S01-S16 are diagram indices requiring main-agent mapping','Freight gradients, foundation stability, coastal flood level and high-risk separations not verified']}
meta['main_axis']={'bearing_degrees_clockwise_from_north':math.degrees(MAIN_BEARING),'representative_caldera_dem_sample_xy':[230,2060],'representative_sample_z_m':dem(230,2060),'method':'low depression sample inside the caldera; direction representative, not surveyed caldera centroid','formal_front':'north / caldera-facing','service_side':'south / coast-facing'}
meta['main_levels_m']={'B2':40.6,'B1':44.8,'1F':49.0,'2F':54.8,'3F':60.4,'4F_local':66.0,'archive_eaves':70.8,'clock_eaves':76.8}
meta['main_floor_heights_m']=[5.8,5.6,5.6]
meta['limitations']=[s for s in meta['limitations'] if not s.startswith('Floor slabs')]
meta['limitations'].extend(['Main stairs, typical pavilion stairs, archive and clock base have explicit split-slab openings; this is still not a full building-code or escape audit','North entrance descends into a local cut forecourt; cut/fill magnitudes are reported rather than hidden','Suite A-D labels and S01-S16 are drawing indices, not new story room numbers','2F four suites have individual entrance/wet-service strip/private corridor/lounge; public circulation is outside them','Upper-stage clock stair termination and underground four-pavilion tunnels require further architectural detailing'])
meta['area_by_type_m2']={}
for r in RECORDS:meta['area_by_type_m2'][r['type']]=meta['area_by_type_m2'].get(r['type'],0)+r['gross_floor_area_m2']
(H/'model_manifest.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
render('01_island.png',(2200,-2900,2300),(0,1050,240),3700,lambda g:g not in ['terrain_original','terrain_local'] and not ('labels' in g or g.startswith('B')))
render('02_campus_birdseye.png',(430,-510,450),(15,60,55),570)
render('03_main_exterior.png',(105,160,190),(28,67,58),165)
render('04_main_2F_cutaway.png',(75,-65,190),(28,65,57),103,lambda g:g in ['main_L2','main_plan_labels','main_core'])
render('05_monastery.png',(115,75,250),(-57,237,115),165)
render('06_village_staff.png',(250,-430,240),(10,-95,20),410)
render('07_original_terrain.png',(430,-510,450),(15,60,55),570,lambda g:g in ['terrain_original','ocean'])
render('08_main_ground_cutaway.png',(85,-65,170),(28,66,50),110,lambda g:g in ['main_L1','main_L1_labels','courtyard','entrance_stairs','main_portico'])
render('09_main_3F_cutaway.png',(75,-65,190),(28,65,62),103,lambda g:g in ['main_L3','main_L3_labels'])
render('10_basement_diagram.png',(65,-65,155),(28,66,45),108,lambda g:g.startswith('B'))
visible(lambda g:g!='terrain_original' and g!='terrain_island' and not ('labels' in g or g.startswith('B')))
camera((430,-510,450),(15,60,55),570)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(H/'astra_campus.blend'))
bpy.ops.object.select_all(action='DESELECT')
for ob in scene.objects:
    if ob.type=='MESH' and not ob.hide_get():ob.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(H/'astra_campus.glb'),export_format='GLB',use_selection=True,export_yup=True)
print('ASTRA_DONE',len(RECORDS),'buildings',len(earth),'local pads')
