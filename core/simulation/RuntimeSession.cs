// @mechanism: causal_clock_and_campaign
// @layer: core/simulation
// @contracts: RuntimeSession

using DuelView.Autonomy;
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
using System.Text.RegularExpressions;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;
using static DuelView.Core.DVSimulationKernel;

// --- Source: RuntimeSession.cs ---
namespace DuelView.Runtime
{


public sealed record ActionInterruption(string Reason,string Label);
public sealed record ActionReceipt(string Actor, string Action, double Time, bool Committed, string Reason,long Execution=0,DecisionIntent? Intent=null,ActionInterruption? Interruption=null,ActionNarrative[]? Narrative=null);
public sealed record ActionOutcome(string Actor,string Action,long Execution,double StartedAt,double Time,bool Committed,string Reason,
    DecisionIntent? Intent,FieldChange[] Changes,Dictionary<string,double> NeedsBefore,Dictionary<string,double> NeedsAfter,
    ActionInterruption? Interruption=null,SavedFact[]? InterruptionBasis=null,ActionNarrative[]? Narrative=null,ActionFailureCheckpoint? LearnedFailure=null,
    ActionStartAttempt? StartAttempt=null);
public sealed record ActionStartAttempt(DecisionOption[] Options,DriveEvaluation Drive,JsonElement Audit);
public sealed record ActivityView(string Actor,string Action,long Execution,string Phase,double? WorkStartedAt,double? WorkEndsAt,bool HostCompleted);
public sealed record PhysicalPlacement(double X,double Y,double Z,double Yaw,string Room,double Time);
public sealed record JourneyView(PathPoint Position,double Distance,double Length,bool Arrived);
public sealed record ExecutionCheckpoint(long Execution,bool HostCompleted,string? ObservationToken,bool WorkAfterArrival,bool HostElapsed,
    double? WorkStartedAt,double? DueAt,bool CancelRequested,bool Dispatched,bool CompletionDeferred);
public sealed record CompletionCheckpoint(long Execution,double Due);
public sealed record StrategyBindingCheckpoint(long LastAttemptRevision,StrategyActivityState Activity);
public sealed record CompletedStrategyCheckpoint(long Execution,long Revision,CaseStrategyChoice[] Choices);
public sealed record ConversationCheckpoint(ConversationState State,JsonElement Declaration,Dictionary<string,double> Holds,string[] Locks);
public sealed record LogicalDoorState(double Angle,double Target,bool Moving,double CloseRemaining);
public sealed record RuntimeSessionState(int Version,FactStoreState World,ActorClientState[] Actors,SimulationClockState Clock,
    WorldRulesState Rules,DecisionSchedulerState Scheduler,ReservationCheckpoint[] Reservations,ExecutionCheckpoint[] Executions,
    CompletionCheckpoint[] Completions,long[] Dispatched,long[] Cancellations,ActionReceipt[] Receipts,
    Dictionary<long,PathJourneyState> Journeys,Dictionary<string,PhysicalPlacement> Placements,Dictionary<string,StrategyBindingCheckpoint> Strategies,
    CompletedStrategyCheckpoint[] CompletedStrategies,ConversationCheckpoint? Conversation,Dictionary<string,LogicalDoorState> Doors,long NextConversation,long NextEvidence,int ActorCursor,int StrategyCursor,
    MechanicsCheckpoint? Mechanics=null,SpatialAuthorityCheckpoint? Spatial=null,Dictionary<string,double>? ActionCooldowns=null,ActionOutcome[]? ActionOutcomes=null);

/// <summary>Serial session owner. The host executes presentation and submits real
/// completion. No action outcome is committed merely because a timer expired.</summary>
public sealed partial class RuntimeSession
{
    private sealed record Execution(Decision Decision, Effect[] Effects, Condition CompletionGate, Condition? Postcondition, bool HostRequired)
    {
        internal bool HostCompleted;
        internal string? ObservationToken;
        internal bool WorkAfterArrival;
        internal bool HostElapsed;
        internal double? WorkStartedAt;
        internal double DueAt=Decision.DueAt;
        internal bool CancelRequested;
        internal bool Dispatched;
        internal bool CompletionDeferred;
    }
    private readonly Dictionary<string, ActorClient> actors = new(StringComparer.Ordinal);
    private readonly List<(string Actor,JsonElement Data,Condition WorldGate,Condition KnowledgeGate,FieldKey[] Fields)> sensoryEvents=[];
    private readonly Dictionary<string, Execution> running = new(StringComparer.Ordinal);
    private readonly PriorityQueue<Execution, double> completions = new();
    private readonly Queue<ActionReceipt> receipts = [];
    private readonly Queue<ActionOutcome> actionOutcomes=[];
    private readonly Dictionary<string,double> actionCooldowns=new(StringComparer.Ordinal);
    private readonly Queue<Decision> dispatched = [];
    private readonly Queue<Decision> cancellations = [];
    private readonly int inferenceBudget;
    private readonly int workBudget;
    private readonly double sliceMilliseconds;
    private readonly FieldKey? clockField;
    private readonly double contentTimeUnit;
    private double observationHorizon=double.PositiveInfinity;
    private readonly HashSet<string> observationKinds;
    private PerceptionBindings perception = new(JsonSerializer.SerializeToElement(Array.Empty<object>()));
    private readonly HashSet<FieldKey> hostPhysicalBindings=[];
    private long nextEvidence;
    private int actorCursor;
    private string[] actorOrder = [];
    private readonly Dictionary<string,double> conversationHolds = new(StringComparer.Ordinal);
    private readonly HashSet<string> conversationLocks = new(StringComparer.Ordinal);
    private ConversationSession? conversation;
    private JsonElement conversationDeclaration;
    private readonly Dictionary<string,Condition> conversationGates=new(StringComparer.Ordinal);
    private readonly Dictionary<string,FieldKey> incapacitation=new(StringComparer.Ordinal);
    private long nextConversation;
    public FactStore World { get; } = new("world");
    private DecisionScheduler Scheduler { get; }
    public long DecisionEvaluations => Scheduler.Evaluations;
    public bool HasPendingDecisionWork { get; private set; }
    public SimulationClock Clock { get; }
    public RoomResidency Rooms { get; }
    public WorldRules Rules { get; }
    public SlotReservations Reservations { get; }=new();
    private readonly Dictionary<string,PhysicalPlacement> placements=new(StringComparer.Ordinal);
    private readonly Dictionary<string,LogicalDoorState> logicalDoors=new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string,LogicalDoorState> LogicalDoors=>logicalDoors;
    public void ReportDoor(string id,LogicalDoorState state)
    {
        if(string.IsNullOrWhiteSpace(id)||!double.IsFinite(state.Angle)||!double.IsFinite(state.Target)||!double.IsFinite(state.CloseRemaining)||state.CloseRemaining<0 || Math.Abs(state.Angle)>180 || Math.Abs(state.Target)>180)
            throw new InvalidDataException("Invalid logical door state.");
        logicalDoors[id]=state;
    }
    private sealed record PresenceGroup(string[] Members,HashSet<string> Rooms,FieldKey Result,double[]? Center,double Radius,bool Latch);
    private readonly List<PresenceGroup> presenceGroups=[];
    private sealed record Accumulator(FieldKey Active,FieldKey Present,FieldKey Amount,double Rate);
    private readonly List<Accumulator> accumulators=[];
    private readonly Dictionary<long,PathJourney> journeys=[];
    public void ConfigureAccumulators(JsonElement declarations)
    {
        foreach(var declaration in declarations.Items())
        {
            if(accumulators.Count>=32)throw new InvalidDataException("Accumulator budget exceeded.");
            var rate=declaration.Get("per_game_second").Number(double.NaN);
            var active=FieldKey.Read(declaration.Get("active"),"world");
            var present=FieldKey.Read(declaration.Get("present"),"world");
            var amount=FieldKey.Read(declaration.Get("amount"),"world");
            if(!double.IsFinite(rate)||rate<=0 || World.Read(amount) is not {Known:true,Conflicted:false} initial || !double.IsFinite(initial.Value.Number(double.NaN)) || initial.Value.Number()<0)
                throw new InvalidDataException("Invalid objective accumulator declaration.");
            accumulators.Add(new(active,present,amount,rate));
        }
    }
    private readonly Dictionary<string,(CaseTheory theory,JsonElement declaration)> deductions=new(StringComparer.Ordinal);
    private sealed class StrategyBinding(string actor,StrategyActivity activity,FieldKey output,FieldKey? completion,bool publish=false)
    {
        internal readonly string Actor=actor;
        internal readonly StrategyActivity Activity=activity;
        internal readonly FieldKey Output=output;
        internal readonly FieldKey? Completion=completion;
        internal readonly bool Publish=publish;
        internal long LastAttemptRevision=-1;
    }
    private readonly Dictionary<string,StrategyBinding> strategyBindings=new(StringComparer.Ordinal);
    private readonly Dictionary<long,(long Revision,IReadOnlyList<CaseStrategyChoice> Choices)> completedStrategies=[];
    private int strategyCursor;
    public object? LastStrategy { get; private set; }
    public Decision? DecisionFor(long execution)=>running.Values.Select(item=>item.Decision).SingleOrDefault(item=>item.Execution==execution);
    public RuntimeSessionState SaveState()
    {
        var actions=running.Values.Select(item=>new ExecutionCheckpoint(item.Decision.Execution,item.HostCompleted,item.ObservationToken,
            item.WorkAfterArrival,item.HostElapsed,item.WorkStartedAt,double.IsFinite(item.DueAt)?item.DueAt:null,item.CancelRequested,item.Dispatched,item.CompletionDeferred)).ToArray();
        return new(4,World.SaveState(),actors.Values.Select(actor=>actor.SaveState()).ToArray(),Clock.SaveState(),Rules.SaveState(),Scheduler.SaveState(),Reservations.SaveState(),actions,
            completions.UnorderedItems.Select(item=>new CompletionCheckpoint(item.Element.Decision.Execution,item.Priority)).ToArray(),
            dispatched.Where(item=>running.TryGetValue(item.Actor,out var run)&&ReferenceEquals(run.Decision,item)).Select(item=>item.Execution).ToArray(),
            cancellations.Where(item=>running.TryGetValue(item.Actor,out var run)&&ReferenceEquals(run.Decision,item)).Select(item=>item.Execution).ToArray(),receipts.ToArray(),
            journeys.ToDictionary(item=>item.Key,item=>item.Value.SaveState()),new(placements,StringComparer.Ordinal),
            strategyBindings.ToDictionary(item=>item.Key,item=>new StrategyBindingCheckpoint(item.Value.LastAttemptRevision,item.Value.Activity.SaveState()),StringComparer.Ordinal),
            completedStrategies.Select(item=>new CompletedStrategyCheckpoint(item.Key,item.Value.Revision,item.Value.Choices.ToArray())).ToArray(),
            conversation is null?null:new(conversation.SaveState(),conversationDeclaration.Clone(),new(conversationHolds,StringComparer.Ordinal),conversationLocks.ToArray()),new(logicalDoors,StringComparer.Ordinal),
            nextConversation,nextEvidence,actorCursor,strategyCursor,SaveMechanics(),SaveSpatial(),new(actionCooldowns,StringComparer.Ordinal),actionOutcomes.ToArray());
    }
    public void RestoreState(RuntimeSessionState state)
    {
        if(state.Version is not (1 or 2 or 3 or 4) || state.Version>=2&&state.Mechanics is null || state.Version>=3&&state.Spatial is null || state.Version>=4&&(state.ActionCooldowns is null||state.ActionOutcomes is null) || state.Actors is null || state.Actors.Length!=actors.Count || state.Actors.Select(item=>item.Actor).Distinct(StringComparer.Ordinal).Count()!=actors.Count ||
           state.Actors.Any(item=>!actors.ContainsKey(item.Actor)) || state.Executions is null || state.Completions is null || state.Dispatched is null || state.Cancellations is null || state.Receipts is null ||
           state.Journeys is null || state.Placements is null || state.Strategies is null || state.CompletedStrategies is null || state.Doors is null || state.NextConversation<0 || state.NextEvidence<0 || state.StrategyCursor<0 || state.ActorCursor<0 || state.ActorCursor>=Math.Max(1,actorOrder.Length) ||
           state.Strategies.Count!=strategyBindings.Count || state.Strategies.Keys.Any(key=>!strategyBindings.ContainsKey(key)))
            throw new InvalidDataException("Incompatible runtime checkpoint.");
        World.RestoreState(state.World);foreach(var actor in state.Actors)actors[actor.Actor].RestoreState(actor);
        Clock.RestoreState(state.Clock);Rules.RestoreState(state.Rules);var decisions=Scheduler.RestoreState(state.Scheduler);
        Reservations.RestoreState(state.Reservations);
        running.Clear();var executions=new Dictionary<long,Execution>();
        foreach(var item in state.Executions)
        {
            if(!decisions.TryGetValue(item.Execution,out var decision) || executions.ContainsKey(item.Execution) || running.ContainsKey(decision.Actor) ||
               item.WorkStartedAt is { } work && (!double.IsFinite(work)||work<0) || item.DueAt is { } due && (!double.IsFinite(due)||due<0))
                throw new InvalidDataException("Invalid runtime execution checkpoint.");
            var declaration=decision.Action.Declaration;
            var gate=declaration.Get("objective_gate").ValueKind==JsonValueKind.Object?Condition.Compile(declaration.Get("objective_gate"),World.Subject):new Condition.Constant(Truth.True);
            var effects=declaration.Get("effects").Items().Select(effect=>Effect.Compile(effect,World.Subject)).ToArray();
            var post=declaration.Get("objective_postcondition").ValueKind==JsonValueKind.Object?Condition.Compile(declaration.Get("objective_postcondition"),World.Subject):null;
            var execution=new Execution(decision,effects,gate,post,declaration.Get("requires_host_completion").Flag()) {
                HostCompleted=item.HostCompleted,ObservationToken=item.ObservationToken,WorkAfterArrival=item.WorkAfterArrival,HostElapsed=item.HostElapsed,
                WorkStartedAt=item.WorkStartedAt,DueAt=item.DueAt??double.PositiveInfinity,CancelRequested=item.CancelRequested,Dispatched=item.Dispatched,CompletionDeferred=item.CompletionDeferred};
            if(execution.WorkAfterArrival!= (execution.HostRequired&&declaration.Get("timing").Get("work_after_arrival").Flag()) ||
               execution.HostElapsed!= (execution.HostRequired&&declaration.Get("timing").Get("host_elapsed").Flag()))throw new InvalidDataException("Execution timing no longer matches content.");
            executions.Add(item.Execution,execution);running.Add(decision.Actor,execution);
        }
        RestoreSpatial(state.Spatial,decisions);
        if(state.Completions.Any(item=>!executions.ContainsKey(item.Execution)||!double.IsFinite(item.Due)||item.Due<0) ||
           state.Dispatched.Any(id=>!executions.ContainsKey(id)) || state.Cancellations.Any(id=>!executions.ContainsKey(id)) || state.Journeys.Keys.Any(id=>!executions.ContainsKey(id)))
            throw new InvalidDataException("Checkpoint contains an orphaned execution.");
        completions.Clear();foreach(var item in state.Completions)completions.Enqueue(executions[item.Execution],item.Due);
        dispatched.Clear();foreach(var id in state.Dispatched)dispatched.Enqueue(executions[id].Decision);
        cancellations.Clear();foreach(var id in state.Cancellations)cancellations.Enqueue(executions[id].Decision);
        receipts.Clear();foreach(var receipt in state.Receipts)receipts.Enqueue(receipt);
        actionOutcomes.Clear();foreach(var outcome in state.ActionOutcomes??[])actionOutcomes.Enqueue(outcome);
        var actionIds=actors.Values.SelectMany(actor=>actor.Declaration.Get("actions").Items().Select(action=>action.Get("id").Text())).ToHashSet(StringComparer.Ordinal);
        actionCooldowns.Clear();foreach(var item in state.ActionCooldowns??[])
        {
            if(!actionIds.Contains(item.Key)||!double.IsFinite(item.Value)||item.Value<0)throw new InvalidDataException("Invalid action cooldown checkpoint.");
            actionCooldowns.Add(item.Key,item.Value);
        }
        journeys.Clear();foreach(var item in state.Journeys)journeys.Add(item.Key,PathJourney.RestoreState(item.Value));
        placements.Clear();foreach(var item in state.Placements)
        {
            var p=item.Value;if(!actors.ContainsKey(item.Key)||!double.IsFinite(p.X)||!double.IsFinite(p.Y)||!double.IsFinite(p.Z)||!double.IsFinite(p.Yaw)||!double.IsFinite(p.Time)||p.Time<0)throw new InvalidDataException("Invalid checkpoint placement.");
            placements.Add(item.Key,p);
        }
        foreach(var item in state.Strategies){strategyBindings[item.Key].LastAttemptRevision=item.Value.LastAttemptRevision;strategyBindings[item.Key].Activity.RestoreState(item.Value.Activity);}
        completedStrategies.Clear();foreach(var item in state.CompletedStrategies)
        {
            if(!executions.ContainsKey(item.Execution)||item.Revision<0||item.Choices is null || !completedStrategies.TryAdd(item.Execution,(item.Revision,item.Choices)))
                throw new InvalidDataException("Invalid completed strategy checkpoint.");
        }
        conversation=null;conversationHolds.Clear();conversationLocks.Clear();
        if(state.Conversation is { } saved)
        {
            if(saved.State.Id>state.NextConversation || saved.Declaration.ValueKind!=JsonValueKind.Object || saved.Holds is null || saved.Locks is null ||
               saved.Locks.Length!=2 || saved.Locks.Distinct(StringComparer.Ordinal).Count()!=2 ||
               !saved.Locks.Contains(saved.State.Initiator,StringComparer.Ordinal) || !saved.Locks.Contains(saved.State.Target,StringComparer.Ordinal) ||
               saved.Locks.Any(id=>!actors.ContainsKey(id)) || saved.Holds.Any(pair=>!saved.Locks.Contains(pair.Key,StringComparer.Ordinal)||!double.IsFinite(pair.Value)||pair.Value<0||pair.Value>Clock.Now))
                throw new InvalidDataException("Invalid conversation locks checkpoint.");
            conversation=ConversationSession.RestoreState(saved.State,saved.Declaration);conversationDeclaration=saved.Declaration.Clone();
            foreach(var item in saved.Holds)conversationHolds.Add(item.Key,item.Value);conversationLocks.UnionWith(saved.Locks);
        }
        logicalDoors.Clear();foreach(var item in state.Doors)ReportDoor(item.Key,item.Value);
        nextConversation=state.NextConversation;nextEvidence=state.NextEvidence;actorCursor=state.ActorCursor;strategyCursor=state.StrategyCursor;
        RestoreMechanics(state.Mechanics,executions.Keys.ToHashSet());
    }
    public void ConfigureStrategy(string source,JsonElement declaration)
    {
        var actor=declaration.Get("actor").Text();var action=declaration.Get("action").Text();
        if(!actors.TryGetValue(actor,out var client)||action.Length==0||strategyBindings.Count>=16)
            throw new InvalidDataException("Invalid timed strategy actor, action or budget.");
        var data=DvScriptReader.Parse(source).GetProperty("data");
        var profileId=declaration.Get("profile").Text(actor);
        var profile=data.GetProperty("case_strategy").GetProperty("actors").EnumerateArray().Single(x=>x.GetProperty("actor").GetString()==profileId);
        var mappings=data.GetProperty("case_mind_evidence").GetProperty("mappings");
        var completion=declaration.Get("completion_output");
        var binding=new StrategyBinding(actor,new StrategyActivity(client,CaseTheory.Parse(source),profile,mappings,contentTimeUnit,declaration.Get("activity_evidence")),FieldKey.Read(declaration.Get("output"),actor),completion.ValueKind==JsonValueKind.Object?FieldKey.Read(completion,actor):null);
        if(!strategyBindings.TryAdd(action,binding))throw new InvalidDataException("Duplicate timed strategy action: "+action);
    }
    public object? LastDeduction { get; private set; }
    public void ConfigureDeduction(string source,JsonElement declaration)
    {
        var action=declaration.Get("action").Text();
        if(action.Length==0 || !deductions.TryAdd(action,(CaseTheory.Parse(source),declaration.Clone())))
            throw new InvalidDataException("Duplicate or missing deduction action.");
    }
    public PhysicalPlacement? Placement(string actor)=>placements.GetValueOrDefault(actor);
    public bool BeginJourney(Decision decision,IEnumerable<PathPoint> route)
    {
        if(!Current(decision,out var execution)||!execution!.HostRequired||execution.CancelRequested||journeys.ContainsKey(decision.Execution))return false;
        var points=route.ToArray();
        if(!placements.TryGetValue(decision.Actor,out var location)||points.Length<2 ||
           points[0].DistanceTo(new(location.X,location.Y,location.Z))>0.25)return false;
        journeys.Add(decision.Execution,new PathJourney(points));
        return true;
    }
    public JourneyView AdvanceJourney(Decision decision,double elapsed,double speed)
    {
        if(!Current(decision,out var execution)||execution!.CancelRequested||!journeys.TryGetValue(decision.Execution,out var journey))
            throw new InvalidOperationException("Journey is not active.");
        journey.Advance(elapsed,speed);return new(journey.Position,journey.Distance,journey.Length,journey.Arrived);
    }
    public PathPoint PreviewJourney(Decision decision,double elapsed,double speed)
    {
        if(!Current(decision,out var execution)||execution!.CancelRequested||!journeys.TryGetValue(decision.Execution,out var journey))
            throw new InvalidOperationException("Journey is not active.");
        return journey.Preview(elapsed,speed);
    }
    public bool EndJourney(Decision decision)
    {
        if(!Current(decision,out _)||!journeys.Remove(decision.Execution))return false;
        return true;
    }
    public void ReportPlacement(string actor,double x,double y,double z,double yaw,string room)
    {
        if(!actors.ContainsKey(actor))throw new InvalidOperationException("Unknown physical actor.");
        if(!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(z)||!double.IsFinite(yaw))
            throw new ArgumentOutOfRangeException(nameof(x));
        var hadPrior=placements.TryGetValue(actor,out var prior);
        var roomChanged=!hadPrior||prior!.Room!=room;
        placements[actor]=new(x,y,z,yaw,room,Clock.Now);
        var radiusChanged=hadPrior && presenceGroups.Any(group=>group.Center is not null && group.Members.Contains(actor,StringComparer.Ordinal)
            && InRadius(prior!,group)!=InRadius(placements[actor],group));
        if(roomChanged||radiusChanged)UpdatePresenceGroups();
    }
    public void ConfigurePresenceGroups(JsonElement declarations)
    {
        foreach(var declaration in declarations.Items())
        {
            if(presenceGroups.Count>=32)throw new InvalidDataException("Presence group budget exceeded.");
            var members=declaration.Get("members").Items().Select(x=>x.Text()).ToArray();
            var rooms=declaration.Get("rooms").Items().Select(x=>x.Text()).ToHashSet(StringComparer.Ordinal);
            if(members.Length is <1 or >32 || members.Distinct(StringComparer.Ordinal).Count()!=members.Length || members.Any(id=>!actors.ContainsKey(id)) || rooms.Count is <1 or >32)
                throw new InvalidDataException("Invalid presence group members or room budget.");
            var center=declaration.Get("center").Items().Select(x=>x.Number(double.NaN)).ToArray();
            var radius=declaration.Get("radius_m").Number(0);
            if(center.Length!=0 && (center.Length!=3 || center.Any(x=>!double.IsFinite(x)) || !double.IsFinite(radius) || radius<=0))
                throw new InvalidDataException("Invalid presence group radius.");
            var mode=declaration.Get("mode").Text("latched");
            if(mode is not ("latched" or "current"))throw new InvalidDataException("Invalid presence group mode.");
            presenceGroups.Add(new(members,rooms,FieldKey.Read(declaration.Get("result"),"world"),center.Length==0?null:center,radius,mode=="latched"));
        }
        UpdatePresenceGroups();
    }
    private void UpdatePresenceGroups()
    {
        foreach(var group in presenceGroups)
        {
            var old=World.Read(group.Result);
            if(group.Latch && old is {Known:true} && old.Value.ValueKind==JsonValueKind.True)continue;
            var present=group.Members.All(id=>placements.TryGetValue(id,out var place)&&group.Rooms.Contains(place.Room)&&InRadius(place,group));
            if(old is {Known:true,Conflicted:false} && old.Value.ValueKind==(present?JsonValueKind.True:JsonValueKind.False))continue;
            Rules.Changed(World.Apply(new Dictionary<FieldKey,Fact> { [group.Result]=new(JsonSerializer.SerializeToElement(present),true,false,1,ContentTime,0) }));
        }
    }
    private static bool InRadius(PhysicalPlacement place,PresenceGroup group)=>group.Center is null ||
        Math.Pow(place.X-group.Center[0],2)+Math.Pow(place.Y-group.Center[1],2)+Math.Pow(place.Z-group.Center[2],2)<=group.Radius*group.Radius;
    public double ContentTime => Clock.Now / contentTimeUnit;
    public void ConfigureObservationHorizon(double gameMinutes)
    {
        if(!double.IsFinite(gameMinutes)||gameMinutes<=ContentTime)throw new InvalidDataException("Invalid observation time boundary.");
        observationHorizon=gameMinutes*contentTimeUnit;
    }

    public ConversationView? Conversation => conversation?.View;
    public JsonElement ConversationDeclaration => conversation is null?default:conversationDeclaration.Clone();
    public bool ConversationAvailable(string initiator,JsonElement declaration)
    {
        if(Incapacitated(initiator) || Incapacitated(declaration.Get("target").Text()))return false;
        var precondition=declaration.Get("precondition");
        var id=declaration.Get("id").Text();
        if(precondition.ValueKind!=JsonValueKind.Undefined)
        {
            var key=id+"/initiator/"+initiator;
            if(!conversationGates.TryGetValue(key,out var gate))conversationGates[key]=gate=Condition.Compile(precondition,initiator);
            if(gate.Evaluate(actors[initiator].Recall)!=Truth.True)return false;
        }
        var targetCondition=declaration.Get("target_precondition");
        if(targetCondition.ValueKind==JsonValueKind.Undefined)return true;
        var target=declaration.Get("target").Text();
        if(!actors.TryGetValue(target,out var targetActor))return false;
        var targetKey=id+"/target/"+target;
        if(!conversationGates.TryGetValue(targetKey,out var targetGate))conversationGates[targetKey]=targetGate=Condition.Compile(targetCondition,target);
        return targetGate.Evaluate(targetActor.Recall)==Truth.True;
    }
    public ConversationView StartConversation(string initiator,string target,JsonElement declaration)
    {
        if(!ConversationAvailable(initiator,declaration))throw new InvalidOperationException("Conversation knowledge condition is not satisfied.");
        if(conversation is not null || !actors.ContainsKey(initiator) || !actors.ContainsKey(target) ||
           (running.TryGetValue(initiator,out var initiatorAction) && initiatorAction.CancelRequested) ||
           (running.TryGetValue(target,out var targetAction) && targetAction.CancelRequested))
            throw new InvalidOperationException("Conversation participants unavailable.");
        var next=new ConversationSession(++nextConversation,initiator,target,declaration);
        conversationDeclaration=declaration.Clone();
        foreach(var actor in new[]{initiator,target})
        {
            conversationLocks.Add(actor);
            Scheduler.Hold(actor,true);
        }
        // The target may finish its current movement while the initiator follows.
        // Its action is physically held only after the host confirms alignment.
        conversationHolds.Add(initiator,Clock.Now);
        conversation=next;
        return next.View;
    }
    public bool AlignConversation(long id)
    {
        if(conversation is not { } current || current.Id!=id || !current.Aligned(Clock.Now))return false;
        conversationHolds.Add(current.Target,Clock.Now);
        return true;
    }
    public bool AdvanceConversation(long id,int line)
    {
        if(conversation is not { } current || current.Id!=id)return false;
        var before=current.View;
        if(!current.Advance(line,Clock.Now))return false;
        var authored=conversationDeclaration.Get("lines").Items().ElementAt(line);
        foreach(var share in authored.Get("shares").Items())
        {
            var sender=before.Line!.Speaker;
            var receiver=sender==current.Initiator?current.Target:current.Initiator;
            var from=FieldKey.Read(share.Get("from"),sender);
            var to=FieldKey.Read(share.Get("to"),receiver);
            if(actors[sender].Recall(from) is not {Known:true,Conflicted:false} fact)continue;
            var sourceIds=actors[sender].Evidence.Where(item=>item.Key==from).Select(item=>item.Id).Distinct().ToArray();
            actors[receiver].ReceiveDirected(new Observation("dialogue/"+ ++nextEvidence,to,fact.Value,fact.Confidence,ContentTime,
                InformationOrigin.Communication,JsonSerializer.SerializeToElement(new {conversation=id,line,sender,from,sourceIds})));
        }
        return true;
    }
    public bool InterruptConversation(long id)
    {
        if(conversation is not { } current || current.Id!=id || !current.Interrupt())return false;
        return true;
    }
    public bool CompleteConversation(long id)
    {
        if(conversation is not { } current || current.Id!=id || current.Phase is not ("finished" or "interrupted"))return false;
        ReleaseConversation();return true;
    }
    private void ReleaseConversation()
    {
        foreach(var (actor,heldAt) in conversationHolds)
        {
            if(running.TryGetValue(actor,out var execution))
            {
                var duration=Clock.Now-heldAt;
                if(execution.WorkStartedAt.HasValue)execution.WorkStartedAt+=duration;
                if(double.IsFinite(execution.DueAt))execution.DueAt+=duration;
                if(execution.CompletionDeferred)
                {
                    execution.CompletionDeferred=false;
                    completions.Enqueue(execution,Math.Max(Clock.Now,execution.DueAt));
                }
            }
        }
        foreach(var actor in conversationLocks)
        {
            Scheduler.Hold(actor,false);
            if(!actors[actor].IsObserving && !actors[actor].IsUpdating && actors[actor].PendingInformation>0)
                actors[actor].BeginRefresh(RefreshCause.Deliberate);
        }
        conversationLocks.Clear();
        conversationHolds.Clear();conversation=null;
    }

    public RuntimeSession(JsonElement world, JsonElement sessionPolicy, JsonElement scheduling,
        JsonElement clockPolicy, JsonElement residency, double initialTime)
    {
        World.Initialize(world.Get("objects"));
        inferenceBudget = sessionPolicy.Get("inference_rules_per_slice").GetInt32();
        workBudget = scheduling.Get("evaluations_per_slice").GetInt32();
        sliceMilliseconds = scheduling.Get("milliseconds_per_slice").Number(double.NaN);
        contentTimeUnit = sessionPolicy.Get("content_time_unit_seconds").Number(1);
        if (!double.IsFinite(contentTimeUnit) || contentTimeUnit <= 0) throw new InvalidDataException("Invalid content time unit.");
        if (inferenceBudget < 1 || workBudget < 1) throw new InvalidDataException("Positive session work budget required.");
        Scheduler = new(scheduling,PositionEligible,(_,action)=>TimeEligible(action));
        Clock = new(clockPolicy, initialTime);
        Rooms = new(residency);
        Rules = new(World, contentTimeUnit);
        observationKinds = sessionPolicy.Get("observation_kinds").Items().Select(x => x.Text()).ToHashSet(StringComparer.Ordinal);
        if (sessionPolicy.Get("clock_ref").ValueKind == JsonValueKind.Object)
            clockField = FieldKey.Read(sessionPolicy.Get("clock_ref"), "world");
    }

    public ActorClient RegisterActor(JsonElement declaration, JsonElement attention, IEnumerable<ActionCandidate> candidates, bool autonomous = true)
    {
        var actor = new ActorClient(declaration, attention);
        if (actors.ContainsKey(actor.Id)) throw new InvalidDataException("Duplicate actor: " + actor.Id);
        Scheduler.Register(actor, candidates, Clock.Now, autonomous);
        actors.Add(actor.Id, actor);
        foreach(var sensor in declaration.Get("perceptions").Items())
            sensoryEvents.Add((actor.Id,sensor.Clone(),Condition.Compile(sensor.Get("condition"),World.Subject),Condition.Compile(sensor.Get("knowledge_condition"),actor.Id),sensor.Get("refs").Items().Select(r=>FieldKey.Read(r,World.Subject)).ToArray()));
        var placement=declaration.Get("placement");
        if(placement.ValueKind==JsonValueKind.Object)
        {
            var position=placement.Get("position").Items().Select(value=>value.Number(double.NaN)).ToArray();
            if(position.Length!=3)throw new InvalidDataException("Physical placement requires three coordinates.");
            ReportPlacement(actor.Id,position[0],position[1],position[2],placement.Get("yaw").Number(),placement.Get("room").Text());
        }
        actorOrder = actors.Keys.ToArray();
        return actor;
    }

    private bool PositionEligible(string actor,ActionCandidate action)
    {
        if(Clock.Now>=observationHorizon)return false;
        if(ContentTime<actors[actor].Declaration.Get("wake_at_game_minutes").Number())return false;
        foreach(var threshold in action.Declaration.Get("need_below").Fields())
            if(!Needs(actor).TryGetValue(threshold.Name,out var value)||value>=threshold.Value.Number())return false;
        foreach(var threshold in action.Declaration.Get("need_above").Fields())
            if(!Needs(actor).TryGetValue(threshold.Name,out var value)||value<=threshold.Value.Number())return false;
        if(Incapacitated(actor))return false;
        if(!KnowledgeEligible(action))return false;
        if(!MechanicEligible(actor,action))return false;
        if(strategyBindings.TryGetValue(action.Id,out var strategy) && strategy.LastAttemptRevision==actors[actor].KnowledgeRevision)return false;
        if(!TimeEligible(action))return false;
        var minimum=action.Declaration.Get("minimum_travel_distance_m");
        if(minimum.ValueKind==JsonValueKind.Undefined)return true;
        var distance=minimum.Number(double.NaN);
        var coordinates=action.Declaration.Get("motor").Get("target").Items().Select(value=>value.Number(double.NaN)).ToArray();
        if(!double.IsFinite(distance)||distance<=0||coordinates.Length!=3||coordinates.Any(value=>!double.IsFinite(value)))
            throw new InvalidDataException("Invalid minimum travel distance or target: "+action.Id);
        if(!placements.TryGetValue(actor,out var placement))return false;
        var x=placement.X-coordinates[0];var y=placement.Y-coordinates[1];var z=placement.Z-coordinates[2];
        return x*x+y*y+z*z>=distance*distance;
    }

    private bool TimeEligible(ActionCandidate action)
    {
        if(actionCooldowns.TryGetValue(action.Id,out var ready)&&ContentTime<ready)return false;
        var notBefore=action.Declaration.Get("not_before_game_minutes");
        if(notBefore.ValueKind!=JsonValueKind.Undefined && ContentTime<notBefore.Number(double.PositiveInfinity))return false;
        var notAfter=action.Declaration.Get("not_after_game_minutes");
        if(notAfter.ValueKind!=JsonValueKind.Undefined && ContentTime>notAfter.Number(double.NegativeInfinity))return false;
        return true;
    }

    private bool KnowledgeEligible(ActionCandidate action)
    {
        foreach(var guard in action.Declaration.Get("knowledge_guards").Items())
        {
            var id=guard.Get("actor").Text();
            if(!actors.TryGetValue(id,out var client))throw new InvalidDataException("Unknown knowledge guard actor: "+id);
            var key=FieldKey.Read(guard.Get("ref"),id);
            var known=client.View().Knowledge.TryGetValue(key,out var fact) && fact.Known && !fact.Conflicted && fact.Value.ValueKind==JsonValueKind.True;
            var mode=guard.Get("mode").Text();
            if(mode=="known_true" && !known || mode=="not_known_true" && known)return false;
            if(mode is not ("known_true" or "not_known_true"))throw new InvalidDataException("Invalid knowledge guard mode.");
        }
        return true;
    }

    public ClientView View(string actor) => actors[actor].View();
    public IReadOnlyList<Evidence> Evidence(string actor)=>actors[actor].Evidence;
    public object DecisionAudit(Decision decision)
    {
        var client=actors[decision.Actor];
        return new {actor=decision.Actor,action=decision.Action.Id,execution=decision.Execution,label=decision.Action.Declaration.Get("label").Text(),
            intent=decision.Intent,needs=Needs(decision.Actor),candidate_scores=decision.Options??[],goal_planning=Scheduler.GoalAudit(decision.Actor),failure_memory=Scheduler.FailureAudit(decision.Actor,Clock.Now),
            recognition=decision.Action.Recognition.Evaluate(client.Recall).ToString(),predicted_readiness=decision.Action.Gate.Evaluate(client.Recall).ToString(),
            knowledge_basis=decision.Action.Gate.Inputs.Concat(decision.Action.Recognition.Inputs).Distinct().Select(key=>new {key,fact=client.Recall(key),
                sources=client.Evidence.Where(item=>item.Key==key&&client.Recall(key) is {Known:true} fact&&Values.Same(item.Value,fact.Value)&&item.Time==fact.ObservedAt&&item.Confidence==fact.Confidence)
                    .Select(item=>new {id=item.Id,time=item.Time,method=item.Method,source=item.Source}).ToArray()}).ToArray()};
    }
    public int PendingInformation(string actor)=>actors[actor].PendingInformation;
    public int EvidenceCount(string actor)=>actors[actor].Evidence.Count;
    public void ConfigureBindings(JsonElement bindings) => perception = new(bindings);
    public void ConfigureHostPhysicalBindings(JsonElement bindings)
    {
        foreach(var binding in bindings.Items())hostPhysicalBindings.Add(FieldKey.Read(binding,"world"));
    }
    public void ReportPhysicalBoolean(FieldKey key,bool value)
    {
        if(!hostPhysicalBindings.Contains(key))throw new InvalidOperationException("Physical field is not host-bound.");
        var before=World.Read(key);
        if(before is {Known:true,Conflicted:false} && (before.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) && before.Value.GetBoolean()==value)return;
        Rules.Changed(World.Apply(new Dictionary<FieldKey,Fact>{{key,new(JsonSerializer.SerializeToElement(value),true,false,1,ContentTime,0)}}));
    }
    public void ConfigureIncapacitation(string actor,JsonElement reference)
    {
        if(!actors.ContainsKey(actor) || incapacitation.ContainsKey(actor))
            throw new InvalidDataException("Invalid incapacitation actor: "+actor);
        var key=FieldKey.Read(reference,World.Subject);
        var value=World.Read(key);
        if(value is not {Known:true,Conflicted:false} || value.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("Incapacitation requires a declared objective boolean: "+actor);
        incapacitation.Add(actor,key);
    }
    public bool Incapacitated(string actor)=>incapacitation.TryGetValue(actor,out var key) &&
        World.Read(key) is {Known:true,Conflicted:false} value && value.Value.ValueKind==JsonValueKind.True;
    public void ConfigureReservations(JsonElement slots)
    {
        foreach(var slot in slots.Items())Reservations.Define(slot.Get("id").Text(),slot.Get("capacity").GetInt32());
    }
    public IReadOnlyDictionary<string,double> Needs(string actor)=>Scheduler.Needs(actor,Clock.Now);
    public object PlayerPaths(string actor)=>Scheduler.Paths(actor,Clock.Now);
    public DecisionIntent? ActiveIntent(string actor)=>running.TryGetValue(actor,out var execution)?execution.Decision.Intent:null;
    public IEnumerable<(string Actor,AffectEvent Event)> DrainAffectEvents()=>Scheduler.DrainAffectEvents();
    public object Affect(string actor)=>Scheduler.Affect(actor);
    public void SetAutonomy(string actor,bool enabled)=>Scheduler.SetAutonomy(actor,enabled);
    public DriveEvaluation EvaluateDrive(string actor,string action)=>Scheduler.EvaluateDrive(actor,action,Clock.Now);
    public void ReconfigureDrives(string actor,JsonElement policy)=>Scheduler.ReconfigureDrives(actor,policy,Clock.Now);
    public Decision Submit(string actor, string action)
    {
        var decision = Scheduler.Submit(actor, action, Clock.Now, enqueue: false);
        Execute(decision);
        return decision;
    }
    public bool Receive(string actor, Observation observation) => actors[actor].Receive(observation);
    // Called only after the host reports an actual local perception. Reception is
    // filtered and buffered; it does not itself update the actor's knowledge.
    public bool ReceivePhysicalObservation(string actor, FieldKey physical, string source)
    {
        var local=perception.Project(actor,physical);
        var fact=World.Read(physical);
        if(local is null || fact is not { Known:true, Conflicted:false } || perception.Project(actor,fact.Value) is not { } value)return false;
        return actors[actor].Receive(new Observation("perception/"+ ++nextEvidence,local.Value,value,fact.Confidence,ContentTime,
            InformationOrigin.Passive,JsonSerializer.SerializeToElement(new {actor,source})));
    }
    public string BeginObservation(string actor, IEnumerable<FieldKey> focus)
    {
        if (running.ContainsKey(actor) || conversationLocks.Contains(actor)) throw new InvalidOperationException("Actor is occupied.");
        return actors[actor].BeginObservation(focus);
    }
    public void EndObservation(string actor, string token) => actors[actor].EndObservation(token);
    public void Refresh(string actor, RefreshCause cause)
    {
        if (running.ContainsKey(actor) || conversationLocks.Contains(actor)) throw new InvalidOperationException("Actor is occupied.");
        actors[actor].BeginRefresh(cause);
    }

    private void Execute(Decision decision)
    {
        if(Incapacitated(decision.Actor))
        {
            var attempt=CaptureStartAttempt(decision);
            Scheduler.Cancel(decision,Clock.Now);
            PublishReceipt(decision,false,"actor_incapacitated",startAttempt:attempt);
            return;
        }
        try { StartExecution(decision); }
        catch
        {
            Reservations.Release(new(decision.Actor,decision.Execution));
            if(running.Remove(decision.Actor,out var execution))FinishObservation(execution);
            actors[decision.Actor].FocusAction([]);
            Scheduler.Cancel(decision,Clock.Now);
            throw;
        }
    }

    private void StartExecution(Decision decision)
    {
        if (!Scheduler.Owns(decision) || decision.StartedAt > Clock.Now || running.ContainsKey(decision.Actor)) throw new InvalidOperationException("Actor cannot start another action.");
        // Actor access and objective validation stay separate. Start conditions never
        // grant knowledge of an unobserved objective failure.
        var declaration = decision.Action.Declaration;
        var gate = declaration.Get("objective_gate").ValueKind == JsonValueKind.Object
            ? Condition.Compile(declaration.Get("objective_gate"), World.Subject) : new Condition.Constant(Truth.True);
        var effects = declaration.Get("effects").Items().Select(x => Effect.Compile(x, World.Subject)).ToArray();
        var postcondition = declaration.Get("objective_postcondition").ValueKind == JsonValueKind.Object
            ? Condition.Compile(declaration.Get("objective_postcondition"), World.Subject) : null;
        var execution = new Execution(decision, effects, gate, postcondition, declaration.Get("requires_host_completion").Flag());
        execution.WorkAfterArrival=execution.HostRequired&&declaration.Get("timing").Get("work_after_arrival").Flag();
        execution.HostElapsed=execution.HostRequired&&declaration.Get("timing").Get("host_elapsed").Flag();
        if(execution.WorkAfterArrival&&execution.HostElapsed)throw new InvalidDataException("An execution must select work timing or physical elapsed timing.");
        if(execution.WorkAfterArrival||execution.HostElapsed)execution.DueAt=double.PositiveInfinity;
        else execution.WorkStartedAt=decision.StartedAt;
        var resourceIds=declaration.Get("resource_reservations").Items().Select(item=>item.Text()).ToArray();
        if(!Reservations.TryAcquire(new(decision.Actor,decision.Execution),resourceIds))
        {
            var attempt=CaptureStartAttempt(decision);
            Scheduler.Cancel(decision,Clock.Now);
            PublishReceipt(decision,false,"standing_resource_busy",blockedResources:Reservations.Blocked(decision.Actor,resourceIds),startAttempt:attempt);
            return;
        }
        var focus = decision.Action.Gate.Inputs.Concat(decision.Action.Recognition.Inputs).Concat(declaration.Get("grants").Items()
            .Concat(declaration.Get("failure_grants").Items()).Select(x => perception.Project(decision.Actor, FieldKey.Read(x, decision.Actor)))
            .Where(x => x.HasValue).Select(x => x!.Value)).ToArray();
        if(declaration.Get("kind").Text()=="deduce" && deductions.TryGetValue(decision.Action.Id,out var configuredDeduction))
            focus=focus.Concat(configuredDeduction.declaration.Get("results").Items().Select(item=>FieldKey.Read(item.Get("ref"),decision.Actor))).ToArray();
        if(strategyBindings.TryGetValue(decision.Action.Id,out var configuredStrategy))
        {
            focus=focus.Append(configuredStrategy.Output).ToArray();
            if(configuredStrategy.Completion is { } completion)focus=focus.Append(completion).ToArray();
        }
        focus=focus.Concat(MechanicFocus(decision.Action)).Distinct().ToArray();
        actors[decision.Actor].FocusAction(focus);
        if (observationKinds.Contains(declaration.Get("kind").Text()))
            execution.ObservationToken = actors[decision.Actor].BeginObservation(focus);
        running.Add(decision.Actor, execution);
        var spatial=BeginSpatialNavigation(execution);
        if(!execution.WorkAfterArrival&&!spatial&&strategyBindings.TryGetValue(decision.Action.Id,out var immediateStrategy))
            immediateStrategy.Activity.Begin(Clock.Now,decision.Action.Duration/contentTimeUnit);
        if (!execution.HostRequired&&!spatial) completions.Enqueue(execution, execution.DueAt);
        dispatched.Enqueue(decision);
    }

    public bool BeginWork(Decision decision)
    {
        if(conversationHolds.ContainsKey(decision.Actor)||!Current(decision,out var execution)||execution!.CancelRequested||!execution.WorkAfterArrival||execution.WorkStartedAt.HasValue)return false;
        execution.WorkStartedAt=Clock.Now;
        execution.DueAt=Clock.Now+decision.Action.Duration;
        if(strategyBindings.TryGetValue(decision.Action.Id,out var strategy))
        {
            if(strategy.Actor!=decision.Actor)throw new InvalidDataException("Timed strategy actor mismatch.");
            strategy.Activity.Begin(Clock.Now,decision.Action.Duration/contentTimeUnit);
        }
        return true;
    }

    private bool Current(Decision decision,out Execution? execution)=>running.TryGetValue(decision.Actor,out execution)&&ReferenceEquals(execution.Decision,decision);

    public bool HostCompleted(Decision decision)
    {
        if(conversationHolds.ContainsKey(decision.Actor)||!Current(decision,out var execution)||execution!.CancelRequested||execution.WorkAfterArrival&&!execution.WorkStartedAt.HasValue)return false;
        if (execution.HostCompleted) return true;
        if(execution.HostElapsed)execution.DueAt=Clock.Now;
        execution.HostCompleted = true;
        if (execution.HostRequired) completions.Enqueue(execution, Math.Max(Clock.Now, execution.DueAt));
        return true;
    }

    public ActivityView? Activity(string actor)
    {
        if(!running.TryGetValue(actor,out var execution))return null;
        if(spatialNavigation.TryGetValue(execution.Decision.Execution,out var navigation))
            return new(actor,execution.Decision.Action.Id,execution.Decision.Execution,execution.CancelRequested?"cancelling":navigation.Phase,
                execution.WorkStartedAt,double.IsFinite(execution.DueAt)?execution.DueAt:null,false);
        return new(actor,execution.Decision.Action.Id,execution.Decision.Execution,
            execution.CancelRequested?"cancelling":execution.WorkAfterArrival?execution.WorkStartedAt.HasValue?"working":"preparing":execution.HostElapsed?"moving":"working",
            execution.WorkStartedAt,double.IsFinite(execution.DueAt)?execution.DueAt:null,execution.HostCompleted);
    }

    public bool WorkElapsed(Decision decision)
    {
        if(conversationHolds.ContainsKey(decision.Actor)||!Current(decision,out var execution)||!execution!.WorkStartedAt.HasValue||Clock.Now<execution.DueAt)return false;
        if(!strategyBindings.TryGetValue(decision.Action.Id,out var strategy))return true;
        if(completedStrategies.ContainsKey(decision.Execution))return true;
        var choices=strategy.Activity.Finish(Clock.Now);
        if(choices is null)return false;
        completedStrategies[decision.Execution]=(actors[decision.Actor].KnowledgeRevision,choices);
        return true;
    }
    public bool PhysicalActionPermitted(Decision decision)=>Current(decision,out var execution)&&!execution!.CancelRequested && TimeEligible(decision.Action) && KnowledgeEligible(decision.Action) && execution.CompletionGate.Evaluate(World.Read)==Truth.True;
    public bool Working(string actor)=>!conversationHolds.ContainsKey(actor)&&running.TryGetValue(actor,out var execution)&&!execution.CancelRequested&&execution.WorkStartedAt.HasValue&&
        (execution.WorkAfterArrival||spatialNavigation.TryGetValue(execution.Decision.Execution,out var nav)&&nav.Phase=="working");

    public bool RequestCancel(string actor)
    {
        if(conversationLocks.Contains(actor) && conversation is { } active)
        {
            InterruptConversation(active.Id);
            CompleteConversation(active.Id);
        }
        if(!running.TryGetValue(actor,out var execution))return false;
        if(execution.CancelRequested)return true;
        execution.CancelRequested=true;
        if(!execution.HostRequired||!execution.Dispatched)return ConfirmCancelled(execution.Decision);
        cancellations.Enqueue(execution.Decision);
        return true;
    }

    public bool ConfirmCancelled(Decision decision,bool retainOccupiedResources=false)
    {
        if(!Current(decision,out var execution)||!execution!.CancelRequested)return false;
        FinishObservation(execution);
        journeys.Remove(decision.Execution);
        spatialNavigation.Remove(decision.Execution);
        if(strategyBindings.TryGetValue(decision.Action.Id,out var strategy))strategy.Activity.Cancel();
        completedStrategies.Remove(decision.Execution);
        deductionSelections.Remove(decision.Execution);
        actors[decision.Actor].FocusAction([]);
        ReleaseResources(execution,retainOccupiedResources);
        Scheduler.Cancel(decision,Clock.Now);running.Remove(decision.Actor);
        PublishReceipt(decision,false,"cancelled");
        return true;
    }

    public bool HostFailed(Decision decision,string reason)
    {
        if(!Current(decision,out var execution))return false;
        if(execution!.CancelRequested)return ConfirmCancelled(decision);
        // A rejected arrival is an attempted action. Report its real boundary
        // failure instead of treating it as a voluntary player cancellation.
        // Actual path/geometry failures retain their original host reason.
        if(reason is "arrival_rejected" or "target_not_present")
        {
            if(!TimeEligible(decision.Action))reason="action_time_window_closed";
            else if(!KnowledgeEligible(decision.Action))reason="action_knowledge_changed";
            else if(execution.CompletionGate.Evaluate(World.Read)!=Truth.True)reason="objective_condition_failed";
        }
        var wasObserving=execution.ObservationToken is not null;
        journeys.Remove(decision.Execution);
        spatialNavigation.Remove(decision.Execution);
        if(strategyBindings.TryGetValue(decision.Action.Id,out var strategy))strategy.Activity.Cancel();
        completedStrategies.Remove(decision.Execution);
        deductionSelections.Remove(decision.Execution);
        FinishObservation(execution);
        Reservations.Release(new(decision.Actor,decision.Execution));
        Scheduler.Cancel(decision,Clock.Now);running.Remove(decision.Actor);
        if(!wasObserving)actors[decision.Actor].BeginRefresh(RefreshCause.FailedAction);
        actors[decision.Actor].FocusAction([]);
        PublishReceipt(decision,false,reason);
        return true;
    }

    private void FinishObservation(Execution execution)
    {
        if(execution.ObservationToken is not { } token)return;
        actors[execution.Decision.Actor].EndObservation(token);execution.ObservationToken=null;
    }

    private void ReleaseResources(Execution execution,bool retain)
    {
        var owner=new ReservationOwner(execution.Decision.Actor,execution.Decision.Execution);
        if(retain&&execution.Decision.Action.Declaration.Get("retain_occupied_resources").Flag())Reservations.Retain(owner);
        else Reservations.Release(owner);
    }

    public void VacatedRetainedResources(string actor)=>Reservations.Release(new(actor,0));

    public bool TryReceipt(out ActionReceipt? receipt) => receipts.TryDequeue(out receipt);
    public bool TryActionOutcome(out ActionOutcome? outcome)=>actionOutcomes.TryDequeue(out outcome);
    private ActionStartAttempt CaptureStartAttempt(Decision decision)=>new(decision.Options??[],EvaluateDrive(decision.Actor,decision.Action.Id),
        JsonSerializer.SerializeToElement(DecisionAudit(decision)));
    private void PublishReceipt(Decision decision,bool success,string reason,IEnumerable<FieldChange>? changes=null,IReadOnlyDictionary<string,double>? needsBefore=null,
        ActionInterruption? interruption=null,SavedFact[]? interruptionBasis=null,string[]? blockedResources=null,ActionStartAttempt? startAttempt=null)
    {
        var client=actors[decision.Actor];
        IEnumerable<FieldKey> Blockers(Condition condition)
        {
            if(condition is Condition.All all)return all.Items.Where(item=>item.Evaluate(World.Read)!=Truth.True).SelectMany(Blockers);
            return condition.Inputs;
        }
        var objective=decision.Action.Declaration.Get("objective_gate");
        var keys=objective.ValueKind==JsonValueKind.Object?Blockers(Condition.Compile(objective,World.Subject)).Where(key=>client.Recall(key) is {Known:true}):[];
        keys=keys.Concat(decision.Action.Declaration.Get("failure_grants").Items().Select(value=>FieldKey.Read(value,decision.Actor)));
        if(reason=="standing_resource_busy")keys=[];
        else if(!keys.Any())keys=decision.Action.Gate.Inputs;
        var learned=Scheduler.RecordOutcome(decision,success,reason,keys,Clock.Now,blockedResources);
        var narrative=success?CompleteNarrative(decision):Array.Empty<ActionNarrative>();
        receipts.Enqueue(new(decision.Actor,decision.Action.Id,Clock.Now,success,reason,decision.Execution,decision.Intent,interruption,narrative));
        var needs=Needs(decision.Actor);
        actionOutcomes.Enqueue(new(decision.Actor,decision.Action.Id,decision.Execution,decision.StartedAt,Clock.Now,success,reason,
            decision.Intent,changes?.ToArray()??[],new(needsBefore??needs,StringComparer.Ordinal),new(needs,StringComparer.Ordinal),interruption,interruptionBasis,narrative,learned,startAttempt));
    }
    public bool Available(string actor,string action)
    {
        return Scheduler.Available(actor,action);
    }
    public ActionCandidate Candidate(string actor,string action)=>Scheduler.Candidate(actor,action);
    public bool TryAction(out Decision? decision)
    {
        var count=dispatched.Count;
        while(count-->0 && dispatched.TryDequeue(out decision))
            if(Current(decision,out var execution)&&!execution!.CancelRequested)
            {
                if(conversationHolds.ContainsKey(decision.Actor)){dispatched.Enqueue(decision);continue;}
                execution.Dispatched=true;return true;
            }
        decision=null;return false;
    }
    public bool TryCancellation(out Decision? decision)
    {
        while(cancellations.TryDequeue(out decision))if(Current(decision,out var execution)&&execution!.CancelRequested)return true;
        decision=null;return false;
    }

    public void Step(double realSeconds, bool idle, bool paused, string? playerActor = null)
    {
        // Only an explicit simulation pause suspends the session. Opening UI or
        // displaying a spoken line does not request a simulation pause.
        if (paused) return;
        AdvanceSpatialNavigation();
        AdvanceMechanics();
        foreach(var sensor in sensoryEvents)
        {
            if(ContentTime<sensor.Data.Get("not_before_game_minutes").Number())continue;
            var client=actors[sensor.Actor];var id="perception/"+sensor.Data.Get("id").Text();
            var during=sensor.Data.Get("during_action").Text();
            if(during.Length>0&&!running.Values.Any(e=>e.Decision.Action.Id==during&&!e.CancelRequested))continue;
            var repeating=sensor.Data.Get("repeat_on_change").Flag();
            if(client.IsUpdating||!repeating&&client.Evidence.Any(e=>e.Id==id)||sensor.WorldGate.Evaluate(World.Read)!=Truth.True||sensor.KnowledgeGate.Evaluate(client.Recall)!=Truth.True)continue;
            foreach(var field in sensor.Fields)
            {
                var local=perception.Project(sensor.Actor,field);var fact=World.Read(field);
                if(local is null||fact is not {Known:true,Conflicted:false})continue;
                var sampleId=repeating?id+"/revision/"+fact.Revision:id;
                if(repeating&&client.Evidence.Any(item=>item.Id==sampleId&&item.Key==local.Value))continue;
                client.ReceiveSalient(new Observation(sampleId,local.Value,fact.Value,fact.Confidence,ContentTime,InformationOrigin.Passive,
                    JsonSerializer.SerializeToElement(new {actor=sensor.Actor,sensor=sensor.Data.Get("id").Text(),channel=sensor.Data.Get("channel").Text("sight"),label=sensor.Data.Get("label").Text(),physical_field=field,world_revision=fact.Revision})));
            }
        }
        Rules.Wake(Clock.Now);
        Rules.Step(ContentTime, workBudget, sliceMilliseconds);
        foreach(var actor in incapacitation.Keys)
            if(Incapacitated(actor) && (running.ContainsKey(actor) || conversationLocks.Contains(actor)))RequestCancel(actor);
        for (var n = 0; n < Math.Min(actorOrder.Length, workBudget); n++)
        {
            var actor = actors[actorOrder[actorCursor]];
            actorCursor = (actorCursor + 1) % actorOrder.Length;
            if (actor.IsUpdating) actor.ContinueRefresh(inferenceBudget);
            if(!running.ContainsKey(actor.Id) && !conversationLocks.Contains(actor.Id))actor.ProcessDirectedMessages();
        }
        if(strategyBindings.Count>0)
        {
            var entry=strategyBindings.ElementAt(strategyCursor++%strategyBindings.Count);
            if(entry.Value.Activity.Active && !conversationHolds.ContainsKey(entry.Value.Actor))
                entry.Value.Activity.Step(inferenceBudget,TimeSpan.FromMilliseconds(sliceMilliseconds));
        }
        var processed = 0;
        while (processed < workBudget && completions.TryPeek(out var execution, out var due))
        {
            if(!Current(execution.Decision,out var active)||!ReferenceEquals(active,execution)||execution.CancelRequested)
            {processed++;completions.Dequeue();continue;}
            if(conversationHolds.ContainsKey(execution.Decision.Actor))
            {processed++;completions.Dequeue();execution.CompletionDeferred=true;continue;}
            if(due<execution.DueAt)
            {processed++;completions.Dequeue();completions.Enqueue(execution,execution.DueAt);continue;}
            if(due>Clock.Now)break;
            if(strategyBindings.ContainsKey(execution.Decision.Action.Id)&&!WorkElapsed(execution.Decision))break;
            processed++;
            Commit(execution);
            completions.Dequeue();
        }
        // Drain decisions retained by an older checkpoint before admitting new work.
        if(Clock.Now<observationHorizon)
        {
            for (var n = 0; n < workBudget && Scheduler.TryTake(out var decision); n++) Execute(decision!);
            HasPendingDecisionWork=Scheduler.Step(Clock.Now,Execute).Pending;
        }
        else HasPendingDecisionWork=false;
        var next = completions.TryPeek(out _, out var nextDue) ? nextDue : double.PositiveInfinity;
        next = Math.Min(next, Rules.NextTime);
        next = Math.Min(next,NextMechanicTime());
        next = Math.Min(next,NextSpatialTime());
        next = Math.Min(next,Scheduler.NextDecisionTime);
        next = Math.Min(next,observationHorizon);
        // A decision is evaluated in bounded CPU slices. Those slices belong to
        // the same simulation instant: spending a renderer frame on candidate
        // scoring must not add poison exposure or delay a rescue in world time.
        // Even a decision with no eligible action completes its finite selection
        // and schedules the next poll, so an idle actor cannot hold time forever.
        var pending = Rules.Pending || HasPendingDecisionWork || next <= Clock.Now;
        var priorTime=Clock.Now;
        Clock.Step(realSeconds, idle, paused, pending, Math.Max(Clock.Now, next),playerActor is { } actorId&&Working(actorId),physicalClockInput||HasHostNavigation);
        var elapsed=Clock.Now-priorTime;
        ElapseSpatialNavigation(elapsed);
        if(elapsed>0 && accumulators.Count>0)
        {
            var writes=new Dictionary<FieldKey,Fact>();
            foreach(var item in accumulators)
            {
                var active=World.Read(item.Active);var present=World.Read(item.Present);var amount=World.Read(item.Amount);
                if(active is not {Known:true,Conflicted:false} || active.Value.ValueKind!=JsonValueKind.True ||
                   present is not {Known:true,Conflicted:false} || present.Value.ValueKind!=JsonValueKind.True ||
                   amount is not {Known:true,Conflicted:false})continue;
                writes[item.Amount]=new(JsonSerializer.SerializeToElement(amount.Value.Number()+elapsed*item.Rate),true,false,1,ContentTime,0);
            }
            if(writes.Count>0)Rules.Changed(World.Apply(writes));
        }
        if (clockField is { } key)
            Rules.Changed(World.Apply(new Dictionary<FieldKey, Fact>
            { [key] = new(JsonSerializer.SerializeToElement(ContentTime), true, false, 1, ContentTime, 0) }));
    }

    private void Commit(Execution execution)
    {
        var decision = execution.Decision;
        if (!Scheduler.Owns(decision) || Clock.Now < execution.DueAt) throw new InvalidOperationException("Invalid action commit.");
        var objectiveTruth=execution.CompletionGate.Evaluate(World.Read);
        var timeEligible=TimeEligible(decision.Action);var knowledgeEligible=KnowledgeEligible(decision.Action);
        var success = objectiveTruth==Truth.True&&timeEligible&&knowledgeEligible;
        var failureReason=objectiveTruth!=Truth.True?"objective_condition_failed":!timeEligible?"action_time_window_closed":!knowledgeEligible?"action_knowledge_changed":"action_postcondition_failed";
        var writes = new Dictionary<FieldKey, Fact>();
        var changes=new List<FieldChange>();
        Fact? Read(FieldKey key) => success ? writes.GetValueOrDefault(key) ?? World.Read(key) : World.Read(key);
        if (success)
        {
            try
            {
                foreach (var effect in execution.Effects) writes[effect.Key] = effect.Resolve(Read, ContentTime);
                if (execution.Postcondition is { } post && post.Evaluate(Read) != Truth.True) success = false;
            }
            catch (InvalidOperationException) { success = false;failureReason="action_effect_failed"; }
        }
        var client = actors[decision.Actor];
        if(strategyBindings.ContainsKey(decision.Action.Id) && (!completedStrategies.TryGetValue(decision.Execution,out var strategyResult) || strategyResult.Revision!=client.KnowledgeRevision))
        {success=false;failureReason="strategy_basis_changed";}
        var feedback = new List<Observation>();
        if(success && strategyBindings.TryGetValue(decision.Action.Id,out var binding) && completedStrategies.TryGetValue(decision.Execution,out var result))
        {
            var best=result.Choices.Where(choice=>choice.Ready).OrderByDescending(choice=>choice.Utility).FirstOrDefault();
            binding.LastAttemptRevision=result.Revision;
            LastStrategy=new {actor=decision.Actor,action=decision.Action.Id,knowledge_revision=result.Revision,choices=result.Choices,selected=best?.Action};
            RecordMechanic(decision,"strategy",best is not null,LastStrategy);
            if(best is not null)
            {
                feedback.Add(new Observation("strategy/"+(++nextEvidence),binding.Output,JsonSerializer.SerializeToElement(best.Action),1,ContentTime,InformationOrigin.ActionFeedback,
                    JsonSerializer.SerializeToElement(new {actor=decision.Actor,action=decision.Action.Id,knowledge_revision=result.Revision,utility=best.Utility,theory_hash=best.TheoryHash,evidence=best.EvidenceNodes})));
                if(binding.Completion is { } completed)
                    feedback.Add(new Observation("strategy/"+(++nextEvidence),completed,JsonSerializer.SerializeToElement(true),1,ContentTime,InformationOrigin.ActionFeedback,
                        JsonSerializer.SerializeToElement(new {actor=decision.Actor,action=decision.Action.Id,knowledge_revision=result.Revision})));
                if(binding.Publish)writes[binding.Output]=new(JsonSerializer.SerializeToElement(best.Action),true,false,1,ContentTime,0);
            }
            else {success=false;failureReason="strategy_no_feasible_option";}
        }
        if(success&&inlineDeductions.ContainsKey(decision.Action.Id))
        {success=EvaluateInlineDeduction(decision,feedback);if(!success)failureReason="deduction_unproved";}
        else if(success && decision.Action.Declaration.Get("kind").Text()=="deduce")
        {
            feedback.AddRange(EvaluateDeduction(client,decision,out var proved));
            if(!proved){success=false;failureReason="deduction_unproved";feedback.Clear();}
        }
        if(success&&decision.Action.Declaration.Get("suggestion").ValueKind==JsonValueKind.Object)
        {success=EvaluateSuggestion(decision,writes,feedback,changes);if(!success)failureReason="suggestion_rejected";}
        if(success&&decision.Action.Declaration.Get("incantation").ValueKind==JsonValueKind.Object)
        {success=EvaluateIncantation(decision,writes,feedback);if(!success)failureReason="incantation_precondition_failed";}
        var grants = decision.Action.Declaration.Get(success ? "grants" : "failure_grants");
        foreach (var grant in grants.Items())
        {
            var key = FieldKey.Read(grant, decision.Actor);
            var target = perception.Project(decision.Actor, key);
            if (target is null || Read(key) is not { Known: true, Conflicted: false } fact || perception.Project(decision.Actor, fact.Value) is not { } value) continue;
            var origin = !success ? InformationOrigin.ActionFailure : execution.ObservationToken is not null
                ? InformationOrigin.DeliberateObservation : InformationOrigin.ActionFeedback;
            var sample = new Observation("action/" + ++nextEvidence, target.Value, value, fact.Confidence, ContentTime, origin,
                JsonSerializer.SerializeToElement(new { actor = decision.Actor, action = decision.Action.Id,execution=decision.Execution,physical_field=key,
                    world_revision=success&&writes.ContainsKey(key)?(long?)null:fact.Revision }));
            if (execution.ObservationToken is not null || !success) client.Receive(sample);
            else feedback.Add(sample);
        }
        if (execution.ObservationToken is { } token)
        {foreach(var sample in feedback)client.Receive(sample);client.EndObservation(token);}
        else if (success||feedback.Count>0) client.BeginActionFeedback(feedback);
        else client.BeginRefresh(RefreshCause.FailedAction);
        if (success)
        {
            var committedChanges=World.Apply(writes);changes.AddRange(committedChanges);Rules.Changed(committedChanges);
            var handoffs=decision.Action.Declaration.Get("handoffs").Items().ToList();
            if(decision.Action.Declaration.Get("handoff").ValueKind==JsonValueKind.Object)handoffs.Add(decision.Action.Declaration.Get("handoff"));
            foreach(var handoff in handoffs)
            {
                if(handoff.Get("only_if").ValueKind==JsonValueKind.Object && Condition.Compile(handoff.Get("only_if"),World.Subject).Evaluate(World.Read)!=Truth.True)continue;
                var recipient=actors[handoff.Get("actor").Text()];
                foreach(var reference in handoff.Get("refs").Items().Concat(handoff.Get("result_refs").Items()))
                {
                    var key=FieldKey.Read(reference,decision.Actor);
                    var isResult=handoff.Get("result_refs").Items().Any(item=>FieldKey.Read(item,decision.Actor)==key);
                    var fresh=feedback.LastOrDefault(item=>item.Key==key);
                    var fact=isResult ? writes.GetValueOrDefault(key) : fresh is not null?new Fact(fresh.Value,true,false,fresh.Confidence,fresh.Time,0):client.Recall(key);
                    if(fact is not {Known:true,Conflicted:false})continue;
                    var sourceIds=client.Evidence.Where(item=>item.Key==key).Select(item=>item.Id).Concat(feedback.Where(item=>item.Key==key).Select(item=>item.Id)).Distinct().ToArray();
                    recipient.ReceiveDirected(new Observation("handoff/"+(++nextEvidence),key,fact.Value,fact.Confidence,ContentTime,
                        InformationOrigin.Communication,JsonSerializer.SerializeToElement(new {actor=decision.Actor,recipient=recipient.Id,action=decision.Action.Id,execution=decision.Execution,sourceIds,isResult})));
                }
                if(!running.ContainsKey(recipient.Id) && !recipient.IsObserving && !recipient.IsUpdating && recipient.PendingInformation>0)
                    recipient.BeginRefresh(RefreshCause.Deliberate);
            }
        }
        if(success)foreach(var broadcast in decision.Action.Declaration.Get("broadcast_observations").Items())
            {
                if(broadcast.Get("only_if").ValueKind==JsonValueKind.Object && Condition.Compile(broadcast.Get("only_if"),World.Subject).Evaluate(World.Read)!=Truth.True)continue;
                var recipient=actors[broadcast.Get("actor").Text()];
                foreach(var reference in broadcast.Get("refs").Items())
                {
                    var key=FieldKey.Read(reference,decision.Actor);
                    if(!grants.Items().Any(grant=>FieldKey.Read(grant,decision.Actor)==key))continue;
                    if(Read(key) is not {Known:true,Conflicted:false} fact)continue;
                    recipient.ReceiveDirected(new Observation("report/"+(++nextEvidence),key,fact.Value,fact.Confidence,ContentTime,
                        InformationOrigin.Communication,JsonSerializer.SerializeToElement(new {actor=decision.Actor,action=decision.Action.Id,execution=decision.Execution,recipient=recipient.Id,physical_field=key,world_revision=World.Read(key)?.Revision})));
                }
                if(!running.ContainsKey(recipient.Id) && !recipient.IsObserving && !recipient.IsUpdating && recipient.PendingInformation>0)
                    recipient.BeginRefresh(RefreshCause.Deliberate);
            }
        client.FocusAction([]);
        var needsBefore=Needs(decision.Actor);
        Scheduler.Complete(decision, Clock.Now, success,execution.HostElapsed||spatialNavigation.TryGetValue(decision.Execution,out var nav)&&nav.PureMovement);
        if(success&&decision.Action.Declaration.Get("cooldown_minutes").Number()>0)
            actionCooldowns[decision.Action.Id]=ContentTime+decision.Action.Declaration.Get("cooldown_minutes").Number();
        journeys.Remove(decision.Execution);
        spatialNavigation.Remove(decision.Execution);
        ReleaseResources(execution,success&&decision.Action.Declaration.Get("retain_occupied_resources").Flag());
        running.Remove(decision.Actor);
        if(success && decision.Action.Declaration.Get("rest_until_game_minutes").ValueKind==JsonValueKind.Number)
            Clock.RestUntil(decision.Action.Declaration.Get("rest_until_game_minutes").Number()*60);
        completedStrategies.Remove(decision.Execution);
        deductionSelections.Remove(decision.Execution);
        ActionInterruption? interruption=null;SavedFact[]? interruptionBasis=null;
        var opportunity=decision.Action.Declaration.Get("interruptible_opportunity");
        if(!success&&objectiveTruth==Truth.False&&timeEligible&&knowledgeEligible&&failureReason=="objective_condition_failed"&&changes.Count==0&&opportunity.ValueKind==JsonValueKind.Object)
        {
            var lost=Condition.Compile(opportunity.Get("lost_when"),World.Subject);var inputs=lost.Inputs.Distinct().ToArray();
            if(inputs.All(key=>World.Read(key) is {Known:true,Conflicted:false})&&lost.Evaluate(World.Read)==Truth.True)
            {
                interruption=new(opportunity.Get("reason").Text(),opportunity.Get("label").Text());
                interruptionBasis=inputs.Select(key=>new SavedFact(key,World.Read(key)!)).ToArray();
            }
        }
        PublishReceipt(decision,success,success?"completed":failureReason,changes,needsBefore,interruption,interruptionBasis);
    }

    private IEnumerable<Observation> EvaluateDeduction(ActorClient client,Decision decision,out bool proved)
    {
        if(!deductions.TryGetValue(decision.Action.Id,out var configured))throw new InvalidOperationException("No deduction theory configured for action.");
        var authored=configured.theory;
        var deductionDeclaration=configured.declaration;
        var scope=deductionDeclaration.Get("scope").Text();
        var original=authored.Scopes[scope];
        var evidence=new List<CaseAxiom>();
        foreach(var mapping in deductionDeclaration.Get("evidence").Items())
        {
            if(Condition.Compile(mapping.Get("when"),client.Id).Evaluate(client.Recall)!=Truth.True)continue;
            var name=mapping.Get("assumption").Text();
            var assumption=original.Axioms.Single(item=>item.Id==name);
            var sources=client.Evidence.Where(item=>Condition.Compile(mapping.Get("when"),client.Id).Inputs.Contains(item.Key)).Select(item=>item.Id).Distinct().ToArray();
            evidence.Add(new("observed/"+name,assumption.Literal,JsonSerializer.Serialize(new {actor=client.Id,sources})));
        }
        var grounded=authored.WithoutAssumptions(scope).WithEvidence(scope,evidence);
        var observations=new List<Observation>();
        var results=new List<object>();
        proved=true;
        foreach(var mapping in deductionDeclaration.Get("results").Items())
        {
            var query=mapping.Get("query").Text();
            var goal=original.Theorems.Single(item=>item.Id==query);
            var result=grounded.Prove(scope,goal.Conclusion);
            proved&=result.Status=="proved";
            results.Add(new {query,label=mapping.Get("label").Text(),unknown_label=mapping.Get("unknown_label").Text(),result.Status,result.Support,result.Refutation});
            if(result.Status!="proved")continue;
            observations.Add(new("proof/"+ ++nextEvidence,FieldKey.Read(mapping.Get("ref"),client.Id),
                JsonSerializer.SerializeToElement(true),1,ContentTime,InformationOrigin.ActionFeedback,
                JsonSerializer.SerializeToElement(new {actor=client.Id,action=decision.Action.Id,query,result.Support})));
        }
        LastDeduction=new {actor=client.Id,scope,knowledge_revision=client.KnowledgeRevision,evidence=evidence.ToArray(),results=results.ToArray()};
        RecordMechanic(decision,"deduction",proved,LastDeduction);
        return observations;
    }
}
}

// --- Source: RuntimeSessionSpatialPart.cs ---
namespace DuelView.Runtime
{
public sealed partial class RuntimeSession
{
    private sealed class SpatialNavigation(Decision decision,string from,string to,bool pureMovement)
    {
        internal readonly Decision Decision=decision;
        internal readonly string FromRoom=from,ToRoom=to;
        internal readonly bool PureMovement=pureMovement;
        internal string Authority="godot",Phase="approaching";
        internal double StartedAt=decision.StartedAt,Elapsed,RemainingDistance,Speed=2.4;
        internal double? DueAt;
        internal PathPoint? Target;
    }
    private readonly Dictionary<long,SpatialNavigation> spatialNavigation=[];
    private bool spatialAuthorityEnabled,physicalClockInput;
    private string visibleRoom="";
    public bool HasHostNavigation=>spatialNavigation.Values.Any(n=>n.Authority=="godot"&&n.Phase=="approaching"&&Current(n.Decision,out var run)&&!run!.CancelRequested);
    public string VisibleRoom=>visibleRoom;

    /// <summary>The host declares residency, never actor location. Leaving a
    /// streamed room transfers only remaining navigation work to Core.</summary>
    public void ConfigureVisibleRoom(string room)
    {
        if(room is null)throw new ArgumentNullException(nameof(room));
        spatialAuthorityEnabled=true;visibleRoom=room;
        foreach(var nav in spatialNavigation.Values)
        {
            if(nav.Phase!="approaching"||!Current(nav.Decision,out _))continue;
            var present=World.Read(new(nav.Decision.Actor,"state","room"))?.Value.Text()==room&&room.Length>0;
            if(nav.Authority=="godot"&&!present)
            {
                nav.Authority="core";
                // A registered path supplies actual remaining metres. Before a
                // target is assembled, the author's finite logical approach
                // duration is the offscreen fallback, never a host completion.
                var remaining=nav.Target is not null?nav.RemainingDistance/nav.Speed:
                    nav.Decision.Action.Declaration.Get("offscreen_approach_seconds").Number(1);
                if(!double.IsFinite(remaining)||remaining<0)throw new InvalidDataException("Invalid logical approach duration.");
                nav.DueAt=Clock.Now+remaining;
            }
            else if(nav.Authority=="core"&&present)
            {
                nav.RemainingDistance=Math.Max(0,(nav.DueAt??Clock.Now)-Clock.Now)*nav.Speed;
                nav.Authority="godot";nav.DueAt=null;
                Clock.ClearUnsimulatedFastForward();
            }
        }
    }

    private bool BeginSpatialNavigation(Execution execution)
    {
        var decision=execution.Decision;var declaration=decision.Action.Declaration;
        // Legacy physical adapters retain their explicit HostElapsed contract.
        // The streamed campaign adapter owns ordinary focused interactions.
        if(!spatialAuthorityEnabled||execution.HostRequired||declaration.Get("focus").Text().Length==0)return false;
        var from=World.Read(new(decision.Actor,"state","room"))?.Value.Text()??"";
        if(from.Length==0)return false;
        var destination=execution.Effects.FirstOrDefault(e=>e.Key==new FieldKey(decision.Actor,"state","room")&&!e.Increment)?.Value.Text()??from;
        var pure=(declaration.Get("kind").Text() is "walk" or "travel")&&destination==from;
        var navigation=new SpatialNavigation(decision,from,destination,pure);
        if(from!=visibleRoom)
        {
            var logicalApproach=declaration.Get("offscreen_approach_seconds").Number();
            if(!double.IsFinite(logicalApproach)||logicalApproach<0)throw new InvalidDataException("Invalid logical approach duration.");
            navigation.Authority="core";navigation.DueAt=Clock.Now+logicalApproach;
        }
        spatialNavigation.Add(decision.Execution,navigation);
        execution.WorkStartedAt=null;execution.DueAt=double.PositiveInfinity;
        if(navigation.Authority=="godot")Clock.ClearUnsimulatedFastForward();
        return true;
    }

    public bool SetNavigationTarget(Decision decision,string room,double x,double y,double z,double remainingDistance,double speed)
    {
        if(!spatialNavigation.TryGetValue(decision.Execution,out var nav)||!Current(decision,out var execution)||execution!.CancelRequested||
           nav.Authority!="godot"||nav.Phase!="approaching"||room!=visibleRoom||room!=nav.FromRoom)return false;
        if(!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(z)||!double.IsFinite(remainingDistance)||remainingDistance<0||remainingDistance>10000||
           !double.IsFinite(speed)||speed<=0||speed>100)throw new InvalidDataException("Invalid real navigation target or path measurement.");
        nav.Target=new(x,y,z);nav.RemainingDistance=remainingDistance;nav.Speed=speed;
        return true;
    }

    public bool ReportNavigationProgress(Decision decision,double x,double y,double z,double remainingDistance)
    {
        if(!spatialNavigation.TryGetValue(decision.Execution,out var nav)||nav.Authority!="godot"||nav.Phase!="approaching"||
           nav.FromRoom!=visibleRoom||!Current(decision,out var execution)||execution!.CancelRequested)return false;
        if(!double.IsFinite(remainingDistance)||remainingDistance<0||remainingDistance>10000)throw new InvalidDataException("Invalid remaining navigation path.");
        var prior=Placement(decision.Actor);
        ReportPlacement(decision.Actor,x,y,z,prior?.Yaw??0,nav.FromRoom);nav.RemainingDistance=remainingDistance;
        return true;
    }

    public bool Arrive(Decision decision,double x,double y,double z)
    {
        if(!spatialNavigation.TryGetValue(decision.Execution,out var nav)||nav.Authority!="godot"||nav.Phase!="approaching"||nav.Target is not { } target||
           !Current(decision,out var execution)||execution!.CancelRequested||visibleRoom!=nav.FromRoom||
           World.Read(new(decision.Actor,"state","room"))?.Value.Text()!=nav.FromRoom||!PhysicalActionPermitted(decision))return false;
        if(!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(z)||target.DistanceTo(new(x,y,z))>0.4)return false;
        ReportNavigationProgress(decision,x,y,z,0);
        BeginSpatialWork(nav,execution);
        return true;
    }

    private void BeginSpatialWork(SpatialNavigation nav,Execution execution)
    {
        nav.Authority="core";nav.Phase=nav.ToRoom!=nav.FromRoom?"transit":"working";nav.DueAt=null;
        execution.WorkStartedAt=Clock.Now;
        execution.DueAt=nav.PureMovement?Clock.Now:Clock.Now+execution.Decision.Action.Duration;
        if(strategyBindings.TryGetValue(execution.Decision.Action.Id,out var strategy))
            strategy.Activity.Begin(Clock.Now,execution.Decision.Action.Duration/contentTimeUnit);
        completions.Enqueue(execution,execution.DueAt);
    }

    public void StepPhysical(double actualGameSeconds,bool paused=false,string? playerActor=null)
    {
        if(paused)return;
        Clock.SubmitPhysicalSeconds(actualGameSeconds);
        physicalClockInput=true;
        try{Step(0,false,false,playerActor);}
        finally{physicalClockInput=false;}
    }
    public void StepLogical(double gameSeconds,string? playerActor=null)
    {
        if(HasHostNavigation)throw new InvalidOperationException("Visible navigation needs actual Godot simulation time.");
        Clock.SubmitLogicalSeconds(gameSeconds);Step(0,false,false,playerActor);
    }

    private void AdvanceSpatialNavigation()
    {
        foreach(var nav in spatialNavigation.Values.ToArray())
        {
            if(!Current(nav.Decision,out var execution)){spatialNavigation.Remove(nav.Decision.Execution);continue;}
            if(nav.Phase=="approaching"&&nav.Authority=="core"&&nav.DueAt is { } due&&Clock.Now>=due&&!execution!.CancelRequested)
                BeginSpatialWork(nav,execution!);
        }
    }
    private double NextSpatialTime()=>spatialNavigation.Values.Where(n=>n.Phase=="approaching"&&n.Authority=="core"&&n.DueAt.HasValue)
        .Select(n=>n.DueAt!.Value).DefaultIfEmpty(double.PositiveInfinity).Min();
    private void ElapseSpatialNavigation(double seconds)
    {
        if(seconds<=0)return;
        foreach(var nav in spatialNavigation.Values.Where(n=>n.Phase=="approaching"))
        {
            nav.Elapsed+=seconds;
            if(nav.Authority=="core")nav.RemainingDistance=Math.Max(0,nav.RemainingDistance-seconds*nav.Speed);
        }
    }
    public object? ActorActivity(string actor)
    {
        if(!running.TryGetValue(actor,out var execution))return null;
        var decision=execution.Decision;
        if(spatialNavigation.TryGetValue(decision.Execution,out var nav))return new {
            execution=decision.Execution,action=decision.Action.Id,phase=execution.CancelRequested?"cancelling":nav.Phase,authority=nav.Authority,
            from_room=nav.FromRoom,to_room=nav.ToRoom,remaining_seconds=nav.Phase=="approaching"?nav.Authority=="core"?Math.Max(0,(nav.DueAt??Clock.Now)-Clock.Now):nav.RemainingDistance/nav.Speed:Math.Max(0,execution.DueAt-Clock.Now),
            requires_arrival=nav.Phase=="approaching"&&nav.Authority=="godot",navigation_elapsed_seconds=nav.Elapsed,
            work_started_at=execution.WorkStartedAt,work_ends_at=double.IsFinite(execution.DueAt)?(double?)execution.DueAt:null
        };
        var room=World.Read(new(actor,"state","room"))?.Value.Text()??"";
        var to=execution.Effects.FirstOrDefault(e=>e.Key==new FieldKey(actor,"state","room")&&!e.Increment)?.Value.Text()??room;
        return new {execution=decision.Execution,action=decision.Action.Id,phase=execution.CancelRequested?"cancelling":to!=room?"transit":"working",authority=execution.HostRequired?"godot":"core",
            from_room=room,to_room=to,remaining_seconds=double.IsFinite(execution.DueAt)?Math.Max(0,execution.DueAt-Clock.Now):0,
            requires_arrival=false,navigation_elapsed_seconds=0.0,work_started_at=execution.WorkStartedAt,work_ends_at=double.IsFinite(execution.DueAt)?(double?)execution.DueAt:null};
    }
    private SpatialAuthorityCheckpoint SaveSpatial()=>new(spatialAuthorityEnabled,visibleRoom,spatialNavigation.Values.Select(n=>new SpatialNavigationCheckpoint(
        n.Decision.Execution,n.FromRoom,n.ToRoom,n.Authority,n.Phase,n.StartedAt,n.Elapsed,n.DueAt,n.RemainingDistance,n.Speed,n.Target,n.PureMovement)).ToArray());
    private void RestoreSpatial(SpatialAuthorityCheckpoint? state,IReadOnlyDictionary<long,Decision> decisions)
    {
        spatialNavigation.Clear();spatialAuthorityEnabled=state?.Enabled??false;visibleRoom=state?.VisibleRoom??"";
        if(state is null)return;
        if(state.Navigation is null||state.Navigation.Select(n=>n.Execution).Distinct().Count()!=state.Navigation.Length)throw new InvalidDataException("Invalid saved navigation authority.");
        foreach(var saved in state.Navigation)
        {
            if(!decisions.TryGetValue(saved.Execution,out var decision)||!Current(decision,out _)||saved.Authority is not ("godot" or "core")||saved.Phase is not ("approaching" or "working" or "transit")||
               !double.IsFinite(saved.StartedAt)||saved.StartedAt<0||!double.IsFinite(saved.Elapsed)||saved.Elapsed<0||!double.IsFinite(saved.RemainingDistance)||saved.RemainingDistance<0||
               !double.IsFinite(saved.Speed)||saved.Speed<=0||saved.DueAt is { } due&&(!double.IsFinite(due)||due<0)||saved.Target is { } point&&(!double.IsFinite(point.X)||!double.IsFinite(point.Y)||!double.IsFinite(point.Z)))
                throw new InvalidDataException("Invalid saved navigation execution.");
            spatialNavigation.Add(saved.Execution,new(decision,saved.FromRoom,saved.ToRoom,saved.PureMovement){Authority=saved.Authority,Phase=saved.Phase,StartedAt=saved.StartedAt,
                Elapsed=saved.Elapsed,DueAt=saved.DueAt,RemainingDistance=saved.RemainingDistance,Speed=saved.Speed,Target=saved.Target});
        }
    }
}
}

// --- Source: Simulation.cs ---
namespace DuelView.Core
{


/// <summary>World / Simulation: native Core mechanisms.</summary>
public static partial class DVSimulationKernel
{
    private sealed class SimulationBindingQueries
    {
        internal readonly ConditionalWeakTable<object, SimulationFactQuery> Facts = new();
    }
    private sealed class SimulationFactQuery
    {
        internal readonly ConditionalWeakTable<object, SimulationQueryValue> References = new();
        internal readonly ConditionalWeakTable<object, SimulationFactValues> RealityValues = new();
    }
    private sealed class SimulationFactValues
    {
        internal readonly ConditionalWeakTable<object, SimulationQueryValue> Observers = new();
    }
    private sealed record SimulationQueryValue(object Value);
    private static readonly ConditionalWeakTable<object, SimulationBindingQueries> SimulationQueries = new();

    // These descriptors depend only on the exact immutable binding/definition
    // values. Current physical and subjective cells are still read every sync.
    private static SimulationFactQuery SimulationQuery(object bindings, object declaration)
        => SimulationQueries.GetValue(bindings, static _ => new()).Facts.GetValue(declaration, static _ => new());

    private static object SimulationQueryReference(SimulationFactQuery query, object bindings, object declaration, object identity)
        => query.References.GetValue(identity, observer =>
        {
            var reference = F_dv_get(declaration, "ref");
            var local = F_dv_local(bindings, F_dv_get(reference, "object"), F_dv_get(observer, "actor"));
            return new SimulationQueryValue(R.Bool(R.eq(local, "")) ? false : R.snapshot(F_dv_scoped_ref(
                F_dv_get(observer, "actor"), local, F_dv_get(reference, "facet"), F_dv_get(reference, "field"))));
        }).Value;

    // Only this owned synchronization pipeline uses the no-op shortcut. Public
    // DV record updates retain their independent writable-copy contract.
    private static object SimulationSet(object record, string key, object value)
    {
        foreach (var entry in R.Items(record))
        {
            if (!R.TryItems(entry, out var pair) || pair.Count != 2) break;
            if (pair[0] is not string name || !string.Equals(name, key, StringComparison.Ordinal)) continue;
            var old = pair[1];
            var same = ReferenceEquals(old, value) ||
                (old is string a && value is string b && string.Equals(a, b, StringComparison.Ordinal)) ||
                (old is bool x && value is bool y && x == y) ||
                (old is double p && value is double q && BitConverter.DoubleToInt64Bits(p) == BitConverter.DoubleToInt64Bits(q));
            if (!same && R.TryItems(old, out _) && R.TryItems(value, out _))
                same = ReferenceEquals(R.snapshot(old), R.snapshot(value));
            return same ? record : F_dv_with(record, key, value);
        }
        return F_dv_with(record, key, value);
    }
    public static object F_dvs_simulation_open(object v_story, object v_world)
    {
        object v_config__383476 = F_dv_find(v_story, "simulation", new System.Collections.Generic.List<object>(0) { });
        object v_identities__383526 = F_dv_find(v_config__383476, "identities", new System.Collections.Generic.List<object>(0) { });
        object v_occupied__383581 = R.number("0");
        object v_names__383602 = new System.Collections.Generic.List<object>(0) { };
        object v_i__383621 = R.number("0");
        while (R.Bool(R.lt(v_i__383621, R.count(v_identities__383526))))
        {
            object v_identity__383675 = R.at(v_identities__383526, v_i__383621);
            var protection = F_dv_find(v_identity__383675, "protected", false);
            _ = F_dv_require(protection is bool, "Identity protection must be a boolean");
            object v_bit__383712 = F_dv_mask_bit(F_dv_get(v_identity__383675, "index"));
            _ = F_dv_require((R.Bool(R.not(F_dv_mask_has(v_occupied__383581, F_dv_get(v_identity__383675, "index")))) ? (object)(R.not(F_dv_has(v_names__383602, F_dv_get(v_identity__383675, "actor")))) : (object)(false)), "Simulation identity index or actor duplicated");
            v_occupied__383581 = R.add(v_occupied__383581, v_bit__383712);
            v_names__383602 = F_dv_append(v_names__383602, F_dv_get(v_identity__383675, "actor"));
            v_i__383621 = R.add(v_i__383621, R.number("1"));
        }
        object v_time__384004 = F_dv_find(v_world, "time", R.number("0"));
        return F_dv_with(v_world, "simulation", new System.Collections.Generic.List<object>(12) { F_dv_pair("version", "approximate-v1"), F_dv_pair("approximation", true), F_dv_pair("config", v_config__383476), F_dv_pair("last_macro", R.mul(R.floor(R.div(v_time__384004, R.number("15"))), R.number("15"))), F_dv_pair("last_nearby_second", R.sub(R.number("0"), R.number("1"))), F_dv_pair("visible_rooms", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("nearby_rooms", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("RealityFacts", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("DemonEntityLock", false), F_dv_pair("macro_count", R.number("0")), F_dv_pair("nearby_count", R.number("0")), F_dv_pair("daily_count", R.number("0")) });
    }

    public static object F_dvs_simulation_fact(object facts, object id)
    {
        var index = F_dv_index_by_id(facts, id);
        return R.Num(index) < 0 ? false : R.at(facts, index);
    }

    public static object F_dvs_simulation_facts(object v_world, object v_actors, object v_bindings)
    {
        // This projection reads all inputs and returns a new world record.
        // Borrow frozen branches rather than acquiring mutable copies per cell.
        v_world = R.snapshot(v_world);
        v_actors = R.snapshot(v_actors);
        v_bindings = R.snapshot(v_bindings);
        object v_simulation__384764 = F_dv_get(v_world, "simulation");
        object v_config__384802 = F_dv_get(v_simulation__384764, "config");
        object v_declarations__384837 = F_dv_find(v_config__384802, "facts", new System.Collections.Generic.List<object>(0) { });
        object v_identities__384889 = F_dv_find(v_config__384802, "identities", new System.Collections.Generic.List<object>(0) { });
        object v_facts__384944 = new System.Collections.Generic.List<object>(0) { };
        // The actor list is fixed throughout this projection. Resolve each
        // declared observer lazily once, retaining the original first-match
        // lookup and its validation order for the first declaration.
        var observerIndices = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        object v_i__384963 = R.number("0");
        while (R.Bool(R.lt(v_i__384963, R.count(v_declarations__384837))))
        {
            object v_declaration__385019 = R.at(v_declarations__384837, v_i__384963);
            object v_reality__385061 = F_dv_read_query(v_world, F_dv_get(v_declaration__385019, "ref"));
            var query = SimulationQuery(v_bindings, R.snapshot(v_declaration__385019));
            var values = query.RealityValues.GetValue(v_reality__385061, static _ => new());
            object v_known__385115 = R.number("0");
            object v_j__385137 = R.number("0");
            while (R.Bool(R.lt(v_j__385137, R.count(v_identities__384889))))
            {
                object v_identity__385199 = R.at(v_identities__384889, v_j__385137);
                if (!observerIndices.TryGetValue(v_identity__385199, out var v_index__385240))
                {
                    v_index__385240 = F_dv_index_by_id(v_actors, F_dv_get(v_identity__385199, "actor"));
                    observerIndices[v_identity__385199] = v_index__385240;
                }
                if (R.Bool(R.not(R.lt(v_index__385240, R.number("0")))))
                {
                    object scoped = SimulationQueryReference(query, v_bindings, v_declaration__385019, v_identity__385199);
                    if (scoped is not false)
                    {
                        object v_cell__385469 = F_dv_read_query(F_dv_get(R.at(v_actors, v_index__385240), "mind"), scoped);
                        object v_projected__385612 = values.Observers.GetValue(v_identity__385199, observer =>
                            new SimulationQueryValue(R.snapshot(F_dv_project_value(F_dv_get(v_reality__385061, "value"),
                                v_bindings, F_dv_get(observer, "actor"))))).Value;
                        if (R.Bool((R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_reality__385061)) ? (object)(F_dv_uncontested(v_cell__385469)) : (object)(false))) ? (object)(F_dv_get(v_projected__385612, "known")) : (object)(false))) ? (object)(F_dv_same(F_dv_get(v_projected__385612, "value"), F_dv_get(v_cell__385469, "value"))) : (object)(false))))
                        {
                            v_known__385115 = R.add(v_known__385115, F_dv_mask_bit(F_dv_get(v_identity__385199, "index")));
                        }
                    }
                }
                v_j__385137 = R.add(v_j__385137, R.number("1"));
            }
            _ = R.push(v_facts__384944, F_dv_reality_fact(F_dv_get(v_declaration__385019, "id"), F_dv_get(v_declaration__385019, "ref"), v_reality__385061, v_known__385115));
            v_i__384963 = R.add(v_i__384963, R.number("1"));
        }
        return SimulationSet(v_world, "simulation", SimulationSet(v_simulation__384764, "RealityFacts", v_facts__384944));
    }

    public static object F_dvs_simulation_danger(object v_config, object v_id)
    {
        object v_dangers__386144 = F_dv_find(v_config, "dangers", new System.Collections.Generic.List<object>(0) { });
        object v_i__386193 = R.number("0");
        while (R.Bool(R.lt(v_i__386193, R.count(v_dangers__386144))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_dangers__386144, v_i__386193), "id"), v_id)))
            {
                return R.at(v_dangers__386144, v_i__386193);
            }
            v_i__386193 = R.add(v_i__386193, R.number("1"));
        }
        return false;
    }

    public static object F_dvs_simulation_protected_mask(object config)
    {
        object mask = R.number("0");
        foreach (var identity in R.Items(F_dv_find(config, "identities", new List<object>())))
            if (R.Bool(F_dv_find(identity, "protected", false)))
                mask = R.add(mask, F_dv_mask_bit(F_dv_get(identity, "index")));
        return F_dv_mask32(mask);
    }

    public static object F_dvs_simulation_lock(object v_world, object v_events)
    {
        object v_simulation__386377 = F_dv_get(v_world, "simulation");
        object v_config__386415 = F_dv_get(v_simulation__386377, "config");
        object v_demon__386450 = F_dv_find(v_config__386415, "demon", false);
        if (R.Bool(R.eq(v_demon__386450, false)))
        {
            return v_world;
        }
        object v_lock__386542 = F_dv_get(v_simulation__386377, "DemonEntityLock");
        if (R.Bool(R.not(R.eq(v_lock__386542, false))))
        {
            object v_danger__386610 = F_dvs_simulation_danger(v_config__386415, F_dv_get(v_lock__386542, "danger"));
            if (R.Bool((R.Bool((R.Bool(R.eq(v_danger__386610, false)) ? (object)(true) : (object)(R.eq(F_dv_truth(v_world, F_dv_get(v_danger__386610, "when")), "false")))) ? (object)(true) : (object)(R.eq(F_dv_truth(v_world, F_dv_find(v_demon__386450, "release_when", F_dv_not(F_dv_true()))), "true")))))
            {
                _ = F_dvs_event(v_events, "simulation_demon_released", F_dv_get(v_demon__386450, "actor"), F_dv_get(v_world, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(2) { F_dv_pair("lock", v_lock__386542), F_dv_pair("approximation", true) });
                v_lock__386542 = false;
            }
        }
        if (R.Bool(R.eq(v_lock__386542, false)))
        {
            object v_dangers__387141 = F_dv_find(v_config__386415, "dangers", new System.Collections.Generic.List<object>(0) { });
            object v_i__387194 = R.number("0");
            while (R.Bool((R.Bool(R.lt(v_i__387194, R.count(v_dangers__387141))) ? (object)(R.eq(v_lock__386542, false)) : (object)(false))))
            {
                object v_danger__387271 = R.at(v_dangers__387141, v_i__387194);
                object v_fact__387307 = F_dvs_simulation_fact(F_dv_get(v_simulation__386377, "RealityFacts"), F_dv_get(v_danger__387271, "fact_id"));
                if (R.Bool((R.Bool(R.not(R.eq(v_fact__387307, false))) ? (object)(R.eq(F_dv_truth(v_world, F_dv_get(v_danger__387271, "when")), "true")) : (object)(false))))
                {
                    object v_victims__387468 = F_dv_mask_without(F_dv_mask_and(F_dv_get(v_danger__387271, "affected_mask"), F_dvs_simulation_protected_mask(v_config__386415)), F_dv_get(v_fact__387307, "KnownByMask"));
                    if (R.Bool(R.not(R.eq(v_victims__387468, R.number("0")))))
                    {
                        v_lock__386542 = new System.Collections.Generic.List<object>(6) { F_dv_pair("actor", F_dv_get(v_demon__386450, "actor")), F_dv_pair("danger", F_dv_get(v_danger__387271, "id")), F_dv_pair("room", F_dv_get(v_danger__387271, "room")), F_dv_pair("victims", v_victims__387468), F_dv_pair("acquired_at", F_dv_get(v_world, "time")), F_dv_pair("protecting", false) };
                        _ = F_dvs_event(v_events, "simulation_demon_reserved", F_dv_get(v_demon__386450, "actor"), F_dv_get(v_world, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(3) { F_dv_pair("lock", v_lock__386542), F_dv_pair("approximation", true), F_dv_pair("movement", false) });
                    }
                }
                v_i__387194 = R.add(v_i__387194, R.number("1"));
            }
        }
        return F_dv_with(v_world, "simulation", F_dv_with(v_simulation__386377, "DemonEntityLock", v_lock__386542));
    }

    public static object F_dvs_simulation_sync(object v_state)
    {
        v_state = R.snapshot(v_state);
        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation"))))
        {
            return v_state;
        }
        object v_actors__388123 = F_dv_get(v_state, "actors");
        object v_simulation__388153 = F_dv_get(F_dv_get(v_state, "physical"), "simulation");
        object v_latest_config__388200 = F_dv_find(F_dv_get(v_state, "story"), "simulation", F_dv_get(v_simulation__388153, "config"));
        var previousSimulation = v_simulation__388153;
        v_simulation__388153 = SimulationSet(v_simulation__388153, "config", v_latest_config__388200);
        if (!ReferenceEquals(previousSimulation, v_simulation__388153))
            v_state = F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("physical", SimulationSet(F_dv_get(v_state, "physical"), "simulation", v_simulation__388153)) });
        object v_filters__388447 = F_dv_find(F_dv_get(v_simulation__388153, "config"), "filters", new System.Collections.Generic.List<object>(0) { });
        object v_i__388507 = R.number("0");
        while (R.Bool(R.lt(v_i__388507, R.count(v_actors__388123))))
        {
            v_actors__388123 = R.snapshot(v_actors__388123);
            object v_actor__388557 = R.at(v_actors__388123, v_i__388507);
            var previousActor = v_actor__388557;
            object v_j__388587 = R.number("0");
            while (R.Bool(R.lt(v_j__388587, R.count(v_filters__388447))))
            {
                if (R.Bool(R.eq(F_dv_get(R.at(v_filters__388447, v_j__388587), "actor"), F_dv_get(v_actor__388557, "id"))))
                {
                    v_actor__388557 = SimulationSet(v_actor__388557, "mind", SimulationSet(F_dv_get(v_actor__388557, "mind"), "CognitionFilter", R.at(v_filters__388447, v_j__388587)));
                }
                v_j__388587 = R.add(v_j__388587, R.number("1"));
            }
            v_actor__388557 = SimulationSet(v_actor__388557, "simulation_priority_action", "");
            object v_schedules__388873 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool(R.not(F_dvs_simulation_onstage(v_actor__388557, v_state))))
            {
                object v_declarations__388957 = F_dv_find(F_dv_get(v_simulation__388153, "config"), "offscreen_schedules", new System.Collections.Generic.List<object>(0) { });
                v_j__388587 = R.number("0");
                while (R.Bool(R.lt(v_j__388587, R.count(v_declarations__388957))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_declarations__388957, v_j__388587), "actor"), F_dv_get(v_actor__388557, "id"))))
                    {
                        _ = R.push(v_schedules__388873, R.at(v_declarations__388957, v_j__388587));
                    }
                    v_j__388587 = R.add(v_j__388587, R.number("1"));
                }
            }
            v_actor__388557 = SimulationSet(v_actor__388557, "policy", SimulationSet(F_dv_get(v_actor__388557, "policy"), "offscreen_schedules", v_schedules__388873));
            if (R.Bool(R.not(R.eq(F_dv_find(v_actor__388557, "simulation_decision_macro", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_simulation__388153, "macro_count")))))
            {
                v_actor__388557 = SimulationSet(v_actor__388557, "idle_guard", false);
            }
            if (R.Bool((R.Bool(R.lt(F_dv_find(v_actor__388557, "simulation_decision_nearby", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_simulation__388153, "last_nearby_second"))) ? (object)(F_dv_has_key(v_actor__388557, "location_ref")) : (object)(false))))
            {
                object v_position__389613 = F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(v_actor__388557, "location_ref"));
                if (R.Bool((R.Bool(F_dv_uncontested(v_position__389613)) ? (object)(F_dv_has(F_dv_get(v_simulation__388153, "nearby_rooms"), F_dvs_simulation_room(F_dv_get(v_simulation__388153, "config"), F_dv_get(v_position__389613, "value")))) : (object)(false))))
                {
                    v_actor__388557 = F_dv_with_many(v_actor__388557, new System.Collections.Generic.List<object>(2) { F_dv_pair("idle_guard", false), F_dv_pair("decision_time", R.sub(R.number("0"), R.number("1"))) });
                }
            }
            if (!ReferenceEquals(R.snapshot(v_actor__388557), previousActor))
                v_actors__388123 = F_dvs_replace(v_actors__388123, v_i__388507, v_actor__388557);
            v_i__388507 = R.add(v_i__388507, R.number("1"));
        }
        object v_events__389962 = F_dv_clone(F_dv_get(v_state, "events"));
        object v_world__390002 = F_dvs_simulation_lock(F_dvs_simulation_facts(F_dv_get(v_state, "physical"), v_actors__388123, F_dv_get(v_state, "bindings")), v_events__389962);
        object v_lock__390108 = F_dv_get(F_dv_get(v_world__390002, "simulation"), "DemonEntityLock");
        object v_declared_demon__390156 = F_dv_find(F_dv_get(F_dv_get(v_world__390002, "simulation"), "config"), "demon", false);
        if (R.Bool(R.not(R.eq(v_declared_demon__390156, false))))
        {
            object v_owner_index__390266 = F_dv_index_by_id(v_actors__388123, F_dv_get(v_declared_demon__390156, "actor"));
            if (R.Bool(R.not(R.lt(v_owner_index__390266, R.number("0")))))
            {
                v_actors__388123 = F_dvs_replace(v_actors__388123, v_owner_index__390266, F_dv_with(R.at(v_actors__388123, v_owner_index__390266), "simulation_entity_lock", v_lock__390108));
            }
        }
        if (R.Bool(R.not(R.eq(v_lock__390108, false))))
        {
            object v_demon__390516 = F_dv_get(F_dv_get(F_dv_get(v_world__390002, "simulation"), "config"), "demon");
            object v_index__390566 = F_dv_index_by_id(v_actors__388123, F_dv_get(v_demon__390516, "actor"));
            if (R.Bool(R.not(R.lt(v_index__390566, R.number("0")))))
            {
                object v_location__390661 = F_dv_read(v_world__390002, F_dv_get(v_demon__390516, "location_ref"));
                object v_requested__390723 = "";
                if (R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_location__390661)) ? (object)(R.eq(F_dv_get(v_location__390661, "value"), F_dv_get(v_lock__390108, "room"))) : (object)(false))) ? (object)(R.eq(F_dv_truth(v_world__390002, F_dv_find(v_demon__390516, "presence_when", F_dv_true())), "true")) : (object)(false))))
                {
                    if (R.Bool(R.not(F_dv_get(v_lock__390108, "protecting"))))
                    {
                        v_requested__390723 = F_dv_get(v_demon__390516, "protect_action_id");
                    }
                }
                else
                {
                    object v_routes__391019 = F_dv_find(v_demon__390516, "travel_actions", new System.Collections.Generic.List<object>(0) { });
                    v_i__388507 = R.number("0");
                    while (R.Bool(R.lt(v_i__388507, R.count(v_routes__391019))))
                    {
                        if (R.Bool(R.eq(F_dv_get(R.at(v_routes__391019, v_i__388507), "room"), F_dv_get(v_lock__390108, "room"))))
                        {
                            v_requested__390723 = F_dv_get(R.at(v_routes__391019, v_i__388507), "action_id");
                        }
                        v_i__388507 = R.add(v_i__388507, R.number("1"));
                    }
                }
                v_actors__388123 = F_dvs_replace(v_actors__388123, v_index__390566, F_dv_with(R.at(v_actors__388123, v_index__390566), "simulation_priority_action", v_requested__390723));
            }
        }
        return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(3) { F_dv_pair("actors", v_actors__388123), F_dv_pair("physical", v_world__390002), F_dv_pair("events", v_events__389962) });
    }

    public static object F_dvs_simulation_next_time(object v_world, object v_due)
    {
        if (R.Bool(F_dv_has_key(v_world, "simulation")))
        {
            return F_dv_min(v_due, R.add(F_dv_get(F_dv_get(v_world, "simulation"), "last_macro"), R.number("15")));
        }
        return v_due;
    }

    public static object F_dvs_simulation_room(object v_config, object v_location)
    {
        object v_locations__391689 = F_dv_find(v_config, "locations", new System.Collections.Generic.List<object>(0) { });
        object v_i__391742 = R.number("0");
        while (R.Bool(R.lt(v_i__391742, R.count(v_locations__391689))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_locations__391689, v_i__391742), "location"), v_location)))
            {
                return F_dv_get(R.at(v_locations__391689, v_i__391742), "room");
            }
            v_i__391742 = R.add(v_i__391742, R.number("1"));
        }
        return v_location;
    }

    public static object F_dvs_simulation_onstage(object v_actor, object v_state)
    {
        if (R.Bool(R.eq(F_dv_get(v_actor, "id"), F_dv_find(v_state, "player_actor", ""))))
        {
            return true;
        }
        if (R.Bool(R.not(F_dv_has_key(v_actor, "location_ref"))))
        {
            return false;
        }
        object v_position__392096 = F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(v_actor, "location_ref"));
        if (R.Bool(R.not(F_dv_uncontested(v_position__392096))))
        {
            return false;
        }
        if (R.Bool(F_dv_has(F_dv_get(F_dv_get(F_dv_get(v_state, "physical"), "simulation"), "visible_rooms"), F_dvs_simulation_room(F_dv_get(F_dv_get(F_dv_get(v_state, "physical"), "simulation"), "config"), F_dv_get(v_position__392096, "value")))))
        {
            return true;
        }
        object v_viewer__392364 = F_dv_find(F_dv_get(v_state, "story"), "host_viewer", F_dv_find(v_state, "player_actor", ""));
        object v_index__392453 = F_dv_index_by_id(F_dv_get(v_state, "actors"), v_viewer__392364);
        if (R.Bool((R.Bool(R.not(R.lt(v_index__392453, R.number("0")))) ? (object)(F_dv_has_key(R.at(F_dv_get(v_state, "actors"), v_index__392453), "location_ref")) : (object)(false))))
        {
            object v_viewing__392593 = F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(R.at(F_dv_get(v_state, "actors"), v_index__392453), "location_ref"));
            return (R.Bool(F_dv_uncontested(v_viewing__392593)) ? (object)(R.eq(F_dv_get(v_viewing__392593, "value"), F_dv_get(v_position__392096, "value"))) : (object)(false));
        }
        return false;
    }

    public static object F_dvs_simulation_actor_ready(object v_actor, object v_state)
    {
        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation"))))
        {
            return true;
        }
        if (R.Bool((R.Bool(F_dvs_simulation_onstage(v_actor, v_state)) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor, "simulation_priority_action", ""), ""))))))
        {
            return true;
        }
        if (R.Bool((R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_actor, "pending_heard", new System.Collections.Generic.List<object>(0) { })))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor, "decision_revision", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_actor, "mind"), "revision")))))))
        {
            return true;
        }
        object v_simulation__393308 = F_dv_get(F_dv_get(v_state, "physical"), "simulation");
        if (R.Bool(R.not(R.eq(F_dv_find(v_actor, "simulation_decision_macro", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_simulation__393308, "macro_count")))))
        {
            return true;
        }
        if (R.Bool(F_dv_has_key(v_actor, "location_ref")))
        {
            object v_position__393503 = F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(v_actor, "location_ref"));
            if (R.Bool((R.Bool(F_dv_uncontested(v_position__393503)) ? (object)(F_dv_has(F_dv_get(v_simulation__393308, "nearby_rooms"), F_dvs_simulation_room(F_dv_get(v_simulation__393308, "config"), F_dv_get(v_position__393503, "value")))) : (object)(false))))
            {
                return R.lt(F_dv_find(v_actor, "simulation_decision_nearby", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_simulation__393308, "last_nearby_second"));
            }
        }
        return false;
    }

    public static object F_dvs_simulation_protect_complete(object v_state, object v_pending)
    {
        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation"))))
        {
            return v_state;
        }
        object v_world__416662 = F_dv_get(v_state, "physical");
        object v_lock__416693 = F_dv_get(F_dv_get(v_world__416662, "simulation"), "DemonEntityLock");
        object v_demon__416741 = F_dv_find(F_dv_get(F_dv_get(v_world__416662, "simulation"), "config"), "demon", false);
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_lock__416693, false)) ? (object)(true) : (object)(R.eq(v_demon__416741, false)))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_pending, "id"), F_dv_get(v_demon__416741, "actor")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(F_dv_get(v_pending, "action"), "id"), F_dv_get(v_demon__416741, "protect_action_id")))))))
        {
            return v_state;
        }
        object v_location__416944 = F_dv_read(v_world__416662, F_dv_get(v_demon__416741, "location_ref"));
        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_location__416944))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_location__416944, "value"), F_dv_get(v_lock__416693, "room")))))))
        {
            return v_state;
        }
        object v_danger__417087 = F_dvs_simulation_danger(F_dv_get(F_dv_get(v_world__416662, "simulation"), "config"), F_dv_get(v_lock__416693, "danger"));
        if (R.Bool((R.Bool(R.eq(v_danger__417087, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_truth(v_world__416662, F_dv_get(v_danger__417087, "when")), "true"))))))
        {
            return v_state;
        }
        object v_fact__417251 = F_dvs_simulation_fact(F_dv_get(F_dv_get(v_world__416662, "simulation"), "RealityFacts"), F_dv_get(v_danger__417087, "fact_id"));
        if (R.Bool(R.eq(v_fact__417251, false)))
        {
            return v_state;
        }
        object v_actors__417376 = F_dv_get(v_state, "actors");
        object v_events__417406 = F_dv_clone(F_dv_get(v_state, "events"));
        object v_event__417446 = F_dvs_event(v_events__417406, "simulation_demon_protection", F_dv_get(v_demon__416741, "actor"), F_dv_get(v_world__416662, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(4) { F_dv_pair("danger", F_dv_get(v_danger__417087, "id")), F_dv_pair("room", F_dv_get(v_lock__416693, "room")), F_dv_pair("action_event", F_dv_get(v_pending, "action_event")), F_dv_pair("approximation", true) });
        object v_i__417643 = R.number("0");
        while (R.Bool(R.lt(v_i__417643, R.count(v_actors__417376))))
        {
            object v_actor__417693 = R.at(v_actors__417376, v_i__417643);
            if (R.Bool(F_dv_has_key(v_actor__417693, "location_ref")))
            {
                object v_position__417773 = F_dv_read(v_world__416662, F_dv_get(v_actor__417693, "location_ref"));
                if (R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_position__417773)) ? (object)(R.eq(F_dv_get(v_position__417773, "value"), F_dv_get(v_lock__416693, "room"))) : (object)(false))) ? (object)(F_dvs_auditory_access(v_actor__417693, v_world__416662)) : (object)(false))))
                {
                    object v_observations__417950 = F_dv_cognition_observations(F_dv_get(v_actor__417693, "mind"), F_dv_perceive(v_world__416662, F_dv_get(v_actor__417693, "mind"), F_dv_get(v_state, "bindings"), new System.Collections.Generic.List<object>(1) { F_dv_get(v_fact__417251, "ref") }, v_event__417446, F_dv_get(v_world__416662, "time")));
                    v_actor__417693 = F_dv_with(v_actor__417693, "mind", F_dv_get(F_dv_update_mind(F_dv_get(v_actor__417693, "mind"), v_observations__417950, F_dv_get(F_dv_get(v_actor__417693, "mind"), "revision"), F_dvs_observation_profile(v_actor__417693)), "world"));
                    if (R.Bool(R.lt(R.number("0"), R.count(v_observations__417950))))
                    {
                        _ = F_dvs_event(v_events__417406, "simulation_public_warning", F_dv_get(v_actor__417693, "id"), F_dv_get(v_world__416662, "time"), new System.Collections.Generic.List<object>(1) { v_event__417446 }, new System.Collections.Generic.List<object>(3) { F_dv_pair("observations", v_observations__417950), F_dv_pair("danger", F_dv_get(v_danger__417087, "id")), F_dv_pair("source", F_dv_get(v_demon__416741, "actor")) });
                    }
                    v_actors__417376 = F_dvs_replace(v_actors__417376, v_i__417643, v_actor__417693);
                }
            }
            v_i__417643 = R.add(v_i__417643, R.number("1"));
        }
        v_world__416662 = F_dv_with(v_world__416662, "simulation", F_dv_with(F_dv_get(v_world__416662, "simulation"), "DemonEntityLock", F_dv_with(v_lock__416693, "protecting", true)));
        return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(3) { F_dv_pair("actors", v_actors__417376), F_dv_pair("physical", v_world__416662), F_dv_pair("events", v_events__417406) });
    }

    public static object F_dvs_simulation_pulse(object v_state, object v_seconds, object v_visible_rooms)
    {
        _ = F_dv_require(R.not(R.lt(v_seconds, R.number("0"))), "Negative host simulation clock");
        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation"))))
        {
            return v_state;
        }
        object v_simulation__419108 = F_dv_get(F_dv_get(v_state, "physical"), "simulation");
        object v_tick__419155 = R.floor(v_seconds);
        _ = F_dv_require(R.not(R.lt(v_tick__419155, F_dv_get(v_simulation__419108, "last_nearby_second"))), "Host simulation clock moved backwards");
        if (R.Bool(R.eq(F_dv_get(v_simulation__419108, "last_nearby_second"), v_tick__419155)))
        {
            return v_state;
        }
        object v_mapped__419350 = new System.Collections.Generic.List<object>(0) { };
        object v_v__419370 = R.number("0");
        while (R.Bool(R.lt(v_v__419370, R.count(v_visible_rooms))))
        {
            object v_room__419427 = F_dvs_simulation_room(F_dv_get(v_simulation__419108, "config"), R.at(v_visible_rooms, v_v__419370));
            if (R.Bool(R.not(F_dv_has(v_mapped__419350, v_room__419427))))
            {
                _ = R.push(v_mapped__419350, v_room__419427);
            }
            v_v__419370 = R.add(v_v__419370, R.number("1"));
        }
        v_visible_rooms = v_mapped__419350;
        object v_nearby__419615 = v_visible_rooms;
        object v_edges__419646 = F_dv_find(F_dv_get(v_simulation__419108, "config"), "edges", new System.Collections.Generic.List<object>(0) { });
        object v_i__419702 = R.number("0");
        while (R.Bool(R.lt(v_i__419702, R.count(v_edges__419646))))
        {
            object v_edge__419751 = R.at(v_edges__419646, v_i__419702);
            if (R.Bool((R.Bool(F_dv_has(v_visible_rooms, F_dv_get(v_edge__419751, "from"))) ? (object)(R.not(F_dv_has(v_nearby__419615, F_dv_get(v_edge__419751, "to")))) : (object)(false))))
            {
                v_nearby__419615 = F_dv_append(v_nearby__419615, F_dv_get(v_edge__419751, "to"));
            }
            if (R.Bool((R.Bool(F_dv_has(v_visible_rooms, F_dv_get(v_edge__419751, "to"))) ? (object)(R.not(F_dv_has(v_nearby__419615, F_dv_get(v_edge__419751, "from")))) : (object)(false))))
            {
                v_nearby__419615 = F_dv_append(v_nearby__419615, F_dv_get(v_edge__419751, "from"));
            }
            v_i__419702 = R.add(v_i__419702, R.number("1"));
        }
        object v_changed__420045 = (R.Bool(R.not(F_dv_same(F_dv_get(v_simulation__419108, "visible_rooms"), v_visible_rooms))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_simulation__419108, "nearby_rooms"), v_nearby__419615))));
        v_simulation__419108 = F_dv_with_many(v_simulation__419108, new System.Collections.Generic.List<object>(4) { F_dv_pair("last_nearby_second", v_tick__419155), F_dv_pair("visible_rooms", v_visible_rooms), F_dv_pair("nearby_rooms", v_nearby__419615), F_dv_pair("nearby_count", R.add(F_dv_get(v_simulation__419108, "nearby_count"), R.number("1"))) });
        object v_events__420337 = F_dv_clone(F_dv_get(v_state, "events"));
        if (R.Bool(v_changed__420045))
        {
            _ = F_dvs_event(v_events__420337, "simulation_nearby_scope", "", F_dv_get(F_dv_get(v_state, "physical"), "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(3) { F_dv_pair("host_second", v_tick__419155), F_dv_pair("rooms", v_nearby__419615), F_dv_pair("approximation", true) });
        }
        return F_dvs_simulation_sync(F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(3) { F_dv_pair("physical", F_dv_with(F_dv_get(v_state, "physical"), "simulation", v_simulation__419108)), F_dv_pair("events", v_events__420337), F_dv_pair("idle", R.number("0")) }));
    }
}
}

// --- Source: SimulationEnvironment.cs ---
namespace DuelView.Core
{


/// <summary>World / SimulationEnvironment: native Core mechanisms.</summary>
public static partial class DVSimulationKernel
{
    public static object F_dvs_simulation_diffuse(object v_world, object v_config, object v_events, object v_parent, object v_time)
    {
        // These inputs are queried or functionally replaced. The event sink
        // deliberately remains the caller's mutable, ordered append root.
        v_world = R.snapshot(v_world);
        v_config = R.snapshot(v_config);
        object v_rooms__403934 = F_dv_find(v_config, "rooms", new System.Collections.Generic.List<object>(0) { });
        object v_edges__403979 = F_dv_find(v_config, "edges", new System.Collections.Generic.List<object>(0) { });
        object v_amounts__404024 = new System.Collections.Generic.List<object>(0) { };
        object v_concentrations__404045 = new System.Collections.Generic.List<object>(0) { };
        object v_outgoing__404073 = new System.Collections.Generic.List<object>(0) { };
        object v_boundaries__404095 = new System.Collections.Generic.List<object>(0) { };
        object v_i__404119 = R.number("0");
        while (R.Bool(R.lt(v_i__404119, R.count(v_rooms__403934))))
        {
            object v_room__404168 = R.at(v_rooms__403934, v_i__404119);
            object v_concentration__404196 = F_dv_read_query(v_world, F_dv_get(v_room__404168, "concentration_ref"));
            object v_volume__404263 = F_dv_find(v_room__404168, "volume", R.number("1"));
            _ = F_dv_require((R.Bool((R.Bool(F_dv_uncontested(v_concentration__404196)) ? (object)(R.not(R.lt(F_dv_get(v_concentration__404196, "value"), R.number("0")))) : (object)(false))) ? (object)(R.lt(R.number("0"), v_volume__404263)) : (object)(false)), "Diffusion requires known nonnegative concentration and positive volume");
            _ = F_dv_unit(F_dv_find(v_room__404168, "settling", R.number("0")));
            object v_boundary__404525 = R.sub(R.number("0"), R.number("1"));
            object v_conditions__404551 = F_dv_find(v_room__404168, "boundaries", new System.Collections.Generic.List<object>(0) { });
            object v_c__404608 = R.number("0");
            while (R.Bool(R.lt(v_c__404608, R.count(v_conditions__404551))))
            {
                if (R.Bool(R.eq(F_dv_truth(v_world, F_dv_get(R.at(v_conditions__404551, v_c__404608), "when")), "true")))
                {
                    v_boundary__404525 = F_dv_get(R.at(v_conditions__404551, v_c__404608), "value");
                }
                v_c__404608 = R.add(v_c__404608, R.number("1"));
            }
            object v_current__404797 = F_dv_get(v_concentration__404196, "value");
            if (R.Bool(R.not(R.lt(v_boundary__404525, R.number("0")))))
            {
                v_current__404797 = v_boundary__404525;
            }
            _ = R.push(v_boundaries__404095, v_boundary__404525);
            _ = R.push(v_amounts__404024, R.mul(v_current__404797, v_volume__404263));
            _ = R.push(v_concentrations__404045, v_current__404797);
            _ = R.push(v_outgoing__404073, R.number("0"));
            v_i__404119 = R.add(v_i__404119, R.number("1"));
        }
        object v_flows__405061 = new System.Collections.Generic.List<object>(0) { };
        v_i__404119 = R.number("0");
        while (R.Bool(R.lt(v_i__404119, R.count(v_edges__403979))))
        {
            object v_edge__405125 = R.at(v_edges__403979, v_i__404119);
            object v_a__405153 = F_dv_index_by_id(v_rooms__403934, F_dv_get(v_edge__405125, "from"));
            object v_b__405213 = F_dv_index_by_id(v_rooms__403934, F_dv_get(v_edge__405125, "to"));
            _ = F_dv_require((R.Bool((R.Bool(R.not(R.lt(v_a__405153, R.number("0")))) ? (object)(R.not(R.lt(v_b__405213, R.number("0")))) : (object)(false))) ? (object)(R.not(R.eq(v_a__405153, v_b__405213))) : (object)(false)), "Diffusion edge references invalid rooms");
            object v_opening__405363 = F_dv_find(v_edge__405125, "opening", R.number("0"));
            if (R.Bool(F_dv_has_key(v_edge__405125, "open_when")))
            {
                if (R.Bool(R.eq(F_dv_truth(v_world, F_dv_get(v_edge__405125, "open_when")), "true")))
                {
                    v_opening__405363 = F_dv_find(v_edge__405125, "open_value", R.number("1"));
                }
                else
                {
                    v_opening__405363 = F_dv_find(v_edge__405125, "closed_value", R.number("0"));
                }
            }
            if (R.Bool(F_dv_has_key(v_edge__405125, "opening_ref")))
            {
                object v_gate__405695 = F_dv_read_query(v_world, F_dv_get(v_edge__405125, "opening_ref"));
                _ = F_dv_require(F_dv_uncontested(v_gate__405695), "Diffusion opening is unknown");
                v_opening__405363 = F_dv_get(v_gate__405695, "value");
            }
            _ = F_dv_unit(v_opening__405363);
            object v_conductance__405882 = F_dv_find(v_edge__405125, "conductance", R.number("0"));
            object v_wind__405940 = F_dv_find(v_edge__405125, "wind", R.number("0"));
            if (R.Bool((R.Bool(F_dv_has_key(v_edge__405125, "wind_when")) ? (object)(R.not(R.eq(F_dv_truth(v_world, F_dv_get(v_edge__405125, "wind_when")), "true"))) : (object)(false))))
            {
                v_wind__405940 = R.number("0");
            }
            object v_downward__406093 = F_dv_find(v_edge__405125, "downward", R.number("0"));
            _ = F_dv_require((R.Bool((R.Bool((R.Bool(R.not(R.lt(v_conductance__405882, R.number("0")))) ? (object)(R.not(R.lt(v_wind__405940, R.sub(R.number("0"), R.number("1"))))) : (object)(false))) ? (object)(R.not(R.lt(R.number("1"), v_wind__405940))) : (object)(false))) ? (object)(R.not(R.lt(v_downward__406093, R.number("0")))) : (object)(false)), "Invalid diffusion transport coefficient");
            object v_height__406272 = R.sub(F_dv_get(R.at(v_rooms__403934, v_a__405153), "height"), F_dv_get(R.at(v_rooms__403934, v_b__405213), "height"));
            object v_down_a__406327 = R.mul(F_dv_max(R.number("0"), v_height__406272), v_downward__406093);
            object v_down_b__406377 = R.mul(F_dv_max(R.number("0"), R.sub(R.number("0"), v_height__406272)), v_downward__406093);
            object v_raw__406431 = R.mul(R.mul(v_opening__405363, v_conductance__405882), R.sub(R.add(R.sub(R.add(R.sub(R.at(v_concentrations__404045, v_a__405153), R.at(v_concentrations__404045, v_b__405213)), R.mul(F_dv_max(R.number("0"), v_wind__405940), R.at(v_concentrations__404045, v_a__405153))), R.mul(F_dv_max(R.number("0"), R.sub(R.number("0"), v_wind__405940)), R.at(v_concentrations__404045, v_b__405213))), R.mul(v_down_a__406327, R.at(v_concentrations__404045, v_a__405153))), R.mul(v_down_b__406377, R.at(v_concentrations__404045, v_b__405213))));
            object v_source__406653 = v_a__405153;
            object v_target__406676 = v_b__405213;
            object v_amount__406699 = v_raw__406431;
            if (R.Bool(R.lt(v_raw__406431, R.number("0"))))
            {
                v_source__406653 = v_b__405213;
                v_target__406676 = v_a__405153;
                v_amount__406699 = R.sub(R.number("0"), v_raw__406431);
            }
            v_outgoing__404073 = F_dvs_replace(v_outgoing__404073, v_source__406653, R.add(R.at(v_outgoing__404073, v_source__406653), v_amount__406699));
            _ = R.push(v_flows__405061, new System.Collections.Generic.List<object>(3) { F_dv_pair("source", v_source__406653), F_dv_pair("target", v_target__406676), F_dv_pair("amount", v_amount__406699) });
            v_i__404119 = R.add(v_i__404119, R.number("1"));
        }
        object v_next__407151 = F_dv_clone(v_amounts__404024);
        v_i__404119 = R.number("0");
        while (R.Bool(R.lt(v_i__404119, R.count(v_flows__405061))))
        {
            object v_flow__407229 = R.at(v_flows__405061, v_i__404119);
            object v_scale__407257 = R.number("1");
            if (R.Bool(R.lt(R.at(v_amounts__404024, F_dv_get(v_flow__407229, "source")), R.at(v_outgoing__404073, F_dv_get(v_flow__407229, "source")))))
            {
                v_scale__407257 = R.div(R.at(v_amounts__404024, F_dv_get(v_flow__407229, "source")), R.at(v_outgoing__404073, F_dv_get(v_flow__407229, "source")));
            }
            object v_amount__407401 = R.mul(F_dv_get(v_flow__407229, "amount"), v_scale__407257);
            v_next__407151 = F_dvs_replace(v_next__407151, F_dv_get(v_flow__407229, "source"), R.sub(R.at(v_next__407151, F_dv_get(v_flow__407229, "source")), v_amount__407401));
            v_next__407151 = F_dvs_replace(v_next__407151, F_dv_get(v_flow__407229, "target"), R.add(R.at(v_next__407151, F_dv_get(v_flow__407229, "target")), v_amount__407401));
            v_i__404119 = R.add(v_i__404119, R.number("1"));
        }
        object v_changes__407604 = new System.Collections.Generic.List<object>(0) { };
        object v_concentrations_changed__407625 = false;
        v_i__404119 = R.number("0");
        while (R.Bool(R.lt(v_i__404119, R.count(v_rooms__403934))))
        {
            object v_room__407709 = R.at(v_rooms__403934, v_i__404119);
            object v_settled__407737 = R.mul(F_dv_max(R.number("0"), R.at(v_next__407151, v_i__404119)), R.sub(R.number("1"), F_dv_find(v_room__407709, "settling", R.number("0"))));
            object v_value__407815 = R.div(v_settled__407737, F_dv_find(v_room__407709, "volume", R.number("1")));
            if (R.Bool(R.not(R.lt(R.at(v_boundaries__404095, v_i__404119), R.number("0")))))
            {
                v_value__407815 = R.at(v_boundaries__404095, v_i__404119);
            }
            object v_previous__407937 = F_dv_get(F_dv_read_query(v_world, F_dv_get(v_room__407709, "concentration_ref")), "value");
            object v_boundary_exchange__408005 = R.sub(R.add(R.mul(R.sub(R.at(v_concentrations__404045, v_i__404119), v_previous__407937), F_dv_find(v_room__407709, "volume", R.number("1"))), R.mul(v_value__407815, F_dv_find(v_room__407709, "volume", R.number("1")))), v_settled__407737);
            if (R.Bool(R.not(R.eq(v_value__407815, v_previous__407937))))
            {
                v_world = R.snapshot(F_dv_store(v_world, F_dv_get(v_room__407709, "concentration_ref"), F_dv_cell(v_value__407815, R.number("1"), v_time, new System.Collections.Generic.List<object>(1) { v_parent })));
                v_concentrations_changed__407625 = true;
            }
            if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_value__407815, v_previous__407937))) ? (object)(true) : (object)(R.not(R.eq(v_boundary_exchange__408005, R.number("0")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_max(R.number("0"), R.at(v_next__407151, v_i__404119)), v_settled__407737))))))
            {
                _ = R.push(v_changes__407604, new System.Collections.Generic.List<object>(5) { F_dv_pair("room", F_dv_get(v_room__407709, "id")), F_dv_pair("before", v_previous__407937), F_dv_pair("after", v_value__407815), F_dv_pair("boundary_exchange", v_boundary_exchange__408005), F_dv_pair("settled_mass", R.sub(F_dv_max(R.number("0"), R.at(v_next__407151, v_i__404119)), v_settled__407737)) });
            }
            v_i__404119 = R.add(v_i__404119, R.number("1"));
        }
        if (R.Bool(v_concentrations_changed__407625))
        {
            v_world = F_dv_with(v_world, "revision", R.add(F_dv_get(v_world, "revision"), R.number("1")));
        }
        _ = F_dvs_event(v_events, "simulation_macro", "", v_time, new System.Collections.Generic.List<object>(1) { v_parent }, new System.Collections.Generic.List<object>(5) { F_dv_pair("changes", v_changes__407604), F_dv_pair("period_minutes", R.number("15")), F_dv_pair("approximation", true), F_dv_pair("model", "synchronous-room-graph"), F_dv_pair("precision_certificate", false) });
        return v_world;
    }

    public static object F_dvs_simulation_calculated_rule(object v_world, object v_rule, object v_time)
    {
        v_world = R.snapshot(v_world);
        v_rule = R.snapshot(v_rule);
        object v_premise__409181 = new System.Collections.Generic.List<object>(1) { F_dv_find(v_rule, "when", F_dv_true()) };
        object v_effects__409234 = F_dv_clone(F_dv_find(v_rule, "effects", new System.Collections.Generic.List<object>(0) { }));
        object v_calculations__409291 = F_dv_find(v_rule, "calculations", new System.Collections.Generic.List<object>(0) { });
        object v_i__409348 = R.number("0");
        while (R.Bool(R.lt(v_i__409348, R.count(v_calculations__409291))))
        {
            object v_calculation__409404 = R.at(v_calculations__409291, v_i__409348);
            object v_value__409446 = F_dv_find(v_calculation__409404, "constant", R.number("0"));
            if (R.Bool(F_dv_find(v_calculation__409404, "clock", false)))
            {
                v_value__409446 = v_time;
            }
            if (R.Bool(F_dv_has_key(v_calculation__409404, "modulo")))
            {
                _ = F_dv_require(R.lt(R.number("0"), F_dv_get(v_calculation__409404, "modulo")), "Settlement modulus must be positive");
                v_value__409446 = R.sub(v_value__409446, R.mul(R.floor(R.div(v_value__409446, F_dv_get(v_calculation__409404, "modulo"))), F_dv_get(v_calculation__409404, "modulo")));
            }
            object v_terms__409794 = F_dv_find(v_calculation__409404, "terms", new System.Collections.Generic.List<object>(0) { });
            object v_j__409848 = R.number("0");
            while (R.Bool(R.lt(v_j__409848, R.count(v_terms__409794))))
            {
                object v_term__409905 = R.at(v_terms__409794, v_j__409848);
                object v_when__409937 = F_dv_find(v_term__409905, "when", F_dv_true());
                object v_truth__409993 = F_dv_truth(v_world, v_when__409937);
                if (R.Bool((R.Bool(R.not(R.eq(v_truth__409993, "true"))) ? (object)(R.not(R.eq(v_truth__409993, "false"))) : (object)(false))))
                {
                    return new System.Collections.Generic.List<object>(3) { F_dv_pair("ready", false), F_dv_pair("reason", "unknown-term-condition"), F_dv_pair("ref", F_dv_get(v_calculation__409404, "ref")) };
                }
                if (R.Bool(R.eq(v_truth__409993, "true")))
                {
                    _ = R.push(v_premise__409181, v_when__409937);
                    if (R.Bool(F_dv_has_key(v_term__409905, "ref")))
                    {
                        object v_cell__410315 = F_dv_read_query(v_world, F_dv_get(v_term__409905, "ref"));
                        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_cell__410315))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_cell__410315, "value")))))))
                        {
                            return new System.Collections.Generic.List<object>(3) { F_dv_pair("ready", false), F_dv_pair("reason", "unknown-numeric-input"), F_dv_pair("ref", F_dv_get(v_term__409905, "ref")) };
                        }
                        _ = R.push(v_premise__409181, F_dv_fact(F_dv_get(v_term__409905, "ref"), F_dv_get(v_cell__410315, "value")));
                        v_value__409446 = R.add(v_value__409446, R.mul(F_dv_get(v_cell__410315, "value"), F_dv_find(v_term__409905, "factor", R.number("1"))));
                    }
                    else
                    {
                        v_value__409446 = R.add(v_value__409446, F_dv_get(v_term__409905, "constant"));
                    }
                }
                else
                {
                    _ = R.push(v_premise__409181, F_dv_not(v_when__409937));
                }
                v_j__409848 = R.add(v_j__409848, R.number("1"));
            }
            if (R.Bool(F_dv_has_key(v_calculation__409404, "minimum")))
            {
                v_value__409446 = F_dv_max(F_dv_get(v_calculation__409404, "minimum"), v_value__409446);
            }
            if (R.Bool(F_dv_has_key(v_calculation__409404, "maximum")))
            {
                v_value__409446 = F_dv_min(F_dv_get(v_calculation__409404, "maximum"), v_value__409446);
            }
            _ = R.push(v_effects__409234, new System.Collections.Generic.List<object>(3) { F_dv_pair("op", F_dv_find(v_calculation__409404, "op", "assign")), F_dv_pair("ref", F_dv_get(v_calculation__409404, "ref")), F_dv_pair("value", v_value__409446) });
            v_i__409348 = R.add(v_i__409348, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("ready", true), F_dv_pair("law", F_dv_axiom(F_dv_get(v_rule, "law"), R.number("1"), F_dv_all(v_premise__409181), v_effects__409234)) };
    }

    public static object F_dvs_simulation_execute_rule(object v_world, object v_rule, object v_events, object v_parent, object v_time, object v_kind)
    {
        v_world = R.snapshot(v_world);
        v_rule = R.snapshot(v_rule);
        if (R.Bool(R.not(R.eq(F_dv_truth(v_world, F_dv_find(v_rule, "when", F_dv_true())), "true"))))
        {
            return v_world;
        }
        if (R.Bool(F_dv_has_key(v_rule, "calculations")))
        {
            object v_calculated__411470 = R.snapshot(F_dvs_simulation_calculated_rule(v_world, v_rule, v_time));
            if (R.Bool(R.not(F_dv_get(v_calculated__411470, "ready"))))
            {
                _ = F_dvs_event(v_events, v_kind, "", v_time, new System.Collections.Generic.List<object>(1) { v_parent }, new System.Collections.Generic.List<object>(4) { F_dv_pair("id", F_dv_get(v_rule, "id")), F_dv_pair("status", "unresolved-input"), F_dv_pair("detail", v_calculated__411470), F_dv_pair("approximation", true) });
                return v_world;
            }
            object v_basis__411751 = F_dv_find(v_rule, "basis", new System.Collections.Generic.List<object>(1) { v_parent });
            if (R.Bool(F_dv_has_key(F_dv_get(v_world, "adoptions"), F_dv_get(v_rule, "law"))))
            {
                object v_previous__411858 = F_dv_get(F_dv_get(v_world, "adoptions"), F_dv_get(v_rule, "law"));
                if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_previous__411858, "active"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_previous__411858, "mode"), "enforce"))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_previous__411858, "confidence"), R.number("1")))))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_previous__411858, "law"), F_dv_get(v_calculated__411470, "law")))))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_previous__411858, "basis"), v_basis__411751))))))
                {
                    v_world = F_dv_reconsider(v_world, F_dv_get(v_calculated__411470, "law"), "enforce", R.number("1"), v_basis__411751);
                }
            }
            else
            {
                v_world = F_dv_adopt(v_world, F_dv_get(v_calculated__411470, "law"), "enforce", R.number("1"), v_basis__411751);
            }
        }
        v_world = R.snapshot(v_world);
        object v_execution__412416 = R.snapshot(F_dv_execute(v_world, F_dv_get(v_rule, "law"), R.cat(v_kind, R.cat("-", R.show(R.count(v_events)))), v_time, F_dv_get(v_world, "revision")));
        _ = F_dvs_event(v_events, v_kind, "", v_time, new System.Collections.Generic.List<object>(1) { v_parent }, new System.Collections.Generic.List<object>(4) { F_dv_pair("id", F_dv_get(v_rule, "id")), F_dv_pair("category", F_dv_find(v_rule, "category", "")), F_dv_pair("execution", F_dv_without(v_execution__412416, "world")), F_dv_pair("approximation", true) });
        return F_dv_get(v_execution__412416, "world");
    }

    public static object F_dvs_simulation_daily(object v_world, object v_config, object v_events, object v_parent, object v_time)
    {
        v_world = R.snapshot(v_world);
        v_config = R.snapshot(v_config);
        object v_rules__412806 = F_dv_find(v_config, "daily_rules", new System.Collections.Generic.List<object>(0) { });
        object v_daytime__412857 = R.sub(v_time, R.mul(R.floor(R.div(v_time, R.number("1440"))), R.number("1440")));
        object v_i__412911 = R.number("0");
        while (R.Bool(R.lt(v_i__412911, R.count(v_rules__412806))))
        {
            object v_rule__412960 = R.at(v_rules__412806, v_i__412911);
            _ = F_dv_require((R.Bool(R.eq(F_dv_get(v_rule__412960, "at"), R.number("360"))) ? (object)(true) : (object)(R.eq(F_dv_get(v_rule__412960, "at"), R.number("1080")))), "Daily rules must use 06:00 or 18:00");
            if (R.Bool(R.eq(F_dv_get(v_rule__412960, "at"), v_daytime__412857)))
            {
                v_world = F_dvs_simulation_execute_rule(v_world, v_rule__412960, v_events, v_parent, v_time, "simulation_daily");
                v_world = R.snapshot(F_dv_with(v_world, "simulation", F_dv_with(F_dv_get(v_world, "simulation"), "daily_count", R.add(F_dv_get(F_dv_get(v_world, "simulation"), "daily_count"), R.number("1")))));
            }
            v_i__412911 = R.add(v_i__412911, R.number("1"));
        }
        return v_world;
    }

    public static object F_dvs_simulation_unmanaged_processes(object v_world, object v_processes)
    {
        // Only frozen inputs may reuse this content-only projection. Mutable
        // callers and disabled declaration caches retain the original path.
        if (v_world is List<object> || v_processes is List<object>
            || !R.IsDvList(v_world) || !R.IsDvList(v_processes)
            || !R.Bool(R.memo_group_enabled("declaration")))
            return SimulationUnmanagedProcesses(v_world, v_processes, out _);
        if (!R.Bool(F_dv_has_key(v_world, "simulation")))
            return SimulationUnmanagedProcesses(v_world, v_processes, out _);

        // The complete configuration contains both daily_rules and macro_rules.
        // Reading it preserves the original access order; a hit is possible only
        // after these exact immutable declarations previously succeeded.
        var config = F_dv_get(F_dv_get(v_world, "simulation"), "config");
        if (config is List<object> || !R.IsDvList(config))
            return SimulationUnmanagedProcesses(v_world, v_processes, out _);
        var key = new List<object>(2) { "simulation-unmanaged-processes-v1", config };
        var cached = R.memo_read(v_processes, key);
        if (R.Bool(R.at(cached, 0d)))
            return R.copy_value(R.at(cached, 1d));

        var result = SimulationUnmanagedProcesses(v_world, v_processes, out var textLawIds);
        // With no managed rules the original function returns the input itself;
        // do not cache that case or change its identity/return contract. Filtered
        // results keep their independently writable root on both miss and hit.
        if (textLawIds && result is List<object>) _ = R.memo_put(v_processes, key, result);
        return result;
    }

    private static object SimulationUnmanagedProcesses(object v_world, object v_processes, out bool textLawIds)
    {
        textLawIds = true;
        v_world = R.snapshot(v_world);
        v_processes = R.snapshot(v_processes);
        if (R.Bool(R.not(F_dv_has_key(v_world, "simulation"))))
        {
            return v_processes;
        }
        object v_daily__413498 = F_dv_clone(F_dv_find(F_dv_get(F_dv_get(v_world, "simulation"), "config"), "daily_rules", new System.Collections.Generic.List<object>(0) { }));
        object v_macro__413576 = F_dv_find(F_dv_get(F_dv_get(v_world, "simulation"), "config"), "macro_rules", new System.Collections.Generic.List<object>(0) { });
        object v_m__413644 = R.number("0");
        while (R.Bool(R.lt(v_m__413644, R.count(v_macro__413576))))
        {
            _ = R.push(v_daily__413498, R.at(v_macro__413576, v_m__413644));
            v_m__413644 = R.add(v_m__413644, R.number("1"));
        }
        if (R.Bool(R.eq(R.count(v_daily__413498), R.number("0"))))
        {
            return v_processes;
        }
        object v_result__413794 = new System.Collections.Generic.List<object>(0) { };
        object v_i__413814 = R.number("0");
        while (R.Bool(R.lt(v_i__413814, R.count(v_processes))))
        {
            object v_managed__413867 = false;
            object v_j__413895 = R.number("0");
            while (R.Bool(R.lt(v_j__413895, R.count(v_daily__413498))))
            {
                var managedLaw = F_dv_get(R.at(v_daily__413498, v_j__413895), "law");
                var processLaw = F_dv_get(R.at(v_processes, v_i__413814), "law");
                // Non-text law values can have reference equality semantics.
                // Observe the original operands in order and never cache them.
                if (managedLaw is not string || processLaw is not string) textLawIds = false;
                if (R.Bool(R.eq(managedLaw, processLaw)))
                {
                    v_managed__413867 = true;
                }
                v_j__413895 = R.add(v_j__413895, R.number("1"));
            }
            if (R.Bool(R.not(v_managed__413867)))
            {
                _ = R.push(v_result__413794, R.at(v_processes, v_i__413814));
            }
            v_i__413814 = R.add(v_i__413814, R.number("1"));
        }
        return v_result__413794;
    }

    public static object F_dvs_simulation_environment(object v_world, object v_story, object v_events, object v_parent)
    {
        v_world = R.snapshot(v_world);
        v_story = R.snapshot(v_story);
        if (R.Bool(R.not(F_dv_has_key(v_world, "simulation"))))
        {
            return v_world;
        }
        object v_time__414275 = F_dv_get(v_world, "time");
        object v_config__414301 = F_dv_get(F_dv_get(v_world, "simulation"), "config");
        object v_next__414342 = R.add(F_dv_get(F_dv_get(v_world, "simulation"), "last_macro"), R.number("15"));
        while (R.Bool(R.not(R.lt(v_time__414275, v_next__414342))))
        {
            v_world = R.snapshot(F_dv_with(v_world, "time", v_next__414342));
            object v_clock__414463 = F_dv_find(v_story, "clock_ref", false);
            if (R.Bool(R.not(R.eq(v_clock__414463, false))))
            {
                v_world = R.snapshot(F_dv_store(v_world, v_clock__414463, F_dv_cell(v_next__414342, R.number("1"), v_next__414342, new System.Collections.Generic.List<object>(1) { v_parent })));
            }
            object v_macro_rules__414622 = F_dv_find(v_config__414301, "macro_rules", new System.Collections.Generic.List<object>(0) { });
            object v_m__414683 = R.number("0");
            while (R.Bool(R.lt(v_m__414683, R.count(v_macro_rules__414622))))
            {
                if (R.Bool(F_dv_find(R.at(v_macro_rules__414622, v_m__414683), "before_diffusion", false)))
                {
                    v_world = F_dvs_simulation_execute_rule(v_world, R.at(v_macro_rules__414622, v_m__414683), v_events, v_parent, v_next__414342, "simulation_environment_effect");
                }
                v_m__414683 = R.add(v_m__414683, R.number("1"));
            }
            v_world = R.snapshot(F_dvs_simulation_diffuse(v_world, v_config__414301, v_events, v_parent, v_next__414342));
            v_m__414683 = R.number("0");
            while (R.Bool(R.lt(v_m__414683, R.count(v_macro_rules__414622))))
            {
                if (R.Bool(R.not(F_dv_find(R.at(v_macro_rules__414622, v_m__414683), "before_diffusion", false))))
                {
                    v_world = F_dvs_simulation_execute_rule(v_world, R.at(v_macro_rules__414622, v_m__414683), v_events, v_parent, v_next__414342, "simulation_environment_effect");
                }
                v_m__414683 = R.add(v_m__414683, R.number("1"));
            }
            v_world = F_dvs_simulation_daily(v_world, v_config__414301, v_events, v_parent, v_next__414342);
            v_world = R.snapshot(F_dv_with(v_world, "simulation", F_dv_with_many(F_dv_get(v_world, "simulation"), new System.Collections.Generic.List<object>(2) { F_dv_pair("last_macro", v_next__414342), F_dv_pair("macro_count", R.add(F_dv_get(F_dv_get(v_world, "simulation"), "macro_count"), R.number("1"))) })));
            v_next__414342 = R.add(v_next__414342, R.number("15"));
        }
        return F_dv_with(v_world, "time", v_time__414275);
    }
}
}
