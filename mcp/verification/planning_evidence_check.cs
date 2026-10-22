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
    static void CheckTaskGraph()
    {
        var loc=F_dv_ref("self","state","location");
        var clue=F_dv_ref("self","state","clue");
        var mind=F_dv_add_object(F_dv_world("graph","mind","self"),F_dv_object("self","person",Rec(P("state",Rec(P("location",Cell("a",10d,true)),P("clue",F_dv_unknown()))))));
        mind=F_dv_with(mind,"time",10d);
        var law=F_dv_axiom("a-b",1d,F_dv_fact(loc,"a"),L(Rec(P("op","assign"),P("ref",loc),P("value","b"))));
        mind=F_dv_adopt(mind,law,"predict",1d,L("fixture-route-source"));
        var move=Rec(P("id","a-b"),P("kind","move"),P("precondition",F_dv_fact(loc,"a")),P("predict_law","a-b"),P("travel",Rec(P("from","a"),P("to","b"))),P("repeatable",true));
        var inspect=Rec(P("id","inspect-clue"),P("kind","inspect"),P("precondition",F_dv_fact(loc,"b")),P("purpose_refs",L(clue)),P("repeatable",true));
        var goal=Rec(P("kind","evidence"),P("scope","personal"),P("ref",clue),P("source","perception"),P("minimum_time",0d),P("basis",L("fixture-source")));
        var policy=Rec(P("goals",L(goal)),P("plan_budget",8d),P("dependency_budget",20d),P("plan_depth",3d),P("selective_planning",true),P("time_cost",0.01d));
        var actor=Rec(P("id","self"),P("mind",mind),P("actions",L(move,inspect)),P("location_ref",loc),P("policy",policy));
        var cost=Rec(P("actual",Rec(P("time",1d))),P("maximum",Rec(P("time",1d))));
        var costs=Rec(P("move",cost),P("inspect",cost));
        var choice=F_dvs_choose(actor,L(),costs);
        Require((string)G(choice,"strategy")=="taskgraph-v1","taskgraph is default");
        Require((string)G(G(choice,"action"),"id")=="a-b","taskgraph chooses legal navigation to unknown evidence");
        Require((bool)G(F_dv_read(mind,clue),"known")==false,"navigation never grants unknown inspection result");
        actor=F_dv_with(actor,"taskgraph_task",G(choice,"taskgraph_task"));
        var reuse=F_dvs_choose(actor,L(),costs);
        Require((string)G(reuse,"stop")=="taskgraph-identical-input" && (double)G(reuse,"plan_expansions")==0d,"identical complete input reuses only the prior task");
        var arrived=F_dv_with(F_dv_store(mind,loc,Cell("b",11d,true)),"time",11d);
        var atTarget=F_dv_with(actor,"mind",arrived);
        var terminal=F_dvs_choose(atTarget,L(),costs);
        Require((string)G(G(terminal,"action"),"id")=="inspect-clue","real arrival continues the admitted terminal task");
        var received=F_dv_store(arrived,clue,Cell("seen",12d,true));
        var finished=F_dvs_choose(F_dv_with(atTarget,"mind",received),L(),costs);
        Require(G(finished,"action").Equals(false),"actual task completion removes information reward");
        var blocked=F_dv_with(actor,"mind",F_dv_with(mind,"adoptions",L()));
        Require(G(F_dvs_choose(blocked,L(),costs),"action").Equals(false),"changed personal law dependency invalidates cached navigation");
        var future=F_dv_with(goal,"minimum_time",20d);
        var known=F_dv_store(mind,clue,Cell("seen",10d,true));
        var futureActor=F_dv_with_many(actor,Rec(P("mind",known),P("policy",F_dv_with(policy,"goals",L(future)))));
        Require(G(F_dvs_choose(futureActor,L(),costs),"action").Equals(false),"future receipt requirement cannot create move-inspect task");
        // A personally declared collaboration task persists after real arrival
        // while its method waits for a participant. Only a real new input opens it.
        var done=F_dv_ref("self","state","done");
        var peer=F_dv_ref("self","state","peer-ready");
        var taskMind=F_dv_store(F_dv_store(mind,done,F_dv_field(false)),peer,F_dv_field(false));
        var workPre=F_dv_all(L(F_dv_fact(loc,"b"),F_dv_fact(peer,true)));
        taskMind=F_dv_adopt(taskMind,F_dv_axiom("work",1d,workPre,L(Rec(P("op","assign"),P("ref",done),P("value",true)))),"predict",1d,L("fixture-work-source"));
        var work=Rec(P("id","work"),P("kind","help"),P("precondition",workPre),P("predict_law","work"));
        var taskGoal=Rec(P("kind","achieve"),P("scope","personal"),P("basis",L("fixture-task-acceptance")),P("proposition",F_dv_fact(done,true)),P("navigation_room","b"),P("navigation_methods",L("work")),P("weight",2d));
        var taskActor=F_dv_with_many(actor,Rec(P("mind",taskMind),P("actions",L(move,work)),P("policy",F_dv_with(policy,"goals",L(taskGoal)))));
        var taskCosts=Rec(P("move",cost),P("help",cost));
        var navigate=F_dvs_choose(taskActor,L(),taskCosts);
        Require((string)G(G(navigate,"action"),"id")=="a-b","declared accepted task can navigate before collaborators are present");
        var taskArrived=F_dv_store(taskMind,loc,Cell("b",11d,true));
        taskActor=F_dv_with_many(taskActor,Rec(P("mind",taskArrived),P("taskgraph_task",G(navigate,"taskgraph_task"))));
        var waiting=F_dvs_choose(taskActor,L(),taskCosts);
        Require(G(waiting,"action").Equals(false) && !G(waiting,"taskgraph_task").Equals(false),"arrival retains the accepted task while waiting for real input");
        var urgent=F_dv_ref("self","state","urgent-done");
        var urgentMind=F_dv_store(taskArrived,urgent,F_dv_field(false));
        urgentMind=F_dv_adopt(urgentMind,F_dv_axiom("urgent",1d,F_dv_true(),L(Rec(P("op","assign"),P("ref",urgent),P("value",true)))),"predict",1d,L("fixture-personal-priority"));
        var urgentAction=Rec(P("id","urgent"),P("kind","help"),P("precondition",F_dv_true()),P("predict_law","urgent"));
        var urgentGoal=Rec(P("kind","achieve"),P("scope","personal"),P("basis",L("fixture-higher-owned-value")),P("proposition",F_dv_fact(urgent,true)),P("weight",3d));
        var preemptActor=F_dv_with_many(taskActor,Rec(P("mind",urgentMind),P("actions",L(move,work,urgentAction)),P("policy",F_dv_with(policy,"goals",L(taskGoal,urgentGoal))),P("taskgraph_task",G(waiting,"taskgraph_task"))));
        Require((string)G(G(F_dvs_choose(preemptActor,L(),taskCosts),"action"),"id")=="urgent","higher personally declared value can preempt a retained task");
        taskActor=F_dv_with_many(taskActor,Rec(P("mind",F_dv_store(taskArrived,peer,F_dv_field(true))),P("taskgraph_task",G(waiting,"taskgraph_task"))));
        var perform=F_dvs_choose(taskActor,L(),taskCosts);
        Require((string)G(G(perform,"action"),"id")=="work","actual participant change releases the accepted method");
        Require((bool)G(F_dv_read(G(taskActor,"mind"),done),"value")==false,"taskgraph never commits its predicted task outcome");
        var completedActor=F_dv_with(taskActor,"mind",F_dv_store(G(taskActor,"mind"),done,F_dv_field(true)));
        Require(G(F_dvs_choose(completedActor,L(),taskCosts),"action").Equals(false),"real accepted-task completion clears the task opportunity");
        var legacy=F_dvs_choose(F_dv_with(actor,"policy",F_dv_with(policy,"opt_taskgraph",false)),L(),costs);
        Require((string)G(legacy,"strategy")!="taskgraph-v1","off retains original planner");
    }
    public static void Main()
    {
        foreach(var cache in new[]{"0","1"})
        {
            Environment.SetEnvironmentVariable("DV_OPT_PLANCACHE",cache);
            using var values = R.UseValues(new R.ValueContext());
            var reference=F_dv_ref("self","state","location");
            var mind=F_dv_add_object(F_dv_world("fixture","mind","self"),F_dv_object("self","person",Rec(P("state",Rec(P("location",Cell("a",10d,true)))))));
            var law=F_dv_axiom("move",1d,F_dv_true(),L(Rec(P("op","assign"),P("ref",reference),P("value","b"))));
            mind=F_dv_with(F_dv_adopt(mind,law,"predict",1d,L("fixture")),"time",10d);
            var goal=Rec(P("kind","evidence"),P("scope","personal"),P("basis",L("fixture")),P("ref",reference),P("source","perception"),P("minimum_time",10d));
            var actor=Rec(P("id","self"),P("mind",mind),P("policy",Rec(P("goals",L(goal)))));
            var inspect=Rec(P("id","inspect-self"),P("kind","inspect"),P("purpose_refs",L(reference)),P("grants",L(reference)),P("precondition",F_dv_true()));
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
            var futureActor=F_dv_with(actor,"policy",Rec(P("goals",L(freshGoal))));
            Require(Info(futureActor,inspect,reference)==0d,"future minimum_time cannot reward current inspection");
            var beforeSignature=F_dvp_inspection_signature(futureActor,inspect,mind);
            var dueMind=F_dv_with(mind,"time",20d);
            var freshActor=F_dv_with(futureActor,"mind",dueMind);
            Require(!(bool)F_dv_same(beforeSignature,F_dvp_inspection_signature(freshActor,inspect,dueMind)),"real due boundary releases the inspection guard");
            Require(Info(freshActor,inspect,reference)==1d,"real freshness requirement invalidates older evidence");
            var received=F_dv_store(dueMind,reference,Cell("b",20d,true));
            Require(Info(F_dv_with(freshActor,"mind",received),inspect,reference)==0d,"new real perception satisfies freshness again");
            Require(!(bool)F_dv_has_key(actor,"planning_evidence_mind"),"prediction view does not mutate real actor");
            CheckTaskGraph();
            Console.WriteLine($"cache={cache}: evidence origin, future boundary, taskgraph navigation/commitment/cache/knowledge invalidation confirmed");
        }
    }
}
