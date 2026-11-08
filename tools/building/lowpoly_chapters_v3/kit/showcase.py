"""Nine independent, genuinely rendered component demonstrations."""
import json
import math
from pathlib import Path
from . import core as C, rendering as R, architecture as A, openings as O
from . import furniture as F, lighting as L, flooring as T, decor as D
from .paths import sample_cubic_bezier, offset_polyline


def component_wall(m):
    T.tiled_floor('Wall_sample_floor',m,width=5.2,depth=2.1,tile=.65)
    A.wall_path('Panelled_wall',[(-2.35,.6,0),(2.35,.6,0)],3.5,.24,m,openings=[
      dict(center=1.1,width=1.1,height=2.6,bottom=0,shape='rect'),
      dict(center=3.45,width=1.25,height=2.4,bottom=.65,shape='arch')])
    C.place(O.closed_door('Door',m,width=1.1,height=2.6),(-1.25,.48,0))
    C.place(O.arched_window('Window',m,width=1.25,height=2.4), (1.1,.48,.65))
    C.place(L.wall_lantern('Wall_lantern',m,height=.6),(0,.43,2.45))


def component_stair(m):
    T.tiled_floor('Stair_sample_floor',m,width=4.3,depth=4.0,tile=.65)
    points=[(.9,-1.55,.04),(.9,.4,1.24),(.9,1.25,1.24),(-.05,1.25,1.24),(-1.5,1.25,2.15)]
    A.staircase('Turn_stair',points,.98,m,step_rise=.16)
    C.place(D.platform('Square_landing',m,width=1.06,depth=1.06,height=1.19),(.9,1.25,0))
    for side in (-1,1):
        A.railing('Stair_rail_'+str(side),offset_polyline(points,side*.46),m,height=.75,spacing=.2)


def component_rails(m):
    T.tiled_floor('Rail_sample_floor',m,width=4.9,depth=3.2,tile=.65)
    A.railing('Straight_and_corner',[(-2,1,.04),(.8,1,.04),(1.6,.5,.04)],m,height=.95,spacing=.22)
    curve=sample_cubic_bezier((-2,-.9,.04),(-.7,-.95,.04),(1.4,-1.2,.04),(2,.55,.04),segments=20)
    A.railing('Bezier_railing',curve,m,height=.85,spacing=.2)


def component_bookshelf(m):
    C.place(F.bookshelf('Bookcase',m,width=2.8,depth=.5,height=3.3,shelves=5,bays=3,seed=24),(0,.35,0))
    C.place(D.library_rail('Sliding_ladder_rail',m,length=2.8,height=2.8),(0,-.04,0))
    C.place(F.ladder('Ladder',m,width=.47,height=2.8,depth=.55),(0,-.65,0))


def component_table(m):
    C.place(F.table('Reading_table',m,width=2.4,depth=.95,height=.82),(0,0,0))
    for x in (-.75,.75):
        C.place(F.chair('Reading_chair_'+str(x),mats=m),(x,.85,0))
    C.place(F.chair('Armchair',m,arms=True),(-.78,-.92,0),math.pi)
    C.place(L.banker_lamp('Banker_lamp',m,height=.57),(.38,0,.82))
    C.place(F.open_book('Open_book',m),(-.5,-.06,.82))
    C.place(F.books('Book_stack',m),(.91,.14,.82))


def component_counter(m):
    C.place(F.reception_desk('Counter',m,width=2.5,height=1.06,details=True),(0,.45,0))
    C.place(F.bench('Bench',m,width=1.8),(0,-1.2,0))
    C.place(L.banker_lamp('Desk_lamp',m,height=.5),(-.85,.05,1.06))
    C.place(F.books('Counter_books',m),(.18,.05,1.06))


def component_plants(m):
    C.place(D.pedestal('Flower_pedestal',m,height=.9,width=.5),(-.6,.25,0))
    C.place(F.planter('Tall_flowers',m,width=.85,height=.95,seed=15),(-.6,.25,.9))
    C.place(F.planter('Small_flowers',m,width=.62,height=.72,seed=12),(.6,-.15,0))
    C.place(F.planter('Medium_flowers',m,width=.75,height=.86,seed=31),(1.25,.55,0))


def component_paintings(m):
    C.place(F.painting('City_mesh_art',m,width=1.55,height=1.3),(0,0,.3))
    C.place(F.painting('Mountain_mesh_art',m,width=1.25,height=1.0,style='landscape'),(1.65,.5,0),-.15)


def component_lights(m):
    C.place(L.wall_lantern('Faceted_lantern',m,height=1.0),(-1.2,.1,1.1))
    C.place(L.banker_lamp('Green_banker_lamp',m,height=.9,width=.75),(0,0,0))
    C.place(L.pendant_lamp('Polygon_pendant',m,drop=1.15,radius=.43),(1.35,.1,1.7))


COMPONENTS=[('01_wall',component_wall),('02_stairs',component_stair),('03_railings',component_rails),
            ('04_bookshelf',component_bookshelf),('05_table_chairs',component_table),('06_counter_bench',component_counter),
            ('07_plants',component_plants),('08_paintings',component_paintings),('09_lights',component_lights)]


def render_components(output,samples=48,resolution=(1000,800),render=True):
    output=Path(output)/'components'
    output.mkdir(parents=True,exist_ok=True)
    for name,builder in COMPONENTS:
        R.reset_scene();m=C.palette();builder(m)
        R.studio_rig(height=4.5,with_sun=False)
        R.configure(samples=samples)
        R.camera_fit(location=(7,-12,7.4),target=(0,0,1.1),resolution=resolution,margin=1.17)
        R.save_and_render(output/(name+'.blend'),output/(name+'.png'),render)
        stats=C.mesh_statistics()
        (output/(name+'_stats.json')).write_text(json.dumps(stats,indent=2))
        print('COMPONENT_DONE',name,stats,flush=True)
