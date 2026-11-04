using DuelView.Dv;
using System.Text.Json;
using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    private CaseAuditResult? lastCaseLibraryAudit;
    private string? caseSnapshotSha256;
    private readonly Dictionary<string, CaseHistory.Report> lastCaseHistoryReports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CaseHistory.Report> lastCaseHistoryBranches = new(StringComparer.Ordinal);
    /// <summary>Metadata only. Ordinary inspection never runs the authored proof/test corpus.</summary>
    public IReadOnlyDictionary<string, object?> CaseReasoningInfo => BuildCaseReasoningInfo();
    public IReadOnlyDictionary<string, object?> AuditCaseReasoningInfo()
    {
        lastCaseLibraryAudit = AuditCaseLibraryCore();
        return BuildCaseReasoningInfo(includeHistoryReports: true);
    }
    private IReadOnlyDictionary<string, object?> BuildCaseReasoningInfo(bool includeHistoryReports = false)
    {
        using var valuesScope = R.UseValues(valueContext);
        var result = lastCaseLibraryAudit;
        var sources = CaseSnapshotSources();
        var libraryHash = caseSnapshotSha256 ??= HashCaseSources(sources);
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["logic"] = "finite-signed-horn-with-intuitionistic-first-order-proof-checker", ["library_count"] = sources.Count,
            ["library_sha256"] = libraryHash,
            ["audited_library_sha256"] = result?.LibraryHash,
            ["checks"] = result?.Total ?? 0, ["passed"] = result?.Passed ?? 0,
            ["history_reports"] = includeHistoryReports ? lastCaseHistoryReports : HostValues.Empty,
            ["history_branches"] = includeHistoryReports ? lastCaseHistoryBranches : HostValues.Empty,
            ["check_kinds"] = result is null ? HostValues.Empty : HostValues.Freeze(result.Items.GroupBy(x => x.Kind).ToDictionary(x => x.Key, x => (object?)x.Count(), StringComparer.Ordinal)),
            ["status"] = result is null ? "not_run" : result.Total > 0 && result.Total == result.Passed ? "passed" : "failed",
            ["audit_trigger"] = "explicit-info-audit-request-or-developer-audit",
            ["coverage"] = "authored-case-theorems-candidates-proof-checks-and-travel-queries",
            ["campaign_story_integrated"] = false,
            ["campaign_integration"] = "partial-declared-actor-mind-evidence-and-strategy-choices",
            ["strategy_scope"] = "actor-owned-admission-and-immediate-utility-not-complete-campaign-planning",
            ["strategy_update_mode"] = "related-cognitive-evidence-change-with-declared-sliced-computation",
            ["strategy_pending_computations"] = pendingCaseStrategies.Count,
            ["strategy_slice_count"] = caseStrategySliceCount,
            ["strategy_slice_operations"] = caseStrategySliceOperations,
            ["strategy_last_failure"] = caseStrategyLastFailure,
            ["strategy_pending_operations"] = pendingCaseStrategies.Values.Sum(p => p.Work.TotalOperations),
            ["strategy_proof_updates"] = caseStrategyProofUpdates,
            ["strategy_explicit_reflections"] = caseStrategyExplicitReflections,
            ["strategy_cache_hits"] = caseStrategyCacheHits,
            ["strategy_dormant_judgments"] = caseStrategyDormantJudgments,
            ["failed_checks"] = result is null ? Array.Empty<object>() : result.Items.Where(x => !x.Passed).Select(x => HostValues.Freeze(new Dictionary<string, object?>
                { ["case_id"] = x.CaseId, ["scope"] = x.Scope, ["kind"] = x.Kind, ["id"] = x.Id, ["expected"] = x.Expected, ["actual"] = x.Actual })).ToArray()
        });
    }
    private sealed record CaseEvidenceEntry(string CaseId, string Actor, CaseAxiom Evidence);
    public sealed record CaseAuditItem(string CaseId, string Scope, string Kind, string Id, string Expected, string Actual, bool Passed);
    public sealed record CaseAuditResult(int Total, int Passed, IReadOnlyList<CaseAuditItem> Items, string LibraryHash = "");

    private static string HashCaseSources(IReadOnlyDictionary<string, string> sources) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            sources.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)))));

    private static string CasePath(string caseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        if (caseId is "." or ".." || caseId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || caseId.Contains('/') || caseId.Contains('\\'))
            throw new ArgumentException("Invalid case identifier.", nameof(caseId));
        return Path.Combine(AppContext.BaseDirectory, "Content", "Cases", caseId + ".dv");
    }
    public IReadOnlyList<string> CaseLibrary() => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Content", "Cases"), "*.dv")
        .Select(Path.GetFileNameWithoutExtension).Where(x => x is not null).Select(x => x!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    private static CaseTheory LoadCase(string caseId)
    {
        var data = DvScriptReader.Parse(File.ReadAllText(CasePath(caseId))).GetProperty("data");
        return CaseHistoryReference.Apply(CaseTheory.FromData(data.GetProperty("case_theory")), data,
            id => DvScriptReader.Parse(File.ReadAllText(CasePath(id))).GetProperty("data"));
    }

    private static object InitializeCaseLibrarySnapshot(object value)
    {
        var existing = Field(value, "case_library_sources_json", "") as string ?? "";
        if (existing.Length != 0) return value;
        var directory = Path.Combine(AppContext.BaseDirectory, "Content", "Cases");
        var sources = Directory.GetFiles(directory, "*.dv").ToDictionary(path => Path.GetFileNameWithoutExtension(path)!,
            File.ReadAllText, StringComparer.Ordinal);
        return R.record_with(value, "case_library_sources_json", JsonSerializer.Serialize(sources));
    }

    private List<CaseEvidenceEntry> CaseEvidence()
    {
        var json = Field(state, "case_actor_evidence_json", "") as string ?? "";
        return json.Length == 0 ? new() : JsonSerializer.Deserialize<List<CaseEvidenceEntry>>(json)
            ?? throw new InvalidDataException("Invalid case evidence archive.");
    }
    private CaseTheory ActorCase(string caseId, string actor)
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        if (IsClosed || HasPending(state)) throw new InvalidOperationException("Case reasoning requires a committed action boundary.");
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (actor == "objective") throw new ArgumentException("The objective scope cannot be used as an actor scope.");
        if (!string.Equals(actor, Viewer, StringComparison.Ordinal)) throw new InvalidOperationException("Case actor scope must be the current viewer.");
        var declared = SnapshotCase(caseId);
        var theory = declared.ActorTheory;
        if (!theory.Scopes.ContainsKey(actor)) throw new ArgumentException("No declared scope for this actor.");
        return theory.WithEvidence(actor, CaseEvidence().Where(e => e.CaseId == caseId && e.Actor == actor).Select(e => e.Evidence)
            .Concat(ReadCaseMindEvidence(declared.Source, actor)));
    }
    /// <summary>Records explicitly supplied evidence only in the named actor's case scope.</summary>
    public void AddCaseActorEvidence(string caseId, string actor, CaseAxiom evidence)
    {
        RequireCaseDeveloper();
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed || HasPending(state)) throw new InvalidOperationException("Evidence requires an open session at a committed action boundary.");
        ArgumentNullException.ThrowIfNull(evidence);
        var theory = ActorCase(caseId, actor);
        _ = theory.WithEvidence(actor, new[] { evidence });
        var entries = CaseEvidence();
        entries.Add(new(caseId, actor, evidence));
        var updated = R.record_with(state, "case_actor_evidence_json", JsonSerializer.Serialize(entries));
        BeginTransition(); state = updated; RememberCommitted();
    }
    public CaseProof InferCaseActor(string caseId, string actor) => ActorCase(caseId, actor).Infer(actor);
    public CaseCheckResult CheckCaseActor(string caseId, string actor, CaseProof proof) => ActorCase(caseId, actor).Check(actor, proof);
    public IReadOnlyList<CaseCandidateReview> ReviewCaseActor(string caseId, string actor) => ActorCase(caseId, actor).AuditCandidates(actor);

    // Design/review calls explicitly name a scope and are not actor projections.
    private void RequireCaseDeveloper() { if (!DevMode) throw new InvalidOperationException("Case design requires development mode."); }
    public CaseProof InferCaseDesign(string caseId, string scope) { RequireCaseDeveloper(); return LoadCase(caseId).Infer(scope); }
    public CaseCheckResult CheckCaseDesign(string caseId, string scope, CaseProof proof) { RequireCaseDeveloper(); return LoadCase(caseId).Check(scope, proof); }
    public IReadOnlyList<CaseCandidateReview> ReviewCaseDesign(string caseId, string scope) { RequireCaseDeveloper(); return LoadCase(caseId).AuditCandidates(scope); }
    public CaseDesignReview EvaluateCaseDesign(string caseId, string scope) { RequireCaseDeveloper(); return LoadCase(caseId).EvaluateDesign(scope); }
    public CaseTravel.Result EstimateCaseTravel(CaseTravel.Mode mode, string origin, string target,
        IEnumerable<string> knownNodes, IEnumerable<CaseTravel.Edge> edges, double? minimumSpeed = null, double? maximumSpeed = null)
        => CaseTravel.Estimate(mode, origin, target, knownNodes, edges, minimumSpeed, maximumSpeed);

    private static CaseTravel.Result TravelQuery(JsonElement query)
    {
        var mode = query.GetProperty("mode").GetString() switch
        {
            "metric_path" => CaseTravel.Mode.MetricPath,
            "room_graph_seconds" => CaseTravel.Mode.RoomGraphSeconds,
            _ => throw new InvalidDataException("Invalid travel mode.")
        };
        var nodes = query.GetProperty("nodes").EnumerateArray().Select(n => n.GetString() ?? throw new InvalidDataException("Invalid node.")).ToArray();
        var edges = query.GetProperty("edges").EnumerateArray().Select(e => new CaseTravel.Edge(
            e.GetProperty("from").GetString()!, e.GetProperty("to").GetString()!,
            e.GetProperty("lower").GetDouble(), e.GetProperty("upper").GetDouble(),
            e.GetProperty("access").GetString() switch { "open" => CaseTravel.Access.Open, "unknown" => CaseTravel.Access.Unknown,
                "locked" => CaseTravel.Access.Locked, _ => throw new InvalidDataException("Invalid access.") },
            e.GetProperty("source").GetString()!)).ToArray();
        double? min = query.TryGetProperty("minimum_speed", out var a) ? a.GetDouble() : null;
        double? max = query.TryGetProperty("maximum_speed", out var b) ? b.GetDouble() : null;
        return CaseTravel.Estimate(mode, query.GetProperty("origin").GetString()!, query.GetProperty("target").GetString()!, nodes, edges, min, max);
    }
    public CaseTravel.Result EstimateCaseTravelFromDv(string caseId, string queryId)
    {
        RequireCaseDeveloper();
        var data = DvScriptReader.Parse(File.ReadAllText(CasePath(caseId))).GetProperty("data");
        var query = data.GetProperty("case_travel").GetProperty("queries").EnumerateArray()
            .Single(q => q.GetProperty("id").GetString() == queryId);
        return TravelQuery(query);
    }

    /// <summary>A DV-declared estimate known by this actor. It is not a physical travel completion.</summary>
    public CaseTravel.Result EstimateActorTravelFromDv(string caseId, string queryId)
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed || HasPending(state)) throw new InvalidOperationException("Travel reasoning requires a committed action boundary.");
        var data = SnapshotCase(caseId).Data;
        var query = data.GetProperty("case_travel").GetProperty("queries").EnumerateArray()
            .Single(q => q.GetProperty("id").GetString() == queryId);
        if (!query.TryGetProperty("actor", out var actor) || actor.GetString() != Viewer || Viewer == "objective")
            throw new InvalidOperationException("Travel estimate is not declared for the current viewer.");
        return TravelQuery(query);
    }

    /// <summary>Compares actual theorem and candidate results against expectations declared in each case DV.</summary>
    public CaseAuditResult AuditCaseLibrary() { RequireCaseDeveloper(); return lastCaseLibraryAudit = AuditCaseLibraryCore(); }
    private CaseAuditResult AuditCaseLibraryCore()
    {
        var items = new List<CaseAuditItem>();
        var auditSources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var caseId in CaseLibrary())
        {
            var source = File.ReadAllText(CasePath(caseId));
            auditSources.Add(caseId, source);
            CaseTheory theory;
            var data = DvScriptReader.Parse(source).GetProperty("data");
            try { theory = CaseHistoryReference.Apply(CaseTheory.FromData(data.GetProperty("case_theory")), data,
                id => DvScriptReader.Parse(File.ReadAllText(CasePath(id))).GetProperty("data")); }
            catch (FormatException error) { throw new FormatException("Case " + caseId + ": " + error.Message, error); }
            var root = DvScriptReader.Parse(source).GetProperty("data").GetProperty("case_theory");
            if (!root.TryGetProperty("audit", out var audit)) throw new InvalidDataException("Missing case audit: " + caseId);
            foreach (var entry in audit.EnumerateArray())
            {
                var scope = entry.GetProperty("scope").GetString() ?? throw new InvalidDataException("Missing scope.");
                var proof = theory.Infer(scope);
                var check = theory.Check(scope, proof);
                if (!check.Valid) throw new InvalidDataException("Case proof failed: " + string.Join("; ", check.Errors));
                var facts = proof.Nodes.Select(n => n.Conclusion.Key).ToHashSet(StringComparer.Ordinal);
                if (entry.TryGetProperty("theorems", out var theorems))
                    foreach (var expectation in theorems.EnumerateObject())
                    {
                        var literal = theory.Scopes[scope].Theorems.Single(t => t.Id == expectation.Name).Conclusion;
                        var positive = facts.Contains(literal.Key); var negative = facts.Contains(literal.Opposite.Key);
                        var actual = positive && negative ? "conflicted" : positive ? "proved" : negative ? "refuted" : "unknown";
                        var expected = expectation.Value.GetString() ?? throw new InvalidDataException("Missing expectation.");
                        items.Add(new(caseId, scope, "theorem", expectation.Name, expected, actual, expected == actual));
                    }
                if (entry.TryGetProperty("candidates", out var candidates))
                {
                    var reviews = theory.AuditCandidates(scope, proof).ToDictionary(r => r.Id, StringComparer.Ordinal);
                    foreach (var expectation in candidates.EnumerateObject())
                    {
                        var actual = reviews[expectation.Name].Status;
                        var expected = expectation.Value.GetString() ?? throw new InvalidDataException("Missing expectation.");
                        items.Add(new(caseId, scope, "candidate", expectation.Name, expected, actual, expected == actual));
                    }
                }
                if (entry.TryGetProperty("unique_within_declared_candidates", out var unique) || entry.TryGetProperty("clue_removal", out _))
                {
                    var design = theory.EvaluateDesign(scope);
                    if (unique.ValueKind != JsonValueKind.Undefined)
                        items.Add(new(caseId, scope, "design", "unique_within_declared_candidates", unique.GetBoolean().ToString(), design.UniqueWithinDeclaredCandidates.ToString(), unique.GetBoolean() == design.UniqueWithinDeclaredCandidates));
                    if (entry.TryGetProperty("clue_removal", out var removals))
                        foreach (var removal in removals.EnumerateObject())
                        {
                            var expected = removal.Value.EnumerateArray().Select(x => x.GetString()!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                            var actual = design.ClueRemoval.Single(x => x.RemovedAxiom == removal.Name).GoalsNoLongerProvable.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                            items.Add(new(caseId, scope, "clue_removal", removal.Name, string.Join(",", expected), string.Join(",", actual), expected.SequenceEqual(actual)));
                        }
                }
            }
            if (data.TryGetProperty("sim4_audit", out var sim4Audit))
                foreach (var item in Sim4Planner.Audit(sim4Audit))
                    items.Add(new(caseId, "sim4", "sim4", item.Id, item.Expected, item.Actual, item.Passed));
            if (data.TryGetProperty("case_history_reference_audit", out var referencedAudit))
            {
                var referencedId = referencedAudit.GetProperty("case_id").GetString() ?? throw new InvalidDataException("Missing history case");
                // Authored multiline mutation strings use LF; source checkout line endings do not change DV meaning.
                var referencedSource = File.ReadAllText(CasePath(referencedId)).Replace("\r\n", "\n", StringComparison.Ordinal);
                var referencedHistory = DvScriptReader.Parse(referencedSource).GetProperty("data").GetProperty("case_history");
                var mutationIds = referencedAudit.GetProperty("mutations").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
                foreach (var item in CaseHistory.Audit(referencedHistory, referencedAudit).Where(x => mutationIds.Contains(x.Id)))
                    items.Add(new(caseId, "history", "history", item.Id, item.Expected, item.Actual, item.Passed));
                if (referencedAudit.TryGetProperty("projections", out var projections))
                    foreach (var projection in projections.EnumerateArray())
                    {
                        var request = projection.GetProperty("request").Deserialize<CaseHistory.CheckRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                        var result = CaseHistory.Check(referencedHistory, request);
                        var expected = projection.GetProperty("expect_valid").GetBoolean();
                        var excluded = projection.TryGetProperty("exclude_actors", out var excludedActors)
                            ? excludedActors.EnumerateArray().Select(x => x.GetString()!).ToArray() : [];
                        var passed = result.Valid == expected && !result.ActiveActors.Intersect(excluded, StringComparer.Ordinal).Any();
                        items.Add(new(caseId, "history", "history-projection", projection.GetProperty("id").GetString()!, expected.ToString(),
                            JsonSerializer.Serialize(new { result.Valid, result.ActiveActors, result.Events, result.Issues }), passed));
                    }
                if (referencedAudit.TryGetProperty("syntax_mutations", out var syntaxMutations))
                    foreach (var mutation in syntaxMutations.EnumerateArray())
                    {
                        var find = mutation.GetProperty("find").GetString()!;
                        var replacement = mutation.GetProperty("replace").GetString()!;
                        var expected = mutation.GetProperty("expect_error").GetString()!;
                        var actual = "accepted";
                        if (!referencedSource.Contains(find, StringComparison.Ordinal)) actual = "mutation did not match source";
                        else
                        {
                            try { _ = DvScriptReader.Parse(referencedSource.Replace(find, replacement, StringComparison.Ordinal)); }
                            catch (FormatException error) { actual = error.Message; }
                        }
                        items.Add(new(caseId, "history", "history-syntax", mutation.GetProperty("id").GetString()!, expected, actual,
                            actual.Contains(expected, StringComparison.OrdinalIgnoreCase)));
                    }
                if (referencedAudit.TryGetProperty("history_mutations", out var historyMutations))
                    foreach (var mutation in historyMutations.EnumerateArray())
                    {
                        var find = mutation.GetProperty("find").GetString()!;
                        var replacement = mutation.GetProperty("replace").GetString()!;
                        if (!referencedSource.Contains(find, StringComparison.Ordinal)) throw new InvalidDataException("History mutation did not match");
                        var changed = DvScriptReader.Parse(referencedSource.Replace(find, replacement, StringComparison.Ordinal)).GetProperty("data").GetProperty("case_history");
                        var request = mutation.TryGetProperty("request", out var requestData)
                            ? requestData.Deserialize<CaseHistory.CheckRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
                        var result = CaseHistory.Check(changed, request);
                        var expected = mutation.TryGetProperty("expect_issue", out var issue) ? issue.GetString()! : "valid";
                        var passed = expected == "valid" ? result.Valid : result.Issues.Any(x => x.Kind == expected);
                        items.Add(new(caseId, "history", "history-source", mutation.GetProperty("id").GetString()!, expected,
                            passed ? expected : JsonSerializer.Serialize(result.Issues), passed));
                    }
            }
            if (data.TryGetProperty("case_history", out var history))
            {
                lastCaseHistoryReports[caseId] = CaseHistory.Check(history);
                if (history.TryGetProperty("branches", out var historyBranches))
                    foreach (var branch in historyBranches.EnumerateArray())
                        lastCaseHistoryBranches[caseId + ":" + branch.GetProperty("id").GetString()] = CaseHistory.Check(history,
                            new(Events: branch.GetProperty("events").EnumerateArray().Select(x => x.GetString()!).ToArray(),
                                Goals: branch.GetProperty("goals").EnumerateArray().Select(x => x.GetString()!).ToArray()));
                data.TryGetProperty("case_history_audit", out var historyAudit);
                foreach (var item in CaseHistory.Audit(history, historyAudit))
                    items.Add(new(caseId, "history", "history", item.Id, item.Expected, item.Actual, item.Passed));
            }
            if (data.TryGetProperty("case_models", out var models))
                foreach (var item in CaseFiniteModel.Audit(theory, models))
                    items.Add(new(caseId, item.Scope, "finite-model", item.Id, item.Expected, item.Actual, item.Passed));
            if (data.TryGetProperty("natural_deduction_audit", out var naturalAudit))
                foreach (var item in NaturalDeduction.Audit(naturalAudit))
                    items.Add(new(caseId, item.Scope, "natural-deduction", item.Id, item.Expected, item.Actual, item.Passed));
            if (data.TryGetProperty("case_strategy", out var strategy))
                foreach (var profile in strategy.GetProperty("actors").EnumerateArray())
                {
                    var actor = profile.GetProperty("actor").GetString() ?? throw new InvalidDataException("Missing strategy actor.");
                    _ = CaseStrategy.Evaluate(theory, actor, profile);
                    foreach (var expectation in profile.GetProperty("audit").EnumerateArray())
                    {
                        var action = expectation.GetProperty("action").GetString()!;
                        var auditId = expectation.TryGetProperty("id", out var declaredAuditId) ? declaredAuditId.GetString()! : action;
                        var fixture = expectation.TryGetProperty("evidence", out var evidenceData)
                            ? theory.WithEvidence(actor, evidenceData.Deserialize<CaseAxiom[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                ?? throw new InvalidDataException("Invalid strategy audit evidence."))
                            : theory;
                        var result = CaseStrategy.Evaluate(fixture, actor, profile, action).Single();
                        var ready = expectation.GetProperty("ready").GetBoolean();
                        items.Add(new(caseId, actor, "strategy-ready", auditId, ready.ToString(), result.Ready.ToString(), ready == result.Ready));
                        var utility = expectation.GetProperty("utility").GetDouble();
                        items.Add(new(caseId, actor, "strategy-utility", auditId, utility.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            result.Utility.ToString(System.Globalization.CultureInfo.InvariantCulture), utility == result.Utility));
                    }
                }
            if (data.TryGetProperty("case_branches", out var branches))
                foreach (var branch in CaseBranches.Audit(theory, branches))
                    items.Add(new(caseId, branch.Event + ":" + branch.Branch, "branch-" + branch.Kind,
                        branch.Id, branch.Expected, branch.Actual, branch.Passed));
            if (data.TryGetProperty("case_certificate_audit", out var certificateAudit))
                foreach (var test in certificateAudit.GetProperty("checks").EnumerateArray())
                {
                    var scope = test.GetProperty("scope").GetString()!;
                    var nodes = test.GetProperty("nodes").Deserialize<CaseProofNode[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                    var theoryHash = test.GetProperty("theory_version").GetString() == "current" ? CaseTheory.Fingerprint(theory.Scopes[scope]) : "stale-fixture-version";
                    var proofScope = test.TryGetProperty("proof_scope", out var ps) ? ps.GetString()! : scope;
                    var certificate = new CaseProof(proofScope, nodes, theoryHash);
                    var checkedCertificate = test.TryGetProperty("goal", out var goalData)
                        ? theory.CheckGoal(scope, certificate, goalData.Deserialize<CaseLiteral>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                            ?? throw new InvalidDataException("Invalid certificate audit goal."))
                        : theory.Check(scope, certificate);
                    var actual = checkedCertificate.Valid ? "valid" : "invalid";
                    var expected = test.GetProperty("expect").GetString()!;
                    items.Add(new(caseId, scope, "certificate", test.GetProperty("id").GetString()!, expected, actual, expected == actual));
                }
            if (data.TryGetProperty("case_travel", out var travel))
                foreach (var query in travel.GetProperty("queries").EnumerateArray())
                {
                    var id = query.GetProperty("id").GetString()!;
                    var result = TravelQuery(query);
                    var actual = result.Status.ToString().ToLowerInvariant();
                    var expected = query.GetProperty("expect_status").GetString()!;
                    items.Add(new(caseId, "travel", "status", id, expected, actual, expected == actual));
                    if (query.TryGetProperty("expect_unknown_alternative", out var unknownAlternative))
                        items.Add(new(caseId, "travel", "unknown_alternative", id, unknownAlternative.ToString(), result.UnknownAlternative.ToString(), result.UnknownAlternative == unknownAlternative.GetBoolean()));
                    if (query.TryGetProperty("expect_lower", out var lower))
                        items.Add(new(caseId, "travel", "lower_seconds", id, lower.ToString(), result.Lower?.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "missing", result.Lower is not null && result.Lower.Seconds == lower.GetDouble()));
                    if (query.TryGetProperty("expect_upper", out var upper))
                        items.Add(new(caseId, "travel", "upper_seconds", id, upper.ToString(), result.Upper?.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "missing", result.Upper is not null && result.Upper.Seconds == upper.GetDouble()));
                }
            if (data.TryGetProperty("case_proof_audit", out var proofAudit))
                foreach (var test in proofAudit.GetProperty("checks").EnumerateArray())
                {
                    var expected = test.GetProperty("expect").GetString()!;
                    string actual;
                    try { _ = CaseTheory.ParseScript(test.GetProperty("script").GetString()!); actual = "valid"; }
                    catch (FormatException) { actual = "invalid"; }
                    items.Add(new(caseId, "proof_script", "proof", test.GetProperty("id").GetString()!, expected, actual, expected == actual));
                }
        }
        var hash = HashCaseSources(auditSources);
        return new(items.Count, items.Count(i => i.Passed), items.AsReadOnly(), hash);
    }
}
