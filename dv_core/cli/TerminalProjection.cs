using System.Globalization;
using System.Text.Json;

/// <summary>Projects supplied observer and preview values; never reads or mutates session authority.</summary>
internal static class TerminalProjection
{
    private static string Text(IReadOnlyDictionary<string, object?> map, string key, string fallback = "") => HostValues.Text(map, key, fallback);
    private static string EntityId(IReadOnlyDictionary<string, object?> value) => Text(value, "id", Text(value, "key", Text(value, "actor")));
    private static string Identity(IReadOnlyDictionary<string, object?> room) => Text(room, "map") + "|" + Text(room, "room");
    private static IEnumerable<IReadOnlyDictionary<string, object?>> Records(IReadOnlyDictionary<string, object?> value, string key) =>
        HostValues.List(value, key).OfType<IReadOnlyDictionary<string, object?>>();
    private static string Value(object? value) => value is null ? "—" : value is string text ? text :
        value is IReadOnlyDictionary<string, object?> exact && exact.TryGetValue("$dv_rational", out var rational) ? rational as string ?? "—" : JsonSerializer.Serialize(value);
    private static string Score(object? value) => value switch
    {
        double number when double.IsFinite(number) => number.ToString("G8", CultureInfo.InvariantCulture),
        float number when float.IsFinite(number) => number.ToString("G8", CultureInfo.InvariantCulture),
        _ => Value(value)
    };
    private static string MemberTitle(IReadOnlyDictionary<string, object?> member, bool identifiers)
    {
        var id = EntityId(member);
        var title = new[] { Text(member, "title"), Text(member, "label"), Text(member, "name") }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value != id) ?? "";
        var missing = string.IsNullOrWhiteSpace(title) || title == id;
        if (missing) title = "（未记录名称）";
        return identifiers && id.Length > 0 && (missing || Text(member, "kind") is "person" or "actor") ? title + " / " + id : title;
    }
    private static string Cost(object? value)
    {
        if (value is null) return "";
        if (value is not IReadOnlyDictionary<string, object?> cost) return Value(value);
        return string.Join(" · ", cost.Where(pair => !IsZero(pair.Value))
            .OrderBy(pair => pair.Key switch { "time" => 0, "computation" => 1, "stamina" => 2, _ => 3 })
            .ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key switch
        {
            "time" => Value(pair.Value) + "分", "computation" => "计算 " + Value(pair.Value),
            "stamina" => "体力 " + Value(pair.Value), _ => pair.Key + " " + Value(pair.Value)
        }));
    }

    private static bool IsZero(object? value)
    {
        var text = Value(value);
        var numerator = text.Split('/')[0];
        return double.TryParse(numerator, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number == 0;
    }

    private static (string Label, string Disambiguator)[] OptionLabels(IReadOnlyList<IReadOnlyDictionary<string, object?>> options)
    {
        var labels = options.Select(option => (Label: Text(option, "label").Trim(), Disambiguator: "")).ToArray();
        for (var i = 0; i < labels.Length; i++)
            if (labels[i].Label.Length == 0) labels[i].Label = "（未记录标签）";
        foreach (var group in Enumerable.Range(0, labels.Length).GroupBy(index => labels[index].Label, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1).Select(group => group.ToArray()))
        {
            foreach (var index in group)
            {
                var id = Text(options[index], "id");
                var parts = id.Split('.', StringSplitOptions.RemoveEmptyEntries);
                var suffix = id;
                for (var length = 1; length <= parts.Length; length++)
                {
                    suffix = string.Join(".", parts.TakeLast(length));
                    if (group.Where(other => other != index).All(other =>
                        string.Join(".", Text(options[other], "id").Split('.', StringSplitOptions.RemoveEmptyEntries).TakeLast(length)) != suffix)) break;
                }
                if (suffix.Length == 0) suffix = "未记录标识";
                labels[index].Disambiguator = suffix;
            }
        }
        return labels;
    }

    internal static TerminalModel FromSnapshot(IReadOnlyDictionary<string, object?> snapshot,
        IReadOnlyDictionary<string, object?> status, IReadOnlyList<IReadOnlyDictionary<string, object?>> options,
        IReadOnlyList<TerminalEntry> entries, string mode, string phase, string source, string selected = "",
        IReadOnlyDictionary<string, object?>? preview = null, string mapView = "subjective")
    {
        var viewer = Text(snapshot, "viewer", Text(status, "viewer"));
        var previewData = preview ?? HostValues.Empty;
        var actorRecords = Records(snapshot, "actors").Concat(Records(previewData, "actors"))
            .Concat(Records(status, "actors")).GroupBy(EntityId, StringComparer.Ordinal).Select(group => group.First());
        var actors = actorRecords.Select(actor => new TerminalActor
        {
            Id = EntityId(actor), Title = MemberTitle(actor, false), Selected = viewer.Length > 0 && EntityId(actor) == viewer
        }).ToArray();
        var actorTitle = Text(snapshot, "viewer_title", Text(status, "viewer_name", Text(status, "viewer_title")));
        if (string.IsNullOrWhiteSpace(actorTitle) || actorTitle == viewer)
            actorTitle = actors.FirstOrDefault(actor => actor.Id == viewer)?.Title ?? "（未记录名称） / " + viewer;
        if (actorTitle == "（未记录名称）" && viewer.Length > 0) actorTitle += " / " + viewer;
        var recordedObserver = HostValues.Map(previewData, "observers").GetValueOrDefault(viewer) as IReadOnlyDictionary<string, object?>;
        var previewViewer = Text(previewData, "viewer", Text(HostValues.Map(previewData, "snapshot"), "viewer"));
        var subjective = recordedObserver is not null
            ? HostValues.Map(recordedObserver, "subjective")
            : viewer.Length > 0 && previewViewer == viewer ? HostValues.Map(previewData, "subjective") : HostValues.Empty;
        var objective = HostValues.Map(previewData, "objective");
        var rooms = subjective.Count > 0 ? ProjectRooms(subjective, viewer) : LegacyRooms(snapshot, status, viewer, actorTitle);
        var edges = ProjectEdges(subjective.Count > 0 ? subjective : HostValues.Map(snapshot, "map_knowledge"));
        var labels = OptionLabels(options);
        var choices = options.Select((option, index) => new TerminalOption
        {
            Id = Text(option, "id"), Title = labels[index].Label, Disambiguator = labels[index].Disambiguator,
            Detail = Cost(option.GetValueOrDefault("cost")), HasCost = option.GetValueOrDefault("cost") is not null,
            Score = Score(option.GetValueOrDefault("ai_score") ?? option.GetValueOrDefault("score")),
            ScoreKind = Text(option, "score_kind"), PlanningStatus = Text(option, "planning_status"),
            Selected = Text(option, "id") == selected && selected.Length > 0
        }).ToArray();
        var clock = Value(snapshot.GetValueOrDefault("game_time") ?? status.GetValueOrDefault("game_time"));
        if (double.TryParse(clock, CultureInfo.InvariantCulture, out var minutes) && double.IsFinite(minutes))
            clock = $"第{(long)minutes / 1440 + 1}日 {(long)minutes % 1440 / 60:00}:{(long)minutes % 60:00}";
        return new TerminalModel
        {
            Title = "DV CORE / 观察台", Mode = mode, Actor = viewer, ActorTitle = actorTitle, Actors = actors,
            Location = Text(snapshot, "location_title", Text(status, "location", "—")), Clock = clock, Phase = phase,
            Source = source, Rooms = rooms, Edges = edges, Options = choices, Entries = entries,
            ObjectiveRooms = ProjectRooms(objective, viewer, true), ObjectiveEdges = ProjectEdges(objective),
            MapView = mapView, Footer = ""
        };
    }

    private static IReadOnlyList<TerminalRoom> ProjectRooms(IReadOnlyDictionary<string, object?> map, string viewer, bool identifiers = false) =>
        Records(map, "rooms").Select(room =>
        {
            var members = Records(room, "members").Select(member => new TerminalMember
            {
                Id = EntityId(member), Title = MemberTitle(member, identifiers),
                Kind = Text(member, "kind"), Detail = Text(member, "detail"),
                IsViewer = viewer.Length > 0 && EntityId(member) == viewer || member.GetValueOrDefault("is_viewer") is true
            }).ToArray();
            return new TerminalRoom
            {
                Id = Identity(room), Title = Text(room, "label", Text(room, "title", Text(room, "room", "—"))),
                IsCurrent = room.GetValueOrDefault("is_current") is bool isCurrent ? isCurrent : members.Any(member => member.IsViewer),
                Members = members
            };
        }).ToArray();

    private static IReadOnlyList<TerminalEdge> ProjectEdges(IReadOnlyDictionary<string, object?> map) =>
        Records(map, "doors").Select(door => new TerminalEdge
        {
            From = Identity(HostValues.Map(door, "from")), To = Identity(HostValues.Map(door, "to")),
            State = Text(door, "state", "")
        }).ToArray();

    private static IReadOnlyList<TerminalRoom> LegacyRooms(IReadOnlyDictionary<string, object?> snapshot,
        IReadOnlyDictionary<string, object?> status, string viewer, string actorTitle)
    {
        var current = Identity(snapshot);
        var known = Records(HostValues.Map(snapshot, "map_knowledge"), "rooms").ToList();
        if (!known.Any(room => Identity(room) == current) && Text(snapshot, "room").Length > 0)
            known.Insert(0, new Dictionary<string, object?>
            {
                ["map"] = Text(snapshot, "map"), ["room"] = Text(snapshot, "room"),
                ["label"] = Text(snapshot, "location_title", Text(status, "location", "—"))
            });
        return known.Select(room =>
        {
            var here = Identity(room) == current;
            var members = Records(room, "members").Select(member => new TerminalMember
            {
                Id = EntityId(member), Title = MemberTitle(member, false),
                Kind = Text(member, "kind"), Detail = Text(member, "detail"), IsViewer = viewer.Length > 0 && EntityId(member) == viewer
            }).ToList();
            if (here)
            {
                foreach (var person in Records(snapshot, "people").Where(person => Identity(person) == current))
                {
                    var id = EntityId(person);
                    if (id.Length > 0 && members.Any(member => member.Id == id)) continue;
                    members.Add(new TerminalMember
                    {
                        Id = id, Title = MemberTitle(person, false), Kind = "actor", IsViewer = id == viewer
                    });
                }
                if (!members.Any(member => member.IsViewer) && actorTitle.Length > 0)
                    members.Insert(0, new TerminalMember { Id = viewer, Title = actorTitle, Kind = "actor", IsViewer = true });
            }
            return new TerminalRoom
            {
                Id = Identity(room), Title = Text(room, "label", Text(room, "title", Text(room, "room", "—"))),
                IsCurrent = here, Members = members
            };
        }).ToArray();
    }

    internal static TerminalModel Preview(int roomCount = 3, int currentRoom = 1)
    {
        if (roomCount is < 1 or > 24 || currentRoom < 1 || currentRoom > roomCount) throw new ArgumentOutOfRangeException(nameof(roomCount));
        var rooms = Enumerable.Range(0, roomCount).Select(index => new TerminalRoom
        {
            Id = "room-" + index, Title = "Room " + (char)('A' + index), IsCurrent = index + 1 == currentRoom,
            Members = index + 1 == currentRoom
                ? new[]
                {
                    new TerminalMember { Id = "preview.actor.1", Title = "人物 A", Kind = "actor", IsViewer = true },
                    new TerminalMember { Id = "preview.object." + index, Title = "物体 " + (char)('A' + index), Kind = "object" }
                }
                : new[] { new TerminalMember { Id = "preview.object." + index, Title = "物体 " + (char)('A' + index), Kind = "object" } }
        }).ToArray();
        var edges = new List<TerminalEdge>();
        void Edge(int a, int b) { if (a < roomCount && b < roomCount) edges.Add(new TerminalEdge { From = rooms[a].Id, To = rooms[b].Id, State = "" }); }
        Edge(0, 1); Edge(1, 2);
        if (roomCount > 3)
        {
            Edge(1, 3); Edge(3, 4); Edge(2, 5); Edge(5, 6); Edge(4, 6);
            // Room H is deliberately disconnected in this UI fixture.
            for (var index = 8; index < roomCount; index++) Edge(index - 1, index);
        }
        var knownRooms = rooms.Where((room, index) => index < 2 || room.IsCurrent).ToArray();
        var knownIds = knownRooms.Select(room => room.Id).ToHashSet(StringComparer.Ordinal);
        return new TerminalModel
        {
            Title = "DV CORE / 观察台", Mode = "界面预览", IsPreview = true, Actor = "preview.actor.1", ActorTitle = "人物 A",
            Location = rooms[currentRoom - 1].Title, Clock = "", Phase = "focus", Source = "UI示例 · 未打开会话",
            MapView = "both", Rooms = knownRooms, Edges = edges.Where(edge => knownIds.Contains(edge.From) && knownIds.Contains(edge.To)).ToArray(),
            ObjectiveRooms = rooms, ObjectiveEdges = edges,
            Actors = new[] { new TerminalActor { Id = "preview.actor.1", Title = "人物 A", Selected = true } },
            Options = new[]
            {
                new TerminalOption { Id = "preview.inspect", Title = "检视", Score = "0.8", ScoreKind = "immediate", Selected = true },
                new TerminalOption { Id = "preview.move", Title = "移动", Score = "0.6", ScoreKind = "immediate" }
            },
            Entries = Array.Empty<TerminalEntry>(), Footer = ""
        };
    }
}
