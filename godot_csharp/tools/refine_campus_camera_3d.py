"""Headless reproducible Corvo refinement with immutable source mesh/layout.

Run Blender -b -P this_script.py to rebuild both structural and material passes.
Use -- --materials-only to refine the saved production source without rebuilding.
Add --preview-only to either mode for a quick EEVEE inspection.
"""
import bpy, bmesh, json, os, math, hashlib, random, sys
from mathutils import Vector
import numpy as np
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
TEST=os.path.join(ROOT,'artifacts/map-optimization/camera-tests')
OUT=os.path.join(TEST,'refined'); os.makedirs(OUT,exist_ok=True)
SAVE=os.path.join(ROOT,'godot_csharp/AssetsSource/Internal/Props/MapOptimization/3D/campus_camera_refined_v1.blend')
os.makedirs(os.path.dirname(SAVE),exist_ok=True)
BASE=os.path.join(TEST,'camera_tests.blend')
def refine_material_pass():
    """World-metre shader-only pass on the existing structural production source."""
    import shutil
    bpy.ops.wm.open_mainfile(filepath=SAVE)
    scene=bpy.context.scene
    def record(ob):
        a=np.array([v.co[:] for v in ob.data.vertices],dtype=np.float64)
        bb=np.array([tuple(ob.matrix_world@Vector(v)) for v in ob.bound_box])
        return dict(vertices_sha256=hashlib.sha256(a.tobytes()).hexdigest(),matrix=[list(r) for r in ob.matrix_world],bounds=[bb.min(0).tolist(),bb.max(0).tolist()])
    before={o.name:record(o) for o in bpy.data.objects if o.type=='MESH'}
    src_hash=hashlib.sha256(open(SAVE,'rb').read()).hexdigest()
    for name in ['02_court_eye','05_south_25m']:
        old=os.path.join(OUT,name+'_final.png');preserved=os.path.join(OUT,name+'_structure_pass.png')
        if os.path.exists(old) and not os.path.exists(preserved):shutil.copy2(old,preserved)
        first=os.path.join(OUT,name+'_material_pass_first.png')
        if '--fix-gable-caps' in sys.argv and os.path.exists(old) and not os.path.exists(first):shutil.copy2(old,first)
    changed=[]
    for mat in bpy.data.materials:
        if not mat.use_nodes:continue
        p=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if p is None:continue
        name=mat.name
        kind=('ground' if name=='地面' else 'paving' if name in ('铺石','碎石路','码头') else 'basalt' if name in ('玄武岩','台基石') else 'lime' if name in ('白灰墙','白塔') else 'roof_red' if name=='红瓦' else 'roof_slate' if name=='石板瓦' else None)
        if not kind:
            textures=[n for n in mat.node_tree.nodes if n.type in ('TEX_NOISE','TEX_BRICK') and not n.inputs['Vector'].is_linked]
            if textures:
                world=mat.node_tree.nodes.new('ShaderNodeNewGeometry')
                for texture in textures:mat.node_tree.links.new(world.outputs['Position'],texture.inputs['Vector'])
                changed.append(dict(material=name,kind='existing_fine_detail',coordinates='Geometry.Position, world metres'))
            continue
        nodes=mat.node_tree.nodes;links=mat.node_tree.links
        for node in list(nodes):
            if node!=p and node.type!='OUTPUT_MATERIAL':nodes.remove(node)
        def node(typ):return nodes.new(typ)
        def mathnode(op,a,b=None):
            n=node('ShaderNodeMath');n.operation=op
            if hasattr(a,'node'):links.new(a,n.inputs[0])
            else:n.inputs[0].default_value=a
            if b is not None:
                if hasattr(b,'node'):links.new(b,n.inputs[1])
                else:n.inputs[1].default_value=b
            return n.outputs[0]
        geom=node('ShaderNodeNewGeometry');pos=node('ShaderNodeSeparateXYZ');links.new(geom.outputs['Position'],pos.inputs[0])
        norm=node('ShaderNodeSeparateXYZ');links.new(geom.outputs['Normal'],norm.inputs[0])
        def noise(scale,detail=2):
            n=node('ShaderNodeTexNoise');n.inputs['Scale'].default_value=scale;n.inputs['Detail'].default_value=detail;links.new(geom.outputs['Position'],n.inputs['Vector']);return n
        def ramp(fac,c0,c1):
            n=node('ShaderNodeValToRGB');n.color_ramp.elements[0].color=(*c0,1);n.color_ramp.elements[1].color=(*c1,1);links.new(fac,n.inputs['Fac']);return n.outputs['Color']
        def bump(fac,strength,distance):
            n=node('ShaderNodeBump');n.inputs['Strength'].default_value=strength;n.inputs['Distance'].default_value=distance;links.new(fac,n.inputs['Height']);links.new(n.outputs['Normal'],p.inputs['Normal'])
        def rough(fac,lo,hi):
            n=node('ShaderNodeMapRange');n.inputs['To Min'].default_value=lo;n.inputs['To Max'].default_value=hi;links.new(fac,n.inputs['Value']);links.new(n.outputs['Result'],p.inputs['Roughness'])
        fine=noise(32,2)
        if kind=='ground':
            broad=noise(.015,4);mid=noise(.15,2)
            fac=mathnode('ADD',mathnode('MULTIPLY',broad.outputs['Fac'],.73),mathnode('MULTIPLY',mid.outputs['Fac'],.27))
            color=ramp(fac,(.085,.145,.046),(.165,.225,.084))
            steep=node('ShaderNodeMapRange');steep.inputs['From Min'].default_value=.72;steep.inputs['From Max'].default_value=.95;links.new(norm.outputs['Z'],steep.inputs['Value'])
            rocks=ramp(mid.outputs['Fac'],(.12,.112,.088),(.205,.187,.135))
            mix=node('ShaderNodeMixRGB');mix.blend_type='MIX';links.new(steep.outputs['Result'],mix.inputs[0]);links.new(rocks,mix.inputs[1]);links.new(color,mix.inputs[2])
            height=node('ShaderNodeMapRange');height.inputs['From Min'].default_value=80;height.inputs['From Max'].default_value=260;links.new(pos.outputs['Z'],height.inputs['Value'])
            high=node('ShaderNodeMixRGB');links.new(mathnode('MULTIPLY',height.outputs['Result'],.4),high.inputs[0]);links.new(mix.outputs[0],high.inputs[1]);high.inputs[2].default_value=(.16,.16,.076,1)
            links.new(high.outputs[0],p.inputs['Base Color']);bump(fine.outputs['Fac'],.15,.035);rough(fine.outputs['Fac'],.88,.98)
        elif kind=='lime':
            base=(.76,.735,.665) if name=='白灰墙' else (.84,.835,.79)
            links.new(ramp(fine.outputs['Fac'],tuple(v*.955 for v in base),tuple(v*1.025 for v in base)),p.inputs['Base Color']);bump(fine.outputs['Fac'],.07,.008);rough(fine.outputs['Fac'],.82,.96)
        else:
            coord=node('ShaderNodeCombineXYZ')
            if kind=='basalt':
                # Select X/Z or Y/Z projection from the dominant horizontal face normal.
                dom=mathnode('GREATER_THAN',mathnode('ABSOLUTE',norm.outputs['X']),mathnode('ABSOLUTE',norm.outputs['Y']))
                tang=mathnode('ADD',mathnode('MULTIPLY',dom,pos.outputs['Y']),mathnode('MULTIPLY',mathnode('SUBTRACT',1,dom),pos.outputs['X']))
                links.new(tang,coord.inputs['X']);links.new(pos.outputs['Z'],coord.inputs['Y'])
                c0,c1,mortar=(.218,.21,.188),(.252,.24,.214),(.191,.185,.169);w,h,gap=.65,.25,.006
            else:
                links.new(pos.outputs['X'],coord.inputs['X']);links.new(pos.outputs['Y'],coord.inputs['Y'])
                if kind=='paving':c0,c1,mortar=(.36,.345,.31),(.46,.445,.405),(.305,.295,.271);w,h,gap=.52,.38,.007
                elif kind=='roof_red':c0,c1,mortar=(.38,.145,.09),(.47,.21,.13),(.335,.14,.09);w,h,gap=.28,.24,.003
                else:c0,c1,mortar=(.145,.16,.16),(.19,.205,.20),(.135,.15,.15);w,h,gap=.3,.26,.003
            brick=node('ShaderNodeTexBrick');links.new(coord.outputs[0],brick.inputs['Vector']);brick.inputs['Scale'].default_value=1;brick.inputs['Brick Width'].default_value=w;brick.inputs['Row Height'].default_value=h;brick.inputs['Mortar Size'].default_value=gap;brick.inputs['Mortar Smooth'].default_value=.001;brick.inputs['Color1'].default_value=(*c0,1);brick.inputs['Color2'].default_value=(*c1,1);brick.inputs['Mortar'].default_value=(*mortar,1);brick.offset=.5;brick.offset_frequency=2
            color=brick.outputs['Color'];joints=brick.outputs['Fac']
            if kind in ('roof_red','roof_slate'):
                # Roof meshes contain both slopes and vertical triangular closure faces.
                # Preserve real tile repeats on slopes; a wall projection handles gable caps.
                slope=mathnode('GREATER_THAN',mathnode('ABSOLUTE',norm.outputs['Z']),.2)
                if kind=='roof_red':
                    wall_color=ramp(fine.outputs['Fac'],(.726,.702,.636),(.779,.753,.682))
                    wall_joints=mathnode('MULTIPLY',fine.outputs['Fac'],.035)
                else:
                    wall_coord=node('ShaderNodeCombineXYZ')
                    dom=mathnode('GREATER_THAN',mathnode('ABSOLUTE',norm.outputs['X']),mathnode('ABSOLUTE',norm.outputs['Y']))
                    tang=mathnode('ADD',mathnode('MULTIPLY',dom,pos.outputs['Y']),mathnode('MULTIPLY',mathnode('SUBTRACT',1,dom),pos.outputs['X']))
                    links.new(tang,wall_coord.inputs['X']);links.new(pos.outputs['Z'],wall_coord.inputs['Y'])
                    wall=node('ShaderNodeTexBrick');links.new(wall_coord.outputs[0],wall.inputs['Vector']);wall.inputs['Scale'].default_value=1;wall.inputs['Brick Width'].default_value=.65;wall.inputs['Row Height'].default_value=.25;wall.inputs['Mortar Size'].default_value=.006;wall.inputs['Mortar Smooth'].default_value=.001
                    wall.inputs['Color1'].default_value=(.218,.21,.188,1);wall.inputs['Color2'].default_value=(.252,.24,.214,1);wall.inputs['Mortar'].default_value=(.191,.185,.169,1);wall.offset=.5;wall.offset_frequency=2
                    wall_color=wall.outputs['Color'];wall_joints=wall.outputs['Fac']
                mix=node('ShaderNodeMixRGB');links.new(slope,mix.inputs[0]);links.new(wall_color,mix.inputs[1]);links.new(brick.outputs['Color'],mix.inputs[2]);color=mix.outputs[0]
                joints=mathnode('ADD',mathnode('MULTIPLY',slope,brick.outputs['Fac']),mathnode('MULTIPLY',mathnode('SUBTRACT',1,slope),wall_joints))
            links.new(color,p.inputs['Base Color']);bump(mathnode('ADD',mathnode('MULTIPLY',joints,-.7),mathnode('MULTIPLY',fine.outputs['Fac'],.15)),.28,.018 if kind=='basalt' else .012);rough(fine.outputs['Fac'],.78,.95)
        changed.append(dict(material=name,kind=kind,coordinates='Geometry.Position, world metres'))
    sun=next((o for o in bpy.data.objects if o.type=='LIGHT' and o.data.type=='SUN'),None)
    if sun:sun.data.color=(1,.93,.82);sun.data.energy=2.4
    bg=next((n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND'),None)
    if bg:bg.inputs['Strength'].default_value=.24
    scene.view_settings.exposure=-.1
    after={o.name:record(o) for o in bpy.data.objects if o.type=='MESH'}
    assert before==after,'Material pass altered mesh geometry/layout'
    scene.render.engine='BLENDER_EEVEE_NEXT';scene.eevee.taa_render_samples=32;scene.render.resolution_x=1280;scene.render.resolution_y=720
    scene.camera=bpy.data.objects['02_court_eye'];scene.render.filepath=os.path.join(OUT,'02_court_eye_material_preview.png');bpy.ops.render.render(write_still=True)
    if '--preview-only' not in sys.argv:
        scene.render.engine='CYCLES';scene.cycles.samples=64;scene.cycles.device='CPU';scene.cycles.use_denoising=True;scene.cycles.use_adaptive_sampling=True;scene.render.resolution_x=1920;scene.render.resolution_y=1080
        for name in ['02_court_eye','05_south_25m']:
            scene.camera=bpy.data.objects[name];scene.render.filepath=os.path.join(OUT,name+'_final.png');bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=SAVE)
    json.dump(dict(source_blend_sha256=src_hash,final_blend_sha256=hashlib.sha256(open(SAVE,'rb').read()).hexdigest(),mesh_count=len(before),all_mesh_geometry_unchanged=True,geometry_before=before,geometry_after=after,materials=changed,engine=scene.render.engine,samples=scene.cycles.samples,resolution=[scene.render.resolution_x,scene.render.resolution_y]),open(os.path.join(OUT,'material_pass_evidence.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=2)
    manifest_path=os.path.join(OUT,'delivery_manifest.json')
    manifest=json.load(open(manifest_path,encoding='utf-8')) if os.path.exists(manifest_path) else {}
    manifest.update(blend_sha256=hashlib.sha256(open(SAVE,'rb').read()).hexdigest(),production_script_sha256=hashlib.sha256(open(__file__,'rb').read()).hexdigest(),render_engine=scene.render.engine,resolution=[scene.render.resolution_x,scene.render.resolution_y],samples=scene.cycles.samples,material_pass_meshes_unchanged=len(before),material_coordinates='Geometry.Position world metres')
    json.dump(manifest,open(manifest_path,'w',encoding='utf-8'),ensure_ascii=False,indent=2)
    print('World metre material pass complete; meshes unchanged:',len(before))
if '--materials-only' in sys.argv:
    refine_material_pass()
    sys.exit(0)
bpy.ops.wm.open_mainfile(filepath=BASE)
scn=bpy.context.scene
SC=json.load(open(os.path.join(ROOT,'artifacts/remake-chapter2/campus-site/scene.json'),encoding='utf-8'))
eu,ev=Vector((*SC['eu'],0)),Vector((*SC['ev'],0))
O=Vector((*SC['origin'],0))
def meshhash(ob):
    if ob.type!='MESH': return None
    a=np.array([v.co[:] for v in ob.data.vertices],dtype=np.float64)
    return hashlib.sha256(a.tobytes()).hexdigest()
def bounds(ob):
    a=np.array([tuple(ob.matrix_world@Vector(v)) for v in ob.bound_box]); return [a.min(0).tolist(),a.max(0).tolist()]
original={o.name:dict(matrix=list(sum((list(row) for row in o.matrix_world),[])),bounds=bounds(o),vertices=meshhash(o)) for o in bpy.data.objects if o.type=='MESH'}
collection=bpy.data.collections.new('Refined_Additions_v1'); scn.collection.children.link(collection)
def move(ob,name,mat):
    ob.name='Refined·'+name
    for c in list(ob.users_collection):c.objects.unlink(ob)
    collection.objects.link(ob)
    if mat:ob.data.materials.append(mat)
    return ob
def material(name,color,metal=0):
    m=bpy.data.materials.new('Refined·'+name);m.use_nodes=True
    p=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None);p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.65;p.inputs['Metallic'].default_value=metal
    return m
stone=material('Weathered pale basalt',(.32,.31,.27));wood=material('Old oak',(.13,.075,.038));glass=material('Blue cool glass',(.13,.25,.28),.22)
dark=material('Door recess',(.025,.026,.023));copper=material('Patinated copper seams',(.14,.29,.25),.5);leaf=material('Cryptomeria foliage',(.075,.13,.066));trunk=material('Tree bark',(.16,.105,.062));flower=material('Hydrangea muted blue',(.28,.37,.49));grass=material('Low grass',(.14,.22,.08))
for m in list(bpy.data.materials):
    if not m.use_nodes or not next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None):continue
    p=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if any(k in m.name for k in ('天空','玻璃','窗','glass','foliage','blue')):continue
    n=m.node_tree.nodes.new('ShaderNodeTexNoise');n.inputs['Scale'].default_value=38;n.inputs['Detail'].default_value=2
    bump=m.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.13;bump.inputs['Distance'].default_value=.025
    m.node_tree.links.new(n.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs['Normal'],p.inputs['Normal'])
    ramp=m.node_tree.nodes.new('ShaderNodeMapRange');ramp.inputs['To Min'].default_value=.57;ramp.inputs['To Max'].default_value=.88
    m.node_tree.links.new(n.outputs['Fac'],ramp.inputs['Value']);m.node_tree.links.new(ramp.outputs['Result'],p.inputs['Roughness'])
def box(name,loc,size,mat,angle=0):
    x,y,z=(v/2 for v in size)
    verts=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    me=bpy.data.meshes.new('Refined·'+name);me.from_pydata(verts,[],[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    ob=bpy.data.objects.new('Refined·'+name,me);collection.objects.link(ob);me.materials.append(mat);ob.location=loc;ob.rotation_euler.z=angle;return ob
def cyl(name,loc,r,depth,mat,r2=None):
    bm=bmesh.new();bmesh.ops.create_cone(bm,cap_ends=True,segments=12,radius1=r,radius2=r if r2 is None else r2,depth=depth)
    me=bpy.data.meshes.new('Refined·'+name);bm.to_mesh(me);bm.free();ob=bpy.data.objects.new('Refined·'+name,me);collection.objects.link(ob);me.materials.append(mat);ob.location=loc;return ob
def line(name,pts,r,mat):
    cu=bpy.data.curves.new('Refined·'+name,'CURVE');cu.dimensions='3D';cu.resolution_u=1;cu.bevel_depth=r;cu.bevel_resolution=1
    sp=cu.splines.new('POLY');sp.points.add(len(pts)-1)
    for p,v in zip(sp.points,pts):p.co=(*v,1)
    ob=bpy.data.objects.new('Refined·'+name,cu);collection.objects.link(ob);cu.materials.append(mat);return ob
angle=math.atan2(eu.y,eu.x)
def panel(name,c,tangent,normal,w,h,z,door=False):
    c=Vector((*c[:2],z));a=math.atan2(tangent.y,tangent.x)
    box(name+' pane',c+normal*.025,(w,.035,h),wood if door else glass,a)
    for x in (-w/2,w/2):box(name+' jamb',c+tangent*x+normal*.07,(.09,.11,h+.14),stone if door else wood,a)
    for zz in (-h/2,h/2):box(name+' sill',c+Vector((0,0,zz))+normal*.075,(w+.18,.15,.09),stone if door else wood,a)
    if not door:
        box(name+' mullion',c+normal*.06,(.055,.06,h),wood,a)
        box(name+' transom',c+normal*.06,(w,.06,.055),wood,a)
    else:
        for k in range(1,8):box(name+' plank',c+tangent*(-w/2+w*k/8)+normal*.05,(.018,.025,h-.05),dark,a)
T=np.load(os.path.join(TEST,'terrain.npz')); TX,TY,TZ=T['xs'],T['ys'],T['z']
def ground(x,y):
    fx=np.interp(x,TX,np.arange(len(TX)));fy=np.interp(y,TY,np.arange(len(TY)))
    j=min(int(fx),len(TX)-2);i=min(int(fy),len(TY)-2);tx=fx-j;ty=fy-i
    return float(TZ[i,j]*(1-tx)*(1-ty)+TZ[i,j+1]*tx*(1-ty)+TZ[i+1,j]*(1-tx)*ty+TZ[i+1,j+1]*tx*ty)
def steps(b,width=3):
    c=Vector((*b['center'],b['floor']));c-=ev*(b['size'][1]/2+.35)
    g=ground(c.x,c.y);rise=b['floor']-g
    count=max(1,min(14,math.ceil(max(.18,rise)/.19)))
    for k in range(count):
        q=c-ev*((count-k)*.29);z=g+(k+1)*max(.18,rise)/count
        box(b['name']+' step',Vector((q.x,q.y,z-.11)),(width,.34,.22),stone,angle)
for b in SC['buildings']:
    nm=b['name'];f=b['floor'];h=b['height'];c=Vector((*b['center'],0))
    if nm=='中央厅':
        for k in range(20):
            th=2*math.pi*k/20;n=Vector((math.cos(th),math.sin(th),0));t=Vector((-n.y,n.x,0));q=c+n*12.025
            panel('Central hall window',q,t,n,1.05,2.25,f+5.1)
        for k in range(24):
            th=2*math.pi*k/24
            pts=[(c.x+10.34*math.sin(a)*math.cos(th),c.y+10.34*math.sin(a)*math.sin(th),f+10.6+10.32*.75*math.cos(a)) for a in np.linspace(.1,math.pi/2,30)]
            line('Dome copper rib',pts,.035,copper)
        cyl('Lantern copper cap',(c.x,c.y,f+20.55),1.55,.65,copper,.2)
    if nm=='中央厅南门廊':
        q=c-ev*(b['size'][1]/2+.035)
        panel('Central hall oak double door',q,eu,-ev,2.15,3.6,f+1.8,True)
        for side in (-1,1):
            p=q+eu*side*2.55-ev*.4
            cyl('South portico column',(*p[:2],f+2.35),.24,4.7,stone)
            box('Column capital',(*p[:2],f+4.65),(.67,.67,.23),stone,angle)
            box('Column plinth',(*p[:2],f+.16),(.65,.65,.32),stone,angle)
        steps(b,3.1)
    if nm=='门楼':
        front=c-ev*(b['size'][1]*.4+.09)
        # Stone voussoirs around the existing opening; gate leaves open away from the passage.
        for k in range(15):
            th=math.pi*k/14;p=front+eu*(2.45*math.cos(th));z=f+3.15+1.6*math.sin(th)
            ob=box('Gate arch voussoir',(*p[:2],z),(.36,.3,.35),stone,angle)
        for side in (-1,1):
            hinge=front+eu*side*2.25
            leafcenter=hinge-ev*1.0
            box('Open oak gate',(*leafcenter[:2],f+1.8),(.15,2.0,3.5),wood,angle)
            for z in (.6,2.8):box('Gate iron strap',(*leafcenter[:2],f+z),(.17,2.05,.09),dark,angle)
    if nm.startswith('生活翼·') or nm=='后勤院' or nm=='诊疗所与安全端':
        if not b.get('size'):continue
        su,sv=b['size']
        for side in (-1,1):
            normal=ev*side
            for x in np.arange(-su/2+2.2,su/2-1.5,3.7):
                q=c+eu*x+normal*(sv/2+.035);panel(nm+' window',q,eu,normal,1.15,1.65,f+2.15)
            q=c+normal*(sv/2+.32)
            line(nm+' eave gutter',[q-eu*(su/2+.3)+Vector((0,0,f+h+.05)),q+eu*(su/2+.3)+Vector((0,0,f+h+.05))],.07,copper)
            p=q+eu*(su/2-.45)
            line(nm+' drainpipe',[(p.x,p.y,f+.2),(p.x,p.y,f+h+.05)],.065,copper)
        if nm=='后勤院':
            q=c-ev*(sv/2+.04);panel(nm+' entrance',q,eu,-ev,1.4,2.65,f+1.33,True);steps(b,2.2)
    if nm in ('讲堂','名录库','文献工房','文契院（封闭部分）','共鸣厅','星盘讲堂（封闭）','化学实验室（封闭）'):
        su,sv=b['size']
        for side in (-1,1):
            normal=ev*side
            for x in np.arange(-su/2+2,su/2-1.2,3.6):
                q=c+eu*x+normal*(sv/2+.035);panel(nm+' stone framed window',q,eu,normal,1.15,2.05,f+2.65)
random.seed(17)
for u,v in [(-81,-70),(-79,-38),(65,-73),(78,-44),(114,-109),(-103,23)]:
    p=O+eu*u+ev*v;z=ground(p.x,p.y);cyl('Cryptomeria trunk',(p.x,p.y,z+2.5),.13,5,trunk)
    for level,r in [(2,1.4),(3,1.1),(4,.8)]:cyl('Cryptomeria crown',(p.x,p.y,z+level),r,2.5,leaf,.04)
for u,v in [(-75,-74),(-69,-73),(48,-75),(60,-46),(-43,-24),(-40,-23),(102,-25),(-20,-18),(20,-18),(22,-20)]:
    p=O+eu*u+ev*v;z=ground(p.x,p.y)
    for k in range(9):
        q=p+Vector((random.uniform(-.6,.6),random.uniform(-.6,.6),.35+random.uniform(0,.2)))
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.32,location=(q.x,q.y,z+q.z));move(bpy.context.object,'Hydrangea cluster',flower if k%3==0 else leaf)
for u,v in [(-80,-80),(-70,-78),(58,-78),(68,-44),(-42,-28)]:
    p=O+eu*u+ev*v;z=ground(p.x,p.y)
    for k in range(12):
        q=p+Vector((random.uniform(-1.3,1.3),random.uniform(-1.3,1.3),0));cyl('Low grass tuft',(q.x,q.y,z+.14),.12,.28,grass,0)
bpy.context.view_layer.update()
checks=[]
for name,record in original.items():
    ob=bpy.data.objects[name];cur=dict(matrix=list(sum((list(row) for row in ob.matrix_world),[])),bounds=bounds(ob),vertices=meshhash(ob));checks.append(dict(name=name,unchanged=record==cur,before=record,after=cur))
assert all(c['unchanged'] for c in checks),'Original geometry changed'
orig=bpy.data.objects['02_court_eye'];clone=orig.copy();clone.data=orig.data.copy();clone.name='02_court_eye_original';scn.collection.objects.link(clone)
p=O-ev*88;orig.location=(p.x,p.y,ground(p.x,p.y)+1.7)
aim=O+ev*500;aim.z=100;orig.rotation_euler=(aim-orig.location).to_track_quat('-Z','Y').to_euler()
camera_records=[]
for name in ['02_court_eye','02_court_eye_original','03_court_6m','05_south_25m']:
    ob=bpy.data.objects[name];ray=ob.rotation_euler.to_quaternion()@Vector((0,0,-1000))
    camera_records.append(dict(name=name,location=list(ob.location),rotation=list(ob.rotation_euler),target_ray_point=list(ob.location+ray),lens_mm=ob.data.lens,eye_above_ground_m=ob.location.z-ground(ob.location.x,ob.location.y)))
json.dump(dict(source_sha256=hashlib.sha256(open(BASE,'rb').read()).hexdigest(),original_meshes=len(original),all_original_geometry_unchanged=True,checks=checks,additions=[o.name for o in collection.objects],cameras=camera_records),open(os.path.join(OUT,'geometry_and_cameras.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=2)
scn.render.image_settings.file_format='PNG';scn.render.resolution_percentage=100
scn.render.engine='BLENDER_EEVEE_NEXT';scn.eevee.taa_render_samples=32;scn.render.resolution_x=1280;scn.render.resolution_y=720
for name in ['02_court_eye','03_court_6m','05_south_25m']:
    scn.camera=bpy.data.objects[name];scn.render.filepath=os.path.join(OUT,name+'_preview.png');bpy.ops.render.render(write_still=True)
if '--preview-only' not in sys.argv:
    scn.render.engine='CYCLES';scn.cycles.device='CPU';scn.cycles.samples=32;scn.cycles.use_denoising=True;scn.cycles.use_adaptive_sampling=True
    scn.render.resolution_x=1920;scn.render.resolution_y=1080
    for name in ['02_court_eye','05_south_25m']:
        scn.camera=bpy.data.objects[name];scn.render.filepath=os.path.join(OUT,name+'_final.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=SAVE)
json.dump(dict(blend_sha256=hashlib.sha256(open(SAVE,'rb').read()).hexdigest(),production_script_sha256=hashlib.sha256(open(__file__,'rb').read()).hexdigest(),render_engine=scn.render.engine,resolution=[scn.render.resolution_x,scn.render.resolution_y],samples=scn.cycles.samples,final_views=['02_court_eye','05_south_25m'],cameras=camera_records),open(os.path.join(OUT,'delivery_manifest.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=2)
print('Refined additive scene saved',SAVE,'original objects',len(original),'unchanged',all(c['unchanged'] for c in checks))
if '--preview-only' not in sys.argv and '--structure-only' not in sys.argv:
    refine_material_pass()

