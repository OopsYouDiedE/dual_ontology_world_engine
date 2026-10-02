"""Surface-only density experiment. Publish only after explicit art selection."""
from pathlib import Path
import json,importlib.util,hashlib,shutil
import numpy as np
from PIL import Image,ImageDraw
from .pixel import Canvas,mix,rng
from .palette import WALNUT,TILE,BOTTLE
from . import props,red_token
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/"artifacts/first-awakening/style-study/texture-study"
VARIANTS=["texture-low","texture-reference","texture-high"]
NAMES=["return_box","spring_handle","spring_handle_pressed","filter_basin"]
RECTS={"return_box":[(5,8,38,12),(5,22,38,20)],"spring_handle":[(4,21,56,12)],"spring_handle_pressed":[(4,21,56,12)],"filter_basin":[(5,25,86,14),(10,45,33,10),(51,45,33,10)]}

def snapshot():
    target=OUT/"baseline";target.mkdir(parents=True,exist_ok=True)
    for name in NAMES+["inspect_basin","inspect_box","inspect_box_received","inspect_box_stamped","inspect_box_white"]:
        f="red_"+name+".png"
        if not (target/f).exists(): shutil.copy2(ROOT/"godot_csharp/ShareAsset/Internal/Props/FirstAwakeningInterior"/f,target/f)
    if not (target/"red_token.py").exists():shutil.copy2(Path(red_token.__file__),target/"red_token.py")

def clean_builders():
    # Evaluate the frozen recipe with only surface marks suppressed.
    text=(OUT/"baseline/red_token.py").read_text()
    text=text.replace("from .props import slab, drawer, leg, _finish, chair_back","from .props import slab as original_slab, drawer, leg, _finish, chair_back\ndef slab(*args,**kwargs):\n    kwargs['grain']=False\n    return original_slab(*args,**kwargs)")
    text=text.replace("c.line(8,35,19,27,TILE[2]); c.line(72,37,82,27,TILE[3])","pass # suppressed surface stone veins")
    namespace={"__name__":"first_awakening_art.texture_clean","__package__":"first_awakening_art"};exec(text,namespace)
    return namespace

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

def generate():
    OUT.mkdir(parents=True,exist_ok=True);snapshot();recipes=clean_builders();rows=[];assets={}
    functions={"return_box":lambda:recipes["receiver"](),"spring_handle":lambda:recipes["handle"](),"spring_handle_pressed":lambda:recipes["handle"](True),"filter_basin":lambda:recipes["basin"]()}
    for name,build in functions.items():
        base=build();wood,stone=material_masks(base,name);mask=wood|stone
        Image.fromarray(np.stack([mask*255]*3+[mask*255],axis=2).astype('uint8'),"RGBA").save(OUT/(name+"-allowed-mask.png"))
        for level,label in enumerate(VARIANTS):
            c,_,_=apply(build(),name,level);delta=np.any(c.a!=base.a,axis=2)
            assert np.array_equal(c.a[:,:,3],base.a[:,:,3]) and not np.any(delta & ~mask)
            assets[(label,name)]=c
            Image.fromarray(c.a,"RGBA").save(OUT/(name+"-"+label+".png"))
            rows.append({"asset":name,"variant":label,"size":[c.w,c.h],"allowed_pixels":int(mask.sum()),"changed_pixels":int(delta.sum()),"actual_coverage_percent":round(100*delta.sum()/max(1,mask.sum()),3),"wood_actual_percent":round(100*(delta & wood).sum()/max(1,wood.sum()),3),"stone_actual_percent":round(100*(delta & stone).sum()/max(1,stone.sum()),3),"stone_pixels":int(stone.sum()),"wood_pixels":int(wood.sum()),"alpha_geometry_identical":True,"outside_mask_changed":0})
    common=material_masks(functions['spring_handle'](),'spring_handle')[0] & material_masks(functions['spring_handle_pressed'](),'spring_handle_pressed')[0]
    for label in VARIANTS:
        assert np.array_equal(assets[(label,'spring_handle')].a[common],assets[(label,'spring_handle_pressed')].a[common])
    sheet=Image.new('RGBA',(960,660),(233,226,208,255));d=ImageDraw.Draw(sheet)
    for col,label in enumerate(VARIANTS):
        d.text((8+col*320,8),label,fill=(40,42,38))
        for row,name in enumerate(NAMES):
            y=35+row*150;d.text((8+col*320,y),name,fill=(40,42,38));im=Image.fromarray(assets[(label,name)].a,'RGBA');sheet.alpha_composite(im,(8+col*320,y+20));sheet.alpha_composite(im.resize((im.width*2,im.height*2),Image.Resampling.NEAREST),(112+col*320,y+20))
    sheet.save(OUT/'texture-comparison-native-2x.png');sheet.resize((1920,1320),Image.Resampling.NEAREST).save(OUT/'texture-comparison-2x-4x.png')
    control=Image.new('RGBA',(384,96),(233,226,208,255));d=ImageDraw.Draw(control)
    for i,density in enumerate([0,0.125,0.25]):
        c=Canvas(32,32,BOTTLE[3]);c.dither(0,0,32,32,BOTTLE[4],density);n=int(np.all(c.a==BOTTLE[4],axis=2).sum());assert n==1024*density
        d.text((i*128,0),str(density*100)+'%',fill=(40,42,38));control.alpha_composite(Image.fromarray(c.a,'RGBA').resize((64,64),Image.Resampling.NEAREST),(i*128,20))
    control.save(OUT/'baize-bayer-control.png')
    (OUT/'parameters.json').write_text(json.dumps({"checks":"author development, not independent QA","reference":"props.slab area/18, length2..5; washstand one sparse diagonal vein on48x14 stone; architecture horizontal low-contrast grains","density_levels":[0,1,3],"same_seed":407,"same_sequence_prefix":True,"rest_pressed_common_wood_identical":True,"bayer_control":[0,0.125,0.25],"assets":rows},indent=2))
    return assets

def stage(label):
    assets=generate();recipes=clean_builders()
    if label=='baseline':
        for folder in ['AssetsSource','ShareAsset']:
            target=ROOT/'godot_csharp'/folder/'Internal/Props/FirstAwakeningInterior'
            for file in (OUT/'baseline').glob('*.png'):shutil.copy2(file,target/file.name)
        return
    assert label in VARIANTS
    recipes['receiver']=lambda:assets[(label,'return_box')]
    recipes['handle']=lambda pressed=False:assets[(label,'spring_handle_pressed' if pressed else 'spring_handle')]
    recipes['basin']=lambda:assets[(label,'filter_basin')]
    built=recipes['build']()
    for name in NAMES+['inspect_basin','inspect_box','inspect_box_received','inspect_box_stamped','inspect_box_white']:
        for folder in ['AssetsSource','ShareAsset']:
            target=ROOT/'godot_csharp'/folder/'Internal/Props/FirstAwakeningInterior'/('red_'+name+'.png')
            Image.fromarray(built[name].a,'RGBA').save(target)

if __name__=='__main__':
    import sys
    stage(sys.argv[2]) if len(sys.argv)>2 and sys.argv[1]=='--stage' else generate()
    print(OUT)
