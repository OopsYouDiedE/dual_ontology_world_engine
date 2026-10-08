using System.Collections;
using System.Reflection;
using DualOntologyWorldEngine.Awakening;
using Ev = System.Collections.Generic.Dictionary<string, object?>;

// Independent, injected mechanism fixture. It does not constitute gameplay evidence.
const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
int checks = 0, failures = 0;
void Check(bool ok, string label)
{
    checks++;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {label}");
}
object? Field(Chapter1Session session, string name) => typeof(Chapter1Session).GetField(name, Private)!.GetValue(session);
object? Call(Chapter1Session session, string name, params object?[] args) => typeof(Chapter1Session).GetMethod(name, Private)!.Invoke(session, args);
Ev BeginPrivate(Chapter1Session session, string name, params object?[] args)
{
    var body = Call(session, name, args)!;
    Call(session, "Start", body);
    Call(session, "Pump");
    return ((IEnumerable<Ev>)Field(session, "_out")!).Single(ev => ev.GetValueOrDefault("op")?.ToString() == "walk");
}
bool Stage(Ev ev, string op, string who) => ev.GetValueOrDefault("op")?.ToString() == op && ev.GetValueOrDefault("who")?.ToString() == who;

foreach (string reason in new[] { "stuck", "no_route", "cancelled", "arrived" })
{
    var session = new Chapter1Session();
    var walk = BeginPrivate(session, "WalkTo", "huo", new Ev { ["point"] = new object?[] { 10.0, 20.0 } }, 150.0);
    Check(((IDictionary)Field(session, "_staged")!).Contains("huo"), $"WalkTo {reason} holds actor while awaiting");
    var output = session.Dispatch(new Ev { ["kind"] = "presented", ["presentation_id"] = walk["presentation_id"], ["ok"] = reason == "arrived", ["reason"] = reason });
    Check(!((IDictionary)Field(session, "_staged")!).Contains("huo") && (int)session.Probe("jobs", [])! == 0, $"WalkTo {reason} ack releases actor and waiting job");
    bool fallback = output.Any(ev => Stage(ev, "pose", "huo") && ev.GetValueOrDefault("anim")?.ToString() == "idle") && output.Any(ev => Stage(ev, "face", "huo"));
    Check(fallback == (reason is "stuck" or "no_route"), $"WalkTo {reason} fallback policy");
    Check(!output.Any(ev => ev.GetValueOrDefault("kind")?.ToString() == "error"), $"WalkTo {reason} progresses without error");
}

foreach (string reason in new[] { "stuck", "no_route", "cancelled" })
{
    var session = new Chapter1Session();
    var walk = BeginPrivate(session, "Summon", "huo");
    var output = session.Dispatch(new Ev { ["kind"] = "presented", ["presentation_id"] = walk["presentation_id"], ["ok"] = false, ["reason"] = reason });
    Check((int)session.Probe("jobs", [])! == 0 && !((IDictionary)Field(session, "_staged")!).Contains("huo"), $"Summon {reason} releases nested claims and job");
    Check(output.Any(ev => Stage(ev, "face", "huo")) == (reason != "cancelled"), $"Summon {reason} continues facing only on non-cancellation");
    Check(!output.Any(ev => ev.GetValueOrDefault("kind")?.ToString() == "error"), $"Summon {reason} progresses without error");
}
Console.WriteLine($"RESULT {checks} checks, {failures} failures");
return failures == 0 ? 0 : 1;
