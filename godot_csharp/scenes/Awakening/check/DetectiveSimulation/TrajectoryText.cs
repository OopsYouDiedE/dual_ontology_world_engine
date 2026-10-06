using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DualOntologyWorldEngine.Awakening;

/// <summary>Read-only presentation of recorded logical trajectories; never advances the world.</summary>
public static class TrajectoryText
{
    public static string Render(JsonElement result)
    {
        var people = new Dictionary<string, string>(StringComparer.Ordinal);
        var rooms = new Dictionary<string, string>(StringComparer.Ordinal);
        using var stream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.World.json")
            ?? throw new InvalidOperationException("Missing compiled world resource.");
        using var world = JsonDocument.Parse(stream);
        foreach (var chapter in world.RootElement.GetProperty("chapters").EnumerateArray())
        {
            foreach (var entity in Items(chapter, "entities"))
            {
                var id = String(entity, "id");
                var name = String(entity, "name");
                if (id.StartsWith("person.", StringComparison.Ordinal))
                {
                    var key = id["person.".Length..];
                    people[id] = people[key] = people[key.ToLowerInvariant()] = name;
                }
                else if (id.StartsWith("room.", StringComparison.Ordinal))
                    rooms[id] = rooms[id["room.".Length..]] = name;
            }
            if (chapter.TryGetProperty("design", out var design)
                && design.TryGetProperty("morning", out var morning)
                && morning.TryGetProperty("people", out var routines))
                foreach (var routine in routines.EnumerateObject())
                    if (people.TryGetValue(routine.Name, out var name)
                        && routine.Value.TryGetProperty("actor", out var actor))
                        people[actor.GetString()!] = name;
        }
        foreach (var room in Chapter1Data.Root.GetProperty("ROOM_NAMES").EnumerateObject())
            rooms[room.Name] = room.Value.GetString()!;

        string PersonName(string key) => people.GetValueOrDefault(key, key);
        string RoomName(string key) => rooms.GetValueOrDefault(key, key);
        string Reason(string value)
        {
            var preference = Regex.Match(value, @"^([A-Za-z_][A-Za-z0-9_.]*)：\+\d+$");
            if (preference.Success && people.ContainsKey(preference.Groups[1].Value))
                return "规则依据 " + people[preference.Groups[1].Value] + "（原始记录：" + value + "）";
            return Regex.Replace(value, @"[A-Za-z_][A-Za-z0-9_.]*",
                match => people.GetValueOrDefault(match.Value, match.Value));
        }
        var events = Items(result, "Events");
        var decisions = Items(result, "Decisions");
        var plans = Items(result, "Plans").ToDictionary(p => Integer(p, "Serial"));
        var arrivals = Items(result, "Arrivals").GroupBy(a => Integer(a, "Serial"))
            .ToDictionary(g => g.Key, g => g.ToArray());
        var lines = new List<(double Time, double Game, int Priority, int Order, string Text)>();
        var startMinute = Chapter1Data.Root.GetProperty("DAY_CLOCK").GetProperty("start_minutes").GetDouble();
        var clockAnchors = Items(result, "StoryEvents").Where(e => e.TryGetProperty("GameMinutes", out _))
            .OrderBy(e => Number(e,"Time")).ToArray();
        double AtTime(double logical) => clockAnchors.LastOrDefault(e => Number(e,"Time") <= logical) is var e
            && e.ValueKind == JsonValueKind.Object ? Number(e,"GameMinutes") : startMinute;
        void Add(double time, int priority, string text, double? game = null)
        {
            lines.Add((time, game ?? AtTime(time), priority, lines.Count, text));
        }

        string? previousMode = null;
        foreach (var control in Items(result, "Controls"))
        {
            var mode = String(control, "Mode");
            var details = new List<string>();
            if (control.TryGetProperty("SameMind", out var same))
                details.Add(same.GetBoolean() ? "保留同一认知对象" : "认知对象已不同");
            if (control.TryGetProperty("DetectiveDecisions", out var count)) details.Add($"侦探累计决定 {count.GetInt32()} 次");
            if (control.TryGetProperty("DetectiveCompleted", out count)) details.Add($"累计完成 {count.GetInt32()} 次");
            var reason = String(control, "Reason");
            // Older JSON has English diagnostic prose. Its mode/counters remain authoritative.
            if (reason.Any(c => c >= '\u4e00' && c <= '\u9fff')) details.Add(reason);
            Add(Number(control, "Time"), 0, $"{(previousMode == mode ? "控制记录" : "控制切换")}：{Mode(mode)}（{string.Join("；", details)}）");
            previousMode = mode;
        }
        foreach (var input in Items(result, "PlayerInputs"))
            Add(Number(input, "Time"), 1, $"玩家输入〔{Mode(String(input, "Control"))}〕：{Action(String(input, "Action"))}；目标 {String(input, "Target")}（诊断入口显式输入）");

        foreach (var item in Items(result, "StoryEvents"))
            Add(Number(item, "Time"), 1, $"{(item.TryGetProperty("TimeLabel",out var label) && label.ValueKind == JsonValueKind.String ? label.GetString() + "｜" : item.TryGetProperty("GameMinutes", out var game) ? "游戏时钟 " + GameStamp(game.GetDouble()) + "｜" : "")}{String(item, "Kind")}｜{String(item, "Text")}", item.TryGetProperty("GameMinutes", out var minute) ? minute.GetDouble() : null);

        var previousMotives = new Dictionary<string,string>();
        foreach (var item in Items(result,"Searches"))
        {
            var search = item.GetProperty("Decision");
            var searchMinute = item.TryGetProperty("GameMinutes",out var searchGame) ? searchGame.GetDouble() : AtTime(Number(item,"Time"));
            Add(Number(item,"Time"), 1, $"搜索诊断｜迭代 {Integer(search,"Iterations")}；节点 {Integer(search,"Nodes")}；多步深度 {Integer(search,"MaxDepth")}；剩余假设 {Integer(search,"RemainingHypotheses")}；条件置信度 {Decimal(Number(search,"Confidence"))}", searchMinute);
            foreach (var motive in Items(search,"Motives"))
            {
                var key = String(motive,"Id");
                var signature = motive.GetRawText();
                if (previousMotives.GetValueOrDefault(key) == signature) continue;
                previousMotives[key] = signature;
                Add(Number(item,"Time"), 1, $"动机假设更新｜{PersonName(String(motive,"Person"))}：{String(motive,"Label")}；主观概率 {Decimal(Number(motive,"Probability"))}；支持：{string.Join("；",Items(motive,"Support").Select(s => s.GetString()))}；反证：{string.Join("；",Items(motive,"CounterEvidence").Select(s => s.GetString()))}；来源 {String(motive,"Source")}",searchMinute);
            }
            var leading = Items(search,"Branches").OrderByDescending(b => Integer(b,"Visits")).Take(3);
            Add(Number(item,"Time"),1,"主要备选｜" + string.Join("；",leading.Select(branch => $"{String(branch,"Action")}（访问 {Integer(branch,"Visits")}，采样值 {Decimal(Number(branch,"MeanValue"))}）")),searchMinute);
        }
        foreach (var item in Items(result,"Timing"))
        {
            var entry = item.GetProperty("Timing");
            Add(Number(item,"Time"),1,$"实际计时｜{String(entry,"Kind")}：{String(entry,"Id")}；{GameStamp(Number(entry,"StartMinutes"))} → {GameStamp(Number(entry,"EndMinutes"))}",Number(entry,"EndMinutes"));
        }

        foreach (var decision in decisions)
        {
            var serial = Integer(decision, "Serial");
            var hasPlan = plans.TryGetValue(serial, out var plan);
            var action = hasPlan && String(plan, "Note") is { Length: > 0 } note
                ? note : Action(String(decision, "Action"));
            var details = new List<string>();
            var reasons = Items(decision, "Reasons").Select(r => Reason(r.GetString() ?? "")).ToArray();
            if (reasons.Length > 0) details.Add(string.Join("、", reasons));
            if (arrivals.TryGetValue(serial, out var reports))
                foreach (var arrival in reports)
                    details.Add($"{GameLabel(AtTime(Number(arrival, "Time")))} 到场代报{(Boolean(arrival, "Accepted") ? "已接受" : "未接受")}");
            var startLabel = Boolean(decision, "Resumed") ? "续做动作" : "开始动作";
            Add(Number(decision, "Time"), 3,
                $"{ClockPrefix(decision)}{PersonName(String(decision, "Person"))}｜{RoomName(String(decision, "Room"))}｜{startLabel}〔{Mode(String(decision, "Control"))}〕：{action}（依据：{(details.Count > 0 ? string.Join("；", details) : "源记录未提供")}）", decision.TryGetProperty("GameMinutes",out var decisionMinute) ? decisionMinute.GetDouble() : null);
        }
        // Every completion is kept, including multiple identical actions and text.
        foreach (var item in events)
            Add(Number(item, "Time"), 2,
                $"{ClockPrefix(item)}{PersonName(String(item, "Person"))}｜{RoomName(String(item, "Room"))}｜完成动作〔{Action(String(item, "Action"))}〕：{String(item, "Text")}", item.TryGetProperty("GameMinutes",out var eventMinute) ? eventMinute.GetDouble() : null);

        // Do not discard records if an imported file contains arrivals without decisions.
        var decisionSerials = decisions.Select(d => Integer(d, "Serial")).ToHashSet();
        foreach (var arrival in Items(result, "Arrivals").Where(a => !decisionSerials.Contains(Integer(a, "Serial"))))
            Add(Number(arrival, "Time"), 3,
                $"{PersonName(String(arrival, "Who"))}｜{RoomName(String(arrival, "Room"))}｜第 {Integer(arrival, "Serial")} 次到场代报{(Boolean(arrival, "Accepted") ? "已接受" : "未接受")}（源文件无对应决定）");

        var text = new StringBuilder();
        var decisionSummary = result.TryGetProperty("Decisions", out var decisionRecords)
            && decisionRecords.ValueKind == JsonValueKind.Array ? $"决定 {decisions.Length} 条" : "决定记录未提供";
        var continuationSummary=result.TryGetProperty("Continuation",out var continuation)&&continuation.ValueKind==JsonValueKind.Object
            ? $"｜续篇人物执行 {Integer(continuation,"CharacterActions")} 条（失败 {Integer(continuation,"FailedActions")} 条）" : "｜续篇未启动";
        var lastMinute = lines.Select(l => l.Game).DefaultIfEmpty(startMinute).Max();
        var elapsedGame = Math.Max(0, lastMinute - startMinute);
        text.AppendLine($"游戏动作时间线（到场代报，未验几何；非实机盲玩）｜{GameLabel(startMinute)} → {GameLabel(lastMinute)}｜游戏历时 {(int)(elapsedGame / 1440)}天{(int)(elapsedGame % 1440 / 60)}小时{(int)(elapsedGame % 60)}分钟｜生活翼完成事件 {events.Length} 条｜生活翼{decisionSummary}{continuationSummary}｜整场剧情及续篇记录 {Items(result, "StoryEvents").Length} 条");
        foreach (var line in lines.OrderBy(l => l.Game).ThenBy(l => l.Time).ThenBy(l => l.Priority).ThenBy(l => l.Order))
            text.AppendLine($"[{GameLabel(line.Game)}] {line.Text}");
        if (result.TryGetProperty("Pause", out var pause))
            text.AppendLine($"暂停记录：活动模拟 {Decimal(Number(pause, "ActiveSimulationAtPause"))} 秒时，输入非活动时段 {Decimal(Number(pause, "InactiveDeltaSeconds"))} 秒；完成事件从 {Integer(pause, "BeforeEvents")} 条到 {Integer(pause, "AfterEvents")} 条。");
        text.AppendLine();
        text.AppendLine("信息限制：仅转写已有记录；原始规则与分数是诊断依据，不等于完整动机因果。按游戏绝对分钟排列，同分钟保留调度记录顺序；时段边界由story声明，CTB动作进度映射到该时段的游戏分钟，午夜跨日；调度刻度不显示为分钟秒，也不作为游戏历时；延迟捕获的记录保留原游戏时刻，不能据此推定真实先后。同刻按控制、输入与剧情、完成、开始排列，未记录的先后不可推定；完成事件无动作序号，不强行配对。游戏历时不是实际计算耗时或有效游玩时长；搜索诊断仅显示访问最多的三个备选，动机未变化时不重复输出；未来采样不是事实。");
        return text.ToString();
    }

    private static JsonElement[] Items(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().ToArray() : [];
    private static string String(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(key, out var item) ? item.GetString() ?? "" : "";
    private static double Number(JsonElement value, string key) => value.TryGetProperty(key, out var item) ? item.GetDouble() : 0;
    private static int Integer(JsonElement value, string key) => value.TryGetProperty(key, out var item) ? item.GetInt32() : 0;
    private static bool Boolean(JsonElement value, string key) => value.TryGetProperty(key, out var item) && item.GetBoolean();
    private static string Decimal(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string GameStamp(double value) => $"{(int)value/60:00}:{(int)value%60:00}";
    private static string ClockPrefix(JsonElement value) => value.TryGetProperty("GameMinutes",out var minutes) ? "游戏时钟 " + GameStamp(minutes.GetDouble()) + "｜" : "";
    private static string GameLabel(double value)
    {
        var day = DualOntologyWorldEngine.Chapter2.Prototype.Chapter2Design.FirstDay + (int)Math.Floor(value / 1440);
        var minute = (int)value % 1440;
        return $"{DualOntologyWorldEngine.Chapter2.Prototype.Chapter2Design.DayName(day)} {minute / 60:00}:{minute % 60:00}";
    }
    private static string Mode(string mode) => mode switch { "player" => "玩家控制", "simulation" => "自动模拟", _ => mode };
    private static string Action(string action) => action switch
    {
        "check" => "检查", "listen" => "听辨", "work" => "工作", "rest" => "休息",
        "inspect" => "查看", "talk" => "交谈", "move" => "移动", _ => action
    };
}
