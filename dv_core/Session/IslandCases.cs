using DuelView.Core;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;
public static partial class CampaignRuntime {
    public static object F_island_case_parameters() {
        return DvValueTemplate.Instantiate("template.island_case_parameters");
    }
    public static object F_island_case_ref(object v_field) {
        return DvValueTemplate.Instantiate("template.island_case_ref", v_field);
    }
    public static object F_island_case_director(object v_field) {
        return DvValueTemplate.Instantiate("template.island_case_director", v_field);
    }

    public static object F_island_case_value(object v_actor,object v_values) {
        object v_order__1061 = F_dv_find(F_dv_get(v_actor, "policy"), Authored("m56.island_case_value.literal"), new List<object> {  });
        object v_value__1174 = R.number("0");
        object v_i__1193 = R.number("0");
        while (R.Bool(R.lt(v_i__1193, R.count(v_order__1061))))
        {
            if (R.Bool(F_dv_has(v_values, R.at(v_order__1061, v_i__1193))))
            {
                v_value__1174 = R.add(v_value__1174, R.div(R.sub(R.count(v_order__1061), v_i__1193), F_dv_max(R.number("1"), R.count(v_order__1061))));
            }
            v_i__1193 = R.add(v_i__1193, R.number("1"));
        }
        return v_value__1174;
    }

    public static object F_island_case_adopt(object v_world,object v_law,object v_mode) {
        object v_old__5499 = F_dv_find(F_dv_get(v_world, "adoptions"), F_dv_get(v_law, "name"), false);
        if (R.Bool(R.not(R.eq(v_old__5499, false))))
        {
            if (R.Bool((R.Bool((R.Bool((R.Bool(F_dv_get(v_old__5499, "active")) ? (object)(R.eq(F_dv_get(v_old__5499, "mode"), v_mode)) : (object)(false))) ? (object)(R.eq(F_dv_get(v_old__5499, "confidence"), R.number("1"))) : (object)(false))) ? (object)(F_dv_same(F_dv_get(v_old__5499, "law"), v_law)) : (object)(false))))
            {
                return v_world;
            }
            return F_dv_reconsider(v_world, v_law, v_mode, R.number("1"), new List<object> { F_island_source() });
        }
        return F_dv_adopt(v_world, v_law, v_mode, R.number("1"), new List<object> { F_island_source() });
    }
    public static object F_island_case_allocate(object v_state) {
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(F_dv_get(v_state,"story"),Authored("m56.island_case_allocate.literal"),R.number("1")),Authored("m56.island_case_allocate.literal_0002")))) ? (object)(true) : (object)(R.lt(F_dv_get(F_dv_get(v_state,"physical"),"time"),F_dv_get(F_island_case_parameters(),"start_minute")))))) {
            return v_state;
        }
        if (R.Bool(R.eq(F_dv_truth(F_dv_get(v_state,"physical"),F_dv_fact(F_island_case_ref(Authored("m56.island_case_allocate.literal_0003")),true)),"true"))) {
            return v_state;
        }
        object v_settlement__6312 = F_dv_read(F_dv_get(v_state,"physical"),F_island_ref(Authored("m56.island_case_allocate.literal_0004"),Authored("m56.island_case_allocate.literal_0005")));
        object v_matched__6526 = F_dv_read(F_dv_get(v_state,"physical"),F_island_case_director(Authored("m56.island_case_allocate.literal_0006")));
        if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_settlement__6312))) ? (object)(true) : (object)(R.lt(F_dv_get(v_settlement__6312,"value"),F_dv_get(F_island_case_parameters(),"start_minute")))))) {
            return v_state;
        }
        if (R.Bool((R.Bool(F_dv_uncontested(v_matched__6526)) ? (object)(R.not(R.lt(F_dv_get(v_matched__6526,"value"),F_dv_get(v_settlement__6312,"value")))) : (object)(false)))) {
            return v_state;
        }
        object v_roles__6884 = F_island_case_roles();
        object v_retained__7075 = new System.Collections.Generic.List<object>(0) {};
        object v_incumbent_index__7098 = R.number("0");
        while (R.Bool(R.lt(v_incumbent_index__7098,R.count(v_roles__6884)))) {
            object v_incumbent__7177 = F_dv_read(F_dv_get(v_state,"physical"),F_island_case_director(F_dv_get(R.at(v_roles__6884,v_incumbent_index__7098),"id")));
            if (R.Bool((R.Bool(F_dv_uncontested(v_incumbent__7177)) ? (object)(R.not(R.eq(F_dv_get(v_incumbent__7177,"value"),""))) : (object)(false)))) {
                object v_index__7344 = F_dv_index_by_id(F_dv_get(v_state,"actors"),F_dv_get(v_incumbent__7177,"value"));
                if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.lt(v_index__7344,R.number("0")))) ? (object)(F_dvs_actor_can_act(R.at(F_dv_get(v_state,"actors"),v_index__7344))) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_state,"physical"),F_c2_can_act(F_dv_get(v_incumbent__7177,"value"))),"true")) : (object)(false))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_state,"physical"),F_dv_fact(F_campaign_own(F_dv_get(v_incumbent__7177,"value"),Authored("m56.island_case_candidate.literal")),true)),"true"))) : (object)(false)))) {
                    v_retained__7075 = F_dv_append(v_retained__7075,F_dv_get(v_incumbent__7177,"value"));
                }
            }
            v_incumbent_index__7098 = R.add(v_incumbent_index__7098,R.number("1"));
        }
        object v_used__7817 = new System.Collections.Generic.List<object>(0) {};
        object v_masks__7836 = R.number("0");
        object v_effects__7855 = new System.Collections.Generic.List<object>(0) {};
        object v_reasons__7877 = new System.Collections.Generic.List<object>(0) {};
        object v_r__7899 = R.number("0");
        while (R.Bool(R.lt(v_r__7899,R.count(v_roles__6884)))) {
            object v_role__7950 = R.at(v_roles__6884,v_r__7899);
            object v_previous__7979 = F_dv_read(F_dv_get(v_state,"physical"),F_island_case_director(F_dv_get(v_role__7950,"id")));
            object v_chosen__8058 = "";
            if (R.Bool((R.Bool(F_dv_uncontested(v_previous__7979)) ? (object)(R.not(R.eq(F_dv_get(v_previous__7979,"value"),""))) : (object)(false)))) {
                object v_index__8302 = F_dv_index_by_id(F_dv_get(v_state,"actors"),F_dv_get(v_previous__7979,"value"));
                if (R.Bool((R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.lt(v_index__8302,R.number("0")))) ? (object)(F_dvs_actor_can_act(R.at(F_dv_get(v_state,"actors"),v_index__8302))) : (object)(false))) ? (object)(R.eq(F_dv_truth(F_dv_get(v_state,"physical"),F_c2_can_act(F_dv_get(v_previous__7979,"value"))),"true")) : (object)(false))) ? (object)(R.not(R.eq(F_dv_truth(F_dv_get(v_state,"physical"),F_dv_fact(F_campaign_own(F_dv_get(v_previous__7979,"value"),Authored("m56.island_case_candidate.literal")),true)),"true"))) : (object)(false))) ? (object)(R.not(F_dv_has(v_used__7817,F_dv_get(v_previous__7979,"value")))) : (object)(false)))) {
                    v_chosen__8058 = F_dv_get(v_previous__7979,"value");
                }
            }
            object v_best__8742 = Authored("m56.island_case_allocate.best");
            object v_detail__8831 = new System.Collections.Generic.List<object>(0) {};
            if (R.Bool(R.eq(v_chosen__8058,""))) {
                object v_i__8886 = R.number("0");
                while (R.Bool(R.lt(v_i__8886,R.count(F_dv_get(v_state,"actors"))))) {
                    object v_actor__8960 = R.at(F_dv_get(v_state,"actors"),v_i__8886);
                    if (R.Bool((R.Bool((R.Bool(R.not(F_dv_has(v_used__7817,F_dv_get(v_actor__8960,"id")))) ? (object)(R.not(F_dv_has(v_retained__7075,F_dv_get(v_actor__8960,"id")))) : (object)(false))) ? (object)(F_island_case_candidate(v_actor__8960,v_role__7950,F_dv_get(v_state,"physical"))) : (object)(false)))) {
                        object v_assessment__9147 = F_island_case_score(v_actor__8960,v_role__7950);
                        if (R.Bool(R.lt(v_best__8742,F_dv_get(v_assessment__9147,"score")))) {
                            v_best__8742 = F_dv_get(v_assessment__9147,"score");
                            v_chosen__8058 = F_dv_get(v_actor__8960,"id");
                            v_detail__8831 = v_assessment__9147;
                        }
                    }
                    v_i__8886 = R.add(v_i__8886,R.number("1"));
                }
            }
            if (R.Bool(R.not(R.eq(v_chosen__8058,"")))) {
                v_used__7817 = F_dv_append(v_used__7817,v_chosen__8058);
                object v_identity__9491 = F_dv_index_by_id(R.Block(() => { object comp_source_9521 = F_campaign_people();var comp_result_9521 = new System.Collections.Generic.List<object>();for (int comp_i_9521 = 0; comp_i_9521 < R.Items(comp_source_9521).Count; comp_i_9521++) {object v_id__comp_9521 = R.at(comp_source_9521, (double)comp_i_9521);if (R.Bool(true)) comp_result_9521.Add(new System.Collections.Generic.List<object>(1) {F_dv_pair("id",v_id__comp_9521)});}return (object)comp_result_9521;}),v_chosen__8058);
                if (R.Bool((R.Bool(R.not(R.lt(v_identity__9491,R.number("0")))) ? (object)(R.lt(v_identity__9491,Authored("m56.island_case_allocate.literal_0007"))) : (object)(false)))) {
                    v_masks__7836 = R.add(v_masks__7836,F_dv_mask_bit(v_identity__9491));
                }
            }
            if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_previous__7979))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_previous__7979,"value"),v_chosen__8058)))))) {
                _ = R.push(v_effects__7855,F_dv_assign(F_island_case_director(F_dv_get(v_role__7950,"id")),v_chosen__8058));
            }
            _ = R.push(v_reasons__7877,new System.Collections.Generic.List<object>(4) {F_dv_pair("role",F_dv_get(v_role__7950,"id")),F_dv_pair("actor",v_chosen__8058),F_dv_pair("assessment",v_detail__8831),F_dv_pair("retained",(R.Bool(F_dv_uncontested(v_previous__7979)) ? (object)(R.eq(F_dv_get(v_previous__7979,"value"),v_chosen__8058)) : (object)(false)))});
            v_r__7899 = R.add(v_r__7899,R.number("1"));
        }
        _ = R.push(v_effects__7855,F_dv_assign(F_island_case_director(Authored("m56.island_case_allocate.literal_0006")),F_dv_get(v_settlement__6312,"value")));
        _ = R.push(v_effects__7855,F_dv_assign(F_island_case_director(Authored("m56.island_case_allocate.literal_0008")),v_masks__7836));
        object v_law__10366 = DvValueTemplate.Instantiate("template.inline.island_case_allocate.1515", v_settlement__6312, v_effects__7855);
        object v_world__10866 = F_island_case_adopt(F_dv_get(v_state,"physical"),v_law__10366,"enforce");
        object v_execution__10933 = F_dv_execute(v_world__10866,F_dv_get(v_law__10366,"name"),R.cat(F_dv_get(v_law__10366,"name"),R.cat(Authored("m56.island_case_allocate.literal_0010"),R.show(R.count(F_dv_get(v_state,"events"))))),F_dv_get(v_world__10866,"time"),F_dv_get(v_world__10866,"revision"));
        object v_events__11147 = F_dv_clone(F_dv_get(v_state,"events"));
        _ = F_dvs_event(v_events__11147,Authored("m56.island_case_allocate.literal_0011"),"",F_dv_get(v_world__10866,"time"),new System.Collections.Generic.List<object>(0) {},new System.Collections.Generic.List<object>(6) {F_dv_pair("settled_at",F_dv_get(v_settlement__6312,"value")),F_dv_pair("assignments",v_reasons__7877),F_dv_pair("participant_mask",v_masks__7836),F_dv_pair("execution",F_dv_without(v_execution__10933,"world")),F_dv_pair("knowledge_granted",false),F_dv_pair("approximation",true)});
        return F_dvs_owned_state_update(v_state,new System.Collections.Generic.List<object>(2) {F_dv_pair("physical",F_dv_get(v_execution__10933,"world")),F_dv_pair("events",v_events__11147)});
    }
    public static object F_island_case_fields() {
        return DvValueTemplate.Instantiate("template.island_case_fields");
    }
    public static object F_island_case_inspections(object v_actor) {
        object v_records__11743 = F_island_inspect(v_actor,R.cat(v_actor,Authored("m56.island_case_inspections.literal")),Authored("m56.island_case_roles.literal_0005"),Authored("m56.island_case_inspections.literal_0002"),F_island_case_fields(),Authored("m56.island_case_inspections.literal_0003"),"",AuthoredCopy("m56.island_case_inspections.literal_0004"));
        object v_bindings__12234 = F_island_case_clock_bindings(F_dv_get(R.at(F_dv_get(F_dv_get(v_records__11743,"presentation"),"lines"),R.number("0")),"bindings"));
        object v_lines__12321 = new System.Collections.Generic.List<object>(5) {F_c2_visible_line(F_dv_fact(F_island_case_ref(Authored("m56.island_case_inspections.literal_0005")),false),Authored("m56.island_case_inspections.literal_0006"),new System.Collections.Generic.List<object>(0) {},F_island_source()),F_c2_visible_line(F_dv_fact(F_island_case_ref(Authored("m56.island_case_inspections.literal_0005")),true),Authored("m56.island_case_inspections.literal_0007"),R.Block(() => { object comp_source_12777 = v_bindings__12234;var comp_result_12777 = new System.Collections.Generic.List<object>();for (int comp_i_12777 = 0; comp_i_12777 < R.Items(comp_source_12777).Count; comp_i_12777++) {object v_binding__comp_12777 = R.at(comp_source_12777, (double)comp_i_12777);if (R.Bool(F_dv_has(AuthoredCopy("m56.island_case_inspections.literal_0008"),F_dv_get(v_binding__comp_12777,"name")))) comp_result_12777.Add(v_binding__comp_12777);}return (object)comp_result_12777;}),F_island_source()),F_c2_visible_line(F_dv_fact(F_island_case_ref(Authored("m56.island_case_inspections.literal_0009")),true),Authored("m56.island_case_inspections.literal_0010"),R.Block(() => { object comp_source_13161 = v_bindings__12234;var comp_result_13161 = new System.Collections.Generic.List<object>();for (int comp_i_13161 = 0; comp_i_13161 < R.Items(comp_source_13161).Count; comp_i_13161++) {object v_binding__comp_13161 = R.at(comp_source_13161, (double)comp_i_13161);if (R.Bool(F_dv_has(AuthoredCopy("m56.island_case_inspections.literal_0011"),F_dv_get(v_binding__comp_13161,"name")))) comp_result_13161.Add(v_binding__comp_13161);}return (object)comp_result_13161;}),F_island_source()),F_c2_visible_line(F_dv_fact(F_island_case_ref(Authored("m56.island_case_inspections.literal_0012")),true),Authored("m56.island_case_inspections.literal_0013"),R.Block(() => { object comp_source_13545 = v_bindings__12234;var comp_result_13545 = new System.Collections.Generic.List<object>();for (int comp_i_13545 = 0; comp_i_13545 < R.Items(comp_source_13545).Count; comp_i_13545++) {object v_binding__comp_13545 = R.at(comp_source_13545, (double)comp_i_13545);if (R.Bool(F_dv_has(AuthoredCopy("m56.island_case_inspections.literal_0014"),F_dv_get(v_binding__comp_13545,"name")))) comp_result_13545.Add(v_binding__comp_13545);}return (object)comp_result_13545;}),F_island_source()),F_c2_visible_line(F_dv_fact(F_island_case_ref(Authored("m56.island_case_allocate.literal_0003")),true),Authored("m56.island_case_inspections.literal_0015"),new System.Collections.Generic.List<object>(0) {},F_island_source())};
        v_records__11743 = F_dv_with_many(v_records__11743,new System.Collections.Generic.List<object>(2) {F_dv_pair("presentation",F_dv_with(F_dv_get(v_records__11743,"presentation"),"lines",v_lines__12321)),F_dv_pair("interaction",AuthoredCopy("m56.island_case_inspections.literal_0016"))});
        object v_trace__14166 = F_island_inspect(v_actor,R.cat(v_actor,Authored("m56.island_case_inspections.literal_0017")),Authored("m56.island_case_inspections.literal_0018"),Authored("m56.island_case_inspections.literal_0019"),AuthoredCopy("m56.island_case_inspections.literal_0020"),Authored("m56.island_case_inspections.literal_0021"),"",AuthoredCopy("m56.island_case_inspections.literal_0022"));
        object v_trace_bindings__14740 = F_island_case_clock_bindings(F_dv_get(R.at(F_dv_get(F_dv_get(v_trace__14166,"presentation"),"lines"),R.number("0")),"bindings"));
        v_trace__14166 = F_dv_with(v_trace__14166,Authored("m56.island_case_inspections.literal_0023"),AuthoredCopy("m56.island_case_inspections.literal_0024"));
        v_trace__14166 = F_dv_with(v_trace__14166,"presentation",F_dv_with(F_dv_get(v_trace__14166,"presentation"),"lines",new System.Collections.Generic.List<object>(2) {F_c2_visible_line(F_dv_fact(F_island_ref(Authored("m56.island_case_inspections.literal_0025"),Authored("m56.island_case_inspections.literal_0026")),false),Authored("m56.island_case_inspections.literal_0027"),R.Block(() => { object comp_source_15400 = v_trace_bindings__14740;var comp_result_15400 = new System.Collections.Generic.List<object>();for (int comp_i_15400 = 0; comp_i_15400 < R.Items(comp_source_15400).Count; comp_i_15400++) {object v_binding__comp_15400 = R.at(comp_source_15400, (double)comp_i_15400);if (R.Bool(R.eq(F_dv_get(v_binding__comp_15400,"name"),Authored("m56.island_case_inspections.literal_0028")))) comp_result_15400.Add(v_binding__comp_15400);}return (object)comp_result_15400;}),F_island_source()),F_c2_visible_line(F_dv_fact(F_island_ref(Authored("m56.island_case_inspections.literal_0025"),Authored("m56.island_case_inspections.literal_0026")),true),Authored("m56.island_case_inspections.literal_0029"),v_trace_bindings__14740,F_island_source())}));
        object v_ledger__15886 = F_island_inspect(v_actor,R.cat(v_actor,Authored("m56.island_case_inspections.literal_0030")),Authored("m56.island_case_roles.literal_0005"),Authored("m56.island_case_score.literal_0008"),AuthoredCopy("m56.island_case_inspections.literal_0031"),Authored("m56.island_case_inspections.literal_0032"),Authored("m56.island_case_inspections.literal_0033"),AuthoredCopy("m56.island_case_inspections.literal_0034"));
        v_ledger__15886 = F_dv_with(v_ledger__15886,Authored("m56.island_case_inspections.literal_0023"),AuthoredCopy("m56.island_case_inspections.literal_0016"));
        object v_counter__16733 = F_dv_with(F_island_inspect(v_actor,R.cat(v_actor,Authored("m56.island_case_inspections.literal_0035")),Authored("m56.island_case_roles.literal_0005"),Authored("m56.island_case_inspections.literal_0036"),AuthoredCopy("m56.island_case_inspections.literal_0037"),Authored("m56.island_case_inspections.literal_0038"),Authored("m56.island_case_inspections.literal_0039"),AuthoredCopy("m56.island_case_inspections.literal_0040")),Authored("m56.island_case_inspections.literal_0023"),AuthoredCopy("m56.island_case_inspections.literal_0041"));
        object v_own__17566 = F_island_inspect(v_actor,R.cat(v_actor,Authored("m56.island_case_inspections.literal_0042")),Authored("m56.island_case_roles.literal_0005"),F_campaign_body(v_actor),AuthoredCopy("m56.island_case_inspections.literal_0043"),Authored("m56.island_case_inspections.literal_0044"),Authored("m56.island_case_inspections.literal_0045"),AuthoredCopy("m56.island_case_inspections.literal_0046"));
        return R.Block(() => { object comp_source_18157 = new System.Collections.Generic.List<object>(5) {v_records__11743,v_trace__14166,v_ledger__15886,v_counter__16733,v_own__17566};var comp_result_18157 = new System.Collections.Generic.List<object>();for (int comp_i_18157 = 0; comp_i_18157 < R.Items(comp_source_18157).Count; comp_i_18157++) {object v_action__comp_18157 = R.at(comp_source_18157, (double)comp_i_18157);if (R.Bool(true)) comp_result_18157.Add(F_dv_with_many(v_action__comp_18157,AuthoredCopy("m56.island_case_inspections.literal_0047")));}return (object)comp_result_18157;});
    }
    public static object F_island_case_clock_bindings(object v_bindings) {
        return DvValueTemplate.Instantiate("template.island_case_clock_bindings", v_bindings);
    }
    public static object F_island_case_numeric(object v_mind,object v_refs) {
        object v_values__18932 = new List<object> {  };
        object v_i__18953 = R.number("0");
        while (R.Bool(R.lt(v_i__18953, R.count(v_refs))))
        {
            object v_cell__19003 = F_dv_read(v_mind, R.at(v_refs, v_i__18953));
            if (R.Bool((R.Bool(R.not(F_dv_uncontested(v_cell__19003))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_cell__19003, "value")))))))
            {
                return false;
            }
            object v_seen_at__19149 = R.sub(R.number("0"), R.number("1"));
            object v_observation__19175 = false;
            object v_claims__19208 = F_dv_find(v_cell__19003, Authored("m56.island_case_numeric.literal"), new List<object> {  });
            object v_c__19320 = R.number("0");
            while (R.Bool(R.lt(v_c__19320, R.count(v_claims__19208))))
            {
                object v_claim__19380 = R.at(v_claims__19208, v_c__19320);
                if (R.Bool(DvValueTemplate.Instantiate("template.inline.island_case_numeric.1516", v_claim__19380, v_cell__19003, v_seen_at__19149)))
                {
                    v_seen_at__19149 = F_dv_get(v_claim__19380, "time");
                    v_observation__19175 = F_dv_get(v_claim__19380, "id");
                }
                v_c__19320 = R.add(v_c__19320, R.number("1"));
            }
            if (R.Bool(R.lt(v_seen_at__19149, R.number("0"))))
            {
                return false;
            }
            _ = R.push(v_values__18932, new List<object> { F_dv_pair("ref", R.at(v_refs, v_i__18953)), F_dv_pair("value", F_dv_get(v_cell__19003, "value")), F_dv_pair("seen_at", v_seen_at__19149), F_dv_pair("observation", v_observation__19175) });
            v_i__18953 = R.add(v_i__18953, R.number("1"));
        }
        return v_values__18932;
    }

    public static object F_island_case_with_evidence(object v_job,object v_evidence) {
        return DvValueTemplate.Instantiate("template.island_case_with_evidence", v_job, v_evidence);
    }

    public static object F_island_case_install(object v_scene) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.island_case_install.1517", v_scene)))
        {
            return v_scene;
        }
        object v_physical__32894 = F_dv_get(v_scene, "physical");
        v_physical__32894 = F_campaign_add_new(v_physical__32894, F_campaign_state_object(Authored("m56.island_case_install.literal_0002"), DvValueTemplate.Instantiate("template.inline.island_case_install.1518")));
        object v_fields__33211 = new List<object> { F_dv_pair("snapshot_done", F_dv_field(false)), F_dv_pair("challenge_done", F_dv_field(false)), F_dv_pair("defense_done", F_dv_field(false)), F_dv_pair("resolved", F_dv_field(false)) };
        object v_names__33361 = F_island_case_fields();
        object v_i__33399 = R.number("0");
        while (R.Bool(R.lt(v_i__33399, R.count(v_names__33361))))
        {
            if (R.Bool(R.not(F_dv_has_key(v_fields__33211, R.at(v_names__33361, v_i__33399)))))
            {
                v_fields__33211 = F_dv_with(v_fields__33211, R.at(v_names__33361, v_i__33399), F_dv_unknown());
            }
            v_i__33399 = R.add(v_i__33399, R.number("1"));
        }
        v_physical__32894 = F_campaign_add_new(v_physical__32894, F_island_display_object(Authored("m56.island_case_inspections.literal_0002"), Authored("m56.island_case_roles.literal_0005"), Authored("m56.island_case_install.literal_0003"), v_fields__33211));
        v_physical__32894 = F_campaign_add_new(v_physical__32894, F_island_display_object(Authored("m56.island_case_inspections.literal_0019"), Authored("m56.island_case_inspections.literal_0018"), Authored("m56.island_case_install.literal_0004"), new List<object> { F_dv_pair("done", F_dv_field(false)), F_dv_pair("signed_name", F_dv_unknown()), F_dv_pair("sample_seen_at", F_dv_unknown()), F_dv_pair("sample_reading", F_dv_unknown()), F_dv_pair("opening", F_dv_field(R.number("1"))), F_dv_pair("completed_at", F_dv_unknown()) }));
        object v_bindings__34359 = F_dv_get(v_scene, "bindings");
        object v_actors__34394 = new List<object> {  };
        object v_objects__34415 = AuthoredCopy("m56.island_case_install.objects");
        v_i__33399 = R.number("0");
        while (R.Bool(R.lt(v_i__33399, R.count(F_dv_get(v_scene, "actors")))))
        {
            object v_actor__34574 = R.at(F_dv_get(v_scene, "actors"), v_i__33399);
            if (R.Bool(F_dv_has(F_campaign_people(), F_dv_get(v_actor__34574, "id"))))
            {
                object v_mind__34664 = F_dv_get(v_actor__34574, "mind");
                object v_j__34699 = R.number("0");
                while (R.Bool(R.lt(v_j__34699, R.count(v_objects__34415))))
                {
                    if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_mind__34664, "objects"), R.at(v_objects__34415, v_j__34699)))))
                    {
                        v_mind__34664 = F_dv_add_object(v_mind__34664, F_campaign_local_object(R.at(v_objects__34415, v_j__34699)));
                    }
                    if (R.Bool(R.eq(F_dv_local(v_bindings__34359, R.at(v_objects__34415, v_j__34699), F_dv_get(v_actor__34574, "id")), "")))
                    {
                        v_bindings__34359 = F_dv_append(v_bindings__34359, F_dv_binding(R.at(v_objects__34415, v_j__34699), R.at(v_objects__34415, v_j__34699), F_dv_get(v_actor__34574, "id")));
                    }
                    v_j__34699 = R.add(v_j__34699, R.number("1"));
                }
                v_actor__34574 = F_dv_with(v_actor__34574, "mind", v_mind__34664);
            }
            _ = R.push(v_actors__34394, v_actor__34574);
            v_i__33399 = R.add(v_i__33399, R.number("1"));
        }
        object v_declarations__35195 = F_dv_with(F_dv_get(v_scene, "simulation_declarations"), "edges", F_island_case_edges(F_dv_get(F_dv_get(v_scene, "simulation_declarations"), "edges")));
        return F_dv_with_many(v_scene, new List<object> { F_dv_pair("physical", v_physical__32894), F_dv_pair("actors", v_actors__34394), F_dv_pair("bindings", v_bindings__34359), F_dv_pair("simulation_declarations", v_declarations__35195), F_dv_pair("island_case_installed", true) });
    }
    public static object F_island_case_stamp(object v_state) {
        if (R.Bool(DvValueTemplate.Instantiate("template.inline.island_case_stamp.1519", v_state)))
        {
            return v_state;
        }
        object v_pairs__35815 = DvValueTemplate.Instantiate("template.inline.island_case_stamp.1520");
        object v_effects__36816 = new List<object> {  };
        object v_premise__36838 = new List<object> {  };
        object v_basis__36860 = new List<object> {  };
        object v_i__36880 = R.number("0");
        while (R.Bool(R.lt(v_i__36880, R.count(v_pairs__35815))))
        {
            object v_item__36931 = R.at(v_pairs__35815, v_i__36880);
            object v_cell__36960 = F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(v_item__36931, "flag"));
            if (R.Bool((R.Bool((R.Bool(F_dv_uncontested(v_cell__36960)) ? (object)(R.eq(F_dv_get(v_cell__36960, "value"), true)) : (object)(false))) ? (object)(R.not(F_dv_uncontested(F_dv_read(F_dv_get(v_state, "physical"), F_dv_get(v_item__36931, "stamp"))))) : (object)(false))))
            {
                _ = R.push(v_effects__36816, F_dv_assign(F_dv_get(v_item__36931, "stamp"), F_dv_get(v_cell__36960, "time")));
                _ = R.push(v_premise__36838, F_dv_fact(F_dv_get(v_item__36931, "flag"), true));
                _ = R.push(v_basis__36860, new List<object> { F_dv_pair("ref", F_dv_get(v_item__36931, "flag")), F_dv_pair("completed_cell", v_cell__36960) });
            }
            v_i__36880 = R.add(v_i__36880, R.number("1"));
        }
        if (R.Bool(R.eq(R.count(v_effects__36816), R.number("0"))))
        {
            return v_state;
        }
        object v_law__37393 = DvValueTemplate.Instantiate("template.inline.island_case_stamp.1521", v_premise__36838, v_effects__36816);
        object v_world__37521 = F_island_case_adopt(F_dv_get(v_state, "physical"), v_law__37393, "enforce");
        object v_execution__37588 = F_dv_execute(v_world__37521, F_dv_get(v_law__37393, "name"), R.cat(F_dv_get(v_law__37393, "name"), R.cat(Authored("m56.island_case_allocate.literal_0010"), R.show(R.count(F_dv_get(v_state, "events"))))), F_dv_get(v_world__37521, "time"), F_dv_get(v_world__37521, "revision"));
        object v_events__37802 = F_dv_clone(F_dv_get(v_state, "events"));
        _ = F_dvs_event(v_events__37802, Authored("m56.island_case_stamp.literal_0006"), "", F_dv_get(v_world__37521, "time"), new List<object> {  }, new List<object> { F_dv_pair("basis", v_basis__36860), F_dv_pair("execution", F_dv_without(v_execution__37588, "world")), F_dv_pair("knowledge_granted", false) });
        return F_dvs_owned_state_update(v_state, new List<object> { F_dv_pair("physical", F_dv_get(v_execution__37588, "world")), F_dv_pair("events", v_events__37802) });
    }
    public static object F_island_case_account_evidence(object v_actor) {
        object v_refs__38343 = DvValueTemplate.Instantiate("template.inline.island_case_account_evidence.1522");
        object v_values__39266 = F_island_case_numeric(F_dv_get(v_actor, "mind"), v_refs__38343);
        if (R.Bool(R.eq(v_values__39266, false)))
        {
            return false;
        }
        if (R.Bool((R.Bool(R.lt(F_dv_get(R.at(v_values__39266, R.number("5")), "seen_at"), F_dv_get(R.at(v_values__39266, R.number("3")), "value"))) ? (object)(true) : (object)(R.lt(F_dv_get(R.at(v_values__39266, R.number("6")), "seen_at"), F_dv_get(R.at(v_values__39266, R.number("4")), "value"))))))
        {
            return false;
        }
        object v_removed__39475 = R.sub(F_dv_get(R.at(v_values__39266, R.number("6")), "value"), F_dv_get(R.at(v_values__39266, R.number("1")), "value"));
        object v_received__39528 = R.sub(F_dv_get(R.at(v_values__39266, R.number("7")), "value"), F_dv_get(R.at(v_values__39266, R.number("2")), "value"));
        if (R.Bool((R.Bool((R.Bool(R.lt(v_removed__39475, R.number("0"))) ? (object)(true) : (object)(R.lt(v_received__39528, R.number("0"))))) ? (object)(true) : (object)(R.not(R.eq(R.sub(R.add(F_dv_get(R.at(v_values__39266, R.number("0")), "value"), v_received__39528), v_removed__39475), F_dv_get(R.at(v_values__39266, R.number("5")), "value")))))))
        {
            return false;
        }
        return new List<object> { F_dv_pair("values", v_values__39266), F_dv_pair("removed", v_removed__39475), F_dv_pair("received", v_received__39528), F_dv_pair("counter_at", F_dv_get(R.at(v_values__39266, R.number("5")), "seen_at")), F_dv_pair("ledger_at", F_dv_get(R.at(v_values__39266, R.number("6")), "seen_at")) };
    }
    public static object F_island_case_reconcile(object v_actor) {
        v_actor = F_c2_query_view(v_actor, v_actor);
        object v_evidence__39915 = F_island_case_account_evidence(v_actor);
        if (R.Bool(R.eq(v_evidence__39915, false)))
        {
            return false;
        }
        object v_values__40020 = F_dv_get(v_evidence__39915, "values");
        object v_removed__40054 = F_dv_get(v_evidence__39915, "removed");
        object v_received__40090 = F_dv_get(v_evidence__39915, "received");
        object v_known__40128 = DvValueTemplate.Instantiate("template.inline.island_case_reconcile.1523");
        object v_i__40785 = R.number("0");
        while (R.Bool(R.lt(v_i__40785, R.count(v_values__40020))))
        {
            _ = R.push(v_known__40128, F_dv_fact(F_dv_get(R.at(v_values__40020, v_i__40785), "ref"), F_dv_get(R.at(v_values__40020, v_i__40785), "value")));
            v_i__40785 = R.add(v_i__40785, R.number("1"));
        }
        object v_premise__40917 = DvValueTemplate.Instantiate("template.inline.island_case_reconcile.1524", v_known__40128, v_actor);
        object v_id__41059 = R.cat(F_dv_get(v_actor, "id"), Authored("m56.island_case_reconcile.literal"));
        object v_law__41164 = DvValueTemplate.Instantiate("template.inline.island_case_reconcile.1525", v_id__41059, v_premise__40917, v_actor);
        object v_explanation__41350 = R.cat(Authored("m56.island_case_reconcile.literal_0002"), R.cat(R.show(v_removed__40054), R.cat(Authored("m56.island_case_reconcile.literal_0003"), R.cat(R.show(v_received__40090), Authored("m56.island_case_reconcile.literal_0004")))));
        object v_action__41678 = DvValueTemplate.Instantiate("template.inline.island_case_reconcile.1526", v_id__41059, v_premise__40917, v_law__41164, v_explanation__41350);
        return F_island_case_with_evidence(new List<object> { F_dv_pair("law", v_law__41164), F_dv_pair("action", v_action__41678) }, v_values__40020);
    }
    public static object F_island_case_target(object v_actor,object v_role) {
        return DvValueTemplate.Instantiate("template.island_case_target", v_actor, v_role);
    }
    public static object F_island_case_observation_utility(object actor, object action, object role)
    {
        actor = F_c2_query_view(actor, actor);
        action = F_c2_query_view(actor, action);
        var groups = DvValueTemplate.Instantiate("template.island_case_observation_groups", actor, role);
        var minimum = DvValueTemplate.Instantiate("template.island_case_observation_minimum", actor);
        return IslandCaseObservationUtility(actor, action, role, groups, minimum);
    }

    // Goal admission and the observation bonus use the same real perception
    // receipts. Stage selection and the validity period remain authored in DV.
    private static object IslandCaseObservationUtility(object actor, object action, object role, object groups, object minimum)
    {
        if (R.Bool(R.eq(role, ""))) return 0d;
        var grants = F_dv_get(action, "grants");
        var mind = F_dv_get(actor, "mind");
        foreach (var group in R.Items(groups))
            foreach (var reference in R.Items(F_dv_get(group, "refs")))
                if (R.Bool(F_dv_has(grants, reference)) &&
                    !R.Bool(F_dvp_source_supported_since(F_dv_read(mind, reference), "perception", minimum)))
                    return F_dv_get(F_island_case_parameters(), "observation_utility");
        return 0d;
    }

    public static object F_island_case_pending_route(object actor, object role, object jobs, object inspections)
    {
        actor = F_c2_query_view(actor, actor);
        jobs = F_c2_query_view(actor, jobs);
        inspections = F_c2_query_view(actor, inspections);
        var groups = DvValueTemplate.Instantiate("template.island_case_observation_groups", actor, role);
        var minimum = DvValueTemplate.Instantiate("template.island_case_observation_minimum", actor);
        return IslandCasePendingRoute(actor, role, jobs, inspections, groups, minimum);
    }

    private static object IslandCasePendingRoute(object actor, object role, object jobs, object inspections, object groups, object minimum)
    {
        var route = F_c2i_known_route(actor, F_island_case_target(actor, role));
        if (R.Bool(R.eq(route, false))) return false;
        foreach (var job in R.Items(jobs))
        {
            var action = F_dv_get(job, "action");
            var goal = F_dv_with(F_campaign_goal(F_dv_true(), F_dv_true(), new List<object> { F_island_source() }),
                "navigation_methods", new List<object> { F_dv_get(action, "id") });
            if (R.Bool(F_c2_navigation_route_ready(actor, goal, route))) return route;
        }
        foreach (var action in R.Items(inspections))
        {
            if (!R.Bool(R.lt(0d, IslandCaseObservationUtility(actor, action, role, groups, minimum)))) continue;
            var grants = F_dv_get(action, "grants");
            foreach (var group in R.Items(groups))
                foreach (var reference in R.Items(F_dv_get(group, "refs")))
                {
                    if (!R.Bool(F_dv_has(grants, reference))) continue;
                    var goal = F_dv_with(F_campaign_observation_goal(reference, F_dv_true(),
                        new List<object> { F_island_source() }, minimum), "navigation_methods",
                        new List<object> { F_dv_get(action, "id") });
                    if (R.Bool(F_c2_navigation_pending(actor, goal)) &&
                        R.Bool(F_c2_navigation_route_ready(actor, goal, route))) return route;
                }
        }
        return false;
    }
    public static object F_island_case_selected_route(object v_actor,object v_route) {
        return DvValueTemplate.Instantiate("template.island_case_selected_route", v_actor, v_route);
    }
    public static object F_island_case_refresh(object v_state) {
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_find(F_dv_get(v_state,"story"),Authored("m56.island_case_allocate.literal"),R.number("1")),Authored("m56.island_case_allocate.literal_0002")))) ? (object)(true) : (object)(R.lt(F_dv_get(F_dv_get(v_state,"physical"),"time"),F_dv_get(F_island_case_parameters(),"start_minute"))))) ? (object)(true) : (object)(R.not(F_dv_find(F_dv_get(v_state,"story"),Authored("m56.island_case_install.literal"),false)))))) {
            return v_state;
        }
        v_state = F_island_case_allocate(F_island_case_stamp(v_state));
        object v_resolved__47779 = R.eq(F_dv_truth(F_dv_get(v_state,"physical"),F_dv_fact(F_island_case_ref(Authored("m56.island_case_allocate.literal_0003")),true)),"true");
        object v_physical__47944 = F_dv_get(v_state,"physical");
        object v_bindings__47979 = F_dv_get(v_state,"bindings");
        object v_actors__48014 = F_dv_get(v_state,"actors");
        object v_index__48045 = F_dv_get(v_state,"cursor");
        object v_actor__48075 = R.at(v_actors__48014,v_index__48045);
        if (R.Bool((R.Bool(R.not(F_dv_has(F_campaign_people(),F_dv_get(v_actor__48075,"id")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_actor__48075,Authored("m56.island_case_refresh.literal"),false),false)))))) {
            return v_state;
        }
        object v_mind__48283 = F_dv_get(v_actor__48075,"mind");
        object v_objects__48310 = AuthoredCopy("m56.island_case_install.objects");
        object v_i__48415 = R.number("0");
        while (R.Bool(R.lt(v_i__48415,R.count(v_objects__48310)))) {
            if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_mind__48283,"objects"),R.at(v_objects__48310,v_i__48415))))) {
                v_mind__48283 = F_dv_add_object(v_mind__48283,F_campaign_local_object(R.at(v_objects__48310,v_i__48415)));
            }
            if (R.Bool(R.eq(F_dv_local(v_bindings__47979,R.at(v_objects__48310,v_i__48415),F_dv_get(v_actor__48075,"id")),""))) {
                v_bindings__47979 = F_dv_append(v_bindings__47979,F_dv_binding(R.at(v_objects__48310,v_i__48415),R.at(v_objects__48310,v_i__48415),F_dv_get(v_actor__48075,"id")));
            }
            v_i__48415 = R.add(v_i__48415,R.number("1"));
        }
        v_actor__48075 = F_c2_query_view(v_actor__48075,F_dv_with(v_actor__48075,"mind",v_mind__48283));
        object v_role__48826 = "";
        object v_roles__48845 = F_island_case_roles();
        v_i__48415 = R.number("0");
        while (R.Bool(R.lt(v_i__48415,R.count(v_roles__48845)))) {
            if (R.Bool((R.Bool(R.not(v_resolved__47779)) ? (object)(R.eq(F_dv_truth(v_physical__47944,F_dv_fact(F_island_case_director(F_dv_get(R.at(v_roles__48845,v_i__48415),"id")),F_dv_get(v_actor__48075,"id"))),"true")) : (object)(false)))) {
                v_role__48826 = F_dv_get(R.at(v_roles__48845,v_i__48415),"id");
            }
            v_i__48415 = R.add(v_i__48415,R.number("1"));
        }
        object v_actions__49089 = new System.Collections.Generic.List<object>(0) {};
        v_i__48415 = R.number("0");
        while (R.Bool(R.lt(v_i__48415,R.count(F_dv_get(v_actor__48075,"actions"))))) {
            object v_action__49166 = R.at(F_dv_get(v_actor__48075,"actions"),v_i__48415);
            if (R.Bool(R.not(F_dv_find(v_action__49166,Authored("m56.island_case_refresh.literal_0002"),false)))) {
                if (R.Bool(F_dv_has_key(v_action__49166,"island_case_original_utility"))) {
                    v_action__49166 = F_dv_without(F_dv_with(v_action__49166,Authored("m56.island_case_refresh.literal_0003"),F_dv_get(v_action__49166,"island_case_original_utility")),"island_case_original_utility");
                }
                _ = R.push(v_actions__49089,v_action__49166);
            }
            v_i__48415 = R.add(v_i__48415,R.number("1"));
        }
        object v_inspections__49643 = F_island_case_inspections(F_dv_get(v_actor__48075,"id"));
        object v_evidence__50359 = DvValueTemplate.Instantiate("template.island_case_observation_groups", v_actor__48075, v_role__48826);
        object v_minimum_time__50285 = DvValueTemplate.Instantiate("template.island_case_observation_minimum", v_actor__48075);
        object v_goals__49700 = R.Block(() => { object comp_source_49712 = F_dv_find(F_dv_get(v_actor__48075,"policy"),Authored("m56.island_case_selected_route.literal"),new System.Collections.Generic.List<object>(0) {});var comp_result_49712 = new System.Collections.Generic.List<object>();for (int comp_i_49712 = 0; comp_i_49712 < R.Items(comp_source_49712).Count; comp_i_49712++) {object v_goal__comp_49712 = R.at(comp_source_49712, (double)comp_i_49712);if (R.Bool((R.Bool(R.not(F_dv_find(v_goal__comp_49712,Authored("m56.island_case_refresh.literal_0002"),false))) ? (object)(R.not(F_dv_has_key(v_goal__comp_49712,Authored("m56.island_case_selected_route.literal_0002")))) : (object)(false)))) comp_result_49712.Add(v_goal__comp_49712);}return (object)comp_result_49712;});
        if (R.Bool((R.Bool(R.not(v_resolved__47779)) ? (object)((R.Bool(R.not(R.eq(v_role__48826,""))) ? (object)(true) : (object)(R.eq(F_dv_find(v_state,Authored("m56.island_case_refresh.literal_0004"),""),F_dv_get(v_actor__48075,"id"))))) : (object)(false)))) {
            object v_e__52023 = R.number("0");
            while (R.Bool(R.lt(v_e__52023,R.count(v_evidence__50359)))) {
                object v_f__52085 = R.number("0");
                while (R.Bool(R.lt(v_f__52085,R.count(F_dv_get(R.at(v_evidence__50359,v_e__52023),"refs"))))) {
                    object v_goal__52163 = F_campaign_observation_goal(R.at(F_dv_get(R.at(v_evidence__50359,v_e__52023),"refs"),v_f__52085),F_campaign_at(F_dv_get(v_actor__48075,"id"),F_dv_get(R.at(v_evidence__50359,v_e__52023),"room")),new System.Collections.Generic.List<object>(1) {F_island_source()},v_minimum_time__50285);
                    object v_methods__52312 = R.Block(() => { object comp_source_52326 = v_inspections__49643;var comp_result_52326 = new System.Collections.Generic.List<object>();for (int comp_i_52326 = 0; comp_i_52326 < R.Items(comp_source_52326).Count; comp_i_52326++) {object v_action__comp_52326 = R.at(comp_source_52326, (double)comp_i_52326);if (R.Bool(F_dv_has(F_dv_get(v_action__comp_52326,"grants"),R.at(F_dv_get(R.at(v_evidence__50359,v_e__52023),"refs"),v_f__52085)))) comp_result_52326.Add(F_dv_get(v_action__comp_52326,"id"));}return (object)comp_result_52326;});
                    _ = R.push(v_goals__49700,F_dv_with_many(v_goal__52163,new System.Collections.Generic.List<object>(3) {F_dv_pair("island_dynamic",true),F_dv_pair("navigation_room",F_dv_get(R.at(v_evidence__50359,v_e__52023),"room")),F_dv_pair("navigation_methods",v_methods__52312)}));
                    v_f__52085 = R.add(v_f__52085,R.number("1"));
                }
                v_e__52023 = R.add(v_e__52023,R.number("1"));
            }
        }
        v_i__48415 = R.number("0");
        while (R.Bool(R.lt(v_i__48415,R.count(v_inspections__49643)))) {
            _ = R.push(v_actions__49089,F_dv_with(R.at(v_inspections__49643,v_i__48415),Authored("m56.island_case_refresh.literal_0003"),IslandCaseObservationUtility(v_actor__48075,R.at(v_inspections__49643,v_i__48415),v_role__48826,v_evidence__50359,v_minimum_time__50285)));
            v_i__48415 = R.add(v_i__48415,R.number("1"));
        }
        object v_jobs__52870 = new System.Collections.Generic.List<object>(0) {};
        if (R.Bool(R.not(R.eq(v_role__48826,"")))) {
            v_jobs__52870 = F_island_case_jobs(v_actor__48075,v_role__48826);
        }
        if (R.Bool(R.not(v_resolved__47779))) {
            object v_reconciliation__52981 = F_island_case_reconcile(v_actor__48075);
            if (R.Bool(R.not(R.eq(v_reconciliation__52981,false)))) {
                _ = R.push(v_jobs__52870,v_reconciliation__52981);
            }
        }
        v_i__48415 = R.number("0");
        while (R.Bool(R.lt(v_i__48415,R.count(v_jobs__52870)))) {
            v_physical__47944 = F_island_case_adopt(v_physical__47944,F_dv_get(R.at(v_jobs__52870,v_i__48415),"law"),"enforce");
            _ = R.push(v_actions__49089,F_dv_get(R.at(v_jobs__52870,v_i__48415),"action"));
            object v_action__53276 = F_dv_get(R.at(v_jobs__52870,v_i__48415),"action");
            object v_room__53313 = F_dvp_extract_required_room(F_dv_get(v_action__53276,"precondition"),F_dv_get(v_actor__48075,"location_ref"));
            if (R.Bool((R.Bool(R.not(R.eq(v_room__53313,""))) ? (object)(R.lt(R.number("0"),R.count(F_dv_get(v_action__53276,"grants")))) : (object)(false)))) {
                object v_goal__53460 = F_campaign_goal(F_dv_fact(R.at(F_dv_get(v_action__53276,"grants"),R.number("0")),true),F_c2_navigation_condition(F_dv_get(v_action__53276,"precondition"),F_dv_get(v_actor__48075,"id")),new System.Collections.Generic.List<object>(1) {F_island_source()});
                _ = R.push(v_goals__49700,F_dv_with_many(v_goal__53460,new System.Collections.Generic.List<object>(4) {F_dv_pair("island_dynamic",true),F_dv_pair("weight",R.number("0")),F_dv_pair("navigation_room",v_room__53313),F_dv_pair("navigation_methods",new System.Collections.Generic.List<object>(1) {F_dv_get(v_action__53276,"id")})}));
            }
            v_i__48415 = R.add(v_i__48415,R.number("1"));
        }
        v_actor__48075 = F_c2_query_view(v_actor__48075,F_dv_with_many(v_actor__48075,new System.Collections.Generic.List<object>(2) {F_dv_pair("actions",v_actions__49089),F_dv_pair("policy",F_dv_with(F_dv_get(v_actor__48075,"policy"),Authored("m56.island_case_selected_route.literal"),v_goals__49700))}));
        object navigationGoals = F_c2_navigation_goals(v_actor__48075, v_goals__49700, out object navigationDiagnostic);
        v_actor__48075 = F_campaign_request_withdrawals(F_campaign_navigation_frontier(F_dv_with_many(v_actor__48075, new List<object> { F_dv_pair("policy", F_dv_with(F_dv_get(v_actor__48075, "policy"), Authored("m56.island_case_selected_route.literal"), navigationGoals)), F_dv_pair("navigation_route_diagnostic", navigationDiagnostic) })));
        if (R.Bool(R.not(R.eq(v_role__48826,"")))) {
            object v_route__54311 = IslandCasePendingRoute(v_actor__48075,v_role__48826,v_jobs__52870,v_inspections__49643,v_evidence__50359,v_minimum_time__50285);
            if (R.Bool((R.Bool(R.not(R.eq(v_route__54311,false))) ? (object)(F_island_case_selected_route(v_actor__48075,v_route__54311)) : (object)(false)))) {
                v_i__48415 = R.number("0");
                while (R.Bool(R.lt(v_i__48415,R.count(v_actions__49089)))) {
                    object v_action__54532 = R.at(v_actions__49089,v_i__48415);
                    object v_travel__54573 = F_dv_find(v_action__54532,Authored("m56.island_case_refresh.literal_0005"),new System.Collections.Generic.List<object>(0) {});
                    if (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_action__54532,"kind"),"move")) ? (object)(R.eq(F_dv_find(v_travel__54573,Authored("m56.island_case_refresh.literal_0006"),""),F_dv_get(v_route__54311,"origin"))) : (object)(false))) ? (object)(R.eq(F_dv_find(v_travel__54573,Authored("m56.island_case_refresh.literal_0007"),""),F_dv_get(v_route__54311,"next"))) : (object)(false)))) {
                        object v_original__54979 = F_dv_find(v_action__54532,Authored("m56.island_case_refresh.literal_0003"),R.number("0"));
                        v_actions__49089 = F_dvs_replace(v_actions__49089,v_i__48415,F_dv_with_many(v_action__54532,new System.Collections.Generic.List<object>(2) {F_dv_pair("island_case_original_utility",v_original__54979),F_dv_pair("simulation_utility",R.add(v_original__54979,F_dv_get(F_island_case_parameters(),"travel_utility")))}));
                    }
                    v_i__48415 = R.add(v_i__48415,R.number("1"));
                }
            }
        }
        v_actor__48075 = F_dv_with_many(v_actor__48075,new System.Collections.Generic.List<object>(2) {F_dv_pair("actions",v_actions__49089),F_dv_pair("island_case_intention",new System.Collections.Generic.List<object>(3) {F_dv_pair("role",v_role__48826),F_dv_pair("basis",Authored("m56.island_case_refresh.literal_0008")),F_dv_pair("knowledge_granted",false)})});
        return F_dvs_owned_state_update(v_state,new System.Collections.Generic.List<object>(3) {F_dv_pair("physical",v_physical__47944),F_dv_pair("actors",F_dvs_replace(v_actors__48014,v_index__48045,v_actor__48075)),F_dv_pair("bindings",v_bindings__47979)});
    }
    public static object F_island_case_edges(object v_edges) {
        return DvValueTemplate.Instantiate("template.island_case_edges", v_edges);
    }
}
