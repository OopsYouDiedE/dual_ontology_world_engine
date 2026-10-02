"""Controlled native-style experiment; never publishes runtime assets implicitly."""
from pathlib import Path
from PIL import Image, ImageDraw
from .pixel import Canvas
from .palette import WALNUT, BRASS, IRON, LINEN, CLARET, TEAL, FOG, TILE, STONE
from .props import slab, drawer, leg, _finish, washstand, study_table, bookcase
import importlib.util
_base=Path(__file__).resolve().parents[3]/"artifacts/first-awakening/style-study/baseline/red_token.py"
_spec=importlib.util.spec_from_file_location("first_awakening_art.style_baseline",_base)
old=importlib.util.module_from_spec(_spec);_spec.loader.exec_module(old)

def receiver():
    c=Canvas(48,64)
    slab(c,3,7,42,14,34,WALNUT,seed=118)
    for x,ramp in [(7,BRASS),(27,CLARET)]:
        # Slots lie in a receding top, with a dark lip and a visible brass throat.
        c.rect(x,11,13,6,IRON[1]); c.hline(x,11,13,IRON[0])
        c.hline(x+1,16,11,ramp[3]); c.hline(x+2,13,9,IRON[0])
        c.px(x+1,12,ramp[4])
    c.rect(7,25,34,16,WALNUT[2]); c.hline(7,25,34,WALNUT[1])
    c.rect(9,28,30,10,IRON[1]); c.hline(9,28,30,IRON[0]); c.hline(10,37,28,BRASS[2])
    drawer(c,6,44,36,10,WALNUT)
    for x in (5,39):
        leg(c,x,55,9,WALNUT); c.vline(x+2,55,9,WALNUT[3])
    return _finish(c)

def handle(pressed=False):
    c=Canvas(64,48)
    slab(c,2,20,60,14,6,WALNUT,seed=116)
    # Compact mechanical bed seated directly on the worktop.
    slab(c,11,13,29,9,5,IRON,grain=False)
    c.rect(14,16,22,4,IRON[0])
    for x in range(15,34,4): c.line(x,17,x+2,19,IRON[4])
    c.ellipse(25,24,6,3,BRASS[1]); c.ellipse(24,23,4,2,BRASS[4])
    end=(48,22) if pressed else (43,5)
    c.line(25,24,*end,BRASS[0]); c.line(25,23,end[0],end[1]-1,BRASS[3]); c.line(25,22,end[0],end[1]-2,BRASS[5])
    c.rect(end[0]-4,end[1]-3,9,5,WALNUT[2]); c.hline(end[0]-4,end[1]-3,9,WALNUT[5])
    old.paper(c,44,25,13,6)
    c.hline(5,37,54,WALNUT[1])
    for x in (5,55):
        c.rect(x,40,4,8,WALNUT[3]); c.hline(x,42,4,WALNUT[5]); c.hline(x,46,4,WALNUT[1])
    return _finish(c)

def basin():
    c=Canvas(96,64)
    top=[WALNUT[0],WALNUT[1],WALNUT[2],WALNUT[3],TILE[4],TILE[5]]
    slab(c,3,24,90,16,19,top,grain=False)
    # Stone seam and recess establish a substantial washable counter.
    c.line(8,35,19,27,TILE[2]); c.line(72,37,82,27,TILE[3])
    c.ellipse(44,31,31,8,IRON[1]); c.ellipse(44,30,29,7,IRON[4])
    c.ellipse(44,30,26,5,FOG[1]); c.hline(25,29,33,FOG[3]); c.ellipse(45,32,3,1.5,IRON[1])
    for x in (8,49):
        drawer(c,x,43,37,14,WALNUT)
        c.vline(x+2,45,9,WALNUT[4])
    c.rect(3,59,90,5,WALNUT[1]); c.hline(4,59,88,WALNUT[3])
    # Fold over the right counter edge; the detachable clip remains its own state asset.
    c.rect(80,27,9,16,LINEN[3]); c.hline(80,27,9,LINEN[5]); c.vline(81,28,13,LINEN[4]); c.vline(88,29,14,LINEN[1])
    return _finish(c)

BUILDERS={"return_box":(old.receiver,receiver),"spring_handle":(old.handle,handle),"spring_handle_pressed":(lambda:old.handle(True),lambda:handle(True)),"filter_basin":(old.basin,basin)}

def generate():
    root=Path(__file__).resolve().parents[3]; out=root/"artifacts/first-awakening/style-study"; out.mkdir(parents=True,exist_ok=True)
    result={}
    for name,(a,c) in BUILDERS.items():
        result[name]=[a(),_finish(a()),c()]
        for label,canvas in zip("ABC",result[name]): Image.fromarray(canvas.a,"RGBA").save(out/(name+"_"+label+".png"))
    sheet=Image.new("RGBA",(960,1040),(233,226,208,255)); d=ImageDraw.Draw(sheet)
    for i,text in enumerate(["A BASELINE","B FINISH ONLY","C FORM / MATERIAL"]):d.text((12+i*320,8),text,fill=(40,42,38))
    for row,(name,variants) in enumerate(result.items()):
        y=35+row*150; d.text((8,y),name,fill=(40,42,38))
        for col,canvas in enumerate(variants):
            im=Image.fromarray(canvas.a,"RGBA"); sheet.alpha_composite(im,(12+320*col,y+17))
            sheet.alpha_composite(im.resize((im.width*2,im.height*2),Image.Resampling.NEAREST),(112+320*col,y+13))
    # Original furniture references at the same native / 2x scale.
    y=660
    for i,(name,canvas) in enumerate([("washstand",washstand()),("bookcase",bookcase(19)),("study_table",study_table())]):
        im=Image.fromarray(canvas.a,"RGBA");d.text((8+i*320,y),name,fill=(40,42,38));sheet.alpha_composite(im,(8+i*320,y+16));sheet.alpha_composite(im.resize((im.width*2,im.height*2),Image.Resampling.NEAREST),(8+i*320,y+130))
    sheet.save(out/"abc-native-2x.png");sheet.resize((1920,2080),Image.Resampling.NEAREST).save(out/"abc-2x-4x.png")
    # Combined near-view, same coordinate placements as the runtime layout.
    for label in "ABC":
        c=Canvas(120,112); c.blit(result["filter_basin"]["ABC".index(label)],0,48);c.blit(old.inlet(),16,6);c.blit(old.capture(),32,22);c.blit(old.clip(),79,70 if label == "C" else 46);c.blit(old.confirmation(),100,41)
        Image.fromarray(c.a,"RGBA").save(out/("basin_combined_"+label+".png"))
    return out

if __name__ == "__main__": print(generate())
