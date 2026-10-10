using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using static DualOntologyWorldEngine.Story.ChapterEvents;

namespace DualOntologyWorldEngine.Story;

public sealed class ActorControllerDefinition
{
    public string Actor { get; set; } = "";
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "";
    public List<ControllerRuleDefinition> Rules { get; set; } = [];
    public ControllerReasoningDefinition Reasoning { get; set; } = new();
}
public sealed class ControllerReasoningDefinition
{
    public int Depth { get; set; } = 1;
    public int Expansions { get; set; } = 256;
    public int Hypotheses { get; set; } = 16;
    public int ArgumentWeight { get; set; } = 1;
    public int InformationWeight { get; set; } = 1;
}
public sealed class ControllerRuleDefinition
{
    public string Mode { get; set; } = "";
    public string Kind { get; set; } = "";
    public int Weight { get; set; }
    public string LabelContains { get; set; } = "";
}
public sealed record PlayerTime(int Day, double Minutes, string Phase);
public sealed record PlayerCognition(CognitionSnapshotV1 Snapshot, SubjectProjectionId Actor);

// Implemented by trusted chapter adapters. Never handed to a controller.
public interface IPlayerRoleSession
{
    string PlayerActor { get; }
    PlayerCognition CapturePlayerCognition(string actor);
    PlayerTime PlayerTime { get; }
    void ObservePlayerOffers(IReadOnlyList<PlayerFieldOffer> offers) { }
    void ObservePlayerFormField(IReadOnlyDictionary<string, object?> field) { }
}

public sealed record PublicPlayerLine(string Speaker, string Mood, string Text);
public sealed record PlayerPoint(double X, double Y);
public sealed record PublicPlayerCandidate(string Token, string Label, string Kind, bool New, string Group = "", string Value = "", string Purpose = "", bool Selected = false)
{
    public PlayerPoint? Location { get; init; }
}
public sealed record PublicPlayerObservation(long Turn, ImmutableArray<PublicPlayerLine> Lines,
    ImmutableArray<string> Text, ImmutableArray<PublicPlayerCandidate> Candidates, bool Complete);
public sealed record PlayerDecision(string? Token, string Reason, int? Score)
{
    public ImmutableArray<string> Basis { get; init; } = [];
    public int ReasoningExpansions { get; init; }
    public string ReasoningStop { get; init; } = "not_run";
    public int HypothesesCompared { get; init; }
    public ImmutableArray<PlayerArgument> Arguments { get; init; } = [];
}
public sealed record PlayerArgument(string Proposition, string Value, bool Complete, ImmutableArray<string> Materials, ImmutableArray<string> Steps)
{
    public ImmutableArray<PlayerInference> Derivations { get; init; } = [];
    public ImmutableArray<string> Missing { get; init; } = [];
    public bool? MaterialSupports { get; init; }
}
public sealed record PlayerInference(string Conclusion, ImmutableArray<string> Premises, string Material);
public sealed class PlayerBoundaryException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}
public interface IPlayerCandidatePort
{
    PublicPlayerObservation Submit(SelectCandidate selection);
}

// Host-generated offers must already pass the real field's visibility/navigation gates.
// Their payload is retained by the trusted port, never exposed to the controller.
public sealed record PlayerFieldOffer(string Label, string Kind, bool New,
    IReadOnlyDictionary<string, object?> Input, string Group = "", string Value = "", string Purpose = "", bool Selected = false)
{
    public PlayerPoint? Location { get; init; }
}

// Trusted projection of exactly the shared panels' presentation contracts.
// IDs, numeric option indices and maps remain in private submission payloads.
public sealed record PlayerPanelView(IReadOnlyList<string> Text, IReadOnlyList<PlayerFieldOffer> Offers);
public static class PlayerPanels
{
    private static IEnumerable<IReadOnlyDictionary<string, object?>> Entries(IReadOnlyDictionary<string, object?> data, string key)
        => (data.GetValueOrDefault(key) as IEnumerable<object?> ?? []).OfType<IReadOnlyDictionary<string, object?>>();
    public static PlayerPanelView Project(IReadOnlyDictionary<string, object?> presentation, string panel, string runLabel)
    {
        var text = new List<string>(); var offers = new List<PlayerFieldOffer>();
        void Line(string value) { if (value != "") text.Add(value); }
        void Card(IReadOnlyDictionary<string, object?> entry)
        {
            if (Text(entry, "card_id") is { Length: > 0 } id)
                offers.Add(new(Text(entry, "label", Text(entry, "title")), "interact", false, Event("card", ("card_id", id))));
        }
        if (panel == "notebook" && Flag(Map(presentation, "controls"), "notebook"))
        {
            var data = Map(presentation, "notebook"); var sections = Entries(data, "sections").ToArray();
            if (sections.Length == 0) Line(Text(data, "empty_text"));
            foreach (var section in sections)
            {
                Line(Text(section, "heading"));
                foreach (var entry in Entries(section, "entries")) { Line(Text(entry, "label")); Card(entry); }
            }
        }
        else if (panel == "campus" && Flag(Map(presentation, "controls"), "campus"))
        {
            var data = Map(presentation, "campus"); Line(Text(data, "title"));
            var known = Map(data, "known");
            foreach (var (id, value) in Map(data, "nodes"))
                if (Flag(known, id) && value is IReadOnlyDictionary<string, object?> node)
                    offers.Add(new(Text(node, "label"), "move", false, Event("travel", ("node_id", id))));
        }
        else if (panel == "manuscript" && Flag(Map(presentation, "controls"), "manuscript"))
        {
            var data = Map(presentation, "replay");
            if (!Flag(data, "open")) throw new PlayerBoundaryException("panel_unavailable");
            Line(Text(data, "title")); Line(Text(data, "hint"));
            var questions = Entries(data, "questions").ToArray();
            var current = (data.GetValueOrDefault("vars") as IEnumerable<object?> ?? []).ToArray();
            var values = questions.Select((q, i) => i < current.Length && int.TryParse(current[i]?.ToString(), out var v)
                && v >= 0 && v < (q.GetValueOrDefault("options") as IEnumerable<object?> ?? []).Count() ? v : -1).ToArray();
            for (var i = 0; i < questions.Length; i++)
            {
                var question = questions[i]; Line(Text(question, "question") + Text(question, "mark"));
                var options = (question.GetValueOrDefault("options") as IEnumerable<object?> ?? []).OfType<string>().ToArray();
                for (var j = 0; j < options.Length; j++)
                {
                    var next = values.Cast<object?>().ToArray(); next[i] = j;
                    offers.Add(new(Text(question, "question") + " " + options[j], "form_field", values[i] == -1,
                        Event("replay_vars", ("vars", next)), Text(question, "question"), options[j], "hypothesis", values[i] == j));
                }
            }
            if (values.Length > 0 && values.All(v => v >= 0))
                offers.Add(new(runLabel, "form_submit", false, Event("replay", ("vars", values.Cast<object?>().ToArray()), ("skip", false)), Purpose: "commit"));
            foreach (var finding in Entries(Map(data, "result"), "findings"))
            {
                Line(Text(finding, "title")); Line(Text(finding, "status"));
                if (Text(finding, "status") != "match") Line(Text(finding, "text"));
                if (Text(finding, "status") != "unvisited") Card(finding);
            }
        }
        else throw new PlayerBoundaryException("panel_unavailable");
        offers.Add(new("Esc", "continue", false, Event("player_panel", ("panel", ""))));
        return new(text, offers);
    }
}

public static class PlayerControllers
{
    public static IReadOnlyList<ActorControllerDefinition> Read(string json)
    {
        var chapters = WorldScriptDocument.Parse(json, allowChapterAdapter: true);
        var controls = chapters.SelectMany(c => c.Controllers).ToArray();
        var actors = chapters.SelectMany(c => c.Entities.Where(e => e.Kind == "character")
            .Select(e => e.Id.Contains(':') ? e.Id : (e.Scope == "world" ? "world" : c.Id) + ":" + e.Id)).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var control in controls)
        {
            if (!actors.Contains(control.Actor) || !seen.Add(control.Actor) || string.IsNullOrWhiteSpace(control.Name))
                throw new InvalidDataException("Invalid or duplicate controller actor.");
            Validate(control);
        }
        return controls;
    }
    public static void Validate(ActorControllerDefinition control)
    {
        if (control.Mode is not ("agent" or "player") || control.Rules.Count > 64
            || control.Mode == "agent" && control.Rules.Count == 0)
            throw new InvalidDataException("Invalid controller mode or rule count.");
        foreach (var rule in control.Rules)
            if (rule.Mode is not ("prefer" or "require" or "forbid" or "permit")
                || rule.Kind is not ("menu" or "form_field" or "form_submit" or "continue" or "interact" or "move" or "wait" or "untried" or "new")
                || rule.Weight is < 0 or > 1000 || rule.LabelContains is null || rule.LabelContains.Length > 200)
                throw new InvalidDataException("Unsupported subjective controller rule.");
        var reasoning = control.Reasoning;
        if (reasoning is null || reasoning.Depth is < 0 or > 64 || reasoning.Expansions is < 1 or > 100000
            || reasoning.Hypotheses is < 1 or > 4096 || reasoning.ArgumentWeight is < 0 or > 1000 || reasoning.InformationWeight is < 0 or > 1000)
            throw new InvalidDataException("Unsupported subjective reasoning budget.");
    }
}

/// <summary>Pure controller: only public offers and this actor's immutable cognition.
/// Assess a detached augmentation; never mutate the actor, simulation or authoritative world.</summary>
public sealed class SubjectivePlayerController
{
    private readonly ImmutableArray<(RuleMode Mode, string Kind, int Weight, string Label)> _rules;
    // Versioned xorshift32 tie stream; independent of wall time and System.Random.
    private uint _tieState;
    private readonly HashSet<(string Kind, string Label)> _tried = [];
    private readonly Dictionary<(string Kind, string Label), int> _visits = [];
    private readonly Dictionary<string, PublicPlayerCandidate> _seenPlaces = new(StringComparer.Ordinal);
    private readonly int _revisitWeight;
    private string _knowledgeEpoch = "";
    private readonly ReasoningBudgetV1 _reasoning;
    private readonly int _argumentWeight, _informationWeight;
    private FrozenConditionV1? _argumentGoal;
    private string _argumentLabel = "";
    private ImmutableArray<PlayerArgument> _lastArgument = [];
    private bool _supportedEvidence;
    private bool _evidenceRequired;
    public SubjectivePlayerController(ActorControllerDefinition declaration, int seed)
    {
        PlayerControllers.Validate(declaration);
        _rules = [..declaration.Rules.Select(r => (Enum.Parse<RuleMode>(r.Mode, true), r.Kind, r.Weight, r.LabelContains))];
        _revisitWeight = _rules.Where(r => r.Mode == RuleMode.Prefer && r.Kind == "untried").Select(r => r.Weight).DefaultIfEmpty(0).Max();
        var r = declaration.Reasoning;
        _reasoning = new(r.Depth, r.Expansions, r.Hypotheses);
        (_argumentWeight, _informationWeight) = (r.ArgumentWeight, r.InformationWeight);
        _tieState = unchecked((uint)seed);
        if (_tieState == 0) _tieState = 0x9E3779B9;
    }
    private int TieIndex(int count)
    {
        _tieState ^= _tieState << 13;
        _tieState ^= _tieState >> 17;
        _tieState ^= _tieState << 5;
        return (int)(_tieState % (uint)count);
    }
    private static (string Kind, string Label) HistoryKey(PublicPlayerCandidate candidate)
        => candidate.Kind == "move" && candidate.Location is { } point
            ? (candidate.Kind, $"{Math.Floor(point.X / 64)}:{Math.Floor(point.Y / 64)}")
            : (candidate.Kind, candidate.Label);
    public PlayerDecision Decide(PublicPlayerObservation observation, PlayerCognition cognition)
    {
        if (observation.Candidates.Length == 0) return new(null, "candidate_missing", null);
        var snapshot = cognition.Snapshot;
        if (!snapshot.Projections.Any(p => p.Id == cognition.Actor)) return new(null, "actor_cognition_missing", null);
        // Reconsider old inputs after learning new accepted material or hearing a
        // new question. Movement and ordinary observation revisions do not reset visits.
        var epoch = string.Join(";", snapshot.Projections.Where(p => p.KnownTerms.Any(t => t.TermId.Value.StartsWith("acquired.", StringComparison.Ordinal))
            && (p.Rules.Any(r => r.Mode == RuleMode.Infer && r.Acceptance == Truth.True)
                || p.Tags.GetValueOrDefault(AcquiredPropositions.Question) == 1))
            .Select(p => p.Id.ToString() + ":" + p.Rules.Count(r => r.Acceptance == Truth.True)).Order());
        if (epoch != _knowledgeEpoch) { _visits.Clear(); _knowledgeEpoch = epoch; }
        var reasoning = new SubjectiveReasoner(snapshot, cognition.Actor, _reasoning);
        if (observation.Candidates.Any(c => c.Purpose == "evidence")) _evidenceRequired = true;
        if (_argumentGoal is not null)
            try { reasoning.References(_argumentGoal); }
            catch (CognitionContractException) { _argumentGoal = null; }
        // Only acquired lexical material can bind a visible phrase to a subjective claim.
        // No chapter names, answer tables, private payloads or objective lookup.
        FrozenProjectionV1[] Materials(PublicPlayerCandidate c)
        {
            var exact = snapshot.Projections.Where(p => p.Name.Length > 0 && (c.Value == p.Name || c.Label == p.Name)
                || p.KnownTerms.Any(t => t.CanonicalLexeme.Length > 0 && (c.Value == t.CanonicalLexeme || c.Label == t.CanonicalLexeme))).ToArray();
            if (exact.Length > 0) return exact;
            // An observed entrance is a particular offered input. A person's
            // name inside another menu label does not promise the same material.
            return snapshot.Projections.Where(p => p.Tags.GetValueOrDefault(AcquiredPropositions.Opportunity) != 1
                && p.KnownTerms.Any(t => t.CanonicalLexeme.Length > 0
                && c.Label.Contains(t.CanonicalLexeme, StringComparison.Ordinal))).ToArray();
        }
        var materials = observation.Candidates.ToDictionary(c => c.Token, Materials);
        foreach (var place in observation.Candidates.Where(c => c.Kind == "interact" && c.Location is not null))
            _seenPlaces[place.Label] = place;
        var currentQuestion = snapshot.Projections.LastOrDefault(p => p.Tags.GetValueOrDefault(AcquiredPropositions.Question) == 1);
        var asked = currentQuestion is null ? null : reasoning.Claims(currentQuestion.Id).FirstOrDefault();
        var askedProof = asked is null ? null : reasoning.Prove(asked);
        bool ReadyOpportunity(PublicPlayerCandidate candidate, IEnumerable<FrozenProjectionV1> held) => asked is not null
            && askedProof is { Value: Truth.True, Complete: true, Hypothetical: false, Contradicted: false }
            && held.Where(p => p.Tags.GetValueOrDefault(AcquiredPropositions.Opportunity) == 1
                && (p.Name == candidate.Label || p.Name == candidate.Value))
                .SelectMany(p => reasoning.Claims(p.Id)).Any(goal => Ast.Equivalent(asked, goal) is ContractResult<bool>.Success { Value: true });
        var readyPlaces = _seenPlaces.Values.Where(p => ReadyOpportunity(p, Materials(p))).ToArray();
        bool NeededOpportunity(IEnumerable<FrozenProjectionV1> held) => askedProof is { Value: Truth.Unknown }
            && held.Where(p => p.Tags.GetValueOrDefault(AcquiredPropositions.Opportunity) == 1)
                .SelectMany(p => reasoning.Claims(p.Id)).Any(claim => askedProof.Missing.Any(gap =>
                    Ast.Equivalent(gap, claim) is ContractResult<bool>.Success { Value: true }));
        var wantedPlaces = readyPlaces.Length > 0 ? readyPlaces
            : _seenPlaces.Values.Where(p => NeededOpportunity(Materials(p))).ToArray();
        var moves = observation.Candidates.Where(c => c.Kind == "move" && c.Location is not null).ToArray();
        double Distance(PlayerPoint a, PlayerPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        double GoalDistance(PublicPlayerCandidate c) => wantedPlaces.Min(p => Distance(c.Location!, p.Location!));
        var farthestMove = wantedPlaces.Length == 0 || moves.Length == 0 ? 0 : moves.Max(GoalDistance);
        var groups = observation.Candidates.Where(c => c.Kind == "form_field" && c.Group != "").GroupBy(c => c.Group).ToArray();
        var hypotheses = new List<SubjectiveHypothesisV1>();
        void Combinations(int index, ImmutableArray<FrozenConditionV1> assumptions, string label)
        {
            if (hypotheses.Count > _reasoning.Hypotheses) return;
            if (index == groups.Length) { if (assumptions.Length > 0) hypotheses.Add(new(label, assumptions)); return; }
            foreach (var option in groups[index])
            {
                var claims = materials[option.Token].SelectMany(p => reasoning.Claims(p.Id)).ToArray();
                if (claims.Length == 1) Combinations(index + 1, assumptions.Add(claims[0]), label + option.Group + "=" + option.Value + ";");
            }
        }
        Combinations(0, [], "");
        var inquiries = asked is null ? reasoning.KnownQueries.Take(16).ToImmutableArray() : ImmutableArray.Create(asked);
        var exploration = reasoning.Explore(inquiries, []);
        var reasoningScores = new Dictionary<string, (int Score, ImmutableArray<string> Basis)>();
        var arguments = new Dictionary<string, ImmutableArray<PlayerArgument>>();
        string MaterialName(SubjectProjectionId id)
        {
            var material = snapshot.Projections.First(p => p.Id == id);
            if (material.KnownTerms.Any(t => t.TermId.Value.StartsWith("acquired.laws.", StringComparison.Ordinal))) return "acquired_rule";
            return material.Name.Length > 0 ? material.Name : material.KnownTerms.FirstOrDefault()?.RawLexeme ?? "acquired_material";
        }
        string PropositionName(FrozenConditionV1 claim) => claim switch
        {
            FrozenConditionV1.Not negated => "!" + PropositionName(negated.Operand),
            FrozenConditionV1.All all => string.Join(" & ", all.Items.Select(PropositionName)),
            FrozenConditionV1.Any any => string.Join(" | ", any.Items.Select(PropositionName)),
            _ => string.Join(" / ", reasoning.References(claim).Select(MaterialName)) is { Length: > 0 } label ? label : "acquired_proposition"
        };
        PlayerArgument Trace(string label, SubjectiveProofV1 proof, bool? participates = null) => new(label,
            proof.Value.ToString(), proof.Complete, [..proof.Basis.Select(MaterialName).Distinct()], proof.Steps)
        { Derivations = [..proof.Derivations.Select(d => new PlayerInference(PropositionName(d.Conclusion),
            [..d.Premises.Select(PropositionName)], MaterialName(d.Source)))],
            Missing = [..proof.Missing.Select(PropositionName).Distinct()], MaterialSupports = participates };
        foreach (var candidate in observation.Candidates)
        {
            var held = materials[candidate.Token]; var ids = held.Select(p => p.Id).ToHashSet();
            var why = new List<string>(); long score = 0;
            var chains = new List<PlayerArgument>();
            var visits = _visits.GetValueOrDefault(HistoryKey(candidate));
            // A mandatory acknowledgement has no alternative to explore.
            if (visits > 0 && _revisitWeight > 0 && observation.Candidates.Length > 1)
            {
                score -= (long)Math.Min(visits, 1000) * _revisitWeight;
                why.Add("revisit_without_new_accepted_material:" + visits);
            }
            if (candidate.Selected) score -= _argumentWeight * 4L;
            if (candidate.Purpose == "tab" && !candidate.Selected && _argumentGoal is not null) score += _argumentWeight * 2L;
            if (candidate.Purpose == "commit" && _argumentGoal is not null) score += _argumentWeight * (_supportedEvidence ? 3L : -8L);
            if (candidate.Purpose == "cancel") score -= _argumentWeight;
            if (ReadyOpportunity(candidate, held) || candidate.Purpose == "argument"
                && askedProof is { Value: Truth.True, Complete: true, Hypothetical: false, Contradicted: false })
            {
                score += _argumentWeight * 3L;
                why.Add("return_with_personal_answer:" + candidate.Label);
                chains.Add(Trace(PropositionName(asked!), askedProof!));
            }
            if (candidate.Kind == "move" && candidate.Location is not null && wantedPlaces.Length > 0)
            {
                // Soft directional preference toward a place actually seen before.
                // The real field still resolves collisions; repeat penalties permit detours.
                var weight = readyPlaces.Length > 0 ? _argumentWeight : _informationWeight;
                score += (long)Math.Clamp((farthestMove - GoalDistance(candidate)) / 64 * weight, 0, weight * 2);
                why.Add(readyPlaces.Length > 0 ? "approach_observed_answer_opportunity" : "approach_observed_question_material");
            }
            foreach (var inquiry in exploration.Inquiries.Where(q => q.Targets.Any(ids.Contains)))
            {
                score += _informationWeight * (1 + Math.Min(inquiry.DiscriminatingPairs, 1000));
                why.Add("seek_unknown_acquired_proposition:" + candidate.Label);
            }
            foreach (var opportunity in held.Where(p => p.Tags.GetValueOrDefault(AcquiredPropositions.Opportunity) == 1))
                foreach (var missing in reasoning.Claims(opportunity.Id))
                {
                    var proof = reasoning.Prove(missing);
                    if (proof.Value == Truth.Unknown && !proof.Contradicted && visits == 0)
                    {
                        var relevant = askedProof?.Missing.Any(gap => Ast.Equivalent(gap, missing) is ContractResult<bool>.Success { Value: true }) == true;
                        score += _informationWeight * (relevant ? 3L : 1L);
                        why.Add((relevant ? "seek_current_question_material:" : "seek_unknown_acquired_proposition:") + candidate.Label);
                        chains.Add(Trace(PropositionName(missing), proof));
                    }
                }
            var goals = candidate.Purpose == "evidence" && _argumentGoal is null ? Enumerable.Empty<FrozenConditionV1>()
                : _argumentGoal is null || candidate.Purpose is "statement" or "hypothesis" ? held.Where(p => p.Kind == ProjectionKind.Concept).SelectMany(p => reasoning.Claims(p.Id))
                : new[] { _argumentGoal }.AsEnumerable();
            if (candidate.Kind == "form_field")
                foreach (var goal in goals)
                {
                    var proof = candidate.Purpose is "statement" or "hypothesis" or "evidence"
                        ? reasoning.Prove(goal)
                        : reasoning.Prove(goal, [..ids.Concat(reasoning.StandingKnowledge).Distinct()]);
                    var participates = candidate.Purpose != "evidence" || proof.Basis.Any(ids.Contains)
                        && held.Any(p => p.Tags.GetValueOrDefault(AcquiredPropositions.Presentable) == 1);
                    chains.Add(Trace(candidate.Purpose == "evidence" ? _argumentLabel : candidate.Value == "" ? candidate.Label : candidate.Value,
                        proof, candidate.Purpose == "evidence" ? participates : null));
                    if (!proof.Complete || proof.Hypothetical) continue;
                    if (proof.Contradicted || proof.Value == Truth.False) score -= _argumentWeight;
                    else if (proof.Value == Truth.True && participates)
                    {
                        score += _argumentWeight;
                        if (asked is not null && Ast.Equivalent(asked, goal) is ContractResult<bool>.Success { Value: true }) score += _argumentWeight * 2L;
                        why.Add("subjective_argument_supported:" + candidate.Label);
                        why.AddRange(proof.Basis.Select(MaterialName));
                        why.AddRange(proof.Steps);
                    }
                }
            reasoningScores[candidate.Token] = ((int)Math.Clamp(score, -100000000L, 100000000L), [..why.Distinct()]);
            arguments[candidate.Token] = [..chains];
        }
        if (hypotheses.Count > 0)
            exploration = reasoning.Explore(asked is null ? [..reasoning.KnownQueries.Take(4)] : inquiries, [..hypotheses]);
        var nodes = snapshot.Projections.ToBuilder();
        var effects = new List<(FrozenEffectV1 Effect, PublicPlayerCandidate Candidate)>();
        foreach (var candidate in observation.Candidates)
        {
            var id = new SubjectProjectionId(snapshot.SubjectWorld, Guid.NewGuid());
            var noun = new FrozenNounV1.Reference(id);
            // Deliberation about an offered input. This effect is never executed in Core;
            // its only executable meaning is the current opaque token at the port.
            var effect = new FrozenEffectV1.Think(new FrozenConditionV1.Exists(noun));
            var tags = ImmutableDictionary<Tag, int>.Empty.Add(new(candidate.Kind), 1)
                .Add(new("new"), candidate.New ? 1 : 0)
                .Add(new("untried"), _tried.Contains(HistoryKey(candidate)) ? 0 : 1);
            var rules = ImmutableArray.CreateBuilder<FrozenRuleV1>();
            rules.Add(new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Prefer, effect, 0, Truth.True));
            foreach (var rule in _rules)
                if (rule.Label == "" || candidate.Label.Contains(rule.Label, StringComparison.Ordinal))
                    rules.Add(new(new FrozenConditionV1.Tagged(noun, new(rule.Kind), 1), rule.Mode, effect, rule.Weight, Truth.True));
            nodes.Add(new(id, candidate.Label, ProjectionKind.Concept, Truth.True, null, tags, [], [], rules.ToImmutable(), null, [], null));
            effects.Add((effect, candidate));
        }
        var result = DecisionSystem.Assess(snapshot with { Projections = nodes.ToImmutable() }, cognition.Actor);
        if (result is ContractResult<ImmutableArray<SnapshotDecisionAssessmentV1>>.Failure failure)
            return new(null, "cognition_unsupported:" + failure.Error.Code, null);
        var assessments = ((ContractResult<ImmutableArray<SnapshotDecisionAssessmentV1>>.Success)result).Value;
        // Argument submission requires a belief, not merely an offered claim.
        // Experiments/hypotheses and ordinary text forms do not set an argument goal.
        var argumentProof = _argumentGoal is null ? null : reasoning.Prove(_argumentGoal);
        bool CanSubmitArgument() => _argumentGoal is null || argumentProof is { Value: Truth.True, Complete: true, Hypothetical: false, Contradicted: false }
            && (!_evidenceRequired || _supportedEvidence);
        bool AnswersCurrentQuestion(PublicPlayerCandidate candidate) => candidate.Purpose != "statement" || asked is null
            || arguments[candidate.Token].Any(a => a.Value == "True" && a.Complete)
                && materials[candidate.Token].SelectMany(p => reasoning.Claims(p.Id)).Any(goal =>
                    Ast.Equivalent(asked, goal) is ContractResult<bool>.Success { Value: true });
        bool SupportsSelectedArgument(PublicPlayerCandidate candidate) => candidate.Purpose != "evidence" || _argumentGoal is null
            || arguments[candidate.Token].Any(a => a.Value == "True" && a.Complete && a.MaterialSupports == true);
        var available = effects.Select(pair => (pair.Candidate, Assessment: assessments.FirstOrDefault(a => a.Effect == pair.Effect)))
            .Where(pair => pair.Assessment is { Forbidden: false } && AnswersCurrentQuestion(pair.Candidate) && SupportsSelectedArgument(pair.Candidate)
                && (pair.Candidate.Purpose != "commit" || CanSubmitArgument())).ToArray();
        if (available.Length == 0) return new(null, "agent_cannot_assess", null);
        var best = available.Max(p => p.Assessment!.Score + reasoningScores[p.Candidate.Token].Score);
        var tied = available.Where(p => p.Assessment!.Score + reasoningScores[p.Candidate.Token].Score == best).ToArray();
        var chosen = tied[TieIndex(tied.Length)].Candidate;
        var matchedRules = _rules.Where(rule => (rule.Label == "" || chosen.Label.Contains(rule.Label, StringComparison.Ordinal)) && (rule.Kind == chosen.Kind
            || rule.Kind == "new" && chosen.New
            || rule.Kind == "untried" && !_tried.Contains(HistoryKey(chosen))))
            .Select(rule => "authored_rule:" + rule.Mode.ToString().ToLowerInvariant() + ":" + rule.Kind + ":" + rule.Weight + (rule.Label == "" ? "" : ":public_label=" + rule.Label));
        // Materialize before updating choice history: the untried rule describes
        // the cognition used for this choice, not the state after it.
        var publicBasis = reasoningScores[chosen.Token].Basis.AddRange(matchedRules).Add("authored_subjective_preferences");
        _tried.Add(HistoryKey(chosen));
        _visits[HistoryKey(chosen)] = _visits.GetValueOrDefault(HistoryKey(chosen)) + 1;
        var chosenArguments = arguments[chosen.Token];
        if (chosen.Purpose == "evidence")
        {
            _supportedEvidence = reasoningScores[chosen.Token].Basis.Any(b => b.StartsWith("subjective_argument_supported:", StringComparison.Ordinal));
            _lastArgument = chosenArguments;
        }
        if (chosen.Kind == "form_field" && chosen.Purpose is "statement" or "")
        {
            var selectedClaims = materials[chosen.Token].Where(p => p.Kind == ProjectionKind.Concept)
                .SelectMany(p => reasoning.Claims(p.Id)).Distinct().ToArray();
            if (selectedClaims.Length == 1 && chosen.Purpose != "evidence") { _argumentGoal = selectedClaims[0]; _argumentLabel = chosen.Value == "" ? chosen.Label : chosen.Value; _supportedEvidence = false; _lastArgument = chosenArguments; }
            else if (selectedClaims.Length > 1) _argumentGoal = null;
        }
        // A page switch is a menu input inside the same visible form. Retain the
        // statement until submission or leaving the form; editing it replaces it.
        if (chosen.Purpose == "commit" && chosenArguments.IsEmpty) chosenArguments = _lastArgument;
        if (chosenArguments.IsEmpty && askedProof is not null) chosenArguments = [Trace(PropositionName(asked!), askedProof)];
        if (chosen.Kind == "form_submit" || !observation.Candidates.Any(c => c.Kind == "form_field"))
        { _argumentGoal = null; _supportedEvidence = false; _evidenceRequired = false; _lastArgument = []; _argumentLabel = ""; }
        return new(chosen.Token, "subjective_rules", best)
        {
            Basis = publicBasis,
            ReasoningExpansions = reasoning.Expansions,
            ReasoningStop = reasoning.KnownQueries.Length == 0 ? "no_acquired_propositional_model" : reasoning.StopReason,
            HypothesesCompared = exploration.Predictions.Length,
            Arguments = chosenArguments
        };
    }
}

/// <summary>Trusted token translation into the unchanged player Dispatch entry.
/// All source IDs, raw state, presentation IDs and command payloads stay here.</summary>
public sealed class PlayerCandidateSession : IPlayerCandidatePort
{
    private sealed record Offer(PublicPlayerCandidate Visible, Dictionary<string, object?> Input, string? Field = null, object? Value = null, bool Tab = false);
    private readonly Func<IReadOnlyDictionary<string, object?>, IReadOnlyList<Dictionary<string, object?>>> _dispatch;
    private readonly string _selfName;
    private readonly int _maxSelections, _maxObservations;
    private readonly Action<IReadOnlyDictionary<string, object?>>? _observeField;
    private readonly string _session = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, Offer> _offers = new(StringComparer.Ordinal);
    private Dictionary<string, object?>? _pending;
    private string _activeField = "";
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private long _serial, _turn;
    private bool _closed;
    public int Selections { get; private set; }
    public PublicPlayerObservation Current { get; private set; } = new(0, [], [], [], false);
    public IReadOnlyList<Dictionary<string, object?>> LastOutput { get; private set; } = [];
    public IReadOnlyDictionary<string, object?>? LastInput { get; private set; }
    public PlayerCandidateSession(Func<IReadOnlyDictionary<string, object?>, IReadOnlyList<Dictionary<string, object?>>> dispatch,
        string selfName, int maxSelections = 10000, int maxObservations = 20000,
        Action<IReadOnlyDictionary<string, object?>>? observeField = null)
    {
        if (maxSelections < 0 || maxObservations < 1) throw new ArgumentOutOfRangeException(nameof(maxSelections));
        (_dispatch, _selfName, _maxSelections, _maxObservations) = (dispatch, selfName, maxSelections, maxObservations);
        _observeField = observeField;
    }
    private void Open() { if (_closed) throw new PlayerBoundaryException("boundary_closed"); }
    private PlayerBoundaryException Fail(string code) { _closed = true; _offers.Clear(); return new(code); }
    public void Close() { _closed = true; _offers.Clear(); }
    private static IEnumerable<IReadOnlyDictionary<string, object?>> Maps(IReadOnlyDictionary<string, object?> map, string key)
        => (map.GetValueOrDefault(key) as System.Collections.IEnumerable ?? Array.Empty<object>()).Cast<object?>().OfType<IReadOnlyDictionary<string, object?>>();
    private void Add(string label, string kind, Dictionary<string, object?> input, bool isNew = false, string? field = null, object? value = null, bool tab = false, string group = "", string publicValue = "", string purpose = "", bool selected = false, PlayerPoint? location = null)
    {
        if (_offers.Count >= 256) throw Fail("candidate_limit");
        var token = "player:" + _session + ":" + ++_serial;
        _offers.Add(token, new(new(token, label, kind, isNew, group, publicValue, purpose, selected) { Location = location }, input, field, value, tab));
    }
    public PublicPlayerObservation Observe(IReadOnlyList<Dictionary<string, object?>> output,
        IReadOnlyList<PlayerFieldOffer>? fieldOffers = null)
    {
        Open();
        if (_turn >= _maxObservations) throw Fail("observation_budget");
        _offers.Clear(); LastOutput = output;
        var lines = ImmutableArray.CreateBuilder<PublicPlayerLine>();
        var text = ImmutableArray.CreateBuilder<string>();
        var complete = false;
        foreach (var ev in output)
        {
            var kind = Text(ev, "kind");
            if (kind == "lines")
                foreach (var line in Maps(ev, "lines")) lines.Add(new(Text(line, "speaker_name", Text(line, "speaker")), Text(line, "mood"), Text(line, "text")));
            if (kind is "lines" or "card" or "candidates" or "form" or "ending")
            {
                foreach (var key in new[] { "title", "prompt", "intro", "facts", "source", "text", "outcome" })
                    if (Text(ev, key) is { Length: > 0 } value) text.Add(value);
                foreach (var key in new[] { "notes", "rows", "items" })
                    if (ev.GetValueOrDefault(key) is IEnumerable<object?> entries)
                        text.AddRange(entries.OfType<string>());
                if (ev.GetValueOrDefault("table") is IEnumerable<object?> table)
                    foreach (var row in table.OfType<IEnumerable<object?>>())
                        text.Add(string.Join("\t", row.OfType<string>()));
                var header = Map(ev, "header");
                foreach (var key in new[] { "question", "instructions" })
                    if (Text(header, key) is { Length: > 0 } value) text.Add(value);
                foreach (var option in Maps(ev, kind == "lines" ? "choices" : "options").Where(o => !Flag(o, "available", true)))
                    text.Add(Text(option, "label") + " " + Text(option, "lock_hint"));
                _pending = ev; _values.Clear();
                _activeField = Maps(ev, "fields").Select(f => Text(f, "id")).FirstOrDefault() ?? "";
                if (kind == "ending") complete = !Maps(ev, "actions").Any(a => Text(a, "id") is "continue" or "chapter2");
            }
            if (kind is "aside" or "error") text.Add(Text(ev, kind == "error" ? "message" : "text"));
            if (kind == "player_panel_view" && ev.GetValueOrDefault("rows") is IEnumerable<string> panelText) text.AddRange(panelText);
            if (kind == "replay_play")
                foreach (var key in new[] { "title", "caption" })
                    if (Text(ev, key) is { Length: > 0 } value) text.Add(value);
            if (kind == "error") { _closed = true; _pending = null; }
            if (kind == "close") { _pending = null; _values.Clear(); _activeField = ""; }
            if (kind == "state")
            {
                var hud = Map(Map(ev, "snapshot"), "hud");
                foreach (var key in new[] { "title", "time" }) if (Text(hud, key) is { Length: > 0 } value) text.Add(value);
            }
            if (kind == "replay_play" && Text(ev, "mode") is not ("stage" or "campus")) throw Fail("unsupported_replay_presentation");
        }
        if (_pending is { } form && Text(form, "kind") == "form")
            foreach (var field in Maps(form, "fields").Where(f => Text(form, "layout") != "tabs" || Text(f, "id") == _activeField))
            {
                foreach (var key in new[] { "heading", "placeholder", "empty_text" })
                    if (Text(field, key) is { Length: > 0 } value) text.Add(value);
                foreach (var option in Maps(field, "options"))
                    text.Add(Text(option, "label") + (Flag(option, "available", true) ? "" : " " + Text(option, "lock_hint")));
            }
        if (!_closed && !complete)
        {
            if (_pending is { } pending) BuildPending(pending);
            else if (fieldOffers is not null)
                foreach (var offer in fieldOffers)
                {
                    if (offer.Kind is not ("move" or "interact" or "wait" or "menu" or "form_field" or "form_submit" or "continue")) throw Fail("unsupported_field_offer");
                    Add(offer.Label, offer.Kind, offer.Input.ToDictionary(p => p.Key, p => p.Value), offer.New, group: offer.Group, publicValue: offer.Value, purpose: offer.Purpose, selected: offer.Selected, location: offer.Location);
                }
        }
        Current = new(++_turn, lines.ToImmutable(), text.ToImmutable(), [.._offers.Values.Select(o => o.Visible)], complete);
        if (System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(Current)) > 65536) throw Fail("observation_limit");
        return Current;
    }
    private void BuildPending(Dictionary<string, object?> pending)
    {
        var kind = Text(pending, "kind");
        var pid = Text(pending, "presentation_id");
        if (kind == "form")
        {
            var tabs = Text(pending, "layout") == "tabs";
            foreach (var field in Maps(pending, "fields"))
            {
                var id = Text(field, "id");
                if (tabs && id != _activeField || !tabs && _values.ContainsKey(id)) continue;
                var fieldKind = Text(field, "kind");
                _observeField?.Invoke(field);
                if (fieldKind == "choice")
                    foreach (var option in Maps(field, "options").Where(o => Flag(o, "available", true)))
                        Add(Text(field, "heading") + " " + Text(option, "label"), "form_field", [], field: id, value: Text(option, "id"), group: Text(field, "heading"), publicValue: Text(option, "label"), purpose: Text(field, "purpose"), selected: _values.GetValueOrDefault(id)?.ToString() == Text(option, "id"));
                else if (fieldKind == "text")
                    Add(Text(field, "placeholder") + " " + _selfName, "form_field", [], field: id, value: _selfName);
                else throw Fail("unsupported_form_field");
                // One field at a time, as in the ordinary form. Empty fields stay empty;
                // action requirements below must still pass before submission.
                if (_offers.Count > 0) break;
            }
            foreach (var action in Maps(pending, "actions").Where(a => Flag(a, "available", true)))
            {
                var required = (action.GetValueOrDefault("requires") as IEnumerable<object?> ?? []).Select(v => v?.ToString() ?? "");
                if (required.Any(id => !_values.TryGetValue(id, out var value) || string.IsNullOrEmpty(value?.ToString()))) continue;
                if (action.ContainsKey("tab"))
                {
                    var target = Text(action, "tab");
                    if (Maps(pending, "fields").Any(f => Text(f, "id") == target))
                        Add(Text(action, "label"), "menu", [], field: target, tab: true, purpose: "tab", selected: target == _activeField);
                    continue;
                }
                Add(Text(action, "label"), "form_submit", Event("form", ("presentation_id", pid), ("action", Text(action, "id")), ("values", new Dictionary<string, object?>(_values))), purpose: required.Any() || Flag(action, "default") ? "commit" : "cancel");
            }
            return;
        }
        if (kind == "ending")
        {
            foreach (var action in Maps(pending, "actions").Where(a => Text(a, "id") is "continue" or "chapter2"))
                Add(Text(action, "label"), "continue", Event("navigate", ("to", Text(action, "id"))));
            return;
        }
        var options = Maps(pending, kind == "lines" ? "choices" : "options").ToArray();
        foreach (var option in options.Where(o => Flag(o, "available", true)))
            Add(Text(option, "label"), "menu", Event("select", ("presentation_id", pid), ("choice_id", Text(option, "id")), ("token", Text(option, "token"))), purpose: Text(option, "purpose"));
        if (options.Length == 0 && pid != "") Add("", "continue", Event("presented", ("presentation_id", pid)));
    }
    public PublicPlayerObservation Submit(SelectCandidate selection)
    {
        Open();
        if (Selections >= _maxSelections) throw Fail("selection_budget");
        if (!_offers.TryGetValue(selection.Token, out var offer)) throw Fail("unknown_consumed_or_expired_token");
        _offers.Clear(); Selections++;
        if (offer.Field is { } field)
        {
            if (offer.Tab) _activeField = field;
            else _values[field] = offer.Value;
            LastInput = offer.Tab ? Event("form_tab", ("field", field)) : Event("form_field", ("field", field), ("value", offer.Value));
            LastOutput = [];
            // Preserve the current form but issue a fresh token batch.
            return Observe([]);
        }
        _pending = null; _values.Clear(); _activeField = ""; LastInput = offer.Input;
        try { return Observe(_dispatch(offer.Input)); }
        catch { _closed = true; _offers.Clear(); throw; }
    }
}
