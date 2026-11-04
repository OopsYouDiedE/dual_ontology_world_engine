using System.Diagnostics;
using System.Text.Json;
using DuelView.Runtime;
using DuelView.Dv;
using DuelView.Core;

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
