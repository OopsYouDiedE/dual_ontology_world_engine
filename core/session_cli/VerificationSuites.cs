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
        Check("authored character session starts at 480 minutes with DV needs",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage2-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();
            Expect(Close(session.ContentTime,480)&&Close(session.Clock.Now,480*60));
            var needs=session.Needs("detective");
            Expect(Close(needs["energy"],42)&&Close(needs["hunger"],80)&&Close(needs["care"],65));
        });
        Check("authored character session autonomously selects DV rest candidate",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage2-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();Decision? selected=null;
            for(var i=0;i<20&&selected is null;i++){session.Step(0,false,false,"detective");session.TryAction(out selected);}
            Expect(selected is not null&&selected.Action.Id=="detective.rest-chair"&&Close(selected.Action.Duration,240));
            Expect(session.Activity("detective")?.Phase=="preparing"&&session.Reservations.Occupants("R05-chair")==1);
        });
        Check("authored character work durations and standing resources",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage2-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            foreach(var item in new[]{(Id:"detective.rest-chair",Duration:240.0,Slot:"R05-chair"),
                                      (Id:"detective.wash",Duration:180.0,Slot:"R05-washstand")}) {
                var session=definition.Create();var decision=session.Submit("detective",item.Id);
                Expect(Close(decision.Action.Duration,item.Duration)&&session.Reservations.Occupants(item.Slot)==1);
                Expect(session.TryAction(out var dispatched)&&ReferenceEquals(decision,dispatched));
                Expect(session.BeginWork(decision)&&Close(session.Activity("detective")!.WorkEndsAt!.Value-session.Clock.Now,item.Duration));
            }
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
        Check("authored initial placement uses actor DV",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage2-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();var placement=session.Placement("detective");
            Expect(placement is not null&&Close(placement.X,59.7)&&Close(placement.Y,8.43)&&Close(placement.Z,-20.4)
                &&Close(placement.Yaw,0)&&placement.Room=="R05"&&Close(placement.Time,480*60));
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
        Check("curiosity can select an actually observed exit without knowing the corridor in advance",()=>{
            var source=File.ReadAllText("assets/data/stage10-character-session.dv").Replace("autonomous = true;","autonomous = false;");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var exit=new FieldKey("D-R05","passage","open");
            var visited=new FieldKey("detective-exploration","progress","corridor_visited");
            Expect(session.View("detective").Knowledge.GetValueOrDefault(exit) is not {Known:true});
            Expect(!session.Available("detective","detective.explore-b-corridor"));
            session.ReportPlacement("detective",61.5,8.4,-20.1,0,"R05");
            Expect(session.View("detective").Knowledge.GetValueOrDefault(exit) is not {Known:true});
            void Act(string action) {
                var decision=session.Submit("detective",action);var found=false;
                while(session.TryAction(out var next))if(ReferenceEquals(next,decision))found=true;
                Expect(found && session.BeginWork(decision));
                for(var n=0;n<300 && !session.WorkElapsed(decision);n++)session.Step(1,false,false,"detective");
                Expect(session.WorkElapsed(decision) && session.HostCompleted(decision));
                for(var n=0;n<20 && session.Activity("detective") is not null;n++)session.Step(0,false,false,"detective");
                for(var n=0;n<20;n++)session.Step(0,false,false,"detective");
            }
            Act("detective.rest-chair");
            var rest=session.EvaluateDrive("detective","detective.rest-chair");
            var observe=session.EvaluateDrive("detective","detective.inspect-r05-exit");
            Expect(observe.Score>rest.Score && observe.NeedContributions.GetValueOrDefault("curiosity")>0);
            Act("detective.inspect-r05-exit");
            Expect(session.View("detective").Knowledge.GetValueOrDefault(exit) is {Known:true} known && known.Value.GetBoolean());
            Expect(session.View("detective").Knowledge.GetValueOrDefault(visited) is {Known:true} before && !before.Value.GetBoolean());
            Expect(session.Available("detective","detective.explore-b-corridor"));
        });
        Check("idle detective can autonomously select rest then observed exit and corridor travel",()=>{
            var source=File.ReadAllText("assets/data/stage10-character-session.dv").Replace("\r\n","\n")
                .Replace("autonomous = true;","autonomous = false;")
                .Replace("id = \"detective\";\n        placement = { position = [59.7,8.43,-20.4]; yaw = 0; room = \"R05\"; };\n        autonomous = false;",
                         "id = \"detective\";\n        placement = { position = [59.7,8.43,-20.4]; yaw = 0; room = \"R05\"; };\n        autonomous = true;");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var chosen=new List<string>();Decision? active=null;
            for(var n=0;n<1800 && !chosen.Contains("detective.explore-b-corridor");n++)
            {
                session.Step(1,true,false,"detective");
                while(session.TryAction(out var next))
                {
                    if(next!.Actor!="detective")throw new InvalidOperationException("Unexpected autonomous actor.");
                    active=next;chosen.Add(next.Action.Id);
                    if(next.Action.Id=="detective.explore-b-corridor")
                    {
                        session.ReportPlacement("detective",63.6,8.4,-20.1,0,"corridor");
                        Expect(session.HostCompleted(next));active=null;
                    }
                    else Expect(session.BeginWork(next));
                }
                if(active is not null && session.WorkElapsed(active))
                { Expect(session.HostCompleted(active));active=null; }
            }
            Expect(chosen.Contains("detective.rest-chair") && chosen.Contains("detective.inspect-r05-exit") &&
                   chosen.Contains("detective.explore-b-corridor"));
        });
        Check("authored kitchen observation and transactional item actions",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage2-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();
            var location=new FieldKey("cloth","placement","location");
            var wet=new FieldKey("cloth","material","wet");
            var supply=new FieldKey("ration","supply","remaining");
            Fact? Mind(FieldKey key)=>session.View("detective").Knowledge.GetValueOrDefault(key);
            string Place()=>session.World.Read(location)!.Value.GetString()!;
            double Amount()=>session.World.Read(supply)!.Value.GetDouble();
            void Settle() { for(var i=0;i<12;i++)session.Step(0,false,false,"detective"); }
            Decision Start(string action) {
                var decision=session.Submit("detective",action);
                Expect(session.TryAction(out var dispatched)&&ReferenceEquals(decision,dispatched));
                Expect(session.BeginWork(decision));
                return decision;
            }
            void Complete(Decision decision) {
                Expect(session.HostCompleted(decision));
                var slices=(int)Math.Ceiling(decision.Action.Duration)+4;
                for(var i=0;i<slices&&session.Activity("detective") is not null;i++)session.Step(1,false,false,"detective");
                Settle();
                Expect(session.TryReceipt(out var receipt)&&receipt!.Committed&&!session.TryReceipt(out _));
                Expect(!session.HostCompleted(decision));
            }
            void Cancel(Decision decision) {
                Expect(session.RequestCancel("detective")&&session.ConfirmCancelled(decision));
                Settle();
                Expect(session.TryReceipt(out var receipt)&&!receipt!.Committed&&receipt.Reason=="cancelled"&&!session.TryReceipt(out _));
                Expect(!session.HostCompleted(decision));
            }
            Expect(Mind(location) is not {Known:true}&&Mind(wet) is not {Known:true}&&Mind(supply) is not {Known:true});
            Expect(Place()=="B-kitchen-hook"&&Close(Amount(),4));
            var inspect=Start("detective.inspect-kitchen");
            Expect(Mind(location) is not {Known:true}&&Mind(supply) is not {Known:true});
            Complete(inspect);
            Expect(Mind(location) is {Known:true} located&&located.Value.GetString()=="B-kitchen-hook");
            Expect(Mind(wet) is {Known:true} moisture&&moisture.Value.GetBoolean());
            Expect(Mind(supply) is {Known:true} stock&&Close(stock.Value.GetDouble(),4));
            var take=Start("detective.take-cloth");Cancel(take);Expect(Place()=="B-kitchen-hook");
            take=Start("detective.take-cloth");Complete(take);Expect(Place()=="detective");
            var place=Start("detective.place-cloth");Cancel(place);Expect(Place()=="detective");
            place=Start("detective.place-cloth");Complete(place);Expect(Place()=="B-kitchen-hook");
            var before=session.Needs("detective")["hunger"];
            var meal=Start("detective.eat");Cancel(meal);Expect(Close(Amount(),4)&&session.Needs("detective")["hunger"]<=before);
            before=session.Needs("detective")["hunger"];
            meal=Start("detective.eat");Complete(meal);
            Expect(Close(Amount(),3)&&session.Needs("detective")["hunger"]>before);
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
        Check("stage seven public corridor uses authored identities and portal graph",()=>{
            var manifest=JsonDocument.Parse(File.ReadAllText(".runtime/generated/stage7/bundles/manifest.json")).RootElement;
            var scope=DuelView.Dv.DvScriptReader.Parse(File.ReadAllText("assets/data/stage7-zone-b-cross.dv"))
                .GetProperty("data").GetProperty("stage7.zone_b_cross").GetProperty("value");
            var author=DuelView.Dv.DvScriptReader.Parse(File.ReadAllText("assets/data/95-configurable-building.data.dv"))
                .GetProperty("data").GetProperty("space.configurable_building").GetProperty("value");
            var floor=author.GetProperty("floors").EnumerateArray().Single(item=>item.GetProperty("id").GetString()=="2F");
            foreach(var id in new[]{"2F-cross-S","T02"})
            {
                var authored=floor.GetProperty("rooms").EnumerateArray().Single(item=>item.GetProperty("id").GetString()==id).GetProperty("bbox");
                var actual=manifest.GetProperty("rooms").GetProperty(id);
                Expect(authored.EnumerateArray().Zip(actual.EnumerateArray()).All(pair=>Close(pair.First.GetDouble(),pair.Second.GetDouble())));
            }
            Expect(scope.GetProperty("open_passages").EnumerateArray().Any(pair=>pair[0].GetString()=="gate-throat"&&pair[1].GetString()=="2F-cross-S"));
            var gate=manifest.GetProperty("openings").EnumerateArray().Single(item=>item.GetProperty("id").GetString()=="G-E-south");
            var door=manifest.GetProperty("openings").EnumerateArray().Single(item=>item.GetProperty("id").GetString()=="D-T02");
            Expect(gate.GetProperty("room_links").EnumerateArray().Select(item=>item.GetString()).ToHashSet().SetEquals(new[]{"foyer","gate-throat"}));
            Expect(door.GetProperty("room_links").EnumerateArray().Select(item=>item.GetString()).ToHashSet().SetEquals(new[]{"2F-cross-S","T02"}));
        });
        Check("stage seven A geometry preserves authored rooms and closed drum",()=>{
            var manifest=JsonDocument.Parse(File.ReadAllText(".runtime/generated/stage7a/bundles/manifest.json")).RootElement;
            var source=DuelView.Dv.DvScriptReader.Parse(File.ReadAllText("assets/data/95-configurable-building.data.dv"))
                .GetProperty("data").GetProperty("space.configurable_building").GetProperty("value");
            var floor=source.GetProperty("floors").EnumerateArray().Single(item=>item.GetProperty("id").GetString()=="2F");
            foreach(var id in new[]{"R01","R02","R03","R04","A-P"})
            {
                var authored=floor.GetProperty("rooms").EnumerateArray().Single(item=>item.GetProperty("id").GetString()==id).GetProperty("bbox");
                Expect(authored.EnumerateArray().Zip(manifest.GetProperty("rooms").GetProperty(id).EnumerateArray()).All(pair=>Close(pair.First.GetDouble(),pair.Second.GetDouble())));
            }
            var gate=manifest.GetProperty("openings").EnumerateArray().Single(item=>item.GetProperty("id").GetString()=="G-W-south");
            Expect(!gate.TryGetProperty("door",out _)&&!PortalPolicy.OpenWithoutLeaf(gate));
            Expect(gate.GetProperty("room_links").EnumerateArray().Select(item=>item.GetString()).ToHashSet().SetEquals(new[]{"A-corridor","A-throat"}));
            Expect(PortalPolicy.OpenWithoutLeaf(J(new {source_mechanism=new {closed_at_start=false}})));
        });
        Check("R03 service entrance connects authored room and closed physical door",()=>{
            var manifest=JsonDocument.Parse(File.ReadAllText(".runtime/generated/stage7b/bundles/manifest.json")).RootElement;
            var gate=manifest.GetProperty("openings").EnumerateArray().Single(item=>item.GetProperty("id").GetString()=="D-R03-service");
            Expect(gate.GetProperty("room_links").EnumerateArray().Select(item=>item.GetString()).ToHashSet().SetEquals(new[]{"R03","R03-service"}));
            Expect(Close(gate.GetProperty("door").GetProperty("initial_angle").GetDouble(),0));
            Expect(manifest.GetProperty("scope").GetProperty("open_passages").EnumerateArray().Any(pair=>pair[0].GetString()=="R03-service"&&pair[1].GetString()=="2F-cross-S"));
            var source=DuelView.Dv.DvScriptReader.Parse(File.ReadAllText("assets/data/stage7b-character-session.dv"))
                .GetProperty("data").GetProperty("runtime.character_session").GetProperty("value");
            var actors=source.GetProperty("actors").EnumerateArray().ToArray();
            var quer=actors.Single(actor=>actor.GetProperty("id").GetString()=="quer");
            var lucian=actors.Single(actor=>actor.GetProperty("id").GetString()=="lucian");
            Expect(quer.GetProperty("actions").EnumerateArray().Any(action=>action.GetProperty("id").GetString()=="quer.retrieve-service-map"));
            var qa=quer.GetProperty("actions").EnumerateArray().Single(action=>action.GetProperty("id").GetString()=="quer.exit-service").GetProperty("motor");
            var la=lucian.GetProperty("actions").EnumerateArray().Single(action=>action.GetProperty("id").GetString()=="lucian.exit-service").GetProperty("motor");
            Expect(la.GetProperty("door_opening").GetString()=="D-R03-service");
            var qp=qa.GetProperty("cross_target");var lp=la.GetProperty("cross_target");
            Expect(Math.Sqrt(Math.Pow(qp[0].GetDouble()-lp[0].GetDouble(),2)+Math.Pow(qp[2].GetDouble()-lp[2].GetDouble(),2))>=1);
        });
        Check("service exit requires actor knowledge and physical possession",()=>{
            var source=DuelView.Dv.DvScriptReader.Parse(File.ReadAllText("assets/data/stage7c-character-session.dv"))
                .GetProperty("data").GetProperty("runtime.character_session").GetProperty("value");
            var quer=source.GetProperty("actors").EnumerateArray().Single(item=>item.GetProperty("id").GetString()=="quer");
            var action=quer.GetProperty("actions").EnumerateArray().Single(item=>item.GetProperty("id").GetString()=="quer.exit-service");
            var gate=Condition.Compile(action.GetProperty("precondition"),"quer");
            var facts=new Dictionary<FieldKey,Fact>{
                [new("A-service-grille","mechanism","unlocked")]=new(J(true),true,false,1,0,0),
                [new("A-service-investigation","progress","quer_left")]=new(J(false),true,false,1,0,0),
                [new("A-network-map","placement","location")]=new(J("R03-service-grille"),true,false,1,0,0)
            };
            Expect(gate.Evaluate(key=>facts.GetValueOrDefault(key))==Truth.False);
            facts[new("A-network-map","placement","location")]=new(J("quer"),true,false,1,0,0);
            Expect(gate.Evaluate(key=>facts.GetValueOrDefault(key))==Truth.True);
            var law=source.GetProperty("physical").GetProperty("adoptions").GetProperty("stage7.quer-service-exit").GetProperty("law");
            var physical=Condition.Compile(law.GetProperty("premise"),"world");
            facts[new("A-network-map","placement","location")]=new(J("R03-service-grille"),true,false,1,0,0);
            Expect(physical.Evaluate(key=>facts.GetValueOrDefault(key))==Truth.False);
        });
        Check("C airlock keeps distinct doors and author interlock",()=>{
            var manifest=JsonDocument.Parse(File.ReadAllText(".runtime/generated/stage7c/bundles/manifest.json")).RootElement;
            var doors=manifest.GetProperty("openings").EnumerateArray().ToArray();
            var inner=doors.Single(item=>item.GetProperty("id").GetString()=="G-W-north");
            var outer=doors.Single(item=>item.GetProperty("id").GetString()=="D-CA-OUT");
            Expect(inner.GetProperty("room_links").EnumerateArray().Select(item=>item.GetString()).ToHashSet().SetEquals(new[]{"C-corridor","C-A"}));
            Expect(outer.GetProperty("room_links").EnumerateArray().Select(item=>item.GetString()).ToHashSet().SetEquals(new[]{"C-A","C-A-public-approach"}));
            Expect(Close(inner.GetProperty("door").GetProperty("initial_angle").GetDouble(),0));
            Expect(Math.Abs(outer.GetProperty("door").GetProperty("initial_angle").GetDouble())>1);
            Expect(manifest.GetProperty("scope").GetProperty("door_interlocks").EnumerateArray().Any(pair=>pair[0].GetString()=="G-W-north"&&pair[1].GetString()=="D-CA-OUT"));
            var south=doors.Single(item=>item.GetProperty("id").GetString()=="G-W-north_inner");
            Expect(south.GetProperty("source_mechanism").GetProperty("type").GetString()=="eccentric_heavy_gate");
            Expect(Close(south.GetProperty("door").GetProperty("initial_angle").GetDouble(),0));
            Expect(south.GetProperty("room_links").EnumerateArray().Select(item=>item.GetString()).ToHashSet().SetEquals(new[]{"C-corridor","M-W-approach"}));
        });
        Check("C correlation requires all four independently supplied observations",()=>{
            var theory=DuelView.Core.CaseTheory.Load("assets/data/cases/stage7_c_action_inquiry.dv");
            var original=theory.Scopes["day1_c_action_inquiry"];
            var goal=original.Theorems.Single(item=>item.Id=="correlation").Conclusion;
            var stripped=theory.WithoutAssumptions("day1_c_action_inquiry");
            Expect(stripped.Prove("day1_c_action_inquiry",goal).Status=="unknown");
            var evidence=original.Axioms.Where(item=>new[]{"ankle","palm","mesh","bandage"}.Contains(item.Id)).ToArray();
            Expect(stripped.WithEvidence("day1_c_action_inquiry",evidence.Take(3)).Prove("day1_c_action_inquiry",goal).Status=="unknown");
            var grounded=stripped.WithEvidence("day1_c_action_inquiry",evidence);
            Expect(grounded.Prove("day1_c_action_inquiry",goal).Status=="proved");
            Expect(grounded.Prove("day1_c_action_inquiry",original.Theorems.Single(item=>item.Id=="impact").Conclusion).Status=="proved");
            Expect(grounded.Prove("day1_c_action_inquiry",original.Theorems.Single(item=>item.Id=="cleared").Conclusion).Status=="unknown");
            Expect(grounded.Prove("day1_c_action_inquiry",original.Theorems.Single(item=>item.Id=="assailant").Conclusion).Status=="unknown");
        });
        Check("navigation journey rejects invalid paths and time",()=>{
            var rejected=false;
            try{_ = new PathJourney([new(0,0,0),new(0,0,0)]);}catch(InvalidDataException){rejected=true;}
            Expect(rejected);
            var journey=new PathJourney([new(0,0,0),new(1,0,0)]);
            rejected=false;try{journey.Advance(double.NaN,1);}catch(ArgumentOutOfRangeException){rejected=true;}
            Expect(rejected&&Close(journey.Distance,0));
        });
        Check("offscreen path belongs to an active execution and ends on cancellation",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage5-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();
            var decision=session.Submit("su","su.walk-to-room");
            Expect(session.TryAction(out var dispatched)&&ReferenceEquals(dispatched,decision));
            var start=session.Placement("su")!;
            var path=new[]{new PathPoint(start.X,start.Y,start.Z),new PathPoint(start.X+3,start.Y,start.Z)};
            Expect(session.BeginJourney(decision,path)&&!session.BeginJourney(decision,path));
            var view=session.AdvanceJourney(decision,1,1);
            Expect(Close(view.Distance,1)&&Close(view.Position.X,start.X+1)&&!view.Arrived);
            Expect(session.RequestCancel("su")&&session.ConfirmCancelled(decision));
            Expect(!session.EndJourney(decision));
        });
        Check("stage four passive information buffers until a deliberate refresh",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage4-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();
            var wet=new FieldKey("cloth","material","wet");
            Expect(session.View("su").Knowledge.GetValueOrDefault(wet) is not {Known:true});
            Expect(session.ReceivePhysicalObservation("su",wet,"test-visibility"));
            Expect(session.View("su").Knowledge.GetValueOrDefault(wet) is not {Known:true});
            session.Refresh("su",RefreshCause.Deliberate);
            for(var i=0;i<12;i++)session.Step(0,false,false,"detective");
            Expect(session.View("su").Knowledge.GetValueOrDefault(wet) is {Known:true} known && known.Value.GetBoolean());
            Expect(!session.ReceivePhysicalObservation("huo",wet,"unbound"));
        });
        Check("stage four kitchen reservation rejects conflicts and releases after cancellation",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage4-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();
            var su=session.Submit("su","su.wash");
            Expect(session.TryAction(out var dispatched) && ReferenceEquals(dispatched,su));
            var player=session.Submit("detective","detective.kitchen-wash");
            Expect(session.TryReceipt(out var busy) && busy!.Execution==player.Execution && busy.Reason=="standing_resource_busy");
            Expect(session.RequestCancel("su") && session.ConfirmCancelled(su));
            Expect(session.TryReceipt(out var cancelled) && cancelled!.Execution==su.Execution && cancelled.Reason=="cancelled");
            var retry=session.Submit("detective","detective.kitchen-wash");
            Expect(session.Activity("detective")?.Execution==retry.Execution);
        });
        Check("reservation declarations reject missing slots at load with actor and action context",()=>{
            var source=File.ReadAllText("assets/data/stage7d-character-session.dv");
            var policy=File.ReadAllText("assets/data/98-runtime-session.data.dv");
            _=new CharacterSessionDefinition(source,policy);
            var broken=source.Replace("{ id = \"D-gate-operation\"; capacity = 1; }", "{ id = \"D-other-slot\"; capacity = 1; }");
            Expect(broken!=source);
            try { _=new CharacterSessionDefinition(broken,policy); throw new Exception("Missing slot was accepted."); }
            catch(InvalidDataException error)
            {
                Expect(error.Message.Contains("margaret") && error.Message.Contains("margaret.inspect-pressure") && error.Message.Contains("D-gate-operation"));
            }
        });
        Check("day one gathering needs all sixteen physical placements within the authored radius",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage8-character-session.dv"),File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var group=new FieldKey("day1-gathering","progress","gathered");
            var actors=new[]{"detective","su","huo","shen","lucian","gu","quer","ward","ruan","selma","elena","moran","margaret","veronica","mora","bertrand"};
            for(var i=0;i<actors.Length-1;i++)session.ReportPlacement(actors[i],37.8,8.4,-15.6,0,"2F-cross-S");
            Expect(session.World.Read(group) is {Known:true} before && !before.Value.GetBoolean());
            session.ReportPlacement(actors[^1],60,8.4,-15.6,0,"2F-cross-S");
            Expect(session.World.Read(group) is {Known:true} outside && !outside.Value.GetBoolean());
            session.ReportPlacement(actors[^1],37.8,8.4,-15.6,0,"2F-cross-S");
            Expect(session.World.Read(group) is {Known:true} after && after.Value.GetBoolean());
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
        Check("stage six requires actual observed evidence before the authored deduction and gate",()=>{
            var source=File.ReadAllText("assets/data/stage6-character-session.dv");
            var policy=File.ReadAllText("assets/data/98-runtime-session.data.dv");
            var session=new CharacterSessionDefinition(source,policy).Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var actions=new FieldKey("zone-b","deduction","actions");
            var fifth=new FieldKey("zone-b","deduction","fifth_entity");
            var gate=new FieldKey("B-gate-inner","passage","open");
            void Act(string id) {
                var decision=session.Submit("detective",id);
                var found=false;
                while(session.TryAction(out var item))if(ReferenceEquals(item,decision))found=true;
                Expect(found&&session.BeginWork(decision)&&session.HostCompleted(decision));
                for(var i=0;i<200&&session.Activity("detective") is not null;i++)session.Step(1,false,false,"detective");
                Expect(session.Activity("detective") is null);
                for(var i=0;i<8;i++)session.Step(0,false,false,"detective");
            }
            Expect(!session.Available("detective","detective.unlock-gate"));
            Act("detective.reconstruct");
            Expect(session.View("detective").Knowledge.GetValueOrDefault(actions) is not {Known:true});
            foreach(var id in new[]{"detective.inspect-empty-room","detective.inspect-r06","detective.inspect-r07","detective.inspect-r08",
                "detective.inspect-struggle","detective.inspect-ratchet","detective.inspect-threshold","detective.inspect-kitchen"})Act(id);
            Act("detective.reconstruct");
            Expect(session.View("detective").Knowledge.GetValueOrDefault(actions) is {Known:true} a&&a.Value.GetBoolean());
            Expect(session.View("detective").Knowledge.GetValueOrDefault(fifth) is {Known:true} f&&f.Value.GetBoolean());
            Expect(session.Available("detective","detective.unlock-gate"));
            var unlock=session.Submit("detective","detective.unlock-gate");
            var dispatched=false;while(session.TryAction(out var item))if(ReferenceEquals(item,unlock))dispatched=true;
            Expect(dispatched&&session.PhysicalActionPermitted(unlock)&&session.BeginWork(unlock)&&session.HostCompleted(unlock));
            for(var i=0;i<100&&session.Activity("detective") is not null;i++)session.Step(1,false,false,"detective");
            Expect(session.World.Read(gate) is {Known:true} opened&&opened.Value.GetBoolean());
            Expect(!session.HostCompleted(unlock));
        });
        Check("stage six directed conversation transfers only the speaker's known evidence",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage6-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var conversation=definition.Declaration.GetProperty("conversations").EnumerateArray().Single(item=>item.GetProperty("id").GetString()=="detective.huo.greeting");
            var key=new FieldKey("B-gate-inner","mechanism","engineered_deadlock");
            Expect(session.View("detective").Knowledge.GetValueOrDefault(key) is not {Known:true});
            var view=session.StartConversation("detective","huo",conversation);
            Expect(session.AlignConversation(view.Id)&&!session.AdvanceConversation(view.Id,0));
            for(var i=0;i<16;i++)session.Step(1,true,false,"detective");
            Expect(session.AdvanceConversation(view.Id,0));
            for(var i=0;i<16;i++)session.Step(1,true,false,"detective");
            Expect(session.AdvanceConversation(view.Id,1)&&session.CompleteConversation(view.Id));
            for(var i=0;i<8;i++)session.Step(0,true,false,"detective");
            Expect(session.View("detective").Knowledge.GetValueOrDefault(key) is {Known:true} known&&known.Value.GetBoolean());
            Expect(session.EvidenceCount("detective")>0);
        });
        Check("stage six yield uses a reserved host movement and completes only once",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage6-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var decision=session.Submit("su","su.yield.r08-a");
            Expect(session.Reservations.Occupants("yield.station.r08-a")==1);
            Expect(session.TryAction(out var dispatched)&&ReferenceEquals(dispatched,decision));
            Expect(session.HostCompleted(decision));session.Step(0,true,false,"detective");
            Expect(session.Reservations.Occupants("yield.station.r08-a")==0&&!session.HostCompleted(decision));
        });
        Check("conversation locks both actors and resumes the reserved action",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage3-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();
            var action=session.Submit("detective","detective.wash");
            Expect(session.TryAction(out _)&&session.BeginWork(action));
            var before=session.Activity("detective")!.WorkEndsAt!.Value;
            var spec=definition.Declaration.GetProperty("conversations")[0];
            var conversation=session.StartConversation("detective","su",spec);
            Expect(session.Reservations.Occupants("R05-washstand")==1);
            var occupied=false;try {session.Submit("detective","detective.rest-chair");} catch(InvalidOperationException){occupied=true;}
            Expect(occupied&&!session.BeginWork(action)&&!session.HostCompleted(action));
            occupied=false;try {session.Submit("su","su.walk-to-room");} catch(InvalidOperationException){occupied=true;}
            Expect(occupied);
            for(var n=0;n<10;n++)session.Step(1,true,false,"detective");
            Expect(Close(session.Clock.Now,480*60+10));
            Expect(session.InterruptConversation(conversation.Id));
            Expect(session.CompleteConversation(conversation.Id));
            Expect(Close(session.Activity("detective")!.WorkEndsAt!.Value,before+10));
            Expect(session.Reservations.Occupants("R05-washstand")==1&&!session.InterruptConversation(conversation.Id));
            for(var n=0;n<200&&!session.WorkElapsed(action);n++)session.Step(1,false,false,"detective");
            Expect(session.WorkElapsed(action)&&session.HostCompleted(action));
            for(var n=0;n<8&&session.Activity("detective") is not null;n++)session.Step(0,false,false,"detective");
            Expect(session.TryReceipt(out var receipt)&&receipt!.Committed&&!session.TryReceipt(out _));
            Expect(session.Reservations.Occupants("R05-washstand")==0);
        });
        Check("conversation lines require deadlines and reject duplicate input",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage3-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();var spec=definition.Declaration.GetProperty("conversations")[0];
            var conversation=session.StartConversation("detective","su",spec);
            Expect(session.AlignConversation(conversation.Id)&&!session.AlignConversation(conversation.Id));
            Expect(!session.AdvanceConversation(conversation.Id,0));
            for(var n=0;n<60;n++)session.Step(1,true,false,"detective");
            Expect(session.AdvanceConversation(conversation.Id,0)&&!session.AdvanceConversation(conversation.Id,0));
            for(var n=0;n<60;n++)session.Step(1,true,false,"detective");
            Expect(session.AdvanceConversation(conversation.Id,1));
            for(var n=0;n<60;n++)session.Step(1,true,false,"detective");
            Expect(session.AdvanceConversation(conversation.Id,2)&&session.CompleteConversation(conversation.Id)&&session.Conversation is null);
        });
        Check("moving target keeps its action until alignment and resumes after interruption",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage3-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();var movement=session.Submit("su","su.walk-to-room");
            Expect(session.TryAction(out _));
            var conversation=session.StartConversation("detective","su",definition.Declaration.GetProperty("conversations")[0]);
            Expect(session.Activity("su")?.Phase=="moving");
            Expect(session.AlignConversation(conversation.Id));
            Expect(!session.HostCompleted(movement));
            Expect(session.InterruptConversation(conversation.Id));
            Expect(session.CompleteConversation(conversation.Id));
            Expect(session.HostCompleted(movement));
            session.Step(0,true,false,"detective");
            Expect(session.TryReceipt(out var receipt)&&receipt!.Committed&&!session.TryReceipt(out _));
        });
        Check("authored world clock changes wind without granting actor knowledge",()=>{
            var source=File.ReadAllText("assets/data/stage9-character-session.dv")
                .Replace("physical = { domain = \"physical\"; time = 480;", "physical = { domain = \"physical\"; time = 1919;")
                .Replace("minutes = { known = true; value = 480;", "minutes = { known = true; value = 1919;");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var weather=new FieldKey("island-weather","wind","window_open");
            Expect(session.World.Read(weather)?.Value.GetBoolean()==false);
            for(var n=0;n<180;n++)session.Step(1,true,false,"detective");
            Expect(session.World.Read(weather)?.Value.GetBoolean()==true);
            Expect(!session.View("huo").Knowledge.ContainsKey(weather));
        });
        Check("current hazard presence stops dose on exit and resumes on re-entry",()=>{
            var source=File.ReadAllText("assets/data/stage10-character-session.dv")
                .Replace("physical = { domain = \"physical\"; time = 450;", "physical = { domain = \"physical\"; time = 5369;")
                .Replace("minutes = { known = true; value = 450;", "minutes = { known = true; value = 5369;")
                .Replace("host_physical_bindings = [", "host_physical_bindings = [{ type = \"ref\"; object = \"B2-reactor\"; facet = \"hazard\"; field = \"backflow_active\"; }, ");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            for(var n=0;n<180;n++)session.Step(1,true,false,"detective");
            var present=new FieldKey("night4-presence","in_danger_zone","detective");
            var dose=new FieldKey("night4-exposure","dose_seconds","detective");
            session.ReportPlacement("detective",11.7,-8.4,-1,0,"B2-control");
            for(var n=0;n<10;n++)session.Step(1,true,false,"detective");
            var first=session.World.Read(dose)!.Value.GetDouble();
            Expect(session.World.Read(present)!.Value.GetBoolean() && first>0);
            session.ReportPlacement("detective",11.7,-4.2,-19,0,"B1-service");
            for(var n=0;n<10;n++)session.Step(1,true,false,"detective");
            Expect(!session.World.Read(present)!.Value.GetBoolean() && session.World.Read(dose)!.Value.GetDouble()==first);
            session.ReportPlacement("detective",11.7,-8.4,-1,0,"B2-control");
            for(var n=0;n<10;n++)session.Step(1,true,false,"detective");
            Expect(session.World.Read(dose)!.Value.GetDouble()>first);
            var beforeSeal=session.World.Read(dose)!.Value.GetDouble();
            session.ReportPhysicalBoolean(new FieldKey("B2-reactor","hazard","backflow_active"),false);
            for(var n=0;n<10;n++)session.Step(1,true,false,"detective");
            Expect(session.World.Read(dose)!.Value.GetDouble()==beforeSeal);
        });
        Check("sustained untreated exposure incapacitates once and cancels active work",()=>{
            var source=File.ReadAllText("assets/data/stage10-character-session.dv")
                .Replace("autonomous = true;","autonomous = false;")
                .Replace("physical = { domain = \"physical\"; time = 450;", "physical = { domain = \"physical\"; time = 5369;")
                .Replace("minutes = { known = true; value = 450;", "minutes = { known = true; value = 5369;");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            for(var n=0;n<180;n++)session.Step(1,true,false,"detective");
            var decision=session.Submit("moran","moran.inspect-marks");
            Expect(session.TryAction(out var dispatched) && ReferenceEquals(dispatched,decision) && session.BeginWork(decision));
            session.ReportPlacement("moran",11.7,-8.4,-1,0,"B2-control");
            for(var n=0;n<90;n++)session.Step(1,true,false,"detective");
            var dose=new FieldKey("night4-health","untreated_dose_seconds","moran");
            var beforeExit=session.World.Read(dose)!.Value.GetDouble();
            Expect(beforeExit>0 && beforeExit<300 && !session.Incapacitated("moran"));
            session.ReportPlacement("moran",11.7,-4.2,-19,0,"B1-service");
            for(var n=0;n<360;n++)session.Step(1,true,false,"detective");
            Expect(session.World.Read(dose)!.Value.GetDouble()==beforeExit && !session.Incapacitated("moran"));
            session.ReportPlacement("moran",11.7,-8.4,-1,0,"B2-control");
            for(var n=0;n<400 && !session.Incapacitated("moran");n++)session.Step(1,true,false,"detective");
            Expect(session.Incapacitated("moran") && !session.Available("moran","moran.inspect-marks"));
            for(var n=0;n<8;n++)session.Step(0,false,false,"detective");
            Expect(session.TryCancellation(out var cancel) && ReferenceEquals(cancel,decision));
            Expect(session.ConfirmCancelled(decision) && !session.HostCompleted(decision));
            var saved=session.SaveState();var restored=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            restored.RestoreState(saved);Expect(restored.Incapacitated("moran") && !restored.Available("moran","moran.inspect-marks"));
        });
        Check("night watch needs real concurrent presence and both completed work periods",()=>{
            var source=File.ReadAllText("assets/data/stage10-character-session.dv")
                .Replace("autonomous = true;","autonomous = false;")
                .Replace("value = \"F05-counter\"; confidence = 1;","value = \"B1-watch-post\"; confidence = 1;")
                .Replace("prepared_doses = { known = true; value = 0;","prepared_doses = { known = true; value = 1;")
                .Replace("location = { known = true; value = \"R13-desk\";","location = { known = true; value = \"mora\";");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            void Settle() { for(var n=0;n<20;n++)session.Step(0,false,false,"detective"); }
            void Observe(string actor,FieldKey key,object value) {
                var token=session.BeginObservation(actor,[key]);
                Expect(session.Receive(actor,new Observation("test/watch/"+actor+"/"+key.Field,key,J(value),1,session.ContentTime,InformationOrigin.DeliberateObservation,J(new {source="unit"}))));
                session.EndObservation(actor,token);Settle();
            }
            Decision Begin(string actor,string action) {
                var decision=session.Submit(actor,action);var found=false;
                while(session.TryAction(out var next))if(ReferenceEquals(next,decision))found=true;
                Expect(found && session.BeginWork(decision));return decision;
            }
            void Finish(Decision decision) {
                for(var n=0;n<400 && !session.WorkElapsed(decision);n++)session.Step(1,false,false,"detective");
                Expect(session.WorkElapsed(decision) && session.HostCompleted(decision));
                for(var n=0;n<20 && session.Activity(decision.Actor) is not null;n++)session.Step(0,false,false,"detective");
                Expect(session.Activity(decision.Actor) is null);Settle();
            }
            foreach(var actor in new[]{"detective","mora","bertrand"})
                session.ReportPlacement(actor,11.7,-4.2,-19.5,0,"B1-service");
            foreach(var actor in new[]{"detective","mora","bertrand"})
                Observe("detective",new FieldKey("night4-watch","presence",actor),true);
            Observe("detective",new FieldKey("night4-knowledge","hazard","specific_lethal"),true);
            Observe("detective",new FieldKey("c2.strategy","state","watch_supply"),true);
            Observe("detective",new FieldKey("R-07","placement","location"),"mora");
            Observe("detective",new FieldKey("R-07","stock","prepared_doses"),1);
            Finish(Begin("detective","detective.start-normal-watch"));
            var active=new FieldKey("c2.strategy","state","watch_active");
            var completed=new FieldKey("night4-watch","progress","completed");
            Expect(session.World.Read(active)!.Value.GetBoolean());
            Observe("bertrand",active,true);
            var checkpoint=session.SaveState();
            var detective=Begin("detective","detective.maintain-normal-watch");
            var bertrand=Begin("bertrand","bertrand.maintain-normal-watch");
            for(var n=0;n<100;n++)session.Step(1,false,false,"detective");
            session.ReportPlacement("mora",11.7,-4.2,-30,0,"B1-service");Settle();
            Expect(!session.World.Read(active)!.Value.GetBoolean() && !session.World.Read(completed)!.Value.GetBoolean());
            Finish(detective);Finish(bertrand);
            Expect(!session.World.Read(completed)!.Value.GetBoolean());
            session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));session.RestoreState(checkpoint);
            detective=Begin("detective","detective.maintain-normal-watch");
            bertrand=Begin("bertrand","bertrand.maintain-normal-watch");
            Finish(detective);Finish(bertrand);
            for(var n=0;n<30;n++)session.Step(0,false,false,"detective");
            Expect(session.World.Read(completed)!.Value.GetBoolean() && !session.World.Read(active)!.Value.GetBoolean());
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
        Check("runtime checkpoint resumes reserved work once with its observation",()=>{
            CharacterSessionDefinition Definition()=>new(File.ReadAllText("assets/data/stage2-character-session.dv"),File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var first=Definition().Create();var decision=first.Submit("detective","detective.inspect-kitchen");
            Expect(first.TryAction(out var dispatched)&&ReferenceEquals(decision,dispatched)&&first.BeginWork(decision));
            for(var n=0;n<10;n++)first.Step(1,true,false,"detective");
            Expect(first.HostCompleted(decision));
            var saved=JsonSerializer.Serialize(first.SaveState());
            var second=Definition().Create();second.RestoreState(JsonSerializer.Deserialize<RuntimeSessionState>(saved)!);
            var restored=second.DecisionFor(decision.Execution);Expect(restored is not null && second.Reservations.Occupants("B-kitchen-hook")==1);
            for(var n=0;n<180 && second.Activity("detective") is not null;n++)second.Step(1,true,false,"detective");
            Expect(second.TryReceipt(out var receipt)&&receipt!.Committed && receipt.Execution==decision.Execution);
            Expect(!second.HostCompleted(restored!) && second.Reservations.Occupants("B-kitchen-hook")==0);
            var wet=new FieldKey("cloth","material","wet");for(var n=0;n<20;n++)second.Step(0,false,false,"detective");
            Expect(second.View("detective").Knowledge.GetValueOrDefault(wet) is {Known:true} fact && fact.Value.GetBoolean());
        });
        Check("runtime checkpoint resumes conversation line and suspended action",()=>{
            CharacterSessionDefinition Definition()=>new(File.ReadAllText("assets/data/stage3-character-session.dv"),File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var definition=Definition();var first=definition.Create();var movement=first.Submit("su","su.walk-to-room");Expect(first.TryAction(out _));
            var conversation=first.StartConversation("detective","su",definition.Declaration.GetProperty("conversations")[0]);
            Expect(first.AlignConversation(conversation.Id));for(var n=0;n<60;n++)first.Step(1,true,false,"detective");
            Expect(first.AdvanceConversation(conversation.Id,0));for(var n=0;n<10;n++)first.Step(1,true,false,"detective");
            var saved=JsonSerializer.Serialize(first.SaveState());var second=Definition().Create();second.RestoreState(JsonSerializer.Deserialize<RuntimeSessionState>(saved)!);
            Expect(second.Conversation is {LineIndex:1,Phase:"speaking"} && !second.AdvanceConversation(conversation.Id,1));
            for(var n=0;n<50;n++)second.Step(1,true,false,"detective");
            Expect(second.AdvanceConversation(conversation.Id,1));
            for(var n=0;n<60;n++)second.Step(1,true,false,"detective");
            Expect(second.AdvanceConversation(conversation.Id,2) && second.CompleteConversation(conversation.Id));
            Expect(second.DecisionFor(movement.Execution) is not null);
        });
        Check("runtime checkpoint resumes an in-flight route by distance",()=>{
            CharacterSessionDefinition Definition()=>new(File.ReadAllText("assets/data/stage2-character-session.dv"),File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var first=Definition().Create();var decision=first.Submit("detective","detective.inspect-kitchen");
            Expect(first.TryAction(out _) && first.BeginJourney(decision,[new(59.7,8.43,-20.4),new(61.7,8.43,-20.4)]));
            Expect(Close(first.AdvanceJourney(decision,1,1).Distance,1));
            var saved=JsonSerializer.Serialize(first.SaveState());var second=Definition().Create();second.RestoreState(JsonSerializer.Deserialize<RuntimeSessionState>(saved)!);
            var restored=second.DecisionFor(decision.Execution)!;var remaining=second.AdvanceJourney(restored,1,1);
            Expect(remaining.Arrived && Close(remaining.Distance,2) && second.EndJourney(restored) && !second.EndJourney(restored));
        });
        Check("night four strategy profiles bind to their actual individual actors",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage10-character-session.dv"),File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var source=File.ReadAllText("assets/data/cases/r3_npc_strategy.dv");
            foreach(var activity in definition.Declaration.GetProperty("strategy_activities").EnumerateArray())session.ConfigureStrategy(source,activity);
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
        Check("physical handoff gives recipient only actual result and sender-known count",()=>{
            var source=File.ReadAllText("assets/data/stage10-character-session.dv")
                .Replace("prepared_doses = { known = true; value = 0;", "prepared_doses = { known = true; value = 2;")
                .Replace("location = { known = true; value = \"R13-desk\";", "location = { known = true; value = \"veronica\";")
                .Replace("mind = { subject = \"veronica\"; objects = {", "mind = { subject = \"veronica\"; objects = { \"R-07\" = { facets = { stock = { prepared_doses = { known = true; value = 2; confidence = 1; }; }; placement = { location = { known = true; value = \"veronica\"; confidence = 1; }; }; }; };")
                .Replace("placement = { position = [61.0,8.43,-55.0]; yaw = 0; room = \"R13\"; }; autonomous = true;", "placement = { position = [61.0,8.43,-55.0]; yaw = 0; room = \"R13\"; }; autonomous = false;")
                .Replace("placement = { position = [67.0,8.43,-61.5]; yaw = 0; room = \"R16\"; }; autonomous = true;", "placement = { position = [67.0,8.43,-61.5]; yaw = 0; room = \"R16\"; }; autonomous = false;");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var stock=new FieldKey("R-07","stock","prepared_doses");var location=new FieldKey("R-07","placement","location");
            Expect(!session.View("mora").Knowledge.ContainsKey(stock));
            var decision=session.Submit("veronica","veronica.give-r07");
            var found=false;while(session.TryAction(out var next))if(ReferenceEquals(decision,next))found=true;
            Expect(found && session.BeginWork(decision) && session.HostCompleted(decision));
            for(var n=0;n<120 && session.Activity("veronica") is not null;n++)session.Step(1,false,false,"detective");
            for(var n=0;n<20;n++)session.Step(0,false,false,"detective");
            Expect(session.View("mora").Knowledge.GetValueOrDefault(stock) is {Known:true} amount && amount.Value.GetDouble()==2);
            Expect(session.View("mora").Knowledge.GetValueOrDefault(location) is {Known:true} held && held.Value.GetString()=="mora");
            Expect(!session.HostCompleted(decision));
        });
        Check("meal serving consumes one shared portion and cannot be completed twice",()=>{
            var source=File.ReadAllText("assets/data/stage10-character-session.dv").Replace("\r\n","\n")
                .Replace("portions = { known = true; value = 0; confidence = 1; }; }; eaten =",
                         "portions = { known = true; value = 1; confidence = 1; }; }; eaten =")
                .Replace("id = \"detective\";\n        placement = { position = [59.7,8.43,-20.4]; yaw = 0; room = \"R05\"; };\n        autonomous = true;",
                         "id = \"detective\";\n        placement = { position = [59.7,8.43,-20.4]; yaw = 0; room = \"R05\"; };\n        autonomous = false;");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var portions=new FieldKey("day1-meal","supply","portions");
            var token=session.BeginObservation("detective",[portions]);
            Expect(session.Receive("detective",new Observation("test/food",portions,J(1),1,0,InformationOrigin.DeliberateObservation,J(new {source="unit"}))));
            session.EndObservation("detective",token);
            for(var n=0;n<20;n++)session.Step(0,false,false,"detective");
            void Complete(string action) {
                var decision=session.Submit("detective",action);
                var found=false;while(session.TryAction(out var next))if(ReferenceEquals(decision,next))found=true;
                Expect(found && session.BeginWork(decision) && session.HostCompleted(decision));
                for(var n=0;n<300 && session.Activity("detective") is not null;n++)session.Step(1,false,false,"detective");
                for(var n=0;n<20;n++)session.Step(0,false,false,"detective");
                Expect(!session.HostCompleted(decision));
            }
            Complete("detective.take-dumplings");
            Expect(session.World.Read(portions)!.Value.GetDouble()==0);
            Expect(session.World.Read(new FieldKey("meal-bowl-detective","placement","location"))!.Value.GetString()=="detective");
            Complete("detective.eat-dumplings");
            Expect(session.World.Read(new FieldKey("day1-meal","eaten","detective"))!.Value.GetBoolean());
            Expect(session.World.Read(new FieldKey("meal-bowl-detective","placement","location"))!.Value.GetString()=="consumed");
            Expect(!session.Available("detective","detective.take-dumplings") && !session.Available("detective","detective.eat-dumplings"));
        });
        Check("return votes and probe handoff follow individual actions and allow refusal",()=>{
            var source=File.ReadAllText("assets/data/stage10-character-session.dv")
                .Replace("autonomous = true;","autonomous = false;")
                .Replace("not_before_game_minutes = 1920;","not_before_game_minutes = 450;")
                .Replace("not_before_game_minutes = 2040;","not_before_game_minutes = 450;")
                .Replace("not_before_game_minutes = 2220;","not_before_game_minutes = 450;")
                .Replace("not_before_game_minutes = 2730;","not_before_game_minutes = 450;");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var strategy=definition.Declaration.GetProperty("strategy_activities").EnumerateArray()
                .Single(item=>item.GetProperty("action").GetString()=="ruan.self-play");
            RuntimeSession CreateSession() {
                var created=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
                created.ConfigureStrategy(File.ReadAllText("assets/data/cases/r3_npc_strategy.dv"),strategy);return created;
            }
            var session=CreateSession();
            void Settle() { for(var i=0;i<20;i++)session.Step(0,false,false,"detective"); }
            void Observe(string actor,FieldKey key,object value) {
                var token=session.BeginObservation(actor,[key]);
                Expect(session.Receive(actor,new Observation("test/"+actor+"/"+key.Field,key,J(value),1,session.ContentTime,InformationOrigin.DeliberateObservation,J(new {source="unit"}))));
                session.EndObservation(actor,token);Settle();
            }
            void Act(string actor,string action) {
                var decision=session.Submit(actor,action);var dispatched=false;
                while(session.TryAction(out var next))if(ReferenceEquals(next,decision))dispatched=true;
                Expect(dispatched && session.BeginWork(decision));
                for(var i=0;i<400 && !session.WorkElapsed(decision);i++)session.Step(1,false,false,"detective");
                Expect(session.WorkElapsed(decision) && session.HostCompleted(decision));
                for(var i=0;i<200 && session.Activity(actor) is not null;i++)session.Step(1,false,false,"detective");
                Expect(session.Activity(actor) is null);Settle();Expect(!session.HostCompleted(decision));
            }
            var voters=new[]{"detective","huo","shen","lucian","gu","ward","ruan","selma","elena","moran","margaret","veronica","mora","bertrand"};
            foreach(var actor in voters.Where(actor=>actor!="detective").Append("quer").Append("su"))
                Observe(actor,new FieldKey("day1-arrival","progress",actor),true);
            Observe("ruan",new FieldKey("C-south-gate","passage","ruan_left"),true);
            Act("quer","quer.request-return");
            Act("ward","ward.reply-to-quer-return");
            Act("ward","ward.tell-ruan-return-dispute");
            Act("lucian","lucian.tell-ruan-ration-plan");
            Act("ward","ward.post-return-request");
            Act("ruan","ruan.self-play");
            var selection=new FieldKey("c2.ruan-plan","choice","selected");
            if(session.View("ruan").Knowledge.GetValueOrDefault(selection) is not {Known:true} selected ||
               selected.Value.GetString()!="ruan.ch2.gambit-fourteen-votes-probes")
                throw new InvalidOperationException("Missing choice: "+JsonSerializer.Serialize(new {session.LastStrategy,knowledge=session.View("ruan").Knowledge.Select(item=>new {item.Key,item.Value})}));
            foreach(var actor in voters) { Act(actor,actor+".inspect-return-request");Act(actor,actor+".vote-return"); }
            var tally=new FieldKey("c2.return-votes","tally","approvals");
            Expect(session.World.Read(tally)!.Value.GetDouble()==14);
            Expect(!session.Available("detective","detective.vote-return"));
            Act("su","su.inspect-return-request");Act("su","su.tell-ruan-abstains");
            Act("demon","demon.inspect-return-tally");Act("demon","demon.report-return-tally");
            Act("ruan","ruan.claim-unanimity");
            var checkpoint=session.SaveState();
            Observe("quer",new FieldKey("c2.return-request","state","su_not_approved"),true);
            Act("quer","quer.reject-false-return");
            var location=new FieldKey("precision-probes","placement","location");
            Expect(session.World.Read(location)!.Value.GetString()=="quer");
            session=CreateSession();session.RestoreState(checkpoint);
            Act("quer","quer.inspect-own-probes");Act("quer","quer.hand-probes-to-ruan");
            Expect(session.World.Read(location)!.Value.GetString()=="ruan");
            Expect(!session.Available("quer","quer.hand-probes-to-ruan"));
        });
        Check("return case distinguishes cast votes from unanimity and leaves motive unknown",()=>{
            var theory=CaseTheory.Load("assets/data/cases/stage9_return_inquiry.dv");
            var original=theory.Scopes["day3_return_inquiry"];
            var stripped=theory.WithoutAssumptions("day3_return_inquiry");
            string Prove(CaseTheory value,string id)=>value.Prove("day3_return_inquiry",original.Theorems.Single(item=>item.Id==id).Conclusion).Status;
            Expect(Prove(stripped,"not_unanimous")=="unknown" && Prove(stripped,"misleading")=="unknown");
            var su=original.Axioms.Where(item=>item.Id=="su").ToArray();
            var afterSu=stripped.WithEvidence("day3_return_inquiry",su);
            Expect(Prove(afterSu,"not_unanimous")=="proved" && Prove(afterSu,"misleading")=="unknown");
            var full=afterSu.WithEvidence("day3_return_inquiry",original.Axioms.Where(item=>item.Id is "claim" or "tally"));
            Expect(Prove(full,"misleading")=="proved" && Prove(full,"limited")=="proved" && Prove(full,"motive")=="unknown");
        });
        Check("conversation target gates use target knowledge without granting it to the player",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage10-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            var spec=definition.Declaration.GetProperty("conversations").EnumerateArray()
                .Single(x=>x.GetProperty("id").GetString()=="detective.quer.ask-return-report");
            var key=new FieldKey("c2.false-return","state","claimed");
            Expect(!session.ConversationAvailable("detective",spec));
            var token=session.BeginObservation("quer",[key]);
            Expect(session.Receive("quer",new Observation("test/quer-witness",key,J(true),1,0,InformationOrigin.DeliberateObservation,J(new {source="unit"}))));
            session.EndObservation("quer",token);
            for(var n=0;n<20;n++)session.Step(0,false,false,"detective");
            Expect(session.ConversationAvailable("detective",spec));
            Expect(session.View("detective").Knowledge.GetValueOrDefault(key) is not {Known:true});
        });
        Check("protection guard follows the victim's actual knowledge",()=>{
            var source=File.ReadAllText("assets/data/stage10-character-session.dv")
                .Replace("backflow_active = { known = true; value = false;", "backflow_active = { known = true; value = true;")
                .Replace("placement = { position = [4.5,8.43,-11.5]; yaw = 0; room = \"T01\"; }; autonomous = true;", "placement = { position = [4.5,8.43,-11.5]; yaw = 0; room = \"T01\"; }; autonomous = false;")
                .Replace("mind = { subject = \"demon\"; objects = {", "mind = { subject = \"demon\"; objects = { \"night4-knowledge\" = { facets = { hazard = { specific_lethal = { known = true; value = true; confidence = 1; }; }; }; }; \"night4-presence\" = { facets = { in_danger_zone = { elena = { known = true; value = true; confidence = 1; }; }; }; };");
            var definition=new CharacterSessionDefinition(source,File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create(File.ReadAllText("assets/data/cases/r3_opening_history.dv"));
            session.ReportPlacement("elena",11.7,-8.4,-1,0,"B2-control");
            Expect(session.Available("demon","demon.reach-unaware-elena"));
            var key=new FieldKey("night4-knowledge","hazard","specific_lethal");
            var token=session.BeginObservation("elena",[key]);
            Expect(session.Receive("elena",new Observation("test/understood",key,J(true),1,0,InformationOrigin.DeliberateObservation,J(new {source="unit"}))));
            session.EndObservation("elena",token);
            for(var n=0;n<20;n++)session.Step(0,false,false,"detective");
            Expect(!session.Available("demon","demon.reach-unaware-elena"));
        });
        Check("travel eligibility follows the actor's actual distance to the DV target",()=>{
            var definition=new CharacterSessionDefinition(File.ReadAllText("assets/data/stage3-character-session.dv"),
                File.ReadAllText("assets/data/98-runtime-session.data.dv"));
            var session=definition.Create();
            Expect(session.Available("su","su.walk-to-room"));
            session.ReportPlacement("su",61.85,8.43,-29.7,0,"B-P");
            Expect(!session.Available("su","su.walk-to-room"));
            var refused=false;try {session.Submit("su","su.walk-to-room");} catch(InvalidOperationException){refused=true;}
            Expect(refused);
            for(var n=0;n<120;n++)session.Step(1,true,false,"detective");
            Expect(session.Activity("su") is null);
            session.ReportPlacement("su",61.85,8.43,-29.2,0,"B-P");
            Expect(session.Available("su","su.walk-to-room"));
            session.ReportPlacement("su",61.85,4.23,-29.7,0,"1F");
            Expect(session.Available("su","su.walk-to-room"));
        });
        return failed==0?0:1;
    }
}
// --- Source: RoomBundleChecks.cs ---

internal static class RoomBundleChecks
{
    public static int Run()
    {
        var scopeFile = "assets/data/stage1-zone-b.dv";
        if (!File.Exists(scopeFile))
        {
            Console.WriteLine("SKIP RoomBundleChecks: stage1-zone-b.dv not found");
            return 0;
        }
        var failed=0;
        void Check(string name,Action run) {
            var clock=Stopwatch.StartNew();
            try { run(); Console.WriteLine($"PASS {name} {clock.Elapsed.TotalMilliseconds:F3} ms"); }
            catch(Exception error) { failed++;Console.WriteLine($"FAIL {name}: {error.Message}"); }
        }
        static void Expect(bool value) { if(!value)throw new InvalidDataException("Assertion failed."); }
        static string PathOf(string resource)=>Path.GetFullPath(resource.Replace("res://",""));
        static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(PathOf(path)))).ToLowerInvariant();
        var scope=DvScriptReader.Parse(File.ReadAllText(scopeFile)).GetProperty("data").GetProperty("stage1.zone_b").GetProperty("value");
        var source=DvScriptReader.Parse(File.ReadAllText(PathOf(scope.GetProperty("source").GetString()!))).GetProperty("data").GetProperty("space.configurable_building").GetProperty("value");
        var manifest=JsonDocument.Parse(File.ReadAllText(".runtime/generated/stage1/bundles/manifest.json")).RootElement;
        Check("compiled room metadata matches current DV publication",()=>{
            Expect(Hash(scope.GetProperty("source").GetString()!)==manifest.GetProperty("source_sha256").GetString());
            Expect(Hash($"res://{scopeFile}")==manifest.GetProperty("scope_sha256").GetString());
            var floor=source.GetProperty("floors").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==scope.GetProperty("floor").GetString());
            foreach(var id in scope.GetProperty("rooms").EnumerateArray().Concat(scope.GetProperty("boundary_rooms").EnumerateArray())) {
                var authored=floor.GetProperty("rooms").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id.GetString());
                var expected=authored.GetProperty("bbox").EnumerateArray().Select(x=>x.GetDouble());
                var actual=manifest.GetProperty("rooms").GetProperty(id.GetString()!).EnumerateArray().Select(x=>x.GetDouble()).ToArray();
                Expect(actual.Length==4 && expected.Zip(actual).All(pair=>Math.Abs(pair.First-pair.Second)<1e-8));
            }
        });
        Check("every room owns resource bundles and all bundle checksums match",()=>{
            var rooms=manifest.GetProperty("rooms").EnumerateObject().Select(p=>p.Name).ToHashSet();
            var owned=new HashSet<string>();var paths=new HashSet<string>();
            foreach(var b in manifest.GetProperty("bundles").EnumerateArray()) {
                var path=b.GetProperty("path").GetString()!;
                Expect(paths.Add(path) && Hash(path)==b.GetProperty("sha256").GetString());
                foreach(var owner in b.GetProperty("rooms").EnumerateArray()) {Expect(rooms.Contains(owner.GetString()!));owned.Add(owner.GetString()!);}
            }
            Expect(rooms.SetEquals(owned));
        });
        Check("boundary view regions derive dimensions from authored rooms",()=>{
            var floor=source.GetProperty("floors").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==scope.GetProperty("floor").GetString());
            foreach(var region in scope.GetProperty("boundary_regions").EnumerateArray()) {
                var authored=floor.GetProperty("rooms").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==region.GetProperty("room_ref").GetString()).GetProperty("bbox");
                var actual=manifest.GetProperty("rooms").GetProperty(region.GetProperty("id").GetString()!);
                var lateral=manifest.GetProperty("rooms").GetProperty(region.GetProperty("lateral_ref").GetString()!);
                var margin=region.GetProperty("lateral_margin_m").GetDouble();
                Expect(Math.Abs(actual[0].GetDouble()-Math.Max(authored[0].GetDouble(),lateral[0].GetDouble()-margin))<1e-8);
                Expect(Math.Abs(actual[2].GetDouble()-Math.Min(authored[2].GetDouble(),lateral[2].GetDouble()+margin))<1e-8);
                Expect(actual[1].GetDouble()==authored[1].GetDouble());
                var expectedEnd=region.TryGetProperty("cap_at_room_end",out var cap)&&cap.GetBoolean()?authored[3].GetDouble():manifest.GetProperty("rooms").GetProperty(region.GetProperty("connect_to").GetString()!)[1].GetDouble();
                Expect(actual[3].GetDouble()==expectedEnd);
            }
        });
        Check("room portals connect both authored sides including gate exterior",()=>{
            foreach(var portal in manifest.GetProperty("openings").EnumerateArray()) {
                var links=portal.GetProperty("room_links").EnumerateArray().Select(x=>x.GetString()!).ToArray();
                Expect(links.Length==2 && links.Distinct().Count()==links.Length);
                Expect(links.All(id=>manifest.GetProperty("rooms").TryGetProperty(id,out _)));
                Expect(portal.GetProperty("top").GetDouble()>portal.GetProperty("bottom").GetDouble());
            }
        });
        Check("self-closing boundary leaf replaces all obsolete rotor collision",()=>{
            var gate=manifest.GetProperty("openings").EnumerateArray().Single(o=>o.GetProperty("id").GetString()==scope.GetProperty("gate").GetString());
            var leaf=gate.GetProperty("door");
            Expect(leaf.GetProperty("self_close_seconds").GetDouble()>0);
            Expect(leaf.GetProperty("initial_angle").GetDouble()==0);
            Expect(leaf.GetProperty("angle_min").GetDouble()<=leaf.GetProperty("open_angle").GetDouble());
            var replaced=leaf.GetProperty("replaces_colliders").EnumerateArray().Select(x=>x.GetString()).ToHashSet();
            var floor=source.GetProperty("floors").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==scope.GetProperty("floor").GetString());
            var mechanism=gate.GetProperty("source_mechanism").GetProperty("point").EnumerateArray().Select(x=>x.GetDouble()).ToArray();
            foreach(var collider in floor.GetProperty("colliders").EnumerateArray()) {
                if(collider.GetProperty("kind").GetString()!="gate_rotor" || !collider.TryGetProperty("rect",out var rectangle))continue;
                var r=rectangle.EnumerateArray().Select(x=>x.GetDouble()).ToArray();
                if(Math.Abs((r[0]+r[2])/2-mechanism[0])<1 && Math.Abs((r[1]+r[3])/2-mechanism[1])<1)
                    Expect(replaced.Contains(collider.GetProperty("id").GetString()));
            }
        });
        Check("circular blast hatch dimensions align bore plate hinge and floor gap",()=>{
            var gate=manifest.GetProperty("openings").EnumerateArray().Single(o=>o.GetProperty("id").GetString()==scope.GetProperty("gate").GetString());
            var frame=gate.GetProperty("frame");var leaf=gate.GetProperty("door");
            Expect(frame.GetProperty("profile").GetString()=="circular_blast" && leaf.GetProperty("profile").GetString()=="circular_blast");
            var radius=frame.GetProperty("clear_diameter_m").GetDouble()/2;
            var width=leaf.GetProperty("width").GetDouble();
            Expect(width==leaf.GetProperty("height").GetDouble() && width/2>radius && width<gate.GetProperty("structural_width").GetDouble());
            var floor=gate.GetProperty("bottom").GetDouble();
            var hingeZ=leaf.GetProperty("hinge_design")[2].GetDouble();
            Expect(Math.Abs(hingeZ-floor-gate.GetProperty("source_mechanism").GetProperty("under_gap").GetDouble())<1e-8);
            Expect(Math.Abs(hingeZ+width/2-floor-frame.GetProperty("center_height_m").GetDouble())<1e-8);
            Expect(frame.GetProperty("center_height_m").GetDouble()-radius>=0);
            Expect(leaf.GetProperty("dog_count").GetDouble()>=3 && leaf.GetProperty("wheel_radius_m").GetDouble()>0);
        });
        Check("rectangular hinges clear their frame face and floor",()=>{
            var floor=source.GetProperty("floors").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==scope.GetProperty("floor").GetString());
            foreach(var opening in manifest.GetProperty("openings").EnumerateArray()) {
                var leaf=opening.GetProperty("door");
                if(leaf.TryGetProperty("profile",out var profile)&&profile.GetString()=="circular_blast")continue;
                var rect=opening.GetProperty("rect");var hinge=leaf.GetProperty("hinge_design");
                var axis=rect[2].GetDouble()-rect[0].GetDouble()<rect[3].GetDouble()-rect[1].GetDouble()?0:1;
                var center=(rect[axis].GetDouble()+rect[axis+2].GetDouble())/2;
                var frame=opening.GetProperty("frame");
                var offset=frame.GetProperty("depth").GetDouble()/2+leaf.GetProperty("thickness").GetDouble()/2+leaf.GetProperty("hinge_face_clearance_m").GetDouble();
                Expect(Math.Abs(Math.Abs(hinge[axis].GetDouble()-center)-offset)<1e-8);
                var room=floor.GetProperty("rooms").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==leaf.GetProperty("opens_into").GetString()).GetProperty("bbox");
                Expect((hinge[axis].GetDouble()-center)*((room[axis].GetDouble()+room[axis+2].GetDouble())/2-center)>0);
                var gap=leaf.GetProperty("floor_gap_m").GetDouble();
                Expect(Math.Abs(hinge[2].GetDouble()-opening.GetProperty("bottom").GetDouble()-gap)<1e-8);
                Expect(Math.Abs(leaf.GetProperty("height").GetDouble()+2*gap-frame.GetProperty("net_height").GetDouble())<1e-8);
            }
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
// --- Source: CaseProofAuthoring.cs ---

/// <summary>Maintained authoring command. Cases remain in DV; generated Lean and receipts stay in .runtime.</summary>
internal static class CaseProofAuthoring
{
    public static int Run(string[] arguments)
    {
        if (arguments.Length is < 3 or > 4)
            throw new ArgumentException("Usage: --verify-case-proofs <project-local-lean-executable> <.runtime-output-directory> [case-id]");
        var root = Path.GetFullPath(Environment.CurrentDirectory);
        var generatedRoot = Path.GetFullPath(Path.Combine(root, ".runtime")) + Path.DirectorySeparatorChar;
        var executable = Path.GetFullPath(arguments[1]);
        var output = Path.GetFullPath(arguments[2]);
        if (!executable.StartsWith(generatedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(executable))
            throw new ArgumentException("Use the verified project-local Lean executable.");
        if (!output.StartsWith(generatedRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Generated proofs must stay inside the project's .runtime directory.");
        Directory.CreateDirectory(output);
        var version = Invoke(executable, new[] { "--version" }, output);
        if (version.Code != 0) throw new InvalidOperationException("Lean did not report its version: " + version.Error);
        var results = new List<object>();
        var failed = 0;
        var leanChecked = 0;
        var rejectedBeforeExport = 0;
        var unexportedGoals = new List<object>();
        var caseFiles = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Content", "Cases"), "*.dv")
            .ToDictionary(x => Path.GetFileNameWithoutExtension(x), StringComparer.Ordinal);
        foreach (var file in caseFiles.Values.Order(StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file, Encoding.UTF8);
            var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
            var caseId = Path.GetFileNameWithoutExtension(file);
            var data = DvScriptReader.Parse(source).GetProperty("data");
            if (arguments.Length == 4 && caseId != arguments[3] &&
                !(data.TryGetProperty("case_history_reference_audit", out var relatedAudit) && relatedAudit.GetProperty("case_id").GetString() == arguments[3])) continue;
            var contexts = new List<(string Id, JsonElement History, JsonElement Request, string? Rejection)>();
            var historySourceHash = sourceHash;
            var sourceCaseId = caseId;
            if (data.TryGetProperty("case_history", out var history))
            {
                contexts.Add(("full", history, default, null));
                if (history.TryGetProperty("branches", out var branches))
                    foreach (var branch in branches.EnumerateArray())
                    {
                        var request = new CaseHistory.CheckRequest(
                            Events: branch.GetProperty("events").EnumerateArray().Select(x => x.GetString()!).ToArray(),
                            Goals: branch.GetProperty("goals").EnumerateArray().Select(x => x.GetString()!).ToArray());
                        contexts.Add((branch.GetProperty("id").GetString()!, CaseHistory.Project(history, request), branch, null));
                    }
            }
            if (data.TryGetProperty("case_history_reference_audit", out var historyAudit))
            {
                sourceCaseId = historyAudit.GetProperty("case_id").GetString()!;
                var referencedSource = File.ReadAllText(caseFiles[sourceCaseId], Encoding.UTF8);
                historySourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(referencedSource)));
                var referencedHistory = DvScriptReader.Parse(referencedSource).GetProperty("data").GetProperty("case_history");
                foreach (var mutation in historyAudit.GetProperty("mutations").EnumerateArray())
                {
                    if (!mutation.TryGetProperty("lean_obligation", out var obligation)) continue;
                    var target = obligation.GetString() switch
                    {
                        "schedule" => "scheduleValid", "transitions" => "runResult.1", "final" => "finalValid",
                        _ => throw new FormatException("Unknown Lean mutation obligation")
                    };
                    var mutatedHistory = referencedHistory;
                    if (mutation.TryGetProperty("lean_events", out var roots))
                        mutatedHistory = CaseHistory.Project(referencedHistory, new CaseHistory.CheckRequest(
                            Events: roots.EnumerateArray().Select(x => x.GetString()!).ToArray(),
                            Goals: mutation.TryGetProperty("lean_goals", out var goals) ? goals.EnumerateArray().Select(x => x.GetString()!).ToArray() : null));
                    var node = JsonNode.Parse(mutatedHistory.GetRawText())!.AsObject();
                    var actionId = mutation.GetProperty("action").GetString();
                    var action = node["actions"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == actionId)!;
                    foreach (var field in mutation.GetProperty("set").EnumerateObject()) action[field.Name] = JsonNode.Parse(field.Value.GetRawText());
                    contexts.Add((mutation.GetProperty("id").GetString()!, JsonSerializer.SerializeToElement(node), default, target));
                }
            }
            foreach (var context in contexts)
            {
                var module = "History_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(caseId + ":" + context.Id)));
                var exported = CaseHistoryLean.Export(context.History, module, context.Request, context.Rejection);
                var path = Path.Combine(output, module + ".lean");
                File.WriteAllText(path, exported.Source, new UTF8Encoding(false));
                var receipt = Invoke(executable, new[] { path }, output, 180000);
                leanChecked++;
                var passed = receipt.Code == 0 && !receipt.Output.Contains("sorryAx", StringComparison.Ordinal)
                    && !receipt.Output.Contains("declaration uses 'sorry'", StringComparison.Ordinal)
                    && !receipt.Error.Contains("declaration uses 'sorry'", StringComparison.Ordinal);
                if (!passed) failed++;
                Console.WriteLine(JsonSerializer.Serialize(new { case_id = caseId, context = context.Id, check = "lean4-computable-history", passed, exit_code = receipt.Code }));
                results.Add(new { case_id = caseId, source_case_id = sourceCaseId, id = "declared-history-transitions", context = context.Id, dv_sha256 = sourceHash, history_dv_sha256 = historySourceHash,
                    check = "lean4-computable-history", expected = context.Rejection is null ? "valid" : "rejected", rejection_target = context.Rejection, passed, generated_file = path,
                    generated_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exported.Source))),
                    exported.Scope, exported.Covered, exported.NotCovered, exported.Actors, exported.Events, exported.SelectedEvents,
                    active_actors = context.History.GetProperty("actions").EnumerateArray().Select(x => x.GetProperty("actor").GetString()).Distinct().Order().ToArray(),
                    start = context.History.GetProperty("start").GetInt32(), end = context.History.GetProperty("end").GetInt32(),
                    dependencies = context.History.TryGetProperty("projection", out var projection) ? (JsonElement?)projection.GetProperty("dependencies").Clone() : null,
                    exit_code = receipt.Code, stdout = receipt.Output, stderr = receipt.Error });
            }
            if (!data.TryGetProperty("natural_deduction_audit", out var audit)) continue;
            var index = 0;
            foreach (var request in audit.GetProperty("checks").EnumerateArray())
            {
                index++;
                var id = request.GetProperty("id").GetString()!;
                var expected = request.GetProperty("expect").GetString();
                var checkedProof = NaturalDeduction.Check(request);
                if (expected == "invalid")
                {
                    rejectedBeforeExport++;
                    if (checkedProof.Valid) failed++;
                    results.Add(new { case_id = caseId, id, dv_sha256 = sourceHash,
                        check = "csharp-rejection-before-export", passed = !checkedProof.Valid, errors = checkedProof.Errors });
                    continue;
                }
                if (expected != "valid" || !checkedProof.Valid)
                {
                    failed++;
                    results.Add(new { case_id = caseId, id, check = "csharp-proof", passed = false, errors = checkedProof.Errors });
                    continue;
                }
                // Encode file/module names independently of case text, which is never executable shell input.
                var stem = "Case_" + Convert.ToHexString(Encoding.UTF8.GetBytes(caseId)) + "_" + index;
                var leanSource = NaturalDeduction.ExportLean(request, stem);
                var path = Path.Combine(output, stem + ".lean");
                File.WriteAllText(path, leanSource, new UTF8Encoding(false));
                var receipt = Invoke(executable, new[] { path }, output);
                leanChecked++;
                var passed = receipt.Code == 0 && !receipt.Output.Contains("sorryAx", StringComparison.Ordinal)
                    && !receipt.Output.Contains("declaration uses 'sorry'", StringComparison.Ordinal)
                    && !receipt.Error.Contains("declaration uses 'sorry'", StringComparison.Ordinal);
                if (!passed) failed++;
                results.Add(new { case_id = caseId, id, dv_sha256 = sourceHash, check = "lean4-kernel",
                    passed, generated_file = path,
                    generated_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(leanSource))),
                    axioms_used_in_dv = checkedProof.AxiomsUsed, exit_code = receipt.Code,
                    stdout = receipt.Output, stderr = receipt.Error });
            }
            if (data.TryGetProperty("case_design", out _))
            {
                var theory = CaseTheory.Parse(source);
                foreach (var scope in theory.Scopes.Values)
                foreach (var theorem in scope.Theorems)
                {
                    var judgment = theory.Prove(scope.Id, theorem.Conclusion);
                    if (judgment.Status == "unknown")
                    {
                        unexportedGoals.Add(new { case_id = caseId, scope = scope.Id, goal = theorem.Id, status = "unknown" });
                        continue;
                    }
                    foreach (var sign in new[] { false, true })
                    {
                        if (sign ? judgment.Refutation is null : judgment.Support is null) continue;
                        var goal = sign ? theorem.Conclusion.Opposite : theorem.Conclusion;
                        var request = CaseLeanBridge.BuildRequest(theory, scope.Id, goal);
                        var stem = "Goal_" + Convert.ToHexString(Encoding.UTF8.GetBytes(caseId + ":" + scope.Id + ":" + theorem.Id)) + (sign ? "_negative" : "_positive");
                        // The module name is a short stable identifier; source identities stay in the receipt.
                        var module = "Goal_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stem)));
                        var leanSource = NaturalDeduction.ExportLean(request, module);
                        var path = Path.Combine(output, module + ".lean");
                        File.WriteAllText(path, leanSource, new UTF8Encoding(false));
                        var receipt = Invoke(executable, new[] { path }, output);
                        leanChecked++;
                        var passed = receipt.Code == 0 && !receipt.Output.Contains("sorryAx", StringComparison.Ordinal)
                            && !receipt.Output.Contains("declaration uses 'sorry'", StringComparison.Ordinal)
                            && !receipt.Error.Contains("declaration uses 'sorry'", StringComparison.Ordinal);
                        if (!passed) failed++;
                        results.Add(new { case_id = caseId, scope = scope.Id, id = theorem.Id, negative = sign,
                            dv_sha256 = sourceHash, check = "horn-certificate-to-lean4-kernel", passed,
                            generated_file = path,
                            generated_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(leanSource))),
                            exit_code = receipt.Code, stdout = receipt.Output, stderr = receipt.Error });
                    }
                }
            }
        }
        var report = new { kind = "dv-case-authoring-lean-verification", created_utc = DateTimeOffset.UtcNow,
            lean = executable, version = version.Output.Trim(), checks = results.Count, lean_checked = leanChecked,
            rejected_before_export = rejectedBeforeExport, failed,
            coverage = "Explicit DV proof terms and independently computed typed-history scheduling/world transitions checked by generated Lean. Per-result coverage excludes cognition/Sim4/custody/physical motion where not exported. No gameplay completion claim.",
            unexported_goals = unexportedGoals, results };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var reportPath = Path.Combine(output, "verification.json");
        File.WriteAllText(reportPath, json, new UTF8Encoding(false));
        Console.WriteLine(JsonSerializer.Serialize(new { checks = results.Count, lean_checked = leanChecked,
            rejected_before_export = rejectedBeforeExport, failed, report = reportPath, version = version.Output.Trim() }));
        return failed == 0 && leanChecked > 0 ? 0 : 1;
    }

    private static (int Code, string Output, string Error) Invoke(string executable, IEnumerable<string> arguments, string directory, int timeoutMilliseconds = 30000)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Lean.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return (-1, stdout.GetAwaiter().GetResult(), "Lean deadline exceeded; no proof accepted. " + stderr.GetAwaiter().GetResult());
        }
        return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
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
