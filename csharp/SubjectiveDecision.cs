using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DualOntologyWorldEngine.Core;

public enum DecisionOutcomeV1 { Attempted, Succeeded, Failed, NoProgress, Interrupted }
public sealed record DecisionExperienceKeyV1([property:JsonRequired] string Method, [property:JsonRequired] string Context = "");
public sealed record DecisionExperienceV1([property:JsonRequired] DecisionExperienceKeyV1 Key,
    [property:JsonRequired] int Attempts = 0, [property:JsonRequired] int Successes = 0,
    [property:JsonRequired] int Failures = 0, [property:JsonRequired] int NoProgress = 0, [property:JsonRequired] int Interruptions = 0);
public sealed record DecisionMemoryV1([property:JsonRequired] int Version, [property:JsonRequired] ImmutableArray<DecisionExperienceV1> Experiences);

/// <summary>One actor's observed input history. No world access, clock, geometry or UI semantics.</summary>
public sealed class SubjectiveDecisionMemory
{
    private readonly Dictionary<DecisionExperienceKeyV1, DecisionExperienceV1> _entries = [];
    public DecisionExperienceV1 Get(DecisionExperienceKeyV1 key) { Validate(key); return _entries.GetValueOrDefault(key) ?? new(key); }
    public DecisionExperienceV1 Record(DecisionExperienceKeyV1 key, DecisionOutcomeV1 outcome)
    {
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        var old = Get(key);
        int Next(int n) => Math.Min(n, 999999) + 1;
        var updated = outcome switch
        {
            DecisionOutcomeV1.Attempted => old with { Attempts = Next(old.Attempts) },
            DecisionOutcomeV1.Succeeded => old with { Successes = Next(old.Successes) },
            DecisionOutcomeV1.Failed => old with { Failures = Next(old.Failures) },
            DecisionOutcomeV1.NoProgress => old with { NoProgress = Next(old.NoProgress) },
            _ => old with { Interruptions = Next(old.Interruptions) }
        };
        if (!_entries.ContainsKey(key) && _entries.Count >= 20000) throw new InvalidOperationException("decision_memory_limit");
        _entries[key] = updated;
        return updated;
    }
    public DecisionMemoryV1 Capture() => new(1, [.._entries.Values.OrderBy(e => e.Key.Method, StringComparer.Ordinal).ThenBy(e => e.Key.Context, StringComparer.Ordinal)]);
    public static SubjectiveDecisionMemory Restore(DecisionMemoryV1 state)
    {
        if (state.Version != 1 || state.Experiences.IsDefault || state.Experiences.Length > 20000) throw new InvalidDataException("decision_memory_version_or_limit");
        var result = new SubjectiveDecisionMemory();
        foreach (var experience in state.Experiences)
        {
            Validate(experience);
            if (!result._entries.TryAdd(experience.Key, experience)) throw new InvalidDataException("decision_memory_entry");
        }
        return result;
    }
    public void ReconsiderAttempts(IEnumerable<DecisionExperienceKeyV1> keys)
    {
        // A new relevant observation permits another attempt; it does not erase
        // success, failure or interrupted execution. Hosts choose relevant contexts.
        foreach (var key in keys.ToArray())
            if (_entries.TryGetValue(key, out var old)) _entries[key] = old with { Attempts = 0 };
    }
    public byte[] Encode() => JsonSerializer.SerializeToUtf8Bytes(Capture());
    public static SubjectiveDecisionMemory Decode(byte[] bytes)
    {
        using var document = CognitionCodec.Parse(bytes);
        return Restore(document.RootElement.Deserialize<DecisionMemoryV1>(new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("decision_memory_missing"));
    }
    private static void Validate(DecisionExperienceKeyV1 key)
    {
        if (key is null || string.IsNullOrWhiteSpace(key.Method) || key.Method.Length > 2048 || key.Context is null || key.Context.Length > 2048)
            throw new InvalidDataException("decision_experience_key");
    }
    internal static void Validate(DecisionExperienceV1 experience)
    {
        if (experience is null) throw new InvalidDataException("decision_memory_entry");
        Validate(experience.Key);
        if (new[] { experience.Attempts, experience.Successes, experience.Failures, experience.NoProgress, experience.Interruptions }.Any(n => n is < 0 or > 1000000))
            throw new InvalidDataException("decision_memory_entry");
    }
}

public sealed record DecisionScoreInputV1(int RuleScore, DecisionExperienceV1 Experience, int RevisitWeight = 1,
    int InformationValue = 0, int Cost = 0, bool NecessaryReturn = false, bool Forbidden = false, int? FailureWeight = null);
public sealed record DecisionScoreV1(int Score, bool Forbidden, ImmutableArray<string> Basis);

public static class SubjectiveDecision
{
    /// <summary>Compose subjective rule, expected information, effort and observed experience.</summary>
    public static DecisionScoreV1 Evaluate(DecisionScoreInputV1 input)
    {
        if (input.RevisitWeight is < 0 or > 1000000 || input.FailureWeight is < 0 or > 1000000 || input.Cost < 0) throw new ArgumentOutOfRangeException(nameof(input));
        SubjectiveDecisionMemory.Validate(input.Experience);
        var reasons = ImmutableArray.CreateBuilder<string>();
        long score = (long)input.RuleScore + input.InformationValue - input.Cost;
        var memory = input.Experience;
        if (memory.Attempts > 0 && input.RevisitWeight > 0)
        {
            score -= (long)Math.Min(memory.Attempts, input.NecessaryReturn ? 1 : 1000) * input.RevisitWeight;
            reasons.Add("revisit_without_new_accepted_material:" + memory.Attempts);
        }
        if (memory.Failures > 0 || memory.NoProgress > 0)
        {
            score -= (long)Math.Min((long)memory.Failures + memory.NoProgress, 1000) * Math.Max(1, input.FailureWeight ?? input.RevisitWeight) * 4;
            if (memory.Failures > 0) reasons.Add("observed_action_failed:" + memory.Failures);
            if (memory.NoProgress > 0) reasons.Add("observed_movement_stalled:" + memory.NoProgress);
        }
        if (input.InformationValue != 0) reasons.Add("expected_subjective_information");
        return new((int)Math.Clamp(score, -100000000L, 100000000L), input.Forbidden, reasons.ToImmutable());
    }
    public static int DirectionValue(double distance, double nearest, double farthest, int weight)
    {
        if (!double.IsFinite(distance) || !double.IsFinite(nearest) || !double.IsFinite(farthest) || weight < 0) throw new ArgumentOutOfRangeException(nameof(distance));
        // Scaling before subtraction keeps finite extreme inputs finite.
        var scale = Math.Max(1, Math.Max(Math.Abs(distance), Math.Max(Math.Abs(nearest), Math.Abs(farthest))));
        var ratio = (farthest / scale - distance / scale) / Math.Max(1 / scale, farthest / scale - nearest / scale);
        return (int)Math.Clamp(Math.Clamp(ratio, 0, 1) * weight * 2, 0, int.MaxValue);
    }
    public static bool ShouldReconsider(double currentScore, double proposedScore, bool interruptible)
        => interruptible && proposedScore > currentScore;
    public static int InformationValue(SubjectiveProofV1 proof, bool servesCurrentGoal, int weight)
    {
        if (weight is < 0 or > 1000000) throw new ArgumentOutOfRangeException(nameof(weight));
        return proof.Value == Truth.Unknown && !proof.Contradicted ? weight * (servesCurrentGoal ? 3 : 1) : 0;
    }
}

/// <summary>Live adapter for observed executions, stored in the actor's cognition and ordinary save graph.</summary>
public static class ActionLearning
{
    private static readonly Tag ExperienceTag = new("core.decision-experience.v1");
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    private sealed class DecodedObservation
    {
        internal ObservationEvent? Observation;
        internal DecisionMemoryV1? State;
    }
    private static readonly ConditionalWeakTable<Projection, DecodedObservation> Decoded = new();
    private static DecisionMemoryV1 ReadNode(Projection node)
    {
        var observation = node.LastObservation ?? throw new InvalidDataException("decision_memory_missing");
        var cache = Decoded.GetOrCreateValue(node);
        if (!ReferenceEquals(cache.Observation, observation))
        {
            var parsed = SubjectiveDecisionMemory.Decode(Encoding.UTF8.GetBytes(observation.Details)).Capture();
            cache.State = parsed;
            cache.Observation = observation;
        }
        return cache.State!;
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static Tag KeyTag(DecisionExperienceKeyV1 key) => new("core.decision-experience.key:" + Hash(JsonSerializer.Serialize(key)));
    private static string Method(ProjectionWorld world, Effect effect)
    {
        var frozen = CognitionAst.Freeze(effect, projection => world.Contains(projection)
            ? projection.SubjectId!.Value : throw new InvalidOperationException("action_learning_reference_domain"));
        return Hash(CognitionCodec.Write(frozen)!.ToJsonString());
    }
    /// <summary>Only the actor and referenced known objects affect retry context. Unrelated observations do not.</summary>
    public static string Context(ProjectionWorld world, Projection actor, Effect effect)
    {
        if (!world.Contains(actor)) throw new InvalidOperationException("action_learning_actor_domain");
        var refs = Ast.Nouns(effect).OfType<Noun.Reference>().Select(r => r.Projection).Append(actor).Distinct().ToArray();
        if (refs.Any(p => !world.Contains(p))) throw new InvalidOperationException("action_learning_reference_domain");
        return Hash(JsonSerializer.Serialize(refs.OrderBy(p => p.SubjectId!.Value.ToString(), StringComparer.Ordinal).Select(p => new {
            p.SubjectId, p.Existence, Parent = p.Parent?.SubjectId,
            Tags = p.Tags.OrderBy(t => t.Key.Name, StringComparer.Ordinal).Select(t => new { t.Key.Name, t.Value }),
            Relations = p.Relations.OrderBy(r => r.Relation.Name, StringComparer.Ordinal).ThenBy(r => r.Target.SubjectId!.Value.ToString(), StringComparer.Ordinal)
                .Select(r => new { r.Relation.Name, r.Target.SubjectId })
        })));
    }
    public static DecisionExperienceKeyV1 Key(ProjectionWorld world, Projection actor, Effect effect)
        => new(Method(world, effect), Context(world, actor, effect));
    private static SubjectiveDecisionMemory Read(ProjectionWorld world, Projection actor)
    {
        if (!world.Contains(actor)) throw new InvalidOperationException("action_learning_actor_domain");
        var entries = world.Projections.Where(p => p.Tags.GetValueOrDefault(ExperienceTag) == 1 && p.Relations.Contains((Relations.Subject, actor)))
            .SelectMany(p => ReadNode(p).Experiences).ToImmutableArray();
        return SubjectiveDecisionMemory.Restore(new(1, entries));
    }
    public static DecisionMemoryV1 Capture(ProjectionWorld world, Projection actor)
    {
        lock (world.Gate) return Read(world, actor).Capture();
    }
    public static void Observe(SoulEssence soul, Projection actor, DecisionExperienceKeyV1 key, DecisionOutcomeV1 outcome, double time = 0)
    {
        lock (soul.World.Gate) ObserveLocked(soul, actor, key, outcome, time);
    }
    private static void ObserveLocked(SoulEssence soul, Projection actor, DecisionExperienceKeyV1 key, DecisionOutcomeV1 outcome, double time)
    {
        if (!soul.World.Contains(actor)) throw new InvalidOperationException("action_learning_actor_domain");
        var marker = KeyTag(key);
        var nodes = soul.World.Projections.Where(p => p.Tags.GetValueOrDefault(ExperienceTag) == 1 && p.Relations.Contains((Relations.Subject, actor))).ToArray();
        var node = nodes.FirstOrDefault(p => p.Tags.GetValueOrDefault(marker) == 1);
        if (node is null && nodes.Length >= 20000) throw new InvalidOperationException("decision_memory_limit");
        var state = node is null ? new SubjectiveDecisionMemory() : SubjectiveDecisionMemory.Restore(ReadNode(node));
        state.Record(key, outcome);
        Observation.Observe(soul, new ObservationInput("core.decision-experience", ProjectionKind.Concept)
        {
            Target = node, Source = ObservationSource.Introspection, Existence = Truth.True, Time = time,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(ExperienceTag, 1).Add(marker, 1), AddRelations = [(Relations.Subject, actor)],
            Details = JsonSerializer.Serialize(state.Capture(), Json)
        });
    }
    public static IReadOnlyList<DecisionAssessment> Assess(ProjectionWorld world, Projection actor, ReasoningBudgetV1? budget = null)
    {
        // Rule evaluation, retry context and learning must see one published version.
        lock (world.Gate) return AssessLocked(world, actor, budget);
    }
    private static IReadOnlyList<DecisionAssessment> AssessLocked(ProjectionWorld world, Projection actor, ReasoningBudgetV1? budget)
    {
        var raw = DecisionSystem.Assess(world, actor);
        var memory = Read(world, actor);
        var snapshot = Contract.Value(world.CaptureSnapshot());
        var reasoner = new SubjectiveReasoner(snapshot, actor.SubjectId!.Value, budget ?? new());
        var inquiries = reasoner.Explore([..reasoner.KnownQueries.Take(16)], []).Inquiries;
        return raw.Select(a => {
            var experience = memory.Get(Key(world, actor, a.Decision.Effect));
            var targets = Ast.Nouns(a.Decision.Effect).OfType<Noun.Reference>().Where(r => !ReferenceEquals(r.Projection, actor))
                .Select(r => r.Projection.SubjectId!.Value).ToHashSet();
            // Unknown effects do not supply hidden knowledge. Only an inspection
            // of known targets is an information-gathering method.
            var inspect = HasInspection(a.Decision.Effect);
            var information = inspect && experience.Attempts == 0 ? inquiries.Where(q => q.Targets.Any(targets.Contains)).Sum(q => 1 + Math.Min(q.DiscriminatingPairs, 1000)) : 0;
            var score = SubjectiveDecision.Evaluate(new(a.Decision.Score, experience,
                (int)Math.Clamp(Math.Abs((long)a.Decision.Score), 1, 1000000), information, Forbidden: a.Forbidden));
            return a with { Decision = a.Decision with { Score = score.Score }, Reasons = a.Reasons.Concat(score.Basis).ToArray() };
        }).OrderBy(a => a.Forbidden).ThenByDescending(a => a.Decision.Score).ToArray();
    }
    private static bool HasInspection(Effect effect) => effect switch {
        Effect.Act { Operation: ActionOp.Inspect } => true,
        Effect.Sequence sequence => sequence.Items.Any(HasInspection),
        Effect.Wait wait => HasInspection(wait.Body),
        Effect.When conditional => HasInspection(conditional.Body),
        _ => false
    };
}
