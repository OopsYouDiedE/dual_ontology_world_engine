using DuelView.Compiled;
using static DuelView.Core.DVKernel;
class Probe {
static object L(params object[] a)=>new List<object>(a);
static object Rec(params object[] a){var r=new List<object>();for(int i=0;i<a.Length;i+=2)r.Add(L(a[i],a[i+1]));return r;}
static object Get(object a,string k)=>R.record_find(a,k,false);
static object With(object a,string k,object v)=>R.record_with(a,k,v);
static object Ref(string f)=>F_dv_ref("o","state",f);
static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine(text);}
static object Put(object m,string k,object v)=>F_dv_store(m,Ref(k),F_dv_cell(v,1d,0d,L()));
static object Actor(object m,double budget=320)=>Rec("id","s","mind",R.snapshot(m),"policy",Rec("model_budget",budget,"model_work_remaining",budget,"Replay_Depth",64d),"response_models",L());
static object Run(object actor,object hypothesis,object events)=>F_dvs_model_probe(actor,hypothesis,events,0d,"paid");
static void Main(){
var m=F_dv_add_object(F_dv_world("probe","mind","s"),F_dv_object("o","perceived",L()));m=Put(Put(m,"x",true),"observed",true);
var law=F_dv_axiom("p",1d,F_dv_fact(Ref("x"),true),L(F_dv_assign(Ref("y"),true)));m=F_dv_adopt(m,law,"predict",0.5,L("source"));
var h=Rec("id","first","timeline",L(Rec("at",0d,"laws",L("p"),"optional",false)),"assumptions",L(),"features",L(Rec("proposition",F_dv_fact(Ref("y"),true),"observation",F_dv_fact(Ref("observed"),true))),"prior",0.5,"basis",L("source"),"start_time",0d);
var events=L();var a=Actor(m);var first=Run(a,h,events);var fr=Get(first,"result");Check(R.Bool(Get(fr,"plan_complete"))&&Equals(Get(fr,"status"),"explained"),"first probe is a complete matching prediction");
a=With(Actor(m,318),"model_probe_cache",Get(first,"cache"));var second=Run(a,With(h,"id","alternative"),events);var sr=Get(second,"result");Check(R.Num(Get(sr,"expanded"))==0&&R.Num(Get(sr,"reused_work"))==R.Num(Get(fr,"expanded")),"equivalent alternative reuses actual work without a second charge");Check(R.Num(Get(sr,"remaining"))==318&&R.Bool(R.same_value(Get(fr,"predicted"),Get(sr,"predicted"))),"reused result preserves source prediction and current budget");Check(!Equals(Get(sr,"reused_from_event"),false),"reuse retains its original event reference");
var changed=With(Actor(Put(m,"x",false)),"model_probe_cache",Get(first,"cache"));var cr=Get(Run(changed,h,events),"result");Check(R.Num(Get(cr,"expanded"))>0&&Equals(Get(cr,"reused_from_event"),false),"changed own input cannot reuse a prior prediction");
var limited=With(Actor(m,1),"model_probe_cache",Get(first,"cache"));var lim=Run(limited,h,events);Check(R.Num(Get(Get(lim,"result"),"expanded"))>0,"insufficient current work allowance cannot import a larger cached calculation");Check(((List<object>)Get(lim,"cache")).Count==1,"exhausted comparison is not cached as a completed probe");
var other=With(a,"id","another-owner");Check(R.Num(Get(Get(Run(other,h,events),"result"),"expanded"))>0,"different owner cannot reuse a prediction");
}
}
