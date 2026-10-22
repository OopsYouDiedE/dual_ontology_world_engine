using DuelView.Compiled;
using static DVProgram;
class SubjectIsolation {
static object L(params object[] a)=>new System.Collections.Generic.List<object>(a);
static object Rec(params object[] a){var r=new System.Collections.Generic.List<object>();for(int i=0;i<a.Length;i+=2)r.Add(L(a[i],a[i+1]));return r;}
static object With(object a,string k,object v)=>R.record_with(a,k,v);
static object Actor(object adoptions,string subject)=>Rec("id",subject,"mind",With(F_dv_world("w", "mind", subject),"adoptions",adoptions),"inference_signatures",L());
static object SharedAdoptions(object reference){var law=F_dv_axiom("r",1d,F_dv_fact(reference,1d),L(F_dv_assign(reference,2d)));return R.snapshot(Rec("r",Rec("law",law,"active",true,"mode","infer","basis",L("basis"),"confidence",1d)));}
static void Main(){
var shared=SharedAdoptions(F_dv_ref("o","f","x"));var a=Actor(shared,"alpha");var b=Actor(shared,"beta");
var ia=F_dvs_dirty_reason_index(a);var ib=F_dvs_dirty_reason_index(b);
// Snapshot interning may share structurally equal index values; queue state is subject-keyed.
if(!R.Bool(R.same_value(ia,F_dvs_dirty_reason_index(a)))||!R.Bool(R.same_value(ib,F_dvs_dirty_reason_index(b))))throw new System.Exception("Same subject changed index value");
var ma=R.record_find(a,"mind",false);var mb=R.record_find(b,"mind",false);
if(R.Num(R.dirty_next(ma,ia,-1d))!=0||R.Num(R.dirty_next(mb,ib,-1d))!=0)throw new System.Exception("Subject lost initial candidate");
if(R.Num(R.dirty_next(ma,ia,-1d))!=1||R.Num(R.dirty_next(mb,ib,-1d))!=1)throw new System.Exception("Consumed candidate repeated");
System.Console.WriteLine("Shared frozen adoptions, different unscoped subjects: stable indices and independent subject queues.");
var scoped=SharedAdoptions(F_dv_scoped_ref("alpha","o","f","x"));F_dvs_dirty_reason_index(Actor(scoped,"alpha"));
try{F_dvs_dirty_reason_index(Actor(scoped,"beta"));throw new System.Exception("Cross-subject index bypassed validation");}
catch(System.Exception error)when(error.Message.Contains("Cross-subject reference access")){System.Console.WriteLine("Cached alpha-scoped adoption rejects beta subject instead of bypassing access check.");}
}
}
