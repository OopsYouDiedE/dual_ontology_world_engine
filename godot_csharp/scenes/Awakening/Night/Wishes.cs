using System.Text.Json;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>
/// 心愿：由人物的欲望标志经方法库分解出的目标树；每一步按本人的本事看能否自己做到，做不到须找会的人（总纲 §五.4）。
/// 只用于向玩家显示所控角色自己的心愿。
/// </summary>
public static class Wishes
{
    private static readonly Dictionary<string, string> SkillNames = new()
    {
        ["strength"] = Chapter1Data.Text("心愿·技能·strength"), ["lock"] = Chapter1Data.Text("心愿·技能·lock"), ["signal"] = Chapter1Data.Text("心愿·技能·signal"), ["structure"] = Chapter1Data.Text("心愿·技能·structure"), ["cook"] = Chapter1Data.Text("心愿·技能·cook"), ["greenhouse_skill"] = Chapter1Data.Text("心愿·技能·greenhouse_skill"),
    };

    private static readonly Lazy<Dictionary<string, JsonElement>> Profiles = new(() =>
    {
        using var stream = typeof(Wishes).Assembly.GetManifestResourceStream("Awakening.World.json")
            ?? throw new InvalidOperationException("Missing compiled world resource.");
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.GetProperty("chapters")[0].GetProperty("entities").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "character")
            .ToDictionary(e => e.GetProperty("name").GetString()!, e => e.GetProperty("profile").Clone(), StringComparer.Ordinal);
    });

    /// <summary>此人的心愿，逐行缩进表示分解；没有方法库或此人不存在时为空。</summary>
    public static IReadOnlyList<string> For(string person)
    {
        var root = Chapter1Data.Root;
        if (!Profiles.Value.TryGetValue(person, out var profile) || !root.TryGetProperty("WISHES", out var wishes)
            || !root.TryGetProperty("METHODS", out var methods)) return [];
        var lines = new List<string> { Chapter1Data.Text("心愿·标题", ("名", person), ("欲望", profile.GetProperty("desire").GetString()!)) };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Goal(string goal, int depth)
        {
            if (!methods.TryGetProperty(goal, out var method) || !seen.Add(goal + depth)) return;
            var needs = method.GetProperty("needs").EnumerateObject().Select(n =>
            {
                var own = profile.TryGetProperty(n.Name, out var level) ? level.GetInt32() : 0;
                return Chapter1Data.Text(own >= n.Value.GetInt32() ? "心愿·自己够" : "心愿·须找人",
                    ("技能", SkillNames[n.Name]), ("自己", own.ToString()), ("需要", n.Value.GetInt32().ToString()));
            }).ToArray();
            lines.Add(new string('　', depth) + Chapter1Data.Text("心愿·目标", ("目标", goal), ("方法", method.GetProperty("how").GetString()!))
                + (needs.Length > 0 ? Chapter1Data.Text("心愿·需求", ("列表", string.Join("；", needs))) : ""));
            foreach (var part in method.GetProperty("parts").EnumerateArray()) Goal(part.GetString()!, depth + 1);
        }
        foreach (var flag in profile.GetProperty("desire_flags").EnumerateArray())
            if (wishes.TryGetProperty(flag.GetString()!, out var goals))
                foreach (var goal in goals.EnumerateArray()) Goal(goal.GetString()!, 1);
        return lines;
    }
}
