using DuelView.Compiled;
using System.Security.Cryptography;
using System.Text;
static List<object> L(params object[] v) => new(v);
static object Run(string mode) {
 Environment.SetEnvironmentVariable("DV_OPT_RUNTIMESPEED", mode);
 using var scope = R.UseValues(new R.ValueContext());
 var original = R.snapshot(L(L("nested", L(L("x", R.div(R.exact(1d),3d)), L("y", L("a", "b")))),L("dup", 1d),L("dup", 2d),L("extra", true)));
 var left=R.copy_value(original); var right=R.copy_value(original);
 var nested=R.record_find(left,"nested",false);
 var pair=R.at(nested,0d); R.replace(pair,1d,R.div(R.exact(2d),7d));
 var before = R.snapshot(left); R.push(R.record_find(nested,"y",false),"c");
 var changed=R.snapshot(left);
 if(!R.Bool(R.same_value(original,right))) throw new Exception("COW sibling changed");
 if(R.Bool(R.same_value(before,changed))) throw new Exception("Nested parent invalidation lost");
 // Key edits invalidate record indices, including duplicate last-match behavior.
 for(int i=0;i<10;i++) _=R.record_find(left,"dup",false);
 R.replace(R.at(left,2d),0d,"renamed");
 var lookup=R.record_find(left,"dup",false);
 var keyChanged=R.snapshot(left);
 var current=R.copy_value(original);
 for(int i=0;i<20000;i++) {
   var copy=R.copy_value(current); var n=R.record_find(copy,"nested",false);
   R.replace(R.at(n,0d),1d,R.number((i%17).ToString()));
   current=R.snapshot(copy);
 }
 // Plain external List ABI and shared aliases use the same invalidation path.
 var shared=L("original"); var external=L(shared,shared); var first=R.snapshot(external);
 R.push(shared,"updated"); var second=R.snapshot(external);
 if(R.Bool(R.same_value(first,second))) throw new Exception("External alias invalidation lost");
 if(!R.Bool(R.same_value(lookup,1d))) throw new Exception("Duplicate/key invalidation lost");
 return L(original,right,before,changed,keyChanged,lookup,current,first,second);
}
var off=Run("0");var on=Run("1");
if(!R.Bool(R.same_value(off,on))) throw new Exception("on/off mismatch");
var text=R.Text(R.json_stringify(on));
Console.WriteLine("runtime-speed mechanism parity: identical exact nested values, COW isolation, duplicate keys, parent invalidation, aliases and external List ABI");
Console.WriteLine("sha256="+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))));

