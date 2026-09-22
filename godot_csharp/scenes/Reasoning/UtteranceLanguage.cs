using System.Collections.Immutable;
using System.Text.RegularExpressions;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.Reasoning;

public enum ReceiverScope { All, Kitchen, Hall }
public sealed record UtteranceClause(ReceiverScope Scope, Condition Sounds, bool Accept, int Line);

/// <summary>道本语启发的学园受限方言。词义锚定是游戏约定，不冒充完整 Toki Pona 翻译器。</summary>
public static class UtteranceLanguage
{
    public static readonly Tag Current = new("本轮发声"), First = new("该来源首次到达");
    public static readonly string[] Particles = ["o", "e", "pi", "li", "la"];
    public static readonly (string Word, string Meaning, int Stage)[] Lexicon =
    [
        ("ilo", "器物、工具", 0), ("kute", "听、接收", 0),
        ("tomo", "房间、建筑", 0), ("moku", "饮食", 0), ("toki", "交谈、语言", 0),
        ("kalama", "声音", 0), ("jo", "持有、接纳", 0), ("weka", "移开、排除", 0),
        ("o", "向前面的对象发出命令", 0), ("e", "后接受作用的事物", 0), ("pi", "将后面的词合为一个修饰组", 0),
        ("nanpa", "序数", 1), ("wan", "一", 1), ("ala", "否定前一个修饰或动作", 1),
        ("tenpo", "时间", 2), ("ni", "此、这", 2), ("pini", "结束、已过", 2),
        ("sin", "新、此轮发生", 2), ("li", "将事物与其描述分开", 2), ("la", "前面的描述限定后面命令的情境", 2)
    ];
    public static IEnumerable<(string Word, string Meaning)> Vocabulary(int stage)
        => Lexicon.Where(w => w.Stage <= stage).Select(w => (w.Word, w.Meaning));

    public static (List<UtteranceClause> Clauses, List<string> Errors) Parse(string text, int stage)
    {
        var clauses = new List<UtteranceClause>(); var errors = new List<string>();
        var known = Vocabulary(stage).Select(w => w.Word).ToHashSet();
        var lines = text.Replace("\r", "").Split('\n');
        for (var line = 0; line < lines.Length; line++)
        {
            var raw = lines[line].Split('#')[0].Trim().TrimEnd('.', '。');
            if (raw.Length == 0) continue;
            var tokens = Regex.Split(raw, @"\s+");
            var unknown = tokens.FirstOrDefault(w => !known.Contains(w));
            if (unknown is not null) { errors.Add($"第 {line + 1} 行：“{unknown}”尚未掌握或拼写错误。"); continue; }
            try
            {
                Condition context = new Condition.Constant(Truth.True);
                var la = Array.IndexOf(tokens, "la");
                if (la >= 0)
                {
                    if (tokens.Count(w => w == "la") != 1 || la < 3 || tokens[0] != "kalama" || tokens[1] != "li")
                        throw new FormatException("情境句式：kalama li 描述 la 命令。");
                    context = SoundDescription(tokens[2..la]);
                    tokens = tokens[(la + 1)..];
                }
                var o = Array.IndexOf(tokens, "o"); var e = Array.IndexOf(tokens, "e");
                if (o < 1 || tokens.Count(w => w == "o") != 1 || tokens.Count(w => w == "e") != 1 || e <= o + 1 || e == tokens.Length - 1)
                    throw new FormatException("句式：对象 o 动作 e 事物。每行一个命令。");
                var scope = Receiver(tokens[..o]);
                var verb = tokens[(o + 1)..e];
                if (verb.Length is < 1 or > 2 || verb[0] is not ("jo" or "weka") || verb.Length == 2 && verb[1] != "ala")
                    throw new FormatException("动作使用 jo 或 weka；ala 可否定动作。");
                var accept = verb[0] == "jo";
                if (verb.Length == 2) accept = !accept;
                if (tokens[e + 1] != "kalama") throw new FormatException("此试验台只能约束声音 kalama。");
                var description = SoundDescription(tokens[(e + 2)..]);
                clauses.Add(new(scope, new Condition.All([context, description]), accept, line));
            }
            catch (FormatException error) { errors.Add($"第 {line + 1} 行：{error.Message}"); }
        }
        return (clauses, errors);
    }

    private static ReceiverScope Receiver(string[] words)
    {
        if (words[0] != "ilo") throw new FormatException("被命令的对象须以 ilo 开始。");
        var i = 1;
        if (i < words.Length && words[i] == "kute") i++;
        if (i == words.Length) return ReceiverScope.All;
        if (words.Length - i != 3 || words[i] != "pi" || words[i + 1] != "tomo")
            throw new FormatException("房间修饰须成组：pi tomo 后接房间用途词。");
        return words[i + 2] switch
        {
            "moku" => ReceiverScope.Kitchen,
            "toki" => ReceiverScope.Hall,
            _ => throw new FormatException("这个房间描述尚无可核对的锚点。")
        };
    }

    private static Condition SoundDescription(string[] words)
    {
        var conditions = ImmutableArray.CreateBuilder<Condition>();
        for (var i = 0; i < words.Length;)
        {
            Condition item;
            if (words[i] == "nanpa" && i + 1 < words.Length && words[i + 1] == "wan")
            { item = new Condition.Tagged(new Noun.Slot(RuleSlot.Target), First, 1); i += 2; }
            else if (words[i] == "sin")
            { item = new Condition.Tagged(new Noun.Slot(RuleSlot.Target), Current, 1); i++; }
            else if (words[i] == "pi" && i + 2 < words.Length && words[i + 1] == "tenpo" && words[i + 2] is "ni" or "pini")
            {
                item = new Condition.Tagged(new Noun.Slot(RuleSlot.Target), Current, 1);
                if (words[i + 2] == "pini") item = new Condition.Not(item);
                i += 3;
                // pi 分组延伸到短语末尾；把序数放在 pi 之后会改变其修饰对象。
                if (i < words.Length && words[i] != "ala")
                    throw new FormatException("pi tenpo 的组内只能描述时间；声音的其他修饰需放在 pi 之前。");
            }
            else throw new FormatException($"“{words[i]}”在此处不能独立描述声音。");
            if (i < words.Length && words[i] == "ala") { item = new Condition.Not(item); i++; }
            conditions.Add(item);
        }
        return new Condition.All(conditions.ToImmutable());
    }

    public static string Describe(IEnumerable<UtteranceClause> clauses) => string.Join("\n", clauses.Select(c =>
        $"第 {c.Line + 1} 句：{(c.Scope == ReceiverScope.All ? "两台接收器" : c.Scope == ReceiverScope.Kitchen ? "厨房接收器" : "交谈室接收器")} → {(c.Accept ? "接纳" : "排除")} {DescribeCondition(c.Sounds)}"));
    private static string DescribeCondition(Condition condition) => condition switch
    {
        Condition.All a => string.Join("、", a.Items.Select(DescribeCondition).Where(s => s.Length > 0)) is { Length: > 0 } text ? text : "所有声音",
        Condition.Constant => "",
        Condition.Tagged t => t.Tag == Current ? "本轮产生的声音" : "每个来源第一次到达的声音",
        Condition.Not n => "非（" + DescribeCondition(n.Operand) + "）",
        _ => throw new NotSupportedException()
    };
}
