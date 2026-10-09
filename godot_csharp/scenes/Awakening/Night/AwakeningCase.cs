using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

/// <summary>
/// 生活翼一案的共享案件模型：六个推演变量的全部组合，各自经昨夜重演预测出的痕迹。
/// 只算一次；任何人的推理器都拿它与自己亲见的痕迹比对（总纲 §六 第二期）。
/// </summary>
public static class AwakeningCase
{
    public sealed record Prediction(NightVariables Variables, ImmutableDictionary<string, int> Facts);

    private static readonly Lazy<(CaseModel Model, ImmutableArray<Prediction> Predictions)> Built = new(Build);
    public static CaseModel Model => Built.Value.Model;
    public static ImmutableArray<Prediction> Predictions => Built.Value.Predictions;

    public static int[] Values(NightVariables v) => [(int)v.V1, (int)v.V2, (int)v.V3, (int)v.V4, (int)v.V5, (int)v.V6];

    // STORY-HARDCODE[AWK-003][schema][production]: 将本章 V1–V6、证物字段及第六问默认认知装配成案件假设。
    private static (CaseModel, ImmutableArray<Prediction>) Build()
    {
        var root = Chapter1Data.Root;
        var fields = root.GetProperty("DETECTIVE_TRACE_FIELDS").EnumerateObject().Select(item =>
        {
            var evidence = item.Value.GetProperty("evidence").GetString()!;
            var card = root.GetProperty("EVIDENCE").GetProperty(evidence);
            var projection = card.TryGetProperty("mind_name", out var alias) ? alias.GetString()! : card.GetProperty("name").GetString()!;
            return (Field: new CaseField(projection, new Tag(item.Value.GetProperty("tag").GetString()!), item.Value.GetProperty("field").GetString()!),
                Expected: item.Value.GetProperty("equals").GetInt32());
        }).ToArray();
        var predictions = NightVariables.All().Select(variables =>
        {
            var traces = NightReplay.Run(variables).Traces;
            var facts = fields.ToImmutableDictionary(f => f.Field.Key, f =>
            {
                var value = typeof(MorningTraces).GetProperty(f.Field.Key)?.GetValue(traces)
                    ?? throw new InvalidDataException("Unknown predicted trace field: " + f.Field.Key);
                var scalar = value switch { bool b => b ? 1 : 0, int n => n,
                    _ => throw new InvalidDataException("Trace fields must be boolean or integer.") };
                return f.Expected < 0 ? scalar : scalar == f.Expected ? 1 : 0;
            });
            return new Prediction(variables, facts);
        }).ToImmutableArray();
        var variables = root.GetProperty("VARIABLES").EnumerateArray().ToArray();
        var model = new CaseModel("awakening",
            variables.Select(v => v.GetProperty("key").GetString()!),
            variables.Select(v => v.GetProperty("options").EnumerateArray().Select(o => o.GetString()!)),
            fields.Select(f => f.Field),
            predictions.Select(p => new CaseHypothesis([.. Values(p.Variables)], p.Facts)),
            // “素不相识”是不写出来的默认：想到“昨夜之前彼此如何”这一问之前，人人都这样以为。
            variables.Select(v => v.GetProperty("key").GetString() == "V6" ? (int?)(int)Acquaintance.Strangers : null));
        return (model, predictions);
    }

    /// <summary>某组变量所对应的全部认定（概念名），供写入认知。</summary>
    public static IEnumerable<string> Claims(NightVariables variables)
        => Values(variables).Select((option, question) => Model.ClaimName(question, option));
}
