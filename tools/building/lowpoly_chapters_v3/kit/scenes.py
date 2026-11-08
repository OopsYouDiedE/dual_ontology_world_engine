"""Room assembly only. All component construction lives in separate modules."""
import math
from .core import place
from . import architecture as A, openings as O, furniture as F, lighting as L
from . import flooring as T, decor as D, rendering as R
from .paths import offset_polyline


def mount(obj,location,rotation=0,inset=.14):
    """Place the opening at the room-facing surface of a wall centreline."""
    x,y,z=location
    return place(obj,(x+math.sin(rotation)*inset,y-math.cos(rotation)*inset,z),rotation)


def lobby(spec,mats):
    w,d,h=spec['width'],spec['depth'],spec['height']
    bx,by=w/2,d/2
    T.tiled_floor('Lobby_floor',mats,width=w,depth=d,tile=spec['tile'])
    windows=[dict(segment=0,center=bx+x,width=1.4,height=2.8,bottom=1.05,shape='arch',rise=.7) for x in (-2.95,2.95)]
    door=dict(segment=0,center=bx,width=2.05,height=3.25,bottom=0,shape='rect')
    A.wall_path('Lobby_back_wall',[(-bx,by,0),(bx,by,0)],h,.26,mats,openings=windows+[door])
    for x in (-2.95,2.95):
        mount(O.arched_window('Lobby_window_'+str(x),mats,width=1.4,height=2.8,cols=4,rows=5), (x,by,1.05))
    mount(O.closed_door('Lobby_main_door',mats,width=2.05,height=3.25,double=True), (0,by,0))
    A.wall_path('Lobby_left_wall',[(-bx,-2.65,0),(-bx,by,0)],h,.26,mats,
                openings=[dict(segment=0,center=3.8,width=1.25,height=2.85,bottom=0,shape='rect')])
    mount(O.closed_door('Lobby_left_closed_door',mats,width=1.25,height=2.85),(-bx,1.15,0),math.pi/2)
    for x in (-1.52,1.52):
        place(L.wall_lantern('Lobby_wall_lantern_'+str(x),mats,height=.68,light_energy=16),(x,by-.18,2.7))
        place(D.pedestal('Lobby_pedestal_'+str(x),mats,height=.82,width=.48),(x,by-.6,0))
        place(F.planter('Lobby_flowers_'+str(x),mats,width=.67,height=.74),(x,by-.6,.82))
    place(F.bench('Lobby_bench',mats,width=2.0),(-bx+.52,-1.2,.04),math.pi/2)
    place(F.painting('Lobby_city_painting',mats,width=1.15,height=.95),(-bx+.17,-1.1,1.65),math.pi/2)
    place(D.coat_stand('Lobby_coat_stand',mats),(-2.85,1.12,.04))
    place(F.reception_desk('Lobby_reception_desk',mats,width=2.6,depth=1.03,height=1.1),(2.55,-1.15,.04))
    place(F.chair('Lobby_reception_chair',mats),(2.55,-.21,.04))
    place(L.banker_lamp('Lobby_desk_lamp',mats,height=.57),(1.65,-1.55,1.14))
    place(F.books('Lobby_books',mats),(2.70,-1.55,1.14))
    place(F.planter('Lobby_desk_flowers',mats,width=.44,height=.61),(3.67,-1.55,1.14))
    place(L.wall_lantern('Lobby_side_lantern',mats,height=.58),(-bx+.18,.15,2.75),math.pi/2)
    A.staircase('Lobby_entry_steps',[(0,-4.12,-.48),(0,-3.24,.04)],3.0,mats,step_rise=.17)
    R.shadow_roof('Lobby_cutaway_ceiling',mats,w,d,h)
    R.studio_rig(height=h,with_sun=True,sun_direction=spec['sun_direction'])
    return dict(location=(9,-17,12),target=(0,0,1.6))


def library(spec,mats):
    w,d,h=spec['width'],spec['depth'],spec['height']
    bx,by=w/2,d/2
    T.tiled_floor('Library_floor',mats,width=w,depth=d,tile=spec['tile'])
    opens=[dict(segment=0,center=bx+x,width=1.65,height=3.25,bottom=1.0,shape='arch',rise=.825) for x in (-2.55,0,2.55)]
    A.wall_path('Library_window_wall',[(-bx,by,0),(bx,by,0)],h,.26,mats,openings=opens)
    for x in (-2.55,0,2.55):
        mount(O.arched_window('Library_window_'+str(x),mats,width=1.65,height=3.25,cols=4,rows=6),(x,by,1.0))
    A.wall_path('Library_book_wall',[(-bx,-by,0),(-bx,by,0)],h,.26,mats,
                openings=[dict(segment=0,center=1.0,width=1.2,height=3.05,bottom=0,shape='rect')])
    mount(O.closed_door('Library_closed_door',mats,width=1.2,height=3.05),(-bx,-by+1,0),math.pi/2)
    for index,y in enumerate((.12,2.17)):
        place(F.bookshelf('Library_bookcase_'+str(index),mats,width=2.0,depth=.50,height=3.95,shelves=5,bays=2,seed=18+index),(-bx+.39,y,.04),math.pi/2)
    place(D.library_rail('Library_ladder_rail',mats,length=4.08,height=3.3),(-bx+.62,1.14,0),math.pi/2)
    place(F.ladder('Library_ladder',mats,height=3.30,width=.52,depth=.50,rungs=9),(-bx+1.20,.8,.04),math.pi/2)
    place(F.table('Library_reading_table',mats,width=3.75,depth=1.25,height=.84),(.35,-.66,.04))
    for x in (-.9,.35,1.60):
        place(F.chair('Library_front_chair_'+str(x),mats),(x,-1.66,.04),math.pi)
        place(F.chair('Library_back_chair_'+str(x),mats),(x,.36,.04))
    for x in (-.58,1.3):
        place(L.banker_lamp('Library_table_lamp_'+str(x),mats,height=.6),(x,-.62,.88))
    place(F.books('Library_table_books',mats,width=.44,depth=.3,height=.22),(-1.09,-.83,.88))
    place(F.open_book('Library_table_open_book',mats),(.34,-.9,.88))
    for x in (-1.35,1.5):
        place(F.table('Library_window_desk_'+str(x),mats,width=1.7,depth=.78,height=.84),(x,2.42,.04))
        place(F.chair('Library_window_chair_'+str(x),mats),(x,1.58,.04),math.pi)
        place(L.banker_lamp('Library_desk_lamp_'+str(x),mats,height=.57),(x+.48,2.47,.88))
        place(F.open_book('Library_desk_book_'+str(x),mats,width=.48),(x-.28,2.35,.88))
        place(F.books('Library_desk_stack_'+str(x),mats,width=.23,depth=.21,height=.17),(x+.58,2.12,.88))
    place(D.pedestal('Library_flower_pedestal',mats,height=.75),(3.42,2.66,.04))
    place(F.planter('Library_corner_flowers',mats,width=.66,height=.8),(3.42,2.66,.79))
    place(F.painting('Library_landscape',mats,width=.75,height=.95,style='landscape'),(-bx+.17,-1.08,2.0),math.pi/2)
    place(L.wall_lantern('Library_door_light',mats,height=.65),(-bx+.18,-1.57,2.9),math.pi/2)
    place(L.wall_lantern('Library_corner_light',mats,height=.65),(-3.6,by-.16,3.45))
    A.railing('Library_front_balustrade',[(-1.25,-by+.20,.04),(3.75,-by+.20,.04)],mats,height=.91,spacing=.25)
    R.shadow_roof('Library_cutaway_ceiling',mats,w,d,h)
    R.studio_rig(height=h,with_sun=True,sun_direction=spec['sun_direction'])
    return dict(location=(11,-15,12),target=(0,0,1.6))


def stairs(spec,mats):
    import json
    from .core import group, polyprism, beam
    w,d,h=spec['width'],spec['depth'],spec['height']
    bx,by=w/2,d/2
    T.tiled_floor('Stairhall_floor',mats,width=w,depth=d,tile=spec['tile'])
    A.wall_path('Stairhall_back_wall',[(-bx,by,0),(bx,by,0)],h,.28,mats,
                openings=[dict(segment=0,center=bx+1.45,width=1.9,height=3.85,bottom=2.25,shape='arch',rise=.95)])
    mount(O.arched_window('Stairhall_tall_window',mats,width=1.9,height=3.85,cols=3,rows=6),(1.45,by,2.25))
    A.wall_path('Stairhall_left_wall',[(-bx,-by,0),(-bx,by,0)],h,.28,mats,
                openings=[dict(segment=0,center=1.5,width=1.6,height=2.95,bottom=0,shape='rect'),
                          dict(segment=0,center=4.95,width=1.25,height=2.35,bottom=3.9,shape='rect')])
    mount(O.closed_door('Stairhall_lower_door',mats,width=1.6,height=2.95,double=True),(-bx,-by+1.5,0),math.pi/2)
    mount(O.closed_door('Stairhall_upper_door',mats,width=1.25,height=2.35),(-bx,1.65,3.9),math.pi/2)
    # A horizontal buffer around C separates the plan turn from the next rise.
    # Offsetting a corner that immediately rises would change the rail's grade.
    points=[(1.7,-2.7,.04),(1.7,.23,1.84),(1.7,1.65,1.84),
            (.6,1.65,1.84),(-2.1,1.65,3.9),(-bx+.14,1.65,3.9)]
    stair_root=group('Stairhall_main_stair')
    stair_root['path_nodes']=json.dumps(points)
    stair_root['width']=1.65
    A.staircase('Stairhall_lower_flight',points[:2],1.65,mats,step_rise=.18,parent=stair_root)
    A.staircase('Stairhall_upper_flight',points[3:],1.65,mats,step_rise=.18,parent=stair_root)
    # One continuous L-shaped deck includes the 1.65 m square turning pad and
    # the flower terrace; there are no coplanar overlapping landing caps.
    deck=[(.875,.23),(3.89,.23),(3.89,2.475),(.6,2.475),(.6,.825),(.875,.825)]
    polyprism('Stairhall_middle_landing_body',deck,1.54,1.785,mats['wood'],stair_root)
    polyprism('Stairhall_middle_landing_surface',deck,1.785,1.84,mats.get('stair_tread',mats['stone']),stair_root)
    for a,b in [((2.525,.23),(3.89,.23)),((3.89,.23),(3.89,2.475)),((3.89,2.475),(.6,2.475))]:
        beam('Stairhall_landing_edge',(a[0],a[1],1.70),(b[0],b[1],1.70),.075,.13,mats['wood_light'],parent=stair_root)
    left=offset_polyline(points,-.79)
    right=offset_polyline(points,.79)
    A.railing('Stairhall_inner_handrail',left,mats,height=.9,spacing=.24)
    # Leave B -> C open for walking onto the terrace. Its exposed outer edge
    # has a separate guard, sharing existing endpoint newels.
    A.railing('Stairhall_lower_outer_handrail',right[:2],mats,height=.9,spacing=.24)
    A.railing('Stairhall_upper_outer_handrail',right[2:],mats,height=.9,spacing=.24)
    A.railing('Stairhall_middle_guard',[right[1],(3.84,.23,1.84),(3.84,2.44,1.84),right[2]],
              mats,height=.9,spacing=.24,end_posts=(False,False))
    place(D.pedestal('Stairhall_planter_pedestal',mats,height=.62,width=.45),(3.12,1.65,1.84))
    place(F.planter('Stairhall_flowers',mats,width=.67,height=.74),(3.12,1.65,2.46))
    place(F.painting('Stairhall_city_painting',mats,width=1.15,height=1.0),(-bx+.18,.35,1.65),math.pi/2)
    for idx,(x,z) in enumerate([(-2.7,3.0),(-.38,4.1),(3.12,4.0)]):
        place(L.wall_lantern('Stairhall_wall_light_'+str(idx),mats,height=.72,light_energy=20),(x,by-.18,z))
    place(L.wall_lantern('Stairhall_upper_light',mats,height=.62,light_energy=18),(-bx+.18,.35,5.50),math.pi/2)
    place(L.wall_lantern('Stairhall_lower_light',mats,height=.64,light_energy=16),(-bx+.18,-.61,2.8),math.pi/2)
    R.shadow_roof('Stairhall_cutaway_ceiling',mats,w,d,h)
    R.studio_rig(height=h,with_sun=True,sun_direction=spec['sun_direction'])
    return dict(location=(12,-18,13),target=(0,0,2.5))


BUILDERS={'lobby':lobby,'library':library,'stairs':stairs}
