import fs from 'node:fs/promises';
import path from 'node:path';
import http from 'node:http';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const host=http.createServer(async(req,res)=>{try{const file=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!file.startsWith(root+path.sep)&&file!==root){res.writeHead(403);res.end();return}const f=file===root?path.join(file,'index.html'):file;res.setHeader('Content-Type',f.endsWith('.html')?'text/html; charset=utf-8':f.endsWith('.js')?'text/javascript; charset=utf-8':f.endsWith('.png')?'image/png':'application/octet-stream');res.end(await fs.readFile(f))}catch{res.writeHead(404);res.end()}});
host.listen(8769,'127.0.0.1');
const chrome=spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',['--headless=new','--no-sandbox','--no-first-run','--no-default-browser-check','--disable-gpu','--hide-scrollbars','--remote-debugging-port=9239','--user-data-dir=C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/chrome-art-capture','about:blank'],{windowsHide:true,stdio:'ignore'});
let pages;
for(let i=0;i<30;i++){try{pages=await (await fetch('http://127.0.0.1:9239/json',{signal:AbortSignal.timeout(700)})).json();if(pages.length)break}catch{}await new Promise(r=>setTimeout(r,200))}
if(!pages?.length)throw Error('Chrome unavailable');
const ws=new WebSocket(pages.find(p=>p.type==='page').webSocketDebuggerUrl);await new Promise(r=>ws.addEventListener('open',r,{once:true}));
let seq=0;const pending=new Map;
ws.addEventListener('message',e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);if(m.error)p.reject(m.error);else p.resolve(m.result)}});
const call=(method,params={})=>new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}))});
await call('Page.enable');await call('Emulation.setDeviceMetricsOverride',{width:1536,height:1200,deviceScaleFactor:1,mobile:false});
await call('Page.navigate',{url:'http://127.0.0.1:8769/index.html'});
for(let i=0;i<60;i++){const r=await call('Runtime.evaluate',{expression:'typeof window.ready !== "undefined"',returnByValue:true});if(r.result.value)break;await new Promise(r=>setTimeout(r,100))}
const ready=await call('Runtime.evaluate',{expression:'window.ready',awaitPromise:true,returnByValue:true});if(ready.exceptionDetails)throw Error(JSON.stringify(ready.exceptionDetails));
const info=await call('Runtime.evaluate',{expression:'window.checkpoints',returnByValue:true});
const checkpoints=info.result.value;
if(!checkpoints){console.log(await call('Runtime.evaluate',{expression:'JSON.stringify({url:location.href,html:document.documentElement.outerHTML.slice(0,1800)})',returnByValue:true}));throw Error('Page did not initialize')}
const all=process.argv.includes('--all'),list=all?checkpoints:checkpoints.filter((_,i)=>[2,7,14,18].includes(i));
for(const [t,name] of list){
const result=await call('Runtime.evaluate',{expression:'window.render('+t+'); document.querySelector("canvas").toDataURL("image/png")',returnByValue:true});
if(result.exceptionDetails)throw Error(JSON.stringify(result.exceptionDetails));
await fs.writeFile(path.join(root,'frames',name+'.png'),Buffer.from(result.result.value.split(',')[1],'base64'));
}
if(all){
const result=await call('Runtime.evaluate',{expression:"(()=>{const out=document.createElement('canvas');out.width=1536;out.height=1460;const c=out.getContext('2d');c.fillStyle='#171310';c.fillRect(0,0,out.width,out.height);c.font='bold 23px sans-serif';c.fillStyle='#e7d9c4';c.fillText('言灵发动 · 固定机位连续关键帧',24,36);for(let i=0;i<checkpoints.length;i++){render(checkpoints[i][0]);const x=(i%4)*384,y=60+Math.floor(i/4)*280;c.drawImage(canvas,x,y,384,256);c.font='14px sans-serif';c.fillStyle='#e7d9c4';c.fillText(checkpoints[i][1],x+10,y+272)}return out.toDataURL('image/png')})()",returnByValue:true});
await fs.writeFile(path.join(root,'连续关键帧总览.png'),Buffer.from(result.result.value.split(',')[1],'base64'));
await fs.writeFile(path.join(root,'keyframes.json'),JSON.stringify(checkpoints.map(([time,name,note])=>({time,file:'frames/'+name+'.png',note})),null,2));
}
console.log(JSON.stringify({exported:list.length,url:'http://127.0.0.1:8769/index.html'}));
if(process.argv.includes('--video')){
 const ffmpeg=spawn('C:/Users/iii/Documents/ffmpeg/bin/ffmpeg.exe',['-y','-loglevel','error','-f','image2pipe','-framerate','24','-i','pipe:0','-c:v','libx264','-preset','fast','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',path.join(root,'连续动作预演.mp4')],{windowsHide:true,stdio:['pipe','ignore','inherit']});
 const encoded=new Promise((resolve,reject)=>{ffmpeg.on('exit',code=>code===0?resolve():reject(Error('ffmpeg exit '+code)));ffmpeg.on('error',reject)});
 await call('Runtime.evaluate',{expression:"pause();window.videoCanvas=document.createElement('canvas');videoCanvas.width=1152;videoCanvas.height=768;window.videoCtx=videoCanvas.getContext('2d')"});
 for(let i=0;i<384;i++){
   const frame=await call('Runtime.evaluate',{expression:'render('+i/24+');videoCtx.drawImage(canvas,0,0,1152,768);videoCanvas.toDataURL("image/png")',returnByValue:true});
   if(!ffmpeg.stdin.write(Buffer.from(frame.result.value.split(',')[1],'base64')))await new Promise(resolve=>ffmpeg.stdin.once('drain',resolve));
 }
 ffmpeg.stdin.end();await encoded;
 console.log('Video exported: 16 seconds.');
}
await call('Browser.close').catch(()=>{});ws.close();chrome.kill();
if(process.argv.includes('--serve')){console.log('Preview server stays available.')}else host.close();
