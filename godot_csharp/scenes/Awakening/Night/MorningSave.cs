using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.SessionPersistence;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>Quiescent chapter adapter state. All references are engine IDs, never display names.</summary>
public sealed partial class MorningWorld
{
    internal sealed record MindSave([property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,SubjectProjectionId> Nodes,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,byte[]> Effects,[property:System.Text.Json.Serialization.JsonRequired] string Memory);
    internal sealed record StepSave([property:System.Text.Json.Serialization.JsonRequired] string Room,[property:System.Text.Json.Serialization.JsonRequired] string Target,[property:System.Text.Json.Serialization.JsonRequired] string Action,[property:System.Text.Json.Serialization.JsonRequired] string Note,[property:System.Text.Json.Serialization.JsonRequired] int Weight,[property:System.Text.Json.Serialization.JsonRequired] double Seconds,[property:System.Text.Json.Serialization.JsonRequired] HashSet<string> AllowedStages);
    internal sealed record DailySave([property:System.Text.Json.Serialization.JsonRequired] string Key,[property:System.Text.Json.Serialization.JsonRequired] string Actor,[property:System.Text.Json.Serialization.JsonRequired] EntityId Soul,[property:System.Text.Json.Serialization.JsonRequired] EntityId Body,[property:System.Text.Json.Serialization.JsonRequired] SubjectProjectionId Self,[property:System.Text.Json.Serialization.JsonRequired] double Speed,[property:System.Text.Json.Serialization.JsonRequired] Person? Hosted,
        [property:System.Text.Json.Serialization.JsonRequired] StepSave[] Steps,[property:System.Text.Json.Serialization.JsonRequired] HashSet<string>? AllowedStages,[property:System.Text.Json.Serialization.JsonRequired] SubjectProjectionId[] Ready,[property:System.Text.Json.Serialization.JsonRequired] byte[][] Effects,[property:System.Text.Json.Serialization.JsonRequired] int Index,[property:System.Text.Json.Serialization.JsonRequired] int Serial,
        [property:System.Text.Json.Serialization.JsonRequired] double Remaining,[property:System.Text.Json.Serialization.JsonRequired] double StartAfter,[property:System.Text.Json.Serialization.JsonRequired] bool Arrived,[property:System.Text.Json.Serialization.JsonRequired] int? DepartureMinutes,[property:System.Text.Json.Serialization.JsonRequired] string Dorm,[property:System.Text.Json.Serialization.JsonRequired] StepSave[] DepartureSteps,
        [property:System.Text.Json.Serialization.JsonRequired] bool Departed,[property:System.Text.Json.Serialization.JsonRequired] bool DepartureComplete,[property:System.Text.Json.Serialization.JsonRequired] bool FieldResident);
    internal sealed record ModelSave([property:System.Text.Json.Serialization.JsonRequired] string Id,[property:System.Text.Json.Serialization.JsonRequired] ImmutableArray<string> Questions,[property:System.Text.Json.Serialization.JsonRequired] ImmutableArray<ImmutableArray<string>> Options,
        [property:System.Text.Json.Serialization.JsonRequired] ImmutableArray<CaseField> Fields,[property:System.Text.Json.Serialization.JsonRequired] ImmutableArray<CaseHypothesis> Hypotheses,[property:System.Text.Json.Serialization.JsonRequired] ImmutableArray<int?> Defaults);
    internal sealed record TraceSave([property:System.Text.Json.Serialization.JsonRequired] string Key,[property:System.Text.Json.Serialization.JsonRequired] string Text,[property:System.Text.Json.Serialization.JsonRequired] int Needs,[property:System.Text.Json.Serialization.JsonRequired] int Value);
    internal sealed record DormSave([property:System.Text.Json.Serialization.JsonRequired] string Id,[property:System.Text.Json.Serialization.JsonRequired] string Dorm,[property:System.Text.Json.Serialization.JsonRequired] string Title,[property:System.Text.Json.Serialization.JsonRequired] ModelSave Model,[property:System.Text.Json.Serialization.JsonRequired] int Answer,[property:System.Text.Json.Serialization.JsonRequired] TraceSave[] Traces,
        [property:System.Text.Json.Serialization.JsonRequired] int Progress,[property:System.Text.Json.Serialization.JsonRequired] int Revealed,[property:System.Text.Json.Serialization.JsonRequired] int EarliestOut,[property:System.Text.Json.Serialization.JsonRequired] bool Answered);
    internal sealed record MorningSave([property:System.Text.Json.Serialization.JsonRequired] Dictionary<Person,EntityId> Souls,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<Person,EntityId> Bodies,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<Room,EntityId> Spaces,
        [property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,EntityId> Evidence,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<Person,SubjectProjectionId> Desires,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<Person,SubjectProjectionId> Recalled,
        [property:System.Text.Json.Serialization.JsonRequired] Dictionary<Person,MindSave> Minds,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<Person,int> Hooks,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,Room> EvidenceRooms,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<Person,string> Unheeded,
        [property:System.Text.Json.Serialization.JsonRequired] string[] Destroyed,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,bool> Joints,[property:System.Text.Json.Serialization.JsonRequired] double Time,
        [property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,EntityId> DailySpaces,[property:System.Text.Json.Serialization.JsonRequired] DailySave[] DailyPeople,[property:System.Text.Json.Serialization.JsonRequired] MorningTrajectory[] Trajectory,[property:System.Text.Json.Serialization.JsonRequired] MorningDailyDecision[] Decisions,
        [property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,string[]> Witnesses,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,string[]> RemoteWitnesses,
        [property:System.Text.Json.Serialization.JsonRequired] double DailyNow,[property:System.Text.Json.Serialization.JsonRequired] long DailySequence,[property:System.Text.Json.Serialization.JsonRequired] int DailySerial,[property:System.Text.Json.Serialization.JsonRequired] bool DailyClosed,[property:System.Text.Json.Serialization.JsonRequired] string DailyStage,[property:System.Text.Json.Serialization.JsonRequired] string[] DailyStages,
        [property:System.Text.Json.Serialization.JsonRequired] string DetectiveControl,[property:System.Text.Json.Serialization.JsonRequired] int GameMinutes,[property:System.Text.Json.Serialization.JsonRequired] int DailySettledMinutes,[property:System.Text.Json.Serialization.JsonRequired] double SecondsPerGameMinute,
        [property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,DormSave> DormCases,[property:System.Text.Json.Serialization.JsonRequired] DormCaseResult[] DormResults,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,int> DormNeeds,
        [property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,int> DormProgress,[property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,int> DormOpened,[property:System.Text.Json.Serialization.JsonRequired] bool ScriptWorld);

    internal ContractResult<bool> CheckSavePoint() => Try(() =>
    {
        Value(_session.CheckSavePoint());
        if(!_dailyClosed) Value(_dailySession.CheckSavePoint());
        Require(_storyActions.Values.All(a=>a.Status!=ExecutionStatus.Pending),"chapter.actions",ContractErrorCode.UnsupportedSavePoint);
        Require(_dailyPeople.Values.All(p=>p.Execution is null&&p.Serial==0),"chapter.ecology",ContractErrorCode.UnsupportedSavePoint);
        Require(_continuation is null,"chapter.handoff",ContractErrorCode.UnsupportedSavePoint);
        return true;
    });
    private static SubjectProjectionId Handle(Projection p) => p.SubjectId ?? throw new CognitionContractException(new(ContractErrorCode.InvalidReference,"chapter.projection","需要主体句柄。",CandidateIds:[]));
    internal MorningSave CaptureSave()
    {
        Value(CheckSavePoint());
        Dictionary<SubjectProjectionId,Projection> Map(SoulEssence soul)=>soul.World.Projections.ToDictionary(Handle);
        byte[] EffectSave(Effect e,SoulEssence soul)=>Value(CognitionCodec.EncodeEffect(e,Map(soul)));
        MindSave MindState(Person who,Mind m)=>new(new()
        {
            ["self"]=Handle(m.Self),["desire"]=Handle(m.Desire),["demon"]=Handle(m.Demon),["rule1"]=Handle(m.Rule1),
            ["detective"]=Handle(m.Detective),["object"]=Handle(m.Object),["advice_touch"]=Handle(m.AdviceTouch),
            ["advice_decline"]=Handle(m.AdviceDecline),["advice_own"]=Handle(m.AdviceOwn),["whisper"]=Handle(m.Whisper),
            ["settled"]=Handle(m.Settled),["room"]=Handle(m.Room),["habits"]=Handle(m.Habits)
        },new() { ["touch"]=EffectSave(m.Touch,_souls[who]),["decline"]=EffectSave(m.Decline,_souls[who]),["reach"]=EffectSave(m.Reach,_souls[who]),["stay"]=EffectSave(m.Stay,_souls[who]) },m.Memory.Evidence);
        static StepSave Step(DailyStep s)=>new(s.Room,s.Target,s.Action,s.Note,s.Weight,s.Seconds,s.AllowedStages);
        static ModelSave Model(CaseModel m)=>new(m.Id,m.Questions,m.Options,m.Fields,m.Hypotheses,m.Defaults);
        return new(_souls.ToDictionary(p=>p.Key,p=>p.Value.Id),_bodies.ToDictionary(p=>p.Key,p=>p.Value.Id),_space.ToDictionary(p=>p.Key,p=>p.Value.Id),
            _evidence.ToDictionary(p=>p.Key,p=>p.Value.Id),_desires.ToDictionary(p=>p.Key,p=>Handle(p.Value)),_recalled.ToDictionary(p=>p.Key,p=>Handle(p.Value)),
            _minds.ToDictionary(p=>p.Key,p=>MindState(p.Key,p.Value)),new(_hooks),new(_evidenceRoom),new(_unheeded),[.._destroyed.Order(StringComparer.Ordinal)],new(_joints),_time,
            _dailySpaces.ToDictionary(p=>p.Key,p=>p.Value.Id),[.._dailyPeople.Values.Select(p=>new DailySave(p.Key,p.Actor,p.Soul.Id,p.Body.Id,Handle(p.Self),p.Speed,p.Hosted,
                [..p.Steps.Select(Step)],p.AllowedStages,[..p.Ready.Select(Handle)],[..p.Effects.Select(e=>EffectSave(e,p.Soul))],p.Index,p.Serial,p.Remaining,p.StartAfter,p.Arrived,p.DepartureMinutes,p.Dorm,
                [..p.DepartureSteps.Select(Step)],p.Departed,p.DepartureComplete,p.FieldResident))],
            [.._dailyTrajectory],[.._dailyDecisions],_dailyWitnesses.ToDictionary(p=>p.Key,p=>p.Value.ToArray()),
            new() { ["room"]=[.._dailyRemoteWitnesses.Select(p=>p.Room)],["text"]=[.._dailyRemoteWitnesses.Select(p=>p.Text)] },
            _dailySession.CTB.Now,_dailySession.CTB.Sequence,_dailySerial,_dailyClosed,_dailyStage,[.._dailyStages.Order(StringComparer.Ordinal)],
            DetectiveControl,GameMinutes,_dailySettledMinutes,_secondsPerGameMinute,
            _dormCases.ToDictionary(p=>p.Key,p=>new DormSave(p.Value.Id,p.Value.Dorm,p.Value.Title,Model(p.Value.Model),p.Value.Answer,
                [..p.Value.Traces.Select(t=>new TraceSave(t.Key,t.Text,t.Needs,t.Value))],p.Value.Progress,p.Value.Revealed,p.Value.EarliestOut,p.Value.Answered)),
            [.._dormResults],new(_dormNeeds),new(_dormProgress),new(_dormOpened),_scriptWorld is not null);
    }
    // This constructor bypasses authored initial facts, observations, and default property installation.
    private MorningWorld(WorldSession session,MorningSave d)
    {
        _persistentSession=session; _runtime=session.Runtime; _session=session.Simulation;
        var entities=_runtime.PersistenceEntities;
        Essence Entity(EntityId id) { Require(entities.TryGetValue(id,out var e),"chapter.entity",ContractErrorCode.InvalidReference); return e!; }
        var maps=entities.Values.OfType<SoulEssence>().ToDictionary(s=>s.World.SubjectWorldId!.Value,s=>s.World.Projections.ToDictionary(Handle));
        Projection P(SubjectProjectionId id) { Require(maps.TryGetValue(id.SubjectWorld,out var m)&&m.ContainsKey(id),"chapter.projection",ContractErrorCode.InvalidReference); return maps[id.SubjectWorld][id]; }
        Effect E(byte[] b,SoulEssence soul)=>Value(CognitionCodec.RestoreEffect(b,maps[soul.World.SubjectWorldId!.Value]));
        foreach(var p in d.Souls) { Require(Enum.IsDefined(p.Key)&&Entity(p.Value) is SoulEssence,"souls"); _souls.Add(p.Key,(SoulEssence)Entity(p.Value)); }
        foreach(var p in d.Bodies) { Require(Enum.IsDefined(p.Key)&&Entity(p.Value).Kind==ProjectionKind.Body,"bodies"); _bodies.Add(p.Key,Entity(p.Value)); }
        foreach(var p in d.Spaces) { Require(Enum.IsDefined(p.Key)&&Entity(p.Value).Kind==ProjectionKind.Space,"spaces"); _space.Add(p.Key,Entity(p.Value)); }
        foreach(var p in d.Evidence) _evidence.Add(p.Key,Entity(p.Value));
        foreach(var p in d.Desires) _desires.Add(p.Key,P(p.Value)); foreach(var p in d.Recalled) _recalled.Add(p.Key,P(p.Value));
        foreach(var pair in d.Minds)
        {
            var dMind=pair.Value; var soul=_souls[pair.Key]; Projection N(string key)=>P(dMind.Nodes[key]);
            Require(dMind.Nodes.Count==13&&dMind.Effects.Count==4,"mind.fields");
            _minds.Add(pair.Key,new Mind { Self=N("self"),Desire=N("desire"),Demon=N("demon"),Rule1=N("rule1"),Detective=N("detective"),Object=N("object"),
                AdviceTouch=N("advice_touch"),AdviceDecline=N("advice_decline"),AdviceOwn=N("advice_own"),Whisper=N("whisper"),Settled=N("settled"),Room=N("room"),Habits=N("habits"),
                Touch=E(dMind.Effects["touch"],soul),Decline=E(dMind.Effects["decline"],soul),Reach=E(dMind.Effects["reach"],soul),Stay=E(dMind.Effects["stay"],soul),Memory=Memories.Single(m=>m.Evidence==dMind.Memory&&m.Who==pair.Key) });
            Require(dMind.Nodes.Values.All(id=>id.SubjectWorld==soul.World.SubjectWorldId),"mind.subject",ContractErrorCode.InvalidReference);
        }
        foreach(var p in d.Hooks) _hooks.Add(p.Key,p.Value); foreach(var p in d.EvidenceRooms) { Require(Enum.IsDefined(p.Value),"evidence_room"); _evidenceRoom.Add(p.Key,p.Value); }
        foreach(var p in d.Unheeded) _unheeded.Add(p.Key,p.Value); _destroyed.UnionWith(d.Destroyed); _joints.Clear(); foreach(var p in d.Joints) _joints.Add(p.Key,p.Value); _time=d.Time;
        _dailySession=new(_runtime,new CTBSystem(d.DailyNow,d.DailySequence)); _dailySession.Completed+=DailyCompleted;
        foreach(var p in d.DailySpaces) _dailySpaces.Add(p.Key,Entity(p.Value));
        static DailyStep Step(StepSave s)=>new(s.Room,s.Target,s.Action,s.Note,s.Weight,s.Seconds,s.AllowedStages);
        foreach(var p in d.DailyPeople)
        {
            Require(p.Steps is not null&&p.Ready.Length==p.Steps.Length&&p.Effects.Length==p.Steps.Length&&
                p.Index>=0&&(p.Steps.Length==0?p.Index==0:p.Index<p.Steps.Length)&&p.Serial==0&&double.IsFinite(p.Speed)&&p.Speed>0&&double.IsFinite(p.Remaining)&&p.Remaining>=0&&double.IsFinite(p.StartAfter)&&p.StartAfter>=0,"ecology");
            var soul=Entity(p.Soul) as SoulEssence; Require(soul is not null&&soul.World.Contains(P(p.Self)),"ecology.self",ContractErrorCode.InvalidReference);
            _dailyPeople.Add(p.Key,new DailyPerson { Key=p.Key,Actor=p.Actor,Soul=soul!,Body=Entity(p.Body),Self=P(p.Self),Speed=p.Speed,Hosted=p.Hosted,
                Steps=[..p.Steps!.Select(Step)],AllowedStages=p.AllowedStages,Ready=[..p.Ready.Select(P)],Effects=[..p.Effects.Select(b=>E(b,soul!))],
                Index=p.Index,Serial=p.Serial,Remaining=p.Remaining,StartAfter=p.StartAfter,Arrived=p.Arrived,DepartureMinutes=p.DepartureMinutes,Dorm=p.Dorm,
                DepartureSteps=[..p.DepartureSteps.Select(Step)],Departed=p.Departed,DepartureComplete=p.DepartureComplete,FieldResident=p.FieldResident });
        }
        _dailyTrajectory.AddRange(d.Trajectory); _dailyDecisions.AddRange(d.Decisions); foreach(var p in d.Witnesses) _dailyWitnesses.Add(p.Key,new(p.Value));
        Require(d.RemoteWitnesses.Count==2&&d.RemoteWitnesses["room"].Length==d.RemoteWitnesses["text"].Length,"remote_witnesses");
        _dailyRemoteWitnesses.AddRange(d.RemoteWitnesses["room"].Zip(d.RemoteWitnesses["text"]));
        _dailySerial=d.DailySerial; _dailyClosed=d.DailyClosed; _dailyStage=d.DailyStage; _dailyStages.UnionWith(d.DailyStages);
        Require(d.DetectiveControl is "player" or "simulation"&&d.GameMinutes>=0&&d.DailySettledMinutes>=0&&d.DailySettledMinutes<=d.GameMinutes&&double.IsFinite(d.SecondsPerGameMinute)&&d.SecondsPerGameMinute>0&&double.IsFinite(d.Time),"clock");
        DetectiveControl=d.DetectiveControl; GameMinutes=d.GameMinutes; _dailySettledMinutes=d.DailySettledMinutes; _secondsPerGameMinute=d.SecondsPerGameMinute;
        foreach(var p in d.DormCases)
        {
            var c=p.Value; var m=c.Model;
            var model=new CaseModel(m.Id,m.Questions,m.Options.Select(o=>o.AsEnumerable()),m.Fields,m.Hypotheses,m.Defaults);
            Require(c.Progress>=0&&c.Revealed>=0&&c.Revealed<=c.Traces.Length,"dorm_case");
            _dormCases.Add(p.Key,new DormCase { Id=c.Id,Dorm=c.Dorm,Title=c.Title,Model=model,Answer=c.Answer,Traces=[..c.Traces.Select(t=>(t.Key,t.Text,t.Needs,t.Value))],Progress=c.Progress,Revealed=c.Revealed,EarliestOut=c.EarliestOut,Answered=c.Answered });
        }
        _dormResults.AddRange(d.DormResults); foreach(var p in d.DormNeeds) _dormNeeds.Add(p.Key,p.Value); foreach(var p in d.DormProgress) _dormProgress.Add(p.Key,p.Value); foreach(var p in d.DormOpened) _dormOpened.Add(p.Key,p.Value);
        if(d.ScriptWorld)
        {
            _scriptWorld=new(_runtime,_session); _session.Completed+=_scriptWorld.RefreshAfter;
            foreach(var p in _space) _scriptWorld.Register("room:"+p.Key,p.Value); foreach(var p in _evidence) _scriptWorld.Register("object:"+p.Key,p.Value);
            foreach(var p in _souls)
            {
                var who=p.Key; var self=_runtime.KnownProjection(p.Value,_bodies[who]); Require(self is not null,"script.self",ContractErrorCode.InvalidReference);
                _scriptWorld.RegisterCharacter("person:"+who,p.Value,_bodies[who],self!,()=>StoryBusy(who),execution=> { DailySuspend(Names.PersonKeys[(int)who]); _storyActions[who]=execution; });
            }
        }
        if(_dailyClosed) { _dailySession.Completed-=DailyCompleted; _dailySession.Dispose(); }
    }
    internal static MorningWorld RestoreSave(WorldSession session,MorningSave data)=>new(session,data);
}
