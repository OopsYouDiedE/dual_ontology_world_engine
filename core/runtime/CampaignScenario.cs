using System.Text.Json;
using System.Text.Json.Nodes;
using DuelView.Dv;

namespace DuelView.Runtime;

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

    static void Append(JsonObject target, string key, JsonNode? source)
    {
        target[key] ??= new JsonArray();
        foreach (var item in List(source)) target[key]!.AsArray().Add(Copy(item));
    }

    public static JsonElement Expand(JsonElement manifest, Func<string, string> read)
    {
        if (manifest.Get("campaign").ValueKind != JsonValueKind.Array) return CompactScenario.Expand(manifest);
        JsonObject? combined = null;
        var previousSleep = new Dictionary<string, string>();
        var dayNumber = 0;
        foreach (var day in manifest.Get("campaign").Items())
        {
            ++dayNumber;
            var source = read(day.Get("source").Text());
            var raw = DvDocument.Parse(source).Definition("data", "runtime.character_session").GetProperty("value");
            if (raw.Get("campaign").ValueKind != JsonValueKind.Undefined) throw new InvalidDataException("Nested campaigns are not supported.");
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
                    // Later day's moodlets become available, but must not rewrite the opening day's scoring.
                    AddMissing(Map(existing["autonomy"]!["buff_definitions"]), Map(actor["autonomy"]!["buff_definitions"]));
                    AddMissing(Map(existing["mind"]!["adoptions"]), Map(actor["mind"]!["adoptions"]));
                }
                foreach (var key in new[] { "room_labels", "fact_labels" }) AddMissing(Map(combined[key]), Map(expanded[key]));
                foreach (var key in new[] { "expectations", "knowledge_boundaries" }) Append(combined, key, expanded[key]);
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
        return JsonSerializer.SerializeToElement(combined);
    }
}
