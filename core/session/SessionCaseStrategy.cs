using DuelView.Dv;
using System.Text.Json;
using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    private sealed class ParsedSessionCase(string source, JsonElement data, string label, Func<string, JsonElement> resolve)
    {
        public string Source { get; } = source;
        public JsonElement Data { get; } = data;
        public string Version { get; } = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)));
        private CaseTheory? theory;
        private CaseTheory? actorTheory;
        // Actor scopes are independent of the objective history import. Author
        // history verification remains mandatory when the objective theory is requested.
        public CaseTheory ActorTheory => actorTheory ??= CaseTheory.FromData(Data.GetProperty("case_theory"));
        public CaseTheory Theory
        {
            get
            {
                try { return theory ??= CaseHistoryReference.Apply(ActorTheory, Data, resolve); }
                catch (FormatException error) { throw new FormatException("Case " + label + ": " + error.Message, error); }
            }
        }
    }
    private Dictionary<string, string>? caseSnapshotSources;
    private readonly Dictionary<string, ParsedSessionCase> parsedSessionCases = new(StringComparer.Ordinal);

    private IReadOnlyDictionary<string, string> CaseSnapshotSources() => caseSnapshotSources ??=
        JsonSerializer.Deserialize<Dictionary<string, string>>((string)Field(state, "case_library_sources_json", ""))
        ?? throw new InvalidDataException("Invalid case library snapshot.");

    private ParsedSessionCase SnapshotCase(string? caseId, string? source = null)
    {
        if (caseId is not null && !CaseSnapshotSources().TryGetValue(caseId, out source))
            throw new ArgumentException("Case is not in the session library snapshot.");
        ArgumentNullException.ThrowIfNull(source);
        if (!parsedSessionCases.TryGetValue(source, out var parsed))
        {
            try
            {
                var data = DvScriptReader.Parse(source).GetProperty("data");
                parsed = new(source, data, caseId ?? "(snapshot source)", id => SnapshotCase(id).Data);
            }
            catch (FormatException error)
            {
                throw new FormatException("Case " + (caseId ?? "(snapshot source)") + ": " + error.Message, error);
            }
            parsedSessionCases.Add(source, parsed);
        }
        return parsed;
    }

    private sealed record StrategyProfile(string CaseId, ParsedSessionCase Case, JsonElement Profile)
    {
        public string Scope => Profile.GetProperty("actor").GetString()!;
        public string Actor => Profile.TryGetProperty("runtime_actor", out var actor) ? actor.GetString()! : Scope;
        public bool Sliced => Profile.TryGetProperty("computation", out var mode) && mode.GetString() == "sliced";
    }
    private sealed record PendingStrategy(string Signature, StrategyJudgment.CaseStrategyWork Work);
    private readonly Dictionary<(string Case, string Actor), PendingStrategy> pendingCaseStrategies = new();
    private long caseStrategySliceOperations, caseStrategySliceCount;
    private string? caseStrategyLastFailure;
    private readonly Dictionary<(string Case, string Actor), (string Signature, string Error)> failedCaseStrategies = new();
    public sealed record CaseComputationState(string Status, bool Completed, bool Failed, string? Error);
    public CaseComputationState PreparedComputationStatus
    {
        get
        {
            using var valuesScope = R.UseValues(valueContext);
            var pending = Field(state, "pending", false);
            if (pending is false || !R.Bool(Field(Field(pending, "action", new List<object>()), "case_computation_required", false)))
                return new("not_required", true, false, null);
            return GetCaseComputationStatus(R.Text(Field(pending, "id", "")));
        }
    }

    // Shared budget across all outstanding jobs; no elapsed computation time is game time.
    private void AdvanceCaseStrategyWork(int maxOperations = 128, double milliseconds = 2)
    {
        // Observe declared sliced jobs even while their physical activity prerequisites
        // keep gambit actions out of the candidate set.
        var prepared = Field(state, "pending", false);
        var requiredActor = prepared is not false && R.Bool(Field(Field(prepared, "action", new List<object>()), "case_computation_required", false))
            ? R.Text(Field(prepared, "id", "")) : "";
        foreach (var entry in StrategyIndex().Values.SelectMany(x => x).Where(x => x.Sliced).Distinct())
        {
            var actor = R.Items(Field(state, "actors", new List<object>())).FirstOrDefault(x => R.Text(Field(x, "id", "")) == entry.Actor);
            if (actor is null) continue;
            var action = entry.Profile.GetProperty("choices").EnumerateArray().First().GetProperty("action").GetString()!;
            _ = ReadCaseStrategyJudgment(actor, Field(actor, "mind", new List<object>()), action, entry.Actor == requiredActor || inFlightMovements.Values.Any(activity => activity.BackgroundActivity
                && R.Text(Field(activity.Pending, "id", "")) == entry.Actor));
        }
        var deadline = System.Diagnostics.Stopwatch.GetTimestamp() + (long)(milliseconds / 1000 * System.Diagnostics.Stopwatch.Frequency);
        foreach (var pair in pendingCaseStrategies.ToArray())
        {
            if (maxOperations <= 0 || System.Diagnostics.Stopwatch.GetTimestamp() >= deadline) break;
            var slice = pair.Value.Work.AdvanceUntil(Math.Min(32, maxOperations), deadline);
            maxOperations -= slice.Operations;
            caseStrategySliceOperations += slice.Operations;
            caseStrategySliceCount++;
            if (slice.Status == CaseWorkStatus.Completed)
            {
                caseStrategyJudgments[pair.Key] = new(pair.Value.Signature, pair.Value.Work.Result, true);
                pendingCaseStrategies.Remove(pair.Key);
                pair.Value.Work.Dispose();
                caseStrategyProofUpdates++;
            }
            else if (slice.Status != CaseWorkStatus.Running)
            {
                caseStrategyLastFailure = slice.Status + ": " + slice.Error;
                failedCaseStrategies[pair.Key] = (pair.Value.Signature, caseStrategyLastFailure);
                pendingCaseStrategies.Remove(pair.Key);
                pair.Value.Work.Dispose();
            }
        }
    }
    // Used only by actions explicitly declaring case_computation_required in DV.
    // Completion requires a checked result for the latest actor-owned inputs.
    private bool IsCaseComputationReady(string actorId) => GetCaseComputationStatus(actorId).Completed;

    public CaseComputationState GetCaseComputationStatus(string actorId)
    {
        using var valuesScope = R.UseValues(valueContext);
        var profiles = StrategyIndex().Values.SelectMany(x => x).Where(x => x.Sliced && x.Actor == actorId).Distinct().ToArray();
        if (profiles.Length == 0) return new("missing_profile", false, true, "No sliced computation is declared for this actor.");
        var actor = R.Items(Field(state, "actors", new List<object>())).FirstOrDefault(x => R.Text(Field(x, "id", "")) == actorId);
        if (actor is null) return new("missing_actor", false, true, "The computation actor is absent.");
        var running = false;
        foreach (var entry in profiles)
        {
            var inputs = ReadCaseMindInputs(entry.Case.Data, actorId, Field(actor, "mind", new List<object>()));
            var evidence = CaseEvidence().Where(e => e.CaseId == entry.CaseId && e.Actor == actorId).Select(e => e.Evidence).ToArray();
            var signature = entry.Case.Version + ":" + inputs.Signature + ":" + JsonSerializer.Serialize(evidence);
            var key = (entry.CaseId, entry.Scope);
            if (failedCaseStrategies.TryGetValue(key, out var failure) && failure.Signature == signature)
                return new("failed", false, true, failure.Error);
            if (pendingCaseStrategies.TryGetValue(key, out var pending) && pending.Signature == signature) { running = true; continue; }
            if (!caseStrategyJudgments.TryGetValue(key, out var judgment) || judgment.Signature != signature || !judgment.ComputationCompleted)
                return new("not_started", false, true, "No current computation. AdvancePendingComputation must start the declared prepared job.");
        }
        // This says the computation finished and its certificate was checked, even
        // when every candidate is unavailable or the resulting theory is empty.
        return running ? new("running", false, false, null) : new("completed", true, false, null);
    }

    private void CancelCaseStrategyWork()
    {
        foreach (var pending in pendingCaseStrategies.Values) pending.Work.Dispose();
        pendingCaseStrategies.Clear();
    }
    private sealed record CachedStrategyJudgment(string Signature, IReadOnlyList<CaseStrategyChoice> Choices, bool ComputationCompleted);
    private Dictionary<(string Actor, string Action), List<StrategyProfile>>? caseStrategyIndex;
    private readonly Dictionary<(string Case, string Actor), CachedStrategyJudgment> caseStrategyJudgments = new();
    private long caseStrategyProofUpdates, caseStrategyCacheHits, caseStrategyDormantJudgments, caseStrategyExplicitReflections;

    private Dictionary<(string Actor, string Action), List<StrategyProfile>> StrategyIndex()
    {
        if (caseStrategyIndex is not null) return caseStrategyIndex;
        var index = new Dictionary<(string Actor, string Action), List<StrategyProfile>>();
        foreach (var caseId in CaseSnapshotSources().Keys)
        {
            var declared = SnapshotCase(caseId); // Parse DV metadata once; do not elaborate or prove its theory.
            if (!declared.Data.TryGetProperty("case_strategy", out var strategy)) continue;
            var actors = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in strategy.GetProperty("actors").EnumerateArray())
            {
                var actorId = profile.GetProperty("actor").GetString() ?? throw new InvalidDataException("Missing strategy actor.");
                if (!actors.Add(actorId)) throw new InvalidDataException("Duplicate case strategy actor: " + actorId);
                var entry = new StrategyProfile(caseId, declared, profile);
                foreach (var choice in profile.GetProperty("choices").EnumerateArray())
                {
                    var action = choice.GetProperty("action").GetString() ?? throw new InvalidDataException("Missing strategy action.");
                    var key = (entry.Actor, action);
                    if (!index.TryGetValue(key, out var profiles)) index.Add(key, profiles = new());
                    profiles.Add(entry);
                }
            }
        }
        return caseStrategyIndex = index;
    }

    // Binding an operation does not parse a theory, inspect evidence, audit a library, or run proof search.
    // Ordinary admission uses a retained judgment. Only a changed related cognitive input refreshes it.
    private IDisposable UseCaseStrategy() => CaseStrategy.Use((actor, mind, action) => ReadCaseStrategyJudgment(actor, mind, action));

    private IReadOnlyList<CaseStrategyChoice> ReadCaseStrategyJudgment(object actor, object mind, string action, bool forceComputation = false)
    {
        var actorId = R.Text(Field(actor, "id", ""));
        if (!StrategyIndex().TryGetValue((actorId, action), out var profiles)) return Array.Empty<CaseStrategyChoice>();
        var result = new List<CaseStrategyChoice>();
        foreach (var entry in profiles)
        {
            var inputs = ReadCaseMindInputs(entry.Case.Data, actorId, mind);
            var explicitEvidence = CaseEvidence().Where(e => e.CaseId == entry.CaseId && e.Actor == actorId).Select(e => e.Evidence).ToArray();
            var signature = entry.Case.Version + ":" + inputs.Signature + ":" + JsonSerializer.Serialize(explicitEvidence);
            var key = (entry.CaseId, entry.Scope);
            if (caseStrategyJudgments.TryGetValue(key, out var cached) && cached.Signature == signature &&
                (!forceComputation || cached.ComputationCompleted || pendingCaseStrategies.ContainsKey(key) ||
                    failedCaseStrategies.TryGetValue(key, out var failed) && failed.Signature == signature))
            {
                caseStrategyCacheHits++;
            }
            else
            {
                // Initial absence of observations does not justify running every role's proof theory at game start.
                // Losing previously used evidence does refresh the theory so stale conclusions cannot survive.
                var needsProof = forceComputation || inputs.Evidence.Count > 0 || explicitEvidence.Length > 0 || cached?.ComputationCompleted == true;
                if (failedCaseStrategies.TryGetValue(key, out var staleFailure) && staleFailure.Signature != signature) failedCaseStrategies.Remove(key);
                if (pendingCaseStrategies.TryGetValue(key, out var obsolete) && obsolete.Signature != signature)
                { obsolete.Work.Dispose(); pendingCaseStrategies.Remove(key); }
                IReadOnlyList<CaseStrategyChoice> choices;
                if (needsProof)
                {
                    var theory = entry.Case.ActorTheory.WithEvidence(entry.Scope, explicitEvidence.Concat(inputs.Evidence));
                    if (entry.Sliced)
                    {
                        if (!pendingCaseStrategies.TryGetValue(key, out var pending) || pending.Signature != signature)
                        {
                            pending?.Work.Dispose();
                            pendingCaseStrategies[key] = new(signature, CaseStrategy.BeginEvaluation(theory, entry.Scope, entry.Profile));
                        }
                        // An old proof cannot admit an action after its premises changed.
                        choices = CaseStrategy.AwaitingEvidence(entry.Scope, entry.Profile, "pending:" + signature)
                            .Select(choice => choice with { Ready = false }).ToArray();
                    }
                    else
                    {
                        choices = CaseStrategy.Evaluate(theory, entry.Scope, entry.Profile);
                        caseStrategyProofUpdates++;
                    }
                }
                else
                {
                    choices = CaseStrategy.AwaitingEvidence(entry.Scope, entry.Profile, "unproved:" + entry.Case.Version);
                    caseStrategyDormantJudgments++;
                }
                cached = new(signature, choices, needsProof && !entry.Sliced);
                caseStrategyJudgments[key] = cached;
            }
            result.AddRange(cached.Choices.Where(x => x.Action == action));
        }
        return result.AsReadOnly();
    }

    public IReadOnlyList<CaseStrategyChoice> ReviewCaseStrategy(string caseId, string actor)
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed || HasPending(state) || actor != Viewer || actor == "objective")
            throw new InvalidOperationException("Strategy review requires the current viewer at a committed boundary.");
        var declared = SnapshotCase(caseId);
        if (!declared.Data.TryGetProperty("case_strategy", out var strategy)) return Array.Empty<CaseStrategyChoice>();
        var actorState = R.Items(Field(state, "actors", new List<object>())).Single(x => R.Text(Field(x, "id", "")) == actor);
        var inputs = ReadCaseMindInputs(declared.Data, actor, Field(actorState, "mind", new List<object>()));
        var explicitEvidence = CaseEvidence().Where(e => e.CaseId == caseId && e.Actor == actor).Select(e => e.Evidence).ToArray();
        var signature = declared.Version + ":" + inputs.Signature + ":" + JsonSerializer.Serialize(explicitEvidence);
        var judgments = new List<CaseStrategyChoice>();
        foreach (var profile in strategy.GetProperty("actors").EnumerateArray())
        {
            var entry = new StrategyProfile(caseId, declared, profile);
            if (entry.Actor != actor) continue;
            var theory = declared.ActorTheory.WithEvidence(entry.Scope, explicitEvidence.Concat(inputs.Evidence));
            var choices = CaseStrategy.Evaluate(theory, entry.Scope, profile);
            var key = (caseId, entry.Scope);
            if (pendingCaseStrategies.Remove(key, out var pending)) pending.Work.Dispose();
            caseStrategyJudgments[key] = new(signature, choices, true);
            judgments.AddRange(choices);
        }
        caseStrategyExplicitReflections++;
        return judgments;
    }

    public CaseGoalResult ProveCaseActor(string caseId, string actor, CaseLiteral goal) => ActorCase(caseId, actor).Prove(actor, goal);
    public CaseCheckResult CheckCaseActorGoal(string caseId, string actor, CaseProof proof, CaseLiteral goal) => ActorCase(caseId, actor).CheckGoal(actor, proof, goal);
    public CaseGoalResult ProveCaseDesign(string caseId, string scope, CaseLiteral goal) { RequireCaseDeveloper(); return LoadCase(caseId).Prove(scope, goal); }
    public CaseCheckResult CheckCaseDesignGoal(string caseId, string scope, CaseProof proof, CaseLiteral goal) { RequireCaseDeveloper(); return LoadCase(caseId).CheckGoal(scope, proof, goal); }
}
