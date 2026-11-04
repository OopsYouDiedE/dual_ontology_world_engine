using DuelView.Compiled;
using static DuelView.Core.DVKernel;

public static partial class CampaignRuntime
{
    // Scene additions contain authored objects, methods and actor-local knowledge.
    // Installing a method never executes it or copies its physical conclusions to a mind.
    public static object F_campaign_install_extension(object scene, object extension)
    {
        var empty = new List<object>();
        var physical = F_dv_get(scene, "physical");
        var objects = F_dv_find(extension, "objects", empty);
        foreach (var item in R.Items(objects)) physical = F_dv_add_object(physical, item);
        foreach (var cell in R.Items(F_dv_find(extension, "physical_cells", empty)))
            physical = F_dv_store(physical, F_dv_get(cell, "ref"), F_dv_get(cell, "cell"));

        var rules = F_dv_find(extension, "rules", empty);
        foreach (var rule in R.Items(rules))
            physical = F_dv_adopt(physical,
                F_campaign_method(F_dv_get(rule, "name"), F_dv_get(rule, "law")),
                "enforce", 1d, F_dv_get(rule, "basis"));

        var actors = new List<object>();
        var bindings = F_dv_clone(F_dv_get(scene, "bindings"));
        foreach (var original in R.Items(F_dv_get(scene, "actors")))
        {
            var actor = original;
            var id = F_dv_get(actor, "id");
            var mind = F_dv_get(actor, "mind");
            foreach (var item in R.Items(objects))
            {
                var objectId = F_dv_get(item, "id");
                mind = F_dv_add_object(mind, F_campaign_local_object(objectId));
                bindings = F_dv_append(bindings, F_dv_binding(objectId, objectId, id));
            }
            foreach (var entry in R.Items(F_dv_find(extension, "actors", empty)))
            {
                if (!R.Bool(R.eq(F_dv_get(entry, "id"), id))) continue;
                foreach (var cell in R.Items(F_dv_find(entry, "initial_cells", empty)))
                {
                    var reference = F_dv_get(cell, "ref");
                    var objectId = F_dv_get(reference, "object");
                    if (!R.Bool(F_dv_has_key(F_dv_get(mind, "objects"), objectId)))
                    {
                        // An authored initial belief can refer to an existing scene object
                        // that this actor's original mind did not yet represent.
                        F_dv_require(F_dv_has_key(F_dv_get(physical, "objects"), objectId),
                            R.cat("Unknown initial belief object: ", objectId));
                        mind = F_dv_add_object(mind, F_campaign_local_object(objectId));
                        if (R.Bool(R.eq(F_dv_local(bindings, objectId, id), "")))
                            bindings = F_dv_append(bindings, F_dv_binding(objectId, objectId, id));
                    }
                    mind = F_dv_store(mind, reference, F_dv_get(cell, "cell"));
                }
                foreach (var rule in R.Items(rules))
                {
                    if (!R.Bool(F_dv_has(F_dv_find(rule, "predictors", empty), id))) continue;
                    mind = F_dv_adopt(mind,
                        F_campaign_method(F_dv_get(rule, "name"),
                            F_dv_find(rule, "prediction_law", F_dv_get(rule, "law"))),
                        "predict", 1d, F_dv_get(rule, "basis"));
                }
                foreach (var field in new[] { "actions", "hearing_grants", "request_channels" })
                {
                    var values = F_dv_clone(F_dv_find(actor, field, empty));
                    foreach (var item in R.Items(F_dv_find(entry, field, empty)))
                        values = F_dv_append(values, item);
                    actor = F_dv_with(actor, field, values);
                }
                var policy = F_dv_get(actor, "policy");
                var goals = F_dv_clone(F_dv_find(policy, "goals", empty));
                foreach (var goal in R.Items(F_dv_find(entry, "goals", empty)))
                    goals = F_dv_append(goals, goal);
                actor = F_dv_with(actor, "policy", F_dv_with(policy, "goals", goals));
            }
            actors.Add(F_dv_with(actor, "mind", mind));
        }
        return F_dv_with_many(scene, new List<object>
        {
            F_dv_pair("physical", physical), F_dv_pair("actors", actors),
            F_dv_pair("bindings", bindings)
        });
    }
}
