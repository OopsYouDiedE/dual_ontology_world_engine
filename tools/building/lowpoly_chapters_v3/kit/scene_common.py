"""Shared one-room shell, prefab placement and presentation contract.

All placement coordinates are Blender Z-up metres. Interior Y runs from 0
at the cutaway/front to depth at the rear. Exact legacy dimensions are not
authoritative for these visual candidates; source IDs remain metadata only.
"""
import hashlib
import json
import math
import bpy
from . import core as C, architecture as A, flooring as T
from . import openings as O, variants as V, furniture as F, lighting as L
from .export_godot import tag_component, make_asset_id


def hide_tree(root):
    for ob in [root]+list(root.children_recursive):
        ob.hide_render=True
        ob['exclude_frame']=True
    root['preview_visible']=False


def room_palette(base, theme='walnut'):
    mats=dict(base)
    # The structural system is shared, while material roles stay explicit.
    # Latest art direction locks every room to the approved walnut palette.
    # Theme identifies function only; it must not recolour walls or doors.
    return mats


class RoomBuilder:
    def __init__(self,spec,mats):
        self.spec=spec
        self.id=spec['id']
        self.width=float(spec.get('width',8))
        self.depth=float(spec.get('depth',7))
        self.height=float(spec.get('height',3.7))
        self.theme=spec.get('theme','walnut')
        self.mats=room_palette(mats,self.theme)
        self.root=C.group(self.id)
        self.root['display_name']=spec.get('name',self.id)
        self.root['source_ids_json']=json.dumps(spec.get('source_ids',[]),ensure_ascii=False)
        self.static=C.group(self.id+'_Architecture',parent=self.root)
        self.fixtures=C.group(self.id+'_Components',parent=self.root)
        self.components=[]
        self.ports=[]
        self._counts={}

    def add(self,kind,factory,position=(0,0,0),rotation=0,params=None,
            interactive=True,name=None,anchors=None,boxes=None):
        """Create a reusable component from an independent generator."""
        params=dict(params or {})
        self._counts[kind]=self._counts.get(kind,0)+1
        name=name or '%s_%s_%02d'%(self.id,kind,self._counts[kind])
        root=factory(name,self.mats,**params)
        root.parent=self.fixtures
        C.place(root,position,rotation)
        asset_params=dict(params,material_theme=self.theme)
        tag_component(root,make_asset_id(kind,asset_params),kind,asset_params,
                      instance_id=name,interactive=interactive,anchors=anchors,
                      collision_boxes=boxes)
        self.components.append(root)
        return root

    def add_existing(self,root,kind,params=None,interactive=True,anchors=None):
        root.parent=self.fixtures
        p=dict(params or {},material_theme=self.theme)
        tag_component(root,make_asset_id(kind,p),kind,p,
                      instance_id=root.name.replace('.','_'),interactive=interactive,anchors=anchors)
        self.components.append(root)
        return root

    def table(self,pos,rotation=0,width=1.6,depth=.85,height=.78,shape='rect'):
        return self.add('table',F.table,pos,rotation,
          dict(width=width,depth=depth,height=height,shape=shape),boxes=[
            dict(name='TableTop',center=[0,0,height-.04],size=[width,depth,.08])])

    def chair(self,pos,rotation=0,arms=False):
        return self.add('chair',F.chair,pos,rotation,
          dict(width=.56,depth=.56,height=1.02,seat_height=.46,arms=arms),boxes=[
            dict(name='Seat',center=[0,0,.42],size=[.56,.56,.08])])

    def bench(self,pos,rotation=0,width=1.8):
        return self.add('bench',F.bench,pos,rotation,dict(width=width,depth=.6,height=1.02,seat_height=.46))

    def shelf(self,pos,rotation=0,width=1.8,height=2.6,fill=.65,seed=7):
        return self.add('bookshelf',F.bookshelf,pos,rotation,
          dict(width=width,depth=.43,height=height,shelves=4,bays=2,fill=fill,seed=seed))

    def plant(self,pos,rotation=0,width=.58,height=.74,seed=7):
        return self.add('planter',F.planter,pos,rotation,
          dict(width=width,height=height,flower_count=6,seed=seed),False)

    def lamp(self,pos,rotation=0,kind='banker'):
        if kind=='wall':
            return self.add('wall_lantern',L.wall_lantern,pos,rotation,
               dict(height=.7,light_energy=28),False)
        if kind=='pendant':
            return self.add('pendant_lamp',L.pendant_lamp,pos,rotation,
               dict(drop=.75,radius=.34,light_energy=40),False)
        return self.add('banker_lamp',L.banker_lamp,pos,rotation,
               dict(height=.55,width=.43,light_energy=16),False)

    def shell(self,doors=None,windows=None,floor='mosaic',tile=.8,cutaway=True):
        """Closed semantic room, with an explicit presentation cutaway shell.

        Door/window center is X on rear/front walls and Y on side walls.
        Full near walls are retained as a separate hidden prefab. The cutaway
        shows no neighboring room and does not invent its geometry.
        """
        w,d,h=self.width,self.depth,self.height
        doors=list(doors if doors is not None else self.spec.get('doors',[
          dict(wall='rear',center=-w*.28,width=1.15,height=2.35)]))
        windows=list(windows if windows is not None else self.spec.get('windows',[
          dict(wall='rear',center=w*.22,width=1.65,height=min(2.25,h-1.1),bottom=.85,shape='arch')]))
        if floor=='plank':
            C.box(self.id+'_Foundation',(0,d/2,-.15),(w,d,.3),self.mats['stone_dark'],.025,self.static)
            batches=[T.Batch() for _ in range(3)]
            cols=math.ceil(w/.28)
            for ix in range(cols):
                x0=-w/2+ix*w/cols+.006;x1=-w/2+(ix+1)*w/cols-.006
                step=1.35
                for iy in range(-1,math.ceil(d/step)+1):
                    y0=max(0,iy*step+(ix%2)*step*.5)+.006
                    y1=min(d,(iy+1)*step+(ix%2)*step*.5)-.006
                    if y1>y0:batches[(ix+iy)%3].prism([(x0,y0),(x1,y0),(x1,y1),(x0,y1)],-.012,0)
            for b,key in zip(batches,('wood','wood_light','wall')):
                b.finish(self.id+'_Parquet_'+key,self.mats[key],self.static)
        else:
            f=T.tiled_floor(self.id+'_Floor',self.mats,width=w,depth=d,
                tile=tile,cut=min(.11,tile*.16),border=.38,seed=self.spec.get('seed',7),parent=self.static)
            C.place(f,(0,d/2,-.04))
        wall_info={
          'rear':([(-w/2,d+.14,0),(w/2,d+.14,0)],w,lambda c:c+w/2,lambda c,z:(c,d,z),0),
          'left':([(-w/2-.14,0,0),(-w/2-.14,d,0)],d,lambda c:c,lambda c,z:(-w/2,c,z),math.pi/2),
          'right':([(w/2+.14,d,0),(w/2+.14,0,0)],d,lambda c:d-c,lambda c,z:(w/2,c,z),-math.pi/2),
          'front':([(w/2,-.14,0),(-w/2,-.14,0)],w,lambda c:w/2-c,lambda c,z:(c,0,z),math.pi),
        }
        hidden=C.group(self.id+'_Complete_Near_Shell')
        for side,(points,length,to_center,to_pos,yaw) in wall_info.items():
            ops=[]
            for ob in [*doors,*windows]:
                if ob.get('wall','rear')!=side:continue
                op=dict(center=to_center(ob['center']),width=ob['width'],height=ob['height'],bottom=ob.get('bottom',0))
                if ob.get('shape')=='arch':op.update(shape='arch',rise=ob.get('rise',ob['width']/2),segments=10)
                ops.append(op)
            is_cut=cutaway and side in ('right','front')
            A.wall_path(self.id+'_'+side+'_Wall',points,h,.28,self.mats,openings=ops,
                        panel_height=1.02,parent=hidden if is_cut else self.static)
            if is_cut:
                # Floors keep a restrained cut edge; doors remain opaque.
                low_ops=[dict(o,height=.32,bottom=0) for o in ops if o.get('bottom',0)<.3]
                A.wall_path(self.id+'_'+side+'_Cutedge',points,.32,.28,self.mats,
                            openings=low_ops,panel_height=.20,parent=self.static)
        self.add_existing(hidden,'complete_shell',dict(room_id=self.id,width=w,depth=d,height=h),False)
        hide_tree(hidden)
        for i,dr in enumerate(doors):
            side=dr.get('wall','rear');data=wall_info[side]
            double=dr.get('double',dr['width']>1.6)
            params=dict(width=dr['width'],height=dr['height'],double=double)
            if dr.get('shape')=='arch':
                params['rise']=dr.get('rise',dr['width']/2)
                factory=V.arched_closed_door
            else:factory=O.closed_door
            door=self.add('door',factory,data[3](dr['center'],dr.get('bottom',0)),data[4],params,
                          name=self.id+'_Door_%02d'%i)
            door['destination_scene_hint']=dr.get('to','')
            door['source_portal_id']=dr.get('source_id','')
            if side=='front' or (side=='right' and dr.get('preview_hide',True)):
                hide_tree(door)
            self.ports.append(dict(id=door['instance_id'],wall=side,position=list(door.location),
                                   destination=dr.get('to',''),source_id=dr.get('source_id','')))
        for i,wi in enumerate(windows):
            side=wi.get('wall','rear');data=wall_info[side]
            params=dict(width=wi['width'],height=wi['height'],cols=wi.get('cols',3),rows=wi.get('rows',4))
            if wi.get('shape','arch')=='arch':
                params['rise']=wi.get('rise',wi['width']/2)
                factory=O.arched_window
            else:factory=V.rectangular_window
            root=self.add('window',factory,data[3](wi['center'],wi.get('bottom',.85)),data[4],params,False)
            if cutaway and side in ('right','front'):hide_tree(root)
        return self

    def finish(self,camera=None,lighting=None):
        w,d,h=self.width,self.depth,self.height
        default_camera=dict(location=[w*1.05,-d*1.25,max(w,d)*1.15],target=[0,d*.5,h*.35],projection='ORTHO',margin=1.09)
        return dict(root=self.root,components=self.components,static_objects=[self.static],
          width=w,depth=d,height=h,ports=self.ports,spec=self.spec,
          camera=camera or default_camera,lighting=lighting or {},
          bounds_blender=[[-w/2-.5,-.5,-.3],[w/2+.5,d+.5,h+.3]])
