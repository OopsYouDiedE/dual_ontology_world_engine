using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
var runtime = new WorldRuntime();
using var simulation = new SimulationSession(runtime);
var world = new CoreScriptWorld(runtime, simulation);
var room = world.DefineSpace("room", "空间");
var actor = world.DefineCharacter("actor", "主体", room);
var token = world.DefineObject("token", "件", room);
var mental = world.Observe(actor, token).Single();
var prepared = simulation.Submit(actor.Soul, new(actor.Self, new Effect.Change(mental, new("counter"), 1), 1));
Observation.Observe(actor.Soul, new(mental.Name, mental.Kind) { Target = mental, Forget = true });
var containedAtActivation = actor.Soul.World.Contains(mental);
simulation.CTB.RunUntil(1);
var other = world.DefineObject("other", "另一件", room);
var replacement = runtime.Representations(other).Single();
// Re-observe the original entity; the old removed mental object is not resurrected.
var current = world.Observe(actor, token).Single();
string rebind;
try { runtime.Associate(actor.Soul, current, replacement); rebind = "NO_EXCEPTION"; }
catch (Exception exception) { rebind = exception.GetType().Name + ": " + exception.Message; }
var afterRebind = simulation.Submit(actor.Soul, new(actor.Self, new Effect.Change(current, new("counter"), 1), 0));
simulation.CTB.RunUntil(1);
Console.WriteLine(JsonSerializer.Serialize(new { schema = "D01A-reference-observations/v1", id = "X02",
    targetContainedAtActivation = containedAtActivation, targetForgottenAfterSubmit = true, status = prepared.Status.ToString(), failure = prepared.Result!.Failure.ToString(),
    counterAfterForgottenReferenceExecution = token.Get<StateProperty>()!.Values[new("counter")] - 1,
    observedAgainCreatesNewMentalReference = !ReferenceEquals(mental, current), rebind,
    originalBindingStillExecuted = afterRebind.Status.ToString(), originalEntityCounter = token.Get<StateProperty>()!.Values[new("counter")],
    otherEntityCounter = other.Get<StateProperty>()!.Values.GetValueOrDefault(new("counter")),
    interpretation = "Existing Core preserves the submitted target binding after mental Forget. Whether an accepted intent should be invalidated is an unapproved policy choice." },
    new JsonSerializerOptions { WriteIndented = true }));
