using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Awakening;

public sealed partial class MorningWorld
{
    private readonly StrategyAssessmentCache _strategyAssessments = new();
    private static readonly Lazy<Dictionary<string, JsonElement>> StrategyProfiles = new(() =>
    {
        using var stream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.World.json")!;
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.GetProperty("chapters")[0].GetProperty("entities").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "character")
            .ToDictionary(e => e.GetProperty("id").GetString()!["person.".Length..], e => e.GetProperty("profile").Clone());
    });
    private static CharacterStrategy[] StrategiesFor(string key) => StrategyPolicy.Read(StrategyProfiles.Value[key]);
    private static string DesireOfKey(string key) => StrategyProfiles.Value[key].GetProperty("desire").GetString()!;

    public bool TakeEvidence(string actor, string id)
    {
        if (DailyActor(actor) is not { } person || !_evidence.TryGetValue(id, out var body)) return false;
        var known = _runtime.KnownProjection(person.Soul, body);
        if (known is null) return false;
        var action = SubmitStory(Names.PersonOf(actor), new Effect.Act(ActionOp.Take, known));
        for (var steps = 0; action.Status == ExecutionStatus.Pending && steps < 100 && _session.CTB.Step(); steps++) { }
        return action.Status == ExecutionStatus.Completed && action.Result is { Succeeded: true };
    }

    public void AcquireStrategySource(string actor, string source)
    {
        if (DailyActor(actor) is not { } person) return;
        if (!StrategiesFor(person.Key).Any(s => s.Source == source)) return;
        StrategyPolicy.Acquire(person.Soul, person.Self, source, ++_time, source);
    }

    public void ObserveStrategyState(string actor, IReadOnlyDictionary<string, int> values, string basis)
    {
        var person = DailyActor(actor) ?? throw new ArgumentException("Unknown strategy actor.");
        if (values.All(v => person.Self.Tags.GetValueOrDefault(new Tag(v.Key)) == v.Value)) return;
        Observation.Observe(person.Soul, new(person.Self.Name, person.Self.Kind) { Target = person.Self,
            Source = ObservationSource.Perception, Time = ++_time, Details = basis,
            Tags = values.ToImmutableDictionary(v => new Tag(v.Key), v => (int?)v.Value) });
    }

    public DecisionAssessment? ChooseStrategyMethod(string actor, IReadOnlyList<string> methods)
    {
        if (DailyActor(actor) is not { } person) return null;
        var available = methods.Select(m => new StrategyMethod(m, new Effect.Set(person.Self, new Tag("strategy-method:" + m), 1),
            new Condition.Constant(Truth.True))).ToArray();
        // Rebind only the currently feasible methods. Strategies cannot bypass availability or create a queue.
        StrategyPolicy.Bind(person.Soul, person.Self, StrategiesFor(person.Key), available, "field-strategy", ++_time, "awakening");
        return _strategyAssessments.Assess(person.Soul.World, person.Self).FirstOrDefault(a => !a.Forbidden && a.Decision.Score > 0
            && available.Any(m => Ast.Equivalent(m.Effect, a.Decision.Effect)));
    }
    public static string StrategyMethodKey(DecisionAssessment decision)
        => ((Effect.Set)decision.Decision.Effect).Tag.Name["strategy-method:".Length..];

    public DecisionExperienceKeyV1 BeginStrategyMethod(string actor, DecisionAssessment decision)
    {
        var person = DailyActor(actor) ?? throw new ArgumentException("Unknown strategy actor.");
        var key = ActionLearning.Key(person.Soul.World, person.Self, decision.Decision.Effect);
        ActionLearning.Observe(person.Soul, person.Self, key, DecisionOutcomeV1.Attempted, _session.CTB.Now);
        return key;
    }
    public void CompleteStrategyMethod(string actor, DecisionExperienceKeyV1 key, DecisionOutcomeV1 outcome)
    {
        var person = DailyActor(actor) ?? throw new ArgumentException("Unknown strategy actor.");
        ActionLearning.Observe(person.Soul, person.Self, key, outcome, _session.CTB.Now);
    }

    public string KnownEvidenceText(string actor, string id)
    {
        if (DailyActor(actor) is not { } person || !_evidence.TryGetValue(id, out var entity)) return "";
        // The binding lookup returns only a projection already in this person's cognition.
        var known = _runtime.KnownProjection(person.Soul, entity);
        return known is null ? "" : person.Soul.World.History.LastOrDefault(h => ReferenceEquals(h.Target, known)
            && h.Source is ObservationSource.Perception or ObservationSource.Recall && h.Details != "")?.Details ?? "";
    }
    public IReadOnlyDictionary<string, int> StrategyBasis(string actor)
        => DailyActor(actor)!.Self.Tags.Where(t => t.Key.Name.StartsWith("strategy-source:") || t.Key.Name.StartsWith("observed_") || t.Key.Name == "present")
            .ToDictionary(t => t.Key.Name, t => t.Value);

    public bool NoticeMissingKnownEvidence(string actor, IEnumerable<string> ids)
    {
        if (DailyActor(actor) is not { } person) return false;
        var here = person.Body.Get<SpatialProperty>()!.Space;
        foreach (var id in ids)
        {
            if (!_evidence.TryGetValue(id, out var entity)) continue;
            var known = _runtime.KnownProjection(person.Soul, entity);
            // Absence is observed only at the location remembered by this actor, never by querying a remote room.
            if (KnownEvidenceText(actor, id) == "" || known is null || known.Parent != person.Self.Parent
                || ReferenceEquals(entity.Get<SpatialProperty>()?.Space, here)) continue;
            ObserveStrategyState(actor, new Dictionary<string, int> { ["observed_damage"] = 1 }, "missing_at_known_location:" + known.Name);
            return true;
        }
        return false;
    }
    public bool ReportKnownEvidence(string actor, string id, out string text)
    {
        text = KnownEvidenceText(actor, id);
        if (text == "" || DailyActor(actor) is not { } speaker) return false;
        var listener = _souls[Person.Detective];
        var subject = _runtime.KnownProjection(listener, speaker.Body);
        if (subject is null) return false;
        var name = actor + ":" + id;
        var prior = listener.World.Projections.FirstOrDefault(p => p.Name == name);
        Observation.Observe(listener, new(name, ProjectionKind.Concept) { Target = prior,
            Source = ObservationSource.Testimony, Existence = Truth.True, Time = ++_time, Details = text,
            AddRelations = [(Relations.Subject, subject)] });
        return true;
    }
}
