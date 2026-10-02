"""Native 1x Red Token fixtures. Shared life-wing materials and foot anchors."""
from pathlib import Path
import json
from PIL import Image, ImageDraw
from .pixel import Canvas
from .palette import WALNUT, BRASS, IRON, LINEN, CLARET, TEAL, FOG, INK, STONE, TILE
from .props import slab, drawer, leg, _finish, chair_back
from .surface_texture import apply as surface_texture

# Permanent placement and collision are authored together, independent of art bounds.
FIXTURES = [
    {"name":"reward_cabinet", "position":[512,602], "footprint":[458,588,108,14]},
    {"name":"assignment_board", "position":[408,558], "footprint":None},
    {"name":"handle_rack", "position":[424,652], "footprint":None},
    {"name":"spring_handle", "position":[424,720], "footprint":[394,700,60,20]},
    {"name":"return_box", "position":[548,730], "footprint":[526,716,44,14]},
    {"name":"paper_band", "position":[548,708], "footprint":None},
    {"name":"lead_seal", "position":[548,722], "footprint":None},
    {"name":"duty_chair", "position":[500,774], "footprint":[492,760,16,12]},
    {"name":"red_token", "position":[500,750], "footprint":None},
    {"name":"filter_basin", "position":[865,608], "footprint":[821,548,88,60]},
    {"name":"dual_inlet", "position":[865,566], "footprint":None},
    {"name":"capture_plate", "position":[850,546], "footprint":None},
    {"name":"cloth_clip", "position":[901,590], "footprint":None},
    {"name":"confirmation_card", "position":[922,574], "footprint":None},
    {"name":"sign_strip", "position":[512,438], "footprint":None},
    {"name":"white_ticket", "position":[536,433], "footprint":None},
    {"name":"pressure_route", "position":[528,912], "footprint":None,"floor_layer":True},
]
SITES = {"E01":[512,602],"E03":[424,720],"E08":[901,590],"E09":[865,608],
         "E10":[424,652],"E11":[408,558],"E12":[424,652],"E13":[548,730],
         "E14":[512,438],"E15":[500,774],"E16":[512,602]}
REPLACED = ["Washstand", "StudyLampE"]
OWNERS = {"paper_band":"return_box","lead_seal":"return_box","red_token":"duty_chair",
          "dual_inlet":"filter_basin","capture_plate":"filter_basin","cloth_clip":"filter_basin",
          "confirmation_card":"filter_basin","white_ticket":"sign_strip"}
for fixture in FIXTURES:
    if fixture["name"] in OWNERS: fixture["owner"]=OWNERS[fixture["name"]]

def screw(c,x,y):
    c.rect(x,y,3,3,IRON[0]); c.px(x,y,IRON[4]); c.px(x+1,y+1,IRON[2])

def bevel(c,x,y,w,h,r):
    c.rect(x,y,w,h,r[2]); c.frame(x,y,w,h,r[0])
    c.hline(x+1,y+1,w-2,r[4]); c.vline(x+1,y+1,h-2,r[3])
    c.hline(x+1,y+h-2,w-2,r[1]); c.vline(x+w-2,y+2,h-3,r[1])

def paper(c,x,y,w,h):
    c.rect(x+1,y+1,w,h,LINEN[0]); c.rect(x,y,w,h,LINEN[4]); c.hline(x+1,y+1,w-2,LINEN[5])
    for yy in range(y+5,y+h-2,4): c.hline(x+3,yy,w-6,LINEN[1])

def cabinet():
    c=Canvas(112,72)
    slab(c,2,8,108,7,54,WALNUT,seed=115)
    for x in [6,31,56,81]:
        bevel(c,x,19,23,40,WALNUT)
        bevel(c,x+3,22,17,13,BRASS)
        c.rect(x+5,24,13,9,TEAL[1])
        paper(c,x+7,26,9,5)
        c.vline(x+4,39,15,WALNUT[1]); c.vline(x+18,39,15,WALNUT[1])
        c.rect(x+15,43,4,3,BRASS[0]); c.hline(x+15,43,3,BRASS[4])
        screw(c,x+2,20); screw(c,x+18,53)
    c.rect(5,65,5,7,WALNUT[1]); c.rect(102,65,5,7,WALNUT[1])
    return c

def board():
    c=Canvas(48,48); bevel(c,2,2,44,44,WALNUT)
    c.rect(6,6,36,35,TEAL[1]); paper(c,8,8,15,29); paper(c,25,11,14,24)
    c.rect(12,6,7,4,BRASS[1]); c.hline(12,6,7,BRASS[4])
    c.rect(28,8,7,4,BRASS[1]); c.hline(28,8,7,BRASS[4])
    c.ellipse(17,31,3,3,CLARET[3]); c.ellipse(32,29,3,3,CLARET[3])
    c.line(23,14,25,14,LINEN[1]); c.line(23,25,25,25,LINEN[1])
    screw(c,3,3); screw(c,42,42); return c

def rack():
    c=Canvas(32,64); bevel(c,2,3,28,42,WALNUT)
    c.rect(4,43,4,19,WALNUT[1]); c.rect(24,43,4,19,WALNUT[1]); bevel(c,1,58,30,5,WALNUT)
    c.rect(6,8,20,31,IRON[1]); c.vline(15,13,21,IRON[0])
    c.line(10,11,20,11,BRASS[3]); c.line(20,11,20,19,BRASS[4]); c.line(20,19,15,24,BRASS[3])
    c.line(15,24,15,33,BRASS[2]); c.rect(12,32,7,5,IRON[3])
    c.px(13,33,IRON[5]); screw(c,4,5); screw(c,25,39); return c

def handle(pressed=False):
    c=Canvas(64,48)
    slab(c,2,20,60,14,6,WALNUT,grain=False,seed=116)
    # Compact mechanical bed seated directly on the worktop.
    slab(c,11,13,29,9,5,IRON,grain=False)
    c.rect(14,16,22,4,IRON[0])
    for x in range(15,34,4): c.line(x,17,x+2,19,IRON[4])
    c.ellipse(25,24,6,3,BRASS[1]); c.ellipse(24,23,4,2,BRASS[4])
    end=(48,22) if pressed else (43,5)
    c.line(25,24,*end,BRASS[0]); c.line(25,23,end[0],end[1]-1,BRASS[3]); c.line(25,22,end[0],end[1]-2,BRASS[5])
    c.rect(end[0]-4,end[1]-3,9,5,WALNUT[2]); c.hline(end[0]-4,end[1]-3,9,WALNUT[5])
    paper(c,44,25,13,6)
    c.hline(5,37,54,WALNUT[1])
    for x in (5,55):
        c.rect(x,40,4,8,WALNUT[3]); c.hline(x,42,4,WALNUT[5]); c.hline(x,46,4,WALNUT[1])
    return surface_texture(_finish(c),"spring_handle_pressed" if pressed else "spring_handle",1)[0]


def basin():
    c=Canvas(96,64)
    top=[WALNUT[0],WALNUT[1],WALNUT[2],WALNUT[3],TILE[4],TILE[5]]
    slab(c,3,24,90,16,19,top,grain=False)
    # Stone seam and recess establish a substantial washable counter.
    pass # stone surface distribution is applied below
    c.ellipse(44,31,31,8,IRON[1]); c.ellipse(44,30,29,7,IRON[4])
    c.ellipse(44,30,26,5,FOG[1]); c.hline(25,29,33,FOG[3]); c.ellipse(45,32,3,1.5,IRON[1])
    for x in (8,49):
        drawer(c,x,43,37,14,WALNUT)
        c.vline(x+2,45,9,WALNUT[4])
    c.rect(3,59,90,5,WALNUT[1]); c.hline(4,59,88,WALNUT[3])
    # Fold over the right counter edge; the detachable clip remains its own state asset.
    c.rect(80,27,9,16,LINEN[3]); c.hline(80,27,9,LINEN[5]); c.vline(81,28,13,LINEN[4]); c.vline(88,29,14,LINEN[1])
    return surface_texture(_finish(c),'filter_basin',1)[0]


def inlet():
    c=Canvas(64,48)
    for x,r in [(13,BRASS),(43,IRON)]:
        bevel(c,x-6,3,17,12,r)
        c.rect(x,12,5,20,r[1]); c.vline(x+1,12,19,r[4])
        c.rect(x-2,25,9,6,r[2]); c.hline(x-1,25,7,r[5])
        c.rect(x,30,5,7,r[2]); c.hline(x,36,5,r[0])
    # Both pipe mouths terminate above the bowl. The transparent gap is real art.
    c.rect(3,16,26,13,IRON[0]); c.frame(3,16,26,13,BRASS[2]); c.rect(6,19,20,6,FOG[0])
    c.px(9,22,STONE[3]); c.px(15,20,STONE[2]); c.px(21,23,STONE[3])
    c.hline(44,38,3,FOG[3]); c.px(45,41,FOG[2]); c.px(45,45,FOG[3])
    return c

def capture():
    c=Canvas(24,16); bevel(c,1,1,22,14,BRASS); c.rect(4,4,15,7,IRON[0])
    for x,y in [(5,5),(7,8),(11,6),(15,9),(17,5)]: c.px(x,y,STONE[3])
    c.rect(19,5,3,5,BRASS[3]); c.px(20,6,BRASS[5]); return c

def clip():
    c=Canvas(24,24); c.poly([(2,5),(19,3),(21,19),(5,23)],LINEN[2])
    c.line(4,7,7,20,LINEN[4]); c.line(10,5,12,20,LINEN[3]); c.line(18,5,19,16,LINEN[0])
    bevel(c,1,2,21,5,IRON); c.rect(5,1,4,3,BRASS[3]); c.rect(15,1,4,3,BRASS[3]); return c

def confirmation():
    c=Canvas(20,32); c.rect(7,25,6,7,WALNUT[2]); bevel(c,1,1,18,25,BRASS)
    c.rect(4,4,12,18,LINEN[4]); c.hline(6,8,8,LINEN[1]); c.hline(6,12,6,LINEN[1]);
    c.ellipse(10,18,3,2,TEAL[3]); return c

def receiver():
    c=Canvas(48,64)
    slab(c,3,7,42,14,34,WALNUT,grain=False,seed=118)
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
    return surface_texture(_finish(c),'return_box',1)[0]


def band():
    c=Canvas(32,8); paper(c,0,0,31,7); c.rect(4,2,5,3,CLARET[2]); c.px(8,4,LINEN[4]);
    c.rect(15,2,8,3,IRON[3]); c.vline(24,1,5,LINEN[1]); return c

def seal():
    c=Canvas(16,16); c.line(3,2,11,12,LINEN[2]); c.line(4,2,12,12,LINEN[4]);
    c.ellipse(4,5,3,3,IRON[2]); c.ellipse(4,4,3,2,IRON[4]);
    c.ellipse(12,11,3,3,IRON[1]); c.ellipse(12,10,3,2,IRON[3]); c.px(6,5,INK); c.px(10,9,INK); return c

def ticket():
    c=Canvas(16,16); paper(c,2,1,12,13); c.hline(5,5,6,IRON[2]); c.hline(5,9,4,IRON[2]); c.px(13,13,LINEN[1]); return c

def token():
    c=Canvas(16,16); c.poly([(2,2),(13,2),(14,9),(9,14),(2,14)],CLARET[0]);
    c.poly([(3,3),(12,3),(13,8),(8,13),(3,13)],CLARET[3]); c.hline(4,4,8,CLARET[5]);
    c.line(5,7,10,7,BRASS[3]); c.px(5,8,CLARET[1]); c.px(10,8,CLARET[1]); return c

def sign_strip():
    c=Canvas(48,32); bevel(c,2,1,44,30,WALNUT)
    for i,x in enumerate([5,15,25,35]):
        paper(c,x,7,8,20); c.rect(x+2,3,4,5,BRASS[2]); c.hline(x+2,3,4,BRASS[5]);
        c.line(x+2,20-i%2,x+5,22,IRON[2]); c.px(x+4,24,CLARET[3]);
    return c

def pressure_route():
    c=Canvas(160,216)
    for delta,color in [(-2,IRON[0]),(-1,BRASS[1]),(0,BRASS[3]),(1,BRASS[4])]:
        c.line(156+delta,214,156+delta,132,color)
        c.line(156,132+delta,8,132+delta,color)
        c.line(8+delta,132,8+delta,8,color)
    for x,y in [(156,180),(156,132),(108,132),(60,132),(8,132),(8,72),(8,8)]:
        bevel(c,x-3,y-3,7,7,BRASS)
    return c

def box_preview(state=0):
    c=Canvas(48,72); c.blit(receiver(),0,0)
    strip=Canvas(32,8); paper(strip,0,0,31,7)
    if state>=1: strip.rect(4,2,5,3,TEAL[4]); strip.px(8,4,LINEN[4])
    if state>=3: strip.rect(16,2,8,3,IRON[3]); c.blit(ticket(),16,56)
    if state>=2:
        strip.rect(11,1,5,5,CLARET[2]); strip.px(13,2,CLARET[4]); strip.px(15,5,LINEN[4])
    c.blit(strip,8,33); c.blit(seal(),16,43)
    return c

def basin_preview():
    c=Canvas(120,112)
    c.blit(basin(),0,48); c.blit(inlet(),16,6); c.blit(capture(),32,22)
    c.blit(clip(),79,70); c.blit(confirmation(),100,41)
    return c

def build():
    return {"reward_cabinet":cabinet(),"assignment_board":board(),"handle_rack":rack(),
            "spring_handle":handle(),"spring_handle_pressed":handle(True),"filter_basin":basin(),
            "dual_inlet":inlet(),"capture_plate":capture(),"cloth_clip":clip(),"confirmation_card":confirmation(),
            "return_box":receiver(),"paper_band":band(),"lead_seal":seal(),"white_ticket":ticket(),
            "red_token":token(),"duty_chair":chair_back(),"sign_strip":sign_strip(),"pressure_route":pressure_route(),
            "inspect_basin":basin_preview(),"inspect_box":box_preview(),"inspect_box_received":box_preview(1),"inspect_box_stamped":box_preview(2),"inspect_box_white":box_preview(3)}

def publish(game: Path):
    assets=build(); source=game/"AssetsSource/Internal/Props/FirstAwakeningInterior"; runtime=game/"ShareAsset/Internal/Props/FirstAwakeningInterior"
    for target in [source,runtime]:
        target.mkdir(parents=True,exist_ok=True)
        for name,canvas in assets.items(): canvas.save(target/("red_"+name+".png"))
    data={"fixtures":FIXTURES,"sites":SITES,"replaced":REPLACED,"cell":32}
    text='[gd_resource type="Resource" format=3]\n\n[ext_resource type="Script" path="res://scenes/FirstAwakening/LayoutResource.gd" id="layout"]\n\n[resource]\nscript = ExtResource("layout")\ndata = '+json.dumps(data,ensure_ascii=False,indent=1)+'\n'
    (runtime/"red_mechanisms.tres").write_text(text,encoding="utf-8")
    manifest={name:{"size":[canvas.w,canvas.h],"source":str((source/("red_"+name+".png")).relative_to(game)).replace('\\','/'),"runtime":str((runtime/("red_"+name+".png")).relative_to(game)).replace('\\','/'),"process":"native 1x pixels; shared life-wing material ramps; bottom-centre anchor"} for name,canvas in assets.items()}
    (runtime/"red_mechanisms_manifest.json").write_text(json.dumps({"assets":manifest,**data},ensure_ascii=False,indent=2),encoding="utf-8")
    report=game.parent/"artifacts/first-awakening"; report.mkdir(parents=True,exist_ok=True)
    sheet=Image.new("RGBA",(768,520),(233,226,208,255)); draw=ImageDraw.Draw(sheet)
    preview_names=[f["name"] for f in FIXTURES if f["name"]!="pressure_route"]+["spring_handle_pressed"]
    for i,name in enumerate(preview_names):
        canvas=assets[name]
        x=(i%4)*192; y=(i//4)*104
        im=Image.fromarray(canvas.a,"RGBA"); sheet.alpha_composite(im,(x+8,y+20))
        enlarged=im.resize((im.width*2,im.height*2),Image.Resampling.NEAREST)
        # Separate comparison sheet accommodates the larger multi-cell fixtures.
        draw.text((x+8,y+4),name,fill=(48,44,38,255))
    sheet.save(report/"red-token-props-native.png")
    sheet.resize((1536,1040),Image.Resampling.NEAREST).save(report/"red-token-props-2x.png")
    return manifest

if __name__=="__main__": publish(Path(__file__).resolve().parents[2])
