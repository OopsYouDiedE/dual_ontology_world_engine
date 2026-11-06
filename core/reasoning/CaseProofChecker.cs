// @mechanism: signed_horn_reasoning
// @layer: core/reasoning
// @contracts: CaseProofChecker

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
using System.Text.RegularExpressions;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;

// --- Source: CaseProofChecker.cs ---
namespace DuelView.Core
{

/// <summary>The small trusted checker does not invoke proof search. All edges point to earlier nodes.</summary>
public static class CaseProofChecker
{
    public static CaseCheckResult Check(CaseScope theory, CaseProof proof)
    {
        CaseCheckResult? result = null;
        foreach (var _ in CheckSteps(theory, proof, value => result = value)) { }
        return result!;
    }

    // The synchronous and sliced callers consume the same independent checker.
    internal static IEnumerable<int> CheckSteps(CaseScope theory, CaseProof proof, Action<CaseCheckResult> complete)
    {
        var errors = new List<string>();
        CaseCheckResult? theoryCheck = null;
        foreach (var step in ValidateTheorySteps(theory, value => theoryCheck = value)) yield return step;
        if (!theoryCheck!.Valid) { complete(theoryCheck); yield break; }
        if (proof is null || proof.Nodes is null) { complete(new CaseCheckResult(false, new[] { "Missing proof or nodes" })); yield break; }
        if (proof.Nodes.Count > 10000) { complete(new CaseCheckResult(false, new[] { "Proof node budget exceeded" })); yield break; }
        if (proof.Scope != theory.Id) errors.Add("Proof scope differs from theory");
        string? fingerprint = null;
        foreach (var step in CaseWorkFingerprint.Steps(theory, value => fingerprint = value)) yield return step;
        if (proof.TheoryHash != fingerprint) errors.Add("Proof belongs to a different theory version");
        var nodes = new Dictionary<string, CaseProofNode>(StringComparer.Ordinal);
        var axioms = new Dictionary<string, CaseAxiom>(StringComparer.Ordinal);
        var rules = new Dictionary<string, CaseRule>(StringComparer.Ordinal);
        foreach (var axiom in theory.Axioms) { axioms.Add(axiom.Id, axiom); yield return 0; }
        foreach (var rule in theory.Rules) { rules.Add(rule.Id, rule); yield return 0; }
        foreach (var node in proof.Nodes)
        {
            yield return 0;
            // Certificates can arrive from a JSON client rather than the DV elaborator.
            // Malformed nodes must be rejected, never dereferenced as trusted objects.
            if (node is null || node.Conclusion is null || node.Conclusion.Args is null || node.Parents is null || node.Substitution is null || node.Reference is null ||
                string.IsNullOrWhiteSpace(node.Conclusion.Predicate) || node.Conclusion.Predicate.StartsWith('?') ||
                node.Conclusion.Args.Any(string.IsNullOrWhiteSpace) || node.Parents.Any(string.IsNullOrWhiteSpace))
            { errors.Add("Malformed proof node"); continue; }
            if (string.IsNullOrWhiteSpace(node.Id) || nodes.ContainsKey(node.Id)) { errors.Add("Duplicate or empty proof node: " + node.Id); continue; }
            var valid = node.Conclusion.Ground;
            if (node.Kind == "axiom")
                valid &= node.Parents.Count == 0 && node.Substitution.Count == 0 && axioms.TryGetValue(node.Reference, out var a) && a.Literal.Key == node.Conclusion.Key;
            else if (node.Kind == "apply" && rules.TryGetValue(node.Reference, out var rule))
            {
                var variables = rule.Premises.Append(rule.Conclusion).SelectMany(l => l.Args).Where(a => a.StartsWith('?')).ToHashSet(StringComparer.Ordinal);
                valid &= variables.SetEquals(node.Substitution.Keys) && node.Substitution.Values.All(v => !string.IsNullOrWhiteSpace(v) && !v.StartsWith('?'));
                valid &= rule.Premises.Count == node.Parents.Count && rule.Conclusion.Substitute(node.Substitution).Key == node.Conclusion.Key;
                if (rule.Premises.Count == node.Parents.Count)
                    for (var i = 0; i < rule.Premises.Count; i++)
                    {
                        valid &= nodes.TryGetValue(node.Parents[i], out var parent) && parent.Conclusion.Key == rule.Premises[i].Substitute(node.Substitution).Key;
                        yield return 0;
                    }
            }
            else if (node.Kind == "exact")
                valid &= node.Reference.Length == 0 && node.Substitution.Count == 0 && node.Parents.Count == 1 && nodes.TryGetValue(node.Parents[0], out var parent) && parent.Conclusion.Key == node.Conclusion.Key;
            else valid = false;
            if (!valid) errors.Add("Invalid proof node: " + node.Id);
            else nodes.Add(node.Id, node);
        }
        complete(new CaseCheckResult(errors.Count == 0, errors.AsReadOnly()));
    }

    /// <summary>A valid derivation alone does not assert a particular goal. Goal certificates
    /// must end in the requested atom; an empty or unrelated derivation never proves it.</summary>
    public static CaseCheckResult CheckGoal(CaseScope theory, CaseProof proof, CaseLiteral goal)
    {
        var check = Check(theory, proof);
        if (!check.Valid) return check;
        if (!ValidLiteral(goal, true)) return new(false, new[] { "Goal must be a well-formed ground literal" });
        return proof.Nodes.Count > 0 && proof.Nodes[^1].Conclusion.Key == goal.Key
            ? check : new(false, new[] { "Proof does not conclude the requested goal" });
    }

    // Public records can be created without the DV loader. The trusted boundary checks
    // the theory as well as its certificate, instead of trusting constructor provenance.
    public static CaseCheckResult ValidateTheory(CaseScope theory)
    {
        CaseCheckResult? result = null;
        foreach (var _ in ValidateTheorySteps(theory, value => result = value)) { }
        return result!;
    }

    internal static IEnumerable<int> ValidateTheorySteps(CaseScope theory, Action<CaseCheckResult> complete)
    {
        var errors = new List<string>();
        if (theory is null || string.IsNullOrWhiteSpace(theory.Id) || theory.Axioms is null || theory.Rules is null || theory.Theorems is null)
        { complete(new(false, new[] { "Missing or malformed theory" })); yield break; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var arities = new Dictionary<string, int>(StringComparer.Ordinal);
        bool Declaration(string id) => !string.IsNullOrWhiteSpace(id) && ids.Add(id);
        bool Atom(CaseLiteral literal, bool ground)
        {
            if (!ValidLiteral(literal, ground)) return false;
            if (arities.TryGetValue(literal.Predicate, out var arity)) return arity == literal.Args.Count;
            arities.Add(literal.Predicate, literal.Args.Count); return true;
        }
        foreach (var a in theory.Axioms)
        {
            if (a is null || !Declaration(a.Id) || string.IsNullOrWhiteSpace(a.Source) || !Atom(a.Literal, true))
                errors.Add("Malformed or duplicate axiom");
            yield return 0;
        }
        foreach (var r in theory.Rules)
        {
            yield return 0;
            if (r is null || !Declaration(r.Id) || string.IsNullOrWhiteSpace(r.Source) || r.Premises is null ||
                r.Premises.Count > 64 || !Atom(r.Conclusion, false) || r.Premises.Any(p => !Atom(p, false)))
            { errors.Add("Malformed or duplicate rule"); continue; }
            var bound = r.Premises.SelectMany(p => p.Args).Where(a => a.StartsWith('?')).ToHashSet(StringComparer.Ordinal);
            if (r.Conclusion.Args.Any(a => a.StartsWith('?') && !bound.Contains(a))) errors.Add("Unsafe rule: " + r.Id);
        }
        foreach (var t in theory.Theorems)
        {
            if (t is null || !Declaration(t.Id) || !Atom(t.Conclusion, true)) errors.Add("Malformed or duplicate theorem");
            yield return 0;
        }
        complete(new(errors.Count == 0, errors.AsReadOnly()));
    }

    public static bool ValidLiteral(CaseLiteral literal, bool ground) => literal is not null &&
        !string.IsNullOrWhiteSpace(literal.Predicate) && !literal.Predicate.StartsWith('?') && literal.Args is not null &&
        literal.Args.All(a => !string.IsNullOrWhiteSpace(a) && a != "?" && (!ground || !a.StartsWith('?')));
}

/// <summary>Untrusted bounded proof search. Its result must pass the independent checker.</summary>
internal static class CaseInference
{
    internal static CaseProof Infer(CaseScope scope, int maxNodes, int maxMatches)
    {
        using var work = new CaseInferenceWork(scope, maxNodes, maxMatches);
        return work.RunToCompletion();
    }
}
}

// --- Source: Proofs.cs ---
namespace DuelView.Core
{


/// <summary>Cognition / Proofs: native Core mechanisms.</summary>
public static partial class DVReasoningKernel
{
    public static object F_dv_proof_step(object v_id, object v_kind, object v_conclusion, object v_parents, object v_law)
    {
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("kind", v_kind), F_dv_pair("conclusion", v_conclusion), F_dv_pair("parents", v_parents) }, "law", v_law);
    }

    public static object F_dv_cost_policy(object v_step, object v_evidence, object v_assumption, object v_conflict, object v_reference, object v_maximum, object v_allow_assumptions, object v_budget)
    {
        _ = F_dv_require(F_dv_and(F_dv_and(R.not(R.lt(v_step, R.number("0"))), R.not(R.lt(v_evidence, R.number("0")))), F_dv_and(F_dv_and(R.lt(R.number("0"), v_assumption), R.not(R.lt(v_conflict, R.number("0")))), F_dv_and(R.lt(R.number("0"), v_reference), R.not(R.lt(v_maximum, R.number("0")))))), "Invalid reasoning costs");
        _ = F_dv_require(R.lt(R.number("0"), v_budget), "Invalid proof budget");
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("step", v_step), F_dv_pair("evidence", v_evidence), F_dv_pair("assumption", v_assumption), F_dv_pair("conflict", v_conflict) }, new System.Collections.Generic.List<object>(4) { F_dv_pair("reference", v_reference), F_dv_pair("maximum", v_maximum), F_dv_pair("allow_assumptions", v_allow_assumptions), F_dv_pair("budget", v_budget) });
    }

    public static object F_dv_rule_concludes(object v_law, object v_conclusion)
    {
        object v_effects__106537 = F_dv_get(v_law, "effects");
        object v_i__106567 = R.number("0");
        object v_found__106581 = false;
        while (R.Bool(R.lt(v_i__106567, R.count(v_effects__106537))))
        {
            object v_effect__106642 = R.at(v_effects__106537, v_i__106567);
            if (R.Bool(R.eq(F_dv_get(v_effect__106642, "op"), "assign")))
            {
                if (R.Bool(F_dv_same(v_conclusion, F_dv_fact(F_dv_get(v_effect__106642, "ref"), F_dv_get(v_effect__106642, "value")))))
                {
                    v_found__106581 = true;
                }
            }
            v_i__106567 = R.add(v_i__106567, R.number("1"));
        }
        return v_found__106581;
    }

    public static object F_dv_match_premises(object v_premise, object v_supplied)
    {
        if (R.Bool(F_dv_same(v_premise, F_dv_true())))
        {
            return R.eq(R.count(v_supplied), R.number("0"));
        }
        else
        {
            if (R.Bool(R.eq(F_dv_get(v_premise, "op"), "all")))
            {
                object v_required__107014 = F_dv_get(v_premise, "items");
                object v_i__107055 = R.number("0");
                object v_valid__107077 = R.eq(R.count(v_supplied), R.count(v_required__107014));
                while (R.Bool(R.lt(v_i__107055, R.count(v_required__107014))))
                {
                    v_valid__107077 = F_dv_and(v_valid__107077, F_dv_has(v_supplied, R.at(v_required__107014, v_i__107055)));
                    v_i__107055 = R.add(v_i__107055, R.number("1"));
                }
                return v_valid__107077;
            }
            else
            {
                return F_dv_same(v_supplied, F_dv_single(v_premise));
            }
        }
    }

    public static object F_dv_check_step(object v_world, object v_step, object v_checked, object v_policy)
    {
        object v_kind__107422 = F_dv_get(v_step, "kind");
        object v_conclusion__107447 = F_dv_get(v_step, "conclusion");
        object v_parents__107484 = F_dv_get(v_step, "parents");
        object v_supplied__107515 = new System.Collections.Generic.List<object>(0) { };
        object v_confidence__107537 = R.number("1");
        object v_i__107560 = R.number("0");
        object v_hypothetical__107574 = false;
        while (R.Bool(R.lt(v_i__107560, R.count(v_parents__107484))))
        {
            object v_parent__107642 = F_dv_get(v_checked, R.at(v_parents__107484, v_i__107560));
            _ = R.push(v_supplied__107515, F_dv_get(v_parent__107642, "conclusion"));
            v_confidence__107537 = F_dv_min(v_confidence__107537, F_dv_get(v_parent__107642, "confidence"));
            v_hypothetical__107574 = F_dv_or(v_hypothetical__107574, F_dv_get(v_parent__107642, "hypothetical"));
            v_i__107560 = R.add(v_i__107560, R.number("1"));
        }
        object v_assumptions__107875 = R.number("0");
        object v_conflicts__107899 = R.number("0");
        object v_local_uncertainty__107921 = R.number("0");
        if (R.Bool(R.eq(v_kind__107422, "known")))
        {
            _ = F_dv_require(R.eq(R.count(v_parents__107484), R.number("0")), "Known premise cannot hide parents");
            _ = F_dv_require(R.eq(F_dv_truth(v_world, v_conclusion__107447), "true"), "Conclusion is not available in this view");
            v_confidence__107537 = F_dv_premise_confidence(v_world, v_conclusion__107447);
            v_local_uncertainty__107921 = R.sub(R.number("1"), v_confidence__107537);
        }
        else
        {
            if (R.Bool(R.eq(v_kind__107422, "assume")))
            {
                _ = F_dv_require(R.eq(R.count(v_parents__107484), R.number("0")), "Assumption cannot hide parents");
                _ = F_dv_require(F_dv_and(F_dv_get(v_policy, "allow_assumptions"), R.not(R.eq(F_dv_get(v_world, "domain"), "physical"))), "Assumption not permitted");
                v_hypothetical__107574 = true;
                v_assumptions__107875 = R.number("1");
                v_confidence__107537 = R.number("0");
                if (R.Bool(R.eq(F_dv_truth(v_world, v_conclusion__107447), "false")))
                {
                    v_conflicts__107899 = R.number("1");
                }
            }
            else
            {
                if (R.Bool(R.eq(v_kind__107422, "axiom")))
                {
                    object v_adoption__108749 = F_dv_get(F_dv_get(v_world, "adoptions"), F_dv_get(v_step, "law"));
                    object v_law__108814 = F_dv_get(v_adoption__108749, "law");
                    _ = F_dv_require(F_dv_get(v_adoption__108749, "active"), "Proof uses a suspended axiom");
                    _ = F_dv_require(R.eq(F_dv_get(v_adoption__108749, "mode"), "infer"), "Operational effects do not prove that an event occurred");
                    _ = F_dv_require(F_dv_rule_concludes(v_law__108814, v_conclusion__107447), "Axiom does not conclude this proposition");
                    _ = F_dv_require(F_dv_match_premises(F_dv_get(v_law__108814, "premise"), v_supplied__107515), "Axiom premises are not established");
                    v_confidence__107537 = F_dv_min(v_confidence__107537, F_dv_get(v_adoption__108749, "confidence"));
                    v_local_uncertainty__107921 = R.sub(R.number("1"), F_dv_get(v_adoption__108749, "confidence"));
                }
                else
                {
                    _ = R.fail("Unsupported proof step");
                }
            }
        }
        return F_dv_with_many(F_dv_clone(v_step), new System.Collections.Generic.List<object>(5) { F_dv_pair("confidence", v_confidence__107537), F_dv_pair("hypothetical", v_hypothetical__107574), F_dv_pair("assumptions", v_assumptions__107875), F_dv_pair("conflicts", v_conflicts__107899), F_dv_pair("local_uncertainty", v_local_uncertainty__107921) });
    }

    public static object F_dv_judge_conclusion(object v_world, object v_conclusion, object v_proof, object v_policy)
    {
        _ = F_dv_require(R.lt(R.number("0"), R.count(v_proof)), "Empty derivation");
        _ = F_dv_require(R.not(R.lt(F_dv_get(v_policy, "budget"), R.count(v_proof))), "Derivation budget exhausted");
        object v_checked__109838 = new System.Collections.Generic.List<object>(0) { };
        object v_i__109859 = R.number("0");
        while (R.Bool(R.lt(v_i__109859, R.count(v_proof))))
        {
            object v_step__109910 = R.at(v_proof, v_i__109859);
            object v_id__109938 = F_dv_get(v_step__109910, "id");
            _ = F_dv_require(R.not(F_dv_has_key(v_checked__109838, v_id__109938)), "Repeated proof node identity");
            v_checked__109838 = F_dv_with(v_checked__109838, v_id__109938, F_dv_check_step(v_world, v_step__109910, v_checked__109838, v_policy));
            v_i__109859 = R.add(v_i__109859, R.number("1"));
        }
        object v_root__110145 = F_dv_get(v_checked__109838, F_dv_get(R.at(v_proof, R.sub(R.count(v_proof), R.number("1"))), "id"));
        object v_queue__110209 = F_dv_single(F_dv_get(v_root__110145, "id"));
        object v_visited__110244 = new System.Collections.Generic.List<object>(0) { };
        object v_cursor__110265 = R.number("0");
        object v_steps__110284 = R.number("0");
        object v_uncertainty__110302 = R.number("0");
        object v_assumptions__110326 = R.number("0");
        object v_conflicts__110350 = R.number("0");
        _ = F_dv_require(F_dv_same(v_conclusion, F_dv_get(v_root__110145, "conclusion")), "Derivation concludes another proposition");
        while (R.Bool(R.lt(v_cursor__110265, R.count(v_queue__110209))))
        {
            object v_id__110511 = R.at(v_queue__110209, v_cursor__110265);
            if (R.Bool(F_dv_has(v_visited__110244, v_id__110511)))
            {
                _ = false;
            }
            else
            {
                object v_step__110610 = F_dv_get(v_checked__109838, v_id__110511);
                object v_parents__110653 = F_dv_get(v_step__110610, "parents");
                object v_p__110692 = R.number("0");
                _ = R.push(v_visited__110244, v_id__110511);
                v_steps__110284 = R.add(v_steps__110284, R.number("1"));
                v_uncertainty__110302 = R.add(v_uncertainty__110302, F_dv_get(v_step__110610, "local_uncertainty"));
                v_assumptions__110326 = R.add(v_assumptions__110326, F_dv_get(v_step__110610, "assumptions"));
                v_conflicts__110350 = R.add(v_conflicts__110350, F_dv_get(v_step__110610, "conflicts"));
                while (R.Bool(R.lt(v_p__110692, R.count(v_parents__110653))))
                {
                    _ = R.push(v_queue__110209, R.at(v_parents__110653, v_p__110692));
                    v_p__110692 = R.add(v_p__110692, R.number("1"));
                }
            }
            v_cursor__110265 = R.add(v_cursor__110265, R.number("1"));
        }
        object v_cost__111092 = R.add(R.add(R.mul(v_steps__110284, F_dv_get(v_policy, "step")), R.mul(v_uncertainty__110302, F_dv_get(v_policy, "evidence"))), R.add(R.mul(v_assumptions__110326, F_dv_get(v_policy, "assumption")), R.mul(v_conflicts__110350, F_dv_get(v_policy, "conflict"))));
        object v_components__111237 = new System.Collections.Generic.List<object>(4) { F_dv_pair("steps", v_steps__110284), F_dv_pair("evidence_uncertainty", v_uncertainty__110302), F_dv_pair("assumptions", v_assumptions__110326), F_dv_pair("conflicts", v_conflicts__110350) };
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("valid", true), F_dv_pair("cost", v_cost__111092), F_dv_pair("components", v_components__111237), F_dv_pair("reasonable", R.not(R.lt(F_dv_get(v_policy, "maximum"), v_cost__111092))) }, new System.Collections.Generic.List<object>(5) { F_dv_pair("reasonableness", R.div(R.number("1"), R.add(R.number("1"), R.div(v_cost__111092, F_dv_get(v_policy, "reference"))))), F_dv_pair("confidence", F_dv_get(v_root__110145, "confidence")), F_dv_pair("hypothetical", F_dv_or(F_dv_get(v_root__110145, "hypothetical"), R.eq(F_dv_get(v_world, "domain"), "prediction"))), F_dv_pair("scope", new System.Collections.Generic.List<object>(3) { F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("subject", F_dv_get(v_world, "subject")), F_dv_pair("revision", F_dv_get(v_world, "revision")) }), F_dv_pair("basis", v_visited__110244) });
    }
}
}

// --- Source: CaseProofScript.cs ---
namespace DuelView.Core
{


/// <summary>Elaborates a small Lean-inspired proof language into checker input.
/// This is not a DV parser or an implementation of Lean's type theory.</summary>
public sealed class CaseProofScript
{
    private readonly List<string> tokens = new();
    private int at;
    private readonly List<object> scopes = new();
    private readonly Dictionary<string, CaseAxiom> axioms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CaseRule> rules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CaseProof> lemmas = new(StringComparer.Ordinal);
    private readonly HashSet<string> declarations = new(StringComparer.Ordinal);
    private string scope = "";

    private CaseProofScript(string source)
    {
        if (source.Length > 1_000_000) throw new FormatException("Case script exceeds source budget");
        var pattern = new Regex(@"\G(?:\s+|--[^\r\n]*|(?<token>:=|->|→|∧|¬|[{}(),:;=&]|[?]?[\p{L}\p{N}_](?:[\p{L}\p{N}_.?/]|-(?!>))*))", RegexOptions.CultureInvariant);
        var offset = 0;
        while (offset < source.Length)
        {
            var match = pattern.Match(source, offset);
            if (!match.Success) throw new FormatException("Invalid case script character at offset " + offset);
            if (match.Groups["token"].Success) tokens.Add(match.Groups["token"].Value);
            offset += match.Length;
        }
        if (tokens.Count > 100_000) throw new FormatException("Case script exceeds token budget");
    }

    public static JsonElement Elaborate(string script, JsonElement? metadata = null)
    {
        var parser = new CaseProofScript(script);
        parser.Read();
        var result = new Dictionary<string, object> { ["scopes"] = parser.scopes };
        if (metadata is { } m && m.TryGetProperty("candidates", out var candidates)) result["candidates"] = candidates.Clone();
        return JsonSerializer.SerializeToElement(result);
    }

    private string Peek => at < tokens.Count ? tokens[at] : "<eof>";
    private bool Take(string token) { if (Peek != token) return false; at++; return true; }
    private void Need(string token) { if (!Take(token)) throw Error("Expected '" + token + "'"); }
    private FormatException Error(string message) => new(message + " at token " + at + " ('" + Peek + "')");
    private string Name()
    {
        var value = Peek;
        if (value == "<eof>" || !Regex.IsMatch(value, @"^[?]?[\p{L}\p{N}_][\p{L}\p{N}_.?/-]*$")) throw Error("Expected name");
        at++; return value;
    }
    private CaseLiteral Literal()
    {
        var negative = Take("not") || Take("¬");
        var predicate = Name();
        var args = new List<string>();
        if (Take("("))
        {
            if (!Take(")"))
            {
                do { args.Add(Name()); } while (Take(","));
                Need(")");
            }
        }
        return new CaseLiteral(predicate, args.AsReadOnly(), negative);
    }
    private void Read()
    {
        var scopeIds = new HashSet<string>(StringComparer.Ordinal);
        while (Peek != "<eof>")
        {
            Need("scope"); scope = Name();
            if (!scopeIds.Add(scope)) throw Error("Duplicate scope");
            var braced = Take("{");
            axioms.Clear(); rules.Clear(); lemmas.Clear(); declarations.Clear();
            var theoremRecords = new List<object>();
            while (Peek != (braced ? "}" : "end"))
            {
                var kind = Name();
                if (kind != "axiom" && kind != "theorem" && kind != "query") throw Error("Expected axiom, theorem or query");
                var id = Name();
                if (!declarations.Add(id)) throw Error("Duplicate declaration: " + id);
                Need(":");
                var first = Literal();
                if (kind == "axiom")
                {
                    var premises = new List<CaseLiteral> { first };
                    while (Take("&") || Take("∧")) premises.Add(Literal());
                    if (Take("->") || Take("→"))
                    {
                        var conclusion = Literal();
                        // Curried implications are elaborated to a conjunction of premises.
                        while (Take("->") || Take("→")) { premises.Add(conclusion); conclusion = Literal(); }
                        rules.Add(id, new CaseRule(id, premises.AsReadOnly(), conclusion, "script:" + scope + ":" + id));
                    }
                    else
                    {
                        if (premises.Count != 1 || !first.Ground) throw Error("Fact axiom must be one ground literal");
                        axioms.Add(id, new CaseAxiom(id, first, "script:" + scope + ":" + id));
                    }
                    Need(";");
                }
                else if (kind == "query")
                {
                    if (!first.Ground) throw Error("Query goal must be ground");
                    Need(";");
                    // A query declares a question, never an accepted premise or lemma.
                    theoremRecords.Add(new { id, conclusion = Atom(first) });
                }
                else
                {
                    if (!first.Ground) throw Error("Theorem goal must be ground");
                    Need(":="); Need("by");
                    var proofBraced = Take("{");
                    var nodes = new List<CaseProofNode>();
                    Prove(first, nodes, 0);
                    if (proofBraced) Need("}");
                    Take(";");
                    var context = new CaseScope(scope, axioms.Values.ToArray(), rules.Values.ToArray(), System.Array.Empty<CaseTheorem>());
                    var proof = new CaseProof(scope, nodes.AsReadOnly(), CaseTheory.Fingerprint(context));
                    var checkedProof = CaseProofChecker.CheckGoal(context, proof, first);
                    if (!checkedProof.Valid) throw Error("Elaborated proof rejected: " + string.Join("; ", checkedProof.Errors));
                    lemmas.Add(id, proof);
                    theoremRecords.Add(new { id, conclusion = Atom(first), proof = nodes.Select(Node).ToArray() });
                }
            }
            at++; Take(";");
            scopes.Add(new
            {
                id = scope,
                axioms = axioms.Values.Select(a => new { id = a.Id, literal = Atom(a.Literal), source = a.Source }).ToArray(),
                rules = rules.Values.Select(r => new { id = r.Id, premises = r.Premises.Select(Atom).ToArray(), conclusion = Atom(r.Conclusion), source = r.Source }).ToArray(),
                theorems = theoremRecords
            });
        }
        if (scopes.Count == 0) throw Error("Expected at least one scope");
    }

    private string Prove(CaseLiteral goal, List<CaseProofNode> nodes, int depth, IReadOnlyDictionary<string, string>? inherited = null)
    {
        if (depth > 256 || nodes.Count >= 10000) throw Error("Proof elaboration budget exhausted");
        var locals = inherited is null ? new Dictionary<string, string>(StringComparer.Ordinal)
            : inherited.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        // Each subproof owns its local declarations. A local lemma is available only
        // after its proof finishes, and cannot escape into a sibling or later theorem.
        while (Take("have"))
        {
            var name = Name();
            if (declarations.Contains(name) || locals.ContainsKey(name)) throw Error("Duplicate local declaration: " + name);
            Need(":"); var claim = Literal();
            if (!claim.Ground) throw Error("Local lemma must be ground");
            Need(":="); Need("by"); Need("{");
            var proved = Prove(claim, nodes, depth + 1, locals);
            Need("}"); Take(";");
            locals.Add(name, proved);
        }
        var tactic = Name();
        var reference = Name();
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Take("("))
        {
            if (!Take(")"))
            {
                do
                {
                    var variable = Name(); Need("="); var value = Name();
                    if (!variable.StartsWith('?') || value.StartsWith('?') || !bindings.TryAdd(variable, value)) throw Error("Invalid explicit rule binding");
                } while (Take(","));
                Need(")");
            }
        }
        Need(";");
        string Add(string kind, string target, IReadOnlyList<string> parents, IReadOnlyDictionary<string, string> substitution)
        {
            if (nodes.Count >= 10000) throw Error("Proof node budget exhausted");
            var id = "s" + nodes.Count;
            nodes.Add(new CaseProofNode(id, kind, goal, target, parents, substitution));
            return id;
        }
        if (tactic == "exact")
        {
            if (bindings.Count != 0) throw Error("exact does not accept bindings");
            if (locals.TryGetValue(reference, out var local))
            {
                if (nodes.Single(n => n.Id == local).Conclusion.Key != goal.Key) throw Error("exact local lemma does not match goal");
                return Add("exact", "", new[] { local }, bindings);
            }
            if (axioms.TryGetValue(reference, out var axiom))
            {
                if (axiom.Literal.Key != goal.Key) throw Error("exact axiom does not match goal");
                return Add("axiom", reference, System.Array.Empty<string>(), bindings);
            }
            if (lemmas.TryGetValue(reference, out var lemma))
            {
                if (lemma.Nodes[^1].Conclusion.Key != goal.Key) throw Error("exact theorem does not match goal");
                var remap = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var node in lemma.Nodes)
                {
                    if (nodes.Count >= 10000) throw Error("Proof node budget exhausted");
                    var id = "s" + nodes.Count;
                    remap.Add(node.Id, id);
                    nodes.Add(node with { Id = id, Parents = node.Parents.Select(p => remap[p]).ToArray() });
                }
                return Add("exact", "", new[] { remap[lemma.Nodes[^1].Id] }, bindings);
            }
            throw Error("Unknown local axiom or prior theorem: " + reference);
        }
        if (tactic != "apply" || !rules.TryGetValue(reference, out var rule)) throw Error("Expected apply with a local rule");
        var variables = rule.Premises.Append(rule.Conclusion).SelectMany(l => l.Args).Where(a => a.StartsWith('?')).ToHashSet(StringComparer.Ordinal);
        if (bindings.Keys.Any(k => !variables.Contains(k))) throw Error("Unknown rule variable");
        if (rule.Conclusion.Predicate != goal.Predicate || rule.Conclusion.Negative != goal.Negative || rule.Conclusion.Args.Count != goal.Args.Count) throw Error("Rule conclusion does not match goal");
        for (var i = 0; i < goal.Args.Count; i++)
        {
            var term = rule.Conclusion.Args[i]; var value = goal.Args[i];
            if (term.StartsWith('?'))
            {
                if (bindings.TryGetValue(term, out var previous) && previous != value) throw Error("Inconsistent rule binding");
                bindings[term] = value;
            }
            else if (term != value) throw Error("Rule conclusion does not match goal");
        }
        if (!variables.SetEquals(bindings.Keys)) throw Error("Premise-only variables require explicit bindings, e.g. apply rule(?x=a)");
        var parents = rule.Premises.Select(p => Prove(p.Substitute(bindings), nodes, depth + 1, locals)).ToArray();
        return Add("apply", reference, parents, bindings);
    }

    private static object Atom(CaseLiteral literal) => new { predicate = literal.Predicate, args = literal.Args, negative = literal.Negative };
    private static object Node(CaseProofNode node) => new { id = node.Id, kind = node.Kind, conclusion = Atom(node.Conclusion), reference = node.Reference, parents = node.Parents, substitution = node.Substitution };
}
}
