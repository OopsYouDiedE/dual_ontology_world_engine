using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Numerics;
using DuelView.Compiled;

// Records the production host's actual display and submitted/committed choices.
// It never reads action definitions or reconstructs a missing earlier screen.
internal sealed class CliTranscript : IDisposable
{
    private readonly StreamWriter output;
    private readonly StreamWriter story;
    private readonly StreamWriter data;
    private readonly string decisionMaker;
    private readonly JsonSerializerOptions json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true };
    private string lastDisplay = "";
    private JsonElement options;
    private string lastSelectedBy = "";
    private int panel;
    private readonly Dictionary<string, string> playerActionLabels = new(StringComparer.Ordinal);
    private readonly HashSet<string> storyEvents = new(StringComparer.Ordinal);
    public string Path { get; }
    public string DetailPath { get; }

    public CliTranscript(string path, string viewer, string decisionMaker, object content)
    {
        Path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        DetailPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "details.txt");
        story = new StreamWriter(new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
        output = new StreamWriter(new FileStream(DetailPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
        data = new StreamWriter(new FileStream(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "panels.yaml"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
        this.decisionMaker = decisionMaker;
        story.WriteLine("游玩记录\n");
        output.WriteLine($"CLI 详细日志\n视角：{viewer}\n决策者：{decisionMaker}\n内容包：{JsonSerializer.Serialize(content, json)}\n");
    }

    public void Display(long version, object status, object? screen, object choices, object? presentation, object ranking)
    {
        var display = JsonSerializer.Serialize(new { version, status, screen, options = choices, presentation, ai_candidates = ranking }, json);
        if (display == lastDisplay) return;
        lastDisplay = display;
        options = JsonSerializer.SerializeToElement(choices, json);
        var block = new StringBuilder($"\n===== 显示 {++panel} · 状态版本 {version} · {DateTimeOffset.UtcNow:O} =====\n");
        var state = JsonSerializer.SerializeToElement(status, json);
        foreach (var (key, title) in new[] { ("chapter", "章节"), ("game_time", "游戏时间（分钟）"), ("location", "位置"), ("status", "状态") })
            if (state.TryGetProperty(key, out var value)) block.AppendLine($"{title}：{value}");
        if (presentation is not null)
        {
            var shown = JsonSerializer.SerializeToElement(presentation, json);
            foreach (var key in new[] { "speaker_name", "surface", "lines" })
                if (shown.TryGetProperty(key, out var value))
                {
                    if (value.ValueKind == JsonValueKind.Array)
                        foreach (var line in value.EnumerateArray()) block.AppendLine(line.ToString());
                    else if (value.ValueKind == JsonValueKind.String) block.AppendLine(value.GetString());
                }
            foreach (var key in new[] { "travel", "arrival", "judgment", "reason", "failure_class" })
                if (shown.TryGetProperty(key, out var extra) && extra.ValueKind is not (JsonValueKind.False or JsonValueKind.Null))
                    Render(block, key, extra, "");
        }
        block.AppendLine("场景面板：");
        Render(block, "", JsonSerializer.SerializeToElement(screen, json), "");
        block.AppendLine("选项：");
        var number = 0;
        if (options.ValueKind == JsonValueKind.Array)
            foreach (var option in options.EnumerateArray())
            {
                var label = option.TryGetProperty("label", out var text) ? text.ToString() : option.ToString();
                var id = option.TryGetProperty("id", out var key) ? key.ToString() : "";
                block.AppendLine($"{++number}. {label} [{id}]");
            }
        if (number == 0) block.AppendLine("（无可选项）");
        var ranked = JsonSerializer.SerializeToElement(ranking, json);
        if (ranked.TryGetProperty("top", out var top))
        {
            block.AppendLine("AI 候选前 5 项（按原始分数排序；显示截去小数）：");
            block.AppendLine(ranked.GetProperty("source").GetString() == "recorded-engine-decision"
                ? "评分来源：引擎 AI 本次决策" : "评分来源：当前选项的 AI 参考评分");
            block.AppendLine("主要贡献：正负分别选取接近最大贡献的项目，各最多 3 项；不足 1 的项目省略。");
            var rank = 0;
            foreach (var candidate in top.EnumerateArray())
            {
                block.AppendLine($"{++rank}. {candidate.GetProperty("label")}　总分 {SignedInteger(ReadScore(candidate.GetProperty("score")))}");
                if (candidate.TryGetProperty("score_structure", out var structure) && structure.ValueKind == JsonValueKind.Object)
                    block.AppendLine("   主要贡献：" + MajorContributors(structure));
                else block.AppendLine("   未记录分项。");
            }
            if (rank == 0) block.AppendLine("（本次决策没有可排序的候选分数）");
        }
        output.Write(block.ToString());
        data.Write($"- panel: {panel}\n  displayed_epoch: {(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture)}\n  display: {JsonSerializer.Serialize(new { version, status, screen, options = choices, presentation, ai_candidates = ranking })}\n\n");
    }

    private static Rational ReadScore(JsonElement value) => value.ValueKind == JsonValueKind.Object
        ? Rational.From(value.GetProperty("$dv_rational").GetString()!)
        : Rational.From(value.ToString());

    private static string SignedInteger(Rational value)
    {
        var whole = value.Numerator / value.Denominator; // Truncate toward zero, including negative scores.
        return whole.Sign > 0 ? "+" + whole : whole.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string MajorContributors(JsonElement score)
    {
        var zero = Rational.From("0");
        var terms = new List<(string Label, Rational Value)>();
        void Add(string key, string label, bool negative = false)
        {
            if (!score.TryGetProperty(key, out var value)) return;
            var number = ReadScore(value);
            terms.Add((label, negative ? zero.Subtract(number) : number));
        }
        Add("information_component", "信息收益");
        if (score.TryGetProperty("components", out var components) && components.ValueKind == JsonValueKind.Object)
        {
            var sum = zero;
            foreach (var (key, label) in new[]
            {
                ("declared", "行动收益"), ("private", "私下行动收益"), ("owner", "个人利益"),
                ("achieve", "目标推进"), ("communicate", "沟通推进"), ("navigate", "接近目标"),
                ("personal", "个人动机"), ("answer", "回答收益"), ("response", "回应请求"),
                ("exploration", "探索收益"), ("schedule", "日程收益")
            })
                if (components.TryGetProperty(key, out var value))
                {
                    var number = ReadScore(value);
                    terms.Add((label, number));
                    sum = sum.Add(number);
                }
            // sims_autonomy is unweighted. Read its actual contribution from the
            // recorded progress total rather than displaying the raw input as points.
            if (score.TryGetProperty("progress_component", out var progress))
                terms.Add(("生活动机", ReadScore(progress).Subtract(sum)));
        }
        else Add("progress_component", "进展收益");
        Add("time_penalty", "耗时成本", negative: true);
        var selected = new List<string>();
        foreach (var sign in new[] { 1, -1 })
        {
            var group = terms.Where(term => term.Value.Numerator.Sign == sign &&
                !BigInteger.Divide(term.Value.Numerator, term.Value.Denominator).IsZero)
                .Select(term => (term.Label, term.Value, Magnitude: sign > 0 ? term.Value : zero.Subtract(term.Value)))
                .OrderByDescending(term => term.Magnitude, Comparer<Rational>.Create((a, b) => a.CompareTo(b))).ToArray();
            if (group.Length == 0) continue;
            var threshold = group[0].Magnitude.Multiply(Rational.From("4/5"));
            selected.AddRange(group.Where(term => term.Magnitude.CompareTo(threshold) >= 0).Take(3)
                .Select(term => term.Label + " " + SignedInteger(term.Value)));
        }
        return selected.Count == 0 ? "无整数贡献（小数项已省略）" : string.Join("；", selected);
    }

    private static void Render(StringBuilder block, string key, JsonElement value, string indent)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (key.Length > 0) block.AppendLine(indent + key + "：");
            foreach (var field in value.EnumerateObject()) Render(block, field.Name, field.Value, indent + "  ");
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            if (value.GetArrayLength() == 0) return;
            block.AppendLine(indent + key + "：");
            foreach (var item in value.EnumerateArray()) Render(block, "", item, indent + "  ");
        }
        else if (value.ValueKind is not JsonValueKind.Null && value.ToString().Length > 0)
            block.AppendLine(indent + (key.Length > 0 ? key + "：" : "") + value);
    }

    public void Choice(string id, string actor, string stage, object? action = null, string? selectedBy = null)
    {
        var number = 0;
        string? label = null;
        if (options.ValueKind == JsonValueKind.Array)
            foreach (var option in options.EnumerateArray())
            {
                number++;
                if (option.TryGetProperty("id", out var key) && key.GetString() == id)
                {
                    label = option.TryGetProperty("label", out var text) ? text.ToString() : id;
                    break;
                }
            }
        int? optionNumber = label is null ? null : number;
        var chosen = label is null ? $"{id}（当前面板无对应选项）" : $"第 {number} 项：{label} [{id}]";
        if (label is null && action is not null)
        {
            var value = JsonSerializer.SerializeToElement(action, json);
            if (value.TryGetProperty("label", out var text))
            {
                label = text.GetString();
                chosen += $" · {text}";
            }
        }
        if (stage != "提交" && !string.IsNullOrWhiteSpace(label)) playerActionLabels[id] = label;
        var maker = selectedBy ?? decisionMaker;
        lastSelectedBy = maker;
        output.WriteLine($"\n{(maker == "engine-ai" ? "引擎 AI" : maker is "agent" or "llm" ? "Agent" : maker == "human" ? "人类" : "玩家")}选择（{stage}，决策者：{maker}，角色：{actor}，面板：{panel}）：{chosen}\n");
        data.Write($"- choice_panel: {panel}\n  choice: {JsonSerializer.Serialize(new { id, actor, stage, decisionMaker = maker, number = optionNumber, label })}\n\n");
    }

    public void Execution(IReadOnlyDictionary<string, object?> receipt)
    {
        output.WriteLine($"\n最终选择执行结果：{receipt.GetValueOrDefault("completed_action")} · {receipt.GetValueOrDefault("outcome")} · 决策者：{lastSelectedBy}\n");
        data.Write($"- execution_panel: {panel}\n  execution: {JsonSerializer.Serialize(new { decisionMaker = lastSelectedBy, action = receipt.GetValueOrDefault("completed_action"), outcome = receipt.GetValueOrDefault("outcome"), execution_event = receipt.GetValueOrDefault("execution_event") })}\n\n");
    }

    // Only actual completed presentations enter the simple reading copy. A
    // refreshed panel must never repeat dialogue or expose off-screen actors.
    public void RecordStory(IReadOnlyDictionary<string, object?> receipt, string viewer)
    {
        var own = HostValues.Text(receipt, "completed_actor") == viewer;
        if (!own && receipt.GetValueOrDefault("visible") is not true) return;
        var eventId = HostValues.Text(receipt, "execution_event");
        if (eventId.Length > 0 && !storyEvents.Add(eventId)) return;
        var block = new StringBuilder();
        if (own)
        {
            var id = HostValues.Text(receipt, "completed_action");
            var label = playerActionLabels.GetValueOrDefault(id, "执行所选动作");
            var result = HostValues.Text(receipt, "outcome_category") switch
            {
                "completed" => "",
                "interrupted" => "（等待提前结束）",
                "cancelled" => "（已取消）",
                "condition-invalidated" => "（执行条件已失效）",
                "path-blocked" => "（移动或接触受阻）",
                _ => HostValues.Text(receipt, "outcome") == "applied" ? "" : "（未成功，详见执行回执）"
            };
            block.AppendLine($"你：{label}{result}");
        }
        if (receipt.GetValueOrDefault("visible") is true)
        {
            var surface = HostValues.Text(receipt, "surface");
            var voice = HostValues.Text(receipt, "surface_voice");
            var speaker = HostValues.Text(receipt, "speaker") == viewer ? "你" : HostValues.Text(receipt, "speaker_name");
            // The receipt carries the viewer's name at this event. Do not
            // resolve it again from a later state or collapse equal NPC text.
            if (!own && voice != "dialogue" && speaker.Length > 0 &&
                (!string.IsNullOrWhiteSpace(surface) || HostValues.List(receipt, "lines").Count > 0))
                block.AppendLine(speaker + "（动作）：");
            if (!string.IsNullOrWhiteSpace(surface))
                block.AppendLine(voice == "dialogue" && speaker.Length > 0 ? speaker + "：" + surface : surface);
            var lines = HostValues.List(receipt, "lines").OfType<string>().Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            if (own && HostValues.Text(receipt, "voice") == "private-monologue" && lines.Length > 0)
                block.AppendLine("你（心想）：");
            foreach (var line in lines) block.AppendLine(line);
        }
        if (block.Length > 0) story.WriteLine(block.ToString());
    }

    public void Note(string text) => output.WriteLine($"\n记录：{text}\n");
    public void NavigationDiagnostics(IReadOnlyDictionary<string, object?> diagnostic)
    {
        if (HostValues.List(diagnostic, "events").Count == 0) return;
        data.Write($"- navigation_diagnostics: {JsonSerializer.Serialize(diagnostic)}\n\n");
        output.WriteLine("\n导航任务诊断（开发记录，不属于人物所知）：");
        output.WriteLine(JsonSerializer.Serialize(diagnostic, json));
    }
    public void RequestDiagnostics(IReadOnlyDictionary<string, object?> diagnostic)
    {
        if (HostValues.List(diagnostic, "events").Count == 0) return;
        data.Write($"- request_diagnostics: {JsonSerializer.Serialize(diagnostic)}\n\n");
        output.WriteLine("\n请求事件诊断（开发记录，不属于人物所知）：");
        output.WriteLine(JsonSerializer.Serialize(diagnostic, json));
    }
    public void Dispose() { story.Dispose(); output.Dispose(); data.Dispose(); }
}
