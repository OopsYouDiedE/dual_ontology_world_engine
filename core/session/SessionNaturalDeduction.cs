using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    /// <summary>Proof terms are untrusted input; their premises come only from the retained actor theory.</summary>
    public object CheckNaturalDeductionActor(string caseId, string goal, string proof)
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        if (IsClosed || HasPending(state))
            throw new InvalidOperationException("Natural deduction requires a committed action boundary.");
        if (Viewer == "objective") throw new InvalidOperationException("An objective theory is not an actor's knowledge.");
        var declared = SnapshotCase(caseId);
        var scopes = declared.Data.GetProperty("natural_deduction").GetProperty("scopes");
        var matches = scopes.EnumerateArray().Where(scope => scope.GetProperty("id").GetString() == Viewer).ToArray();
        if (matches.Length != 1) throw new ArgumentException("Exactly one natural deduction theory must be declared for the current viewer.");
        var scope = matches[0];
        var request = new Dictionary<string, object?>
        {
            ["scope"] = Viewer, ["goal"] = goal, ["proof"] = proof,
            ["axioms"] = scope.GetProperty("axioms").Clone(),
            ["theorems"] = scope.TryGetProperty("theorems", out var theorems) ? theorems.Clone() : Array.Empty<object>()
        };
        var result = NaturalDeduction.Check(JsonSerializer.SerializeToElement(request));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope.GetRawText())));
        return new { scope = Viewer, theory_hash = hash, result };
    }
}
