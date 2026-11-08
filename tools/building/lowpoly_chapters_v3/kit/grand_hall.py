"""Grand double-height foyer with a genuinely continuous U-shaped gallery.

The room is a visual candidate, independent of legacy portal coordinates.
Stair exits join the same 4m deck as every upper door. Only the open void
edges receive balustrades; no railing runs against an enclosing wall.
The generators, presentation rig and camera configuration remain separate.
"""
import copy
import json
import math
import random
from pathlib import Path

import bpy
from . import core as C, architecture as A, flooring as T, openings as O
from . import furniture as F, lighting as L, decor as D, rendering as R
from . import hall_architecture as H
from .scene_common import RoomBuilder, hide_tree
from .stair_exact import straight_flight, flight_spec


def default_layout():
    path = Path(__file__).resolve().parents[1] / 'hall_layout.json'
    return json.loads(path.read_text(encoding='utf8'))


def _layout(spec):
    result = default_layout()
    for key, value in (spec or {}).items():
        if isinstance(value, dict) and isinstance(result.get(key), dict):
            result[key].update(copy.deepcopy(value))
        else:
            result[key] = copy.deepcopy(value)
    return result


def _clip_halfplane(poly, a, b, inside=True):
    if len(poly) < 3:
        return []
    cross = lambda p: (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])
    keep = lambda v: v >= -1e-9 if inside else v <= 1e-9
    output = []
    previous = poly[-1]
    pv = cross(previous)
    for current in poly:
        cv = cross(current)
        if keep(cv) != keep(pv):
            ratio = pv / (pv-cv)
            output.append((previous[0]+ratio*(current[0]-previous[0]),
                           previous[1]+ratio*(current[1]-previous[1])))
        if keep(cv):
            output.append(current)
        previous, pv = current, cv
    clean = []
    for point in output:
        if not clean or math.dist(point, clean[-1]) > 1e-7:
            clean.append(point)
    if len(clean) > 1 and math.dist(clean[0], clean[-1]) < 1e-7:
        clean.pop()
    return clean if len(clean) >= 3 else []


def _outside_convex(poly, hole):
    """Partition polygon-minus-convex-hole into disjoint convex pieces."""
    pieces, remainder = [], poly
    for a, b in zip(hole, hole[1:]+hole[:1]):
        if not remainder:
            break
        outside = _clip_halfplane(remainder, a, b, False)
        if outside:
            pieces.append(outside)
        remainder = _clip_halfplane(remainder, a, b, True)
    return pieces


def _ring(batch, outer, inner, bottom=-.028, top=0):
    for i in range(len(outer)):
        j = (i+1) % len(outer)
        batch.prism([outer[i], outer[j], inner[j], inner[i]], bottom, top)


def _floor(name, width, depth, mats, spec, parent):
    """Batched solid mosaic with a real, flush compass inlay cutout."""
    root = C.group(name, parent=parent)
    C.box(name+'_Foundation', (0, depth/2, -.17), (width, depth, .26),
          mats['stone_dark'], .025, root)
    C.box(name+'_Grout', (0, depth/2, -.034), (width, depth, .012), mats['grout'], 0, root)
    rect = lambda inset: [(-width/2+inset, inset), (width/2-inset, inset),
                           (width/2-inset, depth-inset), (-width/2+inset, depth-inset)]
    for a, b, key in ((0,.12,'stone'),(.12,.19,'stone_dark'),(.19,.34,'stone'),
                      (.34,.40,'stone_dark'),(.40,.48,'stone')):
        batch = T.Batch()
        _ring(batch, rect(a), rect(b))
        batch.finish(name+'_Border_'+str(a), mats[key], root)
    tile = spec['tile']
    cut, grout = .14, .012
    bounds = (-width/2+.48, width/2-.48, .48, depth-.48)
    center_y, radius = spec['medallion_y'], spec['medallion_radius']
    circle = lambda r: [(r*math.cos(i*math.pi/4), center_y+r*math.sin(i*math.pi/4)) for i in range(8)]
    hole = circle(radius)
    octagon = T.inset_convex([(cut,0),(tile-cut,0),(tile,cut),(tile,tile-cut),
                              (tile-cut,tile),(cut,tile),(0,tile-cut),(0,cut)], grout/2)
    diamond = T.inset_convex([(0,-cut),(cut,0),(0,cut),(-cut,0)], grout/2)
    batches, dark = [T.Batch() for _ in range(5)], T.Batch()
    rng = random.Random(51)
    for i in range(math.floor(bounds[0]/tile)-1, math.ceil(bounds[1]/tile)+1):
        for j in range(math.floor(bounds[2]/tile)-1, math.ceil(bounds[3]/tile)+1):
            poly = T.clip_rect([(x+i*tile,y+j*tile) for x,y in octagon], bounds)
            target = batches[rng.randrange(5)]
            for piece in _outside_convex(poly, hole):
                target.prism(piece,-.028,0)
            poly = T.clip_rect([(x+i*tile,y+j*tile) for x,y in diamond], bounds)
            for piece in _outside_convex(poly, hole):
                dark.prism(piece,-.028,0)
    for i, batch in enumerate(batches):
        batch.finish(name+'_Stone_Tiles_%d'%i,mats['tile_'+str(i)],root,.002)
    dark.finish(name+'_Diamond_Tiles',mats['stone_dark'],root,.002)
    for a,b,key in ((radius,radius-.07,'stone_dark'),
                    (radius-.07,radius-.27,'stone'),
                    (radius-.27,radius-.31,'stone_dark')):
        batch=T.Batch();_ring(batch,circle(a),circle(b))
        batch.finish(name+'_Compass_Ring_'+str(a),mats[key],root)
    r=radius-.31
    inner=[];star=[]
    for i in range(16):
        angle=i*math.pi/8
        outer_r=r if i%2==0 else r*math.cos(math.pi/8)
        star_r=r*.84 if i%2==0 else r*.26
        inner.append((outer_r*math.cos(angle),center_y+outer_r*math.sin(angle)))
        star.append((star_r*math.cos(angle),center_y+star_r*math.sin(angle)))
    fields=[T.Batch(),T.Batch()];tips=[T.Batch(),T.Batch()]
    for i in range(16):
        j=(i+1)%16
        fields[(i//2)%2].prism([star[i],inner[i],inner[j],star[j]],-.028,0)
        tips[i%2].prism([(0,center_y),star[i],star[j]],-.028,0)
    for batch,key,suffix in ((fields[0],'cream','Field_A'),(fields[1],'stone','Field_B'),
                             (tips[0],'compass_dark','Star_A'),(tips[1],'stone_dark','Star_B')):
        batch.finish(name+'_Compass_'+suffix,mats[key],root)
    root['finished_floor_z']=0.0
    root['compass_inlay']='Tiles clipped against an octagon; all compass faces finish at z=0.'
    return root


def _stone_materials(mats):
    result=dict(mats)
    result.update(wood=mats['stone'],wood_dark=mats['stone_dark'],
                  wood_light=mats['cream'],wall=mats['stone'])
    return result


def _gallery(name,bounds,z,mats,parent):
    x0,x1,y0,y1=bounds
    floor=T.tiled_floor(name+'_Mosaic',mats,width=x1-x0,depth=y1-y0,
                        tile=.85,cut=.105,border=.36,parent=parent)
    C.place(floor,((x0+x1)/2,(y0+y1)/2,z-.04))
    floor['bounds_xy']=list(bounds)
    floor['finished_elevation']=z
    floor['role']='continuous_upper_gallery'
    return floor


def _arcade(name,points,mats,z,parent):
    stone=_stone_materials(mats)
    openings=[]
    for i,(a,b) in enumerate(zip(points,points[1:])):
        run=math.dist(a[:2],b[:2])
        openings.append(dict(segment=i,center=run/2,width=run-.56,
                             height=z-.48,shape='arch',rise=.85,segments=10))
    root=A.wall_path(name,points,z-.30,.38,stone,openings=openings,
                     panel_height=.68,parent=parent)
    for i,p in enumerate(points):
        H.square_pier(name+'_Bearing_Column_%02d'%i,p,z-.30,stone,.52,root)
    root['role']='load_bearing_arcade_below_gallery'
    return root


def _two_levels(name,points,mats,height,upper_z,lower_openings,upper_openings,parent):
    root=C.group(name,parent=parent)
    A.wall_path(name+'_Ground_Register',points,upper_z,.30,mats,
                openings=lower_openings,panel_height=1.12,parent=root)
    elevated=[(p[0],p[1],p[2]+upper_z) for p in points]
    A.wall_path(name+'_Upper_Register',elevated,height-upper_z,.30,mats,
                openings=upper_openings,panel_height=1.12,parent=root)
    root['role']='two_storey_paneled_enclosure'
    return root


def validate_layout(spec):
    """Architectural connectivity checks, independent of scene rendering."""
    w,d=spec['width'],spec['depth'];g,s=spec['gallery'],spec['stairs']
    x=w/2-g['side_width'];back=d-g['back_depth'];top=g['elevation']
    half=s['middle_width']/2;cy=s['branch_center_y'];bw=s['branch_width']
    rectangles=[[-w/2,-x,g['front_y'],back],[x,w/2,g['front_y'],back],[-w/2,w/2,back,d]]
    assert x>half+.8 and g['side_width']>=1.5 and g['back_depth']>=1.5
    assert 0<s['middle_z']<top<spec['height']-2.8
    assert s['middle_front_y']<=cy-bw/2 and cy+bw/2<=s['middle_front_y']+s['middle_depth']
    assert g['front_y']<cy-bw/2<cy+bw/2<back
    assert s['middle_front_y']-s['central_run']>1.0
    def on_gallery(px,py):
        return any(a-1e-6<=px<=b+1e-6 and c-1e-6<=py<=e+1e-6 for a,b,c,e in rectangles)
    route=[[-w/2+g['side_width']/2,g['front_y']+.5,top],
           [-w/2+g['side_width']/2,cy,top],
           [-w/2+g['side_width']/2,(back+d)/2,top],
           [0,(back+d)/2,top],
           [w/2-g['side_width']/2,(back+d)/2,top],
           [w/2-g['side_width']/2,cy,top],
           [w/2-g['side_width']/2,g['front_y']+.5,top]]
    for a,b in zip(route,route[1:]):
        for i in range(21):
            t=i/20
            assert on_gallery(a[0]*(1-t)+b[0]*t,a[1]*(1-t)+b[1]*t),'Gallery route crosses a void'
    flights=[flight_spec((0,s['middle_front_y']-s['central_run'],0),
                         (0,s['middle_front_y'],s['middle_z']),s['central_width'],s['risers_per_flight'])]
    for sign in (-1,1):
        flights.append(flight_spec((sign*half,cy,s['middle_z']),(sign*x,cy,top),bw,s['risers_per_flight']))
        assert on_gallery(sign*(x+.10),cy)
    return dict(continuous_gallery=True,gallery_rectangles=rectangles,
                gallery_route=route,gallery_elevation=top,middle_elevation=s['middle_z'],
                gallery_clear_width=g['side_width'],gallery_back_clear_depth=g['back_depth'],
                stair_exit_gap_y=[cy-bw/2,cy+bw/2],
                stair_specs=[{k:f[k] for k in ('start','end','risers','treads','run','rise','riser_height','tread')} for f in flights],
                platform_contact='Each branch terminates exactly at the gallery inner boundary and finished elevation.',
                slab_bottom=top-.30,column_top=top-.30,
                gallery_support='Three connected stone arcades plus enclosing load-bearing walls.',
                wall_side_railings=False,door_floor_checks=[])


def build_grand_hall(spec,mats):
    spec=_layout(spec)
    qa=validate_layout(spec)
    b=RoomBuilder(spec,mats)
    mats=b.mats;w,d,h=b.width,b.depth,b.height
    mats['compass_dark']=C.solid_material('LP_Grand_Compass_Dark','#554B42',.84)
    g,s=spec['gallery'],spec['stairs'];top=g['elevation'];mid=s['middle_z']
    inner=w/2-g['side_width'];back=d-g['back_depth'];half=s['middle_width']/2
    cy=s['branch_center_y'];bw=s['branch_width'];front=s['middle_front_y']
    stone=_stone_materials(mats)
    _floor('Grand_Ground_Mosaic',w,d,mats,spec['floor'],b.static)
    for i,bounds in enumerate(qa['gallery_rectangles']):
        _gallery('Gallery_'+('West','East','Rear')[i],bounds,top,mats,b.static)
    # Arcade tops meet the underside of every gallery strip exactly at z=3.7.
    ys=[g['front_y'],6.8,10.6,back]
    for sign,side in ((-1,'West'),(1,'East')):
        pts=[(sign*inner,y,0) for y in ys]
        if sign>0:pts.reverse()
        _arcade('Gallery_'+side+'_Stone_Arcade',pts,mats,top,b.static)
    _arcade('Gallery_Rear_Stone_Arcade',[(-inner,back,0),(-2.2,back,0),(2.2,back,0),(inner,back,0)],mats,top,b.static)
    # Pale fascia makes the three joined floor strips readable as one storey.
    for sign,side in ((-1,'West'),(1,'East')):
        for z,depth,height,key in ((top-.15,.14,.22,'stone'),(top-.035,.20,.07,'cream')):
            C.box('Gallery_'+side+'_Stone_Fascia_'+key,(sign*(inner-.02),(g['front_y']+back)/2,z),
                  (depth,back-g['front_y'],height),mats[key],.009,b.static)
    for z,depth,height,key in ((top-.15,.14,.22,'stone'),(top-.035,.20,.07,'cream')):
        C.box('Gallery_Rear_Stone_Fascia_'+key,(0,back-.02,z),(2*inner,depth,height),mats[key],.009,b.static)

    # Opaque portal geometry belongs to this room only. No neighboring room is
    # built, no legacy Core IDs or coincident floor plans are fabricated.
    doors=[]
    for level,z in (('Ground',0),('Upper',top)):
        for sign,side in ((-1,'West'),(1,'East')):
            doors.append(dict(name='Grand_'+level+'_Rear_'+side,wall='rear',center=sign*(w/2-2.65),bottom=z,width=1.50,height=2.80))
            doors.append(dict(name='Grand_'+level+'_Side_'+side,wall=side.lower(),center=7.9,bottom=z,width=1.45,height=2.75))
    windows=[]
    for x,width in ((-3.35,1.70),(0,2.05),(3.35,1.70)):
        windows.append(dict(wall='rear',center=x,bottom=top+.42,width=width,height=3.55,rise=width/2,cols=4,rows=5))
    for side in ('west','east'):
        windows.append(dict(wall=side,center=12.05,bottom=top+.45,width=2.35,height=3.45,rise=1.04,cols=4,rows=5))
        windows.append(dict(wall=side,center=12.05,bottom=.65,width=2.0,height=2.65,rise=.90,cols=4,rows=4))
    wall_info={
        'rear':([(-w/2,d+.15,0),(w/2,d+.15,0)],lambda c:c+w/2,lambda c,z:(c,d,z),0),
        'west':([(-w/2-.15,g['front_y'],0),(-w/2-.15,d+.15,0)],lambda c:c-g['front_y'],lambda c,z:(-w/2,c,z),math.pi/2),
        'east':([(w/2+.15,d+.15,0),(w/2+.15,g['front_y'],0)],lambda c:d+.15-c,lambda c,z:(w/2,c,z),-math.pi/2),
    }
    for side,(points,center,position,yaw) in wall_info.items():
        lower,upper=[],[]
        for item in doors+windows:
            if item['wall']!=side:continue
            is_upper=item['bottom']>=top
            op=dict(center=center(item['center']),width=item['width'],height=item['height'],
                    bottom=item['bottom']-(top if is_upper else 0))
            if 'rise' in item:op.update(shape='arch',rise=item['rise'],segments=12)
            (upper if is_upper else lower).append(op)
        _two_levels('Grand_'+side+'_Wall',points,mats,h,top,lower,upper,b.static)
    for item in doors:
        side=item['wall'];data=wall_info[side]
        root=b.add('door',O.closed_door,data[2](item['center'],item['bottom']),data[3],
                   dict(width=item['width'],height=item['height'],double=False),name=item['name'])
        root['transition_mode']='teleport';root['destination_scene_hint']='unassigned'
        if item['bottom']==top:
            x,y,_=root.location
            facing=(math.sin(data[3]),-math.cos(data[3]))
            point=(x+facing[0]*.65,y+facing[1]*.65)
            supported=any(a<=point[0]<=c and e<=point[1]<=f for a,c,e,f in qa['gallery_rectangles'])
            assert supported,'Upper doorway has no continuous gallery beneath it: '+item['name']
            qa['door_floor_checks'].append(dict(door=item['name'],threshold_z=top,stand_point=list(point),supported=supported))
        b.ports.append(dict(id=root['instance_id'],wall=side,position=list(root.location),transition='teleport'))
    for i,item in enumerate(windows):
        data=wall_info[item['wall']]
        b.add('arched_window',O.arched_window,data[2](item['center'],item['bottom']),data[3],
              {k:item[k] for k in ('width','height','rise','cols','rows')},False,name='Grand_Arched_Window_%02d'%i)
    # Complete front enclosure is retained, explicitly hidden for the cutaway.
    hidden=C.group('Grand_Complete_Front_Enclosure')
    A.wall_path('Grand_Complete_Entry_Wall',[(w/2,-.15,0),(-w/2,-.15,0)],h,.3,mats,
                openings=[dict(center=w/2,width=2.5,height=3.15)],parent=hidden)
    for sign in (-1,1):
        points=[(sign*(w/2+.15),0,0),(sign*(w/2+.15),g['front_y'],0)]
        A.wall_path('Grand_Complete_Near_Side_'+str(sign),points,h,.3,mats,parent=hidden)
    b.add_existing(hidden,'complete_shell',dict(width=w,depth=d,height=h,chapter='grand_hall'),False)
    hide_tree(hidden)
    entry=b.add('door',O.closed_door,(0,0,0),math.pi,dict(width=2.5,height=3.15,double=True),name='Grand_Entry_Door')
    entry['transition_mode']='teleport';hide_tree(entry)
    # Tall pale pilasters unify both wall registers without enlarging mouldings.
    for i,x in enumerate((-w/2+.12,-4.65,-1.55,1.55,4.65,w/2-.12)):
        H.square_pier('Grand_Rear_Stone_Pilaster_%02d'%i,(x,d-.08,0),h,stone,.40,b.static)
    for sign in (-1,1):
        for i,y in enumerate((g['front_y'],5.55,10.05,d-.12)):
            H.square_pier('Grand_Side_Stone_Pilaster_%s_%02d'%(sign,i),
                          (sign*(w/2-.08),y,0),h,stone,.40,b.static)

    # Every flight shares exact endpoint elevations with a physically supported
    # landing/deck. The central platform has a full-height masonry base.
    stairs=C.group('Grand_Central_Bifurcated_Stair',parent=b.static)
    stair_mats=dict(mats);stair_mats['wood_dark']=mats['stone_dark']
    straight_flight('Grand_Central_Flight',(0,front-s['central_run'],0),(0,front,mid),
                     s['central_width'],stair_mats,risers=s['risers_per_flight'],parent=stairs)
    middle_bounds=(-half,half,front,front+s['middle_depth'])
    C.box('Grand_Middle_Landing_Masonry_Base',(0,front+s['middle_depth']/2,(mid-.30)/2),
          (s['middle_width'],s['middle_depth'],mid-.30),mats['wood_dark'],.012,stairs)
    H.gallery_deck('Grand_Middle_Landing',middle_bounds,mid,stone,parent=stairs)
    rail_height=g['railing_height']
    for sign,side in ((-1,'West'),(1,'East')):
        C.box('Grand_'+side+'_Foot_Newel_Platform',
              (sign*(s['central_width']/2+.035),front-s['central_run'],mid/s['risers_per_flight']/2),
              (.27,.27,mid/s['risers_per_flight']),mats['stone'],.005,stairs)
        straight_flight('Grand_'+side+'_Ascending_Branch',(sign*half,cy,mid),(sign*inner,cy,top),
                         bw,stair_mats,risers=s['risers_per_flight'],parent=stairs)
        p=[(sign*(s['central_width']/2+.035),front-s['central_run'],mid/s['risers_per_flight']),
           (sign*(s['central_width']/2+.035),front,mid),
           (sign*half,cy-bw/2,mid),(sign*inner,cy-bw/2,top)]
        A.railing('Grand_'+side+'_Central_and_Front_Branch_Rail',p,mats,
                   height=rail_height,spacing=.23,parent=stairs)
        path=[(sign*w/2,g['front_y'],top),(sign*inner,g['front_y'],top),
              (sign*inner,cy-bw/2,top)]
        A.railing('Grand_'+side+'_Open_Gallery_Edge',path,mats,
                   height=rail_height,spacing=.24,parent=b.static,end_posts=(True,False))
    rear_path=[(-inner,cy+bw/2,top),(-half,cy+bw/2,mid),
               (-half,front+s['middle_depth'],mid),(half,front+s['middle_depth'],mid),
               (half,cy+bw/2,mid),(inner,cy+bw/2,top)]
    A.railing('Grand_Rear_Branch_and_Middle_Landing_Rail',rear_path,mats,
               height=rail_height,spacing=.23,parent=stairs)
    A.railing('Grand_Continuous_Rear_Gallery_Edge',
               [(-inner,cy+bw/2,top),(-inner,back,top),(inner,back,top),(inner,cy+bw/2,top)],
               mats,height=rail_height,spacing=.24,parent=b.static,end_posts=(False,False))
    stairs['upper_exits_join_continuous_gallery']=True
    stairs['no_wall_side_rails']=True
    stairs['layout_qa_json']=json.dumps(qa)

    # Restrained furnishing: a small waiting group, one reception desk, plants
    # at the entrances, and a single large chandelier on the main axis.
    b.table((-4.75,3.65,0),width=1.35,depth=.78,height=.76)
    b.chair((-4.75,2.65,0),math.pi)
    b.chair((-4.75,4.65,0),0)
    b.lamp((-4.75,3.6,.76))
    b.add('reception_desk',F.reception_desk,(4.80,3.6,0),0,
          dict(width=2.50,depth=1.05,height=1.08,panels=3,details=True))
    b.chair((4.80,4.78,0),0)
    b.lamp((4.12,3.22,1.08))
    for sign,side in ((-1,'West'),(1,'East')):
        b.bench((sign*(w/2-1.15),6.1,0),math.pi/2 if sign<0 else -math.pi/2,width=1.85)
        b.plant((sign*(inner-.2),2.6,0),width=.90,height=1.10,seed=8)
        b.plant((sign*(w/2-1.1),d-1.3,top),width=.75,height=.90,seed=8)
        b.add('pedestal',D.pedestal,(sign*2.5,front-.50,0),0,dict(height=.70,width=.55),False)
        b.plant((sign*2.5,front-.50,.70),width=.66,height=.76,seed=9)
        for y,z in ((5.60,2.25),(10.05,2.25),(5.60,6.15),(10.05,6.15)):
            b.add('wall_lantern',L.wall_lantern,(sign*(w/2-.27),y,z),
                  math.pi/2 if sign<0 else -math.pi/2,
                  dict(height=.80,light_energy=20),False)
        b.add('wall_lantern',L.wall_lantern,(sign*4.65,d-.28,5.95),0,
              dict(height=.80,light_energy=20),False)
    b.add('chandelier',H.chandelier,(0,7.35,h-.05),0,
          dict(radius=1.85,drop=1.85,arms=10,energy=100),False,name='Grand_Central_Chandelier')
    b.add('painting',F.painting,(0,d-.25,1.20),0,dict(width=2.8,height=1.65,style='city'),False)
    b.root['architectural_intent']='Continuous U gallery with supported arcades, real bifurcated stair exits, opaque teleport doors.'
    b.root['gallery_qa_json']=json.dumps(qa)
    result=b.finish(camera=spec['camera'],lighting={'style':'grand_foyer','sun_direction':[.27,-1,-.60]})
    result.update(geometry_report=qa,structure_camera=spec['structure_camera'],hidden_complete_shell=hidden)
    return result


def light_grand_hall(spec=None):
    """Black world, physically blocked sun and broad warm presentation fill."""
    spec=_layout(spec);w,d,h=spec['width'],spec['depth'],spec['height']
    world=bpy.data.worlds.new('Grand_Hall_Pure_Black_World');world.use_nodes=True
    background=world.node_tree.nodes.get('Background')
    background.inputs['Color'].default_value=(0,0,0,1)
    background.inputs['Strength'].default_value=0
    bpy.context.scene.world=world
    lights=[]
    lights.append(R.area('Grand_Front_Softbox',(0,-5.0,7.5),(0,9,3),1250,11,(1,.91,.79),8))
    lights.append(R.area('Grand_Upper_Fill',(-5,6,8.3),(0,8,1.5),700,7,(1,.86,.66),8))
    lights.append(R.area('Grand_Right_Fill',(7,5,6.8),(0,9,2),500,6,(.75,.83,1.0),7))
    lights.append(R.area('Grand_Gallery_Bounce',(0,13.8,7.8),(0,13.5,4.2),500,10,(1,.90,.76),3))
    lights.append(R.sun('Grand_Window_Sun',(.80,-.55,-.55),4.0,.65))
    # A presentation-only cap prevents rays entering through the removed roof.
    mats={m.name.removeprefix('LP_'):m for m in bpy.data.materials if m.name.startswith('LP_')}
    if 'wood_dark' in mats:
        R.shadow_roof('Grand_Hidden_Shadow_Roof',mats,w,d,h,(0,d/2,0))
    return lights


build=build_grand_hall
