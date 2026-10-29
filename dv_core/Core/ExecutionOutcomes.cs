using DuelView.Compiled;

namespace DuelView.Core;

public static partial class DVKernel
{
    private static string NativeSpatialCompletionFailure(object action, object receipt, object actorId, object actionEvent)
    {
        object Get(object record, string key) => R.Bool(R.record_like(record)) ? F_dv_find(record, key, false) : false;
        var reference = Get(action, "spatial_execution_ref");
        if (reference is false || Get(receipt, "executor") is not "godot" || Get(receipt, "outcome") is not "applied") return "";
        const string invalid = "native-spatial-completion-invalid";
        if (reference is not string { Length: > 0 }) return invalid;
        var definition = Get(R.content("data", reference), "value");
        var spec = Get(definition, "spatial_execution");
        var field = Get(receipt, "field");
        bool Same(string key, object expected) => R.Bool(F_dv_same(Get(field, key), expected));
        if (Get(definition, "kind") is not "spatial-action" || Get(definition, "status") is not "ready"
            || Get(spec, "status") is not "ready" || Get(spec, "version") is not "spatial-action-v1"
            || Get(field, "executor") is not "godot-native-spatial" || Get(field, "protocol") is not "native-spatial-completion-v1"
            || !Same("action_event", actionEvent) || !Same("actor", actorId) || !Same("action_id", Get(action, "id"))
            || !Same("spatial_execution_ref", reference) || !Same("target_id", Get(spec, "target_id"))
            || Get(field, "visual_finished") is not true || Get(field, "contact_seen") is not true
            || Get(field, "final_on_floor") is not true || Get(field, "final_contact") is not true) return invalid;
        foreach (var (measured, declared) in new[] { ("stand_error_m", "arrival_tolerance_m"), ("contact_error_m", "contact_tolerance_m"), ("facing_error_rad", "facing_tolerance_rad") })
        {
            var value = Get(field, measured);
            var limit = Get(spec, declared);
            if (!R.Bool(R.is_numeric_value(value)) || !R.Bool(R.is_numeric_value(limit))) return invalid;
            var number = R.Num(value);
            var maximum = R.Num(limit);
            if (!double.IsFinite(number) || !double.IsFinite(maximum) || number < 0 || maximum <= 0 || number > maximum) return invalid;
        }
        foreach (var key in new[] { "visual_token", "visual_generation" })
        {
            var value = Get(field, key);
            if (!R.Bool(R.is_numeric_value(value))) return invalid;
            var number = R.Num(value);
            if (!double.IsFinite(number) || number < 0 || number != Math.Floor(number)) return invalid;
        }
        return "";
    }

    private static string NativeSpatialRejectionClass(object action, object receipt)
    {
        if (F_dv_find(action, "spatial_execution_ref", false) is not string
            || F_dv_find(receipt, "executor", false) is not "godot"
            || F_dv_find(receipt, "outcome", false) is not "rejected") return "";
        var field = F_dv_find(receipt, "field", false);
        if (!R.Bool(R.record_like(field)) || F_dv_find(field, "executor", false) is not "godot-native-spatial") return "";
        return R.Text(F_dv_find(field, "reason", "")) switch
        {
            "native-spatial-action-cancelled" or "native-spatial-visual-cancelled" or "native-spatial-view-replaced"
                or "native-spatial-host-closing" or "native-spatial-scene-exited" => "native-spatial-cancelled",
            "native-spatial-approach:blocked" => "native-spatial-path-blocked",
            "native-spatial-actor-left-stand" or "native-spatial-participant-left" or "native-spatial-participant-replaced"
                or "native-spatial-world-changed" or "native-spatial-final-alignment-lost"
                or "native-spatial-contact-not-reached" or "native-spatial-final-contact-lost" => "native-spatial-condition-invalidated",
            "spatial-action-unconfigured" or "spatial-action-not-ready" or "native-spatial-port-unavailable"
                or "spatial-binding-mismatch" or "native-spatial-receipt-mismatch" or "native-spatial-visual-start-failed"
                or "native-spatial-visual-token-mismatch" or "native-spatial-visual-timeout"
                or "native-spatial-invalid-transform" => "native-spatial-execution-fault",
            _ => ""
        };
    }

    // The content names the place and participants. Completion checks their
    // current physical locations, independently of the actor's earlier belief.
    private static string CompletionOpportunityFailure(object action, object actor, object actors, object physical)
    {
        var requirement = F_dv_find(action, "opportunity_requirement", false);
        if (requirement is false) return "";
        var location = F_dv_get(requirement, "location");
        bool Present(object person)
        {
            var reference = F_dv_find(person, "location_ref", false);
            if (reference is false) return false;
            var cell = F_dv_read(physical, reference);
            return R.Bool(F_dv_uncontested(cell)) && R.Bool(R.eq(F_dv_get(cell, "value"), location));
        }
        if (!Present(actor)) return "actor-left-required-location";
        var participants = R.Items(F_dv_get(requirement, "participants"));
        var present = 0;
        var unknown = false;
        foreach (var id in participants)
        {
            var person = R.Items(actors).FirstOrDefault(candidate => R.Bool(R.eq(F_dv_get(candidate, "id"), id)));
            if (person is null) { unknown = true; continue; }
            var reference = F_dv_find(person, "location_ref", false);
            if (reference is false || !R.Bool(F_dv_uncontested(F_dv_read(physical, reference)))) unknown = true;
            else if (Present(person)) present++;
        }
        return R.Text(F_dv_get(requirement, "mode")) switch
        {
            "all-present" => present == participants.Count ? "" : "required-participant-absent",
            "at-least-one-present" => present > 0 ? "" : "required-participant-absent",
            "none-present" => present == 0 && !unknown ? "" : "required-absence-not-established",
            _ => throw new InvalidOperationException("Unknown opportunity mode")
        };
    }

    private static string ExecutionOutcomeCategory(object execution)
    {
        if (R.Text(F_dv_get(execution, "status")) == "applied") return "completed";
        var failureClass = R.Text(F_dv_find(execution, "failure_class", ""));
        var reason = R.Text(F_dv_find(execution, "reason", ""));
        if (failureClass.Length == 0 && reason.Length > 0) failureClass = reason;

        return failureClass switch
        {
            "activity-interrupted" or "interrupted" => "interrupted",
            "native-continuous-source-cancel" or "native-spatial-cancelled" or "cancelled" or "action-cancelled"
                or "user-cancelled" or "task-cancelled" => "cancelled",
            "native-spatial-completion-invalid" or "native-spatial-execution-fault" or "execution-fault"
                or "argument-execution-state-changed" or "execution-state-mismatch" => "execution-fault",
            "native-motion-blocked" or "contact-out-of-reach" or "native-spatial-path-blocked" or "path-blocked" => "path-blocked",
            "condition-invalidated" or "actor-left-required-location" or "required-participant-absent"
                or "required-absence-not-established" or "request-context-invalidated"
                or "native-spatial-condition-invalidated" or "target-left-location" => "condition-invalidated",
            "character-refused" or "actor-refused" or "request-refused" or "declined" or "peer-refused"
                or "norm-refused" or "already-accepted-personal-answer" => "character-refused",
            _ => failureClass.Contains("refus", StringComparison.OrdinalIgnoreCase) || failureClass.Contains("declin", StringComparison.OrdinalIgnoreCase)
                ? "character-refused"
                : failureClass.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                    ? "cancelled"
                    : failureClass.Contains("interrupt", StringComparison.OrdinalIgnoreCase)
                        ? "interrupted"
                        : failureClass.Contains("block", StringComparison.OrdinalIgnoreCase)
                            ? "path-blocked"
                            : failureClass.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                                ? "condition-invalidated"
                                : "unclassified-failure"
        };
    }
}
