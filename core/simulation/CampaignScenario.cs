// @mechanism: causal_clock_and_campaign
// @layer: core/simulation
// @contracts: CampaignScenario

using DuelView.Autonomy;
using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using DuelView.Spatial;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;
using static DuelView.Core.DVSimulationKernel;

// --- Source: CampaignScenario.cs ---
namespace DuelView.Runtime
{


/// <summary>Joins authored days into one persistent session, without resetting facts or minds.</summary>
internal static class CampaignScenario
{
    static JsonObject Map(JsonNode? value) => value as JsonObject ?? new();
    static JsonArray List(JsonNode? value) => value as JsonArray ?? [];
    static string Text(JsonNode? value) => value?.GetValue<string>() ?? "";
    static JsonNode? Copy(JsonNode? value) => value?.DeepClone();

    static void AddMissing(JsonObject target, JsonObject source)
    {
        foreach (var pair in source)
        {
            if (!target.ContainsKey(pair.Key)) target[pair.Key] = Copy(pair.Value);
            else if (target[pair.Key] is JsonObject a && pair.Value is JsonObject b) AddMissing(a, b);
        }
    }

    static JsonNode? Rename(JsonNode? node, Dictionary<string, string> names)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return JsonValue.Create(names.GetValueOrDefault(text, text));
        if (node is JsonArray array) return new JsonArray(array.Select(item => Rename(item, names)).ToArray());
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var pair in obj) result[names.GetValueOrDefault(pair.Key, pair.Key)] = Rename(pair.Value, names);
            return result;
        }
        return Copy(node);
    }

    static void Append(JsonObject target, string key, JsonNode? source, bool unique = false)
    {
        target[key] ??= new JsonArray();
        foreach (var item in List(source))
            if (!unique || !target[key]!.AsArray().Any(prior => JsonNode.DeepEquals(prior, item)))
                target[key]!.AsArray().Add(Copy(item));
    }

    public static JsonElement Expand(JsonElement manifest, Func<string, string> read)
    {
        var shared = SharedScenario.Read(manifest, read);
        if (manifest.Get("campaign").ValueKind != JsonValueKind.Array) return CompactScenario.Expand(SharedScenario.Apply(manifest, shared));
        JsonObject? combined = null;
        var previousSleep = new Dictionary<string, string>();
        var dayNumber = 0;
        foreach (var day in manifest.Get("campaign").Items())
        {
            ++dayNumber;
            var source = read(day.Get("source").Text());
            var raw = DvDocument.Parse(source).Definition("data", "runtime.character_session").GetProperty("value");
            if (raw.Get("campaign").ValueKind != JsonValueKind.Undefined) throw new InvalidDataException("Nested campaigns are not supported.");
            raw = SharedScenario.Apply(raw, shared,dayNumber);
            var expanded = JsonNode.Parse(CompactScenario.Expand(raw).GetRawText())!.AsObject();
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            if (dayNumber > 1)
                foreach (var actor in List(expanded["actors"]))
                foreach (var action in List(actor!["actions"]))
                {
                    var id = Text(action!["id"]);
                    var renamed = Text(actor["id"]) + ".day" + dayNumber + id[Text(actor["id"]).Length..];
                    names[id] = renamed;
                    names["event:" + id] = "event:" + renamed;
                    names["event:" + id + ".done"] = "event:" + renamed + ".done";
                }
            foreach (var actor in List(expanded["actors"]))
            foreach (var sensor in List(actor!["perceptions"]))
            {
                var id = Text(sensor!["id"]);
                names[id] = "day" + dayNumber + ":" + id;
            }
            expanded = Rename(expanded, names)!.AsObject();
            var offset = day.Get("offset_minutes").Number();
            foreach (var actor in List(expanded["actors"]))
            {
                var id = Text(actor!["id"]);
                foreach (var action in List(actor["actions"]))
                {
                    var a = action!.AsObject();
                    var originalStart = a["not_before_game_minutes"]?.GetValue<double>() ?? raw.Get("start_minutes").Number();
                    if (Text(a["id"]).EndsWith(".wake", StringComparison.Ordinal))
                        originalStart = Math.Max(originalStart, actor["wake_at_game_minutes"]?.GetValue<double>() ?? originalStart);
                    a["not_before_game_minutes"] = originalStart + offset;
                    if (a["not_after_game_minutes"] is not null) a["not_after_game_minutes"] = a["not_after_game_minutes"]!.GetValue<double>() + offset;
                    if (a["rest_until_game_minutes"] is not null) a["rest_until_game_minutes"] = a["rest_until_game_minutes"]!.GetValue<double>() + offset;
                    a["campaign_day"] = dayNumber;
                    if (dayNumber > 1 && Text(a["id"]).EndsWith(".wake", StringComparison.Ordinal) && previousSleep.TryGetValue(id, out var sleep))
                    {
                        var gate = Copy(a["precondition"]);
                        a["precondition"] = new JsonObject { ["op"] = "all", ["items"] = new JsonArray(gate,
                            new JsonObject { ["op"] = "equals", ["ref"] = new JsonObject { ["type"] = "ref", ["object"] = "event:" + sleep, ["facet"] = "state", ["field"] = "done" }, ["value"] = true }) };
                    }
                }
                var sleepAction = List(actor["actions"]).LastOrDefault(a => Text(a!["id"]).EndsWith(".sleep_night", StringComparison.Ordinal));
                if (sleepAction is not null) previousSleep[id] = Text(sleepAction["id"]);
                var mind=Map(actor["mind"]);
                if(mind["adoptions"] is JsonObject inference)
                {
                    var renamedRules=new JsonObject();
                    foreach(var rule in inference)renamedRules["day"+dayNumber+":"+rule.Key]=Copy(rule.Value);
                    mind["adoptions"]=renamedRules;
                }
                foreach(var sensor in List(actor["perceptions"]))
                {
                    sensor!["not_before_game_minutes"]=(sensor["not_before_game_minutes"]?.GetValue<double>()??0)+offset;
                }
            }
            foreach(var item in List(expanded["world_events"]))
            {
                item!["id"]="day"+dayNumber+":"+Text(item["id"]);
                item["at_game_minutes"]=(item["at_game_minutes"]?.GetValue<double>()??0)+offset;
            }
            foreach (var expectation in List(expanded["expectations"]))
            {
                expectation!["id"] = "day" + dayNumber + "." + Text(expectation["id"]);
                expectation["from_minutes"] = offset;
                expectation["until_minutes"] = offset + 1440;
            }
            if (combined is null) combined = expanded;
            else
            {
                AddMissing(Map(combined["physical"]!["objects"]), Map(expanded["physical"]!["objects"]));
                AddMissing(Map(combined["physical"]!["adoptions"]), Map(expanded["physical"]!["adoptions"]));
                foreach (var actor in List(expanded["actors"]))
                {
                    var existing = List(combined["actors"]).Single(a => Text(a!["id"]) == Text(actor!["id"]))!.AsObject();
                    Append(existing, "actions", actor!["actions"]);
                    Append(existing, "perceptions", actor["perceptions"]);
                    if(actor["goal_planning"] is JsonObject laterGoals)
                    {
                        if(existing["goal_planning"] is not JsonObject goals)existing["goal_planning"]=Copy(laterGoals);
                        else
                        {
                            Append(goals,"goals",laterGoals["goals"],true);
                            AddMissing(goals,laterGoals);
                        }
                    }
                    // Later day's moodlets become available, but must not rewrite the opening day's scoring.
                    AddMissing(Map(existing["autonomy"]!["buff_definitions"]), Map(actor["autonomy"]!["buff_definitions"]));
                    AddMissing(Map(existing["mind"]!["adoptions"]), Map(actor["mind"]!["adoptions"]));
                    if(existing["opinion"] is null&&actor["opinion"] is not null)existing["opinion"]=Copy(actor["opinion"]);
                    else if(existing["opinion"] is JsonObject opinion&&actor["opinion"] is JsonObject laterOpinion)
                    {
                        opinion["modifiers"]??=new JsonArray();
                        foreach(var modifier in List(laterOpinion["modifiers"]))
                            if(!List(opinion["modifiers"]).Any(prior=>JsonNode.DeepEquals(prior,modifier)))opinion["modifiers"]!.AsArray().Add(Copy(modifier));
                    }
                }
                foreach (var key in new[] { "room_labels", "fact_labels" }) AddMissing(Map(combined[key]), Map(expanded[key]));
                foreach (var key in new[] { "expectations", "knowledge_boundaries", "world_events", "world_processes", "deductions", "strategy_activities", "objective_accumulators", "presence_groups" }) Append(combined, key, expanded[key], true);
                foreach (var key in new[] { "fixtures", "slots" })
                    foreach (var item in List(expanded[key]))
                        if (!List(combined[key]).Any(old => Text(old!["id"]) == Text(item!["id"]))) combined[key]!.AsArray().Add(Copy(item));
            }
            combined["run"] = new JsonObject { ["end_minutes"] = raw.Get("run").Get("end_minutes").Number() + offset };
        }
        if (combined is null) throw new InvalidDataException("Campaign must contain at least one day.");
        var bindings = new JsonArray();
        foreach (var actor in List(combined["actors"]))
        foreach (var obj in Map(combined["physical"]!["objects"]))
            bindings.Add(new JsonObject { ["subject"] = Copy(actor!["id"]), ["physical"] = obj.Key, ["local"] = obj.Key });
        combined["bindings"] = bindings;
        combined["campaign"] = JsonNode.Parse(manifest.Get("campaign").GetRawText());
        foreach (var property in manifest.EnumerateObject().Where(p => p.Name != "campaign")) combined[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        if(manifest.Get("planning").Get("source").Text() is {Length:>0} planningSource)
        {
            var planning=DvDocument.Parse(read(planningSource)).Definition("data",manifest.Get("planning").Get("definition").Text("runtime.character_planning")).GetProperty("value");
            var applied=new HashSet<string>(StringComparer.Ordinal);
            foreach(var actor in planning.Get("actors").Items())
            {
                var id=actor.Get("id").Text();
                var owner=List(combined["actors"]).SingleOrDefault(a=>Text(a!["id"])==id)?.AsObject();
                if(owner is null||!applied.Add(id)||actor.Get("planning").ValueKind!=JsonValueKind.Object)
                    throw new InvalidDataException("Planning source references a missing or duplicate actor.");
                if(owner["planning"] is not null)throw new InvalidDataException("Actor planning has more than one source: "+id);
                owner["planning"]=CompactScenario.ExpandPlanning(JsonNode.Parse(actor.Get("planning").GetRawText())!);
            }
        }
        var autonomyPhase=manifest.Get("autonomy_phase");
        if(autonomyPhase.ValueKind==JsonValueKind.Object&&autonomyPhase.Get("task_guidance").ValueKind==JsonValueKind.False)
            foreach(var actor in List(combined["actors"]))
                if(actor!["planning"] is JsonObject planning)
                    planning["task_guidance_until_game_minutes"]=autonomyPhase.Get("start_minutes").Number();
        return JsonSerializer.SerializeToElement(combined);
    }
}
}

// --- Source: CampaignProgress.cs ---
namespace DuelView.Runtime
{


public sealed record AutonomyPhaseView(
    [property:JsonPropertyName("active")] bool Active,
    [property:JsonPropertyName("start_minutes")] double? StartMinutes,
    [property:JsonPropertyName("label")] string Label,
    [property:JsonPropertyName("description")] string Description,
    [property:JsonPropertyName("task_guidance")] bool TaskGuidance);

public sealed record CampaignProgressView(
    [property:JsonPropertyName("mode")] string Mode,
    [property:JsonPropertyName("completed")] bool Completed,
    [property:JsonPropertyName("window_completed")] bool WindowCompleted,
    [property:JsonPropertyName("end_minutes")] double EndMinutes,
    [property:JsonPropertyName("label")] string Label,
    [property:JsonPropertyName("phase")] AutonomyPhaseView Phase);

/// <summary>Authored session boundaries. Reaching an observation boundary is
/// separate from any assertion about the quality or emergence of its outcome.</summary>
public static class CampaignProgress
{
    private static FieldKey CompletionFact(JsonElement reference)
    {
        // The campaign manifest predates typed law references. Preserve its
        // existing object/facet/field shape without weakening law access rules.
        var scope=reference.Get("subject").Text();var type=reference.Get("type").Text();
        var key=new FieldKey(reference.Get("object").Text(),reference.Get("facet").Text(),reference.Get("field").Text());
        if(scope.Length>0&&scope!="world"||type.Length>0&&type!="ref"||string.IsNullOrWhiteSpace(key.Object)||string.IsNullOrWhiteSpace(key.Facet)||string.IsNullOrWhiteSpace(key.Field))
            throw new InvalidDataException("Invalid campaign completion fact.");
        return key;
    }
    public static void Validate(JsonElement declaration)
    {
        var completion=declaration.Get("completion");
        if(completion.ValueKind==JsonValueKind.Object)
        {
            var mode=completion.Get("mode").Text("facts");
            if(mode=="facts")
            {
                var facts=completion.Get("facts").Items().ToArray();
                if(facts.Length==0)throw new InvalidDataException("Fact completion requires a nonempty set of facts.");
                foreach(var item in facts)_=CompletionFact(item);
            }
            else if(mode=="observation_window")
            {
                var end=completion.Get("end_minutes").Number(double.NaN);
                var horizon=declaration.Get("run").Get("end_minutes").Number(double.NaN);
                if(!double.IsFinite(end)||end<=declaration.Get("physical").Get("time").Number()||!double.IsFinite(horizon)||horizon<end)
                    throw new InvalidDataException("Observation completion requires a reachable finite time boundary.");
                if(completion.Get("facts").Items().Any())throw new InvalidDataException("Observation completion cannot also require story outcome facts.");
            }
            else throw new InvalidDataException("Unknown completion mode: "+mode);
        }
        var phase=declaration.Get("autonomy_phase");
        if(phase.ValueKind==JsonValueKind.Object)
        {
            var start=phase.Get("start_minutes").Number(double.NaN);
            if(!double.IsFinite(start)||start<0||start>=declaration.Get("run").Get("end_minutes").Number(double.NaN)||
               phase.Get("task_guidance").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException("Autonomy phase requires a finite start and an explicit task guidance policy.");
        }
    }

    public static CampaignProgressView Evaluate(JsonElement declaration,RuntimeSession session)
    {
        var completion=declaration.Get("completion");
        var mode=completion.ValueKind==JsonValueKind.Object?completion.Get("mode").Text("facts"):"none";
        var end=mode=="observation_window"?completion.Get("end_minutes").Number():declaration.Get("run").Get("end_minutes").Number();
        var window=mode=="observation_window"&&session.ContentTime>=end;
        var facts=completion.Get("facts").Items().ToArray();
        var completed=window||mode=="facts"&&facts.Length>0&&facts.All(reference=>
            session.World.Read(CompletionFact(reference)) is {Known:true,Conflicted:false} fact&&fact.Value.ValueKind==JsonValueKind.True);
        var phase=declaration.Get("autonomy_phase");
        var active=phase.ValueKind==JsonValueKind.Object&&session.ContentTime>=phase.Get("start_minutes").Number();
        return new(mode,completed,window,end,completion.Get("label").Text(),new(active,
            phase.ValueKind==JsonValueKind.Object?phase.Get("start_minutes").Number():null,
            phase.Get("label").Text(),phase.Get("description").Text(),!active||phase.Get("task_guidance").ValueKind!=JsonValueKind.False));
    }
}
}

// --- Source: SharedScenario.cs ---
namespace DuelView.Runtime
{


internal sealed record SharedFragment(JsonObject Declaration,int[] Days);

/// <summary>Composes reusable authored declarations before compact expansion.
/// Local scenario values win; content still owns all facts, motives and actions.</summary>
internal static class SharedScenario
{
    private static readonly string[] Maps = ["actor_defaults", "facts", "fact_labels", "room_labels", "templates"];
    private static readonly string[] Collections = ["fixtures", "slots", "presence_groups", "objective_accumulators", "world_processes", "deductions", "strategy_activities"];
    private static JsonNode? Copy(JsonNode? value) => value?.DeepClone();
    private static JsonObject Map(JsonNode? value) => value as JsonObject ?? new();
    private static JsonArray List(JsonNode? value) => value as JsonArray ?? [];
    private static string Text(JsonNode? value) => value?.GetValue<string>() ?? "";

    internal static SharedFragment[] Read(JsonElement manifest, Func<string, string> read)
    {
        var references = manifest.Get("shared_sources");
        if (references.ValueKind == JsonValueKind.Undefined) return [];
        if (references.ValueKind != JsonValueKind.Array || references.GetArrayLength() > 32)
            throw new InvalidDataException("Shared sources must be a bounded list of DV declarations.");
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<SharedFragment>();
        foreach (var reference in references.EnumerateArray())
        {
            var path = reference.Get("source").Text();
            if (path.Length == 0 || !paths.Add(path)) throw new InvalidDataException("Missing or duplicate shared DV source: " + path);
            var days=reference.Get("days");
            if(days.ValueKind!=JsonValueKind.Undefined&&(days.ValueKind!=JsonValueKind.Array||days.GetArrayLength() is <1 or >32||
                days.EnumerateArray().Any(day=>day.ValueKind!=JsonValueKind.Number||!day.TryGetInt32(out var number)||number is <1 or >32)))
                throw new InvalidDataException("Shared source days must be distinct positive campaign day numbers: "+path);
            var selectedDays=days.Items().Select(day=>day.GetInt32()).ToArray();
            if(selectedDays.Distinct().Count()!=selectedDays.Length)throw new InvalidDataException("Duplicate shared source day: "+path);
            var data = DvDocument.Parse(read(path)).Definition("data", "runtime.character_session").GetProperty("value");
            if (!data.Get("compact").Flag() || data.Get("campaign").ValueKind != JsonValueKind.Undefined || data.Get("shared_sources").ValueKind != JsonValueKind.Undefined)
                throw new InvalidDataException("Shared source must be a nonrecursive compact declaration: " + path);
            foreach (var field in data.EnumerateObject())
                if (field.Name != "compact" && field.Name != "actors" && !Maps.Contains(field.Name) && !Collections.Contains(field.Name))
                    throw new InvalidDataException("Unsupported shared declaration field: " + field.Name);
            result.Add(new(JsonNode.Parse(data.GetRawText())!.AsObject(),selectedDays));
        }
        return result.ToArray();
    }

    private static void Missing(JsonObject target, JsonObject source)
    {
        foreach (var field in source)
        {
            if (!target.ContainsKey(field.Key)) target[field.Key] = Copy(field.Value);
            else if (target[field.Key] is JsonObject existing && field.Value is JsonObject fallback) Missing(existing, fallback);
        }
    }

    private static void Append(JsonObject target, string field, JsonNode? source, bool deduplicate = false)
    {
        if (source is null) return;
        if (source is not JsonArray values) throw new InvalidDataException("Shared declaration requires an array: " + field);
        target[field] ??= new JsonArray();
        var destination = target[field] as JsonArray ?? throw new InvalidDataException("Scenario declaration requires an array: " + field);
        foreach (var value in values)
            if (!deduplicate || !destination.Any(prior => JsonNode.DeepEquals(prior, value))) destination.Add(Copy(value));
    }

    private static void AppendNamed(JsonObject target, string field, JsonNode? source)
    {
        if (source is null) return;
        if (source is not JsonArray values) throw new InvalidDataException("Shared declaration requires an array: " + field);
        target[field] ??= new JsonArray();
        var destination = target[field] as JsonArray ?? throw new InvalidDataException("Scenario declaration requires an array: " + field);
        foreach (var value in values)
        {
            var id = Text(value?["id"] ?? value?["law"] ?? value?["action"]);
            var prior = id.Length == 0 ? null : destination.FirstOrDefault(item => Text(item?["id"] ?? item?["law"] ?? item?["action"]) == id);
            if (prior is null) destination.Add(Copy(value));
            else if (prior is JsonObject local && value is JsonObject fallback) Missing(local, fallback);
        }
    }

    internal static JsonElement Apply(JsonElement scenario, IReadOnlyList<SharedFragment> shared,int day=1)
    {
        if (shared.Count == 0) return scenario;
        if (!scenario.Get("compact").Flag()) throw new InvalidDataException("Shared sources require compact scenario declarations.");
        var result = JsonNode.Parse(scenario.GetRawText())!.AsObject();
        foreach (var source in shared.Where(fragment=>fragment.Days.Length==0||fragment.Days.Contains(day)))
        {
            var fragment=source.Declaration;
            foreach (var field in Maps)
            {
                if (fragment[field] is null) continue;
                if (fragment[field] is not JsonObject fallback) throw new InvalidDataException("Shared declaration requires an object: " + field);
                result[field] ??= new JsonObject();
                Missing(result[field]!.AsObject(), fallback);
            }
            foreach (var field in Collections) AppendNamed(result, field, fragment[field]);
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var actor in List(fragment["actors"]))
            {
                var id = Text(actor?["id"]);
                if (id.Length == 0 || !used.Add(id)) throw new InvalidDataException("Missing or duplicate shared actor identity.");
                var local = List(result["actors"]).SingleOrDefault(item => Text(item?["id"]) == id) as JsonObject
                    ?? throw new InvalidDataException("Shared declarations reference an actor absent from the scenario: " + id);
                var fallback = actor!.AsObject();
                foreach (var field in new[] { "actions", "perceptions", "rules" }) Append(local, field, fallback[field]);
                var modifiers = fallback["opinion"]?["modifiers"];
                if (modifiers is not null)
                {
                    local["opinion"] ??= new JsonObject();
                    Append(local["opinion"]!.AsObject(), "modifiers", modifiers, true);
                }
                if(fallback["goal_planning"] is JsonObject planning)
                {
                    local["goal_planning"]??=new JsonObject();
                    AppendNamed(local["goal_planning"]!.AsObject(),"goals",planning["goals"]);
                }
                var remaining = fallback.DeepClone().AsObject();
                foreach (var field in new[] { "actions", "perceptions", "rules" }) remaining.Remove(field);
                Missing(local, remaining);
            }
        }
        return JsonSerializer.SerializeToElement(result);
    }
}
}

// --- Source: CompactScenario.cs ---
namespace DuelView.Runtime
{


/// <summary>Generic DV authoring shorthand. Story, evidence, routes and timings remain in DV.</summary>
internal static class CompactScenario
{
    static JsonNode? Copy(JsonNode? value)=>value?.DeepClone();
    static JsonObject Ref(string path)
    {
        var i=path.LastIndexOf('.');if(i<1)throw new InvalidDataException("Expected object[.facet].field: "+path);
        var prefix=path[..i];var j=prefix.LastIndexOf('.');
        var explicitFacet=j>0&&prefix[(j+1)..] is "state" or "phys" or "trace" or "lock" or "doc" or "infer";
        return new(){["type"]="ref",["object"]=explicitFacet?prefix[..j]:prefix,["facet"]=explicitFacet?prefix[(j+1)..]:"state",["field"]=path[(i+1)..]};
    }
    static JsonObject Eq(string path,JsonNode? value)=>new(){["op"]="equals",["ref"]=Ref(path),["value"]=Copy(value)};
    static JsonObject Known(string path)=>new(){["op"]="known",["ref"]=Ref(path)};
    static JsonObject Not(JsonNode value)=>new(){["op"]="not",["item"]=value};
    static JsonObject All(JsonArray items)=>new(){["op"]="all",["items"]=items};
    static JsonArray List(JsonNode? value)=>value as JsonArray??[];
    static JsonObject Map(JsonNode? value)=>value as JsonObject??new();
    static JsonObject Merge(JsonObject defaults,JsonObject overrides)
    {
        var result=(JsonObject)defaults.DeepClone();
        foreach(var p in overrides)result[p.Key]=result[p.Key] is JsonObject old&&p.Value is JsonObject next?Merge(old,next):Copy(p.Value);
        return result;
    }
    static JsonArray References(JsonNode? value)=>new(List(value).Select(v=>(JsonNode)Ref(v!.GetValue<string>())).ToArray());
    static JsonArray Conditions(JsonNode? value)=>value is JsonObject condition&&condition.ContainsKey("op")
        ?new JsonArray(condition.DeepClone()):new(Map(value).Select(p=>(JsonNode)Eq(p.Key,p.Value)).ToArray());
    static JsonArray Effects(JsonNode? value)=>new(Map(value).Select(p=>(JsonNode)new JsonObject{["op"]="assign",["ref"]=Ref(p.Key),["value"]=Copy(p.Value)}).ToArray());
    internal static JsonNode ExpandPlanning(JsonNode value)
    {
        var result=value.DeepClone().AsObject();
        JsonNode ConditionNode(JsonNode? condition)=>condition is JsonObject obj&&obj.ContainsKey("op")?obj.DeepClone():All(Conditions(condition));
        foreach(var task in List(result["tasks"]))
        {
            if(task is not JsonObject t)throw new InvalidDataException("Task declaration must be an object.");
            if(t["available_when"] is null||t["goal"] is null)throw new InvalidDataException("Task requires knowledge admission and completion.");
            t["available_when"]=ConditionNode(t["available_when"]);t["goal"]=ConditionNode(t["goal"]);
            foreach(var step in List(t["steps"]))
            {
                if(step is not JsonObject s||s["done_when"] is null)throw new InvalidDataException("Plan steps require an observed completion condition.");
                s["done_when"]=ConditionNode(s["done_when"]);
            }
        }
        foreach(var route in List(result["physiology"]?["routes"]))
        {
            if(route is not JsonObject r||r["available_when"] is null)throw new InvalidDataException("A physiological route requires a knowledge admission condition.");
            r["available_when"]=ConditionNode(r["available_when"]);
        }
        return result;
    }
    internal static JsonNode ExpandGoalPlanning(JsonNode value)
    {
        var result=value.DeepClone().AsObject();
        if(result["when"] is not null)result["when"]=All(Conditions(result["when"]));
        foreach(var goal in List(result["goals"]))
        {
            if(goal is not JsonObject g||g["target"] is null)throw new InvalidDataException("A personal goal needs an explicit desired condition.");
            g["target"]=All(Conditions(g["target"]));
            g["when"]=All(Conditions(g["when"]));
        }
        return result;
    }
    static void Put(JsonObject objects,string path,JsonNode? value)
    {
        var r=Ref(path);var id=r["object"]!.GetValue<string>();var field=r["field"]!.GetValue<string>();
        objects[id]??=new JsonObject{["facets"]=new JsonObject{["state"]=new JsonObject()}};
        var facet=r["facet"]!.GetValue<string>();
        objects[id]!["facets"]![facet]??=new JsonObject();
        objects[id]!["facets"]![facet]![field]=new JsonObject{["value"]=Copy(value),["known"]=true,["confidence"]=1};
    }
    static JsonNode? Substitute(JsonNode? node,JsonObject arguments)
    {
        if(node is JsonValue v && v.TryGetValue<string>(out var text))
        {foreach(var p in arguments)text=text.Replace("${"+p.Key+"}",p.Value is JsonValue x&&x.TryGetValue<string>(out var s)?s:p.Value?.ToJsonString()??"");return JsonValue.Create(text);}
        if(node is JsonObject o){var result=new JsonObject();foreach(var p in o)result[Substitute(JsonValue.Create(p.Key),arguments)!.GetValue<string>()]=Substitute(p.Value,arguments);return result;}
        if(node is JsonArray a)return new JsonArray(a.Select(n=>Substitute(n,arguments)).ToArray());
        return Copy(node);
    }
    public static JsonElement Expand(JsonElement raw)
    {
        if(!raw.Get("compact").Flag())return raw.Clone();
        var root=JsonNode.Parse(raw.GetRawText())!.AsObject();var objects=new JsonObject();var laws=new JsonObject();var slots=new HashSet<string>();
        var capacities=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var slot in List(root["slots"]))
        {
            var slotId=slot?["id"]?.GetValue<string>()??"";
            if(string.IsNullOrWhiteSpace(slotId)||slot?["capacity"] is not JsonValue capacity||!capacity.TryGetValue<int>(out var count)||count<1||!capacities.TryAdd(slotId,count))
                throw new InvalidDataException("Invalid or duplicate compact reservation slot: "+slotId);
        }
        var actionIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in Map(root["facts"]))Put(objects,p.Key,p.Value);
        var templates=Map(root["templates"]);var defaults=Map(root["actor_defaults"]);
        foreach(var entry in List(root["actors"]))
        {
            var actor=entry!.AsObject();var id=actor["id"]!.GetValue<string>();
            if(actor["autonomy"] is JsonObject overrides)actor["autonomy"]=Merge(Map(defaults["autonomy"]),overrides);
            foreach(var p in defaults)if(!actor.ContainsKey(p.Key))actor[p.Key]=Copy(p.Value);
            var mindObjects=new JsonObject();foreach(var p in Map(actor["knows"]))Put(mindObjects,p.Key,p.Value);
            foreach(var sensor in List(actor["perceptions"]))
            {
                sensor!["condition"]=All(Conditions(sensor["when"]));
                sensor["knowledge_condition"]=All(Conditions(sensor["knows"]));
                sensor["refs"]=References(sensor["observes"]);
            }
            var inferences=new JsonObject();var ruleIndex=0;
            foreach(var rule in List(actor["rules"]))inferences["rule."+(++ruleIndex)]=new JsonObject{["mode"]="infer",["basis"]=Copy(rule!["basis"]),["law"]=new JsonObject{["premise"]=All(Conditions(rule["when"])),["effects"]=Effects(rule["then"])}};
            var actions=new JsonArray();
            foreach(var spec in List(actor["actions"]))
            {
                JsonObject a;
                if(spec!["use"] is { } use)
                {
                    var args=Map(Copy(actor["args"]));args["actor"]=id;
                    foreach(var p in Map(spec["args"]))args[p.Key]=Copy(p.Value);
                    a=Substitute(templates[use.GetValue<string>()]??throw new InvalidDataException("Unknown action template."),args)!.AsObject();
                    foreach(var p in spec.AsObject().Where(p=>p.Key is not ("use" or "args")))a[p.Key]=Substitute(p.Value,args);
                }
                else a=(JsonObject)Copy(spec)!;
                var actionId=a["id"]!.GetValue<string>();
                if(string.IsNullOrWhiteSpace(actionId)||!actionIds.Add(actionId))throw new InvalidDataException("Missing or duplicate expanded action: "+actionId);
                var gate=Conditions(a["when"]);var objective=Conditions(a["requires"]);
                var recognition=a["recognize_when"] is null?null:Conditions(a["recognize_when"]);
                foreach(var reason in Conditions(a["because"]))gate.Add(Copy(reason));
                if(actor["wake_at_game_minutes"] is not null&&actionId!=id+".wake")
                {gate.Add(Eq("event:"+id+".wake.done",JsonValue.Create(true)));recognition?.Add(Eq("event:"+id+".wake.done",JsonValue.Create(true)));}
                if(a["at"] is { } room){gate.Add(Eq(id+".room",room));objective.Add(Eq(id+".room",room));}
                foreach(var unknown in List(a["unknown"]))
                {gate.Add(Not(Known(unknown!.GetValue<string>())));recognition?.Add(Not(Known(unknown.GetValue<string>())));}
                var effects=Effects(a["writes"]);var grants=References(a["observes"]);
                foreach(var change in Map(a["increments"]))
                {
                    if(change.Value is not JsonValue amount||!amount.TryGetValue<double>(out var number)||!double.IsFinite(number))
                        throw new InvalidDataException("A resource increment must be a finite number: "+actionId);
                    if(Map(a["writes"]).ContainsKey(change.Key))throw new InvalidDataException("An action cannot assign and increment the same field: "+actionId);
                    effects.Add(new JsonObject{["op"]="increment",["ref"]=Ref(change.Key),["value"]=Copy(change.Value)});
                }
                foreach(var write in Map(a["writes"]).Concat(Map(a["increments"])))
                {
                    var target=Ref(write.Key)["object"]!.GetValue<string>();
                    objects[target]??=new JsonObject{["facets"]=new JsonObject{["state"]=new JsonObject()}};
                }
                if(a["once"]?.GetValue<bool>()==true)
                {
                    var key="event:"+actionId+".done";Put(objects,key,JsonValue.Create(false));
                    gate.Add(Not(Known(key)));recognition?.Add(Not(Known(key)));objective.Add(Eq(key,JsonValue.Create(false)));
                    effects.Add(new JsonObject{["op"]="assign",["ref"]=Ref(key),["value"]=true});grants.Add(Ref(key));
                }
                laws[actionId]=new JsonObject{["mode"]="enforce",["law"]=new JsonObject{["premise"]=All(objective),["effects"]=effects}};
                a["precondition"]=All(gate);a["law"]=actionId;a["grants"]=grants;
                if(recognition is not null)a["recognize_when"]=All(recognition);
                if(a["planning_model"] is JsonObject model)
                {
                    model["when"]=All(Conditions(model["when"]));
                    var predictions=model["effects"] is JsonArray explicitEffects?explicitEffects.DeepClone().AsArray():Effects(model["effects"]);
                    foreach(var prediction in Map(model["increments"]))
                        predictions.Add(new JsonObject{["op"]="increment",["ref"]=Ref(prediction.Key),["value"]=Copy(prediction.Value)});
                    if(a["once"]?.GetValue<bool>()==true&&!predictions.Any(prediction=>JsonNode.DeepEquals(prediction?["ref"],Ref("event:"+actionId+".done"))))
                        predictions.Add(new JsonObject{["op"]="assign",["ref"]=Ref("event:"+actionId+".done"),["value"]=true});
                    model["effects"]=predictions;model.Remove("increments");
                }
                a["duration_seconds"]=(a["minutes"]?.GetValue<double>()??1)*60;
                a["resource_reservations"]=Copy(a["reserve"])??new JsonArray();
                var reservations=a["resource_reservations"]!.AsArray();reservations.Add("actor."+id);
                if(root["atomic_actions"]?.GetValue<bool>()==true&&a["focus"] is not JsonValue)
                    throw new InvalidDataException("Atomic action requires one primary focus: "+actionId);
                if(a["interaction_slot"] is JsonValue interactionSlot)reservations.Add("interaction."+interactionSlot.GetValue<string>());
                foreach(var r in reservations)slots.Add(r!.GetValue<string>());
                var tells=new JsonArray();
                foreach(var tell in List(a["tell"]))
                {
                    var h=new JsonObject{["actor"]=Copy(tell!["actor"]),["refs"]=References(tell["fields"]),["result_refs"]=References(tell["results"])};
                    var audience=Conditions(tell["when"]);
                    if(tell["room"] is { } location)audience.Add(Eq(tell["actor"]!.GetValue<string>()+".room",location));
                    if(tell["rooms"] is JsonArray locations)audience.Add(Not(All(new JsonArray(locations.Select(room=>(JsonNode)Not(Eq(tell["actor"]!.GetValue<string>()+".room",room))).ToArray()))));
                    if(audience.Count>0)h["only_if"]=All(audience);
                    tells.Add(h);
                }
                a["handoffs"]=tells;
                var availability=Conditions(a["requires"]);
                foreach(var condition in Conditions(a["authority"]))availability.Add(Copy(condition));
                if(availability.Count>0)a["authority_precondition"]=All(availability);
                foreach(var key in new[]{"when","requires","writes","increments","observes","minutes","at","unknown","once","reserve","tell","authority"})a.Remove(key);
                actions.Add(a);
            }
            actor["actions"]=actions;actor["mind"]=new JsonObject{["subject"]=id,["objects"]=mindObjects,["adoptions"]=inferences};
            if(actor["planning"] is JsonObject planning)actor["planning"]=ExpandPlanning(planning);
            if(actor["goal_planning"] is JsonObject goalPlanning)actor["goal_planning"]=ExpandGoalPlanning(goalPlanning);
        }
        root["physical"]=new JsonObject{["subject"]="world",["time"]=Copy(root["start_minutes"]),["objects"]=objects,["adoptions"]=laws};
        root["slots"]=new JsonArray(slots.Concat(capacities.Keys).Distinct(StringComparer.Ordinal).Select(s=>(JsonNode)new JsonObject{["id"]=s,["capacity"]=capacities.GetValueOrDefault(s,1)}).ToArray());
        var bindings=new JsonArray();foreach(var actor in List(root["actors"]))foreach(var p in objects)bindings.Add(new JsonObject{["subject"]=Copy(actor!["id"]),["physical"]=p.Key,["local"]=p.Key});root["bindings"]=bindings;
        foreach(var key in new[]{"templates","actor_defaults","facts","compact"})root.Remove(key);
        return JsonSerializer.SerializeToElement(root);
    }
}
}
