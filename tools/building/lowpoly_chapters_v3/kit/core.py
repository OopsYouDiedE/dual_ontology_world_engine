"""Small mesh primitives and one shared solid-colour material palette.

Blender coordinates: X right, Y back, Z up; component fronts face -Y.
All child transforms are LOCAL to parent. There are no context-sensitive
primitive operators here; geometry is deterministic and seed-independent.
"""
import math
import bpy
import bmesh
from mathutils import Vector


def group(name, location=(0, 0, 0), rotation=0, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.empty_display_type = 'PLAIN_AXES'
    obj.empty_display_size = .12
    obj.parent = parent
    obj.location = location
    obj.rotation_euler[2] = rotation
    return obj


def mesh(name, vertices, faces, mat, parent=None, bevel=0):
    data = bpy.data.meshes.new(name + '_Mesh')
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    if mat is not None:
        data.materials.append(mat)
    # Consistent normals are essential when back faces are hidden.
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    if bevel > 0:
        mod = obj.modifiers.new('One_segment_edge_highlight', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        mod.limit_method = 'ANGLE'
        mod.angle_limit = math.radians(28)
        mod.use_clamp_overlap = True
    for face in data.polygons:
        face.use_smooth = False
    return obj


def box(name, location, size, mat, bevel=.015, parent=None):
    x, y, z = [max(1e-6, float(v)) / 2 for v in size]
    vertices = [(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),
                (-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    faces = [(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]
    obj = mesh(name, vertices, faces, mat, parent, min(bevel, min(x,y,z)*.45))
    obj.location = location
    return obj


def cylinder(name, location, radius, depth, mat, vertices=8, parent=None):
    n = max(3, int(vertices))
    points = [(radius*math.cos(2*math.pi*i/n), radius*math.sin(2*math.pi*i/n)) for i in range(n)]
    obj = polyprism(name, points, -depth/2, depth/2, mat, parent)
    obj.location = location
    return obj


def ico(name, location, radius, mat, subdivisions=1, parent=None):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius)
    data = bpy.data.meshes.new(name + '_Mesh')
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    data.materials.append(mat)
    return obj


def beam(name, a, b, width, depth, mat, parent=None, bevel=.008):
    av, bv = Vector(a), Vector(b)
    delta = bv-av
    if delta.length < 1e-7:
        raise ValueError('beam endpoints must differ: '+name)
    obj = box(name, (av+bv)*.5, (width, depth, delta.length), mat, bevel, parent)
    obj.rotation_euler = delta.to_track_quat('Z', 'Y').to_euler()
    return obj


def lathe(name, profile, sides, mat, parent=None, location=(0,0,0)):
    """Revolve ordered (radius,z) profile, including inner profile if hollow."""
    sides = max(3, int(sides))
    vertices = []
    rings = []
    for radius, z in profile:
        if abs(radius) < 1e-8:
            rings.append([len(vertices)])
            vertices.append((0,0,z))
        else:
            rings.append(list(range(len(vertices), len(vertices)+sides)))
            for j in range(sides):
                theta = 2*math.pi*j/sides + math.pi/8
                vertices.append((radius*math.cos(theta),radius*math.sin(theta),z))
    faces=[]
    for ra, rb in zip(rings,rings[1:]):
        for j in range(sides):
            k=(j+1)%sides
            if len(ra)==1 and len(rb)==1:
                continue
            elif len(ra)==1:
                faces.append((ra[0],rb[k],rb[j]))
            elif len(rb)==1:
                faces.append((ra[j],ra[k],rb[0]))
            else:
                faces.append((ra[j],ra[k],rb[k],rb[j]))
    if len(rings[0])>1:
        faces.append(tuple(reversed(rings[0])))
    if len(rings[-1])>1:
        faces.append(tuple(rings[-1]))
    obj=mesh(name,vertices,faces,mat,parent)
    obj.location=location
    return obj


def polyprism(name, points, bottom, top, mat, parent=None):
    """Extrude an ordered XY polygon. N-gon caps handled by Blender."""
    n=len(points)
    if n<3 or top<=bottom:
        raise ValueError('polyprism requires >=3 vertices and positive height')
    vs=[(x,y,bottom) for x,y in points]+[(x,y,top) for x,y in points]
    fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    fs += [(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)]
    return mesh(name,vs,fs,mat,parent)


def srgb_hex(code):
    code=code.lstrip('#')
    rgb=[int(code[i:i+2],16)/255 for i in (0,2,4)]
    return tuple(c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in rgb)+(1,)


def solid_material(name, color, roughness=.62, metallic=0, emission=0, glass=False):
    mat=bpy.data.materials.new(name)
    mat.diffuse_color=srgb_hex(color)
    mat.use_nodes=True
    mat.use_backface_culling=True
    if hasattr(mat,'use_backface_culling_shadow'):
        mat.use_backface_culling_shadow=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    nodes.clear()
    out=nodes.new('ShaderNodeOutputMaterial')
    out.location=(620,0)
    bsdf=nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.location=(-200,100)
    bsdf.inputs['Base Color'].default_value=mat.diffuse_color
    bsdf.inputs['Roughness'].default_value=roughness
    bsdf.inputs['Metallic'].default_value=metallic
    bsdf.inputs['Emission Color'].default_value=mat.diffuse_color
    bsdf.inputs['Emission Strength'].default_value=emission
    transparent=nodes.new('ShaderNodeBsdfTransparent')
    transparent.location=(-180,-260)
    shader=bsdf.outputs[0]
    if glass:
        # Stylized pane: coloured to camera, transparent to shadow rays.
        # Sun shadows come only from the real opening, frame and mullions.
        lp=nodes.new('ShaderNodeLightPath')
        lp.location=(-600,350)
        pane_mix=nodes.new('ShaderNodeMixShader')
        pane_mix.location=(50,170)
        links.new(lp.outputs['Is Shadow Ray'],pane_mix.inputs[0])
        links.new(shader,pane_mix.inputs[1])
        links.new(transparent.outputs[0],pane_mix.inputs[2])
        shader=pane_mix.outputs[0]
        mat['pane_model']='camera_coloured_shadow_transparent'
    geometry=nodes.new('ShaderNodeNewGeometry')
    geometry.location=(30,-320)
    cull=nodes.new('ShaderNodeMixShader')
    cull.location=(380,0)
    cull.label='Cycles: transparent back faces'
    links.new(geometry.outputs['Backfacing'],cull.inputs[0])
    links.new(shader,cull.inputs[1])
    links.new(transparent.outputs[0],cull.inputs[2])
    links.new(cull.outputs[0],out.inputs['Surface'])
    mat['solid_colour']=color
    mat['backface_culling']='EEVEE flag + Cycles Backfacing shader'
    return mat


def palette():
    specs={
      'wood':('#6D3C28',.6,0,0), 'wood_dark':('#402319',.68,0,0),
      'wood_light':('#915A3D',.56,0,0), 'wall':('#AD8158',.82,0,0),
      'stone':('#C9B698',.82,0,0), 'stone_dark':('#82756A',.76,0,0),
      'grout':('#958775',.85,0,0), 'gold':('#CAA15A',.3,.68,0),
      'green':('#40563C',.42,.05,0), 'leaf':('#52613A',.78,0,0),
      'cream':('#E7D6AA',.72,0,0), 'emission':('#FFD58B',.32,0,3),
      'book_red':('#7A4937',.72,0,0), 'book_blue':('#455961',.72,0,0),
      'book_green':('#4C5740',.72,0,0), 'glass':('#8BAAB7',.35,0,.12),
    }
    mats={k:solid_material('LP_'+k,*v,glass=k=='glass') for k,v in specs.items()}
    for i,c in enumerate(['#C6B191','#D2BFA1','#CDB79A','#BEAC92','#D3C1A5']):
        mats['tile_'+str(i)]=solid_material('LP_tile_'+str(i),c,.83)
    return mats


def place(obj, location=(0,0,0), rotation=0):
    obj.location=location
    obj.rotation_euler[2]=rotation
    return obj


def mesh_statistics():
    dg=bpy.context.evaluated_depsgraph_get()
    objects=vertices=triangles=0
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or obj.hide_render:
            continue
        evaluated=obj.evaluated_get(dg)
        me=evaluated.to_mesh()
        me.calc_loop_triangles()
        objects+=1;vertices+=len(me.vertices);triangles+=len(me.loop_triangles)
        evaluated.to_mesh_clear()
    return dict(mesh_objects=objects,evaluated_vertices=vertices,evaluated_triangles=triangles,
                materials=len(bpy.data.materials))
