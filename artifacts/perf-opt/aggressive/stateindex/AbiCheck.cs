using DuelView.Compiled;
using System.Diagnostics;
using L = System.Collections.Generic.List<object>;
static L Pair(string k, object v) => new(){k,v};
static L Rec(params object[] p) => new(p);
static object Ref(string id, string field) => Rec(Pair("object",id),Pair("facet","state"),Pair("field",field));
static object World(int count) { var os=new L(); for(int i=0;i<count;i++) os.Add(Pair("o"+i,Rec(Pair("facets",Rec(Pair("state",Rec(Pair("x", new Rational(1,3))))))))); return Rec(Pair("objects",os)); }
static object Run(bool on, object source, int n) {
 Environment.SetEnvironmentVariable("DV_OPT_STATEINDEX",on?"1":"0"); using var scope=R.UseValues(new R.ValueContext());
 var owned=R.copy_value(source); for(int i=0;i<n;i++) {var result=R.stage_store_cow(owned,Ref("o"+(i%128),"x"),new Rational(i+2,7)); if(result is false) throw new Exception("unsupported");}
 return R.snapshot(owned);
}
var source=R.snapshot(World(128)); var before=R.json_stringify(source); var a=Run(true,source,512); var b=Run(false,source,512);
if(!R.Bool(R.same_value(a,b)) || !Equals(before,R.json_stringify(source))) throw new Exception("parity/source changed");
// Mutating a thawed result must invalidate the copied index and never its sibling.
Environment.SetEnvironmentVariable("DV_OPT_STATEINDEX","1"); using(var scope=R.UseValues(new R.ValueContext())) {
 var owned=R.copy_value(source); R.stage_store_cow(owned,Ref("o0","x"),new Rational(11,17)); var sealedValue=R.snapshot(owned); var sibling=R.copy_value(owned);
 var objects=R.record_find(owned,"objects",false); var pair=R.at(objects,0d); R.replace(pair,0d,"renamed");
 R.stage_store_cow(owned,Ref("renamed","missing"),new Rational(19,23));
 if(!R.Bool(R.same_value(sealedValue,sibling)) || R.Bool(R.same_value(owned,sibling))) throw new Exception("alias/key invalidation");
}
// Duplicate object identities use old all-matching update behavior.
var dup=World(1); var dos=R.record_find(dup,"objects",false); R.push(dos,R.copy_value(R.at(dos,0d))); var da=Run(true,R.snapshot(dup),1); var db=Run(false,R.snapshot(dup),1);
if(!R.Bool(R.same_value(da,db))) throw new Exception("duplicate fallback, opaque unknown cells, malformed/absent fallback");
// Opaque unknown cells and nested values retain every field, without numeric conversion.
foreach(var enabled in new[]{false,true}) {
 Environment.SetEnvironmentVariable("DV_OPT_STATEINDEX",enabled?"1":"0"); using var scope=R.UseValues(new R.ValueContext());
 var owned=R.copy_value(source); var cell=Rec(Pair("known",false),Pair("value",new Rational(37,101)),Pair("extra",Rec(Pair("nested","kept"))));
 R.stage_store_cow(owned,Ref("o0","new_unknown"),cell); var stable=R.snapshot(owned); var stableText=R.show(stable);
 R.replace(cell,0d,Pair("known",true)); if(!Equals(stableText,R.show(stable))) throw new Exception("cell alias");
 if(R.stage_store_cow(owned,Ref("absent","x"),false) is not false) throw new Exception("unknown object admission");
 var malformed=Rec(Pair("objects",Rec(new L(){"bad"}))); if(R.stage_store_cow(malformed,Ref("bad","x"),false) is not false) throw new Exception("malformed admission");
}
Console.WriteLine("parity: exact fractions, inserted fields, 512 repeated updates, immutable source, sibling isolation, mutable key invalidation, duplicate fallback, opaque unknown cells, malformed/absent fallback");
foreach(var on in new[]{false,true}) { var sw=Stopwatch.StartNew(); for(int i=0;i<10;i++) Run(on,source,2048); sw.Stop(); Console.WriteLine($"stateindex={on} micro_workload_ms={sw.Elapsed.TotalMilliseconds:F1}"); }
