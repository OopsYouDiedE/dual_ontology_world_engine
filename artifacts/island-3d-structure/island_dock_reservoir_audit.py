"""Read-only actual dock/reservoir mesh audit, run with Blender -b -P."""
import bpy
import collections
import hashlib
import json
import math
import pathlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree

def audit_saved_dock_reservoir(ground_z=None, road_z_at=None):
    root = next(p for p in pathlib.Path(__file__).resolve().parents if (p / 'docs/design-standard.md').is_file())
    terrain = bpy.data.objects['岛屿地形']
    terrain.data.calc_loop_triangles()
    terrain_vertices = [terrain.matrix_basis @ v.co for v in terrain.data.vertices]
    ground_tree = BVHTree.FromPolygons(terrain_vertices, [tuple(t.vertices) for t in terrain.data.loop_triangles], all_triangles=True)
    def ground(x, y):
        hit = ground_tree.ray_cast(Vector((x, y, 1000)), Vector((0, 0, -1)), 2000)[0]
        return None if hit is None else float(hit.z)

    if ground_z is not None:
        ground = ground_z

    def data(ob):
        ob.data.calc_loop_triangles()
        return [ob.matrix_basis @ v.co for v in ob.data.vertices], list(ob.data.loop_triangles)

    def triangle_points(vertices, tri):
        ps = [vertices[i] for i in tri.vertices]
        yield from ps
        for weights in ((1/3, 1/3, 1/3), (.5, .5, 0), (0, .5, .5), (.5, 0, .5),
                        (.5, .25, .25), (.25, .5, .25), (.25, .25, .5)):
            yield sum((p * w for p, w in zip(ps, weights)), Vector())

    def tree_for(vertices, triangles):
        return BVHTree.FromPolygons(vertices, [tuple(t.vertices) for t in triangles], all_triangles=True)

    def ray_height(tree, x, y, z=1000):
        hit = tree.ray_cast(Vector((x, y, z)), Vector((0, 0, -1)), 2000)[0]
        if hit is None:
            # Exact saved cap/triangle boundaries can miss due float32 ray tests.
            # Half a millimetre may recover a true boundary, never a 0.25m overhang.
            for dx, dy in ((.0005,0),(-.0005,0),(0,.0005),(0,-.0005)):
                hit = tree.ray_cast(Vector((x+dx, y+dy, z)), Vector((0, 0, -1)), 2000)[0]
                if hit is not None: break
        return None if hit is None else float(hit.z)

    issues = []
    out = dict(blend=bpy.data.filepath, scope='Actual geometry and native terrain BVH; no builder execution or blend save', dock={}, reservoir={})
    dock_decks = {}
    for name in ('潮湾_独立登岸栈道', '潮湾_主栈桥', '潮湾_侧栈桥', '潮湾_石砌卸货岸'):
        ob = bpy.data.objects.get(name)
        if ob is None:
            issues.append('Missing dock deck ' + name); continue
        vertices, triangles = data(ob)
        top, bottom = [], []
        for t in triangles:
            ps = [vertices[i] for i in t.vertices]
            normal = (ps[1] - ps[0]).cross(ps[2] - ps[0])
            if normal.length > 1e-8:
                normal.normalize()
                if normal.z > .01: top.append(t)
                elif normal.z < -.01: bottom.append(t)
        dock_decks[name] = (tree_for(vertices, top), tree_for(vertices, bottom))
        if name != '潮湾_独立登岸栈道': continue
        worst = (-float('inf'), None); grade = (0., None); samples = 0
        for t in top:
            ps = [vertices[i] for i in t.vertices]
            for a, b in zip(ps, ps[1:] + ps[:1]):
                horizontal = math.hypot(b.x - a.x, b.y - a.y)
                if horizontal > 1e-6:
                    value = abs(b.z - a.z) / horizontal
                    if value > grade[0]: grade = (value, [list(a), list(b)])
            for p in triangle_points(vertices, t):
                native = ground(p.x, p.y); intrusion = native - p.z; samples += 1
                if intrusion > worst[0]: worst = (intrusion, dict(xyz=list(p), native_z=native))
        out['dock']['landing'] = dict(actual_max_grade=grade[0], worst_grade_edge=grade[1],
            max_native_intrusion_m=worst[0], worst_intrusion=worst[1], top_triangle_samples=samples)
        if grade[0] > .1201: issues.append('Landing deck actual grade exceeds 12%')
        if worst[0] > .001: issues.append('Landing deck is penetrated by native terrain')

    pile_results = []
    for ob in bpy.data.objects:
        if ob.type != 'MESH' or not any(ob.name.startswith(p) for p in ('潮湾_登岸栈道桩', '潮湾_栈桥桩', '潮湾_卸货平台桩')): continue
        vertices, _ = data(ob); low, high = min(p.z for p in vertices), max(p.z for p in vertices)
        lower, upper = [p for p in vertices if abs(p.z - low) < .001], [p for p in vertices if abs(p.z - high) < .001]
        x, y = sum(p.x for p in lower) / len(lower), sum(p.y for p in lower) / len(lower)
        native = ground(x, y)
        candidates = []
        for name, (top_tree, bottom_tree) in dock_decks.items():
            deck_top, deck_bottom = ray_height(top_tree, x, y), ray_height(bottom_tree, x, y)
            if deck_top is not None and deck_bottom is not None:
                candidates.append(dict(deck=name, actual_top_z=deck_top, actual_bottom_z=deck_bottom,
                                       pile_top_to_bottom_m=high-deck_bottom, pile_above_surface_m=high-deck_top))
        supports = [p for p in candidates if high >= p['actual_bottom_z'] - .001 and high <= p['actual_top_z'] + .001]
        toe_embed = min(ground(p.x, p.y) - p.z for p in lower)
        cap_misses = 0; cap_failures = []; max_protrusion = 0.
        for p in upper:
            found = False; observations=[]
            for name, (top_tree, bottom_tree) in dock_decks.items():
                a, b = ray_height(top_tree, p.x, p.y), ray_height(bottom_tree, p.x, p.y)
                if a is not None and b is not None:
                    max_protrusion=max(max_protrusion,p.z-a)
                    observations.append(dict(deck=name,top_z=a,bottom_z=b,cap_above_deck_m=p.z-a))
                if a is not None and b is not None and p.z >= b - .02 and p.z <= a + .02: found = True
            if not found:
                cap_misses += 1;cap_failures.append(dict(xyz=list(p),actual_deck_observations=observations))
        result = dict(name=ob.name, ground_xy=[x,y], actual_base_z=low, native_center_z=native,
                      center_embed_m=native-low, minimum_toe_perimeter_embed_m=toe_embed,
                      actual_top_z=high, deck_candidates=candidates, supported=bool(supports),
                      cap_perimeter_missing_or_protruding_vertices=cap_misses,
                      max_cap_above_sampled_deck_surface_m=max_protrusion,cap_failures=cap_failures)
        pile_results.append(result)
        if toe_embed < -.001: issues.append(ob.name + ': pile toe perimeter floats above native terrain')
        if not supports: issues.append(ob.name + ': actual pile top does not meet a deck slab')
        if cap_misses: issues.append(ob.name + ': cap perimeter leaves or protrudes through a deck slab')
    out['dock']['piles'] = pile_results
    # The gangway actually meets the unloading platform at its sea end and
    # the separate natural South path across the central 2m at its land end.
    landing=bpy.data.objects.get('潮湾_独立登岸栈道')
    if landing is not None:
        dv,dt=data(landing);tops=[]
        for t in dt:
            ps=[dv[i] for i in t.vertices]
            if (ps[1]-ps[0]).cross(ps[2]-ps[0]).z>1e-8:tops.extend(ps)
        lo,hi=min(p.y for p in tops),max(p.y for p in tops)
        top_tree=dock_decks['潮湾_独立登岸栈道'][0]
        platform_tree=dock_decks['潮湾_石砌卸货岸'][0]
        sea_samples=[];land_samples=[]
        for x in (-2,-1.5,-1,-.5,0,.5,1,1.5,2):
            deck_z=ray_height(top_tree,x,lo);platform_z=ray_height(platform_tree,x,lo)
            error=abs(deck_z-platform_z) if deck_z is not None and platform_z is not None else None
            sea_samples.append(dict(xy=[x,lo],actual_deck_z=deck_z,actual_platform_z=platform_z,error_m=error))
            if error is None or error>.2001:issues.append('Landing sea end does not meet unloading platform within a 0.20m step')
        if road_z_at is not None:
            for x in (-1,-.5,0,.5,1):
                deck_z=ray_height(top_tree,x,hi);road_z=road_z_at(x,hi)
                error=abs(deck_z-road_z) if deck_z is not None and road_z is not None else None
                land_samples.append(dict(xy=[x,hi],actual_deck_z=deck_z,actual_road_z=road_z,error_m=error))
                if error is None or error>.2001:issues.append('Landing land end does not meet actual South path within a 0.20m step')
        out['dock']['connections']=dict(unloading_platform=sea_samples,south_natural_road=land_samples,
            native_road_checked=road_z_at is not None,max_allowed_step_m=.20)

    bank = bpy.data.objects['上水库_独立围堰']; liner = bpy.data.objects['上水库_随地防渗壳']; water = bpy.data.objects['上水库_库水']
    bv, bt = data(bank); lv, lt = data(liner); wv, wt = data(water)
    bank_top = max(set(round(p.z, 4) for p in bv), key=lambda z: sum(abs(p.z-z)<.001 for p in bv))
    bottom_ids = set()
    by_xy = collections.defaultdict(list)
    for i, p in enumerate(bv): by_xy[(round(p.x, 4),round(p.y,4))].append(i)
    for ids in by_xy.values():
        bottom_ids.add(min(ids,key=lambda i:abs(bv[i].z-ground(bv[i].x,bv[i].y)+.05)))
    base_error = max(abs(bv[i].z-ground(bv[i].x,bv[i].y)+.05) for i in bottom_ids)
    base_gap = (-float('inf'), None)
    for t in bt:
        if not all(i in bottom_ids for i in t.vertices): continue
        for p in triangle_points(bv,t):
            gap = p.z-ground(p.x,p.y)
            if gap>base_gap[0]: base_gap=(gap,list(p))
    lin_error = max(abs(p.z-ground(p.x,p.y)-.04) for p in lv)
    liner_clear = (float('inf'), None)
    for t in lt:
        for p in triangle_points(lv,t):
            gap = p.z-ground(p.x,p.y)
            if gap<liner_clear[0]: liner_clear=(gap,list(p))
    def edges(vertices, triangles):
        count=collections.Counter()
        for t in triangles:
            ids=list(t.vertices)
            for a,b in zip(ids,ids[1:]+ids[:1]): count[tuple(sorted((a,b)))]+=1
        return count
    bank_open = sum(n!=2 for n in edges(bv,bt).values())
    water_open = sum(n!=2 for n in edges(wv,wt).values())
    center=Vector((sum(p.x for p in wv)/len(wv),sum(p.y for p in wv)/len(wv),0))
    water_top=max(p.z for p in wv); water_radius=max(math.hypot(p.x-center.x,p.y-center.y) for p in wv)
    inner_radius=min(math.hypot(p.x-center.x,p.y-center.y) for p in bv)
    wall_tree=tree_for(bv,bt); leaks=[]; native_barrier=0
    liner_boundary=[p for p in lv if abs(math.hypot(p.x-center.x,p.y-center.y)-inner_radius)<.001]
    liner_wall_gap=0.
    for p in liner_boundary:
        nearest=wall_tree.find_nearest(p)
        liner_wall_gap=max(liner_wall_gap,nearest[3])
    for i in range(512):
        a=2*math.pi*i/512;dx,dy=math.cos(a),math.sin(a)
        start=Vector((center.x+(inner_radius-.1)*dx,center.y+(inner_radius-.1)*dy,water_top))
        hit=wall_tree.ray_cast(start,Vector((dx,dy,0)),2)[0]
        if hit is None:
            for delta in (-.00001,.00001):
                aa=a+delta;xx,yy=math.cos(aa),math.sin(aa)
                trial=Vector((center.x+(inner_radius-.1)*xx,center.y+(inner_radius-.1)*yy,water_top))
                hit=wall_tree.ray_cast(trial,Vector((xx,yy,0)),2)[0]
                if hit is not None: break
        if hit is None:
            native=ground(center.x+inner_radius*dx,center.y+inner_radius*dy)
            if native>=water_top-.001: native_barrier+=1
            else: leaks.append(dict(angle_degrees=i*360/512,native_z=native,water_z=water_top,xy=[start.x,start.y]))
    out['reservoir']=dict(bank_actual_top_z=bank_top,water_actual_top_z=water_top,
        actual_freeboard_m=bank_top-water_top,bank_bottom_vertex_native_minus05_error_m=base_error,
        max_actual_bank_base_above_native_m=base_gap[0],worst_bank_base_xyz=base_gap[1],bank_nonclosed_edges=bank_open,
        liner_vertex_native_plus04_error_m=lin_error,min_actual_liner_triangle_clearance_m=liner_clear[0],
        worst_liner_xyz=liner_clear[1],water_nonclosed_edges=water_open,water_radius_m=water_radius,
        bank_inner_radius_m=inner_radius,water_to_bank_radial_gap_m=inner_radius-water_radius,
        max_actual_liner_boundary_to_bank_wall_gap_m=liner_wall_gap,
        waterline_native_barrier_rays=native_barrier,waterline_unsealed_rays=leaks)
    if base_gap[0]>.001: issues.append('Bank bottom actual triangles float above native terrain')
    if liner_clear[0]<-.001: issues.append('Liner actual triangles are pierced by native terrain')
    if bank_open: issues.append('Bank mesh has open or nonmanifold edges')
    if water_open: issues.append('Water mesh has open or nonmanifold edges')
    if bank_top-water_top<.2: issues.append('Actual reservoir freeboard is below 0.20m')
    if leaks: issues.append('Reservoir waterline has neither wall nor native terrain barrier at some sampled angles')
    if liner_wall_gap>.001:issues.append('Reservoir liner boundary does not meet actual bank wall')
    out['issues']=issues;out['passed']=not issues
    return out

if __name__ == '__main__':
    artifact_dir = pathlib.Path(__file__).resolve().parent
    if not bpy.data.filepath:
        bpy.ops.wm.open_mainfile(filepath=str(artifact_dir/'island_structure.blend'))
    result = audit_saved_dock_reservoir()
    target = artifact_dir/'dock-reservoir-validation.json'
    target.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(passed=result['passed'],issues=result['issues'],report=str(target)),ensure_ascii=False))
