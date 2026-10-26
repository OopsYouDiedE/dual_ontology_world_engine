"""Publish a browser review of actual E renderer captures, without altering artwork."""
import html
import json
import argparse
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/style-e'

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--zone-yi',action='store_true')
    parser.add_argument('--residences',action='store_true')
    args=parser.parse_args()
    if args.residences:
        residence_report()
        return
    if args.zone_yi:
        yi_report()
        return
    pictures=sorted((OUT/'library-render').glob('*.png'))
    items=[{'id':p.stem.replace('--',':'),'src':'library-render/'+p.name} for p in pictures]
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>E · 区域与人物实机美术库</title>
<style>body{margin:0;background:#18211f;color:#dfd8c3;font:16px/1.65 system-ui}header,main{max-width:1400px;margin:auto;padding:24px}h1{font-weight:500}select,button{padding:9px;background:#34493e;color:#fff;border:1px solid #9d8b62}nav{display:flex;gap:12px;align-items:center;position:sticky;top:0;background:#18211f;padding:12px;z-index:2}.compare{display:grid;grid-template-columns:1fr 1fr;gap:18px}img{width:100%;background:#101817}.actual{overflow:auto}.actual.native img{width:1600px;max-width:none}.note{border-left:3px solid #b69960;padding:12px 20px;background:#26332e}a{color:#d2bc81}figure{margin:0}figcaption{padding:8px}details{margin:20px 0}#grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:12px}#grid img{cursor:pointer}small{color:#b5bcad}</style>
<header><h1>E · 区域与人物实机美术库</h1><p>16人共用身体模型；64个室内空间、第二章六区室外与六幕录制布景。相机与光照来自Godot Forward+实际渲染。</p>
<p class="note">这是独立3D美术库初版。正式游戏Host仍使用现役2D呈现，录制动作与碰撞尚未迁移；室外保留道路地面和部分复杂建筑原图。画面可用来审阅，不代表全部游戏效果已接入。</p>
<p>显示规则：当前房间正常；同地图已探索房间保留最后所见静态家具、压暗覆雾并隐藏人物；未探索隐藏。门暂不设计，后续仅显示当前房间所属模型。<a href="../../docs/style-e-library.md">实现范围与接口</a></p></header>
<main><nav><select id="pick"></select><button id="prev">上一项</button><button id="next">下一项</button><button id="native">原生1600像素</button><span id="name"></span></nav>
<div class="compare"><figure><img src="target-A.png"><figcaption>风格目标A：布局层次、材料、细致纹样；不是所有房间共享这一用途或摆放。</figcaption></figure><figure><div class="actual" id="actual"><img id="shot"></div><figcaption>当前实机 · 1600×1600 · 正交相机。尺寸按资产自动取景；当前未包含正式Host UI及行走。</figcaption></figure></div>
<details open><summary>仍存差异与验证边界</summary><p>模型仍有明显程序几何感，与A手绘目标的细腻轮廓、纹样和材质变化存在差距。已建立区域化陈设和共用人物模板；不是完整人物动画。六幕仅静态布景，不声称旧录制动作已可用。探索覆雾为三房间呈现夹具，正式本人认知接线未完成。室外混合原图与新三维建筑，原始CampusSite缺失部分已在区域说明登记。</p><p>纹理图集上轮仍未通过精确占位核对，本轮没有把错位草稿强行发布。PNG截图和二进制观察是本轮有限渲染证据；无Core/盲玩运行。</p></details>
<div id="grid"></div></main><script>const items=ITEMS;let i=0;const pick=document.querySelector('#pick');items.forEach((x,n)=>{pick.add(new Option(x.id,n));const f=document.createElement('figure');const im=document.createElement('img');im.src=x.src;im.loading='lazy';im.onclick=()=>show(n);const c=document.createElement('figcaption');c.textContent=x.id;f.append(im,c);document.querySelector('#grid').append(f)});function show(n){i=(n+items.length)%items.length;pick.value=i;document.querySelector('#shot').src=items[i].src;document.querySelector('#name').textContent=items[i].id}pick.onchange=()=>show(+pick.value);document.querySelector('#prev').onclick=()=>show(i-1);document.querySelector('#next').onclick=()=>show(i+1);document.querySelector('#native').onclick=()=>document.querySelector('#actual').classList.toggle('native');show(Math.max(0,items.findIndex(x=>x.id==='cast')));</script></html>'''
    page=page.replace('<main><nav>','<main><p><a href="zone-yi-redesign/index.html">查看最新：按乙区参考图重做的12个房间与整体布局</a>（下方全库截图保留为上一版）</p><nav>')
    (OUT/'index.html').write_text(page.replace('ITEMS',json.dumps(items,ensure_ascii=False)),encoding='utf-8')
    print(f'Report contains {len(items)} real renderer images')

def yi_report():
    folder=OUT/'zone-yi-redesign'
    items=[{'id':p.stem.replace('zone_yi--',''),'src':'render/'+p.name} for p in sorted((folder/'render').glob('*.png'))]
    names={'overview':'乙区总览','foyer':'开局门厅','hall':'公共厅','wc':'洗漱间','prep':'备餐间','store':'储物间','corridor':'走廊','north_2':'北排 north_2','north_1':'侦探房 north_1','north_0':'北排空房','south_2':'南排 south_2','south_1':'南排 south_1','south_0':'南排空房'}
    for item in items: item['name']=names.get(item['id'],item['id'])
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>乙区 · 参考图结构重设计</title>
<style>body{background:#171b1a;color:#ded7c5;font:16px/1.7 system-ui;margin:24px auto;max-width:1500px;padding:0 20px}a{color:#d8b36d}h1{font-weight:500}img{width:100%;display:block}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0}button,select{padding:10px;color:#eee;background:#353b36;border:1px solid #907b55}nav{position:sticky;top:0;background:#171b1a;padding:12px 0}#grid{display:grid;grid-template-columns:repeat(3,1fr);gap:15px}.note{background:#29332e;padding:18px;border-left:3px solid #aa8a52}.native{overflow:auto}.native img{min-width:1600px}figcaption{padding:8px}table{border-collapse:collapse}td,th{padding:6px 18px;border-bottom:1px solid #485047}</style>
<h1>第一章乙区 · 按提供的参考图重设计</h1>
<p>左侧洗漱／门厅／储物，中部公共厅／备餐，右侧两排寝室和走廊。独立3D美术设计预览，不启动Core。</p>
<p class="note">按用户追加要求，所有房间长宽增加15%，家具与墙高不缩放。房间尺寸为按实物尺度与参考构图推定的设计值，不是从概念图测得的历史建筑尺寸。新布局尚未替换正式Host的碰撞、站点和行动地图。总览是作者审阅视图，不代表玩家获得所有房间知识。</p>
<nav><select id="pick"></select> <button id="full">原图尺寸</button> <a href="../index.html">全库报告</a></nav>
<div class="pair"><figure><img src="target.png"><figcaption>用户提供的乙区目标图</figcaption></figure><figure id="actual"><img id="shot"><figcaption id="label"></figcaption></figure></div>
<h2>结构和范围</h2><p>每室单独拥有四面墙，移除朝外背面，保留室内框芯板、腰线和厚度截面；朝相机一面采用1.05m剖切，其余墙高3m。保留门洞，未制作门模型。床、桌椅、柜、箱桶使用分件结构；空书架仍为空，未补画证据痕迹。</p>
<table><tr><th>空间</th><th>宽×深，米</th></tr><tr><td>北排寝室／南排寝室</td><td>3.91×4.60／3.91×4.14</td></tr><tr><td>公共厅／备餐间</td><td>5.06×6.90／5.06×4.14</td></tr><tr><td>门厅／洗漱间／储物间</td><td>2.76×2.875／2.76×4.025／2.76×4.14</td></tr><tr><td>走廊</td><td>11.73×2.30</td></tr></table>
<p>镜头：22°透视，58°俯视；总览1600×1000，单室1600×1600。SSAO、SSIL、局部灯影开启。木地板、织毯和墙纸纹样使用米制程序材质；此前漂移的AI图集不用于这版。</p>
<h2>仍存差异</h2><p>这是三维结构重设计，仍未达到目标图的手绘层次和摆件密度。参考图中的附加花盆、装饰画和行李未全量复制；空房与证据保留原游戏身份。窗为有深度的内侧窗框与玻璃，尚未建墙外景。正式行走、碰撞、门的认知选择与整局体验尚未验收。</p>
<p>时代结构参考：<a href="https://collections.manchesterartgallery.org/collections/item/8c0ef582-b19c-331e-bdd1-3297ed5d714e/">Manchester Art Gallery，约1900年的车木腿与横撑椅</a>；<a href="https://collectionswa.net.au/items/fd4fdc42-8e7b-4e34-b8d5-ce3f55739d48">Busselton Historical Society，约1900年石台面洗漱柜</a>。用于构造参考，不宣称逐件复刻馆藏。</p>
<div id="grid"></div><script>const items=ITEMS;const p=document.querySelector('#pick');function show(i){p.value=i;document.querySelector('#shot').src=items[i].src;document.querySelector('#label').textContent=items[i].name+' · Godot Forward+实机'}items.forEach((x,i)=>{p.add(new Option(x.name,i));const f=document.createElement('figure');f.innerHTML='<img loading="lazy" src="'+x.src+'"><figcaption>'+x.name+'</figcaption>';f.onclick=()=>{show(i);window.scrollTo({top:0,behavior:'smooth'})};document.querySelector('#grid').append(f)});p.onchange=()=>show(+p.value);document.querySelector('#full').onclick=()=>document.querySelector('#actual').classList.toggle('native');show(Math.max(0,items.findIndex(x=>x.id==='overview')));</script></html>'''
    page=page.replace('<nav><select','<p><a href="../residences/index.html">查看最新：四翼与公共大厅、共享家具预制件</a></p><nav><select')
    (folder/'index.html').write_text(page.replace('ITEMS',json.dumps(items,ensure_ascii=False)),encoding='utf-8')
    print(f'Yi report: {len(items)} renders')

def residence_report():
    folder=OUT/'residences'
    pictures=sorted((folder/'render').glob('*.png'))
    names={'zone_yi':'乙区','zone_jia':'甲区','zone_bing':'丙区','zone_ding':'丁区','living_hall':'公共大厅'}
    items=[{'id':p.stem.replace('--',':'),'src':'render/'+p.name,'name':names.get(p.stem.split('--')[0],p.stem)+' · '+p.stem.split('--')[-1]} for p in pictures]
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>四翼与公共大厅 · 共享预制件</title>
<style>body{margin:0;background:#18201e;color:#e2dac8;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-weight:500}a{color:#d9bb7a}select,button{padding:10px;background:#314139;color:inherit;border:1px solid #9e895e}nav{position:sticky;top:0;background:#18201e;padding:12px;z-index:2}.pair{display:grid;grid-template-columns:1fr 1.6fr;gap:18px}img{width:100%;display:block}figure{margin:0}figcaption{padding:10px}.note{padding:16px;border-left:3px solid #ba995a;background:#29362f}#grid{display:grid;grid-template-columns:repeat(4,1fr);gap:14px}.native{overflow:auto}.native img{min-width:1600px}@media(max-width:800px){.pair{grid-template-columns:1fr}#grid{grid-template-columns:repeat(2,1fr)}}</style>
<main><h1>甲、乙、丙、丁四翼与公共大厅</h1><p>共享米制家具与独立室内墙体；Godot Forward+ 实际渲染。</p>
<p class="note">这是可运行的3D美术库。总览用于作者检查，不授予玩家探索知识；正式游戏的碰撞、互动点位与DV接线尚未迁移。画面仍保留程序化建模感，与参考图的手绘轮廓和细密纹样有差距。</p>
<nav><select id="pick"></select> <button id="prev">上一张</button> <button id="next">下一张</button> <button id="native">原生尺寸</button></nav>
<div class="pair"><figure><img id="target"><figcaption>目标参考：布局、比例、结构与材料层次；不据此新增剧情物品。</figcaption></figure><figure id="actual"><img id="shot"><figcaption id="caption"></figcaption></figure></div>
<h2>本版结构</h2><p>四翼沿乙区参考布局展开，房间长宽增加15%，床柜桌椅保持实物尺度。公共大厅采用抬高会客区、三级台阶、长桌区与独立洗漱间。各房间分别拥有完整朝内墙体和厚度截面，保留门洞，未制作门模型。</p>
<p>重复家具使用共享 PackedScene 预制件，床框、排骨条、桌腿横撑、柜门框芯板与箱桶为分件结构。22°透视、58°俯视；单室1600×1600、四翼总览1600×1000。世界环境启用SSAO/SSIL与局部光影。固定输出保留来源指纹、日志和二进制观察记录。</p>
<p>各房间保留原陈设身份，包括空寝室与空书架；不凭参考图补造证据。尚未验证正式行走、碰撞、预约讨论、DV覆雾与门的认知切换。<a href="../../../docs/style-e-library.md">实现说明</a></p>
<h2>全部当前画面</h2><div id="grid"></div></main>
<script>const items=ITEMS;let i=0;const pick=document.querySelector('#pick');function show(n){i=(n+items.length)%items.length;pick.value=i;const x=items[i];document.querySelector('#shot').src=x.src;document.querySelector('#target').src=x.id.startsWith('living_hall:')?'target-hall.png':'../zone-yi-redesign/target.png';document.querySelector('#caption').textContent=x.name+' · 当前渲染';}items.forEach((x,n)=>{pick.add(new Option(x.name,n));const f=document.createElement('figure');const im=document.createElement('img');im.loading='lazy';im.src=x.src;const c=document.createElement('figcaption');c.textContent=x.name;f.append(im,c);f.onclick=()=>{show(n);window.scrollTo({top:0,behavior:'smooth'})};document.querySelector('#grid').append(f)});pick.onchange=()=>show(+pick.value);document.querySelector('#prev').onclick=()=>show(i-1);document.querySelector('#next').onclick=()=>show(i+1);document.querySelector('#native').onclick=()=>document.querySelector('#actual').classList.toggle('native');show(Math.max(0,items.findIndex(x=>x.id==='living_hall:overview')));</script></html>'''
    prefabs=sorted((folder/'render/prefabs').glob('*.tscn'))
    links=''.join('<li><a href="render/prefabs/'+html.escape(p.name,quote=True)+'">'+html.escape(p.stem)+'</a></li>' for p in prefabs)
    page=page.replace('<h2>全部当前画面</h2>','<details><summary>导出的共享预制件（'+str(len(prefabs))+'个含材质变体）</summary><ul>'+links+'</ul></details><h2>全部当前画面</h2>')
    (folder/'index.html').write_text(page.replace('ITEMS',json.dumps(items,ensure_ascii=False)),encoding='utf-8')
    print(f'Residence report: {len(items)} renders; {len(prefabs)} prefabs')

if __name__=='__main__': main()
