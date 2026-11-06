// @mechanism: signed_horn_reasoning
// @layer: core/reasoning
// @contracts: CaseTheory

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;

// --- Source: CaseTheory.cs ---
namespace DuelView.Core
{


/// <summary>A signed atom. Negation is explicit evidence, never failure to prove.</summary>
public sealed record CaseLiteral(string Predicate, IReadOnlyList<string> Args, bool Negative = false)
{
    [JsonIgnore] public string Key => JsonSerializer.Serialize(new { Predicate, Args, Negative });
    [JsonIgnore] public bool Ground => Args.All(a => !a.StartsWith('?'));
    [JsonIgnore] public CaseLiteral Opposite => this with { Negative = !Negative };
    public CaseLiteral Substitute(IReadOnlyDictionary<string, string> bindings) =>
        this with { Args = Array.AsReadOnly(Args.Select(a => a.StartsWith('?') && bindings.TryGetValue(a, out var v) ? v : a).ToArray()) };
}
public sealed record CaseAxiom(string Id, CaseLiteral Literal, string Source);
public sealed record CaseRule(string Id, IReadOnlyList<CaseLiteral> Premises, CaseLiteral Conclusion, string Source);
public sealed record CaseTheorem(string Id, CaseLiteral Conclusion, CaseProof? Proof = null);
public sealed record CaseScope(string Id, IReadOnlyList<CaseAxiom> Axioms, IReadOnlyList<CaseRule> Rules, IReadOnlyList<CaseTheorem> Theorems);
public sealed record CaseCandidate(string Id, string Scope, IReadOnlyList<CaseLiteral> Requires, IReadOnlyList<CaseLiteral> Excludes);
public sealed record CaseProofNode(string Id, string Kind, CaseLiteral Conclusion, string Reference,
    IReadOnlyList<string> Parents, IReadOnlyDictionary<string, string> Substitution);
public sealed record CaseProof(string Scope, IReadOnlyList<CaseProofNode> Nodes, string TheoryHash = "");
public sealed record CaseCheckResult(bool Valid, IReadOnlyList<string> Errors);
public sealed record CaseCandidateReview(string Id, string Status, IReadOnlyList<CaseLiteral> Missing,
    IReadOnlyList<CaseLiteral> Refutations, IReadOnlyList<CaseLiteral> Conflicts);
public sealed record CaseGoalProof(CaseProof Certificate, IReadOnlyList<string> Axioms, IReadOnlyList<string> Rules);
public sealed record CaseGoalResult(string Scope, CaseLiteral Goal, string Status, CaseGoalProof? Support, CaseGoalProof? Refutation);

/// <summary>Finite, function-free signed Horn logic, not dependent type theory or full Lean.
/// Each scope is a separate theory: objective evidence is never imported into actor knowledge implicitly.</summary>
public sealed class CaseTheory
{
    public IReadOnlyDictionary<string, CaseScope> Scopes { get; }
    public IReadOnlyList<CaseCandidate> Candidates { get; }
    private CaseTheory(Dictionary<string, CaseScope> scopes, List<CaseCandidate> candidates)
    { Scopes = new ReadOnlyDictionary<string, CaseScope>(scopes); Candidates = candidates.AsReadOnly(); }

    public static CaseTheory Load(string path) => Parse(File.ReadAllText(path));
    public static CaseTheory Parse(string source)
    {
        return FromData(DvScriptReader.Parse(source).GetProperty("data").GetProperty("case_theory"));
    }
    public static CaseTheory FromData(JsonElement root)
    {
        if (root.TryGetProperty("script", out var script))
        {
            if (root.TryGetProperty("scopes", out _)) throw new FormatException("Use either script or scopes, not both");
            root = CaseProofScript.Elaborate(script.GetString() ?? "", root);
        }
        var scopes = new Dictionary<string, CaseScope>(StringComparer.Ordinal);
        foreach (var s in root.GetProperty("scopes").EnumerateArray())
        {
            var id = Text(s, "id");
            var axioms = Array(s, "axioms").Select(a => new CaseAxiom(Text(a, "id"), Literal(a.GetProperty("literal"), true), Text(a, "source"))).ToArray();
            var rules = Array(s, "rules").Select(r => new CaseRule(Text(r, "id"), System.Array.AsReadOnly(Array(r, "premises").Select(x => Literal(x, false)).ToArray()), Literal(r.GetProperty("conclusion"), false), Text(r, "source"))).ToArray();
            var theorems = Array(s, "theorems").Select(t => new CaseTheorem(Text(t, "id"), Literal(t.GetProperty("conclusion"), true))).ToArray();
            Unique(axioms.Select(a => a.Id).Concat(rules.Select(r => r.Id)).Concat(theorems.Select(t => t.Id)), "declaration");
            foreach (var rule in rules)
            {
                if (rule.Premises.Count > 64) throw new FormatException("Rule premise budget exceeded: " + rule.Id);
                var bound = rule.Premises.SelectMany(p => p.Args).Where(a => a.StartsWith('?')).ToHashSet(StringComparer.Ordinal);
                if (rule.Conclusion.Args.Any(a => a.StartsWith('?') && !bound.Contains(a))) throw new FormatException("Unsafe rule: " + rule.Id);
            }
            var scope = new CaseScope(id, System.Array.AsReadOnly(axioms), System.Array.AsReadOnly(rules), System.Array.AsReadOnly(theorems));
            var theoryCheck = CaseProofChecker.ValidateTheory(scope);
            if (!theoryCheck.Valid) throw new FormatException(string.Join("; ", theoryCheck.Errors));
            var theoremData = Array(s, "theorems").ToArray();
            for (var i = 0; i < theorems.Length; i++)
            {
                if (!theoremData[i].TryGetProperty("proof", out var proofData)) continue;
                var nodesData = proofData.ValueKind == JsonValueKind.Array ? proofData : proofData.GetProperty("nodes");
                var nodes = nodesData.EnumerateArray().Select(n => new CaseProofNode(Text(n, "id"), Text(n, "kind"), Literal(n.GetProperty("conclusion"), true),
                    n.TryGetProperty("reference", out var reference) ? reference.GetString() ?? "" : "",
                    System.Array.AsReadOnly(Array(n, "parents").Select(p => p.GetString() ?? "").ToArray()),
                    new ReadOnlyDictionary<string, string>(n.TryGetProperty("substitution", out var substitution) ? substitution.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal) : new Dictionary<string, string>()))).ToArray();
                var proof = new CaseProof(id, System.Array.AsReadOnly(nodes), Fingerprint(scope));
                var check = CaseProofChecker.CheckGoal(scope, proof, theorems[i].Conclusion);
                if (!check.Valid)
                    throw new FormatException("Invalid theorem proof: " + theorems[i].Id + "; " + string.Join("; ", check.Errors));
                theorems[i] = theorems[i] with { Proof = proof };
            }
            if (!scopes.TryAdd(id, scope)) throw new FormatException("Duplicate scope: " + id);
        }
        var candidates = Array(root, "candidates").Select(c => new CaseCandidate(Text(c, "id"), Text(c, "scope"), System.Array.AsReadOnly(Array(c, "requires").Select(x => Literal(x, true)).ToArray()), System.Array.AsReadOnly(Array(c, "excludes").Select(x => Literal(x, true)).ToArray()))).ToList();
        Unique(candidates.Select(c => c.Id), "candidate");
        foreach (var c in candidates)
            if (!scopes.ContainsKey(c.Scope) || c.Requires.Count == 0) throw new FormatException("Candidate needs a known scope and explicit required evidence: " + c.Id);
        return new CaseTheory(scopes, candidates);
    }
    public static CaseTheory ParseScript(string script) => FromData(CaseProofScript.Elaborate(script));
    private static IEnumerable<JsonElement> Array(JsonElement e, string name) => e.TryGetProperty(name, out var a) ? a.EnumerateArray().ToArray() : System.Array.Empty<JsonElement>();
    private static string Text(JsonElement e, string name)
    {
        var value = e.GetProperty(name).GetString();
        return !string.IsNullOrWhiteSpace(value) ? value : throw new FormatException("Empty " + name);
    }
    private static CaseLiteral Literal(JsonElement e, bool ground)
    {
        foreach (var field in e.EnumerateObject())
            if (field.Name is not ("predicate" or "args" or "negative")) throw new FormatException("Unknown literal field: " + field.Name);
        var p = Text(e, "predicate");
        if (p.StartsWith('?')) throw new FormatException("Variable predicates are not supported");
        var args = Array(e, "args").Select(a => a.GetString() ?? throw new FormatException("Atom arguments must be strings")).ToArray();
        if (args.Any(a => string.IsNullOrWhiteSpace(a) || a == "?")) throw new FormatException("Empty atom argument");
        var result = new CaseLiteral(p, System.Array.AsReadOnly(args), e.TryGetProperty("negative", out var n) && n.GetBoolean());
        if (ground && !result.Ground) throw new FormatException("Expected ground literal");
        return result;
    }
    private static void Unique(IEnumerable<string> ids, string kind)
    { var seen = new HashSet<string>(StringComparer.Ordinal); foreach (var id in ids) if (!seen.Add(id)) throw new FormatException("Duplicate " + kind + ": " + id); }

    public CaseProof Infer(string scope, int maxNodes = 10000, int maxMatches = 1000000)
        => CaseInference.Infer(Scopes[scope], maxNodes, maxMatches);
    public CaseCheckResult Check(string scope, CaseProof proof) => CaseProofChecker.Check(Scopes[scope], proof);
    public CaseCheckResult CheckGoal(string scope, CaseProof proof, CaseLiteral goal) => CaseProofChecker.CheckGoal(Scopes[scope], proof, goal);

    /// <summary>Searches both signs and returns independently checkable, goal-rooted proof
    /// slices. Missing evidence remains unknown; contradiction never proves unrelated goals.</summary>
    public CaseGoalResult Prove(string scope, CaseLiteral goal, int maxNodes = 10000, int maxMatches = 1000000)
    {
        if (!CaseProofChecker.ValidLiteral(goal, true)) throw new ArgumentException("Goal must be a well-formed ground literal", nameof(goal));
        goal = goal with { Args = System.Array.AsReadOnly(goal.Args.ToArray()) };
        var closure = Infer(scope, maxNodes, maxMatches);
        var support = Slice(Scopes[scope], closure, goal);
        var refutation = Slice(Scopes[scope], closure, goal.Opposite);
        var status = support is not null && refutation is not null ? "conflicted" : support is not null ? "proved" : refutation is not null ? "refuted" : "unknown";
        return new(scope, goal, status, support, refutation);
    }

    private static CaseGoalProof? Slice(CaseScope scope, CaseProof closure, CaseLiteral goal)
    {
        var root = closure.Nodes.FirstOrDefault(n => n.Conclusion.Key == goal.Key);
        if (root is null) return null;
        var byId = closure.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var needed = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(); pending.Push(root.Id);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!needed.Add(id)) continue;
            foreach (var parent in byId[id].Parents) pending.Push(parent);
        }
        var nodes = closure.Nodes.Where(n => needed.Contains(n.Id)).ToArray();
        var certificate = closure with { Nodes = System.Array.AsReadOnly(nodes) };
        var check = CaseProofChecker.CheckGoal(scope, certificate, goal);
        if (!check.Valid) throw new InvalidOperationException("Goal proof rejected: " + string.Join("; ", check.Errors));
        return new(certificate, System.Array.AsReadOnly(nodes.Where(n => n.Kind == "axiom").Select(n => n.Reference).Distinct(StringComparer.Ordinal).ToArray()),
            System.Array.AsReadOnly(nodes.Where(n => n.Kind == "apply").Select(n => n.Reference).Distinct(StringComparer.Ordinal).ToArray()));
    }

    public CaseTheory WithEvidence(string scope, IEnumerable<CaseAxiom> evidence)
    {
        var original = Scopes[scope];
        var added = evidence.Select(a => a with { Literal = a.Literal with { Args = System.Array.AsReadOnly(a.Literal.Args.ToArray()) } }).ToArray();
        foreach (var a in added)
            if (string.IsNullOrWhiteSpace(a.Id) || string.IsNullOrWhiteSpace(a.Source) || string.IsNullOrWhiteSpace(a.Literal.Predicate) || a.Literal.Predicate.StartsWith('?') || !a.Literal.Ground || a.Literal.Args.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Evidence must be a named ground axiom with a source");
        var axioms = original.Axioms.Concat(added).ToArray();
        Unique(axioms.Select(a => a.Id).Concat(original.Rules.Select(r => r.Id)).Concat(original.Theorems.Select(t => t.Id)), "declaration");
        var scopes = Scopes.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        scopes[scope] = original with { Axioms = System.Array.AsReadOnly(axioms) };
        var check = CaseProofChecker.ValidateTheory(scopes[scope]);
        if (!check.Valid) throw new ArgumentException(string.Join("; ", check.Errors), nameof(evidence));
        return new CaseTheory(scopes, Candidates.ToList());
    }

    /// <summary>Retains authored inference rules and goals while requiring runtime
    /// evidence in place of a design scope's assumed observations.</summary>
    public CaseTheory WithoutAssumptions(string scope)
    {
        var scopes=Scopes.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        var original=scopes[scope];
        scopes[scope]=original with {Axioms=System.Array.Empty<CaseAxiom>()};
        return new CaseTheory(scopes,Candidates.ToList());
    }

    public static string Fingerprint(CaseScope scope) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { scope.Id, scope.Axioms, scope.Rules }))));

    public CaseDesignReview EvaluateDesign(string scope)
    {
        var proof = Infer(scope);
        var facts = proof.Nodes.Select(n => n.Conclusion.Key).ToHashSet(StringComparer.Ordinal);
        var candidates = AuditCandidates(scope, proof);
        var supported = candidates.Where(c => c.Status == "supported").ToArray();
        var uniqueWithinDeclaredCandidates = supported.Length == 1 && candidates.All(c => c.Id == supported[0].Id || c.Status == "refuted");
        var goals = Scopes[scope].Theorems.Select(t => new CaseGoalReview(t.Id, facts.Contains(t.Conclusion.Key), facts.Contains(t.Conclusion.Opposite.Key))).ToArray();
        var removals = new List<CaseClueReview>();
        foreach (var axiom in Scopes[scope].Axioms)
        {
            var scopes = Scopes.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            scopes[scope] = scopes[scope] with { Axioms = System.Array.AsReadOnly(scopes[scope].Axioms.Where(a => a.Id != axiom.Id).ToArray()) };
            var reduced = new CaseTheory(scopes, Candidates.ToList());
            var reducedFacts = reduced.Infer(scope).Nodes.Select(n => n.Conclusion.Key).ToHashSet(StringComparer.Ordinal);
            removals.Add(new CaseClueReview(axiom.Id, Scopes[scope].Theorems.Where(t => facts.Contains(t.Conclusion.Key) && !reducedFacts.Contains(t.Conclusion.Key)).Select(t => t.Id).ToArray(), reduced.AuditCandidates(scope)));
        }
        return new CaseDesignReview("declared_finite_candidates_only", uniqueWithinDeclaredCandidates, goals, candidates, removals, proof);
    }

    public IReadOnlyList<CaseCandidateReview> AuditCandidates(string scope, CaseProof? proof = null)
    {
        proof ??= Infer(scope);
        var check = Check(scope, proof);
        if (!check.Valid) throw new InvalidOperationException(string.Join("; ", check.Errors));
        // An incomplete caller-supplied proof cannot support a design exclusion claim.
        var closure = Infer(scope);
        var facts = closure.Nodes.Select(n => n.Conclusion.Key).ToHashSet(StringComparer.Ordinal);
        return Candidates.Where(c => c.Scope == scope).Select(c =>
        {
            var missing = c.Requires.Concat(c.Excludes.Select(p => p.Opposite)).Where(p => !facts.Contains(p.Key)).ToArray();
            var refutations = c.Requires.Select(p => p.Opposite).Concat(c.Excludes).Where(p => facts.Contains(p.Key)).ToArray();
            var conflicts = c.Requires.Concat(c.Excludes).Where(p => facts.Contains(p.Key) && facts.Contains(p.Opposite.Key)).ToArray();
            var status = conflicts.Length > 0 ? "conflicted" : refutations.Length > 0 ? "refuted" : missing.Length > 0 ? "unresolved" : "supported";
            return new CaseCandidateReview(c.Id, status, missing, refutations, conflicts);
        }).ToArray();
    }
}

public sealed record CaseGoalReview(string Id, bool Provable, bool OppositeProvable);
public sealed record CaseClueReview(string RemovedAxiom, IReadOnlyList<string> GoalsNoLongerProvable, IReadOnlyList<CaseCandidateReview> Candidates);
public sealed record CaseDesignReview(string Coverage, bool UniqueWithinDeclaredCandidates, IReadOnlyList<CaseGoalReview> Goals,
    IReadOnlyList<CaseCandidateReview> Candidates, IReadOnlyList<CaseClueReview> ClueRemoval, CaseProof Proof);
}

// --- Source: Reasoning.cs ---
namespace DuelView.Core
{


/// <summary>Cognition / Reasoning: native Core mechanisms.</summary>
public static partial class DVReasoningKernel
{
    public static object F_dvs_reason_cell(object v_cell)
    {
        return F_dv_without(v_cell, "time");
    }

    public static object F_dvs_reason_value(object v_cell)
    {
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("known", F_dv_get(v_cell, "known")), F_dv_pair("value", F_dv_get(v_cell, "value")), F_dv_pair("confidence", F_dv_get(v_cell, "confidence")), F_dv_pair("conflicted", F_dv_get(v_cell, "conflicted")) };
    }

    public static object F_dvs_reason_premises(object v_mind, object v_proposition)
    {
        object v_op__4191 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__4191, "held_for")))
        {
            return new System.Collections.Generic.List<object>(3) { F_dv_pair("proposition", v_proposition), F_dv_pair("support", F_dv_read(v_mind, F_dv_get(v_proposition, "ref"))), F_dv_pair("clock", F_dv_read(v_mind, F_dv_get(v_proposition, "clock"))) };
        }
        if (R.Bool(R.eq(v_op__4191, "constant")))
        {
            return v_proposition;
        }
        else
        {
            if (R.Bool(R.eq(v_op__4191, "not")))
            {
                return F_dvs_reason_premises(v_mind, F_dv_get(v_proposition, "item"));
            }
            else
            {
                if (R.Bool(R.eq(v_op__4191, "all")))
                {
                    object v_items__4735 = F_dv_get(v_proposition, "items");
                    object v_result__4781 = new System.Collections.Generic.List<object>(0) { };
                    object v_i__4813 = R.number("0");
                    while (R.Bool(R.lt(v_i__4813, R.count(v_items__4735))))
                    {
                        _ = R.push(v_result__4781, F_dvs_reason_premises(v_mind, R.at(v_items__4735, v_i__4813)));
                        v_i__4813 = R.add(v_i__4813, R.number("1"));
                    }
                    return v_result__4781;
                }
                else
                {
                    return new System.Collections.Generic.List<object>(2) { F_dv_pair("proposition", v_proposition), F_dv_pair("support", F_dvs_reason_cell(F_dv_read(v_mind, F_dv_get(v_proposition, "ref")))) };
                }
            }
        }
    }

    public static object F_dvs_reason_signature(object v_mind, object v_adoption)
    {
        object v_law__5179 = F_dv_get(v_adoption, "law");
        object v_effects__5206 = F_dv_get(F_dv_get(v_adoption, "law"), "effects");
        object v_outputs__5245 = new System.Collections.Generic.List<object>(0) { };
        object v_i__5266 = R.number("0");
        while (R.Bool(R.lt(v_i__5266, R.count(v_effects__5206))))
        {
            object v_cell__5319 = F_dv_read(v_mind, F_dv_get(R.at(v_effects__5206, v_i__5266), "ref"));
            _ = R.push(v_outputs__5245, new System.Collections.Generic.List<object>(4) { F_dv_pair("known", F_dv_get(v_cell__5319, "known")), F_dv_pair("value", F_dv_get(v_cell__5319, "value")), F_dv_pair("confidence", F_dv_get(v_cell__5319, "confidence")), F_dv_pair("conflicted", F_dv_get(v_cell__5319, "conflicted")) });
            v_i__5266 = R.add(v_i__5266, R.number("1"));
        }
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("law", v_law__5179), F_dv_pair("confidence", F_dv_get(v_adoption, "confidence")), F_dv_pair("basis", F_dv_get(v_adoption, "basis")), F_dv_pair("premises", F_dvs_reason_premises(v_mind, F_dv_get(v_law__5179, "premise"))) }, "outputs", v_outputs__5245);
    }

    public static object F_dvs_reason_changes(object v_changes)
    {
        object v_i__5713 = R.number("0");
        object v_changed__5727 = false;
        while (R.Bool(R.lt(v_i__5713, R.count(v_changes))))
        {
            object v_change__5790 = R.at(v_changes, v_i__5713);
            if (R.Bool(F_dv_same(F_dvs_reason_value(F_dv_get(v_change__5790, "before")), F_dvs_reason_value(F_dv_get(v_change__5790, "after")))))
            {
                _ = false;
            }
            else
            {
                v_changed__5727 = true;
            }
            v_i__5713 = R.add(v_i__5713, R.number("1"));
        }
        return v_changed__5727;
    }

    public static object F_dvs_reason_monotone_satisfied(object v_mind, object v_effects)
    {
        object v_e__6051 = R.number("0");
        object v_n__6065 = R.count(v_effects);
        while (R.Bool(R.lt(v_e__6051, v_n__6065)))
        {
            object v_eff__6116 = R.at(v_effects, v_e__6051);
            if (R.Bool(R.not(R.eq(F_dv_find(v_eff__6116, "op", ""), "assign"))))
            {
                return false;
            }
            object v_cell__6217 = F_dv_read(v_mind, F_dv_get(v_eff__6116, "ref"));
            if (R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_cell__6217, "known"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_cell__6217, "value"), F_dv_get(v_eff__6116, "value")))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_cell__6217, "confidence"), R.number("1"))))))
            {
                return false;
            }
            v_e__6051 = R.add(v_e__6051, R.number("1"));
        }
        return true;
    }

    public static object F_dvs_dirty_refs(object v_condition) => DirtyRefs(v_condition, false);

    // Dirty truth only reads these declarations. Keep the public operation's
    // writable result while its internal query borrows immutable cache values.
    private static object DirtyRefs(object v_condition, bool readOnly)
    {
        object key = new System.Collections.Generic.List<object>(1) { "dirty-declaration-refs-v1" };
        object v_cached__6580 = readOnly ? R.memo_read(v_condition, key) : R.memo_get(v_condition, key);
        if (R.Bool(R.at(v_cached__6580, R.number("0"))))
        {
            return R.at(v_cached__6580, R.number("1"));
        }
        object v_op__6694 = F_dv_get(v_condition, "op");
        object v_refs__6720 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.eq(v_op__6694, "held_for")))
        {
            object v_declared__6767 = F_dv_condition_inputs(v_condition);
            _ = F_dv_require(F_dv_get(v_declared__6767, "complete"), "Invalid held-for dependency declaration");
            object declaredResult = R.memo_put(v_condition, new System.Collections.Generic.List<object>(1) { "dirty-declaration-refs-v1" }, F_dv_get(v_declared__6767, "reads"));
            return readOnly ? R.snapshot(declaredResult) : declaredResult;
        }
        if (R.Bool((R.Bool(R.eq(v_op__6694, "equals")) ? (object)(true) : (object)(R.eq(v_op__6694, "at_least")))))
        {
            _ = R.push(v_refs__6720, F_dv_get(v_condition, "ref"));
        }
        else
        {
            if (R.Bool(R.eq(v_op__6694, "not")))
            {
                v_refs__6720 = DirtyRefs(F_dv_get(v_condition, "item"), readOnly);
            }
            else
            {
                if (R.Bool(R.eq(v_op__6694, "all")))
                {
                    object v_i__7201 = R.number("0");
                    while (R.Bool(R.lt(v_i__7201, R.count(F_dv_get(v_condition, "items")))))
                    {
                        v_refs__6720 = F_dvp_merge_refs(v_refs__6720, DirtyRefs(R.at(F_dv_get(v_condition, "items"), v_i__7201), readOnly));
                        v_i__7201 = R.add(v_i__7201, R.number("1"));
                    }
                }
                else
                {
                    _ = F_dv_require(R.eq(v_op__6694, "constant"), R.cat("Unindexed condition operator: ", v_op__6694));
                }
            }
        }
        object result = R.memo_put(v_condition, new System.Collections.Generic.List<object>(1) { "dirty-declaration-refs-v1" }, v_refs__6720);
        return readOnly ? R.snapshot(result) : result;
    }

    public static object F_dvs_dirty_inputs(object v_world, object v_refs)
    {
        object v_result__7597 = new System.Collections.Generic.List<object>(1) { F_dv_get(v_world, "subject") };
        object v_i__7630 = R.number("0");
        while (R.Bool(R.lt(v_i__7630, R.count(v_refs))))
        {
            _ = R.push(v_result__7597, new System.Collections.Generic.List<object>(2) { F_dv_has_key(F_dv_get(v_world, "objects"), F_dv_get(R.at(v_refs, v_i__7630), "object")), F_dv_read(v_world, R.at(v_refs, v_i__7630)) });
            v_i__7630 = R.add(v_i__7630, R.number("1"));
        }
        return v_result__7597;
    }

    public static object F_dvs_dirty_truth(object v_world, object v_condition, object v_config, object v_name)
    {
        if (R.Bool(R.not(F_dv_find(v_config, "opt_dirty", false))))
        {
            return F_dv_truth(v_world, v_condition);
        }
        v_condition = R.snapshot(v_condition);
        object v_inputs__8069 = F_dvs_dirty_inputs(v_world, DirtyRefs(v_condition, true));
        object v_key__8137 = new System.Collections.Generic.List<object>(2) { "dirty-truth-last-v1", F_dv_get(v_world, "subject") };
        object v_cached__8190 = R.memo_read(v_condition, v_key__8137);
        object v_calls__8235 = R.number("1");
        if (R.Bool(R.at(v_cached__8190, R.number("0"))))
        {
            v_calls__8235 = R.add(F_dv_get(R.at(v_cached__8190, R.number("1")), "calls"), R.number("1"));
            if (R.Bool(F_dv_same(v_inputs__8069, F_dv_get(R.at(v_cached__8190, R.number("1")), "inputs"))))
            {
                object v_every__8361 = F_dv_find(v_config, "opt_dirty_verify", R.number("0"));
                if (R.Bool((R.Bool(R.lt(R.number("0"), v_every__8361)) ? (object)(R.not(R.lt(v_calls__8235, v_every__8361))) : (object)(false))))
                {
                    _ = F_dv_require(F_dv_same(F_dv_get(R.at(v_cached__8190, R.number("1")), "truth"), F_dv_truth_uncached(v_world, v_condition)), R.cat("Dirty dependency mismatch: ", v_name));
                    v_calls__8235 = R.number("0");
                }
                _ = R.memo_put(v_condition, v_key__8137, F_dv_with(R.at(v_cached__8190, R.number("1")), "calls", v_calls__8235));
                return F_dv_get(R.at(v_cached__8190, R.number("1")), "truth");
            }
        }
        object v_truth__8733 = F_dv_truth(v_world, v_condition);
        _ = R.memo_put(v_condition, v_key__8137, new System.Collections.Generic.List<object>(3) { F_dv_pair("inputs", v_inputs__8069), F_dv_pair("truth", v_truth__8733), F_dv_pair("calls", v_calls__8235) });
        return v_truth__8733;
    }

    public static object F_dvs_dirty_reason_index(object v_actor)
    {
        object v_mind__9070 = F_dv_get(v_actor, "mind");
        object v_adoptions__9096 = R.snapshot(F_dv_get(v_mind__9070, "adoptions"));
        object v_key__9144 = new System.Collections.Generic.List<object>(4) { "dirty-reason-index-v2", F_dv_get(v_mind__9070, "subject"), F_dv_find(v_actor, "inference_signatures", new System.Collections.Generic.List<object>(0) { }), F_dv_find(v_mind__9070, "profile", new System.Collections.Generic.List<object>(0) { }) };
        object v_cached__9272 = R.memo_get(v_adoptions__9096, v_key__9144);
        if (R.Bool(R.at(v_cached__9272, R.number("0"))))
        {
            return R.at(v_cached__9272, R.number("1"));
        }
        object v_result__9360 = new System.Collections.Generic.List<object>(0) { };
        object v_i__9380 = R.number("0");
        while (R.Bool(R.lt(v_i__9380, R.count(v_adoptions__9096))))
        {
            object v_entry__9433 = R.at(v_adoptions__9096, v_i__9380);
            object v_refs__9466 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool((R.Bool(F_dv_get(R.at(v_entry__9433, R.number("1")), "active")) ? (object)(R.eq(F_dv_get(R.at(v_entry__9433, R.number("1")), "mode"), "infer")) : (object)(false))))
            {
                object v_law__9629 = F_dv_get(R.at(v_entry__9433, R.number("1")), "law");
                v_refs__9466 = F_dvs_dirty_refs(F_dv_get(v_law__9629, "premise"));
                object v_j__9711 = R.number("0");
                while (R.Bool(R.lt(v_j__9711, R.count(F_dv_get(v_law__9629, "effects")))))
                {
                    object v_effect__9782 = R.at(F_dv_get(v_law__9629, "effects"), v_j__9711);
                    _ = F_dv_require((R.Bool(R.eq(F_dv_get(v_effect__9782, "op"), "assign")) ? (object)(true) : (object)(R.eq(F_dv_get(v_effect__9782, "op"), "increment"))), R.cat("Unindexed inference effect: ", F_dv_get(v_effect__9782, "op")));
                    v_refs__9466 = F_dvp_merge_refs(v_refs__9466, new System.Collections.Generic.List<object>(1) { F_dv_get(v_effect__9782, "ref") });
                    v_j__9711 = R.add(v_j__9711, R.number("1"));
                }
                v_j__9711 = R.number("0");
                while (R.Bool(R.lt(v_j__9711, R.count(v_refs__9466))))
                {
                    object v_subject__10095 = F_dv_find(R.at(v_refs__9466, v_j__9711), "subject", "");
                    _ = F_dv_require((R.Bool(R.eq(v_subject__10095, "")) ? (object)(true) : (object)(R.eq(v_subject__10095, F_dv_get(v_mind__9070, "subject")))), "Cross-subject reference access");
                    v_j__9711 = R.add(v_j__9711, R.number("1"));
                }
            }
            _ = R.push(v_result__9360, new System.Collections.Generic.List<object>(2) { F_dv_pair("law", R.at(v_entry__9433, R.number("0"))), F_dv_pair("refs", v_refs__9466) });
            v_i__9380 = R.add(v_i__9380, R.number("1"));
        }
        return R.memo_put(v_adoptions__9096, v_key__9144, R.snapshot(v_result__9360));
    }

    public static object F_dvs_dirty_verify_reason(object v_mind, object v_adoptions, object v_signatures, object v_first, object v_until, object v_time)
    {
        object v_i__10487 = v_first;
        while (R.Bool(R.lt(v_i__10487, v_until)))
        {
            object v_entry__10530 = R.at(v_adoptions, v_i__10487);
            object v_law__10563 = F_dv_get(R.at(v_entry__10530, R.number("1")), "law");
            if (R.Bool((R.Bool(F_dv_get(R.at(v_entry__10530, R.number("1")), "active")) ? (object)(R.eq(F_dv_get(R.at(v_entry__10530, R.number("1")), "mode"), "infer")) : (object)(false))))
            {
                object v_skip__10655 = false;
                if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_signatures, R.at(v_entry__10530, R.number("0")), false), false))) ? (object)(F_dvs_reason_monotone_satisfied(v_mind, F_dv_get(v_law__10563, "effects"))) : (object)(false))))
                {
                    v_skip__10655 = true;
                }
                if (R.Bool((R.Bool(R.not(v_skip__10655)) ? (object)(R.eq(F_dv_truth_uncached(v_mind, F_dv_get(v_law__10563, "premise")), "true")) : (object)(false))))
                {
                    object v_signature__10907 = F_dvs_reason_signature(v_mind, R.at(v_entry__10530, R.number("1")));
                    if (R.Bool(R.not(F_dv_same(F_dv_find(v_signatures, R.at(v_entry__10530, R.number("0")), false), v_signature__10907))))
                    {
                        object v_stage__11061 = F_dv_stage(v_mind, F_dv_get(v_law__10563, "effects"), v_time, F_dv_get(R.at(v_entry__10530, R.number("1")), "basis"), F_dv_get(R.at(v_entry__10530, R.number("1")), "confidence"));
                        _ = F_dv_require(R.not(R.eq(F_dv_get(v_stage__11061, "status"), "ready")), R.cat("Dirty inference omitted rule: ", F_dv_get(v_law__10563, "name")));
                    }
                }
            }
            v_i__10487 = R.add(v_i__10487, R.number("1"));
        }
        return true;
    }

    public static object F_dvs_reason_run(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_mind__11502 = F_dv_get(v_actor, "mind");
        object v_policy__11528 = F_dv_get(v_actor, "policy");
        object v_budget__11558 = F_dv_find(v_policy__11528, "reason_budget", R.number("0"));
        object v_count__11611 = R.number("0");
        object v_changed__11629 = false;
        object v_signatures__11653 = F_dv_find(v_actor, "inference_signatures", new System.Collections.Generic.List<object>(0) { });
        object v_derivations__11717 = F_dv_find(v_actor, "inference_derivations", new System.Collections.Generic.List<object>(0) { });
        object v_dirty__11783 = F_dv_find(v_policy__11528, "opt_dirty", false);
        object v_dependency_index__11835 = new System.Collections.Generic.List<object>(0) { };
        object v_verify__11865 = false;
        if (R.Bool((R.Bool(v_dirty__11783) ? (object)(R.lt(R.number("0"), v_budget__11558)) : (object)(false))))
        {
            v_dependency_index__11835 = F_dvs_dirty_reason_index(v_actor);
            object v_every__11978 = F_dv_find(v_policy__11528, "opt_dirty_verify", R.number("0"));
            if (R.Bool(R.lt(R.number("0"), v_every__11978)))
            {
                object v_key__12063 = new System.Collections.Generic.List<object>(1) { "dirty-reason-verify-tick" };
                object v_previous__12114 = R.memo_get(v_dependency_index__11835, v_key__12063);
                object v_calls__12176 = R.number("1");
                if (R.Bool(R.at(v_previous__12114, R.number("0"))))
                {
                    v_calls__12176 = R.add(R.at(v_previous__12114, R.number("1")), R.number("1"));
                }
                if (R.Bool(R.not(R.lt(v_calls__12176, v_every__11978))))
                {
                    v_verify__11865 = true;
                    v_calls__12176 = R.number("0");
                }
                _ = R.memo_put(v_dependency_index__11835, v_key__12063, v_calls__12176);
            }
        }
        if (R.Bool(R.lt(v_count__11611, v_budget__11558)))
        {
            object v_adoptions__12430 = F_dv_get(v_mind__11502, "adoptions");
            object v_i__12469 = R.number("0");
            if (R.Bool(v_dirty__11783))
            {
                v_i__12469 = R.dirty_next(v_mind__11502, v_dependency_index__11835, R.sub(R.number("0"), R.number("1")));
                if (R.Bool(v_verify__11865))
                {
                    _ = F_dvs_dirty_verify_reason(v_mind__11502, v_adoptions__12430, v_signatures__11653, R.number("0"), v_i__12469, v_time);
                }
            }
            v_changed__11629 = false;
            while (R.Bool(F_dv_and(R.lt(v_i__12469, R.count(v_adoptions__12430)), R.lt(v_count__11611, v_budget__11558))))
            {
                object v_entry__12762 = R.at(v_adoptions__12430, v_i__12469);
                object v_law__12799 = F_dv_get(R.at(v_entry__12762, R.number("1")), "law");
                if (R.Bool(F_dv_and(F_dv_get(R.at(v_entry__12762, R.number("1")), "active"), R.eq(F_dv_get(R.at(v_entry__12762, R.number("1")), "mode"), "infer"))))
                {
                    object v_skip__12906 = false;
                    if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_signatures__11653, R.at(v_entry__12762, R.number("0")), false), false))) ? (object)(F_dvs_reason_monotone_satisfied(v_mind__11502, F_dv_get(v_law__12799, "effects"))) : (object)(false))))
                    {
                        v_skip__12906 = true;
                    }
                    if (R.Bool((R.Bool(R.not(v_skip__12906)) ? (object)(R.eq(F_dvs_dirty_truth(v_mind__11502, F_dv_get(v_law__12799, "premise"), v_policy__11528, F_dv_get(v_law__12799, "name")), "true")) : (object)(false))))
                    {
                        object v_sig__13192 = F_dvs_reason_signature(v_mind__11502, R.at(v_entry__12762, R.number("1")));
                        if (R.Bool(R.not(F_dv_same(F_dv_find(v_signatures__11653, R.at(v_entry__12762, R.number("0")), false), v_sig__13192))))
                        {
                            object v_stage__13342 = F_dv_stage(v_mind__11502, F_dv_get(v_law__12799, "effects"), v_time, F_dv_get(R.at(v_entry__12762, R.number("1")), "basis"), F_dv_get(R.at(v_entry__12762, R.number("1")), "confidence"));
                            if (R.Bool(F_dv_and(R.eq(F_dv_get(v_stage__13342, "status"), "ready"), F_dvs_reason_changes(F_dv_get(v_stage__13342, "changes")))))
                            {
                                object v_before__13550 = v_mind__11502;
                                object v_result__13596 = F_dv_infer(v_mind__11502, R.at(v_entry__12762, R.number("0")), R.cat("infer", R.show(R.count(v_events))), v_time, F_dv_get(v_mind__11502, "revision"));
                                v_mind__11502 = F_dv_get(v_result__13596, "world");
                                v_changed__11629 = true;
                                v_count__11611 = R.add(v_count__11611, R.number("1"));
                                v_signatures__11653 = F_dv_with(v_signatures__11653, R.at(v_entry__12762, R.number("0")), v_sig__13192);
                                object v_event__13942 = F_dvs_event(v_events, "inference", F_dv_get(v_actor, "id"), v_time, F_dv_explanation_merge(F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent)), F_dvs_support_parents(v_before__13550, F_dvs_sim_refs(F_dv_get(v_law__12799, "premise")), v_events, v_actor)), new System.Collections.Generic.List<object>(4) { F_dv_pair("mind_before", v_before__13550), F_dv_pair("mind_after", v_mind__11502), F_dv_pair("law", v_law__12799), F_dv_pair("receipt", v_result__13596) });
                                v_actor = F_dv_with(v_actor, "last_cognitive_event", v_event__13942);
                                _ = R.push(v_derivations__11717, F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("event", v_event__13942), F_dv_pair("adoption", R.at(v_entry__12762, R.number("1"))), F_dv_pair("inputs", F_dvs_sim_inputs(v_before__13550, F_dv_get(v_law__12799, "premise"))), F_dv_pair("changes", F_dv_get(v_result__13596, "changes")) }, "valid", true));
                            }
                            else
                            {
                                if (R.Bool(R.eq(F_dv_get(v_stage__13342, "status"), "ready")))
                                {
                                    v_count__11611 = R.add(v_count__11611, R.number("1"));
                                    v_signatures__11653 = F_dv_with(v_signatures__11653, R.at(v_entry__12762, R.number("0")), v_sig__13192);
                                    _ = F_dvs_event(v_events, "inference_alternative_basis", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent)), new System.Collections.Generic.List<object>(4) { F_dv_pair("law", v_law__12799), F_dv_pair("basis", F_dv_get(R.at(v_entry__12762, R.number("1")), "basis")), F_dv_pair("premises", F_dvs_reason_premises(v_mind__11502, F_dv_get(v_law__12799, "premise"))), F_dv_pair("mind_revision", F_dv_get(v_mind__11502, "revision")) });
                                }
                            }
                        }
                    }
                }
                object v_previous__14989 = v_i__12469;
                if (R.Bool(v_dirty__11783))
                {
                    if (R.Bool(R.lt(v_count__11611, v_budget__11558)))
                    {
                        v_i__12469 = R.dirty_next(v_mind__11502, v_dependency_index__11835, v_previous__14989);
                        if (R.Bool(v_verify__11865))
                        {
                            _ = F_dvs_dirty_verify_reason(v_mind__11502, v_adoptions__12430, v_signatures__11653, R.add(v_previous__14989, R.number("1")), v_i__12469, v_time);
                        }
                    }
                }
                else
                {
                    v_i__12469 = R.add(v_i__12469, R.number("1"));
                }
            }
        }
        object v_local_1__15475 = false;
        if (R.Bool(R.lt(v_count__11611, v_budget__11558)))
        {
            v_local_1__15475 = "no-new-effective-conclusion";
        }
        else
        {
            v_local_1__15475 = "budget-exhausted";
        }
        if (R.Bool((R.Bool(v_changed__11629) ? (object)(R.lt(v_count__11611, v_budget__11558)) : (object)(false))))
        {
            v_local_1__15475 = "approximate-single-pass";
        }
        object v_report__15698 = new System.Collections.Generic.List<object>(5) { F_dv_pair("steps", v_count__11611), F_dv_pair("remaining", R.sub(v_budget__11558, v_count__11611)), F_dv_pair("stop", v_local_1__15475), F_dv_pair("mind_revision", F_dv_get(v_mind__11502, "revision")), F_dv_pair("approximation", true) };
        if (R.Bool((R.Bool(R.lt(R.number("0"), v_count__11611)) ? (object)(true) : (object)(R.not(F_dv_same(v_report__15698, F_dv_find(v_actor, "last_reason_report", false)))))))
        {
            v_actor = F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(2) { F_dv_pair("last_cognitive_event", F_dvs_event(v_events, "reason_stop", F_dv_get(v_actor, "id"), v_time, F_dvs_cognitive_parents(v_actor, F_dv_single(v_parent)), v_report__15698)), F_dv_pair("last_reason_report", v_report__15698) });
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(4) { F_dv_pair("mind", v_mind__11502), F_dv_pair("inference_derivations", v_derivations__11717), F_dv_pair("inference_signatures", v_signatures__11653), F_dv_pair("policy", F_dv_with(v_policy__11528, "reason_budget", R.sub(v_budget__11558, v_count__11611))) });
    }

    public static object F_dvs_has_active_communication_goal(object v_actor)
    {
        object v_goals__27977 = F_dv_find(F_dv_get(v_actor, "policy"), "communication_goals", new System.Collections.Generic.List<object>(0) { });
        object v_i__28042 = R.number("0");
        while (R.Bool(R.lt(v_i__28042, R.count(v_goals__27977))))
        {
            object v_goal__28091 = R.at(v_goals__27977, v_i__28042);
            if (R.Bool((R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_goal__28091, "basis", new System.Collections.Generic.List<object>(0) { })))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_goal__28091, "available_when", F_dv_true())), "true")) : (object)(false))))
            {
                return true;
            }
            v_i__28042 = R.add(v_i__28042, R.number("1"));
        }
        return false;
    }

    public static object F_dvs_prepare_cognition(object v_actor, object v_events, object v_time, object v_parent)
    {
        if (R.Bool(F_dv_find(F_dv_find(F_dv_get(v_actor, "mind"), "CognitionFilter", new System.Collections.Generic.List<object>(0) { }), "anterograde", false)))
        {
            return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(3) { F_dv_pair("cognition_stop", "anterograde-no-new-memory"), F_dv_pair("cognition_reused", false), F_dv_pair("reason_reused", false) });
        }
        v_actor = F_dvs_refresh_argument(v_actor, v_events, v_time, v_parent);
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_actor, "argument", false), false))) ? (object)(F_dv_find(v_actor, "cognition_blocked", false)) : (object)(false))))
        {
            return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(3) { F_dv_pair("cognition_stop", "argument-revocation-budget"), F_dv_pair("cognition_reused", false), F_dv_pair("reason_reused", false) });
        }
        v_actor = F_dvs_reason(v_actor, v_events, v_time, v_parent);
        v_actor = F_dvs_current_appraisals(v_actor, v_events, v_time, v_parent);
        v_actor = F_dvs_update_support_events(v_actor, v_events);
        v_actor = F_dvs_capture_public_topics(v_actor, v_events, v_time, v_parent);
        v_actor = F_dvs_refresh_argument(v_actor, v_events, v_time, v_parent);
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(4) { F_dv_pair("cognition_stop", "approximate-single-pass"), F_dv_pair("cognition_mode", "approximate-v1"), F_dv_pair("cognition_reused", false), F_dv_pair("reason_reused", false) });
    }
}
}
