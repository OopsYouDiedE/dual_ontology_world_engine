using DuelView.Compiled;
using System.Runtime.CompilerServices;

namespace DuelView.Core;

/// <summary>World / Simulation: native Core mechanisms.</summary>
public static partial class DVKernel
{
    private sealed class SimulationBindingQueries
    {
        internal readonly ConditionalWeakTable<object, SimulationFactQuery> Facts = new();
    }
    private sealed class SimulationFactQuery
    {
        internal readonly ConditionalWeakTable<object, SimulationQueryValue> References = new();
        internal readonly ConditionalWeakTable<object, SimulationFactValues> RealityValues = new();
    }
    private sealed class SimulationFactValues
    {
        internal readonly ConditionalWeakTable<object, SimulationQueryValue> Observers = new();
    }
    private sealed record SimulationQueryValue(object Value);
    private static readonly ConditionalWeakTable<object, SimulationBindingQueries> SimulationQueries = new();

    // These descriptors depend only on the exact immutable binding/definition
    // values. Current physical and subjective cells are still read every sync.
    private static SimulationFactQuery SimulationQuery(object bindings, object declaration)
        => SimulationQueries.GetValue(bindings, static _ => new()).Facts.GetValue(declaration, static _ => new());

    private static object SimulationQueryReference(SimulationFactQuery query, object bindings, object declaration, object identity)
        => query.References.GetValue(identity, observer =>
        {
            var reference = F_dv_get(declaration, "ref");
            var local = F_dv_local(bindings, F_dv_get(reference, "object"), F_dv_get(observer, "actor"));
            return new SimulationQueryValue(R.Bool(R.eq(local, "")) ? false : R.snapshot(F_dv_scoped_ref(
                F_dv_get(observer, "actor"), local, F_dv_get(reference, "facet"), F_dv_get(reference, "field"))));
        }).Value;

    // Only this owned synchronization pipeline uses the no-op shortcut. Public
    // DV record updates retain their independent writable-copy contract.
    private static object SimulationSet(object record, string key, object value)
    {
        foreach (var entry in R.Items(record))
        {
            if (!R.TryItems(entry, out var pair) || pair.Count != 2) break;
            if (pair[0] is not string name || !string.Equals(name, key, StringComparison.Ordinal)) continue;
            var old = pair[1];
            var same = ReferenceEquals(old, value) ||
                (old is string a && value is string b && string.Equals(a, b, StringComparison.Ordinal)) ||
                (old is bool x && value is bool y && x == y) ||
                (old is double p && value is double q && BitConverter.DoubleToInt64Bits(p) == BitConverter.DoubleToInt64Bits(q));
            if (!same && R.TryItems(old, out _) && R.TryItems(value, out _))
                same = ReferenceEquals(R.snapshot(old), R.snapshot(value));
            return same ? record : F_dv_with(record, key, value);
        }
        return F_dv_with(record, key, value);
    }
    public static object F_dvs_simulation_open(object v_story, object v_world)
    {
        object v_config__383476 = F_dv_find(v_story, "simulation", new System.Collections.Generic.List<object>(0) { });
        object v_identities__383526 = F_dv_find(v_config__383476, "identities", new System.Collections.Generic.List<object>(0) { });
        object v_occupied__383581 = R.number("0");
        object v_names__383602 = new System.Collections.Generic.List<object>(0) { };
        object v_i__383621 = R.number("0");
        while (R.Bool(R.lt(v_i__383621, R.count(v_identities__383526))))
        {
            object v_identity__383675 = R.at(v_identities__383526, v_i__383621);
            var protection = F_dv_find(v_identity__383675, "protected", false);
            _ = F_dv_require(protection is bool, "Identity protection must be a boolean");
            object v_bit__383712 = F_dv_mask_bit(F_dv_get(v_identity__383675, "index"));
            _ = F_dv_require((R.Bool(R.not(F_dv_mask_has(v_occupied__383581, F_dv_get(v_identity__383675, "index")))) ? (object)(R.not(F_dv_has(v_names__383602, F_dv_get(v_identity__383675, "actor")))) : (object)(false)), "Simulation identity index or actor duplicated");
            v_occupied__383581 = R.add(v_occupied__383581, v_bit__383712);
            v_names__383602 = F_dv_append(v_names__383602, F_dv_get(v_identity__383675, "actor"));
            v_i__383621 = R.add(v_i__383621, R.number("1"));
        }
        object v_time__384004 = F_dv_find(v_world, "time", R.number("0"));
        return F_dv_with(v_world, "simulation", new System.Collections.Generic.List<object>(12) { F_dv_pair("version", "approximate-v1"), F_dv_pair("approximation", true), F_dv_pair("config", v_config__383476), F_dv_pair("last_macro", R.mul(R.floor(R.div(v_time__384004, R.number("15"))), R.number("15"))), F_dv_pair("last_nearby_second", R.sub(R.number("0"), R.number("1"))), F_dv_pair("visible_rooms", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("nearby_rooms", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("RealityFacts", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("DemonEntityLock", false), F_dv_pair("macro_count", R.number("0")), F_dv_pair("nearby_count", R.number("0")), F_dv_pair("daily_count", R.number("0")) });
    }

    public static object F_dvs_simulation_fact(object facts, object id)
    {
        var index = F_dv_index_by_id(facts, id);
        return R.Num(index) < 0 ? false : R.at(facts, index);
    }

    public static object F_dvs_simulation_facts(object v_world, object v_actors, object v_bindings)
    {
        // This projection reads all inputs and returns a new world record.
        // Borrow frozen branches rather than acquiring mutable copies per cell.
        v_world = R.snapshot(v_world);
        v_actors = R.snapshot(v_actors);
        v_bindings = R.snapshot(v_bindings);
        object v_simulation__384764 = F_dv_get(v_world, "simulation");
        object v_config__384802 = F_dv_get(v_simulation__384764, "config");
        object v_declarations__384837 = F_dv_find(v_config__384802, "facts", new System.Collections.Generic.List<object>(0) { });
        object v_identities__384889 = F_dv_find(v_config__384802, "identities", new System.Collections.Generic.List<object>(0) { });
        object v_facts__384944 = new System.Collections.Generic.List<object>(0) { };
        // The actor list is fixed throughout this projection. Resolve each
        // declared observer lazily once, retaining the original first-match
        // lookup and its validation order for the first declaration.
        var observerIndices = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        object v_i__384963 = R.number("0");
        while (R.Bool(R.lt(v_i__384963, R.count(v_declarations__384837))))
        {
            object v_declaration__385019 = R.at(v_declarations__384837, v_i__384963);
            object v_reality__385061 = F_dv_read_query(v_world, F_dv_get(v_declaration__385019, "ref"));
            var query = SimulationQuery(v_bindings, R.snapshot(v_declaration__385019));
            var values = query.RealityValues.GetValue(v_reality__385061, static _ => new());
            object v_known__385115 = R.number("0");
            object v_j__385137 = R.number("0");
            while (R.Bool(R.lt(v_j__385137, R.count(v_identities__384889))))
            {
                object v_identity__385199 = R.at(v_identities__384889, v_j__385137);
                if (!observerIndices.TryGetValue(v_identity__385199, out var v_index__385240))
                {
                    v_index__385240 = F_dv_index_by_id(v_actors, F_dv_get(v_identity__385199, "actor"));
                    observerIndices[v_identity__385199] = v_index__385240;
                }
                if (R.Bool(R.not(R.lt(v_index__385240, R.number("0")))))
                {
                    object scoped = SimulationQueryReference(query, v_bindings, v_declaration__385019, v_identity__385199);
                    if (scoped is not false)
                    {
                        object v_cell__385469 = F_dv_read_query(F_dv_get(R.at(v_actors, v_index__385240), "mind"), scoped);
                        object v_projected__385612 = values.Observers.GetValue(v_identity__385199, observer =>
                            new SimulationQueryValue(R.snapshot(F_dv_project_value(F_dv_get(v_reality__385061, "value"),
                                v_bindings, F_dv_get(observer, "actor"))))).Value;
                        if (R.Bool((R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_reality__385061)) ? (object)(F_dv_uncontested(v_cell__385469)) : (object)(false))) ? (object)(F_dv_get(v_projected__385612, "known")) : (object)(false))) ? (object)(F_dv_same(F_dv_get(v_projected__385612, "value"), F_dv_get(v_cell__385469, "value"))) : (object)(false))))
                        {
                            v_known__385115 = R.add(v_known__385115, F_dv_mask_bit(F_dv_get(v_identity__385199, "index")));
                        }
                    }
                }
                v_j__385137 = R.add(v_j__385137, R.number("1"));
            }
            _ = R.push(v_facts__384944, F_dv_reality_fact(F_dv_get(v_declaration__385019, "id"), F_dv_get(v_declaration__385019, "ref"), v_reality__385061, v_known__385115));
            v_i__384963 = R.add(v_i__384963, R.number("1"));
        }
        return SimulationSet(v_world, "simulation", SimulationSet(v_simulation__384764, "RealityFacts", v_facts__384944));
    }

    public static object F_dvs_simulation_danger(object v_config, object v_id)
    {
        object v_dangers__386144 = F_dv_find(v_config, "dangers", new System.Collections.Generic.List<object>(0) { });
        object v_i__386193 = R.number("0");
        while (R.Bool(R.lt(v_i__386193, R.count(v_dangers__386144))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_dangers__386144, v_i__386193), "id"), v_id)))
            {
                return R.at(v_dangers__386144, v_i__386193);
            }
            v_i__386193 = R.add(v_i__386193, R.number("1"));
        }
        return false;
    }

    public static object F_dvs_simulation_protected_mask(object config)
    {
        object mask = R.number("0");
        foreach (var identity in R.Items(F_dv_find(config, "identities", new List<object>())))
            if (R.Bool(F_dv_find(identity, "protected", false)))
                mask = R.add(mask, F_dv_mask_bit(F_dv_get(identity, "index")));
        return F_dv_mask32(mask);
    }

    public static object F_dvs_simulation_lock(object v_world, object v_events)
    {
        object v_simulation__386377 = F_dv_get(v_world, "simulation");
        object v_config__386415 = F_dv_get(v_simulation__386377, "config");
        object v_demon__386450 = F_dv_find(v_config__386415, "demon", false);
        if (R.Bool(R.eq(v_demon__386450, false)))
        {
            return v_world;
        }
        object v_lock__386542 = F_dv_get(v_simulation__386377, "DemonEntityLock");
        if (R.Bool(R.not(R.eq(v_lock__386542, false))))
        {
            object v_danger__386610 = F_dvs_simulation_danger(v_config__386415, F_dv_get(v_lock__386542, "danger"));
            if (R.Bool((R.Bool((R.Bool(R.eq(v_danger__386610, false)) ? (object)(true) : (object)(R.eq(F_dv_truth(v_world, F_dv_get(v_danger__386610, "when")), "false")))) ? (object)(true) : (object)(R.eq(F_dv_truth(v_world, F_dv_find(v_demon__386450, "release_when", F_dv_not(F_dv_true()))), "true")))))
            {
                _ = F_dvs_event(v_events, "simulation_demon_released", F_dv_get(v_demon__386450, "actor"), F_dv_get(v_world, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(2) { F_dv_pair("lock", v_lock__386542), F_dv_pair("approximation", true) });
                v_lock__386542 = false;
            }
        }
        if (R.Bool(R.eq(v_lock__386542, false)))
        {
            object v_dangers__387141 = F_dv_find(v_config__386415, "dangers", new System.Collections.Generic.List<object>(0) { });
            object v_i__387194 = R.number("0");
            while (R.Bool((R.Bool(R.lt(v_i__387194, R.count(v_dangers__387141))) ? (object)(R.eq(v_lock__386542, false)) : (object)(false))))
            {
                object v_danger__387271 = R.at(v_dangers__387141, v_i__387194);
                object v_fact__387307 = F_dvs_simulation_fact(F_dv_get(v_simulation__386377, "RealityFacts"), F_dv_get(v_danger__387271, "fact_id"));
                if (R.Bool((R.Bool(R.not(R.eq(v_fact__387307, false))) ? (object)(R.eq(F_dv_truth(v_world, F_dv_get(v_danger__387271, "when")), "true")) : (object)(false))))
                {
                    object v_victims__387468 = F_dv_mask_without(F_dv_mask_and(F_dv_get(v_danger__387271, "affected_mask"), F_dvs_simulation_protected_mask(v_config__386415)), F_dv_get(v_fact__387307, "KnownByMask"));
                    if (R.Bool(R.not(R.eq(v_victims__387468, R.number("0")))))
                    {
                        v_lock__386542 = new System.Collections.Generic.List<object>(6) { F_dv_pair("actor", F_dv_get(v_demon__386450, "actor")), F_dv_pair("danger", F_dv_get(v_danger__387271, "id")), F_dv_pair("room", F_dv_get(v_danger__387271, "room")), F_dv_pair("victims", v_victims__387468), F_dv_pair("acquired_at", F_dv_get(v_world, "time")), F_dv_pair("protecting", false) };
                        _ = F_dvs_event(v_events, "simulation_demon_reserved", F_dv_get(v_demon__386450, "actor"), F_dv_get(v_world, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(3) { F_dv_pair("lock", v_lock__386542), F_dv_pair("approximation", true), F_dv_pair("movement", false) });
                    }
                }
                v_i__387194 = R.add(v_i__387194, R.number("1"));
            }
        }
        return F_dv_with(v_world, "simulation", F_dv_with(v_simulation__386377, "DemonEntityLock", v_lock__386542));
    }

    public static object F_dvs_simulation_sync(object v_state)
    {
        v_state = R.snapshot(v_state);
        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation"))))
        {
            return v_state;
        }
        object v_actors__388123 = F_dv_get(v_state, "actors");
        object v_simulation__388153 = F_dv_get(F_dv_get(v_state, "physical"), "simulation");
        object v_latest_config__388200 = F_dv_find(F_dv_get(v_state, "story"), "simulation", F_dv_get(v_simulation__388153, "config"));
        var previousSimulation = v_simulation__388153;
        v_simulation__388153 = SimulationSet(v_simulation__388153, "config", v_latest_config__388200);
        if (!ReferenceEquals(previousSimulation, v_simulation__388153))
            v_state = F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("physical", SimulationSet(F_dv_get(v_state, "physical"), "simulation", v_simulation__388153)) });
        object v_filters__388447 = F_dv_find(F_dv_get(v_simulation__388153, "config"), "filters", new System.Collections.Generic.List<object>(0) { });
        object v_i__388507 = R.number("0");
        while (R.Bool(R.lt(v_i__388507, R.count(v_actors__388123))))
        {
            v_actors__388123 = R.snapshot(v_actors__388123);
            object v_actor__388557 = R.at(v_actors__388123, v_i__388507);
            var previousActor = v_actor__388557;
            object v_j__388587 = R.number("0");
            while (R.Bool(R.lt(v_j__388587, R.count(v_filters__388447))))
            {
                if (R.Bool(R.eq(F_dv_get(R.at(v_filters__388447, v_j__388587), "actor"), F_dv_get(v_actor__388557, "id"))))
                {
                    v_actor__388557 = SimulationSet(v_actor__388557, "mind", SimulationSet(F_dv_get(v_actor__388557, "mind"), "CognitionFilter", R.at(v_filters__388447, v_j__388587)));
                }
                v_j__388587 = R.add(v_j__388587, R.number("1"));
            }
            v_actor__388557 = SimulationSet(v_actor__388557, "simulation_priority_action", "");
            object v_schedules__388873 = new System.Collections.Generic.List<object>(0) { };
            if (R.Bool(R.not(F_dvs_simulation_onstage(v_actor__388557, v_state))))
            {
                object v_declarations__388957 = F_dv_find(F_dv_get(v_simulation__388153, "config"), "offscreen_schedules", new System.Collections.Generic.List<object>(0) { });
                v_j__388587 = R.number("0");
                while (R.Bool(R.lt(v_j__388587, R.count(v_declarations__388957))))
                {
                    if (R.Bool(R.eq(F_dv_get(R.at(v_declarations__388957, v_j__388587), "actor"), F_dv_get(v_actor__388557, "id"))))
                    {
                        _ = R.push(v_schedules__388873, R.at(v_declarations__388957, v_j__388587));
                    }
                    v_j__388587 = R.add(v_j__388587, R.number("1"));
                }
            }
            v_actor__388557 = SimulationSet(v_actor__388557, "policy", SimulationSet(F_dv_get(v_actor__388557, "policy"), "offscreen_schedules", v_schedules__388873));
            if (R.Bool(R.not(R.eq(F_dv_find(v_actor__388557, "simulation_decision_macro", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_simulation__388153, "macro_count")))))
            {
                v_actor__388557 = SimulationSet(v_actor__388557, "idle_guard", false);
            }
            if (R.Bool((R.Bool(R.lt(F_dv_find(v_actor__388557, "simulation_decision_nearby", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_simulation__388153, "last_nearby_second"))) ? (object)(F_dv_has_key(v_actor__388557, "location_ref")) : (object)(false))))
            {
                object v_position__389613 = F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(v_actor__388557, "location_ref"));
                if (R.Bool((R.Bool(F_dv_uncontested(v_position__389613)) ? (object)(F_dv_has(F_dv_get(v_simulation__388153, "nearby_rooms"), F_dvs_simulation_room(F_dv_get(v_simulation__388153, "config"), F_dv_get(v_position__389613, "value")))) : (object)(false))))
                {
                    v_actor__388557 = F_dv_with_many(v_actor__388557, new System.Collections.Generic.List<object>(2) { F_dv_pair("idle_guard", false), F_dv_pair("decision_time", R.sub(R.number("0"), R.number("1"))) });
                }
            }
            if (!ReferenceEquals(R.snapshot(v_actor__388557), previousActor))
                v_actors__388123 = F_dvs_replace(v_actors__388123, v_i__388507, v_actor__388557);
            v_i__388507 = R.add(v_i__388507, R.number("1"));
        }
        object v_events__389962 = F_dv_clone(F_dv_get(v_state, "events"));
        object v_world__390002 = F_dvs_simulation_lock(F_dvs_simulation_facts(F_dv_get(v_state, "physical"), v_actors__388123, F_dv_get(v_state, "bindings")), v_events__389962);
        object v_lock__390108 = F_dv_get(F_dv_get(v_world__390002, "simulation"), "DemonEntityLock");
        object v_declared_demon__390156 = F_dv_find(F_dv_get(F_dv_get(v_world__390002, "simulation"), "config"), "demon", false);
        if (R.Bool(R.not(R.eq(v_declared_demon__390156, false))))
        {
            object v_owner_index__390266 = F_dv_index_by_id(v_actors__388123, F_dv_get(v_declared_demon__390156, "actor"));
            if (R.Bool(R.not(R.lt(v_owner_index__390266, R.number("0")))))
            {
                v_actors__388123 = F_dvs_replace(v_actors__388123, v_owner_index__390266, F_dv_with(R.at(v_actors__388123, v_owner_index__390266), "simulation_entity_lock", v_lock__390108));
            }
        }
        if (R.Bool(R.not(R.eq(v_lock__390108, false))))
        {
            object v_demon__390516 = F_dv_get(F_dv_get(F_dv_get(v_world__390002, "simulation"), "config"), "demon");
            object v_index__390566 = F_dv_index_by_id(v_actors__388123, F_dv_get(v_demon__390516, "actor"));
            if (R.Bool(R.not(R.lt(v_index__390566, R.number("0")))))
            {
                object v_location__390661 = F_dv_read(v_world__390002, F_dv_get(v_demon__390516, "location_ref"));
                object v_requested__390723 = "";
                if (R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_location__390661)) ? (object)(R.eq(F_dv_get(v_location__390661, "value"), F_dv_get(v_lock__390108, "room"))) : (object)(false))) ? (object)(R.eq(F_dv_truth(v_world__390002, F_dv_find(v_demon__390516, "presence_when", F_dv_true())), "true")) : (object)(false))))
                {
                    if (R.Bool(R.not(F_dv_get(v_lock__390108, "protecting"))))
                    {
                        v_requested__390723 = F_dv_get(v_demon__390516, "protect_action_id");
                    }
                }
                else
                {
                    object v_routes__391019 = F_dv_find(v_demon__390516, "travel_actions", new System.Collections.Generic.List<object>(0) { });
                    v_i__388507 = R.number("0");
                    while (R.Bool(R.lt(v_i__388507, R.count(v_routes__391019))))
                    {
                        if (R.Bool(R.eq(F_dv_get(R.at(v_routes__391019, v_i__388507), "room"), F_dv_get(v_lock__390108, "room"))))
                        {
                            v_requested__390723 = F_dv_get(R.at(v_routes__391019, v_i__388507), "action_id");
                        }
                        v_i__388507 = R.add(v_i__388507, R.number("1"));
                    }
                }
                v_actors__388123 = F_dvs_replace(v_actors__388123, v_index__390566, F_dv_with(R.at(v_actors__388123, v_index__390566), "simulation_priority_action", v_requested__390723));
            }
        }
        return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(3) { F_dv_pair("actors", v_actors__388123), F_dv_pair("physical", v_world__390002), F_dv_pair("events", v_events__389962) });
    }

    public static object F_dvs_simulation_next_time(object v_world, object v_due)
    {
        if (R.Bool(F_dv_has_key(v_world, "simulation")))
        {
            return F_dv_min(v_due, R.add(F_dv_get(F_dv_get(v_world, "simulation"), "last_macro"), R.number("15")));
        }
        return v_due;
    }

    public static object F_dvs_simulation_room(object v_config, object v_location)
    {
        object v_locations__391689 = F_dv_find(v_config, "locations", new System.Collections.Generic.List<object>(0) { });
        object v_i__391742 = R.number("0");
        while (R.Bool(R.lt(v_i__391742, R.count(v_locations__391689))))
        {
            if (R.Bool(R.eq(F_dv_get(R.at(v_locations__391689, v_i__391742), "location"), v_location)))
            {
                return F_dv_get(R.at(v_locations__391689, v_i__391742), "room");
            }
            v_i__391742 = R.add(v_i__391742, R.number("1"));
        }
        return v_location;
    }

    public static object F_dvs_simulation_onstage(object v_actor, object v_state)
    {
        if (R.Bool(R.eq(F_dv_get(v_actor, "id"), F_dv_find(v_state, "player_actor", ""))))
        {
            return true;
        }
        if (R.Bool(R.not(F_dv_has_key(v_actor, "location_ref"))))
        {
            return false;
        }
        object v_position__392096 = F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(v_actor, "location_ref"));
        if (R.Bool(R.not(F_dv_uncontested(v_position__392096))))
        {
            return false;
        }
        if (R.Bool(F_dv_has(F_dv_get(F_dv_get(F_dv_get(v_state, "physical"), "simulation"), "visible_rooms"), F_dvs_simulation_room(F_dv_get(F_dv_get(F_dv_get(v_state, "physical"), "simulation"), "config"), F_dv_get(v_position__392096, "value")))))
        {
            return true;
        }
        object v_viewer__392364 = F_dv_find(F_dv_get(v_state, "story"), "host_viewer", F_dv_find(v_state, "player_actor", ""));
        object v_index__392453 = F_dv_index_by_id(F_dv_get(v_state, "actors"), v_viewer__392364);
        if (R.Bool((R.Bool(R.not(R.lt(v_index__392453, R.number("0")))) ? (object)(F_dv_has_key(R.at(F_dv_get(v_state, "actors"), v_index__392453), "location_ref")) : (object)(false))))
        {
            object v_viewing__392593 = F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(R.at(F_dv_get(v_state, "actors"), v_index__392453), "location_ref"));
            return (R.Bool(F_dv_uncontested(v_viewing__392593)) ? (object)(R.eq(F_dv_get(v_viewing__392593, "value"), F_dv_get(v_position__392096, "value"))) : (object)(false));
        }
        return false;
    }

    public static object F_dvs_simulation_actor_ready(object v_actor, object v_state)
    {
        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation"))))
        {
            return true;
        }
        if (R.Bool((R.Bool(F_dvs_simulation_onstage(v_actor, v_state)) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor, "simulation_priority_action", ""), ""))))))
        {
            return true;
        }
        if (R.Bool((R.Bool(R.lt(R.number("0"), R.count(F_dv_find(v_actor, "pending_heard", new System.Collections.Generic.List<object>(0) { })))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor, "decision_revision", R.sub(R.number("0"), R.number("1"))), F_dv_get(F_dv_get(v_actor, "mind"), "revision")))))))
        {
            return true;
        }
        object v_simulation__393308 = F_dv_get(F_dv_get(v_state, "physical"), "simulation");
        if (R.Bool(R.not(R.eq(F_dv_find(v_actor, "simulation_decision_macro", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_simulation__393308, "macro_count")))))
        {
            return true;
        }
        if (R.Bool(F_dv_has_key(v_actor, "location_ref")))
        {
            object v_position__393503 = F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(v_actor, "location_ref"));
            if (R.Bool((R.Bool(F_dv_uncontested(v_position__393503)) ? (object)(F_dv_has(F_dv_get(v_simulation__393308, "nearby_rooms"), F_dvs_simulation_room(F_dv_get(v_simulation__393308, "config"), F_dv_get(v_position__393503, "value")))) : (object)(false))))
            {
                return R.lt(F_dv_find(v_actor, "simulation_decision_nearby", R.sub(R.number("0"), R.number("1"))), F_dv_get(v_simulation__393308, "last_nearby_second"));
            }
        }
        return false;
    }

    public static object F_dvs_simulation_protect_complete(object v_state, object v_pending)
    {
        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation"))))
        {
            return v_state;
        }
        object v_world__416662 = F_dv_get(v_state, "physical");
        object v_lock__416693 = F_dv_get(F_dv_get(v_world__416662, "simulation"), "DemonEntityLock");
        object v_demon__416741 = F_dv_find(F_dv_get(F_dv_get(v_world__416662, "simulation"), "config"), "demon", false);
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.eq(v_lock__416693, false)) ? (object)(true) : (object)(R.eq(v_demon__416741, false)))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_pending, "id"), F_dv_get(v_demon__416741, "actor")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(F_dv_get(v_pending, "action"), "id"), F_dv_get(v_demon__416741, "protect_action_id")))))))
        {
            return v_state;
        }
        object v_location__416944 = F_dv_read(v_world__416662, F_dv_get(v_demon__416741, "location_ref"));
        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_location__416944))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_location__416944, "value"), F_dv_get(v_lock__416693, "room")))))))
        {
            return v_state;
        }
        object v_danger__417087 = F_dvs_simulation_danger(F_dv_get(F_dv_get(v_world__416662, "simulation"), "config"), F_dv_get(v_lock__416693, "danger"));
        if (R.Bool((R.Bool(R.eq(v_danger__417087, false)) ? (object)(true) : (object)(R.not(R.eq(F_dv_truth(v_world__416662, F_dv_get(v_danger__417087, "when")), "true"))))))
        {
            return v_state;
        }
        object v_fact__417251 = F_dvs_simulation_fact(F_dv_get(F_dv_get(v_world__416662, "simulation"), "RealityFacts"), F_dv_get(v_danger__417087, "fact_id"));
        if (R.Bool(R.eq(v_fact__417251, false)))
        {
            return v_state;
        }
        object v_actors__417376 = F_dv_get(v_state, "actors");
        object v_events__417406 = F_dv_clone(F_dv_get(v_state, "events"));
        object v_event__417446 = F_dvs_event(v_events__417406, "simulation_demon_protection", F_dv_get(v_demon__416741, "actor"), F_dv_get(v_world__416662, "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(4) { F_dv_pair("danger", F_dv_get(v_danger__417087, "id")), F_dv_pair("room", F_dv_get(v_lock__416693, "room")), F_dv_pair("action_event", F_dv_get(v_pending, "action_event")), F_dv_pair("approximation", true) });
        object v_i__417643 = R.number("0");
        while (R.Bool(R.lt(v_i__417643, R.count(v_actors__417376))))
        {
            object v_actor__417693 = R.at(v_actors__417376, v_i__417643);
            if (R.Bool(F_dv_has_key(v_actor__417693, "location_ref")))
            {
                object v_position__417773 = F_dv_read(v_world__416662, F_dv_get(v_actor__417693, "location_ref"));
                if (R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_position__417773)) ? (object)(R.eq(F_dv_get(v_position__417773, "value"), F_dv_get(v_lock__416693, "room"))) : (object)(false))) ? (object)(F_dvs_auditory_access(v_actor__417693, v_world__416662)) : (object)(false))))
                {
                    object v_observations__417950 = F_dv_cognition_observations(F_dv_get(v_actor__417693, "mind"), F_dv_perceive(v_world__416662, F_dv_get(v_actor__417693, "mind"), F_dv_get(v_state, "bindings"), new System.Collections.Generic.List<object>(1) { F_dv_get(v_fact__417251, "ref") }, v_event__417446, F_dv_get(v_world__416662, "time")));
                    v_actor__417693 = F_dv_with(v_actor__417693, "mind", F_dv_get(F_dv_update_mind(F_dv_get(v_actor__417693, "mind"), v_observations__417950, F_dv_get(F_dv_get(v_actor__417693, "mind"), "revision"), F_dvs_observation_profile(v_actor__417693)), "world"));
                    if (R.Bool(R.lt(R.number("0"), R.count(v_observations__417950))))
                    {
                        _ = F_dvs_event(v_events__417406, "simulation_public_warning", F_dv_get(v_actor__417693, "id"), F_dv_get(v_world__416662, "time"), new System.Collections.Generic.List<object>(1) { v_event__417446 }, new System.Collections.Generic.List<object>(3) { F_dv_pair("observations", v_observations__417950), F_dv_pair("danger", F_dv_get(v_danger__417087, "id")), F_dv_pair("source", F_dv_get(v_demon__416741, "actor")) });
                    }
                    v_actors__417376 = F_dvs_replace(v_actors__417376, v_i__417643, v_actor__417693);
                }
            }
            v_i__417643 = R.add(v_i__417643, R.number("1"));
        }
        v_world__416662 = F_dv_with(v_world__416662, "simulation", F_dv_with(F_dv_get(v_world__416662, "simulation"), "DemonEntityLock", F_dv_with(v_lock__416693, "protecting", true)));
        return F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(3) { F_dv_pair("actors", v_actors__417376), F_dv_pair("physical", v_world__416662), F_dv_pair("events", v_events__417406) });
    }

    public static object F_dvs_simulation_pulse(object v_state, object v_seconds, object v_visible_rooms)
    {
        _ = F_dv_require(R.not(R.lt(v_seconds, R.number("0"))), "Negative host simulation clock");
        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation"))))
        {
            return v_state;
        }
        object v_simulation__419108 = F_dv_get(F_dv_get(v_state, "physical"), "simulation");
        object v_tick__419155 = R.floor(v_seconds);
        _ = F_dv_require(R.not(R.lt(v_tick__419155, F_dv_get(v_simulation__419108, "last_nearby_second"))), "Host simulation clock moved backwards");
        if (R.Bool(R.eq(F_dv_get(v_simulation__419108, "last_nearby_second"), v_tick__419155)))
        {
            return v_state;
        }
        object v_mapped__419350 = new System.Collections.Generic.List<object>(0) { };
        object v_v__419370 = R.number("0");
        while (R.Bool(R.lt(v_v__419370, R.count(v_visible_rooms))))
        {
            object v_room__419427 = F_dvs_simulation_room(F_dv_get(v_simulation__419108, "config"), R.at(v_visible_rooms, v_v__419370));
            if (R.Bool(R.not(F_dv_has(v_mapped__419350, v_room__419427))))
            {
                _ = R.push(v_mapped__419350, v_room__419427);
            }
            v_v__419370 = R.add(v_v__419370, R.number("1"));
        }
        v_visible_rooms = v_mapped__419350;
        object v_nearby__419615 = v_visible_rooms;
        object v_edges__419646 = F_dv_find(F_dv_get(v_simulation__419108, "config"), "edges", new System.Collections.Generic.List<object>(0) { });
        object v_i__419702 = R.number("0");
        while (R.Bool(R.lt(v_i__419702, R.count(v_edges__419646))))
        {
            object v_edge__419751 = R.at(v_edges__419646, v_i__419702);
            if (R.Bool((R.Bool(F_dv_has(v_visible_rooms, F_dv_get(v_edge__419751, "from"))) ? (object)(R.not(F_dv_has(v_nearby__419615, F_dv_get(v_edge__419751, "to")))) : (object)(false))))
            {
                v_nearby__419615 = F_dv_append(v_nearby__419615, F_dv_get(v_edge__419751, "to"));
            }
            if (R.Bool((R.Bool(F_dv_has(v_visible_rooms, F_dv_get(v_edge__419751, "to"))) ? (object)(R.not(F_dv_has(v_nearby__419615, F_dv_get(v_edge__419751, "from")))) : (object)(false))))
            {
                v_nearby__419615 = F_dv_append(v_nearby__419615, F_dv_get(v_edge__419751, "from"));
            }
            v_i__419702 = R.add(v_i__419702, R.number("1"));
        }
        object v_changed__420045 = (R.Bool(R.not(F_dv_same(F_dv_get(v_simulation__419108, "visible_rooms"), v_visible_rooms))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_simulation__419108, "nearby_rooms"), v_nearby__419615))));
        v_simulation__419108 = F_dv_with_many(v_simulation__419108, new System.Collections.Generic.List<object>(4) { F_dv_pair("last_nearby_second", v_tick__419155), F_dv_pair("visible_rooms", v_visible_rooms), F_dv_pair("nearby_rooms", v_nearby__419615), F_dv_pair("nearby_count", R.add(F_dv_get(v_simulation__419108, "nearby_count"), R.number("1"))) });
        object v_events__420337 = F_dv_clone(F_dv_get(v_state, "events"));
        if (R.Bool(v_changed__420045))
        {
            _ = F_dvs_event(v_events__420337, "simulation_nearby_scope", "", F_dv_get(F_dv_get(v_state, "physical"), "time"), new System.Collections.Generic.List<object>(0) { }, new System.Collections.Generic.List<object>(3) { F_dv_pair("host_second", v_tick__419155), F_dv_pair("rooms", v_nearby__419615), F_dv_pair("approximation", true) });
        }
        return F_dvs_simulation_sync(F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(3) { F_dv_pair("physical", F_dv_with(F_dv_get(v_state, "physical"), "simulation", v_simulation__419108)), F_dv_pair("events", v_events__420337), F_dv_pair("idle", R.number("0")) }));
    }
}
