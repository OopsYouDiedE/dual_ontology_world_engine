// @mechanism: autonomy_drive_solver
// @layer: core/autonomy
// @contracts: GoalPlanning

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using DuelView.Spatial;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVAutonomyKernel;
using static DuelView.Core.DVKernel;
using static DuelView.Core.Sim4Planner;

// --- Source: SingleStepPolicy.cs ---
namespace DuelView.Runtime
{


/// <summary>Indexes authored goals by their fields. Scoring previews one owned action
/// law and visits only goals affected by its effects or explicit observation grants.</summary>
internal sealed class SingleStepPolicy
{
    private sealed record Goal(Condition? Proposition, Condition Available, FieldKey? Observe, double Weight);
    private readonly ActorClient actor;
    private readonly Dictionary<string, Rule> predictions = new(StringComparer.Ordinal);
    private readonly Dictionary<FieldKey, HashSet<Goal>> affected = [];

    internal SingleStepPolicy(ActorClient actor)
    {
        this.actor = actor;
        var declaration = actor.Declaration;
        foreach (var adoption in declaration.Get("mind").Get("adoptions").Fields())
            if (adoption.Value.Get("active").Flag(true) && adoption.Value.Get("mode").Text() == "predict")
                predictions.Add(adoption.Name, Rule.Compile(adoption.Name, adoption.Value, actor.Id));
        foreach (var item in declaration.Get("policy").Get("goals").Items())
        {
            var proposition = item.Get("proposition").ValueKind == JsonValueKind.Object ? Condition.Compile(item.Get("proposition"), actor.Id) : null;
            var available = item.Get("available_when").ValueKind == JsonValueKind.Object ? Condition.Compile(item.Get("available_when"), actor.Id) : new Condition.Constant(Truth.True);
            var observe = item.Get("ref").ValueKind == JsonValueKind.Object ? FieldKey.Read(item.Get("ref"), actor.Id) : (FieldKey?)null;
            var weight = item.Get("weight").Number(1);
            if (!double.IsFinite(weight) || weight < 0) throw new InvalidDataException("Invalid authored goal weight.");
            var goal = new Goal(proposition, available, observe, weight);
            foreach (var key in (proposition?.Inputs ?? []).Concat(available.Inputs).Concat(observe is { } target ? [target] : []).Distinct())
            {
                if (!affected.TryGetValue(key, out var set)) affected.Add(key, set = []);
                set.Add(goal);
            }
        }
    }

    internal double Score(ActionCandidate action, double contentTime)
    {
        var writes = new Dictionary<FieldKey, Fact>();
        var relevant = new HashSet<Goal>();
        var lawId = action.Declaration.Get("law").Text();
        if (predictions.TryGetValue(lawId, out var law) && law.Premise.Evaluate(actor.Recall) == Truth.True)
        {
            try { writes = law.Preview(actor.Recall, contentTime); }
            catch (InvalidOperationException) { return double.NegativeInfinity; }
            foreach (var key in writes.Keys) if (affected.TryGetValue(key, out var set)) relevant.UnionWith(set);
        }
        var observed = action.Declaration.Get("grants").Items().Select(x => FieldKey.Read(x, actor.Id)).ToHashSet();
        foreach (var key in observed) if (affected.TryGetValue(key, out var set)) relevant.UnionWith(set);
        Fact? After(FieldKey key) => writes.GetValueOrDefault(key) ?? actor.Recall(key);
        var score = 0.0;
        foreach (var goal in relevant)
        {
            if (goal.Proposition is { } proposition && proposition.Evaluate(actor.Recall) != Truth.True)
            {
                if (goal.Available.Evaluate(actor.Recall) == Truth.True && proposition.Evaluate(After) == Truth.True) score += goal.Weight;
                else if (goal.Available.Evaluate(actor.Recall) != Truth.True && goal.Available.Evaluate(After) == Truth.True) score += goal.Weight;
            }
            if (goal.Observe is { } key && observed.Contains(key) && actor.Recall(key) is not { Known: true, Conflicted: false } && goal.Available.Evaluate(actor.Recall) == Truth.True)
                score += goal.Weight;
        }
        return score;
    }
}
}

// --- Source: ActorClient.cs ---
namespace DuelView.Runtime
{


public enum InformationOrigin { Passive, Communication, DeliberateObservation, ActionFailure, ActionFeedback }
public enum RefreshCause { Deliberate, FailedAction, ObservationEnded, ActionCompleted }
public sealed record Observation(string Id, FieldKey Key, JsonElement Value, double Confidence, double Time,
    InformationOrigin Origin, JsonElement Source, string? MapElement = null);
public sealed record Evidence(string Id, FieldKey Key, JsonElement Value, double Confidence, double Time,
    string Method, JsonElement Source);
public sealed record ClientView(string Actor, long KnowledgeRevision, long MapRevision,
    IReadOnlyDictionary<FieldKey, Fact> Knowledge, IReadOnlyDictionary<string, JsonElement> RememberedMap);
public sealed record InferenceConclusionCheckpoint(string Rule,FieldKey Key);
public sealed record RefreshWorkState(RefreshCause Cause,Observation[] Remaining,bool InputFinished,SavedFact[] Writes,
    Dictionary<string,JsonElement> Map,Evidence[] Evidence,string[] Queue,InferenceConclusionCheckpoint[] Conclusions);
public sealed record ActorClientState(string Actor,FactStoreState Knowledge,Evidence[] Evidence,Observation[] Inbox,
    Dictionary<string,JsonElement> RememberedMap,FieldKey[] ExplicitFocus,FieldKey[] ActionFocus,string? ObservationToken,
    long NextObservation,long MapRevision,long ReceivedCount,long IgnoredCount,long RefreshCount,long InferenceEvaluations,RefreshWorkState? Refresh);

/// <summary>
/// An actor owns a persistent subjective replica. Reception only runs an inexpensive
/// attention filter; it never mutates knowledge or launches reasoning. Player and AI
/// both read this same replica and submit commands through the server.
/// </summary>
public sealed class ActorClient
{
    private readonly FactStore knowledge;
    private readonly Dictionary<string, Rule> inference = new(StringComparer.Ordinal);
    private readonly Dictionary<FieldKey, HashSet<string>> inferenceReaders = [];
    private Dictionary<FieldKey, List<Observation>> inbox = [];
    private HashSet<(string, FieldKey)> pendingIds = [];
    private readonly List<Evidence> evidence = [];
    private readonly Dictionary<string, JsonElement> rememberedMap = new(StringComparer.Ordinal);
    private readonly HashSet<FieldKey> explicitFocus = [];
    private readonly HashSet<FieldKey> actionFocus = [];
    private readonly HashSet<FieldKey> goalFocus = [];
    private readonly JsonElement attention;
    private readonly JsonElement goals;
    private RefreshWork? refresh;
    private ClientView? cachedView;
    private string? observationToken;
    private long nextObservation;
    private readonly SingleStepPolicy singleStep;
    public string Id { get; }
    public long KnowledgeRevision => knowledge.Revision;
    public long MapRevision { get; private set; }
    public long ReceivedCount { get; private set; }
    public long IgnoredCount { get; private set; }
    public long RefreshCount { get; private set; }
    public long InferenceEvaluations { get; private set; }
    public bool IsObserving => observationToken is not null;
    public bool IsUpdating => refresh is not null;
    public int PendingInformation => pendingIds.Count;
    public IReadOnlyList<Evidence> Evidence => evidence.AsReadOnly();
    public JsonElement Declaration { get; }

    public ActorClient(JsonElement declaration, JsonElement attentionPolicy)
    {
        Id = declaration.Get("id").Text();
        if (Id.Length == 0) throw new InvalidDataException("Actor identity is required.");
        Declaration = declaration;
        attention = attentionPolicy;
        foreach (var rule in attention.Get("rules").Items())
        {
            var kind = rule.Get("kind").Text();
            if (kind is not ("deliberate_focus" or "failed_action_focus" or "current_action" or "current_goal" or "known_conflict" or "field"))
                throw new InvalidDataException("Unsupported attention rule: " + kind);
            if (kind == "field") _ = FieldKey.Read(rule.Get("ref"), Id);
            if (rule.Get("when").ValueKind == JsonValueKind.Object) _ = Condition.Compile(rule.Get("when"), Id);
        }
        goals = declaration.Get("policy").Get("goals");
        knowledge = new(Id);
        var mind = declaration.Get("mind");
        if (mind.Get("subject").Text() != Id) throw new InvalidDataException("Actor owns another subject's initial knowledge.");
        knowledge.Initialize(mind.Get("objects"));
        foreach (var adoption in mind.Get("adoptions").Fields())
        {
            if (!adoption.Value.Get("active").Flag(true) || adoption.Value.Get("mode").Text() != "infer") continue;
            var rule = Rule.Compile(adoption.Name, adoption.Value, Id);
            inference.Add(rule.Id, rule);
            foreach (var key in rule.Premise.Inputs)
            {
                if (!inferenceReaders.TryGetValue(key, out var readers)) inferenceReaders.Add(key, readers = []);
                readers.Add(rule.Id);
            }
        }
        RebuildGoalFocus();
        singleStep = new(this);
    }

    public Fact? Recall(FieldKey key) => knowledge.Read(key);
    public ActorClientState SaveState()
    {
        RefreshWorkState? pending=null;
        if(refresh is { } work)
        {
            var remaining=new List<Observation>();
            if(!work.InputFinished)while(work.Incoming.MoveNext())remaining.Add(work.Incoming.Current);
            work.Incoming.Dispose();work.Incoming=remaining.GetEnumerator();
            pending=new(work.Cause,remaining.ToArray(),work.InputFinished,work.Writes.Select(item=>new SavedFact(item.Key,item.Value)).ToArray(),
                new(work.Map,StringComparer.Ordinal),work.Evidence.ToArray(),work.Queue.ToArray(),work.Conclusions.Select(item=>new InferenceConclusionCheckpoint(item.Item1,item.Item2)).ToArray());
        }
        return new(Id,knowledge.SaveState(),evidence.ToArray(),inbox.Values.SelectMany(items=>items).ToArray(),
            new(rememberedMap,StringComparer.Ordinal),explicitFocus.ToArray(),actionFocus.ToArray(),observationToken,nextObservation,
            MapRevision,ReceivedCount,IgnoredCount,RefreshCount,InferenceEvaluations,pending);
    }
    public void RestoreState(ActorClientState state)
    {
        if(state.Actor!=Id || state.NextObservation<0 || state.MapRevision<0 || state.ReceivedCount<0 || state.IgnoredCount<0 || state.RefreshCount<0 || state.InferenceEvaluations<0 ||
           state.Evidence is null || state.Inbox is null || state.RememberedMap is null || state.ExplicitFocus is null || state.ActionFocus is null)
            throw new InvalidDataException("Invalid actor checkpoint.");
        var seen=new HashSet<(string,FieldKey)>();
        static bool ValidKey(FieldKey key)=>!string.IsNullOrWhiteSpace(key.Object)&&!string.IsNullOrWhiteSpace(key.Facet)&&!string.IsNullOrWhiteSpace(key.Field);
        static bool ValidEvidence(Evidence item)=>item is not null && !string.IsNullOrWhiteSpace(item.Id) && ValidKey(item.Key) && item.Value.ValueKind!=JsonValueKind.Undefined && item.Source.ValueKind!=JsonValueKind.Undefined &&
            !string.IsNullOrWhiteSpace(item.Method) && double.IsFinite(item.Confidence) && item.Confidence is >=0 and <=1 && double.IsFinite(item.Time) && item.Time>=0;
        foreach(var item in state.Evidence)
            if(!ValidEvidence(item))
                throw new InvalidDataException("Invalid evidence in actor checkpoint.");
        foreach(var item in state.Inbox)
            if(item is null || string.IsNullOrWhiteSpace(item.Id) || !ValidKey(item.Key) || item.Value.ValueKind==JsonValueKind.Undefined || item.Source.ValueKind==JsonValueKind.Undefined ||
               !seen.Add((item.Id,item.Key)) || !double.IsFinite(item.Confidence) || item.Confidence is <0 or >1 || !double.IsFinite(item.Time) || item.Time<0)
                throw new InvalidDataException("Invalid buffered observation in checkpoint.");
        if(state.RememberedMap.Any(item=>string.IsNullOrWhiteSpace(item.Key)||item.Value.ValueKind==JsonValueKind.Undefined) ||
           state.ExplicitFocus.Any(key=>!ValidKey(key)) || state.ActionFocus.Any(key=>!ValidKey(key)))throw new InvalidDataException("Invalid remembered map or focus in actor checkpoint.");
        if(state.Refresh is { } staged && (staged.Remaining is null || staged.Writes is null || staged.Map is null || staged.Evidence is null || staged.Queue is null || staged.Conclusions is null ||
           staged.Remaining.Any(item=>item is null || !ValidKey(item.Key) || item.Value.ValueKind==JsonValueKind.Undefined || item.Source.ValueKind==JsonValueKind.Undefined) ||
           staged.Writes.Any(item=>!ValidKey(item.Key) || item.Fact is null || item.Fact.Value.ValueKind==JsonValueKind.Undefined) ||
           staged.Queue.Distinct(StringComparer.Ordinal).Count()!=staged.Queue.Length || staged.Queue.Any(id=>!inference.ContainsKey(id)) ||
           staged.Conclusions.Any(item=>!inference.ContainsKey(item.Rule)||!ValidKey(item.Key)) || staged.Evidence.Any(item=>!ValidEvidence(item)) || staged.Map.Any(item=>item.Value.ValueKind==JsonValueKind.Undefined)))
            throw new InvalidDataException("Invalid cognitive work checkpoint.");
        knowledge.RestoreState(state.Knowledge);
        evidence.Clear();evidence.AddRange(state.Evidence);
        inbox.Clear();pendingIds.Clear();
        foreach(var item in state.Inbox)
        {
            if(!inbox.TryGetValue(item.Key,out var bucket))inbox.Add(item.Key,bucket=[]);
            bucket.Add(item);pendingIds.Add((item.Id,item.Key));
        }
        rememberedMap.Clear();foreach(var item in state.RememberedMap)rememberedMap.Add(item.Key,item.Value.Clone());
        explicitFocus.Clear();explicitFocus.UnionWith(state.ExplicitFocus);actionFocus.Clear();actionFocus.UnionWith(state.ActionFocus);
        observationToken=state.ObservationToken;nextObservation=state.NextObservation;MapRevision=state.MapRevision;
        ReceivedCount=state.ReceivedCount;IgnoredCount=state.IgnoredCount;RefreshCount=state.RefreshCount;InferenceEvaluations=state.InferenceEvaluations;
        refresh=null;
        if(state.Refresh is { } saved)
        {
            var work=new RefreshWork(saved.Cause,((IEnumerable<Observation>)saved.Remaining).GetEnumerator()) {InputFinished=saved.InputFinished};
            foreach(var item in saved.Writes)work.Writes.Add(item.Key,item.Fact);
            foreach(var item in saved.Map)work.Map.Add(item.Key,item.Value.Clone());
            work.Evidence.AddRange(saved.Evidence);foreach(var id in saved.Queue){work.Queue.Enqueue(id);work.Queued.Add(id);}
            foreach(var item in saved.Conclusions)work.Conclusions.Add((item.Rule,item.Key));refresh=work;
        }
        cachedView=null;RebuildGoalFocus();
    }
    public double GoalScore(ActionCandidate action, double contentTime) => singleStep.Score(action, contentTime);

    public ClientView View()
    {
        // No world version or clock is part of this key. Unrelated events, incoming
        // messages, render frames and movement do not regenerate a mental map.
        return cachedView ??= new(Id, KnowledgeRevision, MapRevision,
            new System.Collections.ObjectModel.ReadOnlyDictionary<FieldKey, Fact>(knowledge.Entries.ToDictionary()),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>(rememberedMap)));
    }

    public void FocusAction(IEnumerable<FieldKey> keys)
    {
        actionFocus.Clear();
        actionFocus.UnionWith(keys);
    }

    private void RebuildGoalFocus()
    {
        goalFocus.Clear();
        foreach (var goal in goals.Items())
        {
            var gate = goal.Get("available_when");
            if (gate.ValueKind == JsonValueKind.Object && Condition.Compile(gate, Id).Evaluate(knowledge.Read) == Truth.False) continue;
            if (goal.Get("ref").ValueKind == JsonValueKind.Object) goalFocus.Add(FieldKey.Read(goal.Get("ref"), Id));
            if (gate.ValueKind == JsonValueKind.Object) goalFocus.UnionWith(Condition.Compile(gate, Id).Inputs);
        }
    }

    public bool Receive(Observation information)
    {
        ReceivedCount++;
        if (!double.IsFinite(information.Confidence) || information.Confidence is < 0 or > 1 || !double.IsFinite(information.Time))
            throw new InvalidDataException("Invalid observation measurement.");
        if (!IsImportant(information)) { IgnoredCount++; return false; }
        return Enqueue(information);
    }
    public bool ReceiveDirected(Observation information)
    {
        ReceivedCount++;
        if(information.Origin!=InformationOrigin.Communication || !double.IsFinite(information.Confidence) || information.Confidence is < 0 or > 1 || !double.IsFinite(information.Time))
            throw new InvalidDataException("Invalid directed communication.");
        return Enqueue(information);
    }
    public bool ReceiveSalient(Observation information)
    {
        if(information.Origin!=InformationOrigin.Passive)throw new InvalidDataException("Expected a physical perception.");
        ReceivedCount++;
        return Enqueue(information);
    }
    private bool Enqueue(Observation information)
    {
        if (!pendingIds.Add((information.Id, information.Key))) return false;
        if (!inbox.TryGetValue(information.Key, out var entries)) inbox.Add(information.Key, entries = []);
        entries.Add(information);
        return true;
    }

    private bool IsImportant(Observation item)
    {
        if (item.Confidence < attention.Get("minimum_confidence").Number()) return false;
        foreach (var rule in attention.Get("rules").Items())
        {
            var relevant = rule.Get("kind").Text() switch
            {
                "deliberate_focus" => item.Origin == InformationOrigin.DeliberateObservation && explicitFocus.Contains(item.Key),
                "failed_action_focus" => item.Origin == InformationOrigin.ActionFailure && actionFocus.Contains(item.Key),
                "current_action" => actionFocus.Contains(item.Key),
                "current_goal" => goalFocus.Contains(item.Key),
                "known_conflict" => knowledge.Read(item.Key) is { Known: true } old && !Values.Same(old.Value, item.Value),
                "field" => FieldKey.Read(rule.Get("ref"), Id) == item.Key,
                _ => throw new InvalidDataException("Unsupported attention rule: " + rule.Get("kind").Text())
            };
            if (!relevant) continue;
            var condition = rule.Get("when");
            if (condition.ValueKind != JsonValueKind.Object || Condition.Compile(condition, Id).Evaluate(knowledge.Read) == Truth.True)
                return true;
        }
        return false;
    }

    public string BeginObservation(IEnumerable<FieldKey> focus)
    {
        if (IsObserving) throw new InvalidOperationException("This actor is already observing.");
        explicitFocus.Clear();
        explicitFocus.UnionWith(focus);
        observationToken = $"{Id}/observation/{++nextObservation}";
        return observationToken;
    }

    public void EndObservation(string token)
    {
        if (token != observationToken) throw new InvalidOperationException("Observation completion belongs to another interval.");
        observationToken = null;
        explicitFocus.Clear();
        BeginRefresh(RefreshCause.ObservationEnded);
    }

    public void BeginRefresh(RefreshCause cause)
    {
        if (IsObserving && cause != RefreshCause.ObservationEnded)
            throw new InvalidOperationException("Finish the observation interval before committing its batch.");
        if (refresh is not null) throw new InvalidOperationException("This actor already has an active knowledge update.");
        if (inbox.Count == 0) return;
        // Detach in constant time. New arrivals belong to the next deliberate batch.
        refresh = new RefreshWork(cause, inbox.Values.SelectMany(x => x).GetEnumerator());
        inbox = [];
        pendingIds = [];
    }

    public void BeginActionFeedback(IEnumerable<Observation> samples)
    {
        if (IsObserving || refresh is not null) throw new InvalidOperationException("Actor already owns a cognitive activity.");
        // Action feedback is its own batch. Unprocessed ambient messages stay buffered.
        refresh = new RefreshWork(RefreshCause.ActionCompleted, samples.Where(IsImportant).ToList().GetEnumerator());
    }

    public void ProcessDirectedMessages()
    {
        if(IsObserving || refresh is not null)return;
        var messages=inbox.Values.SelectMany(items=>items).Where(item=>item.Origin==InformationOrigin.Communication||item.Id.StartsWith("perception/",StringComparison.Ordinal)).ToArray();
        if(messages.Length==0)return;
        foreach(var message in messages)
        {
            var bucket=inbox[message.Key];bucket.Remove(message);
            if(bucket.Count==0)inbox.Remove(message.Key);
            pendingIds.Remove((message.Id,message.Key));
        }
        // A direct report received while working is considered at the next free moment.
        // Ambient observations remain buffered until deliberate attention.
        refresh=new RefreshWork(RefreshCause.Deliberate,((IEnumerable<Observation>)messages).GetEnumerator());
    }

    private void EnqueueReaders(RefreshWork work, FieldKey key)
    {
        if (!inferenceReaders.TryGetValue(key, out var readers)) return;
        foreach (var reader in readers) if (work.Queued.Add(reader)) work.Queue.Enqueue(reader);
    }

    public bool ContinueRefresh(int ruleBudget)
    {
        if (ruleBudget < 1) throw new ArgumentOutOfRangeException(nameof(ruleBudget));
        if (refresh is not { } work) return true;
        var remaining = ruleBudget;
        while (!work.InputFinished && remaining > 0)
        {
            remaining--;
            if (!work.Incoming.MoveNext()) { work.InputFinished = true; work.Incoming.Dispose(); break; }
            var sample = work.Incoming.Current;
            var key = sample.Key;
            work.Evidence.Add(new(sample.Id, key, sample.Value, sample.Confidence, sample.Time, "observation", sample.Source));
            var before = work.Writes.GetValueOrDefault(key) ?? knowledge.Read(key);
            if (before is { Known: true } && (sample.Time < before.ObservedAt || sample.Time == before.ObservedAt && sample.Confidence < before.Confidence)) continue;
            var tied = before is { Known: true } && sample.Time == before.ObservedAt && sample.Confidence == before.Confidence;
            var conflicted = tied && (before!.Conflicted || !Values.Same(before.Value, sample.Value));
            work.Writes[key] = new(sample.Value, true, conflicted, sample.Confidence, sample.Time, 0);
            if (sample.MapElement is { Length: > 0 } map) work.Map[map] = sample.Value;
            EnqueueReaders(work, key);
        }
        if (!work.InputFinished) return false;
        Fact? Read(FieldKey key) => work.Writes.GetValueOrDefault(key) ?? knowledge.Read(key);
        for (var count = 0; count < remaining && work.Queue.TryDequeue(out var id); count++)
        {
            work.Queued.Remove(id);
            InferenceEvaluations++;
            var rule = inference[id];
            if (rule.Premise.Evaluate(Read) != Truth.True) continue;
            var inputs = rule.Premise.Inputs.Select(Read).Where(f => f is not null).Cast<Fact>().ToArray();
            var confidence = inputs.Select(f => f.Confidence).Append(rule.Confidence).Min();
            var time = inputs.Select(f => f.ObservedAt).DefaultIfEmpty().Max();
            foreach (var (key, inferred) in rule.Preview(Read, time))
            {
                var old = Read(key);
                // Inference does not erase better supported observations. Contradictory
                // equally supported conclusions remain explicitly conflicted.
                if (old is { Known: true } && old.Confidence > confidence) continue;
                var candidate = inferred with { Confidence = confidence,
                    Conflicted = old is { Known: true } && old.Confidence == confidence && !Values.Same(old.Value, inferred.Value) };
                if (old is not null && Values.Same(old.Value, candidate.Value) && old.Confidence == candidate.Confidence && old.Conflicted == candidate.Conflicted) continue;
                if (!work.Conclusions.Add((id, key))) continue;
                work.Writes[key] = candidate;
                work.Evidence.Add(new(id, key, candidate.Value, confidence, time, "inference",
                    JsonSerializer.SerializeToElement(new {rule=id,basis=rule.Basis.ValueKind==JsonValueKind.Undefined?JsonSerializer.SerializeToElement<object?>(null):rule.Basis,
                        premises=rule.Premise.Inputs.Select(input=>new {key=input,value=Read(input)?.Value,evidence_ids=evidence.Concat(work.Evidence).Where(e=>e.Key==input).Select(e=>e.Id).Distinct().ToArray()}).ToArray()})));
                EnqueueReaders(work, key);
            }
        }
        if (work.Queue.Count > 0) return false;
        var changes = knowledge.Apply(work.Writes);
        evidence.AddRange(work.Evidence);
        var mapChanged = false;
        foreach (var (key, value) in work.Map)
        {
            if (rememberedMap.TryGetValue(key, out var old) && Values.Same(old, value)) continue;
            rememberedMap[key] = value;
            mapChanged = true;
        }
        if (mapChanged) MapRevision++;
        if (changes.Count > 0 || mapChanged) cachedView = null;
        RefreshCount++;
        refresh = null;
        if (changes.Count > 0) RebuildGoalFocus();
        return true;
    }

    private sealed class RefreshWork(RefreshCause cause, IEnumerator<Observation> incoming)
    {
        internal IEnumerator<Observation> Incoming { get; set; } = incoming;
        internal bool InputFinished;
        internal RefreshCause Cause { get; } = cause;
        internal Dictionary<FieldKey, Fact> Writes { get; } = [];
        internal Dictionary<string, JsonElement> Map { get; } = [];
        internal List<Evidence> Evidence { get; } = [];
        internal Queue<string> Queue { get; } = [];
        internal HashSet<string> Queued { get; } = [];
        internal HashSet<(string, FieldKey)> Conclusions { get; } = [];
    }
}
}

// --- Source: GoalPlanning.cs ---
namespace DuelView.Runtime
{


internal sealed record GoalPlanningGoal(string Id, Condition Target, double Weight);
internal sealed record GoalPlanningAction(string Id, Condition Precondition, IReadOnlyList<Effect> Effects,
    double Cost, double UncertaintyCost = 0, double FailurePenalty = 0);
internal sealed record GoalPlanningRequest(IReadOnlyDictionary<FieldKey, Fact> KnownFacts,
    IReadOnlyList<GoalPlanningGoal> Goals, IReadOnlyList<GoalPlanningAction> Actions,
    IReadOnlySet<string> FirstStepAllowed, int MaxDepth = 6, int MaxExpanded = 512, int BeamWidth = 20);
internal sealed record GoalPlanningResult(string? FirstAction, IReadOnlyList<string> ActionSequence,
    IReadOnlyList<string> ReachedGoals, double Utility, double Cost, int Expanded, string Status, string Algorithm);

/// <summary>
/// Bounded planning over the actor's explicit action models and own knowledge.
/// A prediction never writes into a live world or an actor's memory. Uncertain
/// outcomes, including another person's agreement, remain predictions to test.
/// </summary>
internal static class GoalPlanning
{
    private sealed record Node(Dictionary<FieldKey, Fact> State, string[] Path,
        double Cost, double Gain, string[] Reached, string Signature)
    {
        internal double Utility => Gain - Cost;
    }

    internal static GoalPlanningResult Solve(GoalPlanningRequest request, string algorithm)
    {
        if (algorithm is not ("best_first" or "beam"))
            throw new ArgumentException("Unknown goal planning algorithm: " + algorithm);
        if (request.MaxDepth is < 1 or > 12 || request.MaxExpanded is < 1 or > 8192 || request.BeamWidth is < 1 or > 128)
            throw new ArgumentException("Goal planning budgets are outside the supported bounds.");
        if (request.Actions.Count > 256 || request.Goals.Count > 32 ||
            request.Actions.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != request.Actions.Count ||
            request.Goals.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != request.Goals.Count)
            throw new ArgumentException("Invalid or excessive goal planning declarations.");
        foreach (var goal in request.Goals)
            if (string.IsNullOrWhiteSpace(goal.Id) || !double.IsFinite(goal.Weight) || goal.Weight <= 0)
                throw new ArgumentException("A planning goal requires a positive finite weight.");
        foreach (var action in request.Actions)
            if (string.IsNullOrWhiteSpace(action.Id) || !FiniteCost(action.Cost) ||
                !FiniteCost(action.UncertaintyCost) || !FiniteCost(action.FailurePenalty))
                throw new ArgumentException("A planning action has an invalid cost.");

        var keys = request.Goals.SelectMany(g => g.Target.Inputs)
            .Concat(request.Actions.SelectMany(a => a.Precondition.Inputs))
            .Concat(request.Actions.SelectMany(a => a.Effects.Select(e => e.Key)))
            .Distinct().OrderBy(k => k.Object, StringComparer.Ordinal)
            .ThenBy(k => k.Facet, StringComparer.Ordinal).ThenBy(k => k.Field, StringComparer.Ordinal).ToArray();
        var initial = new Dictionary<FieldKey, Fact>();
        foreach (var key in keys)
            if (request.KnownFacts.TryGetValue(key, out var fact)) initial[key] = fact;
        Fact? InitialRead(FieldKey key) => initial.GetValueOrDefault(key);
        var initiallyReached = request.Goals.Where(g => g.Target.Evaluate(InitialRead) == Truth.True)
            .Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        var initialWeight = request.Goals.Where(g => initiallyReached.Contains(g.Id)).Sum(g => g.Weight);
        if (request.Goals.Count == 0 || initiallyReached.Count == request.Goals.Count)
            return new(null, [], initiallyReached.Order(StringComparer.Ordinal).ToArray(), 0, 0, 0, "already_satisfied", algorithm);

        // Regression only removes operators with no possible relevance to these
        // goals; it does not invent facts or an undiscovered operator.
        var relevant = request.Goals.SelectMany(g => g.Target.Inputs).ToHashSet();
        for (var level = 0; level < request.MaxDepth; ++level)
        {
            var before = relevant.Count;
            foreach (var action in request.Actions)
                if (action.Effects.Any(e => relevant.Contains(e.Key)))
                    relevant.UnionWith(action.Precondition.Inputs);
            if (relevant.Count == before) break;
        }
        var operators = request.Actions.Where(a => a.Effects.Any(e => relevant.Contains(e.Key)))
            .OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
        var start = new Node(initial, [], 0, 0, initiallyReached.Order(StringComparer.Ordinal).ToArray(), Signature(initial, keys));
        Node? best = null;
        var expanded = 0;
        var limited = false;
        var bestCosts = new Dictionary<(string Signature, int Depth), double> { [(start.Signature, 0)] = 0 };

        IEnumerable<Node> Successors(Node node)
        {
            if (node.Path.Length >= request.MaxDepth) yield break;
            Fact? Read(FieldKey key) => node.State.GetValueOrDefault(key);
            foreach (var action in operators)
            {
                if (node.Path.Length == 0 && !request.FirstStepAllowed.Contains(action.Id)) continue;
                if (action.Precondition.Evaluate(Read) != Truth.True) continue;
                var state = new Dictionary<FieldKey, Fact>(node.State);
                var valid = true;
                foreach (var effect in action.Effects)
                {
                    try
                    {
                        state[effect.Key] = effect.Resolve(k => state.GetValueOrDefault(k), 0, 1);
                    }
                    catch (InvalidOperationException)
                    {
                        // An increment without a known uncontested quantity is
                        // not a permitted mental shortcut to an invented stock.
                        valid = false;
                        break;
                    }
                }
                if (!valid) continue;
                var signature = Signature(state, keys);
                if (signature == node.Signature) continue;
                var cost = node.Cost + Math.Max(0.001, action.Cost) + action.UncertaintyCost + action.FailurePenalty;
                var stateKey = (signature, node.Path.Length + 1);
                if (bestCosts.TryGetValue(stateKey, out var prior) && prior <= cost + 1e-9) continue;
                bestCosts[stateKey] = cost;
                Fact? NextRead(FieldKey key) => state.GetValueOrDefault(key);
                var reached = request.Goals.Where(g => g.Target.Evaluate(NextRead) == Truth.True).ToArray();
                var next = new Node(state, [.. node.Path, action.Id], cost,
                    reached.Sum(g => g.Weight) - initialWeight,
                    reached.Select(g => g.Id).ToArray(), signature);
                if (next.Utility > 0 && Better(next, best)) best = next;
                yield return next;
            }
        }

        if (algorithm == "best_first")
        {
            // Uniform cost expansion is a useful independent comparison to the
            // depth-limited beam; bounded execution makes no optimality claim.
            var queue = new PriorityQueue<Node, (double Cost, int Length, long Order)>();
            long order = 0;
            queue.Enqueue(start, (0, 0, order++));
            while (queue.TryDequeue(out var node, out _))
            {
                if (bestCosts.GetValueOrDefault((node.Signature, node.Path.Length), double.PositiveInfinity) < node.Cost - 1e-9) continue;
                if (expanded >= request.MaxExpanded) { limited = true; break; }
                ++expanded;
                foreach (var child in Successors(node))
                    if (child.Path.Length < request.MaxDepth)
                        queue.Enqueue(child, (child.Cost, child.Path.Length, order++));
            }
        }
        else
        {
            var frontier = new List<Node> { start };
            for (var depth = 0; depth < request.MaxDepth && frontier.Count > 0; ++depth)
            {
                var next = new List<Node>();
                foreach (var node in frontier)
                {
                    if (expanded >= request.MaxExpanded) { limited = true; break; }
                    ++expanded;
                    next.AddRange(Successors(node));
                }
                if (limited) break;
                // Reached utility is the principal beam signal. Stable ordering
                // makes a comparison reproducible for the same known inputs.
                frontier = next.OrderByDescending(n => n.Utility).ThenBy(n => n.Cost)
                    .ThenBy(n => string.Join('\u001f', n.Path), StringComparer.Ordinal)
                    .Take(request.BeamWidth).ToList();
            }
        }

        if (best is null)
            return new(null, [], [], 0, 0, expanded, limited ? "budget_exhausted" : "no_plan", algorithm);
        var status = best.Reached.Length == request.Goals.Count ? "predicted_goals" : "partial_plan";
        if (limited) status = "budget_limited_" + status;
        return new(best.Path[0], best.Path, best.Reached, best.Utility, best.Cost, expanded, status, algorithm);
    }

    private static bool FiniteCost(double value) => double.IsFinite(value) && value >= 0;
    private static bool Better(Node candidate, Node? current) => current is null ||
        candidate.Utility > current.Utility + 1e-9 ||
        Math.Abs(candidate.Utility - current.Utility) < 1e-9 && candidate.Cost < current.Cost - 1e-9;

    private static string Signature(IReadOnlyDictionary<FieldKey, Fact> state, IReadOnlyList<FieldKey> keys)
    {
        var text = new StringBuilder(keys.Count * 16);
        foreach (var key in keys)
        {
            if (!state.TryGetValue(key, out var fact)) { text.Append("?;"); continue; }
            text.Append(fact.Known ? 'K' : 'U').Append(fact.Conflicted ? 'C' : 'S').Append(':');
            if (fact.Value.ValueKind != JsonValueKind.Undefined)
            {
                var raw = fact.Value.GetRawText();
                text.Append(raw.Length).Append(':').Append(raw);
            }
            text.Append(';');
        }
        return text.ToString();
    }
}
}

// --- Source: GoalPlanningAdapter.cs ---
namespace DuelView.Runtime
{


/// <summary>Supplies only owned beliefs and explicit remembered action models to
/// the bounded solver. The objective world's enforcement laws are never read.</summary>
internal sealed class GoalPlanningAdapter
{
    private sealed record Objective(GoalPlanningGoal Goal,Condition Admission);
    private sealed record Model(ActionCandidate Action,Condition Admission,Effect[] Effects,double Cost,double Uncertainty);
    private readonly ActorClient actor;
    private readonly Objective[] goals;
    private readonly Model[] models;
    private readonly Condition admission;
    private readonly string algorithm;
    private readonly bool compare;
    private readonly int depth,expanded,width;
    private readonly double timeUnit;
    private string cache="";
    private GoalPlanningResult? selected,alternative;
    private double selectedFirstPenalty;
    private SavedFact[] basis=[];
    internal string? FirstAction=>selected is {Utility:>0}?selected.FirstAction:null;

    internal GoalPlanningAdapter(ActorClient actor,ActionCandidate[] catalog,double timeUnit)
    {
        this.actor=actor;this.timeUnit=timeUnit;
        var declaration=actor.Declaration.Get("goal_planning");
        admission=declaration.Get("when").ValueKind==JsonValueKind.Object?Condition.Compile(declaration.Get("when"),actor.Id):new Condition.Constant(Truth.True);
        algorithm=declaration.Get("algorithm").Text("best_first");compare=declaration.Get("compare_algorithms").Flag();
        if(algorithm is not ("best_first" or "beam"))throw new InvalidDataException("Unknown personal-goal planning algorithm.");
        int Budget(string name,int fallback,int maximum)
        {
            var value=declaration.Get(name).Number(fallback);
            if(!double.IsFinite(value)||value<1||value>maximum||value!=Math.Truncate(value))throw new InvalidDataException("Invalid goal planning budget: "+name);
            return (int)value;
        }
        depth=Budget("max_depth",6,12);expanded=Budget("max_expanded",512,8192);width=Budget("beam_width",20,128);
        goals=declaration.Get("goals").Items().Select(item=>
        {
            var id=item.Get("id").Text();var weight=item.Get("weight").Number(double.NaN);
            if(string.IsNullOrWhiteSpace(id)||!double.IsFinite(weight)||weight<=0||item.Get("target").ValueKind!=JsonValueKind.Object)
                throw new InvalidDataException("A personal goal needs a named, weighted desired condition.");
            return new Objective(new(id,Condition.Compile(item.Get("target"),actor.Id),weight),
                item.Get("when").ValueKind==JsonValueKind.Object?Condition.Compile(item.Get("when"),actor.Id):new Condition.Constant(Truth.True));
        }).ToArray();
        if(goals.Length>32||goals.Select(item=>item.Goal.Id).Distinct(StringComparer.Ordinal).Count()!=goals.Length)
            throw new InvalidDataException("Duplicate or excessive personal goals.");
        models=catalog.Where(action=>action.Declaration.Get("planning_model").ValueKind==JsonValueKind.Object).Select(action=>
        {
            var model=action.Declaration.Get("planning_model");var cost=model.Get("cost").Number(action.Duration/timeUnit);
            var uncertainty=model.Get("uncertainty_cost").Number();
            var effects=model.Get("effects").Items().Select(effect=>Effect.Compile(effect,actor.Id)).ToArray();
            if(!double.IsFinite(cost)||cost<0||!double.IsFinite(uncertainty)||uncertainty<0||effects.Length is <1 or >64||
                effects.Select(effect=>effect.Key).Distinct().Count()!=effects.Length)
                throw new InvalidDataException("Invalid explicit action planning model: "+action.Id);
            if(action.Declaration.Get("suggestion").ValueKind==JsonValueKind.Object&&uncertainty<=0)
                throw new InvalidDataException("A planning model for persuasion must price another person's uncertain response: "+action.Id);
            return new Model(action,model.Get("when").ValueKind==JsonValueKind.Object?Condition.Compile(model.Get("when"),actor.Id):new Condition.Constant(Truth.True),effects,cost,uncertainty);
        }).ToArray();
        if(models.Length>256)throw new InvalidDataException("Personal goal planning exceeds its action-model budget.");
    }

    internal void Evaluate(double now,Func<ActionCandidate,bool> eligible,Func<ActionCandidate,bool> modelEligible,ActionFailureMemory failures)
    {
        if(goals.Length==0||admission.Evaluate(actor.Recall)!=Truth.True){selected=null;alternative=null;selectedFirstPenalty=0;basis=[];cache="";return;}
        var admitted=goals.Where(goal=>goal.Admission.Evaluate(actor.Recall)==Truth.True).Select(goal=>goal.Goal).ToArray();
        var available=models.Where(model=>!model.Action.Declaration.Get("manual_only").Flag()&&!model.Action.Declaration.Get("physiology_route_only").Flag()&&
            modelEligible(model.Action)&&model.Admission.Evaluate(actor.Recall)==Truth.True&&now/timeUnit>=model.Action.Declaration.Get("not_before_game_minutes").Number()&&
            now/timeUnit<=model.Action.Declaration.Get("not_after_game_minutes").Number(double.PositiveInfinity)).ToArray();
        var first=available.Where(model=>eligible(model.Action)&&model.Action.Recognition.Evaluate(actor.Recall)==Truth.True&&model.Action.Gate.Evaluate(actor.Recall)==Truth.True).Select(model=>model.Action.Id).ToHashSet(StringComparer.Ordinal);
        var operators=available.Select(model=>new GoalPlanningAction(model.Action.Id,new Condition.All([model.Action.Gate,model.Action.Recognition]),model.Effects,model.Cost,model.Uncertainty,failures.Penalty(model.Action.Id,now))).ToArray();
        var keys=admitted.SelectMany(goal=>goal.Target.Inputs).Concat(available.SelectMany(model=>model.Action.Gate.Inputs.Concat(model.Action.Recognition.Inputs).Concat(model.Effects.Select(effect=>effect.Key))))
            .Distinct().OrderBy(key=>key.Object,StringComparer.Ordinal).ThenBy(key=>key.Facet,StringComparer.Ordinal).ThenBy(key=>key.Field,StringComparer.Ordinal).ToArray();
        var facts=keys.Where(key=>actor.Recall(key) is not null).ToDictionary(key=>key,key=>actor.Recall(key)!);
        var signature=string.Join('|',admitted.Select(goal=>goal.Id))+":"+string.Join('|',operators.Select(model=>model.Id+"="+model.FailurePenalty.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+
            ":"+string.Join('|',first.Order(StringComparer.Ordinal))+":"+string.Join('|',keys.Select(key=>key+"="+(facts.TryGetValue(key,out var fact)?fact.Revision+"/"+fact.Known+"/"+fact.Conflicted+"/"+fact.Value.GetRawText():"unknown")));
        if(signature==cache)return;
        var request=new GoalPlanningRequest(facts,admitted,operators,first,depth,expanded,width);
        selected=GoalPlanning.Solve(request,algorithm);
        selectedFirstPenalty=operators.FirstOrDefault(action=>action.Id==selected.FirstAction)?.FailurePenalty??0;
        alternative=compare?GoalPlanning.Solve(request,algorithm=="best_first"?"beam":"best_first"):null;
        basis=facts.Select(pair=>new SavedFact(pair.Key,pair.Value)).ToArray();cache=signature;
    }
    // The scheduler applies the current step's failure penalty once, alongside
    // other candidates. Later predicted failures remain in the plan's cost.
    internal double Score(string action)=>FirstAction==action?Math.Max(0,selected!.Utility)+selectedFirstPenalty:0;
    internal object Audit()=>new {selected,comparison=alternative,owned_basis=basis,prediction_only=true};
    internal JsonElement Save()=>JsonSerializer.SerializeToElement(new {cache,selected,alternative,basis,selectedFirstPenalty});
    internal void Restore(JsonElement state)
    {
        selected=null;alternative=null;basis=[];cache="";selectedFirstPenalty=0;
        if(state.ValueKind==JsonValueKind.Undefined)return;
        if(state.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Invalid goal-planning checkpoint.");
        GoalPlanningResult? Result(string key)
        {
            var value=state.Get(key);
            if(value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)return null;
            var result=value.Deserialize<GoalPlanningResult>()??throw new InvalidDataException("Missing goal-planning result.");
            if(result.Algorithm is not ("best_first" or "beam")||!double.IsFinite(result.Utility)||!double.IsFinite(result.Cost)||result.Cost<0||
                result.Expanded is <0 or >8192||result.ActionSequence is null||result.ActionSequence.Count>12||result.ReachedGoals is null||result.ReachedGoals.Count>32||
                result.ActionSequence.Any(id=>!models.Any(model=>model.Action.Id==id))||result.ReachedGoals.Any(id=>!goals.Any(goal=>goal.Goal.Id==id))||
                result.FirstAction is { } first&&(result.ActionSequence.Count==0||result.ActionSequence[0]!=first))
                throw new InvalidDataException("Incompatible goal-planning result checkpoint.");
            return result;
        }
        selected=Result("selected");alternative=Result("alternative");
        cache=state.Get("cache").Text();selectedFirstPenalty=state.Get("selectedFirstPenalty").Number();
        basis=state.Get("basis").Deserialize<SavedFact[]>()??[];
        if(!double.IsFinite(selectedFirstPenalty)||selectedFirstPenalty<0||cache.Length>1000000||basis.Select(item=>item.Key).Distinct().Count()!=basis.Length)
            throw new InvalidDataException("Invalid goal-planning cache checkpoint.");
    }
    internal DecisionIntent? Intent(string action)=>FirstAction==action?new("goal",null,null,null,
        "根据本人已知条件预测"+selected!.ActionSequence.Count+"步行动，检验能否推进"+selected.ReachedGoals.Count+"项自身目标；结果仍待现场执行。",
        GoalAlgorithm:selected.Algorithm,GoalSequence:selected.ActionSequence.ToArray(),GoalObjectives:selected.ReachedGoals.ToArray()):null;
}
}

// --- Source: GoalPlanningChecks.cs ---
namespace DuelView.Runtime
{


public static class GoalPlanningChecks
{
    private static JsonElement J(object value) => JsonSerializer.SerializeToElement(value);
    private static FieldKey K(string name) => new("subject", "state", name);
    private static Fact F(object value) => new(J(value), true, false, 1, 0, 1);
    private static Condition Eq(string key, object value) => new Condition.EqualsValue(K(key), J(value));
    private static Condition All(params Condition[] items) => new Condition.All(items);
    private static Effect Set(string key, object value) => new(K(key), J(value), false);
    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static GoalPlanningRequest Request(Dictionary<FieldKey, Fact> known, GoalPlanningAction[] actions,
        GoalPlanningGoal[]? goals = null, HashSet<string>? first = null) => new(known,
        goals ?? [new("open", Eq("open", true), 30)], actions,
        first ?? actions.Select(a => a.Id).ToHashSet(StringComparer.Ordinal), MaxExpanded:128);

    public static int Run()
    {
        var failures = 0;
        void Check(string label, Action body)
        {
            var watch = Stopwatch.StartNew();
            try { body(); Console.WriteLine($"PASS {label} {watch.Elapsed.TotalMilliseconds:F3} ms"); }
            catch (Exception error) { ++failures; Console.WriteLine($"FAIL {label}: {error}"); }
        }
        foreach (var algorithm in new[] { "best_first", "beam" })
        {
            Check(algorithm + " plans a grounded three-step means-end chain without mutating knowledge", () =>
            {
                var known = new Dictionary<FieldKey, Fact> { [K("room")] = F("hall"), [K("key")] = F(false), [K("open")] = F(false) };
                var actions = new GoalPlanningAction[] {
                    new("walk", Eq("room", "hall"), [Set("room", "desk")], 1),
                    new("take_known_key", All(Eq("room", "desk"), Eq("key", false)), [Set("key", true)], 1),
                    new("unlock", Eq("key", true), [Set("open", true)], 1)
                };
                var result = GoalPlanning.Solve(Request(known, actions), algorithm);
                Expect(result.ActionSequence.SequenceEqual(new[] { "walk", "take_known_key", "unlock" }), "Expected the actual prerequisite chain.");
                Expect(result.ReachedGoals.Contains("open") && result.Utility == 27, "Incorrect goal utility.");
                Expect(known[K("room")].Value.GetString() == "hall" && !known[K("key")].Value.GetBoolean() && !known[K("open")].Value.GetBoolean(), "Planning wrote live knowledge.");
            });
            Check(algorithm + " cannot invent an unknown prerequisite or an unknown resource quantity", () =>
            {
                var known = new Dictionary<FieldKey, Fact> { [K("open")] = F(false) };
                var actions = new GoalPlanningAction[] {
                    new("unknown_key", Eq("key", true), [Set("open", true)], 1),
                    new("unmeasured_stock", new Condition.Constant(Truth.True), [new Effect(K("stock"), J(-1), true), Set("open", true)], 1)
                };
                Expect(GoalPlanning.Solve(Request(known, actions), algorithm).FirstAction is null, "Unknown facts were silently promoted to true.");
            });
            Check(algorithm + " failed attempts change the selected means and known changes permit replanning", () =>
            {
                var known = new Dictionary<FieldKey, Fact> { [K("open")] = F(false), [K("alternate")] = F(false) };
                var shortcut = new GoalPlanningAction("direct", Eq("open", false), [Set("open", true)], 1);
                var fallback = new GoalPlanningAction[] {
                    new("prepare", Eq("open", false), [Set("alternate", true)], 2),
                    new("alternative", Eq("alternate", true), [Set("open", true)], 2)
                };
                Expect(GoalPlanning.Solve(Request(known, [shortcut, .. fallback]), algorithm).FirstAction == "direct", "A first attempt was preemptively penalized.");
                Expect(GoalPlanning.Solve(Request(known, [shortcut with { FailurePenalty = 12 }, .. fallback]), algorithm).FirstAction == "prepare", "The failed attempt did not change the plan.");
                Expect(GoalPlanning.Solve(Request(known, [shortcut, .. fallback]), algorithm).FirstAction == "direct", "Removing a resolved obstruction did not restore the option.");
            });
            Check(algorithm + " retains uncertainty cost for another actor's consent", () =>
            {
                var known = new Dictionary<FieldKey, Fact> { [K("open")] = F(false), [K("alternate")] = F(false) };
                var actions = new GoalPlanningAction[] {
                    new("ask_unreliable_favor", Eq("open", false), [Set("open", true)], 1, UncertaintyCost:12),
                    new("prepare_own_tool", Eq("open", false), [Set("alternate", true)], 2),
                    new("use_own_tool", Eq("alternate", true), [Set("open", true)], 2)
                };
                Expect(GoalPlanning.Solve(Request(known, actions), algorithm).FirstAction == "prepare_own_tool", "Predicted consent was treated as free certainty.");
            });
            Check(algorithm + " preserves resource accounting between mutually competing goals", () =>
            {
                var known = new Dictionary<FieldKey, Fact> { [K("water")] = F(1), [K("food")] = F(false), [K("cloth")] = F(false) };
                var actions = new GoalPlanningAction[] {
                    new("cook", new Condition.AtLeast(K("water"), J(1)), [new(K("water"), J(-1), true), Set("food", true)], 1),
                    new("wash", new Condition.AtLeast(K("water"), J(1)), [new(K("water"), J(-1), true), Set("cloth", true)], 1)
                };
                var goals = new GoalPlanningGoal[] { new("food", Eq("food", true), 20), new("cloth", Eq("cloth", true), 30) };
                var result = GoalPlanning.Solve(Request(known, actions, goals), algorithm);
                Expect(result.FirstAction == "wash" && result.ReachedGoals.SequenceEqual(new[] { "cloth" }), "One unit of water was spent twice.");
            });
            Check(algorithm + " respects present first-step admission and does not reward already met goals", () =>
            {
                var known = new Dictionary<FieldKey, Fact> { [K("open")] = F(false), [K("prepared")] = F(false) };
                var actions = new GoalPlanningAction[] {
                    new("direct", new Condition.Constant(Truth.True), [Set("open", true)], 1),
                    new("prepare", new Condition.Constant(Truth.True), [Set("prepared", true)], 1),
                    new("finish", Eq("prepared", true), [Set("open", true)], 1)
                };
                var result = GoalPlanning.Solve(Request(known, actions, first:new HashSet<string>{"prepare"}), algorithm);
                Expect(result.FirstAction == "prepare", "A non-admitted action became the first move.");
                known[K("open")] = F(true);
                result = GoalPlanning.Solve(Request(known, actions), algorithm);
                Expect(result.FirstAction is null && result.Utility == 0, "An already satisfied purpose produced a spurious reward.");
            });
            Check(algorithm + " terminates at its explicit search budget", () =>
            {
                var known = new Dictionary<FieldKey, Fact> { [K("counter")] = F(0), [K("open")] = F(false) };
                var actions = new GoalPlanningAction[] {
                    new("count", new Condition.Constant(Truth.True), [new(K("counter"), J(1), true)], 0),
                    new("finish", new Condition.AtLeast(K("counter"), J(100)), [Set("open", true)], 1)
                };
                var result = GoalPlanning.Solve(Request(known, actions) with { MaxExpanded = 3 }, algorithm);
                Expect(result.Expanded <= 3 && result.FirstAction is null, "Bounded search escaped its budget or invented a completed goal.");
            });
        }
        return failures;
    }
}
}

// --- Source: StrategyActivity.cs ---
namespace DuelView.Runtime
{


public sealed record StrategyActivityState(long Revision,double Due,bool Active,long Operations,bool WasComputing,CaseStrategyChoice[]? Cached);

/// <summary>An explicitly started, time-consuming activity. Ordinary autonomy never
/// invokes proof search. Results are cached against the owner's knowledge revision.</summary>
public sealed class StrategyActivity : IDisposable
{
    private readonly ActorClient owner;
    private readonly CaseTheory authored;
    private readonly JsonElement profile;
    private readonly JsonElement[] mappings;
    private readonly double contentTimeUnit;
    private readonly CaseLiteral? activityLiteral;
    private StrategyJudgment.CaseStrategyWork? work;
    private IReadOnlyList<CaseStrategyChoice>? cached;
    private long revision = -1;
    private double due;
    private bool active;
    public bool Computing => work is not null;
    public bool Active => active;
    public long Operations { get; private set; }
    public StrategyActivityState SaveState()=>new(revision,due,active,Operations,work is not null,cached?.ToArray());
    public void RestoreState(StrategyActivityState state)
    {
        if(state.Revision< -1 || !double.IsFinite(state.Due)||state.Due<0 || state.Operations<0 || state.WasComputing && !state.Active || state.WasComputing && state.Cached is not null)
            throw new InvalidDataException("Invalid strategy checkpoint.");
        work?.Dispose();work=null;revision=state.Revision;due=state.Due;active=state.Active;Operations=state.Operations;cached=state.Cached;
        if(state.WasComputing)Restart();
    }

    public StrategyActivity(ActorClient owner, CaseTheory authored, JsonElement profile, JsonElement evidenceMappings, double contentTimeUnit,JsonElement activityEvidence=default)
    {
        this.owner = owner;
        this.authored = authored;
        this.profile = profile.Clone();
        if (!double.IsFinite(contentTimeUnit) || contentTimeUnit <= 0) throw new ArgumentOutOfRangeException(nameof(contentTimeUnit));
        this.contentTimeUnit = contentTimeUnit;
        if(activityEvidence.ValueKind==JsonValueKind.Object)
        {
            activityLiteral=activityEvidence.Deserialize<CaseLiteral>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Invalid activity evidence literal.");
            if(!activityLiteral.Ground)throw new InvalidDataException("Activity evidence must be ground.");
        }
        var actor = profile.Get("runtime_actor").Text(profile.Get("actor").Text());
        if (actor != owner.Id || profile.Get("actor").Text() == "objective") throw new InvalidDataException("Strategy activity belongs to another actor.");
        mappings = evidenceMappings.Items().Where(x => x.Get("actor").Text() == owner.Id).ToArray();
    }

    public void Begin(double now, double authoredDuration)
    {
        if (active) throw new InvalidOperationException("Strategy activity already running.");
        if (!double.IsFinite(now) || now < 0 || !double.IsFinite(authoredDuration) || authoredDuration <= 0 || !double.IsFinite(now + authoredDuration * contentTimeUnit))
            throw new InvalidDataException("Strategy activity needs a finite positive DV duration.");
        due = now + authoredDuration * contentTimeUnit;
        active = true;
        if (cached is null || revision != owner.KnowledgeRevision) Restart();
    }

    private void Restart()
    {
        work?.Dispose();
        cached = null;
        revision = owner.KnowledgeRevision;
        var scope = profile.Get("actor").Text();
        var evidence = new List<CaseAxiom>();
        if(activityLiteral is not null)evidence.Add(new("activity/started",activityLiteral,"actor:"+owner.Id+":active-work"));
        foreach (var mapping in mappings)
        {
            var reference=mapping.Get("reference");
            var key = reference.Get("type").Text()=="ref" ? FieldKey.Read(reference,owner.Id)
                : new FieldKey(reference.Get("object").Text(),reference.Get("facet").Text(),reference.Get("field").Text());
            if(string.IsNullOrWhiteSpace(key.Object)||string.IsNullOrWhiteSpace(key.Facet)||string.IsNullOrWhiteSpace(key.Field))
                throw new InvalidDataException("Invalid strategy evidence reference: "+mapping.Get("id").Text());
            if (owner.Recall(key) is not { Known: true, Conflicted: false } fact || !Values.Same(fact.Value, mapping.Get("equals"))) continue;
            var literal = mapping.Get("literal").Deserialize<CaseLiteral>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Invalid DV strategy evidence literal.");
            evidence.Add(new("knowledge/" + mapping.Get("id").Text() + "/" + fact.Revision, literal, "actor:" + owner.Id));
        }
        // Scenario axioms are authoring fixtures, not the actor's live memory.
        // Stable personal values must be explicitly named in the profile.
        foreach(var item in profile.Get("intrinsic_axioms").Items())
        {
            var id=item.Text();
            var matches=authored.Scopes[scope].Axioms.Where(x=>x.Id==id).ToArray();
            if(matches.Length!=1)throw new InvalidDataException("Unknown intrinsic strategy axiom: "+id);
            evidence.Add(matches[0]);
        }
        var theory = authored.WithoutAssumptions(scope).WithEvidence(scope, evidence);
        work = StrategyJudgment.BeginEvaluation(theory, scope, profile);
    }

    public void Step(int operationBudget, TimeSpan timeBudget)
    {
        if (!active) return;
        if (owner.IsUpdating) return;
        if (revision != owner.KnowledgeRevision) Restart();
        if (work is null) return;
        var slice = work.Advance(operationBudget, timeBudget);
        Operations += slice.Operations;
        if (slice.Status == CaseWorkStatus.Completed)
        {
            cached = work.Result;
            work.Dispose();
            work = null;
        }
        else if (slice.Status is CaseWorkStatus.Faulted or CaseWorkStatus.BudgetExhausted or CaseWorkStatus.Cancelled)
            throw new InvalidOperationException("Strategy computation failed: " + slice.Error);
    }

    public IReadOnlyList<CaseStrategyChoice>? Finish(double now)
    {
        if (!active || now < due || work is not null || owner.IsUpdating || revision != owner.KnowledgeRevision) return null;
        active = false;
        return cached;
    }

    public void Cancel() { work?.Dispose(); work=null; active=false; }

    public void Dispose() { work?.Dispose(); work = null; active = false; }
}
}
