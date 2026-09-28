"""Rebuild the furnished visual sample via the same production operations exposed by MCP."""
import json
import random
from pathlib import Path
import workflow as w

def main():
    palette={'cap':'#263339','joint':'#202c32','chip':'#304047','floor':'#414951','speck':'#49535b','dark':'#394149',
        'wall':'#65716b','walllight':'#707970','walldark':'#5c6863','panel':'#56554b','paneldark':'#353e3d',
        'rail':'#9b947d','gold':'#a49b7e','rug':'#3f5859','ruglight':'#577170','rugdark':'#304648'}
    def rect(x,y,xx,yy,c): return dict(op='rect',xy=[x,y,xx,yy],color=c)
    def line(x,y,xx,yy,c): return dict(op='line',xy=[x,y,xx,yy],color=c)
    def point(x,y,c): return dict(op='point',xy=[x,y],color=c)
    def asset(name,width,height,commands):
        source=w.draw_asset(name,width,height,palette,commands,replace=True)['source']
        return w.adapt_asset(name,source,[0,0,width,height],[width,height],32,'native')['output']
    rng=random.Random(29)
    wall=[rect(0,0,31,31,'cap')]
    for _ in range(90): wall.append(point(rng.randrange(32),rng.randrange(32),'chip'))
    floor=[rect(0,0,31,31,'floor')]
    for y in range(0,32,4):
        for x in range(0,32,4):
            floor += [point((x+(2 if y%8 else 0))%32,y,'speck'),point((x+1)%32,(y+1)%32,'dark')]
    upper=[rect(0,0,31,31,'wall')]
    for y in range(32):
        for x in range(32):
            if rng.random()<.25: upper.append(point(x,y,'walllight' if rng.random()<.4 else 'walldark'))
    lower=upper[:]+[rect(0,10,31,31,'panel'),line(0,9,31,9,'paneldark'),line(0,10,31,10,'rail'),line(0,12,31,12,'paneldark'),line(0,30,31,30,'rail'),line(0,31,31,31,'paneldark')]
    for x in range(0,32,8): lower += [line(x,13,x,29,'paneldark'),line(x+1,13,x+1,29,'rail')]
    for name,commands in [('wall',wall),('floor',floor),('face',upper),('face_lower',lower)]: asset(name,32,32,commands)
    trim=['#101b24','#1c2a32','#52636b','#78847e','#34464d','#26353e']
    w.build_tiles(*(str(w.OUTPUT/f'{n}.png') for n in ('wall','floor','face')),trim, str(w.OUTPUT/'face_lower.png'))
    # Native pixel rug with a complete border and subdued repeated medallions.
    rug=[rect(0,0,223,143,'rugdark'),rect(2,2,221,141,'gold'),rect(4,4,219,139,'rugdark'),rect(8,8,215,135,'ruglight'),rect(10,10,213,133,'rug')]
    for x in range(14,211,12):
        rug += [rect(x,5,x+3,7,'gold'),rect(x,136,x+3,138,'gold')]
    for y in range(14,132,12):
        rug += [rect(5,y,7,y+3,'gold'),rect(216,y,218,y+3,'gold')]
    for y in range(24,125,24):
        for x in range(24,207,24):
            rug += [line(x-5,y,x,y-5,'ruglight'),line(x,y-5,x+5,y,'ruglight'),line(x+5,y,x,y+5,'ruglight'),line(x,y+5,x-5,y,'ruglight')]
    asset('rug',224,144,rug)
    # Rectangular window and curtains: the reference's architectural rhythm, not its pixels.
    palette.update({'glass':'#344e67','glasslight':'#648498','frame':'#b6aa87','curtain':'#9a9c8d','fold':'#6b7a78'})
    window=[rect(8,0,55,70,'paneldark'),rect(11,3,52,67,'glass')]
    for x in (11,31,51): window.append(rect(x,3,x+1,67,'frame'))
    for y in (3,24,45,66): window.append(rect(11,y,53,y+1,'frame'))
    window += [line(13,22,29,7,'glasslight'),line(34,42,49,28,'glasslight')]
    for x in (1,4,7,56,59,62):
        window.append(rect(x,0,min(x+2,63),50 if x<8 else 47,'curtain' if x%2 else 'fold'))
    window += [rect(6,48,10,51,'gold'),rect(54,45,58,48,'gold'),rect(7,70,56,72,'frame')]
    asset('window',64,76,window)
    rows=[
        '########################',
        '#WWWWWWWWWWWWWWWW#WWWWW#',
        '#wwwwwwwwwwwwwwww#wwwww#',
        '#................#.....#',
        '#................#.....#',
        '#................D.....#',
        '#................D.....#',
        '#................#.....#',
        '#................#######',
        '#......................#',
        '#......................#',
        '#####.............##...#',
        '    #.............##...#',
        '    #####DD#########...#',
        '        #..#       #...#',
        '        ####       #####',
    ]
    props=[]
    def prop(file,x,y,anchor=None,collision=None,ground=False):
        from PIL import Image
        p=Path(file)
        with Image.open(p) as im: size=im.size
        props.append(dict(texture=w.res(p),position=[x,y],anchor=anchor or [size[0]//2,size[1]],collision=collision or [],overhead=False,ground=ground))
    internal=w.GAME/'ShareAsset/Internal/Props'
    palette.update({'shadow':'#11192455','shadowsoft':'#11192428'})
    shadow=[]
    for y,row in enumerate(rows):
        for x,c in enumerate(row):
            if c not in '.D': continue
            if y and rows[y-1][x] in '#wW':
                shadow += [rect(x*32,y*32,x*32+31,y*32+3,'shadow'),rect(x*32,y*32+4,x*32+31,y*32+7,'shadowsoft')]
            if x and row[x-1]=='#': shadow.append(rect(x*32,y*32,x*32+3,y*32+31,'shadowsoft'))
    asset('wall_shadow',768,512,shadow)
    prop(w.OUTPUT/'wall_shadow.png',0,0,[0,0],ground=True)
    prop(w.OUTPUT/'rug.png',300,313,ground=True)
    for x in (120,284,448): prop(w.OUTPUT/'window.png',x,104)
    prop(internal/'story_parlor_bookcase.png',65,165,collision=[-22,-15,44,15])
    prop(internal/'story_parlor_bookcase.png',493,165,collision=[-22,-15,44,15])
    prop(internal/'story_fireplace.png',282,162,collision=[-37,-20,74,20])
    prop(internal/'story_sofa.png',289,220,collision=[-44,-22,88,22])
    prop(internal/'story_armchair.png',195,269,collision=[-21,-18,42,18])
    prop(internal/'story_armchair.png',389,269,collision=[-21,-18,42,18])
    prop(internal/'story_tea_table.png',291,278,collision=[-29,-14,58,14])
    prop(internal/'story_parlor_piano.png',96,289,collision=[-37,-22,74,22])
    for x,y in [(180,155),(410,158),(485,309),(664,150)]: prop(internal/'story_tree.png',x,y,collision=[-7,-8,14,8])
    prop(internal/'story_side_table.png',667,242,collision=[-24,-15,48,15])
    prop(internal/'story_bookcase.png',660,166,collision=[-29,-14,58,14])
    # Static scale references use the established atlas, not enlarged figures.
    actor_master=w.GAME/'ShareAsset/Internal/Characters/characters-21-front-master.png'
    w.adapt_asset('guest',str(actor_master),[0,48,32,48],[32,48],32,'native')
    prop(w.OUTPUT/'guest.png',364,310,[16,47])
    result=w.compose_map(rows,[9,10],props)
    print(json.dumps(result,ensure_ascii=False))

if __name__=='__main__': main()
