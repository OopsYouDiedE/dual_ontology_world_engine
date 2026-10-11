using System.Reflection;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
var runtime = new WorldRuntime();
using var simulation = new SimulationSession(runtime);
var world = new CoreScriptWorld(runtime, simulation);
var actor = world.DefineCharacter("neutral.actor", "actor", world.DefineSpace("neutral.room", "room"));
var snapshot = ((ContractResult<CognitionSnapshotV1>.Success)actor.Soul.World.CaptureSnapshot()).Value;
var cognition = new PlayerCognition(snapshot, actor.Self.SubjectId!.Value);
var count = 0;
void Check(string name, bool value) { if (!value) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); count++; }
SubjectivePlayerController Fresh() => new(new() { Actor="neutral.actor", Name="actor", Mode="agent", Rules=[new(){ Mode="prefer", Kind="untried", Weight=6 }] }, 11);
SubjectiveDecisionMemory Memory(SubjectivePlayerController c) => (SubjectiveDecisionMemory) typeof(SubjectivePlayerController).GetField("_experience", BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(c)!;
PublicPlayerCandidate Wait(string label="wait") => new(label,label,"wait",false);
PublicPlayerCandidate Other() => new("other","other","menu",false);
PublicPlayerObservation Observe(params PublicPlayerCandidate[] items) => new(1,[],["visible"],[..items],false);
foreach (var variant in new[]{"same","text","lines","complete"}) {
 var c=Fresh();
 c.Decide(Observe(Other()),cognition);
 c.Decide(Observe(Wait("other-wait")),cognition);
 c.Decide(Observe(Wait()),cognition);
 var before=Memory(c).Capture().Experiences.Where(e=>e.Key.Method!="wait:wait").ToArray();
 var next=Observe(Wait(),Other()) with { Turn=22 };
 next=variant switch { "text"=>next with {Text=["changed"]}, "lines"=>next with {Lines=[new("actor","neutral","changed")]}, "complete"=>next with {Complete=true}, _=>next };
 var decision=c.Decide(next,cognition);
 var waiting=decision.Alternatives.Single(a=>a.Token=="wait");
 Check(variant+"_wait_score", variant=="same" ? waiting.Basis.Any(b=>b.StartsWith("revisit_without_new_accepted_material:")) : !waiting.Basis.Any(b=>b.StartsWith("revisit_without_new_accepted_material:")));
 var history=Memory(c);
 foreach(var prior in before) { var after=history.Get(prior.Key); Check(variant+"_preserves_"+prior.Key.Method, after.Attempts==prior.Attempts+(decision.Token=="other" && prior.Key.Method.EndsWith("other") ? 1 : 0) && after.Successes==prior.Successes && after.Failures==prior.Failures); }
 Check(variant+"_success_receipt",history.Get(new("wait:wait")).Successes==(variant=="same"?0:1));
 Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(decision));
}
Console.WriteLine($"Neutral wait checks: {count} passed, 0 failed");
