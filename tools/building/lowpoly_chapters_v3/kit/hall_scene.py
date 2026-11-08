"""Data-driven single-room assembly of the approved independent components."""
import json
import math
import bpy
from . import architecture as A, flooring as T, furniture as F, openings as O
from . import decor as D, lighting as L, rendering as R
from . import hall_architecture as H
from .core import group, box, place
from .stair_exact import straight_flight
from .export_godot import tag_component


def _hide_tree(root):
    root.hide_render = True
    root['exclude_frame'] = True
    for child in root.children_recursive:
        child.hide_render = True
        child['exclude_frame'] = True


def build_hall(spec, mats):
    room,s = spec['room'],spec['stairs']
    x0,x1,y0,y1 = (room[k] for k in ('xmin','xmax','ymin','ymax'))
    height,top,mid=room['height'],s['top_z'],s['middle_z']
    static=group('Hall_Static_Architecture')
    fixtures=group('Hall_Reusable_Components')
    cutaway=group('Hall_Complete_Near_Walls__hidden_for_cutaway')
    components=[]
    floor=T.tiled_floor('Hall_Geometric_Mosaic',mats,width=x1-x0,depth=y1-y0,
                        tile=room['floor_tile'],cut=.135,border=.5,parent=static)
    place(floor,(0,(y0+y1)/2,-.04))
    rear_ops=[dict(center=x-x0,width=1.36,height=2.48) for x in (-12.42,12.42)]
    upper_ops=[]
    for w in spec.get('windows',[]):
        bottom=w['position'][2]
        rear_ops.append(dict(center=w['position'][0]-x0,width=w['width'],bottom=bottom,height=top-bottom))
        upper_ops.append(dict(center=w['position'][0]-x0,width=w['width'],bottom=0,
                              height=bottom+w['height']-top,shape='arch',rise=w['rise']))
    # The shell is behind the finished room boundary; frame fronts meet y1.
    H.two_register_wall('Hall_Rear_Wall',[(x0,y1+.15,0),(x1,y1+.15,0)],mats,
                         height=height,gallery_z=top,lower_openings=rear_ops,
                         upper_openings=upper_ops,parent=static)
    H.two_register_wall('Hall_Left_Rear_Wall',[(x0-.15,7.8,0),(x0-.15,y1+.15,0)],
                         mats,height=height,gallery_z=top,parent=static)
    H.two_register_wall('Hall_Right_Rear_Wall',[(x1+.15,y1+.15,0),(x1+.15,7.8,0)],
                         mats,height=height,gallery_z=top,parent=static)
    # Low near-wall sections are an explicit presentation cut, with complete
    # replacements stored separately in the same .blend for another camera.
    A.wall_path('Hall_Left_Cutaway_Base',[(x0-.15,y0,0),(x0-.15,7.8,0)],.65,.3,mats,
                panel_height=.42,parent=static)
    A.wall_path('Hall_Right_Cutaway_Base',[(x1+.15,7.8,0),(x1+.15,y0,0)],.65,.3,mats,
                panel_height=.42,parent=static)
    for name,points in [('Left',[(x0-.15,y0,0),(x0-.15,7.8,0)]),
                        ('Right',[(x1+.15,7.8,0),(x1+.15,y0,0)])]:
        H.two_register_wall('Complete_'+name+'_Near_Wall',points,mats,height=height,
                            gallery_z=top,parent=cutaway)
    A.wall_path('Complete_Front_Wall',[(x1,y0-.15,0),(x0,y0-.15,0)],height,.3,mats,
                openings=[dict(center=x1,width=2.56,height=2.48)],parent=cutaway)
    _hide_tree(cutaway)
    # Broad pier rhythm belongs to the room, separate from wall sampling.
    for i,x in enumerate((-13.65,-11.4,-8.1,-3.5,3.5,8.1,11.4,13.65)):
        H.square_pier('Rear_Pilaster_%02d'%i,(x,y1-.04,0),height-.08,mats,.34,static)

    stair=group('Hall_Symmetric_Bifurcated_Stair',parent=static)
    stair['total_rise']=top
    stair['middle_elevation']=mid
    stair['wall_side_railings']=False
    straight_flight('Stair_Central',s['central_start'],s['central_end'],
                     s['central_width'],mats,risers=s['risers_per_flight'],parent=stair)
    H.gallery_deck('Stair_Middle_Landing',s['middle_bounds'],mid,mats,parent=stair,front_trim=False)
    for sign,side in ((-1,'Left'),(1,'Right')):
        a=(sign*s['branch_inner_x'],s['branch_y'],mid)
        b=(sign*s['branch_outer_x'],s['branch_y'],top)
        straight_flight('Stair_Branch_'+side,a,b,s['branch_width'],mats,
                        risers=s['risers_per_flight'],parent=stair)
        xa,xb=sorted((sign*s['branch_outer_x'],sign*s['top_landing_outer_x']))
        H.gallery_deck('Small_Exit_Landing_'+side,(xa,xb,s['gallery_front_y'],y1),top,mats,parent=static)
        # The corridor beyond this opaque portal belongs to another room.
        px=sign*(s['top_landing_outer_x']+.15)
        points=[(px,11.7,top),(px,14.1,top)] if sign<0 else [(px,14.1,top),(px,11.7,top)]
        A.wall_path('Upper_Exit_Portal_'+side,points,2.9,.3,mats,
                    openings=[dict(center=1.2,width=1.56,height=2.58)],parent=static)
        # One connected exposed-edge railing: lower stair, midlanding and
        # front edge of the branch. The wall side is physically against wall.
        p=[(sign*1.76,s['central_start'][1],mid/s['risers_per_flight']),
           (sign*1.76,10.5,mid),(sign*1.76,11.74,mid),
           (sign*s['branch_outer_x'],11.74,top),(sign*(s['top_landing_outer_x']-.1),11.74,top)]
        A.railing('Exposed_Edge_Rail_'+side,p,mats,height=.95,spacing=.24,parent=static)
        H.square_pier('Landing_Support_'+side,(sign*(s['top_landing_outer_x']-.2),11.94,0),top-.3,mats,.38,static)
    # Finished landing fronts provide the last riser of each exact flight.
    stair['risers_per_flight']=s['risers_per_flight']
    stair['physical_treads_per_flight']=s['risers_per_flight']-1

    def component(root,kind,params,position,rotation=0,instance=None,interactive=True,asset=None,boxes=None):
        root.parent=fixtures
        place(root,position,rotation)
        aid=asset or kind+'_'+str(len(components))
        stable_id=(instance or root.name).replace('.','_')
        tag_component(root,aid,kind,params,instance_id=stable_id,
                       interactive=interactive,collision_boxes=boxes)
        components.append(root)
        return root

    for d in spec['doors']:
        params={k:d[k] for k in ('width','height','double')}
        aid='door_double_240' if d['double'] else ('door_single_140' if d['width']>1.3 else 'door_single_120')
        root=component(O.closed_door(d['instance_id'],mats,**params),'door',params,d['position'],
                       math.radians(d['yaw_degrees']),d['instance_id'],True,
                       aid)
        if 'source_portal_id' in d:root['source_portal_id']=d['source_portal_id']
        root['preview_visible']=d.get('preview_visible',True)
        if not d.get('preview_visible',True):_hide_tree(root)
    for w in spec.get('windows',[]):
        params={k:w[k] for k in ('width','height','rise','cols','rows')}
        component(O.arched_window(w['instance_id'],mats,**params),'arched_window',params,w['position'],
                  instance=w['instance_id'],interactive=False,asset='arched_window_210_360')

    # Furnishing layout is deliberately an editable visual proposal. Semantic
    # entity placement and actions are still owned by the host DV/Core.
    chair_p=dict(width=.58,depth=.58,height=1.05,seat_height=.46,arms=False)
    def chair(name,pos,rotation=0):
        return component(F.chair(name,mats,**chair_p),'chair',chair_p,pos,rotation,name,True,'chair_walnut_058',
                         [dict(name='seat',center=[0,0,.45],size=[.58,.58,.10])])
    table_p=dict(width=2.5,depth=1.25,height=.82)
    component(F.table('hall.reading_table',mats,**table_p),'table',table_p,(-7.5,4.3,0),
              instance='hall.reading_table',asset='table_walnut_250',
              boxes=[dict(name='top',center=[0,0,.77],size=[2.5,1.25,.10])])
    for i,x in enumerate((-8.22,-6.78)):
        chair('hall.reading_chair.front.'+str(i),(x,3.22,0),math.pi)
        chair('hall.reading_chair.back.'+str(i),(x,5.38,0),0)
    desk_p=dict(width=3.2,depth=1.15,height=1.08,panels=4,details=True)
    component(F.reception_desk('hall.reception',mats,**desk_p),'reception',desk_p,(8.2,4.9,0),
              instance='hall.reception',asset='reception_walnut_320')
    chair('hall.reception_chair',(8.2,6.05,0),0)
    bench_p=dict(width=2.0,depth=.65,height=1.10,seat_height=.46)
    for i,pos in enumerate(((-12.65,3.15,0),(12.65,2.65,0))):
        component(F.bench('hall.bench.'+str(i),mats,**bench_p),'bench',bench_p,pos,
                  math.pi/2 if i==0 else -math.pi/2,'hall.bench.'+str(i),True,'bench_walnut_200')
    shelf_p=dict(width=2.9,depth=.5,height=2.52,shelves=4,bays=3,fill=.72,seed=9)
    for sign,side in ((-1,'left'),(1,'right')):
        component(F.bookshelf('hall.bookcase.'+side,mats,**shelf_p),'bookshelf',shelf_p,
                  (sign*8.1,13.69,0),instance='hall.bookcase.'+side,asset='bookcase_walnut_290')
        pedestal=D.pedestal('hall.flower_pedestal.'+side,mats,height=.85,width=.65)
        component(pedestal,'pedestal',dict(height=.85,width=.65),(sign*3.1,8.5,0),
                  instance='hall.flower_pedestal.'+side,interactive=False,asset='pedestal_065')
        flower_p=dict(width=.80,height=.9,flower_count=9,seed=7)
        component(F.planter('hall.flowers.'+side,mats,**flower_p),'planter',flower_p,
                  (sign*3.1,8.5,.85),instance='hall.flowers.'+side,interactive=False,asset='planter_white_080')
        component(F.planter('hall.landing_flowers.'+side,mats,**flower_p),'planter',flower_p,
                  (sign*6.4,13.55,top),instance='hall.landing_flowers.'+side,interactive=False,asset='planter_white_080')
    component(F.painting('hall.central_painting',mats,width=2.8,height=1.75,style='city'),
              'painting',dict(width=2.8,height=1.75,style='city'),(0,14.05,4.85),
              instance='hall.central_painting',interactive=False,asset='painting_city_280')
    for sign in (-1,1):
        component(F.painting('hall.side_painting.'+str(sign),mats,width=1.3,height=1.1,style='landscape'),
                  'painting',dict(width=1.3,height=1.1,style='landscape'),(sign*12.42,14.05,4.75),
                  instance='hall.side_painting.'+str(sign),interactive=False,asset='painting_landscape_130')
    component(F.open_book('hall.open_book',mats,width=.55),'book',dict(width=.55),(-7.5,4.05,.82),
              instance='hall.open_book',interactive=False,asset='open_book_055')
    component(F.books('hall.book_stack',mats,width=.38,depth=.28,height=.23),'books',
              dict(width=.38,depth=.28,height=.23),(8.95,4.48,1.14),
              instance='hall.book_stack',interactive=False,asset='book_stack_038')
    for i,p in enumerate(((-8.24,4.36,.82),(7.25,4.48,1.14))):
        component(L.banker_lamp('hall.table_lamp.'+str(i),mats,height=.66,light_energy=20),
                  'banker_lamp',dict(height=.66,light_energy=20),p,instance='hall.table_lamp.'+str(i),
                  interactive=False,asset='banker_lamp_066')
    for i,(x,z) in enumerate(((-11,2.2),(11,2.2),(-3.4,4.7),(3.4,4.7),(-11,5.4),(11,5.4))):
        component(L.wall_lantern('hall.wall_lamp.'+str(i),mats,height=.85,light_energy=55),
                  'wall_lantern',dict(height=.85,light_energy=55),(x,14.08,z),
                  instance='hall.wall_lamp.'+str(i),interactive=False,asset='wall_lantern_085')
    for i,x in enumerate((-7.5,7.5)):
        component(H.chandelier('hall.chandelier.'+str(i),mats), 'chandelier',
                  dict(radius=.95,drop=1.65,arms=8,energy=360),(x,6.6,7.85),
                  instance='hall.chandelier.'+str(i),interactive=False,asset='chandelier_ring_190')
    return dict(components=components,static_objects=[static],hidden_complete_shell=cutaway)


def light_hall():
    world=bpy.data.worlds.new('Hall_Pure_Black_World')
    world.use_nodes=True
    bg=world.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value=(0,0,0,1)
    bg.inputs['Strength'].default_value=0
    bpy.context.scene.world=world
    for i,x in enumerate((-8,0,8)):
        R.area('Hall_Overhead_Softbox_'+str(i),(x,7.0,7.65),(x,7.3,0),650,6.0,(1,.86,.68),size_y=8)
    R.area('Hall_Front_Presentation_Fill',(-3,-4,8),(0,8,2),1100,15,(1,.91,.80))
    R.area('Hall_Right_Cool_Fill',(12,1,7),(1,9,3),650,8,(.72,.83,1))
