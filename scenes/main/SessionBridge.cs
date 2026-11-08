using Godot;
using DuelView.Runtime;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

// @mechanism: dual_ontology_state
// @layer: scenes/main
// @contracts: SessionBridge, ValidatePrefabPlacement

/// <summary>
/// Unified C# host bridge gateway merging original CharacterSessionBridge and GameEngine.
/// Zero literal references to abolished world.dv, defaults to res://assets/data/scenarios/campaign.dv
/// Exposes ValidatePrefabPlacement() to tools/godot_architect/.
/// </summary>
public partial class SessionBridge : Node
{
    [Signal] public delegate void EventsReadyEventHandler(GArray events);
    [Signal] public delegate void TraceClosedEventHandler();
    [Signal] public delegate void StateChangedEventHandler(GDict state);

    // Runtime Core Session for Campaign & 3D Rooms
    private RuntimeSession? characterSession;
    private CharacterSessionDefinition? definition;
    private readonly Dictionary<long, Decision> executions = [];
    private readonly List<object> timeline = [];
    private readonly Dictionary<string, (long revision, int pending)> cognitiveVersions = [];
    private readonly Dictionary<string, int> evidenceCursors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> auditCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> interruptTargets = new(StringComparer.Ordinal);
    private readonly HashSet<string> strategyActions = new(StringComparer.Ordinal);
    private readonly List<GDict> hostErrors = [];
    private readonly List<GDict> timingRecords = [];
    private bool timingEnabled;
    private string player = "detective";
    private string contentPath = "res://assets/data/scenarios/campaign.dv";
    private long generation = 1;
    private CharacterContentSnapshot? contentSnapshot;
    private string revisionArchiveHash = "";
    private object? contentUpdate;
    private (RuntimeSession Session, CharacterContentSnapshot Content, RuntimeSessionState State, object? Update)? prepared;

    // Fields required for CasebookPersistence partial class
    private DvSession? session;
    private Task work = Task.CompletedTask;
    private long interaction = 0;
    private readonly Queue<(long Id, string Json)> commands = new();
    private string pendingEvent = "";
    private string? evidenceDirectory;
    private volatile bool worldPaused;
    private bool closing;
    private bool busy;
    private bool maintenance;
    private bool faulted;

    private StreamWriter? log;
    private static readonly JsonSerializerOptions AuditJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private string auditPath = "";
    private string auditError = "";
    private long auditSequence;
    private bool auditClosed;

    private static GDict Dict(object value) => Json.ParseString(JsonSerializer.Serialize(value)).AsGodotDictionary();

    private void RequireNoPendingExecution() { }
    private static void StartSessionRecording(DvSession current, string directory) { }
    private static string SealSessionEvidence(DvSession current, string directory, string reason, string backupPath = "") => "";
    private void InstallRestored((DvSession Session, string Snapshot) restored, GDict preparedSnapshot, string directory)
    {
        session = restored.Session;
        evidenceDirectory = directory;
        pendingEvent = "";
        commands.Clear();
    }
    private void BeginDeliveredDecision() { }
    private void StartPump() { }
    private void Stamp(string kind, long interactionId, string path) { }
    private string NewSessionEvidenceDirectory() => ProjectSettings.GlobalizePath("user://evidence");
    private static GDict Parse(string json) => string.IsNullOrWhiteSpace(json) ? new() : Json.ParseString(json).AsGodotDictionary();

    private long Handle(long execution)
    {
        if (execution < 1 || execution > uint.MaxValue || generation < 1 || generation > int.MaxValue)
            throw new InvalidDataException("Execution generation exhausted.");
        return (generation << 32) | execution;
    }

    public long RemapExecution(long oldHandle)
    {
        var result = Handle(oldHandle & uint.MaxValue);
        return executions.ContainsKey(result) ? result : 0;
    }

    public override void _Ready()
    {
        Name = "SessionBridge";
    }

    public void SetWorldPaused(bool paused) => worldPaused = paused;
    public bool IsWorldPaused() => worldPaused;
    public void SetDiagnosticsEnabled(bool value) => timingEnabled = value;
    public bool IsDeveloperMode() => true;
    public bool CanInspect() => true;
    public bool IsDispatchIdle() => !busy && !maintenance;
    public bool CanBackgroundTick() => !worldPaused && !busy;
    public bool CanSimulationPulse() => !worldPaused && !busy;

    public GDict Open(string? path = null)
    {
        contentPath = string.IsNullOrWhiteSpace(path) ? "res://assets/data/scenarios/campaign.dv" : path;
        contentSnapshot = ReadContentSnapshot();
        definition = contentSnapshot.Definition;
        interruptTargets.Clear();
        foreach (var actor in definition.Declaration.GetProperty("actors").EnumerateArray())
        {
            foreach (var action in actor.GetProperty("actions").EnumerateArray())
            {
                if (action.TryGetProperty("interrupt_actor", out var target))
                    interruptTargets[action.GetProperty("id").GetString()!] = target.GetString()!;
            }
        }
        characterSession = CreateSession(definition);
        strategyActions.Clear();
        if (definition.Declaration.TryGetProperty("strategy_activities", out var strategies))
        {
            foreach (var item in strategies.EnumerateArray())
                strategyActions.Add(item.GetProperty("action").GetString()!);
        }
        player = definition.Declaration.GetProperty("viewer").GetString()!;
        if (!definition.Declaration.TryGetProperty("presentation", out _))
            characterSession.SetAutonomy(player, false);

        var root = definition.Declaration.TryGetProperty("presentation", out var presentation) && presentation.TryGetProperty("receipt_directory", out var receiptDirectory)
            ? receiptDirectory.GetString()! : "user://character-receipts";
        var directory = ProjectSettings.GlobalizePath(root + "/live-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        auditPath = Path.Combine(directory, "actions.jsonl");
        log = new(new FileStream(auditPath, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        Record("session_open", new { player, content_hash = definition.Hash, audit_schema = 2, progress = DuelView.Runtime.CampaignProgress.Evaluate(definition.Declaration, characterSession) });

        var declarationDict = Json.ParseString(definition.Declaration.GetRawText()).AsGodotDictionary();
        EmitSignal(SignalName.StateChanged, Snapshot());
        return declarationDict;
    }

    private static RuntimeSession CreateSession(CharacterSessionDefinition content)
    {
        var deduction = content.Declaration.TryGetProperty("deduction", out var caseDeclaration)
            ? Godot.FileAccess.GetFileAsString(caseDeclaration.GetProperty("source").GetString()!) : null;
        var candidate = content.Create(deduction);
        if (content.Declaration.TryGetProperty("deductions", out var moreDeductions))
        {
            foreach (var item in moreDeductions.EnumerateArray())
                candidate.ConfigureDeduction(Godot.FileAccess.GetFileAsString(item.GetProperty("source").GetString()!), item);
        }
        if (content.Declaration.TryGetProperty("strategy_activities", out var strategies))
        {
            foreach (var item in strategies.EnumerateArray())
                candidate.ConfigureStrategy(Godot.FileAccess.GetFileAsString(item.GetProperty("source").GetString()!), item);
        }
        return candidate;
    }

    public void ReportDoor(string id, double angle, double target, bool moving, double remaining)
        => characterSession?.ReportDoor(id, new(angle, target, moving, remaining));

    public GDict LogicalDoors() => characterSession != null ? Dict(characterSession.LogicalDoors) : new GDict();

    public GDict LogicalPlacement(string actor)
    {
        if (characterSession == null) return new GDict();
        var value = characterSession.Placement(actor);
        return value is null ? new GDict() : Dict(value);
    }

    public GDict PhysicalFact(string id, string facet, string field)
    {
        if (characterSession == null) return new GDict { { "known", false } };
        var fact = characterSession.World.Read(new(id, facet, field));
        return fact is null ? new GDict { { "known", false } } : Dict(fact);
    }

    public GArray RestoredCommands()
    {
        var result = new GArray();
        if (characterSession == null) return result;
        foreach (var item in executions)
        {
            var action = item.Value;
            var activity = characterSession.Activity(action.Actor);
            result.Add(Dict(new
            {
                kind = "execute",
                actor = action.Actor,
                execution = item.Key,
                action = action.Action.Id,
                duration = action.Action.Duration,
                declaration = action.Action.Declaration,
                work_started = activity?.WorkStartedAt is not null
            }));
        }
        return result;
    }

    public string[] RetainedResources(string actor) => characterSession != null ? characterSession.Reservations.Held(new(actor, 0)).ToArray() : [];
    public GDict ConversationSpec() => characterSession != null && characterSession.ConversationDeclaration.ValueKind == JsonValueKind.Object
        ? Json.ParseString(characterSession.ConversationDeclaration.GetRawText()).AsGodotDictionary() : new GDict();

    public GDict CaptureCheckpoint()
    {
        if (characterSession == null || contentSnapshot == null) return new GDict { { "error", "No active session" } };
        try
        {
            var archive = EnsureRevisionArchive();
            var state = characterSession.SaveState();
            return new GDict
            {
                { "json", JsonSerializer.Serialize(new
                {
                    version = 2,
                    content_path = contentPath,
                    content_hash = contentSnapshot.Revision.Fingerprint,
                    content_revision = new { format = "dv-sources-gzip-v1", archive_sha256 = archive },
                    state
                }) }
            };
        }
        catch (Exception error) { return new GDict { { "error", error.Message } }; }
    }

    public string WriteSavedGame(string payload)
    {
        try
        {
            var path = SavedGamePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, payload, Encoding.UTF8);
            File.Move(temporary, path, true);
            return "";
        }
        catch (Exception error) { return error.Message; }
    }

    public string SavedGameFile() => SavedGamePath();

    public string ReadSavedGame()
    {
        var path = SavedGamePath();
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
    }

    private static string SavedGamePath()
    {
        var slot = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--dv-save-slot=", StringComparison.Ordinal))?[15..];
        var resource = string.IsNullOrEmpty(slot) ? "user://character-session.dvsave" : "user://sessions/" + slot + "/character-session.dvsave";
        return ProjectSettings.GlobalizePath(resource);
    }

    private CharacterContentSnapshot ReadContentSnapshot() => CharacterContentRevisions.Capture(ProjectSettings.LocalizePath(contentPath), path =>
    {
        if (!Godot.FileAccess.FileExists(path)) throw new InvalidDataException("Missing checkpoint dependency: " + path);
        return Godot.FileAccess.GetFileAsString(path);
    });

    private string EnsureRevisionArchive()
    {
        if (contentSnapshot == null) return "";
        if (revisionArchiveHash.Length > 0) return revisionArchiveHash;
        var bytes = CharacterContentRevisions.Compress(contentSnapshot.Revision);
        var archiveHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var path = ProjectSettings.GlobalizePath("user://content-revisions/" + contentSnapshot.Revision.Fingerprint + "-" + archiveHash + ".json.gz");
        if (File.Exists(path))
        {
            bytes = File.ReadAllBytes(path);
            var prior = CharacterContentRevisions.ReadCompressed(bytes);
            if (prior.Revision.Fingerprint != contentSnapshot.Revision.Fingerprint || Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != archiveHash)
                throw new InvalidDataException("Cached content revision has a different contract.");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                try { File.Move(temporary, path, false); }
                catch (IOException) when (File.Exists(path))
                {
                    bytes = File.ReadAllBytes(path);
                    if (CharacterContentRevisions.ReadCompressed(bytes).Revision.Fingerprint != contentSnapshot.Revision.Fingerprint ||
                        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != archiveHash)
                        throw new InvalidDataException("Conflicting cached content revision.");
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        revisionArchiveHash = archiveHash;
        return revisionArchiveHash;
    }

    public string PrepareCheckpoint(string json, bool allowContentChange = false)
    {
        if (contentSnapshot == null) return "No content snapshot";
        try
        {
            prepared = null;
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = root.GetProperty("version").GetInt32();
            if (version is not (1 or 2) || root.GetProperty("content_path").GetString() != contentPath)
                throw new InvalidDataException("Checkpoint format or content path differs.");
            var content = ReadContentSnapshot();
            var state = root.GetProperty("state").Deserialize<RuntimeSessionState>() ?? throw new InvalidDataException("Empty checkpoint state.");
            var previousHash = root.GetProperty("content_hash").GetString() ?? "";
            CharacterContentUpdate? update = null;
            _ = allowContentChange;
            if (previousHash != content.Revision.Fingerprint)
                update = CharacterContentRevisions.Validate(ReadSavedRevision(root, version, previousHash), content, state);
            var candidate = CreateSession(content.Definition);
            candidate.RestoreState(state);
            object? evidence = null;
            if (update is not null)
            {
                var stateHash = CharacterContentRevisions.AssertStatePreserved(state, candidate.SaveState());
                evidence = new
                {
                    policy = "manual-routes-and-stricter-admission-v1",
                    update.PreviousFingerprint,
                    update.Fingerprint,
                    update.AddedManualRoutes,
                    update.TightenedActions,
                    update.RelabelledActions,
                    update.AddedExpectations,
                    state_preserved = true,
                    state_hash = stateHash
                };
            }
            foreach (var execution in state.Executions.Where(item => item.Dispatched))
            {
                if (candidate.DecisionFor(execution.Execution) is null)
                    throw new InvalidDataException("Missing restored execution.");
            }
            prepared = (candidate, content, state, evidence);
            return "";
        }
        catch (Exception error) { return error.Message; }
    }

    private static CharacterContentSnapshot ReadSavedRevision(JsonElement checkpoint, int version, string fingerprint)
    {
        if (!CharacterContentRevisions.ValidFingerprint(fingerprint)) throw new InvalidDataException("Invalid checkpoint content fingerprint.");
        string path;
        string? archiveHash = null;
        if (version == 2)
        {
            var contract = checkpoint.GetProperty("content_revision");
            archiveHash = contract.GetProperty("archive_sha256").GetString();
            if (contract.GetProperty("format").GetString() != "dv-sources-gzip-v1" || !CharacterContentRevisions.ValidFingerprint(archiveHash))
                throw new InvalidDataException("Invalid checkpoint content archive contract.");
            var name = fingerprint + "-" + archiveHash + ".json.gz";
            var cached = "user://content-revisions/" + name;
            path = Godot.FileAccess.FileExists(cached) ? cached : "res://assets/data/content-revisions/" + name;
        }
        else path = "res://assets/data/content-revisions/" + fingerprint + ".json.gz";
        if (!Godot.FileAccess.FileExists(path)) throw new InvalidDataException("Content changed and its verified previous revision is unavailable. A new game is required.");
        var bytes = Godot.FileAccess.GetFileAsBytes(path);
        if (archiveHash is not null && archiveHash != Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())
            throw new InvalidDataException("Checkpoint content archive differs from its saved contract.");
        var revision = CharacterContentRevisions.ReadCompressed(bytes);
        if (revision.Revision.Fingerprint != fingerprint) throw new InvalidDataException("Previous sources do not reproduce the checkpoint fingerprint.");
        return revision;
    }

    public bool CommitPreparedCheckpoint()
    {
        if (prepared is not { } next || generation >= int.MaxValue) return false;
        generation++;
        characterSession = next.Session;
        contentSnapshot = next.Content;
        definition = next.Content.Definition;
        contentUpdate = next.Update;
        revisionArchiveHash = "";
        prepared = null;
        if (!definition.Declaration.TryGetProperty("presentation", out _))
            characterSession.SetAutonomy(player, false);
        executions.Clear();
        cognitiveVersions.Clear();
        evidenceCursors.Clear();
        interruptTargets.Clear();
        strategyActions.Clear();
        foreach (var execution in next.State.Executions.Where(item => item.Dispatched))
            executions.Add(Handle(execution.Execution), characterSession.DecisionFor(execution.Execution)!);
        foreach (var actor in definition.Declaration.GetProperty("actors").EnumerateArray())
        {
            foreach (var action in actor.GetProperty("actions").EnumerateArray())
            {
                if (action.TryGetProperty("interrupt_actor", out var target))
                    interruptTargets[action.GetProperty("id").GetString()!] = target.GetString()!;
            }
        }
        if (definition.Declaration.TryGetProperty("strategy_activities", out var strategies))
        {
            foreach (var item in strategies.EnumerateArray())
                strategyActions.Add(item.GetProperty("action").GetString()!);
        }
        Record("checkpoint_restored", new { content_hash = contentSnapshot.Revision.Fingerprint, generation, game_seconds = characterSession.Clock.Now, content_update = contentUpdate });
        return true;
    }

    private void Record(string kind, object data)
    {
        if (characterSession == null) return;
        var row = new { seq = auditSequence + 1, real_us = Time.GetTicksUsec(), game_seconds = characterSession.Clock.Now, kind, data };
        var payload = JsonSerializer.SerializeToElement(data);
        var actor = payload.TryGetProperty("actor", out var lower) ? lower :
            payload.TryGetProperty("Actor", out var upper) ? upper : default;
        var own = actor.ValueKind == JsonValueKind.String && actor.GetString() == player;
        var publicToPlayer = kind is "session_open" or "checkpoint_restored" or "player_action" or "player_deduction" ||
            kind.StartsWith("conversation_", StringComparison.Ordinal) || own;
        if (publicToPlayer && kind is not ("actor_cognition" or "passive_perception" or "prop_placement" or "actor_motion" or "frame" or "yield_candidates" or "decision_audit" or "action_effects" or "evidence_received"))
        {
            timeline.Add(row);
            if (timeline.Count > 64) timeline.RemoveAt(0);
        }
        if (log is null || auditError.Length > 0) return;
        try
        {
            log.WriteLine(JsonSerializer.Serialize(row, AuditJson));
            log.Flush();
            auditSequence++;
            auditCounts[kind] = auditCounts.GetValueOrDefault(kind) + 1;
            if (kind == "action_receipt")
            {
                var category = payload.GetProperty("Committed").ValueKind == JsonValueKind.True ? "successful_receipt" :
                    payload.TryGetProperty("Interruption", out var interruption) && interruption.ValueKind == JsonValueKind.Object ? "recoverable_cancellation" :
                    payload.GetProperty("Reason").GetString() is "deduction_unproved" or "suggestion_rejected" or "incantation_precondition_failed" ? "rejected_receipt" :
                    payload.GetProperty("Reason").GetString() is "objective_condition_failed" or "standing_resource_busy" ? "blocked_attempt" : "unexpected_failure";
                auditCounts[category] = auditCounts.GetValueOrDefault(category) + 1;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            auditError = error.Message;
            GD.PushError("Causal audit persistence failed: " + error.Message);
        }
    }

    public GDict CampaignProgress()
    {
        if (characterSession == null || definition == null) return new GDict { ["day"] = 1, ["time"] = "07:00" };
        return Dict(DuelView.Runtime.CampaignProgress.Evaluate(definition.Declaration, characterSession));
    }

    private object AuditProjection() => new
    {
        path = auditPath,
        last_sequence = auditSequence,
        dispatch_count = auditCounts.GetValueOrDefault("action_dispatched"),
        receipt_count = auditCounts.GetValueOrDefault("action_receipt"),
        effect_count = auditCounts.GetValueOrDefault("action_effects"),
        evidence_count = auditCounts.GetValueOrDefault("evidence_received"),
        successful_count = auditCounts.GetValueOrDefault("successful_receipt"),
        recoverable_cancellations = auditCounts.GetValueOrDefault("recoverable_cancellation"),
        rejected_count = auditCounts.GetValueOrDefault("rejected_receipt"),
        blocked_attempt_count = auditCounts.GetValueOrDefault("blocked_attempt"),
        unexpected_failure_count = auditCounts.GetValueOrDefault("unexpected_failure"),
        error = auditError,
        closed = auditClosed
    };

    public GDict AuditStatus() => Dict(AuditProjection());

    private void DrainAudit()
    {
        if (characterSession == null || definition == null) return;
        while (characterSession.TryActionOutcome(out var outcome))
        {
            if (outcome!.StartAttempt is { } attempt) Record("decision_audit", attempt.Audit);
            Record("action_effects", outcome);
        }
        foreach (var item in definition.Declaration.GetProperty("actors").EnumerateArray())
        {
            var actor = item.GetProperty("id").GetString()!;
            var evidence = characterSession.Evidence(actor);
            foreach (var entry in evidence.Skip(evidenceCursors.GetValueOrDefault(actor)))
                Record("evidence_received", new { actor, evidence = entry });
            evidenceCursors[actor] = evidence.Count;
        }
    }

    public GDict Step(double delta, bool idle = false, bool paused = false)
    {
        if (characterSession == null) return new GDict();
        var before = characterSession.Clock.Now;
        characterSession.Step(delta, idle, paused, player);
        return CollectStep(before, idle, paused);
    }

    public GDict StepPhysics(double actualGameSeconds)
    {
        if (characterSession == null) return new GDict();
        var before = characterSession.Clock.Now;
        characterSession.StepPhysical(actualGameSeconds, false, player);
        return CollectStep(before, false, false);
    }

    public GDict StepTransit(double gameSeconds)
    {
        if (characterSession == null) return new GDict();
        var before = characterSession.Clock.Now;
        characterSession.StepLogical(gameSeconds, player);
        return CollectStep(before, false, false);
    }

    private GDict CollectStep(double before, bool idle, bool paused)
    {
        if (characterSession == null || definition == null) return new GDict();
        object? completedDeduction = null;
        var cmdList = new Godot.Collections.Array<GDict>();
        var completed = new Godot.Collections.Array<GDict>();
        while (characterSession.TryCancellation(out var cancelled))
            cmdList.Add(Dict(new { kind = "cancel", execution = Handle(cancelled!.Execution), actor = cancelled.Actor }));
        while (characterSession.TryAction(out var decision))
        {
            executions[Handle(decision!.Execution)] = decision;
            var command = Dict(new
            {
                kind = "execute",
                actor = decision.Actor,
                execution = Handle(decision.Execution),
                action = decision.Action.Id,
                duration = decision.Action.Duration,
                declaration = decision.Action.Declaration
            });
            cmdList.Add(command);
            Record("action_dispatched", new { decision.Actor, action = decision.Action.Id, decision.Execution, duration = decision.Action.Duration, intent = decision.Intent });
            Record("decision_audit", characterSession.DecisionAudit(decision));
        }
        while (characterSession.TryReceipt(out var receipt))
        {
            Record("action_receipt", receipt!);
            var published = receipt! with
            {
                Narrative = receipt.Narrative?.Where(line => line.Recipients.Contains(player, StringComparer.Ordinal))
                    .Select(line => line with { Recipients = [player] }).ToArray()
            };
            completed.Add(Dict(published));
            if (receipt!.Committed && interruptTargets.TryGetValue(receipt.Action, out var interrupted)) characterSession.RequestCancel(interrupted);
            if (strategyActions.Contains(receipt.Action)) Record("strategy", new { actor = receipt.Actor, receipt.Committed, result = receipt.Committed ? characterSession.LastStrategy : null });
            if ((definition.Declaration.TryGetProperty("deduction", out var deduction) && receipt!.Action == deduction.GetProperty("action").GetString()) ||
               (definition.Declaration.TryGetProperty("deductions", out var deductions) && deductions.EnumerateArray().Any(item => receipt!.Action == item.GetProperty("action").GetString())))
            {
                var result = characterSession.LastDeduction;
                Record("deduction", new { actor = receipt!.Actor, result });
                if (receipt.Actor == player) completedDeduction = result;
            }
            executions.Remove(Handle(receipt.Execution));
        }
        DrainAudit();
        foreach (var item in definition.Declaration.GetProperty("actors").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()!;
            var view = characterSession.View(id);
            var version = (view.KnowledgeRevision, characterSession.PendingInformation(id));
            if (cognitiveVersions.TryGetValue(id, out var previous) && previous == version) continue;
            cognitiveVersions[id] = version;
            Record("actor_cognition", new { actor = id, revision = version.KnowledgeRevision, pending = version.Item2, knowledge_count = view.Knowledge.Count });
        }
        return new GDict
        {
            { "commands", cmdList },
            { "receipts", completed },
            { "resting", characterSession.Clock.Resting },
            { "deduction", completedDeduction is null ? new GDict() : Dict(completedDeduction) },
            { "game_seconds", characterSession.Clock.Now },
            { "world_revision", characterSession.World.Revision },
            { "decision_pending", characterSession.HasPendingDecisionWork },
            { "has_host_navigation", characterSession.HasHostNavigation },
            { "simulation_seconds", characterSession.Clock.Now - before },
            { "world_speed", paused ? 0 : characterSession.Clock.Rate(idle, characterSession.Working(player)) }
        };
    }

    public bool BeginWork(long execution)
    {
        if (characterSession == null) return false;
        var result = executions.TryGetValue(execution, out var decision) && characterSession.BeginWork(decision);
        if (result) Record("work_started", new { execution });
        return result;
    }

    public bool WorkElapsed(long execution) => characterSession != null && executions.TryGetValue(execution, out var decision) && characterSession.WorkElapsed(decision);
    public bool PhysicalActionPermitted(long execution) => characterSession != null && executions.TryGetValue(execution, out var decision) && characterSession.PhysicalActionPermitted(decision);
    public void ReportPhysicalBoolean(string id, string facet, string field, bool value) => characterSession?.ReportPhysicalBoolean(new(id, facet, field), value);
    public bool Complete(long execution) => characterSession != null && executions.TryGetValue(execution, out var decision) && characterSession.HostCompleted(decision);
    public void ConfigureVisibleRoom(string room)
    {
        if (characterSession == null) return;
        var previous = characterSession.VisibleRoom;
        characterSession.ConfigureVisibleRoom(room);
        Record("navigation_authority_handoff", new { previous, visible_room = room, game_seconds = characterSession.Clock.Now });
    }

    public GDict ActorActivity(string actor)
    {
        if (characterSession == null) return new GDict();
        var activity = characterSession.ActorActivity(actor);
        if (activity is null) return new GDict();
        var value = Dict(activity);
        value["execution"] = Handle(value["execution"].AsInt64());
        return value;
    }

    public bool HasHostNavigation() => characterSession != null && characterSession.HasHostNavigation;
    public bool SetNavigationTarget(long execution, string room, double x, double y, double z, double remainingDistance, double speed)
    {
        if (characterSession == null || !executions.TryGetValue(execution, out var decision)) return false;
        var result = characterSession.SetNavigationTarget(decision, room, x, y, z, remainingDistance, speed);
        if (result) Record("navigation_target", new { execution, actor = decision.Actor, room, x, y, z, remaining_distance = remainingDistance, speed });
        return result;
    }

    public bool ReportNavigationProgress(long execution, double x, double y, double z, double remainingDistance)
        => characterSession != null && executions.TryGetValue(execution, out var decision) && characterSession.ReportNavigationProgress(decision, x, y, z, remainingDistance);

    public bool Arrive(long execution, double x, double y, double z)
    {
        if (characterSession == null || !executions.TryGetValue(execution, out var decision)) return false;
        var result = characterSession.Arrive(decision, x, y, z);
        if (result) Record("navigation_arrived", new { execution, actor = decision.Actor, action = decision.Action.Id, x, y, z, activity = characterSession.ActorActivity(decision.Actor) });
        return result;
    }

    public bool Cancel() => characterSession != null && characterSession.RequestCancel(player);
    public bool CancelActor(string actor) => characterSession != null && characterSession.RequestCancel(actor);
    public bool Available(string action) => characterSession != null && characterSession.Available(player, action);
    public void SetPlayerAutonomy(bool enabled) => characterSession?.SetAutonomy(player, enabled);
    public void ClearPendingTime() => characterSession?.Clock.ClearPendingSeconds();
    public void ClearFastForwardTime() => characterSession?.Clock.ClearUnsimulatedFastForward();
    public double PendingPhysicalSeconds() => characterSession?.Clock.PendingPhysicalSeconds ?? 0.0;
    public double PendingSimulationSeconds() => characterSession?.Clock.PendingSeconds ?? 0.0;
    public GDict PreviewDeduction(string action) => characterSession != null ? Dict(characterSession.DeductionPreview(player, action)) : new GDict();
    public string SubmitDeduction(string action, Godot.Collections.Array<string> evidenceIds)
    {
        if (characterSession == null) return "No active session";
        try
        {
            characterSession.SubmitDeduction(player, action, evidenceIds.ToArray());
            Record("player_deduction", new { action, evidence_ids = evidenceIds.ToArray() });
            return "";
        }
        catch (InvalidOperationException error) { return error.Message; }
    }

    public bool KnowsCondition(string json)
    {
        if (characterSession == null) return false;
        using var document = JsonDocument.Parse(json);
        var knowledge = characterSession.View(player).Knowledge;
        return Condition.Compile(document.RootElement, player).Evaluate(key => knowledge.TryGetValue(key, out var fact) ? fact : null) == Truth.True;
    }

    public GDict WorkProgress(string actor)
    {
        if (characterSession == null) return new GDict();
        var activity = characterSession.Activity(actor);
        if (activity?.WorkStartedAt is not { } start || activity.WorkEndsAt is not { } end) return new GDict();
        return Dict(new { action = activity.Action, progress = Math.Clamp((characterSession.Clock.Now - start) / Math.Max(.01, end - start), 0, 1) });
    }

    public string CheckActionOrder(string json)
    {
        if (characterSession == null || definition == null) return "";
        var spec = definition.Declaration.GetProperty("action_order_panel");
        if (!KnowsCondition(spec.GetProperty("evidence_gate").GetRawText()))
        {
            if (spec.TryGetProperty("evidence_checklist", out var checklist))
            {
                var missing = checklist.EnumerateArray().Where(item => !KnowsCondition(item.GetProperty("when").GetRawText())).Select(item => item.GetProperty("label").GetString());
                return spec.GetProperty("missing_evidence_prefix").GetString() + string.Join("、", missing);
            }
            return spec.GetProperty("missing_evidence_text").GetString()!;
        }
        var submitted = JsonSerializer.Deserialize<string[]>(json) ?? [];
        var expected = spec.GetProperty("correct_order").EnumerateArray().Select(item => item.GetString()!).ToArray();
        return submitted.SequenceEqual(expected) ? "" : spec.GetProperty("wrong_order_text").GetString()!;
    }

    public string CompletionText(string id)
    {
        if (characterSession == null || definition == null) return "";
        var action = definition.Declaration.GetProperty("actors").EnumerateArray().SelectMany(actor => actor.GetProperty("actions").EnumerateArray()).First(item => item.GetProperty("id").GetString() == id);
        var lines = new List<string>();
        if (action.TryGetProperty("completion_text", out var plain)) lines.Add(plain.GetString()!);
        if (action.TryGetProperty("completion_lines", out var variants))
        {
            foreach (var line in variants.EnumerateArray())
            {
                Func<FieldKey, Fact?> read = characterSession.World.Read;
                if (line.TryGetProperty("speaker", out var speaker))
                {
                    var knowledge = characterSession.View(speaker.GetString()!).Knowledge;
                    read = key => knowledge.TryGetValue(key, out var fact) ? fact : null;
                }
                if (!line.TryGetProperty("when", out var gate) || Condition.Compile(gate, player).Evaluate(read) == Truth.True) lines.Add(line.GetProperty("text").GetString()!);
            }
        }
        return string.Join("\n", lines);
    }

    public bool ActorIncapacitated(string actor) => characterSession != null && characterSession.Incapacitated(actor);
    public string ActorWorkingAction(string actor)
    {
        if (characterSession == null) return "";
        var activity = characterSession.Activity(actor);
        return activity is { Phase: "working" } ? activity.Action : "";
    }

    public bool Cancelled(long execution) => characterSession != null && executions.TryGetValue(execution, out var decision) && characterSession.ConfirmCancelled(decision);
    public bool Fail(long execution, string reason)
    {
        if (characterSession == null || !executions.TryGetValue(execution, out var decision)) return false;
        Record("host_action_failed", new { execution, actor = decision.Actor, action = decision.Action.Id, reported_reason = reason });
        return characterSession.HostFailed(decision, reason);
    }

    public void Vacated() => characterSession?.VacatedRetainedResources(player);
    public void ReportPlacement(double x, double y, double z, double yaw, string room) => characterSession?.ReportPlacement(player, x, y, z, yaw, room);
    public void ReportActorPlacement(string actor, double x, double y, double z, double yaw, string room) => characterSession?.ReportPlacement(actor, x, y, z, yaw, room);

    public bool BeginJourney(long execution, Vector3[] path)
    {
        if (characterSession == null || !executions.TryGetValue(execution, out var decision)) return false;
        var result = characterSession.BeginJourney(decision, path.Select(p => new PathPoint(p.X, p.Y, p.Z)));
        if (result) Record("journey_started", new
        {
            execution,
            actor = decision.Actor,
            points = path.Length,
            length_m = path.Zip(path.Skip(1), (a, b) => a.DistanceTo(b)).Sum(),
            start = new { x = path[0].X, y = path[0].Y, z = path[0].Z },
            destination = new { x = path[^1].X, y = path[^1].Y, z = path[^1].Z }
        });
        return result;
    }

    public GDict AdvanceJourney(long execution, double elapsed, double speed)
    {
        if (characterSession == null || !executions.TryGetValue(execution, out var decision)) return new GDict();
        var view = characterSession.AdvanceJourney(decision, elapsed, speed);
        Record("journey_progress", new { execution, actor = decision.Actor, elapsed, speed, distance = view.Distance, length = view.Length, view.Arrived, view.Position });
        return new GDict
        {
            { "position", new Vector3((float)view.Position.X, (float)view.Position.Y, (float)view.Position.Z) },
            { "distance", view.Distance },
            { "length", view.Length },
            { "arrived", view.Arrived }
        };
    }

    public Vector3 PreviewJourney(long execution, double elapsed, double speed)
    {
        if (characterSession == null || !executions.TryGetValue(execution, out var decision)) return Vector3.Zero;
        var point = characterSession.PreviewJourney(decision, elapsed, speed);
        return new((float)point.X, (float)point.Y, (float)point.Z);
    }

    public bool EndJourney(long execution)
    {
        if (characterSession == null || !executions.TryGetValue(execution, out var decision)) return false;
        var result = characterSession.EndJourney(decision);
        if (result) Record("journey_ended", new { execution, actor = decision.Actor });
        return result;
    }

    public void Perceive(string actor, string id, string facet, string field, string source)
    {
        if (characterSession == null) return;
        var accepted = characterSession.ReceivePhysicalObservation(actor, new(id, facet, field), source);
        Record("passive_perception", new { actor, id, facet, field, source, accepted, pending = characterSession.PendingInformation(actor), knowledge_revision = characterSession.View(actor).KnowledgeRevision });
    }

    public GDict ActorDiagnostics(string actor) => characterSession != null ? Dict(new
    {
        actor,
        pending = characterSession.PendingInformation(actor),
        evidence_count = characterSession.EvidenceCount(actor),
        knowledge = characterSession.View(actor).Knowledge.Select(pair => new { key = pair.Key, fact = pair.Value }).ToArray()
    }) : new GDict();

    public bool ConversationAvailable(string id)
    {
        if (characterSession == null || definition == null) return false;
        var declaration = definition.Declaration.GetProperty("conversations").EnumerateArray().Single(item => item.GetProperty("id").GetString() == id);
        return characterSession.ConversationAvailable(player, declaration);
    }

    public GDict StartConversation(string id)
    {
        if (characterSession == null || definition == null) return new GDict { { "error", "No active session" } };
        try
        {
            var declaration = definition.Declaration.GetProperty("conversations").EnumerateArray().Single(item => item.GetProperty("id").GetString() == id);
            var view = characterSession.StartConversation(player, declaration.GetProperty("target").GetString()!, declaration);
            Record("conversation_started", view);
            return Dict(view);
        }
        catch (InvalidOperationException error) { return new GDict { { "error", error.Message } }; }
    }

    public bool AlignConversation(long id)
    {
        if (characterSession == null) return false;
        var result = characterSession.AlignConversation(id);
        if (result) Record("conversation_aligned", characterSession.Conversation!);
        return result;
    }

    public bool AdvanceConversation(long id, int line)
    {
        if (characterSession == null) return false;
        var result = characterSession.AdvanceConversation(id, line);
        if (result) Record("conversation_advanced", new { id, line, next = characterSession.Conversation });
        return result;
    }

    public bool InterruptConversation(long id)
    {
        if (characterSession == null) return false;
        var result = characterSession.InterruptConversation(id);
        if (result) Record("conversation_interrupted", new { id });
        return result;
    }

    public bool CompleteConversation(long id)
    {
        if (characterSession == null) return false;
        var result = characterSession.CompleteConversation(id);
        if (result) Record("conversation_released", new { id });
        return result;
    }

    public GDict Conversation() => characterSession?.Conversation is { } view ? Dict(view) : new GDict();

    public string Submit(string action)
    {
        if (characterSession == null) return "No active session";
        try
        {
            characterSession.Submit(player, action);
            Record("player_action", new { action });
            return "";
        }
        catch (InvalidOperationException error) { return error.Message; }
    }

    public bool RequestYield(string actor, string action)
    {
        if (characterSession == null) return false;
        try
        {
            var decision = characterSession.Submit(actor, action);
            Record("actor_yield_requested", new { actor, action, decision.Execution });
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    public void RecordYieldCandidates(string actor, string room, Godot.Collections.Array<GDict> candidates, string selected)
        => Record("yield_candidates", new { actor, room, candidates = JsonSerializer.Deserialize<object>(Json.Stringify(Variant.From(candidates))), selected });

    public GDict Snapshot()
    {
        if (characterSession == null || definition == null)
        {
            return new GDict
            {
                ["viewer"] = player,
                ["game_seconds"] = 25200.0, // 07:00
                ["content_path"] = contentPath,
                ["world_time"] = 420.0,
                ["day"] = 1,
                ["ready"] = false
            };
        }

        var evidence = characterSession.Evidence(player).ToLookup(item => item.Key);
        return Dict(new
        {
            viewer = player,
            game_seconds = characterSession.Clock.Now,
            game_time = characterSession.ContentTime,
            content_hash = definition.Hash,
            content_update = contentUpdate,
            campaign_progress = DuelView.Runtime.CampaignProgress.Evaluate(definition.Declaration, characterSession),
            audit_status = AuditProjection(),
            activity = characterSession.Activity(player),
            placement = characterSession.Placement(player),
            needs = characterSession.Needs(player),
            world_revision = characterSession.World.Revision,
            conversation = characterSession.Conversation,
            knowledge = characterSession.View(player).Knowledge.Select(pair => new
            {
                key = pair.Key,
                fact = pair.Value,
                source_ids = evidence[pair.Key].Where(item => SameEvidenceValue(item.Value, pair.Value.Value)).Select(item => item.Id).Distinct().ToArray(),
                sources = evidence[pair.Key].Where(item => SameEvidenceValue(item.Value, pair.Value.Value)).Select(item => new
                {
                    id = item.Id,
                    kind = item.Method,
                    label = EvidenceLabel(item),
                    time = item.Time,
                    speaker = EvidenceSourceText(item, "actor", EvidenceSourceText(item, "sender")),
                    recipient = EvidenceSourceText(item, "recipient"),
                    method = EvidenceSourceText(item, "method", item.Method),
                    channel = EvidenceSourceText(item, "channel"),
                    speaker_label = EvidenceSourceText(item, "speaker_label")
                }).ToArray()
            }).ToArray(),
            decision_evaluations = characterSession.DecisionEvaluations,
            pending_seconds = characterSession.Clock.PendingSeconds,
            pending_physical_seconds = characterSession.Clock.PendingPhysicalSeconds,
            decision_pending = characterSession.HasPendingDecisionWork,
            player_paths = characterSession.PlayerPaths(player),
            active_intent = characterSession.ActiveIntent(player),
            has_host_navigation = characterSession.HasHostNavigation,
            visible_room = characterSession.VisibleRoom,
            deduction = OwnMechanic("deduction"),
            opinion = OwnMechanic("suggestion"),
            incantation = OwnMechanic("incantation"),
            mechanic_history = characterSession.MechanicHistoryFor(player).Select(item => new { time = item.Time, execution = item.Execution, actor = item.Actor, action = item.Action, kind = item.Kind, success = item.Success, details = item.Details }).ToArray(),
            recent = timeline.ToArray()
        });
    }

    private static bool SameEvidenceValue(JsonElement left, JsonElement right) => left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number
        ? left.GetDouble() == right.GetDouble() : left.GetRawText() == right.GetRawText();
    private static string EvidenceLabel(Evidence item) => item.Source.ValueKind == JsonValueKind.Object && item.Source.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String
        ? label.GetString()! : item.Method;
    private static string EvidenceSourceText(Evidence item, string key, string fallback = "") => item.Source.ValueKind == JsonValueKind.Object && item.Source.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()! : fallback;
    private object? OwnMechanic(string kind) => characterSession?.MechanicHistoryFor(player).LastOrDefault(item => item.Actor == player && item.Kind == kind)?.Details;

    // Dispatch API for Main.gd
    public GDict Dispatch(GDict input)
    {
        var kind = input.ContainsKey("kind") ? input["kind"].AsString() : "";
        var events = new GArray();

        switch (kind)
        {
            case "simulation_pulse":
                var sec = input.ContainsKey("seconds") ? (double)input["seconds"] : 0.0;
                var res = Step(sec);
                events.Add(new GDict { ["kind"] = "pulse_settled", ["seconds"] = sec, ["step"] = res });
                break;
            case "action":
                var actionId = input.GetValueOrDefault("action", "").AsString();
                var subRes = Submit(actionId);
                events.Add(new GDict { ["kind"] = "action_executed", ["action"] = actionId, ["status"] = string.IsNullOrEmpty(subRes) ? "success" : subRes });
                break;
            default:
                events.Add(new GDict { ["kind"] = "dispatched", ["payload"] = input });
                break;
        }

        EmitSignal(SignalName.EventsReady, events);
        return new GDict { ["dispatched"] = true, ["events"] = events };
    }

    /// <summary>
    /// Validates smart prefab placement and seating clearances for tools/godot_architect/ (integrating SeatingFit).
    /// </summary>
    public GDict ValidatePrefabPlacement(string prefabPath, Vector3 position, Vector3 rotation, GDict? context = null)
    {
        var result = new GDict
        {
            ["valid"] = true,
            ["reason"] = "ok",
            ["prefab"] = prefabPath,
            ["position"] = position,
            ["rotation"] = rotation
        };

        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            result["valid"] = false;
            result["reason"] = "empty_prefab_path";
            return result;
        }

        bool isChair = prefabPath.ToLowerInvariant().Contains("chair") ||
                       prefabPath.ToLowerInvariant().Contains("seat") ||
                       prefabPath.ToLowerInvariant().Contains("bench") ||
                       (context != null && context.ContainsKey("chair_fit"));

        if (isChair)
        {
            double seatHeight = 0.45;
            double standDistance = 0.55;

            if (context != null)
            {
                if (context.ContainsKey("seat_height")) seatHeight = (double)context["seat_height"];
                if (context.ContainsKey("stand_distance")) standDistance = (double)context["stand_distance"];
            }

            if (seatHeight < 0.1 || seatHeight > 1.2)
            {
                result["valid"] = false;
                result["reason"] = "invalid_seat_height";
                return result;
            }

            if (standDistance < 0.1 || standDistance > 2.0)
            {
                result["valid"] = false;
                result["reason"] = "invalid_stand_distance";
                return result;
            }

            result["seat_height"] = seatHeight;
            result["stand_distance"] = standDistance;
            result["reach_target"] = position + new Vector3(0, 0, (float)standDistance);
        }

        if (position.Y < -100.0f || position.Y > 300.0f)
        {
            result["valid"] = false;
            result["reason"] = "position_out_of_world_bounds";
            return result;
        }

        return result;
    }

    public string EstimateNavigationTravel(Rid map, Vector3 origin, Vector3 target,
        double minimumSpeed, double maximumSpeed, double endpointTolerance = 0.25)
    {
        if (!map.IsValid || NavigationServer3D.MapGetIterationId(map) == 0)
            return JsonSerializer.Serialize(new { status = "unknown", reason = "navigation-map-unavailable" });

        var path = NavigationServer3D.MapGetPath(map, origin, target, true);
        if (path.Length == 0 || path[0].DistanceTo(origin) > endpointTolerance || path[^1].DistanceTo(target) > endpointTolerance)
            return JsonSerializer.Serialize(new { status = "unknown", reason = "navigation-endpoints-uncovered" });

        float distance = 0f;
        for (int i = 0; i < path.Length - 1; i++) distance += path[i].DistanceTo(path[i + 1]);
        double avgSpeed = (minimumSpeed + maximumSpeed) * 0.5;
        double travelTime = avgSpeed > 0 ? distance / avgSpeed : 0.0;

        return JsonSerializer.Serialize(new
        {
            status = "reachable",
            distance,
            estimated_seconds = travelTime,
            waypoints = path.Length
        });
    }

    public void CloseTrace()
    {
        closing = true;
        log?.Dispose();
        log = null;
        EmitSignal(SignalName.TraceClosed);
    }

    public override void _ExitTree()
    {
        if (log is null || characterSession == null || definition == null) return;
        while (characterSession.TryReceipt(out var receipt)) Record("action_receipt", receipt!);
        DrainAudit();
        Record("session_closed", new
        {
            dispatch_count = auditCounts.GetValueOrDefault("action_dispatched"),
            receipt_count = auditCounts.GetValueOrDefault("action_receipt"),
            effect_count = auditCounts.GetValueOrDefault("action_effects"),
            evidence_count = auditCounts.GetValueOrDefault("evidence_received"),
            last_sequence = auditSequence + 1,
            successful_count = auditCounts.GetValueOrDefault("successful_receipt"),
            recoverable_cancellations = auditCounts.GetValueOrDefault("recoverable_cancellation"),
            rejected_count = auditCounts.GetValueOrDefault("rejected_receipt"),
            blocked_attempt_count = auditCounts.GetValueOrDefault("blocked_attempt"),
            unexpected_failure_count = auditCounts.GetValueOrDefault("unexpected_failure"),
            content_hash = definition.Hash,
            game_seconds = characterSession.Clock.Now,
            progress = DuelView.Runtime.CampaignProgress.Evaluate(definition.Declaration, characterSession),
            error = auditError
        });
        try { log.Flush(); if (log.BaseStream is FileStream stream) stream.Flush(true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { auditError = error.Message; GD.PushError("Causal audit close failed: " + error.Message); }
        log.Dispose();
        log = null;
        auditClosed = true;
    }
}
