using DuelView.Compiled;

/// <summary>Archive provenance and cuts of the existing authoritative session.</summary>
public sealed partial class DvSession
{
    private long selectedCount, scheduledCount, completedCount;
    private long durableSelectedCount, durableScheduledCount, durableCompletedCount, durableVersion;
    private string executionHost = "unspecified", executionAdapter = "unspecified", sourceRevision = "";
    private string shutdownReason = "";

    public bool PlayerControlled => R.Text(Field(state, "player_actor", "")).Length > 0;

    public void SetExecutionContext(string host, string executor, string revision = "")
    {
        executionHost = host;
        executionAdapter = executor;
        sourceRevision = revision;
        SetPerformanceProvenance(Provenance());
    }

    private IReadOnlyDictionary<string, object?> Provenance() => HostValues.Freeze(new Dictionary<string, object?>
    {
        ["host"] = executionHost, ["executor"] = executionAdapter,
        ["godot_execution"] = executionAdapter == "godot",
        ["controller_kind"] = performance.ControllerKind, ["viewer"] = Viewer,
        ["source_revision"] = sourceRevision.Length > 0 ? sourceRevision : null,
        ["assembly_mvid"] = typeof(DVProgram).Assembly.ManifestModule.ModuleVersionId.ToString(),
        ["runtime"] = Environment.Version.ToString(), ["audit"] = audit, ["development"] = developer
    });

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

    private static IReadOnlyList<object?> ScheduledActions(object value)
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
                ["start"] = HostValues.FromDv(Field(queued, "start", false)),
                ["due"] = HostValues.FromDv(Field(queued, "due", false)), ["state"] = "scheduled"
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
                ["start"] = HostValues.FromDv(Field(Field(pending, "quote", new List<object>()), "scheduled_start", false)),
                ["due"] = HostValues.FromDv(Field(pending, "time", false)), ["state"] = "awaiting-host-receipt"
            }));
        }
        return rows.AsReadOnly();
    }

    private IReadOnlyDictionary<string, object?> EvidenceCut() => HostValues.Freeze(new Dictionary<string, object?>
    {
        ["trace_game_time"] = HostValues.FromDv(Field(Field(state, "physical", new List<object>()), "time", false)),
        ["durable_game_time"] = HostValues.FromDv(Field(Field(lastCommittedState, "physical", new List<object>()), "time", false)),
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
        SetPerformanceLifecycle(HostValues.Freeze(new Dictionary<string, object?>(EvidenceCut(), StringComparer.Ordinal)
        {
            ["scheduled"] = scheduledCount, ["completed"] = completedCount,
            ["pending"] = ScheduledActions(state).Count,
            ["durable_scheduled"] = durableScheduledCount, ["durable_completed"] = durableCompletedCount,
            ["durable_pending"] = ScheduledActions(lastCommittedState).Count
        }));
    }

    private object DecorateTrace(object trace)
    {
        UpdateEvidenceLifecycle();
        var result = R.record_with(trace, "provenance", HostValues.ToDv(Provenance()));
        result = R.record_with(result, "cut", HostValues.ToDv(EvidenceCut()));
        result = R.record_with(result, "lifecycle", HostValues.ToDv(HostValues.Map(PerformanceSummary(), "lifecycle")));
        result = R.record_with(result, "pending_actions", HostValues.ToDv(ScheduledActions(state)));
        result = R.record_with(result, "performance", HostValues.ToDv(PerformanceSummary()));
        result = R.record_with(result, "controller_kind", performance.ControllerKind);
        return R.record_with(result, "scope", "DV production session; host=" + executionHost + "; executor=" + executionAdapter);
    }
}
