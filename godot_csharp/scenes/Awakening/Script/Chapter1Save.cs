using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.SessionPersistence;

namespace DualOntologyWorldEngine.Awakening;

public sealed partial class AwakeningContentSession
{
    internal sealed record ContentSave([property:System.Text.Json.Serialization.JsonRequired] string Content,[property:System.Text.Json.Serialization.JsonRequired] ChapterProgressSaveV1 Progress,[property:System.Text.Json.Serialization.JsonRequired] Chapter1Timing[] Timing,
        [property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,RecallDecision> AloneDecisions);
    internal ContractResult<bool> CheckSavePoint() => Try(() =>
    {
        Require(_activePlan.Presentation=="" && (_activePlan.Story is null || !_activePlan.Story.IsRunning),"host.flow",ContractErrorCode.UnsupportedSavePoint);
        Require(_judgeSaid is null&&_judgeShown is null,"host.judgment",ContractErrorCode.UnsupportedSavePoint);
        return true;
    });
    internal ContentSave CaptureSave()
    { Value(CheckSavePoint()); return new(Content.RootElement.GetRawText(),Session.CaptureProgress(),[.._timing],new(_aloneDecisions)); }
    private AwakeningContentSession(ContentSave d)
    {
        Session=ChapterSession.RestoreProgress(d.Progress); Content.Dispose(); Content=JsonDocument.Parse(d.Content);
        _timing.AddRange(d.Timing); foreach(var p in d.AloneDecisions) { Require(Enum.IsDefined(p.Value.Who)&&Enum.IsDefined(p.Value.Choice),"decision"); _aloneDecisions.Add(p.Key,p.Value); }
    }
    internal static AwakeningContentSession RestoreSave(ContentSave d)=>new(d);
}

public sealed partial class Chapter1Session
{
    private Guid _presentationDomain;
    private long _presentationSerial;
    private string NextPresentationId()=>_presentationDomain.ToString("N")+":"+checked(++_presentationSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private sealed record FactSave([property:System.Text.Json.Serialization.JsonRequired] string Room,[property:System.Text.Json.Serialization.JsonRequired] string Entered,[property:System.Text.Json.Serialization.JsonRequired] string RoomName,[property:System.Text.Json.Serialization.JsonRequired] bool Visible,[property:System.Text.Json.Serialization.JsonRequired] bool Moving,[property:System.Text.Json.Serialization.JsonRequired] double? Distance,[property:System.Text.Json.Serialization.JsonRequired] string[] Near);
    private sealed record ChapterSave
    {
        public required string Format {get;init;}
        public required string ContentVersion {get;init;}
        public required string[] RequiredCapabilities {get;init;}
        public required byte[] World {get;init;}
        public required MorningWorld.MorningSave Morning {get;init;}
        public required AwakeningContentSession.ContentSave Content {get;init;}
        public required bool Begun {get;init;}
        public required double Now {get;init;}
        public required double AsideUntil {get;init;}
        public required double WitnessAfter {get;init;}
        public required string CurrentRoom {get;init;}
        public required string[] FurnitureSpoken {get;init;}
        public required bool DoorwayNear {get;init;}
        public required Dictionary<string,FactSave> Facts {get;init;}
        public required bool FieldActive {get;init;}
        public required string UiModal {get;init;}
        public required string LastSnapshot {get;init;}
        public required string[] HandWait {get;init;}
        public required JsonElement Reply {get;init;}
        public required int ProbePlanKey {get;init;}
        public required Guid PresentationDomain {get;init;}
        public required long PresentationSerial {get;init;}
    }
    private static string SaveContentVersion=>"chapter-content/1:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Chapter1Data.Root.GetRawText())));
    public ContractResult<bool> CheckSavePoint() => Try(() =>
    {
        Require(_jobs.Count==0&&_planDepth==0&&_probePlans.Count==0,"host.fiber",ContractErrorCode.UnsupportedSavePoint);
        Require(_uiOwner is null&&_modal is null&&!_dialogueActive&&!_storyLock&&_uiModal=="","host.presentation",ContractErrorCode.UnsupportedSavePoint);
        Require(_dailyJobs.Count==0&&_staged.Count==0&&_invites.Count==0&&_helpInvites.Count==0&&_facts.Values.All(f=>!f.Moving),"host.staging",ContractErrorCode.UnsupportedSavePoint);
        Require(!_dialogue.IsRunning,"host.dialogue",ContractErrorCode.UnsupportedSavePoint);
        Value(_content.CheckSavePoint()); Value(_world.CheckSavePoint()); return true;
    });
    public ContractResult<byte[]> Save(CodecLimitsV1? limits=null) => Try(() =>
    {
        Value(CheckSavePoint()); var world=_world.PersistentSession; world.SetSavePointGuard(CheckSavePoint);
        var d=new ChapterSave
        {
            Format="dual-ontology-chapter-session/v1",ContentVersion=SaveContentVersion,RequiredCapabilities=["story.chapter-idle-save/1"],
            World=Value(world.Save(limits)),Morning=_world.CaptureSave(),Content=_content.CaptureSave(),Begun=_begun,Now=_now,AsideUntil=_asideUntil,WitnessAfter=_witnessAfter,
            CurrentRoom=_currentRoom,FurnitureSpoken=[.._furnitureSpoken.Order(StringComparer.Ordinal)],DoorwayNear=_doorwayNear,
            Facts=_facts.ToDictionary(p=>p.Key,p=>new FactSave(p.Value.Room,p.Value.Entered,p.Value.RoomName,p.Value.Visible,p.Value.Moving,
                double.IsPositiveInfinity(p.Value.Distance)?null:p.Value.Distance,[..p.Value.Near.Order(StringComparer.Ordinal)])),
            FieldActive=_fieldActive,UiModal=_uiModal,LastSnapshot=_lastSnapshot,HandWait=[.._handWait.Order(StringComparer.Ordinal)],
            Reply=JsonSerializer.SerializeToElement(Pack(_reply.ToDictionary(p=>p.Key,p=>p.Value!))),ProbePlanKey=_probePlanKey,
            PresentationDomain=_presentationDomain,PresentationSerial=_presentationSerial
        };
        var bytes=JsonSerializer.SerializeToUtf8Bytes(d,Json); Value(Restore(bytes,limits)); return bytes;
    });
    private Chapter1Session(ChapterSave d,WorldSession session)
    {
        _world=MorningWorld.RestoreSave(session,d.Morning); _content=AwakeningContentSession.RestoreSave(d.Content);
        _knowledge=new(AcquiredPropositions.Read(_content.Content.RootElement),_world.PersistentSession.Character(PlayerActor).Soul);
        _dialogue=new(_content,_world); _interaction=new(_content,_world);
        _begun=d.Begun; _now=d.Now; _asideUntil=d.AsideUntil; _witnessAfter=d.WitnessAfter; _currentRoom=d.CurrentRoom;
        _furnitureSpoken.UnionWith(d.FurnitureSpoken); _doorwayNear=d.DoorwayNear;
        foreach(var p in d.Facts)
        {
            var f=p.Value; Require(!f.Moving&&(f.Distance is null||double.IsFinite(f.Distance.Value)&&f.Distance>=0),"fact");
            _facts.Add(p.Key,new Fact { Room=f.Room,Entered=f.Entered,RoomName=f.RoomName,Visible=f.Visible,Moving=f.Moving,Distance=f.Distance??double.PositiveInfinity,Near=f.Near.ToHashSet(StringComparer.Ordinal) });
        }
        _fieldActive=d.FieldActive; _uiModal=d.UiModal; _lastSnapshot=d.LastSnapshot; _handWait.UnionWith(d.HandWait);
        _reply=(Dictionary<string,object?>)Unpack(d.Reply)!; _probePlanKey=d.ProbePlanKey;
        _presentationDomain=d.PresentationDomain; _presentationSerial=d.PresentationSerial;
        session.SetSavePointGuard(CheckSavePoint);
    }
    public static ContractResult<Chapter1Session> Restore(byte[] bytes,CodecLimitsV1? limits=null) => Try(() =>
    {
        Value(CognitionCodec.ValidateJson(bytes,limits)); var d=JsonSerializer.Deserialize<ChapterSave>(bytes,Json)!;
        Require(d is not null,"chapter");
        Require(d!.Format=="dual-ontology-chapter-session/v1"&&d.ContentVersion==SaveContentVersion&&d.RequiredCapabilities.All(c=>c=="story.chapter-idle-save/1"),"version",ContractErrorCode.Unsupported);
        Require(double.IsFinite(d.Now)&&d.Now>=0&&double.IsFinite(d.AsideUntil)&&double.IsFinite(d.WitnessAfter)&&d.UiModal==""&&d.ProbePlanKey>=0,"host.clock");
        Require(d.PresentationDomain!=Guid.Empty&&d.PresentationSerial>=0,"host.presentation_serial",ContractErrorCode.Unsupported);
        var world=Value(WorldSession.Restore(d.World,limits));
        var chapter=new Chapter1Session(d,world); Value(chapter.CheckSavePoint()); return chapter;
    });
}

/// <summary>Atomic publication and retry-safe load for the chapter adapter.</summary>
public sealed class Chapter1SessionStore(Chapter1Session? initial=null)
{
    private readonly object _gate=new(); private byte[]? _loaded;
    public Chapter1Session? Current {get;private set;}=initial;
    public Guid RestoreEpoch {get;private set;}=Guid.NewGuid();
    public ContractResult<Chapter1Session> Restore(byte[] bytes,CodecLimitsV1? limits=null)
    {
        lock(_gate)
        {
            if(_loaded is not null&&bytes.AsSpan().SequenceEqual(_loaded)) return new ContractResult<Chapter1Session>.Success(Current!);
            var result=Chapter1Session.Restore(bytes,limits);
            if(result is ContractResult<Chapter1Session>.Success s) { Current=s.Value; _loaded=bytes.ToArray(); RestoreEpoch=Guid.NewGuid(); }
            return result;
        }
    }
}
