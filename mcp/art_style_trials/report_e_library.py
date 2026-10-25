"""Publish a browser review of actual E renderer captures, without altering artwork."""
import html
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/style-e'

def main():
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
    (OUT/'index.html').write_text(page.replace('ITEMS',json.dumps(items,ensure_ascii=False)),encoding='utf-8')
    print(f'Report contains {len(items)} real renderer images')

if __name__=='__main__': main()
