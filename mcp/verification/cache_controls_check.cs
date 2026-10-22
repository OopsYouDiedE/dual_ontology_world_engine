using DuelView.Compiled;
using static DuelView.Core.DVKernel;

static class CacheControlsCheck
{
    static List<object> L(params object[] values) => new(values);
    static object Rec(params object[] pairs) => L(pairs);
    static object Pair(string key, object value) => L(key, value);
    static bool Hit(object value) => (bool)R.at(value, 0d);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static bool Same(object left, object right) => R.json_stringify(left).Equals(R.json_stringify(right));
    static object Get(object value, string key) => F_dv_get(value, key);
    static object Without(object value, string key) => F_dv_without(value, key);
    static object CheckCase(bool plan, bool declaration)
    {
        Environment.SetEnvironmentVariable("DV_OPT_PLANCACHE", plan ? "1" : "0");
        Environment.SetEnvironmentVariable("DV_OPT_DECLCACHE", declaration ? "1" : "0");
        using var scope = R.UseValues(new R.ValueContext());
        var owner = R.snapshot(L("owner"));
        var key = L("original-key", "complete-input");
        F_dvs_plan_cache_put(owner, key, "plan-result");
        Require(Hit(R.memo_get(owner,key)) == plan, "plan disabled must not write");
        Require(Hit(F_dvs_plan_cache_get(owner,key)) == plan, "plan hit control");
        var declaredKey = L("original-declaration-key");
        F_dvs_declaration_cache_put(owner,declaredKey,"declaration-result");
        Require(Hit(R.memo_get(owner,declaredKey)) == declaration,"declaration disabled must not write");
        Require(Hit(F_dvs_declaration_cache_get(owner,declaredKey)) == declaration,"declaration hit control");
        // A preexisting entry must also be ignored when the group is off.
        R.memo_put(owner,L("prepopulated-plan"),"old");
        Require(Hit(F_dvs_plan_cache_get(owner,L("prepopulated-plan"))) == plan,"plan disabled must not read old entry");
        R.memo_put(owner,L("prepopulated-declaration"),"old");
        Require(Hit(F_dvs_declaration_cache_get(owner,L("prepopulated-declaration"))) == declaration,"declaration disabled must not read old entry");
        R.memo_put(owner,L("other-independent-group"),"retained");
        Require(Hit(R.memo_get(owner,L("other-independent-group"))),"unrelated memo remains enabled");
        var mind = F_dv_world("mind-fixture","mind","fixture");
        var law = F_dv_axiom("predict-fixture",1d,F_dv_true(),L());
        mind = F_dv_adopt(mind,law,"predict",1d,L("fixture-source"));
        var first = F_dvp_forecast(mind,"predict-fixture");
        var second = F_dvp_forecast(mind,"predict-fixture");
        Require(!(bool)Get(first,"planning_cache_hit"),"first forecast miss");
        Require((bool)Get(second,"planning_cache_hit") == plan,"second forecast hit follows control");
        Require(Same(Without(first,"planning_cache_hit"),Without(second,"planning_cache_hit")),"forecast repeated computation parity");
        var actor = Rec(Pair("id","fixture"),Pair("mind",mind),Pair("actions",L()),Pair("policy",Rec(Pair("opt_plancache",true))));
        var reference = F_dv_ref("missing","facet","field");
        var deps = F_dvp_cached_dependencies(actor,L(reference),4d);
        actor = F_dv_with(actor,"dependency_cache",Get(deps,"cache"));
        var deps2 = F_dvp_cached_dependencies(actor,L(reference),4d);
        Require((bool)Get(deps2,"cache_hit") == plan,"stored dependency result follows control");
        Require(Same(Get(deps,"refs"),Get(deps2,"refs")),"dependency refs parity");
        Require(Same(Get(deps,"stop"),Get(deps2,"stop")),"dependency stop parity");
        if (!plan) Require(Get(deps2,"cache").Equals(false),"disabled dependency result not saved");
        var methodKey = L("dvp_dependency_methods");
        Require(Hit(R.memo_get(Get(actor,"actions"),methodKey)) == declaration,"method declaration index follows independent control");
        return Rec(Pair("forecast",Without(first,"planning_cache_hit")),Pair("dependency_refs",Get(deps,"refs")),Pair("dependency_stop",Get(deps,"stop")));
    }
    public static void Main()
    {
        foreach (var id in new[]{"detective","huo","shen","su","quer","ward","gu","lucian","demon","resident-fixture"})
        {
            var actor = Rec(Pair("id",id),Pair("policy",Rec(Pair("plan_depth",4d),Pair("wait_chunk_limit",60d))),Pair("mind",Rec(Pair("time",30d))));
            Require(ReferenceEquals(actor,DVProgram.F_c1n_wait_profile(actor)),"first-night refresh must preserve actor, depth and waiting policy for " + id);
        }
        Console.WriteLine("first-night refresh preserves actor, depth and waiting policy across named and unknown identities");
        object? baseline = null;
        foreach (var plan in new[]{false,true}) foreach (var declaration in new[]{false,true})
        {
            var value = CheckCase(plan,declaration);
            baseline ??= value;
            Require(Same(value,baseline),"all switch combinations preserve forecast and dependency results");
            Console.WriteLine($"plan={plan}, declaration={declaration}: cache reads/writes, native memo independence, forecast and dependency parity confirmed");
        }
    }
}
