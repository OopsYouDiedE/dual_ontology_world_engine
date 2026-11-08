"""Independent low-poly industrial components, derived from approved concepts.

Local coordinates are Blender Z-up with the display front toward -Y. Every
factory returns an Empty root; solids, pipe elbows, grilles and dial markings
are actual meshes. Materials stay in the warm wood/stone/brass family.
"""
import math
import bpy
from mathutils import Vector
from . import core as C, paths as P, lighting as L, special_props as S


def material_set(mats):
    result=dict(mats)
    colors={
      'iron':('#51483B',.65,.25,0), 'iron_dark':('#302A24',.7,.15,0),
      'copper':('#AA7643',.4,.55,0), 'brick_red':('#8B4936',.6,.05,0),
      'masonry':('#B39A7D',.86,0,0), 'masonry_light':('#C7AF8D',.83,0,0),
      'masonry_dark':('#8D765E',.9,0,0), 'sack':('#CEB98D',.88,0,0),
    }
    for key,args in colors.items():
        name='LP_Industrial_Warm_'+key
        result[key]=bpy.data.materials.get(name) or C.solid_material(name,*args)
    return result


def _cylinder_between(name,a,b,radius,mat,parent,sides=8):
    a,b=Vector(a),Vector(b);delta=b-a
    ob=C.cylinder(name,(a+b)*.5,radius,delta.length,mat,sides,parent)
    ob.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    return ob


def ring(name,outer,inner,depth,mat,parent=None,sides=12):
    """Closed annular prism around local Z; its hole remains genuinely open."""
    if not 0<inner<outer or depth<=0:raise ValueError('Invalid annular dimensions')
    vs=[]
    for radius,z in ((outer,-depth/2),(outer,depth/2),(inner,-depth/2),(inner,depth/2)):
        vs.extend((radius*math.cos(2*math.pi*i/sides),radius*math.sin(2*math.pi*i/sides),z) for i in range(sides))
    fs=[]
    for i in range(sides):
        j=(i+1)%sides
        fs.extend(((i,j,sides+j,sides+i),(2*sides+j,2*sides+i,3*sides+i,3*sides+j),
                   (i,2*sides+i,2*sides+j,j),(sides+j,3*sides+j,3*sides+i,sides+i)))
    return C.mesh(name,vs,fs,mat,parent)


def flange(name,mats,radius=.12,parent=None,bolts=6):
    m=material_set(mats);root=C.group(name,parent=parent)
    ring(name+'_Flange',radius*1.65,radius*.93,radius*.52,m['gold'],root,12)
    for i in range(bolts):
        a=2*math.pi*i/bolts
        C.cylinder(name+'_Bolt_%02d'%i,(radius*1.3*math.cos(a),radius*1.3*math.sin(a),radius*.32),radius*.12,radius*.22,m['iron'],6,root)
    return root


def _rounded_nodes(nodes,radius):
    if any(isinstance(p,dict) for p in nodes):return P.sample_path(nodes,curve_segments=5)
    pts=[Vector(p) for p in P.clean_polyline(nodes)]
    out=[tuple(pts[0])]
    for a,b,c in zip(pts,pts[1:],pts[2:]):
        incoming,outgoing=(b-a).normalized(),(c-b).normalized()
        if incoming.dot(outgoing)>.995:
            out.append(tuple(b));continue
        d=min(radius,(b-a).length*.35,(c-b).length*.35)
        q0,q3=b-incoming*d,b+outgoing*d
        out.extend(P.sample_cubic_bezier(q0,q0+incoming*d*.552,q3-outgoing*d*.552,q3,5))
    out.append(tuple(pts[-1]));return P.clean_polyline(out)


def pipe_run(name,mats,nodes,radius=.09,sides=8,collars=True,parent=None):
    """Sweep an octagonal section along straight nodes or sampled Bezier nodes.

    Plain sharp corners get a five-span cubic elbow. A transported section
    frame avoids vertical-segment twist; terminal flanges are separate parts.
    """
    m=material_set(mats);root=C.group(name,parent=parent)
    pts=[Vector(p) for p in _rounded_nodes(nodes,radius*2.8)]
    vs=[];normal=None
    for i,p in enumerate(pts):
        tangent=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized()
        if normal is None:
            ref=Vector((0,0,1)) if abs(tangent.z)<.9 else Vector((1,0,0))
            normal=tangent.cross(ref).normalized()
        else:
            projected=normal-tangent*normal.dot(tangent)
            if projected.length<1e-6:projected=tangent.cross(Vector((0,1,0)))
            normal=projected.normalized()
        bitangent=tangent.cross(normal).normalized()
        for j in range(sides):
            a=2*math.pi*j/sides
            vs.append(tuple(p+radius*(normal*math.cos(a)+bitangent*math.sin(a))))
    fs=[tuple(reversed(range(sides)))]
    for i in range(len(pts)-1):
        for j in range(sides):
            k=(j+1)%sides;fs.append((i*sides+j,i*sides+k,(i+1)*sides+k,(i+1)*sides+j))
    fs.append(tuple(range((len(pts)-1)*sides,len(pts)*sides)))
    C.mesh(name+'_Swept_pipe',vs,fs,m['copper'],root)
    if collars:
        for i,(pos,axis) in enumerate(((pts[0],pts[1]-pts[0]),(pts[-1],pts[-1]-pts[-2]))):
            f=flange(name+'_End_%d'%i,m,radius,parent=root)
            f.location=pos;f.rotation_euler=axis.to_track_quat('Z','Y').to_euler()
    root['generator']='sampled_octagonal_pipe_v1';root['pipe_radius']=radius
    return root


def gauge(name,mats,radius=.2,needle_degrees=42,parent=None):
    """A real faceted round dial, with solid tick marks and pointer; faces -Y."""
    m=material_set(mats);root=C.group(name,parent=parent)
    rim=C.cylinder(name+'_Housing',(0,0,0),radius,.10,m['gold'],12,root);rim.rotation_euler[0]=math.pi/2
    face=C.cylinder(name+'_Cream_face',(0,-.058,0),radius*.86,.018,m['cream'],12,root);face.rotation_euler[0]=math.pi/2
    for i in range(11):
        a=math.radians(-140+i*28)
        p=(radius*.66*math.cos(a),-.073,radius*.66*math.sin(a))
        q=(radius*.79*math.cos(a),-.073,radius*.79*math.sin(a))
        C.beam(name+'_Tick_%02d'%i,p,q,radius*.035,.009,m['iron_dark'],root,.001)
    a=math.radians(needle_degrees)
    C.beam(name+'_Pointer',(-radius*.17*math.cos(a),-.088,-radius*.17*math.sin(a)),
           (radius*.65*math.cos(a),-.088,radius*.65*math.sin(a)),radius*.055,.012,m['iron_dark'],root,.002)
    hub=C.cylinder(name+'_Hub',(0,-.09,0),radius*.10,.018,m['gold'],8,root);hub.rotation_euler[0]=math.pi/2
    return root


def interlock_handle(name,mats,radius=.23,parent=None):
    """Wall-mounted spoked wheel, display front -Y; visual candidate only."""
    m=material_set(mats);root=C.group(name,parent=parent)
    C.box(name+'_Mount',(0,0,0),(radius*1.45,.08,radius*1.85),m['iron'],.015,root)
    _cylinder_between(name+'_Spindle',(0,-.04,0),(0,-.20,0),radius*.15,m['gold'],root)
    wheel=C.group(name+'_Wheel',(0,-.21,0),parent=root);wheel.rotation_euler[0]=math.pi/2
    ring(name+'_Rim',radius,radius*.81,radius*.17,m['brick_red'],wheel,12)
    C.cylinder(name+'_Hub',(0,0,0),radius*.24,radius*.24,m['gold'],8,wheel)
    for i in range(5):
        a=i*2*math.pi/5
        C.beam(name+'_Spoke_%d'%i,(0,0,0),(radius*.86*math.cos(a),radius*.86*math.sin(a),0),radius*.1,radius*.1,m['brick_red'],wheel,.003)
    C.box(name+'_Stop',(radius*.48,-.10,-radius*.56),(radius*.22,.14,radius*.23),m['gold'],.004,root)
    root['visual_control_part']=wheel.name;root['binding_status']='unbound_candidate'
    return root


def valve_station(name,mats,width=1.25,height=1.35,parent=None):
    m=material_set(mats);root=C.group(name,parent=parent);s=height/1.35
    C.box(name+'_Plinth',(0,0,.12*s),(width,.72*s,.24*s),m['masonry_light'],.025,root)
    C.box(name+'_Pump_foot',(.28*s,0,.32*s),(.45*s,.48*s,.17*s),m['iron'],.025,root)
    _cylinder_between(name+'_Main_body',(-width*.44,0,.60*s),(width*.44,0,.60*s),.20*s,m['iron'],root,8)
    for x in (-width*.37,width*.37):
        f=flange(name+'_Pipe_flange',m,.19*s,root);f.location=(x,0,.60*s);f.rotation_euler[1]=math.pi/2
    C.cylinder(name+'_Valve_bell',(-.12*s,0,.75*s),.22*s,.37*s,m['iron'],8,root)
    C.cylinder(name+'_Valve_collar',(-.12*s,0,.97*s),.16*s,.10*s,m['gold'],8,root)
    C.cylinder(name+'_Spindle',(-.12*s,0,1.10*s),.055*s,.24*s,m['gold'],8,root)
    wheel=C.group(name+'_Wheel',(-.12*s,0,1.24*s),parent=root)
    ring(name+'_Wheel_rim',.34*s,.285*s,.055*s,m['brick_red'],wheel,12)
    C.cylinder(name+'_Wheel_hub',(0,0,0),.09*s,.075*s,m['gold'],8,wheel)
    for j in range(5):
        a=2*math.pi*j/5
        C.beam(name+'_Wheel_spoke_%d'%j,(0,0,0),(.29*s*math.cos(a),.29*s*math.sin(a),0),.032*s,.03*s,m['brick_red'],wheel,.003)
    g=gauge(name+'_Pressure',m,.17*s,35,parent=root);g.location=(.44*s,-.06*s,1.26*s)
    _cylinder_between(name+'_Gauge_stem',(.44*s,0,.65*s),(.44*s,0,1.18*s),.028*s,m['gold'],root)
    # A modeled mechanical stop belongs to the visual valve assembly.
    C.box(name+'_Mechanical_stop',(-.12*s,.14*s,1.01*s),(.21*s,.08*s,.09*s),m['iron_dark'],.01,root)
    root['visual_control_part']=wheel.name;root['binding_status']='unbound_candidate'
    return root


def control_console(name,mats,width=2.3,depth=.95,height=1.35,parent=None):
    m=material_set(mats);root=C.group(name,parent=parent)
    bottom=.12;front=height*.56;back=height;d=depth/2;w=width/2
    vs=[(-w,-d,bottom),(w,-d,bottom),(w,d,bottom),(-w,d,bottom),
        (-w,-d,front),(w,-d,front),(w,d,back),(-w,d,back)]
    C.mesh(name+'_Closed_cabinet',vs,[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],m['iron'],root,.018)
    C.box(name+'_Foot',(0,0,.08),(width+.12,depth+.08,.16),m['iron_dark'],.015,root)
    for x in (-width*.25,width*.25):
        C.box(name+'_Service_panel',(x,-d-.016,front*.48),(width*.44,.035,front*.65),m['wood'],.008,root)
        for xx in (-width*.18,width*.18):
            for z in (front*.22,front*.72):
                bolt=C.cylinder(name+'_Panel_bolt',(x+xx,-d-.045,z),.023,.02,m['gold'],6,root);bolt.rotation_euler[0]=math.pi/2
    slope=(back-front)/depth
    def top(y):return front+(y+d)*slope
    perimeter=[(-w,-d,front+.025),(w,-d,front+.025),(w,d,back+.025),(-w,d,back+.025)]
    for a,b in zip(perimeter,perimeter[1:]+perimeter[:1]):C.beam(name+'_Deck_edge',a,b,.035,.035,m['gold'],root,.004)
    for i in range(3):
        x=(i-1)*width*.27;y=.14*depth
        g=gauge(name+'_Gauge_%d'%i,m,width*.083,25+i*35,root)
        g.location=(x,y,top(y)+.04);g.rotation_euler[0]=-math.atan2(1,slope)
        base=C.box(name+'_Lever_base',(x,-depth*.27,top(-depth*.27)+.02),(.13,.15,.045),m['gold'],.005,root)
        base.rotation_euler[0]=math.atan(slope)
        a=(x,-depth*.27,top(-depth*.27)+.05);b=(x,-depth*.37,top(-depth*.27)+.24)
        C.beam(name+'_Lever_%d'%i,a,b,.033,.033,m['gold'],root,.004)
        C.ico(name+'_Lever_knob_%d'%i,b,.055,m['wood_light'],1,root)
    for i,mat in enumerate((m['brick_red'],m['book_green'])):
        C.ico(name+'_Indicator_%d'%i,(-width*.40,-depth*.07,top(-depth*.07)+.055+i*.09),.045,mat,1,root)
    return root


def reaction_vessel(name,mats,radius=.72,height=2.7,parent=None):
    m=material_set(mats);root=C.group(name,parent=parent);r,h=radius,height
    profile=[(0,.2),(r*.65,.2),(r*.9,.45),(r,.64),(r,h*.71),(r*.83,h*.82),(r*.32,h*.90),(r*.22,h*.91),(0,h*.91)]
    C.lathe(name+'_Faceted_vessel',profile,10,m['iron'],root)
    for z,rr in ((.58,r*1.03),(h*.46,r*1.04),(h*.72,r*1.02)):
        band=ring(name+'_Band',rr,rr*.94,.10,m['gold'],root,10);band.location.z=z
        for i in range(10):
            a=2*math.pi*i/10+math.pi/8
            p=Vector((rr*math.cos(a),rr*math.sin(a),z))
            _cylinder_between(name+'_Band_bolt',p,p+Vector((math.cos(a),math.sin(a),0))*.04,.03,m['iron_dark'],root,6)
    for x,y in ((-.52,-.40),(.52,-.40),(-.52,.40),(.52,.40)):
        C.beam(name+'_Leg',(x,y,.1),(x*.80,y*.80,.72),.16,.16,m['iron'],root,.01)
        C.box(name+'_Leg_foot',(x,y,.08),(.28,.28,.16),m['iron_dark'],.015,root)
    C.cylinder(name+'_Chimney',(0,0,h*.98),r*.20,h*.20,m['iron_dark'],8,root)
    C.cylinder(name+'_Chimney_cap',(0,0,h*1.085),r*.28,.08,m['gold'],8,root)
    C.box(name+'_Inspection_frame',(0,-r*.98,h*.46),(r*.83,.09,h*.24),m['gold'],.03,root)
    glow=C.box(name+'_Inspection_glow',(0,-r*1.05,h*.46),(r*.66,.025,h*.19),m['emission'],.015,root)
    if hasattr(glow,'visible_shadow'):glow.visible_shadow=False
    for x in (-r*.21,0,r*.21):
        C.box(name+'_Inspection_bar',(x,-r*1.08,h*.46),(.035,.035,h*.21),m['iron'],.003,root)
    C.box(name+'_Inspection_cross',(0,-r*1.08,h*.46),(r*.72,.035,.035),m['iron'],.003,root)
    L._light(name+'_Inspection_practical',root,(0,-r*1.12,h*.46),energy=8,size=.15)
    return root


def caged_lamp(name,mats,height=.5,light_energy=22,parent=None):
    m=material_set(mats);root=C.group(name,parent=parent);s=height/.5
    C.box(name+'_Backplate',(0,.025,0),(.13*s,.06*s,.34*s),m['iron'],.015*s,root)
    C.beam(name+'_Bracket',(0,0,.15*s),(0,-.20*s,.15*s),.04*s,.04*s,m['gold'],root,.004)
    C.lathe(name+'_Cap',[(0,.18*s),(.14*s,.18*s),(.14*s,.23*s),(.06*s,.28*s),(0,.28*s)],8,m['gold'],root,(0,-.2*s,0))
    core=C.cylinder(name+'_Glow',(0,-.2*s,-.025*s),.10*s,.34*s,m['emission'],8,root)
    if hasattr(core,'visible_shadow'):core.visible_shadow=False
    for j in range(6):
        a=j*2*math.pi/6;x=.125*s*math.cos(a);y=-.2*s+.125*s*math.sin(a)
        C.beam(name+'_Cage_%d'%j,(x,y,-.22*s),(x,y,.17*s),.018*s,.018*s,m['gold'],root,.002)
    for z in (-.22*s,-.05*s,.16*s):
        band=ring(name+'_Cage_band',.14*s,.12*s,.022*s,m['gold'],root,8);band.location=(0,-.2*s,z)
    C.cylinder(name+'_Base',(0,-.2*s,-.24*s),.14*s,.045*s,m['gold'],8,root)
    L._light(name+'_Light',root,(0,-.24*s,-.02*s),energy=light_energy,size=.11*s)
    return root


def indicator_board(name,mats,width=.4,height=1.25,kind='flood',parent=None):
    m=material_set(mats);root=C.group(name,parent=parent)
    C.box(name+'_Frame',(0,0,height/2),(width,.08,height),m['gold'],.015,root)
    C.box(name+'_Face',(0,-.05,height/2),(width*.79,.025,height*.90),m['cream'],.006,root)
    colors=(m['book_green'],m['gold'],m['brick_red'])
    for i in range(3):
        C.box(name+'_Level_%d'%i,(0,-.069,height*(.19+i*.30)),(width*.58,.018,height*.26),colors[i],.002,root)
    for i in range(9):
        C.box(name+'_Graduation_%02d'%i,(-width*.18,-.085,height*(.08+.1*i)),(width*(.20 if i%3==0 else .10),.009,.014),m['iron_dark'],0,root)
    if kind=='warning':
        tri=C.polyprism(name+'_Warning_triangle',[(-width*.28,-height*.12),(width*.28,-height*.12),(0,height*.13)],0,.02,m['gold'],root)
        tri.rotation_euler[0]=math.pi/2;tri.location=(0,-.095,height*.56)
        C.box(name+'_Exclamation',(0,-.125,height*.57),(.028,.015,height*.14),m['iron_dark'],.001,root)
        C.ico(name+'_Dot',(0,-.125,height*.46),.022,m['iron_dark'],1,root)
    root['visual_indicator_only']=True
    return root


def schematic(name,mats,width=2.2,height=1.05,parent=None):
    m=material_set(mats);root=C.group(name,parent=parent)
    C.box(name+'_Board',(0,0,0),(width,.07,height),m['cream'],.006,root)
    for x in (-width/2,width/2):C.box(name+'_Side',(x,-.045,0),(.065,.06,height+.07),m['wood'],.005,root)
    for z in (-height/2,height/2):C.box(name+'_Edge',(0,-.045,z),(width+.07,.06,.065),m['wood'],.005,root)
    points=[(-.32*width,-.085,-.24*height),(-.32*width,-.085,.24*height),(-.10*width,-.085,.24*height),
            (-.10*width,-.085,.12*height),(.34*width,-.085,.12*height)]
    for a,b in zip(points,points[1:]):C.beam(name+'_Printed_pipe',a,b,.022,.012,m['wood'],root,.001)
    for x in (.04*width,.20*width,.34*width):
        C.beam(name+'_Branch',(x,-.085,.12*height),(x,-.085,-.20*height),.02,.012,m['wood'],root,.001)
        r=ring(name+'_Symbol',height*.075,height*.056,.012,m['wood'],root,10);r.rotation_euler[0]=math.pi/2;r.location=(x,-.09,-.27*height)
    return root


def pallet(name,mats,width=1.5,depth=1.1,parent=None):
    root=C.group(name,parent=parent)
    for x in (-width*.37,0,width*.37):C.box(name+'_Runner',(x,0,.08),(.12,depth,.16),mats['wood_dark'],.008,root)
    n=max(4,math.ceil(depth/.16))
    for i in range(n):C.box(name+'_Slat_%02d'%i,(0,-depth/2+(i+.5)*depth/n,.19),(width,depth/n-.025,.08),mats['wood'],.007,root)
    return root


def storage_rack(name,mats,width=2.6,depth=.85,height=2.5,parent=None):
    root=C.group(name,parent=parent);m=material_set(mats)
    for x in (-width/2,width/2):
        for y in (-depth/2,depth/2):C.box(name+'_Post',(x,y,height/2),(.12,.12,height),m['wood'],.012,root)
        C.beam(name+'_Brace',(x,-depth/2,.22),(x,depth/2,height-.18),.08,.08,m['wood_dark'],root,.006)
    for z in (.22,height*.53):
        C.box(name+'_Shelf',(0,0,z),(width+.12,depth+.08,.11),m['wood_light'],.01,root)
        C.box(name+'_Front_rail',(0,-depth/2-.02,z-.07),(width+.12,.08,.16),m['wood'],.007,root)
        count=max(2,int(width/.55))
        for i in range(count):
            bag=S.sacks(name+'_Grain_%d_%d'%(int(z*100),i),m,count=1,parent=root)
            bag.location=(-width*.40+i*(width*.8/max(1,count-1))+.24,-.1,z+.06)
    for x in (-width/2,width/2):
        for z in (.22,height*.53):
            cap=C.cylinder(name+'_Fixing',(x,-depth/2-.07,z),.035,.02,m['gold'],6,root);cap.rotation_euler[0]=math.pi/2
    return root


def barrel(name,mats,radius=.4,height=.95,parent=None):
    root=C.group(name,parent=parent);m=material_set(mats)
    C.lathe(name+'_Staves',[(radius*.82,0),(radius,.18*height),(radius*1.05,height*.5),(radius,height*.82),(radius*.82,height)],10,m['wood_light'],root)
    for z in (height*.15,height*.79):
        band=ring(name+'_Hoop',radius*1.045,radius*.98,.07,m['gold'],root,10);band.location.z=z
    C.cylinder(name+'_Lid',(0,0,height+.01),radius*.83,.035,m['wood'],10,root)
    for y in (-radius*.38,0,radius*.38):
        x=math.sqrt((radius*.8)**2-y*y)
        C.box(name+'_Lid_joint',(0,y,height+.03),(x*2,.012,.008),m['wood_dark'],0,root)
    return root


def handcart(name,mats,width=1.1,length=1.6,parent=None):
    root=C.group(name,parent=parent);m=material_set(mats)
    C.box(name+'_Bed',(0,0,.48),(width,length,.12),m['wood'],.015,root)
    for x in (-width/2,width/2):
        C.box(name+'_Side',(x,0,.71),(.065,length,.38),m['wood_light'],.008,root)
        for y in (-length*.42,length*.42):C.box(name+'_Stake',(x,y,.77),(.09,.09,.65),m['wood'],.008,root)
        C.beam(name+'_Handle',(x,-length*.34,.45),(x,-length*1.08,.19),.07,.07,m['wood'],root,.008)
        wheel=C.group(name+'_Wheel',(x+math.copysign(.10,x),.12,.34),parent=root)
        wheel.rotation_euler[1]=math.pi/2
        ring(name+'_Rim',.34,.28,.08,m['wood'],wheel,12)
        C.cylinder(name+'_Hub',(0,0,0),.09,.13,m['gold'],8,wheel)
        for j in range(8):
            a=j*math.pi/4
            C.beam(name+'_Spoke',(0,0,0),(.29*math.cos(a),.29*math.sin(a),0),.04,.045,m['wood_light'],wheel,.003)
    for y in (-.35,.20):
        sacks=S.sacks(name+'_Cargo',m,count=2,parent=root);sacks.location=(0,y,.54)
    return root


def platform_scale(name,mats,width=1.4,depth=1.1,parent=None):
    root=C.group(name,parent=parent);pallet(name+'_Deck',mats,width,depth,root)
    x,y=-width*.38,depth*.29
    C.box(name+'_Column',(x,y,.87),(.14,.14,1.45),mats['wood'],.01,root)
    C.box(name+'_Balance',(x+.28,y,1.61),(.84,.09,.11),mats['gold'],.008,root)
    pan=C.cylinder(name+'_Pan',(x+.59,y,1.05),.23,.035,mats['gold'],8,root)
    for a in (0,2*math.pi/3,4*math.pi/3):
        C.beam(name+'_Pan_hanger',(x+.59,y,1.57),(x+.59+.2*math.cos(a),y+.2*math.sin(a),1.06),.012,.012,mats['gold'],root,.001)
    return root


def drain_grate(name,mats,width=.32,length=4,parent=None):
    root=C.group(name,parent=parent);m=material_set(mats)
    C.box(name+'_Dark_trough',(0,0,-.025),(width,length,.05),m['iron_dark'],0,root)
    for x in (-width/2,width/2):C.box(name+'_Frame',(x,0,.012),(.035,length,.035),m['gold'],.003,root)
    for i in range(max(1,math.ceil(length/.11))):
        y=-length/2+(i+.5)*length/math.ceil(length/.11)
        C.box(name+'_Crossbar_%03d'%i,(0,y,.016),(width,.032,.04),m['iron'],.002,root)
    return root


def simple_rail(name,mats,points,height=.9,spacing=.8,parent=None,post_points=None):
    root=C.group(name,parent=parent);m=material_set(mats);pts=P.clean_polyline(points)
    samples=[tuple(p) for p in post_points] if post_points is not None else P.resample_polyline(pts,spacing=spacing)
    for i,p in enumerate(samples):
        C.beam(name+'_Post_%02d'%i,p,(p[0],p[1],p[2]+height),.055,.055,m['iron'],root,.006)
        C.box(name+'_Foot_%02d'%i,(p[0],p[1],p[2]+.025),(.12,.12,.05),m['iron_dark'],.008,root)
    for a,b in zip(pts,pts[1:]):
        C.beam(name+'_Top',(a[0],a[1],a[2]+height),(b[0],b[1],b[2]+height),.07,.065,m['wood'],root,.008)
        C.beam(name+'_Lower',(a[0],a[1],a[2]+.13),(b[0],b[1],b[2]+.13),.035,.035,m['iron'],root,.004)
    return root


def ramp_module(name,mats,width=3.1,length=8.2,rise=1.4,landing=1.2,parent=None):
    """A closed wedge with two level landings, no stepped approximation."""
    if length<=2*landing or min(width,rise,landing)<=0:raise ValueError('Invalid ramp dimensions')
    m=material_set(mats);root=C.group(name,parent=parent)
    profile=[(0,-.14),(length,-.14),(length,rise),(length-landing,rise),(landing,0),(0,0)]
    n=len(profile);vs=[(x,y,z) for x in (-width/2,width/2) for y,z in profile]
    fs=[tuple(reversed(range(n))),tuple(range(n,n*2))]
    fs += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    C.mesh(name+'_Continuous_solid',vs,fs,m['masonry_light'],root)
    def z_at(y):return rise*max(0,min(1,(y-landing)/(length-2*landing)))
    for i in range(1,7):
        y=landing+i*(length-2*landing)/7;z=z_at(y)
        C.box(name+'_Non_slip_strip_%d'%i,(0,y,z+.009),(width-.20,.035,.018),m['iron'],.002,root)
    for x in (-width/2,width/2):
        path=[(x,0,0),(x,landing,0),(x,length-landing,rise),(x,length,rise)]
        for a,b in zip(path,path[1:]):
            C.beam(name+'_Kerb',(a[0],a[1],a[2]+.05),(b[0],b[1],b[2]+.05),.10,.10,m['masonry_dark'],root,.008)
    rail_path=[(width/2-.08,.18,0),(width/2-.08,landing,0),(width/2-.08,length-landing,rise),(width/2-.08,length-.18,rise)]
    simple_rail(name+'_Handrail',m,rail_path,.88,.9,root)
    root['generator']='continuous_ramp_v1';root['slope']=rise/(length-2*landing);root['stair_steps']=0
    return root


def heavy_lock(name,mats,width=.22,parent=None):
    root=C.group(name,parent=parent);m=material_set(mats);s=width/.22
    C.box(name+'_Body',(0,-.02,0),(.22*s,.11*s,.25*s),m['gold'],.025*s,root)
    shackle=pipe_run(name+'_Shackle',m,[(-.073*s,0,.10*s),(-.073*s,0,.26*s),(.073*s,0,.26*s),(.073*s,0,.10*s)],.023*s,8,False,root)
    key=C.cylinder(name+'_Keyhole',(0,-.082,-.01*s),.026*s,.014,m['iron_dark'],8,root);key.rotation_euler[0]=math.pi/2
    C.box(name+'_Keyslot',(0,-.09,-.045*s),(.022*s,.012,.045*s),m['iron_dark'],.002,root)
    root['visual_lock_not_authoritative_state']=True
    return root


def masonry_joints(name,mats,length,height,openings=(),brick_width=1.08,row_height=.47,parent=None):
    """One combined solid mesh for the shallow stone joints; opening-aware."""
    m=material_set(mats);root=C.group(name,parent=parent);vs=[];fs=[]
    def box_at(x0,x1,z0,z1):
        if x1-x0<.001 or z1-z0<.001:return
        start=len(vs);ys=(-.018,-.006)
        vs.extend([(x0,ys[0],z0),(x1,ys[0],z0),(x1,ys[1],z0),(x0,ys[1],z0),
                   (x0,ys[0],z1),(x1,ys[0],z1),(x1,ys[1],z1),(x0,ys[1],z1)])
        fs.extend(tuple(start+i for i in f) for f in ((3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)))
    def free_intervals(z):
        spans=[(0,length)]
        for o in openings:
            if o.get('bottom',0)-.08<=z<=o.get('bottom',0)+o['height']+.08:
                a=o['center']-o['width']/2-.12;b=o['center']+o['width']/2+.12
                new=[]
                for lo,hi in spans:
                    if b<=lo or a>=hi:new.append((lo,hi))
                    else:
                        if a>lo:new.append((lo,min(a,hi)))
                        if b<hi:new.append((max(b,lo),hi))
                spans=new
        return spans
    rows=max(1,math.ceil(height/row_height))
    for row in range(rows):
        z0=row*row_height;z1=min(height,z0+row_height)
        if row>0:
            for a,b in free_intervals(z0):box_at(a,b,z0-.006,z0+.006)
        x=(row%2)*brick_width*.5
        while x<length:
            if x>.08 and all(not(o['center']-o['width']/2-.12<x<o['center']+o['width']/2+.12 and z0<o.get('bottom',0)+o['height']+.08 and z1>o.get('bottom',0)-.08) for o in openings):
                box_at(x-.006,x+.006,z0+.006,z1-.006)
            x+=brick_width
    if vs:C.mesh(name+'_Real_joint_mesh',vs,fs,m['masonry_dark'],root)
    return root
