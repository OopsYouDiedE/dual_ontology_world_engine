using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

public sealed class CandidatePolicy
{
    public string Mode { get; set; } = "";
    public int Weight { get; set; } = 1;
}
public sealed class WorldCandidateDefinition
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Scope { get; set; } = "world";
    public string Actor { get; set; } = "";
    public string? Target { get; set; }
    public string Label { get; set; } = "";
    public string Intent { get; set; } = "";
    public DialogueExpression? Applicability { get; set; }
    public DialogueExpression? Goal { get; set; }
    public List<DialogueInstruction> Response { get; set; } = new();
    public List<string> ResponseTo { get; set; } = new();
    public CandidatePolicy Policy { get; set; } = new();
    public double Delay { get; set; } = 1;
}

// The host supplies verified perception and player intent, never authoritative state writes.
public abstract record WorldInput;
public sealed record RequestCandidates(string Actor, string? Target = null) : WorldInput;
public sealed record SelectCandidate(string Token) : WorldInput;
public sealed record VerifiedPerception(string Actor, IReadOnlyList<string> Entities) : WorldInput;
public sealed record RequestReaction(string Actor, string? ResponseTo = null) : WorldInput;
public sealed record AdvanceWorld(double Until) : WorldInput;
public sealed record PresentationCompleted(string PresentationId) : WorldInput;
public abstract record WorldOutput;
public sealed record AvailableCandidate(string Token, string Id, string Label);
public sealed record CandidatesAvailable(string Actor, IReadOnlyList<AvailableCandidate> Candidates) : WorldOutput;
public sealed record CandidateActionStarted(string ExecutionId, string Actor, string CandidateId) : WorldOutput;
public sealed record CandidateActionFinished(string ExecutionId, string Actor, string CandidateId, bool Succeeded, ActionFailure Failure) : WorldOutput;
public sealed record SemanticDialogue(string PresentationId, IReadOnlyList<InteractionLine> Lines) : WorldOutput;
public sealed record NoApplicableCandidate(string Actor) : WorldOutput;
public sealed record CandidateAssessment(string CandidateId, bool Forbidden, int Score, IReadOnlyList<string> Reasons);

public sealed partial class WorldSession
{
    private sealed record BoundCandidate(string Chapter, string Id, string Kind, string Scope, string Actor, string? Target,
        string Label, DialogueExpression? Applicability, DialogueExpression? Goal, ImmutableArray<DialogueInstruction> Response,
        ImmutableArray<string> ResponseTo, int Weight, double Delay);
    private sealed record PendingChoice(BoundCandidate Candidate, string Actor, long Version);
    private sealed record ActiveCandidate(string ExecutionId, string Actor, BoundCandidate Candidate);
    private readonly Dictionary<string, List<BoundCandidate>> _candidates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Projection>> _known = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Actor, string Candidate), Projection> _responseMarkers = new();
    private readonly Dictionary<string, PendingChoice> _choiceTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<ActionExecution, ActiveCandidate> _executions = new();
    private readonly HashSet<string> _presentations = new(StringComparer.Ordinal);
    private readonly Queue<WorldOutput> _outputs = new();
    private long _version, _serial;
    private void InvalidateChoiceTokens() { _version++; _choiceTokens.Clear(); }
    private void RegisterCandidates(string chapter, List<BoundCandidate> candidates) => _candidates.Add(chapter, candidates);
    private void RememberInitialCharacter(Planned item, ScriptCharacter actor)
    {
        _known.Add(item.Id, new(StringComparer.Ordinal) { [item.Id] = actor.Self,
            [item.Space!] = actor.Self.Parent ?? throw new InvalidOperationException("Character initialization omitted its room perception.") });
        if (item.Beliefs.Count > 0) Observation.Observe(actor.Soul, new(actor.Self.Name, actor.Self.Kind)
        { Target = actor.Self, Source = ObservationSource.Introspection, Time = Simulation.CTB.Now,
            Tags = item.Beliefs.ToImmutableDictionary(p => new Tag(p.Key), p => (int?)p.Value) });
    }
    public IReadOnlyList<WorldOutput> Process(WorldInput input)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            switch (input)
            {
                case VerifiedPerception perception:
                    var actorId = Resolve(perception.Actor, CurrentChapter ?? "", _entities.Keys);
                    var actor = Character(actorId);
                    var references = perception.Entities.Select(id => Resolve(id, CurrentChapter ?? "", _entities.Keys)).Distinct().ToArray();
                    // Validate the complete input before observing any entity.
                    foreach (var id in references) if (Runtime.Representations(_entities[id].Entity).Count != 1)
                        throw new InvalidOperationException("This binding expects one reality representation per script entity.");
                    foreach (var id in references)
                    {
                        var seen = Runtime.Observe(actor.Soul, Runtime.Representations(_entities[id].Entity), Simulation.CTB.Now);
                        _known[actorId][id] = seen.Single();
                    }
                    InvalidateChoiceTokens();
                    break;
                case RequestCandidates request: Offer(request.Actor, request.Target); break;
                case SelectCandidate selection: SubmitChoice(selection.Token); break;
                case RequestReaction reaction: React(reaction.Actor, reaction.ResponseTo); break;
                case AdvanceWorld advance:
                    if (!double.IsFinite(advance.Until) || advance.Until < Simulation.CTB.Now) throw new ArgumentOutOfRangeException(nameof(advance.Until));
                    Simulation.CTB.RunUntil(advance.Until); break;
                case PresentationCompleted completed:
                    if (!_presentations.Remove(completed.PresentationId)) throw new InvalidOperationException("Unknown or already acknowledged presentation.");
                    break;
                default: throw new NotSupportedException("Unsupported world input.");
            }
            var result = _outputs.ToArray(); _outputs.Clear(); return result;
        }
    }
    private IEnumerable<BoundCandidate> CurrentCandidates => _candidates.Values.SelectMany(c => c)
        .Where(c => c.Scope == "world" || c.Chapter == CurrentChapter);
    public bool IsCandidateAvailable(string candidateId, string actorReference)
    {
        lock (_gate)
        {
            var actorId=Resolve(actorReference,CurrentChapter??"",_entities.Keys);
            var candidate=CurrentCandidates.FirstOrDefault(c=>c.Id==candidateId && c.Kind=="option" && (c.Actor=="player"||c.Actor==actorId));
            return candidate is not null && Applies(candidate,actorId,out _,out _);
        }
    }
    public void RefreshExistingKnowledge()
    {
        lock (_gate)
        {
            foreach (var (id, registered) in _entities)
            {
                if (registered.Character is not { } actor) continue;
                foreach (var (target, entity) in _entities)
                    if (Runtime.KnownProjection(actor.Soul,entity.Entity) is { } known) _known[id][target]=known;
            }
            InvalidateChoiceTokens();
        }
    }
    private bool Applies(BoundCandidate candidate, string actorId, out Effect? effect, out Condition? condition)
    {
        effect = null; condition = null;
        var actor = Character(actorId);
        if (Simulation.IsActing(actor.Self)) return false;
        try
        {
            if (candidate.Target is not null) Known(actorId, candidate.Target);
            condition = BuildCondition(candidate.Applicability, actorId);
            if (Ast.Evaluate(condition, new(actor.Self, Scene: actor.Self.Parent)) != Truth.True) return false;
            effect = BuildEffect(candidate, actorId);
            return true;
        }
        catch (UnknownPerceptionException) { return false; }
    }
    private void Offer(string actorReference, string? targetReference)
    {
        var actorId = Resolve(actorReference, CurrentChapter ?? "", _entities.Keys);
        Character(actorId);
        var target = targetReference is null ? null : Resolve(targetReference, CurrentChapter ?? "", _entities.Keys);
        InvalidateChoiceTokens();
        var choices = new List<AvailableCandidate>();
        foreach (var candidate in CurrentCandidates.Where(c => c.Kind == "option" && (c.Actor == "player" || c.Actor == actorId) && c.Target == target))
        {
            if (!Applies(candidate, actorId, out _, out _)) continue;
            var token = "choice:" + ++_serial;
            _choiceTokens.Add(token, new(candidate, actorId, _version));
            choices.Add(new(token, candidate.Id, candidate.Label));
        }
        _outputs.Enqueue(new CandidatesAvailable(actorId, choices));
    }
    private void SubmitChoice(string token)
    {
        if (!_choiceTokens.TryGetValue(token, out var choice) || choice.Version != _version)
            throw new InvalidOperationException("Unknown, consumed or expired choice token.");
        if (!Applies(choice.Candidate, choice.Actor, out var effect, out _))
        { InvalidateChoiceTokens(); throw new InvalidOperationException("Candidate is no longer applicable."); }
        var actor = Character(choice.Actor);
        // Applicability is rechecked against cognition now. Core checks physical feasibility when the action activates.
        // A subjective belief must never be passed as an objective ActionIntent.Requirements condition.
        var execution = Simulation.Submit(actor.Soul, new(actor.Self, effect!, choice.Candidate.Delay));
        InvalidateChoiceTokens();
        Track(execution, choice.Actor, choice.Candidate);
    }
    private void Track(ActionExecution execution, string actor, BoundCandidate candidate)
    {
        var id = "execution:" + ++_serial;
        _executions.Add(execution, new(id, actor, candidate));
        _outputs.Enqueue(new CandidateActionStarted(id, actor, candidate.Id));
    }
    private void BehaviorCompleted(ActionExecution execution)
    {
        if (!_executions.Remove(execution, out var active)) return;
        InvalidateChoiceTokens();
        _outputs.Enqueue(new CandidateActionFinished(active.ExecutionId, active.Actor, active.Candidate.Id,
            execution.Status == ExecutionStatus.Completed, execution.Result?.Failure ?? ActionFailure.None));
        if (execution.Status != ExecutionStatus.Completed) return;
        var lines = active.Candidate.Response.Where(s => s.Op == "line")
            .Select(s => new InteractionLine(s.Speaker, s.Mood, s.Text, s.VoiceClip, s.LineId, s.SourceId)).ToArray();
        if (lines.Length > 0)
        {
            var presentation = "presentation:" + ++_serial;
            _presentations.Add(presentation);
            _outputs.Enqueue(new SemanticDialogue(presentation, lines));
        }
        // Finite reactions to this option, once after its completed action; reaction completion does not recurse.
        if (active.Candidate.Kind == "option")
            foreach (var actor in CurrentCandidates.Where(c => c.Kind == "reaction" && c.ResponseTo.Contains(active.Candidate.Id)).Select(c => c.Actor).Distinct())
                React(actor, active.Candidate.Id);
    }
    private (ScriptCharacter Actor, List<(BoundCandidate Candidate, Projection Basis, Effect Effect, Condition Condition)> Candidates,
        IReadOnlyList<DecisionAssessment> Assessments) Assess(string actorId, string? responseTo)
    {
        var actor = Character(actorId);
        var prepared = new List<(BoundCandidate, Projection, Effect, Condition)>();
        foreach (var candidate in CurrentCandidates.Where(c => c.Kind == "reaction" && c.Actor == actorId &&
                     (responseTo is null ? c.ResponseTo.Length == 0 : c.ResponseTo.Contains(responseTo))))
        {
            if (!Applies(candidate, actorId, out var effect, out var condition)) continue;
            Condition goal;
            try { goal = BuildCondition(candidate.Goal, actorId); } catch (UnknownPerceptionException) { goal = new Condition.Constant(Truth.Unknown); }
            var basis = Observation.Observe(actor.Soul, new("candidate:" + candidate.Id, ProjectionKind.Concept)
            {
                Source = ObservationSource.Introspection, Time = Simulation.CTB.Now, Existence = Truth.True,
                // 适用条件成立即成为候选（权重 0）；目标成立再加候选权重。Permit 只放行禁令，不提出候选，这里不用它。
                Rules = [new(condition!, RuleMode.Prefer, effect!, 0, Truth.True), new(new Condition.All([condition!, goal]), RuleMode.Prefer, effect!, candidate.Weight, Truth.True)],
            });
            prepared.Add((candidate, basis, effect!, condition!));
        }
        return (actor, prepared, DecisionSystem.Assess(actor.Soul.World, actor.Self));
    }
    private static void ForgetCandidates(ScriptCharacter actor, IEnumerable<Projection> bases)
    {
        foreach (var basis in bases) Observation.Observe(actor.Soul, new(basis.Name, basis.Kind)
        { Target = basis, Source = ObservationSource.Introspection, Forget = true });
    }
    public IReadOnlyList<CandidateAssessment> AssessReactions(string actorReference, string? responseTo = null)
    {
        lock (_gate)
        {
            var actorId = Resolve(actorReference, CurrentChapter ?? "", _entities.Keys);
            var prepared = Assess(actorId, NormalizeResponseTo(responseTo));
            try
            {
                return prepared.Assessments.Where(a => prepared.Candidates.Any(c => ReferenceEquals(c.Basis, a.Decision.Basis)))
                    .Select(a => new CandidateAssessment(prepared.Candidates.Single(c => ReferenceEquals(c.Basis, a.Decision.Basis)).Candidate.Id,
                        a.Forbidden, a.Decision.Score, a.Reasons)).ToArray();
            }
            finally { ForgetCandidates(prepared.Actor, prepared.Candidates.Select(c => c.Basis)); }
        }
    }
    private string? NormalizeResponseTo(string? id) => id is null || id.Contains(':') ? id : CurrentChapter + ":" + id;
    private void React(string actorReference, string? responseTo)
    {
        var actorId = Resolve(actorReference, CurrentChapter ?? "", _entities.Keys);
        var prepared = Assess(actorId, NormalizeResponseTo(responseTo));
        try
        {
            // Assessment order and scores come exclusively from Core, including accepted prohibitions and preferences.
            var decision = prepared.Assessments.FirstOrDefault(a => !a.Forbidden && prepared.Candidates.Any(c => ReferenceEquals(c.Basis, a.Decision.Basis)));
            if (decision is null) { _outputs.Enqueue(new NoApplicableCandidate(actorId)); return; }
            var selected = prepared.Candidates.Single(c => ReferenceEquals(c.Basis, decision.Decision.Basis));
            if (!Applies(selected.Candidate, actorId, out var effect, out _)) return;
            // Recheck subjective applicability immediately before submitting; objective effect validation remains in Core.
            var execution = Simulation.Submit(prepared.Actor.Soul, new(prepared.Actor.Self, effect!, selected.Candidate.Delay));
            InvalidateChoiceTokens(); Track(execution, actorId, selected.Candidate);
        }
        finally { ForgetCandidates(prepared.Actor, prepared.Candidates.Select(c => c.Basis)); }
    }
    /// <summary>Explicit cognitive input for author simulation; it never writes objective StateProperty.</summary>
    public void RememberState(string actorReference, string entityReference, string tag, int value, ObservationSource source = ObservationSource.Introspection)
    {
        lock (_gate)
        {
            var actorId = Resolve(actorReference, CurrentChapter ?? "", _entities.Keys);
            var projection = Known(actorId, entityReference);
            Observation.Observe(Character(actorId).Soul, new(projection.Name, projection.Kind)
            { Target = projection, Source = source, Time = Simulation.CTB.Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(new(tag), value) });
            InvalidateChoiceTokens();
        }
    }
    private sealed class UnknownPerceptionException : Exception { }
    private Projection Known(string actorId, string reference)
    {
        var id = Resolve(reference, CurrentChapter ?? "", _entities.Keys);
        return _known[actorId].TryGetValue(id, out var known) && Character(actorId).Soul.World.Contains(known)
            ? known : throw new UnknownPerceptionException();
    }
    private static string Text(DialogueExpression expression) => expression.Op == "literal" && expression.Value.ValueKind == JsonValueKind.String
        ? expression.Value.GetString()! : throw new InvalidDataException("A literal string is required.");
    private static int Integer(DialogueExpression expression) => expression.Op == "literal" && expression.Value.ValueKind == JsonValueKind.Number && expression.Value.TryGetInt32(out var value)
        ? value : throw new InvalidDataException("A literal Int32 is required.");
    private Condition BuildCondition(DialogueExpression? expression, string actor)
    {
        if (expression is null) return new Condition.Constant(Truth.True);
        Noun Ref(DialogueExpression e) => Known(actor, Text(e));
        if (expression.Op == "literal" && expression.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return new Condition.Constant(expression.Value.GetBoolean() ? Truth.True : Truth.False);
        if (expression.Op == "not") return new Condition.Not(BuildCondition(expression.Args[0], actor));
        if (expression.Op == "and") return new Condition.All([..expression.Args.Select(e => BuildCondition(e, actor))]);
        if (expression.Op == "or") return new Condition.Any([..expression.Args.Select(e => BuildCondition(e, actor))]);
        if (expression.Op == "call")
        {
            try { return expression.Name switch
            {
                "known" => new Condition.Exists(Ref(expression.Args[0])),
                "at" => new Condition.At(Ref(expression.Args[0]), Ref(expression.Args[1])),
                _ => throw new NotSupportedException("Query is not a Core boolean condition: " + expression.Name),
            }; }
            catch (UnknownPerceptionException) { return new Condition.Constant(Truth.Unknown); }
        }
        if (expression.Op is "eq" or "ne" or "gte" or "gt" or "lt" or "lte")
        {
            var query = expression.Args[0];
            if (query.Op != "call" || query.Name != "state") throw new NotSupportedException("Comparisons currently require subjective state(entity, tag) on the left.");
            var value = Integer(expression.Args[1]);
            var tag = new Tag(Text(query.Args[1]));
            Noun noun;
            try { noun = Ref(query.Args[0]); } catch (UnknownPerceptionException) { return new Condition.Constant(Truth.Unknown); }
            Condition Above() => value == int.MaxValue ? new Condition.Constant(Truth.False) : new Condition.Tagged(noun, tag, value + 1);
            var atLeast = new Condition.Tagged(noun, tag, value);
            var equal = new Condition.All([atLeast, new Condition.Not(Above())]);
            return expression.Op switch { "eq" => equal, "ne" => new Condition.Not(equal), "gte" => atLeast,
                "gt" => Above(), "lt" => new Condition.Not(atLeast), "lte" => new Condition.Not(Above()), _ => throw new NotSupportedException() };
        }
        throw new NotSupportedException("Unsupported subjective expression: " + expression.Op);
    }
    private Effect BuildEffect(BoundCandidate candidate, string actor)
    {
        var effects = candidate.Response.Where(s => s.Op == "call").Select(step =>
        {
            Noun Ref(int index) => Known(actor, Text(step.Args[index]));
            Tag Tag(int index) => new(Text(step.Args[index]));
            return step.Name switch
            {
                "set" => (Effect)new Effect.Set(Ref(0), Tag(1), Integer(step.Args[2])),
                "change" => new Effect.Change(Ref(0), Tag(1), Integer(step.Args[2])),
                "spend" => new Effect.Spend(Ref(0), Tag(1), Integer(step.Args[2])),
                "take" => new Effect.Act(ActionOp.Take, Ref(0)), "inspect" => new Effect.Act(ActionOp.Inspect, Ref(0)),
                "destroy" => new Effect.Act(ActionOp.Destroy, Ref(0)), "travel" => new Effect.Act(ActionOp.Travel, Ref(0)),
                "place" => new Effect.Act(ActionOp.Place, Ref(0), Ref(1)), "unlock" => new Effect.Act(ActionOp.Unlock, Ref(0), Ref(1)),
                "say" => new Effect.Say(Ref(0), Text(step.Args[1])),
                _ => throw new NotSupportedException("Unsupported Core response action: " + step.Name),
            };
        }).ToImmutableArray();
        // A semantic reply is a distinct intention even when it has no physical side effect.
        // Keeping its own mental referent prevents Core from deduplicating different replies as the same effect.
        var key = (actor, candidate.Id);
        if (!_responseMarkers.TryGetValue(key, out var marker))
        {
            marker = Observation.Observe(Character(actor).Soul, new("response:" + candidate.Id, ProjectionKind.Concept)
            { Source = ObservationSource.Introspection, Time = Simulation.CTB.Now, Existence = Truth.True });
            _responseMarkers.Add(key, marker);
        }
        return new Effect.Sequence(effects.Add(new Effect.Think(new Condition.Exists(marker))));
    }
    private List<BoundCandidate> ValidateCandidates(ChapterWorldDefinition chapter, HashSet<string> known, Dictionary<string, string> kinds)
        => ValidateCandidatePlan(chapter, known, kinds.Concat(_entities.Where(p=>!kinds.ContainsKey(p.Key)).Select(p=>new KeyValuePair<string,string>(p.Key,p.Value.Kind))).ToDictionary(p=>p.Key,p=>p.Value),
            _candidates.Values.SelectMany(c=>c).Where(c=>c.Kind=="option").Select(c=>c.Id).ToHashSet(StringComparer.Ordinal));
    private static List<BoundCandidate> ValidateCandidatePlan(ChapterWorldDefinition chapter, HashSet<string> known, Dictionary<string,string> kinds, HashSet<string> inheritedOptions)
    {
        string Reference(string id) => Resolve(id, chapter.Id, known);
        string Kind(string id) => kinds[id];
        void Ref(DialogueExpression e) => Reference(Text(e));
        void Condition(DialogueExpression? e)
        {
            if (e is null) return;
            if (e.Op == "literal" && e.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
            if (e.Op is "and" or "or") { foreach (var a in e.Args) Condition(a); return; }
            if (e.Op == "not" && e.Args.Count == 1) { Condition(e.Args[0]); return; }
            if (e.Op == "call" && e.Name == "known" && e.Args.Count == 1) { Ref(e.Args[0]); return; }
            if (e.Op == "call" && e.Name == "at" && e.Args.Count == 2) { Ref(e.Args[0]); Ref(e.Args[1]); return; }
            if (e.Op is "eq" or "ne" or "gt" or "gte" or "lt" or "lte" && e.Args.Count == 2 && e.Args[0] is { Op: "call", Name: "state" } q && q.Args.Count == 2)
            { Ref(q.Args[0]); ValidateName(Text(q.Args[1]), "state tag"); Integer(e.Args[1]); return; }
            throw new NotSupportedException("Unsupported candidate applicability or goal expression.");
        }
        string CandidateId(string id) => id.StartsWith(chapter.Id + ":", StringComparison.Ordinal) ? id : chapter.Id + ":" + id;
        var ids = chapter.Candidates.Select(c => CandidateId(c.Id)).ToHashSet(StringComparer.Ordinal);
        var optionIds = chapter.Candidates.Where(c => c.Kind == "option").Select(c => CandidateId(c.Id)).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != chapter.Candidates.Count) throw new InvalidDataException("Duplicate candidate id.");
        var result = new List<BoundCandidate>();
        foreach (var candidate in chapter.Candidates)
        {
            ValidateName(candidate.Id, "candidate id");
            var id = CandidateId(candidate.Id);
            if (id[(chapter.Id.Length + 1)..].Contains(':')) throw new InvalidDataException("Candidate ids must be local names or qualified by their declaring chapter.");
            if (candidate.Kind is not ("option" or "reaction") || candidate.Policy.Mode != (candidate.Kind == "option" ? "player" : "core"))
                throw new InvalidDataException("Candidate kind and policy do not agree.");
            if (candidate.Scope is not ("world" or "chapter")) throw new InvalidDataException("Unknown candidate scope.");
            if (!double.IsFinite(candidate.Delay) || candidate.Delay < 0) throw new InvalidDataException("Candidate delay must be finite and nonnegative.");
            var actor = candidate.Actor == "player" && candidate.Kind == "option" ? "player" : Reference(candidate.Actor);
            if (actor != "player" && Kind(actor) != "character") throw new InvalidDataException("Candidate actor must be a character.");
            var target = candidate.Target is null ? null : Reference(candidate.Target);
            Condition(candidate.Applicability); Condition(candidate.Goal);
            var responses = new List<DialogueInstruction>();
            foreach (var step in candidate.Response)
            {
                if (step.Op == "line")
                {
                    if (step.Text.Contains("{{", StringComparison.Ordinal)) throw new NotSupportedException("World candidate dialogue must be literal semantic text.");
                    responses.Add(new() { Op = "line", Speaker = step.Speaker, Mood = step.Mood, Text = step.Text, VoiceClip = step.VoiceClip, LineId = step.LineId, SourceId = step.SourceId });
                    continue;
                }
                if (step.Op != "call") throw new NotSupportedException("Candidates contain only flat action calls and attached lines.");
                var arguments = step.Args.Select(e => new DialogueExpression { Op = e.Op, Value = e.Value.Clone(), Name = e.Name, Args = e.Args }).ToList();
                var count = step.Name switch { "set" or "change" or "spend" => 3, "place" or "unlock" or "say" => 2,
                    "take" or "inspect" or "destroy" or "travel" => 1, _ => throw new NotSupportedException("Unsupported Core response action: " + step.Name) };
                if (arguments.Count != count) throw new InvalidDataException("Wrong action argument count: " + step.Name);
                arguments[0] = Literal(Reference(Text(arguments[0])));
                if (step.Name is "set" or "change" or "spend") { ValidateName(Text(arguments[1]), "state tag"); Integer(arguments[2]); }
                else if (step.Name is "place" or "unlock") arguments[1] = Literal(Reference(Text(arguments[1])));
                else if (step.Name == "say") Text(arguments[1]);
                responses.Add(new() { Op = "call", Name = step.Name, Args = arguments });
            }
            var responseTo = candidate.ResponseTo.Select(id => id.Contains(':') ? id : chapter.Id + ":" + id).ToImmutableArray();
            foreach (var responseId in responseTo)
                if (!optionIds.Contains(responseId) && !inheritedOptions.Contains(responseId))
                    throw new InvalidDataException("Unknown response_to option: " + responseId);
            // Freeze data retained by the session, preventing later caller mutation from changing the validated plan.
            DialogueExpression? Copy(DialogueExpression? e)
            {
                if (e is null) return null;
                var copy = new DialogueExpression { Op = e.Op, Name = e.Name,
                    Value = e.Value.ValueKind == JsonValueKind.Undefined ? default : e.Value.Clone(), Args = e.Args.Select(a => Copy(a)!).ToList() };
                if (copy.Op == "call" && copy.Name is "known" or "state" or "at")
                { copy.Args[0] = Literal(Reference(Text(copy.Args[0]))); if (copy.Name == "at") copy.Args[1] = Literal(Reference(Text(copy.Args[1]))); }
                return copy;
            }
            result.Add(new(chapter.Id, id, candidate.Kind, candidate.Scope, actor, target,
                string.IsNullOrEmpty(candidate.Label) ? candidate.Id : candidate.Label, Copy(candidate.Applicability), Copy(candidate.Goal),
                responses.ToImmutableArray(), responseTo, candidate.Policy.Weight, candidate.Delay));
        }
        return result;
    }
    private static DialogueExpression Literal(string value) => new() { Value = JsonSerializer.SerializeToElement(value) };
}
