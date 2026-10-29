using DuelView.Compiled;
using static DuelView.Core.DVKernel;

// Maintained host projection mechanisms. All authored identities, room links,
// text and exceptional matches come from host.runtime.dv.
public static partial class CampaignRuntime
{
    private static object HostRecord(params (string Key, object Value)[] fields)
        => fields.Select(field => (object)new List<object> { field.Key, field.Value }).ToList();

    public static object F_host_data(object id) => F_dv_get(R.content("data", id), "value");

    // Matching reads declared equality/suffix constraints. It cannot invoke
    // functions, mutate world state or create observations.
    public static object F_host_match(object pattern, object query)
    {
        foreach (var entry in R.Items(F_dv_find(pattern, "equals", new List<object>())))
        {
            var pair = R.Items(entry);
            if (!R.Bool(F_dv_has_key(query, pair[0])) || !R.Bool(F_dv_same(F_dv_get(query, pair[0]), pair[1]))) return false;
        }
        foreach (var entry in R.Items(F_dv_find(pattern, "suffix", new List<object>())))
        {
            var pair = R.Items(entry);
            if (F_dv_find(query, pair[0], false) is not string value
                || !value.EndsWith(R.Text(pair[1]), StringComparison.Ordinal)) return false;
        }
        return true;
    }

    public static object F_host_select(object rules, object query, object fallback)
    {
        foreach (var rule in R.Items(rules))
            if (R.Bool(F_host_match(F_dv_get(rule, "match"), query))) return F_dv_get(rule, "value");
        return fallback;
    }

    private static object HostNameReference(object template, object id)
        => F_dv_ref(R.cat(F_dv_get(template, "object_prefix"), id), F_dv_get(template, "facet"), F_dv_get(template, "field"));

    private static string? HostKnownText(object world, object reference)
    {
        var cell = F_dv_read(world, reference);
        return R.Bool(F_dv_uncontested(cell)) && F_dv_get(cell, "value") is string text ? text : null;
    }

    private static bool HostSameEdge(object origin, object destination, object from, object to)
        => (R.Bool(R.eq(origin, from)) && R.Bool(R.eq(destination, to)))
            || (R.Bool(R.eq(origin, to)) && R.Bool(R.eq(destination, from)));

    public static object F_dvg_places() => F_host_data("host.runtime.places");
    public static object F_dvg_places_build() => R.copy_value(F_dvg_places());

    public static object F_dvg_place(object location)
        => F_dv_find(F_dvg_places(), location, HostRecord(("map", ""), ("room", location)));

    public static object F_dvg_initial_anchor(object actor, object location)
        => F_host_select(F_host_data("host.runtime.initial_anchors"), HostRecord(("actor", actor), ("location", location)), "");

    public static object F_dvg_interaction(object action)
    {
        if (R.Bool(F_dv_has_key(action, "interaction")))
        {
            var interaction = F_dv_get(action, "interaction");
            return R.Text(F_dv_find(interaction, "kind", "")) == "self"
                ? F_dv_with(interaction, "purpose", F_dv_find(interaction, "purpose", F_dvg_self_purpose(action)))
                : interaction;
        }
        var travel = F_dv_find(action, "travel", new List<object>());
        if (R.Text(F_dv_get(action, "kind")) == "move" && R.Bool(F_dv_has_key(travel, "to")))
            return HostRecord(("kind", "doorway"), ("destination", F_dvg_place(F_dv_get(travel, "to"))));
        var definitions = F_host_data("host.runtime.interactions");
        if (R.Bool(F_dv_has_key(travel, "station")))
        {
            var station = F_dv_get(travel, "station");
            var prefixLength = F_dv_get(definitions, "station_prefix_length");
            return HostRecord(("kind", "site"), ("site", R.slice(station, prefixLength, R.sub(R.length(station), prefixLength))));
        }
        var selected = F_host_select(F_dv_get(definitions, "rules"), action, false);
        if (selected is not false) return R.copy_value(selected);
        var request = F_dv_find(F_dv_find(action, "speech", new List<object>()), "request", new List<object>());
        if (R.Bool(F_dv_has_key(request, "target")))
            return HostRecord(("kind", "person"), ("person", F_dv_get(request, "target")));
        var listeners = R.Items(F_dv_find(action, "listeners", new List<object>()));
        return listeners.Count == 1
            ? HostRecord(("kind", "person"), ("person", listeners[0]))
            : HostRecord(("kind", "self"), ("purpose", F_dvg_self_purpose(action)));
    }

    public static object F_dvg_unknown_person_label(object actor, object viewer)
    {
        // Archived records can bind an older complete content pack. They may
        // display recorded labels but cannot run a declaration added later.
        if (R.content("data", "template.host_unknown_person_label") is false)
        {
            var identity = R.content("data", "host.runtime.identity");
            if (identity is false) return "";
            var unknown = F_dv_find(F_dv_get(identity, "value"), "unknown", false);
            return unknown is false ? "" : F_dv_find(unknown, "fallback", "");
        }
        return DuelView.Core.DvValueTemplate.Instantiate("template.host_unknown_person_label", actor, viewer);
    }

    public static object F_campaign_unknown_person_label(object actor, object viewer)
        => F_dvg_unknown_person_label(actor, viewer);

    public static object F_campaign_person_name_ref(object actor)
    {
        if (R.content("data", "template.host_person_name_ref") is false)
        {
            var identity = R.content("data", "host.runtime.identity");
            if (identity is not false)
            {
                var references = F_dv_find(F_dv_get(identity, "value"), "person_name_refs", new List<object>());
                var recorded = F_dv_find(references, actor, false);
                if (recorded is not false) return R.copy_value(recorded);
            }
            return F_dv_ref(actor, "identity", "name");
        }
        return DuelView.Core.DvValueTemplate.Instantiate("template.host_person_name_ref", actor);
    }

    public static object F_c1_player_name_parts(object actor, object label)
    {
        var definitions = F_host_data("host.runtime.identity");
        var excluded = F_dv_get(definitions, "label_excluded");
        var overrides = F_dv_get(definitions, "label_name_refs");
        var people = R.Items(F_campaign_people());
        var text = R.Text(label);
        var parts = new List<object>();
        var plain = new System.Text.StringBuilder();
        for (var cursor = 0; cursor < text.Length;)
        {
            object matched = "";
            var matchedName = "";
            foreach (var person in people)
            {
                if (R.Bool(F_dv_has(excluded, person))) continue;
                var name = R.Text(F_campaign_person_name(person));
                if (cursor + name.Length <= text.Length && text.AsSpan(cursor, name.Length).SequenceEqual(name.AsSpan()))
                {
                    matched = person;
                    matchedName = name;
                    break;
                }
            }
            if (R.Text(matched) == "")
            {
                plain.Append(text[cursor++]);
                continue;
            }
            if (plain.Length > 0)
            {
                parts.Add(HostRecord(("text", plain.ToString())));
                plain.Clear();
            }
            var reference = F_dv_find(overrides, matched, F_dv_ref(matched, "identity", "name"));
            parts.Add(HostRecord(("ref", R.copy_value(reference)), ("fallback", F_dvg_unknown_person_label(matched, actor))));
            cursor += matchedName.Length;
        }
        if (plain.Length > 0) parts.Add(HostRecord(("text", plain.ToString())));
        return parts;
    }

    public static object F_dvg_subjective_person_text(object state, object subject, object text)
    {
        var excluded = F_dv_get(F_host_data("host.runtime.identity"), "narration_excluded");
        var result = text;
        var subjectId = F_dv_get(subject, "id");
        foreach (var person in R.Items(F_campaign_people()))
        {
            if (R.Bool(R.eq(person, subjectId)) || R.Bool(F_dv_has(excluded, person))) continue;
            var known = F_dvg_preview_person_name(state, F_dv_get(subject, "mind"), person, person, "");
            if (R.Text(known) == "")
                result = F_dvs_present_replace(result, F_campaign_person_name(person), F_dvg_unknown_person_label(person, subjectId));
        }
        return result;
    }

    public static object F_dvg_preview_name(object world, object id, object fallback)
    {
        var name = HostKnownText(world, F_dv_ref(id, "identity", "name"));
        if (name is not null) return name;
        name = HostKnownText(world, F_dv_ref(id, "state", "name"));
        if (name is not null) return name;
        var definitions = F_host_data("host.runtime.identity");
        var objectName = F_dv_find(F_dv_get(definitions, "object_names"), id, false);
        if (objectName is not false)
        {
            name = HostKnownText(world, F_dv_get(objectName, "ref"));
            if (name is not null) return F_dvs_present_replace(F_dv_get(objectName, "template"), "{value}", name);
        }
        foreach (var template in R.Items(F_dv_get(definitions, "name_lookup_templates")))
        {
            name = HostKnownText(world, HostNameReference(template, id));
            if (name is not null) return name;
        }
        var people = F_campaign_people();
        if (R.Text(F_dv_get(world, "domain")) == "physical")
        {
            if (R.Bool(F_dv_has(people, id)) || R.Bool(F_dv_has(F_dv_get(definitions, "physical_people_extra"), id)))
                return F_campaign_person_name(id);
            return F_dv_find(F_dv_find(F_dv_get(world, "objects"), id, new List<object>()), "map_title", fallback);
        }
        var query = HostRecord(("id", id));
        foreach (var rule in R.Items(F_dv_get(definitions, "known_speaker_rules")))
            if (R.Bool(F_host_match(F_dv_get(rule, "match"), query))
                && R.Text(F_dv_truth(world, F_dv_get(rule, "when"))) == "true")
                return F_dv_get(rule, "value");
        if (R.Bool(F_dv_has(people, id)))
        {
            name = HostKnownText(world, HostNameReference(F_dv_get(definitions, "introduced_name_template"), id));
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return fallback;
    }

    public static object F_dvg_literal_self_name(object literal, object person)
    {
        var operation = R.Text(F_dv_find(literal, "op", ""));
        if (operation == "equals" && F_dv_find(literal, "value", false) is string name)
        {
            var reference = F_dv_find(literal, "ref", false);
            foreach (var template in R.Items(F_dv_get(F_host_data("host.runtime.identity"), "self_name_reference_templates")))
                if (R.Bool(F_dv_same(HostNameReference(template, person), reference))) return name;
        }
        if (operation == "all")
            foreach (var item in R.Items(F_dv_get(literal, "items")))
            {
                var selfName = R.Text(F_dvg_literal_self_name(item, person));
                if (selfName != "") return selfName;
            }
        return "";
    }

    public static object F_dvg_map_access(object state, object currentMap)
    {
        var definitions = F_host_data("host.runtime.access");
        var maps = R.copy_value(F_dv_get(definitions, "defaults"));
        var physical = F_dv_get(state, "physical");
        foreach (var entry in R.Items(F_dv_get(definitions, "maps")))
        {
            var map = F_dv_get(entry, "map");
            var opened = R.Text(F_dv_truth(physical, F_dv_get(entry, "condition"))) == "true";
            maps = F_dv_with(maps, map, opened);
            if (R.Bool(R.eq(currentMap, map))) maps = F_dv_with(maps, F_dv_get(definitions, "hub_map"), opened);
        }
        return maps;
    }

    public static object F_dvg_host_access(object state, object map)
    {
        var definitions = F_host_data("host.runtime.access");
        return F_dv_with_many(F_dv_get(definitions, "host_defaults"), HostRecord(
            ("isolation_open", R.Text(F_dv_truth(F_dv_get(state, "physical"), F_dv_get(definitions, "isolation"))) == "true"),
            ("maps", F_dvg_map_access(state, map))));
    }

    public static object F_dvg_traces(object world)
    {
        object result = new List<object>();
        foreach (var entry in R.Items(F_host_data("host.runtime.traces")))
        {
            var pair = R.Items(entry);
            result = F_dv_with(result, pair[0], R.Text(F_dv_truth(world, pair[1])) == "true");
        }
        return result;
    }

    public static object F_dvg_preview_site_descriptor(object action, object site, object location)
    {
        var descriptor = F_dv_find(F_host_data("host.runtime.site_descriptors"), site, false);
        if (descriptor is not false)
        {
            if (F_dv_find(descriptor, "id_template", false) is string template)
                descriptor = F_dv_with(F_dv_without(descriptor, "id_template"), "id", F_dvs_present_replace(template, "{location}", location));
            return R.copy_value(descriptor);
        }
        var definition = F_campaign_object_descriptor(site);
        return R.Text(F_dv_get(definition, "map_kind")) == "object"
            ? HostRecord(("id", site), ("title", F_dv_get(definition, "map_title")), ("kind", "object"), ("is_viewer", false))
            : false;
    }

    public static object F_dvg_preview_door_gates(object origin, object destination)
    {
        var edges = F_c2_edges();
        var supplemental = F_dv_get(F_host_data("host.runtime.doors"), "supplemental_gates");
        var key = new List<object> { "host-door-gates-v2", origin, destination, supplemental };
        var cached = R.Items(R.memo_get(edges, key));
        if (R.Bool(cached[0])) return cached[1];
        var result = new List<object>();
        foreach (var edge in R.Items(edges))
            if (HostSameEdge(origin, destination, F_dv_get(edge, "from"), F_dv_get(edge, "to")))
                result.Add(F_campaign_access(F_dv_get(edge, "gate")));
        foreach (var entry in R.Items(supplemental))
        {
            var pair = R.Items(entry);
            if (R.Bool(R.eq(origin, pair[0])) || R.Bool(R.eq(destination, pair[0]))) result.Add(R.copy_value(pair[1]));
        }
        return R.memo_put(edges, key, result);
    }

    public static object F_dvg_preview_doors(object world, object doors)
    {
        var subject = F_dv_find(world, "subject", "");
        R.phase("observer-doors-start", subject);
        var definitions = F_host_data("host.runtime.doors");
        var labels = F_dv_get(definitions, "labels");
        var result = new List<object>();
        foreach (var door in R.Items(doors))
        {
            var origin = F_dv_find(F_dv_get(door, "from"), "logical_location", "");
            var destination = F_dv_find(F_dv_get(door, "to"), "logical_location", "");
            object status = "unknown";
            var label = F_dv_get(labels, "unknown");
            object basis = new List<object>();
            object special = false;
            foreach (var rule in R.Items(F_dv_get(definitions, "special")))
            {
                var edge = R.Items(F_dv_get(rule, "between"));
                if (HostSameEdge(origin, destination, edge[0], edge[1])) { special = rule; break; }
            }
            if (special is not false)
            {
                foreach (var choice in R.Items(F_dv_get(special, "states")))
                    if (R.Text(F_dv_truth(world, F_dv_get(choice, "when"))) == "true")
                    {
                        status = F_dv_get(choice, "state");
                        label = F_dv_get(choice, "label");
                        basis = R.copy_value(F_dv_get(choice, "basis"));
                        break;
                    }
            }
            else
            {
                foreach (var reference in R.Items(F_dvg_preview_door_gates(origin, destination)))
                {
                    var access = F_dv_read(world, reference);
                    if (!R.Bool(F_dv_uncontested(access))) continue;
                    basis = new List<object> { reference };
                    status = R.Bool(R.eq(F_dv_get(access, "value"), true)) ? "open" : "closed";
                    label = F_dv_get(labels, status);
                }
            }
            var passed = R.Items(F_dv_find(door, "passage_action_ids", new List<object>())).Count > 0;
            var known = R.Text(status) != "unknown";
            if (!known && passed) label = F_dv_get(labels, "observed_unknown");
            result.Add(F_dv_with_many(door, HostRecord(
                ("state", status), ("state_label", label), ("known", known),
                ("connection_known", true), ("passage_observed", passed),
                ("state_refs", basis), ("state_scope", F_dv_get(world, "domain")))));
        }
        R.phase("observer-doors-end", subject);
        return result;
    }
}
