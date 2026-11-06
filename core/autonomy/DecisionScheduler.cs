// @mechanism: autonomy_drive_solver
// @layer: core/autonomy
// @contracts: DecisionScheduler

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

// --- Source: DecisionScheduler.cs ---
namespace DuelView.Runtime
{


public sealed record ActionCandidate(string Id, Condition Gate, double Duration, double Score,
    JsonElement Declaration,Condition? RecognitionGate=null)
{
    public Condition Recognition=>RecognitionGate??Gate;
}
public sealed record DecisionIntent(string Path,string? Task,string? Step,string? PhysiologicalNeed,string Reason,string? PhysiologicalRoute=null,string? RouteAction=null,
    string? GoalAlgorithm=null,string[]? GoalSequence=null,string[]? GoalObjectives=null);
public sealed record Decision(string Actor, ActionCandidate Action, double StartedAt, double DueAt,long Execution=0,DecisionOption[]? Options=null,DecisionIntent? Intent=null);
public sealed record DecisionOption(string Action,double Score,DriveEvaluation Drive,double FailurePenalty=0,double GoalUtility=0,string PredictedReadiness="True");
public sealed record SchedulerSlice(int Evaluations, bool Pending);
public sealed record DecisionCheckpoint(string Actor,string Action,double StartedAt,double DueAt,long Execution,DecisionIntent? Intent=null);
public sealed record SchedulerSlotCheckpoint(string Actor,double Due,int Cursor,string? Best,double? BestScore,long Revision,bool Autonomous,bool Held,DriveState Needs,string[] Selection,double SelectionTime,DecisionCheckpoint? Running,TaskPlannerCheckpoint? Planning=null,ActionFailureCheckpoint[]? FailureMemory=null,JsonElement GoalPlanning=default);
public sealed record DecisionSchedulerState(SchedulerSlotCheckpoint[] Slots,long[] Ready,int Cursor,long NextExecution,long Evaluations);

/// <summary>Persistent round-robin cursors bound work independently of actor and action counts.
/// Gates read the actor's remembered facts, never the objective world.</summary>
public sealed class DecisionScheduler
{
    private sealed class Slot(ActorClient actor, ActionCandidate[] candidates, double now, double due)
    {
        internal ActorClient Actor = actor;
        internal ActionCandidate[] Candidates = candidates;
        internal double Due = due;
        internal int Cursor;
        internal ActionCandidate? Best;
        internal Decision? Running;
        internal long Revision = actor.KnowledgeRevision;
        internal bool Autonomous = true;
        internal bool Held;
        internal DriveCalculator Needs = new(actor.Declaration.Get("autonomy"), now);
        internal double BestScore = double.NegativeInfinity;
        internal DriveCandidateIndex Index=null!;
        internal ActionCandidate[] Selection=[];
        internal double SelectionTime;
        internal TaskPlanner Planner=null!;
        internal ActionFailureMemory Failures=null!;
        internal GoalPlanningAdapter Goals=null!;
    }

    private readonly List<Slot> slots = [];
    private readonly Dictionary<string, Slot> byActor = new(StringComparer.Ordinal);
    private readonly Queue<Decision> ready = [];
    private readonly double interval;
    private readonly double stagger;
    private readonly int candidatesPerDecision;
    private readonly int evaluationsPerSlice;
    private readonly double millisecondsPerSlice;
    private readonly double contentTimeUnit;
    private readonly int drivesPerDecision,candidatesPerDrive;
    private readonly double minimumUtility;
    private readonly Func<string,ActionCandidate,bool> eligible;
    private readonly Func<string,ActionCandidate,bool> modelEligible;
    private int cursor;
    private long nextExecution;
    public long Evaluations { get; private set; }
    public void SetAutonomy(string actor,bool enabled)=>byActor[actor].Autonomous=enabled;
    public DecisionSchedulerState SaveState()=>new(slots.Select(slot=>new SchedulerSlotCheckpoint(slot.Actor.Id,slot.Due,slot.Cursor,slot.Best?.Id,
        double.IsFinite(slot.BestScore)?slot.BestScore:null,slot.Revision,slot.Autonomous,slot.Held,slot.Needs.SaveState(),
        slot.Selection.Select(action=>action.Id).ToArray(),slot.SelectionTime,slot.Running is { } run?new(run.Actor,run.Action.Id,run.StartedAt,run.DueAt,run.Execution,run.Intent):null,slot.Planner.SaveState(),slot.Failures.Save(),slot.Goals.Save())).ToArray(),
        ready.Where(Owns).Select(item=>item.Execution).ToArray(),cursor,nextExecution,Evaluations);
    public IReadOnlyDictionary<long,Decision> RestoreState(DecisionSchedulerState state)
    {
        if(state.Slots is null || state.Ready is null || state.Slots.Length!=slots.Count || state.Cursor<0 || state.Cursor>=Math.Max(1,slots.Count) || state.NextExecution<0 || state.Evaluations<0 ||
           state.Slots.Select(item=>item.Actor).Distinct(StringComparer.Ordinal).Count()!=slots.Count || state.Ready.Distinct().Count()!=state.Ready.Length)
            throw new InvalidDataException("Invalid scheduler checkpoint.");
        var decisions=new Dictionary<long,Decision>();
        foreach(var item in state.Slots)
        {
            if(!byActor.TryGetValue(item.Actor,out var slot) || !double.IsFinite(item.Due)||item.Due<0 || item.Cursor<0 || item.Selection is null || item.Cursor>item.Selection.Length ||
               item.Revision<0 || !double.IsFinite(item.SelectionTime)||item.SelectionTime<0 || item.BestScore is { } score && !double.IsFinite(score))throw new InvalidDataException("Invalid scheduler slot checkpoint.");
            var actions=slot.Candidates.ToDictionary(action=>action.Id,StringComparer.Ordinal);
            if(item.Selection.Any(id=>!actions.ContainsKey(id)) || item.Best is { } best && !actions.ContainsKey(best))throw new InvalidDataException("Checkpoint references missing action.");
            if(item.Running is { } run)
            {
                if(run.Actor!=item.Actor || !actions.TryGetValue(run.Action,out var action) || run.Execution<1 || run.Execution>state.NextExecution || !double.IsFinite(run.StartedAt)||!double.IsFinite(run.DueAt)||run.StartedAt<0||run.DueAt<run.StartedAt ||
                   !decisions.TryAdd(run.Execution,new(run.Actor,action,run.StartedAt,run.DueAt,run.Execution,null,run.Intent)))throw new InvalidDataException("Invalid scheduled execution checkpoint.");
            }
        }
        if(state.Ready.Any(id=>!decisions.ContainsKey(id)))throw new InvalidDataException("Checkpoint has stale ready action.");
        foreach(var item in state.Slots)
        {
            var slot=byActor[item.Actor];var actions=slot.Candidates.ToDictionary(action=>action.Id,StringComparer.Ordinal);
            slot.Due=item.Due;slot.Cursor=item.Cursor;slot.Best=item.Best is { } best?actions[best]:null;slot.BestScore=item.BestScore??double.NegativeInfinity;
            slot.Revision=item.Revision;slot.Autonomous=item.Autonomous;slot.Held=item.Held;slot.Needs.RestoreState(item.Needs);
            slot.Planner.RestoreState(item.Planning);
            slot.Failures.Restore(item.FailureMemory);
            slot.Goals.Restore(item.GoalPlanning);
            slot.Selection=item.Selection.Select(id=>actions[id]).ToArray();slot.SelectionTime=item.SelectionTime;
            slot.Running=item.Running is { } running?decisions[running.Execution]:null;
        }
        ready.Clear();foreach(var id in state.Ready)ready.Enqueue(decisions[id]);cursor=state.Cursor;nextExecution=state.NextExecution;Evaluations=state.Evaluations;
        return decisions;
    }

    public DecisionScheduler(JsonElement policy,Func<string,ActionCandidate,bool>? eligibility=null,Func<string,ActionCandidate,bool>? modelEligibility=null)
    {
        eligible=eligibility??((_,_)=>true);
        modelEligible=modelEligibility??((_,_)=>true);
        interval = Positive(policy, "decision_interval_seconds");
        stagger = Positive(policy, "actor_stagger_seconds");
        candidatesPerDecision = PositiveInteger(policy, "candidates_per_decision");
        evaluationsPerSlice = PositiveInteger(policy, "evaluations_per_slice");
        millisecondsPerSlice = Positive(policy, "milliseconds_per_slice");
        contentTimeUnit = Positive(policy, "content_time_unit_seconds");
        drivesPerDecision=policy.Get("drives_per_decision").ValueKind==JsonValueKind.Undefined?candidatesPerDecision:PositiveInteger(policy,"drives_per_decision");
        candidatesPerDrive=policy.Get("candidates_per_drive").ValueKind==JsonValueKind.Undefined?1:PositiveInteger(policy,"candidates_per_drive");
        minimumUtility=policy.Get("minimum_action_utility").Number(double.NegativeInfinity);
    }

    private static double Positive(JsonElement policy, string field)
    {
        var value = policy.Get(field).Number(double.NaN);
        if (!double.IsFinite(value) || value <= 0) throw new InvalidDataException("Positive DV scheduling value required: " + field);
        return value;
    }

    private static int PositiveInteger(JsonElement policy, string field)
    {
        var value = Positive(policy, field);
        if (value != Math.Truncate(value) || value > int.MaxValue) throw new InvalidDataException("Integer budget required: " + field);
        return (int)value;
    }

    public void Register(ActorClient actor, IEnumerable<ActionCandidate> candidates, double now, bool autonomous = true)
    {
        ValidateTime(now);
        var items = candidates.ToArray();
        // The content catalog remains complete. Candidate evaluation is sliced below.
        if (items.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new InvalidDataException("Duplicate action identity: " + actor.Id);
        foreach (var item in items)
            if (item.Id.Length == 0 || !double.IsFinite(item.Duration) || item.Duration <= 0 || !double.IsFinite(item.Score))
                throw new InvalidDataException("Invalid authored action: " + item.Id);
        var due = now + slots.Count * stagger;
        var slot = new Slot(actor, items, now, due) { Autonomous = autonomous };
        foreach(var item in items)slot.Needs.Validate(item);
        slot.Index=new(items,slot.Needs);
        slot.Failures=new(actor,items,contentTimeUnit);
        slot.Goals=new(actor,items,contentTimeUnit);
        slot.Planner=new(actor,items,slot.Needs,action=>slot.Running is null&&!slot.Held&&!actor.IsUpdating&&!actor.IsObserving&&eligible(actor.Id,action),contentTimeUnit,
            (action,time)=>slot.Failures.Penalty(action.Id,time));
        byActor.Add(actor.Id, slot);
        slots.Add(slot);
    }

    public SchedulerSlice Step(double now,Action<Decision>? onSelected=null)
    {
        ValidateTime(now);
        foreach(var item in slots)
        {
            item.Failures.Refresh(now);
            if(item.Needs.Tick(now))
            {
                item.Selection=[];item.Cursor=0;item.Best=null;item.BestScore=double.NegativeInfinity;
            }
        }
        var timer = Stopwatch.StartNew();
        var count = 0;
        var idleVisits = 0;
        // Continue the same fair rotation while useful candidate work remains.
        // A lone due actor may use the remaining fixed evaluation budget instead
        // of requiring a full world step for every candidate. One empty rotation
        // still ends the slice, and both the count and wall-clock budgets apply.
        while (slots.Count > 0 && count < Math.Min(evaluationsPerSlice, candidatesPerDecision) && idleVisits < slots.Count && timer.Elapsed.TotalMilliseconds < millisecondsPerSlice)
        {
            var slot = slots[cursor];
            cursor = (cursor + 1) % slots.Count;
            if (slot.Held || !slot.Autonomous || slot.Running is not null || slot.Due > now || slot.Actor.IsUpdating || slot.Actor.IsObserving)
            {idleVisits++;continue;}
            FieldKey[] changed=[];
            if (slot.Revision != slot.Actor.KnowledgeRevision)
            {
                changed=slot.Actor.View().Knowledge.Where(pair=>pair.Value.Revision>slot.Revision).Select(pair=>pair.Key).ToArray();
                slot.Cursor = 0;
                slot.Best = null;
                slot.BestScore = double.NegativeInfinity;
                slot.Revision = slot.Actor.KnowledgeRevision;
                slot.Selection=[];
            }
            if(slot.Selection.Length==0)
            {
                slot.SelectionTime=now;
                var frontier=slot.Planner.Frontier(now);
                var daily=slot.Index.Select(slot.Needs,now,candidatesPerDecision,drivesPerDecision,candidatesPerDrive,changed);
                slot.Goals.Evaluate(now,action=>eligible(slot.Actor.Id,action),action=>modelEligible(slot.Actor.Id,action),slot.Failures);
                var purposeful=slot.Goals.FirstAction is { } first?slot.Candidates.Where(action=>action.Id==first):[];
                slot.Selection=frontier.Concat(purposeful).Concat(daily).DistinctBy(action=>action.Id)
                    .Where(action=>action.Recognition.Evaluate(slot.Actor.Recall)==Truth.True).Take(candidatesPerDecision).ToArray();
            }
            if (slot.Cursor < slot.Selection.Length)
            {
                idleVisits=0;
                var candidate = slot.Selection[slot.Cursor++];
                count++;
                Evaluations++;
                if (!candidate.Declaration.Get("manual_only").Flag() && !candidate.Declaration.Get("physiology_route_only").Flag() && eligible(slot.Actor.Id,candidate) && candidate.Recognition.Evaluate(slot.Actor.Recall) == Truth.True)
                {
                    var score = Score(slot,candidate,slot.SelectionTime);
                    if (score > slot.BestScore) { slot.Best = candidate; slot.BestScore = score; }
                }
            }
            else idleVisits++;
            if (slot.Cursor < slot.Selection.Length) continue;
            var planned=slot.Planner.Choose(slot.Selection,slot.Best,slot.SelectionTime);
            if (planned is { } best && (slot.Planner.DirectedChoice||slot.BestScore>minimumUtility) && eligible(slot.Actor.Id,best))
            {
                var options=slot.Selection.Where(c=>!c.Declaration.Get("manual_only").Flag()&&eligible(slot.Actor.Id,c)&&c.Recognition.Evaluate(slot.Actor.Recall)==Truth.True)
                    .Select(c=>new DecisionOption(c.Id,Score(slot,c,slot.SelectionTime),slot.Needs.Evaluate(c,slot.SelectionTime),slot.Failures.Penalty(c.Id,slot.SelectionTime),slot.Goals.Score(c.Id),c.Gate.Evaluate(slot.Actor.Recall).ToString())).ToArray();
                var intent=slot.Planner.DirectedChoice?slot.Planner.Intent(best.Id):slot.Goals.Intent(best.Id)??slot.Planner.Intent(best.Id);
                var decision = new Decision(slot.Actor.Id, best, now, now + best.Duration,++nextExecution,options,intent);
                slot.Running = decision;
                // The session can reserve the chosen action immediately. Later actors
                // in this bounded pass then see the occupied slot in their eligibility
                // check instead of all choosing the same apparently free resource.
                if(onSelected is null)ready.Enqueue(decision);
                else onSelected(decision);
            }
            slot.Best = null;
            slot.BestScore = double.NegativeInfinity;
            slot.Cursor = 0;
            slot.Selection=[];
            // A long clock jump does not create a backlog of missed decisions.
            slot.Due = now + interval;
        }
        return new(count, slots.Any(x => !x.Held && x.Autonomous && x.Running is null && !x.Actor.IsUpdating && !x.Actor.IsObserving && x.Due <= now));
    }

    public bool TryTake(out Decision? decision)
    {
        var count=ready.Count;
        while(count-->0 && ready.TryDequeue(out decision))
        {
            if(!Owns(decision))continue;
            if(byActor[decision.Actor].Held){ready.Enqueue(decision);continue;}
            return true;
        }
        decision=null;return false;
    }

    public bool Owns(Decision decision) => byActor.TryGetValue(decision.Actor, out var slot) && ReferenceEquals(slot.Running, decision);

    public double NextDecisionTime => slots.Where(x => !x.Held && x.Autonomous && x.Running is null && !x.Actor.IsUpdating && !x.Actor.IsObserving)
        .Select(x => x.Due).DefaultIfEmpty(double.PositiveInfinity).Min();

    public Decision Submit(string actor, string action, double now, bool enqueue = true)
    {
        ValidateTime(now);
        var slot = byActor[actor];
        if (slot.Held || slot.Running is not null || slot.Actor.IsUpdating || slot.Actor.IsObserving)
            throw new InvalidOperationException("Actor is occupied.");
        var candidate = slot.Candidates.SingleOrDefault(x => x.Id == action)
            ?? throw new InvalidOperationException("Unknown actor action.");
        if (!eligible(actor,candidate)) throw new InvalidOperationException("Action is unavailable at the actor's current position.");
        if (candidate.Recognition.Evaluate(slot.Actor.Recall) != Truth.True) throw new InvalidOperationException("Action is unavailable in actor knowledge.");
        slot.Planner.ManualChoice(action,now);slot.Planner.Frontier(now);
        var decision = new Decision(actor, candidate, now, now + candidate.Duration,++nextExecution,null,slot.Planner.Intent(candidate.Id,true));
        slot.Running = decision;
        slot.Cursor = 0;
        slot.Best = null;
        slot.BestScore = double.NegativeInfinity;
        slot.Selection=[];
        if (enqueue) ready.Enqueue(decision);
        return decision;
    }

    public void Complete(Decision decision, double now, bool succeeded = true, bool actualHostElapsed = false)
    {
        ValidateTime(now);
        var slot = byActor[decision.Actor];
        if (!ReferenceEquals(slot.Running, decision)) throw new InvalidOperationException("Stale action completion.");
        if (!actualHostElapsed && now < decision.DueAt) throw new InvalidOperationException("Action duration has not elapsed.");
        if (succeeded){slot.Needs.Complete(decision.Action, now);slot.Planner.Completed(decision,now);}
        slot.Running = null;
        slot.Due = now + interval;
    }

    public bool Cancel(Decision decision,double now)
    {
        ValidateTime(now);
        if(!Owns(decision))return false;
        var slot=byActor[decision.Actor];
        slot.Running=null;slot.Best=null;slot.Selection=[];slot.Cursor=0;
        slot.BestScore=double.NegativeInfinity;slot.Due=now+interval;
        return true;
    }

    public IReadOnlyDictionary<string,double> Needs(string actor,double now)=>byActor[actor].Needs.Snapshot(now);
    public IEnumerable<(string Actor,AffectEvent Event)> DrainAffectEvents()=>slots.SelectMany(slot=>slot.Needs.DrainEvents().Select(item=>(slot.Actor.Id,item)));
    public object Affect(string actor)=>new {emotion=byActor[actor].Needs.Emotion,buffs=byActor[actor].Needs.Buffs};
    public object Paths(string actor,double now)=>byActor[actor].Planner.View(now,byActor[actor].Autonomous);
    private double Score(Slot slot,ActionCandidate action,double now)=>slot.Needs.Score(action,now)+slot.Actor.GoalScore(action,now/contentTimeUnit)+slot.Goals.Score(action.Id)-slot.Failures.Penalty(action.Id,now);
    public object GoalAudit(string actor)=>byActor[actor].Goals.Audit();
    public object FailureAudit(string actor,double now)=>byActor[actor].Failures.Audit(now);
    public ActionFailureCheckpoint? RecordOutcome(Decision decision,bool success,string reason,IEnumerable<FieldKey> basis,double now,string[]? blockedResources=null)
    {
        var slot=byActor[decision.Actor];
        return slot.Failures.Record(decision.Action,success,reason,basis,now,slot.Needs.Score(decision.Action,now)+slot.Actor.GoalScore(decision.Action,now/contentTimeUnit)+slot.Goals.Score(decision.Action.Id),blockedResources);
    }
    public void Hold(string actor,bool held)=>byActor[actor].Held=held;
    public bool Available(string actor,string action)
    {
        var slot=byActor[actor];
        if(slot.Held || slot.Running is not null || slot.Actor.IsUpdating || slot.Actor.IsObserving)return false;
        var candidate=slot.Candidates.Single(candidate=>candidate.Id==action);
        return eligible(actor,candidate)&&candidate.Recognition.Evaluate(slot.Actor.Recall)==Truth.True;
    }
    public ActionCandidate Candidate(string actor,string action)=>byActor[actor].Candidates.Single(candidate=>candidate.Id==action);
    public DriveEvaluation EvaluateDrive(string actor,string action,double now)
    {
        var slot=byActor[actor];
        return slot.Needs.Evaluate(slot.Candidates.Single(candidate=>candidate.Id==action),now);
    }

    public void ReconfigureDrives(string actor,JsonElement policy,double now)
    {
        var slot=byActor[actor];
        var candidate=new DriveCalculator(policy,now);
        foreach(var action in slot.Candidates)candidate.Validate(action);
        slot.Needs.Reconfigure(policy,now);slot.Index=new(slot.Candidates,slot.Needs);
        slot.Selection=[];slot.Cursor=0;slot.Best=null;slot.BestScore=double.NegativeInfinity;
    }

    private static void ValidateTime(double now)
    {
        if (!double.IsFinite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
    }
}
}

// --- Source: Decisions.cs ---
namespace DuelView.Core
{


/// <summary>Planning / Decisions: native Core mechanisms.</summary>
public static partial class DVAutonomyKernel
{
    public static object F_dvs_cached_model(object v_actor, object v_hypothesis)
    {
        object v_model__16342 = F_dv_find(F_dv_find(v_actor, "simulations", new System.Collections.Generic.List<object>(0) { }), F_dv_get(v_hypothesis, "id"), new System.Collections.Generic.List<object>(2) { F_dv_pair("fit", R.number("0")), F_dv_pair("status", "not-simulated") });
        if (R.Bool(F_dv_or(R.eq(F_dv_find(v_model__16342, "source_revision", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_and(F_dv_explanation_model_current(F_dv_get(v_actor, "mind"), v_model__16342), F_dv_same(F_dv_find(v_model__16342, "source_response_models", false), F_dv_find(v_actor, "response_models", new System.Collections.Generic.List<object>(0) { }))))))
        {
            return F_dv_with(v_model__16342, "validated_by_inputs", true);
        }
        else
        {
            if (R.Bool(F_dv_and(F_dvs_argument_valid(v_actor), F_dvs_argument_inputs_current(v_actor))))
            {
                object v_argument__16846 = F_dv_get(v_actor, "argument");
                if (R.Bool(F_dv_and(R.eq(F_dv_find(v_model__16342, "source_revision", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_argument__16846, "source_revision")), F_dv_or(R.eq(F_dv_find(v_model__16342, "event", ""), F_dv_get(v_argument__16846, "model_event")), F_dv_has(F_dv_get(v_argument__16846, "competitor_events"), F_dv_find(v_model__16342, "event", ""))))))
                {
                    return F_dv_with(v_model__16342, "validated_by_argument", F_dv_get(v_argument__16846, "id"));
                }
                else
                {
                    return new System.Collections.Generic.List<object>(3) { F_dv_pair("fit", R.number("0")), F_dv_pair("status", "stale-or-unavailable"), F_dv_pair("source_revision", F_dv_find(v_model__16342, "source_revision", R.sub(R.number("0"), R.number("1")))) };
                }
            }
            else
            {
                return new System.Collections.Generic.List<object>(3) { F_dv_pair("fit", R.number("0")), F_dv_pair("status", "stale-or-unavailable"), F_dv_pair("source_revision", F_dv_find(v_model__16342, "source_revision", R.sub(R.number("0"), R.number("1")))) };
            }
        }
    }

    public static object F_dvs_working_explanation(object v_actor, object v_hypothesis)
    {
        if (R.Bool(F_dvs_factor_candidate_rejected(v_actor, v_hypothesis)))
        {
            return false;
        }
        return F_dv_and(F_dvp_topic(v_actor, v_hypothesis), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_hypothesis, "features", new System.Collections.Generic.List<object>(0) { }))), F_dv_and(R.lt(R.number("0"), R.count(F_dvs_sim_plan(v_hypothesis))), F_dv_and(R.lt(R.number("0"), R.count(F_dv_find(v_hypothesis, "basis", new System.Collections.Generic.List<object>(0) { }))), F_dv_and(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_find(v_hypothesis, "available_when", F_dv_true())), "true"), R.not(R.eq(F_dv_truth(F_dv_get(v_actor, "mind"), F_dv_get(v_hypothesis, "proposition")), "false")))))));
    }

    public static object F_dvs_obligation_actions(object v_actions)
    {
        object v_key__17992 = new System.Collections.Generic.List<object>(1) { "dvs-obligation-actions-v1" };
        object v_cached__18036 = F_dvs_declaration_cache_get(v_actions, v_key__17992);
        if (R.Bool(R.at(v_cached__18036, R.number("0"))))
        {
            return R.at(v_cached__18036, R.number("1"));
        }
        object v_result__18136 = new System.Collections.Generic.List<object>(0) { };
        object v_i__18156 = R.number("0");
        while (R.Bool(R.lt(v_i__18156, R.count(v_actions))))
        {
            if (R.Bool(F_dv_find(R.at(v_actions, v_i__18156), "obligation", false)))
            {
                _ = R.push(v_result__18136, R.at(v_actions, v_i__18156));
            }
            v_i__18156 = R.add(v_i__18156, R.number("1"));
        }
        return F_dvs_declaration_cache_put(v_actions, v_key__17992, v_result__18136);
    }

    public static object F_dvs_scan_obligations(object v_actor, object v_now, object v_costs)
    {
        object v_actions__18418 = F_dvs_obligation_actions(F_dv_get(v_actor, "actions"));
        object v_best__18474 = false;
        object v_earliest__18495 = R.add(v_now, R.number("1"));
        object v_considered__18522 = R.number("0");
        object v_admitted__18545 = R.number("0");
        object v_i__18566 = R.number("0");
        while (R.Bool(R.lt(v_i__18566, R.count(v_actions__18418))))
        {
            object v_action__18617 = R.at(v_actions__18418, v_i__18566);
            object v_due__18649 = F_dv_find(v_action__18617, "obligation_due", R.number("0"));
            if (R.Bool((R.Bool(R.not(R.lt(v_now, v_due__18649))) ? (object)(R.lt(v_due__18649, v_earliest__18495)) : (object)(false))))
            {
                v_considered__18522 = R.add(v_considered__18522, R.number("1"));
                object v_relevant__18790 = F_dvs_sim_refs(F_dv_get(v_action__18617, "precondition"));
                if (R.Bool(F_dvp_eligible(v_actor, v_action__18617, F_dv_get(v_actor, "mind"), v_relevant__18790)))
                {
                    v_admitted__18545 = R.add(v_admitted__18545, R.number("1"));
                    object v_model__18961 = F_dv_get(v_costs, F_dv_get(v_action__18617, "kind"));
                    if (R.Bool(F_dv_action_cost_le(F_dv_get(v_model__18961, "maximum"), F_dv_get(F_dv_get(v_actor, "ledger"), "balances"))))
                    {
                        if (R.Bool((R.Bool(R.not(F_dv_has_key(F_dv_get(v_actor, "policy"), "remaining_game_time"))) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(F_dv_get(v_actor, "policy"), "remaining_game_time"), F_dv_get(F_dv_get(v_model__18961, "maximum"), "time")))))))
                        {
                            v_best__18474 = v_action__18617;
                            v_earliest__18495 = v_due__18649;
                        }
                    }
                }
            }
            v_i__18566 = R.add(v_i__18566, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("action", v_best__18474), F_dv_pair("considered", v_considered__18522), F_dv_pair("admitted", v_admitted__18545) };
    }

    public static object F_dvs_due_obligation(object v_actor, object v_now, object v_costs)
    {
        return F_dv_get(F_dvs_scan_obligations(v_actor, v_now, v_costs), "action");
    }

    public static object F_dvs_obligation_choice(object v_actor, object v_costs)
    {
        object v_scanned__19527 = F_dvs_scan_obligations(v_actor, F_dv_find(v_actor, "decision_time", F_dv_get(F_dv_get(v_actor, "mind"), "time")), v_costs);
        object v_action__19630 = F_dv_get(v_scanned__19527, "action");
        if (R.Bool(R.eq(v_action__19630, false)))
        {
            return false;
        }
        object v_refs__19707 = F_dvs_sim_refs(F_dv_get(v_action__19630, "precondition"));
        object v_reason__19756 = "declared-obligation-due";
        object v_work__19799 = new System.Collections.Generic.List<object>(13) { F_dv_pair("cache_hit", false), F_dv_pair("candidates_considered", F_dv_get(v_scanned__19527, "considered")), F_dv_pair("admission_checks", F_dv_get(v_scanned__19527, "considered")), F_dv_pair("eligible_evaluations", F_dv_get(v_scanned__19527, "admitted")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("forecast_reuses", R.number("0")), F_dv_pair("expanded_nodes", R.number("0")), F_dv_pair("candidate_paths", R.number("1")), F_dv_pair("skipped_expansion", true), F_dv_pair("stop_reason", v_reason__19756), F_dv_pair("candidate_order", "declared-obligation"), F_dv_pair("candidate_order_scans", R.number("0")), F_dv_pair("candidate_order_reuses", R.number("0")) };
        object v_plan__20192 = new System.Collections.Generic.List<object>(6) { F_dv_pair("path", new System.Collections.Generic.List<object>(1) { F_dv_get(v_action__19630, "id") }), F_dv_pair("score", R.number("0")), F_dv_pair("relevant_refs", v_refs__19707), F_dv_pair("score_scope", "declared-obligation"), F_dv_pair("mind_revision", F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_pair("evaluated_at_game_time", F_dv_find(v_actor, "decision_time", F_dv_get(F_dv_get(v_actor, "mind"), "time"))) };
        return new System.Collections.Generic.List<object>(12) { F_dv_pair("action", v_action__19630), F_dv_pair("score", R.number("0")), F_dv_pair("plan_expansions", R.number("0")), F_dv_pair("plans", new System.Collections.Generic.List<object>(1) { v_plan__20192 }), F_dv_pair("selected_path", new System.Collections.Generic.List<object>(1) { F_dv_get(v_action__19630, "id") }), F_dv_pair("relevant_refs", v_refs__19707), F_dv_pair("stop", v_reason__19756), F_dv_pair("strategy", "declared-obligation-v1"), F_dv_pair("approximation", false), F_dv_pair("time_blocked", R.number("0")), F_dv_pair("dependency_expansions", R.number("0")), F_dv_pair("planning_work", v_work__19799) };
    }

    public static object F_dvs_choose(object v_actor, object v_bindings, object v_costs)
    {
        // Selection is a pure query of this decision's owned inputs. Freeze a
        // read view once so record/list reads do not acquire writable branches.
        v_actor = R.snapshot(v_actor);
        v_bindings = R.snapshot(v_bindings);
        v_costs = R.snapshot(v_costs);
        if (R.Bool(R.eq(F_dv_find(v_actor, "simulation_priority_action", ""), "")))
        {
            object v_duty__20882 = F_dvs_obligation_choice(v_actor, v_costs);
            if (R.Bool(R.not(R.eq(v_duty__20882, false))))
            {
                return F_dv_with_many(v_duty__20882, new System.Collections.Generic.List<object>(3) { F_dv_pair("strategy", "approximate-v1"), F_dv_pair("approximation", true), F_dv_pair("stop", "current-owned-obligation") });
            }
        }
        object v_relevant__21088;
        _ = R.phase("decision-goalrefs-start", "");
        try { v_relevant__21088 = ReadGoalRefs(v_actor); }
        finally { _ = R.phase("decision-goalrefs-end", ""); }
        object v_selected__21128 = false;
        object v_best__21153 = R.number("0");
        object v_plans__21170 = new System.Collections.Generic.List<object>(0) { };
        object v_evaluated__21189 = R.number("0");
        object v_considered__21211 = R.number("0");
        object v_time_blocked__21234 = R.number("0");
        object v_priority__21259 = F_dv_find(v_actor, "simulation_priority_action", "");
        object v_time__21327 = F_dv_find(v_actor, "decision_time", F_dv_get(F_dv_get(v_actor, "mind"), "time"));
        object v_i__21391 = R.number("0");
        object decisionActions = F_dvs_navigation_task_candidates(v_actor);
        if (R.Bool(R.eq(v_priority__21259, "")))
            decisionActions = F_dvs_conversation_candidates(v_actor, decisionActions, v_costs);
        while (R.Bool(R.lt(v_i__21391, R.count(decisionActions))))
        {
            object v_action__21448 = R.at(decisionActions, v_i__21391);
            object eligibilityAction = R.Bool(F_dv_has_key(v_action__21448, "base_action_id")) && R.Bool(F_dv_has_key(v_action__21448, "navigation_task"))
                ? F_dv_with(v_action__21448, "id", F_dv_get(v_action__21448, "base_action_id")) : v_action__21448;
            v_considered__21211 = R.add(v_considered__21211, R.number("1"));
            if (R.Bool(F_dvp_eligible(v_actor, eligibilityAction, F_dv_get(v_actor, "mind"), v_relevant__21088)))
            {
                object v_model__21588 = F_dv_get(v_costs, F_dv_get(v_action__21448, "kind"));
                if (R.Bool(R.lt(F_dv_find(F_dv_get(v_actor, "policy"), "remaining_game_time", F_dv_get(F_dv_get(v_model__21588, "maximum"), "time")), F_dv_get(F_dv_get(v_model__21588, "maximum"), "time"))))
                {
                    v_time_blocked__21234 = R.add(v_time_blocked__21234, R.number("1"));
                }
                else
                {
                    if (R.Bool(F_dv_action_cost_le(F_dv_get(v_model__21588, "maximum"), F_dv_get(F_dv_get(v_actor, "ledger"), "balances"))))
                    {
                        v_evaluated__21189 = R.add(v_evaluated__21189, R.number("1"));
                        object v_scored__21937;
                        _ = R.phase("decision-score-start", "");
                        try { v_scored__21937 = F_dvs_current_action_score(v_actor, eligibilityAction, v_bindings, v_relevant__21088, F_dv_get(v_model__21588, "actual")); }
                        finally { _ = R.phase("decision-score-end", ""); }
                        object v_score__22044 = F_dv_get(v_scored__21937, "total");
                        _ = R.push(v_plans__21170, new System.Collections.Generic.List<object>(7) { F_dv_pair("path", new System.Collections.Generic.List<object>(1) { F_dv_get(v_action__21448, "id") }), F_dv_pair("score", v_score__22044), F_dv_pair("value", new System.Collections.Generic.List<object>(2) { F_dv_pair("value", F_dv_get(v_scored__21937, "action_value")), F_dv_pair("current_score", v_scored__21937) }), F_dv_pair("mind_revision", F_dv_get(F_dv_get(v_actor, "mind"), "revision")), F_dv_pair("evaluated_at_game_time", v_time__21327), F_dv_pair("score_scope", "current-owned-utility"), F_dv_pair("prediction_basis", new System.Collections.Generic.List<object>(0) { }) });
                        bool replaceChoice = R.Bool(R.lt(v_best__21153, v_score__22044)) ||
                            (!R.Bool(R.eq(v_priority__21259, "")) && R.Bool(R.eq(F_dv_get(v_action__21448, "id"), v_priority__21259)));
                        object commitment = F_dv_find(v_actor, "navigation_commitment", false);
                        object candidateTask = F_dv_find(v_action__21448, "navigation_task", false);
                        object selectedTask = R.Bool(R.eq(v_selected__21128, false)) ? false : F_dv_find(v_selected__21128, "navigation_task", false);
                        if (!R.Bool(R.eq(commitment, false)))
                        {
                            object held = F_dv_get(commitment, "task");
                            bool candidateHeld = !R.Bool(R.eq(candidateTask, false)) &&
                                R.Bool(F_dv_same(F_dv_get(held, "task_id"), F_dv_get(candidateTask, "task_id"))) &&
                                R.Bool(F_dv_same(F_dv_get(held, "phase"), F_dv_get(candidateTask, "phase")));
                            bool selectedHeld = !R.Bool(R.eq(selectedTask, false)) &&
                                R.Bool(F_dv_same(F_dv_get(held, "task_id"), F_dv_get(selectedTask, "task_id"))) &&
                                R.Bool(F_dv_same(F_dv_get(held, "phase"), F_dv_get(selectedTask, "phase")));
                            // Candidate construction already admits only the held task
                            // or a declared higher-priority preemption. Compare those
                            // symmetrically: scan order must not restore an old task
                            // after an eligible urgent task has already been selected.
                            bool candidateProtected = !R.Bool(R.eq(candidateTask, false));
                            bool selectedProtected = !R.Bool(R.eq(selectedTask, false));
                            if (candidateProtected != selectedProtected)
                                replaceChoice = candidateProtected;
                            else if (candidateProtected && selectedProtected)
                            {
                                object candidatePriority = F_dv_get(candidateTask, "priority");
                                object selectedPriority = F_dv_get(selectedTask, "priority");
                                if (!R.Bool(R.eq(candidatePriority, selectedPriority)))
                                    replaceChoice = R.Bool(R.lt(selectedPriority, candidatePriority));
                                else if (candidateHeld != selectedHeld)
                                    replaceChoice = candidateHeld;
                            }
                        }
                        if (!R.Bool(R.eq(v_priority__21259, "")) && R.Bool(R.eq(F_dv_get(v_action__21448, "id"), v_priority__21259)))
                            replaceChoice = true;
                        if (replaceChoice)
                        {
                            if (R.Bool((R.Bool(R.eq(v_selected__21128, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_selected__21128, "id"), v_priority__21259))))))
                            {
                                v_selected__21128 = v_action__21448;
                                v_best__21153 = v_score__22044;
                            }
                        }
                    }
                }
            }
            v_i__21391 = R.add(v_i__21391, R.number("1"));
        }
        object v_path__22608 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.not(R.eq(v_selected__21128, false))))
        {
            v_path__22608 = new System.Collections.Generic.List<object>(1) { F_dv_get(v_selected__21128, "id") };
        }
        return new System.Collections.Generic.List<object>(12) { F_dv_pair("action", v_selected__21128), F_dv_pair("score", v_best__21153), F_dv_pair("plan_expansions", R.number("0")), F_dv_pair("dependency_expansions", R.number("0")), F_dv_pair("plans", v_plans__21170), F_dv_pair("selected_path", v_path__22608), F_dv_pair("relevant_refs", v_relevant__21088), F_dv_pair("strategy", "approximate-v1"), F_dv_pair("approximation", true), F_dv_pair("stop", "current-candidates-scanned"), F_dv_pair("time_blocked", v_time_blocked__21234), F_dv_pair("planning_work", new System.Collections.Generic.List<object>(7) { F_dv_pair("candidates_considered", v_considered__21211), F_dv_pair("eligible_evaluations", v_evaluated__21189), F_dv_pair("admission_checks", v_considered__21211), F_dv_pair("expanded_nodes", R.number("0")), F_dv_pair("forecasts", R.number("0")), F_dv_pair("skipped_expansion", true), F_dv_pair("stop_reason", "current-candidates-scanned") }) };
    }

    public static object F_dvs_current_appraisals(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_declarations__23254 = F_dv_find(F_dv_get(v_actor, "policy"), "current_appraisals", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(R.eq(R.count(v_declarations__23254), R.number("0"))))
        {
            return v_actor;
        }
        _ = F_dv_domain(F_dv_get(v_actor, "mind"), "mind");
        _ = F_dv_require(R.eq(F_dv_get(F_dv_get(v_actor, "mind"), "subject"), F_dv_get(v_actor, "id")), "Appraisal mind must belong to its evaluator");
        object v_inputs_by_id__23510 = F_dv_find(v_actor, "current_appraisal_inputs", new System.Collections.Generic.List<object>(0) { });
        object v_mind__23580 = F_dv_get(v_actor, "mind");
        object v_cognitive__23606 = F_dv_find(v_actor, "last_cognitive_event", v_parent);
        object v_i__23673 = R.number("0");
        while (R.Bool(R.lt(v_i__23673, R.count(v_declarations__23254))))
        {
            object v_declaration__23729 = R.at(v_declarations__23254, v_i__23673);
            object v_ref__23771 = F_dv_get(v_declaration__23729, "ref");
            _ = F_dv_require((R.Bool(R.eq(F_dv_find(v_ref__23771, "subject", ""), F_dv_get(v_actor, "id"))) ? (object)(R.eq(F_dv_get(v_ref__23771, "facet"), "appraisal")) : (object)(false)), "Current appraisal must be explicitly owned and scoped to appraisal facet");
            _ = F_dv_require(R.lt(R.number("0"), R.count(F_dv_get(v_declaration__23729, "basis"))), "Current appraisal needs a declared personal basis");
            object v_refs__24075 = F_dvs_sim_refs(F_dv_get(v_declaration__23729, "when"));
            object v_inputs__24125 = new System.Collections.Generic.List<object>(0) { };
            object v_j__24149 = R.number("0");
            while (R.Bool(R.lt(v_j__24149, R.count(v_refs__24075))))
            {
                _ = F_dv_require(R.eq(F_dv_find(R.at(v_refs__24075, v_j__24149), "subject", F_dv_get(v_actor, "id")), F_dv_get(v_actor, "id")), "Cross-subject appraisal input");
                _ = F_dv_require(R.not(F_dv_same_local_ref(F_dv_get(v_actor, "id"), R.at(v_refs__24075, v_j__24149), v_ref__23771)), "Current appraisal cannot depend on itself");
                _ = R.push(v_inputs__24125, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", R.at(v_refs__24075, v_j__24149)), F_dv_pair("cell", F_dv_read(v_mind__23580, R.at(v_refs__24075, v_j__24149))) });
                v_j__24149 = R.add(v_j__24149, R.number("1"));
            }
            object v_hypothesis__24523 = F_dv_find(v_actor, "hypothesis", false);
            object v_hypothesis_id__24584 = "";
            if (R.Bool(R.not(R.eq(v_hypothesis__24523, false))))
            {
                v_hypothesis_id__24584 = F_dv_get(v_hypothesis__24523, "id");
            }
            object v_required__24689 = F_dv_find(v_declaration__23729, "hypothesis_id", v_hypothesis_id__24584);
            object v_hypothesis_event__24765 = F_dv_find(v_actor, "hypothesis_event", "");
            object v_signature__24835 = new System.Collections.Generic.List<object>(3) { F_dv_pair("inputs", v_inputs__24125), F_dv_pair("hypothesis_id", v_hypothesis_id__24584), F_dv_pair("declaration", v_declaration__23729) };
            if (R.Bool(R.not(F_dv_same(v_signature__24835, F_dv_find(v_inputs_by_id__23510, F_dv_get(v_declaration__23729, "id"), false)))))
            {
                object v_truth__25023 = F_dv_truth(v_mind__23580, F_dv_get(v_declaration__23729, "when"));
                if (R.Bool(R.not(R.eq(v_hypothesis_id__24584, v_required__24689))))
                {
                    v_truth__25023 = "false";
                }
                object v_next__25154 = F_dv_unknown();
                if (R.Bool(R.eq(v_truth__25023, "true")))
                {
                    v_next__25154 = F_dv_cell(F_dv_get(v_declaration__23729, "value"), R.number("1"), v_time, F_dv_get(v_declaration__23729, "basis"));
                }
                if (R.Bool(R.eq(v_truth__25023, "false")))
                {
                    v_next__25154 = F_dv_cell(F_dv_get(v_declaration__23729, "otherwise"), R.number("1"), v_time, F_dv_get(v_declaration__23729, "basis"));
                }
                v_next__25154 = F_dv_with_many(v_next__25154, new System.Collections.Generic.List<object>(4) { F_dv_pair("time", v_time), F_dv_pair("evaluation_kind", "current-private-appraisal"), F_dv_pair("evaluation_id", F_dv_get(v_declaration__23729, "id")), F_dv_pair("input_signature", v_signature__24835) });
                object v_before__25580 = F_dv_read(v_mind__23580, v_ref__23771);
                object v_changed__25624 = (R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_before__25580, "known"), F_dv_get(v_next__25154, "known")))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_before__25580, "value"), F_dv_get(v_next__25154, "value")))))) ? (object)(true) : (object)(F_dv_find(v_before__25580, "conflicted", false)));
                v_mind__23580 = F_dv_with(F_dv_store(v_mind__23580, v_ref__23771, v_next__25154), "revision", R.add(F_dv_get(v_mind__23580, "revision"), R.number("1")));
                object v_parents__25999 = F_dvs_support_parents(F_dv_get(v_actor, "mind"), v_refs__24075, v_events, v_actor);
                v_parents__25999 = F_dv_explanation_merge(v_parents__25999, new System.Collections.Generic.List<object>(1) { v_cognitive__23606 });
                if (R.Bool(R.not(R.eq(v_hypothesis_event__24765, ""))))
                {
                    v_parents__25999 = F_dv_explanation_merge(v_parents__25999, new System.Collections.Generic.List<object>(1) { v_hypothesis_event__24765 });
                }
                v_cognitive__23606 = F_dvs_event(v_events, "private_appraisal_updated", F_dv_get(v_actor, "id"), v_time, v_parents__25999, new System.Collections.Generic.List<object>(9) { F_dv_pair("id", F_dv_get(v_declaration__23729, "id")), F_dv_pair("ref", v_ref__23771), F_dv_pair("before", v_before__25580), F_dv_pair("after", v_next__25154), F_dv_pair("value_changed", v_changed__25624), F_dv_pair("condition_truth", v_truth__25023), F_dv_pair("inputs", v_inputs__24125), F_dv_pair("hypothesis_event", v_hypothesis_event__24765), F_dv_pair("meaning", "replaceable-personal-evaluation-not-world-fact") });
                v_inputs_by_id__23510 = F_dv_with(v_inputs_by_id__23510, F_dv_get(v_declaration__23729, "id"), v_signature__24835);
            }
            v_i__23673 = R.add(v_i__23673, R.number("1"));
        }
        return F_dv_with_many(v_actor, new System.Collections.Generic.List<object>(3) { F_dv_pair("mind", v_mind__23580), F_dv_pair("current_appraisal_inputs", v_inputs_by_id__23510), F_dv_pair("last_cognitive_event", v_cognitive__23606) });
    }

    public static object F_dvs_reason(object v_actor, object v_events, object v_time, object v_parent)
    {
        object v_refreshed__26847 = F_dvs_refresh_derivations(v_actor, v_events, v_time, v_parent);
        if (R.Bool(F_dv_find(v_refreshed__26847, "cognition_blocked", false)))
        {
            return v_refreshed__26847;
        }
        else
        {
            return F_dvs_reason_run(v_refreshed__26847, v_events, v_time, v_parent);
        }
    }
    public static object F_dvs_plan_cache_get(object owner, object key) => GetCachedValue("plan", owner, key);

    public static object F_dvs_plan_cache_put(object owner, object key, object value) => PutCachedValue("plan", owner, key, value);

    public static object F_dvs_declaration_cache_get(object owner, object key) => GetCachedValue("declaration", owner, key);

    public static object F_dvs_declaration_cache_put(object owner, object key, object value) => PutCachedValue("declaration", owner, key, value);

    private static object GetCachedValue(string group, object owner, object key) =>
        R.Bool(R.memo_group_enabled(group)) ? R.memo_get(owner, key) : new List<object> { false, false };

    // Only internal pure-query consumers borrow immutable cache results. The
    // public cache operations retain their independent writable-copy contract.
    private static object ReadCachedValue(string group, object owner, object key) =>
        R.Bool(R.memo_group_enabled(group)) ? R.memo_read(owner, key) : R.snapshot(new List<object> { false, false });

    private static object PutCachedValue(string group, object owner, object key, object value) =>
        R.Bool(R.memo_group_enabled(group)) ? R.memo_put(owner, key, value) : value;
}
}

// --- Source: Scheduler.cs ---
namespace DuelView.Core
{


/// <summary>Session / Scheduler: native Core mechanisms.</summary>
public static partial class DVAutonomyKernel
{
    public static object F_dvs_scheduled_count(object v_actors)
    {
        object v_count__289712 = R.number("0");
        object v_i__289730 = R.number("0");
        while (R.Bool(R.lt(v_i__289730, R.count(v_actors))))
        {
            if (R.Bool(R.not(R.eq(F_dv_find(R.at(v_actors, v_i__289730), "scheduled_action", false), false))))
            {
                v_count__289712 = R.add(v_count__289712, R.number("1"));
            }
            v_i__289730 = R.add(v_i__289730, R.number("1"));
        }
        return v_count__289712;
    }

    public static object F_dvs_interruptible_wait(object v_action)
    {
        if (R.Bool((R.Bool(R.not(F_dv_find(v_action, "interruptible", false))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_action, "activity", ""), "wait"))))))
        {
            return false;
        }
        object v_kind__290217 = F_dv_get(v_action, "kind");
        return (R.Bool(R.eq(v_kind__290217, "wait")) ? (object)(true) : (object)((R.Bool(R.not(R.lt(R.length(v_kind__290217), R.number("5")))) ? (object)(R.eq(R.slice(v_kind__290217, R.number("0"), R.number("5")), "wait.")) : (object)(false))));
    }

    public static object F_dvs_validate_interruptible_wait(object v_action, object v_model)
    {
        _ = F_dv_require(F_dvs_interruptible_wait(v_action), "Interruptible action must be an explicit wait activity");
        _ = F_dv_require((R.Bool((R.Bool(R.not(F_dv_has_key(v_action, "answer"))) ? (object)(R.not(F_dv_has_key(v_action, "speech"))) : (object)(false))) ? (object)(R.not(F_dv_has_key(v_action, "travel"))) : (object)(false)), "Interruptible wait cannot contain another action");
        _ = F_dv_require((R.Bool(R.eq(F_dv_get(F_dv_get(v_model, "minimum"), "time"), R.number("0"))) ? (object)(R.lt(R.number("0"), F_dv_get(F_dv_get(v_model, "actual"), "time"))) : (object)(false)), "Interruptible wait requires a zero time lower bound and positive duration");
        return true;
    }

    public static object F_dvs_wait_input_events(object v_actor, object v_events, object v_cursor, object v_action, object v_actors)
    {
        v_events = R.snapshot(v_events);
        object v_sources__291064 = new System.Collections.Generic.List<object>(0) { };
        object v_i__291085 = v_cursor;
        while (R.Bool(R.lt(v_i__291085, R.count(v_events))))
        {
            object v_event__291140 = R.at(v_events, v_i__291085);
            if (R.Bool((R.Bool(R.eq(F_dv_get(v_event__291140, "actor"), F_dv_get(v_actor, "id"))) ? (object)(F_dv_has(new System.Collections.Generic.List<object>(5) { "speech_received_pending", "listener_parse", "observation", "co_presence", "room_perception" }, F_dv_get(v_event__291140, "type"))) : (object)(false))))
            {
                object subscription = F_dv_find(v_action, "wait_subscription", false);
                object targets = R.Bool(R.eq(subscription, false)) ? false : F_dv_find(subscription, "contact_targets", false);
                object dangerRefs = R.Bool(R.eq(subscription, false)) ? false : F_dv_find(subscription, "danger_refs", new System.Collections.Generic.List<object>());
                object type = F_dv_get(v_event__291140, "type");
                bool relevant = true;
                if (!R.Bool(R.eq(targets, false)) && (R.Bool(R.eq(type, "co_presence")) || R.Bool(R.eq(type, "room_perception")))) relevant = false;
                if (!R.Bool(R.eq(targets, false)) && R.Bool(R.eq(type, "observation")))
                {
                    object data = F_dv_get(v_event__291140, "data");
                    object observations = R.Bool(R.is_list(data)) && R.Bool(F_dv_record_like(data))
                        ? F_dv_find(data, "observations", new System.Collections.Generic.List<object>()) : false;
                    if (!R.Bool(R.is_list(observations))) observations = new System.Collections.Generic.List<object>();
                    bool anySubscribed = false;
                    for (object j = R.number("0"); R.Bool(R.lt(j, R.count(observations))); j = R.add(j, R.number("1")))
                    {
                        object observation = R.at(observations, j);
                        if (!R.Bool(R.is_list(observation)) || !R.Bool(F_dv_record_like(observation))) continue;
                        object reference = F_dv_find(observation, "ref", false);
                        if (!R.Bool(R.is_list(reference)) || !R.Bool(F_dv_record_like(reference)) ||
                            !R.Bool(F_dv_has_key(reference, "type")) || !R.Bool(F_dv_has_key(reference, "object")) ||
                            !R.Bool(F_dv_has_key(reference, "facet")) || !R.Bool(F_dv_has_key(reference, "field"))) continue;
                        object entity = F_dv_get(reference, "object");
                        if (!R.Bool(R.eq(dangerRefs, false)))
                        {
                            for (object d = R.number("0"); R.Bool(R.lt(d, R.count(dangerRefs))); d = R.add(d, R.number("1")))
                            {
                                if (R.Bool(F_dv_same_local_ref(F_dv_get(v_actor, "id"), reference, R.at(dangerRefs, d))))
                                {
                                    anySubscribed = true;
                                    break;
                                }
                            }
                        }
                        if (anySubscribed) break;
                        if (R.Bool(F_dv_has(targets, entity)))
                        {
                            anySubscribed = true;
                            break;
                        }
                    }
                    if (!anySubscribed) relevant = false;
                }
                if (relevant) _ = R.push(v_sources__291064, F_dv_get(v_event__291140, "id"));
            }
            v_i__291085 = R.add(v_i__291085, R.number("1"));
        }
        return v_sources__291064;
    }

    // A declared peer subscription narrows contact interruptions without suppressing
    // observations themselves. Object perception and received speech stay independent.
    public static object F_dvs_wait_contact_changed(object action, object before, object after)
    {
        object subscription = F_dv_find(action, "wait_subscription", false);
        object targets = R.Bool(R.eq(subscription, false)) ? false : F_dv_find(subscription, "contact_targets", false);
        if (R.Bool(R.eq(targets, false))) return R.not(F_dv_same(before, after));
        if (R.Bool(R.eq(before, false)) || R.Bool(R.eq(after, false))) return R.not(F_dv_same(before, after));
        if (!R.Bool(F_dv_same(F_dv_get(before, "position"), F_dv_get(after, "position")))) return true;
        object oldContext = F_dv_get(before, "context");
        object newContext = F_dv_get(after, "context");
        for (object i = R.number("0"); R.Bool(R.lt(i, R.count(targets))); i = R.add(i, R.number("1")))
        {
            object id = R.at(targets, i);
            if (!R.Bool(F_dv_same(F_dv_find(oldContext, id, false), F_dv_find(newContext, id, false)))) return true;
        }
        return false;
    }

    public static object F_dvs_interrupt_waits(object v_state)
    {
        v_state = R.snapshot(v_state);
        object v_actors__291460 = F_dv_get(v_state, "actors");
        object v_events__291490 = false;
        object v_changed__291513 = false;
        object v_time__291537 = F_dv_get(F_dv_get(v_state, "physical"), "time");
        object v_i__291572 = R.number("0");
        while (R.Bool(R.lt(v_i__291572, R.count(v_actors__291460))))
        {
            object v_actor__291622 = R.at(v_actors__291460, v_i__291572);
            object v_original__291652 = F_dv_find(v_actor__291622, "scheduled_action", false);
            if (R.Bool(R.not(R.eq(v_original__291652, false))))
            {
                if (R.Bool((R.Bool((R.Bool(R.lt(v_time__291537, F_dv_get(v_original__291652, "due"))) ? (object)(F_dvs_interruptible_wait(F_dv_get(v_original__291652, "action"))) : (object)(false))) ? (object)(F_dv_has_key(v_original__291652, "wait_watch")) : (object)(false))))
                {
                    object v_queued__291874 = v_original__291652;
                    object v_watch__291912 = F_dv_get(v_queued__291874, "wait_watch");
                    object v_source_events__291958 = F_dv_get(v_state, "events");
                    if (R.Bool(R.not(R.eq(v_events__291490, false))))
                    {
                        v_source_events__291958 = v_events__291490;
                    }
                    object v_sources__292086 = F_dvs_wait_input_events(v_actor__291622, v_source_events__291958, F_dv_get(v_watch__291912, "event_cursor"), F_dv_get(v_queued__291874, "action"), v_actors__291460);
                    object v_heard__292180 = F_dv_find(v_actor__291622, "pending_heard", new System.Collections.Generic.List<object>(0) { });
                    object v_h__292244 = R.number("0");
                    while (R.Bool(R.lt(v_h__292244, R.count(v_heard__292180))))
                    {
                        if (R.Bool(R.not(F_dv_has(v_sources__292086, F_dv_get(R.at(v_heard__292180, v_h__292244), "source")))))
                        {
                            _ = R.push(v_sources__292086, F_dv_get(R.at(v_heard__292180, v_h__292244), "source"));
                        }
                        v_h__292244 = R.add(v_h__292244, R.number("1"));
                    }
                    object v_contact__292462 = F_dvs_idle_contact_input(v_actor__291622, F_dv_get(v_state, "physical"), v_actors__291460);
                    object v_contact_changed = R.not(F_dv_same(v_contact__292462, F_dv_get(v_watch__291912, "contact")));
                    object v_visible_change__292546 = F_dvs_wait_contact_changed(F_dv_get(v_queued__291874, "action"), F_dv_get(v_watch__291912, "contact"), v_contact__292462);
                    if (R.Bool(v_contact_changed))
                    {
                        if (R.Bool(R.eq(v_events__291490, false)))
                        {
                            v_events__291490 = F_dv_clone(F_dv_get(v_state, "events"));
                        }
                        object v_start__292754 = R.count(v_events__291490);
                        v_actor__291622 = F_dvs_notice_peers(v_actor__291622, F_dv_get(v_state, "physical"), v_actors__291460, F_dv_get(v_state, "bindings"), v_events__291490, v_time__291537, F_dv_get(v_queued__291874, "parent"));
                        v_actor__291622 = F_dvs_notice_visible_objects(v_actor__291622, F_dv_get(v_state, "physical"), F_dv_get(v_state, "bindings"), v_events__291490, v_time__291537, F_dv_get(v_queued__291874, "parent"));
                        v_sources__292086 = F_dv_explanation_merge(v_sources__292086, F_dvs_wait_input_events(v_actor__291622, v_events__291490, v_start__292754, F_dv_get(v_queued__291874, "action"), v_actors__291460));
                    }
                    if (R.Bool((R.Bool(v_visible_change__292546) ? (object)(true) : (object)(R.lt(R.number("0"), R.count(v_sources__292086))))))
                    {
                        if (R.Bool(R.eq(v_events__291490, false)))
                        {
                            v_events__291490 = F_dv_clone(F_dv_get(v_state, "events"));
                        }
                        object v_elapsed__293308 = R.sub(v_time__291537, F_dv_get(v_queued__291874, "start"));
                        _ = F_dv_require((R.Bool(R.not(R.lt(v_elapsed__293308, R.number("0")))) ? (object)(R.lt(v_elapsed__293308, F_dv_get(F_dv_get(F_dv_get(v_queued__291874, "model"), "actual"), "time"))) : (object)(false)), "Invalid interrupted wait duration");
                        object v_interruption__293483 = new System.Collections.Generic.List<object>(9) { F_dv_pair("reason", "activity-interrupted"), F_dv_pair("failure_class", "activity-interrupted"), F_dv_pair("action_id", F_dv_get(F_dv_get(v_queued__291874, "action"), "id")), F_dv_pair("start", F_dv_get(v_queued__291874, "start")), F_dv_pair("planned_due", F_dv_get(v_queued__291874, "due")), F_dv_pair("time", v_time__291537), F_dv_pair("elapsed", v_elapsed__293308), F_dv_pair("source_events", v_sources__292086), F_dv_pair("visible_change", v_visible_change__292546) };
                        object v_interrupted__293773 = F_dvs_event(v_events__291490, "wait_interrupted", F_dv_get(v_actor__291622, "id"), v_time__291537, F_dv_explanation_merge(new System.Collections.Generic.List<object>(1) { F_dv_get(v_queued__291874, "parent") }, v_sources__292086), v_interruption__293483);
                        object v_model__294086 = F_dv_with(F_dv_get(v_queued__291874, "model"), "actual", F_dv_with(F_dv_get(F_dv_get(v_queued__291874, "model"), "actual"), "time", v_elapsed__293308));
                        v_queued__291874 = F_dv_with_many(v_queued__291874, new System.Collections.Generic.List<object>(4) { F_dv_pair("model", v_model__294086), F_dv_pair("due", v_time__291537), F_dv_pair("parent", v_interrupted__293773), F_dv_pair("activity_interruption", F_dv_with(v_interruption__293483, "event", v_interrupted__293773)) });
                    }
                    else
                    {
                        if (R.Bool(v_contact_changed) || R.Bool(R.not(R.eq(F_dv_get(v_watch__291912, "event_cursor"), R.count(v_source_events__291958)))))
                        {
                            object updatedWatch = F_dv_with(v_watch__291912, "contact", v_contact__292462);
                            updatedWatch = F_dv_with(updatedWatch, "event_cursor", R.Bool(R.eq(v_events__291490, false)) ? R.count(v_source_events__291958) : R.count(v_events__291490));
                            v_queued__291874 = F_dv_with(v_queued__291874, "wait_watch", updatedWatch);
                        }
                    }
                    if (R.Bool(R.not(F_dv_same(v_queued__291874, v_original__291652))))
                    {
                        // Later contact queries must see earlier updates without
                        // acquiring writable branches from the replacement root.
                        v_actors__291460 = R.snapshot(F_dvs_replace(v_actors__291460, v_i__291572, F_dv_with(v_actor__291622, "scheduled_action", v_queued__291874)));
                        v_changed__291513 = true;
                    }
                }
            }
            v_i__291572 = R.add(v_i__291572, R.number("1"));
        }
        if (R.Bool(R.not(v_changed__291513)))
        {
            return v_state;
        }
        object v_updates__294961 = new System.Collections.Generic.List<object>(1) { F_dv_pair("actors", v_actors__291460) };
        if (R.Bool(R.not(R.eq(v_events__291490, false))))
        {
            v_updates__294961 = F_dv_with(v_updates__294961, "events", v_events__291490);
        }
        return F_dvs_owned_state_update(v_state, v_updates__294961);
    }

    public static object F_dvs_interrupted_wait_due(object v_actors, object v_time)
    {
        object v_i__295169 = R.number("0");
        while (R.Bool(R.lt(v_i__295169, R.count(v_actors))))
        {
            object v_queued__295219 = F_dv_find(R.at(v_actors, v_i__295169), "scheduled_action", false);
            if (R.Bool(R.not(R.eq(v_queued__295219, false))))
            {
                if (R.Bool((R.Bool(R.not(R.lt(v_time, F_dv_get(v_queued__295219, "due")))) ? (object)(R.not(R.eq(F_dv_find(v_queued__295219, "activity_interruption", false), false))) : (object)(false))))
                {
                    return v_i__295169;
                }
            }
            v_i__295169 = R.add(v_i__295169, R.number("1"));
        }
        return R.sub(R.number("0"), R.number("1"));
    }

    public static object F_dvs_dispatch_due(object v_state, object v_index)
    {
        v_state = R.snapshot(v_state);
        object v_updated_actor__295530 = F_dvs_erosion_update(R.at(F_dv_get(v_state, "actors"), v_index), F_dv_get(v_state, "physical"), F_dv_get(F_dv_get(v_state, "physical"), "time"));
        if (R.Bool(R.not(F_dvs_actor_can_act(v_updated_actor__295530))))
        {
            object queue = F_dv_find(v_updated_actor__295530, "scheduled_action", false);
            object held = F_dv_find(v_updated_actor__295530, "navigation_commitment", false);
            object v_cancelled__295680 = F_dv_with(v_updated_actor__295530, "scheduled_action", false);
            if (!R.Bool(R.eq(queue, false)) && !R.Bool(R.eq(held, false)))
            {
                object action = F_dv_get(queue, "action");
                object candidate = F_dv_find(action, "navigation_candidate_id", false);
                if (!R.Bool(R.eq(candidate, false)) && R.Bool(R.eq(candidate, F_dv_find(held, "candidate_id", false))))
                {
                    object events = F_dv_clone(F_dv_get(v_state, "events"));
                    object task = F_dv_get(held, "task");
                    _ = F_dvs_event(events, "navigation_task_released", F_dv_get(v_updated_actor__295530, "id"), F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_single(F_dv_get(queue, "parent")),
                        new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("reason", "actor-unable-before-dispatch"), F_dv_pair("candidate_id", candidate), F_dv_pair("action_event", "") });
                    v_cancelled__295680 = F_dv_with(v_cancelled__295680, "navigation_commitment", false);
                    return F_dvs_owned_state_update(v_state, new List<object> { F_dv_pair("actors", F_dvs_replace(F_dv_get(v_state, "actors"), v_index, v_cancelled__295680)), F_dv_pair("events", events), F_dv_pair("cursor", v_index), F_dv_pair("idle", R.number("0")) });
                }
            }
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(3) { F_dv_pair("actors", F_dvs_replace(F_dv_get(v_state, "actors"), v_index, v_cancelled__295680)), F_dv_pair("cursor", v_index), F_dv_pair("idle", R.number("0")) });
        }
        object v_queued__295880 = F_dv_get(v_updated_actor__295530, "scheduled_action");
        object v_time__295928 = F_dv_get(F_dv_get(v_state, "physical"), "time");
        object v_events__296126 = F_dv_clone(F_dv_get(v_state, "events"));
        object v_actor__296166 = F_dvs_drain_received_speech(F_dv_with(v_updated_actor__295530, "scheduled_action", false), v_events__296126, v_time__295928);
        object v_action__296273 = F_dvs_bind_reply(v_actor__296166, F_dv_get(v_queued__295880, "action"));
        if (R.Bool(R.not(F_dvs_argument_action_evidence(v_actor__296166, v_action__296273, v_events__296126))))
        {
            _ = F_dvs_event(v_events__296126, "action_cancelled", F_dv_get(v_actor__296166, "id"), v_time__295928, new System.Collections.Generic.List<object>(1) { F_dv_get(v_queued__295880, "parent") }, new System.Collections.Generic.List<object>(5) { F_dv_pair("action_id", F_dv_get(v_action__296273, "id")), F_dv_pair("reason", "argument-no-longer-current"), F_dv_pair("scheduled_start", F_dv_get(v_queued__295880, "start")), F_dv_pair("due", F_dv_get(v_queued__295880, "due")), F_dv_pair("charged", false) });
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(4) { F_dv_pair("actors", F_dvs_replace(F_dv_get(v_state, "actors"), v_index, v_actor__296166)), F_dv_pair("events", v_events__296126), F_dv_pair("cursor", v_index), F_dv_pair("idle", R.number("0")) });
        }
        object v_preparation_parent__296727 = F_dv_get(v_queued__295880, "parent");
        if (R.Bool(F_dvs_action_requires_argument(v_action__296273)))
        {
            v_action__296273 = F_dv_with_many(v_action__296273, new System.Collections.Generic.List<object>(3) { F_dv_pair("argument_evidence_revision", F_dv_get(F_dv_get(v_actor__296166, "mind"), "revision")), F_dv_pair("argument_evidence_time", v_time__295928), F_dv_pair("argument_evidence_mind_time", F_dv_get(F_dv_get(v_actor__296166, "mind"), "time")) });
            v_preparation_parent__296727 = F_dvs_event(v_events__296126, "argument_evidence_prepared", F_dv_get(v_actor__296166, "id"), v_time__295928, F_dvs_cognitive_parents(v_actor__296166, new System.Collections.Generic.List<object>(2) { F_dv_get(v_queued__295880, "parent"), F_dv_get(F_dv_get(v_actor__296166, "argument"), "id") }), new System.Collections.Generic.List<object>(8) { F_dv_pair("action_id", F_dv_get(v_action__296273, "id")), F_dv_pair("argument_id", F_dv_get(F_dv_get(v_actor__296166, "argument"), "id")), F_dv_pair("argument_evidence_revision", F_dv_get(F_dv_get(v_actor__296166, "mind"), "revision")), F_dv_pair("argument_evidence_time", v_time__295928), F_dv_pair("argument_evidence_mind_time", F_dv_get(F_dv_get(v_actor__296166, "mind"), "time")), F_dv_pair("physical_revision", F_dv_get(F_dv_get(v_state, "physical"), "revision")), F_dv_pair("source_event_ids", F_dv_get(F_dv_get(v_actor__296166, "argument"), "source_event_ids")), F_dv_pair("contract", "prepared-evidence-before-own-completion-observations") });
        }
        object v_bounds__297503 = F_dv_action_cost_bounds(F_dv_get(F_dv_get(v_queued__295880, "model"), "minimum"), F_dv_get(F_dv_get(v_queued__295880, "model"), "maximum"));
        object v_event__297586 = R.cat("action", R.show(R.count(v_events__296126)));
        if (R.Bool(F_dv_has_key(v_action__296273, "navigation_task")))
        {
            v_action__296273 = F_dv_with(v_action__296273, "navigation_action_event", v_event__297586);
            v_actor__296166 = F_dvs_navigation_task_dispatch(v_actor__296166, v_action__296273, v_event__297586);
        }
        object v_quote__297644 = F_dv_with(F_dv_action_cost_quote(F_dv_get(v_actor__296166, "ledger"), F_dv_get(v_state, "physical"), F_dv_find(v_action__296273, "law", F_dv_get(v_action__296273, "id")), v_event__297586, v_bounds__297503), "scheduled_start", F_dv_get(v_queued__295880, "start"));
        object v_interruption__297803 = F_dv_find(v_queued__295880, "activity_interruption", false);
        if (R.Bool(R.not(R.eq(v_interruption__297803, false))))
        {
            v_quote__297644 = F_dv_with(v_quote__297644, "activity_interruption", v_interruption__297803);
        }
        object v_parent__297974 = F_dvs_event(v_events__296126, "cost_quote", F_dv_get(v_actor__296166, "id"), v_time__295928, new System.Collections.Generic.List<object>(1) { v_preparation_parent__296727 }, v_quote__297644);
        object v_pending__298068 = new System.Collections.Generic.List<object>(13) { F_dv_pair("index", v_index), F_dv_pair("actor", v_actor__296166), F_dv_pair("parent", v_parent__297974), F_dv_pair("time", v_time__295928), F_dv_pair("action", v_action__296273), F_dv_pair("id", F_dv_get(v_actor__296166, "id")), F_dv_pair("mind", F_dv_get(v_actor__296166, "mind")), F_dv_pair("model", F_dv_get(v_queued__295880, "model")), F_dv_pair("action_event", v_event__297586), F_dv_pair("ledger", F_dv_get(v_actor__296166, "ledger")), F_dv_pair("quote", v_quote__297644), F_dv_pair("choice", F_dv_get(v_queued__295880, "choice")), F_dv_pair("activity_interruption", v_interruption__297803) };
        if (R.Bool(F_dvs_action_requires_argument(v_action__296273)))
        {
            v_pending__298068 = F_dv_with(v_pending__298068, "argument_evidence_world", F_dv_get(v_state, "physical"));
        }
        return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(4) { F_dv_pair("actors", F_dvs_replace(F_dv_get(v_state, "actors"), v_index, v_actor__296166)), F_dv_pair("events", v_events__296126), F_dv_pair("pending", v_pending__298068), F_dv_pair("idle", R.number("0")) });
    }

    public static object F_dvs_known_clock_boundary(object v_condition, object v_clock, object v_now, object v_limit)
    {
        return F_dv_time_boundary(v_condition, v_clock, v_now, v_limit, "invalidate");
    }

    public static object F_dvs_idle_subject_input(object v_actor, object v_costs)
    {
        v_actor = R.snapshot(v_actor);
        v_costs = R.snapshot(v_costs);
        object v_policy__299346 = F_dv_get(v_actor, "policy");
        object v_maximum__299376 = R.number("0");
        object v_i__299396 = R.number("0");
        while (R.Bool(R.lt(v_i__299396, R.count(v_costs))))
        {
            v_maximum__299376 = F_dv_max(v_maximum__299376, F_dv_get(F_dv_get(R.at(R.at(v_costs, v_i__299396), R.number("1")), "maximum"), "time"));
            v_i__299396 = R.add(v_i__299396, R.number("1"));
        }
        object v_bound__299519 = v_maximum__299376;
        if (R.Bool((R.Bool(F_dv_has_key(v_policy__299346, "remaining_game_time")) ? (object)(R.not(R.lt(F_dv_get(v_policy__299346, "remaining_game_time"), v_bound__299519))) : (object)(false))))
        {
            v_policy__299346 = F_dv_with(v_policy__299346, "remaining_game_time", v_bound__299519);
        }
        object v_result__299696 = new System.Collections.Generic.List<object>(0) { };
        v_i__299396 = R.number("0");
        while (R.Bool(R.lt(v_i__299396, R.count(v_actor))))
        {
            object v_entry__299761 = R.at(v_actor, v_i__299396);
            object v_key__299790 = R.at(v_entry__299761, R.number("0"));
            if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(9) { "idle_guard", "decision_time", "decision_revision", "support_events_cursor", "heard_utterances_cursor", "cognition_reused", "cognition_stop", "reason_reused", "last_reason_report" }, v_key__299790))))
            {
                if (R.Bool(R.eq(v_key__299790, "policy")))
                {
                    _ = R.push(v_result__299696, F_dv_pair(v_key__299790, v_policy__299346));
                }
                else
                {
                    _ = R.push(v_result__299696, v_entry__299761);
                }
            }
            v_i__299396 = R.add(v_i__299396, R.number("1"));
        }
        return v_result__299696;
    }

    public static object F_dvs_idle_story_input(object v_story)
    {
        v_story = R.snapshot(v_story);
        object v_result__300267 = new System.Collections.Generic.List<object>(0) { };
        object v_i__300287 = R.number("0");
        while (R.Bool(R.lt(v_i__300287, R.count(v_story))))
        {
            if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(2) { "background_scan_cursor", "background_waiting_cursor" }, R.at(R.at(v_story, v_i__300287), R.number("0"))))))
            {
                _ = R.push(v_result__300267, R.at(v_story, v_i__300287));
            }
            v_i__300287 = R.add(v_i__300287, R.number("1"));
        }
        return v_result__300267;
    }

    public static object F_dvs_idle_contact_input(object v_actor, object v_physical, object v_actors)
    {
        if (R.Bool((R.Bool(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "notice_co_presence", false))) ? (object)(R.not(F_dv_find(F_dv_get(v_actor, "policy"), "notice_visible_objects", false))) : (object)(false))))
        {
            return false;
        }
        object v_context__300861 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(F_dv_find(F_dv_get(v_actor, "policy"), "notice_co_presence", false)))
        {
            v_context__300861 = F_dvs_visible_context(v_actor, v_physical, v_actors);
        }
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("context", v_context__300861), F_dv_pair("objects", F_dvs_visible_objects(v_actor, v_physical)), F_dv_pair("position", F_dv_read(v_physical, F_dv_get(v_actor, "location_ref"))), F_dv_pair("visible", F_dv_truth(v_physical, F_dv_find(F_dv_get(v_actor, "policy"), "visual_identity_when", F_dv_true()))) };
    }

    public static object F_dvs_idle_can_wait(object v_actor)
    {
        return (R.Bool(R.eq(R.count(F_dv_find(v_actor, "pending_heard", new System.Collections.Generic.List<object>(0) { })), R.number("0"))) ? (object)(R.not(F_dv_find(v_actor, "cognition_blocked", false))) : (object)(false));
    }

    public static object F_dvs_idle_events_stable(object v_events, object v_actor, object v_guard)
    {
        return (R.Bool(R.not(R.lt(R.count(v_events), F_dv_get(v_guard, "event_cursor")))) ? (object)(F_dvs_owned_events_stable(v_events, v_actor, F_dv_get(v_guard, "event_cursor"))) : (object)(false));
    }

    public static object F_dvs_idle_guard_matches(object v_actor, object v_state)
    {
        object v_guard__301782 = F_dv_find(v_actor, "idle_guard", false);
        if (R.Bool((R.Bool((R.Bool(R.eq(v_guard__301782, false)) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_guard__301782, "until")))))) ? (object)(true) : (object)(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_guard__301782, "time"))))))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(F_dvs_idle_can_wait(v_actor))) ? (object)(true) : (object)(R.not(F_dvs_idle_events_stable(F_dv_get(v_state, "events"), v_actor, v_guard__301782))))))
        {
            return false;
        }
        if (R.Bool(R.not(F_dv_same(F_dvs_idle_subject_input(v_actor, F_dv_get(v_state, "costs")), F_dv_get(v_guard__301782, "subject")))))
        {
            return false;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_same(F_dv_get(v_state, "bindings"), F_dv_get(v_guard__301782, "bindings")))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_state, "costs"), F_dv_get(v_guard__301782, "costs")))))) ? (object)(true) : (object)(R.not(F_dv_same(F_dvs_idle_story_input(F_dv_get(v_state, "story")), F_dv_get(v_guard__301782, "story")))))))
        {
            return false;
        }
        return F_dv_same(F_dvs_idle_contact_input(v_actor, F_dv_get(v_state, "physical"), F_dv_get(v_state, "actors")), F_dv_get(v_guard__301782, "contact"));
    }

    public static object F_dvs_idle_inputs_changed(object v_actor, object v_state)
    {
        object v_guard__302488 = F_dv_find(v_actor, "idle_guard", false);
        if (R.Bool(R.eq(v_guard__302488, false)))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.not(F_dv_same(F_dvs_idle_subject_input(v_actor, F_dv_get(v_state, "costs")), F_dv_get(v_guard__302488, "subject")))) ? (object)(true) : (object)(R.not(F_dvs_idle_events_stable(F_dv_get(v_state, "events"), v_actor, v_guard__302488))))))
        {
            return true;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_same(F_dv_get(v_state, "bindings"), F_dv_get(v_guard__302488, "bindings")))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_state, "costs"), F_dv_get(v_guard__302488, "costs")))))) ? (object)(true) : (object)(R.not(F_dv_same(F_dvs_idle_story_input(F_dv_get(v_state, "story")), F_dv_get(v_guard__302488, "story")))))))
        {
            return true;
        }
        return R.not(F_dv_same(F_dvs_idle_contact_input(v_actor, F_dv_get(v_state, "physical"), F_dv_get(v_state, "actors")), F_dv_get(v_guard__302488, "contact")));
    }

    public static object F_dvs_declared_conditions(object v_value)
    {
        if (R.Bool(R.not(R.is_list(v_value))))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        if (R.Bool(F_dv_record_like(v_value)))
        {
            if (R.Bool(F_dv_has_key(v_value, "op")))
            {
                if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(2) { "assign", "increment" }, F_dv_get(v_value, "op")))))
                {
                    return new System.Collections.Generic.List<object>(1) { v_value };
                }
            }
        }
        object v_conditions__303416 = new System.Collections.Generic.List<object>(0) { };
        object v_i__303440 = R.number("0");
        while (R.Bool(R.lt(v_i__303440, R.count(v_value))))
        {
            object v_nested__303489 = F_dvs_declared_conditions(R.at(v_value, v_i__303440));
            object v_j__303544 = R.number("0");
            while (R.Bool(R.lt(v_j__303544, R.count(v_nested__303489))))
            {
                _ = R.push(v_conditions__303416, R.at(v_nested__303489, v_j__303544));
                v_j__303544 = R.add(v_j__303544, R.number("1"));
            }
            v_i__303440 = R.add(v_i__303440, R.number("1"));
        }
        return v_conditions__303416;
    }

    public static object F_dvs_declared_conditions_cached(object v_value) => DeclaredConditionsCached(v_value, false);

    // Idle analysis only reads declarations. Keep its cached subtrees frozen;
    // the public entry point retains the ordinary writable-result contract.
    private static object DeclaredConditionsCached(object v_value, bool readOnly)
    {
        if (R.Bool(R.not(R.is_list(v_value))))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        object v_key__304019 = new System.Collections.Generic.List<object>(1) { "dvs-declared-conditions-subtree-v1" };
        object v_cached__304072 = readOnly ? ReadCachedValue("declaration", v_value, v_key__304019)
            : F_dvs_declaration_cache_get(v_value, v_key__304019);
        if (R.Bool(R.at(v_cached__304072, R.number("0"))))
        {
            return R.at(v_cached__304072, R.number("1"));
        }
        object v_conditions__304170 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(F_dv_record_like(v_value)))
        {
            if (R.Bool(F_dv_has_key(v_value, "op")))
            {
                if (R.Bool(R.not(F_dv_has(new System.Collections.Generic.List<object>(2) { "assign", "increment" }, F_dv_get(v_value, "op")))))
                {
                    var result = F_dvs_declaration_cache_put(v_value, v_key__304019, new System.Collections.Generic.List<object>(1) { v_value });
                    return readOnly ? R.snapshot(result) : result;
                }
            }
        }
        object v_i__304392 = R.number("0");
        while (R.Bool(R.lt(v_i__304392, R.count(v_value))))
        {
            object v_nested__304441 = DeclaredConditionsCached(R.at(v_value, v_i__304392), readOnly);
            object v_j__304503 = R.number("0");
            while (R.Bool(R.lt(v_j__304503, R.count(v_nested__304441))))
            {
                _ = R.push(v_conditions__304170, R.at(v_nested__304441, v_j__304503));
                v_j__304503 = R.add(v_j__304503, R.number("1"));
            }
            v_i__304392 = R.add(v_i__304392, R.number("1"));
        }
        var collected = F_dvs_declaration_cache_put(v_value, v_key__304019, v_conditions__304170);
        return readOnly ? R.snapshot(collected) : collected;
    }

    public static object F_dvs_idle_window_boundary(object v_actor, object v_story, object v_now, object v_limit)
    {
        object v_clock__304753 = F_dv_find(v_story, "clock_ref", false);
        object v_key__304804 = new System.Collections.Generic.List<object>(3) { "dvs_idle_declared_conditions", F_dv_get(v_actor, "policy"), F_dv_get(F_dv_get(v_actor, "mind"), "adoptions") };
        object v_cached__304887 = ReadCachedValue("declaration", F_dv_get(v_actor, "actions"), v_key__304804);
        object v_conditions__304950 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.at(v_cached__304887, R.number("0"))))
        {
            v_conditions__304950 = R.at(v_cached__304887, R.number("1"));
        }
        else
        {
            if (R.Bool(F_dv_find(F_dv_get(v_actor, "policy"), "opt_declcache", false)))
            {
                object v_declarations__305326 = R.snapshot(new System.Collections.Generic.List<object>(3) { F_dv_get(v_actor, "actions"), F_dv_get(v_actor, "policy"), F_dv_get(F_dv_get(v_actor, "mind"), "adoptions") });
                v_conditions__304950 = DeclaredConditionsCached(v_declarations__305326, true);
            }
            else
            {
                v_conditions__304950 = F_dvs_declared_conditions(new System.Collections.Generic.List<object>(3) { F_dv_get(v_actor, "actions"), F_dv_get(v_actor, "policy"), F_dv_get(F_dv_get(v_actor, "mind"), "adoptions") });
            }
            _ = F_dvs_declaration_cache_put(F_dv_get(v_actor, "actions"), v_key__304804, v_conditions__304950);
        }
        // Freeze the new wrapper too: reading a frozen child through a mutable
        // record would otherwise acquire a writable copy of the condition tree.
        object v_boundary__305666 = F_dvs_known_clock_boundary(R.snapshot(F_dv_all(v_conditions__304950)), v_clock__304753, v_now, v_limit);
        object v_duties__305749 = F_dvs_obligation_actions(F_dv_get(v_actor, "actions"));
        object v_duty_index__305804 = R.number("0");
        while (R.Bool(R.lt(v_duty_index__305804, R.count(v_duties__305749))))
        {
            object v_due__305872 = F_dv_find(R.at(v_duties__305749, v_duty_index__305804), "obligation_due", R.number("0"));
            if (R.Bool((R.Bool((R.Bool(R.lt(v_now, v_due__305872)) ? (object)(R.lt(v_due__305872, F_dv_get(v_boundary__305666, "next"))) : (object)(false))) ? (object)(R.not(F_dv_has(F_dv_find(v_actor, "completed_actions", new System.Collections.Generic.List<object>(0) { }), F_dv_get(R.at(v_duties__305749, v_duty_index__305804), "id")))) : (object)(false))))
            {
                v_boundary__305666 = F_dv_with(v_boundary__305666, "next", v_due__305872);
            }
            v_duty_index__305804 = R.add(v_duty_index__305804, R.number("1"));
        }
        if (R.Bool(F_dvs_has_active_communication_goal(v_actor)))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", false), F_dv_pair("next", F_dv_min(F_dv_get(v_boundary__305666, "next"), F_dv_min(v_limit, R.add(v_now, R.number("1"))))) };
        }
        return v_boundary__305666;
    }

    public static object F_dvs_schedule(object v_state)
    {
        var inFlightCount = R.Items(F_dv_get(v_state, "actors")).Count(actor => F_dv_find(actor, "in_flight_action", false) is not false);
        if (inFlightCount > 0 && R.Num(F_dv_get(v_state, "executed")) + inFlightCount >= R.Num(F_dv_get(v_state, "limit")))
            return F_dvs_owned_state_update(v_state, new List<object> { F_dv_pair("host_motion_wait", true) });
        _ = R.phase("schedule-sync-start", "");
        try
        {
            v_state = F_dvs_simulation_sync(v_state);
            v_state = R.snapshot(v_state);
        }
        finally { _ = R.phase("schedule-sync-end", ""); }
        object v_original_actors = F_dv_get(v_state, "actors");
        object v_updated__306553 = v_original_actors;
        object v_erosion_index__306584 = R.number("0");
        object v_any_eroded__306610 = false;
        _ = R.phase("schedule-erosion-start", "");
        try
        {
            while (R.Bool(R.lt(v_erosion_index__306584, R.count(v_updated__306553))))
            {
                object v_original__306686 = R.at(v_original_actors, v_erosion_index__306584);
                object v_eroded__306732 = F_dvs_erosion_update(v_original__306686, F_dv_get(v_state, "physical"), F_dv_get(F_dv_get(v_state, "physical"), "time"));
                if (R.Bool(R.not(F_dv_same(v_eroded__306732, v_original__306686))))
                {
                    v_updated__306553 = F_dvs_replace(v_updated__306553, v_erosion_index__306584, v_eroded__306732);
                    v_any_eroded__306610 = true;
                }
                v_erosion_index__306584 = R.add(v_erosion_index__306584, R.number("1"));
            }
            if (R.Bool(v_any_eroded__306610))
            {
                v_state = R.snapshot(F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("actors", v_updated__306553) }));
            }
        }
        finally { _ = R.phase("schedule-erosion-end", ""); }
        _ = R.phase("schedule-waits-start", "");
        try { v_state = R.snapshot(F_dvs_interrupt_waits(v_state)); }
        finally { _ = R.phase("schedule-waits-end", ""); }
        v_updated__306553 = F_dv_get(v_state, "actors");
        object v_current__307173 = v_state;
        object v_due_index__307197 = R.sub(R.number("0"), R.number("1"));
        object v_due_time__307220 = R.add(F_dv_get(v_state, "horizon"), R.number("1"));
        object v_wake_index__307257 = R.sub(R.number("0"), R.number("1"));
        object v_wake_time__307281 = R.add(F_dv_get(v_state, "horizon"), R.number("1"));
        object v_event_wake_index__307319 = R.sub(R.number("0"), R.number("1"));
        object v_event_wake_time__307349 = R.add(F_dv_get(v_state, "horizon"), R.number("1"));
        if (R.Bool(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation")))
        {
            v_event_wake_time__307349 = F_dv_min(R.add(F_dv_get(F_dv_get(F_dv_get(v_state, "physical"), "simulation"), "last_macro"), R.number("15")), F_dv_get(v_state, "horizon"));
            v_event_wake_index__307319 = F_dv_get(v_state, "cursor");
            v_wake_time__307281 = v_event_wake_time__307349;
            v_wake_index__307257 = F_dv_get(v_state, "cursor");
        }
        object v_ready_index__307643 = R.sub(R.number("0"), R.number("1"));
        object v_obligation_index__307668 = R.sub(R.number("0"), R.number("1"));
        object v_obligation_time__307698 = R.add(F_dv_get(v_state, "horizon"), R.number("1"));
        object v_offset__307742 = R.number("0");
        object v_index__307761 = F_dv_get(v_state, "cursor");
        _ = R.phase("schedule-scan-start", "");
        try
        {
            while (R.Bool(R.lt(v_offset__307742, R.count(F_dv_get(v_state, "actors")))))
            {
                object v_actor__307837 = R.at(F_dv_get(v_state, "actors"), v_index__307761);
                if (F_dv_find(v_actor__307837, "in_flight_action", false) is not false)
                {
                    v_index__307761 = R.add(v_index__307761, 1d);
                    if (R.Bool(R.eq(v_index__307761, R.count(F_dv_get(v_state, "actors"))))) v_index__307761 = 0d;
                    v_offset__307742 = R.add(v_offset__307742, 1d);
                    continue;
                }
                object v_queued__307877 = F_dv_find(v_actor__307837, "scheduled_action", false);
                if (R.Bool(R.not(F_dvs_actor_can_act(v_actor__307837))))
                {
                    object held = F_dv_find(v_actor__307837, "navigation_commitment", false);
                    if (!R.Bool(R.eq(v_queued__307877, false)) || !R.Bool(R.eq(held, false)))
                    {
                        object stopped = F_dv_with(v_actor__307837, "scheduled_action", false);
                        object updates = new List<object>();
                        if (!R.Bool(R.eq(held, false)))
                        {
                            object events = F_dv_clone(F_dv_get(v_state, "events"));
                            object task = F_dv_get(held, "task");
                            object parent = R.Bool(R.eq(v_queued__307877, false)) ? F_dv_find(held, "selection_event", "") : F_dv_get(v_queued__307877, "parent");
                            _ = F_dvs_event(events, "navigation_task_released", F_dv_get(v_actor__307837, "id"), F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_single(parent),
                                new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("reason", "actor-unable-to-act"), F_dv_pair("action_event", F_dv_find(held, "pending_action_event", "")) });
                            stopped = F_dv_with(stopped, "navigation_commitment", false);
                            _ = R.push(updates, F_dv_pair("events", events));
                        }
                        v_updated__306553 = F_dvs_replace(v_updated__306553, v_index__307761, stopped);
                        _ = R.push(updates, F_dv_pair("actors", v_updated__306553));
                        v_state = R.snapshot(F_dvs_owned_state_update(v_state, updates));
                    }
                }
                else
                {
                    if (R.Bool(R.not(R.eq(v_queued__307877, false))))
                    {
                        if (R.Bool(R.lt(F_dv_get(v_queued__307877, "due"), v_due_time__307220)))
                        {
                            v_due_time__307220 = F_dv_get(v_queued__307877, "due");
                            v_due_index__307197 = v_index__307761;
                        }
                    }
                    else
                    {
                        object conversationUntil = F_dvs_conversation_wait_until(v_actor__307837, v_state);
                        bool conversationWaiting = R.Bool(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), conversationUntil));
                        if (conversationWaiting)
                        {
                            if (R.Bool(R.lt(conversationUntil, v_wake_time__307281)))
                            {
                                v_wake_index__307257 = v_index__307761;
                                v_wake_time__307281 = conversationUntil;
                            }
                            if (R.Bool(R.lt(conversationUntil, v_event_wake_time__307349)))
                            {
                                v_event_wake_index__307319 = v_index__307761;
                                v_event_wake_time__307349 = conversationUntil;
                            }
                        }
                        if (R.Bool((R.Bool((R.Bool(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_state, "horizon"))) ? (object)(R.lt(R.number("0"), F_dv_find(F_dv_get(v_actor__307837, "policy"), "budget", R.number("0")))) : (object)(false))) ? (object)(F_dvs_simulation_actor_ready(v_actor__307837, v_state)) : (object)(false))))
                        {
                            object v_duty__308540 = F_dvs_due_obligation(v_actor__307837, F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_state, "costs"));
                            if (R.Bool(R.not(R.eq(v_duty__308540, false))))
                            {
                                object v_due__308673 = F_dv_find(v_duty__308540, "obligation_due", R.number("0"));
                                if (R.Bool(R.lt(v_due__308673, v_obligation_time__307698)))
                                {
                                    v_obligation_index__307668 = v_index__307761;
                                    v_obligation_time__307698 = v_due__308673;
                                }
                            }
                        }
                        if (!conversationWaiting && R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_state, "horizon"))) ? (object)(R.lt(v_ready_index__307643, R.number("0"))) : (object)(false))) ? (object)(R.lt(R.number("0"), F_dv_find(F_dv_get(v_actor__307837, "policy"), "budget", R.number("0")))) : (object)(false))) ? (object)(F_dvs_simulation_actor_ready(v_actor__307837, v_state)) : (object)(false))) ? (object)((R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_actor__307837, "decision_time", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_state, "physical"), "time")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor__307837, "decision_revision", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_actor__307837, "mind"), "revision")))))) ? (object)(true) : (object)(R.lt(R.number("0"), R.count(F_dv_find(v_actor__307837, "pending_heard", new System.Collections.Generic.List<object>(0) { })))))) ? (object)(true) : (object)(F_dvs_idle_inputs_changed(v_actor__307837, v_state)))) : (object)(false))))
                        {
                            if (R.Bool(F_dvs_idle_guard_matches(v_actor__307837, v_state)))
                            {
                                object v_guard__309510 = F_dv_get(v_actor__307837, "idle_guard");
                                object v_advanced__309751 = F_dv_with_many(v_guard__309510, new System.Collections.Generic.List<object>(2) { F_dv_pair("event_cursor", R.count(F_dv_get(v_state, "events"))), F_dv_pair("event_anchor", R.at(F_dv_get(v_state, "events"), R.sub(R.count(F_dv_get(v_state, "events")), R.number("1")))) });
                                object v_sleeping__309910 = F_dv_with_many(v_actor__307837, new System.Collections.Generic.List<object>(3) { F_dv_pair("decision_time", F_dv_get(F_dv_get(v_state, "physical"), "time")), F_dv_pair("decision_revision", F_dv_get(F_dv_get(v_actor__307837, "mind"), "revision")), F_dv_pair("idle_guard", v_advanced__309751) });
                                v_updated__306553 = F_dvs_replace(v_updated__306553, v_index__307761, v_sleeping__309910);
                                v_state = R.snapshot(F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(2) { F_dv_pair("actors", v_updated__306553), F_dv_pair("idle", R.add(F_dv_get(v_state, "idle"), R.number("1"))) }));
                            }
                            else
                            {
                                v_ready_index__307643 = v_index__307761;
                            }
                        }
                        object v_wake_guard__310329 = F_dv_find(v_actor__307837, "idle_guard", false);
                        if (R.Bool(R.not(R.eq(v_wake_guard__310329, false))))
                        {
                            if (R.Bool((R.Bool(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_wake_guard__310329, "until"))) ? (object)(R.lt(F_dv_get(v_wake_guard__310329, "until"), v_wake_time__307281)) : (object)(false))))
                            {
                                v_wake_index__307257 = v_index__307761;
                                v_wake_time__307281 = F_dv_get(v_wake_guard__310329, "until");
                            }
                            if (R.Bool((R.Bool((R.Bool(F_dv_find(v_wake_guard__310329, "declared_time_boundary", false)) ? (object)(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_wake_guard__310329, "until"))) : (object)(false))) ? (object)(R.lt(F_dv_get(v_wake_guard__310329, "until"), v_event_wake_time__307349)) : (object)(false))))
                            {
                                v_event_wake_index__307319 = v_index__307761;
                                v_event_wake_time__307349 = F_dv_get(v_wake_guard__310329, "until");
                            }
                        }
                    }
                }
                v_index__307761 = R.add(v_index__307761, R.number("1"));
                if (R.Bool(R.eq(v_index__307761, R.count(F_dv_get(v_state, "actors")))))
                {
                    v_index__307761 = R.number("0");
                }
                v_offset__307742 = R.add(v_offset__307742, R.number("1"));
            }
        }
        finally { _ = R.phase("schedule-scan-end", ""); }
        if (R.Bool((R.Bool(R.not(R.lt(v_due_index__307197, R.number("0")))) ? (object)(R.not(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), v_due_time__307220))) : (object)(false))))
        {
            return F_dvs_dispatch_due(v_state, v_due_index__307197);
        }
        if (R.Bool(R.not(R.lt(v_obligation_index__307668, R.number("0")))))
        {
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("cursor", v_obligation_index__307668) });
        }
        if (R.Bool(R.not(R.lt(v_ready_index__307643, R.number("0")))))
        {
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("cursor", v_ready_index__307643) });
        }
        // A renderer can finish a dispatched movement later. When no independent
        // action is queued, yield instead of burning the horizon on macro ticks.
        if (R.Num(v_due_index__307197) < 0 && R.Items(F_dv_get(v_state, "actors"))
            .Any(actor => F_dv_find(actor, "in_flight_action", false) is not false))
            return F_dvs_owned_state_update(v_state, new List<object> { F_dv_pair("host_motion_wait", true) });
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.lt(v_due_index__307197, R.number("0"))) ? (object)(R.not(R.lt(v_event_wake_index__307319, R.number("0")))) : (object)(false))) ? (object)(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), v_event_wake_time__307349)) : (object)(false))) ? (object)(R.not(R.lt(F_dv_get(v_state, "horizon"), v_event_wake_time__307349))) : (object)(false))))
        {
            object v_physical__311445 = F_dv_get(v_state, "physical");
            object v_events__311483 = F_dv_clone(F_dv_get(v_state, "events"));
            object v_parent__311527 = F_dvs_event(v_events__311483, "action_wait", "", F_dv_get(v_physical__311445, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(3) { F_dv_pair("until", v_event_wake_time__307349), F_dv_pair("waiting", R.number("0")), F_dv_pair("reason", "declared-subject-time-boundary") });
            while (R.Bool(R.lt(F_dv_get(v_physical__311445, "time"), v_event_wake_time__307349)))
            {
                object v_next_time__311741 = F_dvs_environment_next_time(v_physical__311445, F_dv_get(v_state, "story"), v_event_wake_time__307349);
                v_physical__311445 = F_dvs_environment(F_dv_with(v_physical__311445, "time", v_next_time__311741), F_dv_get(v_state, "story"), v_events__311483, v_parent__311527);
            }
            return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(4) { F_dv_pair("physical", v_physical__311445), F_dv_pair("events", v_events__311483), F_dv_pair("cursor", v_event_wake_index__307319), F_dv_pair("idle", R.number("0")) });
        }
        if (R.Bool(R.not(R.lt(v_due_index__307197, R.number("0")))))
        {
            object v_physical__312086 = F_dv_get(v_state, "physical");
            object v_events__312124 = F_dv_clone(F_dv_get(v_state, "events"));
            object v_until__312168 = F_dv_min(v_due_time__307220, v_wake_time__307281);
            object v_parent__312216 = F_dvs_event(v_events__312124, "action_wait", "", F_dv_get(v_physical__312086, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(2) { F_dv_pair("until", v_until__312168), F_dv_pair("waiting", F_dvs_scheduled_count(F_dv_get(v_state, "actors"))) });
            v_current__307173 = F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(2) { F_dv_pair("events", v_events__312124), F_dv_pair("idle", R.number("0")) });
            while (R.Bool(R.lt(F_dv_get(v_physical__312086, "time"), v_until__312168)))
            {
                object v_next_time__312555 = F_dvs_environment_next_time(v_physical__312086, F_dv_get(v_state, "story"), v_until__312168);
                if (R.Bool(R.lt(v_until__312168, v_next_time__312555)))
                {
                    v_next_time__312555 = v_until__312168;
                }
                v_physical__312086 = F_dvs_environment(F_dv_with(v_physical__312086, "time", v_next_time__312555), F_dv_get(v_state, "story"), v_events__312124, v_parent__312216);
                _ = R.phase("schedule-waits-start", "");
                try { v_current__307173 = R.snapshot(F_dvs_interrupt_waits(F_dvs_owned_state_update(v_current__307173, new System.Collections.Generic.List<object>(2) { F_dv_pair("physical", v_physical__312086), F_dv_pair("events", v_events__312124) }))); }
                finally { _ = R.phase("schedule-waits-end", ""); }
                object v_interrupted__312930 = F_dvs_interrupted_wait_due(F_dv_get(v_current__307173, "actors"), F_dv_get(v_physical__312086, "time"));
                if (R.Bool(R.not(R.lt(v_interrupted__312930, R.number("0")))))
                {
                    return F_dvs_dispatch_due(v_current__307173, v_interrupted__312930);
                }
            }
            if (R.Bool(R.lt(v_until__312168, v_due_time__307220)))
            {
                return F_dvs_owned_state_update(v_current__307173, new System.Collections.Generic.List<object>(1) { F_dv_pair("cursor", v_wake_index__307257) });
            }
            return F_dvs_dispatch_due(v_current__307173, v_due_index__307197);
        }
        return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("idle", R.count(F_dv_get(v_state, "actors"))) });
    }

    public static object F_dvs_debug_phase(object v_story, object v_actor, object v_time, object v_phase)
    {
        _ = R.phase(v_phase, v_actor);
        if (R.Bool(F_dv_find(v_story, "debug_phase_progress", false)))
        {
            _ = R.print(R.json_stringify(new System.Collections.Generic.List<object>(4) { F_dv_pair("type", "engine_phase"), F_dv_pair("actor", v_actor), F_dv_pair("game_time", v_time), F_dv_pair("phase", v_phase) }));
        }
        return true;
    }

    public static object F_dvs_prepare_actor(object v_state)
    {
        var preparedActor = R.at(F_dv_get(v_state, "actors"), F_dv_get(v_state, "cursor"));
        if (F_dv_find(preparedActor, "in_flight_action", false) is not false)
        {
            var next = (R.Num(F_dv_get(v_state, "cursor")) + 1) % R.Items(F_dv_get(v_state, "actors")).Count;
            return F_dvs_owned_state_update(v_state, new List<object> {
                F_dv_pair("cursor", next), F_dv_pair("awaiting_player", false),
                F_dv_pair("idle", R.add(F_dv_get(v_state, "idle"), 1d)) });
        }

        if (R.Bool(R.not(R.eq(F_dv_find(R.at(F_dv_get(v_state, "actors"), F_dv_get(v_state, "cursor")), "scheduled_action", false), false))))
        {
            return v_state;
        }
        v_state = F_dvs_simulation_sync(v_state);
        var waitingActor = R.at(F_dv_get(v_state, "actors"), F_dv_get(v_state, "cursor"));
        var conversationTime = F_dv_get(F_dv_get(v_state, "physical"), "time");
        if (R.Text(F_dv_find(v_state, "player_selection", "")).Length == 0
            && R.Text(F_dv_find(waitingActor, "simulation_priority_action", "")).Length == 0
            && R.Bool(R.lt(conversationTime, F_dvs_conversation_wait_until(waitingActor, v_state)))
            && F_dvs_due_obligation(waitingActor, conversationTime, F_dv_get(v_state, "costs")) is false)
        {
            var nextCursor = R.add(F_dv_get(v_state, "cursor"), 1d);
            if (!R.Bool(R.lt(nextCursor, R.count(F_dv_get(v_state, "actors"))))) nextCursor = 0d;
            return F_dvs_owned_state_update(v_state, new List<object> { F_dv_pair("cursor", nextCursor), F_dv_pair("awaiting_player", false) });
        }
        v_state = R.snapshot(v_state);
        object v_story__313956 = F_dv_get(v_state, "story");
        object v_physical__313984 = F_dv_get(v_state, "physical");
        object v_actors__314018 = F_dv_get(v_state, "actors");
        object v_events__314048 = F_dv_clone(F_dv_get(v_state, "events"));
        object v_bindings__314088 = F_dv_get(v_state, "bindings");
        object v_costs__314122 = F_dv_get(v_state, "costs");
        object v_run_start__314150 = F_dv_get(v_state, "run_start");
        object v_duration_budget__314186 = F_dv_get(v_state, "duration_budget");
        object v_horizon__314234 = F_dv_get(v_state, "horizon");
        object v_duration_blocked__314266 = F_dv_get(v_state, "duration_blocked");
        object v_limit__314316 = F_dv_get(v_state, "limit");
        object v_cursor__314344 = F_dv_get(v_state, "cursor");
        object v_executed__314374 = F_dv_get(v_state, "executed");
        object v_idle__314408 = F_dv_get(v_state, "idle");
        object v_status__314434 = F_dv_get(v_state, "status");
        object v_initial__314464 = F_dv_get(v_state, "initial");
        object v_index__314496 = v_cursor__314344;
        object v_actor__314519 = F_dv_with_many(R.at(v_actors__314018, v_cursor__314344), new System.Collections.Generic.List<object>(2) { F_dv_pair("idle_guard", false), F_dv_pair("decision_time", F_dv_get(v_physical__313984, "time")) });
        if (R.Bool(F_dv_has_key(v_physical__313984, "simulation")))
        {
            v_actor__314519 = F_dv_with_many(v_actor__314519, new System.Collections.Generic.List<object>(2) { F_dv_pair("simulation_decision_macro", F_dv_get(F_dv_get(v_physical__313984, "simulation"), "macro_count")), F_dv_pair("simulation_decision_nearby", F_dv_get(F_dv_get(v_physical__313984, "simulation"), "last_nearby_second")) });
        }
        object v_parent__314832 = F_dv_get(R.at(v_events__314048, R.sub(R.count(v_events__314048), R.number("1"))), "id");
        object v_time__314883 = F_dv_find(v_physical__313984, "time", R.number("0"));
        object v_local_1__314927 = false;
        if (R.Bool(R.lt(R.add(v_cursor__314344, R.number("1")), R.count(v_actors__314018))))
        {
            v_local_1__314927 = R.add(v_cursor__314344, R.number("1"));
        }
        else
        {
            v_local_1__314927 = R.number("0");
        }
        v_cursor__314344 = v_local_1__314927;
        if (R.Bool((R.Bool(R.not(F_dvs_actor_can_act(v_actor__314519))) ? (object)(true) : (object)(R.not(R.lt(R.number("0"), F_dv_find(F_dv_get(v_actor__314519, "policy"), "budget", R.number("0"))))))))
        {
            v_idle__314408 = R.add(v_idle__314408, R.number("1"));
        }
        else
        {
            if (R.Bool(R.eq(F_dv_find(v_state, "player_selection", ""), "")))
            {
                v_actor__314519 = F_dv_with(v_actor__314519, "policy", F_dv_with(F_dv_get(v_actor__314519, "policy"), "remaining_game_time", R.sub(v_horizon__314234, v_time__314883)));
                v_actor__314519 = F_dvs_update_support_events(v_actor__314519, v_events__314048);
                _ = F_dvs_debug_phase(v_story__313956, F_dv_get(v_actor__314519, "id"), v_time__314883, "cognition-start");
                v_actor__314519 = F_dvs_drain_received_speech(v_actor__314519, v_events__314048, v_time__314883);
                v_actor__314519 = F_dvs_notice_peers(v_actor__314519, v_physical__313984, v_actors__314018, v_bindings__314088, v_events__314048, v_time__314883, v_parent__314832);
                v_actor__314519 = F_dvs_notice_visible_objects(v_actor__314519, v_physical__313984, v_bindings__314088, v_events__314048, v_time__314883, v_parent__314832);
                v_actor__314519 = F_dvs_prepare_cognition(v_actor__314519, v_events__314048, v_time__314883, v_parent__314832);
                _ = F_dvs_debug_phase(v_story__313956, F_dv_get(v_actor__314519, "id"), v_time__314883, "cognition-end");
            }
            v_actor__314519 = F_dvs_materialize_arguments(v_actor__314519, v_events__314048);
            object v_choice__315950 = false;
            if (R.Bool(R.eq(F_dv_find(v_state, "player_actor", ""), F_dv_get(v_actor__314519, "id"))))
            {
                object v_selection__316040 = F_dv_find(v_state, "player_selection", "");
                if (R.Bool(R.eq(v_selection__316040, "")))
                {
                    object v_options__316143 = F_dvs_player_options(v_actor__314519);
                    if (R.Bool(R.lt(R.number("0"), R.count(v_options__316143))))
                    {
                        object v_offered__316401 = F_dv_find(v_actor__314519, "offered_actions", new System.Collections.Generic.List<object>(0) { });
                        object v_option_index__316473 = R.number("0");
                        while (R.Bool(R.lt(v_option_index__316473, R.count(v_options__316143))))
                        {
                            object v_option__316578 = R.at(v_options__316143, v_option_index__316473);
                            if (R.Bool(R.not(F_dv_has(v_offered__316401, F_dv_get(v_option__316578, "id")))))
                            {
                                v_offered__316401 = F_dv_append(v_offered__316401, F_dv_get(v_option__316578, "id"));
                            }
                            v_option_index__316473 = R.add(v_option_index__316473, R.number("1"));
                        }
                        v_actor__314519 = F_dv_with(v_actor__314519, "offered_actions", v_offered__316401);
                        v_actors__314018 = F_dvs_replace(v_actors__314018, v_index__314496, v_actor__314519);
                        return F_dvs_control_state(v_state, v_actors__314018, v_events__314048, v_index__314496, F_dv_find(v_state, "player_actor", ""), "", true);
                    }
                }
                if (R.Bool(R.eq(v_selection__316040, "")))
                {
                    v_choice__315950 = new System.Collections.Generic.List<object>(4) { F_dv_pair("action", false), F_dv_pair("score", R.number("0")), F_dv_pair("plan_expansions", R.number("0")), F_dv_pair("plans", new System.Collections.Generic.List<object>(0) { }) };
                }
                else
                {
                    v_choice__315950 = F_dvs_player_choice(v_actor__314519, v_selection__316040);
                }
            }
            else
            {
                _ = F_dvs_debug_phase(v_story__313956, F_dv_get(v_actor__314519, "id"), v_time__314883, "choice-start");
                _ = R.phase("decision-compute-start", F_dv_get(v_actor__314519, "id"));
                v_actor__314519 = F_dvs_navigation_task_sync(v_actor__314519, v_events__314048, v_time__314883, v_physical__313984);
                v_choice__315950 = F_dvs_choose(v_actor__314519, v_bindings__314088, v_costs__314122);
                object navigationDiagnostic = F_dvs_navigation_task_diagnostics(v_actor__314519, v_choice__315950);
                object declaredCount = R.number("0");
                foreach (object row in R.Items(navigationDiagnostic))
                    if (R.Bool(F_dv_find(row, "declared", false))) declaredCount = R.add(declaredCount, R.number("1"));
                _ = F_dvs_event(v_events__314048, "navigation_task_diagnostic", F_dv_get(v_actor__314519, "id"), v_time__314883, F_dv_single(v_parent__314832), new List<object> { F_dv_pair("rows", navigationDiagnostic), F_dv_pair("row_count", R.count(navigationDiagnostic)), F_dv_pair("declared_count", declaredCount), F_dv_pair("route_search", F_dv_find(v_actor__314519, "navigation_route_diagnostic", false)) });
                _ = R.phase("decision-compute-end", F_dv_get(v_actor__314519, "id"));
                _ = F_dvs_debug_phase(v_story__313956, F_dv_get(v_actor__314519, "id"), v_time__314883, "choice-end");
            }
            v_choice__315950 = F_dv_with_many(v_choice__315950, new System.Collections.Generic.List<object>(5) { F_dv_pair("cognition_reused", F_dv_find(v_actor__314519, "cognition_reused", false)), F_dv_pair("reason_reused", F_dv_find(v_actor__314519, "reason_reused", false)), F_dv_pair("cognition_stop", F_dv_find(v_actor__314519, "cognition_stop", "ordinary-cognition-pass")), F_dv_pair("evaluated_at_game_time", v_time__314883), F_dv_pair("mind_revision", F_dv_get(F_dv_get(v_actor__314519, "mind"), "revision")) });
            object v_action__317883 = F_dv_get(v_choice__315950, "action");
            v_actor__314519 = F_dv_with(v_actor__314519, "decision_revision", F_dv_get(F_dv_get(v_actor__314519, "mind"), "revision"));
            if (R.Bool(R.eq(v_action__317883, false)))
            {
                _ = R.phase("prepare-idle-start", "");
                try
                {
                    if (R.Bool(R.lt(R.number("0"), F_dv_find(v_choice__315950, "time_blocked", R.number("0")))))
                    {
                        v_duration_blocked__314266 = true;
                    }
                    v_idle__314408 = R.add(v_idle__314408, R.number("1"));
                    object v_stop__318152 = F_dvs_event(v_events__314048, "planning_stop", F_dv_get(v_actor__314519, "id"), v_time__314883, F_dv_single(v_parent__314832), v_choice__315950);
                    if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_state, "player_actor", ""), F_dv_get(v_actor__314519, "id")))) ? (object)(R.eq(F_dv_find(v_choice__315950, "time_blocked", R.number("0")), R.number("0"))) : (object)(false))) ? (object)(F_dvs_idle_can_wait(v_actor__314519)) : (object)(false))))
                    {
                        object v_boundary__318400 = F_dvs_idle_window_boundary(v_actor__314519, v_story__313956, v_time__314883, v_horizon__314234);
                        if (R.Bool(R.lt(v_time__314883, F_dv_get(v_boundary__318400, "next"))))
                        {
                            object v_guard__318530 = new System.Collections.Generic.List<object>(13) { F_dv_pair("time", v_time__314883), F_dv_pair("until", F_dv_get(v_boundary__318400, "next")), F_dv_pair("source_stop", v_stop__318152), F_dv_pair("proof", "approximate-yield"), F_dv_pair("time_analysis_complete", F_dv_get(v_boundary__318400, "complete")), F_dv_pair("declared_time_boundary", (R.Bool(F_dv_get(v_boundary__318400, "complete")) ? (object)(R.lt(F_dv_get(v_boundary__318400, "next"), v_horizon__314234)) : (object)(false))), F_dv_pair("subject", F_dvs_idle_subject_input(v_actor__314519, v_costs__314122)), F_dv_pair("contact", F_dvs_idle_contact_input(v_actor__314519, v_physical__313984, v_actors__314018)), F_dv_pair("bindings", v_bindings__314088), F_dv_pair("costs", v_costs__314122), F_dv_pair("story", F_dvs_idle_story_input(v_story__313956)), F_dv_pair("event_cursor", R.count(v_events__314048)), F_dv_pair("event_anchor", R.at(v_events__314048, R.sub(R.count(v_events__314048), R.number("1")))) };
                            v_actor__314519 = F_dv_with(v_actor__314519, "idle_guard", R.snapshot(v_guard__318530));
                        }
                    }
                }
                finally { _ = R.phase("prepare-idle-end", ""); }
            }
            else
            {
                if (R.Bool(F_dv_has_key(v_action__317883, "base_action_id")) && R.Bool(F_dv_has_key(v_action__317883, "navigation_task")))
                {
                    object candidateId = F_dv_get(v_action__317883, "id");
                    v_action__317883 = F_dv_with_many(v_action__317883, new List<object> { F_dv_pair("navigation_candidate_id", candidateId), F_dv_pair("id", F_dv_get(v_action__317883, "base_action_id")) });
                }
                v_action__317883 = F_dvs_bind_reply(v_actor__314519, v_action__317883);
                v_action__317883 = F_dv_with(v_action__317883, "label", F_dvs_action_label(v_actor__314519, v_action__317883));
                object v_id__319197 = F_dv_get(v_actor__314519, "id");
                object v_mind__319227 = F_dv_get(v_actor__314519, "mind");
                object v_model__319261 = F_dv_get(v_costs__314122, F_dv_get(v_action__317883, "kind"));
                object v_action_event__319312 = R.cat("action", R.show(v_executed__314374));
                object v_ledger__319379 = F_dv_get(v_actor__314519, "ledger");
                _ = R.phase("selection-evidence-start", v_id__319197);
                object v_selection_parents__319470 = F_dvs_selection_parents(v_actor__314519, v_action__317883, v_choice__315950, v_events__314048, v_parent__314832);
                _ = R.phase("selection-evidence-end", v_id__319197);
                _ = R.phase("decision-record-start", v_id__319197);
                v_parent__314832 = F_dvs_event(v_events__314048, "action_selected", v_id__319197, v_time__314883, v_selection_parents__319470, F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("action", v_action__317883), F_dv_pair("revision_before", F_dv_get(v_mind__319227, "revision")), F_dv_pair("score", F_dv_get(v_choice__315950, "score")), F_dv_pair("hypothesis", F_dv_find(v_actor__314519, "hypothesis", false)) }, new System.Collections.Generic.List<object>(8) { F_dv_pair("plan_expansions", F_dv_get(v_choice__315950, "plan_expansions")), F_dv_pair("plans", F_dv_get(v_choice__315950, "plans")), F_dv_pair("selected_path", F_dv_find(v_choice__315950, "selected_path", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("planning_stop", F_dv_find(v_choice__315950, "stop", "manual-choice")), F_dv_pair("planning_strategy", F_dv_find(v_choice__315950, "strategy", "manual-choice")), F_dv_pair("planning_approximation", F_dv_find(v_choice__315950, "approximation", false)), F_dv_pair("planning_work", F_dv_find(v_choice__315950, "planning_work", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("owned_decision", F_dvs_owned_decision_selection(v_actor__314519, v_action__317883)) }));
                v_parent__314832 = F_dvs_event(v_events__314048, "planning_choice", v_id__319197, v_time__314883, F_dv_single(v_parent__314832), v_choice__315950);
                object v_bounds__320442 = F_dv_action_cost_bounds(F_dv_get(v_model__319261, "minimum"), F_dv_get(v_model__319261, "maximum"));
                if (R.Bool(F_dv_or(R.lt(v_horizon__314234, R.add(v_time__314883, F_dv_get(F_dv_get(v_bounds__320442, "maximum"), "time"))), R.lt(v_horizon__314234, R.add(v_time__314883, F_dv_get(F_dv_get(v_model__319261, "actual"), "time"))))))
                {
                    v_duration_blocked__314266 = true;
                    v_idle__314408 = R.add(v_idle__314408, R.number("1"));
                    _ = F_dvs_event(v_events__314048, "duration_budget_rejected", v_id__319197, v_time__314883, F_dv_single(v_parent__314832), new System.Collections.Generic.List<object>(4) { F_dv_pair("action", v_action__317883), F_dv_pair("bounds", v_bounds__320442), F_dv_pair("actual", F_dv_get(v_model__319261, "actual")), F_dv_pair("time_origin", new System.Collections.Generic.List<object>(4) { F_dv_pair("run_start", v_run_start__314150), F_dv_pair("duration_budget", v_duration_budget__314186), F_dv_pair("horizon", v_horizon__314234), F_dv_pair("unit", "minute") }) });
                }
                else
                {
                    if (R.Bool(R.not(F_dv_action_cost_le(F_dv_get(v_bounds__320442, "maximum"), F_dv_get(v_ledger__319379, "balances")))))
                    {
                        _ = F_dvs_event(v_events__314048, "resource_rejected", v_id__319197, v_time__314883, F_dv_single(v_parent__314832), new System.Collections.Generic.List<object>(2) { F_dv_pair("action", v_action__317883), F_dv_pair("ledger", v_ledger__319379) });
                        v_idle__314408 = R.add(v_idle__314408, R.number("1"));
                    }
                    else
                    {
                        object v_queued__321254 = new System.Collections.Generic.List<object>(6) { F_dv_pair("action", v_action__317883), F_dv_pair("choice", new System.Collections.Generic.List<object>(2) { F_dv_pair("relevant_refs", F_dv_get(v_choice__315950, "relevant_refs")), F_dv_pair("selected_path", F_dv_find(v_choice__315950, "selected_path", new System.Collections.Generic.List<object>(0) { })) }), F_dv_pair("model", v_model__319261), F_dv_pair("start", v_time__314883), F_dv_pair("due", R.add(v_time__314883, F_dv_get(F_dv_get(v_model__319261, "actual"), "time"))), F_dv_pair("parent", v_parent__314832) };
                        if (R.Bool(F_dv_find(v_action__317883, "interruptible", false)))
                        {
                            _ = F_dvs_validate_interruptible_wait(v_action__317883, v_model__319261);
                            v_queued__321254 = F_dv_with(v_queued__321254, "wait_watch", new System.Collections.Generic.List<object>(2) { F_dv_pair("contact", F_dvs_idle_contact_input(v_actor__314519, v_physical__313984, v_actors__314018)), F_dv_pair("event_cursor", R.count(v_events__314048)) });
                        }
                        v_actor__314519 = F_dvs_navigation_task_commit(v_actor__314519, v_action__317883, v_events__314048, v_time__314883, v_parent__314832);
                        v_actor__314519 = F_dv_with(v_actor__314519, "scheduled_action", v_queued__321254);
                        _ = F_dvs_event(v_events__314048, "action_scheduled", v_id__319197, v_time__314883, new System.Collections.Generic.List<object>(1) { v_parent__314832 }, new System.Collections.Generic.List<object>(3) { F_dv_pair("action_id", F_dv_get(v_action__317883, "id")), F_dv_pair("due", F_dv_get(v_queued__321254, "due")), F_dv_pair("duration", F_dv_get(F_dv_get(v_model__319261, "actual"), "time")), F_dv_pair("estimated_due", F_dv_find(v_queued__321254, "estimated_due", F_dv_get(v_queued__321254, "due"))) });
                        v_idle__314408 = R.number("0");
                    }
                }
                _ = R.phase("decision-record-end", v_id__319197);
            }
        }
        v_actors__314018 = F_dvs_replace(v_actors__314018, v_index__314496, v_actor__314519);
        return new System.Collections.Generic.List<object>(19) { F_dv_pair("player_actor", F_dv_find(v_state, "player_actor", "")), F_dv_pair("story", v_story__313956), F_dv_pair("physical", v_physical__313984), F_dv_pair("actors", v_actors__314018), F_dv_pair("events", v_events__314048), F_dv_pair("bindings", v_bindings__314088), F_dv_pair("costs", v_costs__314122), F_dv_pair("run_start", v_run_start__314150), F_dv_pair("duration_budget", v_duration_budget__314186), F_dv_pair("horizon", v_horizon__314234), F_dv_pair("duration_blocked", v_duration_blocked__314266), F_dv_pair("limit", v_limit__314316), F_dv_pair("cursor", v_cursor__314344), F_dv_pair("executed", v_executed__314374), F_dv_pair("idle", v_idle__314408), F_dv_pair("status", v_status__314434), F_dv_pair("initial", v_initial__314464), F_dv_pair("awaiting_player", false), F_dv_pair("player_selection", "") };
    }
}
}

// --- Source: ActionFailureMemory.cs ---
namespace DuelView.Runtime
{


public sealed record ActionFailureBasis(FieldKey Key,Fact? Fact);
public sealed record ActionFailureCheckpoint(string Action,string Reason,int Attempts,double LastAttempt,double RetryAt,
    double BasePenalty,ActionFailureBasis[] Basis,bool RefreshPending,bool Active=true,string ReleaseReason="",string[]? Resources=null,
    string RetryPolicy="interval");

/// <summary>A remembered failed attempt changes utility, not the set of known actions.
/// Only the actor's own relevant evidence, or an authored retry interval, releases it.</summary>
internal sealed class ActionFailureMemory
{
    private readonly ActorClient actor;
    private readonly Dictionary<string,ActionCandidate> actions;
    private readonly Dictionary<string,ActionFailureCheckpoint> memories=new(StringComparer.Ordinal);
    private readonly double minimumPenalty,retrySeconds,resourceRetrySeconds;
    internal long Revision {get;private set;}
    internal ActionFailureMemory(ActorClient actor,IEnumerable<ActionCandidate> catalog,double timeUnit)
    {
        this.actor=actor;actions=catalog.ToDictionary(action=>action.Id,StringComparer.Ordinal);
        var declaration=actor.Declaration.Get("autonomy").Get("failure_memory");
        minimumPenalty=declaration.Get("penalty").Number(100);
        retrySeconds=declaration.Get("retry_minutes").Number(45)*timeUnit;
        resourceRetrySeconds=declaration.Get("resource_retry_minutes").Number(1)*timeUnit;
        if(!double.IsFinite(minimumPenalty)||minimumPenalty<=0||!double.IsFinite(retrySeconds)||retrySeconds<=0||!double.IsFinite(resourceRetrySeconds)||resourceRetrySeconds<=0)
            throw new InvalidDataException("Failure memory needs a positive utility penalty and finite retry interval.");
    }
    internal ActionFailureCheckpoint? Record(ActionCandidate action,bool success,string reason,IEnumerable<FieldKey> basis,double now,double score,string[]? blockedResources=null)
    {
        if(success){if(memories.Remove(action.Id))Revision++;return null;}
        if(reason=="cancelled")return null;
        var prior=Read(action.Id,now,true);
        var resource=reason=="standing_resource_busy";
        var resources=resource?(blockedResources??action.Declaration.Get("resource_reservations").Items().Select(item=>item.Text()).ToArray()).Order(StringComparer.Ordinal).ToArray():null;
        var sameResource=resource&&prior?.Reason==reason&&prior.Resources is not null&&prior.Resources.SequenceEqual(resources!,StringComparer.Ordinal);
        var attempts=resource?sameResource?Math.Min(8,prior!.Attempts+1):1:prior is {Active:true}?Math.Min(8,prior.Attempts+1):1;
        var knownBlockers=reason=="objective_condition_failed"?KnownBlockers(action):[];
        var keys=(knownBlockers.Length>0?knownBlockers:basis).Distinct().Take(128).ToArray();
        var retry=resource?resourceRetrySeconds*Math.Min(8,Math.Pow(2,attempts-1)):retrySeconds*Math.Min(4,attempts);
        var memory=new ActionFailureCheckpoint(action.Id,reason,attempts,now,now+retry,
            Math.Max(minimumPenalty,Math.Abs(score)+1),keys.Select(key=>new ActionFailureBasis(key,actor.Recall(key))).ToArray(),actor.IsUpdating||actor.IsObserving,
            Resources:resources,RetryPolicy:knownBlockers.Length>0?"evidence_change":resource?"resource_backoff":"interval");
        memories[action.Id]=memory;Revision++;
        return memory;
    }
    private ActionFailureCheckpoint? Read(string action,double now,bool update=false)
    {
        if(!memories.TryGetValue(action,out var memory))return null;
        if(memory.RefreshPending&&!actor.IsUpdating&&!actor.IsObserving)
        {
            var knownBlockers=memory.Reason=="objective_condition_failed"?KnownBlockers(actions[memory.Action]):[];
            memory=memory with {Basis=(knownBlockers.Length>0?knownBlockers:memory.Basis.Select(item=>item.Key))
                .Select(key=>new ActionFailureBasis(key,actor.Recall(key))).ToArray(),RefreshPending=false,
                RetryPolicy=knownBlockers.Length>0?"evidence_change":memory.RetryPolicy};
            if(update){memories[action]=memory;Revision++;}
        }
        if(memory.Active)
        {
            var changed=!memory.RefreshPending&&memory.Basis.Any(item=>!Same(item.Fact,actor.Recall(item.Key)));
            if(changed||memory.RetryPolicy!="evidence_change"&&now>=memory.RetryAt)
            {
                memory=memory with {Active=false,ReleaseReason=changed?"relevant_owned_evidence_changed":"retry_interval_elapsed"};
                if(update){memories[action]=memory;Revision++;}
            }
        }
        return memory;
    }
    private FieldKey[] KnownBlockers(ActionCandidate action)
    {
        IEnumerable<FieldKey> FalseInputs(Condition condition)
        {
            if(condition.Evaluate(actor.Recall)!=Truth.False)return [];
            return condition is Condition.All all?all.Items.SelectMany(FalseInputs):condition.Inputs;
        }
        var objective=action.Declaration.Get("objective_gate");
        return FalseInputs(action.Gate).Concat(objective.ValueKind==JsonValueKind.Object?
            FalseInputs(Condition.Compile(objective,actor.Id)):[])
            .Where(key=>actor.Recall(key) is {Known:true,Conflicted:false}).Distinct().Take(128).ToArray();
    }
    // Seeing the same empty container again is fresh evidence, but it does not
    // remove the known obstacle. Keep versions in the audit without treating a
    // same-value refresh as a new opportunity to repeat the failed action.
    private static bool Same(Fact? before,Fact? after)=>before is null?after is null:after is not null&&
        before.Known==after.Known&&before.Conflicted==after.Conflicted&&before.Confidence==after.Confidence&&Values.Same(before.Value,after.Value);
    internal double Penalty(string action,double now)=>Read(action,now) is {Active:true} memory?memory.BasePenalty*memory.Attempts:0;
    internal void Refresh(double now){foreach(var action in memories.Keys.ToArray())Read(action,now,true);}
    internal object Audit(double now)=>memories.Keys.ToArray().Select(action=>Read(action,now)).Where(item=>item is not null).ToArray();
    internal ActionFailureCheckpoint[] Save()=>memories.Values.ToArray();
    internal void Restore(ActionFailureCheckpoint[]? state)
    {
        memories.Clear();
        foreach(var memory in state??[])
        {
            if(!actions.ContainsKey(memory.Action)||string.IsNullOrWhiteSpace(memory.Reason)||memory.Attempts is <1 or >8||
                !double.IsFinite(memory.LastAttempt)||memory.LastAttempt<0||!double.IsFinite(memory.RetryAt)||memory.RetryAt<memory.LastAttempt||
                !double.IsFinite(memory.BasePenalty)||memory.BasePenalty<=0||memory.Basis is null||memory.Basis.Length>128||
                memory.Basis.Select(item=>item.Key).Distinct().Count()!=memory.Basis.Length||
                memory.RetryPolicy is not ("interval" or "resource_backoff" or "evidence_change")||
                memory.RetryPolicy=="evidence_change"&&(memory.Reason!="objective_condition_failed"||memory.Basis.Length==0)||
                memory.RetryPolicy=="resource_backoff"&&memory.Reason!="standing_resource_busy"||
                memory.Resources is { } resources&&(resources.Any(string.IsNullOrWhiteSpace)||resources.Distinct(StringComparer.Ordinal).Count()!=resources.Length)||!memories.TryAdd(memory.Action,memory))
                throw new InvalidDataException("Incompatible action failure memory checkpoint.");
        }
        Revision++;
    }
}
}
