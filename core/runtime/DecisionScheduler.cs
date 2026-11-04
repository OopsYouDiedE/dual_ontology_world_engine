using System.Diagnostics;
using System.Text.Json;

namespace DuelView.Runtime;

public sealed record ActionCandidate(string Id, Condition Gate, double Duration, double Score,
    JsonElement Declaration);
public sealed record Decision(string Actor, ActionCandidate Action, double StartedAt, double DueAt,long Execution=0,DecisionOption[]? Options=null);
public sealed record DecisionOption(string Action,double Score,DriveEvaluation Drive);
public sealed record SchedulerSlice(int Evaluations, bool Pending);
public sealed record DecisionCheckpoint(string Actor,string Action,double StartedAt,double DueAt,long Execution);
public sealed record SchedulerSlotCheckpoint(string Actor,double Due,int Cursor,string? Best,double? BestScore,long Revision,bool Autonomous,bool Held,DriveState Needs,string[] Selection,double SelectionTime,DecisionCheckpoint? Running);
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
    private int cursor;
    private long nextExecution;
    public long Evaluations { get; private set; }
    public void SetAutonomy(string actor,bool enabled)=>byActor[actor].Autonomous=enabled;
    public DecisionSchedulerState SaveState()=>new(slots.Select(slot=>new SchedulerSlotCheckpoint(slot.Actor.Id,slot.Due,slot.Cursor,slot.Best?.Id,
        double.IsFinite(slot.BestScore)?slot.BestScore:null,slot.Revision,slot.Autonomous,slot.Held,slot.Needs.SaveState(),
        slot.Selection.Select(action=>action.Id).ToArray(),slot.SelectionTime,slot.Running is { } run?new(run.Actor,run.Action.Id,run.StartedAt,run.DueAt,run.Execution):null)).ToArray(),
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
                   !decisions.TryAdd(run.Execution,new(run.Actor,action,run.StartedAt,run.DueAt,run.Execution)))throw new InvalidDataException("Invalid scheduled execution checkpoint.");
            }
        }
        if(state.Ready.Any(id=>!decisions.ContainsKey(id)))throw new InvalidDataException("Checkpoint has stale ready action.");
        foreach(var item in state.Slots)
        {
            var slot=byActor[item.Actor];var actions=slot.Candidates.ToDictionary(action=>action.Id,StringComparer.Ordinal);
            slot.Due=item.Due;slot.Cursor=item.Cursor;slot.Best=item.Best is { } best?actions[best]:null;slot.BestScore=item.BestScore??double.NegativeInfinity;
            slot.Revision=item.Revision;slot.Autonomous=item.Autonomous;slot.Held=item.Held;slot.Needs.RestoreState(item.Needs);
            slot.Selection=item.Selection.Select(id=>actions[id]).ToArray();slot.SelectionTime=item.SelectionTime;
            slot.Running=item.Running is { } running?decisions[running.Execution]:null;
        }
        ready.Clear();foreach(var id in state.Ready)ready.Enqueue(decisions[id]);cursor=state.Cursor;nextExecution=state.NextExecution;Evaluations=state.Evaluations;
        return decisions;
    }

    public DecisionScheduler(JsonElement policy,Func<string,ActionCandidate,bool>? eligibility=null)
    {
        eligible=eligibility??((_,_)=>true);
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
        byActor.Add(actor.Id, slot);
        slots.Add(slot);
    }

    public SchedulerSlice Step(double now)
    {
        ValidateTime(now);
        foreach(var item in slots)
            if(item.Needs.Tick(now))
            {
                item.Selection=[];item.Cursor=0;item.Best=null;item.BestScore=double.NegativeInfinity;
            }
        var timer = Stopwatch.StartNew();
        var count = 0;
        var visited = 0;
        while (slots.Count > 0 && count < Math.Min(evaluationsPerSlice, candidatesPerDecision) && visited < slots.Count && timer.Elapsed.TotalMilliseconds < millisecondsPerSlice)
        {
            var slot = slots[cursor];
            cursor = (cursor + 1) % slots.Count;
            visited++;
            if (slot.Held || !slot.Autonomous || slot.Running is not null || slot.Due > now || slot.Actor.IsUpdating || slot.Actor.IsObserving) continue;
            if (slot.Revision != slot.Actor.KnowledgeRevision)
            {
                slot.Cursor = 0;
                slot.Best = null;
                slot.BestScore = double.NegativeInfinity;
                slot.Revision = slot.Actor.KnowledgeRevision;
                slot.Selection=[];
            }
            if(slot.Selection.Length==0)
            {
                slot.SelectionTime=now;
                slot.Selection=slot.Index.Select(slot.Needs,now,candidatesPerDecision,drivesPerDecision,candidatesPerDrive);
            }
            if (slot.Cursor < slot.Selection.Length)
            {
                var candidate = slot.Selection[slot.Cursor++];
                count++;
                Evaluations++;
                if (!candidate.Declaration.Get("manual_only").Flag() && eligible(slot.Actor.Id,candidate) && candidate.Gate.Evaluate(slot.Actor.Recall) == Truth.True)
                {
                    var score = slot.Needs.Score(candidate, slot.SelectionTime) + slot.Actor.GoalScore(candidate, slot.SelectionTime / contentTimeUnit);
                    if (score > slot.BestScore) { slot.Best = candidate; slot.BestScore = score; }
                }
            }
            if (slot.Cursor < slot.Selection.Length) continue;
            if (slot.Best is { } best && slot.BestScore>minimumUtility && eligible(slot.Actor.Id,best))
            {
                var options=slot.Selection.Where(c=>!c.Declaration.Get("manual_only").Flag()&&eligible(slot.Actor.Id,c)&&c.Gate.Evaluate(slot.Actor.Recall)==Truth.True)
                    .Select(c=>new DecisionOption(c.Id,slot.Needs.Score(c,slot.SelectionTime)+slot.Actor.GoalScore(c,slot.SelectionTime/contentTimeUnit),slot.Needs.Evaluate(c,slot.SelectionTime))).ToArray();
                var decision = new Decision(slot.Actor.Id, best, now, now + best.Duration,++nextExecution,options);
                slot.Running = decision;
                ready.Enqueue(decision);
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

    internal bool Owns(Decision decision) => byActor.TryGetValue(decision.Actor, out var slot) && ReferenceEquals(slot.Running, decision);

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
        if (candidate.Gate.Evaluate(slot.Actor.Recall) != Truth.True) throw new InvalidOperationException("Action is unavailable in actor knowledge.");
        var decision = new Decision(actor, candidate, now, now + candidate.Duration,++nextExecution);
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
        if (succeeded) slot.Needs.Complete(decision.Action, now);
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
    internal void Hold(string actor,bool held)=>byActor[actor].Held=held;
    public bool Available(string actor,string action)
    {
        var slot=byActor[actor];
        if(slot.Held || slot.Running is not null || slot.Actor.IsUpdating || slot.Actor.IsObserving)return false;
        var candidate=slot.Candidates.Single(candidate=>candidate.Id==action);
        return eligible(actor,candidate)&&candidate.Gate.Evaluate(slot.Actor.Recall)==Truth.True;
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
