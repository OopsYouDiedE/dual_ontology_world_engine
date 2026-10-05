// Exact living-wing structural plan at 14 native px/m; north up. No runtime geometry.
const S=14,X=48,Y=48,W=864,H=640;
const palette={bg:"#171b21",wall:"#30333a",cap:"#62686f",floor:"#cfc6b1",hall:"#a69e8b",prep:"#72847a",wc:"#9fa196",store:"#96816b",lane:"#716a5c",grass:"#273d30",wood:"#795843",woodhi:"#a37f62",shadow:"#494441",linen:"#efe7d3",cover:"#a1b1c7",bed:"#7c6757",brass:"#c89a45"};
const commands=[],rooms=[],openings=[],quadrants=[];const r=(x,y,w,h,c)=>commands.push({op:"rect",xy:[Math.round(x),Math.round(y),Math.max(Math.round(x),Math.round(x+w)-1),Math.max(Math.round(y),Math.round(y+h)-1)],color:c});const l=(a,b,c,d,color)=>commands.push({op:"line",xy:[Math.round(a),Math.round(b),Math.round(c),Math.round(d)],color});
function box(rect,c){r(X+rect[0]*S,Y+rect[1]*S,rect[2]*S,rect[3]*S,c);}
function frame(rect){box(rect,"wall");l(X+rect[0]*S,Y+rect[1]*S,X+(rect[0]+rect[2])*S-1,Y+rect[1]*S,"cap");}
function local(q,rect){let [u,v,w,h]=rect;return [q.mirror?54.8-u-w:u,q.y+v,w,h];}
function fill(q,id,rect,c,occupied=null){const world=local(q,rect);box(world,c);rooms.push({id:q.id+"_"+id,type:id.startsWith("bedroom")?"bedroom":id,local_rect_m:rect,world_rect_m:world,pixel_rect:[Math.round(X+world[0]*S),Math.round(Y+world[1]*S),Math.round(world[2]*S),Math.round(world[3]*S)],occupied});return world;}
function item(q,rect,c){box(local(q,rect),c);}
function door(q,id,rect,c="floor"){box(local(q,rect),c);openings.push({id:q.id+"_"+id,local_rect_m:rect,world_rect_m:local(q,rect)});}
r(0,0,W,H,"bg");box([-1,-1,56.8,37.4],"grass");
// Two continuous exposed lanes between north and south sleeping quarters.
box([0,13.2,22.4,3],"lane");box([32.4,13.2,22.4,3],"lane");
for(let i=0;i<22;i++){let xx=i+0.4;box([xx,13.8,0.3,0.12],"hall");box([32.4+xx,14.5,0.2,0.14],"hall");}
// Main hall and southern annex shell, rooms leave the perimeter wall visible.
frame([22.4,0,10,29.4]);box([22.7,0.3,9.4,28.8],"hall");
frame([22.4,29.4,10,6]);box([22.7,29.7,9.4,5.4],"floor");
rooms.push({id:"central_hall",type:"hall",world_rect_m:[22.4,0,10,29.4],occupied:null},{id:"south_annex",type:"annex",world_rect_m:[22.4,29.4,10,6],occupied:null});
for(let yy=0.6;yy<29;yy+=1.5)for(let xx=23;xx<32;xx+=1.5)l(X+xx*S,Y+yy*S,X+(xx+1.1)*S,Y+yy*S,"floor");
for(const q of [{id:"NW",y:0,mirror:false},{id:"NE",y:0,mirror:true},{id:"SW",y:16.2,mirror:false},{id:"SE",y:16.2,mirror:true}]){
quadrants.push(q);frame(local(q,[0,0,22.4,13.2]));
for(let row=0;row<2;row++)for(let k=0;k<3;k++){
let u=.6+k*3.85,v=row?7.8:.6,occupied=k!==0;fill(q,"bedroom_"+row+"_"+k,[u,v,3.6,4.8],"floor",occupied);
// Sparse native plank seams.
for(let vy=v+.5;vy<v+4.5;vy+=.6)item(q,[u+.15,vy,3.3,.035],"hall");
item(q,[u+.4,v+.5,1.1,2.2],"shadow");item(q,[u+.35,v+.4,1.1,2.2],"bed");
item(q,[u+.42,v+.53,.95,1.85],occupied?"linen":"cover");
item(q,[u+.47,v+.57,.85,.43],occupied?"floor":"cover");
if(!occupied){item(q,[u+.42,v+1.1,.95,.08],"wc");item(q,[u+.55,v+.6,.08,1.75],"wc");}
item(q,[u+2.25,v+.45,.85,.65],"wood");item(q,[u+2.25,v+.45,.85,.08],"woodhi");item(q,[u+2.5,v+2.8,.6,1.1],"wood");
door(q,"bedroom_"+row+"_"+k,[u+1.35,row?7.5:5.4,.9,.3]);}
fill(q,"corridor",[.6,5.7,11.3,1.8],"hall");
fill(q,"activity",[12.2,.6,6,7.5],"floor");fill(q,"prep",[12.2,8.4,6,4.2],"prep");
fill(q,"wc",[18.5,.6,3.3,3.45],"wc");fill(q,"vestibule",[18.5,4.35,3.3,4.5],"hall");fill(q,"store",[18.5,9.15,3.3,3.45],"store");
// Communal furniture kept clear of the central passage and doors.
item(q,[13.25,2.3,3.3,1.5],"shadow");item(q,[13.15,2.2,3.3,1.5],"wood");item(q,[13.2,2.2,3.2,.15],"woodhi");
for(const [uu,vv]of [[13.4,1.4],[15,1.4],[13.4,4],[15,4]]){item(q,[uu,vv,.8,.65],"wood");item(q,[uu,vv,.8,.12],"woodhi");}
item(q,[13,10.3,4.3,.85],"wood");item(q,[13,10.3,4.3,.12],"woodhi");
for(let j=0;j<2;j++){item(q,[19.1+j*1.25,1.3,.75,.7],"linen");item(q,[19.2+j*1.25,1.45,.5,.4],"shadow");}
for(let j=0;j<2;j++){item(q,[19+j*1.3,10,.8,1.6],"wood");item(q,[19+j*1.3,10,.8,.1],"woodhi");}
door(q,"corridor_activity",[11.9,5.8,.3,1.6]);door(q,"prep_door",[14.5,8.1,1,.3]);
door(q,"activity_vestibule",[18.2,5,.3,1]);door(q,"wc_door",[19.6,4.05,1,.3]);door(q,"store_door",[19.6,8.85,1,.3]);
door(q,"isolation_gate",[21.8,5.85,.6,1.5],"hall");
let gate=local(q,[22.05,5.85,.08,1.5]);box(gate,"wood");box(local(q,[22.05,6.5,.08,.2]),"brass");
}
// Hall openings into quadrants align exactly with isolation-gate passage.
for(let yy of [5.85,22.05]){box([22.4,yy,.3,1.5],"hall");box([32.1,yy,.3,1.5],"hall");}
// Side doors into the outdoor 3m lanes.
for(const xx of [22.4,32.1])box([xx,14.15,.3,1.1],"lane");
box([26.85,0,1.1,.3],"hall");box([26.85,29.1,1.1,.6],"hall");box([26.85,35.1,1.1,.3],"floor");
// Three shallow steps across hall, preserving hall's continuous path.
for(let i=0;i<3;i++){box([22.7,14.9+i*.22,9.4,.22],"shadow");box([22.7,14.9+i*.22,9.4,.06],"floor");}
const recipe={name:"living_wing_plan_native_v1",width:W,height:H,palette,commands};
const manifest={asset:recipe.name,kind:"exact structural plan, not runtime texture",north:"up",scale_px_per_m:S,origin_px:[X,Y],total_dimensions_m:[54.8,35.4],canvas_px:[W,H],bedrooms:24,occupied_bedrooms:16,empty_bedrooms:8,quadrants,rooms,openings,lanes_m:[[0,13.2,22.4,3],[32.4,13.2,22.4,3]],central_hall_m:[22.4,0,10,29.4],south_annex_m:[22.4,29.4,10,6],steps:3,collision_footprints:null,occlusion_status:"not runtime verified",drawing_tool:"mcp__rpg_tilemap__draw_asset",runtime_integrated:false};
if(typeof module!=="undefined"){module.exports={recipe,manifest};if(require.main===module)process.stdout.write(JSON.stringify({recipe,manifest}));}

