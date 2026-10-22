using System.Diagnostics;
using System.Globalization;
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
    private string? savePath, tracePath;
    private readonly List<TerminalEntry> terminalEntries = new();
    private IReadOnlyDictionary<string, object?> replaySnapshot = HostValues.Empty;
    private object? replayGameTime;
    private ReplayFrame? terminalFrame;
    private int terminalWidth = 120, terminalHeight = 42;
    private int terminalPage;
    private bool noColor, showActors;
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
    private IReadOnlyList<IReadOnlyDictionary<string, object?>> replayOptions = Array.Empty<IReadOnlyDictionary<string, object?>>();
    private string replaySelected = "";
    private string replayChoiceActor = "";
    private sealed record ChoiceFocus(string Id, string Selection, string Actor, long Version, string InteractionId,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Options, IReadOnlyDictionary<string, object?> Preview);
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
                case "--export-ui": exportUi = Value(); break;
                case "--width": terminalWidth = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--height": terminalHeight = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--no-color": noColor = true; break;
                case "--dev": developer = debugErrors = true; break;
                case "--audit": audit = true; break;
                case "--viewer": viewer = Value(); explicitViewer = true; break;
                case "--controller": controller = Value(); break;
                case "--restore": restore = Value(); break;
                case "--save": savePath = Value(); break;
                case "--trace": tracePath = Value(); break;
                case "--command": command = Value(); break;
                case "--replay": replayPath = Value(); break;
                case "--interactive": interactiveReplay = true; break;
                case "--map": mapView = ParseMap(Value()); explicitMap = true; break;
                case "--speed": speed = double.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--profile": campaign["profile"] = Value(); break;
                case "--zone": campaign["detective_zone"] = Value(); break;
                case "--seed": campaign["seed"] = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }
        if (interactiveReplay && replayPath is null) throw new ArgumentException("--interactive: --replay");
        if (jsonLines && tui) throw new ArgumentException("--jsonl and --tui are separate output modes.");
        if (controller is not ("player" or "ai")) throw new ArgumentException("--controller must be player or ai.");
        if (speed is not (1 or 2 or 4 or 8)) throw new ArgumentException("--speed must be 1, 2, 4 or 8.");
        if (replayPath is not null && (restore is not null || savePath is not null || tracePath is not null))
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
            if (replayPath is not null || restore is not null || savePath is not null || tracePath is not null || command is not null || controller != "player" || developer || audit || campaign.Count > 0)
                throw new ArgumentException("--preview is a session-free UI example; do not combine it with session or replay options.");
            var model = TerminalProjection.Preview(previewRooms, previewCurrent) with { Zoom = terminalZoom, MapView = explicitMap ? mapView : "both" };
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
            if (!tui || command is null || (replayPath is null && command is not ("status" or "options")) || (replayPath is not null && command != "step"))
                throw new ArgumentException("--export-ui requires --preview, or --tui with a read-only --command status/options (recorded replay: step).");
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
            if (tui && !developer) session.DiagnosticOutput = TextWriter.Null;
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
            case "capture":
                if (!tui || argument.Length == 0) throw new ArgumentException("capture <new.svg>");
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
                case "pause": player.Pause(); Emit("replay", ReplayStatus()); break;
                case "play":
                    player.Play(argument.Length == 0 ? player.Speed : double.Parse(argument, CultureInfo.InvariantCulture));
                    if (oneCommand) await PlayReplay(); else Emit("replay", ReplayStatus());
                    break;
                case "restart":
                    player.Restart(); replaySnapshot = HostValues.Empty; replayPreview = HostValues.Empty;
                    replayGameTime = null;
                    replayOptions = Array.Empty<IReadOnlyDictionary<string, object?>>(); replaySelected = "";
                    replayChoiceActor = "";
                    terminalFrame = null; terminalEntries.Clear(); roomOrder.Clear(); interactionPhase = "";
                    Emit("replay", ReplayStatus()); break;
                default: throw new ArgumentException("status / options / map / view / play / pause / step / restart / quit");
            }
            return;
        }
        showActors = false;
        var current = session ?? throw new InvalidOperationException("会话 —");
        switch (name.ToLowerInvariant())
        {
            case "options": Emit("options", VisibleOptions()); break;
            case "next": AdvanceOne(current); break;
            case "focus": case "select": FocusChoice(current, argument); break;
            case "confirm": ConfirmChoice(current); break;
            case "cancel":
                if (focus is { } cancelled) RecordPhase(current, "cancel", cancelled);
                focus = null; interactionPhase = ""; Emit("status", CurrentStatus()); break;
            case "act":
                FocusChoice(current, argument);
                ConfirmChoice(current); break;
            case "inspect": Emit("inspect", current.Inspect(argument.Length == 0 ? Observer : argument)); break;
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
        if (next == "subjective" && mapView != "subjective") terminalEntries.Clear();
        mapView = next;
        terminalPage = 0;
    }

    private void ChangeDisplayViewer(string actor)
    {
        if (displayViewer != actor)
        {
            terminalEntries.Clear();
            roomOrder.Clear();
            orderedViewer = "";
            terminalPage = 0;
        }
        displayViewer = actor;
    }

    private string Observer => displayViewer.Length > 0 ? displayViewer : session?.Viewer ?? HostValues.Text(replaySnapshot, "viewer");

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
        var options = current.ObserverOptions(current.Viewer);
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
        var options = current.ObserverOptions(actor);
        if (followReplayActor) ChangeDisplayViewer(actor);
        focus = NewFocus(current, id, id, actor, options);
        terminalPage = -1;
        interactionPhase = "聚焦";
        RecordPhase(current, "focus", focus);
        Emit("interaction", FocusPayload(focus, "focus"));
    }

    private ChoiceFocus NewFocus(DvSession current, string id, string selection, string actor,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> options) =>
        new(id, selection, actor, current.Version, "cli-" + interactionSession + "-" + (++interactionSequence).ToString(CultureInfo.InvariantCulture),
            options, current.Preview(actor, true));

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
        Emit("presentation", presentation);
        interactionPhase = "完成";
        Emit("status", CurrentStatus());
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
            ["action_id"] = selected.Id, ["selection"] = selected.Selection, ["state_version"] = selected.Version,
            ["options"] = selected.Options
        });

    private void RecordPhase(DvSession current, string phase, ChoiceFocus selected,
        IReadOnlyDictionary<string, object?>? presentation = null)
    {
        var record = new Dictionary<string, object?>(FocusPayload(selected, phase), StringComparer.Ordinal)
        {
            ["kind"] = "cli_interaction", ["viewer"] = Observer,
            ["real_us"] = (long)(recordingClock.Elapsed.TotalMilliseconds * 1000),
            ["input_mode"] = jsonLines ? "jsonl" : tui ? "tui" : "text",
            ["controller"] = ControllerIsAutonomous(current) ? "ai" : "player",
            ["source_state_version"] = selected.Version, ["state_version"] = current.Version,
            ["preview"] = phase == "result" ? current.Preview(selected.Actor, true) : selected.Preview,
            ["presentation"] = presentation ?? HostValues.Empty
        };
        if (phase == "result") record["presentation_viewer"] = Observer;
        current.RecordInteraction(HostValues.Freeze(record));
    }

    private object CurrentStatus() => session is { } current ? current.TextStatus(Observer) : ReplayStatus();

    private IReadOnlyDictionary<string, object?> CurrentPreview(bool objective)
    {
        var source = session is { } current ? current.Preview(Observer, objective) : replayPreview;
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
            ["subjective"] = observation is null ? HostValues.Empty : HostValues.Map(observation, "subjective")
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
            _ = current.Preview(actor, false);
        }
        else if (!RecordedActors().OfType<IReadOnlyDictionary<string, object?>>()
                     .Any(item => HostValues.Text(item, "id") == actor))
            throw new ArgumentException("视角未记录");
        ChangeDisplayViewer(actor);
        followReplayActor = false;
        roomOrder.Clear();
        terminalEntries.Clear();
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object?>> VisibleOptions()
    {
        if (session is { } current) return current.ObserverOptions(Observer);
        if (replayChoiceActor == Observer) return replayOptions;
        var observers = HostValues.Map(replayPreview, "observers");
        if (observers.GetValueOrDefault(Observer) is IReadOnlyDictionary<string, object?> observation)
            return HostValues.List(observation, "options").OfType<IReadOnlyDictionary<string, object?>>().ToArray();
        return Array.Empty<IReadOnlyDictionary<string, object?>>();
    }

    private static bool ControllerIsAutonomous(DvSession current) =>
        current.Materialize(current.Viewer).GetValueOrDefault("player_control") is not true;

    private void EmitIntent(IReadOnlyDictionary<string, object?> intent) => Emit("intent", new
    {
        status = HostValues.Text(intent, "status"),
        game_time = intent.GetValueOrDefault("game_time"),
        // The pending action carries executor-private law inputs. The text host
        // exposes neither those inputs nor any predicted observation to its viewer.
        awaiting_input = HostValues.Text(intent, "status") == "awaiting_input"
    });

    private object ReplayStatus() => new
    {
        mode = "recorded-replay", index = replay?.Index, count = replay?.Count, speed = replay?.Speed,
        playing = replay?.Playing, viewer = Observer, phase = interactionPhase,
        snapshot = HostValues.Map(CurrentPreview(false), "snapshot"), options = VisibleOptions(), selected = replayChoiceActor == Observer ? replaySelected : ""
    };

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

    private void ShowFrame(ReplayFrame? frame)
    {
        if (frame is null) return;
        terminalFrame = frame;
        if (frame.Data.GetValueOrDefault("preview") is IReadOnlyDictionary<string, object?> preview)
            replayPreview = preview;
        if (frame.Data.GetValueOrDefault("snapshot") is IReadOnlyDictionary<string, object?> snapshot)
            replaySnapshot = snapshot;
        var frameSnapshot = HostValues.Map(frame.Data, "snapshot");
        var framePreviewSnapshot = HostValues.Map(HostValues.Map(frame.Data, "preview"), "snapshot");
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
        if (frame.Kind == "cancel") { replaySelected = ""; interactionPhase = "取消"; }
        if (frame.Kind is "focus" or "confirm")
        {
            terminalPage = -1;
            replaySelected = frame.Action;
            interactionPhase = frame.Kind == "focus" ? "聚焦" :
                HostValues.Text(HostValues.Map(frame.Data, "presentation"), "outcome") == "scheduled" ? "已排程" : "确认";
        }
        Emit("frame", frame);
    }

    private void Emit(string kind, object data)
    {
        if (jsonLines)
        {
            object visible = data is ReplayFrame frame ? DisplayReplayFrame(frame) : data;
            if (kind == "interaction" && data is IReadOnlyDictionary<string, object?> interaction &&
                HostValues.Text(interaction, "actor") != Observer)
                visible = new { phase = HostValues.Text(interaction, "phase"), viewer = Observer,
                    options = VisibleOptions(), selected = "", state_version = session?.Version };
            Console.WriteLine(JsonSerializer.Serialize(new { kind, scope = session is null ? "recorded-replay" : "formal-model", data = visible }, Json));
            return;
        }
        if (kind is "presentation" or "frame") AddTranscript(kind, data);
        if (!tui && kind is not ("status" or "options" or "map" or "views" or "interaction" or "frame" or "presentation" or "intent" or "replay"))
        {
            Console.WriteLine(Format(kind, data)); return;
        }
        RenderCurrent();
    }

    private ReplayFrame DisplayReplayFrame(ReplayFrame frame)
    {
        var values = new Dictionary<string, object?>
        {
            ["contract"] = SmartReplay.Contract, ["viewer"] = Observer, ["phase"] = interactionPhase,
            ["options"] = VisibleOptions(), ["selected"] = replayChoiceActor == Observer ? replaySelected : "",
            ["preview"] = CurrentPreview(mapView != "subjective")
        };
        foreach (var key in new[] { "interaction_contract", "interaction_id", "state_version", "source_state_version", "record_index", "result_record_indices", "input_record_index", "controller", "input_mode", "selection_recorded", "result_kind" })
            if (frame.Data.TryGetValue(key, out var value)) values[key] = value;
        var outcome = HostValues.Text(HostValues.Map(frame.Data, "presentation"), "outcome");
        if (outcome.Length > 0) values["outcome"] = outcome;
        var visible = PresentationVisibleToObserver(frame);
        var text = "";
        if (visible && frame.Kind is ("focus" or "confirm" or "presentation" or "visible" or "dialogue" or "investigate" or "rejected" or "move"))
        {
            var presentation = HostValues.Map(frame.Data, "presentation");
            text = presentation.Count > 0 ? PresentationBody(presentation) : frame.Text;
        }
        return frame with { Text = text, Data = HostValues.Freeze(values) };
    }

    private bool PresentationVisibleToObserver(ReplayFrame frame)
    {
        var presentation = HostValues.Map(frame.Data, "presentation");
        if (presentation.GetValueOrDefault("visible") is false) return false;
        var viewer = HostValues.Text(frame.Data, "presentation_viewer");
        if (presentation.Count > 0 && viewer.Length > 0) return viewer == Observer;
        return mapView != "subjective" || frame.Actor.Length == 0 || frame.Actor == Observer;
    }

    private void AddTranscript(string kind, object data)
    {
        var title = "";
        var text = "";
        if (data is ReplayFrame recorded)
        {
            if (recorded.Kind is not ("presentation" or "visible" or "dialogue" or "investigate" or "rejected" or "move" or "confirm")) return;
            if (!PresentationVisibleToObserver(recorded)) return;
            title = recorded.Actor;
            if (recorded.Kind == "confirm" && HostValues.Map(recorded.Data, "presentation") is { Count: > 0 } shown)
            {
                title = HostValues.Text(shown, "speaker_name", recorded.Actor);
                text = PresentationBody(shown);
            }
            else text = recorded.Text;
        }
        if (kind == "presentation" && data is IReadOnlyDictionary<string, object?> presentation)
        {
            if (presentation.GetValueOrDefault("visible") is false) return;
            text = PresentationBody(presentation);
            var outcome = HostValues.Text(presentation, "outcome");
            if (text.Length == 0 && outcome.Length > 0 && outcome != "applied") text = outcome;
            title = HostValues.Text(presentation, "speaker_name");
        }
        if (string.IsNullOrWhiteSpace(text)) return;
        if (terminalEntries.LastOrDefault() is { } last && last.Title == title && last.Text == text) return;
        terminalEntries.Add(new TerminalEntry { Title = title, Text = text, Kind = kind });
        if (terminalEntries.Count > TranscriptCapacity) terminalEntries.RemoveAt(0);
    }

    private void RenderCurrent()
    {
        var preview = CurrentPreview(mapView != "subjective");
        var snapshot = HostValues.Map(preview, "snapshot");
        var status = session?.TextStatus(Observer) ?? HostValues.Map(preview, "status");
        var choices = VisibleOptions();
        var mode = session is null ? $"回放 {replay?.Index + 1}/{replay?.Count} · {replay?.Speed}× · {(replay?.Playing == true ? "播放" : "暂停")}" : ControllerIsAutonomous(session) ? "自主" : "人工";
        var phase = session is null
            ? interactionPhase.Length > 0 ? "帧内：" + interactionPhase : ""
            : interactionPhase.Length > 0 ? interactionPhase : StateLabel(HostValues.Text(session.Intent, "status"));
        var model = TerminalProjection.FromSnapshot(snapshot, status, choices, terminalEntries.ToArray(), mode,
            phase, "", session is null ? replayChoiceActor == Observer ? replaySelected : "" : focus?.Actor == Observer ? focus.Id : "", preview, mapView) with
            { Zoom = terminalZoom, ShowActors = showActors, Paginate = tui, PageIndex = terminalPage };
        if (orderedViewer != model.Actor) { roomOrder.Clear(); orderedViewer = model.Actor; }
        foreach (var room in model.Rooms) if (!roomOrder.ContainsKey(room.Id)) roomOrder.Add(room.Id, roomOrder.Count);
        model = model with { Rooms = model.Rooms.OrderBy(room => roomOrder[room.Id]).ToArray() };
        var width = terminalWidth;
        var height = terminalHeight;
        if (!Console.IsOutputRedirected && tui)
        {
            width = Math.Clamp(Console.WindowWidth - 1, 60, 240);
            height = Math.Clamp(Console.WindowHeight - 2, 24, 100);
            Console.Clear();
        }
        terminalPage = TerminalView.GetPageInfo(model, width, height).Index;
        if (exportFramePath is { } path)
        {
            exportFramePath = null;
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(TerminalView.ExportSvg(model, width, height));
        }
        Console.WriteLine(TerminalView.Render(model, width, height, tui && !noColor && Environment.GetEnvironmentVariable("NO_COLOR") is null));
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
        "awaiting_input" => "待选", "prepared" => "待确认", "stopped" or "closed" => "结束", _ => "就绪"
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
        if (data is ReplayFrame frame) return $"[{frame.Sequence} {frame.Kind}] {frame.Text}";
        if (kind == "status" && data is IReadOnlyDictionary<string, object?> status)
            return Header(status) + "\n" + JsonSerializer.Serialize(status, Json);
        if (kind == "options" && data is IReadOnlyList<IReadOnlyDictionary<string, object?>> options)
            return options.Count == 0 ? "—" : string.Join("\n", options.Select((option, index) =>
                $"[{index + 1:00}] {HostValues.Text(option, "id")} | {HostValues.Text(option, "label")} | 耗费 {JsonSerializer.Serialize(option.GetValueOrDefault("cost"), Json)} | {JsonSerializer.Serialize(option.GetValueOrDefault("interaction"), Json)}"));
        if (kind == "presentation" && data is IReadOnlyDictionary<string, object?> presentation)
        {
            var lines = HostValues.List(presentation, "lines").Select(line => line is string text ? text :
                line is IReadOnlyDictionary<string, object?> item ? HostValues.Text(item, "text") : "");
            var surface = HostValues.Text(presentation, "surface");
            var speaker = HostValues.Text(presentation, "speaker_name");
            return $"[{HostValues.Text(presentation, "outcome")}]\n" +
                (surface.Length == 0 ? "" : (speaker.Length == 0 ? "" : speaker + ": ") + surface + "\n") + string.Join("\n", lines.Where(text => text.Length > 0));
        }
        return JsonSerializer.Serialize(data, Json);
    }

    private void Error(Exception error)
    {
        if (debugErrors) Console.Error.WriteLine(error);
        if (jsonLines) Console.WriteLine(JsonSerializer.Serialize(new { kind = "error", message = error.Message }, Json));
        else Console.Error.WriteLine(error.Message);
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
  --ui-zoom fit|detail|compact --width 120 --height 42 --no-color
  --export-ui NEW.svg --command COMMAND
  --controller player|ai --viewer ID --profile NAME --zone ID --seed N
  --dev --audit --restore FILE --save FILE --trace FILE
  --replay FILE --interactive --speed 1|2|4|8

status  options  views  view ID  map subjective|objective|both
focus 01|ID|JSON  confirm  cancel  act 01|ID|JSON  next
page N|next|prev  capture NEW.svg  zoom fit|in|out
save NEW.dvsave  trace NEW.dvdag
play 1|2|4|8  pause  step  restart  view follow
inspect ID  tp PLACE  time MINUTES  help  quit
""";
}
