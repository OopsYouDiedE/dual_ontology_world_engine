using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>World / SimulationEnvironment: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_simulation_diffuse(object v_world, object v_config, object v_events, object v_parent, object v_time)
    {
        // These inputs are queried or functionally replaced. The event sink
        // deliberately remains the caller's mutable, ordered append root.
        v_world = R.snapshot(v_world);
        v_config = R.snapshot(v_config);
        object v_rooms__403934 = F_dv_find(v_config, "rooms", new System.Collections.Generic.List<object>(0) { });
        object v_edges__403979 = F_dv_find(v_config, "edges", new System.Collections.Generic.List<object>(0) { });
        object v_amounts__404024 = new System.Collections.Generic.List<object>(0) { };
        object v_concentrations__404045 = new System.Collections.Generic.List<object>(0) { };
        object v_outgoing__404073 = new System.Collections.Generic.List<object>(0) { };
        object v_boundaries__404095 = new System.Collections.Generic.List<object>(0) { };
        object v_i__404119 = R.number("0");
        while (R.Bool(R.lt(v_i__404119, R.count(v_rooms__403934))))
        {
            object v_room__404168 = R.at(v_rooms__403934, v_i__404119);
            object v_concentration__404196 = F_dv_read_query(v_world, F_dv_get(v_room__404168, "concentration_ref"));
            object v_volume__404263 = F_dv_find(v_room__404168, "volume", R.number("1"));
            _ = F_dv_require((R.Bool((R.Bool(F_dv_uncontested(v_concentration__404196)) ? (object)(R.not(R.lt(F_dv_get(v_concentration__404196, "value"), R.number("0")))) : (object)(false))) ? (object)(R.lt(R.number("0"), v_volume__404263)) : (object)(false)), "Diffusion requires known nonnegative concentration and positive volume");
            _ = F_dv_unit(F_dv_find(v_room__404168, "settling", R.number("0")));
            object v_boundary__404525 = R.sub(R.number("0"), R.number("1"));
            object v_conditions__404551 = F_dv_find(v_room__404168, "boundaries", new System.Collections.Generic.List<object>(0) { });
            object v_c__404608 = R.number("0");
            while (R.Bool(R.lt(v_c__404608, R.count(v_conditions__404551))))
            {
                if (R.Bool(R.eq(F_dv_truth(v_world, F_dv_get(R.at(v_conditions__404551, v_c__404608), "when")), "true")))
                {
                    v_boundary__404525 = F_dv_get(R.at(v_conditions__404551, v_c__404608), "value");
                }
                v_c__404608 = R.add(v_c__404608, R.number("1"));
            }
            object v_current__404797 = F_dv_get(v_concentration__404196, "value");
            if (R.Bool(R.not(R.lt(v_boundary__404525, R.number("0")))))
            {
                v_current__404797 = v_boundary__404525;
            }
            _ = R.push(v_boundaries__404095, v_boundary__404525);
            _ = R.push(v_amounts__404024, R.mul(v_current__404797, v_volume__404263));
            _ = R.push(v_concentrations__404045, v_current__404797);
            _ = R.push(v_outgoing__404073, R.number("0"));
            v_i__404119 = R.add(v_i__404119, R.number("1"));
        }
        object v_flows__405061 = new System.Collections.Generic.List<object>(0) { };
        v_i__404119 = R.number("0");
        while (R.Bool(R.lt(v_i__404119, R.count(v_edges__403979))))
        {
            object v_edge__405125 = R.at(v_edges__403979, v_i__404119);
            object v_a__405153 = F_dv_index_by_id(v_rooms__403934, F_dv_get(v_edge__405125, "from"));
            object v_b__405213 = F_dv_index_by_id(v_rooms__403934, F_dv_get(v_edge__405125, "to"));
            _ = F_dv_require((R.Bool((R.Bool(R.not(R.lt(v_a__405153, R.number("0")))) ? (object)(R.not(R.lt(v_b__405213, R.number("0")))) : (object)(false))) ? (object)(R.not(R.eq(v_a__405153, v_b__405213))) : (object)(false)), "Diffusion edge references invalid rooms");
            object v_opening__405363 = F_dv_find(v_edge__405125, "opening", R.number("0"));
            if (R.Bool(F_dv_has_key(v_edge__405125, "open_when")))
            {
                if (R.Bool(R.eq(F_dv_truth(v_world, F_dv_get(v_edge__405125, "open_when")), "true")))
                {
                    v_opening__405363 = F_dv_find(v_edge__405125, "open_value", R.number("1"));
                }
                else
                {
                    v_opening__405363 = F_dv_find(v_edge__405125, "closed_value", R.number("0"));
                }
            }
            if (R.Bool(F_dv_has_key(v_edge__405125, "opening_ref")))
            {
                object v_gate__405695 = F_dv_read_query(v_world, F_dv_get(v_edge__405125, "opening_ref"));
                _ = F_dv_require(F_dv_uncontested(v_gate__405695), "Diffusion opening is unknown");
                v_opening__405363 = F_dv_get(v_gate__405695, "value");
            }
            _ = F_dv_unit(v_opening__405363);
            object v_conductance__405882 = F_dv_find(v_edge__405125, "conductance", R.number("0"));
            object v_wind__405940 = F_dv_find(v_edge__405125, "wind", R.number("0"));
            if (R.Bool((R.Bool(F_dv_has_key(v_edge__405125, "wind_when")) ? (object)(R.not(R.eq(F_dv_truth(v_world, F_dv_get(v_edge__405125, "wind_when")), "true"))) : (object)(false))))
            {
                v_wind__405940 = R.number("0");
            }
            object v_downward__406093 = F_dv_find(v_edge__405125, "downward", R.number("0"));
            _ = F_dv_require((R.Bool((R.Bool((R.Bool(R.not(R.lt(v_conductance__405882, R.number("0")))) ? (object)(R.not(R.lt(v_wind__405940, R.sub(R.number("0"), R.number("1"))))) : (object)(false))) ? (object)(R.not(R.lt(R.number("1"), v_wind__405940))) : (object)(false))) ? (object)(R.not(R.lt(v_downward__406093, R.number("0")))) : (object)(false)), "Invalid diffusion transport coefficient");
            object v_height__406272 = R.sub(F_dv_get(R.at(v_rooms__403934, v_a__405153), "height"), F_dv_get(R.at(v_rooms__403934, v_b__405213), "height"));
            object v_down_a__406327 = R.mul(F_dv_max(R.number("0"), v_height__406272), v_downward__406093);
            object v_down_b__406377 = R.mul(F_dv_max(R.number("0"), R.sub(R.number("0"), v_height__406272)), v_downward__406093);
            object v_raw__406431 = R.mul(R.mul(v_opening__405363, v_conductance__405882), R.sub(R.add(R.sub(R.add(R.sub(R.at(v_concentrations__404045, v_a__405153), R.at(v_concentrations__404045, v_b__405213)), R.mul(F_dv_max(R.number("0"), v_wind__405940), R.at(v_concentrations__404045, v_a__405153))), R.mul(F_dv_max(R.number("0"), R.sub(R.number("0"), v_wind__405940)), R.at(v_concentrations__404045, v_b__405213))), R.mul(v_down_a__406327, R.at(v_concentrations__404045, v_a__405153))), R.mul(v_down_b__406377, R.at(v_concentrations__404045, v_b__405213))));
            object v_source__406653 = v_a__405153;
            object v_target__406676 = v_b__405213;
            object v_amount__406699 = v_raw__406431;
            if (R.Bool(R.lt(v_raw__406431, R.number("0"))))
            {
                v_source__406653 = v_b__405213;
                v_target__406676 = v_a__405153;
                v_amount__406699 = R.sub(R.number("0"), v_raw__406431);
            }
            v_outgoing__404073 = F_dvs_replace(v_outgoing__404073, v_source__406653, R.add(R.at(v_outgoing__404073, v_source__406653), v_amount__406699));
            _ = R.push(v_flows__405061, new System.Collections.Generic.List<object>(3) { F_dv_pair("source", v_source__406653), F_dv_pair("target", v_target__406676), F_dv_pair("amount", v_amount__406699) });
            v_i__404119 = R.add(v_i__404119, R.number("1"));
        }
        object v_next__407151 = F_dv_clone(v_amounts__404024);
        v_i__404119 = R.number("0");
        while (R.Bool(R.lt(v_i__404119, R.count(v_flows__405061))))
        {
            object v_flow__407229 = R.at(v_flows__405061, v_i__404119);
            object v_scale__407257 = R.number("1");
            if (R.Bool(R.lt(R.at(v_amounts__404024, F_dv_get(v_flow__407229, "source")), R.at(v_outgoing__404073, F_dv_get(v_flow__407229, "source")))))
            {
                v_scale__407257 = R.div(R.at(v_amounts__404024, F_dv_get(v_flow__407229, "source")), R.at(v_outgoing__404073, F_dv_get(v_flow__407229, "source")));
            }
            object v_amount__407401 = R.mul(F_dv_get(v_flow__407229, "amount"), v_scale__407257);
            v_next__407151 = F_dvs_replace(v_next__407151, F_dv_get(v_flow__407229, "source"), R.sub(R.at(v_next__407151, F_dv_get(v_flow__407229, "source")), v_amount__407401));
            v_next__407151 = F_dvs_replace(v_next__407151, F_dv_get(v_flow__407229, "target"), R.add(R.at(v_next__407151, F_dv_get(v_flow__407229, "target")), v_amount__407401));
            v_i__404119 = R.add(v_i__404119, R.number("1"));
        }
        object v_changes__407604 = new System.Collections.Generic.List<object>(0) { };
        object v_concentrations_changed__407625 = false;
        v_i__404119 = R.number("0");
        while (R.Bool(R.lt(v_i__404119, R.count(v_rooms__403934))))
        {
            object v_room__407709 = R.at(v_rooms__403934, v_i__404119);
            object v_settled__407737 = R.mul(F_dv_max(R.number("0"), R.at(v_next__407151, v_i__404119)), R.sub(R.number("1"), F_dv_find(v_room__407709, "settling", R.number("0"))));
            object v_value__407815 = R.div(v_settled__407737, F_dv_find(v_room__407709, "volume", R.number("1")));
            if (R.Bool(R.not(R.lt(R.at(v_boundaries__404095, v_i__404119), R.number("0")))))
            {
                v_value__407815 = R.at(v_boundaries__404095, v_i__404119);
            }
            object v_previous__407937 = F_dv_get(F_dv_read_query(v_world, F_dv_get(v_room__407709, "concentration_ref")), "value");
            object v_boundary_exchange__408005 = R.sub(R.add(R.mul(R.sub(R.at(v_concentrations__404045, v_i__404119), v_previous__407937), F_dv_find(v_room__407709, "volume", R.number("1"))), R.mul(v_value__407815, F_dv_find(v_room__407709, "volume", R.number("1")))), v_settled__407737);
            if (R.Bool(R.not(R.eq(v_value__407815, v_previous__407937))))
            {
                v_world = R.snapshot(F_dv_store(v_world, F_dv_get(v_room__407709, "concentration_ref"), F_dv_cell(v_value__407815, R.number("1"), v_time, new System.Collections.Generic.List<object>(1) { v_parent })));
                v_concentrations_changed__407625 = true;
            }
            if (R.Bool((R.Bool((R.Bool(R.not(R.eq(v_value__407815, v_previous__407937))) ? (object)(true) : (object)(R.not(R.eq(v_boundary_exchange__408005, R.number("0")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_max(R.number("0"), R.at(v_next__407151, v_i__404119)), v_settled__407737))))))
            {
                _ = R.push(v_changes__407604, new System.Collections.Generic.List<object>(5) { F_dv_pair("room", F_dv_get(v_room__407709, "id")), F_dv_pair("before", v_previous__407937), F_dv_pair("after", v_value__407815), F_dv_pair("boundary_exchange", v_boundary_exchange__408005), F_dv_pair("settled_mass", R.sub(F_dv_max(R.number("0"), R.at(v_next__407151, v_i__404119)), v_settled__407737)) });
            }
            v_i__404119 = R.add(v_i__404119, R.number("1"));
        }
        if (R.Bool(v_concentrations_changed__407625))
        {
            v_world = F_dv_with(v_world, "revision", R.add(F_dv_get(v_world, "revision"), R.number("1")));
        }
        _ = F_dvs_event(v_events, "simulation_macro", "", v_time, new System.Collections.Generic.List<object>(1) { v_parent }, new System.Collections.Generic.List<object>(5) { F_dv_pair("changes", v_changes__407604), F_dv_pair("period_minutes", R.number("15")), F_dv_pair("approximation", true), F_dv_pair("model", "synchronous-room-graph"), F_dv_pair("precision_certificate", false) });
        return v_world;
    }

    public static object F_dvs_simulation_calculated_rule(object v_world, object v_rule, object v_time)
    {
        v_world = R.snapshot(v_world);
        v_rule = R.snapshot(v_rule);
        object v_premise__409181 = new System.Collections.Generic.List<object>(1) { F_dv_find(v_rule, "when", F_dv_true()) };
        object v_effects__409234 = F_dv_clone(F_dv_find(v_rule, "effects", new System.Collections.Generic.List<object>(0) { }));
        object v_calculations__409291 = F_dv_find(v_rule, "calculations", new System.Collections.Generic.List<object>(0) { });
        object v_i__409348 = R.number("0");
        while (R.Bool(R.lt(v_i__409348, R.count(v_calculations__409291))))
        {
            object v_calculation__409404 = R.at(v_calculations__409291, v_i__409348);
            object v_value__409446 = F_dv_find(v_calculation__409404, "constant", R.number("0"));
            if (R.Bool(F_dv_find(v_calculation__409404, "clock", false)))
            {
                v_value__409446 = v_time;
            }
            if (R.Bool(F_dv_has_key(v_calculation__409404, "modulo")))
            {
                _ = F_dv_require(R.lt(R.number("0"), F_dv_get(v_calculation__409404, "modulo")), "Settlement modulus must be positive");
                v_value__409446 = R.sub(v_value__409446, R.mul(R.floor(R.div(v_value__409446, F_dv_get(v_calculation__409404, "modulo"))), F_dv_get(v_calculation__409404, "modulo")));
            }
            object v_terms__409794 = F_dv_find(v_calculation__409404, "terms", new System.Collections.Generic.List<object>(0) { });
            object v_j__409848 = R.number("0");
            while (R.Bool(R.lt(v_j__409848, R.count(v_terms__409794))))
            {
                object v_term__409905 = R.at(v_terms__409794, v_j__409848);
                object v_when__409937 = F_dv_find(v_term__409905, "when", F_dv_true());
                object v_truth__409993 = F_dv_truth(v_world, v_when__409937);
                if (R.Bool((R.Bool(R.not(R.eq(v_truth__409993, "true"))) ? (object)(R.not(R.eq(v_truth__409993, "false"))) : (object)(false))))
                {
                    return new System.Collections.Generic.List<object>(3) { F_dv_pair("ready", false), F_dv_pair("reason", "unknown-term-condition"), F_dv_pair("ref", F_dv_get(v_calculation__409404, "ref")) };
                }
                if (R.Bool(R.eq(v_truth__409993, "true")))
                {
                    _ = R.push(v_premise__409181, v_when__409937);
                    if (R.Bool(F_dv_has_key(v_term__409905, "ref")))
                    {
                        object v_cell__410315 = F_dv_read_query(v_world, F_dv_get(v_term__409905, "ref"));
                        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_cell__410315))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_cell__410315, "value")))))))
                        {
                            return new System.Collections.Generic.List<object>(3) { F_dv_pair("ready", false), F_dv_pair("reason", "unknown-numeric-input"), F_dv_pair("ref", F_dv_get(v_term__409905, "ref")) };
                        }
                        _ = R.push(v_premise__409181, F_dv_fact(F_dv_get(v_term__409905, "ref"), F_dv_get(v_cell__410315, "value")));
                        v_value__409446 = R.add(v_value__409446, R.mul(F_dv_get(v_cell__410315, "value"), F_dv_find(v_term__409905, "factor", R.number("1"))));
                    }
                    else
                    {
                        v_value__409446 = R.add(v_value__409446, F_dv_get(v_term__409905, "constant"));
                    }
                }
                else
                {
                    _ = R.push(v_premise__409181, F_dv_not(v_when__409937));
                }
                v_j__409848 = R.add(v_j__409848, R.number("1"));
            }
            if (R.Bool(F_dv_has_key(v_calculation__409404, "minimum")))
            {
                v_value__409446 = F_dv_max(F_dv_get(v_calculation__409404, "minimum"), v_value__409446);
            }
            if (R.Bool(F_dv_has_key(v_calculation__409404, "maximum")))
            {
                v_value__409446 = F_dv_min(F_dv_get(v_calculation__409404, "maximum"), v_value__409446);
            }
            _ = R.push(v_effects__409234, new System.Collections.Generic.List<object>(3) { F_dv_pair("op", F_dv_find(v_calculation__409404, "op", "assign")), F_dv_pair("ref", F_dv_get(v_calculation__409404, "ref")), F_dv_pair("value", v_value__409446) });
            v_i__409348 = R.add(v_i__409348, R.number("1"));
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("ready", true), F_dv_pair("law", F_dv_axiom(F_dv_get(v_rule, "law"), R.number("1"), F_dv_all(v_premise__409181), v_effects__409234)) };
    }

    public static object F_dvs_simulation_execute_rule(object v_world, object v_rule, object v_events, object v_parent, object v_time, object v_kind)
    {
        v_world = R.snapshot(v_world);
        v_rule = R.snapshot(v_rule);
        if (R.Bool(R.not(R.eq(F_dv_truth(v_world, F_dv_find(v_rule, "when", F_dv_true())), "true"))))
        {
            return v_world;
        }
        if (R.Bool(F_dv_has_key(v_rule, "calculations")))
        {
            object v_calculated__411470 = R.snapshot(F_dvs_simulation_calculated_rule(v_world, v_rule, v_time));
            if (R.Bool(R.not(F_dv_get(v_calculated__411470, "ready"))))
            {
                _ = F_dvs_event(v_events, v_kind, "", v_time, new System.Collections.Generic.List<object>(1) { v_parent }, new System.Collections.Generic.List<object>(4) { F_dv_pair("id", F_dv_get(v_rule, "id")), F_dv_pair("status", "unresolved-input"), F_dv_pair("detail", v_calculated__411470), F_dv_pair("approximation", true) });
                return v_world;
            }
            object v_basis__411751 = F_dv_find(v_rule, "basis", new System.Collections.Generic.List<object>(1) { v_parent });
            if (R.Bool(F_dv_has_key(F_dv_get(v_world, "adoptions"), F_dv_get(v_rule, "law"))))
            {
                object v_previous__411858 = F_dv_get(F_dv_get(v_world, "adoptions"), F_dv_get(v_rule, "law"));
                if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_previous__411858, "active"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_previous__411858, "mode"), "enforce"))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_previous__411858, "confidence"), R.number("1")))))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_previous__411858, "law"), F_dv_get(v_calculated__411470, "law")))))) ? (object)(true) : (object)(R.not(F_dv_same(F_dv_get(v_previous__411858, "basis"), v_basis__411751))))))
                {
                    v_world = F_dv_reconsider(v_world, F_dv_get(v_calculated__411470, "law"), "enforce", R.number("1"), v_basis__411751);
                }
            }
            else
            {
                v_world = F_dv_adopt(v_world, F_dv_get(v_calculated__411470, "law"), "enforce", R.number("1"), v_basis__411751);
            }
        }
        v_world = R.snapshot(v_world);
        object v_execution__412416 = R.snapshot(F_dv_execute(v_world, F_dv_get(v_rule, "law"), R.cat(v_kind, R.cat("-", R.show(R.count(v_events)))), v_time, F_dv_get(v_world, "revision")));
        _ = F_dvs_event(v_events, v_kind, "", v_time, new System.Collections.Generic.List<object>(1) { v_parent }, new System.Collections.Generic.List<object>(4) { F_dv_pair("id", F_dv_get(v_rule, "id")), F_dv_pair("category", F_dv_find(v_rule, "category", "")), F_dv_pair("execution", F_dv_without(v_execution__412416, "world")), F_dv_pair("approximation", true) });
        return F_dv_get(v_execution__412416, "world");
    }

    public static object F_dvs_simulation_daily(object v_world, object v_config, object v_events, object v_parent, object v_time)
    {
        v_world = R.snapshot(v_world);
        v_config = R.snapshot(v_config);
        object v_rules__412806 = F_dv_find(v_config, "daily_rules", new System.Collections.Generic.List<object>(0) { });
        object v_daytime__412857 = R.sub(v_time, R.mul(R.floor(R.div(v_time, R.number("1440"))), R.number("1440")));
        object v_i__412911 = R.number("0");
        while (R.Bool(R.lt(v_i__412911, R.count(v_rules__412806))))
        {
            object v_rule__412960 = R.at(v_rules__412806, v_i__412911);
            _ = F_dv_require((R.Bool(R.eq(F_dv_get(v_rule__412960, "at"), R.number("360"))) ? (object)(true) : (object)(R.eq(F_dv_get(v_rule__412960, "at"), R.number("1080")))), "Daily rules must use 06:00 or 18:00");
            if (R.Bool(R.eq(F_dv_get(v_rule__412960, "at"), v_daytime__412857)))
            {
                v_world = F_dvs_simulation_execute_rule(v_world, v_rule__412960, v_events, v_parent, v_time, "simulation_daily");
                v_world = R.snapshot(F_dv_with(v_world, "simulation", F_dv_with(F_dv_get(v_world, "simulation"), "daily_count", R.add(F_dv_get(F_dv_get(v_world, "simulation"), "daily_count"), R.number("1")))));
            }
            v_i__412911 = R.add(v_i__412911, R.number("1"));
        }
        return v_world;
    }

    public static object F_dvs_simulation_unmanaged_processes(object v_world, object v_processes)
    {
        // Only frozen inputs may reuse this content-only projection. Mutable
        // callers and disabled declaration caches retain the original path.
        if (v_world is List<object> || v_processes is List<object>
            || !R.IsDvList(v_world) || !R.IsDvList(v_processes)
            || !R.Bool(R.memo_group_enabled("declaration")))
            return SimulationUnmanagedProcesses(v_world, v_processes, out _);
        if (!R.Bool(F_dv_has_key(v_world, "simulation")))
            return SimulationUnmanagedProcesses(v_world, v_processes, out _);

        // The complete configuration contains both daily_rules and macro_rules.
        // Reading it preserves the original access order; a hit is possible only
        // after these exact immutable declarations previously succeeded.
        var config = F_dv_get(F_dv_get(v_world, "simulation"), "config");
        if (config is List<object> || !R.IsDvList(config))
            return SimulationUnmanagedProcesses(v_world, v_processes, out _);
        var key = new List<object>(2) { "simulation-unmanaged-processes-v1", config };
        var cached = R.memo_read(v_processes, key);
        if (R.Bool(R.at(cached, 0d)))
            return R.copy_value(R.at(cached, 1d));

        var result = SimulationUnmanagedProcesses(v_world, v_processes, out var textLawIds);
        // With no managed rules the original function returns the input itself;
        // do not cache that case or change its identity/return contract. Filtered
        // results keep their independently writable root on both miss and hit.
        if (textLawIds && result is List<object>) _ = R.memo_put(v_processes, key, result);
        return result;
    }

    private static object SimulationUnmanagedProcesses(object v_world, object v_processes, out bool textLawIds)
    {
        textLawIds = true;
        v_world = R.snapshot(v_world);
        v_processes = R.snapshot(v_processes);
        if (R.Bool(R.not(F_dv_has_key(v_world, "simulation"))))
        {
            return v_processes;
        }
        object v_daily__413498 = F_dv_clone(F_dv_find(F_dv_get(F_dv_get(v_world, "simulation"), "config"), "daily_rules", new System.Collections.Generic.List<object>(0) { }));
        object v_macro__413576 = F_dv_find(F_dv_get(F_dv_get(v_world, "simulation"), "config"), "macro_rules", new System.Collections.Generic.List<object>(0) { });
        object v_m__413644 = R.number("0");
        while (R.Bool(R.lt(v_m__413644, R.count(v_macro__413576))))
        {
            _ = R.push(v_daily__413498, R.at(v_macro__413576, v_m__413644));
            v_m__413644 = R.add(v_m__413644, R.number("1"));
        }
        if (R.Bool(R.eq(R.count(v_daily__413498), R.number("0"))))
        {
            return v_processes;
        }
        object v_result__413794 = new System.Collections.Generic.List<object>(0) { };
        object v_i__413814 = R.number("0");
        while (R.Bool(R.lt(v_i__413814, R.count(v_processes))))
        {
            object v_managed__413867 = false;
            object v_j__413895 = R.number("0");
            while (R.Bool(R.lt(v_j__413895, R.count(v_daily__413498))))
            {
                var managedLaw = F_dv_get(R.at(v_daily__413498, v_j__413895), "law");
                var processLaw = F_dv_get(R.at(v_processes, v_i__413814), "law");
                // Non-text law values can have reference equality semantics.
                // Observe the original operands in order and never cache them.
                if (managedLaw is not string || processLaw is not string) textLawIds = false;
                if (R.Bool(R.eq(managedLaw, processLaw)))
                {
                    v_managed__413867 = true;
                }
                v_j__413895 = R.add(v_j__413895, R.number("1"));
            }
            if (R.Bool(R.not(v_managed__413867)))
            {
                _ = R.push(v_result__413794, R.at(v_processes, v_i__413814));
            }
            v_i__413814 = R.add(v_i__413814, R.number("1"));
        }
        return v_result__413794;
    }

    public static object F_dvs_simulation_environment(object v_world, object v_story, object v_events, object v_parent)
    {
        v_world = R.snapshot(v_world);
        v_story = R.snapshot(v_story);
        if (R.Bool(R.not(F_dv_has_key(v_world, "simulation"))))
        {
            return v_world;
        }
        object v_time__414275 = F_dv_get(v_world, "time");
        object v_config__414301 = F_dv_get(F_dv_get(v_world, "simulation"), "config");
        object v_next__414342 = R.add(F_dv_get(F_dv_get(v_world, "simulation"), "last_macro"), R.number("15"));
        while (R.Bool(R.not(R.lt(v_time__414275, v_next__414342))))
        {
            v_world = R.snapshot(F_dv_with(v_world, "time", v_next__414342));
            object v_clock__414463 = F_dv_find(v_story, "clock_ref", false);
            if (R.Bool(R.not(R.eq(v_clock__414463, false))))
            {
                v_world = R.snapshot(F_dv_store(v_world, v_clock__414463, F_dv_cell(v_next__414342, R.number("1"), v_next__414342, new System.Collections.Generic.List<object>(1) { v_parent })));
            }
            object v_macro_rules__414622 = F_dv_find(v_config__414301, "macro_rules", new System.Collections.Generic.List<object>(0) { });
            object v_m__414683 = R.number("0");
            while (R.Bool(R.lt(v_m__414683, R.count(v_macro_rules__414622))))
            {
                if (R.Bool(F_dv_find(R.at(v_macro_rules__414622, v_m__414683), "before_diffusion", false)))
                {
                    v_world = F_dvs_simulation_execute_rule(v_world, R.at(v_macro_rules__414622, v_m__414683), v_events, v_parent, v_next__414342, "simulation_environment_effect");
                }
                v_m__414683 = R.add(v_m__414683, R.number("1"));
            }
            v_world = R.snapshot(F_dvs_simulation_diffuse(v_world, v_config__414301, v_events, v_parent, v_next__414342));
            v_m__414683 = R.number("0");
            while (R.Bool(R.lt(v_m__414683, R.count(v_macro_rules__414622))))
            {
                if (R.Bool(R.not(F_dv_find(R.at(v_macro_rules__414622, v_m__414683), "before_diffusion", false))))
                {
                    v_world = F_dvs_simulation_execute_rule(v_world, R.at(v_macro_rules__414622, v_m__414683), v_events, v_parent, v_next__414342, "simulation_environment_effect");
                }
                v_m__414683 = R.add(v_m__414683, R.number("1"));
            }
            v_world = F_dvs_simulation_daily(v_world, v_config__414301, v_events, v_parent, v_next__414342);
            v_world = R.snapshot(F_dv_with(v_world, "simulation", F_dv_with_many(F_dv_get(v_world, "simulation"), new System.Collections.Generic.List<object>(2) { F_dv_pair("last_macro", v_next__414342), F_dv_pair("macro_count", R.add(F_dv_get(F_dv_get(v_world, "simulation"), "macro_count"), R.number("1"))) })));
            v_next__414342 = R.add(v_next__414342, R.number("15"));
        }
        return F_dv_with(v_world, "time", v_time__414275);
    }
}
