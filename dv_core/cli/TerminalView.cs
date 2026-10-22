using System.Globalization;
using System.Security;
using System.Text;

public sealed record TerminalModel
{
    public string Title { get; init; } = "DV CORE / 观察台";
    public string Mode { get; init; } = "未记录";
    public string Actor { get; init; } = "未记录";
    public string Location { get; init; } = "未记录";
    public string Clock { get; init; } = "未记录";
    public string Phase { get; init; } = "未记录";
    public string Source { get; init; } = "未记录";
    public string Footer { get; init; } = "命令：未记录";
    public bool IsPreview { get; init; }
    public string Zoom { get; init; } = "fit";
    public string MapView { get; init; } = "subjective";
    public string ActorTitle { get; init; } = "";
    public bool ShowActors { get; init; }
    public bool? ShowScores { get; init; }
    public IReadOnlyDictionary<string, object?> Performance { get; init; } = HostValues.Empty;
    public int PageIndex { get; init; }
    public bool Paginate { get; init; }
    public IReadOnlyList<TerminalRoom> Rooms { get; init; } = Array.Empty<TerminalRoom>();
    public IReadOnlyList<TerminalEdge> Edges { get; init; } = Array.Empty<TerminalEdge>();
    public IReadOnlyList<TerminalRoom> ObjectiveRooms { get; init; } = Array.Empty<TerminalRoom>();
    public IReadOnlyList<TerminalEdge> ObjectiveEdges { get; init; } = Array.Empty<TerminalEdge>();
    public IReadOnlyList<TerminalActor> Actors { get; init; } = Array.Empty<TerminalActor>();
    public IReadOnlyList<TerminalOption> Options { get; init; } = Array.Empty<TerminalOption>();
    public IReadOnlyList<TerminalEntry> Entries { get; init; } = Array.Empty<TerminalEntry>();
}

public sealed record TerminalRoom
{
    public string Id { get; init; } = "";
    public string Region { get; init; } = "";
    public string Title { get; init; } = "未记录";
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TerminalMember> Members { get; init; } = Array.Empty<TerminalMember>();
    public bool IsCurrent { get; init; }
}

public sealed record TerminalMember
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool IsViewer { get; init; }
}

public sealed record TerminalActor
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public bool Selected { get; init; }
}

public sealed record TerminalEdge
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public string State { get; init; } = "状态未记录";
}

public sealed record TerminalOption
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "未记录";
    public string Disambiguator { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool HasCost { get; init; }
    public string Score { get; init; } = "—";
    public string ScoreKind { get; init; } = "";
    public string PlanningStatus { get; init; } = "";
    public bool Selected { get; init; }
}

public sealed record TerminalEntry
{
    public string Title { get; init; } = "未记录";
    public string Text { get; init; } = "未记录";
    public string Kind { get; init; } = "";
}

/// <summary>Pure observer presentation. ANSI and SVG share the same cell layout.</summary>
public static class TerminalView
{
    private const string Background = "#0b1424", Ink = "#eadfca";
    private const string Teal = "#65d8c4", Muted = "#93a8bb", Amber = "#edbd72";
    private sealed record Line(string Text, string Color = Ink, bool Bold = false,
        string Scope = "", Line? Parent = null, bool KeepWithNext = false);
    private sealed record Block(string Title, List<Line> Lines, bool Complete = true);
    private sealed record RoomBlock(string Region, List<Line> Lines);
    private sealed record Cell(string Glyph, string Color, string Back, bool Continuation = false, bool Bold = false);
    private sealed record Placement(int X, int Width, Line Value);
    private sealed class Row : List<Placement>
    {
        public Row() { }
        public Row(IEnumerable<Placement> values) : base(values) { }
    }
    private sealed record Page(int Start, int End, List<Row> Rows);

    public static string Render(TerminalModel model, int width = 120, int height = 40, bool color = true)
    {
        var canvas = Layout(model, width, height);
        var output = new StringBuilder();
        string? foreground = null, background = null;
        var bold = false;
        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                var cell = canvas.Cells[y, x];
                if (cell.Continuation) continue;
                if (color && (cell.Color != foreground || cell.Back != background))
                {
                    output.Append(Ansi(cell.Color, false)).Append(Ansi(cell.Back, true));
                    foreground = cell.Color;
                    background = cell.Back;
                }
                if (color && cell.Bold != bold)
                {
                    output.Append(cell.Bold ? "\u001b[1m" : "\u001b[22m");
                    bold = cell.Bold;
                }
                output.Append(cell.Glyph);
            }
            if (color) { output.Append("\u001b[0m"); foreground = background = null; bold = false; }
            if (y + 1 < canvas.Height) output.Append('\n');
        }
        return output.ToString();
    }

    public static (int Index, int Count) GetPageInfo(TerminalModel model, int width = 120, int height = 40)
    {
        var canvas = Layout(model, width, height);
        return (canvas.PageIndex, canvas.PageCount);
    }

    public static string ExportSvg(TerminalModel model, int width = 120, int height = 40)
    {
        var canvas = Layout(model, width, height);
        const int cw = 10, ch = 20;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{canvas.Width * cw}\" height=\"{canvas.Height * ch}\" viewBox=\"0 0 {canvas.Width * cw} {canvas.Height * ch}\" role=\"img\" aria-label=\"DV terminal cell rendering\">\n");
        svg.Append("<title>").Append(Xml(model.IsPreview ? "预览" : model.Title)).Append("</title>\n");
        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width;)
            {
                var start = x;
                var back = canvas.Cells[y, x].Back;
                while (x < canvas.Width && canvas.Cells[y, x].Back == back) x++;
                svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{start * cw}\" y=\"{y * ch}\" width=\"{(x - start) * cw}\" height=\"{ch}\" fill=\"{back}\"/>\n");
            }
        }
        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width;)
            {
                var first = canvas.Cells[y, x];
                if (first.Continuation || first.Glyph == " ") { x++; continue; }
                var start = x;
                var run = new StringBuilder();
                var contentLength = 0;
                var end = start;
                while (x < canvas.Width)
                {
                    var cell = canvas.Cells[y, x];
                    if (cell.Color != first.Color || cell.Back != first.Back || cell.Bold != first.Bold) break;
                    if (!cell.Continuation) run.Append(cell.Glyph);
                    x++;
                    if (cell.Continuation || cell.Glyph != " ")
                    {
                        contentLength = run.Length;
                        end = x;
                    }
                }
                run.Length = contentLength;
                if (run.Length == 0) continue;
                svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{start * cw}\" y=\"{y * ch + 15}\" textLength=\"{(end - start) * cw}\" lengthAdjust=\"spacingAndGlyphs\" fill=\"{first.Color}\" font-size=\"16\" font-weight=\"{(first.Bold ? 700 : 400)}\" font-family=\"Cascadia Mono, Noto Sans Mono CJK SC, Microsoft YaHei, monospace\" xml:space=\"preserve\">");
                svg.Append(Xml(run.ToString())).Append("</text>\n");
            }
        }
        return svg.Append("</svg>\n").ToString();
    }

    private static Canvas Layout(TerminalModel model, int requestedWidth, int requestedHeight)
    {
        var width = Math.Clamp(requestedWidth, 60, 300);
        var inner = width - 4;
        var rows = new List<Row>();
        void Add(IEnumerable<Line> lines)
        {
            foreach (var line in lines) rows.Add(new Row { new(2, inner, line) });
        }
        void AddColumns(IReadOnlyList<Line> left, IReadOnlyList<Line> right, int leftWidth, int rightWidth)
        {
            for (var i = 0; i < Math.Max(left.Count, right.Count); i++)
            {
                var row = new Row();
                if (i < left.Count) row.Add(new(2, leftWidth, left[i]));
                if (i < right.Count) row.Add(new(6 + leftWidth, rightWidth, right[i]));
                rows.Add(row);
            }
        }
        void AddMap(string title, IReadOnlyList<TerminalRoom> rooms, IReadOnlyList<TerminalEdge> edges)
        {
            if (width < 118 || rooms.Count < 2)
            {
                Add(MapLines(title, rooms, edges, model, inner, requestedHeight));
                return;
            }
            var leftWidth = (inner - 4) / 2;
            var rightWidth = inner - leftWidth - 4;
            var blocks = RoomBlocks(title, rooms, model, leftWidth, requestedHeight);
            if (blocks.Count < 2)
            {
                Add(MapLines(title, rooms, edges, model, inner, requestedHeight));
                return;
            }
            Add(new[] { new Line(title, Muted, KeepWithNext: true) });
            var total = blocks.Sum(block => block.Lines.Count);
            var running = 0;
            var split = 1;
            var distance = int.MaxValue;
            for (var i = 1; i < blocks.Count; i++)
            {
                running += blocks[i - 1].Lines.Count;
                var candidate = Math.Abs(total - running * 2);
                if (candidate < distance) { distance = candidate; split = i; }
            }
            var left = blocks.Take(split).SelectMany(block => block.Lines).ToList();
            var right = blocks.Skip(split).SelectMany(block => block.Lines).ToList();
            var rightRegion = blocks[split].Region;
            if (rightRegion.Length > 0 && right.Count > 0 && right[0].Text != rightRegion)
                right.Insert(0, new Line(rightRegion, Teal, true, title + " · " + rightRegion, KeepWithNext: true));
            AddColumns(left, right, leftWidth, rightWidth);
            Add(new[] { new Line("") });
            Add(PassageLines(title, rooms, edges, inner));
        }

        Add(Lines(model.Title, inner, Teal, true));
        var actorTitle = string.IsNullOrWhiteSpace(model.ActorTitle) ? model.Actor : model.ActorTitle;
        var status = new[] { model.IsPreview ? "预览" : Known(model.Mode), Known(model.Clock),
            string.IsNullOrWhiteSpace(actorTitle) ? "" : "视角 " + actorTitle, PhaseLabel(model.Phase) };
        Add(Lines(string.Join("  ·  ", status.Where(value => value.Length > 0)), model.Paginate ? inner - 12 : inner, Muted));
        var statusRow = rows.Count - 1;
        if (model.Performance.Count > 0) Add(Lines(PerformanceLine(model.Performance), inner, Muted));
        Add(new[] { new Line("") });
        var headerRows = rows.Count;
        if (model.ShowActors)
        {
            Add(new[] { new Line("视角", Teal, true, KeepWithNext: true) });
            foreach (var actor in model.Actors)
                Add(Lines((actor.Selected ? "> " : "· ") + actor.Title, inner, actor.Selected ? Teal : Ink));
            if (model.Actors.Count == 0) Add(new[] { new Line("—", Muted) });
            Add(new[] { new Line("") });
        }
        var showCompleteChoices = !model.Paginate;
        if (model.Paginate && model.Options.Count > 0)
        {
            var choices = Choices(model, inner, true);
            showCompleteChoices = !choices.Complete;
            Add(Lines(choices.Title, inner, Teal, true).Select(line => line with { KeepWithNext = true }));
            Add(choices.Lines);
            Add(new[] { new Line("") });
        }
        if (model.MapView == "both" && width >= 118)
        {
            var leftWidth = (inner - 4) / 2;
            var rightWidth = inner - leftWidth - 4;
            AddColumns(MapLines("客观世界", model.ObjectiveRooms, model.ObjectiveEdges, model, leftWidth, requestedHeight),
                MapLines("主观认知", model.Rooms, model.Edges, model, rightWidth, requestedHeight), leftWidth, rightWidth);
        }
        else
        {
            if (model.MapView is "objective" or "both") AddMap("客观世界", model.ObjectiveRooms, model.ObjectiveEdges);
            if (model.MapView == "both") Add(new[] { new Line("") });
            if (model.MapView != "objective") AddMap("主观认知", model.Rooms, model.Edges);
        }
        if (showCompleteChoices && model.Options.Count > 0)
        {
            var choices = Choices(model, inner);
            Add(new[] { new Line("") });
            Add(Lines(choices.Title, inner, Teal, true).Select(line => line with { KeepWithNext = true }));
            Add(choices.Lines);
        }
        if (model.Entries.Count > 0)
        {
            Add(new[] { new Line(""), new Line("记录", Muted, KeepWithNext: true) });
            Add(Events(model, inner).Lines);
        }
        var canvasHeight = rows.Count + 2;
        var pageIndex = 0;
        var pageCount = 1;
        if (model.Paginate)
        {
            canvasHeight = Math.Clamp(requestedHeight, 24, 100);
            var pages = Paginate(rows, headerRows, canvasHeight - headerRows - 2);
            pageCount = pages.Count;
            var requestedPage = model.PageIndex;
            if (requestedPage < 0)
            {
                var selectedRow = rows.FindIndex(headerRows, row => row.Any(item => item.Value.Text.StartsWith("> ", StringComparison.Ordinal)));
                requestedPage = pages.FindIndex(page => selectedRow >= page.Start && selectedRow < page.End);
            }
            pageIndex = Math.Clamp(requestedPage, 0, pageCount - 1);
            var marker = $"{pageIndex + 1}/{pageCount}";
            rows[statusRow].Add(new(2 + inner - marker.Length, marker.Length, new Line(marker, Muted)));
            rows = rows.Take(headerRows).Concat(pages[pageIndex].Rows).ToList();
        }
        var canvas = new Canvas(width, canvasHeight, pageIndex, pageCount);
        for (var i = 0; i < rows.Count; i++)
            foreach (var item in rows[i]) canvas.Put(item.X, i + 1, item.Value.Text, item.Value.Color, item.Width, item.Value.Bold);
        return canvas;
    }

    private static List<Page> Paginate(List<Row> rows, int headerRows, int capacity)
    {
        capacity = Math.Max(1, capacity);
        var pages = new List<Page>();
        var start = headerRows;
        while (start < rows.Count)
        {
            while (start < rows.Count && rows[start].All(item => string.IsNullOrWhiteSpace(item.Value.Text))) start++;
            if (start == rows.Count) break;
            var continuation = new List<Row>();
            if (pages.Count > 0)
            {
                var columns = rows.Skip(start).Take(capacity).SelectMany(row => row)
                    .Where(item => !string.IsNullOrWhiteSpace(item.Value.Text))
                    .GroupBy(item => (item.X, item.Width)).Select(group => group.First()).ToArray();
                var scopes = new Row(columns.Where(item => item.Value.Scope.Length > 0 && item.Value.Text != item.Value.Scope)
                    .Select(item => new Placement(item.X, item.Width, new Line(item.Value.Scope, Muted, KeepWithNext: true))));
                var parents = new Row(columns.Where(item => item.Value.Parent is not null)
                    .Select(item => new Placement(item.X, item.Width, item.Value.Parent!)));
                if (scopes.Count > 0 && capacity > 2) continuation.Add(scopes);
                if (parents.Count > 0 && capacity > 2) continuation.Add(parents);
            }
            var available = Math.Max(1, capacity - continuation.Count);
            var end = Math.Min(rows.Count, start + available);
            if (end < rows.Count)
            {
                while (end > start + 1 && rows[end - 1].Any(item => item.Value.KeepWithNext)) end--;
            }
            var pageRows = continuation.Concat(rows.Skip(start).Take(end - start)).ToList();
            pages.Add(new(start, end, pageRows));
            start = end;
        }
        if (pages.Count == 0) pages.Add(new(headerRows, headerRows, new List<Row>()));
        return pages;
    }

    private static List<Line> MapLines(string title, IReadOnlyList<TerminalRoom> rooms,
        IReadOnlyList<TerminalEdge> edges, TerminalModel model, int width, int height)
    {
        var content = new List<Line> { new(title, Muted, Scope: title, KeepWithNext: true) };
        content.AddRange(RoomBlocks(title, rooms, model, width, height).SelectMany(block => block.Lines));
        if (rooms.Count == 0) content.Add(new("—", Muted, Scope: title));
        while (content.Count > 0 && content[^1].Text.Length == 0) content.RemoveAt(content.Count - 1);
        content.Add(new("", Scope: title));
        content.AddRange(PassageLines(title, rooms, edges, width));
        return content;
    }

    private static List<RoomBlock> RoomBlocks(string title, IReadOnlyList<TerminalRoom> rooms,
        TerminalModel model, int width, int height)
    {
        var blocks = new List<RoomBlock>();
        var spacing = model.Zoom == "detail" || model.Zoom == "fit" && rooms.Count * 5 < height;
        foreach (var region in rooms.GroupBy(RoomRegion, StringComparer.Ordinal)
                     .OrderBy(group => RegionOrder(group.Key)).ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            var scope = region.Key.Length == 0 ? title : title + " · " + region.Key;
            var firstInRegion = true;
            List<Line> BeginBlock()
            {
                var lines = new List<Line>();
                if (firstInRegion && region.Key.Length > 0)
                    lines.Add(new(region.Key, Teal, true, scope, KeepWithNext: true));
                firstInRegion = false;
                return lines;
            }
            var sorted = region.OrderBy(room => RoomName(room), StringComparer.Ordinal).ThenBy(room => room.Id, StringComparer.Ordinal).ToArray();
            foreach (var room in sorted.Where(room => !IsEmpty(room)))
            {
                var lines = BeginBlock();
                var heading = new Line("- " + RoomName(room), room.IsCurrent ? Teal : Ink, true, scope, KeepWithNext: true);
                lines.AddRange(Indented("- ", RoomName(room), width, heading.Color, true)
                    .Select((line, index) => line with { Scope = scope, Parent = index == 0 ? null : heading, KeepWithNext = true }));
                var members = room.Members.ToList();
                if (room.IsCurrent && !members.Any(member => member.IsViewer))
                    members.Add(new TerminalMember { Id = model.Actor, Title = string.IsNullOrWhiteSpace(model.ActorTitle) ? model.Actor : model.ActorTitle,
                        Kind = "actor", IsViewer = true });
                foreach (var member in members.OrderBy(member => member.Kind is "actor" or "person" || member.IsViewer ? 0 : 1)
                             .ThenBy(member => member.Title, StringComparer.Ordinal).ThenBy(member => member.Id, StringComparer.Ordinal))
                {
                    lines.AddRange(Indented("    ", Missing(member.Title), width, member.IsViewer ? Teal : Ink, true)
                        .Select(line => line with { Scope = scope, Parent = heading }));
                    if (!string.IsNullOrWhiteSpace(member.Detail))
                        lines.AddRange(Indented("      ", member.Detail, width, Muted)
                            .Select(line => line with { Scope = scope, Parent = heading }));
                }
                foreach (var detail in room.Lines.Where(line => !string.IsNullOrWhiteSpace(line)))
                    lines.AddRange(Indented("    ", detail, width, Muted).Select(line => line with { Scope = scope, Parent = heading }));
                if (spacing) lines.Add(new("", Scope: scope));
                blocks.Add(new(region.Key, lines));
            }
            var empty = sorted.Where(IsEmpty).ToArray();
            if (empty.Length > 0)
            {
                var lines = BeginBlock();
                var prefix = title == "客观世界" ? "空：" : "未记录成员：";
                lines.AddRange(Indented("  ", prefix + string.Join("、", empty.Select(RoomName)), width, Muted)
                    .Select(line => line with { Scope = scope }));
                blocks.Add(new(region.Key, lines));
            }
        }
        return blocks;
    }

    private static bool IsEmpty(TerminalRoom room) => !room.IsCurrent && room.Members.Count == 0 && room.Lines.All(string.IsNullOrWhiteSpace);
    private static string RoomRegion(TerminalRoom room)
    {
        if (!string.IsNullOrWhiteSpace(room.Region)) return room.Region.Trim();
        var separator = room.Title.IndexOf('·');
        return separator > 0 ? room.Title[..separator].Trim() : "";
    }
    private static int RegionOrder(string region)
    {
        if (region.Length == 0) return 100;
        var index = "甲乙丙丁戊己庚辛壬癸".IndexOf(region[0]);
        return index >= 0 ? index : 20;
    }
    private static string RoomName(TerminalRoom room)
    {
        var title = Missing(room.Title);
        var region = RoomRegion(room);
        var separator = title.IndexOf('·');
        return separator > 0 && title[..separator].Trim() == region ? title[(separator + 1)..].Trim() : title;
    }

    private static IEnumerable<Line> PassageLines(string title, IReadOnlyList<TerminalRoom> rooms,
        IReadOnlyList<TerminalEdge> edges, int width)
    {
        var passages = new Line("通道", Muted, Scope: title, KeepWithNext: true);
        yield return passages;
        var names = rooms.GroupBy(room => room.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Title.Replace(" · ", " "), StringComparer.Ordinal);
        foreach (var edge in edges.OrderBy(edge => names.GetValueOrDefault(edge.From, edge.From), StringComparer.Ordinal)
                     .ThenBy(edge => names.GetValueOrDefault(edge.To, edge.To), StringComparer.Ordinal))
        {
            var from = names.GetValueOrDefault(edge.From, "（未记录房间）");
            var to = names.GetValueOrDefault(edge.To, "（未记录房间）");
            var state = string.IsNullOrWhiteSpace(edge.State) || edge.State == "状态未记录" ? "" : "  [" + edge.State + "]";
            foreach (var line in Indented("  ", from + " <-> " + to + state, width, Muted))
                yield return line with { Scope = title, Parent = passages };
        }
        if (edges.Count == 0) yield return new("—", Muted, Scope: title, Parent: passages);
    }

    private static string PerformanceLine(IReadOnlyDictionary<string, object?> performance)
    {
        static string Number(IReadOnlyDictionary<string, object?> values, string key, double divisor = 1)
        {
            var text = Convert.ToString(values.GetValueOrDefault(key), CultureInfo.InvariantCulture);
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                ? (value / divisor).ToString("0.###", CultureInfo.InvariantCulture) : "—";
        }
        return "每游戏小时 " + Number(performance, "seconds_per_game_hour") + "秒  ·  累计引擎 " +
            Number(performance, "engine_ms", 1000) + "秒  ·  推进 " + Number(performance, "advanced_game_minutes") + "游戏分钟";
    }

    private static bool ScoresVisible(TerminalModel model) => model.ShowScores ??
        !(model.Mode.Contains("人工", StringComparison.Ordinal) || model.Mode.Contains("manual", StringComparison.OrdinalIgnoreCase));
    private static string Known(string value) => value is "未记录" or "时间未记录" ? "" : value;
    private static string PhaseLabel(string value) => value switch
    {
        "focus" or "focused" => "定位", "confirm" or "confirmed" => "确认",
        "awaiting_player" or "awaiting_input" => "待选", "prepared" => "待确认",
        "applied" or "completed" => "已执行", "未记录" or "未准备" or "只读" => "", _ => value
    };

    private static Block Events(TerminalModel model, int width)
    {
        var result = new List<Line>();
        foreach (var entry in model.Entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Title))
                result.AddRange(Lines(entry.Title, width, entry.Kind == "error" ? Amber : Muted)
                    .Select(line => line with { KeepWithNext = !string.IsNullOrWhiteSpace(entry.Text) }));
            if (!string.IsNullOrWhiteSpace(entry.Text)) result.AddRange(Lines(entry.Text, width));
            result.Add(new(""));
        }
        return new Block("事件 / 观察正文", result);
    }

    private static Block Choices(TerminalModel model, int width, bool compact = false)
    {
        var result = new List<Line>();
        var commonCost = model.Options.Where(option => option.HasCost).GroupBy(option => option.Detail, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).FirstOrDefault();
        var hasSharedCost = commonCost is not null && commonCost.Count() > 1;
        var sharedCost = hasSharedCost ? commonCost!.Key : "";
        var selected = model.Options.ToList().FindIndex(option => option.Selected);
        var count = compact ? Math.Min(5, model.Options.Count) : model.Options.Count;
        var start = compact ? Math.Clamp(Math.Max(0, selected) - count / 2, 0, model.Options.Count - count) : 0;
        var complete = count == model.Options.Count;
        var title = "行动" + (compact && count < model.Options.Count ? $"  {start + 1:00}–{start + count:00}/{model.Options.Count:00}" : "") +
            (sharedCost.Length > 0 ? "  ·  " + sharedCost : "");
        for (var index = start; index < start + count; index++)
        {
            var option = model.Options[index];
            var prefix = (option.Selected ? "> " : "· ") + $"{index + 1:00}  ";
            var cost = option.HasCost
                ? hasSharedCost && option.Detail == sharedCost ? "" : option.Detail.Length == 0 ? "费用 0" : option.Detail
                : hasSharedCost ? "费用未记录" : "";
            var metadata = string.Join("  ", new[] { cost, ScoresVisible(model) ? OptionScore(option) : "" }.Where(value => value.Length > 0));
            var labelWidth = Math.Max(TextWidth(prefix) + 2, width - TextWidth(metadata) - (metadata.Length == 0 ? 0 : 2));
            var fullLabel = Missing(option.Title) + (option.Disambiguator.Length == 0 ? "" : " [" + option.Disambiguator + "]");
            var label = compact ? SingleLine(fullLabel, labelWidth - TextWidth(prefix)) : fullLabel;
            if (compact && label != fullLabel) complete = false;
            var lines = Indented(prefix, label, labelWidth, option.Selected ? Teal : Ink, option.Selected).ToList();
            var first = lines[0] with { Text = lines[0].Text + (metadata.Length == 0 ? "" : "  " + metadata), Scope = title };
            result.Add(first);
            if (!compact)
                result.AddRange(lines.Skip(1).Select(line => line with { Scope = title, Parent = first }));
            if (!compact && model.Zoom == "detail") result.Add(new("", Scope: title));
        }
        return new Block(title, result, complete);
    }

    private static string OptionScore(TerminalOption option)
    {
        if (option.PlanningStatus == "unexpanded") return "AI 未展开";
        var score = string.IsNullOrWhiteSpace(option.Score) ? "—" : option.Score;
        var kind = option.ScoreKind switch
        {
            "recorded-root" => "根", "recorded-choice" => "选择", "immediate" => "即时",
            "" => "口径未记录", _ => option.ScoreKind
        };
        return "AI " + score + "(" + kind + ")";
    }

    private static int TextWidth(string value)
    {
        var width = 0;
        foreach (var rune in Clean(value).EnumerateRunes()) width += RuneWidth(rune);
        return width;
    }

    private static string SingleLine(string value, int width)
    {
        if (width <= 0) return "";
        var text = Clean(value).Replace('\n', ' ');
        if (TextWidth(text) <= width) return text;
        var result = new StringBuilder();
        var used = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var size = RuneWidth(rune);
            if (used + size > width - 1) break;
            result.Append(rune);
            used += size;
        }
        return result.Append('…').ToString();
    }

    private static string Missing(string value) => string.IsNullOrWhiteSpace(value) ? "未记录" : value;
    private static IEnumerable<Line> Indented(string indent, string value, int width, string color = Ink, bool bold = false) =>
        Lines(value, Math.Max(1, width - TextWidth(indent)), color, bold).Select((line, index) =>
            line with { Text = (index == 0 ? indent : new string(' ', TextWidth(indent))) + line.Text });

    private static List<Line> Lines(string value, int width, string color = Ink, bool bold = false)
    {
        var result = new List<Line>();
        var line = new StringBuilder();
        var used = 0;
        foreach (var rune in Clean(Missing(value)).EnumerateRunes())
        {
            if (rune.Value == '\n') { result.Add(new(line.ToString(), color, bold)); line.Clear(); used = 0; continue; }
            var size = RuneWidth(rune);
            if (used + size > width && used > 0) { result.Add(new(line.ToString(), color, bold)); line.Clear(); used = 0; }
            line.Append(rune.ToString());
            used += size;
        }
        if (line.Length > 0 || result.Count == 0) result.Add(new(line.ToString(), color, bold));
        return result;
    }

    private static string Clean(string value)
    {
        var result = new StringBuilder();
        var state = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var n = rune.Value;
            if (state == 1) { state = n == '[' ? 2 : n == ']' || n == 'P' || n == '^' || n == '_' ? 3 : 0; continue; }
            if (state == 2) { if (n is >= 0x40 and <= 0x7e) state = 0; continue; }
            if (state == 3) { if (n == 7 || n == 0x9c) state = 0; else if (n == 27) state = 4; continue; }
            if (state == 4) { state = n == '\\' ? 0 : 3; continue; }
            if (n == 27) { state = 1; continue; }
            if (n == 0x9b) { state = 2; continue; }
            if (n == 0x9d) { state = 3; continue; }
            if (n == '\n') { result.Append('\n'); continue; }
            if (n == '\t') { result.Append("  "); continue; }
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator) continue;
            result.Append(rune.ToString());
        }
        return result.ToString();
    }

    private static int RuneWidth(Rune rune)
    {
        if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark) return 0;
        var n = rune.Value;
        return n == 0x2014 || n >= 0x1100 && (n <= 0x115f || n == 0x2329 || n == 0x232a ||
            n is >= 0x2e80 and <= 0xa4cf || n is >= 0xac00 and <= 0xd7a3 ||
            n is >= 0xf900 and <= 0xfaff || n is >= 0xfe10 and <= 0xfe19 ||
            n is >= 0xfe30 and <= 0xfe6f || n is >= 0xff01 and <= 0xff60 ||
            n is >= 0xffe0 and <= 0xffe6 || n is >= 0x1f300 and <= 0x1faff || n >= 0x20000) ? 2 : 1;
    }

    private static string Ansi(string hex, bool back) => $"\u001b[{(back ? 48 : 38)};2;{Convert.ToInt32(hex.Substring(1, 2), 16)};{Convert.ToInt32(hex.Substring(3, 2), 16)};{Convert.ToInt32(hex.Substring(5, 2), 16)}m";
    private static string Xml(string text) => SecurityElement.Escape(Clean(text)) ?? "";

    private sealed class Canvas
    {
        public int Width { get; }
        public int Height { get; }
        public int PageIndex { get; }
        public int PageCount { get; }
        public Cell[,] Cells { get; }
        public Canvas(int width, int height, int pageIndex = 0, int pageCount = 1)
        {
            Width = width; Height = height;
            PageIndex = pageIndex; PageCount = pageCount;
            Cells = new Cell[height, width];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++) Cells[y, x] = new(" ", Ink, Background);
        }
        public void Put(int x, int y, string text, string color, int available, bool bold = false)
        {
            if (y < 0 || y >= Height) return;
            var end = Math.Min(Width, x + available);
            foreach (var rune in Clean(text).EnumerateRunes())
            {
                if (rune.Value == '\n') break;
                var size = RuneWidth(rune);
                if (size == 0)
                {
                    var previous = x - 1;
                    while (previous >= 0 && Cells[y, previous].Continuation) previous--;
                    if (previous >= 0) Cells[y, previous] = Cells[y, previous] with { Glyph = Cells[y, previous].Glyph + rune };
                    continue;
                }
                if (x + size > end) break;
                Cells[y, x] = new(rune.ToString(), color, Cells[y, x].Back, Bold: bold);
                if (size == 2) Cells[y, x + 1] = new("", color, Cells[y, x + 1].Back, true, bold);
                x += size;
            }
        }
    }
}
