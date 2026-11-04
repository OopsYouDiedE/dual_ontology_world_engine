using System.Text.Json;
using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    /// <summary>Read-only cards from this viewer's theory; never imports objective evidence.</summary>
    public object CaseScene()
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        if (IsClosed || HasPending(state))
            throw new InvalidOperationException("Scene reasoning requires a committed action boundary.");
        var questions = JsonSerializer.SerializeToElement(CaseQuestions()).GetProperty("questions").Clone();
        var evidence = new List<object>();
        foreach (var caseId in questions.EnumerateArray().Select(q => q.GetProperty("case_id").GetString()!).Distinct(StringComparer.Ordinal))
        {
            var theory = ActorCase(caseId, Viewer);
            var scope = theory.Scopes[Viewer];
            foreach (var axiom in scope.Axioms)
                evidence.Add(new { case_id = caseId, id = axiom.Id, text = axiom.Source, literal = axiom.Literal });
        }
        return new { viewer = Viewer, version = Version, questions, evidence };
    }

    /// <summary>Commit one explicit cognitive attempt with its DV-declared world-time cost.
    /// Browsing cards is free. Repeating the same question against the same retained
    /// actor theory returns the saved attempt without charging or publishing twice.</summary>
    public object SealCaseScene(string caseId, string queryId)
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        var questions = JsonSerializer.SerializeToElement(CaseQuestions()).GetProperty("questions");
        var question = questions.EnumerateArray().Single(q => q.GetProperty("case_id").GetString() == caseId &&
            q.GetProperty("query_id").GetString() == queryId);
        if (IsActorInFlight(Viewer)) throw new InvalidOperationException("Finish the current activity before a cognitive submission.");
        var theory = ActorCase(caseId, Viewer); // Checks committed boundary and observer ownership.
        var theoryHash = DuelView.Core.CaseTheory.Fingerprint(theory.Scopes[Viewer]);
        var attempts = JsonSerializer.Deserialize<List<JsonElement>>((string)Field(state, "case_actor_attempts_json", "[]"))!;
        var previous = attempts.FirstOrDefault(r => r.GetProperty("actor").GetString() == Viewer &&
            r.GetProperty("case_id").GetString() == caseId && r.GetProperty("query_id").GetString() == queryId &&
            r.GetProperty("theory_hash").GetString() == theoryHash);
        if (previous.ValueKind != JsonValueKind.Undefined)
            return new { attempt = previous, repeated = true, charged_seconds = 0d, version = Version,
                sealed_record = previous.GetProperty("sealed_record") };
        var seconds = question.GetProperty("reasoning_seconds").GetDouble();
        if (!double.IsFinite(seconds) || seconds <= 0)
            throw new InvalidDataException("A cognitive submission needs a positive DV reasoning_seconds declaration.");
        var goal = question.GetProperty("goal").Deserialize<DuelView.Core.CaseLiteral>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var result = theory.Prove(Viewer, goal);
        JsonElement? seal = null;
        if (result.Status == "proved" && result.Support is not null)
        {
            var check = theory.CheckGoal(Viewer, result.Support.Certificate, goal);
            if (!check.Valid) throw new InvalidOperationException(string.Join("; ", check.Errors));
            seal = JsonSerializer.SerializeToElement(new { actor = Viewer, case_id = caseId, query_id = queryId,
                label = question.GetProperty("label").GetString(), certificate = result.Support.Certificate });
        }
        var before = R.Num(Field(Field(state, "physical", new List<object>()), "time", 0d));
        var attemptId = Guid.NewGuid().ToString("N");
        // CPU elapsed time is deliberately absent: the cost is authored simulation time.
        // Run on a snapshot; failures leave both the clock and the cognitive archive untouched.
        var updated = Run(() => DVKernel.F_dvs_advance_cognitive_time(R.snapshot(state), seconds / 60d, Viewer, attemptId));
        var after = R.Num(Field(Field(updated, "physical", new List<object>()), "time", 0d));
        var attempt = JsonSerializer.SerializeToElement(new { id = attemptId, actor = Viewer, case_id = caseId,
            query_id = queryId, theory_hash = theoryHash, status = result.Status, result,
            reasoning_seconds = seconds, clock_before = before, clock_after = after, sealed_record = seal });
        attempts.Add(attempt);
        updated = R.record_with(updated, "case_actor_attempts_json", JsonSerializer.Serialize(attempts));
        var records = JsonSerializer.Deserialize<List<JsonElement>>((string)Field(updated, "case_actor_seals_json", "[]"))!;
        records.RemoveAll(r => r.GetProperty("actor").GetString() == Viewer && r.GetProperty("case_id").GetString() == caseId &&
            r.GetProperty("query_id").GetString() == queryId);
        if (seal.HasValue) records.Add(seal.Value);
        updated = R.record_with(updated, "case_actor_seals_json", JsonSerializer.Serialize(records));
        BeginTransition();
        state = updated;
        RememberCommitted();
        return new { attempt, repeated = false, charged_seconds = seconds, sealed_record = seal, version = Version };
    }
}
