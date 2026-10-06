"""Native 1x Red Token fixtures. Shared life-wing materials and foot anchors."""
from pathlib import Path
import json
from PIL import Image, ImageDraw
from .pixel import Canvas
from .palette import WALNUT, BRASS, IRON, LINEN, CLARET, TEAL, FOG, INK, STONE, TILE
from .props import slab, drawer, leg, _finish, chair_back

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
    """Two framed leaves, four visible bays and a load-bearing plinth."""
    c=Canvas(112,72)
    # Projecting cornice and dark end grain separate the top from the facade.
    slab(c,2,8,108,6,3,WALNUT,grain=False)
    c.rect(4,17,104,45,WALNUT[2]); c.vline(4,17,45,WALNUT[4])
    c.rect(7,18,98,43,WALNUT[0])
    for x in (8,57):
        bevel(c,x,18,47,42,WALNUT)
        c.rect(x+4,22,39,31,WALNUT[0])
        # The two bays in each leaf are shelves, not four miniature doors.
        for yy in (23,39):
            c.rect(x+5,yy,37,12,TEAL[1]); c.vline(x+5,yy,12,TEAL[2])
            c.hline(x+5,yy+11,37,WALNUT[4])
            c.rect(x+8,yy+4,15,6,LINEN[3]); c.hline(x+8,yy+4,15,LINEN[5])
            c.hline(x+10,yy+6,10,LINEN[1]); c.rect(x+26,yy+3,11,7,IRON[2])
            c.hline(x+26,yy+3,11,IRON[4]); c.rect(x+29,yy+5,5,3,BRASS[2])
        c.hline(x+4,36,39,WALNUT[5]); c.hline(x+4,37,39,WALNUT[1])
        c.hline(x+6,55,34,WALNUT[3])
    # Hinges stay on the outside edges; paired pulls meet at the centre seam.
    for x in (7,102):
        for yy in (24,48):
            c.rect(x,yy,3,6,IRON[1]); c.vline(x,yy,5,IRON[4])
    for x in (49,59):
        c.rect(x,40,3,8,BRASS[0]); c.vline(x,40,7,BRASS[4]); c.px(x+1,47,BRASS[2])
    c.vline(55,18,42,WALNUT[0]); c.vline(56,18,42,WALNUT[4])
    bevel(c,3,61,106,6,WALNUT)
    for x in (5,101):
        c.rect(x,67,6,5,WALNUT[1]); c.vline(x,67,5,WALNUT[3])
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
    # A bolted bed, spring guide and upright cheek carry the same pivot in both states.
    slab(c,8,16,31,7,4,IRON,grain=False)
    c.rect(10,18,27,4,IRON[0]); c.hline(11,20,24,IRON[3])
    c.rect(11,16,3,7,IRON[3]); c.hline(11,16,3,IRON[5])
    spring_end=23 if pressed else 30
    for x in range(15,spring_end,3 if pressed else 4):
        c.line(x,17,x+2,20,IRON[4]); c.line(x+2,20,x+3,17,IRON[2])
    c.rect(spring_end,16,3,6,BRASS[2]); c.vline(spring_end,16,6,BRASS[4])
    c.line(spring_end+1,21,27,24,IRON[3])
    c.poly([(22,28),(23,21),(27,18),(31,22),(32,28)],IRON[1])
    c.line(23,27,24,21,IRON[4]); c.hline(23,28,10,IRON[0])
    end=(49,22) if pressed else (43,5)
    c.line(27,23,*end,BRASS[0]); c.line(27,22,end[0],end[1]-1,BRASS[2])
    c.line(27,21,end[0],end[1]-2,BRASS[4])
    c.ellipse(27,24,4,3,BRASS[0]); c.ellipse(26,23,3,2,BRASS[3])
    c.px(26,22,BRASS[5]); c.rect(26,23,2,2,IRON[1])
    bevel(c,end[0]-4,end[1]-3,9,5,WALNUT)
    for x,y in ((9,24),(35,24)): screw(c,x,y)
    paper(c,44,25,13,6)
    c.hline(5,37,54,WALNUT[1])
    for x in (5,55):
        c.rect(x,40,4,8,WALNUT[3]); c.hline(x,42,4,WALNUT[5]); c.hline(x,46,4,WALNUT[1])
    c.hline(8,30,13,WALNUT[3]); c.hline(25,32,12,WALNUT[3])
    return _finish(c)


def basin():
    c=Canvas(96,64)
    # Counter thickness, bowl wall and water surface use separate silhouettes.
    slab(c,3,24,90,14,4,TILE,grain=False)
    c.rect(4,42,88,17,WALNUT[2]); c.vline(4,42,17,WALNUT[4])
    c.ellipse(44,30,32,9,STONE[1]); c.ellipse(44,29,31,8,TILE[3])
    c.ellipse(44,29,28,7,IRON[0]); c.ellipse(44,30,26,6,IRON[2])
    c.ellipse(44,32,23,4,FOG[1]); c.hline(25,31,19,FOG[2])
    c.hline(26,32,12,FOG[3]); c.hline(49,34,12,FOG[2])
    c.ellipse(45,33,3,1.5,IRON[0]); c.hline(43,32,4,IRON[3])
    c.hline(23,23,29,TILE[5]); c.hline(18,25,10,TILE[4])
    c.hline(35,38,25,STONE[1]); c.vline(92,28,13,TILE[2])
    for x in (8,49):
        drawer(c,x,43,37,14,WALNUT)
        c.vline(x+2,45,9,WALNUT[4])
    c.rect(3,59,90,5,WALNUT[1]); c.hline(4,59,88,WALNUT[3])
    # Fold over the right counter edge; the detachable clip remains its own state asset.
    c.rect(80,27,9,16,LINEN[3]); c.hline(80,27,9,LINEN[5]); c.vline(81,28,13,LINEN[4]); c.vline(88,29,14,LINEN[1])
    c.hline(14,50,11,WALNUT[3]); c.hline(62,53,14,WALNUT[3])
    return _finish(c)


def inlet():
    c=Canvas(64,48)
    for x,r in [(13,BRASS),(43,IRON)]:
        # Separate wall flanges and elbows make both branches readable.
        bevel(c,x-5,3,15,9,IRON)
        c.rect(x-2,6,8,8,r[1]); c.vline(x-2,6,8,r[4])
        c.rect(x,11,5,18,r[2]); c.vline(x,11,18,r[4])
        c.rect(x-2,13,9,3,r[1]); c.hline(x-2,13,9,r[4])
        c.rect(x-2,28,9,4,r[1]); c.hline(x-2,28,9,r[4])
        c.rect(x,32,5,5,r[2]); c.hline(x,36,5,r[0])
        screw(c,x+6,5)
    # Both pipe mouths terminate above the bowl. The transparent gap is real art.
    bevel(c,3,17,26,12,BRASS); c.rect(6,20,20,5,IRON[0])
    c.hline(6,25,20,BRASS[1]); c.px(9,22,STONE[3]); c.px(15,21,STONE[2]); c.px(21,23,STONE[3])
    c.hline(44,38,3,FOG[3]); c.px(45,41,FOG[2]); c.px(45,45,FOG[3])
    return c

def capture():
    c=Canvas(24,16); bevel(c,1,2,22,12,BRASS); c.rect(4,5,15,6,IRON[0])
    c.hline(4,11,15,IRON[3]); c.hline(3,3,17,BRASS[4])
    for x,y in [(5,5),(7,8),(11,6),(15,9),(17,5)]: c.px(x,y,STONE[3])
    c.rect(19,6,3,4,BRASS[3]); c.px(20,6,BRASS[5])
    c.rect(3,13,17,2,BRASS[0]); c.hline(3,13,17,BRASS[2]); return c

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
        # Independent fixed throats: a rear void above a sloping one-way tongue.
        bevel(c,x-1,10,15,9,IRON); c.rect(x+1,12,11,3,IRON[0])
        c.hline(x+1,15,11,ramp[2]); c.hline(x+2,16,9,ramp[4])
        c.hline(x+2,17,9,ramp[1]); c.px(x+2,13,IRON[3])
    c.vline(4,23,29,WALNUT[4]); c.vline(43,23,29,WALNUT[1])
    bevel(c,7,25,34,16,IRON)
    c.rect(10,28,28,8,IRON[0]); c.hline(11,35,26,BRASS[2])
    c.rect(13,30,22,3,LINEN[3]); c.hline(13,30,22,LINEN[5])
    # The front drawer and its rail shadow are independent of the receiving throat.
    c.rect(6,43,36,11,WALNUT[0]); bevel(c,7,45,34,8,WALNUT)
    c.rect(20,47,8,3,BRASS[0]); c.hline(21,47,6,BRASS[4])
    c.hline(9,51,12,WALNUT[3]); c.hline(4,54,40,WALNUT[1])
    for x in (5,39):
        leg(c,x,55,9,WALNUT); c.vline(x+2,55,9,WALNUT[3])
    return _finish(c)


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
    manifest={name:{"size":[canvas.w,canvas.h],"source":str((source/("red_"+name+".png")).relative_to(game)).replace('\\','/'),"runtime":str((runtime/("red_"+name+".png")).relative_to(game)).replace('\\','/'),"process":"native 1x pixels; shared life-wing material ramps; bottom-centre anchor"} for name,canvas in assets.items()}
    metadata={"fixtures":FIXTURES,"sites":SITES,"replaced":REPLACED,"cell":32}
    (runtime/"red_mechanisms_manifest.json").write_text(json.dumps({"assets":manifest,**metadata},ensure_ascii=False,indent=2),encoding="utf-8")
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
