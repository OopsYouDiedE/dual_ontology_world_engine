using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    // Completion is authored content, independent of the host and of any
    // actor's knowledge. Only committed world states can earn this record.
    private void RecordChapterCompletions()
    {
        const string rowsTemplate = "template.campaign_completion_rows";
        if (content.Get("data", rowsTemplate) is false) return;
        state = Run(() =>
        {
            var story = Field(state, "story", new List<object>());
            var physical = Field(state, "physical", new List<object>());
            var chapter = Field(story, "chapter", 1d);
            var recorded = R.Items(Field(story, "completed_chapters", new List<object>())).ToList();
            var identities = recorded.Select(row => R.Text(Field(row, "id", ""))).ToHashSet(StringComparer.Ordinal);
            object? events = null;
            foreach (var row in R.Items(DvValueTemplate.Instantiate(rowsTemplate)))
            {
                var id = R.Text(Field(row, "id", ""));
                if (id.Length == 0) throw new InvalidDataException("A campaign completion requires an id.");
                if (identities.Contains(id) || !R.Bool(R.eq(Field(row, "chapter", false), chapter))) continue;
                var condition = Field(row, "condition", "__dv_missing_field_sentinel__");
                if (R.Text(DVKernel.F_dv_truth(physical, condition)) != "true") continue;
                var entry = new List<object>
                {
                    DVKernel.F_dv_pair("id", id),
                    DVKernel.F_dv_pair("chapter", chapter),
                    DVKernel.F_dv_pair("time", Field(physical, "time", 0d)),
                    DVKernel.F_dv_pair("summary_refs", Field(row, "summary_refs", new List<object>()))
                };
                recorded.Add(entry);
                identities.Add(id);
                events ??= DVKernel.F_dv_clone(Field(state, "events", new List<object>()));
                DVKernel.F_dvs_event(events, "chapter_completed", "", Field(physical, "time", 0d), new List<object>(), entry);
            }
            if (events is null) return state;
            story = R.record_with(story, "completed_chapters", recorded);
            return R.record_with(R.record_with(state, "story", story), "events", events);
        });
    }
}
