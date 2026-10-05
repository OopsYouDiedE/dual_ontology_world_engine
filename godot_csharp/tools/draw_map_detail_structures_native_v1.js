// Native 1x commands for map detail study. Run: node this-file.js > recipe.json
// Existing material ramps: chapter2_art/{buildings,outdoor}.py.
// Upper-left illumination follows oblique.py SUN (-0.40,-0.50,0.77).
const palette={bg:"#171b21",panel:"#242b30",rule:"#47535a",ink:"#dcd8cd",shadow:"#141518"};
const ramps={B:["#141518","#1d1e22","#27292e","#32353b","#3f434a","#4e535b","#62686f","#7a8087"],W:["#5f5b55","#7a756d","#958f85","#b0aa9e","#c9c4b8","#dcd8cd","#ebe8df","#f6f4ee"],T:["#341710","#4d2219","#662e21","#7e3b2a","#964a35","#ac5c42","#bf7253","#cd8a6a"],S:["#4f483f","#665d51","#7d7363","#938875","#a89d88","#bbb19b","#cbc2ad","#d9d1be"],C:["#163029","#1f4034","#295142","#346250","#43755f","#568a70","#6fa084","#8db89c"],G:["#4d473e","#5f584c","#716a5c","#837b6b","#958d7b","#a69e8b","#b6ae9b"],D:["#1e120c","#2c1a11","#3c2418","#4e3020","#613d29","#764c33","#8b5d3f"],L:["#12211b","#1a3027","#244034","#2f5142","#3c6351","#4c7662"],P:["#1f1828","#302539","#43344f","#594767","#745f80","#978099"],F:["#39435c","#53607c","#7888a4","#a1b1c7","#c8d4e1","#e5ecf2"]};
for(const [n,a] of Object.entries(ramps)) a.forEach((v,i)=>palette[n+i]=v);
const commands=[];const r=(x,y,w,h,c)=>commands.push({op:"rect",xy:[x,y,x+w-1,y+h-1],color:c});const l=(x,y,x2,y2,c)=>commands.push({op:"line",xy:[x,y,x2,y2],color:c});const p=(x,y,c)=>commands.push({op:"point",xy:[x,y],color:c});
let seed=2056;function rand(n){seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed%n;}
function texture(x,y,w,h,base,n){const key=base[0],start=Number(base.slice(1));for(let i=0;i<n;i++)p(x+rand(w),y+rand(h),key+(start+rand(3)));}
function stone(x,y,w,h,t=4){r(x,y,w,h,"B1");r(x+1,y+1,w-2,h-2,"B"+t);l(x+1,y+1,x+w-3,y+1,"B"+Math.min(7,t+2));l(x+1,y+2,x+1,y+h-3,"B"+Math.min(7,t+1));l(x+w-2,y+2,x+w-2,y+h-2,"B"+Math.max(1,t-1));texture(x+2,y+2,w-4,h-4,"B"+Math.max(1,t-1),Math.floor(w*h/30));}
function ground(x,y,w,h){r(x,y,w,h,"G2");texture(x,y,w,h,"G2",w*h/7);for(let i=0;i<w*h/90;i++){let xx=x+rand(w-5), yy=y+rand(h-4);r(xx,yy,3+rand(3),2,"G"+(4+rand(3)));p(xx+1,yy+2,"G1");}}
function panel(x,y,n){r(x,y,192,208,"rule");r(x+1,y+1,190,206,"panel");l(x+12,y+26,x+179,y+26,"rule");for(let i=0;i<n;i++){r(x+12+5*i,y+11,2,8,"ink");}for(let xx=x+16;xx<x+180;xx+=32){l(xx,y+195,xx,y+200,"rule");}}
r(0,0,640,480,"bg");const panels=[[16,24],[224,24],[432,24],[16,256],[224,256],[432,256]];panels.forEach((a,i)=>panel(...a,i+1));
// 01. Limewash doorway: native black basalt voussoirs, quoin, red tile canopy.
ground(28,172,168,40);r(30,67,160,108,"W5");texture(31,68,157,100,"W4",240);
for(let yy=68;yy<172;yy+=13){stone(30,yy,yy%26?16:22,13,4);stone(170,yy,20,13,3);}
for(let xx=48;xx<172;xx+=20)stone(xx,155,20,20,3);
r(76,93,69,83,"B1");r(83,98,55,76,"D2");
for(let xx=85;xx<137;xx+=9){r(xx,101,7,69,"D4");l(xx,101,xx,169,"D5");l(xx+6,104,xx+6,170,"D1");}
r(87,105,19,27,"D2");r(113,105,19,27,"D2");r(87,138,19,29,"D3");r(113,138,19,29,"D3");
l(110,100,110,172,"D1");r(129,135,3,7,"S3");p(130,135,"S7");
for(let yy=94;yy<170;yy+=12){stone(72,yy,11,12,4);stone(138,yy,11,12,3);}
for(let xx=73;xx<148;xx+=12)stone(xx,82,12,13,4);
r(26,64,168,8,"B1");
for(let row=0;row<3;row++)for(let xx=28-row*2;xx<190+row*2;xx+=9){let yy=47+row*6;r(xx,yy,9,8,"T3");l(xx+1,yy+1,xx+7,yy+1,"T6");l(xx+1,yy+2,xx+1,yy+5,"T5");l(xx+7,yy+3,xx+7,yy+7,"T1");}
r(27,64,169,3,"T1");l(28,64,194,64,"T4");
r(71,172,78,6,"S2");l(72,172,146,172,"S6");r(65,179,90,7,"S3");l(66,179,153,179,"S7");r(58,187,105,8,"S2");l(59,187,160,187,"S6");
// 02. Stone stair with staggered dressed stone and iron handrail.
ground(236,148,168,64);r(257,91,115,19,"B2");for(let xx=258;xx<371;xx+=24)stone(xx,92,24,18,4);
for(let i=0;i<6;i++){let x=252-i*3,y=110+i*14,w=128+i*6;r(x,y,w,14,"S2");r(x+1,y,w-2,6,"S5");l(x+1,y,x+w-2,y,"S7");l(x+1,y+7,x+w-2,y+7,"S1");for(let xx=x+20+(i%2)*17;xx<x+w-3;xx+=35)l(xx,y+1,xx,y+12,"S3");texture(x+2,y+2,w-4,3,"S4",18);}
for(const x of [251,384]){l(x,90,x,151,"B1");l(x+1,90,x+1,150,"B6");l(x,132,x-15,193,"B1");l(x+1,133,x-14,193,"B5");}
l(250,87,237,153,"B6");l(382,88,400,157,"B4");
// 03. Rain barrel plus wall-fed copper gutter and drain grate.
ground(444,178,168,34);r(449,66,154,104,"W4");texture(450,67,151,97,"W3",180);
r(450,159,153,17,"B2");for(let xx=451;xx<601;xx+=23)stone(xx,160,23,16,3);
r(451,56,143,6,"C1");l(451,56,592,56,"C6");r(585,58,8,62,"C3");l(586,59,586,118,"C6");l(591,61,591,119,"C1");r(580,73,18,6,"B2");r(584,73,10,2,"B6");l(589,117,581,125,"C4");l(581,125,574,125,"C4");
r(482,121,60,67,"D1");r(486,123,52,63,"D3");
for(let xx=487;xx<538;xx+=7){r(xx,125,6,58,"D"+(3+rand(3)));l(xx,127,xx,182,"D6");l(xx+5,127,xx+5,183,"D1");}
for(let yy of [135,168,183]){r(483,yy,58,5,"B2");l(485,yy,537,yy,"B6");for(let xx=489;xx<537;xx+=14)p(xx,yy+2,"B7");}
r(489,118,46,10,"D2");r(493,120,38,6,"F1");l(495,120,529,120,"F3");l(489,118,533,118,"D6");r(514,175,10,5,"C4");r(522,177,5,9,"C2");p(523,177,"C7");
r(552,177,48,16,"B1");r(554,179,44,12,"B3");for(let xx=557;xx<597;xx+=5){l(xx,180,xx,190,"B0");p(xx-1,179,"B7");}l(553,177,598,177,"B6");
// 04. Copper service elbow, flange bolts and valve.
ground(28,402,168,42);r(40,309,145,91,"B2");
for(let yy=310;yy<399;yy+=16)for(let xx=41-(yy%32?10:0);xx<185;xx+=27){let w=Math.min(26,184-xx);if(xx>=41&&w>4)stone(xx,yy,w,15,3);}
r(58,332,105,12,"C2");r(59,333,102,5,"C5");l(60,333,159,333,"C7");l(61,343,159,343,"C1");
r(150,337,15,72,"C2");r(151,339,5,66,"C5");l(151,339,151,404,"C7");l(164,345,164,405,"C0");
for(const xx of [83,124]){r(xx,328,9,21,"C1");r(xx+1,329,6,18,"C4");l(xx+1,329,xx+6,329,"C7");p(xx+3,332,"S6");p(xx+3,345,"S6");}
r(146,369,23,9,"C1");r(147,370,20,5,"C4");l(148,370,165,370,"C7");for(const xx of [148,165]){r(xx,371,2,3,"S6");}
r(105,321,5,14,"C3");l(106,321,106,330,"C7");r(96,313,24,3,"T3");r(98,309,20,2,"T5");l(96,313,99,308,"T6");l(118,309,120,314,"T1");r(106,312,4,3,"S6");
r(45,327,12,23,"B1");r(46,328,9,20,"B4");p(48,330,"B7");p(48,345,"B7");
// 05. Gravel lane, fitted basalt gutter, edging and tufts.
ground(236,301,168,143);
for(let yy=302;yy<442;yy+=16){stone(237,yy,19,16,4);stone(383,yy,19,16,3);}
r(261,302,16,139,"B1");l(261,302,261,440,"B6");l(276,302,276,440,"B3");for(let yy=304;yy<438;yy+=15){r(264,yy,9,12,"B2");l(265,yy,270,yy,"B5");p(270,yy+6,"F1");}
for(let i=0;i<65;i++){let xx=283+rand(94),yy=307+rand(129);r(xx,yy,3+rand(5),2+rand(3),"G"+(3+rand(4)));p(xx,yy,"G6");p(xx+2,yy+3,"G1");}
for(const [xx,yy] of [[243,321],[393,371],[245,417],[394,429]]){l(xx,yy,xx-3,yy-8,"L3");l(xx,yy,xx+2,yy-10,"L4");l(xx+2,yy+1,xx+6,yy-5,"L2");}
// 06. Hydrangea clumps in dark volcanic soil with individual leaf/flower pixels.
ground(444,402,168,42);r(454,414,143,10,"B2");for(let xx=455;xx<596;xx+=22)stone(xx,414,22,10,4);
function ellipse(cx,cy,rx,ry,c){for(let yy=-ry;yy<=ry;yy++){let ww=Math.floor(rx*Math.sqrt(1-yy*yy/(ry*ry)));r(cx-ww,cy+yy,ww*2+1,1,c);}}
for(const [cx,cy] of [[480,386],[519,379],[558,390]]){ellipse(cx+5,cy+20,28,8,"shadow");for(let i=0;i<50;i++){let xx=cx-24+rand(49),yy=cy-23+rand(40);ellipse(xx,yy,3+rand(4),2+rand(3),"L"+(1+rand(4)));l(xx-2,yy-1,xx+2,yy,"L5");}for(let i=0;i<12;i++){let xx=cx-19+rand(39),yy=cy-33+rand(33);ellipse(xx,yy,6,5,"P3");for(let j=0;j<8;j++){let fx=xx-4+rand(9),fy=yy-3+rand(7);p(fx,fy,"F"+(2+rand(3)));p(fx-1,fy-1,"P5");p(fx+1,fy+1,"P2");}p(xx-2,yy-2,"F5");}}
const recipe={name:"detail_structures_native_v1",width:640,height:480,palette,commands};
if(typeof module!=="undefined"){module.exports=recipe;if(require.main===module)process.stdout.write(JSON.stringify(recipe));}

