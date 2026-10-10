using System.Diagnostics;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Gameplay;

/// <summary>Host-side evidence only. Core execution records never imply player observation.</summary>
public sealed class GameTrace : IDisposable
{
    internal sealed record SaveState([property:System.Text.Json.Serialization.JsonRequired] long Sequence,[property:System.Text.Json.Serialization.JsonRequired] long Request,[property:System.Text.Json.Serialization.JsonRequired] long Output,[property:System.Text.Json.Serialization.JsonRequired] long ExecutionSerial,[property:System.Text.Json.Serialization.JsonRequired] string State);
    internal SaveState CaptureSave() => new(_sequence,_request,_output,_executionSerial,_state);
    internal void RestoreSave(SaveState state)
    {
        if(state.Sequence<0||state.Request<0||state.Output<0||state.ExecutionSerial<0) throw new InvalidDataException("Invalid trace counters.");
        _sequence=state.Sequence; _request=state.Request; _output=state.Output; _executionSerial=state.ExecutionSerial; _state=state.State;
    }
    private readonly string? _directory;
    private readonly Action<string>? _warning;
    private readonly Stopwatch _elapsed = new();
    private StreamWriter? _writer;
    private bool _failed;
    private long _sequence, _request, _output, _executionSerial;
    private string _state = "";
    private readonly Dictionary<ActionExecution, long> _executions = new();
    public string RunId { get; private set; } = "";
    public long RequestId => _request;

    public GameTrace(string? directory, Action<string>? warning = null)
    { _directory = directory; _warning = warning; NewRun(); }

    public void NewRun()
    {
        Dispose();
        RunId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N");
        _sequence = _request = _output = _executionSerial = 0;
        _state = ""; _executions.Clear(); _failed = false; _elapsed.Restart();
        if (_directory is null) return;
        try
        {
            if (!Path.IsPathFullyQualified(_directory)) throw new IOException("Trace directory must be absolute.");
            Directory.CreateDirectory(_directory);
            _writer = new StreamWriter(Path.Combine(_directory, RunId + ".jsonl"), false,
                new System.Text.UTF8Encoding(false)) { AutoFlush = true };
            Write("run_started", new { started_utc = DateTime.UtcNow, schema_version = 1 });
        }
        catch (Exception error) { Fail(error); }
    }

    public long BeginRequest(IReadOnlyDictionary<string, object?> input)
    {
        ++_request;
        if (input.GetValueOrDefault("kind") as string is not ("tick" or "daily"))
            Write("input", input);
        return _request;
    }

    public void Output(Dictionary<string, object?> output)
    {
        output["run_id"] = RunId;
        output["request_id"] = _request;
        output["trace_id"] = RunId + ":output:" + ++_output;
        if (output.GetValueOrDefault("lines") is IEnumerable<object?> lines)
        {
            var index = 0;
            foreach (var line in lines.OfType<Dictionary<string, object?>>())
            {
                line.TryAdd("line_id", ""); line.TryAdd("source_id", "");
                line["line_index"] = index++;
                line["text_origin"] = string.IsNullOrEmpty(line.GetValueOrDefault("line_id")?.ToString())
                    && string.IsNullOrEmpty(line.GetValueOrDefault("source_id")?.ToString()) ? "generated" : "authored";
            }
        }
        if (output.GetValueOrDefault("kind") as string == "state")
        {
            if (output.GetValueOrDefault("snapshot") is not IReadOnlyDictionary<string, object?> snapshot) return;
            var summary = snapshot.Where(p => p.Value is null or string or bool or int or long or float or double)
                .ToDictionary(p => p.Key, p => p.Value);
            if (snapshot.GetValueOrDefault("hud") is IReadOnlyDictionary<string, object?> hud)
                summary["hud"] = hud.Where(p => p.Value is null or string or bool or int or long or float or double)
                    .ToDictionary(p => p.Key, p => p.Value);
            var serialized = JsonSerializer.Serialize(summary);
            if (serialized == _state) return;
            _state = serialized;
            Write("state_changed", new { trace_id = output["trace_id"], summary });
        }
        else Write("output", output);
    }

    public void Visible(IReadOnlyDictionary<string, object?> payload) => Write("ui_visible", payload);
    public void PhysicalInput(IReadOnlyDictionary<string, object?> payload) => Write("ui_input", payload);
    public void Metadata(object payload) => Write("author_sources", payload);
    public void RequestFailed(Exception error) => Write("request_failed", new { type = error.GetType().Name, message = error.Message });

    public void Action(string phase, ActionExecution execution, double time)
    {
        if (!_executions.TryGetValue(execution, out var id)) _executions[execution] = id = ++_executionSerial;
        Write("engine_action", new
        {
            phase, execution_id = RunId + ":execution:" + id, core_time = time,
            actor = execution.Intent.Actor.Name, basis = execution.Intent.Basis?.Name,
            effects = EffectText(execution.Intent.Changes),
            requirements = execution.Intent.Requirements?.ToString(),
            status = execution.Status.ToString(), succeeded = execution.Result?.Succeeded,
            failure = execution.Result?.Failure.ToString(), result_time = execution.Result?.Time,
            visibility = "engine_only_not_player_observed"
        });
    }

    private static string NounText(Noun noun) => noun switch
    { Noun.Reference reference => reference.Projection.Name, Noun.Slot slot => slot.Role.ToString(), _ => noun.ToString() ?? "" };
    private static string EffectText(Effect effect) => effect switch
    {
        Effect.Sequence sequence => string.Join("; ", sequence.Items.Select(EffectText)),
        Effect.When conditional => $"when {conditional.Condition}: {EffectText(conditional.Body)}",
        Effect.Wait wait => $"wait {wait.Delay} until {wait.Until}: {EffectText(wait.Body)}",
        Effect.Say say => $"say to {NounText(say.Recipient)}: {say.Text}" + (say.Request is null ? "" : " request " + EffectText(say.Request)),
        Effect.Act act => $"{act.Operation} {NounText(act.Target)}" + (act.Tool is null ? "" : " with " + NounText(act.Tool)),
        _ => effect.ToString() ?? effect.GetType().Name
    };

    private void Write(string kind, object? data)
    {
        if (_writer is null || _failed) return;
        try
        {
            _writer.WriteLine(JsonSerializer.Serialize(new
            { run_id = RunId, sequence = ++_sequence, request_id = _request, elapsed_ms = _elapsed.ElapsedMilliseconds, kind, data }));
        }
        catch (Exception error) { Fail(error); }
    }
    private void Fail(Exception error)
    {
        _failed = true;
        try { _writer?.Dispose(); } catch { }
        _writer = null;
        try { _warning?.Invoke("Player trace could not be written; gameplay continues: " + error.Message); } catch { }
    }
    public void Dispose()
    {
        if (_writer is null) return;
        Write("run_closed", new { elapsed_ms = _elapsed.ElapsedMilliseconds });
        try { _writer?.Dispose(); } catch (Exception error) { Fail(error); }
        _writer = null;
    }
}
