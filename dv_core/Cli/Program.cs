using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

// The production text host. One JSON operation per line, one new observation
// per response. MCP and a human terminal use this same session owner.
Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
var protocolOutput = Console.Out;
Console.SetOut(Console.Error);
DvSession? session = null;
IReadOnlyDictionary<string, object?>? presentation = null;
var faulted = false;
string controller = "player";
StreamWriter? receiptOutput = null;
string? receiptPath = null;
long receiptsFlushed = 0;
long navigationReceiptCursor = 0;
long navigationObservationCursor = 0;
long requestReceiptCursor = 0;
long requestObservationCursor = 0;
var listeningDiagnostics = Environment.GetEnvironmentVariable("DV_LISTENING_DIAGNOSTICS") == "1";
var receiptJson = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
string lastChapterSummary = "false";
string lastRunBounds = "";
CliTranscript? transcript = null;

(object? Screen, object Options, IReadOnlyDictionary<string, object?> Ranking) CaptureDisplay()
{
    var current = session ?? throw new InvalidOperationException("Open a session first.");
    var preview = current.Preview(current.Viewer);
    var intent = current.Intent;
    var options = controller == "ai" ? preview.GetValueOrDefault("options") ?? Array.Empty<object>()
        : intent.GetValueOrDefault("status") as string == "awaiting_input"
            ? intent.GetValueOrDefault("options") ?? Array.Empty<object>() : Array.Empty<object>();
    var screen = preview.GetValueOrDefault("subjective");
    var ranking = current.CliCandidateRanking(controller == "ai");
    transcript?.Display(current.Version, current.TextStatus(), screen, options, presentation, ranking);
    return (screen, options, ranking);
}

Dictionary<string, object?> RunBounds(IReadOnlyDictionary<string, object?> status) =>
    status.Where(pair => pair.Key is "run_start" or "horizon" or "limit")
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

void WriteReceiptBlock(IReadOnlyDictionary<string, object?> record)
{
    if (receiptOutput is null) return;
    var block = new StringBuilder();
    var first = true;
    foreach (var pair in record)
    {
        block.Append(first ? "- " : "  ").Append(pair.Key).Append(": ")
            .AppendLine(JsonSerializer.Serialize(pair.Value, receiptJson));
        first = false;
    }
    block.AppendLine(); // Only a complete blank-line-terminated block is read.
    receiptOutput.Write(block);
    receiptOutput.Flush();
}

void RecordFaultAttempt(IReadOnlyDictionary<string, object?> attempt, Exception error)
{
    var record = new Dictionary<string, object?>
    {
        ["kind"] = "fault", ["completion_confirmed"] = false,
        ["failed_epoch"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
        ["after_receipt"] = receiptsFlushed
    };
    foreach (var pair in attempt) record[pair.Key] = pair.Value;
    record["exception_type"] = error.GetType().FullName;
    record["message"] = error.Message;
    // The failing transition may already have committed. Read no state here
    // and make no claim that it completed, rolled back, or changed nothing.
    WriteReceiptBlock(record);
}

void RecordExecutionReceipt(IReadOnlyDictionary<string, object?> receipt,
    IReadOnlyDictionary<string, object?>? listening = null, IReadOnlyDictionary<string, object?>? coordination = null)
{
    if (receiptOutput is null) return;
    var current = session ?? throw new InvalidOperationException("Open a session first.");
    var completedEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    var status = current.CompactStatus();
    var record = new Dictionary<string, object?>
    {
        ["receipt_index"] = receiptsFlushed + 1, ["completed_epoch"] = completedEpoch,
        ["version"] = current.Version, ["chapter"] = status.GetValueOrDefault("chapter"),
        ["game_time"] = receipt.GetValueOrDefault("game_time"),
        ["actor"] = receipt.GetValueOrDefault("completed_actor"),
        ["action"] = receipt.GetValueOrDefault("completed_action"),
        ["outcome"] = receipt.GetValueOrDefault("outcome"),
        ["action_event"] = receipt.GetValueOrDefault("action_event"),
        ["execution_event"] = receipt.GetValueOrDefault("execution_event"),
        ["visible"] = receipt.GetValueOrDefault("visible"),
        ["state"] = status.Where(pair => pair.Key is not ("version" or "chapter" or "chapter_summary" or "run_start" or "horizon" or "limit"))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
    };
    if (listening is { Count: > 0 }) record["listening_diagnostic"] = listening;
    if (receiptsFlushed == 0) record["host_build"] = current.HostBuildInfo;
    var navigation = current.NavigationDiagnostics(navigationReceiptCursor);
    if (HostValues.List(navigation, "events").Count > 0) record["navigation_diagnostics"] = navigation;
    var requests = current.RequestDiagnostics(requestReceiptCursor);
    if (HostValues.List(requests, "events").Count > 0) record["request_diagnostics"] = requests;
    if (coordination is { Count: > 0 }) record["coordination_diagnostic"] = coordination;
    try
    {
        var contentDecisions = current.ContentDecisionDiagnostics(receipt);
        if (contentDecisions.Count > 0) record["developer_content_decisions"] = contentDecisions;
    }
    catch (Exception diagnosticError)
    {
        record["developer_content_decisions"] = new { status = "diagnostic-unavailable", message = diagnosticError.Message };
    }
    var bounds = RunBounds(status);
    var serializedBounds = JsonSerializer.Serialize(bounds, receiptJson);
    if (serializedBounds != lastRunBounds) record["run_bounds"] = bounds;
    // These fields are already filtered by the real viewer's presentation.
    // Never reconstruct dialogue from hidden action definitions.
    foreach (var key in new[] { "speaker_name", "voice", "surface_voice", "surface", "lines", "travel", "arrival", "judgment", "reason", "failure_class", "outcome_category", "target" })
    {
        if (!receipt.TryGetValue(key, out var value) || value is null or false or "") continue;
        if (value is System.Collections.ICollection collection && collection.Count == 0) continue;
        record[key] = value;
    }
    var chapterSummary = status.GetValueOrDefault("chapter_summary");
    var serializedSummary = JsonSerializer.Serialize(chapterSummary, receiptJson);
    if (serializedSummary != lastChapterSummary)
    {
        record["chapter_summary"] = chapterSummary;
        lastChapterSummary = serializedSummary;
    }
    // A YAML block per completed formal action. JSON-encoded values are valid
    // YAML and keep quotes/newlines unambiguous without another dependency.
    WriteReceiptBlock(record);
    navigationReceiptCursor = Convert.ToInt64(navigation["next_event_cursor"]);
    requestReceiptCursor = Convert.ToInt64(requests["next_event_cursor"]);
    lastRunBounds = serializedBounds;
    receiptsFlushed++;
}

object Observe(int completed = 0)
{
    var current = session ?? throw new InvalidOperationException("Open a session first.");
    var navigation = current.NavigationDiagnostics(navigationObservationCursor);
    transcript?.NavigationDiagnostics(navigation);
    navigationObservationCursor = Convert.ToInt64(navigation["next_event_cursor"]);
    var requests = current.RequestDiagnostics(requestObservationCursor);
    transcript?.RequestDiagnostics(requests);
    requestObservationCursor = Convert.ToInt64(requests["next_event_cursor"]);
    var display = CaptureDisplay();
    if (receiptOutput is not null && controller == "ai")
    {
        var compact = current.CompactStatus();
        if (receiptsFlushed == 0) lastRunBounds = JsonSerializer.Serialize(RunBounds(compact), receiptJson);
        return new
        {
            backend = "cli", process_id = Environment.ProcessId, controller, version = current.Version, completed,
            build = current.HostBuildInfo,
            navigation_diagnostics = navigation,
            request_diagnostics = requests,
            status = compact, runtime = compact, content = current.ContentInfo,
            performance = current.CompactPerformance(),
            receipts_flushed = receiptsFlushed, receipt_format = "dv-cli-actions-yaml-v1",
            active = compact.GetValueOrDefault("active"), faulted, execution = "formal-model",
            screen = display.Screen, options = display.Options, ai_candidates = display.Ranking, presentation,
            transcript_path = transcript?.Path, detail_log_path = transcript?.DetailPath
        };
    }
    var viewer = current.Viewer;
    var preview = current.Preview(viewer);
    var intent = current.Intent;
    return new
    {
        backend = "cli", process_id = Environment.ProcessId, controller, version = current.Version, completed,
        build = new {
            identity = current.HostBuildInfo,
            assembly = typeof(DvSession).Assembly.Location,
            jit_optimizer_disabled = typeof(DvSession).Assembly
                .GetCustomAttributes(typeof(System.Diagnostics.DebuggableAttribute), false)
                .OfType<System.Diagnostics.DebuggableAttribute>().FirstOrDefault()?.IsJITOptimizerDisabled ?? false
        },
        status = current.TextStatus(),
        navigation_diagnostics = navigation,
        request_diagnostics = requests,
        screen = display.Screen,
        options = display.Options,
        ai_candidates = display.Ranking,
        performance = preview.GetValueOrDefault("performance") ?? current.CompactPerformance(),
        planning = controller == "ai" ? current.PlanningDiagnostics() : null,
        runtime = controller == "ai" ? current.ExecutionDiagnostics() : null,
        receipts_flushed = receiptsFlushed,
        content = current.ContentInfo,
        presentation, transcript_path = transcript?.Path, detail_log_path = transcript?.DetailPath, active = current.Active, faulted,
        execution = "formal-model"
    };
}

void Prepare()
{
    if (session!.Active && session.Intent.GetValueOrDefault("status") as string == "idle")
        session.PrepareValue();
}

string? line;
while ((line = Console.ReadLine()) is not null)
{
    line = line.Trim('\uFEFF');
    if (string.IsNullOrWhiteSpace(line)) continue;
    object? requestId = null;
    bool finish = false;
    try
    {
        using var document = JsonDocument.Parse(line);
        var request = document.RootElement;
        if (request.TryGetProperty("id", out var id)) requestId = id.Clone();
        var operation = request.GetProperty("type").GetString();
        object result;
        if (operation == "open")
        {
            if (session is not null) throw new InvalidOperationException("Session is already open.");
            var options = request.TryGetProperty("options", out var config) ? config.GetRawText() : "{}";
            controller = request.TryGetProperty("options", out config) && config.TryGetProperty("controller", out var control)
                ? control.GetString() ?? "player" : "player";
            var decisionMaker = config.TryGetProperty("decision_maker", out var maker) ? maker.GetString() ?? controller
                : controller == "ai" ? "engine-ai" : controller == "llm" ? "llm" : controller == "human" ? "human" : "player";
            if (controller is "human" or "llm") controller = "player";
            var recording = config.TryGetProperty("receipt_path", out var outputPath) &&
                !string.IsNullOrWhiteSpace(outputPath.GetString());
            session = DvSession.OpenConfiguredCampaign(options, recording ? TextWriter.Null : Console.Error);
            session.SetExecutionContext("cli", "cli");
            if (config.TryGetProperty("receipt_path", out var receiptFile) &&
                !string.IsNullOrWhiteSpace(receiptFile.GetString()))
            {
                receiptPath = Path.GetFullPath(receiptFile.GetString()!);
                receiptOutput = new StreamWriter(receiptPath, false, new UTF8Encoding(false));
            }
            var transcriptPath = receiptPath is not null ? Path.Combine(Path.GetDirectoryName(receiptPath)!, "transcript.txt")
                : Path.Combine(Environment.CurrentDirectory, ".runtime", "runs", "cli-input-" + Guid.NewGuid().ToString("N"), "transcript.txt");
            transcript = new CliTranscript(transcriptPath, session.Viewer, decisionMaker, session.ContentInfo);
            Prepare();
            result = Observe();
        }
        else if (operation == "close")
        {
            transcript?.Note("会话关闭。");
            session?.Close();
            result = new { closed = true, receipts_flushed = receiptsFlushed, receipt_path = receiptPath,
                transcript_path = transcript?.Path, detail_log_path = transcript?.DetailPath };
            finish = true;
        }
        else
        {
            if (session is null) throw new InvalidOperationException("Open a session first.");
            if (operation == "observe") result = Observe();
            else if (operation == "reload_content")
            {
                var path = request.TryGetProperty("path", out var requestedPath) ? requestedPath.GetString() : null;
                var source = request.TryGetProperty("content_source", out var json) ? json.GetString() : null;
                session.ReloadContent(path, source);
                result = Observe();
            }
            else if (operation is "select" or "advance")
            {
                if (faulted) throw new InvalidOperationException("Session faulted; observe or close it. Do not retry an action.");
                var steps = request.TryGetProperty("steps", out var count) ? count.GetInt32() : 1;
                if (steps is < 0 or > 100) throw new ArgumentException("steps must be 0..100.");
                if (operation == "select")
                {
                    var choice = request.GetProperty("choice_id").GetString() ?? "";
                    if (controller != "player" || session.Intent.GetValueOrDefault("status") as string != "awaiting_input")
                        throw new InvalidOperationException("Selection requires a player decision boundary.");
                    var options = JsonSerializer.SerializeToElement(session.Intent.GetValueOrDefault("options"));
                    if (!options.EnumerateArray().Any(option => option.TryGetProperty("id", out var key) && key.GetString() == choice))
                        throw new ArgumentException("choice_id is not in the current observation.");
                    CaptureDisplay();
                    var selectedBy = request.TryGetProperty("decision_maker", out var submittedBy) ? submittedBy.GetString() : null;
                    if (selectedBy is not (null or "human" or "agent" or "llm" or "player"))
                        throw new ArgumentException("decision_maker must be human, agent, llm or player.");
                    transcript?.Choice(choice, session.Viewer, "提交", selectedBy: selectedBy);
                    try
                    {
                        session.PrepareValue(choice);
                        transcript?.Choice(choice, session.Viewer, "最终选择已接纳", selectedBy: selectedBy);
                    }
                    catch { faulted = true; throw; }
                }
                var completed = 0;
                try
                {
                    for (; completed < steps && session.Active; completed++)
                    {
                        Prepare();
                        var intent = session.Intent;
                        if (intent.GetValueOrDefault("status") as string != "prepared") break;
                        CaptureDisplay();
                        if (controller == "ai" && HostValues.Text(intent, "actor") == session.Viewer)
                            transcript?.Choice(HostValues.Text(HostValues.Map(intent, "action"), "id"), session.Viewer,
                                "最终选择已准备执行", intent.GetValueOrDefault("action"));
                        var attempt = new Dictionary<string, object?>
                        {
                            ["version_before"] = session.Version,
                            ["game_time"] = intent.GetValueOrDefault("game_time"),
                            ["actor"] = intent.GetValueOrDefault("actor"),
                            ["action"] = HostValues.Map(intent, "action").GetValueOrDefault("id"),
                            ["action_event"] = intent.GetValueOrDefault("action_event")
                        };
                        IReadOnlyDictionary<string, object?>? listening = null;
                        IReadOnlyDictionary<string, object?>? coordination = null;
                        if (listeningDiagnostics && receiptOutput is not null)
                        {
                            // Freeze diagnostics before the actual completion. Missing
                            // plans stay missing; no choice or score is recomputed here.
                            try { listening = session.PreparedListeningDiagnostics(); }
                            catch (Exception diagnosticError)
                            {
                                listening = HostValues.Freeze(new Dictionary<string, object?>
                                {
                                    ["status"] = "diagnostic-failed-before-complete",
                                    ["message"] = diagnosticError.Message
                                });
                            }
                            if (listening is { Count: > 0 }) attempt["listening_diagnostic"] = listening;
                            try { coordination = session.PreparedCoordinationDiagnostics(); }
                            catch (Exception diagnosticError)
                            {
                                coordination = HostValues.Freeze(new Dictionary<string, object?>
                                {
                                    ["status"] = "diagnostic-failed-before-complete",
                                    ["message"] = diagnosticError.Message
                                });
                            }
                            if (coordination is { Count: > 0 }) attempt["coordination_diagnostic"] = coordination;
                        }
                        try { presentation = session.CompleteFormal(session.Viewer); }
                        catch (Exception error)
                        {
                            try { RecordFaultAttempt(attempt, error); }
                            catch (Exception logError)
                            {
                                throw new AggregateException("Formal action fault; the failure record could not be flushed.", error, logError);
                            }
                            throw;
                        }
                        RecordExecutionReceipt(presentation, listening, coordination);
                        transcript?.RecordStory(presentation, session.Viewer);
                        if (HostValues.Text(presentation, "completed_actor") == session.Viewer)
                            transcript?.Execution(presentation);
                        CaptureDisplay();
                    }
                    Prepare();
                }
                catch { faulted = true; throw; }
                result = Observe(completed);
            }
            else if (operation == "cancel")
            {
                if (faulted) throw new InvalidOperationException("Session faulted; observe or close it.");
                Prepare();
                var intent = session.Intent;
                if (intent.GetValueOrDefault("status") as string != "prepared")
                    throw new InvalidOperationException("No prepared action to cancel.");
                var cancelReason = request.TryGetProperty("reason", out var customReason) ? customReason.GetString() ?? "user-cancelled" : "user-cancelled";
                var attempt = new Dictionary<string, object?>
                {
                    ["version_before"] = session.Version,
                    ["game_time"] = intent.GetValueOrDefault("game_time"),
                    ["actor"] = intent.GetValueOrDefault("actor"),
                    ["action"] = HostValues.Map(intent, "action").GetValueOrDefault("id"),
                    ["action_event"] = intent.GetValueOrDefault("action_event")
                };
                var cancelReceipt = new Dictionary<string, object?>
                {
                    ["action_event"] = intent.GetValueOrDefault("action_event"),
                    ["outcome"] = "cancelled",
                    ["reason"] = cancelReason,
                    ["executor"] = "formal-model",
                    ["actual"] = intent.GetValueOrDefault("execution_cost") ?? new Dictionary<string, object?> { ["time"] = 0d }
                };
                try
                {
                    presentation = session.CompleteValue(cancelReceipt, session.Viewer);
                }
                catch (Exception error)
                {
                    try { RecordFaultAttempt(attempt, error); }
                    catch (Exception logError)
                    {
                        throw new AggregateException("Formal action cancel fault; the failure record could not be flushed.", error, logError);
                    }
                    throw;
                }
                RecordExecutionReceipt(presentation);
                transcript?.RecordStory(presentation, session.Viewer);
                if (HostValues.Text(presentation, "completed_actor") == session.Viewer)
                    transcript?.Execution(presentation);
                CaptureDisplay();
                Prepare();
                result = Observe(1);
            }
            else throw new ArgumentException("Unknown operation. Use open, observe, select, advance, cancel, close.");
        }
        protocolOutput.WriteLine(JsonSerializer.Serialize(new { id = requestId, result }));
    }
    catch (Exception error)
    {
        transcript?.Note("请求失败：" + error.Message);
        protocolOutput.WriteLine(JsonSerializer.Serialize(new { id = requestId, error = error.Message, faulted }));
    }
    protocolOutput.Flush();
    if (finish) break;
}
session?.Close();
receiptOutput?.Dispose();
transcript?.Dispose();
