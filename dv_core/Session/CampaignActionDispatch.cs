using System;
using System.Collections.Generic;
using System.Linq;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;

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
