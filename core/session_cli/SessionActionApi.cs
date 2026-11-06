// @mechanism: autonomy_drive_solver
// @layer: core/session_cli
// @contracts: SessionActionApi

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Numerics;
using DuelView.Dv;
using DuelView.Core;
using DuelView.Compiled;
using DuelView.Runtime;
using DuelView.Spatial;
using DuelView.Autonomy;
using static DuelView.Core.DVKernel;
using static DuelView.Compiled.R;


// --- Source: CampaignActionDispatch.cs ---

// Bounded domain templates. The data selects actors, roles, references,
// conditions, effects and ordered action combinations. No arbitrary calls
// or executable source forms are accepted by these assemblers.
public static partial class CampaignRuntime
{
    private sealed class DispatchContext
    {
        public object ActorId = false;
        public object Actor = false;
        public object Role = false;
        public object Room = false;
        public object Origin = false;
        public object Target = false;
        public object Gate = false;
        public object Template = false;
        public object Physical = false;
        public object Observations = false;
        public object Account = false;
        public object Inputs = false;
    }

    private static object DispatchData(string id) => F_dv_get(R.content("data", id), "value");
    private static object DispatchRecord(params (string Key, object Value)[] fields)
        => fields.Select(field => (object)new List<object> { field.Key, field.Value }).ToList();
    private static bool DispatchFalse(object value) => R.Bool(R.eq(value, false));

    private static object DispatchValue(object value, DispatchContext context)
    {
        if (!R.Bool(R.record_like(value)) || !R.Bool(F_dv_has_key(value, "binding"))) return R.copy_value(value);
        return R.Text(F_dv_get(value, "binding")) switch
        {
            "actor_id" => context.ActorId,
            "actor_name" => F_campaign_person_name(context.ActorId),
            "role" => context.Role,
            "room" => context.Room,
            "origin" => context.Origin,
            "target" => context.Target,
            "gate" => context.Gate,
            "origin_label" => F_c2_place_label(context.Origin),
            "target_label" => F_c2_place_label(context.Target),
            "template" => F_dv_get(context.Template, F_dv_get(value, "field")),
            "body" => F_campaign_body(DispatchValue(F_dv_get(value, "actor"), context)),
            "observation" => F_dv_get(R.at(context.Observations, F_dv_get(value, "index")), F_dv_get(value, "field")),
            "account" => F_dv_get(context.Account, F_dv_get(value, "field")),
            "case_parameter" => F_dv_get(F_island_case_parameters(), F_dv_get(value, "field")),
            _ => throw new InvalidOperationException("Unknown dispatch value binding")
        };
    }

    private static object DispatchReference(object definition, DispatchContext context)
    {
        object Owner() => DispatchValue(F_dv_get(definition, "owner"), context);
        object Field() => DispatchValue(F_dv_get(definition, "field"), context);
        return R.Text(F_dv_get(definition, "kind")) switch
        {
            "state" => F_campaign_ref(Owner(), Field()),
            "owned" => F_campaign_own(Owner(), Field()),
            "case" => F_island_case_ref(Field()),
            "director" => F_island_case_director(Field()),
            "island" => F_island_ref(Owner(), Field()),
            "air" => F_island_air_ref(Owner()),
            "position" => F_campaign_position_ref(Owner()),
            "access" => F_campaign_access(Owner()),
            _ => throw new InvalidOperationException("Unknown dispatch reference kind")
        };
    }

    private static object DispatchReferences(object definitions, DispatchContext context)
        => R.Items(definitions).Select(definition => DispatchReference(definition, context)).ToList();

    private static object DispatchPredicate(object definition, DispatchContext context)
    {
        var kind = R.Text(F_dv_get(definition, "kind"));
        switch (kind)
        {
            case "true": return F_dv_true();
            case "physical": return context.Physical;
            case "equals": return F_dv_fact(DispatchReference(F_dv_get(definition, "ref"), context), DispatchValue(F_dv_get(definition, "value"), context));
            case "at_least": return F_dv_at_least(DispatchReference(F_dv_get(definition, "ref"), context), DispatchValue(F_dv_get(definition, "value"), context));
            case "at": return F_campaign_at(DispatchValue(F_dv_get(definition, "actor"), context), DispatchValue(F_dv_get(definition, "room"), context));
            case "can_act": return F_c2_can_act(DispatchValue(F_dv_get(definition, "actor"), context));
            case "alive": return F_c2_alive(DispatchValue(F_dv_get(definition, "actor"), context));
            case "window": return F_campaign_window(F_dv_get(definition, "day"), F_dv_get(definition, "start"), F_dv_get(definition, "end"));
            case "work_accepted": return F_c2_work_accepted(DispatchValue(F_dv_get(definition, "actor"), context), F_dv_get(definition, "id"));
            case "all":
                var conditions = R.Items(F_dv_get(definition, "items")).Select(item => DispatchPredicate(item, context)).ToList();
                if (R.Bool(F_dv_find(definition, "include_input_facts", false)))
                    foreach (var input in R.Items(context.Inputs))
                        conditions.Add(F_dv_fact(F_dv_get(input, "ref"), F_dv_get(input, "value")));
                return F_dv_all(conditions);
            case "mechanism_premise":
                // Closed whitelist of existing domain mechanisms, never a
                // reflected method name or a callback supplied by content.
                var law = R.Text(F_dv_get(definition, "mechanism")) switch
                {
                    "structural_inspection" => F_c2_check_structure_law(context.ActorId),
                    "latch_repair" => F_c2_repair_latch_law(context.ActorId),
                    "food_delivery" => F_c2_deliver_patient_law(context.ActorId),
                    "feeding" => F_c2_feed_patient_law(context.ActorId),
                    _ => throw new InvalidOperationException("Unknown dispatch premise mechanism")
                };
                return F_dv_get(law, "premise");
            default: throw new InvalidOperationException("Unknown dispatch predicate kind");
        }
    }

    private static object DispatchEffects(object definitions, DispatchContext context)
    {
        var effects = new List<object>();
        foreach (var definition in R.Items(definitions))
        {
            var reference = DispatchReference(F_dv_get(definition, "ref"), context);
            var value = DispatchValue(F_dv_get(definition, "value"), context);
            effects.Add(R.Text(F_dv_get(definition, "op")) switch
            {
                "assign" => F_dv_assign(reference, value),
                "increment" => F_dv_increment(reference, value),
                _ => throw new InvalidOperationException("Unknown dispatch effect operation")
            });
        }
        return effects;
    }

    private static object DispatchSource(object definition)
        => R.Text(F_dv_get(definition, "kind")) switch
        {
            "approved" => F_campaign_approved(F_dv_get(definition, "id")),
            "chapter" => F_c2_source(F_dv_get(definition, "path"), F_dv_get(definition, "line"), F_dv_get(definition, "id")),
            _ => throw new InvalidOperationException("Unknown dispatch source kind")
        };

    private static object DispatchUpdates(object value, object definitions, DispatchContext context)
    {
        foreach (var definition in R.Items(definitions))
            value = F_dv_with(value, F_dv_get(definition, "field"), DispatchValue(F_dv_get(definition, "value"), context));
        return value;
    }

    private static object DispatchSpecialAction(object definition, DispatchContext context)
    {
        var source = DispatchSource(F_dv_get(definition, "source"));
        var factory = R.Text(F_dv_get(definition, "factory"));
        object action;
        if (factory == "inspect")
        {
            // The inspection constructor runs before the explicit presence
            // overrides, as in the original action assembly.
            action = F_c2_inspect_action(context.ActorId, DispatchValue(F_dv_get(definition, "object"), context),
                F_dv_get(definition, "room"), R.copy_value(F_dv_get(definition, "fields")),
                F_dv_get(definition, "label"), F_dv_get(definition, "text"), source);
            context.Physical = DispatchPredicate(F_dv_get(definition, "physical"), context);
            var known = DispatchPredicate(F_dv_get(definition, "known"), context);
            action = F_dv_with_many(action, DispatchRecord(("precondition", known), ("authority_precondition", context.Physical)));
        }
        else if (factory == "surface")
        {
            context.Physical = DispatchPredicate(F_dv_get(definition, "physical"), context);
            var known = DispatchPredicate(F_dv_get(definition, "known"), context);
            var identity = F_dv_get(definition, "id");
            var id = R.Bool(F_dv_find(identity, "actor_prefix", false))
                ? R.cat(context.ActorId, F_dv_get(identity, "suffix")) : F_dv_get(identity, "literal");
            action = F_campaign_surface_action(id, F_dv_get(definition, "kind"), known, context.Physical, id,
                DispatchReferences(F_dv_get(definition, "grants"), context), F_dv_get(definition, "label"), F_dv_get(definition, "text"), source);
        }
        else throw new InvalidOperationException("Unknown special action factory");
        if (R.Bool(F_dv_has_key(definition, "speech")))
        {
            var declaration = F_dv_get(definition, "speech");
            var speech = F_dv_with_many(F_campaign_speech(F_dv_true(), source, F_dv_get(declaration, "scope")),
                DispatchRecord(("surface", F_dv_get(declaration, "surface")), ("request", R.copy_value(F_dv_get(declaration, "request")))));
            action = F_dv_with_many(action, DispatchRecord(("speech", speech), ("listeners", R.copy_value(F_dv_get(declaration, "listeners")))));
        }
        return DispatchUpdates(action, F_dv_find(definition, "updates", new List<object>()), context);
    }

    public static object F_c2_special_actions(object actor)
    {
        var result = new List<object>();
        var context = new DispatchContext { ActorId = actor };
        foreach (var group in R.Items(DispatchData("campaign.dispatch.special_actions")))
            if (R.Bool(F_dv_has(F_dv_get(group, "actors"), actor)))
                foreach (var definition in R.Items(F_dv_get(group, "actions")))
                    result.Add(DispatchSpecialAction(definition, context));
        return result;
    }

    private static DispatchContext DispatchMovementContext(object definition, object actor, object origin, object target, object gate)
        => new() { Template = definition, ActorId = actor, Origin = origin, Target = target, Gate = gate };











    private static object DispatchRouteAction(object actor, object origin, object target, object gate, object data)
    {
        // Preserve construction of the default movement action before any
        // owned-knowledge variant is considered.
        object action = F_c2_move_action(F_dv_get(actor, "id"), origin, target, gate);
        foreach (var variant in R.Items(F_dv_get(data, "variants")))
        {
            if (!R.Bool(F_dv_has(F_dv_get(variant, "actors"), F_dv_get(actor, "id")))) continue;
            var definition = F_dv_get(F_dv_get(data, "templates"), F_dv_get(variant, "template"));
            var context = DispatchMovementContext(definition, F_dv_get(actor, "id"), origin, target, gate);
            if (!R.Bool(R.eq(F_dv_truth(F_dv_get(actor, "mind"), DispatchPredicate(F_dv_get(variant, "when"), context)), "true"))) continue;
            action = R.Text(F_dv_get(variant, "factory")) switch
            {
                "carried_move" => DispatchMovementAction(definition, context),
                _ => throw new InvalidOperationException("Unknown route movement factory")
            };
        }
        return action;
    }

    public static object F_c2_navigation_route_uncached(object actor, object target)
    {
        var direct = F_c2i_known_route(actor, target);
        if (!DispatchFalse(direct)) return direct;
        var position = F_dv_read(F_dv_get(actor, "mind"), F_campaign_position_ref(F_dv_get(actor, "id")));
        if (!R.Bool(F_dv_uncontested(position)) || R.Bool(R.eq(F_dv_get(position, "value"), target))) return false;
        var positionValue = F_dv_get(position, "value");
        var queue = new List<object> { DispatchRecord(("room", positionValue), ("first", ""), ("prefix_end", positionValue), ("blocked", false), ("basis", new List<object>())) };
        var visited = new List<object> { positionValue };
        var edges = F_c2_edges();
        var data = DispatchData("campaign.dispatch.navigation");
        for (var cursor = 0; cursor < queue.Count; cursor++)
        {
            var node = queue[cursor];
            foreach (var edge in R.Items(edges))
            {
                object destination = "";
                if (R.Bool(R.eq(F_dv_get(edge, "from"), F_dv_get(node, "room")))) destination = F_dv_get(edge, "to");
                if (R.Bool(R.eq(F_dv_get(edge, "to"), F_dv_get(node, "room")))) destination = F_dv_get(edge, "from");
                if (R.Bool(R.eq(destination, "")) || R.Bool(F_dv_has(visited, destination))) continue;
                var action = DispatchRouteAction(actor, F_dv_get(node, "room"), destination, F_dv_get(edge, "gate"), data);
                var adoption = F_dv_find(F_dv_get(F_dv_get(actor, "mind"), "adoptions"), F_dv_get(action, "predict_law"), false);
                if (DispatchFalse(adoption) || !R.Bool(F_dv_get(adoption, "active")) || !R.Bool(R.eq(F_dv_get(adoption, "mode"), "predict"))) continue;
                var next = F_dv_with(node, "room", destination);
                if (!R.Bool(F_dv_get(node, "blocked")))
                {
                    if (R.Bool(R.eq(F_dv_truth(F_dv_get(actor, "mind"), F_dv_fact(F_campaign_access(F_dv_get(edge, "gate")), true)), "true")))
                    {
                        var first = F_dv_get(node, "first");
                        if (R.Bool(R.eq(first, ""))) first = destination;
                        next = F_dv_with_many(next, DispatchRecord(("first", first), ("prefix_end", destination),
                            ("basis", F_dv_append(F_dv_get(node, "basis"), DispatchRecord(("method", F_dv_get(action, "id")),
                                ("adoption_basis", F_dv_get(adoption, "basis")), ("gate", F_campaign_access(F_dv_get(edge, "gate"))),
                                ("gate_cell", F_dv_read(F_dv_get(actor, "mind"), F_campaign_access(F_dv_get(edge, "gate")))))))));
                    }
                    else next = F_dv_with(next, "blocked", true);
                }
                if (R.Bool(R.eq(destination, target)) && !R.Bool(R.eq(F_dv_get(next, "first"), "")))
                    return DispatchRecord(("origin", positionValue), ("next", F_dv_get(next, "first")), ("target", F_dv_get(next, "prefix_end")),
                        ("requested_target", target), ("path_basis", F_dv_get(next, "basis")), ("scope", F_dv_get(data, "route_scope")));
                queue.Add(next);
                visited.Add(destination);
            }
        }
        return false;
    }

    private static bool DispatchComparisons(object definitions, DispatchContext context)
    {
        foreach (var definition in R.Items(definitions))
        {
            var left = DispatchValue(F_dv_get(definition, "left"), context);
            var right = DispatchValue(F_dv_get(definition, "right"), context);
            var matches = R.Text(F_dv_get(definition, "op")) switch
            {
                "equals" => R.Bool(R.eq(left, right)),
                "not_equals" => !R.Bool(R.eq(left, right)),
                "at_most" => !R.Bool(R.lt(right, left)),
                "at_least" => !R.Bool(R.lt(left, right)),
                _ => throw new InvalidOperationException("Unknown observation comparison")
            };
            if (!matches) return false;
        }
        return true;
    }

    public static object F_island_case_job(object actor, object role, object suffix, object room, object known, object effects, object grants, object label, object text)
    {
        var defaults = F_dv_get(DispatchData("campaign.dispatch.case_jobs"), "defaults");
        var context = new DispatchContext { Actor = actor, ActorId = F_dv_get(actor, "id"), Role = role, Room = room };
        var id = R.cat(context.ActorId, R.cat(F_dv_get(defaults, "id_infix"), suffix));
        var authority = DispatchPredicate(F_dv_get(defaults, "authority"), context);
        var law = F_dv_axiom(id, F_dv_get(defaults, "version"), F_dv_all(new List<object> { authority, known }), effects);
        var premise = F_dv_all(new List<object> { F_campaign_at(context.ActorId, room), known });
        var action = F_campaign_surface_action(id, F_dv_get(defaults, "kind"), premise, F_dv_get(law, "premise"), id, grants, label, text, F_island_source());
        action = DispatchUpdates(action, F_dv_get(defaults, "updates"), context);
        var interaction = F_dv_get(defaults, "interaction");
        foreach (var variant in R.Items(F_dv_get(defaults, "interaction_variants")))
            if (R.Bool(F_dv_has(F_dv_get(variant, "roles"), role))) interaction = F_dv_get(variant, "value");
        action = F_dv_with(action, "interaction", R.copy_value(interaction));
        return DispatchRecord(("law", law), ("action", action));
    }

    public static object F_island_case_jobs(object actor, object role)
    {
        actor = F_c2_query_view(actor, actor);
        var jobs = new List<object>();
        var definitions = F_dv_get(DispatchData("campaign.dispatch.case_jobs"), "rows");
        foreach (var definition in R.Items(definitions))
        {
            if (!R.Bool(F_dv_has(F_dv_get(definition, "roles"), role))) continue;
            var context = new DispatchContext { Actor = actor, ActorId = F_dv_get(actor, "id"), Role = role };
            context.Observations = F_island_case_numeric(F_dv_get(actor, "mind"), DispatchReferences(F_dv_get(definition, "observations"), context));
            var evidenceFactory = F_dv_find(definition, "evidence_factory", "");
            // The account query originally runs even if the first numeric
            // observation query returned false; preserve that evaluation gate.
            if (!R.Bool(R.eq(evidenceFactory, "")))
                context.Account = R.Text(evidenceFactory) switch
                {
                    "inventory_balance" => F_island_case_account_evidence(actor),
                    _ => throw new InvalidOperationException("Unknown case evidence factory")
                };
            if (DispatchFalse(context.Observations)) continue;
            if (!R.Bool(R.eq(evidenceFactory, "")) && DispatchFalse(context.Account)) continue;
            if (!DispatchComparisons(F_dv_find(definition, "comparisons", new List<object>()), context)) continue;
            context.Inputs = R.Bool(R.eq(evidenceFactory, "")) ? context.Observations
                : F_c2m_concat(context.Observations, F_dv_get(context.Account, "values"));
            var known = DispatchPredicate(F_dv_get(definition, "known"), context);
            var effects = DispatchEffects(F_dv_get(definition, "effects"), context);
            var job = F_island_case_job(actor, role, F_dv_get(definition, "suffix"), F_dv_get(definition, "room"), known, effects,
                DispatchReferences(F_dv_get(definition, "grants"), context), F_dv_get(definition, "label"), F_dv_get(definition, "text"));
            if (R.Bool(F_dv_has_key(definition, "speech")))
            {
                var declaration = F_dv_get(definition, "speech");
                var action = F_dv_get(job, "action");
                var speech = F_dv_with(F_campaign_speech(F_dv_true(), F_island_source(), F_dv_get(declaration, "scope")), "surface", F_dv_get(action, "surface"));
                var fields = DispatchRecord(("kind", F_dv_get(declaration, "kind")), ("speech", speech));
                if (R.Bool(F_dv_find(declaration, "public", false)))
                    fields = F_dv_with_many(fields, DispatchRecord(("listeners", F_campaign_people()),
                        ("public_observers", F_campaign_people()), ("public_grants", F_dv_get(action, "grants"))));
                job = F_dv_with(job, "action", F_dv_with_many(action, fields));
            }
            jobs.Add(F_island_case_with_evidence(job, context.Inputs));
        }
        return jobs;
    }
}
// --- Source: SessionSpatialActions.cs ---

public sealed partial class DvSession
{
    /// <summary>Project an explicitly declared interaction for the current prepared action.
    /// Call on the session owner thread, alongside Prepare. This does not reserve or commit state.</summary>
    public IReadOnlyDictionary<string, object?> PreparedSpatialExecutionValue()
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        var pending = Field(state, "pending", false);
        if (pending is false) return HostValues.Empty;
        var action = Field(pending, "action", false);
        var reference = Field(action, "spatial_execution_ref", false);
        if (reference is false) return HostValues.Empty;
        if (reference is not string { Length: > 0 } definitionId)
            throw new InvalidDataException("Prepared spatial action needs a nonempty spatial_execution_ref.");
        var actionEvent = R.Text(Field(pending, "action_event", ""));
        var actor = R.Text(Field(pending, "id", ""));
        var actionId = R.Text(Field(action, "id", ""));
        var definition = Field(content.Get("data", definitionId), "value", false);
        if (definition is false) throw new InvalidDataException($"Missing spatial action definition: {definitionId}");
        if (Field(definition, "kind", false) is not "spatial-action")
            throw new InvalidDataException($"Not a spatial action definition: {definitionId}");
        var result = new Dictionary<string, object?>
        {
            ["action_event"] = actionEvent, ["actor"] = actor, ["action_id"] = actionId,
            ["spatial_execution_ref"] = definitionId, ["state_version"] = Version,
            ["content_hash"] = content.Hash
        };
        var spec = Field(definition, "spatial_execution", false);
        if (Field(definition, "status", false) is not "ready" || spec is false || Field(spec, "status", false) is not "ready")
        {
            result["status"] = "blocked";
            result["reason"] = "spatial-action-unconfigured";
            return HostValues.Freeze(result);
        }
        string Text(object value, string key)
        {
            if (Field(value, key, false) is not string { Length: > 0 } text)
                throw new InvalidDataException($"Spatial action {definitionId}/{key} needs a nonempty identifier.");
            return text;
        }
        double Positive(object value, string key)
        {
            var number = Field(value, key, false);
            if (!R.Bool(R.is_numeric_value(number)) || !double.IsFinite(R.Num(number)) || R.Num(number) <= 0)
                throw new InvalidDataException($"Spatial action {definitionId}/{key} needs a finite positive number.");
            return R.Num(number);
        }
        if (Text(spec, "version") != "spatial-action-v1")
            throw new InvalidDataException($"Unsupported spatial action version: {definitionId}");
        var target = Text(spec, "target_id");
        if (!R.Bool(R.eq(Field(action, "law", false), Field(definition, "method", false))))
            throw new InvalidDataException($"Spatial action method mismatch: {definitionId}");
        var ruleTemplate = Field(content.Get("data", Text(definition, "rule_template")), "value", false);
        var parameters = R.Items(Field(ruleTemplate, "parameters", false));
        var arguments = R.Items(Field(definition, "arguments", false));
        if (parameters.Count != arguments.Count)
            throw new InvalidDataException($"Spatial action argument count mismatch: {definitionId}");
        var actorBound = false;
        var targetBound = false;
        for (var index = 0; index < parameters.Count; index++)
        {
            if (parameters[index] is "actor")
            {
                actorBound = true;
                if (arguments[index] is not string person || person != actor)
                    throw new InvalidDataException($"Spatial action actor mismatch: {definitionId}");
            }
            if (parameters[index] is "seat" or "object")
            {
                targetBound = true;
                if (arguments[index] is not string objectId || objectId != target)
                    throw new InvalidDataException($"Spatial action target mismatch: {definitionId}");
            }
            if (parameters[index] is string angleParameter && angleParameter is "from_angle" or "target_angle")
            {
                var boundMotion = Field(spec, "object_motion", false);
                var expected = Field(boundMotion, angleParameter == "from_angle" ? "from_degrees" : "target_degrees", false);
                if (!R.Bool(R.is_numeric_value(arguments[index])) || !R.Bool(R.is_numeric_value(expected))
                    || !R.Bool(R.eq(arguments[index], expected)))
                    throw new InvalidDataException($"Spatial action law/motion angle mismatch: {definitionId}/{angleParameter}");
            }
        }
        if (!actorBound || !targetBound)
            throw new InvalidDataException($"Spatial action lacks explicit actor and target parameters: {definitionId}");
        // Recheck the original declaration; the published object projection is not player knowledge.
        var pack = Field(content.Get("data", "campaign.pack"), "value", false);
        var selected = Text(pack, "spatial_definition");
        var spatial = Field(content.Get("data", selected), "value", false);
        object? found = null;
        foreach (var floor in R.Items(Field(spatial, "floors", false)))
            foreach (var item in SpatialPublication.InteractionTargets(floor))
                if (Field(item, "id", false) is string id && id == target) found = item;
        if (found is null) throw new InvalidDataException($"Spatial target not published: {target}");
        var interaction = Field(found, "interaction", false);
        if (!R.Bool(R.record_like(interaction)) || Field(interaction, "status", false) is not "ready")
        {
            result["status"] = "blocked";
            result["reason"] = "spatial-target-unconfigured";
            return HostValues.Freeze(result);
        }
        var publication = ProjectSpatialObjects(new[] { target });
        if (!R.Items(Field(interaction, "actions", false)).Any(id => id is string text && text == definitionId))
            throw new InvalidDataException($"Spatial action is not declared by target {target}: {definitionId}");
        var resources = R.Items(Field(spec, "resource_ids", false));
        var declaredResources = R.Items(Field(interaction, "resources", false));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in resources)
            if (resource is not string { Length: > 0 } resourceId || !seen.Add(resourceId)
                || !declaredResources.Any(value => value is string allowed && allowed == resourceId))
                throw new InvalidDataException($"Spatial action resource is undeclared or duplicated: {definitionId}");
        var visual = Field(spec, "visual", false);
        // Godot's unnamed animation library is represented by an explicit empty
        // string. Missing is still invalid; hosts must not guess a library.
        if (Field(visual, "library", false) is not string animationLibrary)
            throw new InvalidDataException($"Spatial action {definitionId}/visual/library needs an explicit string.");
        if (Field(visual, "anchor_root", false) is not bool anchorRoot)
            throw new InvalidDataException($"Spatial action {definitionId}/visual/anchor_root needs an explicit boolean.");
        var checkedVisual = new Dictionary<string, object?>
        {
            ["library"] = animationLibrary, ["clip"] = Text(visual, "clip"),
            ["anchor_root"] = anchorRoot, ["speed"] = Positive(visual, "speed")
        };
        result["status"] = "ready";
        result["version"] = "spatial-action-v1";
        result["target_id"] = target;
        foreach (var key in new[] { "stand_slot", "face_slot", "contact_slot", "actor_contact_slot", "actor_facing_slot" })
            result[key] = Text(spec, key);
        var contactPhase = Text(spec, "contact_phase");
        if (contactPhase is not ("before-and-finish" or "during-visual" or "before-motion"))
            throw new InvalidDataException($"Unknown spatial contact phase: {definitionId}");
        var motion = Field(spec, "object_motion", false);
        if (motion is not false)
        {
            if (contactPhase != "before-motion" || Text(motion, "kind") != "hinged-door"
                || Text(motion, "angle_convention") != "design-clockwise-degrees")
                throw new InvalidDataException($"Unsupported object motion contract: {definitionId}");
            double Angle(string key)
            {
                var value = Field(motion, key, false);
                if (!R.Bool(R.is_numeric_value(value)) || !double.IsFinite(R.Num(value)))
                    throw new InvalidDataException($"Invalid object motion angle: {definitionId}/{key}");
                var angle = R.Num(value);
                var minimum = Field(found, "angle_min", false);
                var maximum = Field(found, "angle_max", false);
                if (!R.Bool(R.is_numeric_value(minimum)) || !R.Bool(R.is_numeric_value(maximum))
                    || angle < R.Num(minimum) || angle > R.Num(maximum))
                    throw new InvalidDataException($"Object motion outside declared door range: {definitionId}/{key}");
                return angle;
            }
            result["object_motion"] = new Dictionary<string, object?>
            {
                ["kind"] = "hinged-door", ["angle_convention"] = "design-clockwise-degrees",
                ["from_degrees"] = Angle("from_degrees"), ["target_degrees"] = Angle("target_degrees"),
                ["speed_degrees_s"] = Positive(motion, "speed_degrees_s"),
                ["timeout_s"] = Positive(motion, "timeout_s"),
                ["tolerance_degrees"] = Positive(motion, "tolerance_degrees")
            };
        }
        else if (contactPhase == "before-motion")
            throw new InvalidDataException($"Before-motion contact needs object motion: {definitionId}");
        var posture = Field(spec, "actor_posture", false);
        if (posture is not false)
        {
            var from = Text(posture, "from"); var to = Text(posture, "to");
            if (!((from == "standing" && to == "sitting") || (from == "sitting" && to == "standing")) || motion is not false)
                throw new InvalidDataException($"Unsupported actor posture transition: {definitionId}");
            var checkedPosture = new Dictionary<string, object?> { ["from"] = from, ["to"] = to };
            foreach (var key in new[] { "seat_slot", "exit_slot", "seat_surface_slot", "left_foot_slot", "right_foot_slot" })
            {
                var slot = Text(posture, key);
                var coordinates = R.Items(Field(interaction, slot, new List<object>()));
                if (coordinates.Count != 3 || coordinates.Any(value => !R.Bool(R.is_numeric_value(value)) || !double.IsFinite(R.Num(value))))
                    throw new InvalidDataException($"Actor posture slot must have three published finite coordinates: {definitionId}/{slot}");
                checkedPosture[key] = slot;
            }
            foreach (var key in new[] { "position_tolerance_m", "foot_tolerance_m", "timeout_s" })
                checkedPosture[key] = Positive(posture, key);
            result["actor_posture"] = checkedPosture;
        }
        result["contact_phase"] = contactPhase;
        foreach (var key in new[] { "arrival_tolerance_m", "contact_tolerance_m", "facing_tolerance_rad", "approach_timeout_s", "turn_timeout_s", "visual_timeout_s" })
            result[key] = Positive(spec, key);
        result["resource_ids"] = resources.Cast<object?>().ToArray();
        result["visual"] = checkedVisual;
        result["spatial_definition"] = publication["spatial_definition"];
        return HostValues.Freeze(result);
    }
}
// --- Source: SessionLocomotion.cs ---

public sealed partial class DvSession
{
    public long LocomotionSequence => (long)R.Num(Field(Field(state, "story", false), "locomotion_sequence", 0d));

    public IReadOnlyDictionary<string, object?> PlayerControlProjection()
    {
        using var scope = R.UseValues(valueContext);
        return converter.Record(Run(() =>
        {
            var player = R.Text(Field(state, "player_actor", ""));
            var actors = R.Items(Field(state, "actors", false));
            var index = actors.ToList().FindIndex(a => R.Text(Field(a, "id", "")) == player);
            if (index < 0) return new List<object>();
            var pack = Field(content.Get("data", "campaign.pack"), "value", false);
            var conditionId = R.Text(Field(Field(pack, "player_locomotion_conditions", new List<object>()), player, ""));
            var canWalk = conditionId.Length == 0 || R.Text(F_dv_truth(Field(state, "physical", false), DvValueTemplate.Instantiate(conditionId))) == "true";
            var pending = Field(state, "pending", false);
            var occupied = IsActorInFlight(player) || (pending is not false && R.Text(Field(pending, "id", "")) == player);
            object options = new List<object>();
            if (!occupied)
            {
                var inputView = R.record_with(R.record_with(R.record_with(state, "pending", false), "cursor", (double)index), "awaiting_player", true);
                options = Field(CampaignRuntime.F_dvg_intent(inputView), "options", new List<object>());
            }
            return new List<object> { F_dv_pair("locomotion_enabled", canWalk && !occupied), F_dv_pair("player_options", options) };
        }));
    }

    // Walking is a physical observation, not a scheduled interaction. The host
    // supplies an ordered crossing after move_and_slide; Core applies the DV
    // movement law and its observation grants without taking an action slot.
    public void CommitLocomotion(IReadOnlyDictionary<string, object?> measurement)
    {
        using var valuesScope = R.UseValues(valueContext);
        var receipt = converter.ToDv(measurement);
        var next = Run(() => CommitLocomotionState(receipt));
        if (ReferenceEquals(next, state)) return;
        BeginTransition();
        state = next;
        RememberCommitted();
    }

    private object CommitLocomotionState(object receipt)
    {
        var actorId = R.Text(Field(receipt, "actor", ""));
        F_dv_require(actorId.Length > 0 && actorId == R.Text(Field(state, "player_actor", "")), "Walking receipt must belong to the controlled actor.");
        F_dv_require(Field(receipt, "basis", "") is "godot-3d-physics-displacement", "Walking needs actual native movement.");
        F_dv_require(Field(receipt, "content_hash", "") is string hash && hash == content.Hash, "Walking content changed.");
        var sequence = R.Num(Field(receipt, "sequence", 0d));
        var story = Field(state, "story", new List<object>());
        var previousSequence = R.Num(Field(story, "locomotion_sequence", 0d));
        if (sequence == previousSequence) return state;
        F_dv_require(sequence == previousSequence + 1 && double.IsFinite(sequence), "Walking receipts must stay ordered.");
        var positions = new[] { "from_position", "to_position" }.Select(key => R.Items(Field(receipt, key, false))).ToArray();
        F_dv_require(positions.All(p => p.Count == 3 && p.All(v => R.Bool(R.is_numeric_value(v)) && double.IsFinite(R.Num(v)))), "Walking positions must be finite.");
        var distance = Math.Sqrt(Enumerable.Range(0, 3).Sum(i => Math.Pow(R.Num(positions[0][i]) - R.Num(positions[1][i]), 2)));
        F_dv_require(distance > 0 && distance < 1, "Walking receipt must describe a local physics step.");
        var actors = Field(state, "actors", false);
        var index = R.Items(actors).ToList().FindIndex(a => R.Text(Field(a, "id", "")) == actorId);
        F_dv_require(index >= 0 && !IsActorInFlight(actorId), "Walking actor is occupied.");
        var pending = Field(state, "pending", false);
        F_dv_require(pending is false || R.Text(Field(pending, "id", "")) != actorId, "An interaction owns the walking actor.");
        var actor = R.at(actors, (double)index);
        var physical = Field(state, "physical", false);
        var location = F_dv_read(physical, Field(actor, "location_ref", false));
        F_dv_require(F_dv_uncontested(location), "Walking origin is unknown.");
        var origin = Field(location, "value", false);
        var fromRoom = R.Text(Field(receipt, "from_room", ""));
        var toRoom = R.Text(Field(receipt, "to_room", ""));
        var map = R.Text(Field(receipt, "map", ""));
        var fromPlace = CampaignRuntime.F_dvg_place(origin);
        F_dv_require(fromRoom != toRoom && R.Text(Field(fromPlace, "map", "")) == map && R.Text(Field(fromPlace, "room", "")) == fromRoom, "Walking origin differs from the committed location.");
        object action = false;
        foreach (var candidate in R.Items(Field(actor, "actions", false)))
        {
            var travel = Field(candidate, "travel", new List<object>());
            if (Field(candidate, "kind", "") is not "move" || Field(travel, "station", false) is not false
                || !R.Bool(R.eq(Field(travel, "from", false), origin))) continue;
            var destination = CampaignRuntime.F_dvg_place(Field(travel, "to", ""));
            if (R.Text(Field(destination, "map", "")) != map || R.Text(Field(destination, "room", "")) != toRoom) continue;
            if (R.Text(F_dv_truth(physical, Field(candidate, "authority_precondition", Field(candidate, "precondition", false)))) != "true") continue;
            action = candidate;
            break;
        }
        F_dv_require(action is not false, "The crossing has no enabled DV movement law.");
        var time = Field(physical, "time", 0d);
        var events = F_dv_clone(Field(state, "events", false));
        var eventId = F_dvs_event(events, "physical_crossing", actorId, time, new List<object>(), receipt);
        var execution = F_dv_execute(physical, Field(action, "law", ""), eventId, time, Field(physical, "revision", 0d));
        F_dv_require(Field(execution, "status", "") is "applied", "The movement law did not apply.");
        var after = Field(execution, "world", false);
        var bindings = Field(state, "bindings", false);
        var mind = Field(actor, "mind", false);
        var grants = Field(action, "grants", new List<object>());
        var observations = F_dv_perceive(after, mind, bindings, grants, eventId, time);
        var observed = Field(F_dv_update_mind(mind, observations, Field(mind, "revision", 0d), F_dvs_observation_profile(actor)), "world", false);
        actor = R.record_with(actor, "mind", observed);
        var observationEvent = F_dvs_event(events, "observation", actorId, time, F_dv_single(eventId),
            new List<object> { F_dv_pair("observations", observations), F_dv_pair("grants", grants), F_dv_pair("execution_id", eventId) });
        actor = R.record_with(actor, "last_cognitive_event", observationEvent);
        actors = F_dvs_replace(actors, (double)index, actor);
        actors = F_dvs_notice_movement_witnesses(actors, (double)index, physical, after, bindings, events, time, eventId);
        actor = F_dvs_notice_peers(actor, after, actors, bindings, events, time, eventId);
        actor = F_dvs_notice_visible_objects(actor, after, bindings, events, time, eventId);
        actors = F_dvs_replace(actors, (double)index, actor);
        story = R.record_with(story, "locomotion_sequence", sequence);
        var result = R.record_with(R.record_with(R.record_with(R.record_with(state, "physical", after), "actors", actors), "events", events), "story", story);
        if (pending is not false)
        {
            // Preserve the binding version handed to the native executor.
            pending = R.record_with(pending, "execution_state_version", Field(pending, "execution_state_version", (double)Version));
            result = R.record_with(result, "pending", pending);
        }
        // Chapter progression remains authored and runs at ordinary boundaries.
        return result;
    }
}
// --- Source: SessionSim4.cs ---

public sealed partial class DvSession
{
    private sealed record SavedSim4Plan(string Id, long StateVersion, Sim4Planner.Plan Plan);
    private SavedSim4Plan? savedSim4Plan;

    private (Sim4Planner.Input Input, IReadOnlyList<object> Excluded) Sim4Input()
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed || HasPending(state) || HostValues.Text(Intent, "status") != "awaiting_input")
            throw new InvalidOperationException("Sim4 planning requires the current player's committed input boundary.");
        EnsureObservers();
        var viewer = Viewer;
        var actor = observerSubjects[viewer];
        var profile = Field(Field(actor, "policy", new List<object>()), "sims_autonomy", false);
        if (profile is false) throw new InvalidOperationException("Current actor has no DV autonomy profile.");
        static Dictionary<string, double> Numbers(object record) => R.Items(record).ToDictionary(
            pair => R.Text(R.at(pair, 0d)), pair => R.Num(R.at(pair, 1d)), StringComparer.Ordinal);
        var needs = Numbers(DVKernel.F_dvs_autonomy_needs(actor));
        var weights = Numbers(Field(profile, "autonomy_weights", new List<object>()));
        var decay = Numbers(Field(profile, "need_decay_per_second", new List<object>()));
        if (needs.Count == 0) throw new InvalidOperationException("Current actor has no declared commodity needs.");
        var actions = R.Items(Field(actor, "actions", new List<object>())).ToDictionary(x => R.Text(Field(x, "id", "")), StringComparer.Ordinal);
        var predictions = Field(profile, "action_predictions", new List<object>());
        var options = HostValues.List(Intent, "options").OfType<IReadOnlyDictionary<string, object?>>().ToArray();
        var candidates = new List<Sim4Planner.Candidate>(); var excluded = new List<object>();
        foreach (var option in options)
        {
            var id = HostValues.Text(option, "id");
            if (!actions.TryGetValue(id, out var action)) { excluded.Add(new { action = id, reason = "no-owned-action" }); continue; }
            var effects = Field(action, "perceived_need_effects", false);
            if (effects is false) effects = Field(predictions, R.Text(Field(action, "kind", "")), false);
            if (effects is false) { excluded.Add(new { action = id, reason = "no-declared-need-prediction" }); continue; }
            var declaredSeconds = Field(action, "predicted_seconds", false);
            var seconds = declaredSeconds is not false ? R.Num(declaredSeconds)
                : R.Num(Field(converter.ToDv(HostValues.Map(option, "cost")), "time", 0d)) * 60;
            if (!double.IsFinite(seconds) || seconds <= 0) { excluded.Add(new { action = id, reason = "no-positive-declared-duration" }); continue; }
            // Current options have already passed the formal actor-owned admission rules.
            // No predicted observations or objective effects are copied from an action here.
            // Later queue entries remain conditional and must be admitted again after completion.
            candidates.Add(new(id, seconds, Numbers(effects), [], [], [], []));
        }
        var revision = R.show(Field(Field(actor, "mind", new List<object>()), "revision", 0d));
        var input = new Sim4Planner.Input(viewer, Version + ":" + content.Hash + ":" + revision,
            needs, weights, decay, R.Num(Field(profile, "time_cost", 0d)), [], candidates);
        return (input, excluded);
    }

    public object PlanSim4(int depth = 1, int beamWidth = 32, int expansionBudget = 4096)
    {
        var (input, excluded) = Sim4Input();
        var plan = Sim4Planner.Build(input, new(depth, beamWidth, expansionBudget));
        var saved = new SavedSim4Plan(Guid.NewGuid().ToString("N"), Version, plan);
        savedSim4Plan = saved;
        return new
        {
            policy = "sim4", plan_id = saved.Id, state_version = Version, content_hash = content.Hash,
            actor = input.Actor, candidate_count = input.Candidates.Count, excluded, plan,
            verified = Sim4Planner.Verify(input, plan),
            scope = "current-owned-options-with-declared-need-predictions",
            future_steps = "conditional-replan-after-each-real-completion",
            need_source = "current-actor-DV-autonomy-profile", execution = "first-action-through-session-prepare"
        };
    }

    public object ExecuteSim4(string planId, long stateVersion)
    {
        if (savedSim4Plan is not { } saved || saved.Id != planId) throw new InvalidOperationException("Unknown or consumed Sim4 plan. Plan from the latest observation first.");
        if (stateVersion != Version || saved.StateVersion != Version) throw new InvalidOperationException("Stale Sim4 plan. Replan after the current action or state change.");
        var (input, _) = Sim4Input();
        var verified = Sim4Planner.Verify(input, saved.Plan);
        if (!verified.Valid) throw new InvalidOperationException("Sim4 plan no longer matches the actor's input: " + string.Join("; ", verified.Errors));
        if (saved.Plan.Queue.Count == 0) throw new InvalidOperationException("No currently available action has a declared need prediction.");
        var first = saved.Plan.Queue[0].Action;
        if (!HostValues.List(Intent, "options").OfType<IReadOnlyDictionary<string, object?>>().Any(x => HostValues.Text(x, "id") == first))
            throw new InvalidOperationException("Planned first action is no longer a current input option.");
        savedSim4Plan = null; // A token authorizes one normal submission, never a repeated completion.
        var intent = PrepareValue(first);
        return new
        {
            policy = "sim4", plan_id = planId, actor = input.Actor, action = first,
            submitted = true, completed = false, state_version = Version, intent,
            continuation = "host-completion-then-new-sim4-plan"
        };
    }
}
// --- Source: CandidateRanking.cs ---

public sealed partial class DvSession
{
    private long candidateRankingVersion = -1;
    private IReadOnlyDictionary<string, object?> candidateRanking = HostValues.Empty;

    /// <summary>Diagnostic scores only. Does not select or commit an action.</summary>
    public IReadOnlyDictionary<string, object?> CliCandidateRanking(bool engineControlled)
    {
        using var valuesScope = R.UseValues(valueContext);
        if (candidateRankingVersion == Version) return candidateRanking;
        var intent = Intent;
        var manual = HostValues.Text(intent, "status") == "awaiting_input";
        var prepared = engineControlled && HostValues.Text(intent, "status") == "prepared" && HostValues.Text(intent, "actor") == Viewer;
        candidateRanking = HostValues.Empty;
        candidateRankingVersion = Version;
        if (!manual && !prepared) return candidateRanking;
        EnsureObservers();
        var actor = observerSubjects[Viewer];
        var rows = new List<(object Score, IReadOnlyDictionary<string, object?> Row)>();
        string source, eventId = "", strategy;
        if (manual)
        {
            source = "current-panel-reference";
            strategy = "current-owned-utility";
            var view = F_dv_with(F_dv_with(actor, "manual_input", true), "decision_time",
                Field(Field(state, "physical", new List<object>()), "time", 0d));
            var actions = R.Items(Field(actor, "actions", new List<object>()));
            foreach (var option in HostValues.List(intent, "options").OfType<IReadOnlyDictionary<string, object?>>())
            {
                var id = HostValues.Text(option, "id");
                var action = actions.FirstOrDefault(item => R.Text(Field(item, "id", "")) == id);
                if (action is null) continue;
                var score = Run(() => F_dvs_current_action_score(view, action, Field(state, "bindings", new List<object>()),
                    F_dvp_goal_refs(view), Field(Field(Field(state, "costs", new List<object>()), R.Text(Field(action, "kind", "")), new List<object>()), "actual", new List<object>())), speculative: true);
                var total = Field(score, "total", false);
                if (!R.Bool(R.is_numeric_value(total))) continue;
                rows.Add((total, HostValues.Freeze(new Dictionary<string, object?>
                {
                    ["id"] = id, ["label"] = HostValues.Text(option, "label"),
                    ["score"] = converter.FromDv(total), ["score_text"] = R.show(total),
                    ["score_structure"] = converter.FromDv(score), ["components_recorded"] = true
                })));
            }
        }
        else
        {
            var entry = LatestObserverEvent(Viewer, "planning_choice");
            if (entry is null) return candidateRanking;
            var choice = Field(entry, "data", new List<object>());
            var selectedId = HostValues.Text(HostValues.Map(intent, "action"), "id");
            if (R.Text(Field(Field(choice, "action", new List<object>()), "id", "")) != selectedId) return candidateRanking;
            source = "recorded-engine-decision";
            eventId = R.Text(Field(entry, "id", ""));
            strategy = R.Text(Field(choice, "strategy", ""));
            foreach (var plan in R.Items(Field(choice, "plans", new List<object>())))
            {
                var path = R.Items(Field(plan, "path", new List<object>()));
                var score = Field(plan, "score", false);
                if (path.Count == 0 || !R.Bool(R.is_numeric_value(score))) continue;
                var id = R.Text(path[0]);
                var action = R.Items(Field(actor, "actions", new List<object>())).FirstOrDefault(item => R.Text(Field(item, "id", "")) == id);
                var label = action is null ? id : R.Text(Run(() => CampaignRuntime.ObserverActionLabel(R.snapshot(state), Viewer, action), speculative: true));
                var components = Field(Field(plan, "value", new List<object>()), "current_score", false);
                rows.Add((score, HostValues.Freeze(new Dictionary<string, object?>
                {
                    ["id"] = id, ["label"] = label, ["score"] = converter.FromDv(score), ["score_text"] = R.show(score),
                    ["path"] = converter.FromDv(Field(plan, "path", new List<object>())),
                    ["score_scope"] = converter.FromDv(Field(plan, "score_scope", "")),
                    ["score_structure"] = components is false ? null : converter.FromDv(components),
                    ["components_recorded"] = components is not false, ["plan"] = converter.FromDv(plan),
                    ["engine_selected"] = id == selectedId
                })));
            }
        }
        // Sort exact DV numbers. Equal scores retain their original catalogue order.
        var sorted = rows.OrderBy(row => row.Score, Comparer<object>.Create((a, b) =>
            R.Bool(R.lt(a, b)) ? 1 : R.Bool(R.lt(b, a)) ? -1 : 0)).Take(5).Select(row => row.Row).ToArray();
        candidateRanking = HostValues.Freeze(new Dictionary<string, object?>
        {
            ["source"] = source, ["event"] = eventId, ["strategy"] = strategy, ["version"] = Version,
            ["actor"] = Viewer, ["candidate_count"] = rows.Count, ["top_limit"] = 5, ["top"] = sorted
        });
        return candidateRanking;
    }
}
