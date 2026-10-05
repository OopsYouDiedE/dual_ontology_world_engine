import bpy, json, os, hashlib, struct, time
from mathutils import Vector
ROOT='C:/Users/iii/Desktop/dual_ontology_world_engine'
OUT=ROOT+'/artifacts/map-optimization/camera-tests/refined'
WORK=OUT+'/independent-gable'
SRC=ROOT+'/godot_csharp/AssetsSource/Internal/Props/MapOptimization/3D/campus_camera_refined_v1.blend'
BASE=ROOT+'/artifacts/map-optimization/camera-tests/camera_tests.blend'
def sha(p):
    with open(p,'rb') as f:return hashlib.sha256(f.read()).hexdigest()
def snap():
    out={}
    for o in bpy.data.objects:
        if o.type!='MESH':continue
        coords=[tuple(v.co) for v in o.data.vertices]
        vb=b''.join(struct.pack('<3d',*v) for v in coords)
        points=[o.matrix_world@Vector(c) for c in o.bound_box]
        out[o.name]={'vertices':len(coords),'vertex_sha256':hashlib.sha256(vb).hexdigest(),'matrix':[list(r) for r in o.matrix_world],'world_bounds':[[min(v[i] for v in points) for i in range(3)],[max(v[i] for v in points) for i in range(3)]]}
    return out
def png_size(p):
    with open(p,'rb') as f:
        d=f.read(24)
    assert d[:8]==b'\x89PNG\r\n\x1a\n'
    return list(struct.unpack('>II',d[16:24]))
before_hash=sha(SRC)
bpy.ops.wm.open_mainfile(filepath=BASE)
base=snap()
bpy.ops.wm.open_mainfile(filepath=SRC)
current=snap()
changes=[n for n,v in base.items() if current.get(n)!=v]
scene=bpy.context.scene
saved_settings={'engine':scene.render.engine,'samples':scene.cycles.samples,'resolution':[scene.render.resolution_x,scene.render.resolution_y],'percentage':scene.render.resolution_percentage}
graphs={}
checks={}
for name in ['红瓦','石板瓦']:
    mat=bpy.data.materials[name]; nt=mat.node_tree
    links=[{'from':l.from_node.name,'from_type':l.from_node.bl_idname,'socket':l.from_socket.name,'to':l.to_node.name,'to_type':l.to_node.bl_idname,'input':l.to_socket.name} for l in nt.links]
    def incoming(n,index):return n.inputs[index].links[0] if n.inputs[index].is_linked else None
    mixes=[n for n in nt.nodes if n.bl_idname=='ShaderNodeMixRGB']
    slope_mix=next(n for n in mixes if incoming(n,0) and incoming(n,0).from_node.bl_idname=='ShaderNodeMath' and incoming(n,0).from_node.operation=='GREATER_THAN')
    greater=incoming(slope_mix,0).from_node
    absolute=incoming(greater,0).from_node
    zlink=incoming(absolute,0)
    normal_sep=zlink.from_node
    normlink=incoming(normal_sep,0)
    tile=incoming(slope_mix,2).from_node
    wall=incoming(slope_mix,1).from_node
    principled=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
    common=(greater.inputs[1].default_value==0.20000000298023224 and absolute.operation=='ABSOLUTE' and zlink.from_socket.name=='Z' and normlink.from_node.bl_idname=='ShaderNodeNewGeometry' and normlink.from_socket.name=='Normal' and tile.bl_idname=='ShaderNodeTexBrick' and incoming(principled,'Base Color').from_node==slope_mix)
    wall_ok=wall.bl_idname==('ShaderNodeValToRGB' if name=='红瓦' else 'ShaderNodeTexBrick')
    if name=='石板瓦':
        coord=incoming(wall,'Vector').from_node
        zl=incoming(coord,'Y')
        wall_ok=wall_ok and coord.bl_idname=='ShaderNodeCombineXYZ' and zl.from_node.bl_idname=='ShaderNodeSeparateXYZ' and zl.from_socket.name=='Z' and incoming(zl.from_node,0).from_socket.name=='Position'
    checks[name]={'normal_z_absolute_threshold':greater.inputs[1].default_value,'normal_branch_valid':common,'wall_branch_valid':wall_ok,'wall_type':wall.bl_idname,'slope_type':tile.bl_idname,'wall_colors':([list(e.color) for e in wall.color_ramp.elements] if name=='红瓦' else [list(wall.inputs[k].default_value) for k in ['Color1','Color2','Mortar']]),'wall_brick_dimensions':None if name=='红瓦' else [wall.inputs['Brick Width'].default_value,wall.inputs['Row Height'].default_value]}
    graphs[name]={'nodes':[{'name':n.name,'type':n.bl_idname,'operation':getattr(n,'operation',None)} for n in nt.nodes],'links':links}
with open(WORK+'/shader-links.json','w',encoding='utf-8') as f:json.dump(graphs,f,ensure_ascii=False,indent=2)
missing=[]
for im in bpy.data.images:
    if im.source=='FILE' and not im.packed_file and im.filepath and not os.path.exists(bpy.path.abspath(im.filepath)):missing.append({'image':im.name,'path':bpy.path.abspath(im.filepath)})
missing_lib=[bpy.path.abspath(l.filepath) for l in bpy.data.libraries if not os.path.exists(bpy.path.abspath(l.filepath))]
manifest=json.load(open(OUT+'/delivery_manifest.json',encoding='utf-8'))
script=ROOT+'/godot_csharp/tools/refine_campus_camera_3d.py'
pngs={n:{'dimensions':png_size(OUT+'/'+n),'sha256':sha(OUT+'/'+n)} for n in ['02_court_eye_final.png','05_south_25m_final.png']}
artdir=ROOT+'/godot_csharp/AssetsSource/Internal/Props/MapOptimization'
paint='academy_volcano_eye_level_painted_v1.png'
prompt=json.load(open(artdir+'/'+paint.replace('.png','.prompt.json'),encoding='utf-8'))
ref=artdir+'/'+prompt['reference']
painting={'source_sha256':sha(artdir+'/'+paint),'display_sha256':sha(OUT+'/'+paint),'dimensions':png_size(artdir+'/'+paint),'reference_path':ref,'reference_exists':os.path.exists(ref),'reference_sha256':sha(ref) if os.path.exists(ref) else None,'purpose':prompt['purpose'],'observed_deviations':prompt['observed_deviations'],'geometry_acceptance':False,'note':'Illustration only. Fine steps and surface changes are not geometry verification; no pixel exact match is required.'}
scene.render.engine='BLENDER_EEVEE_NEXT';scene.eevee.taa_render_samples=8
scene.render.resolution_x=640;scene.render.resolution_y=360;scene.render.resolution_percentage=100
scene.camera=bpy.data.objects['02_court_eye'];scene.render.filepath=WORK+'/saved-source-eevee-preview.png'
start=time.monotonic();bpy.ops.render.render(write_still=True);elapsed=time.monotonic()-start
report={'independent_context':True,'evidence_policy':'No project design documents or earlier acceptance reports read. Direct source, original base, node links, current manifest, prompt metadata and actual image inspection only.','source_sha256':before_hash,'source_unchanged_during_verification':sha(SRC)==before_hash,'original_mesh_count':len(base),'current_mesh_count':len(current),'original_mesh_changes':changes,'original_geometry_unchanged':len(base)==141 and not changes,'original_mesh_snapshots':base,'saved_settings':saved_settings,'shader_checks':checks,'missing_images':missing,'missing_libraries':missing_lib,'manifest_hash_checks':{'blend':manifest['blend_sha256']==before_hash,'production_script':manifest['production_script_sha256']==sha(script)},'final_pngs':pngs,'painting':painting,'independent_preview':{'path':scene.render.filepath,'dimensions':png_size(scene.render.filepath),'render_seconds':elapsed,'engine':'BLENDER_EEVEE_NEXT','samples':8,'rendered_from_saved_source':True},'visual_inspection':{'first_vs_final':'Both first and final images directly viewed. Court view extreme-left terracotta gable lost vertical stripes and is now pale lime. Slate triangular closures near hall and behind left buildings show horizontal stone courses. South view gable closures likewise corrected; sloped terracotta and slate tile repeats remain. Composition, buildings and ridge match before/after.','painting':'Pending separate direct image view.'}}
report['automated_pass']=report['original_geometry_unchanged'] and all(v['normal_branch_valid'] and v['wall_branch_valid'] for v in checks.values()) and not missing and not missing_lib and all(report['manifest_hash_checks'].values()) and saved_settings=={'engine':'CYCLES','samples':64,'resolution':[1920,1080],'percentage':100} and all(p['dimensions']==[1920,1080] for p in pngs.values()) and painting['source_sha256']==painting['display_sha256'] and painting['reference_exists'] and report['source_unchanged_during_verification']
with open(OUT+'/independent-gable-verification.json','w',encoding='utf-8') as f:json.dump(report,f,ensure_ascii=False,indent=2)
print('INDEPENDENT_GABLE_RESULT',report['automated_pass'], 'mesh changes',changes, 'render seconds',elapsed)
