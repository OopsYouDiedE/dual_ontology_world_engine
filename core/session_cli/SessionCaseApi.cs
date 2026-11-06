// @mechanism: signed_horn_reasoning
// @layer: core/session_cli
// @contracts: SessionCaseApi

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Numerics;
using DuelView.Dv;
using DuelView.Core;
using DuelView.Compiled;
using DuelView.Runtime;
using DuelView.Spatial;
using DuelView.Autonomy;
using static DuelView.Core.DVKernel;
using static DuelView.Compiled.R;


// --- Source: SessionCases.cs ---

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
// --- Source: SessionCaseMindEvidence.cs ---

public sealed partial class DvSession
{
    // Read the actor's retained cognition only. A declaration never grants access
    // to physical truth, another mind, or an unexecuted action's predicted result.
    private IEnumerable<CaseAxiom> ReadCaseMindEvidence(string source, string actorId)
    {
        var actor = R.Items(Field(state, "actors", new List<object>()))
            .Single(item => R.Text(Field(item, "id", "")) == actorId);
        return ReadCaseMindEvidence(SnapshotCase(caseId: null, source).Data, actorId, Field(actor, "mind", new List<object>()));
    }

    private IEnumerable<CaseAxiom> ReadCaseMindEvidence(JsonElement data, string actorId, object mind)
        => ReadCaseMindInputs(data, actorId, mind).Evidence;

    private sealed record CaseMindInputs(IReadOnlyList<CaseAxiom> Evidence, string Signature);
    private CaseMindInputs ReadCaseMindInputs(JsonElement data, string actorId, object mind)
    {
        if (!data.TryGetProperty("case_mind_evidence", out var mappings)) return new(Array.Empty<CaseAxiom>(), "no-mappings");
        var result = new List<CaseAxiom>();
        var inputs = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapping in mappings.GetProperty("mappings").EnumerateArray())
        {
            if (mapping.GetProperty("actor").GetString() != actorId) continue;
            var id = mapping.GetProperty("id").GetString() ?? throw new InvalidDataException("Missing evidence mapping id.");
            if (!ids.Add(id)) throw new InvalidDataException("Duplicate evidence mapping id: " + id);
            var reference = mapping.GetProperty("reference");
            var subject = reference.TryGetProperty("subject", out var declaredSubject) ? declaredSubject.GetString() : actorId;
            if (subject != actorId) throw new InvalidDataException("Case evidence cannot read a different subject.");
            var dvRef = DVKernel.F_dv_ref(reference.GetProperty("object").GetString()!,
                reference.GetProperty("facet").GetString()!, reference.GetProperty("field").GetString()!);
            var cell = DVKernel.F_dv_read_query(mind, dvRef);
            var provenance = JsonSerializer.Serialize(converter.FromDv(cell));
            // Include disputed/unknown states and provenance in invalidation, even when no axiom is granted.
            inputs.Add(id + ":" + provenance);
            if (!R.Bool(DVKernel.F_dv_uncontested(cell))) continue;
            if (!R.Bool(R.eq(Field(cell, "value", false), HostValues.ToDv(mapping.GetProperty("equals"))))) continue;
            // Preserve the original cognitive provenance in the proof source.
            // Disputed/unknown cells are deliberately not upgraded to facts.
            var literal = mapping.GetProperty("literal").Deserialize<CaseLiteral>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Invalid evidence literal.");
            result.Add(new CaseAxiom("mind:" + id, literal, "actor-mind:" + actorId + ":" + provenance));
        }
        return new(result.AsReadOnly(), JsonSerializer.Serialize(inputs));
    }
}
// --- Source: SessionCaseQuestions.cs ---

public sealed partial class DvSession
{
    /// <summary>Only authored questions for this viewer and current chapter enter the ordinary UI.</summary>
    public object CaseQuestions()
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        var chapter = Convert.ToInt32(CompactStatus().GetValueOrDefault("chapter"));
        var questions = new List<object>();
        foreach (var pair in CaseSnapshotSources())
        {
            var declared = SnapshotCase(pair.Key);
            if (!declared.Data.TryGetProperty("case_questions", out var section)) continue;
            var entries = section.GetProperty("entries").EnumerateArray().Where(entry =>
                entry.GetProperty("actor").GetString() == Viewer &&
                (!entry.TryGetProperty("chapter", out var requiredChapter) || requiredChapter.GetInt32() == chapter)).ToArray();
            if (entries.Length == 0) continue;
            var scope = declared.ActorTheory.Scopes[Viewer];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var id = entry.GetProperty("id").GetString();
                var title = entry.GetProperty("title").GetString();
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || string.IsNullOrWhiteSpace(title))
                    throw new InvalidDataException("Question identifiers and titles must be nonempty and unique within each case.");
                var goal = entry.GetProperty("goal").Deserialize<CaseLiteral>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (goal is null || !CaseProofChecker.ValidLiteral(goal, true) || !scope.Theorems.Any(t => t.Conclusion.Key == goal!.Key))
                    throw new InvalidDataException("Player question must name a declared query in the viewer's theory.");
                var seconds = entry.TryGetProperty("reasoning_seconds", out var cost) ? cost.GetDouble() : 0d;
                if (!double.IsFinite(seconds) || seconds < 0) throw new InvalidDataException("Invalid question reasoning_seconds.");
                questions.Add(new { case_id = pair.Key, query_id = id, label = title, goal, reasoning_seconds = seconds });
            }
        }
        return new { questions, viewer = Viewer, chapter };
    }
}
// --- Source: SessionCaseRequests.cs ---

public sealed partial class DvSession
{
    /// <summary>Shared production request boundary for CLI and Godot authoring clients.</summary>
    public object CaseReasoningRequest(string json)
    {
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        using var document = JsonDocument.Parse(json);
        var request = document.RootElement;
        if (request.ValueKind != JsonValueKind.Object) throw new ArgumentException("Case request must be an object.");
        string Text(string key) => request.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(field.GetString()) ? field.GetString()! : throw new ArgumentException("Missing or invalid " + key);
        bool Is(string key, string value) => request.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String && field.GetString() == value;
        long Integer(string key, long fallback)
        {
            if (!request.TryGetProperty(key, out var field)) return fallback;
            // Godot's JSON bridge emits integral values as floating-point JSON numbers.
            var value = field.GetDouble();
            if (!double.IsFinite(value) || value != Math.Truncate(value) || value < -9007199254740991d || value > 9007199254740991d)
                throw new ArgumentException("Expected an exact integer: " + key);
            return checked((long)value);
        }
        int Setting(string key, int fallback)
        {
            var value = Integer(key, fallback);
            if (value < int.MinValue || value > int.MaxValue) throw new ArgumentException("Setting is outside integer range: " + key);
            return (int)value;
        }
        var operation = Text("operation");
        if (operation == "sim4_plan" || operation == "strategy" && Is("planner", "sim4"))
            return PlanSim4(Setting("depth", 1), Setting("beam_width", 32), Setting("expansion_budget", 4096));
        if (operation == "sim4_execute") return ExecuteSim4(Text("plan_id"), Integer("state_version", -1));
        if (operation == "info") return request.TryGetProperty("audit", out var audit) && audit.ValueKind == JsonValueKind.True
            ? AuditCaseReasoningInfo() : CaseReasoningInfo;
        if (operation == "questions") return CaseQuestions();
        if (operation == "scene") return CaseScene();
        if (operation == "seal_scene") return SealCaseScene(Text("case_id"), Text("query_id"));
        if (operation == "review" && Is("view", "questions")) return CaseQuestions();
        // Raw actor evidence is an authoring operation. Ordinary player input cannot fabricate observations.
        if (operation is "audit" or "design" or "infer_design" or "check_design" or "prove_design" or "check_design_goal" or "travel" or "add_evidence")
            if (!DevMode) throw new InvalidOperationException("Case authoring operations require developer mode.");
        if (operation == "audit") return AuditCaseLibrary();
        var caseId = Text("case_id");
        return operation switch
        {
            "infer" when request.TryGetProperty("goal", out _) => ProveCaseActor(caseId, Viewer, Read<CaseLiteral>(request, "goal")),
            "infer" => InferCaseActor(caseId, Viewer),
            "check" when Is("logic", "natural") => CheckNaturalDeductionActor(caseId, Text("goal"), Text("proof")),
            "check" when request.TryGetProperty("goal", out _) => CheckCaseActorGoal(caseId, Viewer, Read<CaseProof>(request, "proof"), Read<CaseLiteral>(request, "goal")),
            "check" => CheckCaseActor(caseId, Viewer, Read<CaseProof>(request, "proof")),
            "prove" => ProveCaseActor(caseId, Viewer, Read<CaseLiteral>(request, "goal")),
            "check_goal" => CheckCaseActorGoal(caseId, Viewer, Read<CaseProof>(request, "proof"), Read<CaseLiteral>(request, "goal")),
            "check_natural" => CheckNaturalDeductionActor(caseId, Text("goal"), Text("proof")),
            "review" when Is("view", "strategy") => new { choices = ReviewCaseStrategy(caseId, Viewer) },
            "review" when Is("view", "travel") => EstimateActorTravelFromDv(caseId, Text("query_id")),
            "review" => new { candidates = ReviewCaseActor(caseId, Viewer) },
            "strategy" => new { choices = ReviewCaseStrategy(caseId, Viewer) },
            "travel_actor" => EstimateActorTravelFromDv(caseId, Text("query_id")),
            "design" => EvaluateCaseDesign(caseId, Text("scope")),
            "infer_design" => InferCaseDesign(caseId, Text("scope")),
            "check_design" => CheckCaseDesign(caseId, Text("scope"), Read<CaseProof>(request, "proof")),
            "prove_design" => ProveCaseDesign(caseId, Text("scope"), Read<CaseLiteral>(request, "goal")),
            "check_design_goal" => CheckCaseDesignGoal(caseId, Text("scope"), Read<CaseProof>(request, "proof"), Read<CaseLiteral>(request, "goal")),
            "travel" => EstimateCaseTravelFromDv(caseId, Text("query_id")),
            "add_evidence" => AddEvidence(),
            _ => throw new ArgumentException("Unknown case reasoning operation: " + operation)
        };
        object AddEvidence()
        {
            AddCaseActorEvidence(caseId, Viewer, Read<CaseAxiom>(request, "evidence"));
            return new { recorded = true, actor = Viewer, version = Version };
        }
    }

    private static T Read<T>(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Missing or invalid " + name);
        return field.Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ArgumentException("Invalid " + name);
    }
}
// --- Source: SessionCaseStrategy.cs ---

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
// --- Source: SessionCaseScene.cs ---

public sealed partial class DvSession
{
    /// <summary>Read-only cards from this viewer's theory; never imports objective evidence.</summary>
    public object CaseScene()
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        if (IsClosed || HasPending(state))
            throw new InvalidOperationException("Scene reasoning requires a committed action boundary.");
        var questions = JsonSerializer.SerializeToElement(CaseQuestions()).GetProperty("questions").Clone();
        var evidence = new List<object>();
        foreach (var caseId in questions.EnumerateArray().Select(q => q.GetProperty("case_id").GetString()!).Distinct(StringComparer.Ordinal))
        {
            var theory = ActorCase(caseId, Viewer);
            var scope = theory.Scopes[Viewer];
            foreach (var axiom in scope.Axioms)
                evidence.Add(new { case_id = caseId, id = axiom.Id, text = axiom.Source, literal = axiom.Literal });
        }
        return new { viewer = Viewer, version = Version, questions, evidence };
    }

    /// <summary>Commit one explicit cognitive attempt with its DV-declared world-time cost.
    /// Browsing cards is free. Repeating the same question against the same retained
    /// actor theory returns the saved attempt without charging or publishing twice.</summary>
    public object SealCaseScene(string caseId, string queryId)
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        var questions = JsonSerializer.SerializeToElement(CaseQuestions()).GetProperty("questions");
        var question = questions.EnumerateArray().Single(q => q.GetProperty("case_id").GetString() == caseId &&
            q.GetProperty("query_id").GetString() == queryId);
        if (IsActorInFlight(Viewer)) throw new InvalidOperationException("Finish the current activity before a cognitive submission.");
        var theory = ActorCase(caseId, Viewer); // Checks committed boundary and observer ownership.
        var theoryHash = DuelView.Core.CaseTheory.Fingerprint(theory.Scopes[Viewer]);
        var attempts = JsonSerializer.Deserialize<List<JsonElement>>((string)Field(state, "case_actor_attempts_json", "[]"))!;
        var previous = attempts.FirstOrDefault(r => r.GetProperty("actor").GetString() == Viewer &&
            r.GetProperty("case_id").GetString() == caseId && r.GetProperty("query_id").GetString() == queryId &&
            r.GetProperty("theory_hash").GetString() == theoryHash);
        if (previous.ValueKind != JsonValueKind.Undefined)
            return new { attempt = previous, repeated = true, charged_seconds = 0d, version = Version,
                sealed_record = previous.GetProperty("sealed_record") };
        var seconds = question.GetProperty("reasoning_seconds").GetDouble();
        if (!double.IsFinite(seconds) || seconds <= 0)
            throw new InvalidDataException("A cognitive submission needs a positive DV reasoning_seconds declaration.");
        var goal = question.GetProperty("goal").Deserialize<DuelView.Core.CaseLiteral>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var result = theory.Prove(Viewer, goal);
        JsonElement? seal = null;
        if (result.Status == "proved" && result.Support is not null)
        {
            var check = theory.CheckGoal(Viewer, result.Support.Certificate, goal);
            if (!check.Valid) throw new InvalidOperationException(string.Join("; ", check.Errors));
            seal = JsonSerializer.SerializeToElement(new { actor = Viewer, case_id = caseId, query_id = queryId,
                label = question.GetProperty("label").GetString(), certificate = result.Support.Certificate });
        }
        var before = R.Num(Field(Field(state, "physical", new List<object>()), "time", 0d));
        var attemptId = Guid.NewGuid().ToString("N");
        // CPU elapsed time is deliberately absent: the cost is authored simulation time.
        // Run on a snapshot; failures leave both the clock and the cognitive archive untouched.
        var updated = Run(() => DVKernel.F_dvs_advance_cognitive_time(R.snapshot(state), seconds / 60d, Viewer, attemptId));
        var after = R.Num(Field(Field(updated, "physical", new List<object>()), "time", 0d));
        var attempt = JsonSerializer.SerializeToElement(new { id = attemptId, actor = Viewer, case_id = caseId,
            query_id = queryId, theory_hash = theoryHash, status = result.Status, result,
            reasoning_seconds = seconds, clock_before = before, clock_after = after, sealed_record = seal });
        attempts.Add(attempt);
        updated = R.record_with(updated, "case_actor_attempts_json", JsonSerializer.Serialize(attempts));
        var records = JsonSerializer.Deserialize<List<JsonElement>>((string)Field(updated, "case_actor_seals_json", "[]"))!;
        records.RemoveAll(r => r.GetProperty("actor").GetString() == Viewer && r.GetProperty("case_id").GetString() == caseId &&
            r.GetProperty("query_id").GetString() == queryId);
        if (seal.HasValue) records.Add(seal.Value);
        updated = R.record_with(updated, "case_actor_seals_json", JsonSerializer.Serialize(records));
        BeginTransition();
        state = updated;
        RememberCommitted();
        return new { attempt, repeated = false, charged_seconds = seconds, sealed_record = seal, version = Version };
    }
}
// --- Source: SessionCaseNavigation.cs ---

public sealed partial class DvSession
{
    /// <summary>Estimates the supplied host navigation polyline; the caller is responsible for its provenance.</summary>
    public CaseTravel.Result EstimateNavigationPath(IReadOnlyList<CaseTravel.Point3> points,
        double minimumSpeed, double maximumSpeed, string source)
        => CaseTravel.FromNavigationPath(points, minimumSpeed, maximumSpeed, source);
}
// --- Source: SessionEvidence.cs ---

/// <summary>Archive provenance and cuts of the existing authoritative session.</summary>
public sealed partial class DvSession
{
    public IReadOnlyDictionary<string, object?> HostBuildInfo => HostBuildIdentity.Info;

    // Export only diagnostics already recorded by actual engine decisions.
    // Never run a new decision or reconstruct a missing earlier explanation.
    public IReadOnlyDictionary<string, object?> NavigationDiagnostics(long afterEventCursor)
    {
        using var valuesScope = R.UseValues(valueContext);
        var events = Events(state);
        if (afterEventCursor < 0 || afterEventCursor > events.Count)
            throw new ArgumentOutOfRangeException(nameof(afterEventCursor));
        var rows = new List<object?>();
        for (var index = (int)afterEventCursor; index < events.Count; index++)
        {
            var entry = events[index];
            var type = R.Text(Field(entry, "type", ""));
            if (!type.StartsWith("navigation_task_", StringComparison.Ordinal)) continue;
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["event_id"] = R.Text(Field(entry, "id", "")), ["type"] = type,
                ["actor"] = R.Text(Field(entry, "actor", "")),
                ["game_time"] = converter.FromDv(Field(entry, "time", 0d)),
                ["data"] = converter.FromDv(Field(entry, "data", new List<object>()))
            }));
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["scope"] = "actual-engine-events; developer-diagnostic; not-player-knowledge",
            ["after_event_cursor"] = afterEventCursor, ["next_event_cursor"] = events.Count,
            ["events"] = rows.AsReadOnly()
        });
    }
    // Bounded pages of existing reception and decision events; the cursor is
    // advanced only through inspected events, so no evidence is silently lost.
    public IReadOnlyDictionary<string, object?> RequestDiagnostics(long afterEventCursor)
    {
        using var valuesScope = R.UseValues(valueContext);
        var events = Events(state);
        if (afterEventCursor < 0 || afterEventCursor > events.Count)
            throw new ArgumentOutOfRangeException(nameof(afterEventCursor));
        const int pageSize = 200;
        var rows = new List<object?>();
        var sourceActors = new Dictionary<string, string>(StringComparer.Ordinal);
        var byId = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var entry in events)
        {
            byId[R.Text(Field(entry, "id", ""))] = entry;
            if (R.Text(Field(entry, "type", "")) == "speech_literal")
                sourceActors[R.Text(Field(entry, "id", ""))] = R.Text(Field(entry, "actor", ""));
        }
        var next = (int)afterEventCursor;
        for (; next < events.Count && rows.Count < pageSize; next++)
        {
            var entry = events[next];
            var type = R.Text(Field(entry, "type", ""));
            var data = Field(entry, "data", new List<object>());
            object payload;
            string sender = "";
            if (type == "listener_parse")
            {
                var speech = Field(data, "speech", new List<object>());
                var request = Field(speech, "request", false);
                var change = Field(speech, "request_change", false);
                var source = R.Text(Field(data, "source_event", ""));
                sender = sourceActors.TryGetValue(source, out var actualSender) ? actualSender : "";
                object sourceAction = false;
                if (byId.TryGetValue(source, out var literal))
                    foreach (object parent in R.Items(Field(literal, "parents", new List<object>())))
                        if (byId.TryGetValue(R.Text(parent), out var parentEvent) && R.Text(Field(parentEvent, "type", "")) == "action_execution")
                        { sourceAction = Field(Field(parentEvent, "data", new List<object>()), "action", false); break; }
                bool isReply = !R.Bool(R.eq(sourceAction, false)) && R.Bool(DVKernel.F_dv_has_key(sourceAction, "response_topic"));
                if (R.Bool(R.eq(request, false)) && R.Bool(R.eq(change, false)) && !isReply) continue;
                payload = new List<object> { DVKernel.F_dv_pair("source_event", source), DVKernel.F_dv_pair("request", request), DVKernel.F_dv_pair("request_change", change),
                    DVKernel.F_dv_pair("action_id", R.Bool(R.eq(sourceAction, false)) ? "" : Field(sourceAction, "id", "")),
                    DVKernel.F_dv_pair("literal", isReply ? Field(data, "literal", false) : false),
                    DVKernel.F_dv_pair("proposal_decision", isReply ? Field(data, "proposal_decision", false) : false) };
            }
            else if (type == "owned_decision")
            {
                var context = Field(data, "context", new List<object>());
                if (!R.TryItems(Field(context, "requests", new List<object>()), out var requests) || requests.Count == 0) continue;
                payload = data;
            }
            else if (type == "action_execution")
            {
                var action = Field(data, "action", new List<object>());
                if (R.Bool(R.eq(Field(action, "owned_request_requirement", false), false))) continue;
                var receipt = Field(data, "receipt", new List<object>());
                var execution = Field(receipt, "execution", new List<object>());
                payload = new List<object> {
                    DVKernel.F_dv_pair("action_id", Field(action, "id", "")),
                    DVKernel.F_dv_pair("requirement", Field(action, "owned_request_requirement", false)),
                    DVKernel.F_dv_pair("status", Field(execution, "status", "")),
                    DVKernel.F_dv_pair("parents", Field(entry, "parents", new List<object>())) };
            }
            else continue;
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["event_id"] = R.Text(Field(entry, "id", "")), ["type"] = type,
                ["actor"] = R.Text(Field(entry, "actor", "")), ["sender"] = sender ?? "",
                ["game_time"] = converter.FromDv(Field(entry, "time", 0d)),
                ["data"] = converter.FromDv(payload)
            }));
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["scope"] = "actual-engine-events; developer-diagnostic; not-player-knowledge",
            ["after_event_cursor"] = afterEventCursor, ["next_event_cursor"] = next,
            ["page_limit"] = pageSize, ["has_more"] = next < events.Count, ["events"] = rows.AsReadOnly()
        });
    }
    private long selectedCount, scheduledCount, completedCount;
    private long durableSelectedCount, durableScheduledCount, durableCompletedCount, durableVersion;
    private string executionHost = "unspecified", executionAdapter = "unspecified", sourceRevision = "";
    private string shutdownReason = "";
    private IReadOnlyDictionary<string, object?> evidenceLifecycle = HostValues.Empty;
    private string planningDiagnosticEvent = "";
    private IReadOnlyDictionary<string, object?> planningDiagnostic = HostValues.Empty;
    private readonly Dictionary<string, HashSet<string>> coordinationDiagnosticContexts = new(StringComparer.Ordinal);

    // The automatic CLI needs clocks and the existing chapter settlement, not
    // a scene projection or a copy of every character's cognition/evidence.
    public IReadOnlyDictionary<string, object?> CompactStatus()
    {
        using var valuesScope = R.UseValues(valueContext);
        var active = Active;
        var sessionStatus = R.Text(Run(() => F_dvs_session_status(R.snapshot(state)), speculative: true));
        var summary = Run(() => CampaignRuntime.F_dvg_chapter_summary(R.snapshot(state)), speculative: true);
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["version"] = Version,
            ["host_process_id"] = Environment.ProcessId,
            ["host_build_hash"] = HostBuildIdentity.Hash,
            ["chapter"] = converter.FromDv(Field(Field(state, "story", new List<object>()), "chapter", 1d)),
            ["game_time"] = converter.FromDv(Field(Field(state, "physical", new List<object>()), "time", 0d)),
            ["status"] = HasPending(state) ? "prepared" : R.Bool(Field(state, "awaiting_player", false)) ? "awaiting_input" : "idle",
            ["session_status"] = sessionStatus, ["active"] = active,
            ["stop_reason"] = active ? "" : sessionStatus,
            ["executed"] = converter.FromDv(Field(state, "executed", 0d)),
            ["limit"] = converter.FromDv(Field(state, "limit", 0d)),
            ["run_start"] = converter.FromDv(Field(state, "run_start", 0d)),
            ["horizon"] = converter.FromDv(Field(state, "horizon", 0d)),
            ["chapter_summary"] = CompactSummaryValue(summary),
            ["completed_chapters"] = converter.FromDv(Field(Field(state, "story", new List<object>()), "completed_chapters", new List<object>()))
        });
    }

    private object? CompactSummaryValue(object value)
    {
        if (!R.TryItems(value, out var items)) return converter.FromDv(value);
        if (items.Count > 0 && items.All(item => R.TryItems(item, out var pair) && pair.Count == 2 && pair[0] is string))
        {
            var cell = items.Any(item => (string)R.Items(item)[0] == "known") &&
                       items.Any(item => (string)R.Items(item)[0] == "conflicted");
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var pair = R.Items(item);
                var key = (string)pair[0];
                // Preserve unknown and disputed values explicitly. Their full
                // provenance remains in the authoritative session, not this log.
                if (cell && (key is "claims" or "basis" or "confidence" or "time")) continue;
                result[key] = CompactSummaryValue(pair[1]);
            }
            return HostValues.Freeze(result);
        }
        return Array.AsReadOnly(items.Select(CompactSummaryValue).ToArray());
    }

    public IReadOnlyDictionary<string, object?> ExecutionDiagnostics()
    {
        using var valuesScope = R.UseValues(valueContext);
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["status"] = R.Text(Field(state, "status", "")),
            ["executed"] = converter.FromDv(Field(state, "executed", 0)),
            ["limit"] = converter.FromDv(Field(state, "limit", 0)),
            ["game_time"] = converter.FromDv(Field(Field(state, "physical", new List<object>()), "time", 0)),
            ["horizon"] = converter.FromDv(Field(state, "horizon", 0))
        });
    }

    // Read the actual committed selection, without simulating another choice
    // or exposing this diagnostic through the player's subjective projection.
    public IReadOnlyDictionary<string, object?> PlanningDiagnostics()
    {
        using var valuesScope = R.UseValues(valueContext);
        var events = Events(state);
        for (var i = events.Count - 1; i >= 0; i--)
        {
            var entry = events[i];
            if (R.Text(Field(entry, "type", "")) != "action_selected") continue;
            var id = R.Text(Field(entry, "id", ""));
            if (id == planningDiagnosticEvent) return planningDiagnostic;
            planningDiagnosticEvent = id;
            var data = Field(entry, "data", new List<object>());
            planningDiagnostic = HostValues.Freeze(new Dictionary<string, object?>
            {
                ["event"] = id,
                ["actor"] = R.Text(Field(entry, "actor", "")),
                ["game_time"] = converter.FromDv(Field(entry, "time", 0)),
                ["action"] = R.Text(Field(Field(data, "action", new List<object>()), "id", "")),
                ["score"] = converter.FromDv(Field(data, "score", 0)),
                ["strategy"] = R.Text(Field(data, "planning_strategy", "")),
                ["stop"] = R.Text(Field(data, "planning_stop", "")),
                ["plans"] = converter.FromDv(Field(data, "plans", new List<object>()))
            });
            return planningDiagnostic;
        }
        return HostValues.Empty;
    }

    // Temporary, opt-in receipt diagnostics. This reads the prepared actor and
    // the selection event that actually scheduled this pending action. It does
    // not refresh content, choose an action, forecast, or recover missing scores.
    public IReadOnlyDictionary<string, object?> PreparedListeningDiagnostics()
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        var pending = Field(state, "pending", false);
        if (pending is false || R.Text(Field(pending, "id", "")) != "detective" ||
            R.Num(Field(Field(state, "story", new List<object>()), "chapter", 0d)) != 1d)
            return HostValues.Empty;
        var actor = R.snapshot(Field(pending, "actor", new List<object>()));
        var action = Field(pending, "action", new List<object>());
        var actionId = R.Text(Field(action, "id", ""));
        var completed = R.Items(Field(actor, "completed_actions", new List<object>()));
        var lastCompleted = completed.Count > 0 ? R.Text(completed[^1]) : "";
        var intention = Field(actor, "c1_reply_wait_intention", new List<object>());
        static bool Request(string id) => id is "detective.talk.slate_code" or "detective.talk.cups_known";
        if (!Request(actionId) && !Request(lastCompleted) && actionId != "detective.wait.reply" &&
            !R.Bool(Field(intention, "active", false))) return HostValues.Empty;

        var mind = R.snapshot(Field(actor, "mind", new List<object>()));
        var row = R.Items(Field(intention, "row", new List<object>()));
        var requestId = Request(lastCompleted) ? lastCompleted : Request(actionId) ? actionId :
            row.Count > 0 ? R.Text(row[0]) : "";
        var requestContext = Field(Field(actor, "request_contexts", new List<object>()), requestId, false);
        var declaredRequest = Field(Field(action, "speech", new List<object>()), "request", new List<object>());
        var target = requestContext is not false ? R.Text(Field(requestContext, "target", "")) :
            row.Count > 1 ? R.Text(row[1]) : R.Text(Field(declaredRequest, "target", ""));
        var visibleTarget = Field(Field(actor, "visible_context", new List<object>()), target, new List<object>());
        var waitAction = R.Items(Field(actor, "actions", new List<object>()))
            .FirstOrDefault(item => R.Text(Field(item, "id", "")) == "detective.wait.reply");
        var goals = R.Items(Field(Field(actor, "policy", new List<object>()), "goals", new List<object>()))
            .Where(item => R.Bool(Field(item, "c1_reply_wait", false))).ToArray();

        object? Cell(object reference)
        {
            if (reference is false) return "missing-reference";
            var cell = F_dv_read_query(mind, reference);
            return DiagnosticFields(cell, "known", "value", "conflicted");
        }
        string Truth(object proposition) => proposition is false ? "missing" : R.Text(F_dv_truth(mind, proposition));
        var matchingReplies = new List<object?>();
        var receiptStart = (int)R.Num(Field(intention, "receipt_start", 0d));
        var topic = row.Count > 2 ? R.Text(row[2]) : "";
        var replies = R.Items(Field(actor, "received_speeches", new List<object>()));
        for (var index = Math.Max(0, receiptStart); index < replies.Count && matchingReplies.Count < 2; index++)
        {
            var receipt = replies[index];
            var replyId = R.Text(Field(receipt, "action_id", ""));
            if (R.Text(Field(receipt, "speaker", "")) == target &&
                (replyId == target + ".reply." + topic || replyId == target + ".reply.unknown." + topic))
                matchingReplies.Add(DiagnosticFields(receipt, "speaker", "action_id", "parse_event", "source_event"));
        }
        var selection = PreparedSelectionEvent(pending);
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["phase"] = "prepared-before-complete",
            ["version"] = Version,
            ["mind_time"] = converter.FromDv(Field(mind, "time", false)),
            ["mind_revision"] = converter.FromDv(Field(mind, "revision", false)),
            ["decision_time"] = converter.FromDv(Field(actor, "decision_time", false)),
            ["goal_refresh"] = DiagnosticFields(actor, "campaign_goals_revision", "campaign_goals_time", "campaign_goals_chapter"),
            ["last_completed"] = lastCompleted,
            ["last_action_event"] = R.Text(Field(actor, "last_action_event", "")),
            ["request_id"] = requestId,
            ["request_context"] = requestContext is false ? "missing" :
                DiagnosticFields(requestContext, "target", "topic", "location", "presence_event"),
            ["intention"] = DiagnosticFields(intention, "active", "request_event", "intent_started_at", "until", "room", "receipt_start"),
            ["position"] = Cell(Field(actor, "location_ref", false)),
            ["target"] = target,
            ["visible_target_location"] = converter.FromDv(Field(visibleTarget, "location", false)),
            ["known_target_location"] = target.Length == 0 ? "missing-target" : Cell(F_dv_ref(target, "state", "location")),
            ["wait_elapsed"] = Cell(F_dv_ref("night.wait.detective", "state", "elapsed")),
            ["open_words"] = Cell(F_dv_ref("question", "state", "open_words")),
            ["open_trust"] = Cell(F_dv_ref("question", "state", "open_trust")),
            ["requires_evidence"] = Cell(F_dv_ref("question", "state", "requires_evidence")),
            ["received_reply"] = matchingReplies.AsReadOnly(),
            ["wait_candidate"] = waitAction is not null,
            ["prepared_precondition"] = waitAction is null ? "missing-candidate" : Truth(Field(waitAction, "precondition", false)),
            ["wait_goal_count"] = goals.Length,
            ["prepared_goals"] = Array.AsReadOnly(goals.Take(2).Select(goal => (object?)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["available"] = Truth(Field(goal, "available_when", false)),
                ["satisfied"] = Truth(Field(goal, "proposition", false)),
                ["basis"] = converter.FromDv(Field(goal, "basis", new List<object>())),
                ["weight"] = converter.FromDv(Field(goal, "weight", 1d))
            })).ToArray()),
            ["recorded_selection"] = SelectionDiagnostic(selection, actionId),
            ["recorded_wait_score"] = PlanDiagnostic(selection, "detective.wait.reply")
        });
    }

    private IReadOnlyDictionary<string, object?> DiagnosticFields(object value, params string[] keys)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in keys)
            if (R.Num(R.record_index(value, key)) >= 0) result[key] = converter.FromDv(Field(value, key, false));
        return HostValues.Freeze(result);
    }

    public IReadOnlyDictionary<string, object?> PreparedCoordinationDiagnostics()
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        var pending = Field(state, "pending", false);
        if (pending is false || R.Num(Field(Field(state, "story", new List<object>()), "chapter", 0d)) != 2d)
            return HostValues.Empty;
        var actorId = R.Text(Field(pending, "id", ""));
        if (actorId is not ("detective" or "moran" or "elena" or "selma" or "ruan")) return HostValues.Empty;
        if (!coordinationDiagnosticContexts.TryGetValue(actorId, out var seen))
            coordinationDiagnosticContexts[actorId] = seen = new HashSet<string>(StringComparer.Ordinal);
        if (seen.Count >= 12) return HostValues.Empty;
        var actor = R.snapshot(Field(pending, "actor", new List<object>()));
        var mind = R.snapshot(Field(actor, "mind", new List<object>()));
        var actionId = R.Text(Field(Field(pending, "action", new List<object>()), "id", ""));
        static bool Invitation(string id) => id.Contains(".c2.astral.invite.", StringComparison.Ordinal) ||
            id.Contains(".c2.safety.invite.", StringComparison.Ordinal);
        var invitations = R.Items(Field(actor, "received_speeches", new List<object>()))
            .Where(item => Invitation(R.Text(Field(item, "action_id", "")))).ToArray();
        var sent = R.Items(Field(actor, "completed_actions", new List<object>())).Where(item => Invitation(R.Text(item))).ToArray();
        if (invitations.Length == 0 && sent.Length == 0 && !Invitation(actionId)) return HostValues.Empty;
        string Truth(object proposition) => proposition is false ? "missing" : R.Text(F_dv_truth(mind, proposition));
        var position = F_dv_read_query(mind, Field(actor, "location_ref", new List<object>()));
        var room = R.Text(Field(position, "value", ""));
        var goals = R.Items(Field(Field(actor, "policy", new List<object>()), "goals", new List<object>()))
            .Where(goal => R.Num(R.record_index(goal, "navigation_intention")) >= 0 ||
                R.Items(Field(goal, "basis", new List<object>())).Any(basis => R.TryItems(basis, out _) &&
                    R.Text(Field(basis, "id", "")) == "actual-observed-contact-coordination-20261028")).ToArray();
        var goalRows = goals.Take(5).Select(goal => (object?)HostValues.Freeze(new Dictionary<string, object?>
        {
            ["kind"] = R.Text(Field(goal, "kind", "")),
            ["weight"] = converter.FromDv(Field(goal, "weight", 1d)),
            ["room"] = converter.FromDv(Field(goal, "navigation_room", Field(Field(goal, "proposition", new List<object>()), "value", false))),
            ["maintain_presence"] = converter.FromDv(Field(goal, "maintain_presence", false)),
            ["navigation_intention"] = converter.FromDv(Field(goal, "navigation_intention", false)),
            ["available"] = Truth(Field(goal, "available_when", false)),
            ["satisfied"] = Truth(Field(goal, "proposition", false))
        })).ToArray();
        var context = new Dictionary<string, object?>
        {
            ["position"] = DiagnosticFields(position, "known", "value", "conflicted"),
            ["action"] = actionId,
            ["invitation_count"] = invitations.Length,
            ["sent_count"] = sent.Length,
            ["goal_count"] = goals.Length,
            ["goals"] = Array.AsReadOnly(goalRows)
        };
        if (!seen.Add(System.Text.Json.JsonSerializer.Serialize(context))) return HostValues.Empty;
        var selection = PreparedSelectionEvent(pending);
        var plans = selection is false ? new List<object>() : Field(Field(selection, "data", new List<object>()), "plans", new List<object>());
        var top = R.Items(plans).OrderByDescending(plan => R.Num(Field(plan, "score", 0d))).Take(3)
            .Select(plan => (object?)DiagnosticFields(plan, "path", "score", "mind_revision", "evaluated_at_game_time")).ToArray();
        var moves = R.Items(Field(actor, "actions", new List<object>()))
            .Where(item => R.Text(Field(Field(item, "travel", new List<object>()), "from", "")) == room)
            .Take(4).Select(item => (object?)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["action"] = R.Text(Field(item, "id", "")),
                ["to"] = converter.FromDv(Field(Field(item, "travel", new List<object>()), "to", false)),
                ["prepared_precondition"] = Truth(Field(item, "precondition", false)),
                ["recorded_plan"] = PlanDiagnostic(selection, R.Text(Field(item, "id", "")))
            })).ToArray();
        context["phase"] = "prepared-before-complete";
        context["context_index"] = seen.Count;
        context["mind_time"] = converter.FromDv(Field(mind, "time", false));
        context["mind_revision"] = converter.FromDv(Field(mind, "revision", false));
        context["goal_refresh"] = DiagnosticFields(actor, "campaign_goals_revision", "campaign_goals_time", "campaign_goals_chapter");
        context["last_invitation"] = invitations.Length == 0 ? null :
            DiagnosticFields(invitations[^1], "speaker", "action_id", "parse_event", "source_event");
        context["recorded_selection"] = SelectionDiagnostic(selection, actionId);
        context["recorded_plans_status"] = selection is false ? "missing-selection" :
            R.Num(R.record_index(Field(selection, "data", new List<object>()), "plans")) < 0 ? "missing-plans" : "recorded";
        context["recorded_top_three"] = Array.AsReadOnly(top);
        context["outgoing_moves"] = Array.AsReadOnly(moves);
        return HostValues.Freeze(context);
    }

    // Developer evidence only. Read content-declared validation inputs after a
    // real completion; this is never a source of character knowledge or choice.
    public IReadOnlyDictionary<string, object?> ContentDecisionDiagnostics(IReadOnlyDictionary<string, object?> receipt)
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        static object Optional(object value, string key, object fallback) =>
            R.TryItems(value, out _) ? Field(value, key, fallback) : fallback;
        var actorId = HostValues.Text(receipt, "completed_actor");
        var actor = R.Items(Field(state, "actors", new List<object>())).FirstOrDefault(item => R.Text(Field(item, "id", "")) == actorId);
        if (actor is null) return HostValues.Empty;
        var selectedId = HostValues.Text(receipt, "completed_action");
        var actions = R.Items(Field(actor, "actions", new List<object>()));
        var selected = actions.FirstOrDefault(item => R.Text(Field(item, "id", "")) == selectedId);
        if (selected is null) return HostValues.Empty;
        var selectedRefresh = Field(selected, "content_refresh", false);
        if (R.Text(Field(selected, "kind", "")) != "inspect" && selectedRefresh is false && Field(selected, "owned_decision", false) is false)
            return HostValues.Empty;
        var declared = actions.Where(item => Optional(Optional(item, "content_refresh", false), "diagnostic_template", false) is string id && id.Length > 0).ToArray();
        if (declared.Length == 0) return HostValues.Empty;
        var mind = Field(actor, "mind", new List<object>());
        var rows = new List<object?>();
        var validations = new Dictionary<string, object?>(StringComparer.Ordinal);
        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal);
        var decisions = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var action in declared)
        {
            var refresh = Field(action, "content_refresh", false);
            var declaredRefs = Optional(refresh, "diagnostic_refs", false);
            var refs = R.TryItems(declaredRefs, out var referenceItems) ? referenceItems : Array.Empty<object>();
            var template = R.Text(Field(refresh, "diagnostic_template", ""));
            if (!validations.ContainsKey(template))
                validations[template] = converter.FromDv(DvValueTemplate.Instantiate(template, actor));
            foreach (var reference in refs)
            {
                var key = R.Text(F_dv_ref_lookup_key(reference));
                if (evidence.ContainsKey(key)) continue;
                var cell = F_dv_read(mind, reference);
                evidence[key] = HostValues.Freeze(new Dictionary<string, object?>
                {
                    ["ref"] = converter.FromDv(reference),
                    ["cell"] = DiagnosticFields(cell, "known", "value", "conflicted"),
                    ["sources"] = Array.AsReadOnly(R.Items(Field(cell, "claims", new List<object>())).Select(claim =>
                        (object?)DiagnosticFields(claim, "source", "time", "origin", "id", "dependency")).ToArray())
                });
            }
            var declaration = Optional(action, "owned_decision", false);
            var decisionId = R.Text(Optional(declaration, "id", ""));
            if (decisionId.Length > 0 && !decisions.ContainsKey(decisionId))
                decisions[decisionId] = DiagnosticFields(Optional(Field(actor, "owned_decisions", new List<object>()), decisionId, new List<object>()),
                    "stance", "selection_event", "execution_event", "previous_event", "event", "time");
            var request = F_dvs_pending_request(actor, action);
            var requester = R.Text(Optional(request, "requester", ""));
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["action"] = R.Text(Field(action, "id", "")),
                ["precondition_truth"] = converter.FromDv(F_dv_truth(mind, Field(action, "precondition", F_dv_true()))),
                ["validation_template"] = template,
                ["owned_decision_ready"] = converter.FromDv(F_dvp_owned_decision_ready(actor, action, mind)),
                ["decision_id"] = decisionId,
                ["request"] = DiagnosticFields(request, "requester", "parse_event", "source_event", "topic"),
                ["request_context_available"] = requester.Length > 0 && F_dvs_request_context(actor, action, requester) is not false
            }));
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["scope"] = "developer-detailed-log-only; observational; not player narrative",
            ["phase"] = "after-actual-completion", ["actor"] = actorId,
            ["mind_time"] = converter.FromDv(Field(mind, "time", false)),
            ["owned_decisions"] = HostValues.Freeze(decisions),
            ["visible_context"] = converter.FromDv(Field(actor, "visible_context", new List<object>())),
            ["contexts"] = converter.FromDv(R.Items(actor).Where(pair => R.TryItems(pair, out var parts) && parts.Count == 2 && parts[0] is string name && name.EndsWith("_evidence_context", StringComparison.Ordinal)).ToList()),
            ["received_speeches_tail"] = Array.AsReadOnly(R.Items(Field(actor, "received_speeches", new List<object>())).TakeLast(4).Select(item =>
                (object?)DiagnosticFields(item, "speaker", "action_id", "parse_event", "source_event")).ToArray()),
            ["validations"] = HostValues.Freeze(validations),
            ["evidence"] = Array.AsReadOnly(evidence.Values.ToArray()),
            ["candidates"] = Array.AsReadOnly(rows.ToArray())
        });
    }

    private object PreparedSelectionEvent(object pending)
    {
        var events = Field(state, "events", new List<object>());
        var actionId = R.Text(Field(Field(pending, "action", new List<object>()), "id", ""));
        var actorId = R.Text(Field(pending, "id", ""));
        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue(R.Text(Field(pending, "parent", "")));
        while (queue.Count > 0 && visited.Count < 32)
        {
            var id = queue.Dequeue();
            if (id.Length == 0 || !visited.Add(id)) continue;
            var entry = F_dvs_find_event_by_id(events, id);
            if (entry is false) continue;
            var data = Field(entry, "data", new List<object>());
            if (R.Text(Field(entry, "type", "")) == "action_selected" &&
                R.Text(Field(entry, "actor", "")) == actorId &&
                R.Text(Field(Field(data, "action", new List<object>()), "id", "")) == actionId)
                return entry;
            foreach (var parent in R.Items(Field(entry, "parents", new List<object>()))) queue.Enqueue(R.Text(parent));
        }
        return false;
    }

    private IReadOnlyDictionary<string, object?> SelectionDiagnostic(object selection, string actionId)
    {
        if (selection is false) return HostValues.Freeze(new Dictionary<string, object?> { ["status"] = "missing-linked-action-selected" });
        var data = Field(selection, "data", new List<object>());
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["event"] = R.Text(Field(selection, "id", "")),
            ["game_time"] = converter.FromDv(Field(selection, "time", false)),
            ["action"] = actionId,
            ["strategy"] = R.Text(Field(data, "planning_strategy", "")),
            ["score"] = converter.FromDv(Field(data, "score", false)),
            ["plan"] = PlanDiagnostic(selection, actionId)
        });
    }

    private IReadOnlyDictionary<string, object?> PlanDiagnostic(object selection, string actionId)
    {
        if (selection is not false)
        {
            var data = Field(selection, "data", new List<object>());
            foreach (var plan in R.Items(Field(data, "plans", new List<object>())))
            {
                var path = R.Items(Field(plan, "path", new List<object>()));
                if (path.Count == 0 || R.Text(path[0]) != actionId) continue;
                var score = Field(Field(plan, "value", new List<object>()), "current_score", new List<object>());
                return HostValues.Freeze(new Dictionary<string, object?>
                {
                    ["status"] = "recorded",
                    ["score"] = converter.FromDv(Field(plan, "score", false)),
                    ["mind_revision"] = converter.FromDv(Field(plan, "mind_revision", false)),
                    ["evaluated_at_game_time"] = converter.FromDv(Field(plan, "evaluated_at_game_time", false)),
                    ["scope"] = R.Text(Field(plan, "score_scope", "")),
                    ["utility"] = DiagnosticFields(score, "action_value", "information_component", "progress_component", "time_penalty", "total", "components")
                });
            }
        }
        return HostValues.Freeze(new Dictionary<string, object?> { ["status"] = "missing-recorded-plan", ["action"] = actionId });
    }

    public bool PlayerControlled
    {
        get
        {
            using var valuesScope = R.UseValues(valueContext);
            return R.Text(Field(state, "player_actor", "")).Length > 0;
        }
    }

    public void SetExecutionContext(string host, string executor, string revision = "")
    {
        using var valuesScope = R.UseValues(valueContext);
        executionHost = host;
        executionAdapter = executor;
        sourceRevision = revision;
        SetPerformanceProvenance(Provenance());
    }

    private IReadOnlyDictionary<string, object?> Provenance()
    {
        var configuration = Field(Field(state, "story", new List<object>()), "run_config", new List<object>());
        var simulationMode = R.Text(Field(configuration, "simulation_mode", "unrecorded"));
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["host"] = executionHost, ["executor"] = executionAdapter,
            ["godot_execution"] = executionAdapter == "godot",
            ["controller_kind"] = performance.ControllerKind, ["viewer"] = Viewer,
            ["source_revision"] = sourceRevision.Length > 0 ? sourceRevision : null,
            ["assembly_mvid"] = typeof(CampaignRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["runtime"] = Environment.Version.ToString(), ["audit"] = audit, ["development"] = developer,
            ["content_hash"] = content.Hash, ["content_schema"] = ContentCatalog.Schema,
            ["simulation_mode"] = simulationMode,
            ["opt_storyflow"] = R.Bool(Field(configuration, "opt_storyflow", true)),
            ["opt_stateindex"] = R.Bool(Field(configuration, "opt_stateindex", true)),
            ["storyflow_semantics"] = "content-policy rewrite; similar intended effects, not trace equivalence",
            ["planning_mode"] = simulationMode,
            ["planning_mode_approximation"] = simulationMode == "approximate-v1" ? true : (bool?)null,
            ["planning_search_scope"] = simulationMode == "approximate-v1" ? "current admissible candidates; immediate utility; no action-tree search" : "unrecorded"
        });
    }

    private void ObserveLifecycle(object entry)
    {
        switch (R.Text(Field(entry, "type", "")))
        {
            case "action_selected": selectedCount++; break;
            case "action_scheduled": scheduledCount++; break;
            case "action_execution": completedCount++; break;
        }
    }

    private static string LastEventId(object value)
    {
        var events = Events(value);
        return events.Count > 0 ? R.Text(Field(events[^1], "id", "")) : "";
    }

    private IReadOnlyList<object?> ScheduledActions(object value)
    {
        var rows = new List<object?>();
        foreach (var actor in R.Items(Field(value, "actors", new List<object>())))
        {
            var queued = Field(actor, "scheduled_action", false);
            if (queued is false) continue;
            var action = Field(queued, "action", new List<object>());
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["actor"] = Field(actor, "id", ""), ["action_id"] = Field(action, "id", ""),
                ["label"] = Field(action, "label", ""), ["action_event"] = Field(queued, "action_event", ""),
                ["start"] = converter.FromDv(Field(queued, "start", false)),
                ["due"] = converter.FromDv(Field(queued, "due", false)), ["state"] = "scheduled"
            }));
        }
        var pending = Field(value, "pending", false);
        if (pending is not false)
        {
            var action = Field(pending, "action", new List<object>());
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["actor"] = Field(pending, "id", ""), ["action_id"] = Field(action, "id", ""),
                ["label"] = Field(action, "label", ""), ["action_event"] = Field(pending, "action_event", ""),
                ["start"] = converter.FromDv(Field(Field(pending, "quote", new List<object>()), "scheduled_start", false)),
                ["due"] = converter.FromDv(Field(pending, "time", false)), ["state"] = "awaiting-host-receipt"
            }));
        }
        return rows.AsReadOnly();
    }

    private static int ScheduledActionCount(object value)
    {
        var count = HasPending(value) ? 1 : 0;
        foreach (var actor in R.Items(Field(value, "actors", new List<object>())))
        {
            // Count only; full labels, timing and source rows remain in the
            // archive projection and are not rebuilt at every transition.
            if (Field(actor, "scheduled_action", false) is not false) count++;
        }
        return count;
    }

    private IReadOnlyDictionary<string, object?> EvidenceCut() => HostValues.Freeze(new Dictionary<string, object?>
    {
        ["trace_game_time"] = converter.FromDv(Field(Field(state, "physical", new List<object>()), "time", false)),
        ["durable_game_time"] = converter.FromDv(Field(Field(lastCommittedState, "physical", new List<object>()), "time", false)),
        ["trace_event"] = IsClosed ? LastEventId(closedTrace!) : LastEventId(state),
        ["durable_event"] = LastEventId(lastCommittedState),
        ["state_version"] = Version, ["durable_state_version"] = durableVersion,
        ["selected"] = selectedCount, ["durable_selected"] = durableSelectedCount,
        ["selections_not_in_save"] = selectedCount - durableSelectedCount,
        ["pending_host_receipt"] = HasPending(state), ["shutdown_reason"] = shutdownReason,
        ["decision_scope"] = "new-session-events; restored-history-excluded"
    });

    private void UpdateEvidenceLifecycle()
    {
        evidenceLifecycle = HostValues.Freeze(new Dictionary<string, object?>(EvidenceCut(), StringComparer.Ordinal)
        {
            ["scheduled"] = scheduledCount, ["completed"] = completedCount,
            ["pending"] = ScheduledActionCount(state),
            ["durable_scheduled"] = durableScheduledCount, ["durable_completed"] = durableCompletedCount,
            ["durable_pending"] = ScheduledActionCount(lastCommittedState)
        });
        SetPerformanceLifecycle(evidenceLifecycle);
    }

    private object DecorateTrace(object trace)
    {
        UpdateEvidenceLifecycle();
        var summary = PerformanceSummary();
        var result = R.record_with(trace, "provenance", converter.ToDv(Provenance()));
        result = R.record_with(result, "content_pack_dv", content.Source);
        result = R.record_with(result, "cut", converter.ToDv(EvidenceCut()));
        result = R.record_with(result, "lifecycle", converter.ToDv(evidenceLifecycle));
        result = R.record_with(result, "pending_actions", converter.ToDv(ScheduledActions(state)));
        result = R.record_with(result, "performance", converter.ToDv(summary));
        result = R.record_with(result, "controller_kind", performance.ControllerKind);
        if (closingPreview is not null && closingPreviewVersion == Version)
        {
            result = R.record_with(result, "closing_preview", converter.ToDv(closingPreview));
            result = R.record_with(result, "closing_preview_state_version", (double)closingPreviewVersion);
            result = R.record_with(result, "state_version", (double)Version);
        }
        return R.record_with(result, "scope", "DV production session; host=" + executionHost + "; executor=" + executionAdapter);
    }
}
// --- Source: SessionNaturalDeduction.cs ---

public sealed partial class DvSession
{
    /// <summary>Proof terms are untrusted input; their premises come only from the retained actor theory.</summary>
    public object CheckNaturalDeductionActor(string caseId, string goal, string proof)
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        if (IsClosed || HasPending(state))
            throw new InvalidOperationException("Natural deduction requires a committed action boundary.");
        if (Viewer == "objective") throw new InvalidOperationException("An objective theory is not an actor's knowledge.");
        var declared = SnapshotCase(caseId);
        var scopes = declared.Data.GetProperty("natural_deduction").GetProperty("scopes");
        var matches = scopes.EnumerateArray().Where(scope => scope.GetProperty("id").GetString() == Viewer).ToArray();
        if (matches.Length != 1) throw new ArgumentException("Exactly one natural deduction theory must be declared for the current viewer.");
        var scope = matches[0];
        var request = new Dictionary<string, object?>
        {
            ["scope"] = Viewer, ["goal"] = goal, ["proof"] = proof,
            ["axioms"] = scope.GetProperty("axioms").Clone(),
            ["theorems"] = scope.TryGetProperty("theorems", out var theorems) ? theorems.Clone() : Array.Empty<object>()
        };
        var result = NaturalDeduction.Check(JsonSerializer.SerializeToElement(request));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope.GetRawText())));
        return new { scope = Viewer, theory_hash = hash, result };
    }
}
