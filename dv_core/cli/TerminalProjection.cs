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
    private static string Value(object? value) => value switch
    {
        null => "—",
        string text => text,
        bool flag => flag ? "true" : "false",
        double number when double.IsFinite(number) => number.ToString("R", CultureInfo.InvariantCulture),
        float number when float.IsFinite(number) => number.ToString("R", CultureInfo.InvariantCulture),
        IFormattable number when value is sbyte or byte or short or ushort or int or uint or long or ulong or decimal =>
            number.ToString(null, CultureInfo.InvariantCulture),
        IReadOnlyDictionary<string, object?> exact when exact.TryGetValue("$dv_rational", out var rational) => rational as string ?? "—",
        _ => JsonSerializer.Serialize(value)
    };
    private static string Score(object? value) => value switch
    {
        null or false => "—",
        double number when double.IsFinite(number) => number.ToString("G8", CultureInfo.InvariantCulture),
        float number when float.IsFinite(number) => number.ToString("G8", CultureInfo.InvariantCulture),
        _ => Value(value)
    };

    internal static string GameClock(object? value, bool includeDay = false)
    {
        if (value is null or false) return "";
        var text = Value(value);
        var parts = text.Split('/');
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)) return "";
        if (parts.Length == 2)
        {
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) || denominator == 0) return "";
            minutes /= denominator;
        }
        if (!double.IsFinite(minutes) || minutes < 0 || minutes > long.MaxValue) return "";
        var whole = (long)minutes;
        return (includeDay ? $"第{whole / 1440 + 1}日 " : "") + $"{whole % 1440 / 60:00}:{whole % 60:00}";
    }

    private static string ScoreDetails(IReadOnlyDictionary<string, object?> option)
    {
        var breakdown = HostValues.Map(option, "immediate_breakdown");
        if (breakdown.Count == 0) breakdown = HostValues.Map(option, "score_breakdown");
        if (Text(breakdown, "scope") != "subject-current-immediate") return "";
        var labels = new (string Key, string Label)[]
        {
            ("information_component", "信息"), ("verification_component", "核验"),
            ("progress_component", "进展"), ("achievement_component", "达成"),
            ("weighted_opportunity", "机会"), ("time_penalty", "时间扣分")
        };
        var parts = labels.Where(item => breakdown.ContainsKey(item.Key))
            .Select(item => item.Label + " " + Score(breakdown[item.Key]));
        return "即时分项：" + string.Join(" · ", parts) +
            (breakdown.TryGetValue("total", out var total) ? " · 合计 " + Score(total) : "");
    }

    private static string MemberTitle(IReadOnlyDictionary<string, object?> member, bool objective)
    {
        var id = EntityId(member);
        var title = new[] { Text(member, "title"), Text(member, "label"), Text(member, "name") }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value != id) ?? "（未记录名称）";
        if (objective && title.StartsWith("自己的", StringComparison.Ordinal))
        {
            var owner = Text(member, "owner_title");
            title = (owner.Length > 0 ? owner + "的" : "") + title[3..];
        }
        if (objective && title is "同伴" or "本人")
            title = Text(member, "objective_title", Text(member, "true_name", "（未记录名称）"));
        return title;
    }

    private static bool IsPerson(string kind) => kind is "person" or "actor" or "character" or "npc";
    private static bool IsMapMember(IReadOnlyDictionary<string, object?> member)
    {
        var kind = Text(member, "kind");
        return IsPerson(kind) || kind is "" or "object" or "item" or "physical" or "physical_object" or
            "container" or "furniture" or "prop" or "equipment" or "door" or "passage" or "evidence_object";
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

    private static string KindLabel(string kind) => kind switch
    {
        "move" => "移动", "observe" or "inspect" => "检视", "talk" or "speak" or "communicate" => "交谈",
        "recall" => "回想", "replay" => "重演", "infer" or "reason" => "推理", "wait" => "等待",
        "interact" => "互动", "experiment" => "实验", "collect" => "收取", _ => ""
    };

    private static (string Label, string Disambiguator)[] OptionLabels(IReadOnlyList<IReadOnlyDictionary<string, object?>> options)
    {
        var labels = options.Select(option => (Label: Text(option, "label").Trim(), Disambiguator: "")).ToArray();
        for (var i = 0; i < labels.Length; i++)
            if (labels[i].Label.Length == 0 || labels[i].Label == Text(options[i], "id")) labels[i].Label = "（未记录标签）";
        foreach (var group in Enumerable.Range(0, labels.Length).GroupBy(index => labels[index].Label, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1).Select(group => group.ToArray()))
        {
            var hints = group.ToDictionary(index => index, index =>
            {
                var option = options[index];
                var context = Text(option, "context_label", Text(option, "target_title", Text(option, "location_title")));
                return string.Join(" · ", new[] { context, KindLabel(Text(option, "kind")), Cost(option.GetValueOrDefault("cost")) }
                    .Where(value => value.Length > 0).Distinct(StringComparer.Ordinal));
            });
            for (var ordinal = 0; ordinal < group.Length; ordinal++)
            {
                var index = group[ordinal];
                var hint = hints[index];
                var unique = hint.Length > 0 && group.Count(other => hints[other] == hint) == 1;
                labels[index].Disambiguator = unique ? hint : "候选 " + (ordinal + 1).ToString(CultureInfo.InvariantCulture);
            }
        }
        return labels;
    }

    internal static string VoiceHeading(string speaker, string voice)
    {
        var label = voice switch
        {
            "" or "dialogue" => "",
            "visible-action" => "现场记录",
            "visible-inspection" => "观察记录",
            "execution-feedback" => "行动反馈",
            "formal-receipt" => "答辩回执",
            "bodily-flash" => "本人回想",
            _ => "叙述"
        };
        return label.Length == 0 ? speaker : speaker.Length == 0 || speaker == label ? label : label + " · " + speaker;
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
            actorTitle = actors.FirstOrDefault(actor => actor.Id == viewer)?.Title ?? "（未记录名称）";
        var recordedObserver = HostValues.Map(previewData, "observers").GetValueOrDefault(viewer) as IReadOnlyDictionary<string, object?>;
        var previewViewer = Text(previewData, "viewer", Text(HostValues.Map(previewData, "snapshot"), "viewer"));
        var subjective = recordedObserver is not null
            ? HostValues.Map(recordedObserver, "subjective")
            : viewer.Length > 0 && previewViewer == viewer ? HostValues.Map(previewData, "subjective") : HostValues.Empty;
        var objective = HostValues.Map(previewData, "objective");
        var comparisons = mapView == "both" ? objective : HostValues.Empty;
        var rooms = mapView == "objective" ? Array.Empty<TerminalRoom>() : subjective.Count > 0
            ? ProjectRooms(subjective, viewer, false, comparisons) : LegacyRooms(snapshot, status, viewer, actorTitle);
        var edges = mapView == "objective" ? Array.Empty<TerminalEdge>()
            : ProjectEdges(subjective.Count > 0 ? subjective : HostValues.Map(snapshot, "map_knowledge"));
        var labels = OptionLabels(options);
        var choices = options.Select((option, index) => new TerminalOption
        {
            Id = Text(option, "id"), Title = labels[index].Label, Disambiguator = labels[index].Disambiguator,
            Detail = Cost(option.GetValueOrDefault("cost")), HasCost = option.GetValueOrDefault("cost") is not null,
            Score = Score(option.GetValueOrDefault("ai_score") ?? option.GetValueOrDefault("score")),
            ScoreKind = Text(option, "score_kind"), PlanningStatus = Text(option, "planning_status"),
            SelectionScore = option.GetValueOrDefault("selection_score") is null or false ? "" : Score(option["selection_score"]),
            SelectionClock = GameClock(option.GetValueOrDefault("selection_game_time")),
            ScoreDetail = ScoreDetails(option),
            Selected = Text(option, "id") == selected && selected.Length > 0
        }).ToArray();
        var clock = GameClock(snapshot.GetValueOrDefault("game_time") ?? status.GetValueOrDefault("game_time"), true);
        return new TerminalModel
        {
            Title = "DV CORE / 观察台", Mode = mode, Actor = viewer, ActorTitle = actorTitle, Actors = actors,
            Location = Text(snapshot, "location_title", Text(status, "location", "—")), Clock = clock, Phase = phase,
            Source = source, Rooms = rooms, Edges = edges, Options = choices, Entries = entries,
            ObjectiveRooms = mapView == "subjective" ? Array.Empty<TerminalRoom>() : ProjectRooms(objective, viewer, true),
            ObjectiveEdges = mapView == "subjective" ? Array.Empty<TerminalEdge>() : ProjectEdges(objective),
            Performance = HostValues.Map(previewData, "performance"), MapView = mapView,
            SubjectiveProjectionRecorded = subjective.Count > 0,
            OptionTimeBasis = Text(previewData, "option_time_basis", "current"), Footer = ""
        };
    }

    private static string RoomTitle(IReadOnlyDictionary<string, object?> room) =>
        Text(room, "label", Text(room, "title", Text(room, "room", "—")));

    private static string RoomRegion(IReadOnlyDictionary<string, object?> room)
    {
        var region = Text(room, "region");
        if (region.Length > 0) return region;
        var title = RoomTitle(room);
        var divider = title.IndexOf('·');
        return divider > 0 ? title[..divider].Trim() : "";
    }

    private static string ParentId(IReadOnlyDictionary<string, object?> member)
    {
        var carrier = Text(member, "carrier");
        return carrier.Length > 0 ? carrier : Text(member, "container");
    }

    private static IReadOnlyList<TerminalRoom> ProjectRooms(IReadOnlyDictionary<string, object?> map, string viewer,
        bool objective = false, IReadOnlyDictionary<string, object?>? comparison = null)
    {
        var comparisonMembers = new Dictionary<string, (string Room, IReadOnlyDictionary<string, object?> Member)>(StringComparer.Ordinal);
        if (comparison is not null)
            foreach (var room in Records(comparison, "rooms"))
                foreach (var member in Records(room, "members").Where(IsMapMember))
                {
                    var id = EntityId(member);
                    if (id.Length > 0) comparisonMembers.TryAdd(id, (Identity(room), member));
                }
        return Records(map, "rooms").Select(room =>
        {
            var source = Records(room, "members").Where(IsMapMember).ToArray();
            var byId = source.Where(member => EntityId(member).Length > 0)
                .GroupBy(EntityId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var nested = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in byId)
            {
                var parent = ParentId(pair.Value);
                if (parent.Length == 0 || parent == pair.Key || !byId.ContainsKey(parent)) continue;
                var ancestors = new HashSet<string>(StringComparer.Ordinal) { pair.Key };
                var cursor = parent;
                while (cursor.Length > 0 && byId.TryGetValue(cursor, out var ancestor) && ancestors.Add(cursor)) cursor = ParentId(ancestor);
                if (cursor.Length == 0 || !byId.ContainsKey(cursor)) nested[pair.Key] = parent;
            }
            var children = nested.GroupBy(pair => pair.Value, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(pair => byId[pair.Key])
                    .OrderBy(member => MemberTitle(member, objective), StringComparer.Ordinal)
                    .ThenBy(EntityId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

            string Difference(IReadOnlyDictionary<string, object?> member)
            {
                if (objective || !comparisonMembers.TryGetValue(EntityId(member), out var other)) return "";
                if (Identity(room) != other.Room) return "认知位置与客观不同";
                var parent = ParentId(member);
                var otherParent = ParentId(other.Member);
                return parent.Length > 0 && otherParent.Length > 0 && parent != otherParent ? "认知容纳关系与客观不同" : "";
            }

            string Contents(IReadOnlyDictionary<string, object?> member)
            {
                if (!children.TryGetValue(EntityId(member), out var items)) return "";
                var carried = items.Where(item => Text(item, "carrier") == EntityId(member)).Select(Inline).ToArray();
                var contained = items.Where(item => Text(item, "carrier") != EntityId(member)).Select(Inline).ToArray();
                return string.Join("；", new[]
                {
                    carried.Length > 0 ? "携带：" + string.Join("、", carried) : "",
                    contained.Length > 0 ? "内含：" + string.Join("、", contained) : ""
                }.Where(value => value.Length > 0));
            }

            string Inline(IReadOnlyDictionary<string, object?> member)
            {
                var owner = Text(member, "owner_title");
                var detail = string.Join("；", new[]
                {
                    Text(member, "detail"),
                    owner.Length > 0 && !IsPerson(Text(member, "kind")) ? "所属：" + owner : "",
                    Contents(member), Difference(member)
                }.Where(value => value.Length > 0).Distinct(StringComparer.Ordinal));
                return MemberTitle(member, objective) + (detail.Length > 0 ? "（" + detail + "）" : "");
            }

            string Details(IReadOnlyDictionary<string, object?> member)
            {
                var details = new List<string>();
                var recorded = Text(member, "detail");
                if (recorded.Length > 0) details.Add(recorded);
                var owner = Text(member, "owner_title");
                if (owner.Length > 0 && !IsPerson(Text(member, "kind"))) details.Add("所属：" + owner);
                var parent = ParentId(member);
                if (parent.Length > 0 && !nested.ContainsKey(EntityId(member)))
                {
                    var parentTitle = byId.TryGetValue(parent, out var knownParent) ? MemberTitle(knownParent, objective) : "";
                    details.Add(Text(member, "carrier").Length > 0
                        ? parentTitle.Length > 0 ? "携带者：" + parentTitle : "携带者未具名"
                        : parentTitle.Length > 0 ? "容器：" + parentTitle : "位置：容器内");
                }
                var contents = Contents(member);
                if (contents.Length > 0) details.Add(contents);
                var difference = Difference(member);
                if (difference.Length > 0) details.Add(difference);
                return string.Join(" · ", details.Distinct(StringComparer.Ordinal));
            }

            var members = source.Where(member => !nested.ContainsKey(EntityId(member)))
                .Select(member => new TerminalMember
                {
                    Id = EntityId(member), Title = MemberTitle(member, objective), Kind = Text(member, "kind"), Detail = Details(member),
                    IsViewer = viewer.Length > 0 && EntityId(member) == viewer || member.GetValueOrDefault("is_viewer") is true
                }).OrderBy(member => IsPerson(member.Kind) ? 0 : 1)
                .ThenBy(member => member.Title, StringComparer.Ordinal).ThenBy(member => member.Id, StringComparer.Ordinal).ToArray();
            return new TerminalRoom
            {
                Id = Identity(room), Title = RoomTitle(room), Region = RoomRegion(room),
                IsCurrent = room.GetValueOrDefault("is_current") is bool isCurrent ? isCurrent : members.Any(member => member.IsViewer),
                Members = members
            };
        }).OrderBy(room => room.Region, StringComparer.Ordinal).ThenBy(room => room.Title, StringComparer.Ordinal)
            .ThenBy(room => room.Id, StringComparer.Ordinal).ToArray();
    }

    private static string DoorState(IReadOnlyDictionary<string, object?> door)
    {
        var label = Text(door, "state_label").Trim().Trim('[', ']');
        if (label.Length > 0) return label is "unknown" or "未明" ? "未知" : label;
        return Text(door, "state") switch
        {
            "open" or "opened" => "开", "closed" or "shut" => "关", "locked" => "锁",
            "blocked" or "sealed" => "封闭", "conflicted" => "有争议", "unknown" or "" => "未知",
            "开" => "开", "关" => "关", "锁" => "锁", _ => "未知"
        };
    }

    private static IReadOnlyList<TerminalEdge> ProjectEdges(IReadOnlyDictionary<string, object?> map) =>
        Records(map, "doors").Select(door => new TerminalEdge
        {
            From = Identity(HostValues.Map(door, "from")), To = Identity(HostValues.Map(door, "to")), State = DoorState(door)
        }).OrderBy(edge => edge.From, StringComparer.Ordinal).ThenBy(edge => edge.To, StringComparer.Ordinal).ToArray();

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
        var projected = new List<object?>();
        foreach (var room in known)
        {
            var here = Identity(room) == current;
            var members = Records(room, "members").ToList();
            if (here)
            {
                foreach (var person in Records(snapshot, "people").Where(person => Identity(person) == current))
                {
                    var id = EntityId(person);
                    if (id.Length > 0 && members.Any(member => EntityId(member) == id)) continue;
                    members.Add(HostValues.Freeze(new Dictionary<string, object?>(person, StringComparer.Ordinal)
                    { ["id"] = id, ["kind"] = "person", ["is_viewer"] = id == viewer }));
                }
                if (!members.Any(member => EntityId(member) == viewer) && actorTitle.Length > 0)
                    members.Add(HostValues.Freeze(new Dictionary<string, object?>
                    { ["id"] = viewer, ["title"] = actorTitle, ["kind"] = "person", ["is_viewer"] = true }));
            }
            projected.Add(HostValues.Freeze(new Dictionary<string, object?>(room, StringComparer.Ordinal)
            { ["members"] = members.ToArray(), ["is_current"] = here }));
        }
        return ProjectRooms(HostValues.Freeze(new Dictionary<string, object?> { ["rooms"] = projected.ToArray() }), viewer);
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
