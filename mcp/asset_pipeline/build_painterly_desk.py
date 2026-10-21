"""Build the title desk geometry. Run with Blender 4.2 --background --python.

All authored coordinates below use Godot x/right, y/up, z/towards the viewer.
The export contains geometry only. Godot owns camera, flame and live lighting.
"""
import bpy
import hashlib
import json
import math
import pathlib
import random
import sys
from mathutils import Matrix, Vector

REPO = pathlib.Path(__file__).resolve().parents[2]
ROOT = REPO / "godot_csharp"
SOURCE = ROOT.parent / "mcp/asset_pipeline/sources/Internal/Props/UIDesk/Painterly3D"
RUNTIME = ROOT / "ShareAsset/Internal/Props/UIDesk/Painterly3D"
SOURCE.mkdir(parents=True, exist_ok=True)
RUNTIME.mkdir(parents=True, exist_ok=True)
(ROOT.parent / "mcp/asset_pipeline/sources/.gdignore").touch(exist_ok=True)
SPEC_PATH = SOURCE / "desk_physical_spec.json"
if not SPEC_PATH.is_file():
    raise FileNotFoundError("Required physical scale specification: " + str(SPEC_PATH))
SPEC_BYTES = SPEC_PATH.read_bytes()
PHYSICAL_SPEC = json.loads(SPEC_BYTES.decode("utf-8-sig"))
MM_PER_UNIT = float(PHYSICAL_SPEC["mm_per_unit"])
FLOOR_CLEARANCE = float(PHYSICAL_SPEC["floor_clearance_units"])
INK_BOX_ANGLE = float(PHYSICAL_SPEC["props"]["ink"]["authored_box_angle_radians"])
if MM_PER_UNIT <= 0 or abs(MM_PER_UNIT * PHYSICAL_SPEC["book_outer_units"][1] - PHYSICAL_SPEC["book_length_mm"]) > .0001:
    raise ValueError("Physical specification has inconsistent book dimensions / mm_per_unit")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
random.seed(73417)

def gv(p):
    return Vector((p[0], -p[2], p[1]))

def material(name, color, metal=0.0, rough=0.8, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = (*color, alpha)
    bs = next((node for node in m.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None)
    if bs is None:
        bs = m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    bs.inputs["Base Color"].default_value = (*color, alpha)
    bs.inputs["Metallic"].default_value = metal
    bs.inputs["Roughness"].default_value = rough
    bs.inputs["Alpha"].default_value = alpha
    if alpha < 1.0:
        m.surface_render_method = 'DITHERED'
    return m

wood = [material("Wood_%02d" % i, (.20 + i*.007, .155+i*.005, .142+i*.004), 0, .94) for i in range(8)]
grain_dark = material("WoodGrain", (.115,.099,.096), 0,.95)
grain_light = material("WoodWear", (.28,.23,.21),0,.95)
metal = material("Pewter", (.46,.285,.115), .80,.31)
metal_edge = material("PewterEdge", (.72,.49,.22), .86,.24)
metal_dark = material("PewterDark", (.16,.105,.047), .67,.47)
enamel = material("Enamel", (.20,.18,.12), .12,.7)
ebony = material("Ebony", (.07,.085,.10),.12,.78)
ink = material("InkBlue", (.012,.025,.075), .05,.24)
ink_edge = material("InkEdge", (.17,.27,.4), .25,.36)
ink_glass = material("InkGlass", (.045,.12,.32), .05,.12,.28)
polished_wood = material("PolishedWood", (.115,.047,.020), .06,.34)
glass = material("LampGlass", (.64,.76,.78), .05,.14,.13)
lens_glass = material("LensGlass", (.35,.47,.54), .12,.12,.20)
glass_stroke = material("GlassStroke", (.66,.73,.73), .15,.32)
paper = material("LabelPaper", (.69,.66,.60),0,.95)
seal = material("Seal", (.32,.13,.16),0,.8)

def finish(obj, name, mat, bevel=0.0, smooth=True):
    obj.name = name
    obj.data.materials.append(mat)
    if bevel:
        mod=obj.modifiers.new("Soft tool-cut edges", 'BEVEL')
        mod.width=bevel
        mod.segments=3
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in obj.data.polygons:
        p.use_smooth=smooth
    return obj

def box(name, position, size, mat, bevel=.025, angle=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=gv(position))
    o=bpy.context.object
    o.scale=(size[0],size[2],size[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.rotation_euler.z=-angle
    # Keep the large planar faces planar. Smooth shading on an entire bevelled
    # cube bent the apparent table/bottle surfaces in the previous generation.
    return finish(o,name,mat,bevel,smooth=False)

def uv_sphere(name, pos, scale, mat, segments=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=gv(pos))
    o=bpy.context.object
    o.scale=(scale[0],scale[2],scale[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,mat)

def lathe(name, x, z, profile, mat, segments=64):
    vs=[]; faces=[]; rings=[]; texcoords=[]
    for row,(y,r) in enumerate(profile):
        ring=[]
        if r < 1e-7:
            ring.append(len(vs));vs.append(gv((x,y,z)))
            texcoords.append((.5,float(row)/max(1,len(profile)-1)))
        else:
            for s in range(segments):
                a=2*math.pi*s/segments
                ring.append(len(vs))
                vs.append(gv((x+r*math.cos(a), y,z+r*math.sin(a))))
                texcoords.append((float(s)/segments,float(row)/max(1,len(profile)-1)))
        rings.append(ring)
    for lower,upper in zip(rings,rings[1:]):
        for s in range(segments):
            n=(s+1)%segments
            if len(lower)==1 and len(upper)>1:
                faces.append((lower[0],upper[s],upper[n]))
            elif len(upper)==1 and len(lower)>1:
                faces.append((lower[s],upper[0],lower[n]))
            elif len(lower)>1 and len(upper)>1:
                faces.append((lower[s],upper[s],upper[n],lower[n]))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vs,[],faces)
    mesh.update()
    o=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(o)
    finish(o,name,mat)
    for p in mesh.polygons:
        if abs(p.normal.z)>.9999: p.use_smooth=False
    uv=mesh.uv_layers.new(name="UVMap")
    for p in mesh.polygons:
        for li in p.loop_indices:
            vi=mesh.loops[li].vertex_index
            uv.data[li].uv=texcoords[vi]
    return o

def rod(name, a, b, radius, mat, verts=12):
    d=gv(b)-gv(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=radius,depth=d.length,location=(gv(a)+gv(b))/2)
    o=bpy.context.object
    o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    finish(o,name,mat,min(.006,radius*.18))
    for p in o.data.polygons:
        if len(p.vertices)>=max(8,verts): p.use_smooth=False
    return o

def torus(name, center, major, minor, mat, segments=64):
    bpy.ops.mesh.primitive_torus_add(major_segments=segments,minor_segments=8,location=gv(center),major_radius=major,minor_radius=minor)
    return finish(bpy.context.object,name,mat)

def axis_profile(name, a, b, profile, mat, segments=40):
    direction=gv(b)-gv(a)
    o=lathe(name,0,0,[(t*direction.length,r) for t,r in profile],mat,segments)
    o.location=gv(a)
    o.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    return o

def axis_ring(name, center, axis, major, minor, mat):
    o=torus(name,center,major,minor,mat,48)
    o.rotation_euler=gv(axis).to_track_quat('Z','Y').to_euler()
    return o

def gear(name, center, axis, radius, depth, teeth, mat):
    vertices=[];faces=[];n=teeth*4
    for level in [-depth*.5,depth*.5]:
        for j in range(n):
            a=j*math.tau/n
            r=radius*(1.0 if j%4 in [1,2] else .90)
            vertices.append((r*math.cos(a),r*math.sin(a),level))
    vertices.extend([(0,0,-depth*.5),(0,0,depth*.5)])
    for j in range(n):
        k=(j+1)%n
        faces.extend([(j,k,k+n,j+n),(2*n,k,j),(2*n+1,j+n,k+n)])
    mesh=bpy.data.meshes.new(name+"Mesh");mesh.from_pydata(vertices,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
    o.location=gv(center);o.rotation_euler=gv(axis).to_track_quat('Z','Y').to_euler()
    return finish(o,name,mat,smooth=False)

def perforated_burner(name,x,z,y0,y1,radius,thickness,mat):
    # A double-walled mesh with real holes; no painted dots and no boolean stack.
    columns=168;rows=28;holes=14
    vertices=[];faces=[];count=(rows+1)*columns
    for r in [radius,radius-thickness]:
        for j in range(rows+1):
            y=y0+(y1-y0)*j/rows
            for i in range(columns):
                a=math.tau*i/columns
                vertices.append(gv((x+r*math.cos(a),y,z+r*math.sin(a))))
    keep={}
    for j in range(rows):
        y=y0+(y1-y0)*(j+.5)/rows
        for i in range(columns):
            a=(i+.5)/columns
            cut=False
            for h,phase in [(y0+(y1-y0)*.30,0),(y0+(y1-y0)*.73,.5)]:
                u=((a*holes+phase+.5)%1-.5)*math.tau*radius/holes
                if (u/.032)**2+((y-h)/.027)**2<1: cut=True
            keep[j,i]=not cut
    def index(j,i): return j*columns+(i%columns)
    for (j,i),solid in keep.items():
        if not solid: continue
        a=index(j,i);b=index(j+1,i);c=index(j+1,i+1);d=index(j,i+1)
        faces.extend([(a,b,c,d),(d+count,c+count,b+count,a+count)])
        for p,q,nj,ni in [(a,b,j,i-1),(b,c,j+1,i),(c,d,j,i+1),(d,a,j-1,i)]:
            if not keep.get((nj,ni%columns),False):
                faces.append((q,p,p+count,q+count))
    mesh=bpy.data.meshes.new(name+"Mesh");mesh.from_pydata(vertices,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
    finish(o,name,mat)
    uv=mesh.uv_layers.new(name="UVMap")
    for p in mesh.polygons:
        for li in p.loop_indices:
            vi=mesh.loops[li].vertex_index%count
            uv.data[li].uv=((vi%columns)/columns,(vi//columns)/rows)
    return o

def pierced_nib(name,mat):
    # Curved, two-sided metal sheet with a central slit and an actual air hole.
    nx=64;nz=24;vertices=[];faces=[];widths=[];length=.52
    for layer in [0,1]:
        for i in range(nx+1):
            t=i/nx;x=.65+length*t
            half=.066*(1-t)**.58+.002
            center=3.98-.0673*(x-.65)
            for j in range(nz+1):
                side=(j/nz)*2-1
                y=.145+.032*(1-side*side)*(1-.35*t)-layer*.006
                vertices.append(gv((x,y,center+side*half)))
    count=(nx+1)*(nz+1)
    def idx(i,j): return i*(nz+1)+j
    keep={}
    for i in range(nx):
        t=(i+.5)/nx;x=.65+length*t;half=.066*(1-t)**.58+.002
        for j in range(nz):
            side=((j+.5)/nz)*2-1;cross=side*half
            hole=((x-.82)/.020)**2+(cross/.020)**2<1.0
            slit=x>=.82 and abs(cross)<.0033
            keep[i,j]=not(hole or slit)
    for (i,j),solid in keep.items():
        if not solid:continue
        a=idx(i,j);b=idx(i,j+1);c=idx(i+1,j+1);d=idx(i+1,j)
        faces.extend([(a,b,c,d),(d+count,c+count,b+count,a+count)])
        for p,q,ni,nj in [(a,b,i-1,j),(b,c,i,j+1),(c,d,i+1,j),(d,a,i,j-1)]:
            if not keep.get((ni,nj),False):faces.append((q,p,p+count,q+count))
    mesh=bpy.data.meshes.new(name+"Mesh");mesh.from_pydata(vertices,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
    return finish(o,name,mat)

# Real bevelled boards extend beyond the widest supported camera framing.
for i in range(32):
    x=-31.0+i*2.0
    box("DeskBoard%02d"%i,(x,-.135,0),(1.985,.27,52.0),wood[i % len(wood)],.018)
# Reference oil lamp: a turned brass foot, transparent pear-shaped reservoir,
# pierced burner and a bulbous chimney. Original height/footprint are retained.
lx,lz=-5.55,-2.35
lathe("LampBase",lx,lz,[(.012,0),(.012,.77),(.025,.865),(.055,.91),(.095,.91),(.115,.875),(.135,.878),(.165,.83),(.19,.78),(.215,.76),(.235,.69),(.255,.61),(.28,.56),(.31,.53),(.33,.48),(.35,.38),(.37,.28),(.39,.19),(.41,.19)],metal,96)
for j,(height,radius) in enumerate([(.045,.90),(.11,.88),(.142,.867),(.205,.766),(.248,.631),(.287,.548),(.328,.48)]):
    torus("LampFootEngraving%02d"%j,(lx,height,lz),radius,.008 if j%2 else .013,metal_edge if j%2==0 else metal_dark,96)
lathe("LampReservoir",lx,lz,[(.34,.16),(.36,.31),(.39,.47),(.44,.59),(.52,.65),(.62,.64),(.72,.55),(.82,.39),(.88,.25),(.92,.22)],glass,80)
lathe("LampInnerReservoir",lx,lz,[(.34,0),(.34,.30),(.39,.36),(.48,.38),(.56,.31),(.63,.20),(.69,.12),(.84,.11),(.9,.14)],metal,64)
torus("LampReservoirBand",(lx,.39,lz),.472,.018,metal_edge,80)
torus("LampReservoirCollar",(lx,.885,lz),.24,.018,metal_edge)
lathe("LampBurnerSeat",lx,lz,[(.86,.20),(.88,.27),(.90,.295),(.92,.30)],metal_dark,64)
perforated_burner("LampBurner",lx,lz,.92,1.12,.30,.018,metal)
torus("LampBurnerLowerBead",(lx,.925,lz),.303,.014,metal_edge)
torus("LampBurnerUpperBead",(lx,1.12,lz),.31,.017,metal_edge)
lathe("LampWickSeat",lx,lz,[(1.10,.17),(1.15,.18),(1.18,.11),(1.20,.10)],metal_dark,48)
box("LampWick",(lx,1.23,lz),(.075,.08,.043),ebony,.006)
torus("LampChimneySeat",(lx,1.13,lz),.35,.025,metal_edge)
lathe("LampChimney",lx,lz,[(1.12,.32),(1.16,.395),(1.26,.49),(1.39,.54),(1.54,.548),(1.69,.49),(1.83,.40),(1.96,.32),(2.1,.276),(2.28,.265),(2.67,.265),(2.77,.277),(2.8,.282),(2.8,.270),(2.77,.265),(2.67,.253),(2.28,.253),(2.1,.264),(1.96,.308),(1.83,.388),(1.69,.478),(1.54,.536),(1.39,.528),(1.26,.478),(1.16,.383),(1.12,.308)],glass,80)
torus("LampGlassLip",(lx,2.794,lz),.276,.008,glass_stroke,80)
# Fine glass edge strokes; material shader supplies the larger glancing reflection.
for index,a in enumerate([.45,2.7]):
    px=lx+.269*math.cos(a);pz=lz+.269*math.sin(a)
    rod("ChimneyEdgeStroke%d"%index,(px,2.25,pz),(px,2.65,pz),.0035,glass_stroke,6)
rod("LampRegulator",(lx+.27,1.02,lz),(lx+.65,1.02,lz),.024,metal,20)
axis_ring("LampRegulatorCollar",(lx+.36,1.02,lz),(1,0,0),.035,.009,metal_edge)
gear("LampWheel",(lx+.68,1.02,lz),(1,0,0),.112,.05,28,metal_edge)
axis_ring("LampWheelInset",(lx+.708,1.02,lz),(1,0,0),.070,.007,metal_dark)

# Magnifying glass, flat on the desk to the left of the opening cover.
mx,mz=-5.65,1.18
lathe("LensRim",mx,mz,[(.055,.575),(.06,.63),(.085,.665),(.13,.665),(.17,.635),(.185,.579),(.17,.563),(.08,.563),(.055,.575)],metal,96)
torus("LensUpperRim",(mx,.172,mz),.615,.020,metal_edge,96)
torus("LensInnerRim",(mx,.163,mz),.566,.011,metal_edge,96)
torus("LensLowerEngraving",(mx,.089,mz),.664,.006,metal_dark,96)
lathe("LensOptic",mx,mz,[(.088,0),(.091,.20),(.099,.39),(.117,.548),(.137,.548),(.153,.39),(.162,.20),(.165,0)],lens_glass,96)
la=(mx,.13,mz+.64);lb=(mx+.08,.14,mz+.98)
axis_profile("LensNeck",la,lb,[(0,.045),(.12,.072),(.23,.073),(.35,.055),(.83,.055),(1,.074)],metal,48)
axis=(.17,0,.74)
for j,t in enumerate([.18,.34,.70,.87]):
    center=tuple(la[k]+(lb[k]-la[k])*t for k in range(3))
    axis_ring("LensNeckBand%02d"%j,center,axis,.061,.008,metal_edge)
ha=(mx+.08,.14,mz+.96);hb=(mx+.25,.14,mz+1.68)
axis_profile("LensHandle",ha,hb,[(0,.072),(.06,.088),(.18,.103),(.72,.102),(.91,.087),(1,.068)],polished_wood,48)
axis_ring("LensHandleEnd",hb,axis,.070,.011,metal_edge)
axis_ring("LensHandleFerrule",ha,axis,.081,.014,metal)

# Thick cobalt glass surrounds an opaque ink core; the reference has no label.
ix,iz=-4.93,4.00
box("InkBody",(ix,.39,iz),(.90,.75,.82),ink_glass,.10,INK_BOX_ANGLE)
box("InkCore",(ix,.34,iz),(.71,.56,.63),ink,.075,INK_BOX_ANGLE)
box("InkFoot",(ix,.066,iz),(.91,.12,.83),ink_glass,.045,INK_BOX_ANGLE)
box("InkShoulder",(ix,.72,iz),(.72,.13,.65),ink_glass,.060,INK_BOX_ANGLE)
lathe("InkNeck",ix,iz,[(.72,.18),(.75,.225),(.80,.225),(.815,.239),(.835,.239),(.85,.22),(.88,.22),(.90,.245),(.925,.245),(.925,.19),(.76,.19)],ink_glass,64)
torus("InkNeckThread",(ix,.854,iz),.226,.010,ink_edge)
lathe("InkCap",ix,iz,[(.90,.255),(.93,.278),(.98,.28),(1.09,.28),(1.125,.267),(1.146,.245),(1.15,0)],ink,64)
torus("InkCapRim",(ix,.947,iz),.28,.012,metal_edge)
torus("InkCapCrownRim",(ix,1.13,iz),.253,.007,metal_edge)
for j in range(48):
    a=j*math.tau/48;r=.280
    rod("InkCapKnurl%02d"%j,(ix+r*math.cos(a),.985,iz+r*math.sin(a)),(ix+r*math.cos(a),1.086,iz+r*math.sin(a)),.0042,metal_dark,5)

# Fountain pen below the book, pointing towards the centre.
pa=(-2.6,.12,4.22);pb=(.15,.12,4.02)
axis_profile("PenBarrel",pa,pb,[(0,0),(.012,.045),(.04,.072),(.13,.091),(.50,.095),(.82,.084),(.96,.071),(1,.068)],polished_wood,64)
axis_profile("PenGrip",pb,(.72,.12,3.98),[(0,.072),(.12,.077),(.24,.066),(.71,.052),(1,.053)],ebony,48)
pen_axis=(1,0,-.073)
for j,(x,r) in enumerate([(.08,.084),(.155,.079),(.23,.068),(-2.49,.070),(-2.39,.082)]):
    z=4.02-(x-.15)*.073
    axis_ring("PenBand%02d"%j,(x,.12,z),pen_axis,r,.009,metal_edge)
for j in range(3):
    x=.32+j*.045;z=4.02-(x-.15)*.073
    axis_ring("PenGripGroove%02d"%j,(x,.12,z),pen_axis,.063-j*.002,.0035,metal_dark)
# Thin spring clip follows the curved cap rather than reading as a second barrel.
rod("PenClipStem",(-2.29,.21,4.20),(-1.61,.22,4.15),.012,metal_edge,10)
uv_sphere("PenClipTip",(-1.59,.213,4.148),(.028,.012,.018),metal_edge,16,8)
rod("PenClipHeel",(-2.33,.17,4.20),(-2.29,.21,4.20),.015,metal,10)
pierced_nib("PenNib",metal_edge)
# Small etched shoulders retain the nib's scale and leave the slit physically open.
rod("NibEtchLeft",(.70,.170,3.938),(.78,.175,3.954),.0016,metal_dark,4)
rod("NibEtchRight",(.70,.170,4.015),(.78,.175,3.994),.0016,metal_dark,4)

# A restrained burgundy ribbon/bookmark at the lower margin.
box("Ribbon",(3.55,.018,4.2),(.22,.016,1.15),seal,.004,-.14)

# Diagonal boards follow the reference grain; painterly texture supplies wear.
from mathutils import Matrix
board_turn=Matrix.Rotation(math.radians(-68),4,'Z')
for o in list(bpy.data.objects):
    if o.name.startswith("DeskBoard"):
        o.matrix_world=board_turn @ o.matrix_world

# Physical calibration follows the applied bevel geometry, not guessed object
# scales. The source authoring coordinates above remain independent of layout.
def godot_point(blender_point):
    return Vector((blender_point.x, blender_point.z, -blender_point.y))

def yaw(point, angle):
    # A Blender +Z rotation becomes this Godot +Y rotation on Y-up export.
    c, s = math.cos(angle), math.sin(angle)
    return Vector((c*point.x+s*point.z, point.y, -s*point.x+c*point.z))

def group(prefixes):
    return [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(prefixes)]

def geometry_points(objects):
    # Ignore unused vertices around pierced holes: only rendered faces count.
    for o in objects:
        if o.modifiers:
            raise RuntimeError("Apply modifiers before physical calibration: " + o.name)
        world = o.matrix_world
        indices = {i for face in o.data.polygons for i in face.vertices}
        for i in sorted(indices):
            yield godot_point(world @ o.data.vertices[i].co)

def bounds(points):
    points = list(points)
    if not points:
        raise RuntimeError("Cannot measure an empty geometry group")
    minimum = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    maximum = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return minimum, maximum, maximum-minimum

def apply_points(objects, transform):
    # Bake the complete world transform into each mesh so rotated subparts
    # receive the same group calibration, without local-axis scaling errors.
    for o in objects:
        world = o.matrix_world.copy()
        for vertex in o.data.vertices:
            vertex.co = gv(transform(godot_point(world @ vertex.co)))
        o.matrix_world = Matrix.Identity(4)
        o.data.update()
    bpy.context.view_layer.update()

def array3(vector):
    return [round(float(value), 9) for value in vector]

def bounds_record(points):
    minimum, maximum, size = bounds(points)
    return {"minimum_units": array3(minimum), "maximum_units": array3(maximum),
            "size_units": array3(size), "size_mm": array3(size*MM_PER_UNIT)}

def check_mm(label, actual, target):
    if abs(actual-target) > .03:
        raise RuntimeError("Physical calibration failed for %s: %.6f != %.6f mm" % (label, actual, target))

def finish_layout(name, objects, local_basis="X width / Y height / Z depth"):
    spec = PHYSICAL_SPEC["props"][name]
    local_points = list(geometry_points(objects))
    local_minimum, _, _ = bounds(local_points)
    anchor = Vector(spec["anchor_godot"])
    angle = -INK_BOX_ANGLE if name == "ink" else math.radians(spec["rotation_degrees_blender_z"])
    floor_shift = anchor.y + FLOOR_CLEARANCE-local_minimum.y
    apply_points(objects, lambda p: yaw(p, angle)+Vector((anchor.x, floor_shift, anchor.z)))
    world_record = bounds_record(geometry_points(objects))
    if abs(world_record["minimum_units"][1] - (anchor.y+FLOOR_CLEARANCE)) > .00001:
        raise RuntimeError("Prop did not land at the specified clearance: " + name)
    # Local measurements include the same floor correction, without layout yaw.
    local_record = bounds_record(p+Vector((0, floor_shift, 0)) for p in local_points)
    return {"target": spec, "anchor_godot": list(spec["anchor_godot"]),
            "rotation_degrees_blender_z": math.degrees(angle),
            "world_bounds": world_record, "local_bounds": local_record,
            "local_axes": local_basis, "bottom_y_units": world_record["minimum_units"][1],
            "objects": [o.name for o in objects]}

bpy.context.view_layer.update()
lamp_objects = group(("Lamp", "Chimney", "Burner"))
lens_objects = group("Lens")
ink_objects = group("Ink")
pen_objects = group(("Pen", "Nib"))
manifest = {"schema_version": 1, "mm_per_unit": MM_PER_UNIT,
            "book_outer_units": PHYSICAL_SPEC["book_outer_units"],
            "spec_source": "mcp/asset_pipeline/sources/Internal/Props/UIDesk/Painterly3D/desk_physical_spec.json",
            "spec_sha256": hashlib.sha256(SPEC_BYTES).hexdigest(),
            "coordinates": "Godot X/Y/Z; dimensions are measured from face-referenced mesh vertices",
            "floor_clearance_units": FLOOR_CLEARANCE, "props": {}}

# Lamp: foot and small hardware retain isotropic scale. Extra vertical length
# is assigned continuously to the reservoir and chimney; the wheel at y=1.02
# lies entirely in an unstretched band and stays circular.
lamp_spec = PHYSICAL_SPEC["props"]["lamp"]
lamp_source_min, lamp_source_max, lamp_source_size = bounds(geometry_points(lamp_objects))
foot_objects = [o for o in lamp_objects if o.name == "LampBase" or o.name.startswith("LampFootEngraving")]
_, _, foot_size = bounds(geometry_points(foot_objects))
lamp_radial_scale = (lamp_spec["foot_diameter_mm"]/MM_PER_UNIT)/max(foot_size.x, foot_size.z)
lamp_target_height = lamp_spec["height_mm"]/MM_PER_UNIT
stretch_bands = ((.36, .88), (1.30, lamp_source_max.y))
def stretch_distance(y):
    return sum(max(0.0, min(y, end)-start) for start, end in stretch_bands)
stretch_total = stretch_distance(lamp_source_max.y)-stretch_distance(lamp_source_min.y)
lamp_extra_scale = (lamp_target_height-lamp_source_size.y*lamp_radial_scale)/stretch_total
if lamp_radial_scale+lamp_extra_scale <= 0:
    raise ValueError("Lamp target height would invert a stretch segment")
def calibrate_lamp(p):
    y = (p.y-lamp_source_min.y)*lamp_radial_scale
    y += (stretch_distance(p.y)-stretch_distance(lamp_source_min.y))*lamp_extra_scale
    return Vector(((p.x-lx)*lamp_radial_scale, y, (p.z-lz)*lamp_radial_scale))
apply_points(lamp_objects, calibrate_lamp)
_, _, calibrated_foot = bounds(geometry_points(foot_objects))
lamp_actual_diameter = max(calibrated_foot.x, calibrated_foot.z)*MM_PER_UNIT
lamp_record = finish_layout("lamp", lamp_objects)
lamp_record["actual_foot_diameter_mm"] = lamp_actual_diameter
lamp_record["actual_height_mm"] = lamp_record["world_bounds"]["size_mm"][1]
lamp_record["calibration"] = {"radial_and_hardware_scale": lamp_radial_scale,
                              "extra_vertical_scale_in_bands": lamp_extra_scale,
                              "stretch_bands_source_y": stretch_bands}
check_mm("lamp foot", lamp_actual_diameter, lamp_spec["foot_diameter_mm"])
check_mm("lamp height", lamp_record["actual_height_mm"], lamp_spec["height_mm"])
wick_minimum, wick_maximum, _ = bounds(geometry_points([o for o in lamp_objects if o.name == "LampWick"]))
manifest["lamp_wick_tip_world"] = array3(Vector(((wick_minimum.x+wick_maximum.x)*.5,
                                              wick_maximum.y, (wick_minimum.z+wick_maximum.z)*.5)))
manifest["flame_height_mm"] = lamp_spec["flame_height_mm"]
manifest["flame_radius_mm"] = lamp_spec["flame_radius_mm"]
manifest["props"]["lamp"] = lamp_record

# Magnifier: only the measured glass diameter chooses scale. All other forms
# stay proportional; its final overall length is measured, never assumed.
lens_spec = PHYSICAL_SPEC["props"]["lens"]
optic_objects = [o for o in lens_objects if o.name == "LensOptic"]
_, _, optic_source_size = bounds(geometry_points(optic_objects))
lens_scale = (lens_spec["optic_diameter_mm"]/MM_PER_UNIT)/max(optic_source_size.x, optic_source_size.z)
apply_points(lens_objects, lambda p: (p-Vector((mx, 0, mz)))*lens_scale)
lens_axis = Vector((hb[0]-mx, 0, hb[2]-mz)).normalized()
lens_projections = [p.dot(lens_axis) for p in geometry_points(lens_objects)]
lens_length_mm = (max(lens_projections)-min(lens_projections))*MM_PER_UNIT
_, _, optic_calibrated_size = bounds(geometry_points(optic_objects))
lens_actual_diameter = max(optic_calibrated_size.x, optic_calibrated_size.z)*MM_PER_UNIT
lens_record = finish_layout("lens", lens_objects)
lens_record.update(actual_optic_diameter_mm=lens_actual_diameter, actual_overall_length_mm=lens_length_mm,
                   calibration={"uniform_scale": lens_scale, "length_axis_before_layout": array3(lens_axis)})
check_mm("lens optic", lens_actual_diameter, lens_spec["optic_diameter_mm"])
manifest["props"]["lens"] = lens_record

# Bottle: undo the authored box yaw, measure the whole local envelope, and
# calibrate its three axes independently. Reapply the same yaw at placement.
ink_spec = PHYSICAL_SPEC["props"]["ink"]
ink_source_anchor = Vector((ix, 0, iz))
apply_points(ink_objects, lambda p: yaw(p-ink_source_anchor, INK_BOX_ANGLE))
ink_minimum, _, ink_source_size = bounds(geometry_points(ink_objects))
ink_scale = Vector((ink_spec["width_mm"]/MM_PER_UNIT/ink_source_size.x,
                    ink_spec["height_mm"]/MM_PER_UNIT/ink_source_size.y,
                    ink_spec["depth_mm"]/MM_PER_UNIT/ink_source_size.z))
apply_points(ink_objects, lambda p: Vector((p.x*ink_scale.x, (p.y-ink_minimum.y)*ink_scale.y, p.z*ink_scale.z)))
ink_record = finish_layout("ink", ink_objects)
ink_record["calibration"] = {"local_xyz_scale": array3(ink_scale), "authored_box_angle_radians": INK_BOX_ANGLE}
ink_record["actual_width_mm"], ink_record["actual_height_mm"], ink_record["actual_depth_mm"] = ink_record["local_bounds"]["size_mm"]
for dimension in ("width", "height", "depth"):
    check_mm("ink "+dimension, ink_record["actual_"+dimension+"_mm"], ink_spec[dimension+"_mm"])
manifest["props"]["ink"] = ink_record

# Pen: construct an orthonormal frame along the authored barrel. This changes
# tip-to-tail length and circular cross section independently, not world X/Z.
pen_spec = PHYSICAL_SPEC["props"]["pen"]
pen_source_pivot = Vector((-.7, 0, 4.12))
pen_axis = Vector((pb[0]-pa[0], 0, pb[2]-pa[2])).normalized()
pen_side = pen_axis.cross(Vector((0, 1, 0))).normalized()
def pen_frame(p):
    p = p-pen_source_pivot
    return Vector((p.dot(pen_axis), p.y, p.dot(pen_side)))
apply_points(pen_objects, pen_frame)
_, _, pen_source_size = bounds(geometry_points(pen_objects))
barrel_objects = [o for o in pen_objects if o.name == "PenBarrel"]
_, _, barrel_source_size = bounds(geometry_points(barrel_objects))
pen_length_scale = (pen_spec["length_mm"]/MM_PER_UNIT)/pen_source_size.x
pen_radial_scale = (pen_spec["barrel_diameter_mm"]/MM_PER_UNIT)/max(barrel_source_size.y, barrel_source_size.z)
apply_points(pen_objects, lambda p: Vector((p.x*pen_length_scale, p.y*pen_radial_scale, p.z*pen_radial_scale)))
pen_local_points = list(geometry_points(pen_objects))
_, _, calibrated_pen_size = bounds(pen_local_points)
_, _, calibrated_barrel_size = bounds(geometry_points(barrel_objects))
pen_length_mm = calibrated_pen_size.x*MM_PER_UNIT
pen_diameter_mm = max(calibrated_barrel_size.y, calibrated_barrel_size.z)*MM_PER_UNIT
# Restore the authored direction before the manifest's Blender-Z layout yaw.
apply_points(pen_objects, lambda p: pen_axis*p.x+Vector((0, p.y, 0))+pen_side*p.z)
pen_record = finish_layout("pen", pen_objects, "X/Y/Z before layout; longitudinal measurements use barrel_axis_bounds")
pen_local_minimum, _, _ = bounds(pen_local_points)
pen_record["barrel_axis_bounds"] = bounds_record(p+Vector((0, FLOOR_CLEARANCE-pen_local_minimum.y, 0)) for p in pen_local_points)
pen_record["barrel_axis_bounds"]["axes"] = "X length along barrel / Y up / Z transverse"
pen_record.update(actual_length_mm=pen_length_mm, actual_barrel_diameter_mm=pen_diameter_mm,
                  calibration={"length_scale": pen_length_scale, "cross_section_scale": pen_radial_scale,
                               "authored_axis": array3(pen_axis), "authored_pivot": array3(pen_source_pivot)})
check_mm("pen length", pen_length_mm, pen_spec["length_mm"])
check_mm("pen barrel diameter", pen_diameter_mm, pen_spec["barrel_diameter_mm"])
manifest["props"]["pen"] = pen_record

# Preserve an editable source, export compact runtime geometry.
face_count=sum(len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH')
triangle_count=sum(max(0,len(p.vertices)-2) for o in bpy.data.objects if o.type=='MESH' for p in o.data.polygons)
if triangle_count >= 100000:
    raise RuntimeError("Desk geometry exceeds the 100k runtime triangle budget: %d" % triangle_count)
manifest["triangle_count"] = triangle_count
manifest["mesh_object_count"] = sum(o.type == 'MESH' for o in bpy.data.objects)
manifest_text = json.dumps(manifest, ensure_ascii=False, indent=2)+"\n"
(RUNTIME / "desk-physical-layout.json").write_text(manifest_text, encoding="utf-8")
(SOURCE / "desk-physical-layout.json").write_text(manifest_text, encoding="utf-8")
for name, contents in (("desk_physical_spec.json", SPEC_BYTES.decode("utf-8-sig")), ("desk-physical-layout.json", manifest_text)):
    text_block = bpy.data.texts.get(name) or bpy.data.texts.new(name)
    text_block.clear()
    text_block.write(contents)
bpy.context.scene["desk_physical_spec_sha256"] = manifest["spec_sha256"]
bpy.context.scene["desk_mm_per_unit"] = MM_PER_UNIT
bpy.ops.object.select_all(action='SELECT')
source_path=SOURCE / "painterly_desk.blend"
runtime_path=RUNTIME / "desk.glb"
bpy.ops.wm.save_as_mainfile(filepath=str(source_path))
bpy.ops.export_scene.gltf(filepath=str(runtime_path),export_format='GLB',use_selection=True,export_cameras=False,export_lights=False,export_apply=True,export_yup=True)
print("DESK_EXPORT",runtime_path,"mesh_faces",face_count,"triangles",triangle_count,"objects",len(bpy.data.objects))
print("DESK_PHYSICAL_LAYOUT", json.dumps({"lamp_wick_tip_world": manifest["lamp_wick_tip_world"],
      "props": {name: {key: value for key, value in item.items() if key.startswith("actual_") or key == "world_bounds"}
                for name, item in manifest["props"].items()}}, ensure_ascii=False))
