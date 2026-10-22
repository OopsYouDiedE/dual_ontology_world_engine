using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Parsing help and recorded replay never opens a live campaign.
return await Cli.Run(args);

internal sealed class Cli
{
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 512 };
    private DvSession? session;
    private SmartReplay? replay;
    private bool jsonLines, tui, quit, interactiveReplay, oneCommand, debugErrors;
    private string? savePath, tracePath, performancePath;
    private bool? showScores;
    private readonly List<TerminalEntry> terminalEntries = new();
    private bool transcriptTruncated;
    private IReadOnlyDictionary<string, object?> replaySnapshot = HostValues.Empty;
    private object? replayGameTime;
    private ReplayFrame? terminalFrame;
    private int terminalWidth = 120, terminalHeight = 42;
    private int terminalPage;
    private int actionPage = -1;
    private bool expandActions;
    private string optionTimeBasis = "current";
    private bool noColor, showActors;
    private bool batchAdvance;
    private string terminalHistory = "latest";
    private string? exportFramePath;
    private string terminalZoom = "fit";
    private string mapView = "subjective";
    private string displayViewer = "";
    private bool followReplayActor = true;
    private string interactionPhase = "";
    private ChoiceFocus? focus;
    private long interactionSequence;
    private readonly string interactionSession = Guid.NewGuid().ToString("N");
    private readonly Stopwatch recordingClock = Stopwatch.StartNew();
    private IReadOnlyDictionary<string, object?> replayPreview = HostValues.Empty;
    private IReadOnlyDictionary<string, object?> replayPerformance = HostValues.Empty;
    private string replayControllerKind = "";
    private IReadOnlyList<IReadOnlyDictionary<string, object?>> replayOptions = Array.Empty<IReadOnlyDictionary<string, object?>>();
    private string replaySelected = "";
    private string replayChoiceActor = "";
    private sealed record ChoiceFocus(string Id, string Selection, string Actor, string Label, long Version, string InteractionId,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Options, IReadOnlyDictionary<string, object?> Preview);
    private sealed record CapturedPage(string path, int page, int page_count, int? frame, object? game_time);
    private readonly Dictionary<string, int> roomOrder = new(StringComparer.Ordinal);
    private string orderedViewer = "";
    private readonly CancellationTokenSource cancellation = new();
    private IntPtr consoleOutput;
    private uint? originalOutputMode;
    private const int TranscriptCapacity = 40;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int standardHandle);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);

    private void ConfigureConsoleColor()
    {
        if (!tui || jsonLines || noColor || Environment.GetEnvironmentVariable("NO_COLOR") is not null ||
            !OperatingSystem.IsWindows() || Console.IsOutputRedirected) return;
        var handle = GetStdHandle(-11);
        if (!GetConsoleMode(handle, out var mode)) { noColor = true; return; }
        var enabled = mode | 0x0001u | 0x0004u;
        if (enabled == mode) return;
        if (!SetConsoleMode(handle, enabled)) { noColor = true; return; }
        consoleOutput = handle;
        originalOutputMode = mode;
    }

    private void RestoreConsoleMode()
    {
        if (originalOutputMode is not { } mode) return;
        SetConsoleMode(consoleOutput, mode);
        originalOutputMode = null;
    }

    internal static async Task<int> Run(string[] arguments)
    {
        var cli = new Cli();
        var exitCode = 0;
        Console.CancelKeyPress += cli.Cancel;
        try { await cli.Start(arguments); }
        catch (OperationCanceledException) when (cli.cancellation.IsCancellationRequested) { exitCode = 130; }
        catch (Exception error) { cli.Error(error); exitCode = 1; }
        finally
        {
            Console.CancelKeyPress -= cli.Cancel;
            // Each artifact is attempted independently, with Close called once.
            if (cli.session is { } current)
            {
                try { current.Close(); }
                catch (Exception error) { cli.Error(error); exitCode = 1; }
                if (cli.savePath is { } save)
                    try { current.WriteSave(save); }
                    catch (Exception error) { cli.Error(error); exitCode = 1; }
                if (cli.tracePath is { } trace)
                    try { current.WriteTrace(trace); }
                    catch (Exception error) { cli.Error(error); exitCode = 1; }
                var prefix = cli.performancePath ?? ((cli.tracePath ?? cli.savePath) is { } saveFile
                    ? Path.ChangeExtension(saveFile, null) + ".performance"
                    : Path.Combine("artifacts", "performance",
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + cli.interactionSession[..8], "engine"));
                try { current.WritePerformanceReport(prefix); }
                catch (Exception error) { cli.Error(error); exitCode = 1; }
            }
            cli.RestoreConsoleMode();
            cli.cancellation.Dispose();
        }
        return exitCode;
    }

    private async Task Start(string[] args)
    {
        string? replayPath = null, restore = null, command = null;
        var viewer = "detective";
        var explicitViewer = false;
        var controller = "player";
        var explicitController = false;
        var sourceRevision = "";
        var developer = false;
        var audit = false;
        var speed = 1d;
        var campaign = new Dictionary<string, object?>();
        var preview = false;
        var explicitMap = false;
        var previewRooms = 3;
        var previewCurrent = 1;
        string? exportUi = null;
        for (var i = 0; i < args.Length; i++)
        {
            string Value()
            {
                if (++i >= args.Length) throw new ArgumentException($"Missing value for {args[i - 1]}.");
                return args[i];
            }
            switch (args[i])
            {
                case "--help": case "-h": Console.WriteLine(Help); return;
                case "--jsonl": jsonLines = true; break;
                case "--tui": tui = true; break;
                case "--preview": preview = true; tui = true; break;
                case "--preview-rooms": previewRooms = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--preview-current": previewCurrent = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--ui-zoom": terminalZoom = Value(); break;
                case "--ui-history": terminalHistory = ParseHistory(Value()); break;
                case "--export-ui": exportUi = Value(); break;
                case "--width": terminalWidth = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--height": terminalHeight = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--no-color": noColor = true; break;
                case "--dev": developer = debugErrors = true; break;
                case "--audit": audit = true; break;
                case "--viewer": viewer = Value(); explicitViewer = true; break;
                case "--controller": controller = Value().ToLowerInvariant(); explicitController = true; break;
                case "--restore": restore = Value(); break;
                case "--save": savePath = Value(); break;
                case "--trace": tracePath = Value(); break;
                case "--performance": performancePath = Value(); break;
                case "--source-revision": sourceRevision = Value(); break;
                case "--scores": showScores = true; break;
                case "--no-scores": showScores = false; break;
                case "--command": command = Value(); break;
                case "--replay": replayPath = Value(); break;
                case "--interactive": interactiveReplay = true; break;
                case "--map": mapView = ParseMap(Value()); explicitMap = true; break;
                case "--speed": speed = double.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--profile": campaign["profile"] = Value(); break;
                case "--zone": campaign["detective_zone"] = Value(); break;
                case "--seed": campaign["seed"] = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--opt-taskgraph": case "--opt-storyflow": case "--opt-stateindex":
                    var optimization = args[i][6..];
                    campaign["opt_" + optimization] = Value().ToLowerInvariant() switch
                    {
                        "on" => true, "off" => false,
                        _ => throw new ArgumentException("--opt-" + optimization + " must be on or off.")
                    };
                    break;
                case "--planning":
                    campaign["selective_planning"] = Value().ToLowerInvariant() switch
                    {
                        "selective" => true, "precise" => false,
                        _ => throw new ArgumentException("--planning must be selective or precise.")
                    };
                    break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }
        if (restore is not null && campaign.Keys.Any(key => key is "opt_taskgraph" or "opt_storyflow"))
            throw new ArgumentException("Restored sessions retain saved taskgraph/storyflow policies; these switches require a new session.");
        if (replayPath is not null && campaign.Keys.Any(key => key is "opt_taskgraph" or "opt_storyflow" or "opt_stateindex"))
            throw new ArgumentException("Optimization switches configure live sessions, not recorded replay.");
        if (replayPath is null && restore is null) campaign.TryAdd("opt_taskgraph", false);
        if (campaign.TryGetValue("opt_stateindex", out var stateIndex))
            Environment.SetEnvironmentVariable("DV_OPT_STATEINDEX", stateIndex is false ? "0" : "1");
        else if (replayPath is null && restore is null)
            Environment.SetEnvironmentVariable("DV_OPT_STATEINDEX", "1");
        if (interactiveReplay && replayPath is null) throw new ArgumentException("--interactive: --replay");
        if (jsonLines && tui) throw new ArgumentException("--jsonl and --tui are separate output modes.");
        if (controller is not ("player" or "human" or "llm" or "ai"))
            throw new ArgumentException("--controller must be player, human, llm or ai.");
        if (speed is not (1 or 2 or 4 or 8)) throw new ArgumentException("--speed must be 1, 2, 4 or 8.");
        if (replayPath is not null && (restore is not null || savePath is not null || tracePath is not null || performancePath is not null))
            throw new ArgumentException("Recorded replay does not restore or save live sessions.");
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        ConfigureConsoleColor();
        if (terminalWidth is < 60 or > 240 || terminalHeight is < 24 or > 100)
            throw new ArgumentException("UI dimensions: width 60..240, height 24..100.");
        if (terminalZoom is not ("fit" or "detail" or "compact")) throw new ArgumentException("--ui-zoom: fit, detail or compact.");
        if (!preview && (previewRooms != 3 || previewCurrent != 1)) throw new ArgumentException("Preview room controls require --preview.");
        if (preview)
        {
            if (replayPath is not null || restore is not null || savePath is not null || tracePath is not null || performancePath is not null || command is not null || controller is not ("player" or "human") || developer || audit || campaign.Count > 0)
                throw new ArgumentException("--preview is a session-free UI example; do not combine it with session or replay options.");
            var model = TerminalProjection.Preview(previewRooms, previewCurrent) with { Zoom = terminalZoom, ShowScores = showScores, MapView = explicitMap ? mapView : "both" };
            if (exportUi is not null)
            {
                using var stream = new FileStream(exportUi, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(TerminalView.ExportSvg(model, terminalWidth, terminalHeight));
            }
            Console.Write(TerminalView.Render(model, terminalWidth, terminalHeight, !noColor && Environment.GetEnvironmentVariable("NO_COLOR") is null));
            return;
        }
        if (exportUi is not null)
        {
            var replayCaptureCommand = command == "step" || command?.StartsWith("seek ", StringComparison.OrdinalIgnoreCase) is true ||
                command?.StartsWith("replay frame ", StringComparison.OrdinalIgnoreCase) is true;
            if (!tui || command is null || (replayPath is null && command is not ("status" or "options")) ||
                (replayPath is not null && !replayCaptureCommand))
                throw new ArgumentException("--export-ui requires --preview, or --tui with a read-only --command status/options (recorded replay: step, seek N or replay frame N).");
            exportFramePath = exportUi;
        }
        oneCommand = command is not null;
        displayViewer = viewer;
        followReplayActor = replayPath is null || !explicitViewer;
        if (replayPath is not null)
        {
            replay = SmartReplay.Load(replayPath, cancellation.Token);
            replay.Play(speed);
            if (command is null && !interactiveReplay) { await PlayReplay(); return; }
            replay.Pause();
        }
        else
        {
            session = restore is not null ? DvSession.RestoreFile(restore) : DvSession.OpenConfiguredCampaign(
                JsonSerializer.Serialize(new { controller, viewer, campaign, audit, developer }, Json), tui && !developer ? TextWriter.Null : Console.Error);
            if (restore is not null && explicitController)
                session.SetPerformanceController(controller switch
                {
                    "player" or "human" => "human", "llm" => "llm", _ => "engine"
                });
            if (tui && !developer) session.DiagnosticOutput = TextWriter.Null;
            session.SetExecutionContext("cli", "formal-model", sourceRevision);
            performancePath ??= (tracePath ?? savePath) is { } artifact
                ? Path.ChangeExtension(artifact, null) + ".performance"
                : Path.Combine("artifacts", "performance", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + interactionSession[..8], "engine");
            session.StartPerformanceRecording(performancePath);
            // A caller explicitly advances with next/act. Opening/status does not run NPCs.
        }
        displayViewer = session?.Viewer ?? viewer;
        if (command is not null) { await Execute(command); return; }
        if (!jsonLines) Emit("status", CurrentStatus());
        if (replay is not null) { await ReplayCommandLoop(); return; }
        while (!quit)
        {
            if (!Console.IsInputRedirected && !jsonLines) Console.Write(replay is null ? "dv> " : "replay> ");
            var line = await Console.In.ReadLineAsync(cancellation.Token);
            if (line is null) break;
            try { await Execute(line); }
            catch (Exception error) { if (Console.IsInputRedirected) throw; Error(error); }
        }
    }

    private async Task ReplayCommandLoop()
    {
        var player = replay ?? throw new InvalidOperationException("回放 —");
        Task<string?> ReadInput() => Task.Run(async () => await Console.In.ReadLineAsync(cancellation.Token), cancellation.Token);
        var input = ReadInput();
        var clock = Stopwatch.StartNew();
        var previous = clock.Elapsed.TotalSeconds;
        while (!quit)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            await Task.WhenAny(input, Task.Delay(15, cancellation.Token));
            if (input.IsCompleted)
            {
                var line = await input;
                if (line is null) break;
                try { await Execute(line); }
                catch (Exception error) { if (Console.IsInputRedirected) throw; Error(error); }
                if (quit) break;
                input = ReadInput();
            }
            var now = clock.Elapsed.TotalSeconds;
            foreach (var frame in player.Advance(now - previous)) ShowFrame(frame);
            previous = clock.Elapsed.TotalSeconds;
        }
    }

    private async Task Execute(string input)
    {
        var line = input.Trim();
        if (line.Length == 0 && replay is null) return;
        string name, argument;
        if (line.StartsWith('{'))
        {
            using var document = JsonDocument.Parse(line);
            name = document.RootElement.GetProperty("command").GetString() ?? "";
            argument = document.RootElement.TryGetProperty("argument", out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText() : "";
        }
        else
        {
            var split = line.IndexOf(' ');
            name = split < 0 ? line : line[..split];
            argument = split < 0 ? "" : line[(split + 1)..].Trim();
        }
        switch (name.ToLowerInvariant())
        {
            case "help": Emit("help", Help); return;
            case "quit": case "exit": quit = true; return;
            case "status": Emit("status", CurrentStatus()); return;
            case "history":
                terminalHistory = ParseHistory(argument);
                terminalPage = 0;
                Emit("status", CurrentStatus()); return;
            case "actions":
                switch (argument.ToLowerInvariant())
                {
                    case "next": case "+": actionPage = Math.Max(0, actionPage) + 1; expandActions = false; break;
                    case "prev": case "-": actionPage = Math.Max(0, actionPage - 1); expandActions = false; break;
                    case "all": expandActions = true; break;
                    case "compact": case "": expandActions = false; actionPage = -1; break;
                    default: throw new ArgumentException("actions next / prev / all / compact");
                }
                Emit("status", CurrentStatus()); return;
            case "scores":
                showScores = argument.ToLowerInvariant() switch
                {
                    "on" => true, "off" => false, "auto" or "" => null,
                    _ => throw new ArgumentException("scores on / off / auto")
                };
                Emit("status", CurrentStatus()); return;
            case "capture":
                if (!tui || argument.Length == 0) throw new ArgumentException("capture <new.svg>");
                if (argument.StartsWith("all ", StringComparison.OrdinalIgnoreCase))
                {
                    Emit("capture_summary", new { source = session is null ? "recorded-replay-svg-export" : "current-display-svg-export",
                        captures = CaptureCurrentPages(argument[4..].Trim()) });
                    return;
                }
                exportFramePath = argument;
                Emit("status", CurrentStatus()); return;
            case "zoom":
                terminalZoom = argument.ToLowerInvariant() switch
                {
                    "fit" or "" => "fit", "in" or "detail" => "detail", "out" or "compact" => "compact",
                    _ => throw new ArgumentException("zoom fit / in / out")
                };
                Emit("status", CurrentStatus()); return;
            case "page":
                terminalPage = argument.ToLowerInvariant() switch
                {
                    "next" or "+" => Math.Max(0, terminalPage) + 1,
                    "prev" or "-" => Math.Max(0, terminalPage - 1),
                    _ when int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page > 0 => page - 1,
                    _ => throw new ArgumentException("page <N|next|prev>")
                };
                Emit("status", CurrentStatus()); return;
            case "map":
                SetMap(argument.Length == 0 ? "both" : argument);
                Emit("map", CurrentPreview(mapView != "subjective")); return;
            case "view":
                if (argument.Length == 0) { showActors = true; Emit("views", CurrentActors()); return; }
                showActors = false; SetView(argument); Emit("status", CurrentStatus()); return;
            case "views": showActors = true; Emit("views", CurrentActors()); return;
        }
        if (replay is { } player)
        {
            switch (name.ToLowerInvariant())
            {
                case "": case "step": case "next": player.Pause(); ShowFrame(player.Step()); break;
                case "options": Emit("options", VisibleOptions()); break;
                case "perf": case "performance":
                    if (argument.Length > 0) throw new ArgumentException("录制回放仅查看当前帧已有的统计，不写入会话性能报告。");
                    Emit("performance", HostValues.Map(CurrentPreview(false), "performance")); break;
                case "pause": player.Pause(); Emit("replay", ReplayStatus()); break;
                case "play":
                    player.Play(argument.Length == 0 ? player.Speed : double.Parse(argument, CultureInfo.InvariantCulture));
                    if (oneCommand) await PlayReplay(); else Emit("replay", ReplayStatus());
                    break;
                case "seek": SeekReplay(argument); break;
                case "replay": ReplayOperation(argument); break;
                case "restart":
                    ResetReplay();
                    Emit("replay", ReplayStatus()); break;
                default: throw new ArgumentException("status / options / map / view / perf / play / pause / step / seek / replay / restart / quit");
            }
            return;
        }
        showActors = false;
        var current = session ?? throw new InvalidOperationException("会话 —");
        switch (name.ToLowerInvariant())
        {
            case "options": Emit("options", VisibleOptions()); break;
            case "next": AdvanceOne(current); break;
            case "advance": AdvanceBatch(current, argument); break;
            case "focus": case "select": FocusChoice(current, argument); break;
            case "confirm": ConfirmChoice(current); break;
            case "cancel":
                if (focus is { } cancelled) RecordPhase(current, "cancel", cancelled);
                current.EndDecisionInterface(current.Viewer, "cancelled");
                focus = null; interactionPhase = ""; Emit("status", CurrentStatus()); break;
            case "decision-phase":
                if (argument is not ("request" or "response" or "reading" or "confirmation"))
                    throw new ArgumentException("decision-phase request / response / reading / confirmation");
                current.SetDecisionInterfacePhase(current.Viewer, argument);
                Emit("performance", current.PerformanceSummary()); break;
            case "act":
                FocusChoice(current, argument);
                ConfirmChoice(current); break;
            case "inspect": Emit("inspect", current.Inspect(argument.Length == 0 ? Observer : argument)); break;
            case "perf": case "performance":
                if (argument.Length > 0) current.WritePerformanceReport(argument);
                Emit("performance", current.PerformanceSnapshot()); break;
            case "tp": case "time":
                focus = null; interactionPhase = "";
                Emit("development", current.ExecuteDebug(name.ToLowerInvariant(), argument)); break;
            case "save":
                if (argument.Length == 0) throw new ArgumentException("save <new.dvsave>");
                current.WriteSave(argument); Emit("saved", argument); break;
            case "trace":
                if (argument.Length == 0) throw new ArgumentException("trace <new.dvdag>");
                current.WriteTrace(argument); Emit("trace", argument); break;
            default: throw new ArgumentException("未知命令");
        }
    }

    private static string ParseMap(string value) => value.ToLowerInvariant() switch
    {
        "subjective" or "subject" => "subjective", "objective" or "world" => "objective", "both" => "both",
        _ => throw new ArgumentException("map subjective / objective / both")
    };

    private void SetMap(string value)
    {
        var next = ParseMap(value);
        if (next == "subjective" && mapView != "subjective") ClearTranscript();
        mapView = next;
        terminalPage = 0;
    }

    private void ChangeDisplayViewer(string actor)
    {
        if (displayViewer != actor)
        {
            ClearTranscript();
            roomOrder.Clear();
            orderedViewer = "";
            terminalPage = 0;
            actionPage = -1;
        }
        displayViewer = actor;
    }

    private string Observer => displayViewer.Length > 0 ? displayViewer : session?.Viewer ?? HostValues.Text(replaySnapshot, "viewer");

    private static string ParseHistory(string value) => value.ToLowerInvariant() switch
    {
        "latest" or "" => "latest", "recent" => "recent",
        _ => throw new ArgumentException("history latest / recent")
    };

    private void AdvanceBatch(DvSession current, string argument)
    {
        if (!jsonLines || !ControllerIsAutonomous(current))
            throw new InvalidOperationException("advance requires --jsonl --controller ai");
        if (!int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var budget) || budget is < 1 or > 1000)
            throw new ArgumentException("advance <1..1000 real milliseconds>");
        var clock = Stopwatch.StartNew();
        batchAdvance = true;
        try
        {
            do
            {
                if (!current.Active) break;
                var version = current.Version;
                var wasFocused = focus is not null;
                AdvanceOne(current);
                // No second schedule, invented input, or clock assignment: this
                // is the same focus/confirm chain, with its UI transport batched.
                if (current.Version == version && (focus is not null) == wasFocused) break;
            }
            while (clock.ElapsedMilliseconds < budget && !quit);
        }
        finally { batchAdvance = false; }
        Emit("status", CurrentStatus());
    }

    private void AdvanceOne(DvSession current)
    {
        cancellation.Token.ThrowIfCancellationRequested();
        if (focus is not null) { ConfirmChoice(current); return; }
        var intent = current.Intent;
        if (HostValues.Text(intent, "status") != "prepared") intent = current.PrepareValue();
        if (HostValues.Text(intent, "status") == "prepared") FocusPrepared(current);
        else
        {
            if (followReplayActor && HostValues.Text(intent, "status") == "awaiting_input") ChangeDisplayViewer(current.Viewer);
            interactionPhase = ""; EmitIntent(intent);
        }
    }

    private void FocusChoice(DvSession current, string argument)
    {
        if (ControllerIsAutonomous(current)) throw new InvalidOperationException("控制 · 自主");
        if (Observer != current.Viewer) throw new InvalidOperationException("控制 · " + current.Viewer);
        if (argument.Length == 0) throw new ArgumentException("focus <01|ID|selection>");
        if (current.Active && HostValues.Text(current.Intent, "status") == "idle") current.PrepareValue();
        if (!current.Active || HostValues.Text(current.Intent, "status") != "awaiting_input")
            throw new InvalidOperationException("阶段 · " + HostValues.Text(current.Intent, "status"));
        var options = current.ObserverOptions(current.Viewer, showScores == true);
        var selection = argument;
        if (int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            if (number < 1 || number > options.Count) throw new ArgumentOutOfRangeException(nameof(argument), "选项编号");
            selection = HostValues.Text(options[number - 1], "id");
        }
        var id = SelectionId(selection);
        if (!options.Any(option => HostValues.Text(option, "id") == id)) throw new ArgumentException("选项已失效");
        if (focus is { } previous) RecordPhase(current, "cancel", previous);
        focus = NewFocus(current, id, selection, current.Viewer, options);
        current.SetDecisionInterfacePhase(current.Viewer, "confirmation");
        actionPage = -1;
        optionTimeBasis = "current";
        terminalPage = -1;
        interactionPhase = "聚焦";
        RecordPhase(current, "focus", focus);
        Emit("interaction", FocusPayload(focus, "focus"));
    }

    private void FocusPrepared(DvSession current)
    {
        var intent = current.Intent;
        var actor = HostValues.Text(intent, "actor");
        if (actor.Length == 0) actor = HostValues.Text(intent, "id", current.Viewer);
        var action = HostValues.Map(intent, "action");
        var id = HostValues.Text(action, "id");
        if (id.Length == 0) id = HostValues.Text(intent, "action_id");
        if (id.Length == 0) throw new InvalidOperationException("行动标识 —");
        if (followReplayActor) ChangeDisplayViewer(actor);
        var options = actor == Observer
            ? current.ObserverOptions(actor, showScores == true)
            : Array.Empty<IReadOnlyDictionary<string, object?>>();
        focus = NewFocus(current, id, id, actor, options, HostValues.Text(action, "label"));
        actionPage = -1;
        optionTimeBasis = "current";
        terminalPage = -1;
        interactionPhase = "聚焦";
        RecordPhase(current, "focus", focus);
        Emit("interaction", FocusPayload(focus, "focus"));
    }

    private ChoiceFocus NewFocus(DvSession current, string id, string selection, string actor,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> options, string label = "") =>
        new(id, selection, actor, label.Length > 0 ? label : options.Where(option => HostValues.Text(option, "id") == id)
            .Select(option => HostValues.Text(option, "label")).FirstOrDefault() ?? "", current.Version,
            "cli-" + interactionSession + "-" + (++interactionSequence).ToString(CultureInfo.InvariantCulture),
            options, current.Preview(Observer, mapView != "subjective", showScores == true));

    private void ConfirmChoice(DvSession current)
    {
        cancellation.Token.ThrowIfCancellationRequested();
        var selected = focus ?? throw new InvalidOperationException("选中行动 —");
        // A fixed inspection view must not prevent completion of an NPC intent
        // already selected by Core. Player choices still require their own view.
        if (Observer != selected.Actor && HostValues.Text(current.Intent, "status") != "prepared")
            throw new InvalidOperationException("行动视角 · " + selected.Actor);
        if (selected.Version != current.Version) { focus = null; throw new InvalidOperationException("选项已失效"); }
        interactionPhase = "确认";
        terminalPage = -1;
        // Record the same selection before execution; Complete remains the sole effect boundary.
        RecordPhase(current, "confirm", selected);
        Emit("interaction", FocusPayload(selected, "confirm"));
        focus = null;
        var submitted = HostValues.Text(current.Intent, "status") == "awaiting_input";
        if (submitted) current.PrepareValue(selected.Selection);
        var prepared = current.Intent;
        var matches = HostValues.Text(prepared, "status") == "prepared" &&
            HostValues.Text(prepared, "actor") == selected.Actor &&
            HostValues.Text(HostValues.Map(prepared, "action"), "id") == selected.Id;
        if (submitted && !matches && current.IsScheduled(selected.Actor, selected.Id))
        {
            // A choice can enter the schedule before another actor's earlier receipt is due.
            // Preserve that boundary; the next focus/confirm handles the actual pending action.
            RecordPhase(current, "result", selected, HostValues.Freeze(new Dictionary<string, object?>
            { ["visible"] = false, ["outcome"] = "scheduled" }));
            interactionPhase = "已排程";
            Emit("scheduled", new { interaction_id = selected.InteractionId, actor = selected.Actor,
                action_id = selected.Id, state_version = current.Version });
            Emit("status", CurrentStatus()); return;
        }
        if (HostValues.Text(prepared, "status") != "prepared" || submitted && !matches)
        {
            RecordPhase(current, "result", selected, HostValues.Freeze(new Dictionary<string, object?>
            { ["visible"] = false, ["outcome"] = "not_prepared", ["status"] = HostValues.Text(current.Intent, "status") }));
            interactionPhase = "未执行"; EmitIntent(current.Intent); Emit("status", CurrentStatus()); return;
        }
        if (!matches)
        {
            RecordPhase(current, "result", selected, HostValues.Freeze(new Dictionary<string, object?>
            { ["visible"] = false, ["outcome"] = "selection_changed" }));
            interactionPhase = "未执行";
            throw new InvalidOperationException("行动已变更");
        }
        var presentation = current.CompleteFormal(Observer);
        RecordPhase(current, "result", selected, presentation);
        var outcome = HostValues.Text(presentation, "outcome", HostValues.Text(presentation, "status"));
        interactionPhase = outcome == "rejected"
            ? HostValues.Text(presentation, "failure_class") == "activity-interrupted" ? "等待被打断" : "被拒绝"
            : "已执行";
        terminalPage = 0;
        Emit("presentation", presentation);
        if (!batchAdvance) Emit("status", CurrentStatus());
    }

    private static string SelectionId(string selection)
    {
        if (!selection.StartsWith('{')) return selection;
        using var document = JsonDocument.Parse(selection);
        return document.RootElement.GetProperty("action_id").GetString() ?? "";
    }

    private static IReadOnlyDictionary<string, object?> FocusPayload(ChoiceFocus selected, string phase) =>
        HostValues.Freeze(new Dictionary<string, object?>
        {
            ["phase"] = phase, ["interaction_id"] = selected.InteractionId, ["actor"] = selected.Actor,
            ["action_id"] = selected.Id, ["label"] = selected.Label,
            ["selection"] = selected.Selection, ["state_version"] = selected.Version,
            ["options"] = selected.Options
        });

    private void RecordPhase(DvSession current, string phase, ChoiceFocus selected,
        IReadOnlyDictionary<string, object?>? presentation = null)
    {
        var projected = phase == "result" ? current.Preview(Observer, mapView != "subjective", showScores == true) : selected.Preview;
        var performance = current.PerformanceSummary();
        var measuredPreview = DvSession.WithPerformance(projected, performance);
        var record = new Dictionary<string, object?>(FocusPayload(selected, phase), StringComparer.Ordinal)
        {
            ["kind"] = "cli_interaction", ["viewer"] = Observer,
            ["real_us"] = (long)(recordingClock.Elapsed.TotalMilliseconds * 1000),
            ["input_mode"] = jsonLines ? "jsonl" : tui ? "tui" : "text",
            ["controller"] = ControllerIsAutonomous(current) ? "ai" : "player",
            ["controller_kind"] = PerformanceControllerKind(current, performance),
            ["performance"] = performance,
            ["measured_until_ticks"] = performance.GetValueOrDefault("measured_until_ticks"),
            ["source_state_version"] = selected.Version, ["state_version"] = current.Version,
            ["selection_state_version"] = selected.Version,
            ["selection_game_time"] = HostValues.Map(selected.Preview, "snapshot").GetValueOrDefault("game_time"),
            ["result_game_time"] = phase == "result" ? HostValues.Map(projected, "snapshot").GetValueOrDefault("game_time") : null,
            ["result_state_version"] = phase == "result" ? current.Version : null,
            ["option_time_basis"] = phase == "result" ? "decision-boundary" : "current",
            ["preview"] = measuredPreview,
            ["presentation"] = presentation ?? HostValues.Empty
        };
        if (phase == "result") record["presentation_viewer"] = Observer;
        current.RecordInteraction(HostValues.Freeze(record));
    }

    private object CurrentStatus() => session is { } current ? current.TextStatus(Observer) : ReplayStatus();

    private IReadOnlyDictionary<string, object?> CurrentPreview(bool objective)
    {
        var source = session is { } current ? current.Preview(Observer, objective, showScores == true) : replayPreview;
        var observers = HostValues.Map(source, "observers");
        var observation = observers.GetValueOrDefault(Observer) as IReadOnlyDictionary<string, object?>;
        var sourceViewer = HostValues.Text(source, "viewer", HostValues.Text(HostValues.Map(source, "snapshot"), "viewer"));
        if (observation is null && sourceViewer == Observer) observation = source;
        var snapshot = observation is null ? ReplayObserverSnapshot(source) : HostValues.Map(observation, "snapshot");
        if (session is null && replayGameTime is not null)
            snapshot = HostValues.Freeze(new Dictionary<string, object?>(snapshot, StringComparer.Ordinal)
                { ["game_time"] = replayGameTime });
        var value = new Dictionary<string, object?>
        {
            ["viewer"] = Observer, ["actors"] = session is null ? RecordedActors() : HostValues.List(source, "actors"),
            ["snapshot"] = DisplaySnapshot(snapshot),
            ["status"] = observation is null ? HostValues.Empty : HostValues.Map(observation, "status"),
            ["subjective"] = observation is null ? HostValues.Empty : HostValues.Map(observation, "subjective"),
            ["performance"] = session?.PerformanceSummary() ?? RecordedPerformance(source)
        };
        if (objective) value["objective"] = HostValues.Map(source, "objective");
        return HostValues.Freeze(value);
    }

    private static IReadOnlyDictionary<string, object?> DisplaySnapshot(IReadOnlyDictionary<string, object?> snapshot)
    {
        var keys = new[] { "viewer", "viewer_title", "game_time", "chapter", "map", "room", "location_title", "view_kind", "player_control", "people", "map_knowledge", "actors" };
        return HostValues.Freeze(snapshot.Where(pair => keys.Contains(pair.Key, StringComparer.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    private object CurrentActors() => session is { } current ? current.Actors : RecordedActors();

    private IReadOnlyList<object?> RecordedActors()
    {
        var actors = HostValues.List(replayPreview, "actors");
        if (actors.Count > 0) return actors;
        actors = HostValues.List(HostValues.Map(replayPreview, "snapshot"), "actors");
        return actors.Count > 0 ? actors : HostValues.List(replaySnapshot, "actors");
    }

    private void SetView(string actor)
    {
        if (actor is "follow" or "auto")
        {
            followReplayActor = true;
            if (session is { } live) ChangeDisplayViewer(focus?.Actor ?? live.Viewer);
            else if (replayChoiceActor.Length > 0) ChangeDisplayViewer(replayChoiceActor);
            return;
        }
        if (session is { } current)
        {
            // Validation and projection are read-only; the controller remains unchanged.
            _ = current.Preview(actor, false, showScores == true);
        }
        else if (!RecordedActors().OfType<IReadOnlyDictionary<string, object?>>()
                     .Any(item => HostValues.Text(item, "id") == actor))
            throw new ArgumentException("视角未记录");
        ChangeDisplayViewer(actor);
        followReplayActor = false;
        roomOrder.Clear();
        ClearTranscript();
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object?>> VisibleOptions()
    {
        if (session is { } current) return current.ObserverOptions(Observer, showScores == true);
        if (interactionPhase.StartsWith("本次记录停止", StringComparison.Ordinal) || interactionPhase == "会话已停止")
            return Array.Empty<IReadOnlyDictionary<string, object?>>();
        if (replayChoiceActor == Observer) return replayOptions;
        var observers = HostValues.Map(replayPreview, "observers");
        if (observers.GetValueOrDefault(Observer) is IReadOnlyDictionary<string, object?> observation)
            return HostValues.List(observation, "options").OfType<IReadOnlyDictionary<string, object?>>().ToArray();
        return Array.Empty<IReadOnlyDictionary<string, object?>>();
    }

    private static bool ControllerIsAutonomous(DvSession current) => !current.PlayerControlled;

    private static string NormalizeControllerKind(string value) => value.ToLowerInvariant() switch
    {
        "player" or "human" => "human", "llm" => "llm", "ai" or "engine" => "engine", _ => ""
    };

    private static string PerformanceControllerKind(DvSession current, IReadOnlyDictionary<string, object?>? performance = null)
    {
        var kind = NormalizeControllerKind(HostValues.Text(performance ?? current.PerformanceSummary(), "controller_kind"));
        return kind.Length > 0 ? kind : ControllerIsAutonomous(current) ? "engine" : "human";
    }

    private static string ControllerLabel(string kind) => kind switch
    {
        "human" => "人类", "llm" => "LLM", "engine" => "自主", _ => "—"
    };

    private IReadOnlyDictionary<string, object?> RecordedPerformance(IReadOnlyDictionary<string, object?> source) =>
        replayPerformance.Count > 0 ? replayPerformance : HostValues.Map(source, "performance");

    private void EmitIntent(IReadOnlyDictionary<string, object?> intent) => Emit("intent", new
    {
        status = HostValues.Text(intent, "status"),
        game_time = intent.GetValueOrDefault("game_time"),
        // The pending action carries executor-private law inputs. The text host
        // exposes neither those inputs nor any predicted observation to its viewer.
        awaiting_input = HostValues.Text(intent, "status") == "awaiting_input"
    });

    private object ReplayStatus()
    {
        var preview = CurrentPreview(false);
        return new
        {
            mode = "recorded-replay", index = replay?.Index, count = replay?.Count, speed = replay?.Speed,
            playing = replay?.Playing, viewer = Observer, phase = interactionPhase,
            controller_kind = replayControllerKind.Length > 0 ? replayControllerKind : null,
            performance = HostValues.Map(preview, "performance"),
            snapshot = HostValues.Map(preview, "snapshot"), options = VisibleOptions(), selected = replayChoiceActor == Observer ? replaySelected : ""
        };
    }

    private void ResetReplay()
    {
        (replay ?? throw new InvalidOperationException("回放 —")).Restart();
        replaySnapshot = HostValues.Empty;
        replayPreview = HostValues.Empty;
        replayPerformance = HostValues.Empty;
        replayControllerKind = "";
        replayGameTime = null;
        replayOptions = Array.Empty<IReadOnlyDictionary<string, object?>>();
        replaySelected = replayChoiceActor = interactionPhase = "";
        terminalFrame = null;
        ClearTranscript();
        roomOrder.Clear();
        orderedViewer = "";
        terminalPage = 0;
        actionPage = -1;
        showActors = false;
    }

    private void SeekReplay(string argument, bool render = true)
    {
        var player = replay ?? throw new InvalidOperationException("seek 仅用于已记录回放");
        var target = argument.ToLowerInvariant() switch
        {
            "first" => 1, "last" => player.Count,
            _ when int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var number) => number,
            _ => throw new ArgumentException("seek <一基帧号|first|last>")
        };
        if (target < 1 || target > player.Count) throw new ArgumentOutOfRangeException(nameof(argument), $"回放帧号范围：1..{player.Count}");
        // Replay display state is reconstructed from the recorded prefix. This
        // never opens a session, obtains a missing observer, or renders skipped frames.
        ResetReplay();
        while (player.Index + 1 < target)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            ShowFrame(player.Step(), emit: false);
        }
        terminalPage = 0;
        if (render && player.Current is { } frame) Emit("frame", frame);
    }

    private void ReplayOperation(string argument)
    {
        var split = argument.IndexOf(' ');
        var operation = split < 0 ? argument : argument[..split];
        var value = split < 0 ? "" : argument[(split + 1)..].Trim();
        switch (operation.ToLowerInvariant())
        {
            case "frame": SeekReplay(value); break;
            case "marks":
                if (value.Length > 0) throw new ArgumentException("replay marks");
                var player = replay ?? throw new InvalidOperationException("回放 —");
                var marks = player.Frames.Select((frame, index) => (frame, index))
                    .Where(item => IsOwnCompletedReplayFrame(item.frame) ||
                        HostValues.Text(item.frame.Data, "checkpoint") is "initial" or "final")
                    .Select(item => new
                    {
                        frame = item.index + 1, type = item.frame.Kind,
                        game_time = item.frame.Data.GetValueOrDefault("result_game_time") ?? item.frame.Data.GetValueOrDefault("game_time"),
                        observer = Observer, action_public_label = ReplayPublicLabel(item.frame)
                    }).ToArray();
                Emit("replay_index", new { viewer = Observer, frame_count = player.Count, numbering = "one-based", marks });
                break;
            case "capture": CaptureReplaySet(value); break;
            default: throw new ArgumentException("replay frame N / replay marks / replay capture NEW_DIRECTORY");
        }
    }

    private bool IsOwnCompletedReplayFrame(ReplayFrame frame)
    {
        if (frame.Kind != "confirm" || frame.Actor != Observer || !PresentationVisibleToObserver(frame)) return false;
        var phase = HostValues.Text(frame.Data, "phase", HostValues.Text(frame.Data, "action_state"));
        var outcome = HostValues.Text(HostValues.Map(frame.Data, "presentation"), "outcome");
        return phase is "completed" or "applied" || outcome == "applied";
    }

    private static bool HasReplayBody(ReplayFrame frame)
    {
        var presentation = HostValues.Map(frame.Data, "presentation");
        return !string.IsNullOrWhiteSpace(presentation.Count > 0 ? PresentationBody(presentation) : frame.Text);
    }

    private static string ReplayPublicLabel(ReplayFrame frame)
    {
        var label = HostValues.List(frame.Data, "options").OfType<IReadOnlyDictionary<string, object?>>()
            .Where(option => HostValues.Text(option, "id") == frame.Action)
            .Select(option => HostValues.Text(option, "label")).FirstOrDefault(value => value.Length > 0 && value != frame.Action);
        if (string.IsNullOrWhiteSpace(label)) label = HostValues.Text(HostValues.Map(frame.Data, "action"), "label");
        if (string.IsNullOrWhiteSpace(label)) label = HostValues.Text(frame.Data, "label");
        return !string.IsNullOrWhiteSpace(label) && label != frame.Action ? label : "（未记录标签）";
    }

    private void CaptureReplaySet(string directory)
    {
        var player = replay ?? throw new InvalidOperationException("回放 —");
        if (!tui || string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("--tui: replay capture NEW_DIRECTORY");
        if (followReplayActor) throw new ArgumentException("批量回放导出需要固定 --viewer ID 或 view ID");
        if (player.Count == 0) throw new InvalidOperationException("回放没有可导出的帧");
        directory = Path.GetFullPath(directory);
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("导出目录必须是新路径：" + directory);
        Directory.CreateDirectory(directory);
        var indexPath = Path.Combine(directory, "coverage.csv");
        var partial = indexPath + ".partial";
        var savedHistory = terminalHistory;
        var captured = new Dictionary<string, (int Frame, IReadOnlyList<CapturedPage> Pages)>(StringComparer.Ordinal);
        var exportClock = Stopwatch.StartNew();
        var frameCount = 0;
        var imageCount = 0;
        var reusedFrames = 0;
        var lastBody = "";
        static string Csv(object? value)
        {
            var text = value is null ? "" : value is string literal ? literal : JsonSerializer.Serialize(value, Json);
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }
        try
        {
            // Every recorded frame is visited once. Repeated offscreen work is
            // represented explicitly in the index; no live session is opened.
            terminalHistory = "latest";
            ResetReplay();
            using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write))
            using (var index = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                index.WriteLine("frame,sequence,kind,phase,game_time,viewer,actor,action_id,action_public_label,coverage,representative_frame,page,page_count,svg_path,semantic_sha256,new_visible_text,own_action,checkpoint");
                for (var position = 0; position < player.Count; position++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var frame = player.Step() ?? throw new InvalidOperationException("回放在记录末尾之前结束");
                    ShowFrame(frame, emit: false);
                    var ownAction = frame.Actor == Observer && frame.Kind is ("focus" or "confirm");
                    var checkpoint = HostValues.Text(frame.Data, "checkpoint");
                    var body = terminalEntries.Count > 0 ? terminalEntries[^1].GroupId : "";
                    var newBody = body != lastBody && body.Length > 0;
                    lastBody = body;
                    var model = CurrentModel();
                    var semantic = model with
                    {
                        Mode = "", Clock = "", Performance = HostValues.Empty,
                        PageIndex = 0, ActionPageIndex = -1, ExpandActions = true,
                        TranscriptNotice = ""
                    };
                    var key = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        model = semantic,
                        own_action = ownAction ? frame.Action : "",
                        checkpoint
                    }, Json)));
                    var reused = captured.TryGetValue(key, out var representative);
                    if (!reused)
                    {
                        var prefix = Path.Combine(directory, $"frame-{position + 1:000000}-{frame.Kind}");
                        var pages = CaptureCurrentPages(prefix, model);
                        representative = (position + 1, pages);
                        captured.Add(key, representative);
                        imageCount += pages.Count;
                    }
                    else reusedFrames++;
                    foreach (var page in representative.Pages)
                        index.WriteLine(string.Join(",", new object?[]
                        {
                            position + 1, frame.Sequence, frame.Kind, interactionPhase, replayGameTime,
                            Observer, frame.Actor, frame.Action, ReplayPublicLabel(frame),
                            reused ? "same-visible-content-clock-and-metrics-omitted" : "rendered",
                            representative.Frame, page.page, page.page_count,
                            Path.GetFileName(page.path), key, newBody, ownAction, checkpoint
                        }.Select(Csv)));
                    frameCount++;
                }
            }
            File.Move(partial, indexPath);
        }
        finally { terminalHistory = savedHistory; }
        Emit("capture_summary", new
        {
            source = "recorded-replay-svg-export", viewer = Observer, numbering = "one-based",
            frame_count = player.Count, indexed_frames = frameCount, rendered_states = captured.Count,
            rendered_pages = imageCount, reused_frames = reusedFrames, coverage_index = indexPath,
            transcript_window = "latest complete visible event; earlier events covered at their own frames",
            deduplication = "identical observer content; frame number, running clock and performance metrics remain in the index and original replay",
            export_elapsed_seconds = exportClock.Elapsed.TotalSeconds, opens_live_session = false
        });
    }

    private IReadOnlyList<CapturedPage> CaptureCurrentPages(string prefix, TerminalModel? sourceModel = null)
    {
        if (!tui || string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("capture all NEW_PREFIX");
        var savedPage = terminalPage;
        var savedActionPage = actionPage;
        var savedExpanded = expandActions;
        var captures = new List<CapturedPage>();
        try
        {
            expandActions = true;
            sourceModel ??= CurrentModel();
            var count = 1;
            for (var page = 0; page < count; page++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                terminalPage = page;
                var path = Path.GetFullPath(prefix + (page == 0 ? "" : $"-page-{page + 1:00}") + ".svg");
                exportFramePath = path;
                var rendered = RenderCurrent(writeConsole: false,
                    preparedModel: sourceModel with { PageIndex = page, ActionPageIndex = -1, ExpandActions = true });
                count = rendered.PageCount;
                captures.Add(new CapturedPage(path, rendered.PageIndex + 1, rendered.PageCount,
                    replay is null ? (int?)null : replay.Index + 1, replayGameTime));
            }
        }
        finally
        {
            exportFramePath = null;
            terminalPage = savedPage;
            actionPage = savedActionPage;
            expandActions = savedExpanded;
        }
        return captures.AsReadOnly();
    }

    private async Task PlayReplay()
    {
        var player = replay ?? throw new InvalidOperationException("回放 —");
        var elapsed = Stopwatch.StartNew();
        var remaining = 0d;
        var previous = elapsed.Elapsed.TotalSeconds;
        while (!quit)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            var now = elapsed.Elapsed.TotalSeconds;
            if (player.Playing) remaining -= (now - previous) * player.Speed;
            previous = now;
            if (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.Spacebar:
                        if (player.Playing) player.Pause(); else player.Play(player.Speed);
                        Emit("replay", ReplayStatus()); break;
                    case ConsoleKey.RightArrow:
                        player.Pause(); ShowFrame(player.Step()); remaining = 0; break;
                    case ConsoleKey.Escape: case ConsoleKey.Q: quit = true; player.Pause(); break;
                    case ConsoleKey.D1: player.Play(1); break;
                    case ConsoleKey.D2: player.Play(2); break;
                    case ConsoleKey.D4: player.Play(4); break;
                    case ConsoleKey.D8: player.Play(8); break;
                    case ConsoleKey.PageDown:
                        terminalPage = Math.Max(0, terminalPage) + 1; Emit("replay", ReplayStatus()); break;
                    case ConsoleKey.PageUp:
                        terminalPage = Math.Max(0, terminalPage - 1); Emit("replay", ReplayStatus()); break;
                    case ConsoleKey.Tab:
                        SetMap(mapView == "subjective" ? "both" : mapView == "both" ? "objective" : "subjective");
                        Emit("replay", ReplayStatus()); break;
                }
            }
            if (player.Playing && remaining <= 0)
            {
                var frame = player.Step();
                if (frame is null) { player.Pause(); break; }
                ShowFrame(frame);
                // Each visible beat receives its full display interval, including after a slow render.
                remaining = frame.DurationSeconds;
                previous = elapsed.Elapsed.TotalSeconds;
            }
            if (!player.Playing && (Console.IsInputRedirected || player.Index >= player.Count - 1)) break;
            await Task.Delay(15, cancellation.Token);
        }
    }

    private void ShowFrame(ReplayFrame? frame, bool emit = true)
    {
        if (frame is null) return;
        terminalFrame = frame;
        if (frame.Data.GetValueOrDefault("preview") is IReadOnlyDictionary<string, object?> preview)
            replayPreview = preview;
        else if (frame.Data.ContainsKey("snapshot"))
            replayPreview = HostValues.Empty;
        if (frame.Data.GetValueOrDefault("snapshot") is IReadOnlyDictionary<string, object?> snapshot)
            replaySnapshot = snapshot;
        var frameSnapshot = HostValues.Map(frame.Data, "snapshot");
        var framePreviewSnapshot = HostValues.Map(HostValues.Map(frame.Data, "preview"), "snapshot");
        var performance = HostValues.Map(frame.Data, "performance");
        if (performance.Count == 0) performance = HostValues.Map(HostValues.Map(frame.Data, "preview"), "performance");
        if (performance.Count == 0) performance = HostValues.Map(frameSnapshot, "performance");
        if (performance.Count > 0) replayPerformance = performance;
        var controllerKind = NormalizeControllerKind(HostValues.Text(frame.Data, "controller_kind"));
        if (controllerKind.Length == 0) controllerKind = NormalizeControllerKind(HostValues.Text(performance, "controller_kind"));
        if (controllerKind.Length == 0) controllerKind = NormalizeControllerKind(HostValues.Text(frame.Data, "controller"));
        if (controllerKind.Length > 0) replayControllerKind = controllerKind;
        var recordedTime = frame.Data.GetValueOrDefault("game_time") ??
            framePreviewSnapshot.GetValueOrDefault("game_time") ?? frameSnapshot.GetValueOrDefault("game_time");
        if (recordedTime is not null) replayGameTime = recordedTime;
        if (frame.Kind is "focus" or "confirm" or "cancel")
        {
            replayChoiceActor = frame.Actor;
            replayOptions = Array.Empty<IReadOnlyDictionary<string, object?>>();
        }
        if (followReplayActor && frame.Actor.Length > 0 && frame.Kind is ("focus" or "confirm")) ChangeDisplayViewer(frame.Actor);
        if (frame.Data.ContainsKey("options"))
            replayOptions = HostValues.List(frame.Data, "options").OfType<IReadOnlyDictionary<string, object?>>().ToArray();
        optionTimeBasis = HostValues.Text(frame.Data, "option_time_basis", "current");
        if (frame.Kind == "cancel") { replaySelected = ""; interactionPhase = "取消"; }
        if (frame.Kind is "focus" or "confirm")
        {
            terminalPage = -1;
            actionPage = -1;
            replaySelected = frame.Action;
            interactionPhase = frame.Kind == "focus" ? "聚焦" :
                HostValues.Text(HostValues.Map(frame.Data, "presentation"), "outcome") == "scheduled" ? "已排程" : "确认";
        }
        var recordedPhase = HostValues.Text(frame.Data, "phase", HostValues.Text(frame.Data, "action_state"));
        if (recordedPhase.Length > 0)
            interactionPhase = recordedPhase switch
            {
                "focus" => "聚焦", "confirm" or "awaiting-result" => "待结果", "completed" or "applied" => "已执行",
                "scheduled" => "已排程", "rejected" => "被拒绝", "cancel" or "cancelled" => "取消",
                "closed" => "本次记录停止", "stopped" => "会话已停止",
                "not_prepared" or "selection_changed" => "未执行", _ => recordedPhase
            };
        if (recordedPhase is "completed" or "applied" or "rejected") terminalPage = 0;
        if (recordedPhase == "rejected" && frame.Actor == Observer && PresentationVisibleToObserver(frame) &&
            HostValues.Text(HostValues.Map(frame.Data, "presentation"), "failure_class") == "activity-interrupted")
            interactionPhase = "等待被打断";
        if (recordedPhase == "closed")
        {
            replaySelected = "";
            replayChoiceActor = "";
            replayOptions = Array.Empty<IReadOnlyDictionary<string, object?>>();
            optionTimeBasis = "current";
            var pending = HostValues.List(frame.Data, "pending_actions").Count;
            if (pending > 0) interactionPhase += " · 尚有在途行动 " + pending.ToString(CultureInfo.InvariantCulture);
        }
        if (emit) Emit("frame", frame);
        else AddTranscript("frame", frame);
    }

    private void Emit(string kind, object data)
    {
        // The complete interaction is recorded by RecordPhase. Autonomous
        // batches avoid re-projecting private NPC turns for the stdout pipe.
        if (batchAdvance) return;
        if (jsonLines)
        {
            object visible = data is ReplayFrame frame ? DisplayReplayFrame(frame) : data;
            if (kind == "interaction" && data is IReadOnlyDictionary<string, object?> interaction &&
                HostValues.Text(interaction, "actor") != Observer)
                visible = new { phase = HostValues.Text(interaction, "phase"), viewer = Observer,
                    options = VisibleOptions(), selected = "", state_version = session?.Version };
            Console.WriteLine(JsonSerializer.Serialize(new { kind, scope = session is null ? "recorded-replay" : "formal-model", data = visible }, Json));
            MarkDecisionInterfaceVisible(kind);
            return;
        }
        if (kind is "presentation" or "frame") AddTranscript(kind, data);
        if (kind is "performance" or "replay_index" or "capture_summary" || (!tui && kind is not ("status" or "options" or "map" or "views" or "interaction" or "frame" or "presentation" or "intent" or "replay")))
        {
            Console.WriteLine(Format(kind, data)); return;
        }
        RenderCurrent();
        MarkDecisionInterfaceVisible(kind);
    }

    private void MarkDecisionInterfaceVisible(string kind)
    {
        if (session is not { } current || !current.PlayerControlled || Observer != current.Viewer) return;
        var status = HostValues.Text(current.Intent, "status");
        if (status is not ("awaiting_input" or "idle")) return;
        if (jsonLines && kind is not ("options" or "intent")) return;
        current.BeginDecisionInterface(current.Viewer, PerformanceControllerKind(current) == "llm" ? "request" : "reading");
    }

    private ReplayFrame DisplayReplayFrame(ReplayFrame frame)
    {
        var values = new Dictionary<string, object?>
        {
            ["contract"] = SmartReplay.Contract, ["viewer"] = Observer, ["phase"] = interactionPhase,
            ["options"] = VisibleOptions(), ["selected"] = replayChoiceActor == Observer ? replaySelected : "",
            ["preview"] = CurrentPreview(mapView != "subjective")
        };
        foreach (var key in new[] { "interaction_contract", "interaction_id", "state_version", "source_state_version", "record_index", "result_record_indices", "input_record_index", "controller", "controller_kind", "performance", "input_mode", "selection_recorded", "result_kind", "option_time_basis", "selection_game_time", "selection_state_version", "result_game_time", "result_state_version", "action_state", "provenance", "cut", "lifecycle", "pending_actions", "measured_until_ticks" })
            if ((key != "pending_actions" || mapView != "subjective") && frame.Data.TryGetValue(key, out var value)) values[key] = value;
        var outcome = HostValues.Text(HostValues.Map(frame.Data, "presentation"), "outcome");
        if (outcome.Length > 0) values["outcome"] = outcome;
        var visible = PresentationVisibleToObserver(frame);
        var text = "";
        if (visible && frame.Kind is ("focus" or "confirm" or "presentation" or "visible" or "dialogue" or "investigate" or "rejected" or "move"))
        {
            var presentation = HostValues.Map(frame.Data, "presentation");
            text = presentation.Count > 0 ? PresentationBody(presentation) : frame.Text;
        }
        var privateActor = mapView == "subjective" && frame.Actor != Observer;
        return frame with { Actor = privateActor ? "" : frame.Actor,
            Action = privateActor ? "" : frame.Action, Text = text, Data = HostValues.Freeze(values) };
    }

    private bool PresentationVisibleToObserver(ReplayFrame frame)
    {
        var presentation = HostValues.Map(frame.Data, "presentation");
        if (presentation.GetValueOrDefault("visible") is false) return false;
        var viewer = HostValues.Text(frame.Data, "presentation_viewer");
        if (presentation.Count > 0 && viewer.Length > 0) return viewer == Observer;
        return mapView != "subjective" || frame.Actor.Length == 0 || frame.Actor == Observer;
    }

    private void ClearTranscript()
    {
        terminalEntries.Clear();
        transcriptTruncated = false;
    }

    private void AppendTranscript(string id, object? time, string speaker, string text, string kind, string groupId = "", string voice = "")
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (id.Length > 0 && terminalEntries.Any(entry => entry.Id == id)) return;
        terminalEntries.Add(new TerminalEntry
        {
            Id = id, Clock = time is null ? "" : TerminalProjection.GameClock(time, true),
            Speaker = speaker, Voice = voice, Title = "", Text = text, Kind = kind, GroupId = groupId.Length > 0 ? groupId : id
        });
        while (terminalEntries.Count > TranscriptCapacity && terminalEntries[0].GroupId != terminalEntries[^1].GroupId)
        {
            transcriptTruncated = true;
            var oldest = terminalEntries[0].GroupId;
            do { terminalEntries.RemoveAt(0); }
            while (terminalEntries.Count > 0 && oldest.Length > 0 && terminalEntries[0].GroupId == oldest);
        }
    }

    private void AddTranscript(string kind, object data)
    {
        IReadOnlyDictionary<string, object?> presentation = HostValues.Empty;
        object? time = null;
        var actor = "";
        var id = "";
        var ownRejected = false;
        var interrupted = false;
        if (data is ReplayFrame recorded)
        {
            if (recorded.Kind is not ("presentation" or "visible" or "dialogue" or "investigate" or "rejected" or "move" or "confirm")) return;
            presentation = HostValues.Map(recorded.Data, "presentation");
            actor = recorded.Actor;
            time = recorded.Data.GetValueOrDefault("result_game_time") ?? recorded.Data.GetValueOrDefault("game_time") ?? replayGameTime;
            id = HostValues.Text(presentation, "action_event", HostValues.Text(recorded.Data, "interaction_id",
                "replay-" + recorded.Sequence.ToString(CultureInfo.InvariantCulture)));
            ownRejected = actor == Observer && (recorded.Kind == "rejected" ||
                HostValues.Text(recorded.Data, "action_state", HostValues.Text(recorded.Data, "phase")) == "rejected" ||
                HostValues.Text(presentation, "outcome", HostValues.Text(presentation, "status")) == "rejected");
            if (!PresentationVisibleToObserver(recorded))
            {
                // A generic receipt for one's own refusal reveals no hidden
                // cause. Never inspect or display another actor's private result.
                if (ownRejected) AppendTranscript(id + "-feedback", time, "本人",
                    "本次行动未执行：现场条件尚未满足。", "error", id, "execution-feedback");
                return;
            }
            if (presentation.Count == 0)
            {
                AppendTranscript(id, time, actor == Observer ? "本人" : "未辨明身份",
                    ownRejected ? "本次行动未执行：现场条件尚未满足。" : recorded.Text,
                    ownRejected ? "error" : recorded.Kind, voice: ownRejected ? "execution-feedback" : "");
                return;
            }
        }
        else if (kind == "presentation" && data is IReadOnlyDictionary<string, object?> shown)
        {
            presentation = shown;
            actor = HostValues.Text(shown, "completed_actor", HostValues.Text(shown, "actor"));
            time = shown.GetValueOrDefault("game_time") ?? session?.TextStatus(Observer).GetValueOrDefault("game_time");
            id = HostValues.Text(shown, "action_event");
            ownRejected = actor == Observer && HostValues.Text(shown, "outcome", HostValues.Text(shown, "status")) == "rejected";
        }
        if (presentation.Count == 0) return;
        if (presentation.GetValueOrDefault("visible") is false)
        {
            if (ownRejected) AppendTranscript(id + "-feedback", time, "本人",
                "本次行动未执行：现场条件尚未满足。", "error", id, "execution-feedback");
            return;
        }
        interrupted = ownRejected && HostValues.Text(presentation, "failure_class") == "activity-interrupted";
        var speaker = HostValues.Text(presentation, "speaker_name");
        if (speaker.Length == 0) speaker = actor == Observer ? "本人" : "未辨明身份";
        var blocks = PresentationBlocks(presentation, speaker);
        for (var index = 0; index < blocks.Count; index++)
            AppendTranscript(id + "-block-" + index.ToString(CultureInfo.InvariantCulture), time,
                blocks[index].Speaker, blocks[index].Text, ownRejected && !interrupted ? "error" : kind, id, blocks[index].Voice);
        if (blocks.Count == 0)
        {
            if (ownRejected) AppendTranscript(id + "-feedback", time, speaker,
                interrupted ? "等待被新的动静打断了。" : "本次行动未执行：现场条件尚未满足。",
                interrupted ? kind : "error", id, "execution-feedback");
        }
    }

    private static IReadOnlyList<(string Speaker, string Voice, string Text)> PresentationBlocks(
        IReadOnlyDictionary<string, object?> presentation, string speaker)
    {
        var voice = HostValues.Text(presentation, "voice");
        var lines = HostValues.List(presentation, "lines");
        var body = string.Join("\n", lines.Select(line => line is string text ? text :
            line is IReadOnlyDictionary<string, object?> map ? HostValues.Text(map, "text") : "")).Trim();
        var surface = HostValues.Text(presentation, "surface").Trim();
        var blocks = new List<(string Speaker, string Voice, string Text)>();
        void AddBlock(string name, string recordedVoice, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (blocks.Count > 0 && blocks[^1].Speaker == name && blocks[^1].Voice == recordedVoice)
                blocks[^1] = (name, recordedVoice, blocks[^1].Text + "\n" + text);
            else blocks.Add((name, recordedVoice, text));
        }
        if (surface.Length > 0 && surface != body)
            AddBlock(speaker, HostValues.Text(presentation, "surface_voice", voice), surface);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var text = line is string literal ? literal : line is IReadOnlyDictionary<string, object?> map ? HostValues.Text(map, "text") : "";
            var name = line is IReadOnlyDictionary<string, object?> named ? HostValues.Text(named, "speaker_name", speaker) : speaker;
            var lineVoice = line is IReadOnlyDictionary<string, object?> voiced ? HostValues.Text(voiced, "voice", voice) : voice;
            AddBlock(name, lineVoice, text);
        }
        return blocks;
    }

    private TerminalModel CurrentModel()
    {
        var preview = CurrentPreview(mapView != "subjective");
        var snapshot = HostValues.Map(preview, "snapshot");
        var status = session?.TextStatus(Observer) ?? HostValues.Map(preview, "status");
        var choices = VisibleOptions();
        var performance = session?.PerformanceSummary() ?? HostValues.Map(preview, "performance");
        var controllerKind = session is { } live ? PerformanceControllerKind(live, performance) : replayControllerKind;
        if (controllerKind.Length == 0) controllerKind = NormalizeControllerKind(HostValues.Text(performance, "controller_kind"));
        var mode = session is null
            ? $"回放 {replay?.Index + 1}/{replay?.Count} · {replay?.Speed}× · {(replay?.Playing == true ? "播放" : "暂停")}" + (controllerKind.Length > 0 ? " · " + ControllerLabel(controllerKind) : "")
            : ControllerLabel(controllerKind);
        var phase = session is null
            ? interactionPhase.Length > 0 ? "帧内：" + interactionPhase : ""
            : interactionPhase.Length > 0 ? interactionPhase : StateLabel(HostValues.Text(session.Intent, "status"));
        var shownEntries = terminalHistory == "latest" && terminalEntries.Count > 0
            ? terminalEntries.Where(entry => entry.GroupId == terminalEntries[^1].GroupId).ToArray()
            : terminalEntries.ToArray();
        var model = TerminalProjection.FromSnapshot(snapshot, status, choices, shownEntries, mode,
            phase, "actions next/prev/all · page next/prev · map · perf · quit", session is null ? replayChoiceActor == Observer ? replaySelected : "" : focus?.Actor == Observer ? focus.Id : "", preview, mapView) with
            { Zoom = terminalZoom, ShowActors = showActors, ShowScores = showScores,
              Performance = performance,
              TranscriptNotice = terminalHistory == "recent" && transcriptTruncated && terminalEntries.Count > 0
                  ? "近期正文自 " + (string.IsNullOrWhiteSpace(terminalEntries[0].Clock) ? "时间未记录" : terminalEntries[0].Clock) +
                    (session is null ? " 起 · 更早记录用 seek N 回看" : " 起 · 更早记录可在回放中查看")
                  : "",
              ActionPageIndex = actionPage, ExpandActions = expandActions, OptionTimeBasis = optionTimeBasis,
              Paginate = tui, PageIndex = terminalPage };
        if (orderedViewer != model.Actor) { roomOrder.Clear(); orderedViewer = model.Actor; }
        foreach (var room in model.Rooms) if (!roomOrder.ContainsKey(room.Id)) roomOrder.Add(room.Id, roomOrder.Count);
        model = model with { Rooms = model.Rooms.OrderBy(room => roomOrder[room.Id]).ToArray() };
        return model;
    }

    private TerminalRenderResult RenderCurrent(bool writeConsole = true, TerminalModel? preparedModel = null)
    {
        var model = preparedModel ?? CurrentModel();
        var width = terminalWidth;
        var height = terminalHeight;
        if (!Console.IsOutputRedirected && tui)
        {
            width = Math.Clamp(Console.WindowWidth - 1, 60, 240);
            height = Math.Clamp(Console.WindowHeight - 2, 24, 100);
            if (writeConsole) Console.Clear();
        }
        var rendered = TerminalView.RenderFrame(model, width, height,
            tui && !noColor && Environment.GetEnvironmentVariable("NO_COLOR") is null,
            includeSvg: exportFramePath is not null);
        terminalPage = rendered.PageIndex;
        actionPage = rendered.ActionPageIndex;
        if (exportFramePath is { } path)
        {
            exportFramePath = null;
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(rendered.Svg);
        }
        if (writeConsole) Console.WriteLine(rendered.Text);
        return rendered;
    }

    private IReadOnlyDictionary<string, object?> ReplayObserverSnapshot(IReadOnlyDictionary<string, object?> preview)
    {
        var observers = HostValues.Map(preview, "observers");
        if (observers.GetValueOrDefault(Observer) is IReadOnlyDictionary<string, object?> observation)
            return HostValues.Map(observation, "snapshot");
        var snapshot = HostValues.Map(preview, "snapshot");
        if (HostValues.Text(snapshot, "viewer") == Observer) return snapshot;
        if (HostValues.Text(replaySnapshot, "viewer") == Observer || Observer.Length == 0) return replaySnapshot;
        return HostValues.Freeze(new Dictionary<string, object?> { ["viewer"] = Observer });
    }

    private static string StateLabel(string status) => status switch
    {
        "awaiting_input" => "待选", "prepared" => "待确认", "stopped" or "closed" => "会话已停止", _ => "就绪"
    };

    private static string Header(IReadOnlyDictionary<string, object?> status) =>
        $"[第{status.GetValueOrDefault("chapter")}章 | 游戏时间 {status.GetValueOrDefault("game_time")} 分钟 | {HostValues.Text(status, "location")} | 同房: {string.Join("、", HostValues.List(status, "people").OfType<IReadOnlyDictionary<string, object?>>().Select(person => HostValues.Text(person, "name")))}]";

    private static string PresentationBody(IReadOnlyDictionary<string, object?> presentation)
    {
        if (presentation.GetValueOrDefault("visible") is false) return "";
        var lines = HostValues.List(presentation, "lines").Select(line => line is string value ? value :
            line is IReadOnlyDictionary<string, object?> item ? HostValues.Text(item, "text") : "").Where(value => !string.IsNullOrWhiteSpace(value));
        var body = string.Join("\n", lines);
        var surface = HostValues.Text(presentation, "surface").Trim();
        return surface.Length == 0 || surface == body.Trim() ? body.Trim() : (surface + "\n" + body).Trim();
    }

    private static string Format(string kind, object data)
    {
        if (data is string text) return text;
        if (kind == "performance" && data is IReadOnlyDictionary<string, object?> metrics) return FormatPerformance(metrics);
        if (data is ReplayFrame frame) return $"[{frame.Sequence} {frame.Kind}] {frame.Text}";
        if (kind == "status" && data is IReadOnlyDictionary<string, object?> status)
            return Header(status) + "\n" + JsonSerializer.Serialize(status, Json);
        if (kind == "options" && data is IReadOnlyList<IReadOnlyDictionary<string, object?>> options)
            return options.Count == 0 ? "—" : string.Join("\n", options.Select((option, index) =>
                $"[{index + 1:00}] {HostValues.Text(option, "id")} | {HostValues.Text(option, "label")} | 耗费 {JsonSerializer.Serialize(option.GetValueOrDefault("cost"), Json)} | {JsonSerializer.Serialize(option.GetValueOrDefault("interaction"), Json)}"));
        if (kind == "presentation" && data is IReadOnlyDictionary<string, object?> presentation)
        {
            var speaker = HostValues.Text(presentation, "speaker_name");
            var blocks = PresentationBlocks(presentation, speaker);
            return $"[{HostValues.Text(presentation, "outcome")}]\n" + string.Join("\n", blocks.Select(block =>
            {
                var heading = TerminalProjection.VoiceHeading(block.Speaker, block.Voice);
                return (heading.Length == 0 ? "" : heading + ": ") + block.Text;
            }));
        }
        return JsonSerializer.Serialize(data, Json);
    }

    private static string FormatPerformance(IReadOnlyDictionary<string, object?> metrics)
    {
        static string Number(object? value) => value is null ? "—" : Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.###", CultureInfo.InvariantCulture);
        static string Seconds(object? value) => value is null ? "—" : Number(Convert.ToDouble(value, CultureInfo.InvariantCulture) / 1000d);
        var controllerKind = NormalizeControllerKind(HostValues.Text(metrics, "controller_kind"));
        var text = new StringBuilder();
        text.AppendLine($"推进 {Number(metrics.GetValueOrDefault("game_hours_per_real_hour"))} 游戏小时/现实小时 · 正式决策 {Number(metrics.GetValueOrDefault("decisions_per_real_hour"))} 次/现实小时");
        text.AppendLine($"{ControllerLabel(controllerKind)}接口 {Number(metrics.GetValueOrDefault("interface_hours_per_real_hour"))} 消耗小时/现实小时 · 累计 {Seconds(metrics.GetValueOrDefault("interface_total_ms"))} 秒");
        text.AppendLine($"采样 {Seconds(metrics.GetValueOrDefault("elapsed_wall_ms"))} 现实秒 · 推进 {Number(metrics.GetValueOrDefault("advanced_game_minutes"))} 游戏分钟 · 已提交 {Number(metrics.GetValueOrDefault("total_decisions"))} 次");
        foreach (var source in HostValues.List(metrics, "decision_sources").OfType<IReadOnlyDictionary<string, object?>>())
            text.AppendLine($"{HostValues.Text(source, "controller_label")} · {Number(source.GetValueOrDefault("decisions"))} 次 · {Number(source.GetValueOrDefault("decisions_per_real_hour"))} 次/现实小时 · {Number(source.GetValueOrDefault("interface_hours_per_real_hour"))} 消耗小时/现实小时");
        var lifecycle = HostValues.Map(metrics, "lifecycle");
        if (lifecycle.Count > 0)
            text.AppendLine($"已选择 {Number(lifecycle.GetValueOrDefault("selected"))} · 已排程 {Number(lifecycle.GetValueOrDefault("scheduled"))} · 已完成 {Number(lifecycle.GetValueOrDefault("completed"))} · 待执行 {Number(lifecycle.GetValueOrDefault("pending"))} · 未进入存档 {Number(lifecycle.GetValueOrDefault("selections_not_in_save"))}");
        foreach (var category in HostValues.List(metrics, "categories").OfType<IReadOnlyDictionary<string, object?>>())
            text.AppendLine($"{HostValues.Text(category, "label")} · 排他 {Seconds(category.GetValueOrDefault("exclusive_total_ms"))} 秒");
        foreach (var row in HostValues.List(metrics, "operations").OfType<IReadOnlyDictionary<string, object?>>())
            text.AppendLine($"{HostValues.Text(row, "category_label")}/{HostValues.Text(row, "label")} · {Number(row.GetValueOrDefault("calls"))} 次 · 排他 {Number(row.GetValueOrDefault("exclusive_total_ms"))} ms · 含子项 {Number(row.GetValueOrDefault("inclusive_total_ms"))} ms · 排他均值 {Number(row.GetValueOrDefault("exclusive_mean_ms"))} ms · 含子项均值 {Number(row.GetValueOrDefault("inclusive_mean_ms"))} ms · P95上界 {Number(row.GetValueOrDefault("inclusive_p95_upper_ms"))} ms · 检阅 {HostValues.Text(row, "viewer", "—")} · 范围 {HostValues.Text(row, "projection_scope", "—")}");
        return text.ToString().TrimEnd();
    }

    private void Error(Exception error)
    {
        if (debugErrors) Console.Error.WriteLine(error);
        // Reflection wrappers hide the useful compiler/runtime message. Expose
        // only the actual error text/type, never inspect actor or world state.
        var reported = error;
        while (reported is System.Reflection.TargetInvocationException && reported.InnerException is { } inner)
            reported = inner;
        var stage = session is null && replay is null ? "startup" : "operation";
        var stageLabel = stage == "startup" ? "启动失败" : "操作失败";
        if (jsonLines)
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                kind = "error", stage, error_type = reported.GetType().Name, message = reported.Message
            }, Json));
        Console.Error.WriteLine(stageLabel + " · " + reported.Message);
    }

    private void Cancel(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        quit = true;
        cancellation.Cancel();
    }

    private const string Help = """
DV Core CLI
  --tui | --jsonl
  --preview --preview-rooms 1..24 --preview-current N
  --map subjective|objective|both
  --ui-zoom fit|detail|compact --ui-history latest|recent --width 120 --height 42 --no-color
  --export-ui NEW.svg --command COMMAND
  --controller player|human|llm|ai --viewer ID --profile NAME --zone ID --seed N
  --planning selective|precise
  --opt-taskgraph on|off (default off; experimental)
  --opt-storyflow on|off  --opt-stateindex on|off (default on)
  --dev --audit --restore FILE --save FILE --trace FILE
  --scores | --no-scores
  --performance NEW_PREFIX
  --source-revision RECORDED_REVISION
  --replay FILE --interactive --speed 1|2|4|8

status  options  views  view ID  map subjective|objective|both
focus 01|ID|JSON  confirm  cancel  act 01|ID|JSON  next  advance 1..1000
history latest|recent
page N|next|prev  capture NEW.svg  capture all NEW_PREFIX  zoom fit|in|out
actions next|prev|all|compact  decision-phase request|response|reading|confirmation
save NEW.dvsave  trace NEW.dvdag  perf [NEW_PREFIX]  scores on|off|auto
play 1|2|4|8  pause  step  seek N|first|last  restart  view follow
replay frame N  replay marks  replay capture NEW_DIRECTORY
inspect ID  tp PLACE  time MINUTES  help  quit

controller: player（默认）与 human 均为人类；llm 为外部大模型；ai 为自主引擎。
planning: selective（默认）按当前任务按需展开；precise 保留内容声明的完整预算搜索。
--jsonl 仅指定输入输出格式，不会自动把人类会话记为 LLM。
--restore 默认保留存档的控制器统计分类；显式 --controller 仅调整诊断分类，不改变世界控制权。
人类和 LLM 默认隐藏 AI 评分；--scores 或 scores on 可显式显示。
默认只取已记录根分/选择分；显式开启评分才请求当前即时估值，未计算显示未估值，不补零。
perf 显示游戏小时/现实小时、正式决策次/现实小时、接口消耗小时/现实小时及分项报告。
现实会话从打开完成计至关闭请求；人工/LLM接口单独累计请求交付至正式接受的区间。
decision-phase 为外部适配器标记实际请求、响应、阅读或确认阶段，不推进游戏。
--replay 的 perf 只读当前已播放帧记录的统计；缺失字段显示 —，不新增测量或导出报告。
seek / replay frame 使用与标题相同的一基帧号，从记录起点静默恢复该视角的正文上下文，只绘制目标帧。
advance 仅用于 --jsonl --controller ai，参数为单批现实毫秒预算；保留每次正式聚焦、确认与结果，只合并协议输出。
history latest 默认显示最新完整事件；recent 展开近期事件，原始回放保留全部正文。
replay capture 在固定 --viewer 下顺序处理所有记录帧，导出变化画面的全部续页；coverage.csv 逐帧说明图片和复用范围。
capture all 展开当前行动列表并导出全部页；这些是记录界面的SVG导出，不是操作系统终端截屏。
""";
}
