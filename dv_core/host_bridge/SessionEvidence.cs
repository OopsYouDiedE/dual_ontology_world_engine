using DuelView.Compiled;

/// <summary>Archive provenance and cuts of the existing authoritative session.</summary>
public sealed partial class DvSession
{
    private long selectedCount, scheduledCount, completedCount;
    private long durableSelectedCount, durableScheduledCount, durableCompletedCount, durableVersion;
    private string executionHost = "unspecified", executionAdapter = "unspecified", sourceRevision = "";
    private string shutdownReason = "";
    private IReadOnlyDictionary<string, object?> evidenceLifecycle = HostValues.Empty;

    public bool PlayerControlled
    {
        get
        {
            using var valuesScope = R.UseValues(valueContext);
            return R.Text(Field(state, "player_actor", "")).Length > 0;
        }
    }

    public void SetExecutionContext(string host, string executor, string revision = "")
    {
        using var valuesScope = R.UseValues(valueContext);
        executionHost = host;
        executionAdapter = executor;
        sourceRevision = revision;
        SetPerformanceProvenance(Provenance());
    }

    private IReadOnlyDictionary<string, object?> Provenance()
    {
        var configuration = Field(Field(state, "story", new List<object>()), "run_config", new List<object>());
        var simulationMode = R.Text(Field(configuration, "simulation_mode", "unrecorded"));
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["host"] = executionHost, ["executor"] = executionAdapter,
            ["godot_execution"] = executionAdapter == "godot",
            ["controller_kind"] = performance.ControllerKind, ["viewer"] = Viewer,
            ["source_revision"] = sourceRevision.Length > 0 ? sourceRevision : null,
            ["assembly_mvid"] = typeof(DVProgram).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["runtime"] = Environment.Version.ToString(), ["audit"] = audit, ["development"] = developer,
            ["simulation_mode"] = simulationMode,
            ["opt_storyflow"] = R.Bool(Field(configuration, "opt_storyflow", true)),
            ["opt_stateindex"] = R.Bool(Field(configuration, "opt_stateindex", true)),
            ["storyflow_semantics"] = "content-policy rewrite; similar intended effects, not trace equivalence",
            ["planning_mode"] = simulationMode,
            ["planning_mode_approximation"] = simulationMode == "approximate-v1" ? true : (bool?)null,
            ["planning_search_scope"] = simulationMode == "approximate-v1" ? "current admissible candidates; immediate utility; no action-tree search" : "unrecorded"
        });
    }

    private void ObserveLifecycle(object entry)
    {
        switch (R.Text(Field(entry, "type", "")))
        {
            case "action_selected": selectedCount++; break;
            case "action_scheduled": scheduledCount++; break;
            case "action_execution": completedCount++; break;
        }
    }

    private static string LastEventId(object value)
    {
        var events = Events(value);
        return events.Count > 0 ? R.Text(Field(events[^1], "id", "")) : "";
    }

    private IReadOnlyList<object?> ScheduledActions(object value)
    {
        var rows = new List<object?>();
        foreach (var actor in R.Items(Field(value, "actors", new List<object>())))
        {
            var queued = Field(actor, "scheduled_action", false);
            if (queued is false) continue;
            var action = Field(queued, "action", new List<object>());
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["actor"] = Field(actor, "id", ""), ["action_id"] = Field(action, "id", ""),
                ["label"] = Field(action, "label", ""), ["action_event"] = Field(queued, "action_event", ""),
                ["start"] = converter.FromDv(Field(queued, "start", false)),
                ["due"] = converter.FromDv(Field(queued, "due", false)), ["state"] = "scheduled"
            }));
        }
        var pending = Field(value, "pending", false);
        if (pending is not false)
        {
            var action = Field(pending, "action", new List<object>());
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["actor"] = Field(pending, "id", ""), ["action_id"] = Field(action, "id", ""),
                ["label"] = Field(action, "label", ""), ["action_event"] = Field(pending, "action_event", ""),
                ["start"] = converter.FromDv(Field(Field(pending, "quote", new List<object>()), "scheduled_start", false)),
                ["due"] = converter.FromDv(Field(pending, "time", false)), ["state"] = "awaiting-host-receipt"
            }));
        }
        return rows.AsReadOnly();
    }

    private static int ScheduledActionCount(object value)
    {
        var count = HasPending(value) ? 1 : 0;
        foreach (var actor in R.Items(Field(value, "actors", new List<object>())))
        {
            // Count only; full labels, timing and source rows remain in the
            // archive projection and are not rebuilt at every transition.
            var slot = (int)R.Num(R.record_index(actor, "scheduled_action"));
            if (slot >= 0 && R.Items(R.Items(actor)[slot])[1] is not false) count++;
        }
        return count;
    }

    private IReadOnlyDictionary<string, object?> EvidenceCut() => HostValues.Freeze(new Dictionary<string, object?>
    {
        ["trace_game_time"] = converter.FromDv(Field(Field(state, "physical", new List<object>()), "time", false)),
        ["durable_game_time"] = converter.FromDv(Field(Field(lastCommittedState, "physical", new List<object>()), "time", false)),
        ["trace_event"] = IsClosed ? LastEventId(closedTrace!) : LastEventId(state),
        ["durable_event"] = LastEventId(lastCommittedState),
        ["state_version"] = Version, ["durable_state_version"] = durableVersion,
        ["selected"] = selectedCount, ["durable_selected"] = durableSelectedCount,
        ["selections_not_in_save"] = selectedCount - durableSelectedCount,
        ["pending_host_receipt"] = HasPending(state), ["shutdown_reason"] = shutdownReason,
        ["decision_scope"] = "new-session-events; restored-history-excluded"
    });

    private void UpdateEvidenceLifecycle()
    {
        evidenceLifecycle = HostValues.Freeze(new Dictionary<string, object?>(EvidenceCut(), StringComparer.Ordinal)
        {
            ["scheduled"] = scheduledCount, ["completed"] = completedCount,
            ["pending"] = ScheduledActionCount(state),
            ["durable_scheduled"] = durableScheduledCount, ["durable_completed"] = durableCompletedCount,
            ["durable_pending"] = ScheduledActionCount(lastCommittedState)
        });
        SetPerformanceLifecycle(evidenceLifecycle);
    }

    private object DecorateTrace(object trace)
    {
        UpdateEvidenceLifecycle();
        var summary = PerformanceSummary();
        var result = R.record_with(trace, "provenance", converter.ToDv(Provenance()));
        result = R.record_with(result, "cut", converter.ToDv(EvidenceCut()));
        result = R.record_with(result, "lifecycle", converter.ToDv(evidenceLifecycle));
        result = R.record_with(result, "pending_actions", converter.ToDv(ScheduledActions(state)));
        result = R.record_with(result, "performance", converter.ToDv(summary));
        result = R.record_with(result, "controller_kind", performance.ControllerKind);
        if (closingPreview is not null && closingPreviewVersion == Version)
        {
            result = R.record_with(result, "closing_preview", converter.ToDv(closingPreview));
            result = R.record_with(result, "closing_preview_state_version", (double)closingPreviewVersion);
            result = R.record_with(result, "state_version", (double)Version);
        }
        return R.record_with(result, "scope", "DV production session; host=" + executionHost + "; executor=" + executionAdapter);
    }
}
