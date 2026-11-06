// @mechanism: signed_horn_reasoning
// @layer: core/reasoning
// @contracts: CaseBranches

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;

// --- Source: CaseBranches.cs ---
namespace DuelView.Core
{


/// <summary>Checks declared counterfactual coverage and conditional reaction proofs.
/// A successful audit does not turn scenario assumptions into world facts.</summary>
public static class CaseBranches
{
    public sealed record Check(string Event, string Branch, string Kind, string Id, string Expected, string Actual)
    {
        public bool Passed => Expected == Actual;
    }

    public static IReadOnlyList<Check> Audit(CaseTheory theory, JsonElement declaration)
    {
        var checks = new List<Check>();
        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ev in declaration.GetProperty("events").EnumerateArray())
        {
            var eventId = Text(ev, "id");
            if (!eventIds.Add(eventId)) throw new InvalidDataException("Duplicate branch event: " + eventId);
            var required = ev.GetProperty("decisions").EnumerateArray().Select(x => x.GetString()!).ToArray();
            if (required.Length == 0 || required.Any(string.IsNullOrWhiteSpace) || required.Distinct().Count() != required.Length)
                throw new InvalidDataException("Decision classes must be nonempty and unique.");
            var dimensions = ev.GetProperty("reaction_dimensions").EnumerateArray().Select(x => x.GetString()!).ToArray();
            if (dimensions.Length == 0 || dimensions.Any(string.IsNullOrWhiteSpace) || dimensions.Distinct().Count() != dimensions.Length)
                throw new InvalidDataException("Reaction dimensions must be nonempty and unique.");
            var covered = new HashSet<string>(StringComparer.Ordinal);
            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var branch in ev.GetProperty("branches").EnumerateArray())
            {
                var id = Text(branch, "id");
                if (!branchIds.Add(id)) throw new InvalidDataException("Duplicate branch: " + id);
                var decision = Text(branch, "decision");
                if (!required.Contains(decision)) throw new InvalidDataException("Undeclared decision: " + decision);
                covered.Add(decision);
                // An explicitly inapplicable class is recorded with its reason;
                // it is never silently dropped from the coverage denominator.
                if (branch.TryGetProperty("inapplicable", out var reason))
                {
                    checks.Add(new(eventId, id, "applicability", decision, "explained", string.IsNullOrWhiteSpace(reason.GetString()) ? "missing" : "explained"));
                    continue;
                }
                if (!branch.GetProperty("counterfactual").GetBoolean())
                    throw new InvalidDataException("Branch audit requires explicitly counterfactual premises.");
                var scopeId = Text(branch, "scope");
                var scope = theory.Scopes[scopeId];
                if (branch.GetProperty("expect").EnumerateObject().Count() == 0 ||
                    branch.GetProperty("reactions").GetArrayLength() == 0)
                    throw new InvalidDataException("Executable branch needs outcomes and reactions: " + id);
                var proof = theory.Infer(scopeId);
                var certificate = CaseProofChecker.Check(scope, proof);
                if (!certificate.Valid) throw new InvalidDataException("Invalid branch certificate: " + id);
                var facts = proof.Nodes.Select(n => n.Conclusion.Key).ToHashSet(StringComparer.Ordinal);
                foreach (var goal in branch.GetProperty("expect").EnumerateObject())
                {
                    var literal = scope.Theorems.Single(t => t.Id == goal.Name).Conclusion;
                    var positive = facts.Contains(literal.Key); var negative = facts.Contains(literal.Opposite.Key);
                    var actual = positive && negative ? "conflicted" : positive ? "proved" : negative ? "refuted" : "unknown";
                    checks.Add(new(eventId, id, "outcome", goal.Name, goal.Value.GetString()!, actual));
                }
                foreach (var reaction in branch.GetProperty("reactions").EnumerateArray())
                {
                    var actor = Text(reaction, "actor");
                    var rule = scope.Rules.Single(r => r.Id == Text(reaction, "rule"));
                    var expectedProof = Expectation(reaction, "expect_proof");
                    var expectedBasis = reaction.TryGetProperty("expect_basis", out var basisExpectations)
                        ? basisExpectations.EnumerateObject().ToDictionary(p => p.Name, p => ExpectedProofState(p.Value), StringComparer.Ordinal)
                        : new Dictionary<string, string>(StringComparer.Ordinal);
                    if (expectedBasis.Keys.Any(name => !dimensions.Contains(name)))
                        throw new InvalidDataException("Unknown reaction basis expectation dimension: " + id);
                    var bindings = reaction.TryGetProperty("substitution", out var substitution)
                        ? substitution.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal)
                        : new Dictionary<string, string>(StringComparer.Ordinal);
                    var variables = rule.Premises.Append(rule.Conclusion).SelectMany(p => p.Args).Where(a => a.StartsWith('?')).ToHashSet(StringComparer.Ordinal);
                    if (!variables.SetEquals(bindings.Keys) || bindings.Values.Any(v => string.IsNullOrWhiteSpace(v) || v.StartsWith('?')))
                        throw new InvalidDataException("Reaction must supply exactly the rule's ground variable bindings: " + id);
                    var instantiated = rule.Premises.Select(p => p.Substitute(bindings)).ToArray();
                    var conclusion = rule.Conclusion.Substitute(bindings);
                    var premises = instantiated.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
                    var byLiteral = proof.Nodes.ToDictionary(n => n.Conclusion.Key, StringComparer.Ordinal);
                    // A rule declaration is not proof that its prerequisites hold. Construct
                    // its actual application even if search found the conclusion by another rule.
                    var parents = instantiated.Where(p => byLiteral.ContainsKey(p.Key)).Select(p => byLiteral[p.Key].Id).ToArray();
                    var applied = new CaseProofNode("reaction-check", "apply", conclusion, rule.Id, parents, bindings);
                    var reactionProof = proof with { Nodes = proof.Nodes.Append(applied).ToArray() };
                    var proved = parents.Length == instantiated.Length;
                    if (proved)
                    {
                        var checkedReaction = CaseProofChecker.CheckGoal(scope, reactionProof, conclusion);
                        if (!checkedReaction.Valid)
                            throw new InvalidDataException("Reaction certificate rejected; no unprovability conclusion is available: " + string.Join("; ", checkedReaction.Errors));
                    }
                    // A negative fixture must explicitly expect missing support. This checks
                    // applicability of this rule instance; branch outcomes separately check its
                    // conclusion across the complete theory. Unproved is not explicit negation.
                    checks.Add(new(eventId, id, "reaction-proof", actor + ":" + rule.Id, expectedProof, proved ? "proved" : "unproved"));
                    checks.Add(new(eventId, id, "reaction-actor", actor, "bound", conclusion.Args.Contains(actor) ? "bound" : "missing"));
                    foreach (var name in dimensions)
                    {
                        var specified = reaction.GetProperty("basis").TryGetProperty(name, out var literal);
                        var atom = specified ? literal.Deserialize<CaseLiteral>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
                        var declared = atom is not null && CaseProofChecker.ValidLiteral(atom, true) && premises.Contains(atom.Key);
                        checks.Add(new(eventId, id, "reaction-premise", actor + ":" + name, "declared", declared ? "declared" : "missing"));
                        var expectedEvidence = expectedBasis.TryGetValue(name, out var expectation) ? expectation : "proved";
                        checks.Add(new(eventId, id, "reaction-evidence", actor + ":" + name, expectedEvidence, declared && facts.Contains(atom!.Key) ? "proved" : "unproved"));
                    }
                }
            }
            foreach (var decision in required)
                checks.Add(new(eventId, "coverage", "decision", decision, "covered", covered.Contains(decision) ? "covered" : "missing"));
        }
        return checks.AsReadOnly();
    }

    private static string Text(JsonElement value, string key) =>
        value.GetProperty(key).GetString() is { Length: > 0 } text ? text : throw new InvalidDataException("Missing " + key);

    private static string Expectation(JsonElement value, string key) =>
        value.TryGetProperty(key, out var expected) ? ExpectedProofState(expected) : "proved";

    private static string ExpectedProofState(JsonElement value) => value.GetString() is "proved" or "unproved" ? value.GetString()!
        : throw new InvalidDataException("Reaction proof expectation must be proved or unproved.");
}
}

// --- Source: CaseFiniteModel.cs ---
namespace DuelView.Core
{


public sealed record CaseFiniteModelAudit(string Id, string Scope, string Expected, string Actual,
    bool Passed, IReadOnlyList<string> Errors, string TheoryHash = "");

/// <summary>
/// Independently checks a supplied classical, finite, function-free interpretation.
/// This is an authoring witness checker, not model search and not actor inference.
/// Signed negatives mean Boolean complement here only. Unlisted positive atoms are false.
/// A valid witness proves satisfiability in its declared domain; a false assertion target
/// can witness non-entailment. It does not establish uniqueness or physical executability.
/// </summary>
public static class CaseFiniteModel
{
    private sealed class BudgetExceeded : Exception { }

    public static IReadOnlyList<CaseFiniteModelAudit> Audit(CaseTheory theory, JsonElement declaration,
        int maxEvaluations = 1000000)
    {
        if (maxEvaluations < 1) throw new ArgumentOutOfRangeException(nameof(maxEvaluations));
        var results = new List<CaseFiniteModelAudit>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            Fields(declaration, "checks");
            var checks = RequiredArray(declaration, "checks");
            if (checks.GetArrayLength() == 0) throw new FormatException("At least one model check is required");
            if (checks.GetArrayLength() > 1000)
                return new[] { new CaseFiniteModelAudit("case-models", "", "valid", "incomplete", false,
                    new[] { "Model check count exceeds the audit budget" }) };
            foreach (var check in checks.EnumerateArray())
            {
                var id = "case-model-" + results.Count;
                var scopeId = "";
                var expected = "valid";
                var actual = "invalid";
                var hash = "";
                var errors = new List<string>();
                var expectationValid = false;
                try
                {
                    // Read the expectation before other validation so deliberately malformed
                    // model fixtures may assert rejection, but never assert budget exhaustion.
                    if (check.ValueKind == JsonValueKind.Object && check.TryGetProperty("expect", out var e))
                        expected = e.ValueKind == JsonValueKind.String ? e.GetString()! : "";
                    expectationValid = expected is "valid" or "invalid";
                    if (!expectationValid) throw new FormatException("expect must be valid or invalid");
                    Fields(check, "id", "scope", "domain", "true_atoms", "assertions", "expect");
                    id = Text(check.GetProperty("id"));
                    scopeId = Text(check.GetProperty("scope"));
                    if (!ids.Add(id)) throw new FormatException("Duplicate model check id: " + id);
                    if (theory is null || !theory.Scopes.TryGetValue(scopeId, out var scope))
                        throw new FormatException("Unknown model scope: " + scopeId);
                    var validation = CaseProofChecker.ValidateTheory(scope);
                    if (!validation.Valid) throw new FormatException(string.Join("; ", validation.Errors));
                    hash = CaseTheory.Fingerprint(scope);
                    Check(theory, scope, check, maxEvaluations);
                    actual = "valid";
                }
                catch (BudgetExceeded)
                {
                    actual = "incomplete";
                    errors.Add("Finite model evaluation budget exhausted; no validity or invalidity conclusion");
                }
                catch (Exception ex) when (ex is FormatException or InvalidOperationException or KeyNotFoundException or ArgumentException)
                { errors.Add(ex.Message); }
                results.Add(new(id, scopeId, expected, actual,
                    expectationValid && actual != "incomplete" && actual == expected, errors.AsReadOnly(), hash));
            }
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        { results.Add(new("case-models", "", "valid", "invalid", false, new[] { ex.Message })); }
        return results.AsReadOnly();
    }

    private static void Check(CaseTheory theory, CaseScope scope, JsonElement check, int budget)
    {
        var remaining = budget;
        void Spend() { if (remaining-- <= 0) throw new BudgetExceeded(); }
        var domain = new List<string>();
        var constants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in RequiredArray(check, "domain").EnumerateArray())
        {
            Spend();
            var name = Text(entry);
            if (name.StartsWith('?')) throw new FormatException("Domain members must be constants");
            if (!constants.Add(name)) throw new FormatException("Duplicate domain member: " + name);
            domain.Add(name);
        }
        if (domain.Count == 0) throw new FormatException("Finite domain must be nonempty");
        var arities = new Dictionary<string, int>(StringComparer.Ordinal);
        void Register(CaseLiteral literal)
        {
            Spend();
            if (arities.TryGetValue(literal.Predicate, out var arity) && arity != literal.Args.Count)
                throw new FormatException("Inconsistent predicate arity: " + literal.Predicate);
            arities[literal.Predicate] = literal.Args.Count;
            foreach (var term in literal.Args)
            {
                Spend();
                if (!term.StartsWith('?') && !constants.Contains(term))
                    throw new FormatException("Constant outside declared model domain: " + term);
            }
        }
        foreach (var axiom in scope.Axioms) Register(axiom.Literal);
        foreach (var rule in scope.Rules)
        {
            foreach (var premise in rule.Premises) Register(premise);
            Register(rule.Conclusion);
        }
        // A theorem without a checked proof is a goal, not an extra model axiom.
        // Still include the signature/constants of goals and declared candidates.
        foreach (var theorem in scope.Theorems) Register(theorem.Conclusion);
        foreach (var candidate in theory.Candidates.Where(c => c.Scope == scope.Id))
            foreach (var literal in candidate.Requires.Concat(candidate.Excludes)) Register(literal);

        var truths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in RequiredArray(check, "true_atoms").EnumerateArray())
        {
            Spend();
            var literal = Literal(entry);
            Register(literal);
            if (literal.Negative) throw new FormatException("true_atoms must contain positive atoms only");
            if (!truths.Add(literal.Key)) throw new FormatException("Duplicate true atom: " + literal.Key);
        }
        var assertions = new List<(CaseLiteral Literal, bool Value)>();
        if (check.TryGetProperty("assertions", out _))
            foreach (var entry in RequiredArray(check, "assertions").EnumerateArray())
            {
                Spend();
                Fields(entry, "literal", "value");
                var literal = Literal(entry.GetProperty("literal"));
                Register(literal);
                var value = entry.GetProperty("value");
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new FormatException("Assertion value must be Boolean");
                assertions.Add((literal, value.GetBoolean()));
            }

        bool Holds(CaseLiteral literal, IReadOnlyDictionary<string, string>? bindings = null)
        {
            Spend();
            var grounded = bindings is null ? literal : literal.Substitute(bindings);
            var positive = grounded with { Negative = false };
            var present = truths.Contains(positive.Key);
            return literal.Negative ? !present : present;
        }
        foreach (var axiom in scope.Axioms)
            if (!Holds(axiom.Literal)) throw new FormatException("Model violates axiom: " + axiom.Id);
        foreach (var rule in scope.Rules)
        {
            var variables = rule.Premises.Append(rule.Conclusion).SelectMany(l => l.Args)
                .Where(a => a.StartsWith('?')).Distinct(StringComparer.Ordinal).ToArray();
            var digits = new int[variables.Length];
            var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            while (true)
            {
                Spend();
                for (var i = 0; i < variables.Length; i++)
                { Spend(); bindings[variables[i]] = domain[digits[i]]; }
                if (rule.Premises.All(p => Holds(p, bindings)) && !Holds(rule.Conclusion, bindings))
                    throw new FormatException("Model violates rule " + rule.Id + " at " + JsonSerializer.Serialize(bindings));
                var position = digits.Length - 1;
                while (position >= 0 && ++digits[position] == domain.Count)
                { digits[position] = 0; position--; }
                if (position < 0) break;
            }
        }
        foreach (var assertion in assertions)
            if (Holds(assertion.Literal) != assertion.Value)
                throw new FormatException("Model assertion does not match interpretation: " + assertion.Literal.Key);
    }

    private static CaseLiteral Literal(JsonElement value)
    {
        Fields(value, "predicate", "args", "negative");
        var predicate = Text(value.GetProperty("predicate"));
        var args = RequiredArray(value, "args").EnumerateArray().Select(Text).ToArray();
        var negative = false;
        if (value.TryGetProperty("negative", out var sign))
        {
            if (sign.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new FormatException("Literal negative must be Boolean");
            negative = sign.GetBoolean();
        }
        var literal = new CaseLiteral(predicate, Array.AsReadOnly(args), negative);
        if (!CaseProofChecker.ValidLiteral(literal, true)) throw new FormatException("Expected well-formed ground literal");
        return literal;
    }

    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new FormatException("Expected nonempty string");

    private static JsonElement RequiredArray(JsonElement value, string field)
    {
        var array = value.GetProperty(field);
        return array.ValueKind == JsonValueKind.Array ? array : throw new FormatException("Expected array: " + field);
    }

    private static void Fields(JsonElement value, params string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new FormatException("Expected object");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
                throw new FormatException("Unknown or duplicate model field: " + property.Name);
    }
}
}

// --- Source: CaseInferenceWork.cs ---
namespace DuelView.Core
{


public enum CaseWorkStatus { Running, Completed, Cancelled, BudgetExhausted, Faulted }

/// <summary>Computation telemetry only; elapsed time never advances the game clock.</summary>
public sealed record CaseWorkSlice(CaseWorkStatus Status, int Operations, long TotalOperations,
    TimeSpan Elapsed, string Phase, string? Error);

/// <summary>A single-owner, resumable computation. A deadline is a Stopwatch timestamp,
/// not a game time or UTC date. Deadlines are cooperative between atomic operations.</summary>
public abstract class CaseWork<T> : IDisposable where T : class
{
    private IEnumerator<int>? cursor;
    private T? result;
    private ExceptionDispatchInfo? failure;
    public CaseWorkStatus Status { get; private set; } = CaseWorkStatus.Running;
    public long TotalOperations { get; private set; }
    public TimeSpan TotalElapsed { get; private set; }
    public string Phase { get; protected set; } = "created";
    public string? Error => failure?.SourceException.Message;
    public T Result => Status == CaseWorkStatus.Completed ? result! : throw new InvalidOperationException("Computation has no completed result: " + Status);
    protected abstract IEnumerable<int> Execute();
    protected void Finish(T value) => result = value;

    public CaseWorkSlice Advance(int maxOperations, TimeSpan timeBudget, CancellationToken cancellationToken = default)
    {
        if (timeBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeBudget));
        var now = Stopwatch.GetTimestamp();
        var ticks = timeBudget.TotalSeconds * Stopwatch.Frequency;
        var deadline = ticks >= long.MaxValue - now ? long.MaxValue : now + (long)ticks;
        return AdvanceUntil(maxOperations, deadline, cancellationToken);
    }

    public CaseWorkSlice AdvanceUntil(int maxOperations, long deadlineTimestamp, CancellationToken cancellationToken = default)
    {
        if (maxOperations < 1) throw new ArgumentOutOfRangeException(nameof(maxOperations));
        var started = Stopwatch.GetTimestamp();
        var operations = 0;
        try
        {
            if (Status == CaseWorkStatus.Running && cancellationToken.IsCancellationRequested) Cancel();
            while (Status == CaseWorkStatus.Running && operations < maxOperations && Stopwatch.GetTimestamp() < deadlineTimestamp)
            {
                if (cancellationToken.IsCancellationRequested) { Cancel(); break; }
                cursor ??= Execute().GetEnumerator();
                var more = cursor.MoveNext();
                operations++;
                TotalOperations++;
                if (!more)
                {
                    if (result is null) throw new InvalidOperationException("Computation ended without a result.");
                    Status = CaseWorkStatus.Completed;
                    cursor.Dispose();
                    cursor = null;
                }
            }
        }
        catch (Exception error)
        {
            result = null;
            failure = ExceptionDispatchInfo.Capture(error);
            Status = error is CaseWorkBudgetException ? CaseWorkStatus.BudgetExhausted : CaseWorkStatus.Faulted;
            cursor?.Dispose();
            cursor = null;
        }
        var elapsed = Stopwatch.GetElapsedTime(started);
        TotalElapsed += elapsed;
        return new(Status, operations, TotalOperations, elapsed, Phase, Error);
    }

    public T RunToCompletion(CancellationToken cancellationToken = default)
    {
        while (Status == CaseWorkStatus.Running) AdvanceUntil(4096, long.MaxValue, cancellationToken);
        if (Status == CaseWorkStatus.Cancelled) throw new OperationCanceledException(cancellationToken);
        failure?.Throw();
        return Result;
    }

    public void Cancel()
    {
        if (Status != CaseWorkStatus.Running) return;
        result = null;
        Status = CaseWorkStatus.Cancelled;
        cursor?.Dispose();
        cursor = null;
    }
    public void Dispose() => Cancel();
}

internal sealed class CaseWorkBudgetException(string message) : InvalidOperationException(message);

/// <summary>The existing deterministic Horn inference, suspended inside premise matching.
/// The caller supplies an immutable scope and must not mutate its collections while work is active.
/// No partial proof is published; completion includes the independent proof checker.</summary>
public sealed class CaseInferenceWork : CaseWork<CaseProof>
{
    private readonly CaseScope scope;
    private readonly int maxNodes, maxMatches;
    public long Matches { get; private set; }
    public int NodeCount { get; private set; }

    public CaseInferenceWork(CaseScope scope, int maxNodes = 10000, int maxMatches = 1000000)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (maxNodes < 1 || maxNodes > 10000) throw new ArgumentOutOfRangeException(nameof(maxNodes), "Proof node limit must be within 1..10000");
        if (maxMatches < 1) throw new ArgumentOutOfRangeException(nameof(maxMatches));
        this.scope = scope;
        this.maxNodes = maxNodes;
        this.maxMatches = maxMatches;
    }

    protected override IEnumerable<int> Execute()
    {
        Phase = "validate-theory";
        CaseCheckResult? theoryCheck = null;
        foreach (var step in CaseProofChecker.ValidateTheorySteps(scope, result => theoryCheck = result)) yield return step;
        if (!theoryCheck!.Valid) throw new ArgumentException(string.Join("; ", theoryCheck.Errors), nameof(scope));
        var facts = new Dictionary<string, CaseProofNode>(StringComparer.Ordinal);
        var steps = new List<CaseProofNode>();
        void Add(string kind, CaseLiteral literal, string reference, IReadOnlyList<string> parents, IReadOnlyDictionary<string, string> substitution)
        {
            if (facts.ContainsKey(literal.Key)) return;
            if (steps.Count >= maxNodes) throw new CaseWorkBudgetException("Case proof node budget exhausted; no completeness or uniqueness conclusion is available");
            var node = new CaseProofNode("p" + steps.Count, kind, literal, reference, parents, substitution);
            facts.Add(literal.Key, node);
            steps.Add(node);
            NodeCount = steps.Count;
        }
        void CountMatch()
        {
            if (++Matches > maxMatches) throw new CaseWorkBudgetException("Case match budget exhausted; no completeness or uniqueness conclusion is available");
        }
        Phase = "axioms";
        foreach (var axiom in scope.Axioms)
        {
            Add("axiom", axiom.Literal, axiom.Id, Array.Empty<string>(), new Dictionary<string, string>());
            yield return 0;
        }
        Phase = "match";
        bool changed;
        do
        {
            var before = steps.Count;
            foreach (var rule in scope.Rules)
            {
                // Freeze this rule's fact horizon, as the original array snapshot did.
                var availableCount = steps.Count;
                IEnumerable<int> Match(int index, Dictionary<string, string> substitution, List<string> parents)
                {
                    CountMatch();
                    yield return 0;
                    if (index == rule.Premises.Count)
                    {
                        Add("apply", rule.Conclusion.Substitute(substitution), rule.Id, parents.AsReadOnly(),
                            new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(substitution));
                        yield return 0;
                        yield break;
                    }
                    var required = rule.Premises[index];
                    for (var factIndex = 0; factIndex < availableCount; factIndex++)
                    {
                        var fact = steps[factIndex];
                        CountMatch();
                        yield return 0;
                        var actual = fact.Conclusion;
                        if (required.Predicate != actual.Predicate || required.Negative != actual.Negative || required.Args.Count != actual.Args.Count) continue;
                        var bindings = new Dictionary<string, string>(substitution, StringComparer.Ordinal);
                        var fits = true;
                        for (var i = 0; i < required.Args.Count; i++)
                        {
                            var term = required.Args[i]; var value = actual.Args[i];
                            if (term.StartsWith('?'))
                            { if (bindings.TryGetValue(term, out var old)) fits &= old == value; else bindings.Add(term, value); }
                            else fits &= term == value;
                            yield return 0;
                        }
                        if (fits)
                            foreach (var step in Match(index + 1, bindings, new List<string>(parents) { fact.Id })) yield return step;
                    }
                }
                foreach (var step in Match(0, new Dictionary<string, string>(StringComparer.Ordinal), new List<string>())) yield return step;
            }
            changed = before != steps.Count;
            yield return 0;
        } while (changed);
        Phase = "fingerprint";
        string? fingerprint = null;
        foreach (var step in CaseWorkFingerprint.Steps(scope, value => fingerprint = value)) yield return step;
        var proof = new CaseProof(scope.Id, steps.AsReadOnly(), fingerprint!);
        Phase = "check-proof";
        CaseCheckResult? check = null;
        foreach (var step in CaseProofChecker.CheckSteps(scope, proof, result => check = result)) yield return step;
        if (!check!.Valid) throw new InvalidOperationException("Generated proof rejected: " + string.Join("; ", check.Errors));
        Finish(proof);
    }
}

// Writes precisely the canonical { Id, Axioms, Rules } JSON used by CaseTheory.Fingerprint,
// but hashes each declaration as it is emitted instead of creating one large final buffer.
internal static class CaseWorkFingerprint
{
    internal static IEnumerable<int> Steps(CaseScope scope, Action<string> complete)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sink = new HashStream(hash);
        using var writer = new Utf8JsonWriter(sink);
        writer.WriteStartObject();
        writer.WriteString("Id", scope.Id);
        writer.WriteStartArray("Axioms");
        foreach (var axiom in scope.Axioms)
        {
            JsonSerializer.Serialize(writer, axiom);
            writer.Flush();
            yield return 0;
        }
        writer.WriteEndArray();
        writer.WriteStartArray("Rules");
        foreach (var rule in scope.Rules)
        {
            JsonSerializer.Serialize(writer, rule);
            writer.Flush();
            yield return 0;
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        complete(Convert.ToHexString(hash.GetHashAndReset()));
    }

    private sealed class HashStream(IncrementalHash hash) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count) => hash.AppendData(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => hash.AppendData(buffer);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
}
