'use strict';
const el=id=>document.getElementById(id),canvas=el('game'),ctx=canvas.getContext('2d');
const WORLD={width:1600,height:900},keys=new Map(),totals={wood:0,ice:0,stone:0};
let view={scale:1,x:0,y:0,width:1600,height:900},last=0,time=0,paused=false,ready=false,workHeld=false,logUntil=0;
let sprites={},background,randomSeed=721;
function random(){randomSeed=(randomSeed*1664525+1013904223)>>>0;return randomSeed/4294967296}
const player={name:'威尔逊',sheet:'wilson',x:1080,y:580,dir:0,mode:'idle',phase:0,progress:0,speed:175,target:null};
const companion={name:'温蒂',sheet:'wendy',x:830,y:425,dir:0,mode:'idle',phase:0,progress:0,speed:104,target:null,wait:1.2,stuck:0};
const nodes=[
 {x:1336,y:538,kind:'wood',label:'枯枝',stock:3,max:3},
 {x:1408,y:739,kind:'stone',label:'碎石',stock:3,max:3},
 {x:603,y:655,kind:'ice',label:'冰晶',stock:3,max:3},
 {x:666,y:369,kind:'wood',label:'枯枝',stock:3,max:3},
 {x:385,y:364,kind:'ice',label:'冰晶',stock:3,max:3},
 {x:950,y:837,kind:'wood',label:'枯枝',stock:3,max:3}
].map(n=>({...n,cooldown:0}));
const kindNames={wood:'木料',ice:'冰晶',stone:'石料'},snow=Array.from({length:70},()=>({x:random()*1600,y:random()*900,r:random()*1.6+.5,speed:random()*20+12,phase:random()*6.28}));
function log(message){el('eventLog').textContent=message;logUntil=time+5}
function loadImage(src){return new Promise((resolve,reject)=>{const image=new Image();image.onload=()=>resolve(image);image.onerror=()=>reject(new Error('无法加载 '+src));image.src=src})}
// Each source sheet has four direction rows and four action columns.
// Per-cell alpha bounds keep changing silhouettes anchored to the feet.
function sheet(image){const sw=image.width/4,sh=image.height/4,probe=document.createElement('canvas');probe.width=image.width;probe.height=image.height;const pc=probe.getContext('2d',{willReadFrequently:true});pc.drawImage(image,0,0);const data=pc.getImageData(0,0,image.width,image.height).data;const frames=[];
 for(let row=0;row<4;row++){frames[row]=[];for(let col=0;col<4;col++){let minX=sw,minY=sh,maxX=0,maxY=0;for(let y=4;y<sh-4;y++){for(let x=4;x<sw-4;x++){const gx=Math.floor(col*sw+x),gy=Math.floor(row*sh+y);if(data[(gy*image.width+gx)*4+3]>40){minX=Math.min(minX,x);minY=Math.min(minY,y);maxX=Math.max(maxX,x);maxY=Math.max(maxY,y)}}}frames[row][col]={x:col*sw,y:row*sh,w:sw,h:sh,anchorX:(minX+maxX)/2,anchorY:maxY,height:maxY-minY};}}
 const heights=frames.flat().map(f=>f.height).sort((a,b)=>a-b);return {image,frames,scale:122/heights[8]};}
function portrait(target,s){const c=document.createElement('canvas');c.width=106;c.height=114;const p=c.getContext('2d'),f=s.frames[0][0];const scale=94/f.height;p.drawImage(s.image,f.x,f.y,f.w,f.h,53-f.anchorX*scale,112-f.anchorY*scale,f.w*scale,f.h*scale);el(target).append(c)}
function resize(){const rect=canvas.getBoundingClientRect(),dpr=Math.min(devicePixelRatio||1,2);canvas.width=Math.round(rect.width*dpr);canvas.height=Math.round(rect.height*dpr);view={width:rect.width,height:rect.height,scale:Math.min(rect.width/WORLD.width,rect.height/WORLD.height),x:0,y:0,dpr};view.x=(rect.width-WORLD.width*view.scale)/2;view.y=(rect.height-WORLD.height*view.scale)/2;}
// The camp occupies the upper right. Characters stay outside its palisade.
function walkable(x,y){return x>120&&x<1490&&y>190&&y<845&&!(x>1060&&y<355)}
function facing(a,dx,dy){if(Math.abs(dx)>Math.abs(dy))a.dir=dx<0?1:2;else if(dy)a.dir=dy<0?3:0}
function move(a,dx,dy,dt){facing(a,dx,dy);const nx=a.x+dx*a.speed*dt,ny=a.y+dy*a.speed*dt;let moved=false;if(walkable(nx,a.y)){moved||=nx!==a.x;a.x=nx}if(walkable(a.x,ny)){moved||=ny!==a.y;a.y=ny}a.mode=moved?'walk':'idle';a.progress=0;a.phase+=dt*(moved?9:2);return moved}
function nearest(a){return nodes.filter(n=>n.stock>0).sort((b,c)=>Math.hypot(a.x-b.x,a.y-b.y)-Math.hypot(a.x-c.x,a.y-c.y))[0]}
function inReach(a,n){return n&&Math.hypot(a.x-n.x,a.y-n.y)<88}
function harvest(a,n,dt){a.mode='work';a.phase+=dt*7;facing(a,n.x-a.x,n.y-a.y);a.progress+=dt/1.45;if(a.progress>=1){a.progress=0;n.stock--;totals[n.kind]++;el(n.kind+'Count').textContent=totals[n.kind];log(a.name+'采集了'+kindNames[n.kind]+' +1'+(n.stock===0?'，这处物资需要恢复。':'。'));if(n.stock===0){n.cooldown=28;a.mode='idle';a.target=null;if(a===companion)a.wait=1.3}}}
function updatePlayer(dt){let direction=null;for(const key of keys.keys()){const map={w:'up',arrowup:'up',s:'down',arrowdown:'down',a:'left',arrowleft:'left',d:'right',arrowright:'right'};if(map[key])direction=map[key]}
 const target=nearest(player);player.target=inReach(player,target)?target:null;
 if(direction){const vec={up:[0,-1],down:[0,1],left:[-1,0],right:[1,0]}[direction];move(player,...vec,dt)}else if(workHeld&&player.target){harvest(player,player.target,dt)}else{player.mode='idle';player.phase+=dt;player.progress=0}
 el('playerState').textContent=player.mode==='walk'?'行走中':player.mode==='work'?'正在采集':'待命';
 el('interaction').classList.toggle('working',player.mode==='work');el('interactionText').textContent=player.mode==='work'?'采集'+player.target.label+'中…':player.target?'按住空格采集'+player.target.label+' · 剩余 '+player.target.stock:'靠近标记处，按住空格采集';
}
function updateAI(dt){const a=companion;if(a.wait>0){a.wait-=dt;a.mode='idle';a.phase+=dt;el('aiState').textContent='短暂休息';return}
 if(!a.target||a.target.stock===0){const choices=nodes.filter(n=>n.stock>0&&n!==player.target);a.target=choices.sort((b,c)=>Math.hypot(a.x-b.x,a.y-b.y)-Math.hypot(a.x-c.x,a.y-c.y))[Math.min(1,choices.length-1)]||nearest(a);a.progress=0;a.stuck=0;}
 if(!a.target){a.mode='idle';el('aiState').textContent='等待物资恢复';return}
 if(inReach(a,a.target)){harvest(a,a.target,dt);el('aiState').textContent='正在采集';return}
 const dx=a.target.x-a.x,dy=a.target.y-a.y;let mx=0,my=0;if(Math.abs(dx)>Math.abs(dy))mx=Math.sign(dx);else my=Math.sign(dy);
 if(!move(a,mx,my,dt)){a.stuck+=dt;move(a,mx?0:Math.sign(dx)||1,my?0:Math.sign(dy)||1,dt);if(a.stuck>2){a.target=null;a.wait=.5}}else a.stuck=0;
 el('aiState').textContent='前往'+(a.target?.label||'采集点');}
function update(dt){time+=dt;nodes.forEach(n=>{if(n.stock===0){n.cooldown-=dt;if(n.cooldown<=0)n.stock=n.max}});updatePlayer(dt);updateAI(dt);snow.forEach(s=>{s.x+=dt*(7+Math.sin(time*.2+s.phase)*5);s.y+=dt*s.speed;if(s.y>900){s.y=-4;s.x=random()*1600}if(s.x>1600)s.x=0});if(time>logUntil){el('eventLog').textContent='温蒂会自主寻找并采集物资，收获计入营地。';logUntil=Infinity}}
function label(text,x,y,fill='#23332ee8',color='#f0ebd7',size=13){ctx.font=`${size}px "Microsoft YaHei",sans-serif`;const width=ctx.measureText(text).width+20;ctx.fillStyle=fill;ctx.beginPath();ctx.roundRect(x-width/2,y-14,width,25,3);ctx.fill();ctx.fillStyle=color;ctx.textAlign='center';ctx.fillText(text,x,y+3)}
function drawNodes(){for(const n of nodes){const selected=n===player.target;ctx.save();ctx.translate(n.x,n.y);ctx.scale(1,.48);ctx.strokeStyle=n.stock===0?'#607b7777':selected?'#efe2ab':'#d5e4d9a0';ctx.lineWidth=selected?3:1.5;ctx.setLineDash([5,6]);ctx.beginPath();ctx.arc(0,0,selected?45:32,0,Math.PI*2);ctx.stroke();ctx.restore();if(n.stock>0){ctx.fillStyle=selected?'#f8dd8d':'#dce6d5';ctx.save();ctx.translate(n.x,n.y-9);ctx.rotate(Math.PI/4);ctx.fillRect(-3,-3,6,6);ctx.restore();if(selected)label(n.label,n.x,n.y+35);else{ctx.font='11px serif';ctx.textAlign='center';ctx.fillStyle='#273a33';ctx.fillText(n.label,n.x,n.y+26)}}else{ctx.font='11px monospace';ctx.textAlign='center';ctx.fillStyle='#3d5852';ctx.fillText(Math.ceil(n.cooldown)+'s',n.x,n.y+24)}}}
function drawActor(a){const s=sprites[a.sheet],col=a.mode==='work'?2+Math.floor(a.phase)%2:a.mode==='walk'?Math.floor(a.phase)%2:0,f=s.frames[a.sheet==='wilson'?[0,2,1,3][a.dir]:a.dir][col],scale=s.scale;
 ctx.save();ctx.translate(a.x,a.y);ctx.fillStyle='#1223203b';ctx.beginPath();ctx.ellipse(0,0,25,9,0,0,Math.PI*2);ctx.fill();if(a===player){ctx.strokeStyle='#ecda9eb0';ctx.lineWidth=2;ctx.beginPath();ctx.ellipse(0,1,31,11,0,0,Math.PI*2);ctx.stroke()}
 const bob=a.mode==='walk'?Math.sin(a.phase*Math.PI)*1.5:0;ctx.save();if(a.sheet==='wendy'&&a.dir===1&&col>=2)ctx.scale(-1,1);ctx.drawImage(s.image,f.x,f.y,f.w,f.h,-f.anchorX*scale,-f.anchorY*scale+bob,f.w*scale,f.h*scale);ctx.restore();
 const titleY=-132;label(a.name+(a===player?' · 你':' · AI'),0,titleY,a===player?'#27392deb':'#283b3dc9',a===player?'#f0ddb0':'#e4e9df',12);
 if(a.mode==='work'){ctx.fillStyle='#253b35dd';ctx.fillRect(-26,-117,52,4);ctx.fillStyle='#e7c77d';ctx.fillRect(-26,-117,52*a.progress,4)}ctx.restore();}
function draw(){ctx.setTransform(view.dpr,0,0,view.dpr,0,0);ctx.fillStyle='#243731';ctx.fillRect(0,0,view.width,view.height);ctx.translate(view.x,view.y);ctx.scale(view.scale,view.scale);ctx.drawImage(background,0,0,1600,900);drawNodes();[player,companion].sort((a,b)=>a.y-b.y).forEach(drawActor);ctx.fillStyle='#f4fcfabb';snow.forEach(s=>{ctx.beginPath();ctx.arc(s.x+Math.sin(time+s.phase)*3,s.y,s.r,0,Math.PI*2);ctx.fill()});}
function frame(timestamp){const dt=Math.min((timestamp-last)/1000,.04);last=timestamp;if(ready){if(!paused)update(dt);draw()}requestAnimationFrame(frame)}
function setPause(value){paused=value;keys.clear();workHeld=false;el('pauseOverlay').hidden=!paused;el('pause').setAttribute('aria-pressed',String(paused));el('pause').innerHTML=paused?'▷ <span>继续</span>':'Ⅱ <span>暂停</span>';if(!paused)el('gameFrame').focus({preventScroll:true})}
el('pause').onclick=()=>setPause(!paused);el('resume').onclick=()=>setPause(false);
el('fullscreen').onclick=async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen()}catch{log('当前窗口不支持全屏，可放大浏览器窗口。')}};
function reset(){Object.assign(player,{x:1080,y:580,dir:0,mode:'idle',phase:0,progress:0,target:null});Object.assign(companion,{x:830,y:425,dir:0,mode:'idle',phase:0,progress:0,target:null,wait:1.2,stuck:0});for(const k in totals){totals[k]=0;el(k+'Count').textContent='0'}nodes.forEach(n=>{n.stock=n.max;n.cooldown=0});log('营地已重置。靠近物资标记，按住空格开始采集。');setPause(false)}el('restart').onclick=reset;
const movement=new Set(['w','a','s','d','arrowup','arrowleft','arrowdown','arrowright']);
document.addEventListener('keydown',e=>{if(e.ctrlKey||e.metaKey||e.altKey)return;const key=e.key.toLowerCase();if(movement.has(key)){e.preventDefault();if(!paused&&!keys.has(key))keys.set(key,true)}if(e.code==='Space'&&!e.target.closest('button')){e.preventDefault();if(!paused)workHeld=true}if(key==='p'&&!e.repeat){e.preventDefault();setPause(!paused)}});
document.addEventListener('keyup',e=>{keys.delete(e.key.toLowerCase());if(e.code==='Space')workHeld=false});window.addEventListener('blur',()=>{keys.clear();workHeld=false});document.addEventListener('visibilitychange',()=>{if(document.hidden&&ready)setPause(true)});
document.querySelectorAll('[data-move]').forEach(button=>{const key={up:'w',left:'a',down:'s',right:'d'}[button.dataset.move];button.onpointerdown=e=>{e.preventDefault();button.setPointerCapture(e.pointerId);if(!paused)keys.set(key,true)};button.onpointerup=button.onpointercancel=()=>keys.delete(key)});
el('touchWork').onpointerdown=e=>{e.preventDefault();el('touchWork').setPointerCapture(e.pointerId);if(!paused)workHeld=true};el('touchWork').onpointerup=el('touchWork').onpointercancel=()=>workHeld=false;
new ResizeObserver(resize).observe(canvas);resize();
Promise.all([loadImage('assets/winter-ground.png'),loadImage('assets/wilson-sprite.png'),loadImage('assets/wendy-sprite.png')]).then(([ground,wilson,wendy])=>{background=ground;sprites={wilson:sheet(wilson),wendy:sheet(wendy)};portrait('wilsonPortrait',sprites.wilson);portrait('wendyPortrait',sprites.wendy);ready=true;el('loading').hidden=true;el('gameFrame').focus({preventScroll:true});log('威尔逊已抵达营地。使用 WASD 或方向键移动。')}).catch(error=>{el('loading').querySelector('h2').textContent='素材尚未就绪';el('loading').querySelector('p').textContent=error.message+'，请刷新重试。'});
requestAnimationFrame(frame);
