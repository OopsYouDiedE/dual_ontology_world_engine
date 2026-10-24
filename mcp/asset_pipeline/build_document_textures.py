"""Draw document-led SVG materials, transparent motifs, borders and sample boards."""
from pathlib import Path
from html import escape
import argparse
import hashlib
import json
import math
import random
import re

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot_csharp/ShareAsset/Internal/Textures/DocumentSystems'
SRC=ROOT/'mcp/asset_pipeline/sources/Internal/Textures/DocumentSystems'
ASSETS=[]
TAU=math.tau

def elem(t,**kw):
    return '<'+t+' '+' '.join(k.replace('_','-')+'="'+escape(str(v),quote=True)+'"' for k,v in kw.items())+'/>'
def path(d,c,w=1,opacity=1,fill='none',**kw):
    return elem('path',d=d,stroke=c,stroke_width=w,opacity=opacity,fill=fill,stroke_linecap='round',stroke_linejoin='round',**kw)
def rect(x,y,w,h,c,**kw):return elem('rect',x=x,y=y,width=w,height=h,fill=c,**kw)
def circle(x,y,r,c,**kw):return elem('circle',cx=x,cy=y,r=r,fill=c,**kw)
def gradient(id,stops,vertical=False):
    return f'<linearGradient id="{id}" x1="0" y1="0" x2="{0 if vertical else 1}" y2="{1 if vertical else 0}">'+''.join(f'<stop offset="{o}" stop-color="{c}"/>' for o,c in stops)+'</linearGradient>'
def wrap(shape,x,y,margin=12):
    xs=[0]+([512] if x<margin else [])+([-512] if x>512-margin else [])
    ys=[0]+([512] if y<margin else [])+([-512] if y>512-margin else [])
    return ''.join(f'<g transform="translate({a} {b})">{shape}</g>' for a in xs for b in ys)
def grit(seed,n=1800,colors=('#f3e9d4','#282e34'),opacity=.14,rmax=1.1):
    rng=random.Random(seed);out=[]
    for _ in range(n):
        x,y=rng.random()*512,rng.random()*512;r=rng.uniform(.22,rmax)
        out.append(wrap(circle(round(x,2),round(y,2),round(r,2),rng.choice(colors),opacity=opacity),x,y,2*rmax))
    return ''.join(out)
def svg(body,defs='',size=512,title='',transparent=False):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 512 512"><title>{escape(title)}</title><defs>{defs}</defs>{body}</svg>'
def add(key,name,group,usage,body,defs='',scope='ordinary',kind='material',source='美术案·第一部分 / 地理案·第一部分'):
    file=f'{kind}s/{key}.svg';p=OUT/file;p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(svg(body,defs,title=name),encoding='utf-8')
    ASSETS.append(dict(id=key,name=name,group=group,usage=usage,scope=scope,kind=kind,file=file,source=source,body=body,defs=defs))

def stone_materials():
    # Exact periodic lattice: columns 64 apart, alternating 32 vertical offset.
    body=rect(0,0,512,512,'#343b42');rng=random.Random(10)
    for row in range(-1,9):
        for col in range(-1,9):
            x=col*64;y=row*64+(32 if col%2 else 0)
            pts=[(x-128/3,y),(x-64/3,y-32),(x+64/3,y-32),(x+128/3,y),(x+64/3,y+32),(x-64/3,y+32)]
            shade=['#444c52','#495258','#3e474d','#51595e'][(col%4+row%8*3)%4]
            d='M'+'L'.join(f'{a},{b}' for a,b in pts)+'Z'
            body+=path(d,'#272f36',3,fill=shade)
            body+=path(f'M{x-39},{y}l19,-28h40','#7e8587',1,.34)
            for k in range(6):body+=path(f'M{x-35+k*12},{y-18+(k%3)*9}l5,2','#939998',.6,.24)
    body+=grit(10,2200,('#b7b9b2','#263039'),.12)
    add('01-columnar-basalt','六棱玄武岩巨石','01-ancient','远古基座 / 海堤 / 地下 B2、B3',body)
    body=rect(0,0,512,512,'url(#g)')
    for i in range(11):
        x=i*64-80
        body+=path(f'M{x},512L{x+256},0','#71818c',rng.uniform(.4,1.7),.23)
        body+=path(f'M{x+5},512L{x+261},0','#c4d4d6',.7,.1)
    for yy in (166,406):
        d='M'+'L'.join(f'{x},{yy+9*math.sin(TAU*x/256)+4*math.sin(TAU*x/128):.2f}' for x in range(0,513,8))
        body+=path(d,'#91a2aa',1,.15)
    body+=grit(11,900,('#c3ced0','#27313b'),.075)
    add('02-polished-obsidian','黑曜石镜面','01-ancient','母祭坛 / 远古镜面石材',body,gradient('g',[('0','#282f39'),('.25','#3d4652'),('.5','#262e39'),('.75','#414a55'),('1','#282f39')]))
    body=rect(0,0,512,512,'#414b50')+grit(12,2300,('#b5b5a5','#252f36'),.12)
    for cx,cy in [(128,128),(384,384)]:
        points=[]
        for i in range(330):
            t=i/329*math.pi*7;r=4+t*4.6
            points.append((cx+r*math.cos(t),cy+r*math.sin(t)))
        d='M'+'L'.join(f'{x:.2f},{y:.2f}' for x,y in points)
        body+=path(d,'#202e36',5.4,.8)+path(d,'#a4a690',1.1,.42,transform='translate(1.5 1.5)')
    add('03-carved-spiral-stone','玄武岩螺旋水波刻纹','01-ancient','远古铭文石面 / 粗犷螺旋水波',body)
    body=rect(0,0,512,512,'#5e5d50')
    for row in range(4):
        for col in range(-1,5):
            x=col*128+(64 if row%2 else 0);y=row*128
            c=['#777263','#6a6759','#817a65','#6f6c60'][(row*3+col)%4]
            body+=rect(x+2,y+2,124,124,c,rx=3)
            body+=path(f'M{x+5},{y+122}V{y+5}H{x+121}','#aea088',1.4,.38)
            body+=path(f'M{x+3},{y+121}h120V{y+4}','#383e38',5,.28)
    body+=grit(13,4200,('#d2c7a0','#363e37','#a99a65'),.28,1.7)
    add('04-soot-darkened-tuff','硫磺熏黑凝灰岩','02-hermetic','16 世纪拱券 / 山体石堡',body)
    body=rect(0,0,512,512,'#c7c7ae')+grit(15,3200,('#f0ecda','#7d8c7e','#a4ad92'),.42,1.7)
    rng=random.Random(15)
    for _ in range(240):
        x,y=rng.random()*512,rng.random()*512;r=rng.uniform(1.1,5)
        form=elem('ellipse',cx=round(x,2),cy=round(y,2),rx=round(r,2),ry=round(r*.42,2),fill='#73867a',opacity='.38')
        body+=wrap(form,x,y,10)
    add('06-porous-zeolite','斜发沸石多孔矿面','02-hermetic','丹房过滤矿床 / 沸石水槽',body)
    body=rect(0,0,512,512,'#b5b8a3')+grit(16,1600,('#e4e0cb','#758578'),.24,1.8)
    body+=rect(96,0,320,512,'url(#g)')
    for x in (85,98,414,427):body+=path(f'M{x} 0V512','#647e73',2,.7)
    for i in range(-1,22):
        y=i*512/20
        body+=path(f'M112 {y}Q190 {y+7} 256 {y}T400 {y}','#d9ede1',1.3,.29)
    add('07-zeolite-watercourse','沸石淋洗水槽','02-hermetic','基地自流泉过滤 / B3 水幕槽',body,gradient('g',[('0','#879f91'),('.2','#a9c4b5'),('.5','#7da796'),('.8','#a9c4b5'),('1','#879f91')]))

def wood_material(key,name,base,grain,panel=False):
    defs=gradient('g',[('0',base[0]),('.2',base[1]),('.5',base[0]),('.8',base[2]),('1',base[0])])
    body=rect(0,0,512,512,'url(#g)')
    rng=random.Random(21 if not panel else 22)
    for i in range(240):
        x=rng.uniform(0,512);amp=rng.uniform(1,8);phase=rng.uniform(0,TAU)
        points=[(x+amp*math.sin(TAU*y/512+phase)+1.5*math.sin(TAU*3*y/512+phase),y) for y in range(0,513,8)]
        d='M'+'L'.join(f'{xx:.2f},{yy}' for xx,yy in points)
        body+=wrap(path(d,grain[i%2],rng.uniform(.25,1.15),rng.uniform(.16,.48)),x,256,12)
    for k in range(3):
        x=82+k*172;y=134+k*57
        for r in (5,9,14,20,28):
            body+=elem('ellipse',cx=x,cy=y,rx=r*.33,ry=r*2.4,fill='none',stroke=grain[0],stroke_width='.65',opacity='.35')
    if panel:
        for x,y,w,h in [(16,16,480,480),(29,29,454,454),(41,41,430,430)]:
            body+=rect(x,y,w,h,'none',stroke=grain[0],stroke_width=3)
            body+=path(f'M{x+2},{y+h-2}V{y+2}H{x+w-2}',grain[1],1,.5)
    else:
        for x in (0,128,256,384):
            body+=path(f'M{x} 0V512',grain[0],2,.7)
        for x,y in [(0,144),(128,352),(256,144),(384,352)]:body+=path(f'M{x} {y}h128',grain[0],1.5,.65)
    body+=grit(23,1000,grain,.11,.55)
    add(key,name,'03-academy','护墙 / 地板 / 书案 / 屏风木质底材',body,defs)

def finish_materials():
    wood_material('08-warm-walnut','深暖胡桃木拼板',('#503a2e','#6c4c35','#392c27'),('#2f2924','#b3865c'))
    wood_material('09-nanmu-panelling','楠木框芯护墙板',('#8b6c3c','#a68449','#725831'),('#4e462d','#c6aa6a'),True)
    body=rect(0,0,512,512,'url(#g)')+grit(25,2700,('#aa5b42','#592c26'),.11,.7)
    for y in range(0,513,6):body+=path(f'M0 {y}h512','#c08762',.35,.04)
    add('10-cinnabar-lacquer','朱砂漆面','03-academy','描金屏风底漆 / 皇室器物',body,gradient('g',[('0','#753025'),('.25','#8a3324'),('.5','#994631'),('.75','#8a3324'),('1','#753025')]))
    body=rect(0,0,512,512,'url(#g)')
    for i in range(256):
        y=i*2
        body+=path(f'M0 {y}h512','#9bab79',.35,.1)
    body+=grit(26,8500,('#a0b18c','#142a23'),.12,.55)
    add('11-emerald-velvet','墨绿丝绒绒面','03-academy','帷幕 / 软包；绒毛方向横向',body,gradient('g',[('0','#1e332a'),('.25','#30493a'),('.5','#1e332a'),('.75','#3a5140'),('1','#1e332a')]))
    body=rect(0,0,512,512,'#a1aca5')
    for row in range(4):
        for col in range(4):
            x,y=col*128,row*128
            body+=rect(x+2,y+2,124,124,'url(#g)',rx=2)
            body+=path(f'M{x+6},{y+121}V{y+6}H{x+120}','#ffffff',1.5,.6)
    body+=grit(27,1400,('#ffffff','#c6cebf'),.13,.6)
    add('12-acid-resistant-glaze','白塔防酸釉面砖','03-academy','诊疗 / 实验室 / 可清洗墙地面',body,gradient('g',[('0','#e7e8df'),('.3','#f0efeb'),('.7','#f6f5ee'),('1','#e7e8df')]))
    body=rect(0,0,512,512,'url(#g)')
    rng=random.Random(28)
    for _ in range(1300):
        x,y=rng.random()*512,rng.random()*512;l=rng.uniform(6,46)
        body+=wrap(path(f'M{x:.2f},{y:.2f}h{l:.2f}',rng.choice(['#e4d3a4','#796747']),rng.uniform(.18,.6),.16),x,y,50)
    add('13-polished-brass','抛光拉丝黄铜','03-academy','传声管 / 仪表 / 放大镜；常规金色',body,gradient('g',[('0','#907b50'),('.16','#b89758'),('.4','#dcc99b'),('.52','#a88b50'),('.73','#c3ad77'),('1','#907b50')]))

def metal_materials():
    body=rect(0,0,512,512,'#414d51')
    for x in (0,256):
        body+=path(f'M{x} 0V512','#28353c',5)
        body+=path(f'M{x+4} 0V512','#9da39a',1,.35)
    for y in (0,256):body+=path(f'M0 {y}h512','#28353c',5)
    for x in (17,239,273,495):
        for y in (17,239,273,495):
            body+=circle(x+1.8,y+2,5.4,'#29343a')+circle(x,y,4.5,'url(#rivet)')
    body+=grit(31,3800,('#b8b8a7','#233138','#9c5a3c'),.15,1.1)
    add('05-wrought-iron-rivets','熟铁铆钉板','02-hermetic','古修士铁门 / 后期加固件',body,gradient('rivet',[('0','#747f7f'),('.45','#a3a99b'),('1','#37464c')],True))
    body=rect(0,0,512,512,'#4a5559')+grit(32,8500,('#7d8a85','#263841','#9c5a3c'),.32,1.35)
    for i in range(16):body+=path(f'M{i*32},0v512','#89928a',.6,.08)
    add('14-cast-iron','铸铁滚轴与机床铸面','04-industry','B1 滚轴 / 铁工院设备表面',body)
    body=rect(0,0,512,512,'url(#g)')+grit(33,4800,('#c2c3b8','#697578'),.13,.7)
    for i in range(18):
        y=i*32
        body+=path(f'M0 {y}Q128 {y+12} 256 {y}T512 {y}','#c4c5b8',.8,.12)
    add('15-lead-sheet','铅板阻尼夹层','04-industry','B1 隔震夹层 / 低光泽冷灰',body,gradient('g',[('0','#7c8787'),('.25','#9ca6a1'),('.5','#89958f'),('.75','#a1aba4'),('1','#7c8787')]))
    body=rect(0,0,512,512,'#303940')+grit(34,11000,('#737a76','#23313a','#505950'),.38,1.6)
    add('16-asphalt-damping','沥青阻尼料','04-industry','铅板与玄武岩之间的阻尼层',body)
    body=rect(0,0,512,512,'#c5b48f')+grit(35,14000,('#efe1be','#867c60','#d7c7a6'),.68,1.1)
    add('17-dried-sea-sand','焙干海砂','04-industry','星仪院隔音夹墙填料',body)
    body=rect(0,0,512,512,'#57605c')
    for i in range(32):
        p=i*16
        body+=path(f'M{p} 0V512M0 {p}H512','#4b4d43',4)
        body+=path(f'M{p-.6} 0V512M0 {p-.6}H512','#bb9162',1.9)
        body+=path(f'M{p-1} 0V512M0 {p-1}H512','#d8b385',.5,.7)
    add('18-faraday-copper-mesh','法拉第铜网','04-industry','名录库地面屏蔽层 / 铜丝网',body)

def paper_materials():
    body=rect(0,0,512,512,'#eae0c4')
    for i in range(256):
        p=i*2
        body+=path(f'M0 {p}h512','#a59978',.35,.16)+path(f'M{p} 0v512','#fff8dc',.45,.3)
    body+=grit(41,6500,('#a39474','#fff7de'),.25,.6)
    add('19-linen-casebook-paper','亚麻案卷纸','05-casebook','案卷常规页面 / 亚麻纤维纹',body,source='美术案·第三、四部分')
    body=rect(0,0,512,512,'#1e332a');rng=random.Random(42)
    for _ in range(4200):
        x,y=rng.random()*512,rng.random()*512;r=rng.uniform(1,3)
        d=f'M{x:.2f},{y:.2f}q{r:.2f},{-r:.2f} {2*r:.2f},0'
        body+=wrap(path(d,'#7a8c65',.55,.2),x,y,8)
    body+=grit(42,4000,('#92a17a','#132923'),.12,.65)
    add('20-emerald-book-leather','墨绿案卷书皮','05-casebook','烫金书封基材；纹样另叠加',body,source='美术案·第四部分第一层')
    body=rect(0,0,512,512,'#e6dcbf')+grit(43,6000,('#a49472','#fff2d0'),.22,.7)
    for i in range(20):
        y=i*32;body+=path(f'M0 {y}Q128 {y+2} 256 {y}T512 {y}','#a18d65',.6,.11)
    add('21-memory-parchment','旧羊皮回忆底','06-restricted','仅脑内推演 / 闪回，不作普通装潢',body,scope='mind-only',source='美术案·第三部分第三禁区')
    body=rect(0,0,512,512,'#1e3246')
    for i in range(33):
        p=i*16;body+=path(f'M{p} 0V512M0 {p}H512','#e6dcbf',.6,.13 if i%4 else .3)
    for x in (64,320):
        for y in (64,320):body+=path(f'M{x-5} {y}h10M{x} {y-5}v10','#e6dcbf',1,.7)
    body+=grit(44,800,('#e6dcbf',),.09,.65)
    add('22-mind-blueprint','推演建筑蓝图','06-restricted','脑内推演的线框底；非世界实体材料',body,scope='mind-only',source='美术案·第三、四部分')

def restricted_and_terrain():
    body=rect(0,0,512,512,'#352b4f')
    for row in range(4):
        for col in range(4):
            x=col*128;y=row*128
            body+=rect(x+7,y+7,114,114,'#6b3fa0',stroke='#3d305c',stroke_width=3)
            for k in range(4):
                p=14+k*12
                body+=path(f'M{x+p},{y+128-p}V{y+p}H{x+128-p}V{y+128-p}H{x+p}','#c8b456',1.2,.65-k*.1)
                body+=path(f'M{x+p+3},{y+128-p-3}V{y+p+3}H{x+128-p-3}','#b39aca',2,.45)
    add('23-ether-bismuth-crystal','源质回字霜晶','06-restricted','源质 / 莫兰灵视 / 黄金迷梦专用',body,scope='ether-only',source='美术案·第二部分9 / 第三部分第二禁区')
    body=rect(0,0,512,512,'url(#g)')+grit(47,4500,('#262330','#0e0c10'),.1,.5)
    for y in range(0,512,3):body+=path(f'M0 {y}h512','#302c37',.35,.14)
    add('24-demon-black-satin','恶魔纯黑燕尾服缎面','06-restricted','仅代管者实体燕尾服；不用于人类或建筑',body,gradient('g',[('0','#0e0c10'),('.25','#19151e'),('.5','#0e0c10'),('.75','#211b27'),('1','#0e0c10')]),scope='demon-only',source='美术案·第三部分第一禁区')
    body=rect(0,0,512,512,'#2a6f68')
    for row in range(-1,25):
        y=row*512/24
        d='M'+'L'.join(f'{x},{y+6*math.sin(TAU*x/128+(row%6)*TAU/6):.2f}' for x in range(0,513,8))
        body+=path(d,'#a4c6aa',1,.28 if row%3 else .48)
    body+=grit(48,1800,('#bed8c0','#1f5a58'),.12,.8)
    add('25-emerald-sea','碧水翡翠青水面','07-landscape','海湾 / 自流泉水面；纹线非动画',body,source='美术案·第一部分旗帜色彩 / 地理案水系')
    body=rect(0,0,512,512,'#9b9884');rng=random.Random(49)
    for _ in range(1600):
        x,y=rng.uniform(0,512),rng.uniform(0,512);r=rng.uniform(1.5,7)
        pts=[(x+math.cos(i*TAU/5)*r*rng.uniform(.7,1),y+math.sin(i*TAU/5)*r*rng.uniform(.7,1)) for i in range(5)]
        d='M'+'L'.join(f'{a:.2f},{b:.2f}' for a,b in pts)+'Z'
        body+=wrap(path(d,'#6f776e',.6,.9,rng.choice(['#bab7a0','#8d9486','#c9c0a6','#7d8276'])),x,y,10)
    add('26-open-air-gravel','露天夹道碎石','07-landscape','南北寝区之间低洼露天夹道',body,source='地理案·主楼2F四方学舍')

MOTIFS=[
('academy','四锚十字环','#b89758','#1e332a','常规学园总徽'),
('imperial','三叠云水纹','#d4af37','#8a3324','皇室屏风 / 地毯滚边'),
('science','双环棱镜纹','#a3aeb8','#2b4c7e','格致仪表 / 铭牌'),
('syndicate','蒙眼鸦与八字蛇','#383b42','#6e2626','财团暗记 / 私约'),
('wenqi','方印与双羽笔','#2f3b57','#eae0c4','文契院 / 契约拓片'),
('xingyi','音叉与同心波','#4a6b5d','#eae0c4','星仪院 / 传声铜牌'),
('baita','三角与曲颈瓶','#52684f','#f0efeb','白塔 / 温室 / 丹房'),
('tiegong','八角螺母与铁锤','#4a5559','#d5b89c','铁工 / 连廊钢桁架'),
('alien','回字晶与竖瞳','#c8b456','#6b3fa0','超自然限定；冲突色采用冷金'),
]
def motif_shape(name,color):
    c=color
    if name=='academy':
        b=elem('circle',cx=256,cy=256,r=152,fill='none',stroke=c,stroke_width=12)
        anchor=path('M256 256V146M235 184h42M222 160q4-22 34-24 30 2 34 24M222 160l-2-15m70 15 2-15',c,9)
        for a in (0,90,180,270):b+=f'<g transform="rotate({a} 256 256)">{anchor}</g>'
        for x,y in [(256,75),(437,256),(256,437),(75,256)]:b+=path(f'M{x-6},{y}h12M{x},{y-6}v12',c,5)
        return b
    if name=='imperial':
        b=path('M256 100l19 30-19 30-19-30Z',c,5,fill=c)
        for y in (216,276,336):b+=path(f'M80 {y}q44-54 88 0t88 0t88 0t88 0',c,12)
        return b
    if name=='science':return elem('ellipse',cx=256,cy=256,rx=168,ry=69,fill='none',stroke=c,stroke_width=12,transform='rotate(38 256 256)')+elem('ellipse',cx=256,cy=256,rx=168,ry=69,fill='none',stroke=c,stroke_width=12,transform='rotate(-38 256 256)')+path('M138 144l91 116 36-54 108 139',c,13)
    if name=='syndicate':return path('M256 294C194 170 67 186 81 282S199 379 256 255C313 131 445 153 432 252S322 372 256 294Z',c,15)+path('M207 236l49-128 60 128-60-22Z',c,3,fill=c)+path('M216 174h82','#eae0c4',13)
    if name=='wenqi':return rect(110,110,292,292,'none',stroke=c,stroke_width=12)+path('M167 350 329 159l-39 13-94 115-9 40Zm178 0L183 159l39 13 94 115 9 40Z',c,7,fill=c)
    if name=='xingyi':return path('M204 137v97l52 58 52-58v-97M256 292v100',c,15)+path('M157 190q-56 65 0 130M119 165q-86 91 0 181M355 190q56 65 0 130M393 165q86 91 0 181',c,9)
    if name=='baita':return path('M256 89 432 395H80Z',c,12)+path('M292 175h34v17h-16C286 192 295 225 275 245v22C350 310 320 370 256 370S165 311 241 267v-28C265 220 247 175 292 175Z',c,9)+path('M210 322q46-17 92 0',c,7)
    if name=='tiegong':
        pts=[(256+164*math.cos((i+.5)*TAU/8),256+164*math.sin((i+.5)*TAU/8)) for i in range(8)]
        return path('M'+'L'.join(f'{x:.2f},{y:.2f}' for x,y in pts)+'Z',c,13)+path('M184 181h144v56H184ZM241 237h30v125h-30Z',c,6,fill=c)
    return path('M109 109h294v294H109Zm42 42v210h210V151Z',c,10)+path('M256 189l18 67-18 67-18-67Z',c,4,fill=c)

def motifs():
    for n,title,fg,bg,use in MOTIFS:
        scope='ether-only' if n=='alien' else 'ordinary'
        add('m-'+n,title,'08-motifs',use,motif_shape(n,fg),scope=scope,kind='motif',source='美术案·第二部分纹样表')
        # A genuinely repeatable square overlay tile, no background colour baked in.
        shape=motif_shape(n,fg)
        body=f'<g transform="translate(128 128) scale(.5)">{shape}</g>'
        add('r-'+n,title+'连续底纹','09-repeats',use+'；透明 512 平铺单元',body,scope=scope,kind='repeat',source='美术案·第二部分纹样表；重复节奏为作者补全')

GROUPS=[('01-ancient','远古海民 · 石与刻痕'),('02-hermetic','赫尔墨斯修士 · 石堡与过滤'),('03-academy','皇家学园 · 木、漆、绒、釉与铜'),('04-industry','近代工业 · 隔震与声学构造'),('05-casebook','书案与案卷 · 纸和书皮'),('06-restricted','限定视觉 · 回忆、源质与恶魔'),('07-landscape','室外地表 · 水与碎石'),('08-motifs','九组徽记 · 文档指定几何'),('09-repeats','连续纹样 · 透明平铺单元')]

def unique(fragment,prefix):
    return re.sub(r'url\(#([^)]*)\)',lambda m:f'url(#{prefix}{m[1]})',re.sub(r'id="([^"]+)"',lambda m:f'id="{prefix}{m[1]}"',fragment))

def boards():
    board_files=[]
    for group,title in GROUPS:
        entries=[a for a in ASSETS if a['group']==group]
        for offset in range(0,len(entries),6):
            page=entries[offset:offset+6];content=rect(0,0,1600,1150,'#f1eee5')
            content+=f'<text x="64" y="66" font-size="12" letter-spacing="4" fill="#7e6f58">安眠者有福 / 材质与纹样图谱</text><text x="64" y="122" font-size="33" fill="#263e38">{escape(title)}</text><text x="64" y="158" font-size="14" fill="#627064">单元均为 SVG；材质可平铺，徽记透明；制作补全与原始依据见随附说明。</text>'
            for j,a in enumerate(page):
                x=64+(j%3)*502;y=204+(j//3)*435
                bg=next((t[3] for t in MOTIFS if a['id'].endswith(t[0])),'#d1cec2')
                # isolate defs; each preview shows an actual repeat field, not a flattened placeholder
                defs=unique(a['defs'],a['id']+'-')
                body=unique(a['body'],a['id']+'-')
                p=a['id']+'-tile'
                size=256 if a['kind'] in ('material','repeat') else 512
                content+=f'<defs>{defs}<pattern id="{p}" x="{x}" y="{y}" width="{size}" height="{size}" patternUnits="userSpaceOnUse"><svg width="{size}" height="{size}" viewBox="0 0 512 512">{body}</svg></pattern></defs>'
                content+=rect(x,y,466,300,bg)
                if a['kind']=='motif':content+=f'<svg x="{x}" y="{y}" width="466" height="300" viewBox="0 0 512 512">{body}</svg>'
                else:content+=rect(x,y,466,300,f'url(#{p})')
                content+=f'<text x="{x}" y="{y+331}" font-size="20" fill="#263e38">{escape(a["name"])}</text><text x="{x}" y="{y+355}" font-size="12" fill="#5a695f">{escape(a["usage"])}</text><text x="{x}" y="{y+377}" font-size="11" fill="#7e6f58">{a["id"]} · {a["scope"]} · {a["kind"]}</text>'
            content+='<text x="64" y="1110" font-size="12" fill="#6c7569">原生矢量纹理 / 无外部图像依赖 / 无游戏逻辑修改 / 不代表真实物理材质参数</text>'
            fn=f'boards/{group}-{offset//6+1}.svg';p=OUT/fn;p.parent.mkdir(parents=True,exist_ok=True)
            p.write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1150" viewBox="0 0 1600 1150"><g font-family="Microsoft YaHei, Noto Sans SC, sans-serif">{content}</g></svg>',encoding='utf-8')
            board_files.append((fn,title))
    return board_files

def main():
    argparse.ArgumentParser(description=__doc__).parse_args()
    OUT.mkdir(parents=True,exist_ok=True)
    stone_materials();finish_materials();metal_materials();paper_materials();restricted_and_terrain();motifs()
    b=boards()
    sources={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((ROOT/'docs').glob('*.md'))}
    manifest={'schema':1,'tile_size':512,'construction':'SVG paths, periodic fields and wrapped edge primitives','seed_policy':'fixed per asset','source_sha256':sources,'assets':[{k:v for k,v in a.items() if k not in ('body','defs')} for a in ASSETS],'boards':[f for f,t in b]}
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    cards=''
    for group,title in GROUPS:
        cards+=f'<section><h2>{title}</h2><div class="grid">'
        for a in [a for a in ASSETS if a['group']==group]:
            bg=next((t[3] for t in MOTIFS if a['id'].endswith(t[0])),'#b7b9ac')
            cards+=f'<article><a href="{a["file"]}" target="_blank"><div class="sample" style="background-color:{bg};background-image:url({a["file"]})"></div></a><h3>{a["name"]}</h3><p>{a["usage"]}</p><small>{a["id"]} / {a["scope"]}</small></article>'
        cards+='</div></section>'
    html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>安眠者有福 · 纹理体系</title><style>body{margin:0;background:#eeece3;color:#263e38;font:15px "Microsoft YaHei",sans-serif}header,section{padding:32px 4vw}header{background:#1e332a;color:#eae0c4}header h1{font-size:36px}header p{line-height:1.9;max-width:1000px}a{color:inherit}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:24px}article{background:#f7f4eb;border:1px solid #d5cfbb;padding:12px}h3{font-size:17px;margin:18px 4px 8px}article p{min-height:38px;margin:8px 4px;color:#607063;font-size:13px}small{margin:4px;color:#798475}.sample{height:250px;background-size:256px 256px;background-repeat:repeat}nav{display:flex;gap:20px;flex-wrap:wrap}h2{font-size:24px;border-bottom:1px solid #b7bba6;padding-bottom:18px}@media(max-width:1000px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:600px){.grid{grid-template-columns:1fr}}</style><header><h1>安眠者有福 · 材质与纹样</h1><p>三代建筑材料、工业夹层、案卷纸皮、限定视觉与九组徽记。点击样本打开独立 SVG；卡片以实际单元重复显示。原始设计案没有改动，补全尺度与制作选择见设计说明。</p><nav><a href="DESIGN-NOTES.md">制作设计说明</a>'''+''.join(f'<a href="{f}" target="_blank">{t}</a>' for f,t in b)+f'</nav></header>{cards}</html>'
    (OUT/'index.html').write_text(html,encoding='utf-8')
    note=SRC/'DESIGN.md'
    if note.exists():(OUT/'DESIGN-NOTES.md').write_text(note.read_text(encoding='utf-8'),encoding='utf-8')
    print(f'Published {len(ASSETS)} assets and {len(b)} boards to {OUT}')

if __name__=='__main__':main()
