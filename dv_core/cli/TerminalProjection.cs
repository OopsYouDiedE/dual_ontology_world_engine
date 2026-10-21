using System.Globalization;
using System.Text.Json;

/// <summary>Consumes observer projections only; never reads the authority state.</summary>
internal static class TerminalProjection
{
    private static string Text(IReadOnlyDictionary<string, object?> map, string key, string fallback = "") => HostValues.Text(map, key, fallback);
    private static string Identity(IReadOnlyDictionary<string, object?> room) => Text(room, "map") + "|" + Text(room, "room");
    private static string Value(object? value) => value is null ? "未记录" : value is string text ? text :
        value is IReadOnlyDictionary<string, object?> exact && exact.TryGetValue("$dv_rational", out var rational) ? rational as string ?? "未记录" : JsonSerializer.Serialize(value);

    internal static TerminalModel FromSnapshot(IReadOnlyDictionary<string, object?> snapshot,
        IReadOnlyDictionary<string, object?> status, IReadOnlyList<IReadOnlyDictionary<string, object?>> options,
        IReadOnlyList<TerminalEntry> entries, string mode, string phase, string source, string selected = "")
    {
        var current = Identity(snapshot);
        var knowledge = HostValues.Map(snapshot, "map_knowledge");
        var rooms = new List<TerminalRoom>();
        var known = HostValues.List(knowledge, "rooms").OfType<IReadOnlyDictionary<string, object?>>().ToList();
        if (!known.Any(room => Identity(room) == current) && Text(snapshot, "room").Length > 0)
            known.Insert(0, new Dictionary<string, object?> { ["map"] = Text(snapshot, "map"), ["room"] = Text(snapshot, "room"), ["label"] = Text(snapshot, "location_title", Text(status, "location", "当前位置")) });
        foreach (var room in known)
        {
            var here = Identity(room) == current;
            var lines = new List<string> { here ? "本人在此 · 当前观察投影" : "已知房间 · 本人地图记录" };
            var people = here ? HostValues.List(snapshot, "people").OfType<IReadOnlyDictionary<string, object?>>()
                .Where(person => Text(person, "map") == Text(room, "map") && Text(person, "room") == Text(room, "room"))
                .Select(person => Text(person, "title", "未具名人物")).Where(name => name.Length > 0).ToArray() : Array.Empty<string>();
            lines.Add(people.Length > 0 ? "可见人物：" + string.Join("、", people) : "人物：未记录");
            rooms.Add(new TerminalRoom { Id = Identity(room), Title = Text(room, "label", Text(room, "room", "未记录房间")), IsCurrent = here, Lines = lines });
        }
        var edges = HostValues.List(knowledge, "doors").OfType<IReadOnlyDictionary<string, object?>>()
            .Select(door => new TerminalEdge { From = Identity(HostValues.Map(door, "from")), To = Identity(HostValues.Map(door, "to")), State = "状态未记录" }).ToArray();
        var choices = options.Select((option, index) => new TerminalOption
        {
            Id = Text(option, "id"), Title = $"{index + 1:00}  {Text(option, "label", Text(option, "id"))}",
            Detail = "费用 " + Value(option.GetValueOrDefault("cost")), Selected = Text(option, "id") == selected && selected.Length > 0
        }).ToArray();
        var clock = Value(snapshot.GetValueOrDefault("game_time") ?? status.GetValueOrDefault("game_time"));
        if (double.TryParse(clock, CultureInfo.InvariantCulture, out var minutes))
            clock = $"第{(int)minutes / 1440 + 1}日 {(int)minutes % 1440 / 60:00}:{(int)minutes % 60:00}";
        var visibleEntries = entries.ToList();
        if (visibleEntries.Count == 0)
            foreach (var observation in HostValues.List(status, "observations").OfType<IReadOnlyDictionary<string, object?>>())
                visibleEntries.Add(new TerminalEntry { Title = "本人已取得的观察", Text = Value(observation.GetValueOrDefault("ref")) + " = " + Value(observation.GetValueOrDefault("value")) + "\n来源：" + Text(observation, "source", "未记录"), Kind = "observation" });
        return new TerminalModel
        {
            Title = "DV CORE / 观察台", Mode = mode, Actor = Text(snapshot, "viewer", Text(status, "viewer", "未记录")),
            Location = Text(snapshot, "location_title", Text(status, "location", "未记录")), Clock = clock, Phase = phase,
            Source = source, Rooms = rooms, Edges = edges, Options = choices, Entries = visibleEntries,
            Footer = mode.Contains("回放") ? "只读 · 空格 暂停/继续   → 单步   1/2/4/8 倍速   Q 退出" : "dv> status  /  options  /  act <编号>  /  next  /  quit"
        };
    }

    internal static TerminalModel Preview() => new()
    {
        Title = "DV CORE / 观察台", Mode = "界面预览", IsPreview = true, Actor = "观察者示例", Location = "Room A", Clock = "时间未记录", Phase = "只读布局示例",
        Source = "UI示例数据 · 未打开会话 · 非剧情/行为验收",
        Rooms = new[]
        {
            new TerminalRoom { Id = "a", Title = "Room A", IsCurrent = true, Lines = new[] { "当前选中房间", "人物与物件在各自房间下分组", "只展示已交付的观察内容" } },
            new TerminalRoom { Id = "b", Title = "Room B", Lines = new[] { "已知的相邻房间", "人物：未记录", "连接不代表通行许可" } },
            new TerminalRoom { Id = "c", Title = "Room C", Lines = new[] { "已知房间", "内容：未记录" } }
        },
        Edges = new[] { new TerminalEdge { From = "a", To = "b", State = "状态未记录" }, new TerminalEdge { From = "b", To = "c", State = "状态未记录" } },
        Options = new[]
        {
            new TerminalOption { Id = "preview.inspect", Title = "01  检视当前房间", Detail = "仅用于展示候选布局" },
            new TerminalOption { Id = "preview.move", Title = "02  前往相邻房间", Detail = "正式资格由Core重新核对" },
            new TerminalOption { Id = "preview.wait", Title = "03  等待", Detail = "费用与结果：未记录" }
        },
        Entries = new[]
        {
            new TerminalEntry { Title = "观察内容", Text = "这是一份无会话的界面预览。房间分组、拓扑关系与候选操作分别呈现，避免把地点、人物和日志混在同一层级。", Kind = "observation" },
            new TerminalEntry { Title = "连接与门", Text = "连线表示已知房间之间存在连接。门是否开启、是否获准通过，需要独立来源；没有记录时保持未知。", Kind = "notice" },
            new TerminalEntry { Title = "决策来源", Text = "此处展示实际提交的动作与简短理由。没有规划评分、所选动作或行动理由时，不补造数值与结论。", Kind = "notice" }
        },
        Footer = "只读预览 · 正式模式：status / options / act <编号> / next / quit"
    };
}
