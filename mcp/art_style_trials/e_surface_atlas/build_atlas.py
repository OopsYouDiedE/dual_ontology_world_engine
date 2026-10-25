"""Build measured, non-decorative atlas guides for ArtStyleTrials/E.

Projection is the exact orthographic camera basis from PublicHallE.show_room,
using the 1600x1600 Forward+ capture viewport. No model or texture is changed.
"""
from __future__ import annotations

import json
import hashlib
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "artifacts/style-e/surface-atlas"
SOURCE = ROOT / "godot_csharp/scenes/ArtStyleTrials/E"
VIEW = 1600
ATLAS = 1254
GAP = 20


def camera(size: float):
    p = (0.0, 22.0, 25.0)
    target = (0.0, 0.8, -0.65)
    f = tuple(target[i] - p[i] for i in range(3))
    n = math.sqrt(sum(x*x for x in f))
    f = tuple(x/n for x in f)
    # Godot Camera3D local +X is forward cross world-up; local +Y is right cross forward.
    r = (-f[2], 0.0, f[0])
    n = math.sqrt(sum(x*x for x in r))
    r = tuple(x/n for x in r)
    u = (r[1]*f[2] - r[2]*f[1], r[2]*f[0] - r[0]*f[2], r[0]*f[1] - r[1]*f[0])
    return p, r, u, size


def project(point, cam):
    p, r, u, size = cam
    d = tuple(point[i]-p[i] for i in range(3))
    return [VIEW/2 + sum(d[i]*r[i] for i in range(3))*VIEW/size,
            VIEW/2 - sum(d[i]*u[i] for i in range(3))*VIEW/size]


def plane_corners(center, width, height, plane, cam):
    x, y, z = center
    if plane == "xz":
        pts = [(x-width/2,y,z-height/2),(x+width/2,y,z-height/2),
               (x+width/2,y,z+height/2),(x-width/2,y,z+height/2)]
    elif plane == "xy":
        pts = [(x-width/2,y-height/2,z),(x+width/2,y-height/2,z),
               (x+width/2,y+height/2,z),(x-width/2,y+height/2,z)]
    else:
        raise ValueError(plane)
    return [project(pt,cam) for pt in pts]


def bbox(corners):
    xs = [p[0] for p in corners]
    ys = [p[1] for p in corners]
    left, top = min(xs), min(ys)
    right, bottom = max(xs), max(ys)
    return {"left": left, "top": top, "right": right, "bottom": bottom,
            "width_px_exact": right-left, "height_px_exact": bottom-top,
            "width_px": math.ceil(right-left), "height_px": math.ceil(bottom-top)}


def line_ref(file, pattern):
    for i, line in enumerate((SOURCE/file).read_text(encoding="utf-8").splitlines(), 1):
        if pattern in line:
            return f"godot_csharp/scenes/ArtStyleTrials/E/{file}:{i}"
    return None


def surface(key, role, center, w, h, plane, cam, world, source, uv, color,
            note=""):
    corners = plane_corners(center,w,h,plane,cam)
    return {"id":key,"role":role,"center_world":list(center),
            "world_size_units":list(world),"plane":plane,"projection_corners_1600":corners,
            "projection_bbox_1600":bbox(corners),"camera":cam[-1],
            "camera_position_relative_room":[0,22,25],
            "camera_target_relative_room":[0,0.8,-0.65],"source":source,
            "uv_direction":uv,"guide_color_rgba":color,"note":note}


hall = camera(18.2)
wing = camera(12.8)
surfaces = [
    surface("hall-main-rug","大厅阅读区地毯",(-1.8,0.025,-3.4),3.8,2.53,"xz",hall,[3.8,2.53],
            line_ref("PublicHallE.gd","_rug(Vector3(-1.8"),"PlaneMesh local U maps to world -Z after PI/2 Y rotation; V maps to world +X.",["#315345"],
            "Actual mat is the authored salon-rug raster; screen bbox is perspective foreshortened, recorded separately from world aspect."),
    surface("hall-gathering-rug","大厅会面区地毯",(0,0.026,3.2),2.8,4.2,"xz",hall,[2.8,4.2],
            line_ref("PublicHallE.gd","_rug(Vector3(0,0.026,3.2)"),"PlaneMesh local U=world +X; V=world +Z.",["#315345"],
            "Distinct actual footprint and texture presentation from hall-main-rug."),
    surface("hall-table-runner","公共长桌桌旗",(0,0.952,2.25),3.4,0.473,"xz",hall,[3.4,0.473],
            line_ref("PublicHallE.gd","plane.size=Vector2(size.y*0.43,size.x-0.30)"),"PlaneMesh local U maps world -Z; V maps world +X; material crops central rug strip with uv1 scale/offset.",["#496b59"],
            "World dimensions derive from _table(Vector2(3.7,1.1)); exact runner plane is 3.40 x 0.473m."),
    surface("hall-wallpaper-repeat","大厅墙纸一个完整重复周期",(0,1.8,-7),1/0.65,1/0.65,"xy",hall,[1/0.65,1/0.65],
            line_ref("PublicHallE.gd","wallpaper.albedo_texture"),"StandardMaterial3D world triplanar; no authored planar UV direction (world X/Y projection).",["#c1ae8d"],
            "Complete source-image repetition derives from uv1_scale=0.65: 1/0.65=1.5384615 scene units per repeat. The 1m GridMap wall tile is a different measure."),
    surface("wing-bedroom-rug","四翼寝室地毯",[0.55,0.025,1],1.65,2.475,"xz",wing,[1.65,2.475],
            line_ref("PublicHallE.gd","_rug(Vector3(0.55,0.025,1)"),"PlaneMesh local U=world +X; V=world +Z.",["#315345"],
            "Relative to each 6x8 wing room origin; distinct smaller actual footprint. Identical specimen is instantiated in jia/ding/bing/yi."),
    surface("wing-bedroom-runner","寝室桌旗",[1.6,0.902,-1.7],1.5,0.2795,"xz",wing,[1.5,0.2795],
            line_ref("PublicHallE.gd","plane.size=Vector2(size.y*0.43,size.x-0.30)"),"PlaneMesh local U maps world -Z; V maps world +X; central strip crop via material uv1 scale/offset.",["#496b59"],
            "Relative to each wing origin; derives from _table(Vector2(1.8,0.65)); runner is 1.50 x 0.2795m."),
    surface("wing-wallpaper-repeat","四翼寝室/盥洗墙纸一个完整重复周期",[-2,1.8,-4],1/0.65,1/0.65,"xy",wing,[1/0.65,1/0.65],
            line_ref("PublicHallE.gd","wallpaper.albedo_texture"),"World triplanar; no planar UV direction (world X/Y projection).",["#c1ae8d"],
            "Relative to wing room origin. The bathroom uses the same non-hall camera size=12.8 and the same material repeat; this entry is shared. 1m GridMap wall tile is not the texture period."),
]

# Hand-spread across the preferred square so the image model sees independent
# regions. Coordinates are exact crop rectangles; empty separation is >=20px.
placement={
    "hall-main-rug":(20,20), "hall-gathering-rug":(430,20), "wing-bedroom-rug":(780,20),
    "hall-table-runner":(20,330), "wing-bedroom-runner":(390,330), "hall-wallpaper-repeat":(700,330),
    "wing-wallpaper-repeat":(20,670),
}
for s in surfaces:
    b=s["projection_bbox_1600"]
    w,h=b["width_px"],b["height_px"]
    x,y=placement[s["id"]]
    s["atlas_rect"]={"x":x,"y":y,"width":w,"height":h}
canvas_h=ATLAS

inventory = [
    {"role":"椅背软包","geometry":"ellipsoid, center (0,1.02,-0.22), size (0.84,0.97,0.23), one per reading chair","status":"程序材质候选，未铺平；曲面无可直接对应的平面UV","screen_bbox_estimate_px":{"hall":[74,67],"method":"projected ellipsoid axis-aligned world bounding box, not surface unwrap"},"source":line_ref("PublicHallE.gd","ellipsoid(root,Vector3(0,1.02,-0.22)"),"reason":"not an actual planar surface; excluded from atlas"},
    {"role":"椅垫软包","geometry":"ellipsoid, center (0,0.58,0), size (0.90,0.22,0.82)","status":"程序材质候选，椭球曲面","screen_bbox_estimate_px":{"hall":[79,49],"method":"projected ellipsoid screen-axis envelope with reading chair yaw; bounding extent is not a surface unwrap"},"source":line_ref("PublicHallE.gd","ellipsoid(root,Vector3(0,0.58,0)"),"reason":"not an actual planar surface; excluded from flat artwork atlas"},
    {"role":"椅背簇绒扣","geometry":"12 small ellipsoids, 4 rows x 3 columns","status":"独立程序几何/缝线材质","source":line_ref("PublicHallE.gd","for row in range(4)"),"reason":"small repeated modeled accents, not a continuous paintable surface"},
    {"role":"地毯流苏","geometry":"thin box primitives at rug perimeter, generated every 0.055 scene units","status":"程序材质和重复几何","source":line_ref("PublicHallE.gd","for i in int(size.x/0.055)"),"reason":"not part of the raster carpet plane; existing geometry retained"},
    {"role":"床罩顶面","geometry":"flat box top, size 1.29 x 1.45 scene units, wing-relative center (-1.65,0.8225,0.05)","status":"平面绘制候选，当前程序材质；暂不在本轮七块atlas范围","screen_bbox_estimate_px":{"wing":[162,140],"method":"same orthographic projection math, ceil axis-aligned flat plane footprint"},"source":line_ref("PublicHallE.gd","Vector3(1.29,0.045,1.45)"),"reason":"can be separately added if authoring target includes bed linen"},
    {"role":"床垫、枕头","geometry":"ellipsoids","status":"曲面程序材质","source":line_ref("PublicHallE.gd","Vector3(1.30,0.30,2.18)"),"reason":"not a planar texture surface"},
    {"role":"卧室窗帘","geometry":"shared _window cylinders and valances","status":"same curved assembly as hall curtain, seen with wing camera","source":line_ref("PublicHallE.gd","func _window(at: Vector3)"),"reason":"curved assembly; no separate planar UV or different asset declared"},
    {"role":"厅窗帘单片褶皱组件","geometry":"7 vertical cylinders spanning center x=0.73..1.018 plus radius 0.055 and z fold offset ±0.035, height 2.55; tieback/tassels separately","status":"圆柱组合，未展UV；只登记屏幕外接框，不作为贴图尺寸","screen_bbox_estimate_px":{"hall":[35,183],"method":"projected union of each cylinder axis-aligned world bounds using actual seven fold centers, radius 0.055 and height 2.55; bbox is not a flat unwrap"},"source":line_ref("PublicHallE.gd","for fold in range(7)"),"reason":"requires authored cylindrical/fold UV or procedural material"},
    {"role":"墙面石膏/护墙/压条","geometry":"1m GridMap wall tile, combined ArrayMesh and trim","status":"plaster currently world-triplanar painted wallpaper; wood and moulding are procedural material","source":"godot_csharp/scenes/ArtStyleTrials/E/RoomGrid.gd:3-42","reason":"wallpaper repeat is included; panel wood, trim geometry/materials are not raster-painted surfaces"},
    {"role":"拼木地板与镶边","geometry":"1m GridMap floor plus world-space shader and perimeter strips","status":"procedural parquet shader and inlay boxes","source":"godot_csharp/scenes/ArtStyleTrials/E/RoomGrid.gd:3-10; godot_csharp/scenes/ArtStyleTrials/E/PublicHallE.gd:66-78","reason":"explicitly procedural material; not redrawn in this atlas"},
    {"role":"金属、木作、石材、瓷器、玻璃、镜面","geometry":"chairs/table/bookshelf/fireplace/window/washbasin and prop primitives","status":"solid-color/program shader materials or geometric surfaces","source":"godot_csharp/scenes/ArtStyleTrials/E/PublicHallE.gd:123-457","reason":"no authored raster texture surface in current implementation; intentionally excluded from this raster painting sheet"},
    {"role":"浴缸水面","geometry":"scaled cylinder with water shader","status":"procedural animated shader","source":line_ref("PublicHallE.gd","water_shader.shader=preload"),"reason":"dynamic shader surface, not this painted atlas"},
]

# Build pure placement guide and separately numbered/labelled review copy.
bg=(0,0,0,0)
pure=Image.new("RGBA",(ATLAS,canvas_h),bg)
annotated=Image.new("RGBA",(ATLAS,canvas_h),bg)
dp=ImageDraw.Draw(pure)
da=ImageDraw.Draw(annotated)
font=ImageFont.load_default()
for i,s in enumerate(surfaces,1):
    r=s["atlas_rect"]
    color=tuple(s["guide_color_rgba"][0] and (int(s["guide_color_rgba"][0][1:3],16),int(s["guide_color_rgba"][0][3:5],16),int(s["guide_color_rgba"][0][5:7],16),255))
    box=(r["x"],r["y"],r["x"]+r["width"]-1,r["y"]+r["height"]-1)
    dp.rectangle(box,fill=color)
    da.rectangle(box,fill=color,outline=(245,235,210,255),width=1)
    label=f"{i:02d}"
    da.text((r["x"]+2,r["y"]+2),label,fill=(255,255,255,255),font=font,stroke_width=1,stroke_fill=(0,0,0,200))
pure.save(OUT/"surface-atlas-guide.png")
annotated.save(OUT/"surface-atlas-guide-numbered.png")

source_hashes={}
for rel in ["PublicHallE.gd","HallCraft.gd","RoomGrid.gd","scene.tscn","SalonCamera.tres"]:
    data=(SOURCE/rel).read_bytes()
    source_hashes[f"godot_csharp/scenes/ArtStyleTrials/E/{rel}"]=hashlib.sha256(data).hexdigest()
manifest={"title":"ArtStyleTrials/E measured surface atlas","generated_by":"mcp/art_style_trials/e_surface_atlas/build_atlas.py","source_sha256":source_hashes,"projection":{"source":"manual dimensions/transforms extracted from current E source and exact orthographic projection math; not runtime/Godot sampling","capture_viewport":[1600,1600],"camera_position_relative_room":[0,22,25],"camera_target_relative_room":[0,0.8,-0.65],"hall_orthographic_size":18.2,"wing_bath_orthographic_size":12.8,"rounding":"each axis-aligned projected footprint bbox is ceiled independently to integer pixels; exact float bbox and all four projected corners are retained; no source model is scaled","view_axis":"Godot viewport top-left origin; projected corners use pixel coordinates","unit":"scene units; no claim that these are metres","assumptions":"square 1600x1600 capture viewport; orthographic camera size is vertical span; room-relative transforms from current source; no UI crop; flat planes projected without occlusion/clipping correction","purpose":"screen-density guide for the fixed authored camera. This is not a physically proportioned top-down texture atlas. Map image left/right and back/front according to the listed projected corners and UV direction; rendered planes are foreshortened by the camera."},"atlas":{"width":ATLAS,"height":canvas_h,"rgba":True,"gap_min_px":GAP,"png_pure":"surface-atlas-guide.png","png_numbered":"surface-atlas-guide-numbered.png","guide_only":True,"scaling":"none","tile_scope":"3 rug planes, 2 runner planes, hall wallpaper repeat and shared wing/bath wallpaper repeat; curved curtain and upholstery are inventory only"},"surfaces":surfaces,"non_planar_or_procedural_inventory":inventory}
(OUT/"manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(json.dumps({"atlas_size":[ATLAS,canvas_h],"surfaces":[{"id":s["id"],**s["projection_bbox_1600"],"rect":s["atlas_rect"]} for s in surfaces]},ensure_ascii=False,indent=2))
