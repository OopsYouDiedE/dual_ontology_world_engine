const canvas=document.querySelector('#scene'),ctx=canvas.getContext('2d');
const W=1536,H=1024;
const checkpoints=[
[.12,'01-跨入门槛','威尔逊从敞开的中央门进入；麦斯威尔已站在木桌左侧。'],
[1.15,'02-沿路线走近','沿同一条连续路径进入房间，背景与桌边人物保持不动。'],
[2.55,'03-停步偷懒','停下、松肩、托脸，不愿去书架逐本翻找。'],
[3.65,'04-头顶输入中','道本语逐字出现，书本保持在架上。'],
[5.25,'05-输入完成即生效','最后字符出现，四本书开始响应；无额外提交动作。'],
[5.68,'06-四书离架','同四本书从同一层书架离开，按前后顺序飞近。'],
[6.04,'07-察觉与防备','威尔逊察觉异常抬手；麦斯威尔仍原地旁观。'],
[6.32,'08-第一本扑脸','红书短暂拍到脸侧后滑落，眼睛与表情仍可见。'],
[6.65,'09-后续书接连扑来','其余三本从肩侧和身体旁掠过，避免集中遮脸。'],
[7.55,'10-跌蹲与四书落地','威尔逊原地蹲下扶额；四本书落点从此保留。'],
[8.45,'11-麦斯威尔大笑','桌边原地仰头大笑，脚底不动。'],
[9.55,'12-恶魔完整成句','华丽的道本语一次成句；威尔逊在原地抬头，震惊地看向麦斯威尔。'],
[10.3,'13-桌下指尖涌出','阴影集中在桌底，先露指尖。'],
[11.05,'14-大手抓住桌沿','手腕仍连着桌下阴影，桌子不漂移、不改变占地。'],
[11.7,'15-压改第一段','手指压过桌面，约三分之一变为黑白格。'],
[12.3,'16-压改第二段','继续压改到三分之二；其他对象完全继承。'],
[12.95,'17-棋盘桌面完成','棋盘嵌入同一桌面，原有边框和桌脚保持不变。'],
[13.65,'18-暗影手收回','双手沿桌底方向退回，转换结果保留。'],
[15.2,'19-最终场景','麦斯威尔从容立于棋桌旁，威尔逊仍蹲在原处，睁大眼睛看着他。']
];
window.checkpoints=checkpoints;
const assets={};
const paths=['room','wilson','maxwell','table','hands','books','wilson-reaction'];
const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v));
const lerp=(a,b,t)=>a+(b-a)*t;
function sprite(name,sx,sy,sw,sh,x,y,w,h){ctx.drawImage(assets[name],sx,sy,sw,sh,x,y,w,h)}
function footShadow(x,y,w){ctx.save();ctx.fillStyle='rgba(0,0,0,.25)';ctx.beginPath();ctx.ellipse(x,y-3,w,9,0,0,Math.PI*2);ctx.fill();ctx.restore()}
function wilson(p,x,y,reaction=0){
 if(reaction>0){
   if(reaction<1){ctx.save();ctx.globalAlpha=1-reaction;wilson(p,x,y,0);ctx.restore()}
   ctx.save();ctx.globalAlpha=reaction;
   const s=.175;
   sprite('wilson-reaction',0,0,1254,1254,x-640*s,y-1190*s,1254*s,1254*s);
   ctx.restore();return;
 }
 // Every pose keeps its foot contact on the same world anchor. Crouch retains anatomical scale.
 const anchors=[[257,485],[251,485],[267,485],[249,475],[237,482],[256,477]];
 const [ax,ay]=anchors[p],s=.49;
 sprite('wilson',(p%3)*512,Math.floor(p/3)*512,512,512,x-ax*s,y-ay*s,512*s,512*s);
}
function maxwell(p,x,y){
 const anchors=[[272,951],[279,951],[236,951]],s=.305;
 sprite('maxwell',p*512,0,512,1024,x-anchors[p][0]*s,y-anchors[p][1]*s,512*s,1024*s);
}
const table={x:415,y:525,w:240,h:262};
function tableBase(){
 sprite('table',96,190,650,710,table.x,table.y,table.w,table.h);
}
function board(progress){
 if(progress<=0)return;
 // Only the interior surface is affected. Legs and rim are always the original sprite.
 const sx=table.w/650,sy=table.h/710;
 const pts=[[326,61],[89,202],[326,344],[562,202]].map(([x,y])=>[table.x+x*sx,table.y+y*sy]);
 ctx.save();ctx.beginPath();pts.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.closePath();ctx.clip();
 ctx.beginPath();ctx.rect(table.x,table.y,table.w*progress,table.h);ctx.clip();
 sprite('table',789,190,650,710,table.x,table.y,table.w,table.h);ctx.restore();
}
function shadowHands(p,amount,front=false){
 const x=table.x-16,y=table.y+7,w=table.w+32,h=table.h-2;
 ctx.save();
 // Grow/retract vertically from a fixed ground point. No full-table translation.
 ctx.beginPath();ctx.rect(x-10,y+h*(1-amount)-10,w+20,h*amount+20);ctx.clip();
 if(front){
   ctx.beginPath();ctx.rect(x,y+22,w,115);ctx.clip();
 }
 sprite('hands',p*512,145,512,730,x,y,w,h);
 ctx.restore();
}
function speech(text,x,y){if(!text)return;ctx.save();ctx.font='bold 25px Georgia,serif';ctx.textAlign='center';ctx.textBaseline='bottom';ctx.lineJoin='round';ctx.lineWidth=5;ctx.strokeStyle='#1b1410';ctx.strokeText(text,x,y);ctx.fillStyle='#eee3c9';ctx.fillText(text,x,y);ctx.restore()}
function demonSpeech(text,x,y){
 ctx.save();ctx.font='40px Gabriola, "Palatino Linotype", serif';ctx.textAlign='center';ctx.textBaseline='bottom';
 ctx.lineJoin='round';ctx.lineWidth=4;ctx.strokeStyle='#281319';ctx.strokeText(text,x,y);
 const gold=ctx.createLinearGradient(0,y-40,0,y);gold.addColorStop(0,'#fff1bf');gold.addColorStop(.5,'#e8c37f');gold.addColorStop(1,'#b28b53');ctx.fillStyle=gold;ctx.fillText(text,x,y);
 const half=ctx.measureText(text).width/2;ctx.font='28px Georgia,serif';ctx.fillText('❧',x-half-23,y+1);ctx.save();ctx.translate(x+half+23,y-15);ctx.scale(-1,1);ctx.fillText('❧',0,16);ctx.restore();
 ctx.strokeStyle='#ba995e';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(x-half*.75,y+2);ctx.quadraticCurveTo(x,y+12,x+half*.75,y+2);ctx.stroke();ctx.restore();
}
const bookKinds=[0,1,2,1];
const shelf=[{x:415,y:350},{x:448,y:350},{x:479,y:350},{x:509,y:350}];
const floor=[{x:755,y:698},{x:827,y:697},{x:747,y:727},{x:832,y:735}];
function book(k,x,y,width,rotation=0,open=false){
 const cell=open?3:k,cols=2,iw=assets.books.width/2,ih=assets.books.height/2;
 ctx.save();ctx.translate(x,y);ctx.rotate(rotation);
 // Object-only atlas rectangles, crop is performed by the renderer at draw time.
 const crop=open?[38,85,570,440]:[86,90,445,435];
 const [a,b,c,d]=crop;
 sprite('books',(cell%cols)*iw+a,Math.floor(cell/cols)*ih+b,c,d,-width/2,-width*d/c/2,width,width*d/c);
 ctx.restore();
}
const wilsonWords='lipu mi wile o kama tawa mi.';
const maxwellWords='supa ni o kama supa musi.';
function stateAt(t){
 let wx=770,wy=665;
 if(t<2.25){let u=clamp(t/2.25);wx=lerp(779,770,u);wy=lerp(455,665,u)}
 if(t>=6.22)wx+=24*clamp((t-6.22)/.6);
 let wp=t<2.25?(Math.floor(t*6)%2):2;
 if(t>=5.95&&t<6.22)wp=3;
 if(t>=6.22&&t<7.02)wp=4;
 if(t>=7.02)wp=5;
 let mp=t>=7.7&&t<9.05?1:t>=9.3?2:0;
 let progress=t<11.4?0:t<12?.34:t<12.65?.67:1;
 let hand=0,amount=0;
 if(t>=9.65&&t<10.65){hand=0;amount=clamp((t-9.65)/.65)}
 if(t>=10.65&&t<11.4){hand=1;amount=1}
 if(t>=11.4&&t<13.15){hand=2;amount=1}
 if(t>=13.15&&t<14.15){hand=1;amount=1-clamp((t-13.15)/1)}
 return{wx,wy,wp,mp,progress,hand,amount,reaction:clamp((t-9.3)/.22)};
}
function render(t){
 t=clamp(t,0,16);window.currentTime=t;
 ctx.clearRect(0,0,W,H);ctx.drawImage(assets.room,0,0,W,H);
 const s=stateAt(t);
 // Shelf books are retained until their individual launch, with exact source identity.
 const flights=[];
 for(let i=0;i<4;i++){
   const launch=5.25+i*.12,hit=6.22+i*.12,land=7.05+i*.12;
   if(t<launch){book(bookKinds[i],shelf[i].x,shelf[i].y,29,-.1)}
   else {
     const targets=[[775,529],[705,577],[862,568],[850,618]],target=targets[i];
     if(t<hit){const u=clamp((t-launch)/(hit-launch));flights.push({i,x:lerp(shelf[i].x,target[0],u),y:lerp(shelf[i].y,target[1],u)+(i===0?-35:80)*Math.sin(u*Math.PI),w:lerp(29,42,u),r:lerp(-.1,.2,u),open:false})}
     else if(t<land){const u=clamp((t-hit)/(land-hit)),fall=i===0?clamp((u-.1)/.9):u;flights.push({i,x:lerp(target[0],floor[i].x,fall),y:lerp(target[1],floor[i].y,fall**1.25),w:lerp(i===0?46:42,i===0?52:45,fall),r:lerp(.2,i===0?.12:[0,.3,-.2,.6][i],fall),open:i===0})}
     else book(bookKinds[i],floor[i].x,floor[i].y,i===0?52:45,i===0?.12:[0,.3,-.2,.6][i],i===0);
   }
 }
 footShadow(340,769,28);maxwell(s.mp,340,769);
 if(s.amount>0)shadowHands(s.hand,s.amount);
 tableBase();board(s.progress);
 if(s.amount>0)shadowHands(s.hand,s.amount,true);
 for(const f of flights.filter(f=>f.i>0))book(bookKinds[f.i],f.x,f.y,f.w,f.r,f.open);
 footShadow(s.wx,s.wy,28);wilson(s.wp,s.wx,s.wy,s.reaction);
 for(const f of flights.filter(f=>f.i===0))book(bookKinds[f.i],f.x,f.y,f.w,f.r,f.open);
 if(t>=3&&t<5.95){const n=Math.floor(clamp((t-3)/2.25)*wilsonWords.length);speech(wilsonWords.slice(0,n),s.wx-5,s.wy-258)}
 if(t>=9.3&&t<11.35)demonSpeech(maxwellWords,340,470);
 const index=checkpoints.findLastIndex(c=>c[0]<=t+.01);
 document.querySelector('#notes').textContent=checkpoints[Math.max(0,index)][2];
 document.querySelector('#time').textContent=t.toFixed(2)+' s';
 document.querySelector('#seek').value=t;
 document.querySelectorAll('.frames button').forEach((b,i)=>b.classList.toggle('active',i===index));
 return s;
}
window.render=render;window.stateAt=stateAt;
let playing=false,start=0,offset=0;
function loop(now){if(playing){const t=offset+(now-start)/1000;render(t);if(t>=16){playing=false;document.querySelector('#play').textContent='播放'}}requestAnimationFrame(loop)}
function pause(){playing=false;document.querySelector('#play').textContent='播放'}
document.querySelector('#play').onclick=()=>{if(playing)pause();else{offset=window.currentTime>=16?0:window.currentTime;start=performance.now();playing=true;document.querySelector('#play').textContent='暂停'}};
document.querySelector('#restart').onclick=()=>{offset=0;start=performance.now();playing=true;document.querySelector('#play').textContent='暂停'};
document.querySelector('#seek').oninput=e=>{pause();render(+e.target.value)};
checkpoints.forEach(([t,name])=>{const b=document.createElement('button');b.textContent=name;b.onclick=()=>{pause();render(t)};document.querySelector('#frames').append(b)});
if(new URLSearchParams(location.search).has('scene'))document.body.classList.add('only-scene');
window.ready=Promise.all(paths.map(name=>new Promise((resolve,reject)=>{const im=new Image;im.onload=()=>{assets[name]=im;resolve()};im.onerror=reject;im.src='assets/'+name+'.png'}))).then(()=>{render(0);requestAnimationFrame(loop);return true});
