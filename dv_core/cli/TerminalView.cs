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

/// <summary>Pure observer presentation. ANSI and SVG share the exact same cell layout.</summary>
public static class TerminalView
{
    private const string Background = "#0b1424", Ink = "#eadfca";
    private const string Teal = "#65d8c4", Muted = "#93a8bb", Amber = "#edbd72";
    private sealed record Line(string Text, string Color = Ink, bool Bold = false, string Scope = "", Line? Parent = null);
    private sealed record Block(string Title, List<Line> Lines, bool Complete = true);
    private sealed record Cell(string Glyph, string Color, string Back, bool Continuation = false, bool Bold = false);

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
                if (color && cell.Bold != bold) { output.Append(cell.Bold ? "\u001b[1m" : "\u001b[22m"); bold = cell.Bold; }
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
            for (var x = 0; x < canvas.Width; x++)
            {
                var cell = canvas.Cells[y, x];
                svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x * cw}\" y=\"{y * ch}\" width=\"{cw}\" height=\"{ch}\" fill=\"{cell.Back}\"/>\n");
            }
        }
        for (var y = 0; y < canvas.Height; y++)
            for (var x = 0; x < canvas.Width; x++)
            {
                var cell = canvas.Cells[y, x];
                if (cell.Continuation || cell.Glyph == " ") continue;
                svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{x * cw}\" y=\"{y * ch + 15}\" fill=\"{cell.Color}\" font-size=\"16\" font-weight=\"{(cell.Bold ? 700 : 400)}\" font-family=\"Cascadia Mono, Noto Sans Mono CJK SC, Microsoft YaHei, monospace\" xml:space=\"preserve\">");
                svg.Append(Xml(cell.Glyph)).Append("</text>\n");
            }
        return svg.Append("</svg>\n").ToString();
    }

    private static Canvas Layout(TerminalModel model, int requestedWidth, int requestedHeight)
    {
        var width = Math.Clamp(requestedWidth, 60, 300);
        var inner = width - 4;
        var rows = new List<List<(int X, int Width, Line Value)>>();
        void Add(IEnumerable<Line> lines) { foreach (var line in lines) rows.Add(new() { (2, inner, line) }); }
        Add(Lines(model.Title, inner, Teal, true));
        var actorTitle = string.IsNullOrWhiteSpace(model.ActorTitle) ? model.Actor : model.ActorTitle;
        var status = new[] { model.IsPreview ? "预览" : Known(model.Mode), Known(model.Clock),
            string.IsNullOrWhiteSpace(actorTitle) ? "" : "视角 " + actorTitle, PhaseLabel(model.Phase) };
        Add(Lines(string.Join("  ·  ", status.Where(value => value.Length > 0)), model.Paginate ? inner - 12 : inner, Muted));
        var statusRow = rows.Count - 1;
        Add(new[] { new Line("") });
        var headerRows = rows.Count;
        if (model.ShowActors)
        {
            Add(new[] { new Line("视角", Teal, true) });
            foreach (var actor in model.Actors)
                Add(Lines((actor.Selected ? "> " : "· ") + actor.Title + "  " + actor.Id, inner, actor.Selected ? Teal : Ink));
            if (model.Actors.Count == 0) Add(new[] { new Line("—", Muted) });
            Add(new[] { new Line("") });
        }
        var showCompleteChoices = !model.Paginate;
        if (model.Paginate && model.Options.Count > 0)
        {
            var choices = Choices(model, inner, true);
            showCompleteChoices = !choices.Complete;
            Add(Lines(choices.Title, inner, Teal, true));
            Add(choices.Lines);
            Add(new[] { new Line("") });
        }
        if (model.MapView == "both" && width >= 118)
        {
            var leftWidth = (inner - 4) / 2;
            var rightWidth = inner - leftWidth - 4;
            var left = MapLines("客观世界", model.ObjectiveRooms, model.ObjectiveEdges, model, leftWidth, requestedHeight);
            var right = MapLines("主观认知", model.Rooms, model.Edges, model, rightWidth, requestedHeight);
            for (var i = 0; i < Math.Max(left.Count, right.Count); i++)
            {
                var row = new List<(int X, int Width, Line Value)>();
                if (i < left.Count) row.Add((2, leftWidth, left[i]));
                if (i < right.Count) row.Add((6 + leftWidth, rightWidth, right[i]));
                rows.Add(row);
            }
        }
        else
        {
            if (model.MapView is "objective" or "both")
                Add(MapLines("客观世界", model.ObjectiveRooms, model.ObjectiveEdges, model, inner, requestedHeight));
            if (model.MapView == "both") Add(new[] { new Line("") });
            if (model.MapView != "objective")
                Add(MapLines("主观认知", model.Rooms, model.Edges, model, inner, requestedHeight));
        }
        if (showCompleteChoices && model.Options.Count > 0)
        {
            var choices = Choices(model, inner);
            Add(new[] { new Line("") });
            Add(Lines(choices.Title, inner, Teal, true));
            Add(choices.Lines);
        }
        if (model.Entries.Count > 0)
        {
            Add(new[] { new Line(""), new Line("记录", Muted) });
            Add(Events(model, inner).Lines);
        }
        var canvasHeight = rows.Count + 2;
        var pageIndex = 0;
        var pageCount = 1;
        if (model.Paginate)
        {
            canvasHeight = Math.Clamp(requestedHeight, 24, 100);
            // Reserve scope and room headings when a list continues on another page.
            var pageSize = Math.Max(1, canvasHeight - 4 - headerRows);
            var bodyCount = Math.Max(0, rows.Count - headerRows);
            pageCount = Math.Max(1, (bodyCount + pageSize - 1) / pageSize);
            var requestedPage = model.PageIndex;
            if (requestedPage < 0)
            {
                var selectedRow = rows.FindIndex(headerRows, row => row.Any(item => item.Value.Text.StartsWith("> ", StringComparison.Ordinal)));
                requestedPage = selectedRow < headerRows ? 0 : (selectedRow - headerRows) / pageSize;
            }
            pageIndex = Math.Clamp(requestedPage, 0, pageCount - 1);
            var marker = $"{pageIndex + 1}/{pageCount}";
            rows[statusRow].Add((2 + inner - marker.Length, marker.Length, new Line(marker, Muted)));
            var pageRows = rows.Skip(headerRows + pageIndex * pageSize).Take(pageSize).ToList();
            var continuation = new List<List<(int X, int Width, Line Value)>>();
            if (pageIndex > 0)
            {
                var columns = pageRows.SelectMany(row => row).Where(item => !string.IsNullOrWhiteSpace(item.Value.Text))
                    .GroupBy(item => (item.X, item.Width)).Select(group => group.First()).ToArray();
                var scopes = columns.Where(item => item.Value.Scope.Length > 0 && item.Value.Text != item.Value.Scope)
                    .Select(item => (item.X, item.Width, new Line(item.Value.Scope, Muted))).ToList();
                var parents = columns.Where(item => item.Value.Parent is not null)
                    .Select(item => (item.X, item.Width, item.Value.Parent!)).ToList();
                if (scopes.Count > 0) continuation.Add(scopes);
                if (parents.Count > 0) continuation.Add(parents);
            }
            rows = rows.Take(headerRows).Concat(continuation).Concat(pageRows).ToList();
        }
        var canvas = new Canvas(width, canvasHeight, pageIndex, pageCount);
        for (var i = 0; i < rows.Count; i++)
            foreach (var item in rows[i]) canvas.Put(item.X, i + 1, item.Value.Text, item.Value.Color, item.Width, item.Value.Bold);
        return canvas;
    }

    private static List<Line> MapLines(string title, IReadOnlyList<TerminalRoom> rooms,
        IReadOnlyList<TerminalEdge> edges, TerminalModel model, int width, int height)
    {
        var content = new List<Line> { new(title, Muted), new("") };
        var spacing = model.Zoom == "detail" || model.Zoom == "fit" && rooms.Count * 5 < height;
        foreach (var room in rooms.OrderByDescending(room => room.IsCurrent))
        {
            var heading = new Line("- " + Missing(room.Title), room.IsCurrent ? Teal : Ink, true);
            content.AddRange(Indented("- ", Missing(room.Title), width, heading.Color, true)
                .Select((line, index) => line with { Parent = index == 0 ? null : heading }));
            void AddMemberLines(IEnumerable<Line> lines) => content.AddRange(lines.Select(line => line with { Parent = heading }));
            foreach (var member in room.Members)
            {
                AddMemberLines(Indented("    ", Missing(member.Title), width, member.IsViewer ? Teal : Ink, true));
                if (!string.IsNullOrWhiteSpace(member.Detail))
                    AddMemberLines(Indented("      ", member.Detail, width, Muted));
            }
            if (room.IsCurrent && !room.Members.Any(member => member.IsViewer))
                AddMemberLines(Indented("    ", string.IsNullOrWhiteSpace(model.ActorTitle) ? model.Actor : model.ActorTitle, width, Teal, true));
            foreach (var line in room.Lines.Where(line => !string.IsNullOrWhiteSpace(line)))
                AddMemberLines(Indented("    ", line, width, Muted));
            if (spacing) content.Add(new(""));
        }
        if (rooms.Count == 0) content.Add(new("—", Muted));
        content.Add(new(""));
        var passages = new Line("通道", Muted);
        content.Add(passages);
        var names = rooms.GroupBy(room => room.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Title, StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            var from = names.GetValueOrDefault(edge.From, edge.From);
            var to = names.GetValueOrDefault(edge.To, edge.To);
            var state = string.IsNullOrWhiteSpace(edge.State) || edge.State == "状态未记录" ? "" : "  [" + edge.State + "]";
            content.AddRange(Indented("  ", from + " — " + to + state, width, Muted).Select(line => line with { Parent = passages }));
        }
        if (edges.Count == 0) content.Add(new("—", Muted, Parent: passages));
        return content.Select(line => line with { Scope = title }).ToList();
    }

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
            if (!string.IsNullOrWhiteSpace(entry.Title)) result.AddRange(Lines(entry.Title, width, entry.Kind == "error" ? Amber : Muted));
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
            var score = OptionScore(option);
            var scoreWidth = TextWidth(score);
            var labelWidth = Math.Max(prefix.Length + 1, width - scoreWidth - 3);
            var suffix = option.Disambiguator.Length == 0 ? "" : " [" + option.Disambiguator + "]";
            var available = labelWidth - prefix.Length;
            var separateSuffix = compact && TextWidth(suffix) >= available;
            var fullLabel = Missing(option.Title) + suffix;
            var label = compact ? SingleLine(Missing(option.Title), separateSuffix ? available : available - TextWidth(suffix)) +
                (separateSuffix ? "" : suffix) : fullLabel;
            if (compact && label != fullLabel) complete = false;
            var lines = Indented(prefix, label, labelWidth, option.Selected ? Teal : Ink, option.Selected).ToList();
            var first = lines[0];
            first = first with { Text = first.Text + new string(' ', Math.Max(1, width - scoreWidth - TextWidth(first.Text))) + score };
            result.Add(first with { Scope = title });
            if (!compact)
            {
                result.AddRange(lines.Skip(1).Select(line => line with { Scope = title, Parent = first }));
            }
            if (separateSuffix)
                result.AddRange(Indented(new string(' ', prefix.Length), suffix.TrimStart(), width, Muted)
                    .Select(line => line with { Scope = title, Parent = first }));
            var costDetail = option.HasCost
                ? hasSharedCost && option.Detail == sharedCost ? "" : option.Detail.Length == 0 ? "0" : option.Detail
                : hasSharedCost ? "费用未记录" : "";
            if (costDetail.Length > 0)
                result.AddRange(Indented(new string(' ', prefix.Length), costDetail, width, Muted)
                    .Select(line => line with { Scope = title, Parent = first }));
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
        Lines(value, Math.Max(1, width - indent.Length), color, bold).Select((line, index) =>
            line with { Text = (index == 0 ? indent : new string(' ', indent.Length)) + line.Text });

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

    // Strip terminal commands in their entirety, including OSC hyperlinks and CSI sequences.
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
        return n >= 0x1100 && (n <= 0x115f || n == 0x2329 || n == 0x232a ||
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
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) Cells[y, x] = new(" ", Ink, Background);
        }
        public void Put(int x, int y, string text, string color, int available, bool bold = false)
        {
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
                if (x + size > end || y < 0 || y >= Height) break;
                Cells[y, x] = new(rune.ToString(), color, Cells[y, x].Back, Bold: bold);
                if (size == 2) Cells[y, x + 1] = new("", color, Cells[y, x + 1].Back, true, bold);
                x += size;
            }
        }
    }
}
