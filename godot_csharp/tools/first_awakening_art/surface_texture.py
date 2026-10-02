"""Fixed material masks and prefix grain sequence; no artifact or runtime dependencies."""
import numpy as np
from .pixel import mix,rng
from .palette import WALNUT,TILE
RECTS={"return_box":[(5,8,38,12),(5,22,38,20)],"spring_handle":[(4,21,56,12)],"spring_handle_pressed":[(4,21,56,12)],"filter_basin":[(5,25,86,14),(10,45,33,10),(51,45,33,10)]}

def material_masks(c,name):
    allowed=np.zeros((c.h,c.w),bool)
    for x,y,w,h in RECTS[name]:allowed[y:y+h,x:x+w]=True
    # Exact flat material shades protect all rims, knobs, slots, levers and paper.
    wood=allowed & (np.all(c.a==WALNUT[4],axis=2)|np.all(c.a==WALNUT[3],axis=2))
    stone=allowed & np.all(c.a==TILE[4],axis=2)
    return wood,stone

def apply(c,name,level):
    wood,stone=material_masks(c,name);changed=np.zeros(wood.shape,bool)
    original=c.a.copy();g=rng(407);factor=[0,1,3][level]
    # Prefix sequence: slab's area/18 short strokes; each stroke follows the surface grain.
    for mask,material in [(wood,"wood"),(stone,"stone")]:
        count=int(mask.sum()); attempts=({"return_box":32,"spring_handle":46,"spring_handle_pressed":46,"filter_basin":57}[name] if material=="wood" else 1)
        candidates=np.zeros(mask.shape,bool)
        for x,y,w,h in RECTS[name]:candidates[y:y+h,x:x+w]=True
        coords=np.argwhere(candidates if material=="wood" else stone)
        if len(coords)==0:continue
        for i in range(attempts*3):
            y,x=coords[int(g.integers(0,len(coords)))];length=int(g.integers(2,6)) if material=="wood" else 10
            for t in range(length):
                xx=int(x)+t;yy=int(y) if material=="wood" else int(y)-t//2
                if i>=attempts*factor or not (0<=xx<c.w and 0<=yy<c.h) or not mask[yy,xx]:continue
                base=tuple(int(v) for v in original[yy,xx])
                # Same material adjacent shade, fixed contrast across density levels.
                target=mix(WALNUT[4],WALNUT[3],0.55) if base==WALNUT[4] else mix(WALNUT[3],WALNUT[2],0.3)
                if material=="stone":target=mix(TILE[4],TILE[3],0.5)
                c.a[yy,xx]=target;changed[yy,xx]=True
    return c,wood,stone

