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
    private bool jsonLines, tui, quit;
    private string? savePath, tracePath;
    private readonly Queue<string> transcript = new();
    private readonly CancellationTokenSource cancellation = new();
    private const int TranscriptCapacity = 40;

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
            cli.cancellation.Dispose();
        }
        return exitCode;
    }

    private async Task Start(string[] args)
    {
        string? replayPath = null, restore = null, command = null;
        var viewer = "detective";
        var controller = "player";
        var developer = false;
        var audit = false;
        var speed = 1d;
        var campaign = new Dictionary<string, object?>();
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
                case "--dev": developer = true; break;
                case "--audit": audit = true; break;
                case "--viewer": viewer = Value(); break;
                case "--controller": controller = Value(); break;
                case "--restore": restore = Value(); break;
                case "--save": savePath = Value(); break;
                case "--trace": tracePath = Value(); break;
                case "--command": command = Value(); break;
                case "--replay": replayPath = Value(); break;
                case "--speed": speed = double.Parse(Value(), CultureInfo.InvariantCulture); break;
                case "--profile": campaign["profile"] = Value(); break;
                case "--zone": campaign["detective_zone"] = Value(); break;
                case "--seed": campaign["seed"] = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }
        if (jsonLines && tui) throw new ArgumentException("--jsonl and --tui are separate output modes.");
        if (controller is not ("player" or "ai")) throw new ArgumentException("--controller must be player or ai.");
        if (speed is not (1 or 2 or 4 or 8)) throw new ArgumentException("--speed must be 1, 2, 4 or 8.");
        if (replayPath is not null && (restore is not null || savePath is not null || tracePath is not null))
            throw new ArgumentException("Recorded replay does not restore or save live sessions.");
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        if (replayPath is not null)
        {
            replay = SmartReplay.Load(replayPath);
            replay.Play(speed);
            if (command is null) { await PlayReplay(); return; }
            replay.Pause();
        }
        else
        {
            session = restore is not null ? DvSession.RestoreFile(restore) : DvSession.OpenConfiguredCampaign(
                JsonSerializer.Serialize(new { controller, viewer, campaign, audit, developer }, Json));
            // A caller explicitly advances with next/act. Opening/status does not run NPCs.
        }
        if (command is not null) { await Execute(command); return; }
        if (!jsonLines) Emit("status", session?.TextStatus() ?? ReplayStatus());
        while (!quit)
        {
            if (!Console.IsInputRedirected && !jsonLines) Console.Write(replay is null ? "dv> " : "replay> ");
            var line = await Console.In.ReadLineAsync(cancellation.Token);
            if (line is null) break;
            try { await Execute(line); }
            catch (Exception error) { if (Console.IsInputRedirected) throw; Error(error); }
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
            case "status": Emit("status", session?.TextStatus() ?? ReplayStatus()); return;
        }
        if (replay is { } player)
        {
            switch (name.ToLowerInvariant())
            {
                case "": case "step": case "next": ShowFrame(player.Step()); break;
                case "pause": player.Pause(); Emit("replay", ReplayStatus()); break;
                case "play":
                    player.Play(argument.Length == 0 ? player.Speed : double.Parse(argument, CultureInfo.InvariantCulture));
                    await PlayReplay(); break;
                case "restart": player.Restart(); Emit("replay", ReplayStatus()); break;
                default: throw new ArgumentException("Replay commands: status, play [1|2|4|8], pause, step, restart, quit.");
            }
            return;
        }
        var current = session ?? throw new InvalidOperationException("No session.");
        switch (name.ToLowerInvariant())
        {
            case "options": Emit("options", Options(current)); break;
            case "next":
                if (HostValues.Text(current.Intent, "status") == "prepared")
                    Emit("presentation", current.CompleteFormal(current.Viewer));
                else current.PrepareValue();
                EmitIntent(current.Intent); break;
            case "act":
                if (argument.Length == 0) throw new ArgumentException("act requires an option number, action ID, or selection object.");
                if (ControllerIsAutonomous(current)) throw new InvalidOperationException("act requires player control; use next for an autonomous session.");
                // Reach the player's actual input boundary without manufacturing a choice.
                DrainToInput(current);
                if (!current.Active || HostValues.Text(current.Intent, "status") != "awaiting_input")
                    throw new InvalidOperationException("The session is not at a player input boundary.");
                var selection = argument;
                if (int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                {
                    var options = Options(current);
                    if (number < 1 || number > options.Count) throw new ArgumentOutOfRangeException(nameof(argument), "Option number is outside the current list.");
                    selection = HostValues.Text(options[number - 1], "id");
                }
                if (!selection.StartsWith('{') && !Options(current).Any(option => HostValues.Text(option, "id") == selection))
                    throw new ArgumentException("Action is not a current option.");
                current.PrepareValue(selection);
                DrainToInput(current);
                Emit("status", current.TextStatus()); Emit("options", Options(current)); break;
            case "inspect": Emit("inspect", current.Inspect(argument.Length == 0 ? current.Viewer : argument)); break;
            case "tp": case "time": Emit("development", current.ExecuteDebug(name.ToLowerInvariant(), argument)); break;
            case "save":
                if (argument.Length == 0) throw new ArgumentException("save requires a new .dvsave path.");
                current.WriteSave(argument); Emit("saved", argument); break;
            case "trace":
                if (argument.Length == 0) throw new ArgumentException("trace requires a new .dvdag path.");
                current.WriteTrace(argument); Emit("trace", argument); break;
            default: throw new ArgumentException("Unknown command. Use help.");
        }
    }

    private void DrainToInput(DvSession current)
    {
        // All autonomous work uses production Prepare and formal-model Complete.
        // No timeout, extra budget, route or answer is supplied by this adapter.
        while (current.Active)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            var intent = current.PrepareValue();
            var status = HostValues.Text(intent, "status");
            if (status == "awaiting_input") return;
            if (status != "prepared") { EmitIntent(intent); return; }
            Emit("presentation", current.CompleteFormal(current.Viewer));
        }
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> Options(DvSession current) =>
        HostValues.List(current.Intent, "options").OfType<IReadOnlyDictionary<string, object?>>().ToArray();

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

    private object ReplayStatus() => new { mode = "recorded-replay", index = replay?.Index, count = replay?.Count, speed = replay?.Speed, playing = replay?.Playing };

    private async Task PlayReplay()
    {
        var player = replay ?? throw new InvalidOperationException("No replay.");
        var clock = Stopwatch.StartNew();
        var previous = clock.Elapsed.TotalSeconds;
        while (player.Playing && !quit)
        {
            var now = clock.Elapsed.TotalSeconds;
            foreach (var frame in player.Advance(now - previous)) ShowFrame(frame);
            previous = now;
            if (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.Spacebar: player.Pause(); break;
                    case ConsoleKey.RightArrow: ShowFrame(player.Step()); break;
                    case ConsoleKey.Escape: case ConsoleKey.Q: quit = true; player.Pause(); break;
                    case ConsoleKey.D1: player.Play(1); break;
                    case ConsoleKey.D2: player.Play(2); break;
                    case ConsoleKey.D4: player.Play(4); break;
                    case ConsoleKey.D8: player.Play(8); break;
                }
                if (!player.Playing && !quit)
                {
                    Emit("replay", ReplayStatus());
                    while (!player.Playing && !quit && player.Index < player.Count - 1)
                    {
                        if (!Console.KeyAvailable) { await Task.Delay(15, cancellation.Token); continue; }
                        var key = Console.ReadKey(true).Key;
                        if (key == ConsoleKey.Spacebar) player.Play(player.Speed);
                        else if (key == ConsoleKey.RightArrow) ShowFrame(player.Step());
                        else if (key is ConsoleKey.Q or ConsoleKey.Escape) quit = true;
                        else if (key is ConsoleKey.D1 or ConsoleKey.D2 or ConsoleKey.D4 or ConsoleKey.D8)
                            player.Play(key == ConsoleKey.D1 ? 1 : key == ConsoleKey.D2 ? 2 : key == ConsoleKey.D4 ? 4 : 8);
                    }
                    previous = clock.Elapsed.TotalSeconds;
                }
            }
            await Task.Delay(15, cancellation.Token);
        }
    }

    private void ShowFrame(ReplayFrame? frame) { if (frame is not null) Emit("frame", frame); }

    private void Emit(string kind, object data)
    {
        if (jsonLines) { Console.WriteLine(JsonSerializer.Serialize(new { kind, scope = session is null ? "recorded-replay" : "formal-model", data }, Json)); return; }
        var text = Format(kind, data);
        if (!tui || Console.IsOutputRedirected) { Console.WriteLine(text); return; }
        transcript.Enqueue(text);
        while (transcript.Count > TranscriptCapacity) transcript.Dequeue();
        Console.Clear();
        var width = Math.Max(1, Console.WindowWidth - 1);
        string Fit(string line) => line.Length <= width ? line : line[..width];
        Console.WriteLine(Fit(session is null ? $"[记录回放 | {replay?.Index + 1}/{replay?.Count} | {replay?.Speed}×]" : Header(session.TextStatus())));
        Console.WriteLine(new string('─', Math.Min(width, 100)));
        foreach (var entry in transcript.SelectMany(entry => entry.Split('\n')).TakeLast(Math.Max(1, Console.WindowHeight - 6)))
            Console.WriteLine(Fit(entry.TrimEnd('\r')));
        Console.WriteLine("────────────────────────────────────────");
        Console.WriteLine(session is null ? "空格 暂停/继续 | → 单步 | 1/2/4/8 倍速 | Q 退出" : "status | options | act <编号/ID> | next | save <路径> | quit");
    }

    private static string Header(IReadOnlyDictionary<string, object?> status) =>
        $"[第{status.GetValueOrDefault("chapter")}章 | 游戏时间 {status.GetValueOrDefault("game_time")} 分钟 | {HostValues.Text(status, "location")} | 同房: {string.Join("、", HostValues.List(status, "people").OfType<IReadOnlyDictionary<string, object?>>().Select(person => HostValues.Text(person, "name")))}]";

    private static string Format(string kind, object data)
    {
        if (data is string text) return text;
        if (data is ReplayFrame frame) return $"[{frame.Sequence} {frame.Kind}] {frame.Text}";
        if (kind == "status" && data is IReadOnlyDictionary<string, object?> status)
            return Header(status) + "\n" + JsonSerializer.Serialize(status, Json);
        if (kind == "options" && data is IReadOnlyList<IReadOnlyDictionary<string, object?>> options)
            return options.Count == 0 ? "当前没有选项；next 推进至下一正式边界。" : string.Join("\n", options.Select((option, index) =>
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
DV Core CLI (formal-model; no Godot geometry)
  --tui | --jsonl               Terminal view or JSON-Lines protocol
  --command "status"            Execute one command and exit
  --controller player|ai --viewer ID --profile NAME --zone ID --seed N
  --dev --audit                 Explicit development/recording modes
  --restore FILE                Restore a committed .dvsave
  --save FILE --trace FILE      Seal new binary artifacts on exit
  --replay FILE --speed 1|2|4|8 Read-only .dvdag/.dvlog replay
Commands: status, options, next, act <01|ID|selection JSON>,
          inspect [actor], tp <place>, time <minutes>,
          save <new.dvsave>, trace <new.dvdag>, help, quit.
JSON-Lines input: {"command":"act","argument":"01"}
next advances one Prepare or Complete; act drains to the next input boundary.
Replay: play [speed], pause, step, restart; space pauses, right arrow steps.
Replay uses content beats, never recorded wall-clock waits.
""";
}
