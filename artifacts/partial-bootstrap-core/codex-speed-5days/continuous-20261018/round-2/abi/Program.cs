using DuelView.Compiled;
using L = System.Collections.Generic.List<object>;
static void Need(bool x, string why) { if (!x) throw new Exception(why); }
var child = new L { 1d };
var source = new L { new L { "a", child }, new L { "b", new L { 2d } }, new L { "a", new L { 3d } } };
var updates = new L { new L { "a", new L { 4d } }, new L { "c", new L { 5d } }, new L { "a", new L { 6d } } };
var changed = R.record_with_many(source, updates);
Need(R.Num(R.count(changed)) == 4, "duplicate key order");
Need(R.Num(R.at(R.record_find(changed,"a",false),0d))==6, "last update wins");
var retained = R.snapshot(source);
R.replace(R.record_find(changed,"b",false),0d,99d);
Need(R.Num(R.at(R.record_find(source,"b",false),0d))==2, "output must not mutate source");
R.replace(R.record_find(updates,"a",false),0d,88d);
Need(R.Num(R.at(R.record_find(changed,"a",false),0d))==6, "updates must not alias output");
R.replace(child,0d,77d);
Need(R.Num(R.at(R.at(R.at(retained,0d),1d),0d))==1, "snapshot must retain old value");
Need(ReferenceEquals(source,R.record_with_many(source,new L())),"empty update identity");
Console.WriteLine("Ordered duplicate-key updates, source/output/update independence, frozen evidence and empty identity checked on neutral containers.");

var lazyUpdates=R.copy_value(R.snapshot(updates));
var fromLazy=R.record_with_many(source,lazyUpdates);
Need(R.Num(R.at(R.record_find(fromLazy,"a",false),0d))==88,"lazy updates enumeration");
var frozenUpdates=R.snapshot(updates);
Need(R.Num(R.count(R.record_with_many(source,frozenUpdates)))==4,"frozen updates enumeration");
Need(R.Bool(R.has_value(new L {new L {1d}},new L {R.exact(1d)})),"nested exact numeric membership");
Need(!R.Bool(R.has_value(new L {new L {1d}},new L {2d})),"nested inequality");
Console.WriteLine("Lazy/frozen update tables and nested membership preserve value and copy semantics.");
