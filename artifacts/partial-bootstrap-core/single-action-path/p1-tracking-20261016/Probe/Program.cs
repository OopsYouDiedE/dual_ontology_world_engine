using System.Reflection;
using System.Text.Json;
using DuelView.Compiled;

object Call(string name, params object[] values) => typeof(DVProgram).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, values)!;
object Get(object value, string key) => Call("F_dv_find", value, key, false);
List<object> List(object value) => (List<object>)value;
string Str(object value) => value.ToString()!;
object JsonValue(object value) {
    if (value is Rational) return Str(value);
    if (value is not List<object> items) return value;
    if (items.Count > 0 && items.All(x => x is List<object> pair && pair.Count == 2 && pair[0] is string)) {
        var record = new Dictionary<string, object>();
        foreach (List<object> pair in items) record[(string)pair[0]] = JsonValue(pair[1]);
        return record;
    }
    return items.Select(JsonValue).ToArray();
}
void Write(string path, object value) { using var f = new FileStream(path, FileMode.CreateNew); JsonSerializer.Serialize(f, value, new JsonSerializerOptions { WriteIndented = true }); }
var output = Path.GetFullPath(args[0]);
var steps = args.Length > 1 ? args[1] : "32";
Console.WriteLine($"Starting fresh campaign helper, max completed actions={steps}; formal-model only.");
var watch = System.Diagnostics.Stopwatch.StartNew();
var state = Call("CampaignRun", R.number(steps));
var tracked = List(Call("CampaignTrackedActors"));
var trace = List(Call("CampaignChronologicalTrace", state));
var events = List(Get(state, "events"));
Write(Path.Combine(output, "tracked-trace.json"), JsonValue(trace));
var inversions = new List<object>();
for (int i = 1; i < trace.Count; i++) if (R.Bool(R.lt(Get(trace[i], "time"), Get(trace[i-1], "time")))) inversions.Add(new { previous = JsonValue(trace[i-1]), current = JsonValue(trace[i]) });
var absent = tracked.Where(id => !trace.Any(e => Equals(Get(e, "actor"), id))).ToArray();
var actors = List(Get(state, "actors")).Select(actor => new {
    actor = Get(actor, "id"),
    first_decision = events.Where(e => Equals(Get(e, "actor"), Get(actor, "id")) && new[] { "action_selected", "planning_stop" }.Contains(Str(Get(e, "type")))).Select(e => new { event_id=Get(e,"id"), time=Str(Get(e,"time")), type=Get(e,"type") }).FirstOrDefault()
}).ToArray();
var fields = new Dictionary<string,string[]> {
    ["action_execution"] = new[]{"physical_before", "physical_after", "receipt"},
    ["answer_submitted"] = new[]{"mind", "physical"},
    ["question_transition"] = new[]{"world"},
    ["model_recheck"] = new[]{"actor_before"},
    ["mind_simulation"] = new[]{"actor_before"}
};
var evidence = fields.Select(entry => new { type=entry.Key, first_event=events.Where(e => Str(Get(e,"type"))==entry.Key).Select(e => new { event_id=Get(e,"id"), fields=entry.Value.ToDictionary(k=>k,k=>R.Bool(Call("F_dv_has_key",Get(e,"data"),k))) }).FirstOrDefault() }).ToArray();
var result = new { scope="fresh production campaign; bounded formal-model engineering probe, not gameplay acceptance", max_steps=steps, elapsed_seconds=watch.Elapsed.TotalSeconds, executed=Str(Get(state,"executed")), chapter=Str(Get(Get(state,"story"),"chapter")), time=Str(Get(Get(state,"physical"),"time")), active=Call("SessionActive",state), tracked=JsonValue(tracked), absent, inversions, actors, evidence };
Write(Path.Combine(output,"probe-result.json"), result);
Console.WriteLine(JsonSerializer.Serialize(result));
if (absent.Length != 0 || inversions.Count != 0) Environment.ExitCode=1;
