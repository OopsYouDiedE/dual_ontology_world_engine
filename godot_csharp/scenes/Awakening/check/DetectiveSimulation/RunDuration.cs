using System.Text.RegularExpressions;

/// <summary>运行时长的外部写法，与剧本“运行”语句一致：分钟数、“数字+分钟/小时/天”的组合，或“到结局”（NaN）。</summary>
public static class RunDuration
{
    private static readonly Regex Part = new(@"(\d+)\s*(分钟|小时|天)");

    public static double Parse(string text)
    {
        text = text.Trim();
        if (text == "到结局") return double.NaN;
        if (int.TryParse(text, out var plain) && plain > 0) return plain;
        var parts = Part.Matches(text);
        if (parts.Count == 0 || Part.Replace(text, "").Trim().Length > 0)
            throw new ArgumentException("时长写成分钟数、“数字+分钟/小时/天”或“到结局”：" + text);
        var minutes = parts.Sum(m => int.Parse(m.Groups[1].Value) * (m.Groups[2].Value switch { "小时" => 60, "天" => 1440, _ => 1 }));
        return minutes > 0 ? minutes : throw new ArgumentException("时长必须大于零：" + text);
    }
}
