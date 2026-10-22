using DuelView.Compiled;
static List<object> L(params object[] v) => new(v);
static object F(int a,int b) => R.div(R.exact((double)a),(double)b);
static void Require(bool condition,string text) { if(!condition) throw new Exception(text); }
static object Run(string mode,string output) {
 Environment.SetEnvironmentVariable("DV_OPT_RUNTIMESPEED",mode);
 using var scope=R.UseValues(new R.ValueContext());
 var shared=L(F(1,3),F(2,7),"original",true);
 var source=R.snapshot(L(L("left",shared),L("right",shared),L("id","e1"),L("exact",F(19,23))));
 var owner=R.copy_value(source);
 Require(owner is List<object>,"List ABI lost");
 Require((owner.GetType()==typeof(List<object>))==(mode=="0"),"on/off runtime root mismatch");
 var before=R.snapshot(owner);
 R.raw_open(Path.Combine(output,mode+".dvdag"));
 R.raw_checkpoint("initial",owner); R.raw_event(owner);
 var left=R.record_find(owner,"left",false);
 R.push(left,F(29,31));
 Require(!R.Bool(R.same_value(before,R.snapshot(owner))),"parent invalidation lost");
 Require(R.Bool(R.same_value(R.record_find(owner,"right",false),shared)),"sibling COW isolation lost");
 // A plain external list deliberately contains the very same mutable owner twice.
 var aliases=L(owner,owner); var oldAliases=R.snapshot(aliases);
 R.push(left,"second parent update");
 Require(!R.Bool(R.same_value(oldAliases,R.snapshot(aliases))),"shared external parent invalidation lost");
 var event2=R.copy_value(owner);
 R.replace(R.at(event2,2d),1d,"e2");
 R.raw_event(event2);
 var final=R.copy_value(R.snapshot(L(before,owner,aliases,oldAliases,event2,F(37,41))));
 R.raw_finish(final);
 var save=Path.Combine(output,mode+".dvsave");
 R.WriteEvidenceFile(save,final);
 var restored=R.ReadEvidenceFile(save);
 Require(R.Bool(R.same_value(final,restored)),"full exact value roundtrip mismatch");
 var restoredOwner=R.copy_value(restored);
 R.WriteEvidenceFile(Path.Combine(output,mode+"-roundtrip.dvsave"),restoredOwner);
 Require(R.Bool(R.same_value(final,R.ReadEvidenceFile(Path.Combine(output,mode+"-roundtrip.dvsave")))),"restored subtype write mismatch");
 return final;
}
Directory.CreateDirectory(args[0]);
var off=Run("0",args[0]); var on=Run("1",args[0]);
Require(R.Bool(R.same_value(off,on)),"on/off complete evidence value mismatch");
Console.WriteLine("exact complete values and C# sealed evidence roundtrips identical; owned subtype, shared aliases, nested parent updates and COW siblings verified");
