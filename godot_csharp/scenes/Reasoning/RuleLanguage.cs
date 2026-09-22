using System.Text.RegularExpressions;

namespace DualOntologyWorldEngine.Game.Reasoning;

public enum TaskOp { Go, Sync, Listen, Publish, Compare, Inspect, Open, Close, Knock, Wait }
public sealed record Instruction(TaskOp Op, string Argument, int Duration, bool NeedsSync, int Line);

// 文本只在游戏层解析；可执行方案使用枚举，不把字符串交给 Core 解释。
public static class RuleLanguage
{
    public static readonly string[] Keywords = ["如果", "那么", "前往", "校时", "监听", "提交记录", "比较记录", "检查", "开启", "关闭", "敲击", "等待"];
    public static IEnumerable<string> Vocabulary(int stage)
    {
        string[] basic = ["如果", "那么", "已校时", "前往", "厨房", "走廊", "餐厅", "校时", "监听", "提交记录", "比较记录", "等待"];
        foreach (var word in basic) yield return word;
        if (stage >= 1) foreach (var word in new[] { "检查", "保温柜" }) yield return word;
        if (stage >= 2) foreach (var word in new[] { "开启", "关闭" }) yield return word;
        if (stage >= 3) foreach (var word in new[] { "敲击", "两短一长" }) yield return word;
    }

    public static (List<Instruction> Program, List<string> Errors) Parse(string text, int stage)
    {
        var program = new List<Instruction>();
        var errors = new List<string>();
        var lines = text.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Split('#')[0].Trim().TrimEnd('。', '；', ';');
            if (line.Length == 0) continue;
            var conditional = false;
            var condition = Regex.Match(line, @"^如果\s*已校时\s*那么\s*(.+)$");
            if (condition.Success) { conditional = true; line = condition.Groups[1].Value; }
            Instruction? instruction = null;
            var move = Regex.Match(line, @"^前往\s*(厨房|走廊|餐厅)$");
            var timed = Regex.Match(line, @"^(监听|等待)\s*([0-9]+)$");
            if (move.Success) instruction = new(TaskOp.Go, move.Groups[1].Value, 2, conditional, i);
            else if (timed.Success && int.TryParse(timed.Groups[2].Value, out var n) && n is >= 1 and <= 120)
                instruction = new(timed.Groups[1].Value == "监听" ? TaskOp.Listen : TaskOp.Wait, "", n, conditional, i);
            else
            {
                var compact = Regex.Replace(line, @"\s+", "");
                var op = compact switch
                {
                    "校时" => TaskOp.Sync, "提交记录" => TaskOp.Publish, "比较记录" => TaskOp.Compare,
                    "检查保温柜" when stage >= 1 => TaskOp.Inspect,
                    "开启保温柜" when stage >= 2 => TaskOp.Open,
                    "关闭保温柜" when stage >= 2 => TaskOp.Close,
                    "敲击两短一长" when stage >= 3 => TaskOp.Knock,
                    _ => (TaskOp?)null
                };
                if (op is { } valid) instruction = new(valid, "", 1, conditional, i);
            }
            if (instruction is null) errors.Add($"第 {i + 1} 行：句式不完整或使用了尚未掌握的词。监听／等待需填写 1—120 行动值。");
            else program.Add(instruction);
        }
        return (program, errors);
    }
}
