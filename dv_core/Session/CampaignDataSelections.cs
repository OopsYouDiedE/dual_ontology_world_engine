using DuelView.Compiled;
using static DuelView.Core.DVKernel;

// Authored identities and values are supplied by campaign-selectors.data.dv.
public static partial class CampaignRuntime
{
    private static object SelectionData(string id) => F_dv_get(R.content("data", id), "value");
    private static object SelectionRecord(params (string Key, object Value)[] fields)
        => fields.Select(field => (object)new List<object> { field.Key, field.Value }).ToList();

    // Only DV-declared fixed facilities bind inspections to their authored room.
    // Portable objects keep the caller's existing position-dependent binding.
    private static object SelectionFixedInspectionRoom(object objectId, object fallback)
    {
        object result = fallback;
        bool found = false;
        foreach (var group in R.Items(F_dv_get(SelectionData("campaign.selector.world_initial"), "groups")))
            foreach (var definition in R.Items(F_dv_get(group, "objects")))
                if (R.Bool(R.eq(F_dv_get(definition, "id"), objectId)) &&
                    R.Bool(F_dv_find(definition, "fixed", false)))
                {
                    if (found) throw new InvalidOperationException("Ambiguous fixed inspection object declaration.");
                    result = F_dv_get(definition, "room");
                    if (R.Text(result).Length == 0) throw new InvalidOperationException("Fixed inspection object requires a room.");
                    found = true;
                }
        return result;
    }

    private static object SelectionFields(object definitions, object? context = null)
    {
        var fields = new List<object>();
        foreach (var entry in R.Items(definitions))
        {
            var pair = R.Items(entry);
            var definition = pair[1];
            object cell;
            if (!R.Bool(F_dv_get(definition, "known"))) cell = F_dv_unknown();
            else
            {
                var value = R.Bool(F_dv_has_key(definition, "context_field"))
                    ? F_dv_find(context ?? new List<object>(), F_dv_get(definition, "context_field"), F_dv_find(definition, "default", false))
                    : F_dv_get(definition, "value");
                cell = F_dv_field(R.copy_value(value));
            }
            if (R.Bool(F_dv_has_key(definition, "basis_id")))
                cell = F_dv_with(cell, "basis", new List<object> { F_campaign_approved(F_dv_get(definition, "basis_id")) });
            fields.Add(new List<object> { pair[0], cell });
        }
        return fields;
    }

    private static object SelectionApplyActorFields(object fields, object rules, object actor)
    {
        foreach (var rule in R.Items(rules))
            if (R.Bool(F_dv_has(F_dv_get(rule, "actors"), actor)))
                fields = F_dv_with_many(fields, SelectionFields(F_dv_get(rule, "fields")));
        return fields;
    }

    public static object F_sims_player_visible_rooms(object zone)
        => R.copy_value(F_dv_find(SelectionData("campaign.selector.visible_rooms"), zone, new List<object>()));

    public static object F_sims_unseen_dorm_rooms(object zone)
        => R.copy_value(F_dv_find(SelectionData("campaign.selector.unseen_rooms"), zone, new List<object>()));

    public static object F_sims_station_advertisements(object room)
        => R.copy_value(F_dv_find(SelectionData("campaign.selector.station_advertisements"), room, new List<object>()));

    public static object F_island_outdoors(object map)
        => F_dv_has(F_dv_get(SelectionData("campaign.selector.map_environment"), "outdoors"), map);

    public static object F_island_height(object room)
    {
        var selector = SelectionData("campaign.selector.map_environment");
        var key = F_dv_find(F_dv_get(selector, "height_keys"), F_dv_get(room, "map"), F_dv_get(selector, "default_height_key"));
        return F_dv_get(F_dv_get(F_island_parameters(), "heights"), key);
    }

    public static object F_island_case_roles()
    {
        var roles = new List<object>();
        foreach (var definition in R.Items(SelectionData("campaign.selector.case_roles")))
        {
            var role = R.copy_value(definition);
            if (R.Bool(F_dv_find(role, "all_candidates", false)))
                role = F_dv_with(F_dv_without(role, "all_candidates"), "candidates", F_campaign_people());
            roles.Add(role);
        }
        return roles;
    }

    public static object F_island_case_score(object actor, object role)
    {
        var p = F_island_case_parameters();
        var data = SelectionData("campaign.selector.case_scoring");
        var policy = F_dv_get(actor, "policy");
        var drive = F_dv_find(policy, "drive", F_dv_get(p, "fallback_drive"));
        var caution = F_dv_find(policy, "caution", F_dv_get(p, "fallback_caution"));
        var speakerTrust = F_dv_find(policy, "speaker_trust", new List<object>());
        var trust = F_dv_find(speakerTrust, F_dv_get(data, "trust_speaker"), F_dv_get(p, "fallback_trust"));
        var values = F_island_case_value(actor, F_dv_get(role, "values"));
        var experience = R.mul(R.count(F_dv_find(actor, "completed_actions", new List<object>())), F_dv_get(p, "experience_weight"));
        var erosion = R.mul(F_dv_find(actor, "erosion_rank", 0d), F_dv_get(p, "erosion_weight"));
        object knowledge = 0d;
        foreach (var check in R.Items(F_dv_get(data, "knowledge_checks")))
        {
            var owner = R.Bool(F_dv_get(check, "owned")) ? F_campaign_body(F_dv_get(actor, "id")) : F_dv_get(check, "object");
            var reference = F_campaign_ref(owner, F_dv_get(check, "field"));
            if (R.Text(F_dv_truth(F_dv_get(actor, "mind"), F_dv_at_least(reference, F_dv_get(check, "minimum")))) == "true")
                knowledge = R.add(knowledge, 1d);
        }
        object score = R.add(R.add(R.mul(values, F_dv_get(p, "value_weight")), experience), knowledge);
        var inputs = SelectionRecord(("drive", drive), ("caution", caution), ("trust", trust), ("erosion", erosion),
            ("one", 1d));
        foreach (var term in R.Items(F_dv_find(F_dv_get(data, "role_terms"), F_dv_get(role, "id"), new List<object>())))
        {
            var input = F_dv_get(term, "input");
            // Read role-specific history only when its term is selected.
            var value = R.Bool(R.eq(input, "conflicts"))
                ? R.count(F_dv_find(actor, "observed_claim_conflicts", new List<object>()))
                : F_dv_get(inputs, input);
            score = R.Bool(F_dv_get(term, "subtract")) ? R.sub(score, value) : R.add(score, value);
        }
        return SelectionRecord(("score", score), ("inputs", SelectionRecord(("drive", drive), ("caution", caution),
            ("demon_trust", trust), ("owned_value_order", values), ("actual_completed_actions", experience),
            ("owned_known_records", knowledge), ("actual_erosion_rank", erosion))),
            ("tuning_fallbacks", SelectionRecord(("drive", !R.Bool(F_dv_has_key(policy, "drive"))),
                ("caution", !R.Bool(F_dv_has_key(policy, "caution"))),
                ("demon_trust", !R.Bool(F_dv_has_key(speakerTrust, F_dv_get(data, "trust_speaker")))))),
            ("basis", F_dv_get(p, "basis")), ("scope", F_dv_get(data, "scope")));
    }

    public static object F_island_case_candidate(object actor, object role, object world)
    {
        var id = F_dv_get(actor, "id");
        if (!R.Bool(F_dv_has(F_dv_get(role, "candidates"), id)) || !R.Bool(F_dvs_actor_can_act(actor))
            || R.Text(F_dv_truth(world, F_c2_can_act(id))) != "true"
            || R.Text(F_dv_truth(world, F_dv_fact(F_campaign_own(id, "exiled"), true))) == "true") return false;
        var requirement = F_dv_find(F_dv_get(SelectionData("campaign.selector.case_scoring"), "candidate_requirements"), F_dv_get(role, "id"), false);
        if (requirement is not false && !R.Bool(F_dv_has(F_dv_get(requirement, "exempt"), id)))
        {
            var scholarly = R.Bool(R.eq(F_dv_get(F_campaign_profile(id), "domain"), F_dv_get(requirement, "domain")))
                || R.Bool(F_dv_has(F_dv_find(F_dv_get(actor, "policy"), "value_order", new List<object>()), F_dv_get(requirement, "value")));
            return scholarly && R.Bool(F_c2_received_requirement(actor, F_dv_get(requirement, "receipt")));
        }
        return true;
    }

    public static object F_c2_alive(object actor)
    {
        var definitions = SelectionData("campaign.selector.body_eligibility");
        return R.Bool(F_dv_has(F_dv_get(definitions, "exempt"), actor))
            ? F_dv_true() : F_dv_fact(F_campaign_own(actor, F_dv_get(definitions, "alive_field")), true);
    }

    public static object F_c2_can_act(object actor)
    {
        var definitions = SelectionData("campaign.selector.body_eligibility");
        if (R.Bool(F_dv_has(F_dv_get(definitions, "exempt"), actor))) return F_dv_true();
        var conditions = new List<object> { F_c2_alive(actor) };
        foreach (var field in R.Items(F_dv_get(definitions, "inactive_fields")))
            conditions.Add(F_dv_fact(F_campaign_own(actor, field), false));
        return F_dv_all(conditions);
    }

    public static object F_c2_initial_body(object actor)
    {
        var definition = SelectionData("campaign.selector.body_initial");
        var fields = SelectionApplyActorFields(SelectionFields(F_dv_get(definition, "base_fields")), F_dv_get(definition, "actor_variants"), actor);
        foreach (var work in R.Items(F_c2_work_specs()))
            if (R.Bool(R.eq(F_dv_get(work, "actor"), actor)))
                foreach (var prefix in R.Items(F_dv_get(definition, "work_prefixes")))
                    fields = F_dv_with(fields, R.cat(prefix, F_dv_get(work, "id")), F_dv_field(false));
        fields = SelectionApplyActorFields(fields, F_dv_get(definition, "after_work_variants"), actor);
        fields = F_dv_with_many(fields, SelectionFields(F_dv_get(definition, "tail_fields")));
        foreach (var norm in R.Items(F_c2_norms()))
            foreach (var prefix in R.Items(F_dv_get(definition, "norm_prefixes")))
                fields = F_dv_with(fields, R.cat(prefix, F_dv_get(norm, "id")), F_dv_field(false));
        for (object day = F_dv_get(definition, "first_day"); !R.Bool(R.lt(F_dv_get(definition, "last_day"), day)); day = R.add(day, 1d))
            foreach (var prefix in R.Items(F_dv_get(definition, "daily_prefixes")))
                foreach (var suffix in R.Items(F_dv_get(definition, "meal_suffixes")))
                    fields = F_dv_with(fields, R.cat(prefix, R.cat(R.show(day), suffix)), F_dv_field(false));
        var roles = F_dv_get(F_campaign_profile(actor), "station_roles");
        foreach (var entry in R.Items(F_dv_get(definition, "actor_capabilities")))
        {
            var pair = R.Items(entry);
            fields = F_dv_with(fields, pair[0], F_dv_field(F_dv_has(pair[1], actor)));
        }
        foreach (var entry in R.Items(F_dv_get(definition, "role_capabilities")))
        {
            var pair = R.Items(entry);
            fields = F_dv_with(fields, pair[0], F_dv_field(F_dv_has(roles, pair[1])));
        }
        return F_c2_object(F_campaign_body(actor), "", fields);
    }

    public static object F_c2_initial_body_from(object actor, object world)
    {
        var body = F_c2_initial_body(actor);
        var fields = F_dv_get(F_dv_get(body, "facets"), "state");
        foreach (var entry in R.Items(fields))
        {
            var field = R.Items(entry)[0];
            var prior = F_dv_read(world, F_dv_ref(actor, "body", field));
            if (!R.Bool(F_dv_uncontested(prior))) prior = F_dv_read(world, F_dv_ref(actor, "state", field));
            if (R.Bool(F_dv_uncontested(prior)))
                body = F_dv_with(body, "facets", F_dv_with(F_dv_get(body, "facets"), "state", F_dv_with(F_dv_get(F_dv_get(body, "facets"), "state"), field, prior)));
        }
        if (R.Text(F_dv_truth(world, F_dv_fact(F_campaign_finale_eaten_ref(actor), true))) == "true")
        {
            var definition = SelectionData("campaign.selector.body_initial");
            var meal = SelectionFields(F_dv_get(definition, "inherited_meal"));
            var observed = new List<object>();
            foreach (var entry in R.Items(meal))
            {
                var pair = R.Items(entry);
                observed.Add(new List<object> { pair[0], F_dv_with(pair[1], "basis", new List<object> { F_c2u_source(F_dv_get(definition, "inherited_meal_source")) }) });
            }
            body = F_dv_with(body, "facets", F_dv_with(F_dv_get(body, "facets"), "state", F_dv_with_many(F_dv_get(F_dv_get(body, "facets"), "state"), observed)));
        }
        return body;
    }

    private static object SelectionInstallObject(object world, object definition)
    {
        var id = F_dv_get(definition, "id");
        var fields = SelectionFields(F_dv_get(definition, "fields"), world);
        var instance = R.Bool(F_dv_get(definition, "positioned"))
            ? F_c2_object(id, F_dv_get(definition, "room"), fields)
            : F_campaign_state_object(id, fields);
        var result = F_campaign_add_new(world, instance);
        foreach (var entry in R.Items(F_dv_find(definition, "missing_fields", new List<object>())))
        {
            var pair = R.Items(entry);
            var existingFields = F_dv_get(F_dv_get(F_dv_get(F_dv_get(result, "objects"), id), "facets"), "state");
            if (!R.Bool(F_dv_has_key(existingFields, pair[0])))
            {
                var cell = F_dv_get(SelectionFields(new List<object> { new List<object> { pair[0], pair[1] } }, world), pair[0]);
                result = F_dv_store(result, F_campaign_ref(id, pair[0]), cell);
            }
        }
        return result;
    }

    public static object F_c2_norm_object(object id)
        => R.cat(F_dv_get(F_dv_get(SelectionData("campaign.selector.world_initial"), "norm"), "id_prefix"), id);

    public static object F_c2_world_objects(object world)
    {
        var data = SelectionData("campaign.selector.world_initial");
        var result = world;
        foreach (var group in R.Items(F_dv_get(data, "groups")))
        {
            foreach (var definition in R.Items(F_dv_get(group, "objects")))
                result = SelectionInstallObject(result, definition);
            foreach (var entry in R.Items(F_dv_get(group, "unknown_defaults")))
            {
                var reference = F_dv_get(entry, "ref");
                if (!R.Bool(F_dv_get(F_dv_read(result, reference), "known")))
                    result = F_dv_store(result, reference, F_dv_field(F_dv_get(entry, "value")));
            }
        }
        var normTemplate = F_dv_get(data, "norm");
        foreach (var norm in R.Items(F_c2_norms()))
            result = F_campaign_add_new(result, F_c2_object(F_c2_norm_object(F_dv_get(norm, "id")),
                F_dv_get(norm, F_dv_get(normTemplate, "room_field")), SelectionFields(F_dv_get(normTemplate, "fields"), norm)));
        var litter = F_dv_get(data, "litter");
        foreach (var person in R.Items(F_campaign_people()))
        {
            // Prior body evidence comes from the input world, as in the original
            // chapter handoff; newly installed fields cannot become prior facts.
            result = F_campaign_add_new(result, F_c2_initial_body_from(person, world));
            var group = F_dv_get(F_campaign_profile(person), "group");
            result = F_campaign_add_new(result, F_c2_object(R.cat(F_dv_get(litter, "id_prefix"), group),
                R.cat(F_dv_get(litter, "room_prefix"), group), SelectionFields(F_dv_get(litter, "fields"))));
        }
        result = F_c2i_world_objects(result);
        var weather = F_dv_get(data, "weather");
        foreach (var change in R.Items(F_c2_weather_changes()))
        {
            var reference = F_campaign_ref(F_dv_get(weather, "object"), R.cat(F_dv_get(weather, "occurrence_prefix"), F_dv_get(change, "id")));
            if (!R.Bool(F_dv_get(F_dv_read(result, reference), "known")))
                result = F_dv_store(result, reference, F_dv_field(false));
        }
        return result;
    }








}
