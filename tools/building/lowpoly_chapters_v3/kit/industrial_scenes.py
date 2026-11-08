"""Six independent chapter-two industrial room candidates.

Concepts precede these assemblies. Door placement is local composition only;
teleport destinations and gameplay state belong to Main/Core and remain unset.
"""
import math
from . import core as C, furniture as F, special_props as S, stair_exact as E
from . import industrial_props as I
from .scene_common import RoomBuilder,hide_tree


def _ancestors(ob):
    current=ob
    while current:
        yield current
        current=current.parent


def _shell(b):
    b.mats=I.material_set(b.mats)
    b.shell(doors=b.spec.get('doors',[]),windows=[],floor='mosaic',tile=b.spec.get('tile',.9))
    hidden=next(c for c in b.components if c.get('component_kind')=='complete_shell')
    # Keep the shared structural path/opening generator, adapting its finish
    # to the concept's warm masonry rather than adding another wall system.
    for base in (b.static,hidden):
        for ob in base.children_recursive:
            if ob.type!='MESH' or not any('_Wall' in a.name or '_Cutedge' in a.name for a in _ancestors(ob)):continue
            if any(token in ob.name for token in ('LowerPanel','UpperPanel','Dado')):
                ob.hide_render=True;ob['export_exclude']=True;continue
            for slot in ob.material_slots:
                if slot.material==b.mats['wood_dark']:slot.material=b.mats['masonry_dark']
                elif slot.material==b.mats['wood_light']:slot.material=b.mats['masonry_light']
                else:slot.material=b.mats['masonry']
    w,d,h=b.width,b.depth,b.height
    walls={
      'rear':(((-w/2,d,0),0),w,lambda c:c+w/2),
      'left':(((-w/2,0,0),math.pi/2),d,lambda c:c),
      'right':(((w/2,d,0),-math.pi/2),d,lambda c:d-c),
      'front':(((w/2,0,0),math.pi),w,lambda c:w/2-c),
    }
    for side,((pos,yaw),length,convert) in walls.items():
        ops=[dict(center=convert(dr['center']),width=dr['width'],height=dr['height'],bottom=dr.get('bottom',0)) for dr in b.spec.get('doors',[]) if dr.get('wall')==side]
        parent=hidden if side in ('front','right') else b.static
        joint=I.masonry_joints(b.id+'_'+side+'_Masonry',b.mats,length,h,ops,parent=parent)
        C.place(joint,pos,yaw)
        if side in ('rear','left'):
            pier_positions=(0,length) if side=='rear' and b.spec['category']=='control' else (0,length*.5,length)
            for u in pier_positions:
                if any(abs(u-o['center'])<o['width']/2+.4 for o in ops):continue
                pier=C.group(b.id+'_Stone_pier',parent=joint)
                C.box('Pier',(u,-.10,h/2),(.28,.21,h),b.mats['masonry'],.015,pier)
                C.box('Pier_base',(u,-.14,.14),(.43,.30,.28),b.mats['masonry_light'],.012,pier)
                C.box('Pier_cap',(u,-.13,h-.13),(.48,.30,.26),b.mats['masonry_light'],.012,pier)
    hide_tree(hidden)


def _lamp(b,pos,yaw=0,energy=25):
    return b.add('caged_lamp',I.caged_lamp,pos,yaw,dict(height=.52,light_energy=energy),False)


def _pipe(b,nodes,radius=.09):
    return b.add('pipe_run',I.pipe_run,params=dict(nodes=nodes,radius=radius),interactive=False)


def _indicator(b,pos,yaw=0,kind='flood'):
    return b.add('indicator_board',I.indicator_board,pos,yaw,dict(width=.36,height=1.1,kind=kind),True)


def _grain_store(b):
    w,d,h=b.width,b.depth,b.height
    positions=[(-w/2+.62,d*.33,math.pi/2),(-w/2+.62,d*.72,math.pi/2),(-w*.22,d-.64,0)]
    for i,(x,y,yaw) in enumerate(positions):
        rack=b.add('grain_rack',I.storage_rack,(x,y,0),yaw,dict(width=2.6,depth=.85,height=2.55),True,
                   boxes=[dict(name='Shelf_lower',center=[0,0,.22],size=[2.72,.93,.11]),
                          dict(name='Shelf_upper',center=[0,0,2.55*.53],size=[2.72,.93,.11])])
        rack['source_furniture_id']='U01-rack%d'%i
    for x,y,n in ((w*.34,d*.25,2),(w*.40,d*.53,1),(-w*.27,d*.88,1)):
        b.add('grain_crates',S.crates,(x,y,0),0,dict(width=.85,depth=.70,height=.72,stack=n),True)
    b.add('handcart',I.handcart,(.85,d*.43,0),-.22,dict(width=1.1,length=1.6),True)
    b.add('platform_scale',I.platform_scale,(-.95,d*.24,0),0,dict(width=1.4,depth=1.1),True)
    for x,y in ((-w*.37,1.05),(w*.32,1.2)):
        b.add('barrel',I.barrel,(x,y,0),0,dict(radius=.38,height=.9),True)
    p=b.add('grain_pallet',I.pallet,(.15,d*.75,0),0,dict(width=1.55,depth=1.25),False)
    b.add('grain_sacks',S.sacks,(.15,d*.75,.23),0,dict(count=4),True)
    table=b.table((w*.31,d*.82,0),width=1.45,depth=.78)
    book=F.open_book(b.id+'_Inventory_ledger',b.mats,width=.48,depth=.34,parent=table);book.location=(0,-.03,.81)
    b.chair((w*.31,d*.82-.85,0),math.pi)
    for x,y,yaw in ((-w/2+.04,d*.23,math.pi/2),(-w/2+.04,d*.70,math.pi/2),(-w*.08,d-.03,0),(w*.37,d-.03,0)):
        _lamp(b,(x,y,2.62),yaw,30)
    _indicator(b,(w*.13,d-.07,1.05))
    b.add('heavy_lock',I.heavy_lock,(w*.25,d-.13,1.14),0,dict(width=.24),True)
    # The rear timber tie and diagonal braces are real structural meshes.
    C.box(b.id+'_Rear_tie',(0,d-.23,h-.18),(w,.18,.26),b.mats['wood'],.02,b.static)
    for x in (-w*.40,-w*.12,w*.16,w*.41):
        C.beam(b.id+'_Timber_knee',(x,d-.18,h-.75),(x+.35,d-.18,h-.20),.14,.14,b.mats['wood'],b.static,.01)


def _service_corridor(b):
    w,d,h=b.width,b.depth,b.height
    # Both runs clear the service door's upper frame; no pipe crosses a leaf.
    for z in (2.80,3.16):
        _pipe(b,[(-w/2+.19,.55,z),(-w/2+.19,d-1.1,z),(-w*.2,d-.23,z)],.09)
    for y in (1.4,4.2,7.0):_lamp(b,(-w/2+.02,y,2.35),math.pi/2,23)
    _indicator(b,(w*.25,d-.06,.98))
    _indicator(b,(-w/2+.035,d*.63,1.0),math.pi/2,'warning')
    b.add('interlock_handle',I.interlock_handle,(-w/2+.05,d*.39,1.25),math.pi/2,dict(radius=.24),True)
    b.add('service_cabinet',S.cabinet,(w*.30,d*.65,0),0,dict(width=.85,depth=.4,height=1.65),True)
    b.add('drain',I.drain_grate,(-w*.34,d*.49,.025),0,dict(width=.28,length=d*.8),False)
    b.bench((w*.23,d*.28,0),-math.pi/2,width=1.2)


def _control_room(b):
    w,d,h=b.width,b.depth,b.height
    C.box(b.id+'_Reactor_dais',(-w*.29,d*.74,.13),(1.92,1.9,.26),b.mats['masonry_light'],.025,b.static)
    b.add('reaction_vessel',I.reaction_vessel,(-w*.29,d*.74,.26),0,dict(radius=.70,height=2.6),True)
    b.add('control_console',I.control_console,(w*.06,d*.72,0),0,dict(width=2.25,depth=.95,height=1.30),True,
          boxes=[dict(name='Cabinet_body',center=[0,0,.42],size=[2.25,.95,.7])])
    b.add('piping_schematic',I.schematic,(w*.02,d-.08,2.47),0,dict(width=2.05,height=1.02),False)
    b.chair((w*.08,d*.72-1.0,0),math.pi)
    _pipe(b,[(-w*.39,d*.70,.85),(-w*.44,d*.70,.85),(-w*.44,d-.22,.85),(-w*.44,d-.22,h-.4),(w*.18,d-.22,h-.4),(w*.18,d-.22,.6)],.115)
    _pipe(b,[(-w*.23,d*.74,.75),(-w*.12,d*.74,.75),(-w*.12,d*.90,.75)],.13)
    for x in (-w*.45,-w*.18,w*.22):_lamp(b,(x,d-.02,2.65),0,27)
    _indicator(b,(w*.22,d-.08,1.03))
    b.add('service_chest',S.chest,(w*.35,d*.25,0),0,dict(width=1.2,depth=.6,height=.55),True)


def _valve_gallery(b):
    w,d,h=b.width,b.depth,b.height
    for z in (1.55,2.10,2.65):
        _pipe(b,[(-w*.43,d-.25,z),(-w*.43,d-.25,z-.15),(-w*.36,d-.25,z-.15),(w*.38,d-.25,z-.15)],.105)
    for i,x in enumerate((-w*.30,0,w*.30)):
        b.add('valve_station',I.valve_station,(x,d-.95,0),0,dict(width=1.22,height=1.35),True,
              boxes=[dict(name='Pump_body',center=[0,0,.48],size=[1.0,.6,.7])])
        _pipe(b,[(x-.43,d-.95,.60),(x-.65,d-.95,.60),(x-.65,d-.25,.60),(x-.65,d-.25,1.4)],.115)
        _lamp(b,(x,d-.025,h-.45),0,23)
    b.add('long_drain',I.drain_grate,(0,d-1.7,.02),math.pi/2,dict(width=.34,length=w*.88),False)
    _indicator(b,(-w/2+.04,d*.58,1.0),math.pi/2)
    b.add('maintenance_crate',S.crates,(-w*.36,.9,0),0,dict(width=.75,depth=.6,height=.65,stack=1),True)


def _inspection_ramp(b):
    w,d,h=b.width,b.depth,b.height
    width=w-.9;rise=b.spec.get('ramp_rise',1.4);length=d-.2;landing=1.1
    ramp=I.ramp_module(b.id+'_Ramp',b.mats,width,length,rise,landing,parent=b.static)
    ramp.location=(0,.10,0)
    _pipe(b,[(-w/2+.18,.45,1.1),(-w/2+.18,d*.43,1.1),(-w/2+.18,d*.43,2.55),(-w/2+.18,d-.3,2.55)],.10)
    for y in (1.3,d*.50,d-.8):
        z=2.65 if y<d*.65 else 3.35
        _lamp(b,(-w/2+.03,y,z),math.pi/2,25)
    _indicator(b,(-w/2+.03,d*.40,1.25),math.pi/2)
    # Side grate follows the true grade as separate sloped mesh sections.
    run=length-2*landing;angle=math.atan2(rise,run)
    grate=I.drain_grate(b.id+'_Ramp_side_drain',b.mats,.22,math.sqrt(run*run+rise*rise),parent=b.static)
    grate.location=(-width/2-.14,.10+length/2,rise/2+.025);grate.rotation_euler[0]=angle


def _service_stair(b):
    w,d,h=b.width,b.depth,b.height
    x=-w/2+1.16;rise=2.6;risers=14;start=(x,.68,0);end=(x,d-1.42,rise);width=1.85
    E.straight_flight(b.id+'_Exact_service_flight',start,end,width,b.mats,risers=risers,parent=b.static)
    C.box(b.id+'_Upper_landing',(0,d-.71,rise-.13),(w-.12,1.42,.26),b.mats['masonry_light'],.015,b.static)
    flight=E.flight_spec(start,end,width,risers)
    post_points=[a['parent']['right'] for a in flight['step_top_anchors']]+[flight['railing_baselines']['right']['parent'][-1]]
    I.simple_rail(b.id+'_Stair_open_side',b.mats,[(x+width/2,.68,rise/risers),(x+width/2,d-1.42,rise)],.88,.35,b.static,post_points=post_points)
    I.simple_rail(b.id+'_Landing_front',b.mats,[(x+width/2,d-1.42,rise),(w/2-.16,d-1.42,rise)],.88,.30,b.static)
    C.beam(b.id+'_Wall_handrail',(-w/2+.15,.68,rise/risers+.85),(-w/2+.15,d-1.42,rise+.85),.065,.065,b.mats['wood'],b.static,.006)
    b.add('maintenance_cabinet',S.cabinet,(.2,d-.47,0),0,dict(width=1.05,depth=.5,height=1.65),True)
    _pipe(b,[(w/2-.30,d-.22,.30),(w/2-.30,d-.22,4.6),(.45,d-.22,4.6)],.09)
    _lamp(b,(-w/2+.035,d*.43,2.9),math.pi/2,28)
    _lamp(b,(0,d-.03,4.58),0,30)
    _indicator(b,(.85,d-.06,.98))


BUILDERS={'grain_store':_grain_store,'corridor':_service_corridor,'control':_control_room,
          'valve_gallery':_valve_gallery,'ramp':_inspection_ramp,'stair':_service_stair}


def build_industrial(spec,mats):
    if spec['category'] not in BUILDERS:raise ValueError('Unknown industrial category: '+spec['category'])
    b=RoomBuilder(spec,mats);_shell(b);BUILDERS[spec['category']](b)
    b.root['binding_status']='unbound_candidate';b.root['door_transition']='teleport_placeholder_no_destination_bound'
    b.root['source_room_id_known']=spec.get('source_room_id') is not None
    b.root['concept_reference']=spec.get('concept','')
    b.root['palette_policy']='warm wood / beige stone / brass / warm lamps'
    camera=dict(location=[b.width*1.05,-b.depth*1.15,max(b.width,b.depth)*1.02],
                target=[0,b.depth*.50,b.height*.39],projection='ORTHO',margin=1.10)
    return b.finish(camera=camera,lighting=dict(with_sun=False,description='Underground room: warm practical lamps plus broad fill; no fabricated sunlight window projection.'))


build_scene=build_industrial


def main():
    """Reusable individual-room authoring command for this component family."""
    import argparse,json
    from pathlib import Path
    import bpy
    from . import rendering as R
    parser=argparse.ArgumentParser(description='Build and render one independent industrial room from the permanent source specifications')
    parser.add_argument('--scene',default='b2_control')
    parser.add_argument('--output',default='output/industrial_local')
    parser.add_argument('--samples',type=int,default=32)
    parser.add_argument('--width',type=int,default=1200)
    parser.add_argument('--height',type=int,default=900)
    parser.add_argument('--no-render',action='store_true')
    args=parser.parse_args()
    source=Path(__file__).resolve().parents[1]
    specs=json.loads((source/'specs/industrial.json').read_text(encoding='utf-8'))
    spec=next((s for s in specs if s['id']==args.scene),None)
    if spec is None:raise ValueError('Unknown industrial scene: '+args.scene)
    from generate_chapters import light_room
    R.reset_scene();mats=C.palette();built=build_industrial(spec,mats)
    light_room(built,mats);R.configure(args.samples)
    bpy.context.scene.view_settings.exposure=.05
    camera=built['camera']
    R.camera_fit(camera['location'],camera['target'],(args.width,args.height),camera['margin'])
    bpy.context.scene['lighting_notes']=built['lighting']['description']+' Cycles diffuse GI, black world, restrained compositor glow.'
    out=Path(args.output).resolve();out.mkdir(parents=True,exist_ok=True)
    paths=R.save_and_render(out/(args.scene+'.blend'),out/(args.scene+'.png'),not args.no_render)
    report={'scene_id':args.scene,'blender_version':bpy.app.version_string,'paths':paths,
            'rendered':not args.no_render,'mesh_statistics':C.mesh_statistics(),
            'source_room_id':spec.get('source_room_id'),'binding_status':'unbound_candidate',
            'concept':spec.get('concept'),'lighting':built['lighting'],'bounds_blender':built['bounds_blender']}
    (out/(args.scene+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False),flush=True)


if __name__=='__main__':main()
