import bpy, numpy as np, json, os, hashlib, time
from mathutils import Vector
ROOT='C:/Users/iii/Desktop/dual_ontology_world_engine'
TEST=ROOT+'/artifacts/map-optimization/camera-tests'
OUT=TEST+'/refined/independent-refined'
os.makedirs(OUT,exist_ok=True)
FINAL=ROOT+'/godot_csharp/AssetsSource/Internal/Props/MapOptimization/3D/campus_camera_refined_v1.blend'
def digest(p):
    return hashlib.sha256(open(p,'rb').read()).hexdigest()
def mesh_record(o):
    return {'verts':np.array([v.co[:] for v in o.data.vertices]),'matrix':np.array(o.matrix_world),'worldverts':np.array([tuple(o.matrix_world@v.co) for v in o.data.vertices]),'bounds':np.array([tuple(o.matrix_world@Vector(v)) for v in o.bound_box])}
bpy.ops.wm.open_mainfile(filepath=TEST+'/camera_tests.blend')
before={o.name:mesh_record(o) for o in bpy.data.objects if o.type=='MESH'}
bpy.ops.wm.open_mainfile(filepath=FINAL)
checks=[]
for name,a in before.items():
    o=bpy.data.objects.get(name)
    if o is None:checks.append({'name':name,'missing':True});continue
    b=mesh_record(o)
    checks.append({'name':name,**{k+'_exact':bool(np.array_equal(a[k],b[k])) for k in a},'vertex_count':len(a['verts'])})
additions=[o for o in bpy.data.collections['Refined_Additions_v1'].objects]
categories={k:[o.name for o in additions if term in o.name.lower()] for k,term in [('door','door'),('window','window'),('steps','step'),('copper_seams','dome copper rib'),('gates','open oak gate')]}
def trace(socket,seen=None):
    seen=set() if seen is None else seen
    out=[]
    for link in socket.links:
        key=(link.from_node.as_pointer(),link.from_socket.identifier)
        if key in seen:continue
        seen.add(key)
        out.append({'node_type':link.from_node.bl_idname,'output':link.from_socket.name})
        for inp in link.from_node.inputs:
            if inp.is_linked:out+=trace(inp,seen)
    return out
textures=[]
for m in bpy.data.materials:
    if not m.use_nodes:continue
    for n in m.node_tree.nodes:
        if n.type in ('TEX_NOISE','TEX_BRICK'):
            chain=trace(n.inputs['Vector'])
            textures.append({'material':m.name,'texture':n.type,'vector_upstream':chain,'world_position':any(x['node_type']=='ShaderNodeNewGeometry' and x['output']=='Position' for x in chain)})
missing=[]
for group in (bpy.data.images,bpy.data.libraries,bpy.data.movieclips,bpy.data.fonts,bpy.data.sounds,bpy.data.cache_files):
    for datablock in group:
        p=getattr(datablock,'filepath','')
        if not p or p.startswith('<') or getattr(datablock,'packed_file',None) or getattr(datablock,'packed_files',None):continue
        if not os.path.exists(bpy.path.abspath(p)):missing.append({'name':datablock.name,'path':p})
terrain=np.load(TEST+'/terrain.npz');xs,ys,z=terrain['xs'],terrain['ys'],terrain['z']
gx,gy=np.meshgrid(xs,ys)
terrain_expected=np.stack((gx.ravel(),gy.ravel(),z.ravel()),axis=1)
terrain_actual=mesh_record(bpy.data.objects['地形'])['worldverts']
terrain_match={'vertices_expected':len(terrain_expected),'vertices_actual':len(terrain_actual),'max_abs_world_xyz_difference_m':float(np.max(np.abs(terrain_expected-terrain_actual))),'matches_with_float32_tolerance':bool(np.allclose(terrain_expected,terrain_actual,rtol=0,atol=.0005))}
def ground(x,y):
    j=int(np.searchsorted(xs,x)-1);i=int(np.searchsorted(ys,y)-1)
    j=max(0,min(j,len(xs)-2));i=max(0,min(i,len(ys)-2))
    u=(x-xs[j])/(xs[j+1]-xs[j]);v=(y-ys[i])/(ys[i+1]-ys[i])
    return float(z[i,j]*(1-u)*(1-v)+z[i,j+1]*u*(1-v)+z[i+1,j]*(1-u)*v+z[i+1,j+1]*u*v)
cams=[]
for name,height in [('02_court_eye',1.7),('05_south_25m',25)]:
    o=bpy.data.objects[name];g=ground(o.location.x,o.location.y)
    cams.append({'name':name,'location':list(o.location),'terrain_z':g,'terrain_offset':o.location.z-g,'expected_offset':height,'within_0_001m':abs(o.location.z-g-height)<.001,'lens':o.data.lens})
    land=bpy.data.objects['地形'];hit,location,normal,index=land.ray_cast(land.matrix_world.inverted()@Vector((o.location.x,o.location.y,1000)),Vector((0,0,-1)))
    cams[-1]['actual_mesh_vertical_ray_hit']=hit
    cams[-1]['actual_mesh_vertical_ray_z']=float((land.matrix_world@location).z)
    cams[-1]['actual_mesh_vertical_ray_offset_m']=float(o.location.z-(land.matrix_world@location).z)
sc=bpy.context.scene
settings={'engine':sc.render.engine,'samples':sc.cycles.samples,'resolution':[sc.render.resolution_x,sc.render.resolution_y],'percentage':sc.render.resolution_percentage}
scene=json.load(open(ROOT+'/artifacts/remake-chapter2/campus-site/scene.json',encoding='utf-8'))
gate=next(b for b in scene['buildings'] if b['name']=='门楼');eu=Vector((*scene['eu'],0));ev=Vector((*scene['ev'],0));c=Vector((*gate['center'],gate['floor']))
# Project every added mesh into gate coordinates. A clear 4m-wide central corridor at walking height must remain free.
blockers=[];gate_bounds=[]
for o in additions:
    if o.type!='MESH':continue
    vs=[o.matrix_world@v.co-c for v in o.data.vertices]
    us=[v.dot(eu) for v in vs];vs2=[v.dot(ev) for v in vs];hs=[v.z for v in vs]
    bb=[min(us),max(us),min(vs2),max(vs2),min(hs),max(hs)]
    if 'gate' in o.name.lower():gate_bounds.append({'name':o.name,'uvz_bounds':bb})
    if bb[0]<2 and bb[1]>-2 and bb[2]<gate['size'][1]/2+2 and bb[3]>-gate['size'][1]/2-2 and bb[4]<2.2 and bb[5]>.2:blockers.append({'name':o.name,'uvz_bounds':bb})
manifest=json.load(open(TEST+'/refined/delivery_manifest.json',encoding='utf-8'))
hashes={'blend':digest(FINAL),'script':digest(ROOT+'/godot_csharp/tools/refine_campus_camera_3d.py'),'terrain':digest(TEST+'/terrain.npz'),'scene':digest(ROOT+'/artifacts/remake-chapter2/campus-site/scene.json')}
result={'original_mesh_count':len(before),'original_mesh_checks':checks,'all_original_exact':all(all(c.get(k+'_exact',False) for k in ['verts','matrix','worldverts','bounds']) for c in checks),'addition_count':len(additions),'categories':{k:{'count':len(v),'names':v} for k,v in categories.items()},'texture_chains':textures,'all_texture_vectors_world_position':all(t['world_position'] for t in textures),'missing_external_dependencies':missing,'camera_checks':cams,'saved_render_settings':settings,'terrain_shape':list(z.shape),'terrain_npz_mesh_comparison':terrain_match,'terrain_xy_range':[[float(xs[0]),float(xs[-1])],[float(ys[0]),float(ys[-1])]],'gate_added_corridor_blockers':blockers,'gate_added_bounds':gate_bounds,'hashes':hashes,'manifest_hash_checks':{'blend':hashes['blend']==manifest['blend_sha256'],'script':hashes['script']==manifest['production_script_sha256']}}
json.dump(result,open(OUT+'/audit.json','w',encoding='utf-8'),ensure_ascii=False,indent=2)
sc.camera=bpy.data.objects['02_court_eye'];sc.render.engine='CYCLES';sc.cycles.samples=8;sc.cycles.device='CPU';sc.render.resolution_x=480;sc.render.resolution_y=270;sc.render.resolution_percentage=100;sc.render.filepath=OUT+'/02_saved_source_preview.png'
t=time.time();bpy.ops.render.render(write_still=True);result['independent_render']={'success':os.path.isfile(sc.render.filepath),'seconds':time.time()-t,'engine':'CYCLES','samples':8,'resolution':[480,270],'path':sc.render.filepath}
json.dump(result,open(OUT+'/audit.json','w',encoding='utf-8'),ensure_ascii=False,indent=2)
print('INDEPENDENT_DONE',result['all_original_exact'],len(additions),len(missing),len(blockers))
