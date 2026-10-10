using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

/// <summary>Data-only expressions bound to existing projections; binding never writes cognition.</summary>
public sealed class ScriptAst(Func<string, Noun> reference, Func<string, IEnumerable<Noun>> collection,
    JsonElement procedures, IReadOnlyDictionary<string, string>? options = null,
    IReadOnlyDictionary<string, Condition>? parameters = null, ProjectionWorld? world = null)
{
    private readonly HashSet<string> _expanding = new(StringComparer.Ordinal);
    private static string Text(JsonElement e) => e.GetProperty("value").GetString()!;
    private Noun Ref(JsonElement e) => Text(e) switch
    {
        "$actor" => new Noun.Slot(RuleSlot.Actor), "$target" => new Noun.Slot(RuleSlot.Target),
        "$scene" => new Noun.Slot(RuleSlot.Scene), "$tool" => new Noun.Slot(RuleSlot.Tool),
        var id => reference(id)
    };
    private static JsonElement[] Args(JsonElement e) => e.TryGetProperty("args", out var a) ? [..a.EnumerateArray()] : [];
    private Condition Observed(JsonElement[] a)
    {
        if (Ref(a[0]) is not Noun.Reference r) throw new InvalidDataException("Historical observation requires a known reference.");
        if(world is null || !world.Contains(r.Projection))throw new InvalidDataException("Historical observation requires its subject world.");
        var tag=new Tag(Text(a[1])); var minimum=a[2].GetProperty("value").GetInt32();
        var source=Enum.Parse<ObservationSource>(Text(a[3]));
        return new Condition.Constant(world.History.Any(h=>ReferenceEquals(h.Target,r.Projection)&&h.Source==source
            &&h.Tags.TryGetValue(tag,out var value)&&value>=minimum) ? Truth.True : Truth.False);
    }
    public Condition Condition(JsonElement e)
    {
        var op = e.GetProperty("op").GetString();
        if (op == "literal") return new Condition.Constant(e.GetProperty("value").GetBoolean() ? Truth.True : Truth.False);
        var a = Args(e);
        if (op == "not") return new Condition.Not(Condition(a[0]));
        if (op == "and") return new Condition.All([.. a.Select(Condition)]);
        if (op == "or") return new Condition.Any([.. a.Select(Condition)]);
        if (op != "call") throw new NotSupportedException("Unsupported Core condition: " + op);
        return e.GetProperty("name").GetString() switch
        {
            "true" => new Condition.Constant(Truth.True), "false" => new Condition.Constant(Truth.False),
            "exists" => new Condition.Exists(Ref(a[0])),
            "tag" => new Condition.Tagged(Ref(a[0]), new Tag(Text(a[1])), a[2].GetProperty("value").GetInt32()),
            "at" => new Condition.At(Ref(a[0]), Ref(a[1])),
            "related" => new Condition.Related(Ref(a[0]), new RelationTag(Text(a[1])), Ref(a[2])),
            "kind" => new Condition.IsKind(Ref(a[0]), Enum.Parse<ProjectionKind>(Text(a[1]))),
            "any_tag" => new Condition.Any([.. collection(Text(a[0])).Select(n => (Condition)new Condition.Tagged(n, new Tag(Text(a[1])), a[2].GetProperty("value").GetInt32()))]),
            "observed" => Observed(a),
            "param" => parameters is not null && parameters.TryGetValue(Text(a[0]), out var c) ? c : throw new InvalidDataException("Unknown condition parameter: " + Text(a[0])),
            "option" => new Condition.Constant(options is not null && options.TryGetValue(Text(a[0]), out var v) && v == Text(a[1]) ? Truth.True : Truth.False),
            "choose" => Condition(a[Selected(a[0]) ? 1 : 2]),
            var name => throw new NotSupportedException("Unsupported Core condition: " + name)
        };
    }
    // Selectors may depend only on author options, never on runtime cognition or reality.
    public bool Selected(JsonElement e)
    {
        var op = e.GetProperty("op").GetString();
        if (op == "literal") return e.GetProperty("value").GetBoolean();
        var a = Args(e);
        if (op == "not") return !Selected(a[0]);
        if (op == "and") return a.All(Selected);
        if (op == "or") return a.Any(Selected);
        if (op == "call") return e.GetProperty("name").GetString() switch
        {
            "true" => true, "false" => false,
            "option" => options is not null && options.TryGetValue(Text(a[0]), out var v) && v == Text(a[1]),
            _ => throw new InvalidDataException("A selector can only read author options.")
        };
        throw new NotSupportedException("Unsupported selector: " + op);
    }
    public Effect Procedure(string id)
    {
        if (!procedures.TryGetProperty(id, out var node)) throw new InvalidDataException("Unknown procedure: " + id);
        if (!_expanding.Add(id)) throw new InvalidDataException("Recursive procedure: " + id);
        try { return Effect(node); } finally { _expanding.Remove(id); }
    }
    public Effect Effect(JsonElement e)
    {
        if (e.GetProperty("op").GetString() != "call") throw new NotSupportedException("Expected Core effect call.");
        var a = Args(e);
        Tag Tag(int i) => new(Text(a[i]));
        int Int(int i) => a[i].GetProperty("value").GetInt32();
        return e.GetProperty("name").GetString() switch
        {
            "seq" => new Effect.Sequence([..a.Select(Effect)]),
            "procedure" => Procedure(Text(a[0])), "choose" => Effect(a[Selected(a[0]) ? 1 : 2]),
            "set" => new Effect.Set(Ref(a[0]), Tag(1), Int(2)), "change" => new Effect.Change(Ref(a[0]), Tag(1), Int(2)),
            "spend" => new Effect.Spend(Ref(a[0]), Tag(1), Int(2)),
            "travel" => new Effect.Act(ActionOp.Travel, Ref(a[0])), "inspect" => new Effect.Act(ActionOp.Inspect, Ref(a[0])),
            "take" => new Effect.Act(ActionOp.Take, Ref(a[0])), "destroy" => new Effect.Act(ActionOp.Destroy, Ref(a[0])),
            "place" => new Effect.Act(ActionOp.Place, Ref(a[0]), Ref(a[1])), "unlock" => new Effect.Act(ActionOp.Unlock, Ref(a[0]), Ref(a[1])),
            "require" => new Effect.Require(Condition(a[0])), "when" => new Effect.When(Condition(a[0]), Effect(a[1])),
            "wait" => new Effect.Wait(a[0].GetProperty("value").GetDouble(), Effect(a[1])),
            "wait_until" => new Effect.Wait(Condition(a[0]), Effect(a[1])), "think" => new Effect.Think(Condition(a[0])),
            "relate" => new Effect.Relate(Ref(a[0]), new RelationTag(Text(a[1])), Ref(a[2])),
            "temporary" => new Effect.TemporarySet(Ref(a[0]),Tag(1),Int(2),a[3].GetProperty("value").GetDouble()),
            "say" => new Effect.Say(Ref(a[0]),Text(a[1]),Effect(a[2])),
            var name => throw new NotSupportedException("Unsupported Core effect: " + name)
        };
    }
    public IEnumerable<Rule> Rules(JsonElement rows)
    {
        foreach (var r in rows.EnumerateArray())
            if (Selected(r.GetProperty("select")))
                yield return new(Condition(r.GetProperty("condition")), Enum.Parse<RuleMode>(r.GetProperty("mode").GetString()!),
                    Effect(r.GetProperty("effect")), r.GetProperty("weight").GetInt32());
    }
}
