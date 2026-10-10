using System.Globalization;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

public static class Chapter1Expression
{
    public static string Weight(EvidenceWeight weight) => Chapter1Data.Text("来源·" +
        (weight.Source == ObservationSource.Introspection ? weight.Code.ToString() : weight.Source.ToString()),
        ("说话人", weight.Speaker?.Name ?? Chapter1Data.Text("重演·有人")), ("信任", weight.Trust.ToString()), ("强度", weight.Strength.ToString()));
    public static string Basis(ReasonBasis basis) => Chapter1Data.Text("推理·" + basis.Code,
        ("对象", basis.Subject ?? ""), ("字段", basis.Field ?? ""), ("值", basis.Value.ToString()),
        ("选项", basis.Option ?? ""), ("总数", basis.Total.ToString()),
        ("来源", basis.Weight is { } w ? Weight(w) : ""),
        ("因子", basis.Weight?.Factor.ToString("0.##", CultureInfo.InvariantCulture) ?? ""));
    public static string SpeechReason(SpeechReason reason) => Chapter1Data.Text("言语理由·" + reason.Code + (reason.Code == SpeechReasonCode.Implicated && reason.Value != 0 ? "Penalty" : ""),
        ("值", reason.Value.ToString("+0;-0;0", CultureInfo.InvariantCulture)));
}
