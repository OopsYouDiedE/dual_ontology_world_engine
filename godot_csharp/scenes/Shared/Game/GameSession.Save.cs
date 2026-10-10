using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.SessionPersistence;

namespace DualOntologyWorldEngine.Gameplay;

public sealed partial class GameSession
{
    private sealed record GameSave
    {
        public required string Format {get;init;}
        public required string[] RequiredCapabilities {get;init;}
        public required string CampusScript {get;init;}
        public required string LivingView {get;init;}
        public required string CampusView {get;init;}
        public required bool Started {get;init;}
        public required byte[] Chapter {get;init;}
        public required GameTrace.SaveState Trace {get;init;}
    }
    public ContractResult<byte[]> Save(CodecLimitsV1? limits=null) => Try(() =>
    {
        RequireCompleteControllerSave();
        Require(!_waitingForView,"host.view_receipt",ContractErrorCode.UnsupportedSavePoint);
        Require(_session is Chapter1Session,"host.chapter",ContractErrorCode.UnsupportedSavePoint);
        var d=new GameSave { Format="dual-ontology-game-session/v1",RequiredCapabilities=["story.chapter-idle-save/1"],CampusScript=campusScript,
            LivingView=livingView,CampusView=campusView,Started=_started,Chapter=Value(((Chapter1Session)_session).Save(limits)),Trace=_trace.CaptureSave() };
        var bytes=JsonSerializer.SerializeToUtf8Bytes(d,Json); Value(CognitionCodec.ValidateJson(bytes,limits)); return bytes;
    });
    private byte[]? _loadedSave;
    public Guid RestoreEpoch {get;private set;}=Guid.NewGuid();
    public ContractResult<bool> Restore(byte[] bytes,CodecLimitsV1? limits=null) => Try(() =>
    {
        RequireCompleteControllerSave();
        Value(CognitionCodec.ValidateJson(bytes,limits));
        if(_loadedSave is not null&&bytes.AsSpan().SequenceEqual(_loadedSave)) return true;
        var d=JsonSerializer.Deserialize<GameSave>(bytes,Json)!;
        Require(d is not null,"game");
        Require(d!.Format=="dual-ontology-game-session/v1"&&d.RequiredCapabilities.All(c=>c=="story.chapter-idle-save/1"),"version",ContractErrorCode.Unsupported);
        Require(d.CampusScript==campusScript&&d.LivingView==livingView&&d.CampusView==campusView,"content",ContractErrorCode.Unsupported);
        Require(d.Trace is not null&&d.Trace.Sequence>=0&&d.Trace.Request>=0&&d.Trace.Output>=0&&d.Trace.ExecutionSerial>=0,"trace");
        var chapter=Value(Chapter1Session.Restore(d.Chapter,limits));
        // Publication is last; no initializer, presentation, or view_ready dispatch is repeated.
        DetachTrace(); _session=chapter; _started=d.Started; _waitingForView=false; _trace.RestoreSave(d.Trace!);
        AttachTrace(chapter.World.PersistentSession.Simulation); _loadedSave=bytes.ToArray(); RestoreEpoch=Guid.NewGuid(); return true;
    });
    private void RequireCompleteControllerSave()
    {
        // The chapter codec persists cognition, not controller history/RNG or
        // candidate batches. Do not publish a partial restore with old tokens.
        Require(!AgentEnabled && !ReadControllers().Any(c => c.Mode == "agent"),
            "host.controller_state", ContractErrorCode.UnsupportedSavePoint);
    }
}
