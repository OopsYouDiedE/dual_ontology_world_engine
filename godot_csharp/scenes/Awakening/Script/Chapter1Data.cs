using System.Text.Json;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>
/// 第一章生成数据的静态读口。内容来自 chapter_01.story 编译出的 Awakening.Content.json；
/// 运行时代码（世界、重演、会话）只从这里取文字、名称与规则，不再另存一份。
/// </summary>
public static class Chapter1Data
{
    private static readonly Lazy<JsonDocument> Document = new(() =>
    {
        using var stream = typeof(Chapter1Data).Assembly.GetManifestResourceStream("Awakening.Content.json")
            ?? throw new InvalidOperationException("missing compiled chapter content");
        return JsonDocument.Parse(stream);
    });

    public static JsonElement Root => Document.Value.RootElement;

    /// <summary>文案表里的一条文字；{名称} 占位符由 args 替换。</summary>
    public static string Text(string key, params (string Name, string Value)[] args)
    {
        if (!Root.GetProperty("TEXT").TryGetProperty(key, out var value))
            throw new KeyNotFoundException("chapter text key missing from chapter_01.story: " + key);
        return Fill(value.GetString()!, args);
    }

    public static bool HasText(string key) => Root.GetProperty("TEXT").TryGetProperty(key, out _);

    public static string Fill(string text, params (string Name, string Value)[] args)
    {
        foreach (var (name, value) in args) text = text.Replace("{" + name + "}", value);
        return text;
    }

    public static string PersonName(string key) => Root.GetProperty("PERSON_NAMES").GetProperty(key).GetString()!;
    public static string RoomName(string key) => Root.GetProperty("ROOM_NAMES").GetProperty(key).GetString()!;
    public static IEnumerable<string> Strings(string group) => Root.GetProperty(group).EnumerateArray().Select(item => item.GetString()!);
}
