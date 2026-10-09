using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

public sealed record DetectiveAction(string Id, string Kind, string Target, string Room, string Label,
    double Minutes, double Weight, string RequiresGroup, string RequiresKey, string CompletionGroup,
    string CompletionKey, bool Repeatable);
public sealed record DetectiveMotive(string Id, string Person, string Label, double Probability,
    IReadOnlyList<string> Support, IReadOnlyList<string> CounterEvidence, string Source);
public sealed record DetectiveBranch(string Action, int Visits, double MeanValue);
/// <summary>One answer step. Variable names the replay variable whose predicted value is stated; when Statement is set,
/// that combined statement is said instead, but only if the prediction meets every "V<n>=<value>" in StatementRequires.</summary>
public sealed record DetectiveArgument(int Step, int Variable, string[] Evidence, string RequiredGroup, string RequiredKey,
    string Statement = "", IReadOnlyDictionary<int,int>? StatementRequires = null);
public sealed record DetectiveSearchDecision(DetectiveAction? Action, NightVariables? Prediction,
    int Iterations, int Nodes, int MaxDepth, int RemainingHypotheses, double Confidence,
    IReadOnlyList<DetectiveBranch> Branches, IReadOnlyList<DetectiveMotive> Motives);

/// <summary>A detached subjective planning model. Only Refresh receives real observations;
/// tree transitions sample possible feedback, and never call the morning world's mutators.</summary>
public sealed class DetectivePlanner
{
    private sealed record TraceField(string Evidence, string Field, string Tag, int EqualsValue);
    private sealed record VisibleObservation(string Text, string Source);
    private sealed record Hypothesis(NightVariables Variables, Dictionary<string, int> Facts);
    private sealed class State
    {
        public List<int> Candidates = [];
        public HashSet<string> Progress = new(StringComparer.Ordinal);
        public HashSet<string> Completed = new(StringComparer.Ordinal);
        public Dictionary<string, int> Seen = new(StringComparer.Ordinal);
        public Dictionary<string, double> Motives = new(StringComparer.Ordinal);
        public bool Relaxed;
        public State Copy() => new() { Candidates = [.. Candidates], Progress = new(Progress), Completed = new(Completed), Seen = new(Seen), Motives = new(Motives), Relaxed = Relaxed };
    }
    private sealed class Node(Node? parent, DetectiveAction? action)
    {
        public readonly Node? Parent = parent;
        public readonly DetectiveAction? Action = action;
        public readonly List<Node> Children = [];
        public int Visits;
        public double Value;
    }
    private readonly JsonElement _search;
    private readonly List<DetectiveAction> _actions = [];
    private readonly List<DetectiveArgument> _arguments = [];
    private readonly List<TraceField> _fields = [];
    private readonly List<Hypothesis> _hypotheses = [];
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private readonly List<(NightVariables Variables, Finding[] Findings)> _feedback = [];
    private readonly List<VisibleObservation> _visibleText = [];
    private readonly Random _random;
    private State _belief = new();
    private IReadOnlyList<DetectiveMotive> _motives = [];
    private bool _canAnswer;
    private bool _detectiveBolted;
    public IReadOnlyList<DetectiveAction> Actions => _actions;
    public int ActionBudget => (int)N("action_budget");
    private double N(string key) => _search.GetProperty(key).GetDouble();
    private static string S(JsonElement item, string key) => item.GetProperty(key).GetString()!;
    private static string ProgressKey(string group, string key) => group + ":" + key;
    // STORY-HARDCODE[AWK-038][schema][production]: 装配本章 V1–V6 假设、证物、陈述步与搜索模型。
    public DetectivePlanner(JsonElement content)
    {
        _search = content.GetProperty("DETECTIVE_SEARCH").Clone();
        _random = new((int)N("seed"));
        foreach (var item in content.GetProperty("DETECTIVE_ACTIONS").EnumerateObject())
        {
            var a = item.Value;
            _actions.Add(new(item.Name, S(a,"kind"), S(a,"target"), S(a,"room"), S(a,"label"),
                a.GetProperty("minutes").GetDouble(), a.GetProperty("weight").GetDouble(),
                S(a,"requires_group"), S(a,"requires_key"), S(a,"completion_group"), S(a,"completion_key"), a.GetProperty("repeatable").GetBoolean()));
        }
        foreach (var item in content.GetProperty("DETECTIVE_TRACE_FIELDS").EnumerateObject())
            _fields.Add(new(S(item.Value,"evidence"), S(item.Value,"field"), S(item.Value,"tag"), item.Value.GetProperty("equals").GetInt32()));
        foreach (var item in content.GetProperty("DETECTIVE_ARGUMENTS").EnumerateObject())
            _arguments.Add(new(item.Value.GetProperty("step").GetInt32(),item.Value.GetProperty("variable").GetInt32(),
                S(item.Value,"evidence").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries),
                S(item.Value,"required_group"),S(item.Value,"required_key"),
                item.Value.TryGetProperty("statement",out var st) ? st.GetString()! : "",
                item.Value.TryGetProperty("statement_requires",out var req)
                    ? req.GetString()!.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
                        .Select(t => t.TrimStart('V').Split('=')).ToDictionary(t => int.Parse(t[0]), t => int.Parse(t[1]))
                    : null));
        // Predicted traces only, from the shared case model. ReplayResult.FullMatch and Conflicts are never read.
        foreach (var prediction in AwakeningCase.Predictions)
            _hypotheses.Add(new(prediction.Variables, prediction.Facts.ToDictionary(p => p.Key, p => p.Value)));
    }
    public void RecordVisibleText(string text, string source)
    {
        var observation = new VisibleObservation(text,source);
        if (!_visibleText.Contains(observation)) _visibleText.Add(observation);
    }
    public void Completed(DetectiveAction action) => _completed.Add(action.Id);
    public void RecordReplay(NightVariables variables, IEnumerable<Finding> findings)
        => _feedback.Add((variables, findings.Where(f => f.Status != FindingStatus.Unvisited).ToArray()));

    public void Refresh(ProjectionWorld mind, AwakeningContentSession content)
    {
        var state = new State { Completed = new(_completed) };
        foreach (var action in _actions)
            foreach (var pair in new[] { (action.RequiresGroup, action.RequiresKey), (action.CompletionGroup, action.CompletionKey) })
                if (pair.Item1.Length > 0 && content.Session.GetProgress(pair.Item1).TryGetValue(pair.Item2,out var progressValue) && progressValue is not false)
                    state.Progress.Add(ProgressKey(pair.Item1, pair.Item2));
        foreach (var field in _fields)
        {
            if (!content.Session.Has("examined", field.Evidence)) continue;
            // The public name identifies an already inspected card; no hidden card fields are fetched.
            var card = content.Content.RootElement.GetProperty("EVIDENCE").GetProperty(field.Evidence);
            var name = card.TryGetProperty("mind_name", out var alias) ? alias.GetString() : card.GetProperty("name").GetString();
            var projection = mind.Projections.FirstOrDefault(p => p.Name == name);
            if (projection?.Tags.TryGetValue(new Tag(field.Tag), out var value) is true) state.Seen[field.Field] = value;
        }
        foreach (var observation in mind.History)
            if (observation.Details.Length > 0)
                RecordVisibleText(observation.Details,$"ProjectionWorld:{observation.Source}; kind={observation.Target.Kind}; target={observation.Target.Name}; time={observation.Time}");
        for (var index = 0; index < _hypotheses.Count; index++)
        {
            var hypothesis = _hypotheses[index];
            if (!content.V6Open && hypothesis.Variables.V6 != Acquaintance.Strangers) continue;
            if (!state.Seen.All(f => hypothesis.Facts[f.Key] == f.Value)) continue;
            bool compatible = true;
            foreach (var feedback in _feedback)
            {
                var tested = _hypotheses.First(h => h.Variables == feedback.Variables);
                foreach (var finding in feedback.Findings)
                {
                    var fields = _fields.Where(f => f.Evidence == finding.Evidence).ToArray();
                    if (fields.Length == 0) continue;
                    var match = fields.All(f => hypothesis.Facts[f.Field] == tested.Facts[f.Field]);
                    // A conflict can concern an unmodelled trace. Only a match constrains the fields,
                    // while a conflict does not reject a candidate without a complete trace model.
                    if (finding.Status == FindingStatus.Match && !match) compatible = false;
                }
            }
            if (compatible) state.Candidates.Add(index);
        }
        if (state.Candidates.Count == 0)
        {
            // An incomplete model may contradict an observation before an additional public
            // variable is unlocked. Retain the least contradicted available hypotheses as
            // provisional experiments. Their confidence is zero; they cannot support an answer.
            var allowed = Enumerable.Range(0,_hypotheses.Count).Where(i => content.V6Open || _hypotheses[i].Variables.V6 == Acquaintance.Strangers).ToArray();
            int Contradictions(int i) => state.Seen.Count(f => _hypotheses[i].Facts[f.Key] != f.Value)
                + _feedback.Sum(feedback => feedback.Findings.Where(f => f.Status == FindingStatus.Match)
                    .Sum(f => _fields.Where(field => field.Evidence == f.Evidence).Count(field =>
                        _hypotheses[i].Facts[field.Field] != _hypotheses.First(h => h.Variables == feedback.Variables).Facts[field.Field])));
            var minimum = allowed.Min(Contradictions);
            state.Candidates = allowed.Where(i => Contradictions(i) == minimum).ToList();
            state.Relaxed = true;
        }
        _belief = state;
        _motives = content.Content.RootElement.GetProperty("DETECTIVE_MOTIVES").EnumerateObject().Select(item =>
        {
            var a = item.Value;
            var cue = S(a,"cue"); var counter = S(a,"counter_cue");
            var support = _visibleText.Where(t => cue.Length > 0 && t.Text.Contains(cue, StringComparison.Ordinal)).ToArray();
            var against = _visibleText.Where(t => counter.Length > 0 && t.Text.Contains(counter, StringComparison.Ordinal)).ToArray();
            var prior = a.GetProperty("prior").GetDouble();
            // Retain every provenance record, but repeated rendering or the same words entering
            // the projection history are not additional independent confirmations.
            var supportCount = support.Select(t => t.Text).Distinct(StringComparer.Ordinal).Count();
            var counterCount = against.Select(t => t.Text).Distinct(StringComparer.Ordinal).Count();
            var odds = prior / (1-prior) * Math.Pow(N("unknown_reliability"), supportCount-counterCount);
            return new DetectiveMotive(item.Name,S(a,"person"),S(a,"label"), odds/(1+odds),
                support.Select(t => $"[{t.Source}] {t.Text}").ToArray(),
                against.Select(t => $"[{t.Source}] {t.Text}").ToArray(),
                support.Length + against.Length == 0 ? "prior" : string.Join("; ",support.Concat(against).Select(t => t.Source).Distinct()) + "; motive remains a hypothesis");
        }).ToArray();
        _belief.Motives = _motives.ToDictionary(m => m.Id,m => m.Probability);
        var replay = content.Session.GetProgress("replay_result");
        _detectiveBolted = replay.GetValueOrDefault("detective_bolted") is true;
        _canAnswer = !state.Relaxed && state.Candidates.Count > 0 && 1.0/state.Candidates.Count >= N("confidence")
            && _feedback.Count > 0 && _feedback[^1].Findings.Length > 0
            && _feedback[^1].Findings.All(f => f.Status == FindingStatus.Match)
            // STORY-HARDCODE[AWK-039][schema][production]: 答辩固定为本章四步，并允许 me 作为自闩陈述。
            && _arguments.Count == 4 && _arguments.Select(a => a.Step).Order().SequenceEqual(Enumerable.Range(1,4))
            && _arguments.All(a => a.Evidence.Any(e => content.Session.GetProgress("examined").GetValueOrDefault(e) is true)
                && (a.RequiredGroup.Length == 0 || content.Session.GetProgress(a.RequiredGroup).GetValueOrDefault(a.RequiredKey) is true)
                && (a.Variable != -1 || _detectiveBolted));
    }
    public (string Said,string Shown)? Argument(int step, AwakeningContentSession content, NightVariables prediction)
    {
        if (!_canAnswer) return null;
        var argument = _arguments.FirstOrDefault(a => a.Step == step);
        if (argument is null) return null;
        var evidence = argument.Evidence.Where(e => content.Session.GetProgress("examined").GetValueOrDefault(e) is true).ToArray();
        if (evidence.Length == 0) return null;
        var values = new[] { (int)prediction.V1,(int)prediction.V2,(int)prediction.V3,(int)prediction.V4,(int)prediction.V5,(int)prediction.V6 };
        var said = argument.Statement.Length > 0
            ? (argument.StatementRequires ?? new Dictionary<int,int>()).All(r => values[r.Key-1] == r.Value) ? argument.Statement : ""
            : argument.Variable == -1 && _detectiveBolted ? "me" : argument.Variable is >=1 and <=6 ? $"V{argument.Variable}_{values[argument.Variable-1]}" : "";
        return said.Length == 0 ? null : (said,evidence[_random.Next(evidence.Length)]);
    }
    private List<DetectiveAction> Available(State state) => _actions.Where(a => (a.Kind != "answer" || _canAnswer)
        && (a.Repeatable || !state.Completed.Contains(a.Id))
        && (a.RequiresGroup.Length == 0 || state.Progress.Contains(ProgressKey(a.RequiresGroup,a.RequiresKey)))
        && (a.CompletionGroup.Length == 0 || !state.Progress.Contains(ProgressKey(a.CompletionGroup,a.CompletionKey)))) .ToList();
    private static double BinaryEntropy(double p) => p <= 0 || p >= 1 ? 0 : -p*Math.Log(p)-(1-p)*Math.Log(1-p);
    private double Transition(State state, DetectiveAction action, int sampledWorld, IReadOnlyDictionary<string,bool> sampledMotives)
    {
        var entropy = Math.Log(Math.Max(1,state.Candidates.Count));
        var repeated = !state.Completed.Add(action.Id);
        if (action.CompletionGroup.Length > 0) state.Progress.Add(ProgressKey(action.CompletionGroup,action.CompletionKey));
        // Counterfactual feedback is sampled from a subjective candidate, not the actual world.
        // Looking at a trace reveals that sample's state; replay partitions candidates by agreement.
        var sampled = _hypotheses[sampledWorld];
        if (action.Kind == "inspect")
            foreach (var field in _fields.Where(f => f.Evidence == action.Target)) state.Seen[field.Field] = sampled.Facts[field.Field];
        if (action.Kind == "replay")
        {
            var proposal = _hypotheses[state.Candidates[_random.Next(state.Candidates.Count)]];
            foreach (var evidence in _fields.GroupBy(f => f.Evidence).Where(g => state.Progress.Contains("examined:"+g.Key)))
            {
                var matches = evidence.All(f => sampled.Facts[f.Field] == proposal.Facts[f.Field]);
                // A sampled world that agrees with no remaining candidate tells the rollout nothing; keep the set rather than empty it.
                if (state.Candidates.Any(i => evidence.All(f => _hypotheses[i].Facts[f.Field] == proposal.Facts[f.Field]) == matches))
                    state.Candidates.RemoveAll(i => evidence.All(f => _hypotheses[i].Facts[f.Field] == proposal.Facts[f.Field]) != matches);
            }
        }
        if (state.Relaxed)
        {
            // Rollouts keep the same explicit provisional interpretation rather than turning
            // contradictory observations into certainty or emptying the sampled search state.
            var minimum = state.Candidates.Min(i => state.Seen.Count(f => _hypotheses[i].Facts[f.Key] != f.Value));
            state.Candidates.RemoveAll(i => state.Seen.Count(f => _hypotheses[i].Facts[f.Key] != f.Value) != minimum);
        }
        else state.Candidates.RemoveAll(i => !state.Seen.All(f => _hypotheses[i].Facts[f.Key] == f.Value));
        var information = entropy - Math.Log(Math.Max(1,state.Candidates.Count));
        // A rollout samples a possible motive and an imperfect future response. The posterior
        // lives only in the detached state, and never enters Refresh's real belief or testimony.
        double motiveInformation = 0;
        if (action.Kind == "talk")
            foreach (var motive in _motives.Where(m => m.Person == action.Target))
            {
                var prior = state.Motives[motive.Id];
                var reliability = N("unknown_reliability") / (1+N("unknown_reliability"));
                var sampledCue = _random.NextDouble() < reliability ? sampledMotives[motive.Id] : !sampledMotives[motive.Id];
                var odds = prior/(1-prior) * (sampledCue ? N("unknown_reliability") : 1/N("unknown_reliability"));
                var posterior = odds/(1+odds);
                state.Motives[motive.Id] = posterior;
                motiveInformation += BinaryEntropy(prior)-BinaryEntropy(posterior);
            }
        return N("information_weight") * (information+motiveInformation) + N("progress_weight") * action.Weight
            - N("time_cost") * action.Minutes - (repeated ? N("repeat_penalty") : 0);
    }
    public DetectiveSearchDecision Plan()
    {
        var root = new Node(null,null); int nodes = 1, maxDepth = 0;
        if (_belief.Candidates.Count == 0) return new(null,null,0,1,0,0,0,[],_motives);
        int iterations = (int)N("iterations"), limit = (int)N("depth");
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var state = _belief.Copy(); var node = root; var depth = 0; double reward = 0, discount = 1;
            var sample = state.Candidates[_random.Next(state.Candidates.Count)];
            var sampledMotives = _motives.ToDictionary(m => m.Id,m => _random.NextDouble() < m.Probability);
            var path = new List<Node> { root };
            while (depth < limit)
            {
                var available = Available(state); if (available.Count == 0) break;
                var unexpanded = available.Where(a => node.Children.All(c => c.Action!.Id != a.Id)).ToArray();
                Node child;
                bool expanding = unexpanded.Length > 0;
                if (expanding) { child = new(node,unexpanded[_random.Next(unexpanded.Length)]); node.Children.Add(child); nodes++; }
                else child = node.Children.Where(c => available.Any(a => a.Id == c.Action!.Id))
                    .MaxBy(c => c.Value / Math.Max(1,c.Visits) + N("exploration") * Math.Sqrt(Math.Log(node.Visits+1)/Math.Max(1,c.Visits)))!;
                reward += discount * Transition(state,child.Action!,sample,sampledMotives); discount *= N("discount"); depth++;
                node = child; path.Add(node);
                if (expanding) break;
            }
            while (depth < limit)
            {
                var available = Available(state); if (available.Count == 0) break;
                reward += discount * Transition(state,available[_random.Next(available.Count)],sample,sampledMotives);
                discount *= N("discount"); depth++;
            }
            maxDepth = Math.Max(maxDepth,depth);
            foreach (var ancestor in path) { ancestor.Visits++; ancestor.Value += reward; }
        }
        var best = root.Children.MaxBy(c => c.Visits);
        var prediction = _hypotheses[_belief.Candidates[_random.Next(_belief.Candidates.Count)]].Variables;
        return new(best?.Action,prediction,iterations,nodes,maxDepth,_belief.Candidates.Count,_belief.Relaxed ? 0 : 1.0/_belief.Candidates.Count,
            root.Children.Select(c => new DetectiveBranch(c.Action!.Id,c.Visits,c.Value/Math.Max(1,c.Visits))).ToArray(),_motives);
    }
}
