using DuelView.Core;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;
public static partial class CampaignRuntime {
    public static object F_island_parameters() {
        return DvValueTemplate.Instantiate("template.island_parameters");
    }
    public static object F_island_source() {
        return DvValueTemplate.Instantiate("template.island_source");
    }
    public static object F_island_ref(object v_object,object v_field) {
        return DvValueTemplate.Instantiate("template.island_ref", v_object, v_field);
    }
    public static object F_island_air_ref(object v_room) {
        return DvValueTemplate.Instantiate("template.island_air_ref", v_room);
    }
    public static object F_island_daylight() {
        return DvValueTemplate.Instantiate("template.island_daylight");
    }

    public static object F_island_rooms() {
        object v_p__1977 = F_island_parameters();
        object v_source__2010 = F_island_topology_rooms();
        object v_rooms__2052 = new List<object> {  };
        object v_i__2072 = R.number("0");
        while (R.Bool(R.lt(v_i__2072, R.count(v_source__2010))))
        {
            object v_room__2124 = R.at(v_source__2010, v_i__2072);
            object v_declaration__2154 = F_dv_with_many(v_room__2124, new List<object> { F_dv_pair("concentration_ref", F_island_air_ref(F_dv_get(v_room__2124, "id"))), F_dv_pair("height", F_island_height(v_room__2124)), DvValueTemplate.Instantiate("template.inline.island_rooms.1488"), F_dv_pair("volume", F_dv_get(v_room__2124, "area_32_cells")), F_dv_pair("settling", F_dv_get(v_p__1977, "settling_fraction_per_15_minutes")) });
            if (R.Bool(F_island_outdoors(F_dv_get(v_room__2124, "map"))))
            {
                object v_severe__2481 = F_campaign_any(F_dv_fact(F_campaign_ref(Authored("m55.island_rooms.literal_0002"), Authored("m55.island_rooms.literal_0003")), true), F_dv_fact(F_campaign_ref(Authored("m55.island_rooms.literal_0002"), Authored("m55.island_rooms.literal_0004")), true));
                v_declaration__2154 = DvValueTemplate.Instantiate("template.inline.island_rooms.1489", v_declaration__2154, v_p__1977, v_severe__2481);
            }
            _ = R.push(v_rooms__2052, v_declaration__2154);
            v_i__2072 = R.add(v_i__2072, R.number("1"));
        }
        return v_rooms__2052;
    }
    public static object F_island_edges(object v_rooms) {
        object v_p__3490 = F_island_parameters();
        object v_source__3523 = F_island_topology_edges();
        object v_edges__3565 = new List<object> {  };
        object v_i__3585 = R.number("0");
        while (R.Bool(R.lt(v_i__3585, R.count(v_source__3523))))
        {
            object v_edge__3637 = R.at(v_source__3523, v_i__3585);
            object v_a__3667 = R.at(v_rooms, F_dv_index_by_id(v_rooms, F_dv_get(v_edge__3637, "from")));
            object v_b__3724 = R.at(v_rooms, F_dv_index_by_id(v_rooms, F_dv_get(v_edge__3637, "to")));
            object v_wind__3932 = R.number("0");
            if (R.Bool(R.eq(F_dv_get(v_a__3667, "map"), F_dv_get(v_b__3724, "map"))))
            {
                if (R.Bool(R.lt(F_dv_get(v_a__3667, "center_x"), F_dv_get(v_b__3724, "center_x"))))
                {
                    v_wind__3932 = F_dv_get(v_p__3490, "wind_strength");
                }
                if (R.Bool(R.lt(F_dv_get(v_b__3724, "center_x"), F_dv_get(v_a__3667, "center_x"))))
                {
                    v_wind__3932 = R.sub(R.number("0"), F_dv_get(v_p__3490, "wind_strength"));
                }
            }
            object v_forward__4307 = R.number("0");
            object v_reverse__4332 = R.number("0");
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.island_edges.1490", v_edge__3637)))
            {
                object v_e__4451 = R.number("0");
                while (R.Bool(R.lt(v_e__4451, R.count(v_source__3523))))
                {
                    if (R.Bool(DvValueTemplate.Instantiate("template.inline.island_edges.1491", v_source__3523, v_e__4451)))
                    {
                        if (R.Bool((R.Bool(R.eq(F_dv_get(R.at(v_source__3523, v_e__4451), "from"), F_dv_get(v_edge__3637, "from"))) ? (object)(R.eq(F_dv_get(R.at(v_source__3523, v_e__4451), "to"), F_dv_get(v_edge__3637, "to"))) : (object)(false))))
                        {
                            v_forward__4307 = R.add(v_forward__4307, R.number("1"));
                        }
                        if (R.Bool((R.Bool(R.eq(F_dv_get(R.at(v_source__3523, v_e__4451), "from"), F_dv_get(v_edge__3637, "to"))) ? (object)(R.eq(F_dv_get(R.at(v_source__3523, v_e__4451), "to"), F_dv_get(v_edge__3637, "from"))) : (object)(false))))
                        {
                            v_reverse__4332 = R.add(v_reverse__4332, R.number("1"));
                        }
                    }
                    v_e__4451 = R.add(v_e__4451, R.number("1"));
                }
            }
            object v_share__4897 = R.number("1");
            if (R.Bool(R.lt(R.number("0"), v_reverse__4332)))
            {
                v_share__4897 = R.div(F_dv_max(v_forward__4307, v_reverse__4332), R.add(v_forward__4307, v_reverse__4332));
            }
            object v_declaration__5013 = DvValueTemplate.Instantiate("template.inline.island_edges.1492", v_edge__3637, v_p__3490, v_a__3667, v_b__3724, v_share__4897, v_wind__3932);
            _ = R.push(v_edges__3565, v_declaration__5013);
            v_i__3585 = R.add(v_i__3585, R.number("1"));
        }
        return v_edges__3565;
    }
    public static object F_island_rule(object v_id,object v_category,object v_when,object v_calculations,object v_effects) {
        return DvValueTemplate.Instantiate("template.island_rule", v_id, v_category, v_when, v_calculations, v_effects);
    }
    public static object F_island_daily_copy(object v_rule,object v_at) {
        return DvValueTemplate.Instantiate("template.island_daily_copy", v_rule, v_at);
    }
    public static object F_island_macro_rules(object v_locations,object v_chapter) {
        return DvValueTemplate.Instantiate("template.island_macro_rules", v_locations, v_chapter);
    }
    public static object F_island_daily_rules(object v_chapter) {
        object v_p__11107 = F_island_parameters();
        object v_people__11140 = F_campaign_people();
        object v_consumed__11176 = new List<object> {  };
        object v_carried__11199 = new List<object> {  };
        object v_hungry__11221 = new List<object> {  };
        object v_alive__11242 = new List<object> {  };
        object v_masks__11262 = new List<object> {  };
        object v_rules__11282 = new List<object> {  };
        object v_i__11302 = R.number("0");
        while (R.Bool(R.lt(v_i__11302, R.count(v_people__11140))))
        {
            object v_actor__11354 = R.at(v_people__11140, v_i__11302);
            _ = R.push(v_consumed__11176, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1493", v_actor__11354));
            _ = R.push(v_carried__11199, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1494", v_actor__11354));
            _ = R.push(v_carried__11199, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1495", v_actor__11354));
            _ = R.push(v_hungry__11221, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1496", v_actor__11354));
            _ = R.push(v_alive__11242, new List<object> { F_dv_pair("constant", R.number("1")), F_dv_pair("when", F_c2_alive(v_actor__11354)) });
            _ = R.push(v_masks__11262, new List<object> { F_dv_pair("constant", F_dv_mask_bit(v_i__11302)), F_dv_pair("when", F_c2_alive(v_actor__11354)) });
            object v_dose__12111 = F_island_rule(R.cat(Authored("m55.island_daily_rules.literal_0005"), v_actor__11354), Authored("m55.island_daily_rules.literal_0006"), F_c2_alive(v_actor__11354), DvValueTemplate.Instantiate("template.inline.island_daily_rules.1497", v_actor__11354), new List<object> {  });
            _ = R.push(v_rules__11282, F_island_daily_copy(v_dose__12111, Authored("m55.island_daylight.literal_0003")));
            _ = R.push(v_rules__11282, F_island_daily_copy(v_dose__12111, Authored("m55.island_daylight.literal_0004")));
            if (R.Bool(DvValueTemplate.Instantiate("template.inline.island_daily_rules.1498", v_chapter)))
            {
                object v_pathology__13043 = AuthoredCopy("m55.island_daily_rules.pathology");
                object v_j__13159 = R.number("0");
                while (R.Bool(R.lt(v_j__13159, R.count(v_pathology__13043))))
                {
                    object v_name__13230 = R.cat(Authored("m55.island_daily_rules.literal_0008"), R.cat(v_actor__11354, R.cat(Authored("m55.island_daily_copy.literal"), R.at(v_pathology__13043, v_j__13159))));
                    _ = R.push(v_rules__11282, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1499", v_name__13230));
                    _ = R.push(v_rules__11282, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1500", v_name__13230));
                    v_j__13159 = R.add(v_j__13159, R.number("1"));
                }
            }
            v_i__11302 = R.add(v_i__11302, R.number("1"));
        }
        object v_period__14102 = F_dv_clone(v_consumed__11176);
        _ = R.push(v_period__14102, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1501"));
        object v_inventory__14347 = F_dv_clone(v_carried__11199);
        _ = R.push(v_inventory__14347, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1502"));
        _ = R.push(v_inventory__14347, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1503"));
        _ = R.push(v_inventory__14347, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1504"));
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.island_daily_rules.1505", v_chapter)))
        {
            _ = R.push(v_inventory__14347, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1506"));
            _ = R.push(v_inventory__14347, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1507"));
        }
        object v_ledger__15484 = F_island_rule(Authored("m55.island_daily_rules.literal_0022"), Authored("m55.island_daily_rules.literal_0023"), F_dv_true(), DvValueTemplate.Instantiate("template.inline.island_daily_rules.1508", v_period__14102, v_consumed__11176, v_inventory__14347, v_hungry__11221, v_alive__11242, v_masks__11262), new List<object> {  });
        _ = R.push(v_rules__11282, F_island_daily_copy(v_ledger__15484, Authored("m55.island_daylight.literal_0003")));
        _ = R.push(v_rules__11282, F_island_daily_copy(v_ledger__15484, Authored("m55.island_daylight.literal_0004")));
        object v_crop_air__17268 = F_island_air_ref(Authored("m55.island_daily_rules.literal_0030"));
        object v_safe_crop_air__17379 = F_campaign_any(F_dv_not(F_dv_at_least(v_crop_air__17268, F_dv_get(v_p__11107, "safe_concentration"))), F_dv_fact(v_crop_air__17268, F_dv_get(v_p__11107, "safe_concentration")));
        object v_healthy__17511 = DvValueTemplate.Instantiate("template.inline.island_daily_rules.1509", v_safe_crop_air__17379, v_p__11107);
        object v_growth__18298 = F_island_rule(Authored("m55.island_daily_rules.literal_0036"), Authored("m55.island_daily_rules.literal_0037"), v_healthy__17511, DvValueTemplate.Instantiate("template.inline.island_daily_rules.1510"), DvValueTemplate.Instantiate("template.inline.island_daily_rules.1511"));
        object v_damage__19070 = F_island_rule(Authored("m55.island_daily_rules.literal_0039"), Authored("m55.island_daily_rules.literal_0037"), DvValueTemplate.Instantiate("template.inline.island_daily_rules.1512", v_crop_air__17268, v_p__11107), DvValueTemplate.Instantiate("template.inline.island_daily_rules.1513", v_p__11107), DvValueTemplate.Instantiate("template.inline.island_daily_rules.1514"));
        _ = R.push(v_rules__11282, F_island_daily_copy(v_growth__18298, Authored("m55.island_daylight.literal_0003")));
        _ = R.push(v_rules__11282, F_island_daily_copy(v_growth__18298, Authored("m55.island_daylight.literal_0004")));
        _ = R.push(v_rules__11282, F_island_daily_copy(v_damage__19070, Authored("m55.island_daylight.literal_0003")));
        _ = R.push(v_rules__11282, F_island_daily_copy(v_damage__19070, Authored("m55.island_daylight.literal_0004")));
        return v_rules__11282;
    }
    public static object F_island_display_object(object v_id,object v_room,object v_title,object v_fields) {
        return DvValueTemplate.Instantiate("template.island_display_object", v_id, v_room, v_title, v_fields);
    }
    public static object F_island_inspect(object v_actor,object v_id,object v_room,object v_object,object v_fields,object v_label,object v_template,object v_names) {
        return DvValueTemplate.Instantiate("template.island_inspect", v_actor, v_id, v_room, v_object, v_fields, v_label, v_template, v_names);
    }
    public static object F_island_actions(object v_actor,object v_locations,object v_chapter) {
        return DvValueTemplate.Instantiate("template.island_actions", v_actor, v_locations, v_chapter);
    }
    public static object F_island_crop_jobs(object v_actor) {
        return DvValueTemplate.Instantiate("template.island_crop_jobs", v_actor);
    }
    public static object F_island_install(object v_scene,object v_locations) {
        object v_chapter__36579 = F_dv_find(v_scene,Authored("m55.island_install.literal"),R.number("1"));
        if (R.Bool(R.eq(F_dv_find(v_scene,Authored("m55.island_install.literal_0002"),R.number("0")),v_chapter__36579))) {
            return v_scene;
        }
        object v_p__36815 = F_island_parameters();
        object v_rooms__36848 = F_island_rooms();
        object v_physical__36880 = F_c2_world_objects(F_dv_get(v_scene,"physical"));
        v_physical__36880 = F_campaign_add_new(v_physical__36880,F_campaign_state_object(Authored("m55.island_install.literal_0003"),new System.Collections.Generic.List<object>(1) {F_dv_pair("minute_of_day",F_dv_field(R.sub(F_dv_get(v_physical__36880,"time"),R.mul(R.floor(R.div(F_dv_get(v_physical__36880,"time"),Authored("m55.island_macro_rules.literal_0003"))),Authored("m55.island_macro_rules.literal_0003")))))}));
        if (R.Bool(R.not(F_dv_uncontested(F_dv_read(v_physical__36880,F_campaign_ref(Authored("m55.island_daily_rules.literal_0014"),Authored("m55.island_daily_rules.literal_0015"))))))) {
            v_physical__36880 = F_dv_store(v_physical__36880,F_campaign_ref(Authored("m55.island_daily_rules.literal_0014"),Authored("m55.island_daily_rules.literal_0015")),F_dv_with(F_dv_field(F_dv_get(v_p__36815,"reserve_portions")),"basis",new System.Collections.Generic.List<object>(1) {F_island_source()}));
        }
        if (R.Bool(R.not(F_dv_uncontested(F_dv_read(v_physical__36880,F_campaign_ref(Authored("m55.island_rooms.literal_0002"),Authored("m55.island_rooms.literal_0006"))))))) {
            v_physical__36880 = F_dv_store(v_physical__36880,F_campaign_ref(Authored("m55.island_rooms.literal_0002"),Authored("m55.island_rooms.literal_0006")),F_dv_with(F_dv_field(false),"basis",new System.Collections.Generic.List<object>(1) {F_island_source()}));
        }
        object v_meter_fields__38228 = new System.Collections.Generic.List<object>(3) {F_dv_pair("concentration",F_dv_unknown()),F_dv_pair("numeric_sample_at",F_dv_unknown()),F_dv_pair("numeric_safe",F_dv_field(false))};
        object v_mf__38352 = R.number("0");
        while (R.Bool(R.lt(v_mf__38352,R.count(v_meter_fields__38228)))) {
            object v_ref__38412 = F_campaign_ref(Authored("m55.island_macro_rules.literal_0016"),R.at(R.at(v_meter_fields__38228,v_mf__38352),R.number("0")));
            if (R.Bool(R.not(F_dv_uncontested(F_dv_read(v_physical__36880,v_ref__38412))))) {
                v_physical__36880 = F_dv_store(v_physical__36880,v_ref__38412,R.at(R.at(v_meter_fields__38228,v_mf__38352),R.number("1")));
            }
            v_mf__38352 = R.add(v_mf__38352,R.number("1"));
        }
        object v_i__38683 = R.number("0");
        while (R.Bool(R.lt(v_i__38683,R.count(v_rooms__36848)))) {
            object v_room__38734 = R.at(v_rooms__36848,v_i__38683);
            v_physical__36880 = F_campaign_add_new(v_physical__36880,F_campaign_state_object(R.cat(Authored("m55.island_actions.literal_0002"),F_dv_get(v_room__38734,"id")),new System.Collections.Generic.List<object>(1) {F_dv_pair("concentration",F_dv_with(F_dv_field(F_dv_get(v_p__36815,"initial_concentration")),"basis",new System.Collections.Generic.List<object>(1) {F_island_source()}))}));
            v_i__38683 = R.add(v_i__38683,R.number("1"));
        }
        v_physical__36880 = F_campaign_add_new(v_physical__36880,F_island_display_object(Authored("m55.island_inspect.literal_0010"),Authored("m55.island_actions.literal_0011"),Authored("m55.island_install.literal_0004"),new System.Collections.Generic.List<object>(7) {F_dv_pair("settled_at",F_dv_unknown()),F_dv_pair("period_consumed",F_dv_field(R.number("0"))),F_dv_pair("consumed_total",F_dv_field(R.number("0"))),F_dv_pair("inventory_portions",F_dv_unknown()),F_dv_pair("hungry_people",F_dv_unknown()),F_dv_pair("living_people",F_dv_unknown()),F_dv_pair("living_mask",F_dv_unknown())}));
        v_physical__36880 = F_campaign_add_new(v_physical__36880,F_island_display_object(Authored("m55.island_inspect.literal_0007"),Authored("m55.island_actions.literal_0017"),Authored("m55.island_install.literal_0005"),new System.Collections.Generic.List<object>(7) {F_dv_pair("trays",F_dv_field(F_dv_get(v_p__36815,"crop_trays"))),F_dv_pair("growth",F_dv_field(R.number("0"))),F_dv_pair("cared",F_dv_field(false)),F_dv_pair("light",F_dv_field(F_dv_get(v_p__36815,"crop_initial_light"))),F_dv_pair("damaged_trays",F_dv_field(R.number("0"))),F_dv_pair("last_growth_at",F_dv_unknown()),F_dv_pair("stored_portions",F_dv_field(R.number("0")))}));
        object v_people__40031 = F_campaign_people();
        v_i__38683 = R.number("0");
        while (R.Bool(R.lt(v_i__38683,R.count(v_people__40031)))) {
            object v_fields__40115 = AuthoredCopy("m55.island_install.fields");
            object v_f__40217 = R.number("0");
            while (R.Bool(R.lt(v_f__40217,R.count(v_fields__40115)))) {
                object v_ref__40277 = F_campaign_own(R.at(v_people__40031,v_i__38683),R.at(v_fields__40115,v_f__40217));
                if (R.Bool(R.not(F_dv_uncontested(F_dv_read(v_physical__36880,v_ref__40277))))) {
                    v_physical__36880 = F_dv_store(v_physical__36880,v_ref__40277,F_dv_field(R.number("0")));
                }
                v_f__40217 = R.add(v_f__40217,R.number("1"));
            }
            v_i__38683 = R.add(v_i__38683,R.number("1"));
        }
        object v_actors__40494 = new System.Collections.Generic.List<object>(0) {};
        object v_bindings__40515 = F_dv_get(v_scene,"bindings");
        v_i__38683 = R.number("0");
        while (R.Bool(R.lt(v_i__38683,R.count(F_dv_get(v_scene,"actors"))))) {
            object v_actor__40604 = R.at(F_dv_get(v_scene,"actors"),v_i__38683);
            if (R.Bool(F_dv_has(v_people__40031,F_dv_get(v_actor__40604,"id")))) {
                object v_mind__40683 = F_dv_get(v_actor__40604,"mind");
                object v_objects__40718 = new System.Collections.Generic.List<object>(5) {Authored("m55.island_inspect.literal_0010"),Authored("m55.island_inspect.literal_0007"),F_campaign_body(F_dv_get(v_actor__40604,"id")),Authored("m55.island_daily_rules.literal_0014"),Authored("m55.island_daily_rules.literal_0016")};
                object v_j__41067 = R.number("0");
                while (R.Bool(R.lt(v_j__41067,R.count(v_objects__40718)))) {
                    if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_mind__40683,"objects"),R.at(v_objects__40718,v_j__41067))))) {
                        v_mind__40683 = F_dv_add_object(v_mind__40683,F_campaign_local_object(R.at(v_objects__40718,v_j__41067)));
                    }
                    if (R.Bool(R.eq(F_dv_local(v_bindings__40515,R.at(v_objects__40718,v_j__41067),F_dv_get(v_actor__40604,"id")),""))) {
                        v_bindings__40515 = F_dv_append(v_bindings__40515,F_dv_binding(R.at(v_objects__40718,v_j__41067),R.at(v_objects__40718,v_j__41067),F_dv_get(v_actor__40604,"id")));
                    }
                    v_j__41067 = R.add(v_j__41067,R.number("1"));
                }
                v_j__41067 = R.number("0");
                while (R.Bool(R.lt(v_j__41067,R.count(v_locations)))) {
                    object v_object__41537 = R.cat(Authored("m55.island_actions.literal_0002"),F_dv_get(R.at(v_locations,v_j__41067),"room"));
                    if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_mind__40683,"objects"),v_object__41537)))) {
                        v_mind__40683 = F_dv_add_object(v_mind__40683,F_campaign_local_object(v_object__41537));
                    }
                    if (R.Bool(R.eq(F_dv_local(v_bindings__40515,v_object__41537,F_dv_get(v_actor__40604,"id")),""))) {
                        v_bindings__40515 = F_dv_append(v_bindings__40515,F_dv_binding(v_object__41537,v_object__41537,F_dv_get(v_actor__40604,"id")));
                    }
                    v_j__41067 = R.add(v_j__41067,R.number("1"));
                }
                object v_actions__41979 = R.Block(() => { object comp_source_41993 = F_dv_get(v_actor__40604,"actions");var comp_result_41993 = new System.Collections.Generic.List<object>();for (int comp_i_41993 = 0; comp_i_41993 < R.Items(comp_source_41993).Count; comp_i_41993++) {object v_action__comp_41993 = R.at(comp_source_41993, (double)comp_i_41993);if (R.Bool(R.not(F_dv_find(v_action__comp_41993,Authored("m55.island_install.literal_0006"),false)))) comp_result_41993.Add(v_action__comp_41993);}return (object)comp_result_41993;});
                object v_additions__42146 = F_island_actions(F_dv_get(v_actor__40604,"id"),v_locations,v_chapter__36579);
                v_j__41067 = R.number("0");
                while (R.Bool(R.lt(v_j__41067,R.count(v_additions__42146)))) {
                    _ = R.push(v_actions__41979,R.at(v_additions__42146,v_j__41067));
                    v_j__41067 = R.add(v_j__41067,R.number("1"));
                }
                if (R.Bool(R.eq(v_chapter__36579,Authored("m55.island_macro_rules.literal_0005")))) {
                    object v_jobs__42465 = F_island_crop_jobs(F_dv_get(v_actor__40604,"id"));
                    v_j__41067 = R.number("0");
                    while (R.Bool(R.lt(v_j__41067,R.count(v_jobs__42465)))) {
                        object v_law__42590 = F_dv_get(R.at(v_jobs__42465,v_j__41067),"law");
                        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_physical__36880,"adoptions"),F_dv_get(v_law__42590,"name"))))) {
                            v_physical__36880 = F_dv_adopt(v_physical__36880,v_law__42590,"enforce",R.number("1"),new System.Collections.Generic.List<object>(1) {F_island_source()});
                        }
                        if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_mind__40683,"adoptions"),F_dv_get(v_law__42590,"name"))))) {
                            v_mind__40683 = F_dv_adopt(v_mind__40683,v_law__42590,"predict",R.number("1"),new System.Collections.Generic.List<object>(1) {F_island_source()});
                        }
                        v_j__41067 = R.add(v_j__41067,R.number("1"));
                    }
                }
                v_actor__40604 = F_dv_with_many(v_actor__40604,new System.Collections.Generic.List<object>(2) {F_dv_pair("mind",v_mind__40683),F_dv_pair("actions",v_actions__41979)});
            }
            _ = R.push(v_actors__40494,v_actor__40604);
            v_i__38683 = R.add(v_i__38683,R.number("1"));
        }
        object v_declarations__43090 = F_dv_with_many(F_dv_find(v_scene,Authored("m55.island_install.literal_0007"),new System.Collections.Generic.List<object>(0) {}),new System.Collections.Generic.List<object>(6) {F_dv_pair("rooms",v_rooms__36848),F_dv_pair("edges",F_island_edges(v_rooms__36848)),F_dv_pair("locations",v_locations),F_dv_pair("macro_rules",F_island_macro_rules(v_locations,v_chapter__36579)),F_dv_pair("daily_rules",F_island_daily_rules(v_chapter__36579)),F_dv_pair("island_parameters",v_p__36815)});
        return F_dv_with_many(v_scene,new System.Collections.Generic.List<object>(5) {F_dv_pair("physical",v_physical__36880),F_dv_pair("actors",v_actors__40494),F_dv_pair("bindings",v_bindings__40515),F_dv_pair("simulation_declarations",v_declarations__43090),F_dv_pair("island_installed_chapter",v_chapter__36579)});
    }
}
