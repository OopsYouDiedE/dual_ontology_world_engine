"""Read-only independent saved-mesh audit. Never generate or save the blend.

From the repository root:
blender --factory-startup -b -P artifacts/island-3d-structure/check_island.py
Or load an explicit saved .blend before -P; its sibling validation.json is used.
Default output: artifacts/island-3d-structure/independent-validation.json.
Reference: immutable pre-construction natural_height; construction mesh BVH.
The final verdict covers the enumerated geometry and finite samples below.
"""
import bpy, math, json, pathlib, hashlib, sys, datetime
from math import sin, cos, pi, atan2, hypot, exp, radians
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree

ROOT=next(p for p in pathlib.Path(__file__).resolve().parents if (p/'docs/design-standard.md').is_file())
ARTIFACT=ROOT/'artifacts/island-3d-structure'
if not bpy.data.filepath:
    bpy.ops.wm.open_mainfile(filepath=str(ARTIFACT/'island_structure.blend'))
DEST=ARTIFACT/'independent-validation.json' if pathlib.Path(__file__).resolve().parent==ARTIFACT and pathlib.Path(bpy.data.filepath).parent==ARTIFACT else ROOT/'tmp/island-natural-independent-final.json'
report_path=pathlib.Path(bpy.data.filepath).parent/'validation.json'
report=json.loads(report_path.read_text(encoding='utf-8'))
issues=[]
def issue(message):
    if message not in issues: issues.append(message)
def vec(p): return [round(float(v),6) for v in p]
def js(ob,key,default=None):
    value=ob.get(key)
    return json.loads(value) if isinstance(value,str) else default if value is None else value
def vertices(ob):
    if ob.parent is not None: raise ValueError('Unexpected parent: '+ob.name)
    return [ob.matrix_basis@v.co for v in ob.data.vertices]
def triangles(ob,faces=None):
    ob.data.calc_loop_triangles()
    return [tuple(t.vertices) for t in ob.data.loop_triangles if faces is None or t.polygon_index in faces]
def bvh(ob,faces=None): return BVHTree.FromPolygons(vertices(ob),triangles(ob,faces),all_triangles=True)
def ray(tree,x,y,z=1000,up=False):
    hit=tree.ray_cast(Vector((x,y,z)),Vector((0,0,1 if up else -1)),2000)[0]
    return None if hit is None else float(hit.z)
terrain=bpy.data.objects['岛屿地形'];tv=vertices(terrain);tt=triangles(terrain)
ground_tree=BVHTree.FromPolygons(tv,tt,all_triangles=True)
def ground(x,y):
    z=ray(ground_tree,x,y)
    if z is None: raise ValueError('Outside native terrain at '+str((x,y)))
    return z
def samples(vs,tris):
    # Every actual triangle centroid plus edge midpoints; not a quad average.
    for tri in tris:
        a,b,c=(vs[i] for i in tri)
        yield (a+b+c)/3
        yield (a+b)*.5
        yield (b+c)*.5
        yield (c+a)*.5
def max_record(records):
    value,p=max(records,key=lambda r:r[0]) if records else (0,Vector())
    return dict(value_m=float(value),location=vec(p))
def underside_boundary_sampler(ob,faces=None):
    # A vertical ray on an exact outer edge can miss after float32 rounding.
    # Allow only 3mm of horizontal boundary tolerance, then interpolate the
    # actual downward triangle. This cannot excuse a visible uncovered gap.
    vs=vertices(ob);tris=[]
    for t in triangles(ob,faces):
        a,b,c=(vs[i] for i in t)
        if faces is not None or (b-a).cross(c-a).z<-.000001: tris.append(t)
    flat=BVHTree.FromPolygons([Vector((v.x,v.y,0)) for v in vs],tris,all_triangles=True)
    def boundary(x,y):
        p,normal,index,distance=flat.find_nearest(Vector((x,y,0)),.003)
        if p is None:return None
        a,b,c=(vs[i] for i in tris[index]);den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y)
        if abs(den)<1e-12:return None
        u=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/den
        v=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/den
        return u*a.z+v*b.z+(1-u-v)*c.z
    return boundary
def mesh_edges(ob):
    use={}
    for face in ob.data.polygons:
        for edge in face.edge_keys:
            key=tuple(sorted(edge));use[key]=use.get(key,0)+1
    return sum(n!=2 for n in use.values())

frozen=ARTIFACT/'natural-terrain-reference.py'
if not frozen.exists():frozen=ROOT/'tmp/island-natural-reference.py'
meta_path=ARTIFACT/'natural-terrain-reference-meta.json' if frozen.parent==ARTIFACT else ROOT/'tmp/island-natural-reference-meta.json'
meta=json.loads(meta_path.read_text(encoding='utf-8-sig'))
reference_sha=hashlib.sha256(frozen.read_bytes()).hexdigest()
assert reference_sha==meta['sha256'],'Frozen reference was changed'
context=dict(math=math,Vector=Vector,noise=noise,sin=sin,cos=cos,pi=pi,atan2=atan2,hypot=hypot,exp=exp,radians=radians)
exec(frozen.read_text(encoding='utf-8'),context)
native=context['natural_height']
errors=[abs(p.z-native(p.x,p.y)) for p in tv]
out=dict(method='Read-only actual loop-triangle BVH and geometry vertices; immutable frozen terrain reference',
    started_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),validation_file=str(report_path),
    blend=bpy.data.filepath,blend_sha256=hashlib.sha256(pathlib.Path(bpy.data.filepath).read_bytes()).hexdigest(),
    frozen_reference_sha256=reference_sha,reference_frozen_at_utc=meta['frozen_at_utc'],
    source_validation_passed=report.get('passed'),terrain=dict(vertices=len(tv),triangles=len(tt),
    max_vertex_error_m=max(errors),changed_over_001m=sum(e>.001 for e in errors)),
    roads={},supports={},buildings={},entries={},service_entries={},roofs={},columns={},circulation={},issues=issues)
if max(errors)>.001: issue('Underlying terrain differs from frozen natural reference')
if not report.get('passed'): issue('Generator validation failed')
print('INDEPENDENT terrain',out['terrain'],flush=True)

# Enumerate saved meshes, rather than a fixed previous-version object count.
support_objects={ob.name:ob for ob in bpy.data.objects if ob.type=='MESH' and 'support_bottom_vertex_count' in ob}
for name,ob in support_objects.items():
    vs=vertices(ob);n=int(ob['support_bottom_vertex_count']);bottom=vs[:n];top=vs[n:]
    bottom_faces={p.index for p in ob.data.polygons if all(i<n for i in p.vertices)}
    bt=triangles(ob,bottom_faces)
    errors=[(abs(p.z-ground(p.x,p.y)+.05),p) for p in bottom]
    gaps=[(p.z-ground(p.x,p.y),p) for p in samples(vs,bt)]
    top_z=float(ob['support_top_z'])
    top_error=max(abs(p.z-top_z) for p in top)
    floor_intrusions=[(ground(p.x,p.y)-top_z,p) for p in [*bottom,*samples(vs,bt)]]
    max_gap=max((v for v,p in gaps),default=-.05);min_gap=min((v for v,p in gaps),default=-.05)
    passed=max(v for v,p in errors)<=.001 and max_gap<=.01 and min_gap>=-.12 and top_error<=.001 and max(v for v,p in floor_intrusions)<=.02
    nonmanifold=mesh_edges(ob)
    if nonmanifold: passed=False;issue('Open support solid: '+name)
    out['supports'][name]=dict(bottom_vertices=n,bottom_triangles=len(bt),vertex_contact=max_record(errors),
        interior_max_gap=max_record(gaps),interior_min_gap_m=min_gap,top_plane_error_m=top_error,
        ground_intrusion=max_record(floor_intrusions),nonmanifold_edges=nonmanifold,passed=passed)
    if not passed: issue('Natural support contact/floor: '+name)
print('INDEPENDENT supports',len(support_objects),flush=True)
for name,data in report.get('buildings',{}).items():
    objs=[support_objects.get(n) for n in data.get('support_objects',[])]
    if not objs or any(ob is None for ob in objs): issue('Missing actual building support: '+name);continue
    points=[]
    for ob in objs:
        vs=vertices(ob);n=int(ob['support_bottom_vertex_count'])
        faces={p.index for p in ob.data.polygons if all(i<n for i in p.vertices)}
        points.extend(vs[:n]);points.extend(samples(vs,triangles(ob,faces)))
    floor=float(data['floor_z']);intrusions=[(ground(p.x,p.y)-floor,p) for p in points]
    exposure=max(floor-ground(p.x,p.y) for p in points)
    own=[ob for ob in bpy.data.objects if ob.type=='MESH' and ob.get('site_owner')==name]
    floor_matches=[abs(min(p.z for p in vertices(ob))-floor) for ob in own]
    floor_error=min(floor_matches,default=None)
    out['buildings'][name]=dict(floor_z=floor,max_support_exposure_m=exposure,
        floor_ground_intrusion=max_record(intrusions),owned_meshes=len(own),
        closest_actual_body_bottom_to_floor_error_m=floor_error,support_objects=[ob.name for ob in objs])
    if exposure>1.501: issue('Building base exceeds 1.5m exposure: '+name)
    if max(v for v,p in intrusions)>.02: issue('Building floor cuts natural terrain: '+name)
    if floor_error is None or floor_error>.02: issue('No actual building body meets declared floor: '+name)

road_bvhs={};road_boundaries={};road_sources={}
for ob in bpy.data.collections['道路步道'].objects:
    if ob.type!='MESH': continue
    if ob.get('kind')=='boardwalk_pile':continue
    if ob.get('kind')=='boardwalk':
        sf=set(int(i) for i in ob.get('surface_face_indices',[]))
        road_bvhs[ob.name]=bvh(ob,sf);road_boundaries[ob.name]=underside_boundary_sampler(ob,sf)
        out['roads'][ob.name]=dict(audited=False,kind='boardwalk',pending='Dedicated saved-mesh bridge audit')
        continue
    if not ob.get('natural_surface'):
        out['roads'][ob.name]=dict(audited=False,reason='Separate construction type requires explicit bridge audit')
        issue('Road lacks natural-surface/bridge audit metadata: '+ob.name);continue
    vs=vertices(ob);metadata=js(ob,'route_meta',{});flights=js(ob,'stairs_meta',[])
    surface_faces=set(int(i) for i in ob.get('surface_face_indices',[]));surface=set(int(i) for i in ob.get('surface_indices',[]))
    bottom=set(int(i) for i in ob.get('bottom_indices',[]))
    stair_faces={i for flight in flights for step in flight['steps'] for i in step['top_face_indices']}
    stair_surface={i for flight in flights for step in flight['steps'] for i in step['top_vertex_indices']}
    walk_faces=surface_faces-stair_faces;walk_surface=surface-stair_surface
    bottom_faces={p.index for p in ob.data.polygons if all(i in bottom for i in p.vertices)}
    walk_points=[vs[i] for i in walk_surface];walk_inside=list(samples(vs,triangles(ob,walk_faces)))
    top_error=max((abs(p.z-ground(p.x,p.y)-.12) for p in walk_points),default=0)
    walk_clearance=[(p.z-ground(p.x,p.y),p) for p in [*walk_points,*walk_inside]]
    walk_grades=[];used_faces=set()
    for fi in sorted(walk_faces):
        if fi in used_faces:continue
        indices=list(ob.data.polygons[fi].vertices)
        if len(indices)==4:
            along=((indices[0],indices[3]),(indices[1],indices[2]))
        elif len(indices)==3 and fi+1 in walk_faces:
            second=list(ob.data.polygons[fi+1].vertices)
            if len(second)!=3 or indices[0]!=second[0] or indices[2]!=second[1]:
                issue('Unexpected paired walk triangle topology: '+ob.name);continue
            used_faces.add(fi+1);along=((indices[1],indices[2]),(second[2],second[0]))
        else:issue('Unexpected walk top cell topology: '+ob.name);continue
        for ia,ib in along:
            a,b=vs[ia],vs[ib];length=math.hypot(a.x-b.x,a.y-b.y)
            if length>1e-8:walk_grades.append((abs(a.z-b.z)/length,(a+b)*.5))
    walk_grade=max((g for g,p in walk_grades),default=0)
    grade_limit=metadata.get('requested_max_grade')
    if metadata.get('requested_max_grade_percent') is not None:grade_limit=metadata['requested_max_grade_percent']/100
    if grade_limit is not None and walk_grade>float(grade_limit)+.001:
        issue('Actual walking longitudinal grade: '+ob.name)
    bottom_error=max((abs(vs[i].z-ground(vs[i].x,vs[i].y)+.05) for i in bottom),default=0)
    bottom_gaps=[(p.z-ground(p.x,p.y),p) for p in samples(vs,triangles(ob,bottom_faces))]
    base_max=max((g for g,p in bottom_gaps),default=-.05);base_min=min((g for g,p in bottom_gaps),default=-.05)
    min_clearance=min((v for v,p in walk_clearance),default=.12);max_clearance=max((v for v,p in walk_clearance),default=.12)
    stairs=[];all_rises=[];all_treads=[]
    for flight in flights:
        heights=[];depths=[];intrusions=[];level_error=0;riser_face_max=0
        for step in flight['steps']:
            top=[vs[i] for i in step['top_vertex_indices']];z=sum(p.z for p in top)/len(top)
            heights.append(z);level_error=max(level_error,max(abs(p.z-z) for p in top))
            columns=len(step['start_xy']);rows=[top[i:i+columns] for i in range(0,len(top),columns)]
            depths.append(min(sum(math.dist(a[k][:2],b[k][:2]) for a,b in zip(rows,rows[1:])) for k in (0,-1)))
            points=[*top,*samples(vs,triangles(ob,set(step['top_face_indices'])))]
            intrusions.extend((ground(p.x,p.y)-p.z,p) for p in points)
            for fi in step['riser_face_indices']:
                poly=ob.data.polygons[fi];p=[vs[i] for i in poly.vertices]
                riser_face_max=max(riser_face_max,max(v.z for v in p)-min(v.z for v in p))
                if abs(poly.normal.z)>.001: issue('Nonvertical exposed stair riser: '+ob.name)
        steps=flight['steps']
        first=[vs[i] for i in steps[0]['top_vertex_indices']][:len(steps[0]['start_xy'])]
        last=[vs[i] for i in steps[-1]['top_vertex_indices']][-len(steps[-1]['end_xy']):]
        endpoint=max([abs(p.z-ground(p.x,p.y)-.12) for p in first+last])
        rises=[abs(b-a) for a,b in zip(heights,heights[1:])]+[endpoint,riser_face_max]
        all_rises.extend(rises);all_treads.extend(depths)
        stats=dict(treads=len(steps),max_actual_rise_m=max(rises),min_actual_tread_m=min(depths),
            full_width_endpoint_rise_m=endpoint,horizontal_plane_error_m=level_error,ground_intrusion=max_record(intrusions),
            passed=max(rises)<=.2001 and min(depths)>=.2499 and level_error<=.001 and max(v for v,p in intrusions)<=.001)
        stairs.append(stats)
        if max(rises)>.2001 or min(depths)<.2499 or level_error>.001 or max(v for v,p in intrusions)>.001:
            issue('Actual stair geometry: '+ob.name)
    passed=top_error<=.001 and bottom_error<=.001 and base_max<=.0101 and base_min>=-.12 and min_clearance>=.025 and max_clearance<=.171 and not metadata.get('blocked')
    nonmanifold=mesh_edges(ob)
    if nonmanifold: passed=False;issue('Open path solids: '+ob.name)
    if not passed: issue('Natural road contact/blocked: '+ob.name)
    road_bvhs[ob.name]=bvh(ob,surface_faces)
    road_boundaries[ob.name]=underside_boundary_sampler(ob,surface_faces)
    road_sources[ob.name]=[segment['source_points'] for segment in metadata.get('constructed_segments',[])]
    out['roads'][ob.name]=dict(audited=True,vertices=len(vs),surface_triangles=len(triangles(ob,surface_faces)),
        max_actual_walk_longitudinal_grade_percent=walk_grade*100,requested_max_grade=grade_limit,
        worst_actual_walk_grade_location=vec(max(walk_grades,key=lambda r:r[0])[1]) if walk_grades else None,
        walk_vertex_offset_error_m=top_error,min_walk_clearance_m=min_clearance,max_walk_clearance_m=max_clearance,
        bottom_vertex_contact_error_m=bottom_error,max_bottom_face_gap=max_record(bottom_gaps),
        min_bottom_face_gap_m=base_min,nonmanifold_edges=nonmanifold,stairs=stairs,
        passed=passed and all(s['passed'] for s in stairs) and (grade_limit is None or walk_grade<=float(grade_limit)+.001))
print('INDEPENDENT roads',len(out['roads']),flush=True)

def road_z_at(name,x,y):
    hit=ray(road_bvhs[name],x,y)
    return hit if hit is not None else road_boundaries[name](x,y)
def road_height_at(x,y):
    hits=[road_z_at(name,x,y) for name in road_bvhs]
    return max((z for z in hits if z is not None),default=None)
for name,entry in report.get('entries',{}).items():
    objects=[bpy.data.objects.get(n) for n in entry['step_objects']]
    if not objects or any(ob is None for ob in objects): issue('Missing entry steps: '+name);continue
    start=entry['start_xy'];door=entry['door_xy'];run=math.dist(start,door)
    ux,uy=(door[0]-start[0])/run,(door[1]-start[1])/run
    heights=[];treads=[];side_rows=[];max_level_error=0;gap_between=0
    for ob in objects:
        vs=vertices(ob);n=int(ob['support_bottom_vertex_count']);top=vs[n:]
        z=sum(p.z for p in top)/len(top);heights.append(z)
        max_level_error=max(max_level_error,max(abs(p.z-z) for p in top))
        projection=[(p.x-start[0])*ux+(p.y-start[1])*uy for p in top]
        lo,hi=min(projection),max(projection);treads.append(hi-lo)
        beginning=[p for p,t in zip(top,projection) if abs(t-lo)<.001]
        ending=[p for p,t in zip(top,projection) if abs(t-hi)<.001]
        side_rows.append((beginning,ending,lo,hi))
    for a,b in zip(side_rows,side_rows[1:]): gap_between=max(gap_between,abs(a[3]-b[2]))
    first=side_rows[0][0];last=side_rows[-1][1]
    endpoint=max(abs(heights[0]-ground(p.x,p.y)-.12) for p in first)
    floor_error=abs(heights[-1]-entry['floor_z'])
    rises=[abs(a-b) for a,b in zip(heights,heights[1:])]+[endpoint]
    connection=road_height_at(*start)
    connection_error=abs(heights[0]-connection) if connection is not None else None
    passed=max(rises)<=.2001 and min(treads)>=.3199 and max_level_error<=.001 and gap_between<=.001 and floor_error<=.001 and connection is not None and connection_error<=.2001
    out['entries'][name]=dict(step_count=len(objects),max_actual_rise_m=max(rises),min_actual_tread_m=min(treads),
        entry_full_width_rise_m=endpoint,horizontal_plane_error_m=max_level_error,step_join_gap_m=gap_between,
        floor_join_error_m=floor_error,approach_actual_road_z=connection,approach_actual_riser_m=connection_error,passed=passed)
    if not passed: issue('Entry stairs/approach connection: '+name)

owners={'北':'文契院_文献工房','西':'星仪院_星盘讲堂','南':'铁工院_工房_-1','东':'白塔',
    '生活院':'生活院_公共起居室','后勤院':'后勤院_厨房','训练院':'训练院','浴室诊疗所':'公共浴室',
    '图书馆':'教学图书馆','美术冬园':'美术工房','门楼':'门楼_东塔','中央厅':'中央厅'}
for name,owner in owners.items():
    ob=bpy.data.objects.get('勤务竖井_'+name)
    if ob is None: issue('Missing service shaft: '+name);continue
    vs=vertices(ob);top=max(p.z for p in vs);bottom=min(p.z for p in vs);floor=report['buildings'][owner]['floor_z']
    error=max(abs(top-floor),abs(bottom+4));out['service_entries'][name]=dict(owner=owner,actual_top_z=top,
        actual_bottom_z=bottom,building_floor_z=floor,max_error_m=error,passed=error<=.001)
    if error>.001: issue('Service shaft owner-floor mismatch: '+name)

roof_names={'学园分坡环廊':'分坡环廊_屋面',**{n:n+'_屋面' for n in ('中央北廊','中央西廊','中央东廊','中央南廊')}}
roof_trees={};roof_boundaries={}
for road,roof_name in roof_names.items():
    ob=bpy.data.objects.get(roof_name);path=bpy.data.objects.get(road)
    if ob is None or path is None: issue('Missing canopy/path: '+road);continue
    tree=bvh(ob);roof_trees[road]=tree;roof_boundaries[road]=underside_boundary_sampler(ob);vs=vertices(path)
    sf=set(int(i) for i in path.get('surface_face_indices',[]))
    points=[vs[i] for i in path.get('surface_indices',[])];points.extend(samples(vs,triangles(path,sf)))
    clearances=[];missing=[]
    for p in points:
        z=ray(tree,p.x,p.y,p.z+.001,up=True)
        if z is None:z=roof_boundaries[road](p.x,p.y)
        if z is None: missing.append(vec(p))
        else: clearances.append(z-p.z)
    min_clearance=min(clearances,default=0)
    out['roofs'][road]=dict(samples=len(points),missing_count=len(missing),missing_first_10=missing[:10],
        min_actual_headroom_m=min_clearance,passed=not missing and min_clearance>=2.2)
    if missing or min_clearance<2.2: issue('Canopy coverage/headroom: '+road)
for ob in bpy.data.objects:
    if ob.type!='MESH' or not (ob.get('roof_owner') in roof_names.values() or ob.name.startswith('回廊柱_') or any(ob.name.startswith(n+'_柱') for n in roof_names if n!='学园分坡环廊')): continue
    if ob.get('roof_owner') in roof_names.values():road=next(n for n,roof in roof_names.items() if roof==ob['roof_owner'])
    else:road='学园分坡环廊' if ob.name.startswith('回廊柱_') else next(n for n in roof_names if ob.name.startswith(n+'_柱'))
    vs=vertices(ob);low=min(v.z for v in vs);high=max(v.z for v in vs)
    base=[p for p in vs if abs(p.z-low)<.001];top=[p for p in vs if abs(p.z-high)<.001]
    base_gaps=[(p.z-ground(p.x,p.y),p) for p in base]
    top_errors=[];missing=[]
    for p in top:
        z=ray(roof_trees[road],p.x,p.y,low+1,up=True)
        if z is None:z=roof_boundaries[road](p.x,p.y)
        if z is None: missing.append(vec(p))
        else: top_errors.append(abs(high-z))
    base_max=max(v for v,p in base_gaps);base_min=min(v for v,p in base_gaps)
    error=max(top_errors,default=1000);passed=base_max<=.01 and base_min>=-.3 and not missing and error<=.05
    out['columns'][ob.name]=dict(max_base_gap_m=base_max,min_base_gap_m=base_min,
        roof_contact_error_m=error,missing_roof_samples=missing,passed=passed)
    if not passed: issue('Column base/roof contact: '+ob.name)

# Ring closure and actual radial crossings, using actual path surface BVHs.
ring_sources=road_sources.get('学园分坡环廊',[])
if ring_sources:
    start,end=ring_sources[0][0],ring_sources[-1][-1]
    closure=math.dist(start[:2],end[:2]);out['circulation']['ring_xy_closure_error_m']=closure
    if closure>.001: issue('Campus ring route is not closed')
def intersect(a,b,c,d):
    ax,ay=b[0]-a[0],b[1]-a[1];bx,by=d[0]-c[0],d[1]-c[1];det=ax*by-ay*bx
    if abs(det)<1e-10:return None
    qx,qy=c[0]-a[0],c[1]-a[1];t=(qx*by-qy*bx)/det;u=(qx*ay-qy*ax)/det
    return (a[0]+t*ax,a[1]+t*ay) if -.00001<=t<=1.00001 and -.00001<=u<=1.00001 else None
ring_pairs=[(a,b) for pts in ring_sources for a,b in zip(pts,pts[1:])]
for name in ('中央北廊','中央西廊','中央东廊','中央南廊'):
    joins=[]
    for pts in road_sources.get(name,[]):
        for a,b in zip(pts,pts[1:]):
            for c,d in ring_pairs:
                xy=intersect(a,b,c,d)
                if xy:
                    own=road_z_at(name,*xy);ring=road_z_at('学园分坡环廊',*xy)
                    joins.append(dict(xy=xy,actual_road_z=own,actual_ring_z=ring,
                        error_m=abs(own-ring) if own is not None and ring is not None else None))
    valid=[j for j in joins if j['error_m'] is not None and j['error_m']<=.02]
    out['circulation'][name]=dict(actual_intersections=joins,passed=bool(valid))
    if not valid: issue('Actual radial-ring connection: '+name)
out['limitations']=[
    'Checks every terrain vertex against the frozen fictional procedural island, not a surveyed DEM.',
    'Construction contact/headroom uses every specified surface vertex, actual triangle centroid and edge midpoint; finite samples do not prove every mathematical point.',
    'Closed joined step solids retain internal contact faces; edge topology is checked per original component indices.',
    'Road metadata selects saved-mesh faces; actual coordinates, heights, tread depths and contacts are independently measured.',
    'Canopy outer-edge ray misses permit only 0.003m horizontal float-boundary tolerance, interpolated from actual underside triangles.',
    'Does not establish playable Godot collision, navigation, accessibility, structural loads, waterproofness or full underground room interiors.',
    'Separate bridge meshes require explicit support/entry checks and are flagged until that checker is added.'
]
out['circulation']['all_closed_routes']={}
for name,source_segments in road_sources.items():
    obj=bpy.data.objects[name];nodes=js(obj,'source_nodes',[])
    if len(nodes)<3 or math.dist(nodes[0][:2],nodes[-1][:2])>.001:continue
    if not source_segments:issue('Closed route has no saved construction segments: '+name);continue
    first,last=source_segments[0][0],source_segments[-1][-1]
    xy_error=math.dist(first[:2],last[:2]);a=road_z_at(name,*first[:2]);b=road_z_at(name,*last[:2])
    z_error=abs(a-b) if a is not None and b is not None else None
    passed=xy_error<=.001 and z_error is not None and z_error<=.02
    out['circulation']['all_closed_routes'][name]=dict(actual_source_closure_error_m=xy_error,
        first_surface_z=a,last_surface_z=b,surface_closure_error_m=z_error,passed=passed)
    if not passed:issue('Actual closed-route geometry: '+name)
out['counts']=dict(roads=len(out['roads']),supports=len(out['supports']),buildings=len(out['buildings']),
    entries=len(out['entries']),service_shafts=len(out['service_entries']),columns=len(out['columns']))
sys.path.insert(0,str(ARTIFACT));sys.path.insert(1,str(ROOT/'tmp'))
from island_building_support_entry_audit import audit_saved_geometry, _hull, _bounds, _bounds_overlap, _sat
out['site_geometry']=audit_saved_geometry(report,ground,require_full_road_approach=True)
for message in out['site_geometry']['issues']:issue(message)
try:
    from island_dock_reservoir_audit import audit_saved_dock_reservoir
    out['dock_reservoir']=audit_saved_dock_reservoir(ground,road_z_at=road_height_at)
    for message in out['dock_reservoir']['issues']:issue(message)
except ImportError:
    issue('Saved-mesh dock/reservoir auditor is not available')
out['walls']={};wall_buckets={}
for ob in bpy.data.objects:
    if ob.type!='MESH' or 'wall_bottom_vertex_count' not in ob:continue
    vs=vertices(ob);n=int(ob['wall_bottom_vertex_count']);bottom=vs[:n];top=vs[n:]
    sf={p.index for p in ob.data.polygons if all(i<n for i in p.vertices)}
    points=[*bottom,*samples(vs,triangles(ob,sf))]
    contact=max(abs(p.z-ground(p.x,p.y)+.05) for p in bottom)
    gaps=[p.z-ground(p.x,p.y) for p in points]
    pair_error=max(math.hypot(a.x-b.x,a.y-b.y) for a,b in zip(bottom,top))
    height_error=max(abs(b.z-a.z-5.05) for a,b in zip(bottom,top))
    nonclosed=mesh_edges(ob);poly=_hull([tuple(p) for p in bottom]);bounds=_bounds(poly)
    passed=contact<=.001 and max(gaps)<=.01 and min(gaps)>=-.12 and pair_error<=.001 and height_error<=.001 and nonclosed==0
    out['walls'][ob.name]=dict(bottom_vertices=n,max_bottom_vertex_native_minus005_error_m=contact,
        max_bottom_face_gap_m=max(gaps),min_bottom_face_gap_m=min(gaps),top_bottom_xy_pair_error_m=pair_error,
        wall_height_error_m=height_error,nonclosed_edges=nonclosed,road_collisions=[],passed=passed)
    if not passed:issue('Natural exterior wall contact: '+ob.name)
    for by in range(math.floor(bounds[1]/40),math.floor(bounds[3]/40)+1):
        for bx in range(math.floor(bounds[0]/40),math.floor(bounds[2]/40)+1):
            wall_buckets.setdefault((bx,by),[]).append((ob.name,poly,bounds))
for name in road_bvhs:
    ob=bpy.data.objects[name];vs=vertices(ob);sf=set(int(i) for i in ob['surface_face_indices'])
    for tri in triangles(ob,sf):
        projected=[(vs[i].x,vs[i].y) for i in tri]
        bounds=_bounds(projected);candidates={}
        for by in range(math.floor(bounds[1]/40),math.floor(bounds[3]/40)+1):
            for bx in range(math.floor(bounds[0]/40),math.floor(bounds[2]/40)+1):
                for wall,poly,wb in wall_buckets.get((bx,by),[]):candidates[wall]=(poly,wb)
        for wall,(poly,wb) in candidates.items():
            if _bounds_overlap(bounds,wb) and _sat(_hull(projected),poly) is not None:
                record=out['walls'][wall]
                if name not in record['road_collisions']:record['road_collisions'].append(name)
                record['passed']=False;issue('Actual road intersects exterior wall: '+name+' / '+wall)
out['counts']['exterior_walls']=len(out['walls'])
if any(ob.get('kind')=='boardwalk' for ob in bpy.data.objects):
    try:
        from island_boardwalk_audit import audit_saved_boardwalks
        natural_names=[name for name in road_bvhs if bpy.data.objects[name].get('natural_surface')]
        def natural_road_surface(x,y):
            return max((z for name in natural_names if (z:=road_z_at(name,x,y)) is not None),default=None)
        out['boardwalks']=audit_saved_boardwalks(ground,road_z_at=natural_road_surface)
        for message in out['boardwalks']['issues']:issue(message)
        for name,details in out['boardwalks']['section']['boardwalks'].items():
            out['roads'][name]=dict(audited=True,kind='boardwalk',details=details,passed=not details['issues'])
    except ImportError:
        issue('Dedicated saved-mesh boardwalk auditor is not available')
out['passed']=not issues
out['component_geometry_passed']=not [message for message in issues if message!='Generator validation failed']
out['finished_at_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
DEST.write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(passed=out['passed'],counts=out['counts'],issues=issues,report=str(DEST)),ensure_ascii=False),flush=True)
