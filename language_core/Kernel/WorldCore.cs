using System.Collections.Immutable;

namespace DualOntology.Language;

public readonly record struct EntityId(string Value);
public sealed record EntityState(string Kind, EntityId? Parent = null, bool Exists = true,
    ImmutableDictionary<string, int>? Tags = null,
    ImmutableHashSet<(string Relation, EntityId Other)>? Relations = null);
public sealed record ObservedObject(Truth Existence = Truth.Unknown, string? Kind = null, string? ParentHandle = null,
    ImmutableDictionary<string, int>? Tags = null,
    ImmutableDictionary<(string Relation, string OtherHandle), Truth>? Relations = null);
public sealed record Observation(EntityId Source, string Handle, ObservedObject Facts);
public sealed record ActionBindings(string Actor, string? Target = null, string? Tool = null, string? Scene = null)
{
    internal Value Encode() => new Value.List([Pair("actor", Actor), Pair("target", Target), Pair("tool", Tool), Pair("scene", Scene)]);
    private static Value Pair(string name, string? value) => Value.Seq(new Value.Symbol(name), value is null ? Value.Null : new Value.Text(value));
}

public sealed class SubjectSnapshot
{
    internal SubjectSnapshot(object origin, Guid subject, long revision, Value facts, ImmutableArray<Rule> rules, ImmutableHashSet<string> handles,
        ImmutableArray<EvidenceClaim> claims, ImmutableHashSet<(string Model, int Question)> conceived)
        => (Origin, Subject, Revision, Facts, Rules, Handles, Claims, Conceived) = (origin, subject, revision, facts, rules, handles, claims, conceived);
    internal object Origin { get; }
    public Guid Subject { get; }
    public long Revision { get; }
    public Value Facts { get; }
    public ImmutableArray<Rule> Rules { get; }
    public ImmutableArray<EvidenceClaim> Claims { get; }
    public ImmutableHashSet<(string Model, int Question)> Conceived { get; }
    internal ImmutableHashSet<string> Handles { get; }
}
public sealed record Assessment(Effect Action, int Score, bool Forbidden, ImmutableArray<string> Prohibitions);
public sealed record ActionReceipt(bool Succeeded, string? Failure, long WorldRevision);
public sealed class PreparedAction
{
    internal PreparedAction(WorldCore origin, Guid subject, long cognitionRevision, long revision, string? failure, ImmutableDictionary<EntityId, EntityState> changes)
        => (Origin, Subject, CognitionRevision, Revision, Failure, Changes) = (origin, subject, cognitionRevision, revision, failure, changes);
    internal WorldCore Origin { get; }
    internal Guid Subject { get; }
    internal long CognitionRevision { get; }
    internal ImmutableDictionary<EntityId, EntityState> Changes { get; }
    internal bool Consumed { get; set; }
    public long Revision { get; }
    public string? Failure { get; }
}

/// <summary>Host-owned authority boundary. Subject evaluation receives copied observations only.</summary>
public sealed class WorldCore
{
    private readonly object gate = new();
    private readonly InferenceProgram language = new();
    private readonly Dictionary<EntityId, EntityState> entities = [];
    private readonly Dictionary<Guid, SubjectState> subjects = [];
    private long revision;
    private sealed class SubjectState(EntityId body)
    {
        public EntityId Body { get; } = body;
        public long Revision { get; set; }
        public Dictionary<string, EntityId> Bindings { get; set; } = new(StringComparer.Ordinal);
        public ImmutableDictionary<string, ObservedObject> Facts { get; set; } = ImmutableDictionary<string, ObservedObject>.Empty.WithComparers(StringComparer.Ordinal);
        public ImmutableArray<Rule> Rules { get; set; } = [];
        public ImmutableArray<EvidenceClaim> Claims { get; set; } = [];
        public ImmutableHashSet<(string Model, int Question)> Conceived { get; set; } = [];
    }
    public void Define(EntityId id, EntityState state)
    {
        lock (gate)
        {
            if (string.IsNullOrWhiteSpace(id.Value) || entities.ContainsKey(id)) throw new LanguageException("Invalid or duplicate entity identity.");
            TermReader.Name(state.Kind);
            if (state.Parent is { } parent && !entities.ContainsKey(parent)) throw new LanguageException("Unknown parent.");
            foreach (var (name, _) in state.Tags ?? ImmutableDictionary<string, int>.Empty) TermReader.Name(name);
            foreach (var (relation, other) in state.Relations ?? [])
            { TermReader.Name(relation); if (!entities.ContainsKey(other)) throw new LanguageException("Unknown relation target."); }
            entities.Add(id, state with { Tags = state.Tags ?? ImmutableDictionary<string, int>.Empty, Relations = state.Relations ?? [] });
            revision++;
        }
    }
    public void CreateSubject(Guid id, EntityId body)
    {
        lock (gate)
        {
            if (id == Guid.Empty || !entities.TryGetValue(body, out var state) || state.Kind != "body" || !state.Exists || subjects.Values.Any(s => s.Body == body))
                throw new LanguageException("Subject requires a unique existing body.");
            if (!subjects.TryAdd(id, new(body))) throw new LanguageException("Duplicate subject identity.");
        }
    }
    public EntityState ReadAuthority(EntityId id) { lock (gate) return entities[id]; }
    public void Observe(Guid subject, params Observation[] observations)
    {
        lock (gate)
        {
            var state = subjects[subject];
            var bindings = new Dictionary<string, EntityId>(state.Bindings, StringComparer.Ordinal);
            var facts = state.Facts;
            foreach (var input in observations)
            {
                if (string.IsNullOrWhiteSpace(input.Handle) || !entities.ContainsKey(input.Source)) throw new LanguageException("Invalid observation binding.");
                if (bindings.TryGetValue(input.Handle, out var old) && old != input.Source) throw new LanguageException("Subject handle cannot be rebound.");
                bindings[input.Handle] = input.Source;
                facts = facts.SetItem(input.Handle, input.Facts);
            }
            // Validate the whole proposed graph before replacing any observation or binding.
            foreach (var obj in facts.Values)
            {
                TermReader.TruthValue(obj.Existence);
                if (obj.Kind is { } kind) TermReader.Name(kind);
                if (obj.ParentHandle is { } parent && !facts.ContainsKey(parent)) throw new LanguageException("Unclosed parent handle.");
                foreach (var (tag, _) in obj.Tags ?? ImmutableDictionary<string, int>.Empty) TermReader.Name(tag);
                foreach (var ((relation, other), truth) in obj.Relations ?? ImmutableDictionary<(string, string), Truth>.Empty)
                { TermReader.Name(relation); TermReader.TruthValue(truth); if (!facts.ContainsKey(other)) throw new LanguageException("Unclosed relation handle."); }
            }
            state.Bindings = bindings;
            state.Facts = facts;
            state.Revision++;
        }
    }
    public void AcceptRules(Guid subject, params Rule[] rules)
    {
        lock (gate)
        {
            var state = subjects[subject];
            var all = state.Rules.AddRange(rules);
            if (all.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != all.Length) throw new LanguageException("Duplicate rule identity.");
            foreach (var rule in rules) rule.Encode();
            // All explicit references must be owned by the receiving subject.
            foreach (var rule in rules) ValidateReferences(rule.Encode(), state.Facts.Keys.ToImmutableHashSet(StringComparer.Ordinal));
            state.Rules = all;
            state.Revision++;
        }
    }
    public void RevokeScope(Guid subject, RuleLifetime lifetime, string owner)
    {
        lock (gate)
        {
            if (lifetime == RuleLifetime.World || !Enum.IsDefined(lifetime) || string.IsNullOrWhiteSpace(owner)) throw new LanguageException("Invalid revocation scope.");
            var state = subjects[subject];
            state.Rules = [.. state.Rules.Where(r => r.Lifetime != lifetime || r.Owner != owner)];
            state.Revision++;
        }
    }
    public void ObserveClaims(Guid subject, params EvidenceClaim[] claims)
    {
        lock (gate)
        {
            var state = subjects[subject];
            foreach (var claim in claims)
            {
                TermReader.Name(claim.Model);
                if (!state.Facts.ContainsKey(claim.Handle) || claim.Speaker is { } speaker && !state.Facts.ContainsKey(speaker)) throw new LanguageException("Claim source is outside subject view.");
                if (claim.Question < 0 || claim.Option < 0 || !Enum.IsDefined(claim.Source)) throw new LanguageException("Invalid claim.");
            }
            var merged = state.Claims;
            foreach (var claim in claims) merged = [.. merged.Where(c => c.Model != claim.Model || c.Handle != claim.Handle), claim];
            state.Claims = merged;
            state.Revision++;
        }
    }
    public void Conceive(Guid subject, string model, int question)
    {
        lock (gate)
        {
            TermReader.Name(model);
            if (question < 0) throw new LanguageException("Invalid conceived question.");
            subjects[subject].Conceived = subjects[subject].Conceived.Add((model, question));
            subjects[subject].Revision++;
        }
    }
    public SubjectSnapshot Capture(Guid subject)
    {
        lock (gate)
        {
            var state = subjects[subject];
            return new(this, subject, state.Revision, EncodeFacts(state.Facts), state.Rules, state.Facts.Keys.ToImmutableHashSet(StringComparer.Ordinal), state.Claims, state.Conceived);
        }
    }
    public Truth Evaluate(SubjectSnapshot view, Condition condition, ActionBindings bindings)
    {
        lock (gate)
        {
            Validate(view, bindings, condition.Encode());
            return TermReader.DecodeTruth(language.Call("eval-condition", condition.Encode(), view.Facts, bindings.Encode()));
        }
    }
    public ImmutableArray<Assessment> Assess(SubjectSnapshot view, ActionBindings bindings)
    {
        lock (gate)
        {
            Validate(view, bindings, Value.Null);
            var result = language.Call("assess", new Value.List([.. view.Rules.Select(r => r.Encode())]), view.Facts, bindings.Encode());
            return [.. Value.ItemsOf(result).Select(item =>
            {
                var row = TermReader.Form(item, 4);
                return new Assessment(Effect.Decode(row[0]), TermReader.Integer(row[1]), row[2] == Value.True,
                    [.. Value.ItemsOf(row[3]).Select(Value.SymbolOf)]);
            })];
        }
    }
    public ImmutableArray<(string RuleId, Condition Conclusion)> Infer(SubjectSnapshot view, ActionBindings bindings)
    {
        lock (gate)
        {
            Validate(view, bindings, Value.Null);
            var result = language.Call("infer", new Value.List([.. view.Rules.Select(r => r.Encode())]), view.Facts, bindings.Encode());
            return [.. Value.ItemsOf(result).Select(item => (Value.SymbolOf(Value.ItemsOf(item)[0]), Condition.Decode(Value.ItemsOf(item)[1])))];
        }
    }
    public SubjectiveProof Prove(SubjectSnapshot view, Condition goal, ActionBindings bindings, int depth = 1, int expansions = 256, ImmutableArray<Condition> assumptions = default)
    {
        lock (gate)
        {
            Validate(view, bindings, goal.Encode());
            var assumed = assumptions.IsDefault ? ImmutableArray<Condition>.Empty : assumptions;
            foreach (var assumption in assumed) ValidateReferences(assumption.Encode(), view.Handles);
            var result = Value.ItemsOf(language.Call("prove-condition", new Value.List([.. view.Rules.Select(r => r.Encode())]),
                MakeView(view), bindings.Encode(), goal.Encode(), new Value.List([.. assumed.Select(c => c.Encode())]), new Value.Number(depth), new Value.Number(expansions)));
            var proof = TermReader.Form(result[1], 6);
            return new(view.Subject, view.Revision, TermReader.DecodeTruth(proof[1]), proof[2] == Value.True, proof[3] == Value.True,
                [.. Value.ItemsOf(proof[4]).Select(Value.SymbolOf)], Value.SymbolOf(proof[5]), TermReader.Integer(result[2]));
        }
    }
    public PlannedAction Plan(SubjectSnapshot view, ActionBindings bindings, int depth = 1, int expansions = 256)
    {
        lock (gate)
        {
            Validate(view, bindings, Value.Null);
            var result = language.Call("plan", new Value.List([.. view.Rules.Select(r => r.Encode())]), MakeView(view), bindings.Encode(), new Value.Number(depth), new Value.Number(expansions));
            var summary = Value.ItemsOf(language.Call("planning-summary", result));
            if (Value.SymbolOf(summary[0]) == "no-action") return new(view.Subject, view.Revision, null, Value.Seq(), 0, 0, TermReader.Integer(summary[2]), "no-action",
                [.. Value.ItemsOf(summary[1]).Select(Value.SymbolOf)], 0);
            return new(view.Subject, view.Revision, Effect.Decode(summary[1]), summary[2], Value.NumberOf(summary[3]), TermReader.Integer(summary[4]), TermReader.Integer(summary[5]), Value.SymbolOf(summary[6]),
                [.. Value.ItemsOf(summary[7]).Select(Value.SymbolOf)], TermReader.Integer(summary[8]));
        }
    }
    public CaseBelief Reason(SubjectSnapshot view, CaseModel model)
    {
        lock (gate)
        {
            if (!ReferenceEquals(view.Origin, this)) throw new LanguageException("Foreign subject view.");
            var hypotheses = model.EncodeHypotheses();
            foreach (var claim in view.Claims.Where(c => c.Model == model.Id))
                if (claim.Question >= model.OptionCounts.Length || claim.Option >= model.OptionCounts[claim.Question]) throw new LanguageException("Claim is outside the approved case model.");
            var fields = new Value.List([.. model.Fields.Select(f => Value.Seq(new Value.Text(f.Handle), TermReader.Name(f.Tag), TermReader.Name(f.Key)))]);
            var defaults = new Value.List([.. model.Defaults.OrderBy(p => p.Key).Select(p => Value.Seq(new Value.Number(p.Key), new Value.Number(p.Value)))]);
            var result = language.Call("case-assess", MakeView(view, model.Id), hypotheses, fields, defaults);
            var entries = Value.ItemsOf(result);
            if (entries.Length == 2 && entries[0] is Value.Symbol { Name: "unmodeled" }) throw new LanguageException(Value.Print(result));
            var distribution = Value.ItemsOf(Lookup(entries, "distribution"))[1];
            var credence = language.Call("case-marginals", result, hypotheses, new Value.List([.. model.OptionCounts.Select(c => new Value.Number(c))]));
            return new(view.Subject, view.Revision,
                Value.ItemsOf(distribution).ToImmutableDictionary(p => Value.SymbolOf(Value.ItemsOf(p)[0]), p => Value.NumberOf(Value.ItemsOf(p)[1]), StringComparer.Ordinal),
                [.. Value.ItemsOf(credence).Select(row => Value.ItemsOf(row).Select(Value.NumberOf).ToImmutableArray())],
                [.. Value.ItemsOf(Lookup(entries, "candidates")).Select(Value.SymbolOf)], Lookup(entries, "contradicted") == Value.True, Lookup(entries, "basis"));
        }
    }
    private Value MakeView(SubjectSnapshot snapshot, string? model = null)
    {
        var claims = new Value.List([.. snapshot.Claims.Where(c => c.Model == model).Select(c => Value.Seq(new Value.Text(c.Handle),
            new Value.Number(c.Question), new Value.Number(c.Option), new Value.Symbol(c.Source.ToString().ToLowerInvariant()),
            c.Speaker is null ? Value.Null : new Value.Text(c.Speaker), new Value.Number(c.Strength)))]);
        var conceived = new Value.List([.. snapshot.Conceived.Where(c => c.Model == model).Select(c => new Value.Number(c.Question))]);
        return language.Call("view", new Value.Text(snapshot.Subject.ToString("D")), new Value.Number(snapshot.Revision), snapshot.Facts, conceived, claims, Value.Seq());
    }
    private void Validate(SubjectSnapshot view, ActionBindings bindings, Value term)
    {
        if (!ReferenceEquals(view.Origin, this)) throw new LanguageException("Snapshot belongs to another authority boundary.");
        foreach (var handle in new[] { bindings.Actor, bindings.Target, bindings.Tool, bindings.Scene }.Where(h => h is not null))
            if (!view.Handles.Contains(handle!)) throw new LanguageException("Binding is outside subject view.");
        ValidateReferences(term, view.Handles);
    }
    private static void ValidateReferences(Value value, ImmutableHashSet<string> handles)
    {
        if (value is not Value.List list) return;
        if (list.Items.Length == 2 && list.Items[0] is Value.Symbol { Name: "ref" })
        { if (list.Items[1] is not Value.Text text || !handles.Contains(text.Data)) throw new LanguageException("Reference is outside subject view."); }
        else foreach (var child in list.Items) ValidateReferences(child, handles);
    }
    public PreparedAction Prepare(Guid subject, Effect effect, ActionBindings bindings)
    {
        lock (gate)
        {
            var state = subjects[subject];
            var view = Capture(subject);
            Validate(view, bindings, effect.Encode());
            if (state.Bindings[bindings.Actor] != state.Body) return Failed("no-control");
            if (!entities[state.Body].Exists) return Failed("missing-actor");
            // Authority facts are internal to preparation; no returned snapshot exposes them.
            var reverse = state.Bindings.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Key, StringComparer.Ordinal).First().Key);
            var aliases = new Value.List([.. state.Bindings.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => Value.Seq(new Value.Text(p.Key), new Value.Text(reverse[p.Value])))]);
            var canonicalEffect = language.Call("canonical-nouns", effect.Encode(), bindings.Encode(), aliases);
            var actual = reverse.ToImmutableDictionary(p => p.Value, p =>
            {
                var e = entities[p.Key];
                var relations = (e.Relations ?? []).Where(r => reverse.ContainsKey(r.Other))
                    .ToImmutableDictionary(r => (r.Relation, reverse[r.Other]), _ => Truth.True);
                return new ObservedObject(e.Exists ? Truth.True : Truth.False, e.Kind,
                    e.Parent is { } parent ? reverse.GetValueOrDefault(parent, "<unmapped-parent>") : null, e.Tags, relations);
            }, StringComparer.Ordinal);
            var context = new Value.List([.. Value.ItemsOf(bindings.Encode()), Value.Seq(new Value.Symbol("realm"), new Value.Symbol("authority"))]);
            var actualFacts = EncodeFacts(actual);
            foreach (var touched in Value.ItemsOf(language.Call("touches", canonicalEffect, actualFacts, context)))
            {
                if (touched is not Value.Text handle || !state.Bindings.TryGetValue(handle.Data, out var entity)) return Failed("unbound-noun");
                if (!Reachable(state.Body, entity)) return Failed("out-of-reach");
                if (!entities[entity].Exists) return Failed("missing-object");
            }
            // Rule premises remain subjective. Bound effects share physical identities only here.
            var active = language.Call("active-rules", new Value.List([.. state.Rules.Select(r => r.Encode())]), view.Facts, bindings.Encode());
            var canonicalRules = language.Call("canonical-nouns", active, bindings.Encode(), aliases);
            var assessment = Value.ItemsOf(language.Call("assess-one", canonicalEffect, canonicalRules, actualFacts, context));
            if (assessment[2] == Value.True) return Failed("forbidden");
            var result = Value.ItemsOf(language.Call("prepare", canonicalEffect, actualFacts, context));
            if (Value.SymbolOf(result[0]) != "ok") return Failed(Value.SymbolOf(result[1]));
            var changes = ImmutableDictionary.CreateBuilder<EntityId, EntityState>();
            foreach (var entry in Value.ItemsOf(result[1]))
            {
                var pair = TermReader.Form(entry, 2);
                var handle = ((Value.Text)pair[0]).Data;
                var id = state.Bindings[handle];
                var encoded = Value.ItemsOf(pair[1]);
                var tagData = Lookup(encoded, "tags");
                var tags = Value.ItemsOf(tagData).ToImmutableDictionary(p => Value.SymbolOf(Value.ItemsOf(p)[0]), p => TermReader.Integer(Value.ItemsOf(p)[1]), StringComparer.Ordinal);
                var relationData = Lookup(encoded, "relations");
                var relations = entities[id].Relations!.Where(r => !reverse.ContainsKey(r.Other)).ToImmutableHashSet();
                foreach (var relation in Value.ItemsOf(relationData))
                {
                    var r = Value.ItemsOf(relation);
                    if (r[1] == Value.True)
                    {
                        var key = TermReader.Form(r[0], 2);
                        relations = relations.Add((Value.SymbolOf(key[0]), state.Bindings[((Value.Text)key[1]).Data]));
                    }
                }
                var changed = entities[id] with { Tags = tags, Relations = relations };
                if (entities[id].Tags!.OrderBy(p => p.Key).SequenceEqual(tags.OrderBy(p => p.Key)) && entities[id].Relations!.SetEquals(relations)) continue;
                // Multiple known facets of one entity cannot yield contradictory writes.
                if (changes.TryGetValue(id, out var previous) && (!previous.Tags!.OrderBy(p => p.Key).SequenceEqual(tags.OrderBy(p => p.Key)) || !previous.Relations!.SetEquals(relations)))
                    return Failed("conflicting-facets");
                changes[id] = changed;
            }
            return new(this, subject, state.Revision, revision, null, changes.ToImmutable());
            PreparedAction Failed(string failure) => new(this, subject, state.Revision, revision, failure, ImmutableDictionary<EntityId, EntityState>.Empty);
        }
    }
    public ActionReceipt Commit(PreparedAction action)
    {
        lock (gate)
        {
            if (!ReferenceEquals(action.Origin, this)) throw new LanguageException("Foreign prepared action.");
            if (action.Consumed) return new(false, "already-committed", revision);
            action.Consumed = true;
            if (action.Failure is { } failure) return new(false, failure, revision);
            if (action.Revision != revision) return new(false, "stale-world", revision);
            if (subjects[action.Subject].Revision != action.CognitionRevision) return new(false, "stale-cognition", revision);
            foreach (var (id, state) in action.Changes) entities[id] = state;
            revision++;
            return new(true, null, revision);
        }
    }
    private bool Reachable(EntityId actor, EntityId target)
    {
        if (actor == target) return true;
        EntityId? Room(EntityId id)
        {
            var seen = new HashSet<EntityId>();
            while (entities[id].Parent is { } parent)
            {
                if (!seen.Add(parent) || !entities[parent].Exists || entities[parent].Tags!.GetValueOrDefault("Locked") > 0 || entities[parent].Kind == "body" && parent != actor) return null;
                if (entities[parent].Kind == "space") return parent;
                id = parent;
            }
            return null;
        }
        return Room(actor) is { } room && (Room(target) == room || target == room);
    }
    private static Value Lookup(ImmutableArray<Value> entries, string name) => entries.Select(Value.ItemsOf).First(e => e[0] is Value.Symbol s && s.Name == name)[1];
    private static Value EncodeFacts(ImmutableDictionary<string, ObservedObject> facts) => new Value.List([.. facts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
    {
        var f = p.Value;
        var fields = new List<Value> { Value.Seq(new Value.Symbol("exists"), TermReader.TruthValue(f.Existence)) };
        if (f.Kind is { } kind) fields.Add(Value.Seq(new Value.Symbol("kind"), new Value.Symbol(kind)));
        if (f.ParentHandle is { } parent) fields.Add(Value.Seq(new Value.Symbol("at"), new Value.Text(parent)));
        fields.Add(Value.Seq(new Value.Symbol("tags"), new Value.List([.. (f.Tags ?? ImmutableDictionary<string, int>.Empty).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => Value.Seq(new Value.Symbol(p.Key), new Value.Number(p.Value)))])));
        fields.Add(Value.Seq(new Value.Symbol("relations"), new Value.List([.. (f.Relations ?? ImmutableDictionary<(string, string), Truth>.Empty).OrderBy(p => p.Key).Select(p => Value.Seq(Value.Seq(new Value.Symbol(p.Key.Relation), new Value.Text(p.Key.OtherHandle)), TermReader.TruthValue(p.Value)))])));
        return Value.Seq(new Value.Text(p.Key), new Value.List([.. fields]));
    })]);
}
