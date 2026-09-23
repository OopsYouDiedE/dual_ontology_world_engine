using System.Text.RegularExpressions;

namespace DualOntologyWorldEngine.Game.Reasoning;

public enum TeaVerb { Move, Carry, Pour, Pull, Lock, Inspect }

public sealed record TeaCommand(TeaVerb Verb, string Target, string? Destination = null);

public sealed record TeaSyntaxRule(string Condition, TeaCommand Command, int Priority, int Line);

public sealed record TeaParseResult(IReadOnlyList<TeaSyntaxRule> Rules, IReadOnlyList<string> Errors);

/// <summary>
/// 《安眠者有福》的中文行动规则方言。条件只保留为文本边界，交由 Core 编译器解释。
/// </summary>
public static class TeaRuleLanguage
{
    private static readonly Regex WhenRule = new(
        @"^当\s*(?<condition>.*?)[，,]\s*则\s*(?<action>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex IfRule = new(
        @"^如果\s*(?<condition>.*?)[，,]\s*那么\s*(?<action>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Move = new(
        @"^前往\s*(?<target>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Carry = new(
        @"^将\s*(?<target>.+?)\s*搬到\s*(?<destination>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Pour = new(
        @"^将\s*(?<target>.+?)\s*倒入\s*(?<destination>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Pull = new(
        @"^抽走\s*(?<target>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Lock = new(
        @"^锁上\s*(?<target>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Inspect = new(
        @"^查看\s*(?<target>.+)$",
        RegexOptions.CultureInvariant);

    public static TeaParseResult Parse(string text)
    {
        var rules = new List<TeaSyntaxRule>();
        var errors = new List<string>();
        var lines = text.Replace("\r", string.Empty).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Split('#', 2)[0].Trim();
            if (line.Length == 0) continue;

            if (!TrySeparatePriority(line, out var body, out var priority, out var priorityError))
            {
                errors.Add($"第 {index + 1} 行：{priorityError}");
                continue;
            }

            var header = WhenRule.Match(body);
            if (!header.Success) header = IfRule.Match(body);
            if (!header.Success)
            {
                errors.Add($"第 {index + 1} 行：请使用“当 条件，则 行动”或“如果 条件，那么 行动”，并用中文或英文逗号分隔。条件不能为空。");
                continue;
            }

            var condition = header.Groups["condition"].Value.Trim();
            if (condition.Length == 0)
            {
                errors.Add($"第 {index + 1} 行：条件不能为空，请在逗号前填写条件文本。");
                continue;
            }

            if (!TryParseCommand(header.Groups["action"].Value.Trim(), out var command))
            {
                errors.Add($"第 {index + 1} 行：行动格式不完整，请使用“前往 地点”、“将 对象 搬到 地点”、“将 对象 倒入 容器”、“抽走 对象”、“锁上 器具”或“查看 地点”。");
                continue;
            }

            rules.Add(new TeaSyntaxRule(condition, command, priority, index + 1));
        }

        return errors.Count == 0
            ? new TeaParseResult(rules, errors)
            : new TeaParseResult(Array.Empty<TeaSyntaxRule>(), errors);
    }

    public static string Describe(TeaSyntaxRule rule)
    {
        var command = rule.Command;
        var action = command.Verb switch
        {
            TeaVerb.Move => $"前往 {command.Target}",
            TeaVerb.Carry => $"将 {command.Target} 搬到 {command.Destination ?? "（未指定地点）"}",
            TeaVerb.Pour => $"将 {command.Target} 倒入 {command.Destination ?? "（未指定容器）"}",
            TeaVerb.Pull => $"抽走 {command.Target}",
            TeaVerb.Lock => $"锁上 {command.Target}",
            TeaVerb.Inspect => $"查看 {command.Target}",
            _ => command.Target
        };

        return $"第 {rule.Line} 行：条件“{rule.Condition}”，行动“{action}”，优先 {rule.Priority}；规则之间不按顺序执行。";
    }

    private static bool TrySeparatePriority(
        string line,
        out string body,
        out int priority,
        out string error)
    {
        body = line;
        priority = 0;
        error = string.Empty;

        var separator = line.LastIndexOfAny(['；', ';']);
        if (separator < 0) return true;

        body = line[..separator].Trim();
        var suffix = line[(separator + 1)..].Trim();
        const string marker = "优先";
        if (!suffix.StartsWith(marker, StringComparison.Ordinal))
        {
            error = "优先级后缀格式错误，请使用“；优先 10”（整数范围 -100 到 100）。";
            return false;
        }

        var number = suffix[marker.Length..].Trim();
        if (!int.TryParse(number, out priority) || priority is < -100 or > 100)
        {
            error = "优先级必须是 -100 到 100 的整数，请改写为“；优先 10”这样的格式。";
            return false;
        }

        return true;
    }

    private static bool TryParseCommand(string action, out TeaCommand command)
    {
        command = null!;

        var match = Move.Match(action);
        if (match.Success && TryName(match.Groups["target"].Value, out var moveTarget))
        {
            command = new TeaCommand(TeaVerb.Move, moveTarget);
            return true;
        }

        match = Carry.Match(action);
        if (match.Success
            && TryName(match.Groups["target"].Value, out var carryTarget)
            && TryName(match.Groups["destination"].Value, out var carryDestination))
        {
            command = new TeaCommand(TeaVerb.Carry, carryTarget, carryDestination);
            return true;
        }

        match = Pour.Match(action);
        if (match.Success
            && TryName(match.Groups["target"].Value, out var pourTarget)
            && TryName(match.Groups["destination"].Value, out var pourDestination))
        {
            command = new TeaCommand(TeaVerb.Pour, pourTarget, pourDestination);
            return true;
        }

        match = Pull.Match(action);
        if (match.Success && TryName(match.Groups["target"].Value, out var pullTarget))
        {
            command = new TeaCommand(TeaVerb.Pull, pullTarget);
            return true;
        }

        match = Lock.Match(action);
        if (match.Success && TryName(match.Groups["target"].Value, out var lockTarget))
        {
            command = new TeaCommand(TeaVerb.Lock, lockTarget);
            return true;
        }

        match = Inspect.Match(action);
        if (match.Success && TryName(match.Groups["target"].Value, out var inspectTarget))
        {
            command = new TeaCommand(TeaVerb.Inspect, inspectTarget);
            return true;
        }

        return false;
    }

    private static bool TryName(string raw, out string name)
    {
        name = string.Concat(raw.Where(character => !char.IsWhiteSpace(character)));
        return name.Length > 0;
    }
}
