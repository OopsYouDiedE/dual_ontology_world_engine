using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>
/// 人物在记忆封存之下醒来时的认知初值：初醒信任、谨慎与说话习惯，写在各人的 story 文件里。
/// <see cref="Overrides"/> 只供自动验证微调默认设置（键如“苏泠.初醒信任.侦探”“沈鼎.谨慎”“霍铎.说话习惯.寡言”），
/// 正常游戏不设；须在构造世界之前设定。
/// </summary>
public static class Persona
{
    public static IReadOnlyDictionary<string, double> Overrides { get; set; } = new Dictionary<string, double>();

    /// <summary>
    /// 隐藏心智抽样（总纲 §六 第五期）：按各人 story 中的“心智范围”，以种子在范围内抽一组取值；种子 0 是第一局，
    /// 用作者默认值（不抽）。返回的键与 <see cref="Overrides"/> 相同，可与外部微调合并；玩家看不到这些取值。
    /// </summary>
    public static IReadOnlyDictionary<string, double> Sample(int seed)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (seed == 0 || !Chapter1Data.Root.TryGetProperty("MIND_RANGES", out var ranges)) return result;
        var random = new Random(seed);
        foreach (var person in ranges.EnumerateObject())
            foreach (var range in person.Value.EnumerateObject())
            {
                var low = (int)range.Value[0].GetDouble();
                var high = (int)range.Value[1].GetDouble();
                result[person.Name + "." + range.Name] = random.Next(low, high + 1);
            }
        return result;
    }

    private static JsonElement? Person(Person who)
        => Chapter1Data.Root.GetProperty("PEOPLE").TryGetProperty(Names.PersonKeys[(int)who], out var p) ? p : null;

    private static int Value(Person who, string key, Func<JsonElement, int?> read, int fallback)
        => Overrides.TryGetValue(Names.Of(who) + "." + key, out var forced) ? (int)Math.Round(forced)
            : Person(who) is { } person && read(person) is { } value ? value : fallback;

    /// <summary>此人醒来时心里对某个对象（“侦探”“铜口”或人名）的信任。</summary>
    public static int InitialTrust(Person who, string target) => Value(who, "初醒信任." + target,
        p => p.TryGetProperty("initial_trust", out var t) && t.TryGetProperty(target, out var v) ? (int)v.GetDouble() : null, 0);

    /// <summary>天性里的谨慎：越谨慎，越不肯碰没证明过代价的东西。</summary>
    public static int Caution(Person who) => Value(who, "谨慎",
        p => p.TryGetProperty("caution", out var c) ? (int)c.GetDouble() : null, 2);

    public static SpeechHabits Speech(Person who)
    {
        int Habit(string zh, string key) => Value(who, "说话习惯." + zh,
            p => p.TryGetProperty("speech", out var s) && s.TryGetProperty(key, out var v) ? (int)v.GetDouble() : null, 0);
        var mayLie = Value(who, "说话习惯.会撒谎",
            p => p.TryGetProperty("speech", out var s) && s.TryGetProperty("may_lie", out var v) ? (v.GetBoolean() ? 1 : 0) : null, 0) != 0;
        return new(Habit("坦率", "candor"), Habit("寡言", "reticence"), Habit("好问", "curiosity"), !mayLie);
    }
}
