using System.Text;
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

void RecordExecutionReceipt(IReadOnlyDictionary<string, object?> receipt)
{
    if (receiptOutput is null) return;
    var current = session ?? throw new InvalidOperationException("Open a session first.");
    var completedEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    receiptOutput.WriteLine(JsonSerializer.Serialize(new
    {
        receipt_index = receiptsFlushed + 1,
        completed_epoch = completedEpoch,
        backend = "cli", controller, execution = "formal-model",
        viewer = current.Viewer, version = current.Version,
        content_hash = current.ContentInfo.GetValueOrDefault("hash"),
        content_schema = current.ContentInfo.GetValueOrDefault("schema"),
        game_time = receipt.GetValueOrDefault("game_time"),
        presentation = receipt
    }));
    receiptOutput.Flush();
    receiptsFlushed++;
}

object Observe(int completed = 0)
{
    var current = session ?? throw new InvalidOperationException("Open a session first.");
    var viewer = current.Viewer;
    var preview = current.Preview(viewer);
    var intent = current.Intent;
    return new
    {
        backend = "cli", controller, version = current.Version, completed,
        build = new {
            assembly = typeof(DvSession).Assembly.Location,
            jit_optimizer_disabled = typeof(DvSession).Assembly
                .GetCustomAttributes(typeof(System.Diagnostics.DebuggableAttribute), false)
                .OfType<System.Diagnostics.DebuggableAttribute>().FirstOrDefault()?.IsJITOptimizerDisabled ?? false
        },
        status = current.TextStatus(),
        screen = preview.GetValueOrDefault("subjective"),
        options = intent.GetValueOrDefault("status") as string == "awaiting_input"
            ? intent.GetValueOrDefault("options") : Array.Empty<object>(),
        performance = preview.GetValueOrDefault("performance"),
        planning = controller == "ai" ? current.PlanningDiagnostics() : null,
        runtime = controller == "ai" ? current.ExecutionDiagnostics() : null,
        receipts_flushed = receiptsFlushed,
        content = current.ContentInfo,
        presentation, active = current.Active, faulted,
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
            session = DvSession.OpenConfiguredCampaign(options, Console.Error);
            session.SetExecutionContext("cli", "cli");
            if (controller == "ai" && config.TryGetProperty("receipt_path", out var receiptFile) &&
                !string.IsNullOrWhiteSpace(receiptFile.GetString()))
            {
                receiptPath = Path.GetFullPath(receiptFile.GetString()!);
                receiptOutput = new StreamWriter(receiptPath, false, new UTF8Encoding(false));
            }
            Prepare();
            result = Observe();
        }
        else if (operation == "close")
        {
            session?.Close();
            result = new { closed = true, receipts_flushed = receiptsFlushed, receipt_path = receiptPath };
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
                if (steps is < 1 or > 100) throw new ArgumentException("steps must be 1..100.");
                if (operation == "select")
                {
                    var choice = request.GetProperty("choice_id").GetString() ?? "";
                    if (controller != "player" || session.Intent.GetValueOrDefault("status") as string != "awaiting_input")
                        throw new InvalidOperationException("Selection requires a player decision boundary.");
                    var options = JsonSerializer.SerializeToElement(session.Intent.GetValueOrDefault("options"));
                    if (!options.EnumerateArray().Any(option => option.TryGetProperty("id", out var key) && key.GetString() == choice))
                        throw new ArgumentException("choice_id is not in the current observation.");
                    try { session.PrepareValue(choice); }
                    catch { faulted = true; throw; }
                }
                var completed = 0;
                try
                {
                    for (; completed < steps && session.Active; completed++)
                    {
                        Prepare();
                        if (session.Intent.GetValueOrDefault("status") as string != "prepared") break;
                        presentation = session.CompleteFormal(session.Viewer);
                        RecordExecutionReceipt(presentation);
                    }
                    Prepare();
                }
                catch { faulted = true; throw; }
                result = Observe(completed);
            }
            else throw new ArgumentException("Unknown operation. Use open, observe, select, advance, close.");
        }
        protocolOutput.WriteLine(JsonSerializer.Serialize(new { id = requestId, result }));
    }
    catch (Exception error)
    {
        protocolOutput.WriteLine(JsonSerializer.Serialize(new { id = requestId, error = error.Message, faulted }));
    }
    protocolOutput.Flush();
    if (finish) break;
}
session?.Close();
receiptOutput?.Dispose();
