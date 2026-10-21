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
    public IReadOnlyList<TerminalRoom> Rooms { get; init; } = Array.Empty<TerminalRoom>();
    public IReadOnlyList<TerminalEdge> Edges { get; init; } = Array.Empty<TerminalEdge>();
    public IReadOnlyList<TerminalOption> Options { get; init; } = Array.Empty<TerminalOption>();
    public IReadOnlyList<TerminalEntry> Entries { get; init; } = Array.Empty<TerminalEntry>();
}

public sealed record TerminalRoom
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "未记录";
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
    public bool IsCurrent { get; init; }
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
    public string Detail { get; init; } = "未记录";
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
    private const string Background = "#0b1424", Panel = "#101f32", Ink = "#eadfca";
    private const string Teal = "#65d8c4", Muted = "#93a8bb", Amber = "#edbd72", Border = "#324d67";
    private sealed record Line(string Text, string Color = Ink);
    private sealed record Block(string Title, List<Line> Lines);
    private sealed record Cell(string Glyph, string Color, string Back, bool Continuation = false);

    public static string Render(TerminalModel model, int width = 120, int height = 40, bool color = true)
    {
        var canvas = Layout(model, width, height);
        var output = new StringBuilder();
        string? foreground = null, background = null;
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
                output.Append(cell.Glyph);
            }
            if (color) { output.Append("\u001b[0m"); foreground = background = null; }
            if (y + 1 < canvas.Height) output.Append('\n');
        }
        return output.ToString();
    }

    public static string ExportSvg(TerminalModel model, int width = 120, int height = 40)
    {
        var canvas = Layout(model, width, height);
        const int cw = 10, ch = 20;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{canvas.Width * cw}\" height=\"{canvas.Height * ch}\" viewBox=\"0 0 {canvas.Width * cw} {canvas.Height * ch}\" role=\"img\" aria-label=\"DV terminal cell rendering\">\n");
        svg.Append("<title>").Append(Xml(model.IsPreview ? "界面预览 · 未打开会话" : model.Title)).Append("</title>\n");
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
                svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{x * cw}\" y=\"{y * ch + 15}\" fill=\"{cell.Color}\" font-size=\"16\" font-family=\"Cascadia Mono, Noto Sans Mono CJK SC, Microsoft YaHei, monospace\" xml:space=\"preserve\">");
                svg.Append(Xml(cell.Glyph)).Append("</text>\n");
            }
        return svg.Append("</svg>\n").ToString();
    }

    private static Canvas Layout(TerminalModel model, int requestedWidth, int requestedHeight)
    {
        var width = Math.Clamp(requestedWidth, 60, 300);
        var wide = width >= 110;
        var inner = width - 4;
        var leftWidth = wide ? Math.Max(28, inner * 28 / 100) : inner;
        var rightWidth = wide ? Math.Max(29, inner * 28 / 100) : inner;
        var centerWidth = wide ? inner - leftWidth - rightWidth - 2 : inner;
        var left = Rooms(model, leftWidth - 4);
        var center = Events(model, centerWidth - 4);
        var right = Choices(model, rightWidth - 4);
        var source = new Block("来源 / 记录边界", Lines(model.Source, rightWidth - 4, Muted));
        var heading = Lines($"{model.Actor}  ·  {model.Location}  ·  {model.Clock}", width - 6, Ink);
        var phase = Lines($"{model.Mode}  /  {model.Phase}", width - 6, Muted);
        var topHeight = heading.Count + phase.Count + 4 + (model.IsPreview ? 1 : 0);
        var footer = Lines(model.Footer, width - 6, Muted);
        var footerHeight = footer.Count + 2;
        var bodyHeight = wide
            ? Math.Max(left.Lines.Count + 2, Math.Max(center.Lines.Count + 2, right.Lines.Count + source.Lines.Count + 5))
            : left.Lines.Count + center.Lines.Count + right.Lines.Count + source.Lines.Count + 11;
        // Height is a minimum: long and narrow views flow vertically instead of hiding observations.
        var height = Math.Max(Math.Clamp(requestedHeight, 20, 1000), topHeight + bodyHeight + footerHeight + 2);
        var canvas = new Canvas(width, height);
        canvas.Put(2, 1, model.Title, Teal, width - 4);
        var cursor = 2;
        if (model.IsPreview) canvas.Put(2, cursor++, "◆ 界面预览 · 未打开会话 · 非游戏验收", Amber, width - 4);
        foreach (var line in heading) canvas.Put(3, cursor++, line.Text, line.Color, width - 6);
        foreach (var line in phase) canvas.Put(3, cursor++, line.Text, line.Color, width - 6);
        canvas.Rule(2, cursor, width - 4);
        var y = topHeight;
        if (wide)
        {
            canvas.Box(2, y, leftWidth, bodyHeight, left);
            canvas.Box(3 + leftWidth, y, centerWidth, bodyHeight, center);
            var rightX = 4 + leftWidth + centerWidth;
            var choiceHeight = right.Lines.Count + 2;
            canvas.Box(rightX, y, rightWidth, choiceHeight, right);
            canvas.Box(rightX, y + choiceHeight + 1, rightWidth, bodyHeight - choiceHeight - 1, source);
        }
        else
        {
            foreach (var block in new[] { left, center, right, source })
            {
                canvas.Box(2, y, inner, block.Lines.Count + 2, block);
                y += block.Lines.Count + 3;
            }
        }
        canvas.Box(2, height - footerHeight - 1, inner, footerHeight, new Block("回放 / 命令", footer));
        return canvas;
    }

    private static Block Rooms(TerminalModel model, int width)
    {
        var result = new List<Line>();
        if (model.Rooms.Count == 0) result.Add(new("房间：未记录", Muted));
        foreach (var room in model.Rooms)
        {
            result.AddRange(Lines((room.IsCurrent ? "● " : "○ ") + room.Title, width, room.IsCurrent ? Teal : Ink));
            if (room.Lines.Count == 0) result.Add(new("  内容：未记录", Muted));
            foreach (var item in room.Lines) result.AddRange(Lines("  · " + item, width, Muted));
            result.Add(new(""));
        }
        result.Add(new("连接拓扑 · 非平面图", Teal));
        if (model.Edges.Count == 0) result.Add(new("连接：未记录", Muted));
        foreach (var edge in model.Edges)
        {
            var from = model.Rooms.FirstOrDefault(room => room.Id == edge.From)?.Title ?? edge.From;
            var to = model.Rooms.FirstOrDefault(room => room.Id == edge.To)?.Title ?? edge.To;
            result.AddRange(Lines("╭─ " + Missing(from), width, Ink));
            result.AddRange(Lines("│  " + Missing(edge.State), width, Amber));
            result.AddRange(Lines("╰─ " + Missing(to), width, Ink));
        }
        foreach (var room in model.Rooms.Where(room => !model.Edges.Any(edge => edge.From == room.Id || edge.To == room.Id)))
            result.AddRange(Lines("○ " + room.Title + " · 未记录连接", width, Muted));
        return new Block("房间 / 空间", result);
    }

    private static Block Events(TerminalModel model, int width)
    {
        var result = new List<Line>();
        if (model.Entries.Count == 0) result.Add(new("事件内容：未记录", Muted));
        foreach (var entry in model.Entries)
        {
            result.AddRange(Lines("◇ " + Missing(entry.Title), width, entry.Kind == "error" ? Amber : Teal));
            result.AddRange(Lines(entry.Text, width));
            result.Add(new(""));
        }
        return new Block("事件 / 观察正文", result);
    }

    private static Block Choices(TerminalModel model, int width)
    {
        var result = new List<Line>();
        if (model.Options.Count == 0) result.Add(new("候选：未记录", Muted));
        foreach (var option in model.Options)
        {
            result.AddRange(Lines((option.Selected ? "▸ " : "· ") + Missing(option.Title), width, option.Selected ? Teal : Ink));
            result.AddRange(Lines("  " + Missing(option.Detail), width, Muted));
            result.Add(new(""));
        }
        return new Block("当前候选", result);
    }

    private static string Missing(string value) => string.IsNullOrWhiteSpace(value) ? "未记录" : value;
    private static List<Line> Lines(string value, int width, string color = Ink)
    {
        var result = new List<Line>();
        var line = new StringBuilder();
        var used = 0;
        foreach (var rune in Clean(Missing(value)).EnumerateRunes())
        {
            if (rune.Value == '\n') { result.Add(new(line.ToString(), color)); line.Clear(); used = 0; continue; }
            var size = RuneWidth(rune);
            if (used + size > width && used > 0) { result.Add(new(line.ToString(), color)); line.Clear(); used = 0; }
            line.Append(rune.ToString());
            used += size;
        }
        if (line.Length > 0 || result.Count == 0) result.Add(new(line.ToString(), color));
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
        public Cell[,] Cells { get; }
        public Canvas(int width, int height)
        {
            Width = width; Height = height;
            Cells = new Cell[height, width];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) Cells[y, x] = new(" ", Ink, Background);
        }
        public void Put(int x, int y, string text, string color, int available)
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
                Cells[y, x] = new(rune.ToString(), color, Cells[y, x].Back);
                if (size == 2) Cells[y, x + 1] = new("", color, Cells[y, x + 1].Back, true);
                x += size;
            }
        }
        public void Rule(int x, int y, int width) => Put(x, y, new string('─', width), Border, width);
        public void Box(int x, int y, int width, int height, Block block)
        {
            for (var row = y; row < y + height; row++)
                for (var column = x; column < x + width; column++) Cells[row, column] = new(" ", Ink, Panel);
            Put(x, y, "╭" + new string('─', width - 2) + "╮", Border, width);
            Put(x, y + height - 1, "╰" + new string('─', width - 2) + "╯", Border, width);
            for (var row = y + 1; row < y + height - 1; row++)
            {
                Put(x, row, "│", Border, 1); Put(x + width - 1, row, "│", Border, 1);
            }
            Put(x + 2, y, " " + block.Title + " ", Teal, width - 4);
            for (var i = 0; i < block.Lines.Count && i < height - 2; i++)
                Put(x + 2, y + i + 1, block.Lines[i].Text, block.Lines[i].Color, width - 4);
        }
    }
}
