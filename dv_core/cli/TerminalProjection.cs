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
            var lines = new List<string>();
            var people = here ? HostValues.List(snapshot, "people").OfType<IReadOnlyDictionary<string, object?>>()
                .Where(person => Text(person, "map") == Text(room, "map") && Text(person, "room") == Text(room, "room"))
                .Select(person => Text(person, "title", "未具名人物")).Where(name => name.Length > 0).ToArray() : Array.Empty<string>();
            if (people.Length > 0) lines.Add(string.Join("、", people));
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

    internal static TerminalModel Preview(int roomCount = 3, int currentRoom = 1)
    {
        if (roomCount is < 1 or > 24 || currentRoom < 1 || currentRoom > roomCount) throw new ArgumentOutOfRangeException(nameof(roomCount));
        var rooms = Enumerable.Range(0, roomCount).Select(index => new TerminalRoom
        {
            Id = "room-" + index, Title = "Room " + (char)('A' + index), IsCurrent = index + 1 == currentRoom,
            Lines = new[] { "记录 " + (index * 2 + 1), "记录 " + (index * 2 + 2) }
        }).ToArray();
        var edges = new List<TerminalEdge>();
        void Edge(int a, int b) { if (a < roomCount && b < roomCount) edges.Add(new TerminalEdge { From = rooms[a].Id, To = rooms[b].Id, State = "状态未记录" }); }
        Edge(0, 1); Edge(1, 2);
        if (roomCount > 3)
        {
            Edge(1, 3); Edge(3, 4); Edge(2, 5); Edge(5, 6); Edge(4, 6);
            // Room H is deliberately disconnected in this UI fixture.
            for (var index = 8; index < roomCount; index++) Edge(index - 1, index);
        }
        return new TerminalModel
        {
            Title = "DV CORE / 观察台", Mode = "界面预览", IsPreview = true, Actor = "侦探", Location = rooms[currentRoom - 1].Title,
            Clock = "时间未记录", Phase = "只读", Source = "UI示例 · 未打开会话", Rooms = rooms, Edges = edges,
            Options = new[]
            {
                new TerminalOption { Id = "preview.inspect", Title = "01  检视" },
                new TerminalOption { Id = "preview.move", Title = "02  移动" }
            },
            Entries = Array.Empty<TerminalEntry>(),
            Footer = "status / options / act <编号> / next / quit"
        };
    }
}
