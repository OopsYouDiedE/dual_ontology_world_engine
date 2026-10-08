using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 一条可被知道、被转述的知识：谓词 + 主语、对象、地点、哪一夜，以及少量取值（数目、规矩、课名、身形……）。
/// 句式写在 story「知识.&lt;谓词&gt;」，C# 只登记知识、不拼句子。说出口时由说话人的认知填槽：
/// 说的是自己就是“我”，说的是听者就是“你”，铜口背后的声音用说话人心里对它的称呼。
/// </summary>
public sealed record Claim(string Predicate, P? Subject = null, P? Object = null, Place? Where = null, int Night = 0,
    ImmutableDictionary<string, string>? Values = null)
{
    public Claim With(string key, object value) => this with { Values = (Values ?? ImmutableDictionary<string, string>.Empty).SetItem(key, value.ToString() ?? "") };
    public string Value(string key) => Values?.GetValueOrDefault(key) ?? "";
    /// <summary>不含主语的内容：主语不同、其余相同的几条知识可以合成一句。</summary>
    public string Content => $"{Predicate}|{Object}|{Where}|{Night}|{string.Join(",", (Values ?? ImmutableDictionary<string, string>.Empty).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value))}";
    public string Identity => $"{Subject}|{Content}";
}

public sealed partial class Game
{
    /// <summary>此人心里对铜口背后那个声音的称呼（投影名）。</summary>
    public string VoiceName(Person? who) => (who ?? Det).M.TryGetValue(CopperVoice, out var v) ? v.Name : View[CopperVoice].Name;

    /// <summary>某人被说起时的称呼：说话人自己是“我”，听者是“你”。</summary>
    private string Ref(P? who, Person? speaker, Person? listener, bool plural = false)
    {
        if (who is not { } p) return "";
        if (speaker?.Id == p) return plural ? "我们" : "我";
        if (listener?.Id == p) return "你";
        return Names.Of(p);
    }

    private string Pronoun(P? who, Person? speaker, Person? listener)
        => who is not { } p ? "" : speaker?.Id == p ? "我" : listener?.Id == p ? "你" : this[p].Pro.Pronoun;

    /// <summary>说话人此刻说起某一夜：前一夜是“昨夜”，当夜是“今夜”，再早就说日名。</summary>
    private string NightWord(int night) => night <= 0 ? "" : night == Day - 1 ? "昨夜" : night == Day ? "今夜" : Chapter2Design.DayName(night) + "夜里";

    /// <summary>把一条知识说成一句话。speaker 为空是旁白（按侦探的认知称呼），listener 为空是说给众人。</summary>
    public string Say(Claim c, Person? speaker = null, Person? listener = null, IReadOnlyList<P>? subjects = null, string? template = null)
    {
        var t = X(template ?? "知识." + c.Predicate);
        var subject = subjects is { Count: > 1 } ? JoinNames(subjects.Select(s => Ref(s, speaker, listener))) : Ref(c.Subject, speaker, listener);
        t = t.Replace("{主语}", subject).Replace("{对象}", Ref(c.Object, speaker, listener))
            .Replace("{主语他}", Pronoun(c.Subject, speaker, listener)).Replace("{对象他}", Pronoun(c.Object, speaker, listener))
            .Replace("{地}", c.Where is { } w ? Names.Of(w) : "").Replace("{夜}", NightWord(c.Night))
            .Replace("{铜口}", VoiceName(speaker));
        foreach (var (k, v) in c.Values ?? ImmutableDictionary<string, string>.Empty) t = t.Replace("{" + k + "}", v);
        return t;
    }

    /// <summary>侦探笔记、日志里的第三人称写法（不带“我/你”）。</summary>
    public string Plain(Claim c) => Say(c, speaker: null, listener: null);
    public string Plain(NewsItem item) => Plain(item.Claim);

    private static string JoinNames(IEnumerable<string> names)
    {
        var a = names.ToArray();
        return a.Length <= 1 ? string.Concat(a) : string.Join("、", a[..^1]) + "和" + a[^1];
    }

    /// <summary>句末补上句号（已是问号、叹号、句号的不补）。</summary>
    public static string Stop(string line)
    {
        var t = line.TrimEnd('；', '，', ' ');
        return t.Length > 0 && "。？！”".Contains(t[^1]) ? t : t + "。";
    }
}

public sealed partial class Game
{
    /// <summary>登记一条知识的简写。</summary>
    public static Claim K(string predicate, P? subject = null, P? obj = null, Place? at = null, int night = 0, params (string Key, object Value)[] values)
    {
        var c = new Claim(predicate, subject, obj, at, night);
        foreach (var (k, v) in values) c = c.With(k, v);
        return c;
    }
}
