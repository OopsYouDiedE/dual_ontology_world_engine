"""Shared camera and lighting rig; kept separate from geometry generators."""
import math
from pathlib import Path
import bpy
from mathutils import Vector
from .core import box


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    return scene


def aim(obj,target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()


def area(name,location,target,power,size,color=(1,.87,.69),size_y=None):
    data=bpy.data.lights.new(name,'AREA')
    data.energy=power
    data.color=color
    data.shape='DISK' if size_y is None else 'RECTANGLE'
    data.size=size
    if size_y is not None:data.size_y=size_y
    obj=bpy.data.objects.new(name,data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location=location
    aim(obj,target)
    return obj


def sun(name='Sun_through_window',direction=(.24,-1,-.78),strength=3.4,angle=.6):
    data=bpy.data.lights.new(name,'SUN')
    data.energy=strength
    data.color=(1.0,.74,.42)
    data.angle=math.radians(angle)
    obj=bpy.data.objects.new(name,data)
    bpy.context.scene.collection.objects.link(obj)
    obj.rotation_euler=Vector(direction).to_track_quat('-Z','Y').to_euler()
    obj['ray_direction']=list(direction)
    obj['angle_degrees']=angle
    return obj


def shadow_roof(name,mats,width,depth,height,location=(0,0,0)):
    """Invisible to camera, blocks sunlight that would enter the cutaway top.

    This is a deliberate Cycles presentation helper, not an exported asset.
    """
    x,y,z=location
    obj=box(name,(x,y,z+height+.09),(width+.9,depth+.9,.12),mats['wood_dark'],0)
    obj.visible_camera=False
    obj.visible_glossy=False
    obj['exclude_frame']=True
    obj['render_helper']='shadow_only_cutaway_ceiling'
    return obj


def studio_rig(room_size=8,height=4.5,with_sun=True,sun_direction=(.24,-1,-.78)):
    world=bpy.data.worlds.new('Pure_black_world')
    world.use_nodes=True
    bg=world.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value=(0,0,0,1)
    bg.inputs['Strength'].default_value=0
    bpy.context.scene.world=world
    area('Warm_large_key',(-3.5,-4.5,height-.3),(0,1,1.3),450,5.0,(1,.88,.73))
    area('Cool_soft_fill',(5,-2.8,3.7),(0,1,1.5),280,4.0,(.68,.79,1.0))
    area('Front_softbox',(0,-6,5),(0,0,1.5),160,4.5,(1,.92,.80))
    if with_sun:sun(direction=sun_direction)


def camera_fit(location=(11,-15,12),target=(0,0,1.8),resolution=(1440,1100),margin=1.12):
    data=bpy.data.cameras.new('Orthographic_Camera')
    data.type='ORTHO'
    obj=bpy.data.objects.new('Orthographic_Camera',data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location=location
    aim(obj,target)
    scene=bpy.context.scene
    scene.camera=obj
    scene.render.resolution_x=resolution[0]
    scene.render.resolution_y=resolution[1]
    scene.render.resolution_percentage=100
    bpy.context.view_layer.update()
    inv_rot=obj.rotation_euler.to_matrix().transposed()
    coords=[]
    for item in scene.objects:
        if item.type!='MESH' or item.hide_render or item.get('exclude_frame'):
            continue
        for p in item.bound_box:
            coords.append(inv_rot @ (item.matrix_world @ Vector(p)))
    if not coords:raise RuntimeError('No renderable mesh objects')
    xmin,xmax=min(p.x for p in coords),max(p.x for p in coords)
    ymin,ymax=min(p.y for p in coords),max(p.y for p in coords)
    current=inv_rot @ obj.location
    current.x=(xmin+xmax)/2
    current.y=(ymin+ymax)/2
    obj.location=obj.rotation_euler.to_matrix() @ current
    aspect=resolution[0]/resolution[1]
    # ortho_scale is horizontal width in landscape aspect.
    data.ortho_scale=margin*max(xmax-xmin,(ymax-ymin)*aspect)
    data.clip_end=200
    return obj


def configure(samples=64):
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.device='CPU'
    scene.cycles.samples=samples
    scene.cycles.use_denoising=True
    scene.cycles.use_adaptive_sampling=True
    scene.cycles.adaptive_threshold=.025
    scene.cycles.max_bounces=7
    scene.cycles.diffuse_bounces=4
    scene.cycles.glossy_bounces=3
    scene.cycles.transmission_bounces=4
    scene.cycles.transparent_max_bounces=12
    scene.cycles.seed=24
    scene.render.threads_mode='FIXED'
    scene.render.threads=8
    scene.render.film_transparent=False
    scene.render.image_settings.file_format='PNG'
    scene.render.image_settings.color_mode='RGB'
    scene.render.image_settings.color_depth='8'
    scene.view_settings.view_transform='AgX'
    try:scene.view_settings.look='AgX - Medium High Contrast'
    except TypeError:pass
    scene.view_settings.exposure=.7
    scene.view_settings.gamma=1
    scene.use_nodes=True
    nodes=scene.node_tree.nodes
    nodes.clear()
    src=nodes.new('CompositorNodeRLayers')
    glow=nodes.new('CompositorNodeGlare')
    glow.glare_type='FOG_GLOW'
    glow.quality='HIGH'
    if 'Threshold' in glow.inputs:
        # Blender 4.5 exposes these as sockets; old threshold RNA is stale.
        glow.inputs['Threshold'].default_value=1.0
        glow.inputs['Strength'].default_value=.08
        glow.inputs['Size'].default_value=.25
    else:
        glow.threshold=1.0
        glow.size=7
        glow.mix=-.92
    out=nodes.new('CompositorNodeComposite')
    scene.node_tree.links.new(src.outputs['Image'],glow.inputs['Image'])
    scene.node_tree.links.new(glow.outputs['Image'],out.inputs['Image'])
    for screen in bpy.data.screens:
        for area_view in screen.areas:
            if area_view.type=='VIEW_3D':
                area_view.spaces.active.shading.show_backface_culling=True
    scene['lighting_notes']='Black world; real SUN/window shadows; large area fill; fixture lights; Cycles diffuse GI; subtle compositor glow.'
    scene['room_isolation']='Only one room generated. Doors are opaque and closed.'


def save_and_render(blend_path,png_path,render=True):
    blend_path=Path(blend_path).resolve()
    png_path=Path(png_path).resolve()
    blend_path.parent.mkdir(parents=True,exist_ok=True)
    png_path.parent.mkdir(parents=True,exist_ok=True)
    bpy.context.scene.render.filepath=str(png_path)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path),check_existing=False)
    if render:
        bpy.ops.render.render(write_still=True)
    return str(blend_path),str(png_path)
