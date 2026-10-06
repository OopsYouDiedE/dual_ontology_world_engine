using System.Text.Json;
using System.Text.Json.Nodes;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>
/// 寝区案件的变体与求解（总纲 §五.8、§六 第五期）。真相与答案由后续章节依赖，固定不变（过去定）；
/// 变的是痕迹的出现次序、所需的调查量、是否留下一锤定音的痕迹。求解器检查：作答的问题唯一可解、
/// 另一问在寝区内无解、每条痕迹都排除过组合、没有单件定案、最短推理不少于两条痕迹。种子 0 是作者版本。
/// </summary>
public static class CaseVariants
{
    public static int Seed { get; set; }

    public sealed record Metrics(bool UniqueAnswer, bool OtherOpen, bool EveryTraceUsed, int SingleDecisive, int MinPath)
    {
        public bool Acceptable => UniqueAnswer && OtherOpen && EveryTraceUsed && SingleDecisive == 0 && MinPath >= 2;
    }

    public static Metrics Solve(JsonElement data)
    {
        var answer = data.GetProperty("answer").GetInt32();
        var questions = data.GetProperty("questions").GetArrayLength();
        var truth = data.GetProperty("true_facts");
        var traces = data.GetProperty("traces").EnumerateArray().Select(t => t.GetProperty("key").GetString()!).ToArray();
        var hypotheses = data.GetProperty("hypotheses").EnumerateArray()
            .Select(h => (Options: h.GetProperty("options").EnumerateArray().Select(o => o.GetInt32()).ToArray(), Facts: h.GetProperty("facts"))).ToArray();
        bool Fits((int[] Options, JsonElement Facts) h, IEnumerable<string> seen)
            => seen.All(t => h.Facts.GetProperty(t).GetInt32() == truth.GetProperty(t).GetInt32());
        bool Decides(IEnumerable<string> seen) => hypotheses.Where(h => Fits(h, seen)).Select(h => h.Options[answer]).Distinct().Count() == 1;
        var all = hypotheses.Where(h => Fits(h, traces)).ToArray();
        var otherOpen = Enumerable.Range(0, questions).Where(q => q != answer).All(q => all.Select(h => h.Options[q]).Distinct().Count() > 1);
        var used = true;
        for (var i = 0; i < traces.Length; i++)
            used &= hypotheses.Count(h => Fits(h, traces[..i])) > hypotheses.Count(h => Fits(h, traces[..(i + 1)]));
        var single = traces.Count(t => Decides([t]));
        var minPath = Enumerable.Range(1, (1 << traces.Length) - 1)
            .Select(mask => traces.Where((_, i) => (mask & (1 << i)) != 0).ToArray())
            .Where(Decides).Select(s => s.Length).DefaultIfEmpty(int.MaxValue).Min();
        return new(Decides(traces), otherOpen, used, single, minPath);
    }

    /// <summary>按种子给出一个通过求解器的变体；种子 0 原样返回作者版本。</summary>
    public static JsonElement Variant(string id, JsonElement authored, int seed)
    {
        if (seed == 0) return authored;
        var random = new Random(HashCode.Combine(seed, id.Aggregate(17, (h, c) => h * 31 + c)));
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var node = JsonNode.Parse(authored.GetRawText())!.AsObject();
            var traces = node["traces"]!.AsArray().Select(t => t!.DeepClone()).ToList();
            var elimination = traces.Where(t => t!["negate"]!.GetValue<bool>()).OrderBy(_ => random.Next()).ToList();
            var decisive = traces.Where(t => !t!["negate"]!.GetValue<bool>()).ToList();
            var kept = random.Next(4) == 0 ? [.. elimination, .. decisive] : elimination;
            var most = traces.Max(t => t!["needs"]!.GetValue<int>());
            var needs = Enumerable.Range(0, kept.Count).Select(_ => random.Next(8, most + 1)).Order().ToArray();
            for (var i = 1; i < needs.Length; i++) needs[i] = Math.Max(needs[i], needs[i - 1] + 4);
            for (var i = 0; i < kept.Count; i++) kept[i]!["needs"] = needs[i];
            node["traces"] = new JsonArray([.. kept]);
            var candidate = JsonSerializer.SerializeToElement(node);
            if (Solve(candidate).Acceptable) return candidate;
        }
        return authored;
    }
}
