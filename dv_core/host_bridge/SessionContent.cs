using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    private IReadOnlyDictionary<string, object?> lastContentReload = HostValues.Empty;
    public IReadOnlyDictionary<string, object?> ContentInfo => HostValues.Freeze(new Dictionary<string, object?>
    {
        ["schema"] = ContentCatalog.Schema, ["hash"] = content.Hash,
        ["items"] = content.Items.Count, ["rules"] = content.Rules.Count,
        ["narratives"] = content.NarrativeCount, ["profiles"] = content.ProfileCount,
        ["characters"] = content.Characters.Count, ["layouts"] = content.LayoutCount,
        ["last_reload"] = lastContentReload
    });

    /// <summary>Replace authored definitions at an unprepared action boundary, never a state snapshot.</summary>
    public IReadOnlyDictionary<string, object?> ReloadContent(string? path = null, string? source = null)
    {
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        if (HasPending(state)) throw new InvalidOperationException("Finish the prepared action before reloading content.");
        using var valuesScope = R.UseValues(valueContext);
        using var auditScope = R.UseAudit(audit);
        using var outputScope = R.UseOutput(DiagnosticOutput);
        var next = source is not null ? ContentCatalog.Parse(source) : ContentCatalog.Load(path ?? content.Path
            ?? throw new InvalidOperationException("Specify a content path for embedded/restored definitions."));
        if (next.Hash == content.Hash) return ContentInfo;
        if (next.InitializationDefinitions != content.InitializationDefinitions)
            throw new InvalidOperationException("Narrative and cognitive template changes require a new session.");
        using var contentScope = R.UseContent(next.Data);
        var physical = R.snapshot(Field(state, "physical", new List<object>()));
        var objects = Field(physical, "objects", new List<object>());
        var adoptions = Field(physical, "adoptions", new List<object>());
        var changedInstances = new HashSet<string>(StringComparer.Ordinal);
        var renamedItems = 0;
        var changedCharacters = 0;
        foreach (var entry in R.Items(adoptions))
        {
            var pair = R.Items(entry);
            var adoption = pair[1];
            var law = Field(adoption, "law", new List<object>());
            var id = R.Text(Field(law, "schema", Field(law, "name", "")));
            if (!content.Rules.ContainsKey(id)) continue;
            if (!next.Rules.ContainsKey(id)) throw new InvalidDataException("Cannot remove an adopted live rule: " + id);
            if (content.Rules[id].GetRawText() == next.Rules[id].GetRawText()) continue;
            var replacement = R.record_with(next.Get("rules", id), "name", Field(law, "name", id));
            replacement = R.record_with(replacement, "schema", id);
            adoptions = R.record_with(adoptions, pair[0], R.record_with(adoption, "law", replacement));
            changedInstances.Add(R.Text(pair[0]));
        }
        foreach (var actor in R.Items(Field(state, "actors", new List<object>())))
        {
            var scheduled = Field(actor, "scheduled_action", false);
            if (scheduled is false) continue;
            var action = Field(scheduled, "action", new List<object>());
            var law = Field(action, "law", "") as string ?? "";
            var id = Field(action, "id", "") as string ?? "";
            if (changedInstances.Contains(law) || changedInstances.Contains(id))
                throw new InvalidOperationException("An affected action is scheduled; finish it before reloading: " + id);
        }
        physical = R.record_with(physical, "adoptions", adoptions);
        foreach (var entry in content.Items)
        {
            var existing = Field(objects, entry.Key, false);
            if (existing is false) continue;
            if (!next.Items.TryGetValue(entry.Key, out var definition))
                throw new InvalidDataException("Cannot remove a live item's definition: " + entry.Key);
            var previousName = entry.Value.GetProperty("name").GetString()!;
            var name = definition.GetProperty("name").GetString()!;
            if (Field(existing, "map_title", "") is string title && title == previousName)
                existing = R.record_with(existing, "map_title", name);
            existing = R.record_with(existing, "visible_fields", Field(next.Get("items", entry.Key), "visible_fields", new List<object>()));
            objects = R.record_with(objects, entry.Key, existing);
            physical = R.record_with(physical, "objects", objects);
            var reference = F_dv_ref(entry.Key, "state", "name");
            var cell = F_dv_read(physical, reference);
            if (name != previousName && R.Bool(F_dv_uncontested(cell)) && Field(cell, "value", false) is string old && old == previousName)
            {
                var basis = new List<object> { R.record_with(new List<object>(), "content_hash", next.Hash) };
                physical = F_dv_store(physical, reference, F_dv_cell(name, 1d, Field(physical, "time", 0d), basis));
                renamedItems++;
                objects = Field(physical, "objects", objects);
            }
        }
        var actors = R.Items(Field(state, "actors", new List<object>())).ToList();
        for (var index = 0; index < actors.Count; index++)
        {
            var actor = actors[index];
            var id = R.Text(Field(actor, "id", ""));
            if (!content.Characters.TryGetValue(id, out var previous)) continue;
            if (!next.Characters.TryGetValue(id, out var definition))
                throw new InvalidDataException("Cannot remove a live character definition: " + id);
            if (previous.GetRawText() == definition.GetRawText()) continue;
            if (Field(actor, "scheduled_action", false) is not false)
                throw new InvalidOperationException("Finish this character's scheduled action before reloading its personality or norms: " + id);
            foreach (var structural in new[] { "group", "domain", "station_roles" })
                if (previous.GetProperty("personality").GetProperty(structural).GetRawText() != definition.GetProperty("personality").GetProperty(structural).GetRawText())
                    throw new InvalidOperationException("Structural character changes require a new session: " + structural);
            actors[index] = F_dv_apply_content_policy(actor, next.Get("characters", id), true);
            changedCharacters++;
        }
        physical = R.record_with(physical, "revision", R.Num(Field(physical, "revision", 0d)) + 1d);
        var candidate = R.record_with(R.snapshot(state), "physical", physical);
        candidate = R.record_with(candidate, "actors", actors);
        candidate = R.record_with(candidate, "content_pack_dv", next.Source);
        var events = F_dv_clone(Field(candidate, "events", new List<object>()));
        F_dvs_event(events, "content_reloaded", "", Field(physical, "time", 0d), new List<object>(),
            HostValues.ToDv(new Dictionary<string, object?> { ["previous_hash"] = content.Hash, ["hash"] = next.Hash,
                ["physical_laws_replaced"] = changedInstances.Count, ["belief_policy"] = "existing-minds-retained" }));
        candidate = R.record_with(candidate, "events", events);
        // No live state changes occurred before every definition and boundary check succeeded.
        BeginTransition();
        content = next;
        state = candidate;
        lastContentReload = HostValues.Freeze(new Dictionary<string, object?>
        {
            ["physical_rules"] = changedInstances.Count, ["item_names"] = renamedItems,
            ["characters"] = changedCharacters, ["beliefs_preserved"] = true
        });
        RecordCommittedDecisions();
        RememberCommitted();
        SetPerformanceProvenance(Provenance());
        return ContentInfo;
    }
}
