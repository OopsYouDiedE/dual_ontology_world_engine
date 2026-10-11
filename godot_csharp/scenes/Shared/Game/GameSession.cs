using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Chapter2.Prototype;
using DualOntologyWorldEngine.Story;
using DualOntologyWorldEngine.Core;
using System.Text.Json;
using static DualOntologyWorldEngine.Story.ChapterEvents;

namespace DualOntologyWorldEngine.Gameplay;

/// <summary>连续游戏的流程所有者。宿主只执行这里给出的视图、界面和空间事件。</summary>
public sealed partial class GameSession(string campusScript, string livingView, string campusView,
    string? traceDirectory = null, Action<string>? traceWarning = null,
    Func<IReadOnlyDictionary<string, string>>? authorSources = null,
    string? controllerScript = null, int maxAgentSelections = 10000, int maxAgentObservations = 20000,
    bool aiTakesPlayerSeat = false) : IDisposable
{
    public static Task PrepareAsync() => AwakeningCase.PrepareAsync();
    private IChapterSession? _session;
    private bool _waitingForView;
    private bool _started;
    private bool _metadataRecorded;
    private readonly GameTrace _trace = new(traceDirectory, traceWarning);
    private SimulationSession? _traceSimulation;
    private PlayerCandidateSession? _player;
    private bool _trustedFieldInput;
    public ActorControllerDefinition? AgentDeclaration { get; private set; }
    public bool AgentEnabled => AgentDeclaration is not null;
    public PublicPlayerObservation? AgentObservation => _player?.Current;
    public PlayerTime? AgentTime => (_session as IPlayerRoleSession)?.PlayerTime;
    public int AgentSelections => _player?.Selections ?? 0;
    public void LocateAgent(PlayerFieldState field)
        => (_player ?? throw new PlayerBoundaryException("agent_not_declared")).Locate(field);
    public PlayerCognition AgentCognition => (_session as IPlayerRoleSession)?.CapturePlayerCognition(AgentDeclaration?.Actor
        ?? throw new PlayerBoundaryException("agent_not_declared")) ?? throw new PlayerBoundaryException("unsupported_player_role");
    public PublicPlayerObservation ObserveAgentField(IReadOnlyList<PlayerFieldOffer> offers)
    {
        (_session as IPlayerRoleSession)?.ObservePlayerOffers(offers);
        return (_player ?? throw new PlayerBoundaryException("agent_not_declared")).Observe([], offers);
    }
    public PublicPlayerObservation ObserveAgentPanel(string panel, string runLabel)
    {
        var view = PlayerPanels.Project(Snapshot(), panel, runLabel);
        return (_player ?? throw new PlayerBoundaryException("agent_not_declared"))
            .Observe([Event("player_panel_view", ("rows", view.Text))], view.Offers);
    }
    public void CloseAgent() => _player?.Close();
    /// <summary>The ordinary input a current token stands for, so the host can show the act being made.</summary>
    public IReadOnlyDictionary<string, object?> AgentCandidateInput(string token)
        => (_player ?? throw new PlayerBoundaryException("agent_not_declared")).Peek(token);
    public IReadOnlyList<Dictionary<string, object?>> DispatchField(IReadOnlyDictionary<string, object?> input)
    {
        if (Text(input, "kind") is not ("tick" or "daily" or "presented" or "move" or "blocked_move" or "interact" or "opened"))
            throw new PlayerBoundaryException("unsupported_field_feedback");
        _trustedFieldInput = true;
        try { return Dispatch(input); }
        finally { _trustedFieldInput = false; }
    }
    private void BindController()
    {
        var role = _session as IPlayerRoleSession ?? throw new PlayerBoundaryException("unsupported_player_role");
        var declarations = ReadControllers();
        if (declarations.Any(c => c.Mode == "agent" && c.Actor != role.PlayerActor))
            throw new PlayerBoundaryException("unsupported_player_role");
        // The seat's own declared rules decide; a launch only chooses who sits in it.
        var declaration = declarations.SingleOrDefault(c => c.Actor == role.PlayerActor
            && (c.Mode == "agent" || aiTakesPlayerSeat && c.Rules.Count > 0));
        if (declaration is null && aiTakesPlayerSeat) throw new PlayerBoundaryException("agent_rules_missing");
        if (declaration is null) { _player?.Close(); _player = null; AgentDeclaration = null; return; }
        if (_player is not null && AgentDeclaration?.Actor == declaration.Actor) return;
        AgentDeclaration = declaration;
        _player = new(DispatchGame, declaration.Name, maxAgentSelections, maxAgentObservations, role.ObservePlayerFormField);
    }
    private IReadOnlyList<ActorControllerDefinition> ReadControllers()
    {
        if (controllerScript is not null) return PlayerControllers.Read(controllerScript);
        // World-scoped characters are declared once and keep their seat across chapters.
        using var stream = typeof(Chapter1Data).Assembly.GetManifestResourceStream("Awakening.World.json")!;
        using var reader = new StreamReader(stream);
        var declared = PlayerControllers.Read(reader.ReadToEnd()).ToList();
        if (_session is Chapter2InteractionSession)
            declared.AddRange(PlayerControllers.Read(campusScript).Where(c => declared.All(d => d.Actor != c.Actor)));
        return declared;
    }

    public void RecordVisible(IReadOnlyDictionary<string, object?> payload) { TraceMetadata(); _trace.Visible(payload); }
    public void RecordInput(IReadOnlyDictionary<string, object?> payload) { TraceMetadata(); _trace.PhysicalInput(payload); }
    public void RecordOutput(Dictionary<string, object?> output) => _trace.Output(output);

    public IReadOnlyList<Dictionary<string, object?>> Dispatch(IReadOnlyDictionary<string, object?> input)
    {
        var kind = Text(input, "kind");
        if ((kind == "start" && _started) || (kind == "navigate" && Text(input, "to") == "restart"))
        { DetachTrace(); _trace.NewRun(); _metadataRecorded = false; }
        TraceMetadata();
        _trace.BeginRequest(input);
        try
        {
            IReadOnlyList<Dictionary<string, object?>> output;
            if (kind == "select_candidate")
            {
                var port = _player ?? throw new PlayerBoundaryException("agent_not_declared");
                port.Submit(new SelectCandidate(Text(input, "token")));
                output = port.LastOutput;
            }
            else
            {
                // Once the role is agent-controlled, user decisions can only use
                // the same current token entry. Trusted lifecycle inputs still run.
                if (_player is not null && !_trustedFieldInput && kind is not ("start" or "view_ready" or "tick" or "daily" or "presented"))
                    throw new PlayerBoundaryException("agent_requires_candidate_token");
                output = DispatchGame(input);
                _player?.Observe(output);
            }
            foreach (var ev in output)
            {
                _trace.Output(ev);
            }
            return output;
        }
        catch (Exception error)
        {
            _trace.RequestFailed(error);
            throw;
        }
    }

    private IReadOnlyList<Dictionary<string, object?>> DispatchGame(IReadOnlyDictionary<string, object?> input)
    {
        var kind = Text(input, "kind");
        if (kind == "start") return Start();
        if (kind == "navigate")
        {
            var destination = Text(input, "to");
            if (destination == "restart") return Start();
            if (destination == "title")
            {
                DetachTrace();
                _session = null;
                _waitingForView = false;
                ChapterWorldContinuity.Clear();
                return [Event("home")];
            }
            if (destination is "continue" or "chapter2") return Continue();
            throw new InvalidOperationException(Chapter1Data.Text("界面·错误·未知去处", ("去处", destination)));
        }
        if (_session is null) throw new InvalidOperationException(Chapter1Data.Text("界面·错误·游戏未开始"));
        if (kind == "view_ready")
        {
            if (!_waitingForView) return [];
            _waitingForView = false;
            var begin = input.ToDictionary(p => p.Key, p => p.Value);
            begin["kind"] = "begin";
            return Normalize(_session.Dispatch(begin));
        }
        if (_waitingForView) return [];
        if (kind == "player_panel")
        {
            var panel = Text(input, "panel");
            if (panel is not ("" or "notebook" or "manuscript" or "campus")
                || panel != "" && !Flag(Map(_session.Snapshot(), "controls"), panel))
                throw new PlayerBoundaryException("panel_unavailable");
            var output = new List<Dictionary<string, object?>> { Event("player_panel", ("panel", panel)) };
            if (panel == "manuscript") output.AddRange(Normalize(_session.Dispatch(Event("opened", ("panel", panel)))));
            return output;
        }
        if (kind == "field_action") return [Event("player_field", ("input", input))];
        if (kind == "travel")
        {
            var nodeId = Text(input, "node_id");
            var campus = Map(_session.Snapshot(), "campus");
            var node = Map(Map(campus, "nodes"), nodeId);
            var known = Map(campus, "known");
            if (node.Count == 0 || !Flag(known, nodeId) || _session.Snapshot().GetValueOrDefault("field_enabled") is not true)
                return [Event("error", ("message", Chapter1Data.Text("界面·地点未开放")))];
            return [Event("travel", ("node", node))];
        }
        return Normalize(_session.Dispatch(input));
    }

    public IReadOnlyDictionary<string, object?> Snapshot()
        => _session is null ? new Dictionary<string, object?>() : Present(_session.Snapshot());

    private IReadOnlyList<Dictionary<string, object?>> Start()
    {
        DetachTrace();
        _session = new Chapter1Session();
        _player?.Close(); _player = null; AgentDeclaration = null;
        BindController();
        _started = true;
        AttachTrace(((Chapter1Session)_session).World.PersistentSession.Simulation);
        _waitingForView = true;
        return [Event("view", ("resource", livingView)), Event("state", ("snapshot", Snapshot()))];
    }

    private IReadOnlyList<Dictionary<string, object?>> Continue()
    {
        if (_session is not Chapter1Session || _session.Snapshot().GetValueOrDefault("ended") is not true)
            throw new InvalidOperationException(Chapter1Data.Text("界面·错误·尚未出区"));
        var live = ChapterWorldContinuity.Current
            ?? throw new InvalidOperationException(Chapter1Data.Text("界面·错误·缺少世界"));
        var options = Chapter2AuthoredProfiles.Apply(campusScript);
        _ = new Chapter2Narrative(campusScript, true);
        Chapter2InteractionSession.ValidateScript(campusScript);
        foreach (var character in live.Characters.Values)
            if (character.Self is null || live.Runtime.Representations(character.Body).Count != 1
                || !ReferenceEquals(live.Runtime.KnownProjection(character.Soul, character.Body), character.Self)
                || live.Simulation.IsActing(character.Self))
                throw new InvalidOperationException(Chapter1Data.Text("界面·错误·人物尚未就绪"));
        var game = new DualOntologyWorldEngine.Chapter2.Prototype.Game(options, live,
            WorldScriptDocument.Parse(campusScript, allowChapterAdapter: true).Single());
        _session = new Chapter2InteractionSession(game, campusScript);
        BindController();
        AttachTrace(game.S);
        ChapterWorldContinuity.Clear();
        _waitingForView = true;
        return [Event("view", ("resource", campusView), ("map", "living_hall"), ("spawn", "east_south")),
            Event("state", ("snapshot", Snapshot()))];
    }

    private static Dictionary<string, object?> Present(IReadOnlyDictionary<string, object?> snapshot)
    {
        var result = snapshot.ToDictionary(p => p.Key, p => p.Value);
        result.Remove("chapter");
        result["presentation"] = new Dictionary<string, object?> { ["window_title"] = Chapter1Data.Text("游戏名") };
        return result;
    }

    private static IReadOnlyList<Dictionary<string, object?>> Normalize(IReadOnlyList<Dictionary<string, object?>> events)
    {
        foreach (var ev in events)
            if (Text(ev, "kind") == "state") ev["snapshot"] = Present(Map(ev, "snapshot"));
        return events;
    }

    private void AttachTrace(SimulationSession simulation)
    {
        if (ReferenceEquals(_traceSimulation, simulation)) return;
        DetachTrace();
        _traceSimulation = simulation;
        simulation.Completed += TraceCompleted;
        simulation.StageCommitted += TraceStage;
    }
    private void TraceMetadata()
    {
        if (_metadataRecorded) return;
        _metadataRecorded = true;
        // Content provenance is read-only and does not initialize any chapter world.
        IReadOnlyDictionary<string, string> storyFiles = new SortedDictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (authorSources is not null)
            {
                var snapshot = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var (path, text) in authorSources()) snapshot[path] = text;
                storyFiles = snapshot;
            }
        }
        catch (Exception error)
        {
            try { traceWarning?.Invoke("Author story snapshot could not be read; tracing continues: " + error.Message); } catch { }
        }
        try
        {
            using var document = JsonDocument.Parse(campusScript);
            _trace.Metadata(new
            {
                chapter = "chapter2", source_map = document.RootElement.TryGetProperty("source_map", out var map) ? map.Clone() : default(JsonElement?),
                compiled_sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(campusScript))),
                story_files = storyFiles
            });
        }
        catch (JsonException error)
        { _trace.RequestFailed(error); _trace.Metadata(new { story_files = storyFiles }); }
    }
    private void TraceCompleted(ActionExecution execution) => _trace.Action("completed", execution, _traceSimulation?.CTB.Now ?? execution.Result?.Time ?? 0);
    private void TraceStage(ActionExecution execution) => _trace.Action("stage_committed", execution, _traceSimulation?.CTB.Now ?? execution.Result?.Time ?? 0);
    private void DetachTrace()
    {
        if (_traceSimulation is null) return;
        _traceSimulation.Completed -= TraceCompleted;
        _traceSimulation.StageCommitted -= TraceStage;
        _traceSimulation = null;
    }
    public void Dispose() { DetachTrace(); _trace.Dispose(); }
}
