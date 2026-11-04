using DuelView.Compiled;
using static DuelView.Core.DVKernel;

public static partial class CampaignRuntime
{
    public static object F_dvg_open()
        => F_dvg_open_config(F_dv_get(F_host_data("campaign.runtime.config"), "opening"));

    public static object F_dvg_host_open(object options)
    {
        var definitions = F_dv_get(F_host_data("campaign.runtime.config"), "host");
        var controller = F_dv_find(options, "controller", F_dv_get(definitions, "default_controller"));
        var viewer = F_dv_find(options, "viewer", F_dv_get(definitions, "default_viewer"));
        F_dv_require(F_dv_has(F_dv_get(definitions, "controllers"), controller), "Unsupported host controller");
        var state = F_dvg_open_config(F_dv_find(options, "campaign", new List<object>()));
        var found = false;
        foreach (var actor in R.Items(F_dv_get(state, "actors")))
        {
            if (!R.Bool(R.eq(F_dv_get(actor, "id"), viewer))) continue;
            found = true;
            F_dv_require(R.Text(F_dv_local(F_dv_get(state, "bindings"), viewer, viewer)) != "", "Host viewer has no projection binding");
            var position = F_dv_read(F_dv_get(state, "physical"), F_dv_get(actor, "location_ref"));
            F_dv_require(F_dv_uncontested(position), "Host viewer has no settled location");
            F_dv_require(R.Text(F_dv_get(F_dvg_place(F_dv_get(position, "value")), "map")) != "", "Host viewer has no map binding");
        }
        F_dv_require(found, "Unknown host viewer");
        var player = R.Bool(R.eq(controller, F_dv_get(definitions, "interactive_controller"))) ? viewer : "";
        state = F_dvs_set_player(state, player);
        return F_dvs_owned_state_update(state, HostRecord(("story", F_dv_with(F_dv_get(state, "story"), "host_viewer", viewer))));
    }

    public static object F_dvg_host_viewer(object state)
        => F_dv_find(F_dv_get(state, "story"), "host_viewer", F_dv_get(F_dv_get(F_host_data("campaign.runtime.config"), "host"), "default_viewer"));

    public static object F_dvg_run_config(object config)
    {
        var definitions = F_host_data("campaign.runtime.config");
        foreach (var option in R.Items(F_dv_get(definitions, "retired")))
            F_dv_require(!R.Bool(F_dv_has_key(config, option)), R.cat("Retired planning option: ", option));
        object result = new List<object>();
        foreach (var entry in R.Items(F_dv_get(definitions, "defaults")))
        {
            var pair = R.Items(entry);
            result = F_dv_with(result, pair[0], F_dv_find(config, pair[0], pair[1]));
        }
        foreach (var check in R.Items(F_dv_get(definitions, "checks")))
        {
            var value = F_dv_get(result, F_dv_get(check, "key"));
            var valid = R.Text(F_dv_get(check, "kind")) switch
            {
                "one_of" => R.Bool(F_dv_has(F_dv_get(check, "values"), value)),
                "boolean" => value is bool,
                "nonnegative_integer" => R.Bool(R.eq(R.floor(value), value)) && !R.Bool(R.lt(value, 0d)),
                _ => throw new InvalidDataException("Unknown campaign configuration constraint")
            };
            F_dv_require(valid, F_dv_get(check, "message"));
        }
        return result;
    }

    // Campaign callers pass the existing validated, normalized run_config.
    // A standalone scenario may declare only its explicit mechanism switches;
    // its other switches retain the context's original environment fallback.
    internal static void ConfigureCoreRuntime(object config)
    {
        var fallback = R.CurrentRuntimeOptions;
        R.ConfigureRuntimeOptions(new R.RuntimeOptions(
            LazyInternal: R.Bool(F_dv_find(config, "opt_lazyinternal", fallback.LazyInternal)),
            Cow: R.Bool(F_dv_find(config, "opt_cow", fallback.Cow)),
            Mask: R.Bool(F_dv_find(config, "opt_mask", fallback.Mask)),
            MaskVerify: R.Bool(F_dv_find(config, "opt_mask_verify", fallback.MaskVerify)),
            AppendShare: R.Bool(F_dv_find(config, "opt_appendshare", fallback.AppendShare)),
            ReadView: R.Bool(F_dv_find(config, "opt_readview", fallback.ReadView)),
            RecordCache: R.Bool(F_dv_find(config, "opt_recordcache", fallback.RecordCache)),
            PlanCache: R.Bool(F_dv_find(config, "opt_plancache", fallback.PlanCache)),
            DeclarationCache: R.Bool(F_dv_find(config, "opt_declcache", fallback.DeclarationCache)),
            Predicate: R.Bool(F_dv_find(config, "opt_predicate", fallback.Predicate)),
            PredicateCache: R.Bool(F_dv_find(config, "opt_predicatecache", fallback.PredicateCache)),
            StateIndex: R.Bool(F_dv_find(config, "opt_stateindex", fallback.StateIndex)),
            RuntimeSpeed: R.Bool(F_dv_find(config, "opt_runtimespeed", fallback.RuntimeSpeed))));
    }

    public static object F_dvg_configure_initial_room(object scene, object config)
    {
        foreach (var relocation in R.Items(F_dv_get(F_host_data("campaign.runtime.config"), "relocations")))
        {
            var location = F_dv_get(config, F_dv_get(relocation, "option"));
            if (R.Bool(R.eq(location, F_dv_get(relocation, "default")))) continue;
            var identity = F_dv_get(relocation, "actor");
            var reference = F_dv_get(relocation, "ref");
            var cell = F_dv_with(F_dv_field(location), "basis", F_dv_get(relocation, "basis"));
            F_dv_require(R.eq(F_dv_local(F_dv_get(scene, "bindings"), identity, identity), identity), F_dv_get(relocation, "binding_error"));
            var physical = F_dv_store(F_dv_get(scene, "physical"), reference, cell);
            var actors = new List<object>();
            foreach (var current in R.Items(F_dv_get(scene, "actors")))
            {
                var actor = current;
                if (R.Bool(R.eq(F_dv_get(actor, "id"), identity)))
                    actor = F_dv_with_many(actor, HostRecord(("location", location),
                        ("mind", F_dv_store(F_dv_get(actor, "mind"), F_dv_get(actor, "location_ref"), cell))));
                actors.Add(actor);
            }
            scene = F_dv_with_many(scene, HostRecord(("physical", physical), ("actors", actors)));
        }
        return scene;
    }

    public static object F_dvg_without_detective(object scene)
    {
        var definition = F_dv_get(F_host_data("campaign.runtime.config"), "excluded_actor");
        var identity = F_dv_get(definition, "id");
        var reference = F_dv_get(definition, "ref");
        var actors = new List<object>();
        foreach (var actor in R.Items(F_dv_get(scene, "actors")))
        {
            if (R.Bool(R.eq(F_dv_get(actor, "id"), identity))) continue;
            var mind = F_dv_get(actor, "mind");
            if (R.Bool(F_dv_has_key(F_dv_get(mind, "objects"), identity)))
                mind = F_dv_store(mind, reference, F_dv_unknown());
            actors.Add(F_dv_with(actor, "mind", mind));
        }
        var physical = F_dv_store(F_dv_get(scene, "physical"), reference, F_dv_field(""));
        return F_dv_with_many(scene, HostRecord(("actors", actors), ("physical", physical)));
    }
}
