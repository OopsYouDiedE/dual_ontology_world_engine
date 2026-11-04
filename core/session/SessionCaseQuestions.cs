using System.Text.Json;
using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    /// <summary>Only authored questions for this viewer and current chapter enter the ordinary UI.</summary>
    public object CaseQuestions()
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        var chapter = Convert.ToInt32(CompactStatus().GetValueOrDefault("chapter"));
        var questions = new List<object>();
        foreach (var pair in CaseSnapshotSources())
        {
            var declared = SnapshotCase(pair.Key);
            if (!declared.Data.TryGetProperty("case_questions", out var section)) continue;
            var entries = section.GetProperty("entries").EnumerateArray().Where(entry =>
                entry.GetProperty("actor").GetString() == Viewer &&
                (!entry.TryGetProperty("chapter", out var requiredChapter) || requiredChapter.GetInt32() == chapter)).ToArray();
            if (entries.Length == 0) continue;
            var scope = declared.ActorTheory.Scopes[Viewer];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var id = entry.GetProperty("id").GetString();
                var title = entry.GetProperty("title").GetString();
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || string.IsNullOrWhiteSpace(title))
                    throw new InvalidDataException("Question identifiers and titles must be nonempty and unique within each case.");
                var goal = entry.GetProperty("goal").Deserialize<CaseLiteral>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (goal is null || !CaseProofChecker.ValidLiteral(goal, true) || !scope.Theorems.Any(t => t.Conclusion.Key == goal!.Key))
                    throw new InvalidDataException("Player question must name a declared query in the viewer's theory.");
                var seconds = entry.TryGetProperty("reasoning_seconds", out var cost) ? cost.GetDouble() : 0d;
                if (!double.IsFinite(seconds) || seconds < 0) throw new InvalidDataException("Invalid question reasoning_seconds.");
                questions.Add(new { case_id = pair.Key, query_id = id, label = title, goal, reasoning_seconds = seconds });
            }
        }
        return new { questions, viewer = Viewer, chapter };
    }
}
