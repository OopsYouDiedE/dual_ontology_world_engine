using System.Text.Json;
using System.Text.Json.Nodes;
using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using DualOntologyWorldEngine.Gameplay;
using static DualOntologyWorldEngine.Story.SessionPersistence;

namespace DualOntologyWorldEngine.Checks;

/// <summary>Injected headless mechanism checks, not a playtest or UI acceptance.</summary>
public static class SessionSaveCheck
{
    private static int checks;
    private static void Check(bool ok,string name) { if(!ok) throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
    private static ContractError Error<T>(ContractResult<T> r) => r is ContractResult<T>.Failure f ? f.Error : throw new Exception("Expected rejection");
    private static Dictionary<string,object?> Context(Chapter1Session session) => new()
    {
        ["field_active"]=false,["modal"]="",
        ["actors"]=session.World.PersistentSession.World.Characters.ToDictionary(p=>p.Key.Split('.').Last().ToLowerInvariant(),p=>(object?)new Dictionary<string,object?>
        {
            ["room"]=session.ContentSession.Session.CharacterRooms.GetValueOrDefault(p.Key.Split('.').Last().ToLowerInvariant(),""),
            ["entered"]="",["room_name"]="",["visible"]=true,["moving"]=false,["distance"]=40.0,["near_sites"]=Array.Empty<object>()
        })
    };
    private static List<Dictionary<string,object?>> Drive(Chapter1Session session,Dictionary<string,object?> input)
    {
        var trace=new List<Dictionary<string,object?>>(); var queue=new Queue<Dictionary<string,object?>>(); var externalModal=false;
        void Send(Dictionary<string,object?> message) { message["context"]=Context(session); foreach(var e in session.Dispatch(message)) queue.Enqueue(e); }
        Send(input);
        for(var budget=0;budget<10000;budget++)
        {
            if(queue.Count==0)
            {
                if(externalModal||Convert.ToInt32(session.Probe("jobs",[]))==0) return trace;
                Send(new() { ["kind"]="tick",["delta"]=1.0/60 }); continue;
            }
            var e=queue.Dequeue(); trace.Add(e); var kind=(string)e["kind"]!; var id=e.GetValueOrDefault("presentation_id");
            if(kind=="error") throw new Exception("Chapter error: "+e["message"]);
            if(kind=="lines")
            {
                if(e.GetValueOrDefault("choices") is object[] choices) Send(new() { ["kind"]="select",["presentation_id"]=id,["choice_id"]=choices.Cast<Dictionary<string,object?>>().First(c=>c["available"] is true)["id"] });
                else Send(new() { ["kind"]="presented",["presentation_id"]=id });
            }
            else if(kind=="form")
            {
                var fields=((object[])e["fields"]!).Cast<Dictionary<string,object?>>().ToDictionary(f=>(string)f["id"]!,f=>
                    f.GetValueOrDefault("value") ?? ((f.GetValueOrDefault("options") as object[])?.Cast<Dictionary<string,object?>>().FirstOrDefault()?.GetValueOrDefault("id") ?? ""));
                var action=((object[])e["actions"]!).Cast<Dictionary<string,object?>>().Last(a=>!a.ContainsKey("tab")&&a["id"] as string!="cancel")["id"];
                Send(new() { ["kind"]="form",["presentation_id"]=id,["action"]=action,["values"]=fields });
            }
            else if(kind=="stage"&&id is not null || kind=="replay_play") Send(new() { ["kind"]="presented",["presentation_id"]=id,["ok"]=true });
            else if(kind is "card" or "candidates"&&id is not null) externalModal=true;
        }
        throw new Exception("Chapter drive budget exhausted");
    }
    private static string Semantic(Chapter1Session session,List<Dictionary<string,object?>> trace) => JsonSerializer.Serialize(new
    {
        trace,snapshot=session.Snapshot(),progress=session.ContentSession.Session.CaptureProgress(),
        identities=session.World.PersistentSession.Runtime.PersistenceEntities.Keys.Select(e=>e.Value).Order(StringComparer.Ordinal),
        revisions=session.World.PersistentSession.Runtime.PersistenceEntities.Values.OfType<SoulEssence>().OrderBy(s=>s.Id.Value,StringComparer.Ordinal).Select(s=>new { id=s.Id.Value,revision=s.World.Revision.Value,vocabulary=s.World.VocabularyRevision.Value }),
        time=session.World.StoryTimeline.Now,sequence=session.World.StoryTimeline.Sequence,random=session.World.PersistentSession.RandomState
    });
    private static Dictionary<string,object?> NextInput(Chapter1Session session)
    {
        // Read a real authored interaction target; do not encode chapter prose or an answer in the harness.
        var card=session.ContentSession.Content.RootElement.GetProperty("SITE_INTERACTIONS").EnumerateObject().First(p=>p.Value.GetProperty("kind").GetString()=="evidence").Value.GetProperty("target").GetString();
        return new() { ["kind"]="card",["card_id"]=card };
    }
    public static int Write(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory); checks=0;
            var session=new Chapter1Session();
            session.Dispatch(new Dictionary<string,object?> { ["kind"]="begin",["context"]=Context(session) });
            Check(Error(session.Save()).Code==ContractErrorCode.UnsupportedSavePoint,"CB-V12 pending chapter fiber/presentation refuses save");
            session=new Chapter1Session(); var opening=Drive(session,new() { ["kind"]="begin" });
            Check(opening.Any(e=>e.GetValueOrDefault("kind") as string=="lines"),"authored first chapter actually advanced");
            var cut=session.CheckSavePoint();
            if(cut is ContractResult<bool>.Failure f) throw new Exception("Opening cut rejected: "+f.Error);
            var before=Semantic(session,[]); var save=Value(session.Save());
            Check(before==Semantic(session,[]),"save leaves live chapter observations/progress unchanged");
            var store=new Chapter1SessionStore(session); var restored=Value(store.Restore(save));
            Check(Semantic(restored,[])==before,"idle chapter save restores full state");
            var epoch=store.RestoreEpoch;
            Check(ReferenceEquals(restored,Value(store.Restore(save)))&&store.RestoreEpoch==epoch,"CB-V11 duplicate load reuses objects and epoch");
            var bad=JsonNode.Parse(save)!.AsObject(); bad["unknown_property"]=1;
            Check(Error(store.Restore(JsonSerializer.SerializeToUtf8Bytes(bad))).Code==ContractErrorCode.Unsupported&&ReferenceEquals(store.Current,restored),"failed load keeps running chapter");
            File.WriteAllBytes(Path.Combine(directory,"chapter.json"),save);
            File.WriteAllText(Path.Combine(directory,"expected.json"),Semantic(session,Drive(session,NextInput(session))));
            CheckSaveCuts();
            Console.WriteLine($"TOTAL {checks} save checks, 0 failed; WRITE COMPLETE"); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    public static int Read(string directory)
    {
        try
        {
            checks=0; var store=new Chapter1SessionStore(); var session=Value(store.Restore(File.ReadAllBytes(Path.Combine(directory,"chapter.json"))));
            var result=Semantic(session,Drive(session,NextInput(session)));
            File.WriteAllText(Path.Combine(directory,"actual.json"),result);
            Check(result==File.ReadAllText(Path.Combine(directory,"expected.json")),"fresh process first chapter continuation matches uninterrupted run");
            Console.WriteLine($"TOTAL {checks} save checks, 0 failed; READ COMPLETE"); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private sealed class UnsupportedProperty : Property;
    private static void CheckSaveCuts()
    {
        var w=new WorldSession(); var body=new Essence("body",ProjectionKind.Body); var soul=new SoulEssence("observer");
        var room=new Essence("space",ProjectionKind.Space); body.Add(new SpatialProperty { Space=room }); soul.Relations.Add((Relations.Controls,body));
        w.Runtime.Project(room,"space"); w.Runtime.Project(body,"body"); w.Runtime.Project(soul,"observer");
        var self=w.Runtime.Observe(soul,w.Runtime.Representations(body)).Single();
        var action=w.Simulation.Submit(soul,new(self,new Effect.Set(self,new("v"),1),1));
        Check(Error(w.Save()).Code==ContractErrorCode.UnsupportedSavePoint,"CB-V12 active action refuses save"); w.Simulation.Cancel(action);
        var delayed=w.Simulation.Submit(soul,new(self,new Effect.Wait(0,new Effect.Set(self,new("v"),1),new Condition.Constant(Truth.False)),0)); w.Simulation.CTB.Step();
        Check(Error(w.Save()).Code==ContractErrorCode.UnsupportedSavePoint,"CB-V12 waiting action refuses save"); w.Simulation.Cancel(delayed);
        body.Add(new StateProperty());
        var leased=w.Simulation.Submit(soul,new(self,new Effect.TemporarySet(self,new("v"),1,10),0)); w.Simulation.CTB.Step();
        Check(leased.Status==ExecutionStatus.Completed&&Error(w.Save()).Code==ContractErrorCode.UnsupportedSavePoint&&Error(w.Runtime.Save()).Code==ContractErrorCode.UnsupportedSavePoint,"CB-V12 active empty but effects/lease still refuse save"); w.Simulation.Cancel(leased);
        w.Simulation.Activate(soul,self);
        Check(Error(w.Save()).Code==ContractErrorCode.UnsupportedSavePoint,"CB-V12 autonomous wakeup refuses save"); w.Simulation.Deactivate(self);
        var pending=w.Simulation.CTB.Schedule(1,()=>{});
        Check(Error(w.Save()).Code==ContractErrorCode.UnsupportedSavePoint,"CB-V12 untyped scheduler callback refuses save"); w.Simulation.CTB.Cancel(pending);
        body.Add(new UnsupportedProperty());
        Check(Error(w.Runtime.Save()).Code==ContractErrorCode.Unsupported,"unknown Property refuses restoration schema");
        var external=new WorldSession(w.Runtime,new SimulationSession(w.Runtime));
        Check(Error(external.Save()).Code==ContractErrorCode.UnsupportedSavePoint,"CB-V12 external host without provider refuses save");
        external.SetSavePointGuard(()=>new ContractResult<bool>.Failure(new(ContractErrorCode.UnsupportedSavePoint,"host.fiber","Pending host work",CandidateIds:[])));
        Check(Error(external.Save()).Path=="host.fiber","CB-V12 host fiber provider rejection remains explicit");
        var game=new GameSession("{}","neutral-living","neutral-campus"); game.Dispatch(new Dictionary<string,object?> { ["kind"]="start" });
        Check(Error(game.Save()).Path=="host.view_receipt","CB-V12 pending GameSession view receipt refuses save"); game.Dispose();

        DialogueExpression Lit(object v)=>new() { Value=JsonSerializer.SerializeToElement(v) };
        var chapter=new ChapterWorldDefinition { Id="neutral",Entities=[new() { Id="room",Name="space",Kind="room" },
            new() { Id="actor",Name="body",Kind="character",Properties=new() { ["space"]=new() { Type="entity",Value=JsonSerializer.SerializeToElement("room") } } }],
            Candidates=[new() { Id="action",Kind="option",Actor="player",Policy=new() { Mode="player" },Response=[new() { Op="call",Name="set",Args=[Lit("actor"),Lit("value"),Lit(1)] },new() { Op="line",Text="neutral" }] }] };
        var world=new WorldSession(); world.Load(chapter);
        var choice=world.Process(new RequestCandidates("actor")).OfType<CandidatesAvailable>().Single().Candidates.Single();
        Check(Error(world.Save()).Path=="host.choice","CB-V12 pending candidate choice refuses save");
        world.Process(new SelectCandidate(choice.Token));
        var outputs=world.Process(new AdvanceWorld(10)); var presentation=outputs.OfType<SemanticDialogue>().Single();
        Check(Error(world.Save()).Path=="host.presentation","CB-V12 active empty but host presentation refuses save");
        world.Process(new PresentationCompleted(presentation.PresentationId)); world.Progress.Set("neutral","count",3); world.Progress.Set("clock","minute",10);
        world.NextRandom(); var bytes=Value(world.Save()); var expectedRandom=world.NextRandom();
        var store=new WorldSessionStore(world); var loaded=Value(store.Restore(bytes));
        string EventState(WorldSession s)=>JsonSerializer.Serialize(s.Runtime.EventAudit.Select(e=>new { kind=e.GetType().Name, fields=TypedEvents.Map(e,r=>r?.Id).Fields }));
        Check(world.Runtime.EventAudit.Count>0&&EventState(world)==EventState(loaded),"typed authority events/causes survive idle WorldSession restore");
        Check(loaded.NextRandom()==expectedRandom&&Equals(loaded.Progress.GetProgress("neutral")["count"],3)&&loaded.Simulation.CTB.Now==10&&loaded.Simulation.CTB.Sequence==world.Simulation.CTB.Sequence,
            "idle WorldSession progress/clock/random/serial restore");
        var count=loaded.Runtime.PersistenceEntities.Count; var epoch=store.RestoreEpoch;
        var eventCount=loaded.Runtime.EventAudit.Count; var eventState=EventState(loaded);
        Check(ReferenceEquals(loaded,Value(store.Restore(bytes)))&&store.RestoreEpoch==epoch&&loaded.Runtime.PersistenceEntities.Count==count&&loaded.Entity("actor").Get<StateProperty>()!.Values[new("value")]==1,
            "WorldSession duplicate load does not create objects or grant effects");
        Check(loaded.Runtime.EventAudit.Count==eventCount&&EventState(loaded)==eventState,"duplicate WorldSession load does not register typed events again");
        var corrupt=JsonNode.Parse(bytes)!.AsObject(); corrupt["now"]=-1;
        Check(Error(store.Restore(JsonSerializer.SerializeToUtf8Bytes(corrupt))).Code==ContractErrorCode.Unsupported&&ReferenceEquals(store.Current,loaded),"WorldSession invalid candidate leaves current unchanged");
    }
}
