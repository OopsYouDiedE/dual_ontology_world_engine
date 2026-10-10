namespace DualOntologyWorldEngine.Story;

/// <summary>
/// 所有章节与宿主之间唯一的事件接口（设计第四部分 §2）。
/// 宿主只把界面与现场的输入原样转给 Dispatch，再把返回的事件原样交给界面或现场；
/// 章节规则、文字与流程全在实现方。无画面驱动使用同一接口。
/// </summary>
public interface IChapterSession
{
    string ChapterId { get; }
    IReadOnlyList<Dictionary<string, object?>> Dispatch(IReadOnlyDictionary<string, object?> input);
    IReadOnlyDictionary<string, object?> Snapshot();
}

public static class ChapterEvents
{
    public static string SourceSite(string runtimeSite) => runtimeSite.StartsWith("person:", StringComparison.Ordinal)
        ? runtimeSite[7..] : runtimeSite;
    public static Dictionary<string, object?> Event(string kind, params (string Key, object? Value)[] args)
    {
        var result = new Dictionary<string, object?> { ["kind"] = kind };
        foreach (var (key, value) in args) result[key] = value;
        return result;
    }

    public static Dictionary<string, object?> Option(string id, string label, bool available = true, string lockHint = "", string group = "", string purpose = "")
    {
        var option = new Dictionary<string, object?> { ["id"] = id, ["label"] = label, ["available"] = available, ["lock_hint"] = lockHint };
        if (group != "") option["group"] = group;
        if (purpose != "") option["purpose"] = purpose;
        return option;
    }

    public static string Text(IReadOnlyDictionary<string, object?> input, string key, string fallback = "")
        => input.GetValueOrDefault(key)?.ToString() ?? fallback;

    public static IReadOnlyDictionary<string, object?> Map(IReadOnlyDictionary<string, object?> input, string key)
        => input.GetValueOrDefault(key) as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>();

    public static bool Flag(IReadOnlyDictionary<string, object?> input, string key, bool fallback = false)
        => input.GetValueOrDefault(key) is bool value ? value : fallback;

    public static double Number(IReadOnlyDictionary<string, object?> input, string key, double fallback = 0)
        => input.GetValueOrDefault(key) switch { long l => l, int i => i, double d => d, float f => f, _ => fallback };
}

/// <summary>自动回归用的探针：读内部状态或注入局面。不属于游玩接口，宿主与界面不调用。</summary>
public interface IChapterProbe
{
    object? Probe(string name, object?[] args);
}
