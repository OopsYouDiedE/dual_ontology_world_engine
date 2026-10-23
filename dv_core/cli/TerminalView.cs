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
    public string TranscriptNotice { get; init; } = "";
    public bool IsPreview { get; init; }
    public string Zoom { get; init; } = "fit";
    public string MapView { get; init; } = "subjective";
    public bool SubjectiveProjectionRecorded { get; init; } = true;
    public string ActorTitle { get; init; } = "";
    public bool ShowActors { get; init; }
    public bool? ShowScores { get; init; }
    public IReadOnlyDictionary<string, object?> Performance { get; init; } = HostValues.Empty;
    public int PageIndex { get; init; }
    public int ActionPageIndex { get; init; } = -1;
    public bool ExpandActions { get; init; }
    public string OptionTimeBasis { get; init; } = "current";
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
    public string SelectionScore { get; init; } = "";
    public string SelectionClock { get; init; } = "";
    public string ScoreDetail { get; init; } = "";
    public bool Selected { get; init; }
}

public sealed record TerminalEntry
{
    public string Title { get; init; } = "未记录";
    public string Text { get; init; } = "未记录";
    public string Kind { get; init; } = "";
    public string Id { get; init; } = "";
    public string Clock { get; init; } = "";
    public string Speaker { get; init; } = "";
    public string Voice { get; init; } = "";
    public string GroupId { get; init; } = "";
}

public sealed record TerminalRenderResult(string Text, string? Svg, int PageIndex, int PageCount,
    int ActionPageIndex, int ActionPageCount);

/// <summary>Pure observer presentation. ANSI and SVG share the same cell layout.</summary>
public static class TerminalView
{
    private const string Background = "#171914", Ink = "#e7dec7";
    private const string Teal = "#c9b88a", Muted = "#a7a293", Amber = "#cfab72";
    private sealed record Line(string Text, string Color = Ink, bool Bold = false,
        string Scope = "", Line? Parent = null, bool KeepWithNext = false);
    private sealed record Block(string Title, List<Line> Lines);
    private sealed record RoomBlock(string Region, List<Line> Lines);
    private readonly record struct Cell(string Glyph, string Color, string Back, bool Continuation = false, bool Bold = false);
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
        return Render(canvas, color);
    }

    /// <summary>Build one immutable display canvas for text, capture, and page metadata.</summary>
    public static TerminalRenderResult RenderFrame(TerminalModel model, int width = 120, int height = 40,
        bool color = true, bool includeSvg = false)
    {
        var canvas = Layout(model, width, height);
        var actions = GetActionPageInfo(model);
        return new TerminalRenderResult(Render(canvas, color), includeSvg ? ExportSvg(model, canvas) : null,
            canvas.PageIndex, canvas.PageCount, actions.Index, actions.Count);
    }

    private static string Render(Canvas canvas, bool color)
    {
        var output = new StringBuilder(canvas.Width * canvas.Height + canvas.Height);
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

    public static (int Index, int Count) GetActionPageInfo(TerminalModel model)
    {
        const int pageSize = 5;
        var count = Math.Max(1, (model.Options.Count + pageSize - 1) / pageSize);
        var selected = -1;
        for (var index = 0; index < model.Options.Count; index++)
            if (model.Options[index].Selected) { selected = index; break; }
        var requested = model.ActionPageIndex < 0 ? Math.Max(0, selected) / pageSize : model.ActionPageIndex;
        return (Math.Clamp(requested, 0, count - 1), count);
    }

    public static string ExportSvg(TerminalModel model, int width = 120, int height = 40)
    {
        var canvas = Layout(model, width, height);
        return ExportSvg(model, canvas);
    }

    private static string ExportSvg(TerminalModel model, Canvas canvas)
    {
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
        var entryGroups = EntryGroups(model.Entries);
        var latestEntries = entryGroups.Count > 0 ? entryGroups[^1].ToArray() : Array.Empty<TerminalEntry>();
        var earlierEntries = entryGroups.Take(Math.Max(0, entryGroups.Count - 1)).SelectMany(group => group).ToArray();
        var subjectiveTitle = model.SubjectiveProjectionRecorded ? "主观认知" : "已记录位置";
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
        Add(Lines(PerformanceLine(model), inner, Muted));
        if (!string.IsNullOrWhiteSpace(model.TranscriptNotice))
            Add(Lines(model.TranscriptNotice, inner, Muted));
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
        // The current event is the primary reading surface. Keep it full width
        // before candidates and maps; diagnostic detail remains expandable.
        if (model.Entries.Count > 0)
            Add(Events(model with { Entries = latestEntries }, inner).Lines);
        if (model.Options.Count > 0)
        {
            var choices = Choices(model, inner, model.Paginate && !model.ExpandActions);
            Add(Lines(choices.Title, inner, Teal, true).Select(line => line with { KeepWithNext = true }));
            Add(choices.Lines);
            Add(new[] { new Line("") });
        }
        if (model.MapView == "both")
        {
            if (width >= 100)
            {
                var leftWidth = (inner - 4) / 2;
                var rightWidth = inner - leftWidth - 4;
                AddColumns(MapLines("客观世界", model.ObjectiveRooms, model.ObjectiveEdges, model, leftWidth, requestedHeight),
                    MapLines(subjectiveTitle, model.Rooms, model.Edges, model, rightWidth, requestedHeight), leftWidth, rightWidth);
            }
            else
            {
                AddMap(subjectiveTitle, model.Rooms, model.Edges);
                Add(new[] { new Line("") });
                AddMap("客观世界", model.ObjectiveRooms, model.ObjectiveEdges);
            }
            if (earlierEntries.Length > 0)
            {
                Add(new[] { new Line("") });
                Add(Events(model with { Entries = earlierEntries }, inner, "较早记录").Lines);
            }
        }
        else
        {
            var objective = model.MapView == "objective";
            var title = objective ? "客观世界" : subjectiveTitle;
            var rooms = objective ? model.ObjectiveRooms : model.Rooms;
            var edges = objective ? model.ObjectiveEdges : model.Edges;
            AddMap(title, rooms, edges);
            if (earlierEntries.Length > 0)
            {
                Add(new[] { new Line("") });
                Add(Events(model with { Entries = earlierEntries }, inner, "较早记录").Lines);
            }
        }
        var footer = string.IsNullOrWhiteSpace(model.Footer) ? new List<Line>() : Lines(model.Footer, inner, Muted);
        var footerRows = footer.Count == 0 ? 0 : footer.Count + 1;
        var canvasHeight = rows.Count + footerRows + 2;
        var pageIndex = 0;
        var pageCount = 1;
        if (model.Paginate)
        {
            canvasHeight = Math.Clamp(requestedHeight, 24, 100);
            var pages = Paginate(rows, headerRows, canvasHeight - headerRows - footerRows - 2);
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
        if (footer.Count > 0)
        {
            var top = canvasHeight - footer.Count - 1;
            for (var index = 0; index < footer.Count; index++)
                canvas.Put(2, top + index, footer[index].Text, Muted, inner);
        }
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
                var activeColumns = rows[start].Select(item => (item.X, item.Width)).ToHashSet();
                var columns = rows.Skip(start).Take(capacity).SelectMany(row => row)
                    .Where(item => activeColumns.Contains((item.X, item.Width)) && !string.IsNullOrWhiteSpace(item.Value.Text))
                    .GroupBy(item => (item.X, item.Width)).Select(group => group.First()).ToArray();
                var scopes = new Row(columns.Where(item => item.Value.Scope.Length > 0 && item.Value.Text != item.Value.Scope)
                    .Select(item => new Placement(item.X, item.Width, new Line(item.Value.Scope, Muted, KeepWithNext: true))));
                var parents = new Row(columns.Where(item => item.Value.Parent is not null)
                    .Select(item => new Placement(item.X, item.Width,
                        item.Value.Parent! with { Text = item.Value.Parent!.Text + "（续）" })));
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
        if (title == "已记录位置") content.Add(new("本帧未记录完整认知图", Muted, Scope: title, KeepWithNext: true));
        content.AddRange(RoomBlocks(title, rooms, model, width, height).SelectMany(block => block.Lines));
        if (rooms.Count == 0) content.Add(new("未记录", Muted, Scope: title));
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
                     .OrderByDescending(group => group.Any(room => room.IsCurrent))
                     .ThenBy(group => RegionOrder(group.Key)).ThenBy(group => group.Key, StringComparer.Ordinal))
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
            var sorted = region.OrderByDescending(room => room.IsCurrent)
                .ThenBy(room => RoomName(room), StringComparer.Ordinal).ThenBy(room => room.Id, StringComparer.Ordinal).ToArray();
            foreach (var room in sorted.Where(room => !IsEmpty(room)))
            {
                var lines = BeginBlock();
                if (title == "主观认知" && !room.IsCurrent && model.Paginate && model.Zoom != "detail")
                {
                    var known = string.Join("、", room.Members.OrderBy(member => member.Kind is "actor" or "person" ? 0 : 1)
                        .ThenBy(member => member.Title, StringComparer.Ordinal).Select(member => Missing(member.Title)));
                    var summary = RoomName(room) + (known.Length > 0 ? " · 已知：" + known : " · 未记录成员");
                    lines.AddRange(Indented("  ", summary, width, Muted).Select(line => line with { Scope = scope }));
                    blocks.Add(new(region.Key, lines));
                    continue;
                }
                var roomTitle = RoomName(room) + (room.IsCurrent ? " · 当前" : "");
                var heading = new Line("- " + roomTitle, room.IsCurrent ? Teal : Ink, true, scope, KeepWithNext: true);
                lines.AddRange(Indented("- ", roomTitle, width, heading.Color, true)
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
            var endpoints = from + " <-> " + to;
            var separateState = state.Length > 0 && TextWidth("  " + endpoints + state) > width;
            foreach (var line in Indented("  ", endpoints + (separateState ? "" : state), width, Muted))
                yield return line with { Scope = title, Parent = passages, KeepWithNext = separateState };
            if (separateState)
                foreach (var line in Indented("    ", state.Trim(), width, Muted))
                    yield return line with { Scope = title, Parent = passages };
        }
        if (edges.Count == 0) yield return new(title == "已记录位置" ? "未记录" : "—", Muted, Scope: title, Parent: passages);
    }

    private static string PerformanceLine(TerminalModel model)
    {
        var performance = model.Performance;
        static double? Number(IReadOnlyDictionary<string, object?> values, string key)
        {
            var text = Convert.ToString(values.GetValueOrDefault(key), CultureInfo.InvariantCulture);
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value >= 0
                ? value : null;
        }
        static string Format(double? value, double divisor = 1) => value is double number
            ? (number / divisor).ToString("G4", CultureInfo.InvariantCulture) : "—";
        var label = Convert.ToString(performance.GetValueOrDefault("controller_label"), CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(label)) label = ControllerKind(model) switch
        {
            "human" => "人工", "llm" => "LLM", _ => "引擎"
        };
        static string Count(double? value) => value is double number ? number.ToString("0", CultureInfo.InvariantCulture) : "—";
        var engine = ControllerKind(model) == "engine";
        var actorRows = HostValues.List(performance, "decisions").OfType<IReadOnlyDictionary<string, object?>>()
            .Where(row => HostValues.Text(row, "actor") == model.Actor).ToArray();
        var actorScope = HostValues.Text(performance, "decision_actor_scope");
        var zeroRecorded = actorScope == "all" || actorScope == model.Actor;
        double? actorSelected = actorRows.Length == 0 ? zeroRecorded ? 0 : null : actorRows.Sum(row => Number(row, "decisions") ?? 0);
        var wall = Number(performance, "elapsed_wall_ms");
        double? actorRate = actorSelected is double count && wall is double elapsed && elapsed > 0
            ? count * 3600000d / elapsed : null;
        var lifecycle = HostValues.Map(performance, "lifecycle");
        var selected = Number(lifecycle, "selected") ?? Number(performance, "total_decisions");
        var cumulative = engine
            ? "会话累计：已选择 " + Count(selected) + " · 已完成 " + Count(Number(lifecycle, "completed")) +
                "（含拒绝） · 在途 " + Count(Number(lifecycle, "pending"))
            : "当前检阅者累计：已选择 " + Count(actorSelected);
        var rates = "速率（每现实小时）：游戏 " + Format(Number(performance, "game_hours_per_real_hour")) +
            " h · " + (engine ? "会话选择 " : "本人选择 ") +
            Format(engine ? Number(performance, "decisions_per_real_hour") : actorRate) + " 次 · " + label + "接口 " +
            Format(Number(performance, "interface_hours_per_real_hour")) + " h";
        var perGameHour = engine ? "按本次累计推进折算／游戏小时：引擎选择 " +
            Format(Number(performance, "engine_decisions_per_game_hour")) + " 次 · 选策计算 " +
            Format(Number(performance, "engine_decision_compute_ms_per_game_hour"), 1000) + " 秒 · 现实经过 " +
            Format(Number(performance, "elapsed_real_ms_per_game_hour"), 1000) + " 秒" : "";
        var statistics = cumulative + "\n" + rates + (perGameHour.Length > 0 ? "\n" + perGameHour : "");
        if (model.Zoom != "detail") return statistics;
        var duration = wall is double milliseconds ? Format(milliseconds, 1000) + " 秒" : "—";
        return statistics + "\n" + (engine ? "当前检阅者累计已选择 " + Count(actorSelected) + " · " : "") +
            "采样 " + duration + " / 推进 " + Format(Number(performance, "advanced_game_minutes")) + " 游戏分";
    }

    private static string ControllerKind(TerminalModel model)
    {
        var kind = Convert.ToString(model.Performance.GetValueOrDefault("controller_kind"), CultureInfo.InvariantCulture);
        if (kind is "engine" or "human" or "llm") return kind;
        if (model.Mode.Contains("LLM", StringComparison.OrdinalIgnoreCase) || model.Mode.Contains("大模型", StringComparison.Ordinal)) return "llm";
        if (model.Mode.Contains("人类", StringComparison.Ordinal) || model.Mode.Contains("人工", StringComparison.Ordinal) ||
            model.Mode.Contains("human", StringComparison.OrdinalIgnoreCase) || model.Mode.Contains("manual", StringComparison.OrdinalIgnoreCase)) return "human";
        return "engine";
    }

    private static bool ScoresVisible(TerminalModel model) => model.ShowScores ?? (ControllerKind(model) == "engine");
    private static string Known(string value) => value is "未记录" or "时间未记录" ? "" : value;
    private static string PhaseLabel(string value) => value switch
    {
        "focus" or "focused" => "聚焦", "confirm" or "confirmed" => "已确认·待结果",
        "awaiting_player" or "awaiting_input" => "待选", "prepared" => "待确认",
        "applied" or "completed" => "已执行", "scheduled" => "已排程·待执行",
        "rejected" => "被拒绝", "closed" => "本次记录停止", "stopped" => "会话已停止", "cancel" or "cancelled" => "已取消",
        "未记录" or "未准备" or "只读" => "", _ => value
    };

    private static List<List<TerminalEntry>> EntryGroups(IReadOnlyList<TerminalEntry> entries)
    {
        var groups = new List<List<TerminalEntry>>();
        foreach (var entry in entries)
        {
            if (groups.Count == 0 || entry.GroupId.Length == 0 || groups[^1][0].GroupId != entry.GroupId)
                groups.Add(new List<TerminalEntry>());
            groups[^1].Add(entry);
        }
        return groups;
    }

    private static Block Events(TerminalModel model, int width, string scope = "最近记录")
    {
        var groups = EntryGroups(model.Entries);
        var headingText = groups.Count > 1 ? scope + " · 新→旧" : scope == "最近记录" ? "当前记录" : scope;
        var result = new List<Line> { new(headingText, Teal, true, scope, KeepWithNext: true) };
        foreach (var group in groups.AsEnumerable().Reverse())
        foreach (var entry in group)
        {
            var speaker = string.IsNullOrWhiteSpace(entry.Speaker) ? entry.Title : entry.Speaker;
            if (string.IsNullOrWhiteSpace(speaker) || speaker == "未记录") speaker = "观察记录";
            speaker = TerminalProjection.VoiceHeading(speaker, entry.Voice);
            var title = (string.IsNullOrWhiteSpace(entry.Clock) ? "时间未记录" : entry.Clock) + " · " + speaker;
            var heading = new Line(title, entry.Kind == "error" ? Amber : Muted, true, scope,
                KeepWithNext: !string.IsNullOrWhiteSpace(entry.Text));
            result.AddRange(Lines(title, width, heading.Color, true).Select((line, index) => line with
            {
                Scope = scope, Parent = index == 0 ? null : heading,
                KeepWithNext = !string.IsNullOrWhiteSpace(entry.Text)
            }));
            if (!string.IsNullOrWhiteSpace(entry.Text))
                result.AddRange(Lines(entry.Text, width).Select(line => line with { Scope = scope, Parent = heading }));
            result.Add(new("", Scope: scope));
        }
        return new Block(scope, result);
    }

    private static Block Choices(TerminalModel model, int width, bool compact = false)
    {
        var result = new List<Line>();
        var sharedCost = model.Options.Count > 0 ? model.Options[0].Detail : "";
        var hasSharedCost = model.Options.Count > 1 && model.Options.All(option => option.HasCost && option.Detail == sharedCost);
        if (!hasSharedCost) sharedCost = "";
        var actionPage = GetActionPageInfo(model);
        var start = compact ? actionPage.Index * 5 : 0;
        var count = compact ? Math.Min(5, model.Options.Count - start) : model.Options.Count;
        var detailedScores = model.ShowScores == true || model.Zoom == "detail";
        var title = (model.OptionTimeBasis == "decision-boundary" ? "当时的候选" : "行动") +
            (compact && count < model.Options.Count ? $"  {start + 1:00}–{start + count:00}/{model.Options.Count:00}" : "") +
            (hasSharedCost ? "  ·  各项 " + (sharedCost.Length > 0 ? sharedCost : "费用 0") : "") +
            (ScoresVisible(model) && !detailedScores ? "  ·  评分详情 scores on" : "");
        for (var index = start; index < start + count; index++)
        {
            var option = model.Options[index];
            var prefix = (option.Selected ? "> " : "· ") + $"{index + 1:00}  ";
            var cost = option.HasCost
                ? hasSharedCost && option.Detail == sharedCost ? "" : option.Detail.Length == 0 ? "费用 0" : option.Detail
                : hasSharedCost ? "费用未记录" : "";
            var metadata = string.Join("  ", new[] { cost,
                ScoresVisible(model) && (detailedScores || option.Selected) ? OptionScore(option, detailedScores) : ""
            }.Where(value => value.Length > 0));
            var fullLabel = Missing(option.Title) + (option.Disambiguator.Length == 0 ? "" : " [" + option.Disambiguator + "]");
            var label = fullLabel + (metadata.Length == 0 ? "" : "  " + metadata);
            var lines = Indented(prefix, label, width, option.Selected ? Teal : Ink, option.Selected).ToList();
            var first = lines[0] with { Scope = title, KeepWithNext = lines.Count > 1 };
            result.Add(first);
            result.AddRange(lines.Skip(1).Select(line => line with { Scope = title, Parent = first }));
            if (ScoresVisible(model) && model.Zoom == "detail" && option.ScoreDetail.Length > 0)
                result.AddRange(Indented(new string(' ', TextWidth(prefix)), option.ScoreDetail, width, Muted)
                    .Select(line => line with { Scope = title, Parent = first }));
            if (model.Zoom == "detail") result.Add(new("", Scope: title));
        }
        if (compact && actionPage.Count > 1)
            result.Add(new($"行动页 {actionPage.Index + 1}/{actionPage.Count} · actions prev/next/all", Muted, Scope: title));
        return new Block(title, result);
    }

    private static string OptionScore(TerminalOption option, bool detailed)
    {
        var score = string.IsNullOrWhiteSpace(option.Score) ? "—" : option.Score;
        var kind = option.ScoreKind switch
        {
            "recorded-root" => "决策根", "recorded-choice" => "选择路径", "immediate" => "当前即时",
            "not-evaluated" => "未估值",
            "" => "口径未记录", _ => option.ScoreKind
        };
        var current = option.PlanningStatus == "unexpanded" ? "当前未展开" : kind == "未估值" ? kind : kind + " " + score;
        if (!option.Selected || option.SelectionScore.Length == 0) return "AI " + current;
        if (!detailed) return "AI 选择 " + option.SelectionScore;
        var when = option.SelectionClock.Length > 0 ? option.SelectionClock + " " : "";
        var repeated = option.ScoreKind == "recorded-choice" && option.Score == option.SelectionScore;
        return "AI " + when + "选择路径 " + option.SelectionScore + (repeated ? "" : " · " + current);
    }

    private static int TextWidth(string value)
    {
        var width = 0;
        foreach (var rune in Clean(value).EnumerateRunes()) width += RuneWidth(rune);
        return width;
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
