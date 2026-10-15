using System.Reflection;
using System.Text.Json;
using DuelView.Compiled;

// Neutral mechanism counterexample. No campaign, oracle, answer, or game run.
object N(int x) => R.number(x.ToString());
object L(params object[] xs) => xs.ToList();
object Rec(params object[] pairs) => Enumerable.Range(0, pairs.Length / 2)
    .Select(i => L(pairs[2*i], pairs[2*i+1])).ToList();
object Call(string name, params object[] xs) => typeof(DVProgram)
    .GetMethod("F_" + name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, xs)!;
object Get(object x, string k) => Call("dv_find", x, k, false);
object Put(object x, string k, object v) => Call("dv_with", x, k, v);
var yes = Call("dv_true");
var mind = Call("dv_world", "neutral.mind", "mind", "neutral");
var physical = Call("dv_world", "neutral.physical", "physical", "");
var loc = Call("dv_ref", "neutral", "state", "location");
var action = Rec("id", "neutral.reply", "kind", "talk", "precondition", yes,
    "scope", L(), "response_topic", "neutral.topic", "offer_to", "remote",
    "speech", Rec("literal_ast", yes), "request_transport", Rec("channel", "neutral.channel"));
var actor = Rec("id", "neutral", "mind", mind, "location_ref", loc, "location", "room-a",
    "policy", Rec("budget", N(10), "plan_budget", N(100), "plan_depth", N(1), "goals", L()),
    "actions", L(action), "visible_context", L(),
    "request_channels", L(Rec("target", "remote", "channel", "neutral.channel", "basis", L("neutral source"), "available_when", yes)));
var costs = Rec("talk", Rec("actual", Rec("time", N(0)), "maximum", Rec("time", N(0))));
var first = Call("dvs_choose", actor, L(), costs);
actor = Put(actor, "idle_choice_action", Get(first, "action"));
actor = Put(actor, "idle_mind_revision", Get(mind, "revision"));
actor = Put(actor, "idle_time", N(0));
actor = Put(actor, "idle_location", "room-a");
actor = Put(actor, "idle_visible_context", L());
var observations = Call("dvs_speech_received_observations", mind, yes, "source-neutral", "remote", N(0), Rec("status", "ordinary", "confidence_cap", N(1)));
var profile = Call("dv_observation_profile", Rec("perception", N(1), "testimony", N(1), "recall", N(1)), R.number("0.5"));
var updated = Call("dv_update_mind", mind, observations, Get(mind, "revision"), profile);
actor = Put(actor, "mind", Get(updated, "world"));
var received = Call("dvs_receive_request", actor,
    Rec("request", Rec("target", "neutral", "topic", "neutral.topic")), "parse-neutral", "source-neutral", "remote");
var second = Call("dvs_choose", received, L(), costs);
var events = L(Rec("id", "e0"));
var state = Rec("story", L(), "physical", physical, "actors", L(received), "events", events,
    "bindings", L(), "costs", costs, "run_start", N(0), "duration_budget", N(100),
    "horizon", N(100), "duration_blocked", false, "limit", N(10), "cursor", N(0),
    "executed", N(0), "idle", N(0), "status", "neutral", "initial", L());
var prepared = Call("dvs_session_prepare", state);
Console.WriteLine(JsonSerializer.Serialize(new {
    scope = "neutral mechanism probe; not gameplay acceptance",
    initial_choice = Get(first, "action"),
    received_observation_count = ((List<object>)observations).Count,
    mind_revision_unchanged = R.Bool(Call("dv_same", Get(Get(received, "mind"), "revision"), Get(mind, "revision"))),
    received_request_count = ((List<object>)Get(received, "received_requests")).Count,
    uncached_choice = Get(Get(second, "action"), "id"),
    cached_prepare_pending = Get(prepared, "pending"),
    cached_prepare_idle = Get(prepared, "idle"),
    cached_prepare_event_count = ((List<object>)Get(prepared, "events")).Count
}, new JsonSerializerOptions { WriteIndented = true }));
