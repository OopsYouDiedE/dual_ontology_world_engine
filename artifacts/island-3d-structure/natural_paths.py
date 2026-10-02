"""Route paths on an immutable height function; never assign terrain elevations.

route_on_natural(height_fn, polyline_xy, width, max_grade=.12) returns XY/Z
points and contiguous ``walk`` / ``footstairs`` segments. Z always comes from
height_fn. A* keeps every supplied waypoint and searches a +/-60m corridor on
a 12m, eight-neighbour grid. ``blocked`` means water/cliffs prevented both
walking and staircase routing. Grade limits are fractions, e.g. .12 = 12%.

This is a finite-sample route planner, not an all-point proof or staircase
geometry generator. Call grade_statistics independently to recheck a route.
"""
import heapq
import math


def _xy(points):
    result = []
    for p in points:
        q = (float(p[0]), float(p[1]))
        if not all(math.isfinite(v) for v in q):
            raise ValueError("Route coordinates must be finite")
        if not result or math.dist(q, result[-1]) > 1e-7:
            result.append(q)
    if len(result) < 2:
        raise ValueError("At least two distinct route points are required")
    return result


class _Terrain:
    def __init__(self, fn, width, step, sea, cliff, cross, obstacle_fn=None):
        self.fn, self.width, self.step = fn, width, step
        self.sea, self.cliff, self.cross = sea, cliff, cross
        self.heights, self.edges = {}, {}
        self.obstacle_fn = obstacle_fn
        self.obstacle_checks = {}

    def height(self, x, y):
        key = (round(x, 6), round(y, 6))
        if key not in self.heights:
            z = float(self.fn(x, y))
            if not math.isfinite(z):
                raise ValueError("height_fn returned a non-finite value")
            self.heights[key] = z
        return self.heights[key]

    def edge(self, a, b):
        a,b=tuple(a),tuple(b)
        key = tuple(sorted((a, b)))
        if key in self.edges:
            return self.edges[key]
        length = math.dist(a, b)
        if length <= 1e-8:
            return dict(length=0, grade=0, center_grade=0, cross_grade=0,
                        min_height=self.height(*a), land=True, cliff=False)
        count = max(1, math.ceil(length / self.step))
        nx, ny = -(b[1]-a[1])/length, (b[0]-a[0])/length
        cross_count=max(4,2*math.ceil(self.width))
        offsets=[i/cross_count-.5 for i in range(cross_count+1)]
        tracks = [[] for _ in offsets]
        cross_grade = 0.0
        for i in range(count+1):
            t = i/count
            x, y = a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t
            row = [self.height(x+nx*self.width*f, y+ny*self.width*f)
                   for f in offsets]
            for track, z in zip(tracks, row):
                track.append(z)
            cross_grade = max(cross_grade, *(abs(v-u)/(self.width/cross_count)
                                            for u,v in zip(row,row[1:])))
        grades = [max(abs(v-u)/(length/count) for u,v in zip(track,track[1:]))
                  for track in tracks]
        minimum = min(min(track) for track in tracks)
        result = dict(length=length, grade=max(grades), center_grade=grades[cross_count//2],
                      cross_grade=cross_grade, min_height=minimum,
                      land=minimum > self.sea,
                      cliff=max(max(grades),cross_grade) > self.cliff)
        self.edges[key] = result
        return result

    def admissible(self, a, b, grade_limit):
        m = self.edge(a,b)
        if self.obstacle_fn:
            key=tuple(sorted((tuple(a),tuple(b))))
            if key not in self.obstacle_checks:
                length=math.dist(a,b);count=max(1,math.ceil(length));blocked=False
                nx,ny=(-(b[1]-a[1])/length,(b[0]-a[0])/length) if length>1e-8 else (0,0)
                for i in range(count+1):
                    x,y=a[0]+(b[0]-a[0])*i/count,a[1]+(b[1]-a[1])*i/count
                    if any(self.obstacle_fn(x+nx*self.width*side,y+ny*self.width*side)
                           for side in (-.5,-.25,0,.25,.5)):
                        blocked=True;break
                self.obstacle_checks[key]=blocked
            if self.obstacle_checks[key]: return False
        return (m['land'] and not m['cliff'] and
                (grade_limit is None or
                 (m['grade'] <= grade_limit+1e-9 and m['cross_grade'] <= self.cross+1e-9)))


def _distance_to_segment(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]
    t=max(0.0,min(1.0,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/(dx*dx+dy*dy)))
    return math.dist(p,(a[0]+t*dx,a[1]+t*dy))


def _astar(terrain,a,b,grid,corridor,grade_limit,max_expansions):
    # Global grid orientation is stable across legs; exact endpoints are extra
    # graph nodes, avoiding a snapped endpoint or invented shared junction Z.
    start, goal = ('start',), ('goal',)
    def position(node):
        return a if node==start else b if node==goal else (node[0]*grid,node[1]*grid)
    def inside(p):
        return _distance_to_segment(p,a,b) <= corridor+1e-7
    def neighbours(node):
        p=position(node)
        if node==start:
            ix,iy=round(a[0]/grid),round(a[1]/grid)
            candidates=[(ix+dx,iy+dy) for dx in range(-2,3) for dy in range(-2,3)]
        else:
            ix,iy=node
            candidates=[(ix+dx,iy+dy) for dx in (-1,0,1) for dy in (-1,0,1) if dx or dy]
        for q in candidates:
            qp=position(q)
            if math.dist(p,qp)>1e-7 and inside(qp):
                yield q,qp
        if math.dist(p,b) <= grid*2.1:
            yield goal,b
    queue=[(math.dist(a,b),0,start)]
    costs={start:0.0};parents={};settled=set();serial=0;expansions=0
    while queue and expansions<max_expansions:
        _,_,node=heapq.heappop(queue)
        if node in settled:
            continue
        settled.add(node)
        if node==goal:
            path=[b]
            while node!=start:
                node=parents[node]
                path.append(position(node))
            return list(reversed(path)),expansions
        current=costs[node]
        p=position(node);expansions+=1
        for q,qp in neighbours(node):
            if not terrain.admissible(p,qp,grade_limit):
                continue
            m=terrain.edge(p,qp)
            cost=m['length']*(1+.3*(m['grade']/max(grade_limit or .15,.01))**2
                +.15*_distance_to_segment(qp,a,b)/max(corridor,grid))
            candidate=current+cost
            if candidate < costs.get(q,float('inf'))-1e-9:
                costs[q]=candidate;parents[q]=node;serial+=1
                heapq.heappush(queue,(candidate+math.dist(qp,b),serial,q))
    return None,expansions


def _simplify(terrain,path,grade_limit):
    result=[path[0]];i=0
    while i<len(path)-1:
        j=min(i+16,len(path)-1)
        while j>i+1 and not terrain.admissible(path[i],path[j],grade_limit):
            j-=1
        result.append(path[j]);i=j
    return result


def _statistics(terrain,points):
    metrics=[terrain.edge(a,b) for a,b in zip(points,points[1:])]
    return dict(length_m=sum(m['length'] for m in metrics),
        max_grade_percent=100*max(m['grade'] for m in metrics),
        max_center_grade_percent=100*max(m['center_grade'] for m in metrics),
        max_cross_grade_percent=100*max(m['cross_grade'] for m in metrics),
        min_sampled_height_m=min(m['min_height'] for m in metrics),
        water_crossing=any(not m['land'] for m in metrics),
        cliff_crossing=any(m['cliff'] for m in metrics),sample_step_m=terrain.step,
        width_m=terrain.width,terrain_edited=False)


def grade_statistics(height_fn,polyline_xy,width,sample_step=3.0,water_level=0.0,
                     cliff_grade=.75,max_cross_grade=.30):
    """Fresh finite-sample check of center and four parallel width tracks."""
    if not all(math.isfinite(float(v)) for v in
               (width,sample_step,water_level,cliff_grade,max_cross_grade)):
        raise ValueError("All verification parameters must be finite")
    if width<=0 or sample_step<=0:
        raise ValueError("width and sample_step must be positive")
    terrain=_Terrain(height_fn,float(width),float(sample_step),water_level,cliff_grade,max_cross_grade)
    return _statistics(terrain,_xy(polyline_xy))


def route_on_natural(height_fn,polyline_xy,width,max_grade=.12,corridor=60.0,
                     grid=12.0,sample_step=3.0,water_level=0.0,cliff_grade=.75,
                     max_cross_grade=.30,max_expansions=5000,obstacle_fn=None,direct_stair_legs=()):
    """Find walking detours, then land-only staircase routes, preserving anchors.

    ``points`` are XYZ without pavement lift. ``segments`` share exact endpoints
    and carry their geometry kind; footstairs is a construction requirement,
    not permission to alter ground or a proof that stairs have been built.
    Water or true cliffs with no admissible detour yield ``blocked``.
    """
    if not all(math.isfinite(float(v)) for v in
               (width,max_grade,corridor,grid,sample_step,water_level,cliff_grade,max_cross_grade)):
        raise ValueError("All route parameters must be finite")
    if width<=0 or grid<=0 or sample_step<=0 or corridor<0 or not 0<max_grade<=1:
        raise ValueError("Invalid width/grid/step/corridor or fractional grade limit")
    if max_expansions<=0 or cliff_grade<=max_grade or max_cross_grade<=0:
        raise ValueError("Invalid expansion/cliff/cross-grade limit")
    anchors=_xy(polyline_xy)
    terrain=_Terrain(height_fn,float(width),float(sample_step),water_level,cliff_grade,max_cross_grade,obstacle_fn)
    routed=[anchors[0]];segments=[];warnings=[];expansions=0;rerouted=0
    for leg,(a,b) in enumerate(zip(anchors,anchors[1:])):
        if terrain.admissible(a,b,max_grade):
            path=[a,b];kind='walk'
        elif leg in direct_stair_legs and terrain.admissible(a,b,None):
            path=[a,b];kind='footstairs'
        else:
            path,count=_astar(terrain,a,b,grid,corridor,max_grade,max_expansions)
            expansions+=count
            if path:
                path=_simplify(terrain,path,max_grade);kind='walk';rerouted+=1
            elif terrain.admissible(a,b,None):
                # Dense breakpoints let the caller build stairs only on steep
                # parts, while retaining walking portions of a mixed leg.
                n=max(1,math.ceil(math.dist(a,b)/grid))
                path=[(a[0]+(b[0]-a[0])*i/n,a[1]+(b[1]-a[1])*i/n) for i in range(n+1)]
                kind='footstairs'
            else:
                path,count=_astar(terrain,a,b,grid,corridor,None,max_expansions)
                expansions+=count
                if path:
                    kind='footstairs';rerouted+=1
                else:
                    path=[a,b];kind='blocked'
                    warnings.append('Leg %d: no sampled land route avoids water/cliffs within corridor'%leg)
        path=_xy(path)  # Exact endpoint graph nodes can coincide with grid nodes.
        routed.extend(path[1:])
        # Keep endpoint-grade labels per actual edge, then group adjacent kinds.
        for u,v in zip(path,path[1:]):
            edge_kind=kind
            if kind=='footstairs' and terrain.admissible(u,v,max_grade):
                edge_kind='walk'
            xyz=lambda p:[p[0],p[1],terrain.height(*p)]
            if segments and segments[-1]['kind']==edge_kind and segments[-1]['leg']==leg:
                segments[-1]['points'].append(xyz(v))
            else:
                segments.append(dict(kind=edge_kind,leg=leg,points=[xyz(u),xyz(v)]))
    for segment in segments:
        segment['statistics']=_statistics(terrain,[p[:2] for p in segment['points']])
        segment['requires_width_review']=segment['statistics']['max_cross_grade_percent']>100*max_cross_grade+1e-7
    kinds={s['kind'] for s in segments}
    kind='blocked' if 'blocked' in kinds else 'footstairs' if 'footstairs' in kinds else 'walk'
    if 'footstairs' in kinds:
        warnings.append('No walking service line found on this finite grid for some legs; build actual stairs on returned steep segments')
    if any(s['requires_width_review'] for s in segments):
        warnings.append('Excessive natural crossfall: narrow/re-align the path or design transverse treads; longitudinal stairs alone do not resolve it')
    return dict(kind=kind,points=[[x,y,terrain.height(x,y)] for x,y in routed],segments=segments,
        statistics=_statistics(terrain,routed),anchors=[[x,y,terrain.height(x,y)] for x,y in anchors],
        max_grade_percent=100*max_grade,rerouted_legs=rerouted,astar_expansions=expansions,
        corridor_m=corridor,grid_m=grid,warnings=warnings,terrain_edited=False)
