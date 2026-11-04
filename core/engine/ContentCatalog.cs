using DuelView.Dv;
using System.Text;
using System.Text.Json;
using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Data-only authored definitions. No game-specific identities or rule bodies live here.</summary>
public sealed class ContentCatalog
{
    public const string Schema = DvDocument.Schema;
    public string Source { get; }
    public string Hash { get; }
    public string? Path { get; }
    public object Data { get; }
    public IReadOnlyDictionary<string, JsonElement> Items { get; }
    public IReadOnlyDictionary<string, JsonElement> Rules { get; }
    public IReadOnlyDictionary<string, JsonElement> Characters { get; }
    public int LayoutCount { get; }
    public int NarrativeCount { get; }
    public int ProfileCount { get; }
    public int DataCount { get; }
    public string InitializationDefinitions { get; }

    private ContentCatalog(string source, string? path, DvDocument? parsed = null)
    {
        var document = parsed ?? DvDocument.Parse(source);
        var root = document.Root;
        Source = document.Source;
        Path = path;
        Hash = document.Hash;
        var items = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var rules = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var itemValues = new Dictionary<string, object>(StringComparer.Ordinal);
        var ruleValues = new Dictionary<string, object>(StringComparer.Ordinal);
        var groups = new Dictionary<string, List<object>>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("items").EnumerateObject())
        {
            var item = property.Value;
            RequireId(property.Name);
            var name = RequiredText(item, "name");
            var group = RequiredText(item, "group");
            var kind = RequiredText(item, "kind");
            var mapKind = RequiredText(item, "map_kind");
            var layout = root.GetProperty("layouts").TryGetProperty(group, out var placement) ? placement : default;
            var facets = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var facet in item.GetProperty("facets").EnumerateObject())
            {
                var fields = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var field in facet.Value.EnumerateObject()) fields.Add(field.Name, DVKernel.F_dv_field(Value(field.Value)));
                if (facet.Name == "state")
                {
                    if (fields.ContainsKey("name")) throw new InvalidDataException("Item name has one source: the top-level name property");
                    fields.Add("name", DVKernel.F_dv_field(name));
                    if (layout.ValueKind == JsonValueKind.Object && layout.GetProperty("items").TryGetProperty(property.Name, out var location))
                    {
                        if (fields.ContainsKey("location")) throw new InvalidDataException("Initial location must have one source: item state or layout");
                        fields.Add("location", DVKernel.F_dv_field(location.GetString() ?? throw new InvalidDataException("Invalid layout location")));
                    }
                }
                facets.Add(facet.Name, Record(fields));
            }
            if (!facets.ContainsKey("state")) throw new InvalidDataException("Item needs state facets");
            var visible = item.GetProperty("visible_fields").EnumerateArray().Select(v => (object)(v.GetString() ?? throw new InvalidDataException("Invalid visible field"))).ToList();
            var value = Record(new() { ["id"] = property.Name, ["kind"] = kind, ["facets"] = Record(facets),
                ["map_kind"] = mapKind, ["map_title"] = name, ["visible_fields"] = visible });
            items.Add(property.Name, item.Clone());
            itemValues.Add(property.Name, value);
            if (!groups.TryGetValue(group, out var list)) groups.Add(group, list = []);
            list.Add(value);
        }
        foreach (var property in root.GetProperty("rules").EnumerateObject())
        {
            RequireId(property.Name);
            var rule = property.Value;
            if (!rule.GetProperty("version").TryGetInt32(out var version) || version < 1)
                throw new InvalidDataException("Rule version must be a positive integer");
            Condition(rule.GetProperty("premise"));
            var effects = rule.GetProperty("effects");
            foreach (var effect in effects.EnumerateArray())
            {
                var op = RequiredText(effect, "op");
                if (op is not ("assign" or "increment")) throw new InvalidDataException("Unsupported effect: " + op);
                Reference(effect.GetProperty("ref"));
                if (op == "increment" && effect.GetProperty("value").ValueKind != JsonValueKind.Number)
                    throw new InvalidDataException("Increment requires a number");
                _ = Value(effect.GetProperty("value"));
            }
            ruleValues.Add(property.Name, Record(new() { ["name"] = property.Name, ["version"] = (double)version,
                ["premise"] = Value(rule.GetProperty("premise")), ["effects"] = Value(effects) }));
            rules.Add(property.Name, rule.Clone());
        }
        Items = items;
        Rules = rules;
        var characters = new Dictionary<string, object>(StringComparer.Ordinal);
        var characterSources = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("characters").EnumerateObject())
        {
            var definition = property.Value;
            RequireId(property.Name);
            if (definition.TryGetProperty("name", out _)) _ = RequiredText(definition, "name");
            if (definition.TryGetProperty("autonomy", out var autonomy)) ValidateAutonomy(autonomy);
            var profile = Value(definition.GetProperty("personality"));
            if (definition.GetProperty("personality").ValueKind != JsonValueKind.Object) throw new InvalidDataException("Personality must be a record");
            var personality = definition.GetProperty("personality");
            foreach (var key in new[] { "group", "domain" }) _ = RequiredText(personality, key);
            foreach (var key in new[] { "station_roles", "value_order" })
                foreach (var value in personality.GetProperty(key).EnumerateArray())
                    if (value.ValueKind != JsonValueKind.String) throw new InvalidDataException("Personality lists must contain text");
            var obeys = definition.GetProperty("obeys");
            var normValues = new List<object>();
            var normIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var norm in obeys.EnumerateArray())
            {
                foreach (var key in new[] { "id", "method", "mode" }) _ = RequiredText(norm, key);
                if (!normIds.Add(RequiredText(norm, "id"))) throw new InvalidDataException("Duplicate authored norm identity");
                if (RequiredText(norm, "mode") is not ("forbid" or "prefer")) throw new InvalidDataException("Norm mode must be forbid or prefer");
                Condition(norm.GetProperty("available_when"));
                if (norm.GetProperty("basis").GetArrayLength() == 0) throw new InvalidDataException("Authored norm needs a basis");
                normValues.Add(R.record_with(Value(norm), "content_origin", Hash));
            }
            characters.Add(property.Name, Record(new() { ["personality"] = R.record_with(profile, "id", property.Name), ["obeys"] = normValues,
                ["name"] = definition.TryGetProperty("name", out var displayName) ? Value(displayName) : property.Name,
                ["autonomy"] = definition.TryGetProperty("autonomy", out var autonomyValue) ? Value(autonomyValue) : false }));
            characterSources.Add(property.Name, definition.Clone());
        }
        Characters = characterSources;
        LayoutCount = root.GetProperty("layouts").EnumerateObject().Count();
        foreach (var layout in root.GetProperty("layouts").EnumerateObject())
            foreach (var placement in layout.Value.GetProperty("items").EnumerateObject())
                if (!items.ContainsKey(placement.Name) || placement.Value.ValueKind != JsonValueKind.String || items[placement.Name].GetProperty("group").GetString() != layout.Name)
                    throw new InvalidDataException("Layout must reference a declared item and a text location");
        InitializationDefinitions = root.GetProperty("narratives").GetRawText() + root.GetProperty("profiles").GetRawText()
            + root.GetProperty("data").GetRawText();
        DataCount = root.GetProperty("data").EnumerateObject().Count();
        foreach (var entry in root.GetProperty("data").EnumerateObject())
        {
            RequireId(entry.Name);
            if (!entry.Value.TryGetProperty("value", out var literal))
                throw new InvalidDataException("Data declaration needs a value: " + entry.Name);
            _ = Value(literal);
        }
        NarrativeCount = root.GetProperty("narratives").EnumerateObject().Count();
        foreach (var narrative in root.GetProperty("narratives").EnumerateObject())
        {
            RequireId(narrative.Name);
            _ = RequiredText(narrative.Value, "text");
        }
        ProfileCount = root.GetProperty("profiles").EnumerateObject().Count();
        foreach (var profile in root.GetProperty("profiles").EnumerateObject())
        {
            RequireId(profile.Name);
            _ = RequiredText(profile.Value, "cognitive_profile");
            foreach (var key in new[] { "empirical_initiative", "counterfactual_initiative", "plan_depth" })
                NonnegativeNumber(profile.Value.GetProperty(key));
            foreach (var key in new[] { "value_sensitive_progress", "coordinate_owned_tasks", "scope_reference_check" })
                if (profile.Value.GetProperty(key).ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new InvalidDataException("Cognitive switches must be booleans: " + key);
        }
        Data = R.snapshot(Record(new() { ["items"] = Record(itemValues), ["rules"] = Record(ruleValues),
            ["data"] = Value(root.GetProperty("data")),
            ["narratives"] = Value(root.GetProperty("narratives")), ["profiles"] = Value(root.GetProperty("profiles")),
            ["characters"] = Record(characters), ["layouts"] = Value(root.GetProperty("layouts")),
            ["groups"] = Record(groups.ToDictionary(p => p.Key, p => (object)p.Value, StringComparer.Ordinal)),
            ["meta"] = Record(new() { ["hash"] = Hash, ["schema"] = Schema }) }));
    }

    public static ContentCatalog Parse(string source) => new(source, null);
    public static ContentCatalog Load(string path) => new(File.ReadAllText(System.IO.Path.GetFullPath(path), Encoding.UTF8), System.IO.Path.GetFullPath(path));
    public static ContentCatalog ParseBundle(IEnumerable<string> sources, string? path = null)
    {
        return new ContentCatalog("", path, DvDocument.ParseBundle(sources));
    }
    public object Get(string category, string id) => R.record_find(R.record_find(Data, category, new List<object>()), id, false);

    private static void NonnegativeNumber(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number) || number < 0)
            throw new InvalidDataException("Expected a finite nonnegative number");
    }

    private static void ValidateAutonomy(JsonElement autonomy)
    {
        if (autonomy.ValueKind == JsonValueKind.False) return;
        foreach (var key in new[] { "commodity_needs", "autonomy_weights" })
            foreach (var entry in autonomy.GetProperty(key).EnumerateObject()) NonnegativeNumber(entry.Value);
        var needs = autonomy.GetProperty("commodity_needs");
        foreach (var entry in needs.EnumerateObject())
            if (entry.Value.GetDouble() > 100) throw new InvalidDataException("Need level exceeds 100: " + entry.Name);
        foreach (var key in new[] { "score_scale", "time_cost" })
            if (autonomy.TryGetProperty(key, out var number)) NonnegativeNumber(number);
        void ValidateEffects(JsonElement effects, bool decay)
        {
            foreach (var entry in effects.EnumerateObject())
            {
                if (!needs.TryGetProperty(entry.Name, out _) || !autonomy.GetProperty("autonomy_weights").TryGetProperty(entry.Name, out _))
                    throw new InvalidDataException("Undeclared autonomy need: " + entry.Name);
                if (!entry.Value.TryGetDouble(out var value) || !double.IsFinite(value) || (decay && value < 0))
                    throw new InvalidDataException("Invalid autonomy effect: " + entry.Name);
            }
        }
        if (autonomy.TryGetProperty("need_decay_per_second", out var decay)) ValidateEffects(decay, true);
        foreach (var key in new[] { "action_predictions", "completed_action_effects" })
            if (autonomy.TryGetProperty(key, out var actions))
                foreach (var action in actions.EnumerateObject()) ValidateEffects(action.Value, false);
        foreach (var entry in autonomy.GetProperty("action_weights").EnumerateObject())
        {
            var need = RequiredText(entry.Value, "need");
            if (!autonomy.GetProperty("autonomy_weights").TryGetProperty(need, out _))
                throw new InvalidDataException("Action weight references an undeclared need: " + need);
            NonnegativeNumber(entry.Value.GetProperty("scale"));
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var habit in autonomy.GetProperty("implicit_habits").EnumerateArray())
        {
            foreach (var key in new[] { "id", "trigger", "kind", "surface" }) _ = RequiredText(habit, key);
            if (!ids.Add(RequiredText(habit, "id"))) throw new InvalidDataException("Duplicate habit identity");
            NonnegativeNumber(habit.GetProperty("bonus"));
        }
    }

    private static object Record(Dictionary<string, object> values) => values.Select(p => (object)new List<object> { p.Key, p.Value }).ToList();
    private static object Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object when value.TryGetProperty("$dv_rational", out var exact) => Rational.From(exact.GetString()!),
        JsonValueKind.Object => Record(value.EnumerateObject().ToDictionary(p => p.Name, p => Value(p.Value), StringComparer.Ordinal)),
        JsonValueKind.Array => value.EnumerateArray().Select(Value).ToList(),
        JsonValueKind.String => value.GetString()!, JsonValueKind.True => true, JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetDouble(out var number) && double.IsFinite(number) => number,
        _ => throw new InvalidDataException("Content values must be finite numbers, text, booleans, arrays or records")
    };
    private static string RequiredText(JsonElement element, string key) => element.GetProperty(key).GetString() is { Length: > 0 } text
        ? text : throw new InvalidDataException("Missing text property: " + key);
    private static void RequireId(string id) { if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Empty content identity"); }
    private static void Reference(JsonElement reference)
    {
        if (RequiredText(reference, "type") != "ref") throw new InvalidDataException("Expected a field reference");
        foreach (var field in new[] { "object", "facet", "field" }) _ = RequiredText(reference, field);
    }
    private static void Condition(JsonElement condition)
    {
        var op = RequiredText(condition, "op");
        switch (op)
        {
            case "constant":
                if (RequiredText(condition, "value") is not ("true" or "false" or "unknown")) throw new InvalidDataException("Invalid truth constant");
                break;
            case "all": foreach (var child in condition.GetProperty("items").EnumerateArray()) Condition(child); break;
            case "not": Condition(condition.GetProperty("item")); break;
            case "equals": case "at_least":
                Reference(condition.GetProperty("ref")); _ = Value(condition.GetProperty("value")); break;
            default: throw new InvalidDataException("Unsupported condition: " + op);
        }
    }
}
