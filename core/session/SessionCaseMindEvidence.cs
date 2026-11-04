using System.Text.Json;
using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    // Read the actor's retained cognition only. A declaration never grants access
    // to physical truth, another mind, or an unexecuted action's predicted result.
    private IEnumerable<CaseAxiom> ReadCaseMindEvidence(string source, string actorId)
    {
        var actor = R.Items(Field(state, "actors", new List<object>()))
            .Single(item => R.Text(Field(item, "id", "")) == actorId);
        return ReadCaseMindEvidence(SnapshotCase(caseId: null, source).Data, actorId, Field(actor, "mind", new List<object>()));
    }

    private IEnumerable<CaseAxiom> ReadCaseMindEvidence(JsonElement data, string actorId, object mind)
        => ReadCaseMindInputs(data, actorId, mind).Evidence;

    private sealed record CaseMindInputs(IReadOnlyList<CaseAxiom> Evidence, string Signature);
    private CaseMindInputs ReadCaseMindInputs(JsonElement data, string actorId, object mind)
    {
        if (!data.TryGetProperty("case_mind_evidence", out var mappings)) return new(Array.Empty<CaseAxiom>(), "no-mappings");
        var result = new List<CaseAxiom>();
        var inputs = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapping in mappings.GetProperty("mappings").EnumerateArray())
        {
            if (mapping.GetProperty("actor").GetString() != actorId) continue;
            var id = mapping.GetProperty("id").GetString() ?? throw new InvalidDataException("Missing evidence mapping id.");
            if (!ids.Add(id)) throw new InvalidDataException("Duplicate evidence mapping id: " + id);
            var reference = mapping.GetProperty("reference");
            var subject = reference.TryGetProperty("subject", out var declaredSubject) ? declaredSubject.GetString() : actorId;
            if (subject != actorId) throw new InvalidDataException("Case evidence cannot read a different subject.");
            var dvRef = DVKernel.F_dv_ref(reference.GetProperty("object").GetString()!,
                reference.GetProperty("facet").GetString()!, reference.GetProperty("field").GetString()!);
            var cell = DVKernel.F_dv_read_query(mind, dvRef);
            var provenance = JsonSerializer.Serialize(converter.FromDv(cell));
            // Include disputed/unknown states and provenance in invalidation, even when no axiom is granted.
            inputs.Add(id + ":" + provenance);
            if (!R.Bool(DVKernel.F_dv_uncontested(cell))) continue;
            if (!R.Bool(R.eq(Field(cell, "value", false), HostValues.ToDv(mapping.GetProperty("equals"))))) continue;
            // Preserve the original cognitive provenance in the proof source.
            // Disputed/unknown cells are deliberately not upgraded to facts.
            var literal = mapping.GetProperty("literal").Deserialize<CaseLiteral>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Invalid evidence literal.");
            result.Add(new CaseAxiom("mind:" + id, literal, "actor-mind:" + actorId + ":" + provenance));
        }
        return new(result.AsReadOnly(), JsonSerializer.Serialize(inputs));
    }
}
