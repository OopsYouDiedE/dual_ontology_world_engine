using DuelView.Compiled;
using static DVProgram;
class Mechanisms {
static object L(params object[] a)=>new List<object>(a);
static object Rec(params object[] a){var r=new List<object>();for(int i=0;i<a.Length;i+=2)r.Add(L(a[i],a[i+1]));return r;}
static object Get(object a,string k)=>R.record_find(a,k,false);
static object With(object a,string k,object v)=>R.record_with(a,k,v);
static object Ref(string f)=>F_dv_ref("o","f",f);
static object Cell(object v,object? claims=null)=>With(F_dv_cell(v,1d,0d,L()),"claims",claims??L());
static object World(bool physical=false){var w=F_dv_world("w",physical?"physical":"mind",physical?"":"s");w=F_dv_add_object(w,F_dv_object("o","perceived",L()));return With(w,"time",0d);}
static object Put(object w,string field,object value)=>F_dv_store(w,Ref(field),Cell(value));
static object Law(string name,object premise,string target,double conf=1)=>F_dv_axiom(name,1d,premise,L(F_dv_assign(Ref(target),1d)));
static object Adopt(object w,object law,double conf=1,string mode="infer")=>F_dv_adopt(w,law,mode,conf,L("basis"));
static object Actor(object w,bool dirty,double budget)=>Rec("id","s","mind",R.snapshot(w),"policy",Rec("reason_budget",budget,"opt_dirty",dirty,"opt_dirty_verify",1d));
static object Clean(object a){var p=F_dv_without(F_dv_without(Get(a,"policy"),"opt_dirty"),"opt_dirty_verify");return With(a,"policy",p);}
static void Equal(string label,object a,object b){if(!R.Bool(R.same_value(a,b)))throw new Exception(label+" DIFFER\n"+R.json_stringify(a)+"\n"+R.json_stringify(b));}
static object Run(object actor,object events,double budget)=>F_dvs_reason_run(With(actor,"policy",With(Get(actor,"policy"),"reason_budget",budget)),events,0d,"root");
static void Reason(string label,object world,double budget,int rounds=1,bool mutateClaims=false){
var off=Actor(world,false,budget);var on=Actor(world,true,budget);var eo=L();var en=L();
for(int i=0;i<rounds;i++){
 if(mutateClaims&&i==3){var claims=L(Rec("id","obs2","source","initial","origin","initial","ref",Ref("x"),"value",1d,"trust",1d));off=With(off,"mind",F_dv_store(Get(off,"mind"),Ref("x"),Cell(1d,claims)));on=With(on,"mind",F_dv_store(Get(on,"mind"),Ref("x"),Cell(1d,claims)));}
 off=Run(off,eo,budget);on=Run(on,en,budget);Equal(label+" actor round "+i,Clean(off),Clean(on));Equal(label+" events round "+i,eo,en);
 if(mutateClaims&&i==3&&R.Num(Get(Get(on,"last_reason_report"),"steps"))==0)throw new Exception("claims change did not wake rule");
}
Console.WriteLine(label+": identical actor, signatures, derivations and ordered events");}
static void Clock(string label,object world,object law,object clock,double due,double expected){
world=Adopt(world,law,1,"enforce");var proc=R.snapshot(L(Rec("law",Get(law,"name"))));var story=Rec("clock_ref",clock,"world_processes",proc,"run_config",Rec("opt_dirty",false));
var a=F_dvs_environment_next_time(world,story,due);var b=F_dvs_environment_next_time(world,With(story,"run_config",Rec("opt_dirty",true,"opt_dirty_verify",1d)),due);Equal(label,a,b);Equal(label+" expected",b,expected);Console.WriteLine(label+": "+b+" identical with verification");}
static void Main(){
var forward=Put(World(),"x",1d);forward=Adopt(forward,Law("a",F_dv_fact(Ref("x"),1d),"y"));forward=Adopt(forward,Law("b",F_dv_fact(Ref("y"),1d),"z"));Reason("forward cascade",forward,10);
var reverse=Put(World(),"x",1d);reverse=Adopt(reverse,Law("b",F_dv_fact(Ref("y"),1d),"z"));reverse=Adopt(reverse,Law("a",F_dv_fact(Ref("x"),1d),"y"));Reason("earlier-rule next-pass cascade",reverse,10);Reason("budget exhausted then resume",reverse,1,4);
var claims=Put(World(),"x",1d);claims=Adopt(claims,Law("claim",F_dv_fact(Ref("x"),1d),"y"),0.6);Reason("equal value different claims",claims,10,5,true);
var bad=F_dv_axiom("bad",1d,Rec("op","unsupported","ref",F_dv_scoped_ref("other","o","f","x")),L(Rec("op","unsupported")));var ignored=Adopt(forward,bad);ignored=F_dv_suspend(ignored,"bad");ignored=Adopt(ignored,With(bad,"name","predict-bad"),1,"predict");Reason("inactive and noninfer malformed declaration",ignored,10);
Reason("zero-budget malformed active declaration",Adopt(World(),bad),0);
try{Run(Actor(Adopt(World(),bad),true,1),L(),1);throw new Exception("active unsupported operator accepted");}catch(Exception ex)when(ex.Message.Contains("Unindexed condition operator")){Console.WriteLine("active unsupported operator: explicit error");}
var physical=Put(Put(With(World(true),"time",10d),"clock",10d),"stable",1d);
Clock("reverse interval",physical,Law("threshold",F_dv_at_least(Ref("clock"),15d),"stable"),Ref("clock"),8,8);
Clock("static threshold",physical,Law("threshold",F_dv_at_least(Ref("clock"),15d),"stable"),Ref("clock"),30,15);
Clock("fractional threshold lattice",physical,Law("fraction",F_dv_at_least(Ref("clock"),15.2d),"stable"),Ref("clock"),30,16);
Clock("clock equality conservative minute",physical,Law("eq",F_dv_fact(Ref("clock"),10d),"stable"),Ref("clock"),30,11);
Clock("live-process early return verified",physical,F_dv_axiom("live",1d,F_dv_true(),L(F_dv_increment(Ref("stable"),1d))),Ref("clock"),30,11);
Clock("absent clock",physical,Law("no-clock",F_dv_at_least(Ref("clock"),15d),"stable"),false,30,30);
var wb=Adopt(physical,bad,1,"enforce");wb=F_dv_suspend(wb,"bad");var st=Rec("clock_ref",Ref("clock"),"world_processes",L(Rec("law","bad")),"run_config",Rec("opt_dirty",true,"opt_dirty_verify",1d));Equal("inactive clock declaration",F_dvs_environment_next_time(wb,st,30d),30d);Console.WriteLine("inactive clock malformed declaration: untouched");
}
}

