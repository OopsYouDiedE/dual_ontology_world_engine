using DuelView.Compiled;
using static DuelView.Core.DVKernel;

static class PlanningEvidenceCheck
{
    static List<object> L(params object[] v) => new(v);
    static object P(string k, object v) => L(k,v);
    static object Rec(params object[] p) => L(p);
    static object G(object v,string k) => F_dv_get(v,k);
    static void Require(bool v,string m) { if (!v) throw new Exception(m); }
    static object Cell(string value,double time,bool received) => F_dv_with(F_dv_cell(value,1d,time,L("fixture")),"claims",received ? L(Rec(P("source","perception"),P("value",value),P("time",time))) : L());
    static double Info(object actor,object action,object reference) => (double)G(F_dvp_action_value(actor,action,L(),L(reference),L()),"information");
    public static void Main()
    {
        foreach(var cache in new[]{"0","1"})
        {
            Environment.SetEnvironmentVariable("DV_OPT_PLANCACHE",cache);
            using var values = R.UseValues(new R.ValueContext());
            var reference=F_dv_ref("self","state","location");
            var mind=F_dv_add_object(F_dv_world("fixture","mind","self"),F_dv_object("self","person",Rec(P("state",Rec(P("location",Cell("a",10d,true)))))));
            var law=F_dv_axiom("move",1d,F_dv_true(),L(Rec(P("op","assign"),P("ref",reference),P("value","b"))));
            mind=F_dv_adopt(mind,law,"predict",1d,L("fixture"));
            var goal=Rec(P("kind","evidence"),P("scope","personal"),P("basis",L("fixture")),P("ref",reference),P("source","perception"),P("minimum_time",10d));
            var actor=Rec(P("id","self"),P("mind",mind),P("policy",Rec(P("goals",L(goal)))));
            var inspect=Rec(P("id","inspect-self"),P("kind","inspect"),P("purpose_refs",L(reference)),P("precondition",F_dv_true()));
            Require(Info(actor,inspect,reference)==0d,"received root evidence has no information reward");
            var predicted=G(F_dvp_forecast(mind,"move"),"world");
            Require(!(bool)F_dvp_source_supported_since(F_dv_read(predicted,reference),"perception",10d),"fixture actually replaces perception claims");
            var view=F_dvp_actor_view(actor,predicted);
            Require(Info(F_dv_without(view,"planning_evidence_mind"),inspect,reference)==1d,"legacy node-local scoring reproduces the false reward");
            Require(Info(view,inspect,reference)==0d,"forecast movement must not manufacture self-inspection information");
            var move=Rec(P("id","move"),P("kind","move"),P("precondition",F_dv_true()),P("predict_law","move"),P("repeatable",true));
            var cost=Rec(P("actual",Rec(P("time",1d))),P("maximum",Rec(P("time",1d))));
            var routeActor=F_dv_with_many(actor,Rec(P("actions",L(move,inspect)),P("planned_path",L("move","inspect-self"))));
            var route=F_dvp_selective_followup(routeActor,L(),Rec(P("move",cost),P("inspect",cost)),L(reference),L(),3d,10d);
            Require(!(bool)G(route,"matched") && (string)G(route,"reason")=="committed-task-no-longer-valuable","route revalidation rejects the false move-inspect loop");
            var second=F_dvp_actor_view(view,G(F_dvp_forecast(predicted,"move"),"world"));
            Require(Info(second,inspect,reference)==0d,"descendant forecasts retain the real evidence origin");
            var unknown=F_dv_store(mind,reference,F_dv_unknown());
            var unknownActor=F_dv_with(actor,"mind",unknown);
            Require(Info(unknownActor,inspect,reference)==1d,"unknown real input remains valuable to inspect");
            var changed=F_dv_store(mind,reference,Cell("b",20d,false));
            Require(Info(F_dv_with(actor,"mind",changed),inspect,reference)==1d,"real changed input without perception invalidates evidence");
            var freshGoal=F_dv_with(goal,"minimum_time",20d);
            var freshActor=F_dv_with(actor,"policy",Rec(P("goals",L(freshGoal))));
            Require(Info(freshActor,inspect,reference)==1d,"real freshness requirement invalidates older evidence");
            var received=F_dv_store(mind,reference,Cell("b",20d,true));
            Require(Info(F_dv_with(freshActor,"mind",received),inspect,reference)==0d,"new real perception satisfies freshness again");
            Require(!(bool)F_dv_has_key(actor,"planning_evidence_mind"),"prediction view does not mutate real actor");
            Console.WriteLine($"cache={cache}: received/unknown evidence, forecast descendants, real change and freshness confirmed");
        }
    }
}
