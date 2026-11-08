"""Actual cut-corner floor tiles, diamond inlays and nonoverlapping borders."""
import math
import random
from .core import group, box, mesh


def clip_rect(polygon, bounds):
    """Sutherland-Hodgman clipping against an axis-aligned XY rectangle."""
    xmin,xmax,ymin,ymax=bounds
    result=list(polygon)
    for axis,bound,sign in ((0,xmin,1),(0,xmax,-1),(1,ymin,1),(1,ymax,-1)):
        source,result=result,[]
        if not source:
            break
        for a,b in zip(source,source[1:]+source[:1]):
            va=(a[axis]-bound)*sign
            vb=(b[axis]-bound)*sign
            ina,inb=va>=-1e-9,vb>=-1e-9
            if ina:
                result.append(a)
            if ina!=inb:
                t=(bound-a[axis])/(b[axis]-a[axis])
                result.append((a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])))
    clean=[]
    for p in result:
        if not clean or math.dist(p,clean[-1])>1e-8:
            clean.append(p)
    if len(clean)>2 and math.dist(clean[0],clean[-1])<1e-8:
        clean.pop()
    if len(clean)<3:
        return []
    area=sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(clean,clean[1:]+clean[:1]))*.5
    return clean if area>1e-9 else []


def inset_convex(poly, distance):
    """Offset a CCW convex contour inwards, preserving real grout width."""
    lines=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        dx,dy=b[0]-a[0],b[1]-a[1]
        length=math.hypot(dx,dy)
        nx,ny=-dy/length,dx/length
        lines.append(((a[0]+nx*distance,a[1]+ny*distance),(dx,dy)))
    out=[]
    for i,(p,v) in enumerate(lines):
        q,w=lines[i-1]
        den=v[0]*w[1]-v[1]*w[0]
        t=((q[0]-p[0])*w[1]-(q[1]-p[1])*w[0])/den
        out.append((p[0]+t*v[0],p[1]+t*v[1]))
    return out


class Batch:
    def __init__(self):
        self.verts=[]; self.faces=[]
    def prism(self,poly,z0,z1):
        n=len(poly)
        if n<3:return
        k=len(self.verts)
        self.verts.extend((x,y,z0) for x,y in poly)
        self.verts.extend((x,y,z1) for x,y in poly)
        self.faces.append(tuple(k+i for i in reversed(range(n))))
        self.faces.append(tuple(k+n+i for i in range(n)))
        self.faces.extend((k+i,k+(i+1)%n,k+(i+1)%n+n,k+i+n) for i in range(n))
    def finish(self,name,mat,parent,bevel=0):
        return mesh(name,self.verts,self.faces,mat,parent,bevel) if self.verts else None


def _ring(batch, outer, inner, z=.038):
    ox0,ox1,oy0,oy1=outer
    ix0,ix1,iy0,iy1=inner
    for p in [[(ox0,oy0),(ox1,oy0),(ix1,iy0),(ix0,iy0)],
              [(ox1,oy0),(ox1,oy1),(ix1,iy1),(ix1,iy0)],
              [(ox1,oy1),(ox0,oy1),(ix0,iy1),(ix1,iy1)],
              [(ox0,oy1),(ox0,oy0),(ix0,iy0),(ix0,iy1)]]:
        batch.prism(p,.012,z)


def tiled_floor(name,mats,width=8,depth=6,tile=.8,cut=.12,grout=.012,border=.42,seed=12,parent=None):
    """Mosaic: octagonal tiles + square diamonds; each region is real mesh.

    One mesh per colour, including clipped perimeter tiles. No coplanar
    texture overlays and no Boolean-per-tile construction.
    """
    if not (0<cut<tile/2 and 0<=grout<cut/2 and border>=.35 and min(width,depth)>2*border):
        raise ValueError('invalid tile dimensions / border')
    root=group(name,parent=parent)
    root['generator']='tiled_floor';root['tile_size']=tile;root['grout_width']=grout
    box(name+'_Foundation',(0,0,-.13),(width,depth,.26),mats['stone_dark'],.035,root)
    box(name+'_Grout',(0,0,.006),(width,depth,.012),mats['grout'],0,root)
    def rect(d):return(-width/2+d,width/2-d,-depth/2+d,depth/2-d)
    rings=[(0,.10,'stone'),(.10,.17,'stone_dark'),(.17,.29,'stone'),(.29,.35,'stone_dark'),(.35,border,'stone')]
    for a,b,col in rings:
        batch=Batch();_ring(batch,rect(a),rect(b));batch.finish(name+'_Border_'+str(a),mats[col],root)
    bounds=rect(border)
    shapes=[Batch() for _ in range(5)];diamonds=Batch();rng=random.Random(seed)
    s,d=tile,cut
    octagon=[(d,0),(s-d,0),(s,d),(s,s-d),(s-d,s),(d,s),(0,s-d),(0,d)]
    diamond=[(0,-d),(d,0),(0,d),(-d,0)]
    octagon=inset_convex(octagon,grout/2)
    diamond=inset_convex(diamond,grout/2)
    for i in range(math.floor(bounds[0]/s)-1,math.ceil(bounds[1]/s)+1):
        for j in range(math.floor(bounds[2]/s)-1,math.ceil(bounds[3]/s)+1):
            poly=clip_rect([(x+i*s,y+j*s) for x,y in octagon],bounds)
            shapes[rng.randrange(5)].prism(poly,.012,.04)
            poly=clip_rect([(x+i*s,y+j*s) for x,y in diamond],bounds)
            diamonds.prism(poly,.012,.04)
    for i,b in enumerate(shapes):b.finish(name+'_Octagons_'+str(i),mats['tile_'+str(i)],root,0.004)
    diamonds.finish(name+'_Diamond_inlays',mats['stone_dark'],root,.003)
    return root
