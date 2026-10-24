"""Reusable model-sheet captions and geometry-derived area summary; run after Blender build."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,csv,math,numpy as np
H=Path(__file__).resolve().parent
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',25)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',19)
titles={
'01_island.png':'真实高程地形 / 水平×0.5，高程×1 / 建筑不随地形缩小',
'02_campus_birdseye.png':'学院、科研、后勤、架空生活区与约110m修士组团',
'03_main_exterior.png':'主楼火山口侧正立面 / 层间5.8、5.6、5.6m / 局部4F',
'04_main_2F_cutaway.png':'2F结构草图 / 四个独立四人寝区＋共用讲堂与起居厅',
'05_monastery.png':'山腰修士建筑群 / 多台地、多栋与两处院落 / 人数未设定',
'06_village_staff.png':'近海单层架空生活组团 / 杂役64席、村落100人设计情景',
'07_original_terrain.png':'同机位原始DEM地形 / 不含建筑台基挖填',
'08_main_ground_cutaway.png':'1F结构草图 / 北侧礼仪门厅、食堂、厨房、粮仓与公共生活',
'09_main_3F_cutaway.png':'3F结构草图 / 迎宾藏书＋八间客房十六席设计容量',
'10_basement_diagram.png':'B1/B2局部结构示意 / 水窖、勤务与四锚占位 / 隧道未深化'}
for name,title in titles.items():
    p=H/name
    if not p.exists():continue
    im=Image.open(p).convert('RGB');d=ImageDraw.Draw(im)
    d.rectangle((0,0,im.width,66),fill=(246,244,235));d.text((23,12),title,font=font,fill=(32,40,38))
    d.rectangle((0,im.height-40,im.width,im.height),fill=(246,244,235));d.text((23,im.height-33),'Astra 独立三维设计草图 · 非测绘/施工验算 · 地形源：Mapzen Terrarium',font=small,fill=(65,65,59))
    if name=='02_campus_birdseye.png':
        fw=np.array([15-430,60+510,55-450],float);fw/=np.linalg.norm(fw);right=np.cross(fw,[0,0,1]);right/=np.linalg.norm(right);up=np.cross(right,fw)
        north=np.array([math.sin(math.radians(5.73)),math.cos(math.radians(5.73)),0]);v=np.array([north@right,-north@up]);v=v/np.linalg.norm(v)*80
        a=np.array([1320,220]);b=a+v
        d.rounded_rectangle((1135,82,1485,255),10,fill=(246,244,235));d.line([tuple(a),tuple(b)],fill=(80,49,33),width=5)
        u=v/np.linalg.norm(v);perp=np.array([-u[1],u[0]])
        d.polygon([tuple(b),tuple(b-u*18+perp*8),tuple(b-u*18-perp*8)],fill=(80,49,33))
        d.text((1150,95),'主轴→火山口方向',font=small,fill=(32,40,38));d.text((1150,125),'真北顺时针约5.73°',font=small,fill=(32,40,38))
    im.save(p)
m=json.loads((H/'model_manifest.json').read_text(encoding='utf-8'))
with (H/'building_schedule.csv').open('w',encoding='utf-8-sig',newline='') as f:
    wr=csv.writer(f);wr.writerow(['id','type','x_m','y_m','lon','lat','floor_z_m','floors','footprint_m2','gross_floor_m2'])
    for r in m['buildings']:wr.writerow([r['id'],r['type'],r.get('x_m'),r.get('y_m'),*r.get('source_lon_lat',[None,None]),r.get('finished_floor_m'),r.get('floors'),r.get('footprint_m2'),r['gross_floor_area_m2']])
print(m['area_by_type_m2'])
