using DuelView.Compiled;

namespace DuelView.Core;

public static partial class DVKernel
{
    public static object F_dvs_navigation_task_completed(object actor, object task)
    {
        foreach (object item in R.Items(F_dv_find(actor, "navigation_task_history", new List<object>())))
            if (R.Bool(R.eq(F_dv_find(item, "reason", ""), "completed")) &&
                R.Bool(F_dv_same(F_dv_find(item, "task_id", false), F_dv_get(task, "task_id"))) &&
                R.Bool(F_dv_same(F_dv_find(item, "phase", false), F_dv_get(task, "phase")))) return true;
        return false;
    }
    // Only authored tasks with a complete contract and an explicit next hop
    // produce candidates. Base actions remain available and unchanged.
    // Request contexts are tied to the actually received request and the
    // recipient's own recorded acceptance, never inferred from proximity.
    public static object F_dvs_navigation_request_status(object actor, object task)
    {
        object context = F_dv_get(task, "request_context");
        if (!R.Bool(R.is_list(context)) || !R.Bool(F_dv_record_like(context))) throw new InvalidOperationException("navigation_task: request_context must be a record");
        object kind = F_dv_find(context, "kind", "");
        if (R.Bool(R.eq(kind, "none"))) return new List<object> { F_dv_pair("status", "valid"), F_dv_pair("source", "") };
        if (!R.Bool(R.eq(kind, "received"))) throw new InvalidOperationException("navigation_task: unsupported request_context kind");
        string[] fields = { "requester", "topic", "source_event", "parse_event", "decision_id", "decision_event" };
        foreach (string field in fields)
            if (F_dv_find(context, field, false) is not string text || string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException($"navigation_task {R.Text(F_dv_get(task, "task_id"))}: request_context requires nonempty {field}");
        object request = false;
        foreach (object item in R.Items(F_dv_find(actor, "received_requests", new List<object>())))
            if (R.Bool(R.eq(F_dv_find(item, "requester", ""), F_dv_get(context, "requester"))) &&
                R.Bool(R.eq(F_dv_find(item, "topic", ""), F_dv_get(context, "topic"))) &&
                R.Bool(R.eq(F_dv_find(item, "source_event", ""), F_dv_get(context, "source_event"))) &&
                R.Bool(R.eq(F_dv_find(item, "parse_event", ""), F_dv_get(context, "parse_event")))) { request = item; break; }
        if (R.Bool(R.eq(request, false))) return new List<object> { F_dv_pair("status", "unknown"), F_dv_pair("source", "") };
        foreach (object change in R.Items(F_dv_find(actor, "received_request_changes", new List<object>())))
            if (R.Bool(F_dv_find(change, "matched_received_request", false)) &&
                R.Bool(R.eq(F_dv_find(change, "request_source_event", ""), F_dv_get(context, "source_event"))) &&
                R.Bool(R.eq(F_dv_find(change, "requester", ""), F_dv_get(context, "requester"))))
                return new List<object> { F_dv_pair("status", "withdrawn"), F_dv_pair("source", F_dv_find(change, "parse_event", "")) };
        object decision = F_dv_find(F_dv_find(actor, "owned_decisions", new List<object>()), F_dv_get(context, "decision_id"), false);
        if (R.Bool(R.eq(decision, false))) return new List<object> { F_dv_pair("status", "unknown"), F_dv_pair("source", "") };
        if (!R.Bool(R.eq(F_dv_find(decision, "event", ""), F_dv_get(context, "decision_event"))) || !R.Bool(R.eq(F_dv_find(decision, "stance", ""), "accepted")))
            return new List<object> { F_dv_pair("status", "acceptance-changed"), F_dv_pair("source", F_dv_find(decision, "event", "")) };
        object decisionContext = F_dv_find(decision, "context", new List<object>());
        foreach (object entry in R.Items(F_dv_find(decisionContext, "requests", new List<object>())))
        {
            object receipt = F_dv_find(entry, "receipt", false);
            if (R.Bool(R.eq(receipt, false))) continue;
            if (R.Bool(R.eq(F_dv_find(entry, "topic", ""), F_dv_get(context, "topic"))) &&
                R.Bool(R.eq(F_dv_find(receipt, "requester", ""), F_dv_get(context, "requester"))) &&
                R.Bool(R.eq(F_dv_find(receipt, "source_event", ""), F_dv_get(context, "source_event"))) &&
                R.Bool(R.eq(F_dv_find(receipt, "parse_event", ""), F_dv_get(context, "parse_event"))))
                return new List<object> { F_dv_pair("status", "valid"), F_dv_pair("source", F_dv_get(context, "decision_event")) };
        }
        return new List<object> { F_dv_pair("status", "unknown"), F_dv_pair("source", "") };
    }

    public static object F_dvs_navigation_task_candidates(object actor)
    {
        object result = new List<object>();
        object actions = F_dv_get(actor, "actions");
        object goals = F_dv_find(F_dv_get(actor, "policy"), "goals", new List<object>());
        object mind = F_dv_get(actor, "mind");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (object action in R.Items(actions)) ids.Add(R.Text(F_dv_get(action, "id")));
        var tasks = new List<object>();
        var taskKeys = new HashSet<string>(StringComparer.Ordinal);
        static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        foreach (object goal in R.Items(goals))
        {
            object task = F_dv_find(goal, "navigation_task", false);
            if (R.Bool(R.eq(task, false))) continue;
            if (!R.Bool(R.is_list(task)) || !R.Bool(F_dv_record_like(task))) throw new InvalidOperationException("navigation_task must be a record");
            object labelValue = F_dv_find(task, "task_id", "");
            string taskLabel = labelValue is string label ? label : "<invalid>";
            string[] required = { "task_id", "phase", "request_context", "priority", "valid_when", "complete_when", "preempt_when", "origin", "next", "target", "base_action_id" };
            foreach (string field in required) if (!R.Bool(F_dv_has_key(task, field))) throw new InvalidOperationException($"navigation_task {taskLabel}: missing {field}");
            if (F_dv_get(task, "task_id") is not string id || string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("navigation_task: task_id must be nonempty text");
            if (F_dv_get(task, "phase") is not string phase || string.IsNullOrWhiteSpace(phase)) throw new InvalidOperationException($"navigation_task {id}: phase must be nonempty text");
            object priority = F_dv_get(task, "priority");
            if (!R.Bool(R.is_numeric_value(priority))) throw new InvalidOperationException($"navigation_task {id}: priority must be a finite number");
            if (R.Bool(F_dvs_navigation_task_completed(actor, task))) continue;
            string key = Encode(id) + ":" + Encode(phase);
            if (!taskKeys.Add(key)) throw new InvalidOperationException($"navigation_task {id}: duplicate task_id and phase");
            object context = F_dv_get(task, "request_context");
            if (!R.Bool(R.is_list(context)) || !R.Bool(F_dv_record_like(context)) || !R.Bool(F_dv_has_key(context, "kind"))) throw new InvalidOperationException($"navigation_task {id}: invalid request_context");
            object requestStatus = F_dvs_navigation_request_status(actor, task);
            if (!R.Bool(R.eq(F_dv_get(requestStatus, "status"), "valid"))) continue;
            object basis = F_dv_find(goal, "basis", new List<object>());
            if (!R.Bool(R.eq(F_dv_find(goal, "kind", ""), "achieve")) || !R.Bool(R.lt(R.number("0"), F_dv_find(goal, "weight", R.number("1")))) ||
                !R.Bool(R.is_list(basis)) || !R.Bool(R.lt(R.number("0"), R.count(basis))) ||
                !R.Bool(R.eq(F_dv_truth(mind, F_dv_find(goal, "available_when", F_dv_true())), "true")) ||
                !R.Bool(R.eq(F_dv_truth(mind, F_dv_get(task, "valid_when")), "true")) ||
                !R.Bool(R.eq(F_dv_truth(mind, F_dv_get(task, "complete_when")), "false"))) continue;
            object commitment = F_dv_find(actor, "navigation_commitment", false);
            if (!R.Bool(R.eq(commitment, false)))
            {
                object held = F_dv_get(commitment, "task");
                bool same = R.Bool(F_dv_same(F_dv_get(held, "task_id"), id)) && R.Bool(F_dv_same(F_dv_get(held, "phase"), phase));
                object heldValid = F_dv_truth(mind, F_dv_get(held, "valid_when"));
                object heldComplete = F_dv_truth(mind, F_dv_get(held, "complete_when"));
                if (same && (!R.Bool(F_dv_same(F_dv_get(held, "target"), F_dv_get(task, "target"))) ||
                    !R.Bool(F_dv_same(F_dv_get(held, "request_context"), F_dv_get(task, "request_context"))) ||
                    !R.Bool(R.eq(heldValid, "true")) || !R.Bool(R.eq(heldComplete, "false")))) continue;
                if (!same && (!R.Bool(R.lt(F_dv_get(held, "priority"), priority)) ||
                    !R.Bool(R.eq(F_dv_truth(mind, F_dv_get(held, "preempt_when")), "true")))) continue;
            }
            tasks.Add(task);
        }
        foreach (object action in R.Items(actions))
        {
            if (R.Bool(R.eq(F_dv_find(action, "kind", ""), "move")) && R.Bool(F_dv_has_key(action, "travel")))
            {
                object travel = F_dv_get(action, "travel");
                foreach (object task in tasks)
                {
                    if (!R.Bool(R.eq(F_dv_get(task, "origin"), F_dv_find(travel, "from", false))) || !R.Bool(R.eq(F_dv_get(task, "next"), F_dv_find(travel, "to", false))) ||
                        !R.Bool(R.eq(F_dv_get(task, "base_action_id"), F_dv_get(action, "id")))) continue;
                    string baseId = R.Text(F_dv_get(action, "id"));
                    string candidateId = "nav-task:" + Encode(baseId) + ":" + Encode(R.Text(F_dv_get(task, "task_id"))) + ":" + Encode(R.Text(F_dv_get(task, "phase")));
                    if (!ids.Add(candidateId)) throw new InvalidOperationException($"navigation_task {R.Text(F_dv_get(task, "task_id"))}: duplicate or conflicting candidate {candidateId}");
                    object variant = F_dv_with_many(action, new List<object> { F_dv_pair("id", candidateId), F_dv_pair("base_action_id", baseId), F_dv_pair("navigation_task", task) });
                    _ = R.push(result, variant);
                }
            }
            _ = R.push(result, action);
        }
        return result;
    }

    public static object F_dvs_navigation_task_diagnostics(object actor, object choice)
    {
        object rows = new List<object>();
        object mind = F_dv_get(actor, "mind");
        object goals = F_dv_find(F_dv_get(actor, "policy"), "goals", new List<object>());
        object plans = F_dv_find(choice, "plans", new List<object>());
        object selected = F_dv_find(choice, "action", false);
        if (!R.Bool(R.eq(selected, false)) && R.Bool(R.eq(F_dv_find(selected, "kind", ""), "move")))
        {
            foreach (object plan in R.Items(plans))
            {
                object path = F_dv_find(plan, "path", new List<object>());
                if (!R.Bool(R.lt(R.number("0"), R.count(path))) || !R.Bool(R.eq(R.at(path, R.number("0")), F_dv_get(selected, "id")))) continue;
                object currentScore = F_dv_find(F_dv_find(plan, "value", new List<object>()), "current_score", new List<object>());
                object attribution = F_dv_find(currentScore, "achievement_goals", new List<object>());
                if (R.Bool(R.lt(R.number("0"), R.count(attribution))))
                    _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("kind", "selected-move-achievement"), F_dv_pair("action_id", F_dv_get(selected, "id")), F_dv_pair("goals", attribution) });
                break;
            }
        }
        foreach (object action in R.Items(F_dv_get(actor, "actions")))
        {
            if (!R.Bool(F_dv_has_key(action, "owned_request_requirement"))) continue;
            object id = F_dv_get(action, "id");
            object score = false;
            foreach (object plan in R.Items(plans))
            {
                object path = F_dv_find(plan, "path", new List<object>());
                if (R.Bool(R.lt(R.number("0"), R.count(path))) && R.Bool(R.eq(R.at(path, R.number("0")), id))) { score = F_dv_find(plan, "score", false); break; }
            }
            _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("kind", "owned-request-action"), F_dv_pair("action_id", id),
                F_dv_pair("request_ready", F_dvp_owned_request_ready(actor, action)), F_dv_pair("subjective_precondition", F_dv_truth(mind, F_dv_get(action, "precondition"))),
                F_dv_pair("pending_request_available", R.lt(R.number("0"), R.count(F_dvs_pending_request(actor, action)))), F_dv_pair("score", score),
                F_dv_pair("selected", !R.Bool(R.eq(selected, false)) && R.Bool(R.eq(F_dv_find(selected, "id", ""), id))) });
        }
        foreach (object action in R.Items(F_dv_get(actor, "actions")))
        {
            if (!R.Bool(R.eq(F_dv_find(action, "kind", ""), "inspect"))) continue;
            bool matchesFrontier = false;
            foreach (object goal in R.Items(goals))
            {
                if (!R.Bool(F_dv_find(goal, "navigation_gate_probe", false))) continue;
                foreach (object grant in R.Items(F_dv_find(action, "grants", new List<object>())))
                {
                    if (!R.Bool(R.is_list(grant)) || !R.Bool(F_dv_record_like(grant)) || !R.Bool(F_dv_has_key(grant, "type")) ||
                        !R.Bool(F_dv_has_key(grant, "object")) || !R.Bool(F_dv_has_key(grant, "facet")) || !R.Bool(F_dv_has_key(grant, "field"))) continue;
                    if (R.Bool(F_dv_same_local_ref(F_dv_get(actor, "id"), grant, F_dv_get(goal, "ref")))) { matchesFrontier = true; break; }
                }
                if (matchesFrontier) break;
            }
            if (!matchesFrontier) continue;
            object id = F_dv_get(action, "id");
            object score = false;
            foreach (object plan in R.Items(plans))
            {
                object path = F_dv_find(plan, "path", new List<object>());
                if (R.Bool(R.lt(R.number("0"), R.count(path))) && R.Bool(R.eq(R.at(path, R.number("0")), id))) { score = F_dv_find(plan, "score", false); break; }
            }
            _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("kind", "gate-probe-action"), F_dv_pair("action_id", id), F_dv_pair("score", score),
                F_dv_pair("reason", R.Bool(R.eq(score, false)) ? "not-scored-or-ineligible" : "scored") });
        }
        foreach (object goal in R.Items(goals))
            if (R.Bool(F_dv_find(goal, "navigation_gate_probe", false)))
                _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("kind", "gate-probe-goal"), F_dv_pair("ref", F_dv_get(goal, "ref")), F_dv_pair("available", F_dv_truth(mind, F_dv_find(goal, "available_when", F_dv_true()))) });
        object commitment = F_dv_find(actor, "navigation_commitment", false);
        if (!R.Bool(R.eq(commitment, false)))
        {
            object heldTask = F_dv_get(commitment, "task");
            object heldValid = F_dv_truth(mind, F_dv_get(heldTask, "valid_when"));
            object heldComplete = F_dv_truth(mind, F_dv_get(heldTask, "complete_when"));
            object heldRequestStatus = F_dv_get(F_dvs_navigation_request_status(actor, heldTask), "status");
            string heldReason = R.Bool(R.eq(heldRequestStatus, "unknown")) ? "request-unknown-paused" : !R.Bool(R.eq(heldValid, "true")) && !R.Bool(R.eq(heldValid, "false")) ? "validity-unknown-paused" :
                !R.Bool(R.eq(heldComplete, "true")) && !R.Bool(R.eq(heldComplete, "false")) ? "completion-unknown-paused" :
                R.Bool(R.eq(heldComplete, "true")) ? "completion-awaiting-objective-verification" : "held";
            _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("held", true), F_dv_pair("task_id", F_dv_get(heldTask, "task_id")),
                F_dv_pair("phase", F_dv_get(heldTask, "phase")), F_dv_pair("task_valid", heldValid), F_dv_pair("task_complete", heldComplete), F_dv_pair("reason", heldReason) });
        }
        foreach (object goal in R.Items(goals))
        {
            object task = F_dv_find(goal, "navigation_task", false);
            if (R.Bool(R.eq(task, false)))
            {
                if (R.Bool(F_dv_has_key(goal, "navigation_intention")))
                    _ = R.push(rows, new List<object> { F_dv_pair("declared", false), F_dv_pair("reason", "no-task-declaration") });
                continue;
            }
            if (!R.Bool(R.is_list(task)) || !R.Bool(F_dv_record_like(task)) || !R.Bool(F_dv_has_key(task, "task_id")) ||
                !R.Bool(F_dv_has_key(task, "phase")) || !R.Bool(F_dv_has_key(task, "valid_when")) ||
                !R.Bool(F_dv_has_key(task, "complete_when")) || !R.Bool(F_dv_has_key(task, "priority")) || !R.Bool(F_dv_has_key(task, "preempt_when")) ||
                !R.Bool(F_dv_has_key(task, "origin")) || !R.Bool(F_dv_has_key(task, "next")) ||
                !R.Bool(F_dv_has_key(task, "target")) || !R.Bool(F_dv_has_key(task, "request_context")) || !R.Bool(F_dv_has_key(task, "base_action_id")))
            { _ = R.push(rows, new List<object> { F_dv_pair("declared", true), F_dv_pair("reason", "invalid-task-declaration") }); continue; }
            object goalTruth = F_dv_truth(mind, F_dv_find(goal, "available_when", F_dv_true()));
            object valid = F_dv_truth(mind, F_dv_get(task, "valid_when"));
            object completed = F_dv_truth(mind, F_dv_get(task, "complete_when"));
            int matching = 0;
            foreach (object action in R.Items(F_dv_get(actor, "actions")))
            {
                object travel = F_dv_find(action, "travel", false);
                if (!R.Bool(R.eq(travel, false)) && R.Bool(R.eq(F_dv_find(action, "kind", ""), "move")) &&
                    R.Bool(R.eq(F_dv_find(travel, "from", false), F_dv_get(task, "origin"))) && R.Bool(R.eq(F_dv_find(travel, "to", false), F_dv_get(task, "next"))) &&
                    R.Bool(R.eq(F_dv_get(action, "id"), F_dv_get(task, "base_action_id")))) matching++;
            }
            object basis = F_dv_find(goal, "basis", new List<object>());
            object context = F_dv_find(task, "request_context", false);
            object held = F_dv_find(actor, "navigation_commitment", false);
            bool supportedContext = R.Bool(R.is_list(context)) && R.Bool(F_dv_record_like(context)) && R.Bool(R.eq(F_dv_get(F_dvs_navigation_request_status(actor, task), "status"), "valid"));
            bool allowedByHeld = true;
            if (!R.Bool(R.eq(held, false)))
            {
                object heldTask = F_dv_get(held, "task");
                bool same = R.Bool(F_dv_same(F_dv_get(heldTask, "task_id"), F_dv_get(task, "task_id"))) && R.Bool(F_dv_same(F_dv_get(heldTask, "phase"), F_dv_get(task, "phase")));
                allowedByHeld = same ? R.Bool(F_dv_same(F_dv_get(heldTask, "target"), F_dv_get(task, "target"))) &&
                    R.Bool(F_dv_same(F_dv_get(heldTask, "request_context"), F_dv_get(task, "request_context"))) &&
                    R.Bool(R.eq(F_dv_truth(mind, F_dv_get(heldTask, "valid_when")), "true")) && R.Bool(R.eq(F_dv_truth(mind, F_dv_get(heldTask, "complete_when")), "false")) :
                    R.Bool(R.lt(F_dv_get(heldTask, "priority"), F_dv_get(task, "priority"))) && R.Bool(R.eq(F_dv_truth(mind, F_dv_get(heldTask, "preempt_when")), "true"));
            }
            string reason = !R.Bool(R.eq(F_dv_find(goal, "kind", ""), "achieve")) ? "goal-kind" :
                !R.Bool(R.lt(R.number("0"), F_dv_find(goal, "weight", R.number("1")))) ? "goal-weight" :
                !R.Bool(R.is_list(basis)) || !R.Bool(R.lt(R.number("0"), R.count(basis))) ? "goal-basis" :
                !supportedContext ? "unsupported-request-context" : !R.Bool(R.eq(goalTruth, "true")) ? "goal-not-known-available" :
                !R.Bool(R.eq(valid, "true")) ? "task-not-known-valid" :
                !R.Bool(R.eq(completed, "false")) ? "completion-not-known-false" : !allowedByHeld ? "held-task-or-preemption" :
                matching == 0 ? "no-matching-move" : "eligible-for-binding";
            _ = R.push(rows, new List<object> { F_dv_pair("declared", true), F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")),
                F_dv_pair("goal_available", goalTruth), F_dv_pair("task_valid", valid), F_dv_pair("task_complete", completed), F_dv_pair("matching_moves", (double)matching), F_dv_pair("reason", reason) });
        }
        return rows;
    }

    public static object F_dvs_navigation_task_dispatch(object actor, object action, object actionEvent)
    {
        object held = F_dv_find(actor, "navigation_commitment", false);
        if (R.Bool(R.eq(held, false)) || !R.Bool(R.eq(F_dv_find(held, "candidate_id", false), F_dv_find(action, "navigation_candidate_id", false)))) return actor;
        return F_dv_with(actor, "navigation_commitment", F_dv_with(held, "pending_action_event", actionEvent));
    }

    public static object F_dvs_navigation_task_sync(object actor, object events, object time, object physical)
    {
        object held = F_dv_find(actor, "navigation_commitment", false);
        if (R.Bool(R.eq(held, false))) return actor;
        object task = F_dv_get(held, "task");
        object mind = F_dv_get(actor, "mind");
        string reason = "";
        object requestStatus = F_dvs_navigation_request_status(actor, task);
        object requestState = F_dv_get(requestStatus, "status");
        object source = F_dv_find(held, "last_execution_event", F_dv_find(held, "selection_event", ""));
        if (R.Bool(R.eq(requestState, "withdrawn")) || R.Bool(R.eq(requestState, "acceptance-changed")))
        { reason = R.Bool(R.eq(requestState, "withdrawn")) ? "request-withdrawn" : "acceptance-changed"; source = F_dv_get(requestStatus, "source"); }
        object actionEvent = F_dv_find(held, "pending_action_event", F_dv_find(held, "last_action_event", ""));
        object cursor = F_dv_find(held, "event_cursor", R.count(events));
        if (R.Bool(R.lt(R.count(events), cursor))) cursor = R.count(events);
        for (object i = cursor; reason.Length == 0 && R.Bool(R.lt(i, R.count(events))); i = R.add(i, R.number("1")))
        {
            object evt = R.at(events, i);
            if (!R.Bool(R.eq(F_dv_get(evt, "actor"), F_dv_get(actor, "id"))) || !R.Bool(R.eq(F_dv_get(evt, "type"), "action_execution"))) continue;
            object data = F_dv_get(evt, "data");
            object action = F_dv_find(data, "action", false);
            if (R.Bool(R.eq(action, false)) || !R.Bool(R.eq(F_dv_find(action, "navigation_candidate_id", false), F_dv_find(held, "candidate_id", false))) ||
                R.Bool(R.eq(F_dv_find(held, "pending_action_event", false), false)) ||
                !R.Bool(R.eq(F_dv_find(action, "navigation_action_event", false), F_dv_get(held, "pending_action_event")))) continue;
            object receipt = F_dv_find(data, "receipt", false);
            object execution = R.Bool(R.eq(receipt, false)) ? false : F_dv_find(receipt, "execution", false);
            if (!R.Bool(R.eq(execution, false)))
            {
                source = F_dv_get(evt, "id");
                actionEvent = F_dv_find(action, "navigation_action_event", "");
                if (!R.Bool(R.eq(F_dv_find(execution, "status", ""), "applied"))) reason = "move-not-applied";
            }
        }
        if (reason.Length == 0 && R.Bool(R.eq(F_dv_truth(mind, F_dv_get(task, "complete_when")), "true")) &&
            R.Bool(R.eq(F_dv_truth(physical, F_dv_get(task, "complete_when")), "true"))) reason = "completed";
        else if (reason.Length == 0 && R.Bool(R.eq(F_dv_truth(mind, F_dv_get(task, "valid_when")), "false"))) reason = "condition-invalidated";
        if (reason.Length != 0)
        {
            object release = F_dvs_event(events, "navigation_task_released", F_dv_get(actor, "id"), time, F_dv_single(source),
                new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("reason", reason), F_dv_pair("action_event", actionEvent) });
            object history = F_dv_append(F_dv_find(actor, "navigation_task_history", new List<object>()),
                new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("reason", reason), F_dv_pair("event", release) });
            return F_dv_with_many(actor, new List<object> { F_dv_pair("navigation_commitment", false), F_dv_pair("navigation_task_history", history) });
        }
        return F_dv_with(actor, "navigation_commitment", F_dv_with_many(held, new List<object> { F_dv_pair("event_cursor", R.count(events)), F_dv_pair("last_execution_event", source), F_dv_pair("last_action_event", actionEvent) }));
    }

    public static object F_dvs_navigation_task_commit(object actor, object action, object events, object time, object parent)
    {
        object task = F_dv_find(action, "navigation_task", false);
        if (R.Bool(R.eq(task, false))) return actor;
        object old = F_dv_find(actor, "navigation_commitment", false);
        if (!R.Bool(R.eq(old, false)))
        {
            object oldTask = F_dv_get(old, "task");
            bool same = R.Bool(F_dv_same(F_dv_get(oldTask, "task_id"), F_dv_get(task, "task_id"))) && R.Bool(F_dv_same(F_dv_get(oldTask, "phase"), F_dv_get(task, "phase")));
            if (!same)
            {
                if (!R.Bool(R.lt(F_dv_get(oldTask, "priority"), F_dv_get(task, "priority"))) ||
                    !R.Bool(R.eq(F_dv_truth(F_dv_get(actor, "mind"), F_dv_get(oldTask, "preempt_when")), "true"))) return actor;
                _ = F_dvs_event(events, "navigation_task_released", F_dv_get(actor, "id"), time, F_dv_single(parent),
                    new List<object> { F_dv_pair("task_id", F_dv_get(oldTask, "task_id")), F_dv_pair("phase", F_dv_get(oldTask, "phase")), F_dv_pair("reason", "preempted") });
            }
            else task = F_dv_with_many(oldTask, new List<object> { F_dv_pair("origin", F_dv_get(task, "origin")), F_dv_pair("next", F_dv_get(task, "next")), F_dv_pair("base_action_id", F_dv_get(task, "base_action_id")) });
        }
        object evtId = F_dvs_event(events, "navigation_task_committed", F_dv_get(actor, "id"), time, F_dv_single(parent),
            new List<object> { F_dv_pair("task_id", F_dv_get(task, "task_id")), F_dv_pair("phase", F_dv_get(task, "phase")), F_dv_pair("candidate_id", F_dv_get(action, "navigation_candidate_id")), F_dv_pair("base_action_id", F_dv_get(action, "id")) });
        return F_dv_with(actor, "navigation_commitment", new List<object> { F_dv_pair("task", task), F_dv_pair("selection_event", evtId), F_dv_pair("candidate_id", F_dv_get(action, "navigation_candidate_id")), F_dv_pair("event_cursor", R.count(events)) });
    }

}
