// @mechanism: signed_horn_reasoning
// @layer: core/reasoning
// @contracts: CaseStrategy

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

// --- Source: CaseStrategy.cs ---
namespace DuelView.Core
{


/// <summary>Actor-owned admission and immediate utility. The host supplies only the actor's theory.
/// This does not infer an objective obligation or forecast the outcome of an unexecuted action.</summary>
public static class CaseStrategy
{
    private static readonly AsyncLocal<Func<object, object, string, IReadOnlyList<CaseStrategyChoice>>?> Current = new();

    public static IDisposable Use(Func<object, object, string, IReadOnlyList<CaseStrategyChoice>> evaluate)
    {
        var previous = Current.Value;
        Current.Value = evaluate;
        return new Scope(() => Current.Value = previous);
    }
    private sealed class Scope(Action restore) : IDisposable
    {
        private Action? restoreAction = restore;
        public void Dispose() => Interlocked.Exchange(ref restoreAction, null)?.Invoke();
    }
    public static IReadOnlyList<CaseStrategyChoice> ForAction(object actor, object mind, object action)
        => Current.Value?.Invoke(actor, mind, R.Text(DVKernel.F_dv_get(action, "id"))) ?? Array.Empty<CaseStrategyChoice>();
    public static bool Ready(object actor, object mind, object action) => ForAction(actor, mind, action).All(x => x.Ready);
    public static double Utility(object actor, object mind, object action)
    {
        var utility = ForAction(actor, mind, action).Where(x => x.Ready).Sum(x => x.Utility);
        return double.IsFinite(utility) ? utility : throw new InvalidDataException("Combined strategy utility exceeds finite range.");
    }

    public static IReadOnlyList<CaseStrategyChoice> Evaluate(CaseTheory theory, string actor, JsonElement profile, string? action = null)
        => StrategyJudgment.Evaluate(theory, actor, profile, action);
    public static StrategyJudgment.CaseStrategyWork BeginEvaluation(CaseTheory theory, string actor, JsonElement profile, string? action = null, int maxNodes = 10000, int maxMatches = 1000000)
        => StrategyJudgment.BeginEvaluation(theory, actor, profile, action, maxNodes, maxMatches);
    public static IReadOnlyList<CaseStrategyChoice> AwaitingEvidence(string actor, JsonElement profile, string version)
        => StrategyJudgment.AwaitingEvidence(actor, profile, version);
}
}

// --- Source: StrategyJudgment.cs ---
namespace DuelView.Core
{
public sealed record CaseStrategyChoice(string Action, bool Ready, double Utility,
    IReadOnlyList<string> Missing, IReadOnlyList<string> Blocked, IReadOnlyList<string> Basis,
    string TheoryHash, IReadOnlyList<string> EvidenceNodes);

public static class StrategyJudgment
{
    public static IReadOnlyList<CaseStrategyChoice> Evaluate(CaseTheory theory, string actor, JsonElement profile,
        string? action = null)
    {
        using var work = BeginEvaluation(theory, actor, profile, action);
        return work.RunToCompletion();
    }

    /// <summary>Creates computation only. A completed judgment neither completes an activity
    /// nor advances simulation time. Advance/AdvanceUntil are single-owner operations.</summary>
    public static CaseStrategyWork BeginEvaluation(CaseTheory theory, string actor, JsonElement profile,
        string? action = null, int maxNodes = 10000, int maxMatches = 1000000)
        => new(theory, actor, profile, action, maxNodes, maxMatches);

    public sealed class CaseStrategyWork : CaseWork<IReadOnlyList<CaseStrategyChoice>>
    {
        private readonly CaseInferenceWork inference;
        private readonly string actor;
        private readonly JsonElement profile;
        private readonly string? action;
        public long Matches => inference.Matches;
        public int NodeCount => inference.NodeCount;

        internal CaseStrategyWork(CaseTheory theory, string actor, JsonElement profile, string? action, int maxNodes, int maxMatches)
        {
            if (actor == "objective" || profile.GetProperty("actor").GetString() != actor)
                throw new InvalidDataException("Strategy must use its own actor scope.");
            this.actor = actor;
            this.profile = profile.Clone();
            this.action = action;
            inference = new CaseInferenceWork(theory.Scopes[actor], maxNodes, maxMatches);
        }

        protected override IEnumerable<int> Execute()
        {
            using (inference)
            {
                while (inference.Status == CaseWorkStatus.Running)
                {
                    inference.AdvanceUntil(1, long.MaxValue);
                    Phase = inference.Phase;
                    yield return 0;
                }
                // This only retrieves a terminal result or rethrows its original failure.
                var proof = inference.RunToCompletion();
                Phase = "index-judgment";
                var facts = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var node in proof.Nodes)
                {
                    facts.Add(node.Conclusion.Key, node.Id);
                    yield return 0;
                }
                Phase = "score-choices";
                IReadOnlyList<CaseStrategyChoice>? result = null;
                foreach (var step in JudgmentSteps(actor, profile, facts, proof.TheoryHash, action, value => result = value)) yield return step;
                Finish(result!);
            }
        }
    }

    /// <summary>Conservative ordinary judgment before any relevant observation requests proof work.</summary>
    public static IReadOnlyList<CaseStrategyChoice> AwaitingEvidence(string actor, JsonElement profile, string version)
        => FromJudgment(actor, profile, new Dictionary<string, string>(StringComparer.Ordinal), version);

    private static IReadOnlyList<CaseStrategyChoice> FromJudgment(string actor, JsonElement profile,
        IReadOnlyDictionary<string, string> facts, string theoryHash, string? action = null)
    {
        IReadOnlyList<CaseStrategyChoice>? result = null;
        foreach (var _ in JudgmentSteps(actor, profile, facts, theoryHash, action, value => result = value)) { }
        return result!;
    }

    private static IEnumerable<int> JudgmentSteps(string actor, JsonElement profile,
        IReadOnlyDictionary<string, string> facts, string theoryHash, string? action,
        Action<IReadOnlyList<CaseStrategyChoice>> complete)
    {
        if (actor == "objective" || profile.GetProperty("actor").GetString() != actor)
            throw new InvalidDataException("Strategy must use its own actor scope.");
        var basis = Strings(profile, "basis");
        if (basis.Length == 0) throw new InvalidDataException("Strategy requires declared provenance.");
        var drives = profile.GetProperty("drives").EnumerateObject().ToDictionary(x => x.Name, x => Finite(x.Value), StringComparer.Ordinal);
        var choices = profile.GetProperty("choices").EnumerateArray().ToArray();
        if (choices.Select(x => x.GetProperty("action").GetString()).Distinct(StringComparer.Ordinal).Count() != choices.Length)
            throw new InvalidDataException("Duplicate strategy action.");
        var selected = choices.Where(x => action is null || x.GetProperty("action").GetString() == action).ToArray();
        yield return 0;
        var results = new List<CaseStrategyChoice>();
        foreach (var choice in selected)
        {
            var declaration = Strings(choice, "basis");
            if (declaration.Length == 0) throw new InvalidDataException("Strategy choice requires declared provenance.");
            var requires = Literals(choice, "requires");
            var prohibits = Literals(choice, "prohibits");
            var missing = requires.Where(x => !facts.ContainsKey(x.Key)).Select(x => x.Key).ToArray();
            var blocked = requires.Where(x => facts.ContainsKey(x.Opposite.Key)).Select(x => x.Opposite.Key)
                .Concat(prohibits.Where(x => facts.ContainsKey(x.Key)).Select(x => x.Key)).Distinct().ToArray();
            double utility = 0;
            foreach (var promotion in choice.GetProperty("promotes").EnumerateObject())
            {
                if (!drives.TryGetValue(promotion.Name, out var weight)) throw new InvalidDataException("Undeclared strategy drive: " + promotion.Name);
                utility += weight * Finite(promotion.Value);
                if (!double.IsFinite(utility)) throw new InvalidDataException("Strategy utility exceeds finite range.");
                yield return 0;
            }
            var evidence = requires.Concat(prohibits).SelectMany(x => new[] { x.Key, x.Opposite.Key })
                .Where(facts.ContainsKey).Select(x => facts[x]).Distinct().ToArray();
            results.Add(new(choice.GetProperty("action").GetString() ?? throw new InvalidDataException("Missing strategy action."),
                missing.Length == 0 && blocked.Length == 0, utility, missing, blocked,
                basis.Concat(declaration).ToArray(), theoryHash, evidence));
            yield return 0;
        }
        complete(results.AsReadOnly());
    }
    private static double Finite(JsonElement value)
    {
        var number = value.GetDouble();
        return double.IsFinite(number) ? number : throw new InvalidDataException("Strategy weights must be finite.");
    }
    private static string[] Strings(JsonElement value, string field) => value.GetProperty(field).EnumerateArray()
        .Select(x => !string.IsNullOrWhiteSpace(x.GetString()) ? x.GetString()! : throw new InvalidDataException("Empty strategy provenance.")).ToArray();
    private static CaseLiteral[] Literals(JsonElement value, string field) => value.GetProperty(field).EnumerateArray().Select(x =>
    {
        var literal = x.Deserialize<CaseLiteral>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Invalid strategy literal.");
        if (string.IsNullOrWhiteSpace(literal.Predicate) || literal.Predicate.StartsWith('?') || literal.Args is null ||
            literal.Args.Any(a => string.IsNullOrWhiteSpace(a)) || !literal.Ground)
            throw new InvalidDataException("Strategy conditions must be ground literals.");
        return literal;
    }).ToArray();
}
}

// --- Source: CaseTravel.cs ---
namespace DuelView.Core
{

/// <summary>Travel time estimates from supplied navigation data; no world state is changed.</summary>
public static class CaseTravel
{
    public enum Mode { MetricPath, RoomGraphSeconds }
    public enum Access { Open, Unknown, Locked }
    public enum Status { Estimated, Unknown, Locked, Unreachable }

    // Directed edge. Bounds are metres in MetricPath mode and seconds in RoomGraphSeconds mode.
    public sealed record Edge(string From, string To, double Lower, double Upper, Access Access, string Source);
    public sealed record PathEstimate(double Seconds, IReadOnlyList<string> Nodes, IReadOnlyList<string> Sources);
    public sealed record Result(Mode Mode, Status Status, PathEstimate? Lower, PathEstimate? Upper, bool UnknownAlternative = false);
    public sealed record Point3(double X, double Y, double Z);

    /// <summary>Estimate from a host-supplied navigation polyline in metres, without claiming it was traversed.</summary>
    public static Result FromNavigationPath(IEnumerable<Point3> points, double minimumSpeed, double maximumSpeed, string source)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (!double.IsFinite(minimumSpeed) || !double.IsFinite(maximumSpeed) || minimumSpeed <= 0 || maximumSpeed < minimumSpeed)
            throw new ArgumentException("Finite positive ordered speed bounds are required.");
        var path = points.ToArray();
        if (path.Length == 0) throw new ArgumentException("Navigation path must contain a point.");
        if (path.Any(p => p is null || !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z)))
            throw new ArgumentException("Navigation points must have finite coordinates.");
        double length = 0;
        for (var i = 1; i < path.Length; i++)
        {
            var dx = path[i].X - path[i - 1].X; var dy = path[i].Y - path[i - 1].Y; var dz = path[i].Z - path[i - 1].Z;
            length += double.Hypot(double.Hypot(dx, dy), dz);
            if (!double.IsFinite(length)) throw new OverflowException("Navigation path exceeds finite range.");
        }
        var lower = length / maximumSpeed; var upper = length / minimumSpeed;
        if (!double.IsFinite(lower) || !double.IsFinite(upper)) throw new OverflowException("Travel estimate exceeds finite range.");
        var nodes = Enumerable.Range(0, path.Length).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var sources = Enumerable.Repeat(source, path.Length - 1).ToArray();
        return new(Mode.MetricPath, Status.Estimated, new(lower, nodes, sources), new(upper, nodes, sources));
    }

    /// <summary>
    /// Bounds estimate the fastest route in the open subgraph, not all possible routes, observations or guarantees.
    /// Metric speed bounds are metres per second and strictly positive. The caller supplies all known nodes.
    /// </summary>
    public static Result Estimate(Mode mode, string origin, string target, IEnumerable<string> knownNodes,
        IEnumerable<Edge> edges, double? minimumSpeed = null, double? maximumSpeed = null)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(knownNodes);
        ArgumentNullException.ThrowIfNull(edges);
        if (mode == Mode.MetricPath)
        {
            if (minimumSpeed is null || maximumSpeed is null || !double.IsFinite(minimumSpeed.Value) ||
                !double.IsFinite(maximumSpeed.Value) || minimumSpeed <= 0 || maximumSpeed < minimumSpeed)
                throw new ArgumentException("Metric mode requires finite positive ordered speed bounds.");
        }
        else if (minimumSpeed is not null || maximumSpeed is not null)
            throw new ArgumentException("Room graph mode does not accept speeds.");

        var nodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in knownNodes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(node);
            nodes.Add(node);
        }
        var graph = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            ArgumentNullException.ThrowIfNull(edge);
            ArgumentException.ThrowIfNullOrWhiteSpace(edge.From);
            ArgumentException.ThrowIfNullOrWhiteSpace(edge.To);
            ArgumentException.ThrowIfNullOrWhiteSpace(edge.Source);
            if (!nodes.Contains(edge.From) || !nodes.Contains(edge.To))
                throw new ArgumentException("Every edge endpoint must be a known node.");
            if (!Enum.IsDefined(edge.Access) || !double.IsFinite(edge.Lower) || !double.IsFinite(edge.Upper) ||
                edge.Lower < 0 || edge.Upper < edge.Lower)
                throw new ArgumentException("Edge access and finite nonnegative ordered bounds are required.");
            if (!graph.TryGetValue(edge.From, out var outgoing)) graph[edge.From] = outgoing = new List<Edge>();
            outgoing.Add(edge);
        }
        if (!nodes.Contains(origin) || !nodes.Contains(target)) return new(mode, Status.Unknown, null, null);
        var low = Shortest(origin, target, graph, e => e.Access == Access.Open, false);
        if (low is null)
        {
            if (Shortest(origin, target, graph, e => e.Access != Access.Locked, false) is not null)
                return new(mode, Status.Unknown, null, null);
            if (Shortest(origin, target, graph, _ => true, false) is not null)
                return new(mode, Status.Locked, null, null);
            return new(mode, Status.Unreachable, null, null);
        }
        var high = Shortest(origin, target, graph, e => e.Access == Access.Open, true)!;
        double lower = mode == Mode.MetricPath ? low.Seconds / maximumSpeed!.Value : low.Seconds;
        double upper = mode == Mode.MetricPath ? high.Seconds / minimumSpeed!.Value : high.Seconds;
        if (!double.IsFinite(lower) || !double.IsFinite(upper)) throw new OverflowException("Travel estimate exceeds finite range.");
        return new(mode, Status.Estimated, low with { Seconds = lower }, high with { Seconds = upper },
            HasUnknownAlternative(origin, target, graph));
    }

    private static bool HasUnknownAlternative(string origin, string target, Dictionary<string, List<Edge>> graph)
    {
        var visited = new HashSet<(string Node, bool Unknown)> { (origin, false) };
        var queue = new Queue<(string Node, bool Unknown)>(); queue.Enqueue((origin, false));
        while (queue.TryDequeue(out var item))
        {
            if (item.Node == target && item.Unknown) return true;
            if (!graph.TryGetValue(item.Node, out var outgoing)) continue;
            foreach (var edge in outgoing)
            {
                if (edge.Access == Access.Locked) continue;
                var next = (edge.To, item.Unknown || edge.Access == Access.Unknown);
                if (visited.Add(next)) queue.Enqueue(next);
            }
        }
        return false;
    }

    private static PathEstimate? Shortest(string origin, string target, Dictionary<string, List<Edge>> graph,
        Func<Edge, bool> allowed, bool upper)
    {
        var distances = new Dictionary<string, double>(StringComparer.Ordinal) { [origin] = 0 };
        var previous = new Dictionary<string, Edge>(StringComparer.Ordinal);
        var queue = new PriorityQueue<string, double>();
        queue.Enqueue(origin, 0);
        while (queue.TryDequeue(out var node, out var distance))
        {
            if (distance > distances[node]) continue;
            if (node == target)
            {
                var path = new List<string> { node };
                var sources = new List<string>();
                while (previous.TryGetValue(node, out var edge))
                {
                    sources.Add(edge.Source);
                    node = edge.From;
                    path.Add(node);
                }
                path.Reverse(); sources.Reverse();
                return new(distance, path, sources);
            }
            if (!graph.TryGetValue(node, out var outgoing)) continue;
            foreach (var edge in outgoing)
            {
                if (!allowed(edge)) continue;
                var candidate = distance + (upper ? edge.Upper : edge.Lower);
                if (!double.IsFinite(candidate)) throw new OverflowException("Travel path exceeds finite range.");
                if (distances.TryGetValue(edge.To, out var old) && candidate >= old) continue;
                distances[edge.To] = candidate;
                previous[edge.To] = edge;
                queue.Enqueue(edge.To, candidate);
            }
        }
        return null;
    }
}
}

// --- Source: CaseLeanBridge.cs ---
namespace DuelView.Core
{


/// <summary>
/// Translates a checked signed-Horn goal certificate into the independent natural
/// deduction language consumed by the Lean exporter. Explicit negative evidence
/// gets a separate predicate, never logical negation or False. This preserves
/// non-explosive actor theories even when both signs have proofs.
/// </summary>
public static class CaseLeanBridge
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static string Name(string category, string value) =>
        category + "_" + Convert.ToHexString(StrictUtf8.GetBytes(value)).ToLowerInvariant();

    private static string Atom(CaseLiteral literal) =>
        "(at " + Name(literal.Negative ? "negative" : "positive", literal.Predicate) +
        string.Concat(literal.Args.Select(term => " " + (term.StartsWith('?')
            ? "?" + Name("v", term) : Name("c", term)))) + ")";

    private static string[] Variables(CaseRule rule) => rule.Premises.Append(rule.Conclusion)
        .SelectMany(literal => literal.Args).Where(term => term.StartsWith('?'))
        .Distinct(StringComparer.Ordinal).OrderBy(term => term, StringComparer.Ordinal).ToArray();

    private static string RuleFormula(CaseRule rule)
    {
        var formula = Atom(rule.Conclusion);
        for (var i = rule.Premises.Count - 1; i >= 0; i--)
            formula = "(imp " + Atom(rule.Premises[i]) + " " + formula + ")";
        var variables = Variables(rule);
        for (var i = variables.Length - 1; i >= 0; i--)
            formula = "(all " + Name("v", variables[i]) + " " + formula + ")";
        if (formula.Length > 100000)
            throw new InvalidOperationException("Horn-to-ND rule exceeds the natural deduction source budget");
        return formula;
    }

    /// <summary>
    /// Returns an ND request only after both the Horn goal checker and independent
    /// ND checker accept it. No support, malformed input or budget exhaustion throws.
    /// For a refutation export the opposite signed goal explicitly. Calling this
    /// method does not run Lean; the authoring host must export and compile the result.
    /// </summary>
    public static JsonElement BuildRequest(CaseTheory theory, string scope, CaseLiteral goal)
    {
        ArgumentNullException.ThrowIfNull(theory);
        if (scope is null || !theory.Scopes.TryGetValue(scope, out var source))
            throw new ArgumentException("Unknown Horn scope", nameof(scope));
        var support = theory.Prove(scope, goal).Support
            ?? throw new InvalidOperationException("Requested signed goal has no supporting Horn certificate");
        var certificate = support.Certificate;
        var checkedHorn = theory.CheckGoal(scope, certificate, goal);
        if (!checkedHorn.Valid)
            throw new InvalidOperationException("Horn certificate rejected: " + string.Join("; ", checkedHorn.Errors));

        var facts = source.Axioms.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var rules = source.Rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var usedFacts = certificate.Nodes.Where(n => n.Kind == "axiom").Select(n => n.Reference).ToHashSet(StringComparer.Ordinal);
        var usedRules = certificate.Nodes.Where(n => n.Kind == "apply").Select(n => n.Reference).ToHashSet(StringComparer.Ordinal);
        var axioms = new List<object>();
        foreach (var id in usedFacts.OrderBy(x => x, StringComparer.Ordinal))
            axioms.Add(new { id = Name("fact", id), formula = Atom(facts[id].Literal) });
        foreach (var id in usedRules.OrderBy(x => x, StringComparer.Ordinal))
            axioms.Add(new { id = Name("rule", id), formula = RuleFormula(rules[id]) });

        // Local checked lemmas preserve sharing in the Horn DAG. Inlining the
        // parents recursively would make a small shared certificate exponential.
        var theorems = new List<object>();
        var references = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in certificate.Nodes)
        {
            if (node.Kind == "axiom")
            {
                references.Add(node.Id, Name("fact", node.Reference));
                continue;
            }
            if (node.Kind == "exact")
            {
                references.Add(node.Id, references[node.Parents[0]]);
                continue;
            }
            if (node.Kind != "apply")
                throw new InvalidOperationException("Unsupported checked Horn certificate node: " + node.Kind);
            var proof = "(use " + Name("rule", node.Reference) + ")";
            foreach (var variable in Variables(rules[node.Reference]))
                proof = "(all_elim " + proof + " " + Name("c", node.Substitution[variable]) + ")";
            foreach (var parent in node.Parents)
                proof = "(apply " + proof + " (use " + references[parent] + "))";
            if (proof.Length > 100000 || axioms.Count + theorems.Count >= 1024)
                throw new InvalidOperationException("Horn-to-ND certificate exceeds the natural deduction budget");
            var lemma = Name("node", node.Id);
            theorems.Add(new { id = lemma, formula = Atom(node.Conclusion), proof });
            references.Add(node.Id, lemma);
        }
        var request = JsonSerializer.SerializeToElement(new
        {
            scope,
            theory_hash = certificate.TheoryHash,
            semantics = "signed_predicates_are_independent",
            axioms,
            theorems,
            goal = Atom(goal),
            proof = "(use " + references[certificate.Nodes[^1].Id] + ")"
        });
        var checkedNd = NaturalDeduction.Check(request);
        if (!checkedNd.Valid)
            throw new InvalidOperationException("Horn-to-ND translation rejected: " + string.Join("; ", checkedNd.Errors));
        var expectedDependencies = usedFacts.Select(id => Name("fact", id))
            .Concat(usedRules.Select(id => Name("rule", id))).ToHashSet(StringComparer.Ordinal);
        if (!expectedDependencies.SetEquals(checkedNd.AxiomsUsed))
            throw new InvalidOperationException("Horn-to-ND translation changed the certificate's axiom dependencies");
        return request;
    }
}
}
