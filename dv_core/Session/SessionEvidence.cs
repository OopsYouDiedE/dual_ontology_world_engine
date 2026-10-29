using DuelView.Compiled;
using DuelView.Core;

/// <summary>Archive provenance and cuts of the existing authoritative session.</summary>
public sealed partial class DvSession
{
    public IReadOnlyDictionary<string, object?> HostBuildInfo => HostBuildIdentity.Info;

    // Export only diagnostics already recorded by actual engine decisions.
    // Never run a new decision or reconstruct a missing earlier explanation.
    public IReadOnlyDictionary<string, object?> NavigationDiagnostics(long afterEventCursor)
    {
        using var valuesScope = R.UseValues(valueContext);
        var events = Events(state);
        if (afterEventCursor < 0 || afterEventCursor > events.Count)
            throw new ArgumentOutOfRangeException(nameof(afterEventCursor));
        var rows = new List<object?>();
        for (var index = (int)afterEventCursor; index < events.Count; index++)
        {
            var entry = events[index];
            var type = R.Text(Field(entry, "type", ""));
            if (!type.StartsWith("navigation_task_", StringComparison.Ordinal)) continue;
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["event_id"] = R.Text(Field(entry, "id", "")), ["type"] = type,
                ["actor"] = R.Text(Field(entry, "actor", "")),
                ["game_time"] = converter.FromDv(Field(entry, "time", 0d)),
                ["data"] = converter.FromDv(Field(entry, "data", new List<object>()))
            }));
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["scope"] = "actual-engine-events; developer-diagnostic; not-player-knowledge",
            ["after_event_cursor"] = afterEventCursor, ["next_event_cursor"] = events.Count,
            ["events"] = rows.AsReadOnly()
        });
    }
    // Bounded pages of existing reception and decision events; the cursor is
    // advanced only through inspected events, so no evidence is silently lost.
    public IReadOnlyDictionary<string, object?> RequestDiagnostics(long afterEventCursor)
    {
        using var valuesScope = R.UseValues(valueContext);
        var events = Events(state);
        if (afterEventCursor < 0 || afterEventCursor > events.Count)
            throw new ArgumentOutOfRangeException(nameof(afterEventCursor));
        const int pageSize = 200;
        var rows = new List<object?>();
        var sourceActors = new Dictionary<string, string>(StringComparer.Ordinal);
        var byId = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var entry in events)
        {
            byId[R.Text(Field(entry, "id", ""))] = entry;
            if (R.Text(Field(entry, "type", "")) == "speech_literal")
                sourceActors[R.Text(Field(entry, "id", ""))] = R.Text(Field(entry, "actor", ""));
        }
        var next = (int)afterEventCursor;
        for (; next < events.Count && rows.Count < pageSize; next++)
        {
            var entry = events[next];
            var type = R.Text(Field(entry, "type", ""));
            var data = Field(entry, "data", new List<object>());
            object payload;
            string sender = "";
            if (type == "listener_parse")
            {
                var speech = Field(data, "speech", new List<object>());
                var request = Field(speech, "request", false);
                var change = Field(speech, "request_change", false);
                var source = R.Text(Field(data, "source_event", ""));
                sender = sourceActors.TryGetValue(source, out var actualSender) ? actualSender : "";
                object sourceAction = false;
                if (byId.TryGetValue(source, out var literal))
                    foreach (object parent in R.Items(Field(literal, "parents", new List<object>())))
                        if (byId.TryGetValue(R.Text(parent), out var parentEvent) && R.Text(Field(parentEvent, "type", "")) == "action_execution")
                        { sourceAction = Field(Field(parentEvent, "data", new List<object>()), "action", false); break; }
                bool isReply = !R.Bool(R.eq(sourceAction, false)) && R.Bool(DVKernel.F_dv_has_key(sourceAction, "response_topic"));
                if (R.Bool(R.eq(request, false)) && R.Bool(R.eq(change, false)) && !isReply) continue;
                payload = new List<object> { DVKernel.F_dv_pair("source_event", source), DVKernel.F_dv_pair("request", request), DVKernel.F_dv_pair("request_change", change),
                    DVKernel.F_dv_pair("action_id", R.Bool(R.eq(sourceAction, false)) ? "" : Field(sourceAction, "id", "")),
                    DVKernel.F_dv_pair("literal", isReply ? Field(data, "literal", false) : false),
                    DVKernel.F_dv_pair("proposal_decision", isReply ? Field(data, "proposal_decision", false) : false) };
            }
            else if (type == "owned_decision")
            {
                var context = Field(data, "context", new List<object>());
                if (!R.TryItems(Field(context, "requests", new List<object>()), out var requests) || requests.Count == 0) continue;
                payload = data;
            }
            else if (type == "action_execution")
            {
                var action = Field(data, "action", new List<object>());
                if (R.Bool(R.eq(Field(action, "owned_request_requirement", false), false))) continue;
                var receipt = Field(data, "receipt", new List<object>());
                var execution = Field(receipt, "execution", new List<object>());
                payload = new List<object> {
                    DVKernel.F_dv_pair("action_id", Field(action, "id", "")),
                    DVKernel.F_dv_pair("requirement", Field(action, "owned_request_requirement", false)),
                    DVKernel.F_dv_pair("status", Field(execution, "status", "")),
                    DVKernel.F_dv_pair("parents", Field(entry, "parents", new List<object>())) };
            }
            else continue;
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["event_id"] = R.Text(Field(entry, "id", "")), ["type"] = type,
                ["actor"] = R.Text(Field(entry, "actor", "")), ["sender"] = sender ?? "",
                ["game_time"] = converter.FromDv(Field(entry, "time", 0d)),
                ["data"] = converter.FromDv(payload)
            }));
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["scope"] = "actual-engine-events; developer-diagnostic; not-player-knowledge",
            ["after_event_cursor"] = afterEventCursor, ["next_event_cursor"] = next,
            ["page_limit"] = pageSize, ["has_more"] = next < events.Count, ["events"] = rows.AsReadOnly()
        });
    }
    private long selectedCount, scheduledCount, completedCount;
    private long durableSelectedCount, durableScheduledCount, durableCompletedCount, durableVersion;
    private string executionHost = "unspecified", executionAdapter = "unspecified", sourceRevision = "";
    private string shutdownReason = "";
    private IReadOnlyDictionary<string, object?> evidenceLifecycle = HostValues.Empty;
    private string planningDiagnosticEvent = "";
    private IReadOnlyDictionary<string, object?> planningDiagnostic = HostValues.Empty;
    private readonly Dictionary<string, HashSet<string>> coordinationDiagnosticContexts = new(StringComparer.Ordinal);

    // The automatic CLI needs clocks and the existing chapter settlement, not
    // a scene projection or a copy of every character's cognition/evidence.
    public IReadOnlyDictionary<string, object?> CompactStatus()
    {
        using var valuesScope = R.UseValues(valueContext);
        var active = Active;
        var sessionStatus = R.Text(Run(() => F_dvs_session_status(R.snapshot(state)), speculative: true));
        var summary = Run(() => CampaignRuntime.F_dvg_chapter_summary(R.snapshot(state)), speculative: true);
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["version"] = Version,
            ["host_process_id"] = Environment.ProcessId,
            ["host_build_hash"] = HostBuildIdentity.Hash,
            ["chapter"] = converter.FromDv(Field(Field(state, "story", new List<object>()), "chapter", 1d)),
            ["game_time"] = converter.FromDv(Field(Field(state, "physical", new List<object>()), "time", 0d)),
            ["status"] = HasPending(state) ? "prepared" : R.Bool(Field(state, "awaiting_player", false)) ? "awaiting_input" : "idle",
            ["session_status"] = sessionStatus, ["active"] = active,
            ["stop_reason"] = active ? "" : sessionStatus,
            ["executed"] = converter.FromDv(Field(state, "executed", 0d)),
            ["limit"] = converter.FromDv(Field(state, "limit", 0d)),
            ["run_start"] = converter.FromDv(Field(state, "run_start", 0d)),
            ["horizon"] = converter.FromDv(Field(state, "horizon", 0d)),
            ["chapter_summary"] = CompactSummaryValue(summary)
        });
    }

    private object? CompactSummaryValue(object value)
    {
        if (!R.TryItems(value, out var items)) return converter.FromDv(value);
        if (items.Count > 0 && items.All(item => R.TryItems(item, out var pair) && pair.Count == 2 && pair[0] is string))
        {
            var cell = items.Any(item => (string)R.Items(item)[0] == "known") &&
                       items.Any(item => (string)R.Items(item)[0] == "conflicted");
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var pair = R.Items(item);
                var key = (string)pair[0];
                // Preserve unknown and disputed values explicitly. Their full
                // provenance remains in the authoritative session, not this log.
                if (cell && (key is "claims" or "basis" or "confidence" or "time")) continue;
                result[key] = CompactSummaryValue(pair[1]);
            }
            return HostValues.Freeze(result);
        }
        return Array.AsReadOnly(items.Select(CompactSummaryValue).ToArray());
    }

    public IReadOnlyDictionary<string, object?> ExecutionDiagnostics()
    {
        using var valuesScope = R.UseValues(valueContext);
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["status"] = R.Text(Field(state, "status", "")),
            ["executed"] = converter.FromDv(Field(state, "executed", 0)),
            ["limit"] = converter.FromDv(Field(state, "limit", 0)),
            ["game_time"] = converter.FromDv(Field(Field(state, "physical", new List<object>()), "time", 0)),
            ["horizon"] = converter.FromDv(Field(state, "horizon", 0))
        });
    }

    // Read the actual committed selection, without simulating another choice
    // or exposing this diagnostic through the player's subjective projection.
    public IReadOnlyDictionary<string, object?> PlanningDiagnostics()
    {
        using var valuesScope = R.UseValues(valueContext);
        var events = Events(state);
        for (var i = events.Count - 1; i >= 0; i--)
        {
            var entry = events[i];
            if (R.Text(Field(entry, "type", "")) != "action_selected") continue;
            var id = R.Text(Field(entry, "id", ""));
            if (id == planningDiagnosticEvent) return planningDiagnostic;
            planningDiagnosticEvent = id;
            var data = Field(entry, "data", new List<object>());
            planningDiagnostic = HostValues.Freeze(new Dictionary<string, object?>
            {
                ["event"] = id,
                ["actor"] = R.Text(Field(entry, "actor", "")),
                ["game_time"] = converter.FromDv(Field(entry, "time", 0)),
                ["action"] = R.Text(Field(Field(data, "action", new List<object>()), "id", "")),
                ["score"] = converter.FromDv(Field(data, "score", 0)),
                ["strategy"] = R.Text(Field(data, "planning_strategy", "")),
                ["stop"] = R.Text(Field(data, "planning_stop", "")),
                ["plans"] = converter.FromDv(Field(data, "plans", new List<object>()))
            });
            return planningDiagnostic;
        }
        return HostValues.Empty;
    }

    // Temporary, opt-in receipt diagnostics. This reads the prepared actor and
    // the selection event that actually scheduled this pending action. It does
    // not refresh content, choose an action, forecast, or recover missing scores.
    public IReadOnlyDictionary<string, object?> PreparedListeningDiagnostics()
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        var pending = Field(state, "pending", false);
        if (pending is false || R.Text(Field(pending, "id", "")) != "detective" ||
            R.Num(Field(Field(state, "story", new List<object>()), "chapter", 0d)) != 1d)
            return HostValues.Empty;
        var actor = R.snapshot(Field(pending, "actor", new List<object>()));
        var action = Field(pending, "action", new List<object>());
        var actionId = R.Text(Field(action, "id", ""));
        var completed = R.Items(Field(actor, "completed_actions", new List<object>()));
        var lastCompleted = completed.Count > 0 ? R.Text(completed[^1]) : "";
        var intention = Field(actor, "c1_reply_wait_intention", new List<object>());
        static bool Request(string id) => id is "detective.talk.slate_code" or "detective.talk.cups_known";
        if (!Request(actionId) && !Request(lastCompleted) && actionId != "detective.wait.reply" &&
            !R.Bool(Field(intention, "active", false))) return HostValues.Empty;

        var mind = R.snapshot(Field(actor, "mind", new List<object>()));
        var row = R.Items(Field(intention, "row", new List<object>()));
        var requestId = Request(lastCompleted) ? lastCompleted : Request(actionId) ? actionId :
            row.Count > 0 ? R.Text(row[0]) : "";
        var requestContext = Field(Field(actor, "request_contexts", new List<object>()), requestId, false);
        var declaredRequest = Field(Field(action, "speech", new List<object>()), "request", new List<object>());
        var target = requestContext is not false ? R.Text(Field(requestContext, "target", "")) :
            row.Count > 1 ? R.Text(row[1]) : R.Text(Field(declaredRequest, "target", ""));
        var visibleTarget = Field(Field(actor, "visible_context", new List<object>()), target, new List<object>());
        var waitAction = R.Items(Field(actor, "actions", new List<object>()))
            .FirstOrDefault(item => R.Text(Field(item, "id", "")) == "detective.wait.reply");
        var goals = R.Items(Field(Field(actor, "policy", new List<object>()), "goals", new List<object>()))
            .Where(item => R.Bool(Field(item, "c1_reply_wait", false))).ToArray();

        object? Cell(object reference)
        {
            if (reference is false) return "missing-reference";
            var cell = F_dv_read_query(mind, reference);
            return DiagnosticFields(cell, "known", "value", "conflicted");
        }
        string Truth(object proposition) => proposition is false ? "missing" : R.Text(F_dv_truth(mind, proposition));
        var matchingReplies = new List<object?>();
        var receiptStart = (int)R.Num(Field(intention, "receipt_start", 0d));
        var topic = row.Count > 2 ? R.Text(row[2]) : "";
        var replies = R.Items(Field(actor, "received_speeches", new List<object>()));
        for (var index = Math.Max(0, receiptStart); index < replies.Count && matchingReplies.Count < 2; index++)
        {
            var receipt = replies[index];
            var replyId = R.Text(Field(receipt, "action_id", ""));
            if (R.Text(Field(receipt, "speaker", "")) == target &&
                (replyId == target + ".reply." + topic || replyId == target + ".reply.unknown." + topic))
                matchingReplies.Add(DiagnosticFields(receipt, "speaker", "action_id", "parse_event", "source_event"));
        }
        var selection = PreparedSelectionEvent(pending);
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["phase"] = "prepared-before-complete",
            ["version"] = Version,
            ["mind_time"] = converter.FromDv(Field(mind, "time", false)),
            ["mind_revision"] = converter.FromDv(Field(mind, "revision", false)),
            ["decision_time"] = converter.FromDv(Field(actor, "decision_time", false)),
            ["goal_refresh"] = DiagnosticFields(actor, "campaign_goals_revision", "campaign_goals_time", "campaign_goals_chapter"),
            ["last_completed"] = lastCompleted,
            ["last_action_event"] = R.Text(Field(actor, "last_action_event", "")),
            ["request_id"] = requestId,
            ["request_context"] = requestContext is false ? "missing" :
                DiagnosticFields(requestContext, "target", "topic", "location", "presence_event"),
            ["intention"] = DiagnosticFields(intention, "active", "request_event", "intent_started_at", "until", "room", "receipt_start"),
            ["position"] = Cell(Field(actor, "location_ref", false)),
            ["target"] = target,
            ["visible_target_location"] = converter.FromDv(Field(visibleTarget, "location", false)),
            ["known_target_location"] = target.Length == 0 ? "missing-target" : Cell(F_dv_ref(target, "state", "location")),
            ["wait_elapsed"] = Cell(F_dv_ref("night.wait.detective", "state", "elapsed")),
            ["open_words"] = Cell(F_dv_ref("question", "state", "open_words")),
            ["open_trust"] = Cell(F_dv_ref("question", "state", "open_trust")),
            ["requires_evidence"] = Cell(F_dv_ref("question", "state", "requires_evidence")),
            ["received_reply"] = matchingReplies.AsReadOnly(),
            ["wait_candidate"] = waitAction is not null,
            ["prepared_precondition"] = waitAction is null ? "missing-candidate" : Truth(Field(waitAction, "precondition", false)),
            ["wait_goal_count"] = goals.Length,
            ["prepared_goals"] = Array.AsReadOnly(goals.Take(2).Select(goal => (object?)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["available"] = Truth(Field(goal, "available_when", false)),
                ["satisfied"] = Truth(Field(goal, "proposition", false)),
                ["basis"] = converter.FromDv(Field(goal, "basis", new List<object>())),
                ["weight"] = converter.FromDv(Field(goal, "weight", 1d))
            })).ToArray()),
            ["recorded_selection"] = SelectionDiagnostic(selection, actionId),
            ["recorded_wait_score"] = PlanDiagnostic(selection, "detective.wait.reply")
        });
    }

    private IReadOnlyDictionary<string, object?> DiagnosticFields(object value, params string[] keys)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in keys)
            if (R.Num(R.record_index(value, key)) >= 0) result[key] = converter.FromDv(Field(value, key, false));
        return HostValues.Freeze(result);
    }

    public IReadOnlyDictionary<string, object?> PreparedCoordinationDiagnostics()
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        var pending = Field(state, "pending", false);
        if (pending is false || R.Num(Field(Field(state, "story", new List<object>()), "chapter", 0d)) != 2d)
            return HostValues.Empty;
        var actorId = R.Text(Field(pending, "id", ""));
        if (actorId is not ("detective" or "moran" or "elena" or "selma" or "ruan")) return HostValues.Empty;
        if (!coordinationDiagnosticContexts.TryGetValue(actorId, out var seen))
            coordinationDiagnosticContexts[actorId] = seen = new HashSet<string>(StringComparer.Ordinal);
        if (seen.Count >= 12) return HostValues.Empty;
        var actor = R.snapshot(Field(pending, "actor", new List<object>()));
        var mind = R.snapshot(Field(actor, "mind", new List<object>()));
        var actionId = R.Text(Field(Field(pending, "action", new List<object>()), "id", ""));
        static bool Invitation(string id) => id.Contains(".c2.astral.invite.", StringComparison.Ordinal) ||
            id.Contains(".c2.safety.invite.", StringComparison.Ordinal);
        var invitations = R.Items(Field(actor, "received_speeches", new List<object>()))
            .Where(item => Invitation(R.Text(Field(item, "action_id", "")))).ToArray();
        var sent = R.Items(Field(actor, "completed_actions", new List<object>())).Where(item => Invitation(R.Text(item))).ToArray();
        if (invitations.Length == 0 && sent.Length == 0 && !Invitation(actionId)) return HostValues.Empty;
        string Truth(object proposition) => proposition is false ? "missing" : R.Text(F_dv_truth(mind, proposition));
        var position = F_dv_read_query(mind, Field(actor, "location_ref", new List<object>()));
        var room = R.Text(Field(position, "value", ""));
        var goals = R.Items(Field(Field(actor, "policy", new List<object>()), "goals", new List<object>()))
            .Where(goal => R.Num(R.record_index(goal, "navigation_intention")) >= 0 ||
                R.Items(Field(goal, "basis", new List<object>())).Any(basis => R.TryItems(basis, out _) &&
                    R.Text(Field(basis, "id", "")) == "actual-observed-contact-coordination-20261028")).ToArray();
        var goalRows = goals.Take(5).Select(goal => (object?)HostValues.Freeze(new Dictionary<string, object?>
        {
            ["kind"] = R.Text(Field(goal, "kind", "")),
            ["weight"] = converter.FromDv(Field(goal, "weight", 1d)),
            ["room"] = converter.FromDv(Field(goal, "navigation_room", Field(Field(goal, "proposition", new List<object>()), "value", false))),
            ["maintain_presence"] = converter.FromDv(Field(goal, "maintain_presence", false)),
            ["navigation_intention"] = converter.FromDv(Field(goal, "navigation_intention", false)),
            ["available"] = Truth(Field(goal, "available_when", false)),
            ["satisfied"] = Truth(Field(goal, "proposition", false))
        })).ToArray();
        var context = new Dictionary<string, object?>
        {
            ["position"] = DiagnosticFields(position, "known", "value", "conflicted"),
            ["action"] = actionId,
            ["invitation_count"] = invitations.Length,
            ["sent_count"] = sent.Length,
            ["goal_count"] = goals.Length,
            ["goals"] = Array.AsReadOnly(goalRows)
        };
        if (!seen.Add(System.Text.Json.JsonSerializer.Serialize(context))) return HostValues.Empty;
        var selection = PreparedSelectionEvent(pending);
        var plans = selection is false ? new List<object>() : Field(Field(selection, "data", new List<object>()), "plans", new List<object>());
        var top = R.Items(plans).OrderByDescending(plan => R.Num(Field(plan, "score", 0d))).Take(3)
            .Select(plan => (object?)DiagnosticFields(plan, "path", "score", "mind_revision", "evaluated_at_game_time")).ToArray();
        var moves = R.Items(Field(actor, "actions", new List<object>()))
            .Where(item => R.Text(Field(Field(item, "travel", new List<object>()), "from", "")) == room)
            .Take(4).Select(item => (object?)HostValues.Freeze(new Dictionary<string, object?>
            {
                ["action"] = R.Text(Field(item, "id", "")),
                ["to"] = converter.FromDv(Field(Field(item, "travel", new List<object>()), "to", false)),
                ["prepared_precondition"] = Truth(Field(item, "precondition", false)),
                ["recorded_plan"] = PlanDiagnostic(selection, R.Text(Field(item, "id", "")))
            })).ToArray();
        context["phase"] = "prepared-before-complete";
        context["context_index"] = seen.Count;
        context["mind_time"] = converter.FromDv(Field(mind, "time", false));
        context["mind_revision"] = converter.FromDv(Field(mind, "revision", false));
        context["goal_refresh"] = DiagnosticFields(actor, "campaign_goals_revision", "campaign_goals_time", "campaign_goals_chapter");
        context["last_invitation"] = invitations.Length == 0 ? null :
            DiagnosticFields(invitations[^1], "speaker", "action_id", "parse_event", "source_event");
        context["recorded_selection"] = SelectionDiagnostic(selection, actionId);
        context["recorded_plans_status"] = selection is false ? "missing-selection" :
            R.Num(R.record_index(Field(selection, "data", new List<object>()), "plans")) < 0 ? "missing-plans" : "recorded";
        context["recorded_top_three"] = Array.AsReadOnly(top);
        context["outgoing_moves"] = Array.AsReadOnly(moves);
        return HostValues.Freeze(context);
    }

    // Developer evidence only. Read content-declared validation inputs after a
    // real completion; this is never a source of character knowledge or choice.
    public IReadOnlyDictionary<string, object?> ContentDecisionDiagnostics(IReadOnlyDictionary<string, object?> receipt)
    {
        using var valuesScope = R.UseValues(valueContext);
        using var contentScope = R.UseContent(content.Data);
        static object Optional(object value, string key, object fallback) =>
            R.TryItems(value, out _) ? Field(value, key, fallback) : fallback;
        var actorId = HostValues.Text(receipt, "completed_actor");
        var actor = R.Items(Field(state, "actors", new List<object>())).FirstOrDefault(item => R.Text(Field(item, "id", "")) == actorId);
        if (actor is null) return HostValues.Empty;
        var selectedId = HostValues.Text(receipt, "completed_action");
        var actions = R.Items(Field(actor, "actions", new List<object>()));
        var selected = actions.FirstOrDefault(item => R.Text(Field(item, "id", "")) == selectedId);
        if (selected is null) return HostValues.Empty;
        var selectedRefresh = Field(selected, "content_refresh", false);
        if (R.Text(Field(selected, "kind", "")) != "inspect" && selectedRefresh is false && Field(selected, "owned_decision", false) is false)
            return HostValues.Empty;
        var declared = actions.Where(item => Optional(Optional(item, "content_refresh", false), "diagnostic_template", false) is string id && id.Length > 0).ToArray();
        if (declared.Length == 0) return HostValues.Empty;
        var mind = Field(actor, "mind", new List<object>());
        var rows = new List<object?>();
        var validations = new Dictionary<string, object?>(StringComparer.Ordinal);
        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal);
        var decisions = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var action in declared)
        {
            var refresh = Field(action, "content_refresh", false);
            var declaredRefs = Optional(refresh, "diagnostic_refs", false);
            var refs = R.TryItems(declaredRefs, out var referenceItems) ? referenceItems : Array.Empty<object>();
            var template = R.Text(Field(refresh, "diagnostic_template", ""));
            if (!validations.ContainsKey(template))
                validations[template] = converter.FromDv(DvValueTemplate.Instantiate(template, actor));
            foreach (var reference in refs)
            {
                var key = R.Text(F_dv_ref_lookup_key(reference));
                if (evidence.ContainsKey(key)) continue;
                var cell = F_dv_read(mind, reference);
                evidence[key] = HostValues.Freeze(new Dictionary<string, object?>
                {
                    ["ref"] = converter.FromDv(reference),
                    ["cell"] = DiagnosticFields(cell, "known", "value", "conflicted"),
                    ["sources"] = Array.AsReadOnly(R.Items(Field(cell, "claims", new List<object>())).Select(claim =>
                        (object?)DiagnosticFields(claim, "source", "time", "origin", "id", "dependency")).ToArray())
                });
            }
            var declaration = Optional(action, "owned_decision", false);
            var decisionId = R.Text(Optional(declaration, "id", ""));
            if (decisionId.Length > 0 && !decisions.ContainsKey(decisionId))
                decisions[decisionId] = DiagnosticFields(Optional(Field(actor, "owned_decisions", new List<object>()), decisionId, new List<object>()),
                    "stance", "selection_event", "execution_event", "previous_event", "event", "time");
            var request = F_dvs_pending_request(actor, action);
            var requester = R.Text(Optional(request, "requester", ""));
            rows.Add(HostValues.Freeze(new Dictionary<string, object?>
            {
                ["action"] = R.Text(Field(action, "id", "")),
                ["precondition_truth"] = converter.FromDv(F_dv_truth(mind, Field(action, "precondition", F_dv_true()))),
                ["validation_template"] = template,
                ["owned_decision_ready"] = converter.FromDv(F_dvp_owned_decision_ready(actor, action, mind)),
                ["decision_id"] = decisionId,
                ["request"] = DiagnosticFields(request, "requester", "parse_event", "source_event", "topic"),
                ["request_context_available"] = requester.Length > 0 && F_dvs_request_context(actor, action, requester) is not false
            }));
        }
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["scope"] = "developer-detailed-log-only; observational; not player narrative",
            ["phase"] = "after-actual-completion", ["actor"] = actorId,
            ["mind_time"] = converter.FromDv(Field(mind, "time", false)),
            ["owned_decisions"] = HostValues.Freeze(decisions),
            ["visible_context"] = converter.FromDv(Field(actor, "visible_context", new List<object>())),
            ["contexts"] = converter.FromDv(R.Items(actor).Where(pair => R.TryItems(pair, out var parts) && parts.Count == 2 && parts[0] is string name && name.EndsWith("_evidence_context", StringComparison.Ordinal)).ToList()),
            ["received_speeches_tail"] = Array.AsReadOnly(R.Items(Field(actor, "received_speeches", new List<object>())).TakeLast(4).Select(item =>
                (object?)DiagnosticFields(item, "speaker", "action_id", "parse_event", "source_event")).ToArray()),
            ["validations"] = HostValues.Freeze(validations),
            ["evidence"] = Array.AsReadOnly(evidence.Values.ToArray()),
            ["candidates"] = Array.AsReadOnly(rows.ToArray())
        });
    }

    private object PreparedSelectionEvent(object pending)
    {
        var events = Field(state, "events", new List<object>());
        var actionId = R.Text(Field(Field(pending, "action", new List<object>()), "id", ""));
        var actorId = R.Text(Field(pending, "id", ""));
        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue(R.Text(Field(pending, "parent", "")));
        while (queue.Count > 0 && visited.Count < 32)
        {
            var id = queue.Dequeue();
            if (id.Length == 0 || !visited.Add(id)) continue;
            var entry = F_dvs_find_event_by_id(events, id);
            if (entry is false) continue;
            var data = Field(entry, "data", new List<object>());
            if (R.Text(Field(entry, "type", "")) == "action_selected" &&
                R.Text(Field(entry, "actor", "")) == actorId &&
                R.Text(Field(Field(data, "action", new List<object>()), "id", "")) == actionId)
                return entry;
            foreach (var parent in R.Items(Field(entry, "parents", new List<object>()))) queue.Enqueue(R.Text(parent));
        }
        return false;
    }

    private IReadOnlyDictionary<string, object?> SelectionDiagnostic(object selection, string actionId)
    {
        if (selection is false) return HostValues.Freeze(new Dictionary<string, object?> { ["status"] = "missing-linked-action-selected" });
        var data = Field(selection, "data", new List<object>());
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["event"] = R.Text(Field(selection, "id", "")),
            ["game_time"] = converter.FromDv(Field(selection, "time", false)),
            ["action"] = actionId,
            ["strategy"] = R.Text(Field(data, "planning_strategy", "")),
            ["score"] = converter.FromDv(Field(data, "score", false)),
            ["plan"] = PlanDiagnostic(selection, actionId)
        });
    }

    private IReadOnlyDictionary<string, object?> PlanDiagnostic(object selection, string actionId)
    {
        if (selection is not false)
        {
            var data = Field(selection, "data", new List<object>());
            foreach (var plan in R.Items(Field(data, "plans", new List<object>())))
            {
                var path = R.Items(Field(plan, "path", new List<object>()));
                if (path.Count == 0 || R.Text(path[0]) != actionId) continue;
                var score = Field(Field(plan, "value", new List<object>()), "current_score", new List<object>());
                return HostValues.Freeze(new Dictionary<string, object?>
                {
                    ["status"] = "recorded",
                    ["score"] = converter.FromDv(Field(plan, "score", false)),
                    ["mind_revision"] = converter.FromDv(Field(plan, "mind_revision", false)),
                    ["evaluated_at_game_time"] = converter.FromDv(Field(plan, "evaluated_at_game_time", false)),
                    ["scope"] = R.Text(Field(plan, "score_scope", "")),
                    ["utility"] = DiagnosticFields(score, "action_value", "information_component", "progress_component", "time_penalty", "total", "components")
                });
            }
        }
        return HostValues.Freeze(new Dictionary<string, object?> { ["status"] = "missing-recorded-plan", ["action"] = actionId });
    }

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
            ["assembly_mvid"] = typeof(CampaignRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["runtime"] = Environment.Version.ToString(), ["audit"] = audit, ["development"] = developer,
            ["content_hash"] = content.Hash, ["content_schema"] = ContentCatalog.Schema,
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
            if (Field(actor, "scheduled_action", false) is not false) count++;
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
        result = R.record_with(result, "content_pack_dv", content.Source);
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
