// @mechanism: causal_clock_and_campaign
// @layer: core/session_cli
// @contracts: VerificationSuites

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Numerics;
using DuelView.Dv;
using DuelView.Core;
using DuelView.Compiled;
using DuelView.Runtime;
using DuelView.Spatial;
using DuelView.Autonomy;
using static DuelView.Core.DVKernel;
using static DuelView.Compiled.R;


// --- Source: DriveCalculatorChecks.cs ---

internal static class DriveCalculatorChecks
{
    private static JsonElement J(object value)=>JsonSerializer.SerializeToElement(value);
    private static void Expect(bool value) {if(!value)throw new InvalidOperationException("Assertion failed.");}
    private static bool Close(double a,double b)=>Math.Abs(a-b)<1e-7;
    private static JsonElement Profile(double hunger=20,double energy=80,double hungerWeight=1,double energyWeight=1,double decay=0)=>J(new {
        commodity_needs=new{hunger,energy},need_decay_per_second=new{hunger=decay,energy=decay},
        autonomy_weights=new{hunger=hungerWeight,energy=energyWeight},time_cost=.01,
        action_predictions=new{meal=new{hunger=50},rest=new{energy=50}},
        completed_action_effects=new{meal=new{hunger=50},rest=new{energy=50}}
    });
    private static ActionCandidate Action(string id,string kind,double duration=20,bool host=true,bool work=true,string[]? resources=null,object? predicted=null,object[]? effects=null)=>
        new(id,new Condition.Constant(Truth.True),duration,0,J(new {
            kind,requires_host_completion=host,timing=new{work_after_arrival=host&&work,host_elapsed=host&&!work},
            resource_reservations=resources??[],perceived_need_effects=predicted,
            effects=effects??[]
        }));
    private static JsonElement Scheduling(int limit=8)=>J(new {decision_interval_seconds=90,actor_stagger_seconds=5,
        candidates_per_decision=limit,evaluations_per_slice=4,milliseconds_per_slice=100,content_time_unit_seconds=1,
        drives_per_decision=2,candidates_per_drive=2,minimum_action_utility=0});
    private static JsonElement ClockPolicy(double max=5)=>J(new {active_game_seconds_per_second=1,idle_game_seconds_per_second=1,work_game_seconds_per_second=4,maximum_game_seconds_per_slice=max});
    private static JsonElement Actor(string id,JsonElement profile)=>J(new {id,mind=new{subject=id,objects=new{},adoptions=new{}},policy=new{goals=Array.Empty<object>()},autonomy=profile});
    private static RuntimeSession Session(ActionCandidate[] actions,bool auto=false,JsonElement? profile=null)
    {
        var session=new RuntimeSession(J(new{objects=new{}}),J(new{inference_rules_per_slice=8,content_time_unit_seconds=1,observation_kinds=Array.Empty<string>()}),
            Scheduling(),ClockPolicy(),J(new{inactive_retention_seconds=60,room_requests_per_slice=2,retry_delay_seconds=1,inactive_simulation="core_path_distance"}),0);
        session.RegisterActor(Actor("subject",profile??Profile()),J(new{rules=Array.Empty<object>()}),actions,auto);
        return session;
    }
    public static int Run()
    {
        var failed=0;
        void Check(string name,Action test)
        {
            var watch=Stopwatch.StartNew();
            try {test();Console.WriteLine($"PASS {name} {watch.Elapsed.TotalMilliseconds:F3} ms");}
            catch(FileNotFoundException ex){Console.WriteLine($"SKIP {name} (missing fixture: {Path.GetFileName(ex.FileName)})");}
            catch(Exception error){failed++;Console.WriteLine($"FAIL {name}: {error}");}
        }
        Check("drive choice changes with DV need levels and values",()=>{
            var meal=Action("meal","meal");var rest=Action("rest","rest");
            var hungry=new DriveCalculator(Profile(),0);var tired=new DriveCalculator(Profile(80,20),0);
            Expect(hungry.Score(meal,0)>hungry.Score(rest,0));Expect(tired.Score(rest,0)>tired.Score(meal,0));
            var duty=new DriveCalculator(Profile(20,20,.1,4),0);Expect(duty.Score(rest,0)>duty.Score(meal,0));
        });
        Check("same-kind actions take their own declared predictions",()=>{
            var drives=new DriveCalculator(Profile(),0);
            Expect(drives.Score(Action("small","meal",predicted:new{hunger=5}),0)<drives.Score(Action("large","meal",predicted:new{hunger=50}),0));
            var score=drives.Evaluate(Action("large","meal",predicted:new{hunger=50}),0);
            Expect(Close(score.Score,score.BaseBias+score.NeedContributions.Values.Sum()-score.TimeCost));
        });
        Check("preview has no reward and DV reconfigure preserves live needs",()=>{
            var drives=new DriveCalculator(Profile(decay:1),0);var before=drives.Snapshot(10);
            for(var n=0;n<20;n++)drives.Evaluate(Action("meal","meal"),10);
            Expect(Close(drives.Snapshot(10)["hunger"],before["hunger"]));
            drives.Reconfigure(Profile(99,99,3,2,1),10);
            Expect(Close(drives.Snapshot(10)["hunger"],10)&&Close(drives.Snapshot(10)["energy"],70));
        });
        Check("large catalogs use a constant candidate budget without freezing time",()=>{
            var actions=Enumerable.Range(0,1000).Select(n=>Action("option-"+n,"meal",duration:1000)).ToArray();
            var session=Session(actions,true);
            for(var n=0;n<8;n++)session.Step(1,false,false,"subject");
            Expect(session.DecisionEvaluations==8&&session.TryAction(out _));
            Expect(Close(session.Clock.Now,8));
        });
        Check("stagger does not move initial need time into the future",()=>{
            var session=Session([]);var scheduler=new DecisionScheduler(Scheduling());
            var a=session.RegisterActor(Actor("second",Profile(decay:1)),J(new{rules=Array.Empty<object>()}),[],false);
            var b=session.RegisterActor(Actor("third",Profile(decay:1)),J(new{rules=Array.Empty<object>()}),[],false);
            scheduler.Register(a,[],0);scheduler.Register(b,[],0);
            Expect(Close(scheduler.Needs("second",10)["hunger"],10)&&Close(scheduler.Needs("third",10)["hunger"],10));
        });
        Check("clock uses normal movement fourfold work and normal idle",()=>{
            var clock=new SimulationClock(ClockPolicy(100),0);
            clock.Step(1,false,false,false,double.PositiveInfinity);Expect(Close(clock.Now,1));
            clock.Step(1,false,false,false,double.PositiveInfinity,true);Expect(Close(clock.Now,5));
            clock.Step(1,true,false,false,double.PositiveInfinity);Expect(Close(clock.Now,6));
            clock.Step(10,false,true,false,double.PositiveInfinity,true);Expect(Close(clock.Now,6));
        });
        Check("dialogue preserves current world speed and explicit pause",()=>{
            foreach(var working in new[]{false,true}) {
                var clock=new SimulationClock(ClockPolicy(100),0);
                clock.BeginDialogue(60);
                clock.Step(1,false,false,false,double.PositiveInfinity,working);
                Expect(Close(clock.Now,working?4:1));
                var before=clock.Now;
                clock.Step(1,false,true,false,double.PositiveInfinity,working);
                Expect(Close(clock.Now,before));
            }
        });
        Check("clock preserves elapsed time across slices events and pending work",()=>{
            var clock=new SimulationClock(ClockPolicy(2),0);
            clock.Step(3,false,false,false,1,true);Expect(Close(clock.Now,1)&&Close(clock.PendingSeconds,11));
            for(var n=0;n<6;n++)clock.Step(0,false,false,false,double.PositiveInfinity);
            Expect(Close(clock.Now,12)&&Close(clock.PendingSeconds,0));
            clock.Step(1,false,false,true,double.PositiveInfinity,true);Expect(Close(clock.Now,12));
            clock.Step(0,false,false,false,double.PositiveInfinity);clock.Step(0,false,false,false,double.PositiveInfinity);
            Expect(Close(clock.Now,16)&&Close(clock.PendingSeconds,0));
        });
        Check("standing leases acquire atomically and survive same-actor handover",()=>{
            var slots=new SlotReservations();slots.Define("a",1);slots.Define("b",1);
            Expect(slots.TryAcquire(new("other",1),["b"]));Expect(!slots.TryAcquire(new("subject",2),["a","b"])&&slots.Occupants("a")==0);
            Expect(slots.TryAcquire(new("subject",2),["a"]));slots.Retain(new("subject",2));
            Expect(slots.TryAcquire(new("subject",3),["a"])&&slots.Occupants("a")==1);
            slots.Release(new("subject",0));Expect(slots.Occupants("a")==1);
            slots.Release(new("subject",3));Expect(slots.Occupants("a")==0);
            var independent=new SlotReservations();independent.Define("b",1);Expect(independent.TryAcquire(new("third",1),["b"]));
        });
        Check("work duration starts after actual arrival and completes once",()=>{
            var session=Session([Action("work","meal",20,resources:["standing"])]);
            session.ConfigureReservations(J(new[]{new{id="standing",capacity=1}}));
            var decision=session.Submit("subject","work");Expect(session.TryAction(out _));
            for(var n=0;n<25;n++)session.Step(4,false,false,"subject");
            Expect(Close(session.Clock.Now,100)&&!session.Working("subject")&&!session.HostCompleted(decision));
            Expect(session.BeginWork(decision)&&!session.BeginWork(decision));
            Expect(Close(session.Activity("subject")!.WorkEndsAt!.Value,120));
            Expect(session.HostCompleted(decision)&&session.HostCompleted(decision));
            session.Step(4,false,false,"subject");Expect(Close(session.Clock.Now,105));
            for(var n=0;n<3;n++)session.Step(0,false,false,"subject");
            Expect(Close(session.Clock.Now,116)&&!session.TryReceipt(out _));
            session.Step(1,false,false,"subject");session.Step(0,false,false,"subject");
            Expect(session.TryReceipt(out var receipt)&&receipt!.Committed&&!session.TryReceipt(out _));
            Expect(session.Reservations.Occupants("standing")==0&&!session.HostCompleted(decision));
        });
        Check("movement completion uses actual physical elapsed and normal clock",()=>{
            var session=Session([Action("move","travel",100,work:false)]);var decision=session.Submit("subject","move");session.TryAction(out _);
            session.Step(4,false,false,"subject");Expect(Close(session.Clock.Now,4)&&!session.Working("subject"));
            Expect(session.HostCompleted(decision));session.Step(0,false,false,"subject");
            Expect(session.TryReceipt(out var receipt)&&receipt!.Committed&&Close(receipt.Time,4));
        });
        Check("cancellation waits for native safe exit and rejects stale completion",()=>{
            var session=Session([Action("work","meal",20,resources:["standing"])]);session.ConfigureReservations(J(new[]{new{id="standing",capacity=1}}));
            var decision=session.Submit("subject","work");session.TryAction(out _);session.BeginWork(decision);session.HostCompleted(decision);
            Expect(session.RequestCancel("subject")&&!session.HostCompleted(decision)&&session.Reservations.Occupants("standing")==1);
            Expect(session.TryCancellation(out var cancelled)&&ReferenceEquals(cancelled,decision));
            for(var n=0;n<5;n++)session.Step(1,false,false,"subject");
            Expect(session.Activity("subject")?.Phase=="cancelling"&&session.Reservations.Occupants("standing")==1&&!session.TryReceipt(out _));
            Expect(session.ConfirmCancelled(decision)&&!session.ConfirmCancelled(decision)&&session.Reservations.Occupants("standing")==0);
            for(var n=0;n<30;n++)session.Step(1,false,false,"subject");
            Expect(session.TryReceipt(out var receipt)&&!receipt!.Committed&&!session.TryReceipt(out _));
            Expect(Close(session.Needs("subject")["hunger"],20));
        });
        Check("invalid drive reconfiguration preserves previous state",()=>{
            var drives=new DriveCalculator(Profile(),0);
            var invalid=J(new {commodity_needs=new{hunger=101},need_decay_per_second=new{hunger=0},autonomy_weights=new{hunger=1}});
            var caught=false;try {drives.Reconfigure(invalid,1);} catch(InvalidDataException){caught=true;}
            Expect(caught&&Close(drives.Snapshot(1)["hunger"],20)&&Close(drives.Snapshot(1)["energy"],80));
        });
        Check("cancel before host dispatch removes queued action once",()=>{
            var session=Session([Action("work","meal")]);var decision=session.Submit("subject","work");
            Expect(session.RequestCancel("subject")&&!session.TryAction(out _)&&!session.TryCancellation(out _));
            Expect(session.TryReceipt(out var receipt)&&!receipt!.Committed&&!session.TryReceipt(out _));
            Expect(!session.BeginWork(decision)&&!session.HostCompleted(decision));
        });
        Check("host failure after completion queued never commits need reward",()=>{
            var session=Session([Action("work","meal",20,resources:["standing"])]);
            session.ConfigureReservations(J(new[]{new{id="standing",capacity=1}}));
            var decision=session.Submit("subject","work");
            session.TryAction(out _);Expect(session.BeginWork(decision)&&session.HostCompleted(decision));
            Expect(session.HostFailed(decision,"physical_failure")&&!session.HostFailed(decision,"duplicate"));
            Expect(session.Reservations.Occupants("standing")==0);
            for(var n=0;n<25;n++)session.Step(1,false,false,"subject");
            Expect(session.TryReceipt(out var receipt)&&!receipt!.Committed&&receipt.Reason=="physical_failure"&&!session.TryReceipt(out _));
            Expect(Close(session.Needs("subject")["hunger"],20));
            var retry=session.Submit("subject","work");
            Expect(session.TryAction(out var dispatched)&&ReferenceEquals(retry,dispatched)&&session.Reservations.Occupants("standing")==1);
        });
        Check("undefined reservation does not leave actor occupied",()=>{
            var session=Session([Action("invalid","meal",resources:["missing"]),Action("valid","meal")]);
            var caught=false;try {session.Submit("subject","invalid");} catch(KeyNotFoundException){caught=true;}
            Expect(caught);
            var valid=session.Submit("subject","valid");Expect(session.TryAction(out var actual)&&ReferenceEquals(valid,actual));
        });
        Check("undeclared direct completion effect is rejected before execution",()=>{
            var invalid=new ActionCandidate("bad",new Condition.Constant(Truth.True),1,0,
                J(new {kind="meal",completed_need_effects=new {unknown=5}}));
            var caught=false;
            try {var session=Session([invalid]);session.Submit("subject","bad");}
            catch(InvalidDataException){caught=true;}
            Expect(caught);
        });
        Check("conflicting timing leaves actor available",()=>{
            var bad=new ActionCandidate("bad_timing",new Condition.Constant(Truth.True),1,0,
                J(new {kind="meal",requires_host_completion=true,timing=new {work_after_arrival=true,host_elapsed=true}}));
            var session=Session([bad,Action("valid","meal")]);var caught=false;
            try {session.Submit("subject","bad_timing");} catch(InvalidDataException){caught=true;}
            Expect(caught&&session.Activity("subject") is null);
            var valid=session.Submit("subject","valid");Expect(session.TryAction(out var actual)&&ReferenceEquals(valid,actual));
        });
        Check("invalid effect compilation leaves actor available",()=>{
            var bad=new ActionCandidate("bad_effect",new Condition.Constant(Truth.True),1,0,
                J(new {kind="meal",effects=new[]{new {op="invalid",@ref=new {type="ref",@object="o",facet="f",field="x"},value=1}}}));
            var session=Session([bad,Action("valid","meal")]);var caught=false;
            try {session.Submit("subject","bad_effect");} catch(InvalidDataException){caught=true;}
            Expect(caught&&session.Activity("subject") is null);
            var valid=session.Submit("subject","valid");Expect(session.TryAction(out var actual)&&ReferenceEquals(valid,actual));
        });
        Check("invalid focus after reservation releases acquired slot",()=>{
            var bad=new ActionCandidate("bad_focus",new Condition.Constant(Truth.True),1,0,
                J(new {kind="meal",resource_reservations=new[]{"standing"},grants=new[]{new {type="invalid"}}}));
            var session=Session([bad,Action("valid","meal",resources:["standing"])]);
            session.ConfigureReservations(J(new[]{new{id="standing",capacity=1}}));var caught=false;
            try {session.Submit("subject","bad_focus");} catch(InvalidDataException){caught=true;}
            Expect(caught&&session.Activity("subject") is null&&session.Reservations.Occupants("standing")==0);
            var valid=session.Submit("subject","valid");Expect(session.TryAction(out var actual)&&ReferenceEquals(valid,actual));
        });
        Check("DV depth counts real object and array containers consistently",()=>{
            static string Source(int containers,int mode) {
                // The declaration mapping is container one; each wrapper adds one.
                var value="1";
                for(var n=1;n<containers;n++)
                    value=(mode==0||mode==2&&n%2==0)?"{ child = "+value+"; }":"["+value+"]";
                return "schema \"dv-script-v1\"; data specimen { value = "+value+"; }";
            }
            foreach(var mode in new[]{0,1,2}) {
                var parsed=DvScriptReader.Parse(Source(64,mode));
                Expect(parsed.GetProperty("data").GetProperty("specimen").ValueKind==JsonValueKind.Object);
                var document=DvDocument.Parse(Source(64,mode));
                Expect(document.Definition("data","specimen").ValueKind==JsonValueKind.Object);
                var caught=false;try {DvScriptReader.Parse(Source(65,mode));} catch(FormatException){caught=true;}
                Expect(caught);
            }
        });
        Check("physical movement does not change actor knowledge revision",()=>{
            var session=Session([Action("walk","travel",work:false)]);
            var before=session.View("subject").KnowledgeRevision;
            session.ReportPlacement("subject",1,2,3,.5,"room");
            var placement=session.Placement("subject");
            Expect(placement is not null&&Close(placement.X,1)&&Close(placement.Y,2)&&Close(placement.Z,3)
                &&Close(placement.Yaw,.5)&&placement.Room=="room"&&session.View("subject").KnowledgeRevision==before);
        });
        Check("cancelling movement preserves completed physical displacement",()=>{
            var session=Session([Action("walk","travel",work:false)]);var decision=session.Submit("subject","walk");
            Expect(session.TryAction(out _));session.ReportPlacement("subject",2,3,4,1,"room");
            Expect(session.RequestCancel("subject")&&session.ConfirmCancelled(decision));
            var p=session.Placement("subject");Expect(p is not null&&Close(p.X,2)&&Close(p.Y,3)&&Close(p.Z,4)&&Close(p.Yaw,1));
            Expect(session.TryReceipt(out var receipt)&&!receipt!.Committed&&receipt.Reason=="cancelled");
        });
        Check("nonfinite placement leaves previous position intact",()=>{
            var session=Session([]);session.ReportPlacement("subject",1,2,3,.5,"room");var prior=session.Placement("subject");
            foreach(var index in Enumerable.Range(0,4)) {
                var values=new[]{1.0,2.0,3.0,.5};values[index]=double.NaN;var caught=false;
                try {session.ReportPlacement("subject",values[0],values[1],values[2],values[3],"room");}
                catch(ArgumentOutOfRangeException){caught=true;}
                Expect(caught&&session.Placement("subject")==prior);
            }
        });
        Check("navigation journey preserves distance across update slices and completes once",()=>{
            var path=new[]{new PathPoint(0,0,0),new PathPoint(3,0,0),new PathPoint(3,0,4)};
            var first=new PathJourney(path);var sliced=new PathJourney(path);
            first.Advance(2,2);sliced.Advance(0.5,2);sliced.Advance(1.5,2);
            Expect(Close(first.Length,7)&&Close(first.Distance,4)&&first.Position==new PathPoint(3,0,1));
            Expect(first.Position==sliced.Position&&!first.TakeCompletion());
            first.Advance(10,2);
            Expect(first.Position==path[^1]&&first.TakeCompletion()&&!first.TakeCompletion());
        });
        Check("navigation journey rejects invalid paths and time",()=>{
            var rejected=false;
            try{_ = new PathJourney([new(0,0,0),new(0,0,0)]);}catch(InvalidDataException){rejected=true;}
            Expect(rejected);
            var journey=new PathJourney([new(0,0,0),new(1,0,0)]);
            rejected=false;try{journey.Advance(double.NaN,1);}catch(ArgumentOutOfRangeException){rejected=true;}
            Expect(rejected&&Close(journey.Distance,0));
        });
        Check("timed strategy excludes authored scenario assumptions and uses only actor evidence",()=>{
            var source=File.ReadAllText("assets/data/cases/r3_npc_strategy.dv");
            var data=DvScriptReader.Parse(source).GetProperty("data");
            var profile=data.GetProperty("case_strategy").GetProperty("actors").EnumerateArray().Single(x=>x.GetProperty("actor").GetString()=="ruan");
            var mappings=data.GetProperty("case_mind_evidence").GetProperty("mappings");
            ActorClient MakeActor(int evidence) {
                var fields=new Dictionary<string,object>();
                var pair=evidence switch {1=>new[]{"lucian_ration_plan","ward_quer_dispute"},2=>new[]{"moran_window_routine","ward_veronica_dispute"},3=>new[]{"huo_pipe_routine","bertrand_lucian_dispute"},_=>Array.Empty<string>()};
                foreach(var key in pair)fields[key]=new {known=true,value=true,confidence=1};
                var actor=J(new {id="ruan",mind=new {subject="ruan",objects=new Dictionary<string,object> {
                    ["c2.ruan-knowledge"]=new {facets=new {situation=fields}}
                },adoptions=new {}},policy=new {goals=Array.Empty<object>()},autonomy=Profile()});
                var session=new RuntimeSession(J(new {objects=new {}}),J(new {inference_rules_per_slice=8,content_time_unit_seconds=1,observation_kinds=Array.Empty<string>()}),
                    Scheduling(),ClockPolicy(),J(new {inactive_retention_seconds=60,room_requests_per_slice=2,retry_delay_seconds=1,inactive_simulation="core_path_distance"}),0);
                return session.RegisterActor(actor,J(new {rules=Array.Empty<object>()}),[],false);
            }
            IReadOnlyList<CaseStrategyChoice> Run(int evidence) {
                using var activity=new StrategyActivity(MakeActor(evidence),CaseTheory.Parse(source),profile,mappings,1,J(new {predicate="self_play_started",args=new[]{"ruan","day2"}}));
                activity.Begin(0,120);
                for(var i=0;i<2000&&activity.Computing;i++)activity.Step(8,TimeSpan.FromMilliseconds(10));
                Expect(!activity.Computing && activity.Finish(119) is null);
                return activity.Finish(120) ?? throw new InvalidOperationException("Strategy did not finish.");
            }
            Expect(Run(0).All(x=>!x.Ready));
            var selected=Run(1).Where(x=>x.Ready).OrderByDescending(x=>x.Utility).FirstOrDefault();
            Expect(selected is not null && selected.Action=="ruan.ch2.gambit-fourteen-votes-probes" && Close(selected.Utility,17));
            var second=Run(2).Single(x=>x.Ready);Expect(second.Action=="ruan.ch2.gambit-sealed-folio-key" && Close(second.Utility,12));
            var third=Run(3).Single(x=>x.Ready);Expect(third.Action=="ruan.ch2.gambit-two-signatures-ration" && Close(third.Utility,7));
            using var cached=new StrategyActivity(MakeActor(1),CaseTheory.Parse(source),profile,mappings,1,J(new {predicate="self_play_started",args=new[]{"ruan","day2"}}));
            cached.Begin(0,120);
            var inProgress=JsonSerializer.Deserialize<StrategyActivityState>(JsonSerializer.Serialize(cached.SaveState()))!;
            using(var resumed=new StrategyActivity(MakeActor(1),CaseTheory.Parse(source),profile,mappings,1,J(new {predicate="self_play_started",args=new[]{"ruan","day2"}})))
            {
                resumed.RestoreState(inProgress);for(var i=0;i<2000&&resumed.Computing;i++)resumed.Step(8,TimeSpan.FromMilliseconds(10));
                Expect(resumed.Finish(119) is null && resumed.Finish(120) is not null);
            }
            for(var i=0;i<2000&&cached.Computing;i++)cached.Step(8,TimeSpan.FromMilliseconds(10));
            Expect(cached.Finish(120) is not null);var operations=cached.Operations;
            cached.Begin(120,120);Expect(!cached.Computing && cached.Operations==operations && cached.Finish(240) is not null);
        });
        Check("timed strategy cache invalidates after newly observed evidence",()=>{
            var source=File.ReadAllText("assets/data/cases/r3_npc_strategy.dv");
            var data=DvScriptReader.Parse(source).GetProperty("data");
            var profile=data.GetProperty("case_strategy").GetProperty("actors").EnumerateArray().Single(x=>x.GetProperty("actor").GetString()=="ruan");
            var mappings=data.GetProperty("case_mind_evidence").GetProperty("mappings");
            var key1=new FieldKey("c2.ruan-knowledge","situation","lucian_ration_plan");
            var key2=new FieldKey("c2.ruan-knowledge","situation","ward_quer_dispute");
            var world=J(new {objects=new Dictionary<string,object> { ["c2.ruan-knowledge"]=new {facets=new {situation=new {
                lucian_ration_plan=new {known=true,value=true,confidence=1},ward_quer_dispute=new {known=true,value=true,confidence=1}
            }}} }});
            var session=new RuntimeSession(world,J(new {inference_rules_per_slice=8,content_time_unit_seconds=1,observation_kinds=Array.Empty<string>()}),Scheduling(),ClockPolicy(),
                J(new {inactive_retention_seconds=60,room_requests_per_slice=2,retry_delay_seconds=1,inactive_simulation="core_path_distance"}),0);
            var attention=J(new {minimum_confidence=0,rules=new object[] {
                new {kind="field",@ref=new {type="ref",@object=key1.Object,facet=key1.Facet,field=key1.Field}},
                new {kind="field",@ref=new {type="ref",@object=key2.Object,facet=key2.Facet,field=key2.Field}}
            }});
            var actor=session.RegisterActor(Actor("ruan",Profile()),attention,[],false);
            session.ConfigureBindings(J(new[]{new {subject="ruan",physical="c2.ruan-knowledge",local="c2.ruan-knowledge"}}));
            using var activity=new StrategyActivity(actor,CaseTheory.Parse(source),profile,mappings,1,J(new {predicate="self_play_started",args=new[]{"ruan","day2"}}));
            activity.Begin(0,120);for(var i=0;i<2000&&activity.Computing;i++)activity.Step(8,TimeSpan.FromMilliseconds(10));
            Expect(activity.Finish(120)!.All(x=>!x.Ready));var operations=activity.Operations;
            Expect(session.ReceivePhysicalObservation("ruan",key1,"unit-observed-1") && session.ReceivePhysicalObservation("ruan",key2,"unit-observed-2"));
            session.Refresh("ruan",RefreshCause.Deliberate);for(var i=0;i<20&&actor.IsUpdating;i++)session.Step(0,false,false,"ruan");
            Expect(!actor.IsUpdating && actor.KnowledgeRevision>0);
            activity.Begin(120,120);Expect(activity.Computing);
            for(var i=0;i<2000&&activity.Computing;i++)activity.Step(8,TimeSpan.FromMilliseconds(10));
            Expect(activity.Operations>operations && activity.Finish(240)!.Any(x=>x.Ready&&Close(x.Utility,17)));
        });
        Check("actor checkpoint preserves private knowledge evidence and buffered observations",()=>{
            var key=new FieldKey("test-object","state","seen");
            var world=J(new {objects=new Dictionary<string,object> { ["test-object"]=new {facets=new {state=new {seen=new {known=true,value=true,confidence=1}}}} }});
            var attention=J(new {minimum_confidence=0,rules=new object[] {new {kind="field",@ref=new {type="ref",@object=key.Object,facet=key.Facet,field=key.Field}}}});
            RuntimeSession Make()=>new(world,J(new {inference_rules_per_slice=8,content_time_unit_seconds=1,observation_kinds=Array.Empty<string>()}),Scheduling(),ClockPolicy(),
                J(new {inactive_retention_seconds=60,room_requests_per_slice=2,retry_delay_seconds=1,inactive_simulation="core_path_distance"}),0);
            var first=Make();var actor=first.RegisterActor(Actor("ruan",Profile()),attention,[],false);
            first.ConfigureBindings(J(new[]{new {subject="ruan",physical="test-object",local="test-object"}}));
            Expect(first.ReceivePhysicalObservation("ruan",key,"first"));first.Refresh("ruan",RefreshCause.Deliberate);
            for(var n=0;n<20&&actor.IsUpdating;n++)first.Step(0,false,false,"ruan");
            Expect(first.ReceivePhysicalObservation("ruan",key,"buffered") && actor.PendingInformation==1);
            var serialized=JsonSerializer.Serialize(actor.SaveState());
            var second=Make();var restored=second.RegisterActor(Actor("ruan",Profile()),attention,[],false);
            restored.RestoreState(JsonSerializer.Deserialize<ActorClientState>(serialized)!);
            Expect(restored.Recall(key) is {Known:true} fact && fact.Value.GetBoolean());
            Expect(restored.Evidence.Count==actor.Evidence.Count && restored.PendingInformation==1 && restored.KnowledgeRevision==actor.KnowledgeRevision);
            second.Refresh("ruan",RefreshCause.Deliberate);for(var n=0;n<20&&restored.IsUpdating;n++)second.Step(0,false,false,"ruan");
            Expect(restored.PendingInformation==0 && restored.Evidence.Count==actor.Evidence.Count+1);
            for(var n=0;n<20;n++)Expect(first.ReceivePhysicalObservation("ruan",key,"staged-"+n));
            first.Refresh("ruan",RefreshCause.Deliberate);first.Step(0,false,false,"ruan");Expect(actor.IsUpdating);
            var staged=JsonSerializer.Deserialize<ActorClientState>(JsonSerializer.Serialize(actor.SaveState()))!;
            var third=Make();var resumed=third.RegisterActor(Actor("ruan",Profile()),attention,[],false);resumed.RestoreState(staged);
            for(var n=0;n<20&&resumed.IsUpdating;n++)third.Step(0,false,false,"ruan");
            Expect(!resumed.IsUpdating && resumed.Evidence.Count==actor.Evidence.Count+21);
        });
        Check("individual evaluation uses own evidence and declared values",()=>{
            var source=File.ReadAllText("assets/data/cases/r3_npc_strategy.dv");
            var data=DvScriptReader.Parse(source).GetProperty("data");
            var profile=data.GetProperty("case_strategy").GetProperty("actors").EnumerateArray().Single(x=>x.GetProperty("actor").GetString()=="ward_eval");
            var mappings=data.GetProperty("case_mind_evidence").GetProperty("mappings");
            var key=new FieldKey("c2.strategy","state","diversion_fabricated");
            var world=J(new {objects=new Dictionary<string,object> { ["c2.strategy"]=new {facets=new {state=new {diversion_fabricated=new {known=true,value=true,confidence=1}}}} }});
            var session=new RuntimeSession(world,J(new {inference_rules_per_slice=8,content_time_unit_seconds=1,observation_kinds=Array.Empty<string>()}),Scheduling(),ClockPolicy(),
                J(new {inactive_retention_seconds=60,room_requests_per_slice=2,retry_delay_seconds=1,inactive_simulation="core_path_distance"}),0);
            var attention=J(new {minimum_confidence=0,rules=new object[] {new {kind="field",@ref=new {type="ref",@object=key.Object,facet=key.Facet,field=key.Field}}}});
            var actor=session.RegisterActor(Actor("ward",Profile()),attention,[],false);
            session.ConfigureBindings(J(new[]{new {subject="ward",physical="c2.strategy",local="c2.strategy"}}));
            using var activity=new StrategyActivity(actor,CaseTheory.Parse(source),profile,mappings,1);
            activity.Begin(0,60);for(var n=0;n<2000&&activity.Computing;n++)activity.Step(8,TimeSpan.FromMilliseconds(10));
            Expect(activity.Finish(60)!.All(choice=>!choice.Ready));
            Expect(session.ReceivePhysicalObservation("ward",key,"unit-evidence"));session.Refresh("ward",RefreshCause.Deliberate);
            for(var n=0;n<20&&actor.IsUpdating;n++)session.Step(0,false,false,"ward");
            activity.Begin(60,60);for(var n=0;n<2000&&activity.Computing;n++)activity.Step(8,TimeSpan.FromMilliseconds(10));
            Expect(activity.Finish(120)!.Any(choice=>choice.Action=="ward.eval.condemn-fabricated-diversion" && choice.Ready));
        });
        return failed==0?0:1;
    }
}
// --- Source: RoomLifecycleChecks.cs ---

internal static class RoomLifecycleChecks
{
    public static int Run()
    {
        var failures = 0;
        void Check(string name, Action action)
        {
            var timer = Stopwatch.StartNew();
            try { action(); Console.WriteLine($"PASS {name} {timer.Elapsed.TotalMilliseconds:F3} ms"); }
            catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
        }
        static void Expect(bool value) { if (!value) throw new InvalidOperationException("Assertion failed"); }
        static RoomLifecycle Active()
        {
            var room = new RoomLifecycle(); room.Update(0, true, true, false, 5);
            room.CompletePreparation(room.Generation); return room;
        }
        Check("cold demand prepares before activation", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, true, false, 5);
            Expect(t.Effect == RoomEffect.Prepare && r.Phase == RoomPhase.Preparing);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.Activate);
        });
        Check("prepared room remains invisible until active demand", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, false, false, 5);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.Phase == RoomPhase.Preparing && r.Prepared);
            Expect(r.Update(1, true, true, false, 5).Effect == RoomEffect.Activate);
        });
        Check("cooldown retains and reactivates same generation", () => {
            var r = Active(); var g = r.Generation;
            Expect(r.Update(1, false, false, false, 5).Effect == RoomEffect.Retain);
            Expect(r.Update(2, true, true, false, 5).Effect == RoomEffect.Activate);
            Expect(r.Generation == g);
        });
        Check("release occurs at exact retention boundary", () => {
            var r = Active(); r.Update(1, false, false, false, 5);
            Expect(r.Update(4.999, false, false, false, 5).Effect == RoomEffect.None);
            var t = r.Update(5, false, false, false, 5);
            Expect(t.Effect == RoomEffect.Release && r.Phase == RoomPhase.Unloading);
            Expect(r.CompleteRelease(t.Generation) && r.Phase == RoomPhase.Unloaded && !r.Prepared);
        });
        Check("late async callback cannot reactivate cancelled preparation", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, true, false, 0);
            var release = r.Update(1, false, false, false, 0);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.CompleteRelease(release.Generation));
            r.Update(2, true, true, false, 0);
            Expect(!r.CompleteRelease(release.Generation));
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
        });
        Check("in flight protection delays release", () => {
            var r = Active(); r.Update(1, false, false, false, 5);
            Expect(r.Update(100, false, false, true, 5).Effect == RoomEffect.None);
            Expect(r.Update(104.99, false, false, false, 5).Effect == RoomEffect.None);
            Expect(r.Update(105, false, false, false, 5).Effect == RoomEffect.Release);
        });
        Check("10000 doorway reversals do not start a second preparation", () => {
            var r = Active(); var g = r.Generation;
            for (var i = 1; i <= 10000; i++) r.Update(i * .001, true, i % 2 == 0, false, 5);
            Expect(r.Generation == g && r.Prepared);
        });
        Check("monotonic finite clock required", () => {
            var r = Active(); var caught = 0;
            foreach (var time in new[] { -1.0, double.NaN, double.PositiveInfinity })
                try { r.Update(time, false, false, false, 5); } catch (ArgumentOutOfRangeException) { caught++; }
            Expect(caught == 3);
        });
        Check("failed preparation releases before retry", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, false, false, 5);
            var failed = r.FailPreparation(t.Generation);
            Expect(failed.Effect == RoomEffect.Release && r.Phase == RoomPhase.Unloading);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.CompleteRelease(failed.Generation));
            Expect(r.Update(1, true, true, false, 5).Effect == RoomEffect.Prepare);
        });
        Check("demand arriving during release starts a fresh generation on next slice", () => {
            var r = Active(); r.Update(1, false, false, false, 0);
            var release = r.Update(2, false, false, false, 0);
            Expect(r.Update(3, true, true, false, 0).Effect == RoomEffect.None);
            Expect(r.CompleteRelease(release.Generation));
            var restart = r.Update(4, true, true, false, 0);
            Expect(restart.Effect == RoomEffect.Prepare && restart.Generation > release.Generation);
        });
        Check("cold pinned room prepares without activation", () => {
            var r = new RoomLifecycle();
            Expect(r.Update(0, false, false, true, 5).Effect == RoomEffect.Prepare);
            Expect(r.CompletePreparation(r.Generation).Effect == RoomEffect.None);
        });
        Check("invalid retention cannot mutate state", () => {
            foreach (var retention in new[] { -1.0, double.NaN, double.PositiveInfinity }) {
                var r = new RoomLifecycle(); var caught = false;
                try { r.Update(1, true, true, false, retention); } catch (ArgumentOutOfRangeException) { caught = true; }
                Expect(caught && r.Phase == RoomPhase.Unloaded && r.Generation == 0);
            }
        });
        Check("residency emits preparation activation retention and release separately", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 5,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetPrepare("fixture", true);
            var prepare = manager.Step(0).Single();
            Expect(prepare.Effect == RoomEffect.Prepare && !manager.Ready("fixture"));
            Expect(manager.Acknowledge(prepare, true, 0) is null);
            manager.SetVisible("fixture", true, 1);
            Expect(manager.Step(1).Single().Effect == RoomEffect.Activate && manager.Ready("fixture"));
            manager.SetPrepare("fixture", false); manager.SetVisible("fixture", false, 2);
            Expect(manager.Step(2).Single().Effect == RoomEffect.Retain);
            var release = manager.Step(6).Single();
            Expect(release.Effect == RoomEffect.Release && !manager.Ready("fixture"));
            Expect(manager.Acknowledge(release, true, 6) is null);
            Expect(manager.Phase("fixture") == RoomPhase.Unloaded);
        });
        Check("duplicate preparation completion and failure cannot mutate prepared room", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, false, false, 5);
            r.CompletePreparation(t.Generation);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.FailPreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.Prepared && r.Phase == RoomPhase.Preparing && r.Generation == t.Generation);
        });
        Check("residency returns activation after asynchronous preparation", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 5,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetVisible("fixture", true, 0);
            var prepare = manager.Step(0).Single();
            var activation = manager.Acknowledge(prepare, true, 0);
            Expect(activation?.Effect == RoomEffect.Activate && manager.Ready("fixture"));
        });
        Check("failed release remains pending and returns same request", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 0,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetVisible("fixture", true, 0);
            var prepare = manager.Step(0).Single(); manager.Acknowledge(prepare, true, 0);
            manager.SetVisible("fixture", false, 1); manager.Step(1);
            var release = manager.Step(2).Single();
            Expect(manager.Acknowledge(release, false, 2) == release);
            Expect(manager.Phase("fixture") == RoomPhase.Unloading);
            Expect(manager.Acknowledge(release, true, 3) is null && manager.Phase("fixture") == RoomPhase.Unloaded);
        });
        Check("failed preparation retains retry delay after release", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 5,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetPrepare("fixture", true);
            var prepare = manager.Step(0).Single();
            var release = manager.CancelPreparation(prepare, 0);
            manager.Acknowledge(release, true, 0);
            Expect(manager.Step(0.5).Count == 0 && manager.Phase("fixture") == RoomPhase.Unloaded);
            Expect(manager.Step(1).Single().Effect == RoomEffect.Prepare);
        });
        Check("completion uses current visibility before next slice", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 5,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetVisible("fixture", true, 0);
            var prepare = manager.Step(0).Single();
            manager.SetVisible("fixture", false, 0);
            Expect(manager.Acknowledge(prepare, true, 0) is null && !manager.Ready("fixture"));
            manager.SetVisible("fixture", true, 1);
            Expect(manager.Step(1).Single().Effect == RoomEffect.Activate && manager.Ready("fixture"));
        });
        Check("room bounds include plane contact and reject outside", () => {
            Plane[] planes = [new(Vector3.UnitX, 0)];
            Expect(RoomVisibility.IntersectsBounds(new(-1, -1, -1), new(0, 1, 1), planes));
            Expect(RoomVisibility.IntersectsBounds(new(-1, -1, -1), new(1, 1, 1), planes));
            Expect(!RoomVisibility.IntersectsBounds(new(-2, -1, -1), new(-1, 1, 1), planes));
            var caught = false;
            try { RoomVisibility.IntersectsBounds(Vector3.One, Vector3.Zero, planes); } catch (ArgumentException) { caught = true; }
            Expect(caught);
        });
        Check("aperture clipping preserves intersection and rejects outside", () => {
            Vector3[] aperture = [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)];
            Plane[] planes = [new(Vector3.UnitX, 0), new(Vector3.UnitY, 0)];
            var result = RoomVisibility.ClipAperture(aperture, planes);
            Expect(result.Count == 4 && result.All(p => p.X >= 0 && p.Y >= 0));
            Expect(result.Any(p => p == Vector3.Zero) && result.Any(p => p == new Vector3(1, 1, 0)));
            Expect(RoomVisibility.ClipAperture(aperture, [new Plane(Vector3.UnitX, -2)]).Count == 0);
        });
        Check("external retention setting updates existing cooldown without resetting elapsed time", () => {
            var settings=RuntimeDebugSettings.Parse("schema \"dv-script-v1\"; data debug.coregodot { value = { room_inactive_retention_seconds = 60; }; }");
            var policy=JsonSerializer.SerializeToElement(new {inactive_retention_seconds=5,room_requests_per_slice=1,inactive_simulation="core",retry_delay_seconds=1});
            var manager=new RoomResidency(policy);manager.Register("fixture",10,1);
            manager.SetVisible("fixture",true,0);var prepare=manager.Step(0).Single();manager.Acknowledge(prepare,true,0);
            manager.SetVisible("fixture",false,1);manager.Step(1);
            manager.SetRetentionSeconds(settings.RoomInactiveRetentionSeconds);
            Expect(manager.Step(5).Count==0 && manager.Phase("fixture")==RoomPhase.CoolingDown);
            Expect(manager.Step(59.999).Count==0);
            Expect(manager.Step(60).Single().Effect==RoomEffect.Release);
            var rejected=false;try { manager.SetRetentionSeconds(double.NaN); } catch(ArgumentOutOfRangeException) {rejected=true;}
            Expect(rejected && manager.RetentionSeconds==60);
        });
        Check("connected sightlines prepare behind closed leaves before first opening", () => {
            RoomSightLink[] links=[new("corridor","foyer",true,true),new("foyer","outside",true,false),
                new("outside","backdrop",true,true),new("backdrop","foyer",true,true),new("foyer","hidden",false,true)];
            Expect(RoomVisibility.ReachableRooms(["corridor"],links,false).SetEquals(["corridor","foyer"]));
            Expect(RoomVisibility.ReachableRooms(["corridor"],links,true).SetEquals(["corridor","foyer","outside","backdrop"]));
            links[1]=links[1] with {Open=true};
            Expect(RoomVisibility.ReachableRooms(["corridor"],links,false).SetEquals(["corridor","foyer","outside","backdrop"]));
            Expect(RoomVisibility.ReachableRooms([],links,true).Count==0);
        });
        Check("room visibility rejects invalid planes", () => {
            Vector3[] aperture = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
            foreach (Plane[] planes in new Plane[][] { [], [new Plane(Vector3.Zero, 0)],
                [new Plane(new Vector3(float.NaN, 0, 0), 0)], [new Plane(Vector3.UnitX, float.PositiveInfinity)] }) {
                var boundsCaught = false; var clipCaught = false;
                try { RoomVisibility.IntersectsBounds(Vector3.Zero, Vector3.One, planes); } catch (ArgumentException) { boundsCaught = true; }
                try { RoomVisibility.ClipAperture(aperture, planes); } catch (ArgumentException) { clipCaught = true; }
                Expect(boundsCaught && clipCaught);
            }
        });
        return failures == 0 ? 0 : 1;
    }
}
// --- Source: SpatialPublication.Session.cs ---

/// <summary>Validates the selected spatial data before a host can publish it.</summary>
internal static partial class SpatialPublication
{
    private static readonly object Missing = new();
    private static object Field(object record, string key) => R.Bool(R.record_like(record)) ? R.record_find(record, key, Missing) : Missing;

    private static List<object> Items(object value, string path)
    {
        if (!R.TryItems(value, out var items)) throw new InvalidDataException($"Spatial {path} must be a list.");
        return items.ToList();
    }

    // Doors and furniture share the same explicit publication/action contract.
    // A geometric opening or review pose never implies interaction readiness.
    internal static IEnumerable<object> InteractionTargets(object floor)
    {
        foreach (var item in Items(Field(floor, "furniture"), "furniture")) yield return item;
        foreach (var opening in Items(Field(floor, "openings"), "openings"))
        {
            var door = Field(opening, "door");
            if (!ReferenceEquals(door, Missing)) yield return door;
            var extra = Field(opening, "additional_doors");
            if (!ReferenceEquals(extra, Missing))
                foreach (var leaf in Items(extra, "additional_doors")) yield return leaf;
        }
    }

    private static string Id(object record, string path)
    {
        if (Field(record, "id") is not string { Length: > 0 } id)
            throw new InvalidDataException($"Spatial {path} needs a nonempty id.");
        return id;
    }

    private static double Number(object value, string path)
    {
        if (value is not double number || !double.IsFinite(number))
            throw new InvalidDataException($"Spatial {path} must be a finite number.");
        return number;
    }

    private static bool IsDeclaredGeometry(object record, string path)
    {
        var source = Field(record, "geometry_source");
        if (ReferenceEquals(source, Missing) || source is "replacement") return false;
        if (source is "declared") return true;
        throw new InvalidDataException($"Unsupported geometry source: {path}/geometry_source");
    }

    private static double[] Vector(object value, int length, string path)
    {
        var items = Items(value, path);
        if (items.Count != length) throw new InvalidDataException($"Spatial {path} needs {length} numbers.");
        return items.Select((item, index) => Number(item, $"{path}/{index}")).ToArray();
    }

    private static void Rect(object value, string path)
    {
        var r = Vector(value, 4, path);
        if (r[2] <= r[0] || r[3] <= r[1]) throw new InvalidDataException($"Spatial {path} has nonpositive extent.");
    }

    private static void ValidateSemanticRegions(object spatial, List<object> floors)
    {
        var declared = Field(spatial, "semantic_regions");
        if (ReferenceEquals(declared, Missing)) return;
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var occupied = new Dictionary<string, List<double[]>>(StringComparer.Ordinal);
        foreach (var region in Items(declared, "semantic_regions"))
        {
            var id = Id(region, "semantic_regions");
            var floorId = Field(region, "floor") as string ?? "";
            var floor = floors.SingleOrDefault(f => Field(f, "id") as string == floorId)
                ?? throw new InvalidDataException("Semantic region floor is missing: " + id);
            if (!identities.Add(floorId + ":" + id) || id.Contains(':') ||
                Items(Field(floor, "rooms"), floorId + "/rooms").Any(r => Id(r, "room").Replace("-", "") == id))
                throw new InvalidDataException("Duplicate semantic room identity: " + id);
            if (Field(region, "source") is not string { Length: > 0 })
                throw new InvalidDataException("Semantic region needs provenance: " + id);
            if (!occupied.TryGetValue(floorId, out var previous)) occupied[floorId] = previous = new();
            var surfaces = Items(Field(floor, "pieces"), floorId + "/pieces")
                .Where(p => Field(p, "kind") as string == "floor")
                .Select(p => Vector(Field(p, "rect"), 4, "floor surface")).ToArray();
            var rooms = Items(Field(floor, "rooms"), floorId + "/rooms")
                .Select(r => Vector(Field(r, "bbox"), 4, "room bounds")).ToArray();
            var rectangles = Items(Field(region, "rects"), id + "/rects");
            if (rectangles.Count == 0) throw new InvalidDataException("Empty semantic region: " + id);
            foreach (var item in rectangles)
            {
                Rect(item, id); var area = Vector(item, 4, id);
                bool Overlaps(double[] other) => Math.Min(area[2], other[2]) > Math.Max(area[0], other[0]) + 1e-6 &&
                    Math.Min(area[3], other[3]) > Math.Max(area[1], other[1]) + 1e-6;
                if (rooms.Any(Overlaps) || previous.Any(Overlaps))
                    throw new InvalidDataException("Semantic region overlaps a room or region: " + id);
                // Check the complete rectangle against the union of real floor
                // surfaces, including holes, rather than checking only its centre.
                var xs = surfaces.SelectMany(s => new[] { s[0], s[2] }).Append(area[0]).Append(area[2])
                    .Where(x => x >= area[0] && x <= area[2]).Distinct().Order().ToArray();
                var ys = surfaces.SelectMany(s => new[] { s[1], s[3] }).Append(area[1]).Append(area[3])
                    .Where(y => y >= area[1] && y <= area[3]).Distinct().Order().ToArray();
                for (var x = 1; x < xs.Length; x++)
                    for (var y = 1; y < ys.Length; y++)
                    {
                        var cx = (xs[x - 1] + xs[x]) / 2; var cy = (ys[y - 1] + ys[y]) / 2;
                        if (!surfaces.Any(s => cx >= s[0] && cx <= s[2] && cy >= s[1] && cy <= s[3]))
                            throw new InvalidDataException("Semantic region has no supporting floor: " + id);
                    }
                previous.Add(area);
            }
        }
    }

    private static object ResolveWorldDatum(ContentCatalog catalog, object manifest, object spatial)
    {
        var reference = Field(spatial, "world_datum_ref");
        if (Field(reference, "definition") is not string { Length: > 0 } id || Field(reference, "field") is not string { Length: > 0 } field)
            throw new InvalidDataException("Spatial world_datum_ref must identify a data definition and field.");
        if (!Items(Field(manifest, "definitions"), "manifest/definitions").Any(item => item is string candidate && candidate == id))
            throw new InvalidDataException($"World datum definition absent from manifest: {id}");
        if (Field(reference, "file") is not string { Length: > 0 } file || Path.GetFileName(file) != file)
            throw new InvalidDataException("Spatial world_datum_ref/file must be a published filename.");
        var definition = catalog.Get("data", id);
        if (definition is false) throw new InvalidDataException($"Missing world datum definition: {id}");
        var datum = Field(Field(definition, "value"), field);
        _ = Vector(datum, 3, $"{id}/{field}");
        return datum;
    }

    private static void ValidateRailingCandidate(object spatial, List<object> floors)
    {
        var candidate = Field(spatial, "railing_assembly_candidate");
        if (ReferenceEquals(candidate, Missing)) return;
        const string path = "railing_assembly_candidate";
        if (Field(candidate, "status") is not "full-path-candidate-not-published") throw new InvalidDataException($"Invalid {path}/status");
        foreach (var key in new[] { "max_post_spacing_m", "arc_tolerance_m", "post_radius_m", "rail_radius_m", "guard_thickness_m", "junction_tolerance_m" })
            if (Number(Field(candidate, key), $"{path}/{key}") <= 0) throw new InvalidDataException($"Invalid {path}/{key}");
        var paths = Items(Field(candidate, "paths"), path + "/paths");
        if (paths.Count == 0) throw new InvalidDataException($"Empty {path}/paths");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in paths)
        {
            var id = Id(item, path + "/paths");
            var itemPath = $"{path}/paths/{id}";
            if (!ids.Add(id)) throw new InvalidDataException($"Duplicate {itemPath}");
            var points = Items(Field(item, "points_design"), itemPath + "/points_design");
            var heights = Items(Field(item, "control_heights_m"), itemPath + "/control_heights_m");
            if (points.Count < 2 || heights.Count != points.Count) throw new InvalidDataException($"Invalid {itemPath}/points_design");
            for (var i = 0; i < points.Count; i++)
            {
                _ = Vector(points[i], 3, $"{itemPath}/points_design/{i}");
                if (Number(heights[i], $"{itemPath}/control_heights_m/{i}") <= 0) throw new InvalidDataException($"Invalid {itemPath}/control_heights_m/{i}");
            }
            foreach (var key in new[] { "own_start_post", "own_end_post" })
                if (Field(item, key) is not bool) throw new InvalidDataException($"Invalid {itemPath}/{key}");
            foreach (var key in new[] { "start_guard_cut_m", "end_guard_cut_m" }) _ = Vector(Field(item, key), 2, $"{itemPath}/{key}");
            var support = Field(item, "support");
            if (Field(support, "enabled") is not bool enabled) throw new InvalidDataException($"Invalid {itemPath}/support/enabled");
            if (enabled)
            {
                if (Vector(Field(support, "size_design"), 3, itemPath + "/support/size_design").Any(v => v <= 0)) throw new InvalidDataException($"Invalid {itemPath}/support/size_design");
                _ = Vector(Field(support, "offset_design"), 3, itemPath + "/support/offset_design");
            }
        }
        var usedEnds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var joint in Items(Field(candidate, "junctions"), path + "/junctions"))
        {
            if (Field(joint, "a") is not string a || Field(joint, "b") is not string b || a == b || !ids.Contains(a) || !ids.Contains(b)
                || Field(joint, "a_end") is not string ae || ae is not ("start" or "end") || Field(joint, "b_end") is not string be || be is not ("start" or "end")
                || !usedEnds.Add(a + ":" + ae) || !usedEnds.Add(b + ":" + be))
                throw new InvalidDataException($"Invalid {path}/junctions");
        }
        var replacements = Field(candidate, "replaces");
        foreach (var floor in floors)
        {
            var floorId = Id(floor, "floor");
            var group = Field(replacements, floorId);
            if (ReferenceEquals(group, Missing)) continue;
            foreach (var pair in new[] { ("pieces", "pieces"), ("colliders", "colliders") })
            {
                var allowed = Items(Field(floor, pair.Item2), $"{floorId}/{pair.Item2}").Select(item => Id(item, $"{floorId}/{pair.Item2}")).ToHashSet(StringComparer.Ordinal);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in Items(Field(group, pair.Item1), $"{path}/replaces/{floorId}/{pair.Item1}"))
                    if (entry is not string id || !allowed.Contains(id) || !seen.Add(id)) throw new InvalidDataException($"Invalid {path}/replaces/{floorId}/{pair.Item1}");
            }
        }
    }

    private static void ValidateAnchorCandidates(ContentCatalog catalog, object spatial, List<object> floors, HashSet<string> spatialIds)
    {
        var gates = Items(Field(spatial, "isolation_gate_candidates"), "isolation_gate_candidates");
        var byId = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var gate in gates)
        {
            var id = Id(gate, "isolation_gate_candidates");
            var path = $"isolation_gate_candidates/{id}";
            if (!spatialIds.Add(id) || !byId.TryAdd(id, gate)) throw new InvalidDataException($"Duplicate {path}");
            if (Field(gate, "status") is not "unconfigured") throw new InvalidDataException($"Invalid {path}/status");
            var center = Vector(Field(gate, "center"), 3, path + "/center");
            var floorId = Field(gate, "floor") as string;
            var floor = floors.SingleOrDefault(f => Field(f, "id") is string floorName && floorName == floorId);
            if (floor is null || Math.Abs(center[2] - Number(Field(floor, "z"), path + "/floor/z")) > 0.001) throw new InvalidDataException($"Invalid {path}/floor");
            foreach (var key in new[] { "clear_width_m", "clear_height_m" })
                if (Number(Field(gate, key), $"{path}/{key}") <= 0) throw new InvalidDataException($"Invalid {path}/{key}");
        }
        foreach (var anchor in Items(Field(spatial, "spatial_anchors"), "spatial_anchors"))
        {
            var id = Id(anchor, "spatial_anchors");
            var path = $"spatial_anchors/{id}";
            if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate {path}");
            if (Field(anchor, "anchor_kind") is "interior")
            {
                if (Field(anchor, "status") is not "ready") throw new InvalidDataException($"Invalid {path}/status");
                var interiorPlace = Field(Field(catalog.Get("data", "host.runtime.places"), "value"), Field(anchor, "logical_location") as string ?? "");
                if (!R.Bool(R.eq(Field(interiorPlace, "map"), Field(anchor, "host_map"))) || !R.Bool(R.eq(Field(interiorPlace, "room"), Field(anchor, "host_room"))))
                    throw new InvalidDataException($"Host place mismatch: {path}/logical_location");
                var interiorFloor = floors.SingleOrDefault(f => R.Bool(R.eq(Field(f, "id"), Field(anchor, "floor"))));
                if (interiorFloor is null) throw new InvalidDataException($"Invalid {path}/floor");
                var interiorPiece = Items(Field(interiorFloor, "pieces"), path).SingleOrDefault(p => R.Bool(R.eq(Field(p, "id"), Field(anchor, "floor_piece_ref"))));
                if (interiorPiece is null || Field(interiorPiece, "kind") is not "floor") throw new InvalidDataException($"Invalid {path}/floor_piece_ref");
                var interiorPoint = Vector(Field(anchor, "position"), 3, path + "/position");
                var interiorRect = Vector(Field(interiorPiece, "rect"), 4, path + "/floor_piece_ref/rect");
                var interiorRoom = Items(Field(interiorFloor, "rooms"), path).SingleOrDefault(r => R.Bool(R.eq(Field(r, "id"), Field(anchor, "room_ref"))));
                if (interiorRoom is null) throw new InvalidDataException($"Invalid {path}/room_ref");
                var roomRect = Vector(Field(interiorRoom, "bbox"), 4, path + "/room_ref/bbox");
                if (new[] { interiorRect, roomRect }.Any(rectangle => interiorPoint[0] <= rectangle[0] || interiorPoint[0] >= rectangle[2] || interiorPoint[1] <= rectangle[1] || interiorPoint[1] >= rectangle[3])
                    || Math.Abs(interiorPoint[2] - Number(Field(interiorPiece, "z"), path) - Number(Field(interiorPiece, "height"), path)) > 0.001)
                    throw new InvalidDataException($"Invalid {path}/position");
                continue;
            }
            if (Field(anchor, "status") is not "unconfigured") throw new InvalidDataException($"Invalid {path}/status");
            foreach (var key in new[] { "logical_location", "host_map", "host_room" })
                if (Field(anchor, key) is not string { Length: > 0 }) throw new InvalidDataException($"Invalid {path}/{key}");
            var placesDefinition = catalog.Get("data", "host.runtime.places");
            var places = Field(placesDefinition, "value");
            var place = Field(places, (string)Field(anchor, "logical_location"));
            if (!R.Bool(R.eq(Field(place, "map"), Field(anchor, "host_map"))) || !R.Bool(R.eq(Field(place, "room"), Field(anchor, "host_room"))))
                throw new InvalidDataException($"Host place mismatch: {path}/logical_location");
            if (Field(anchor, "gate_ref") is not string gateId || !byId.TryGetValue(gateId, out var gate)) throw new InvalidDataException($"Invalid {path}/gate_ref");
            if (Field(anchor, "floor") is not string { Length: > 0 } floorId) throw new InvalidDataException($"Invalid {path}/floor");
            var floor = floors.SingleOrDefault(f => Field(f, "id") is string floorName && floorName == floorId);
            if (floor is null || Field(gate, "floor") is not string gateFloor || gateFloor != floorId)
                throw new InvalidDataException($"Invalid {path}/floor");
            if (Field(anchor, "floor_piece_ref") is not string pieceId) throw new InvalidDataException($"Invalid {path}/floor_piece_ref");
            var piece = Items(Field(floor, "pieces"), floorId + "/pieces").SingleOrDefault(item => Field(item, "id") is string pieceName && pieceName == pieceId);
            if (piece is null || Field(piece, "kind") is not "floor") throw new InvalidDataException($"Invalid {path}/floor_piece_ref");
            var rect = Vector(Field(piece, "rect"), 4, path + "/floor_piece_ref/rect");
            var position = Vector(Field(anchor, "position"), 3, path + "/position");
            var center = Vector(Field(gate, "center"), 3, path + "/gate_ref/center");
            if (position[0] <= rect[0] || position[0] >= rect[2] || position[1] <= rect[1] || position[1] >= rect[3]
                || Math.Abs(position[2] - Number(Field(piece, "z"), path + "/floor_piece_ref/z") - Number(Field(piece, "height"), path + "/floor_piece_ref/height")) > 0.001)
                throw new InvalidDataException($"Invalid {path}/position");
            var halfWidth = Number(Field(gate, "clear_width_m"), path + "/gate_ref/clear_width_m") / 2;
            var validSide = Field(anchor, "side") switch
            {
                "south" => position[1] < center[1] && Math.Abs(position[0] - center[0]) <= halfWidth,
                "north" => position[1] > center[1] && Math.Abs(position[0] - center[0]) <= halfWidth,
                "west" => position[0] < center[0] && Math.Abs(position[1] - center[1]) <= halfWidth,
                "east" => position[0] > center[0] && Math.Abs(position[1] - center[1]) <= halfWidth,
                _ => false
            };
            if (!validSide)
                throw new InvalidDataException($"Invalid {path}/side");
        }
    }

    private static void ValidateDeclaredActions(ContentCatalog catalog, object manifest)
    {
        foreach (var entry in Items(Field(manifest, "definitions"), "manifest/definitions"))
        {
            if (entry is not string id) continue;
            var definition = catalog.Get("data", id);
            var value = Field(definition, "value");
            if (Field(value, "kind") is not "spatial-action") continue;
            var path = $"data/{id}";
            if (Field(value, "status") is not ("ready" or "unconfigured")) throw new InvalidDataException($"Invalid action status: {path}/status");
            if (Field(value, "method") is not string { Length: > 0 }) throw new InvalidDataException($"Missing action method: {path}/method");
            if (Field(value, "law_schema") is not string { Length: > 0 }) throw new InvalidDataException($"Missing action law schema: {path}/law_schema");
            if (Field(value, "rule_template") is not string templateId || catalog.Get("data", templateId) is false
                || Field(Field(catalog.Get("data", templateId), "value"), "kind") is not "dv-value-template")
                throw new InvalidDataException($"Invalid action template reference: {path}/rule_template");
            var parameters = Items(Field(Field(catalog.Get("data", templateId), "value"), "parameters"), path + "/rule_template/parameters");
            var arguments = Items(Field(value, "arguments"), path + "/arguments");
            if (parameters.Count != arguments.Count) throw new InvalidDataException($"Action argument count mismatch: {path}/arguments");
        }
    }

    private static void ValidateInteraction(ContentCatalog catalog, object item, string path)
    {
        var interaction = Field(item, "interaction");
        if (!R.Bool(R.record_like(interaction))) throw new InvalidDataException($"Spatial {path}/interaction must be a record.");
        var status = Field(interaction, "status");
        if (status is "unconfigured" or "noninteractive") return;
        if (status is not "ready") throw new InvalidDataException($"Spatial {path}/interaction/status is invalid.");
        if (Field(interaction, "coordinate_space") is not "building-local-meters") throw new InvalidDataException($"Invalid coordinate space: {path}/interaction/coordinate_space");
        if (Field(interaction, "size_space") is not "design-local-meters") throw new InvalidDataException($"Invalid size space: {path}/interaction/size_space");
        Number(Field(item, "model_forward_yaw"), path + "/model_forward_yaw");
        foreach (var field in new[] { "approach", "stand", "face_target", "contact", "area_center", "label_position" })
            _ = Vector(Field(interaction, field), 3, $"{path}/interaction/{field}");
        foreach (var field in new[] { "area_size" })
            if (Vector(Field(interaction, field), 3, $"{path}/interaction/{field}").Any(n => n <= 0))
                throw new InvalidDataException($"Spatial {path}/interaction/{field} must be positive.");
        var shapes = Items(Field(interaction, "collision_shapes"), $"{path}/interaction/collision_shapes");
        var collision = Field(item, "collision");
        if (collision is not bool enabled || enabled != (shapes.Count > 0))
            throw new InvalidDataException($"Spatial {path}/interaction/collision_shapes conflicts with collision.");
        for (var index = 0; index < shapes.Count; index++)
        {
            var shapePath = $"{path}/interaction/collision_shapes/{index}";
            var shape = shapes[index];
            if (Field(shape, "kind") is not "box") throw new InvalidDataException($"Unsupported shape: {shapePath}/kind");
            _ = Vector(Field(shape, "center"), 3, shapePath + "/center");
            if (Vector(Field(shape, "size"), 3, shapePath + "/size").Any(n => n <= 0))
                throw new InvalidDataException($"Spatial {shapePath}/size must be positive.");
        }
        foreach (var field in new[] { "actions", "resources", "state_visuals" })
        {
            var entries = Items(Field(interaction, field), $"{path}/interaction/{field}");
            if (entries.Any(entry => entry is not string { Length: > 0 }))
                throw new InvalidDataException($"Spatial {path}/interaction/{field} must contain nonempty identifiers.");
            if (field == "actions" && entries.Count == 0)
                throw new InvalidDataException($"Spatial {path}/interaction/actions must not be empty.");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var id = (string)entry;
                if (!unique.Add(id)) throw new InvalidDataException($"Duplicate identifier: {path}/interaction/{field}/{id}");
                var definition = catalog.Get("data", id);
                var expectedKind = field == "actions" ? "spatial-action" : field == "resources" ? "spatial-resource" : "spatial-state-visual";
                if (definition is false || Field(Field(definition, "value"), "kind") is not string kind || kind != expectedKind)
                    throw new InvalidDataException($"Invalid typed reference: {path}/interaction/{field}/{id}");
                if (field == "actions")
                {
                    var action = Field(definition, "value");
                    if (Field(action, "status") is not "ready")
                        throw new InvalidDataException($"Action not ready: {path}/interaction/actions/{id}/status");
                    if (Field(action, "method") is not string { Length: > 0 })
                        throw new InvalidDataException($"Action has no method binding: {path}/interaction/actions/{id}/method");
                    if (Field(action, "law_schema") is not string { Length: > 0 })
                        throw new InvalidDataException($"Action has no law schema: {path}/interaction/actions/{id}/law_schema");
                    if (Field(action, "rule_template") is not string templateId || catalog.Get("data", templateId) is false
                        || Field(Field(catalog.Get("data", templateId), "value"), "kind") is not "dv-value-template")
                        throw new InvalidDataException($"Action has no valid rule template: {path}/interaction/actions/{id}/rule_template");
                    var template = Field(catalog.Get("data", templateId), "value");
                    var parameters = Items(Field(template, "parameters"), $"{path}/interaction/actions/{id}/rule_template/parameters");
                    var arguments = Items(Field(action, "arguments"), $"{path}/interaction/actions/{id}/arguments");
                    if (parameters.Count != arguments.Count) throw new InvalidDataException($"Action argument count mismatch: {path}/interaction/actions/{id}/arguments");
                    var targetBound = false;
                    for (var i = 0; i < parameters.Count; i++)
                        if (parameters[i] is string parameter && parameter is "seat" or "object")
                        {
                            targetBound = true;
                            if (arguments[i] is not string target || target != (string)Field(item, "id"))
                                throw new InvalidDataException($"Action target mismatch: {path}/interaction/actions/{id}/arguments/{i}");
                        }
                    if (!targetBound) throw new InvalidDataException($"Action template has no object parameter: {path}/interaction/actions/{id}");
                }
            }
        }
    }

    // Formal assembly must call this gate; a partially declared pack may still
    // be loaded for CLI and content editing, but cannot publish these objects.
    internal static void RequireInteractionReadiness(ContentCatalog catalog, object manifest, object physical, IReadOnlyList<string> objectIds)
    {
        if (objectIds.Count == 0) throw new InvalidDataException("Spatial publication requires an explicit nonempty object manifest.");
        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in objectIds)
            if (string.IsNullOrWhiteSpace(id) || !requested.Add(id)) throw new InvalidDataException("Spatial publication has an invalid or duplicate object id.");
        var found = new HashSet<string>(StringComparer.Ordinal);
        var selected = (string)Field(manifest, "spatial_definition");
        var spatial = Field(catalog.Get("data", selected), "value");
        foreach (var floor in Items(Field(spatial, "floors"), "floors"))
        {
            var floorId = Id(floor, "floor");
            foreach (var item in InteractionTargets(floor))
            {
                var id = Id(item, "interaction target");
                if (!requested.Contains(id)) continue;
                found.Add(id);
                var path = $"{floorId}/interaction-target/{id}";
                ValidateInteraction(catalog, item, path);
                if (Field(Field(item, "interaction"), "status") is not ("ready" or "noninteractive"))
                    throw new InvalidDataException($"Spatial {path}/interaction is not ready for publication.");
                if (Field(Field(item, "interaction"), "status") is "ready")
                {
                    var adoptions = Field(physical, "adoptions");
                    foreach (var actionId in Items(Field(Field(item, "interaction"), "actions"), path + "/interaction/actions"))
                    {
                        var action = Field(catalog.Get("data", (string)actionId), "value");
                        var method = (string)Field(action, "method");
                        var template = Field(catalog.Get("data", (string)Field(action, "rule_template")), "value");
                        var parameters = Items(Field(template, "parameters"), path + "/action/parameters");
                        var arguments = Items(Field(action, "arguments"), path + "/action/arguments");
                        var objects = Field(physical, "objects");
                        for (var i = 0; i < parameters.Count; i++)
                            if (parameters[i] is string parameter && parameter is "actor" or "actor_state" or "seat" or "object")
                            {
                                if (arguments[i] is not string objectId || ReferenceEquals(Field(objects, objectId), Missing))
                                    throw new InvalidDataException($"Action object missing from physical world: {path}/interaction/actions/{actionId}/arguments/{i}");
                            }
                        var adoption = Field(adoptions, method);
                        if (ReferenceEquals(adoption, Missing) || Field(adoption, "mode") is not "enforce" || Field(adoption, "active") is not true
                            || !R.Bool(R.eq(Field(Field(adoption, "law"), "schema"), Field(action, "law_schema"))))
                            throw new InvalidDataException($"Action method not adopted for enforcement with declared schema: {path}/interaction/actions/{actionId}");
                    }
                }
            }
        }
        foreach (var id in requested)
            if (!found.Contains(id)) throw new InvalidDataException($"Unknown spatial publication object: {id}");
    }

    internal static IReadOnlyDictionary<string, object?> ProjectObjects(ContentCatalog catalog, object manifest, object physical, IReadOnlyList<string> objectIds)
    {
        RequireInteractionReadiness(catalog, manifest, physical, objectIds);
        var selected = (string)Field(manifest, "spatial_definition");
        var spatial = Field(catalog.Get("data", selected), "value");
        var index = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var floor in Items(Field(spatial, "floors"), "floors"))
            foreach (var item in InteractionTargets(floor))
                index.Add(Id(item, "interaction target"), item);
        var objects = objectIds.Select(id => HostValues.FromDv(index[id])).ToArray();
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["content_hash"] = catalog.Hash,
            ["spatial_definition"] = selected,
            ["world_datum"] = HostValues.FromDv(ResolveWorldDatum(catalog, manifest, spatial)),
            ["world_datum_ref"] = HostValues.FromDv(Field(spatial, "world_datum_ref")),
            ["objects"] = objects
        });
    }

    internal static IReadOnlyDictionary<string, object?> ProjectFloorSurfaces(ContentCatalog catalog, object manifest, IReadOnlyList<string> pieceIds)
    {
        if (pieceIds.Count == 0) throw new InvalidDataException("Floor publication requires an explicit nonempty piece manifest.");
        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in pieceIds)
            if (string.IsNullOrWhiteSpace(id) || !requested.Add(id)) throw new InvalidDataException("Floor publication has an invalid or duplicate piece id.");
        var selected = (string)Field(manifest, "spatial_definition");
        var spatial = Field(catalog.Get("data", selected), "value");
        var index = new Dictionary<string, object>(StringComparer.Ordinal);
        var materials = Items(Field(spatial, "surface_materials"), "surface_materials").ToDictionary(item => Id(item, "surface_materials"), StringComparer.Ordinal);
        foreach (var floor in Items(Field(spatial, "floors"), "floors"))
            foreach (var piece in Items(Field(floor, "pieces"), "pieces"))
                if (Field(piece, "kind") is "floor") index.Add(Id(piece, "floor piece"), piece);
        foreach (var id in requested)
            if (!index.ContainsKey(id)) throw new InvalidDataException($"Unknown floor publication piece: {id}");
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["content_hash"] = catalog.Hash,
            ["spatial_definition"] = selected,
            ["world_datum"] = HostValues.FromDv(ResolveWorldDatum(catalog, manifest, spatial)),
            ["world_datum_ref"] = HostValues.FromDv(Field(spatial, "world_datum_ref")),
            ["pieces"] = pieceIds.Select(id => HostValues.FromDv(index[id])).ToArray(),
            ["materials"] = pieceIds.Select(id => (string)Field(index[id], "material_ref")).Distinct(StringComparer.Ordinal)
                .ToDictionary(id => id, id => HostValues.FromDv(materials[id]), StringComparer.Ordinal)
        });
    }

    internal static void Validate(ContentCatalog catalog, object manifest)
    {
        ValidateDeclaredActions(catalog, manifest);
        if (Field(manifest, "spatial_definition") is not string { Length: > 0 } selected)
            throw new InvalidDataException("Campaign pack needs one spatial_definition.");
        if (!Items(Field(manifest, "definitions"), "manifest/definitions").Any(x => x is string id && id == selected))
            throw new InvalidDataException($"Selected spatial definition is absent from manifest: {selected}");
        var definition = catalog.Get("data", selected);
        if (definition is false) throw new InvalidDataException($"Missing selected spatial definition: {selected}");
        var spatial = Field(definition, "value");
        var executionProfile = Field(spatial, "host_spatial_execution_profile");
        if (!ReferenceEquals(executionProfile, Missing))
        {
            foreach (var key in new[] { "speed_mps", "arrival_tolerance_m", "stall_timeout_s", "minimum_progress_m", "gravity_mps2", "turn_speed_degrees_s", "turn_tolerance_degrees" })
                if (Number(Field(executionProfile, key), $"{selected}/host_spatial_execution_profile/{key}") <= 0)
                    throw new InvalidDataException($"Invalid native interaction execution parameter: {key}");
            if (Number(Field(executionProfile, "foot_offset_m"), $"{selected}/host_spatial_execution_profile/foot_offset_m") < 0
                || Number(Field(executionProfile, "turn_tolerance_degrees"), $"{selected}/host_spatial_execution_profile/turn_tolerance_degrees") >= 180)
                throw new InvalidDataException("Invalid native interaction foot offset or turning tolerance.");
            if (Field(executionProfile, "source") is not string { Length: > 0 })
                throw new InvalidDataException("Native interaction execution parameters need an explicit source.");
        }
        if (Field(spatial, "version") is not "configurable-building-v1")
            throw new InvalidDataException($"Unsupported spatial version: {selected}/version");
        if (Field(spatial, "units") is not "meters; X east, Y north, Z up")
            throw new InvalidDataException($"Unsupported spatial units and axes: {selected}/units");
        if (Field(spatial, "coordinate_space") is not "building-local")
            throw new InvalidDataException($"Unsupported spatial coordinate space: {selected}/coordinate_space");
        var actorProfile = Field(spatial, "host_actor_profile");
        if (Number(Field(actorProfile, "nominal_actor_height_m"), $"{selected}/host_actor_profile/nominal_actor_height_m") <= 0
            || Number(Field(actorProfile, "collision_radius_m"), $"{selected}/host_actor_profile/collision_radius_m") <= 0
            || Field(actorProfile, "source") is not string { Length: > 0 })
            throw new InvalidDataException($"Invalid host actor profile: {selected}/host_actor_profile");
        var datum = Vector(ResolveWorldDatum(catalog, manifest, spatial), 3, $"{selected}/world_datum_ref");
        var probes = Items(Field(spatial, "coordinate_probes"), $"{selected}/coordinate_probes");
        if (probes.Count < 4) throw new InvalidDataException($"Spatial {selected}/coordinate_probes needs at least four references.");
        var probeIds = new HashSet<string>(StringComparer.Ordinal);
        var localPoints = new List<double[]>();
        foreach (var probe in probes)
        {
            var id = Id(probe, "coordinate_probes");
            if (!probeIds.Add(id)) throw new InvalidDataException($"Duplicate coordinate probe: {id}");
            var local = Vector(Field(probe, "local"), 3, $"coordinate_probes/{id}/local");
            var world = Vector(Field(probe, "world"), 3, $"coordinate_probes/{id}/world");
            var godotWorld = Vector(Field(probe, "godot_world"), 3, $"coordinate_probes/{id}/godot_world");
            localPoints.Add(local);
            // Validate the authored reference values, not only their array shape.
            // Design: X east, Y north, Z up. Godot: X east, Y up, Z south.
            for (var axis = 0; axis < 3; axis++)
                if (Math.Abs(world[axis] - (local[axis] + datum[axis])) > 0.001)
                    throw new InvalidDataException($"Coordinate probe world datum mismatch: coordinate_probes/{id}/world/{axis}");
            var expected = new[] { world[0], world[2], -world[1] };
            for (var axis = 0; axis < 3; axis++)
                if (Math.Abs(godotWorld[axis] - expected[axis]) > 0.001)
                    throw new InvalidDataException($"Coordinate probe Godot axes mismatch: coordinate_probes/{id}/godot_world/{axis}");
            if (Field(probe, "source") is not string { Length: > 0 }) throw new InvalidDataException($"Missing coordinate probe source: {id}");
        }
        var noncollinear = false;
        for (var i = 0; i < localPoints.Count && !noncollinear; i++)
            for (var j = i + 1; j < localPoints.Count && !noncollinear; j++)
                for (var k = j + 1; k < localPoints.Count; k++)
                {
                    var a = localPoints[i]; var b = localPoints[j]; var c = localPoints[k];
                    if (Math.Abs((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])) >= 0.000001)
                    {
                        noncollinear = true;
                        break;
                    }
                }
        if (!noncollinear || !localPoints.Any(point => Math.Abs(point[2]-localPoints[0][2]) > 0.000001))
            throw new InvalidDataException($"Spatial {selected}/coordinate_probes must include noncollinear points and a different elevation.");
        var floors = Items(Field(spatial, "floors"), "floors");
        if (floors.Count == 0) throw new InvalidDataException("Spatial definition has no floors.");
        var floorIds = new HashSet<string>(StringComparer.Ordinal);
        var spatialIds = new HashSet<string>(StringComparer.Ordinal);
        var materialIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var material in Items(Field(spatial, "surface_materials"), "surface_materials"))
        {
            var id = Id(material, "surface_materials");
            var path = $"surface_materials/{id}";
            if (!spatialIds.Add(id) || !materialIds.Add(id)) throw new InvalidDataException($"Duplicate {path}");
            if (Field(material, "shader") is not "framework_surface" || Field(material, "floor_surface") is not true || Field(material, "status") is not ("provisional-study-source" or "ready"))
                throw new InvalidDataException($"Unsupported {path}/shader or status");
            if (Field(material, "base_color") is not string color || color.Length != 6 || !color.All(Uri.IsHexDigit)) throw new InvalidDataException($"Invalid {path}/base_color");
            var roughness = Number(Field(material, "roughness"), path + "/roughness");
            if (roughness < 0 || roughness > 1 || Number(Field(material, "texture_scale_m"), path + "/texture_scale_m") <= 0) throw new InvalidDataException($"Invalid {path} parameters");
        }
        var knownFloorIds = floors.Select(floor => Id(floor, "floor")).ToHashSet(StringComparer.Ordinal);
        var stairs = Items(Field(spatial, "effective_stairs"), "effective_stairs");
        var stairsById = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var stair in stairs)
        {
            var id = Id(stair, "effective_stairs");
            if (!stairsById.TryAdd(id, stair)) throw new InvalidDataException($"Duplicate stair id: effective_stairs/{id}");
        }
        var voidValues = Field(spatial, "voids");
        var voids = ReferenceEquals(voidValues, Missing) ? new List<object>() : Items(voidValues, "voids");
        var voidRects = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var voidDestinations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var value in voids)
        {
            var id = Id(value, "voids");
            var path = $"voids/{id}";
            if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {path}");
            if (Field(value, "stair_ref") is not string stairId || !stairsById.TryGetValue(stairId, out var stair))
                throw new InvalidDataException($"Invalid stair reference: {path}/stair_ref");
            var segments = Items(Field(stair, "segments"), $"effective_stairs/{stairId}/segments");
            var indexValue = Number(Field(value, "segment_index"), path + "/segment_index");
            if (indexValue < 0 || indexValue != Math.Truncate(indexValue) || indexValue >= segments.Count)
                throw new InvalidDataException($"Invalid segment index: {path}/segment_index");
            var segment = segments[(int)indexValue];
            if (Field(value, "source_field") is not "floor_opening_bbox") throw new InvalidDataException($"Unsupported void source: {path}/source_field");
            var rect = Vector(Field(segment, "floor_opening_bbox"), 4, $"effective_stairs/{stairId}/segments/{(int)indexValue}/floor_opening_bbox");
            if (rect[2] <= rect[0] || rect[3] <= rect[1]) throw new InvalidDataException($"Invalid void extent: {path}");
            if (Field(value, "from_floor") is not string from || !knownFloorIds.Contains(from) || Field(segment, "from") is not string actualFrom || actualFrom != from
                || Field(value, "to_floor") is not string to || !knownFloorIds.Contains(to) || Field(segment, "to") is not string actualTo || actualTo != to)
                throw new InvalidDataException($"Invalid void floor references: {path}");
            if (Field(value, "diagnostic_control_floor_piece_ref") is not string controlId || string.IsNullOrEmpty(controlId))
                throw new InvalidDataException($"Missing floor control reference: {path}/diagnostic_control_floor_piece_ref");
            var targetFloor = floors.Single(f => Id(f, "floor") == to);
            var control = Items(Field(targetFloor, "pieces"), $"{to}/pieces").SingleOrDefault(piece => Field(piece, "id") is string pieceId && pieceId == controlId);
            if (control is null || Field(control, "kind") is not "floor" || ReferenceEquals(Field(control, "rect"), Missing))
                throw new InvalidDataException($"Invalid floor control reference: {path}/diagnostic_control_floor_piece_ref");
            Rect(Field(control, "rect"), $"{to}/pieces/{controlId}/rect");
            if (Math.Abs(Number(Field(control, "z"), $"{to}/pieces/{controlId}/z") + Number(Field(control, "height"), $"{to}/pieces/{controlId}/height") - Number(Field(targetFloor, "z"), $"{to}/z")) > 0.001)
                throw new InvalidDataException($"Floor control elevation mismatch: {path}/diagnostic_control_floor_piece_ref");
            voidRects.Add(id, rect);
            voidDestinations.Add(id, to);
        }
        foreach (var floor in floors)
        {
            var floorId = Id(floor, "floor");
            if (!floorIds.Add(floorId) || !spatialIds.Add(floorId)) throw new InvalidDataException($"Duplicate floor id: {floorId}");
            var floorElevation = Number(Field(floor, "z"), $"{floorId}/z");
            var rooms = Items(Field(floor, "rooms"), $"{floorId}/rooms");
            var roomIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in rooms)
            {
                var id = Id(room, $"{floorId}/room");
                if (!roomIds.Add(id) || !spatialIds.Add(id)) throw new InvalidDataException($"Duplicate room id: {floorId}/{id}");
                if (Field(room, "name") is not string { Length: > 0 }) throw new InvalidDataException($"Room needs a name: {floorId}/{id}/name");
                Rect(Field(room, "bbox"), $"{floorId}/rooms/{id}/bbox");
            }
            foreach (var category in new[] { "pieces", "colliders", "openings", "furniture" })
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in Items(Field(floor, category), $"{floorId}/{category}"))
                {
                    var id = Id(item, $"{floorId}/{category}");
                    if (!ids.Add(id) || !spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {floorId}/{category}/{id}");
                    var path = $"{floorId}/{category}/{id}";
                    if (category == "pieces" && Field(item, "kind") is "floor" && (Field(item, "material_ref") is not string materialId || !materialIds.Contains(materialId)))
                        throw new InvalidDataException($"Invalid {path}/material_ref");
                    var roomValue = Field(item, "room");
                    if (category == "furniture" && ReferenceEquals(roomValue, Missing))
                        throw new InvalidDataException($"Spatial {path}/room is required.");
                    if (!ReferenceEquals(roomValue, Missing) && (roomValue is not string room || room.Length == 0 || !roomIds.Contains(room)))
                        throw new InvalidDataException($"Invalid room reference: {path}/room");
                    if (category != "furniture" && ReferenceEquals(Field(item, "rect"), Missing) && ReferenceEquals(Field(item, "points"), Missing))
                        throw new InvalidDataException($"Spatial geometry missing: {floorId}/{category}/{id}");
                    if (category == "furniture")
                    {
                        _ = Vector(Field(item, "position"), 3, path + "/position");
                        var size = Vector(Field(item, "size"), 3, path + "/size");
                        if (size.Any(n => n <= 0)) throw new InvalidDataException($"Spatial {path}/size must be positive.");
                        Number(Field(item, "yaw"), path + "/yaw");
                        if (Field(item, "kind") is not string { Length: > 0 }) throw new InvalidDataException($"Spatial {path}/kind is required.");
                        if (Field(item, "name") is not string { Length: > 0 }) throw new InvalidDataException($"Spatial {path}/name is required.");
                        if (Field(item, "collision") is not bool) throw new InvalidDataException($"Spatial {path}/collision must be boolean.");
                        if (Field(item, "assembly") is "valve-linkage-v1")
                        {
                            var assembly = Field(item, "assembly_spec");
                            foreach (var rod in Items(Field(assembly, "rods"), path + "/assembly_spec/rods"))
                            {
                                var start = Vector(Field(rod, "from"), 3, path);
                                var end = Vector(Field(rod, "to"), 3, path);
                                if (start.SequenceEqual(end) || Number(Field(rod, "radius_m"), path) <= 0)
                                    throw new InvalidDataException($"Invalid {path}/assembly_spec/rod");
                            }
                            foreach (var plate in Items(Field(assembly, "plates"), path + "/assembly_spec/plates"))
                            {
                                _ = Vector(Field(plate, "center"), 3, path);
                                if (Vector(Field(plate, "size"), 3, path).Any(n => n <= 0)) throw new InvalidDataException($"Invalid {path}/assembly_spec/plate");
                            }
                            foreach (var wheel in Items(Field(assembly, "wheels"), path + "/assembly_spec/wheels"))
                            {
                                _ = Vector(Field(wheel, "center"), 3, path);
                                var axis = Vector(Field(wheel, "axis"), 3, path);
                                var tube = Number(Field(wheel, "tube_radius_m"), path);
                                if (axis.All(n => n == 0) || tube <= 0 || Number(Field(wheel, "radius_m"), path) <= tube)
                                    throw new InvalidDataException($"Invalid {path}/assembly_spec/wheel");
                            }
                        }
                        ValidateInteraction(catalog, item, path);
                    }
                    else
                    {
                        var rect = Field(item, "rect");
                        if (!ReferenceEquals(rect, Missing))
                        {
                            Rect(rect, path + "/rect");
                            if (category != "openings")
                            {
                                Number(Field(item, "z"), path + "/z");
                                if (Number(Field(item, "height"), path + "/height") <= 0) throw new InvalidDataException($"Spatial {path}/height must be positive.");
                            }
                        }
                        var points = Field(item, "points");
                        if (!ReferenceEquals(points, Missing))
                        {
                            var vertices = Items(points, path + "/points");
                            if (vertices.Count < 6 || vertices.Count % 2 != 0) throw new InvalidDataException($"Spatial {path}/points needs paired prism vertices.");
                            for (var i = 0; i < vertices.Count; i++) _ = Vector(vertices[i], 3, $"{path}/points/{i}");
                        }
                        if (category == "openings" && Number(Field(item, "top"), path + "/top") <= Number(Field(item, "bottom"), path + "/bottom"))
                            throw new InvalidDataException($"Spatial {path}/top must exceed bottom.");
                    }
                }
            }
            var furniture = Items(Field(floor, "furniture"), $"{floorId}/furniture");
            var colliders = Items(Field(floor, "colliders"), $"{floorId}/colliders");
            var pieces = Items(Field(floor, "pieces"), $"{floorId}/pieces");
            var piecesById = pieces.ToDictionary(item => Id(item, $"{floorId}/pieces"), StringComparer.Ordinal);
            var collidersById = colliders.ToDictionary(item => Id(item, $"{floorId}/colliders"), StringComparer.Ordinal);
            foreach (var piece in pieces)
            {
                if (Field(piece, "kind") is not "floor") continue;
                var id = Id(piece, $"{floorId}/pieces");
                if (Field(piece, "collider_ref") is not string collisionId || !collidersById.TryGetValue(collisionId, out var collision)
                    || Field(collision, "piece_ref") is not string back || back != id || Field(collision, "kind") is not "floor")
                    throw new InvalidDataException($"Invalid floor collision binding: {floorId}/pieces/{id}/collider_ref");
                if (!R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(collision, "rect"))) || !R.Bool(R.eq(Field(piece, "z"), Field(collision, "z")))
                    || !R.Bool(R.eq(Field(piece, "height"), Field(collision, "height"))))
                    throw new InvalidDataException($"Floor geometry differs from collision: {floorId}/pieces/{id}");
                if (Math.Abs(Number(Field(piece, "z"), $"{floorId}/pieces/{id}/z") + Number(Field(piece, "height"), $"{floorId}/pieces/{id}/height") - floorElevation) > 0.000001)
                    throw new InvalidDataException($"Floor top differs from level: {floorId}/pieces/{id}/height");
            }
            foreach (var collision in colliders)
            {
                if (Field(collision, "kind") is not "floor") continue;
                var id = Id(collision, $"{floorId}/colliders");
                if (Field(collision, "piece_ref") is not string pieceId || !piecesById.TryGetValue(pieceId, out var piece)
                    || Field(piece, "collider_ref") is not string back || back != id)
                    throw new InvalidDataException($"Invalid floor piece binding: {floorId}/colliders/{id}/piece_ref");
            }
            var voidRefs = Field(floor, "void_refs");
            if (!ReferenceEquals(voidRefs, Missing))
                foreach (var reference in Items(voidRefs, $"{floorId}/void_refs"))
                {
                    if (reference is not string voidId || !voidRects.TryGetValue(voidId, out var hole) || voidDestinations[voidId] != floorId)
                        throw new InvalidDataException($"Invalid floor void reference: {floorId}/void_refs");
                    foreach (var piece in pieces)
                    {
                        if (Field(piece, "kind") is not "floor") continue;
                        var r = Vector(Field(piece, "rect"), 4, $"{floorId}/pieces/{Id(piece, "piece")}/rect");
                        if (Math.Min(r[2], hole[2]) - Math.Max(r[0], hole[0]) > 0.000001 && Math.Min(r[3], hole[3]) - Math.Max(r[1], hole[1]) > 0.000001)
                            throw new InvalidDataException($"Floor overlaps declared void: {floorId}/pieces/{Id(piece, "piece")}");
                    }
                }
            var openingById = Items(Field(floor, "openings"), $"{floorId}/openings").ToDictionary(item => Id(item, $"{floorId}/openings"), StringComparer.Ordinal);
            var wallsValue = Field(floor, "wall_segments");
            var usedPieces = new HashSet<string>(StringComparer.Ordinal);
            var usedColliders = new HashSet<string>(StringComparer.Ordinal);
            var usedOpenings = new HashSet<string>(StringComparer.Ordinal);
            var wallIds = new HashSet<string>(StringComparer.Ordinal);
            if (!ReferenceEquals(wallsValue, Missing))
                foreach (var wall in Items(wallsValue, $"{floorId}/wall_segments"))
                {
                    var id = Id(wall, $"{floorId}/wall_segments");
                    var path = $"{floorId}/wall_segments/{id}";
                    var declaredGeometry = IsDeclaredGeometry(wall, path);
                    if (!spatialIds.Add(id) || !wallIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {path}");
                    var start = Vector(Field(wall, "start"), 3, path + "/start");
                    var end = Vector(Field(wall, "end"), 3, path + "/end");
                    var length = Math.Sqrt(Math.Pow(end[0] - start[0], 2) + Math.Pow(end[1] - start[1], 2));
                    if (length <= 0 || Math.Abs(end[2] - start[2]) > 0.000001) throw new InvalidDataException($"Invalid wall line: {path}");
                    if (Number(Field(wall, "height"), path + "/height") <= 0 || Number(Field(wall, "thickness"), path + "/thickness") <= 0)
                        throw new InvalidDataException($"Invalid wall dimensions: {path}");
                    var startCut = Vector(Field(wall, "start_cut"), 2, path + "/start_cut");
                    var endCut = Vector(Field(wall, "end_cut"), 2, path + "/end_cut");
                    if (declaredGeometry && Math.Max(startCut[0], startCut[1]) >= length + Math.Min(endCut[0], endCut[1]))
                        throw new InvalidDataException($"Wall end cuts overlap: {path}");
                    var declaredApertures = new List<(double From, double To, double Bottom, double Top)>();
                    foreach (var reference in Items(Field(wall, "openings"), path + "/openings"))
                    {
                        if (reference is not string openingId || !usedOpenings.Add(openingId) || !openingById.TryGetValue(openingId, out var opening)
                            || Field(opening, "wall_ref") is not string parent || parent != id)
                            throw new InvalidDataException($"Invalid opening reference: {path}/openings");
                        var from = Number(Field(opening, "from"), $"{floorId}/openings/{openingId}/from");
                        var to = Number(Field(opening, "to"), $"{floorId}/openings/{openingId}/to");
                        if (from < 0 || to <= from || to > length) throw new InvalidDataException($"Opening outside wall: {floorId}/openings/{openingId}");
                        var bottom = Number(Field(opening, "bottom"), $"{floorId}/openings/{openingId}/bottom");
                        var top = Number(Field(opening, "top"), $"{floorId}/openings/{openingId}/top");
                        if (bottom < start[2] || top > start[2] + Number(Field(wall, "height"), path + "/height"))
                            throw new InvalidDataException($"Opening outside wall height: {floorId}/openings/{openingId}");
                        if (declaredGeometry)
                        {
                            if (from <= Math.Max(startCut[0], startCut[1]) + 0.000001 || to >= length + Math.Min(endCut[0], endCut[1]) - 0.000001)
                                throw new InvalidDataException($"Opening intersects wall end cut: {floorId}/openings/{openingId}");
                            if (declaredApertures.Any(other => from < other.To - 0.000001 && to > other.From + 0.000001
                                && bottom < other.Top - 0.000001 && top > other.Bottom + 0.000001))
                                throw new InvalidDataException($"Wall openings overlap: {floorId}/openings/{openingId}");
                            declaredApertures.Add((from, to, bottom, top));
                        }
                        var openingPath = $"{floorId}/openings/{openingId}";
                        var frame = Field(opening, "frame");
                        var primaryDoor = Field(opening, "door");
                        var extraDoors = Field(opening, "additional_doors");
                        var doors = new List<object>();
                        if (!ReferenceEquals(primaryDoor, Missing)) doors.Add(primaryDoor);
                        if (!ReferenceEquals(extraDoors, Missing))
                        {
                            if (ReferenceEquals(primaryDoor, Missing)) throw new InvalidDataException($"Additional doors require primary door: {openingPath}");
                            doors.AddRange(Items(extraDoors, openingPath + "/additional_doors"));
                        }
                        if (ReferenceEquals(frame, Missing) && doors.Count > 0)
                            throw new InvalidDataException($"Door requires frame: {openingPath}/door");
                        if (!ReferenceEquals(frame, Missing))
                        {
                            var frameId = Id(frame, openingPath + "/frame");
                            if (!spatialIds.Add(frameId)) throw new InvalidDataException($"Duplicate frame id: {openingPath}/frame");
                            var structuralWidth = Number(Field(opening, "structural_width"), openingPath + "/structural_width");
                            var structuralHeight = Number(Field(opening, "structural_height"), openingPath + "/structural_height");
                            var thickness = Number(Field(frame, "thickness"), openingPath + "/frame/thickness");
                            var netWidth = Number(Field(frame, "net_width"), openingPath + "/frame/net_width");
                            var netHeight = Number(Field(frame, "net_height"), openingPath + "/frame/net_height");
                            var circular=Field(frame,"profile") is "circular_blast";
                            if (thickness <= 0 || netWidth <= 0 || netHeight <= 0 || Math.Abs(structuralWidth - (to - from)) > 0.000001
                                || Math.Abs(structuralHeight - (top - bottom)) > 0.000001 || Math.Abs(netWidth - (structuralWidth - 2 * thickness)) > 0.000001
                                || (!circular&&Math.Abs(netHeight - (structuralHeight - thickness)) > 0.000001))
                                throw new InvalidDataException($"Inconsistent frame dimensions: {openingPath}/frame");
                            if(circular&&(Math.Abs(netHeight-netWidth)>.000001
                                ||Math.Abs(Number(Field(frame,"clear_diameter_m"),openingPath+"/frame/clear_diameter_m")-netWidth)>.000001
                                ||netHeight+2*thickness>structuralHeight+.000001))
                                throw new InvalidDataException($"Inconsistent circular frame dimensions: {openingPath}/frame");
                            _ = Vector(Field(frame, "origin_design"), 3, openingPath + "/frame/origin_design");
                            if (Number(Field(frame, "depth"), openingPath + "/frame/depth") <= 0) throw new InvalidDataException($"Invalid frame depth: {openingPath}/frame/depth");
                            Number(Field(frame, "orientation_yaw"), openingPath + "/frame/orientation_yaw");
                            Number(Field(frame, "model_forward_yaw"), openingPath + "/frame/model_forward_yaw");
                            double totalLeafWidth = 0;
                            foreach (var door in doors)
                            {
                            var doorId = Id(door, openingPath + "/door");
                            if (!spatialIds.Add(doorId)) throw new InvalidDataException($"Duplicate door id: {openingPath}/door");
                            _ = Vector(Field(door, "hinge_design"), 3, openingPath + "/door/hinge_design");
                            var leafWidth = Number(Field(door, "width"), openingPath + "/door/width");
                            var leafHeight = Number(Field(door, "height"), openingPath + "/door/height");
                            totalLeafWidth += leafWidth;
                            if (leafWidth <= 0 || leafWidth > (circular?structuralWidth:netWidth) || leafHeight <= 0 || leafHeight > (circular?structuralHeight:netHeight) || Number(Field(door, "thickness"), openingPath + "/door/thickness") <= 0
                                ||circular&&(Field(door,"profile") is not "circular_blast"||Math.Abs(leafWidth-leafHeight)>.000001||leafWidth<netWidth))
                                throw new InvalidDataException($"Invalid leaf dimensions: {openingPath}/door");
                            Number(Field(door, "orientation_yaw"), openingPath + "/door/orientation_yaw");
                            Number(Field(door, "model_forward_yaw"), openingPath + "/door/model_forward_yaw");
                            if (Field(door, "angle_convention") is not "design-clockwise-degrees") throw new InvalidDataException($"Invalid door angle convention: {openingPath}/door/angle_convention");
                            var minAngle = Number(Field(door, "angle_min"), openingPath + "/door/angle_min");
                            var maxAngle = Number(Field(door, "angle_max"), openingPath + "/door/angle_max");
                            if (minAngle > 0 || maxAngle < 0 || minAngle >= maxAngle) throw new InvalidDataException($"Invalid leaf angle: {openingPath}/door");
                            var initialValue = Field(door, "initial_angle");
                            if (!ReferenceEquals(initialValue, Missing))
                            {
                                var initialAngle = Number(initialValue, openingPath + "/door/initial_angle");
                                if (initialAngle < minAngle || initialAngle > maxAngle) throw new InvalidDataException($"Invalid initial leaf angle: {openingPath}/door");
                            }
                            if (Number(Field(door, "motion_duration_seconds"), openingPath + "/door/motion_duration_seconds") <= 0)
                                throw new InvalidDataException($"Invalid door motion duration: {openingPath}/door/motion_duration_seconds");
                            var openAngle = Number(Field(door, "open_angle"), openingPath + "/door/open_angle");
                            if (openAngle < minAngle || openAngle > maxAngle || Number(Field(door, "review_control_radius_m"), openingPath + "/door/review_control_radius_m") <= 0)
                                throw new InvalidDataException($"Invalid review opening: {openingPath}/door");
                            if (Field(door, "opens_into") is not string roomId || (!roomIds.Contains(roomId)
                                &&!Items(Field(spatial,"semantic_regions"),"semantic_regions").Any(region=>Field(region,"id") is string regionId&&regionId==roomId&&Field(region,"floor") is string regionFloor&&regionFloor==floorId))) throw new InvalidDataException($"Invalid open-side room: {openingPath}/door/opens_into");
                            var measurement = Field(opening, "clearance_measurement");
                            _ = Vector(Field(measurement, "center_design"), 3, openingPath + "/clearance_measurement/center_design");
                            var lateral = Vector(Field(measurement, "lateral_design"), 3, openingPath + "/clearance_measurement/lateral_design");
                            if (Math.Abs(lateral[2]) > 0.000001 || Math.Sqrt(lateral[0]*lateral[0] + lateral[1]*lateral[1]) <= 0.000001)
                                throw new InvalidDataException($"Invalid measurement direction: {openingPath}/clearance_measurement/lateral_design");
                            var heights = Items(Field(measurement, "heights_m"), openingPath + "/clearance_measurement/heights_m");
                            if (heights.Count == 0 || heights.Any(height => Number(height, openingPath + "/clearance_measurement/heights_m") <= 0 || Number(height, openingPath + "/clearance_measurement/heights_m") >= netHeight))
                                throw new InvalidDataException($"Invalid measurement heights: {openingPath}/clearance_measurement/heights_m");
                            if (Number(Field(measurement, "ray_extent_m"), openingPath + "/clearance_measurement/ray_extent_m") <= netWidth/2
                                || Number(Field(measurement, "tolerance_m"), openingPath + "/clearance_measurement/tolerance_m") <= 0)
                                throw new InvalidDataException($"Invalid measurement bounds: {openingPath}/clearance_measurement");
                            var replacedDoorPieces = new List<object>();
                            foreach (var oldReference in Items(Field(door, "replaces_pieces"), openingPath + "/door/replaces_pieces"))
                            {
                                if (oldReference is not string pieceId || !usedPieces.Add(pieceId) || !piecesById.TryGetValue(pieceId, out var oldPiece)
                                    || !(Field(oldPiece, "kind") is "door_panel" or "door_handle"
                                         || circular && Field(oldPiece, "kind") is "gate_shell" or "gate_rotor"))
                                    throw new InvalidDataException($"Invalid door replacement: {openingPath}/door/replaces_pieces");
                                replacedDoorPieces.Add(oldPiece);
                            }
                            foreach (var oldReference in Items(Field(door, "replaces_colliders"), openingPath + "/door/replaces_colliders"))
                            {
                                if (oldReference is not string colliderId || !usedColliders.Add(colliderId) || !collidersById.TryGetValue(colliderId, out var oldCollider)
                                    || !(Field(oldCollider, "kind") is "door_panel"
                                         || circular && Field(oldCollider, "kind") is "gate_shell" or "gate_rotor")
                                    || replacedDoorPieces.Count(piece => R.Bool(R.eq(Field(piece, "kind"), Field(oldCollider, "kind")))
                                        && (Field(oldCollider,"shape") is "convex"
                                            ? !ReferenceEquals(Field(piece,"points"),Missing)
                                              && R.Bool(DVKernel.F_dv_same(Field(piece,"points"),Field(oldCollider,"points")))
                                            : R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(oldCollider, "rect")))
                                              && R.Bool(R.eq(Field(piece, "z"), Field(oldCollider, "z")))
                                              && R.Bool(R.eq(Field(piece, "height"), Field(oldCollider, "height"))))) != 1)
                                    throw new InvalidDataException($"Invalid door collision replacement: {openingPath}/door/replaces_colliders");
                            }
                            }
                            if (totalLeafWidth > (circular?structuralWidth:netWidth) + 0.000001)
                                throw new InvalidDataException($"Door leaves exceed frame width: {openingPath}/door");
                        }
                        var rect = Vector(Field(opening, "rect"), 4, $"{floorId}/openings/{openingId}/rect");
                        if (Math.Abs(end[0] - start[0]) < 0.000001 || Math.Abs(end[1] - start[1]) < 0.000001)
                        {
                            var vertical = Math.Abs(end[0] - start[0]) < 0.000001;
                            var axis = vertical ? 1 : 0;
                            var cross = 1 - axis;
                            var sign = Math.Sign(end[axis] - start[axis]);
                            var x1 = (rect[axis] - start[axis]) * sign;
                            var x2 = (rect[axis + 2] - start[axis]) * sign;
                            if (start[cross] < rect[cross] || start[cross] > rect[cross + 2]
                                || Math.Abs(Math.Min(x1, x2) - from) > 0.000001 || Math.Abs(Math.Max(x1, x2) - to) > 0.000001)
                                throw new InvalidDataException($"Opening geometry disagrees with wall: {floorId}/openings/{openingId}/rect");
                        }
                    }
                    var replaced = new List<object>();
                    foreach (var reference in Items(Field(wall, "replaces_pieces"), path + "/replaces_pieces"))
                    {
                        if (reference is not string pieceId || !usedPieces.Add(pieceId) || !piecesById.TryGetValue(pieceId, out var piece)
                            || Field(piece, "kind") is not string kind || !kind.StartsWith("wall_", StringComparison.Ordinal))
                            throw new InvalidDataException($"Invalid replaced piece reference: {path}/replaces_pieces");
                        replaced.Add(piece);
                    }
                    var replacedBodies = new List<object>();
                    foreach (var reference in Items(Field(wall, "replaces_colliders"), path + "/replaces_colliders"))
                    {
                        if (reference is not string colliderId || !usedColliders.Add(colliderId) || !collidersById.TryGetValue(colliderId, out var body)
                            || Field(body, "kind") is not string kind || !kind.StartsWith("wall_", StringComparison.Ordinal))
                            throw new InvalidDataException($"Invalid replaced collision reference: {path}/replaces_colliders");
                        replacedBodies.Add(body);
                    }
                    if (declaredGeometry && (replaced.Count != 0 || replacedBodies.Count != 0))
                        throw new InvalidDataException($"Declared wall must not replace legacy geometry: {path}");
                    if (!declaredGeometry && (replaced.Count == 0 || replaced.Count != replacedBodies.Count))
                        throw new InvalidDataException($"Wall replacement count mismatch: {path}");
                    foreach (var piece in replaced)
                        if (replacedBodies.Count(body => R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(body, "rect")))
                            && R.Bool(R.eq(Field(piece, "z"), Field(body, "z"))) && R.Bool(R.eq(Field(piece, "height"), Field(body, "height")))) != 1)
                            throw new InvalidDataException($"Wall replacement geometry mismatch: {path}/replaces_colliders");
                    var splitsValue = Field(wall, "source_splits");
                    if (declaredGeometry && !ReferenceEquals(splitsValue, Missing)
                        && Items(splitsValue, path + "/source_splits").Count != 0)
                        throw new InvalidDataException($"Declared wall cannot split legacy geometry: {path}/source_splits");
                    if (!declaredGeometry && !ReferenceEquals(splitsValue, Missing))
                    {
                        var splits = Items(splitsValue, path + "/source_splits");
                        if (splits.Count != replaced.Count) throw new InvalidDataException($"Split source count mismatch: {path}/source_splits");
                        var horizontal = Math.Abs(end[1] - start[1]) < 0.000001;
                        var vertical = Math.Abs(end[0] - start[0]) < 0.000001;
                        if (horizontal == vertical) throw new InvalidDataException($"Split wall must be axis aligned: {path}");
                        var cross = horizontal ? 1 : 0;
                        var cutLow = start[cross] - Number(Field(wall, "thickness"), path + "/thickness") / 2;
                        var cutHigh = start[cross] + Number(Field(wall, "thickness"), path + "/thickness") / 2;
                        var seenSources = new HashSet<string>(StringComparer.Ordinal);
                        var seenResiduals = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var split in splits)
                        {
                            if (Field(split, "old_piece_id") is not string oldId || !seenSources.Add(oldId) || !piecesById.TryGetValue(oldId, out var oldPiece)
                                || !replaced.Contains(oldPiece) || Field(split, "old_collider_id") is not string oldBodyId || !collidersById.TryGetValue(oldBodyId, out var oldBody) || !replacedBodies.Contains(oldBody))
                                throw new InvalidDataException($"Invalid split source: {path}/source_splits");
                            var oldRect = Vector(Field(oldPiece, "rect"), 4, path + "/source_splits/old_rect");
                            if (!R.Bool(DVKernel.F_dv_same(Field(oldPiece, "rect"), Field(oldBody, "rect"))) || !R.Bool(R.eq(Field(oldPiece, "z"), Field(oldBody, "z"))) || !R.Bool(R.eq(Field(oldPiece, "height"), Field(oldBody, "height")))
                                || oldRect[cross] >= cutLow || oldRect[cross + 2] <= cutHigh)
                                throw new InvalidDataException($"Invalid split source geometry: {path}/source_splits/{oldId}");
                            var pieceRefs = Items(Field(split, "residual_piece_ids"), path + "/source_splits/residual_piece_ids");
                            var bodyRefs = Items(Field(split, "residual_collider_ids"), path + "/source_splits/residual_collider_ids");
                            if (pieceRefs.Count != 2 || bodyRefs.Count != 2) throw new InvalidDataException($"Invalid split residual count: {path}/source_splits/{oldId}");
                            for (var i = 0; i < 2; i++)
                            {
                                if (pieceRefs[i] is not string newId || !seenResiduals.Add(newId) || !piecesById.TryGetValue(newId, out var piece)
                                    || bodyRefs[i] is not string newBodyId || !seenResiduals.Add(newBodyId) || !collidersById.TryGetValue(newBodyId, out var body)
                                    || Field(piece, "collider_ref") is not string pieceBody || pieceBody != newBodyId || Field(body, "piece_ref") is not string bodyPiece || bodyPiece != newId
                                    || !R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(body, "rect"))) || !R.Bool(R.eq(Field(piece, "z"), Field(oldPiece, "z")))
                                    || !R.Bool(R.eq(Field(piece, "height"), Field(oldPiece, "height"))) || !R.Bool(R.eq(Field(body, "z"), Field(oldPiece, "z"))) || !R.Bool(R.eq(Field(body, "height"), Field(oldPiece, "height"))))
                                    throw new InvalidDataException($"Invalid split residual references: {path}/source_splits/{oldId}");
                                var expected = (double[])oldRect.Clone();
                                if (i == 0) expected[cross + 2] = cutLow; else expected[cross] = cutHigh;
                                var actual = Vector(Field(piece, "rect"), 4, path + "/source_splits/rect");
                                if (actual.Where((v, index) => Math.Abs(v - expected[index]) > 0.000001).Any()) throw new InvalidDataException($"Invalid split residual geometry: {path}/source_splits/{oldId}");
                            }
                        }
                    }
                }
            var straightValues = Field(floor, "straight_stairs");
            if (!ReferenceEquals(straightValues, Missing))
                foreach (var flight in Items(straightValues, $"{floorId}/straight_stairs"))
                {
                    var id = Id(flight, "straight_stairs");
                    var path = $"{floorId}/straight_stairs/{id}";
                    var declaredGeometry = IsDeclaredGeometry(flight, path);
                    if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {path}");
                    if (Field(flight, "stair_ref") is not string stairId || !stairsById.TryGetValue(stairId, out var stair)) throw new InvalidDataException($"Invalid stair reference: {path}/stair_ref");
                    var segments = Items(Field(stair, "segments"), $"effective_stairs/{stairId}/segments");
                    var segmentIndex = Number(Field(flight, "segment_index"), path + "/segment_index");
                    if (segmentIndex < 0 || segmentIndex != Math.Truncate(segmentIndex) || segmentIndex >= segments.Count) throw new InvalidDataException($"Invalid segment index: {path}/segment_index");
                    var segment = segments[(int)segmentIndex];
                    if (Field(segment, "from") is not string fromFloor || fromFloor != floorId) throw new InvalidDataException($"Flight is not on its source floor: {path}");
                    var points = Items(Field(segment, "points"), path + "/points");
                    var fromIndex = Number(Field(flight, "from_point_index"), path + "/from_point_index");
                    var toIndex = Number(Field(flight, "to_point_index"), path + "/to_point_index");
                    if (fromIndex < 0 || fromIndex != Math.Truncate(fromIndex) || toIndex != fromIndex + 1 || toIndex >= points.Count) throw new InvalidDataException($"Invalid flight points: {path}");
                    var start = Vector(points[(int)fromIndex], 3, path + "/start");
                    var end = Vector(points[(int)toIndex], 3, path + "/end");
                    var run = Math.Sqrt(Math.Pow(end[0]-start[0],2)+Math.Pow(end[1]-start[1],2));
                    var widths = Items(Field(segment, "segment_widths"), path + "/segment_widths");
                    var widthIndex = Number(Field(flight, "width_index"), path + "/width_index");
                    if (widthIndex < 0 || widthIndex != Math.Truncate(widthIndex) || widthIndex >= widths.Count || Number(widths[(int)widthIndex], path + "/width") <= 0) throw new InvalidDataException($"Invalid flight width: {path}");
                    if (Field(flight, "count_field") is not "steps_per_flight") throw new InvalidDataException($"Unsupported step count source: {path}/count_field");
                    var count = Number(Field(segment, "steps_per_flight"), path + "/count");
                    var treadValue = Field(flight, "tread_count");
                    var treadCount = ReferenceEquals(treadValue, Missing) ? count : Number(treadValue, path + "/tread_count");
                    var tread = Number(Field(segment, "tread"), path + "/tread");
                    var riser = Number(Field(segment, "riser"), path + "/riser");
                    if (count <= 0 || count != Math.Truncate(count) || treadCount <= 0 || treadCount != Math.Truncate(treadCount)
                        || (treadCount != count && treadCount != count - 1) || tread <= 0 || riser <= 0 || end[2] <= start[2] || run <= 0
                        || Math.Abs(run - treadCount*tread) > 0.000001
                        || Math.Abs(end[2]-start[2]-count*riser) > 0.000001)
                        throw new InvalidDataException($"Flight dimensions disagree with stair source: {path}");
                    if (Number(Field(flight, "clearance_max_spacing"), path + "/clearance_max_spacing") <= 0) throw new InvalidDataException($"Invalid clearance sampling interval: {path}/clearance_max_spacing");
                    if (Number(Field(flight, "tread_thickness"), path + "/tread_thickness") <= 0 || Number(Field(flight, "ramp_thickness"), path + "/ramp_thickness") <= 0)
                        throw new InvalidDataException($"Invalid flight thickness: {path}");
                    var oldPieces = Items(Field(flight, "replaces_pieces"), path + "/replaces_pieces");
                    if (declaredGeometry ? oldPieces.Count != 0 : oldPieces.Count != count)
                        throw new InvalidDataException(declaredGeometry ? $"Declared flight must not replace legacy pieces: {path}" : $"Flight step count differs from replaced pieces: {path}");
                    foreach (var reference in oldPieces)
                        if (reference is not string pieceId || !usedPieces.Add(pieceId) || !piecesById.TryGetValue(pieceId, out var piece) || Field(piece, "kind") is not "stairs")
                            throw new InvalidDataException($"Invalid flight piece: {path}/replaces_pieces");
                    var oldBodies = Items(Field(flight, "replaces_colliders"), path + "/replaces_colliders");
                    if (declaredGeometry ? oldBodies.Count != 0 : oldBodies.Count != 1)
                        throw new InvalidDataException(declaredGeometry ? $"Declared flight must not replace legacy colliders: {path}" : $"Flight needs one source ramp: {path}");
                    foreach (var reference in oldBodies)
                        if (reference is not string colliderId || !usedColliders.Add(colliderId) || !collidersById.TryGetValue(colliderId, out var collider) || Field(collider, "kind") is not "stair_ramp")
                            throw new InvalidDataException($"Invalid flight ramp: {path}/replaces_colliders");
                }
            var ceilingValues = Field(floor, "ceiling_surfaces");
            if (!ReferenceEquals(ceilingValues, Missing))
                foreach (var ceiling in Items(ceilingValues, $"{floorId}/ceiling_surfaces"))
                {
                    var id = Id(ceiling, "ceiling_surfaces");
                    var path = $"{floorId}/ceiling_surfaces/{id}";
                    if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {path}");
                    if (Field(ceiling, "room_ref") is not string room || !roomIds.Contains(room)) throw new InvalidDataException($"Invalid room reference: {path}/room_ref");
                    foreach (var reference in Items(Field(ceiling, "hole_refs"), path + "/hole_refs"))
                        if (reference is not string hole || !voidRects.ContainsKey(hole)) throw new InvalidDataException($"Invalid ceiling hole: {path}/hole_refs");
                    var underside = Number(Field(ceiling, "underside"), path + "/underside");
                    var thickness = Number(Field(ceiling, "thickness"), path + "/thickness");
                    if (thickness <= 0 || Field(ceiling, "collision_enabled") is not bool) throw new InvalidDataException($"Invalid ceiling declaration: {path}");
                    var ceilingPieces = new List<object>();
                    foreach (var reference in Items(Field(ceiling, "replaces_pieces"), path + "/replaces_pieces"))
                    {
                        if (reference is not string pieceId || !usedPieces.Add(pieceId) || !piecesById.TryGetValue(pieceId, out var piece)
                            || Field(piece, "kind") is not "ceiling" || Field(piece, "room") is not string pieceRoom || pieceRoom != room
                            || Math.Abs(Number(Field(piece, "z"), path + "/underside") - underside) > 0.000001
                            || Math.Abs(Number(Field(piece, "height"), path + "/thickness") - thickness) > 0.000001)
                            throw new InvalidDataException($"Invalid ceiling piece reference: {path}/replaces_pieces");
                        ceilingPieces.Add(piece);
                    }
                    foreach (var reference in Items(Field(ceiling, "replaces_colliders"), path + "/replaces_colliders"))
                    {
                        if (reference is not string colliderId || !usedColliders.Add(colliderId) || !collidersById.TryGetValue(colliderId, out var collider)
                            || Field(collider, "kind") is not "ceiling" || ceilingPieces.Count(piece => R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(collider, "rect")))
                                && R.Bool(R.eq(Field(piece, "z"), Field(collider, "z"))) && R.Bool(R.eq(Field(piece, "height"), Field(collider, "height")))) != 1)
                            throw new InvalidDataException($"Invalid ceiling collision reference: {path}/replaces_colliders");
                    }
                }
            foreach (var opening in openingById.Values)
            {
                var parent = Field(opening, "wall_ref");
                if (!ReferenceEquals(parent, Missing))
                {
                    if (parent is not string { Length: > 0 } wallId)
                        throw new InvalidDataException($"Invalid reverse wall reference: {floorId}/openings/{Id(opening, "opening")}/wall_ref");
                    if (wallIds.Contains(wallId) && !usedOpenings.Contains(Id(opening, "opening")))
                        throw new InvalidDataException($"Invalid reverse wall reference: {floorId}/openings/{Id(opening, "opening")}/wall_ref");
                }
            }
            var furnitureById = furniture.ToDictionary(item => Id(item, $"{floorId}/furniture"), StringComparer.Ordinal);
            var furnitureIds = furnitureById.Keys.ToHashSet(StringComparer.Ordinal);
            foreach (var item in furniture)
            {
                var id = Id(item, $"{floorId}/furniture");
                var references = Field(item, "collision_refs");
                if (ReferenceEquals(references, Missing)) continue;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reference in Items(references, $"{floorId}/furniture/{id}/collision_refs"))
                    if (reference is not string collider || !seen.Add(collider) || !collidersById.TryGetValue(collider, out var body)
                        || Field(body, "object_ref") is not string owner || owner != id)
                        throw new InvalidDataException($"Invalid collision reference: {floorId}/furniture/{id}/collision_refs");
            }
            foreach (var collider in colliders)
            {
                var reference = Field(collider, "object_ref");
                if (ReferenceEquals(reference, Missing)) continue;
                var colliderId = Id(collider, "collider");
                if (reference is not string target || !furnitureIds.Contains(target)
                    || !Items(Field(furnitureById[target], "collision_refs"), $"{floorId}/furniture/{target}/collision_refs").Any(item => item is string refId && refId == colliderId))
                    throw new InvalidDataException($"Invalid object reference: {floorId}/colliders/{colliderId}/object_ref");
            }
        }
        ValidateRailingCandidate(spatial, floors);
        ValidateSemanticRegions(spatial, floors);
        ValidateAnchorCandidates(catalog, spatial, floors, spatialIds);
        foreach (var stair in Items(Field(spatial, "effective_stairs"), "effective_stairs"))
        {
            var id = Id(stair, "effective_stairs");
            if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: effective_stairs/{id}");
            foreach (var level in Items(Field(stair, "levels"), $"effective_stairs/{id}/levels"))
                if (level is not string floor || !floorIds.Contains(floor)) throw new InvalidDataException($"Unknown level: effective_stairs/{id}/levels");
            foreach (var segment in Items(Field(stair, "segments"), $"effective_stairs/{id}/segments"))
                foreach (var field in new[] { "from", "to" })
                    if (Field(segment, field) is not string floor || !floorIds.Contains(floor)) throw new InvalidDataException($"Unknown level: effective_stairs/{id}/segments/{field}");
        }
    }
}
// --- Source: SpatialPublication.Cli.cs ---

internal static partial class SpatialPublication
{
    public static int Run(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("--export-spatial-data <DV source> <generated JSON>");
        var output = Path.GetFullPath(args[2]);
        var generated = Path.GetFullPath(".runtime") + Path.DirectorySeparatorChar;
        if (!output.StartsWith(generated, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Compiled spatial data belongs in .runtime.");
        var document = DvScriptReader.Parse(File.ReadAllText(args[1]));
        var value = document.GetProperty("data").GetProperty("space.configurable_building").GetProperty("value");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, value.GetRawText());
        Console.WriteLine(output);
        return 0;
    }
}
