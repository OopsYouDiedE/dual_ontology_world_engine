using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>World / State: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dv_world(object v_id, object v_domain, object v_subject)
    {
        _ = F_dv_require(F_dv_or(R.eq(v_domain, "physical"), R.eq(v_domain, "mind")), "Invalid world domain");
        object v_local_1__5041 = false;
        if (R.Bool(R.eq(v_domain, "physical")))
        {
            v_local_1__5041 = R.eq(v_subject, "");
        }
        else
        {
            v_local_1__5041 = R.lt(R.number("0"), R.length(v_subject));
        }
        _ = F_dv_require(v_local_1__5041, "World subject mismatch");
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("domain", v_domain), F_dv_pair("subject", v_subject), F_dv_pair("revision", R.number("0")) }, new System.Collections.Generic.List<object>(4) { F_dv_pair("objects", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("adoptions", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("history", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("seen", new System.Collections.Generic.List<object>(0) { }) });
    }

    public static object F_dv_mask32(object v_mask)
    {
        _ = F_dv_require((R.Bool((R.Bool(R.not(R.lt(v_mask, R.number("0")))) ? (object)(R.not(R.lt(R.number("4294967295"), v_mask))) : (object)(false))) ? (object)(R.eq(R.floor(v_mask), v_mask)) : (object)(false)), "Mask must be an unsigned 32-bit integer");
        return v_mask;
    }

    public static object F_dv_mask_bit(object v_index)
    {
        _ = F_dv_require((R.Bool((R.Bool(R.not(R.lt(v_index, R.number("0")))) ? (object)(R.lt(v_index, R.number("32"))) : (object)(false))) ? (object)(R.eq(R.floor(v_index), v_index)) : (object)(false)), "Mask index outside 32 identities");
        object v_bit__5836 = R.number("1");
        object v_i__5852 = R.number("0");
        while (R.Bool(R.lt(v_i__5852, v_index)))
        {
            v_bit__5836 = R.mul(v_bit__5836, R.number("2"));
            v_i__5852 = R.add(v_i__5852, R.number("1"));
        }
        return v_bit__5836;
    }

    public static object F_dv_mask_has(object v_mask, object v_index)
    {
        object v_quotient__5972 = R.floor(R.div(F_dv_mask32(v_mask), F_dv_mask_bit(v_index)));
        return R.eq(R.sub(v_quotient__5972, R.mul(R.floor(R.div(v_quotient__5972, R.number("2"))), R.number("2"))), R.number("1"));
    }

    public static object F_dv_mask_and(object v_left, object v_right)
    {
        _ = F_dv_mask32(v_left);
        _ = F_dv_mask32(v_right);
        object v_result__6163 = R.number("0");
        object v_i__6182 = R.number("0");
        while (R.Bool(R.lt(v_i__6182, R.number("32"))))
        {
            if (R.Bool((R.Bool(F_dv_mask_has(v_left, v_i__6182)) ? (object)(F_dv_mask_has(v_right, v_i__6182)) : (object)(false))))
            {
                v_result__6163 = R.add(v_result__6163, F_dv_mask_bit(v_i__6182));
            }
            v_i__6182 = R.add(v_i__6182, R.number("1"));
        }
        return v_result__6163;
    }

    public static object F_dv_mask_union(object v_left, object v_right)
    {
        return R.sub(R.add(F_dv_mask32(v_left), F_dv_mask32(v_right)), F_dv_mask_and(v_left, v_right));
    }

    public static object F_dv_mask_without(object v_mask, object v_excluded)
    {
        return R.sub(F_dv_mask32(v_mask), F_dv_mask_and(v_mask, v_excluded));
    }

    public static object F_dv_reality_fact(object v_id, object v_ref, object v_cell, object v_known_by)
    {
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("ref", v_ref), F_dv_pair("RealityFact", v_cell), F_dv_pair("KnownByMask", F_dv_mask32(v_known_by)) };
    }

    public static object F_dv_domain(object v_world, object v_domain)
    {
        return F_dv_require(R.eq(F_dv_get(v_world, "domain"), v_domain), "Wrong world domain");
    }

    public static object F_dv_object(object v_id, object v_kind, object v_facets)
    {
        object v_local_1__7355 = false;
        if (R.Bool(R.eq(v_kind, "perceived")))
        {
            v_local_1__7355 = F_dv_unknown();
        }
        else
        {
            v_local_1__7355 = F_dv_field(v_kind);
        }
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("id", v_id), F_dv_pair("kind", v_kind), F_dv_pair("facets", F_dv_with(v_facets, "identity", F_dv_with(F_dv_find(v_facets, "identity", new System.Collections.Generic.List<object>(0) { }), "kind", v_local_1__7355))) };
    }

    public static object F_dv_add_object(object v_world, object v_object)
    {
        object v_objects__7651 = F_dv_get(v_world, "objects");
        object v_id__7683 = F_dv_get(v_object, "id");
        _ = F_dv_require(R.not(F_dv_has_key(v_objects__7651, v_id__7683)), "Duplicate object identity");
        return F_dv_with_many(v_world, new System.Collections.Generic.List<object>(2) { F_dv_pair("objects", F_dv_with(v_objects__7651, v_id__7683, v_object)), F_dv_pair("revision", R.add(F_dv_get(v_world, "revision"), R.number("1"))) });
    }

    public static object F_dv_read(object v_world, object v_ref)
        => ReadCell(v_world, v_ref, false);

    // Same subject/hidden/object/field checks as an ordinary read, with a
    // frozen result for callers that only inspect evidence and never edit it.
    public static object F_dv_read_query(object world, object reference)
        => R.snapshot(ReadCell(R.snapshot(world), R.snapshot(reference), true));

    private static object ReadCell(object v_world, object v_ref, bool readOnly)
    {
        if (R.Bool(R.read_ref_view_safe(v_ref)))
        {
            v_world = R.read_view(v_world);
        }
        if (R.Bool(F_dv_has_key(v_ref, "subject")))
        {
            object v_subj__8173 = F_dv_get(v_ref, "subject");
            if (R.Bool(R.not(R.eq(v_subj__8173, ""))))
            {
                _ = F_dv_require(R.eq(v_subj__8173, F_dv_get(v_world, "subject")), "Cross-subject reference access");
            }
        }
        if (R.Bool((R.Bool(R.eq(F_dv_get(v_world, "domain"), "mind")) ? (object)(F_dv_cognition_hidden(v_world, F_dv_get(v_ref, "object"))) : (object)(false))))
        {
            return F_dv_unknown();
        }
        object v_objects__8415 = F_dv_get(v_world, "objects");
        object v_id__8447 = F_dv_get(v_ref, "object");
        if (R.Bool(R.not(F_dv_has_key(v_objects__8415, v_id__8447))))
        {
            return F_dv_unknown();
        }
        object v_object__8690 = F_dv_get(v_objects__8415, v_id__8447);
        object v_key__8727 = F_dv_list3("dv_read-object-field-v2", F_dv_get(v_ref, "facet"), F_dv_get(v_ref, "field"));
        object v_cached__8791 = readOnly ? R.memo_read(v_object__8690, v_key__8727)
            : R.memo_get(v_object__8690, v_key__8727);
        if (R.Bool(R.at(v_cached__8791, R.number("0"))))
        {
            return R.at(v_cached__8791, R.number("1"));
        }
        return R.memo_put(v_object__8690, v_key__8727, ReadFields(v_objects__8415, v_id__8447, v_ref, readOnly));
    }

    public static object F_dv_read_fields(object v_objects, object v_id, object v_ref)
        => ReadFields(v_objects, v_id, v_ref, false);

    private static object ReadFields(object v_objects, object v_id, object v_ref, bool readOnly)
    {
        if (R.Bool(F_dv_has_key(v_objects, v_id)))
        {
            object v_facets__9020 = F_dv_get(F_dv_get(v_objects, v_id), "facets");
            object v_facet__9068 = F_dv_get(v_ref, "facet");
            if (R.Bool(F_dv_has_key(v_facets__9020, v_facet__9068)))
            {
                object v_fields__9140 = F_dv_get(v_facets__9020, v_facet__9068);
                object v_field_key__9187 = F_dv_get(v_ref, "field");
                if (R.Bool(F_dv_has_key(v_fields__9140, v_field_key__9187)))
                {
                    var cell = F_dv_get(v_fields__9140, v_field_key__9187);
                    return readOnly ? R.snapshot(cell) : F_dv_clone(cell);
                }
                else
                {
                    return F_dv_unknown();
                }
            }
            else
            {
                return F_dv_unknown();
            }
        }
        else
        {
            return F_dv_unknown();
        }
    }

    public static object F_dv_store(object v_world, object v_ref, object v_cell)
    {
        object v_objects__9492 = F_dv_get(v_world, "objects");
        object v_id__9524 = F_dv_get(v_ref, "object");
        if (R.Bool(R.not(F_dv_has_key(v_objects__9492, v_id__9524))))
        {
            _ = F_dv_require(false, R.cat("Unknown object identity: ", R.json_stringify(new System.Collections.Generic.List<object>(7) { F_dv_pair("operation", "dv_store"), F_dv_pair("object", v_id__9524), F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("domain", F_dv_get(v_world, "domain")), F_dv_pair("subject", F_dv_get(v_world, "subject")), F_dv_pair("facet", F_dv_get(v_ref, "facet")), F_dv_pair("field", F_dv_get(v_ref, "field")) })));
        }
        object v_object__9899 = F_dv_get(v_objects__9492, v_id__9524);
        object v_facets__9936 = F_dv_get(v_object__9899, "facets");
        object v_facet__9967 = F_dv_get(v_ref, "facet");
        object v_fields__9993 = F_dv_find(v_facets__9936, v_facet__9967, new System.Collections.Generic.List<object>(0) { });
        object v_updated_fields__10037 = F_dv_with(v_fields__9993, F_dv_get(v_ref, "field"), v_cell);
        object v_updated_object__10095 = F_dv_with(v_object__9899, "facets", F_dv_with(v_facets__9936, v_facet__9967, v_updated_fields__10037));
        return F_dv_with(v_world, "objects", F_dv_with(v_objects__9492, v_id__9524, v_updated_object__10095));
    }

    public static object F_dv_slice(object v_world, object v_object_id, object v_facet, object v_fields)
    {
        object v_result__10308 = new System.Collections.Generic.List<object>(0) { };
        object v_i__10328 = R.number("0");
        while (R.Bool(R.lt(v_i__10328, R.count(v_fields))))
        {
            object v_field__10380 = R.at(v_fields, v_i__10328);
            _ = R.push(v_result__10308, F_dv_pair(v_field__10380, F_dv_read(v_world, F_dv_ref(v_object_id, v_facet, v_field__10380))));
            v_i__10328 = R.add(v_i__10328, R.number("1"));
        }
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("domain", F_dv_get(v_world, "domain")), F_dv_pair("subject", F_dv_get(v_world, "subject")), F_dv_pair("revision", F_dv_get(v_world, "revision")) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("object", v_object_id), F_dv_pair("facet", v_facet), F_dv_pair("fields", v_result__10308) });
    }

    public static object F_dv_field(object v_value)
    {
        return F_dv_cell(v_value, R.number("1"), R.number("0"), new System.Collections.Generic.List<object>(0) { });
    }

    public static object F_dv_uncontested(object v_cell)
    {
        if (R.Bool(R.mask_enabled()))
        {
            return R.cell_uncontested(v_cell);
        }
        return F_dv_and(F_dv_get(v_cell, "known"), R.not(F_dv_get(v_cell, "conflicted")));
    }

    public static object F_dv_door(object v_id, object v_from, object v_to, object v_closed, object v_held, object v_inside, object v_outside, object v_automatic, object v_gap)
    {
        return F_dv_object(v_id, "door", new System.Collections.Generic.List<object>(2) { F_dv_pair("link", new System.Collections.Generic.List<object>(2) { F_dv_pair("from", F_dv_field(F_dv_entity_ref(v_from))), F_dv_pair("to", F_dv_field(F_dv_entity_ref(v_to))) }), F_dv_pair("mechanical", F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("closed", F_dv_field(v_closed)), F_dv_pair("held", F_dv_field(v_held)), F_dv_pair("inside_latch", F_dv_field(v_inside)), F_dv_pair("outside_latch", F_dv_field(v_outside)) }, new System.Collections.Generic.List<object>(3) { F_dv_pair("automatic", F_dv_field(v_automatic)), F_dv_pair("gap", F_dv_field(v_gap)), F_dv_pair("sealed", F_dv_field(false)) })) });
    }

    public static object F_dv_channel(object v_id, object v_from, object v_to, object v_medium, object v_enabled, object v_gain, object v_gate)
    {
        _ = F_dv_unit(v_gain);
        return F_dv_object(v_id, "channel", new System.Collections.Generic.List<object>(2) { F_dv_pair("link", new System.Collections.Generic.List<object>(4) { F_dv_pair("from", F_dv_field(F_dv_entity_ref(v_from))), F_dv_pair("to", F_dv_field(F_dv_entity_ref(v_to))), F_dv_pair("medium", F_dv_field(v_medium)), F_dv_pair("gate", F_dv_field(v_gate)) }), F_dv_pair("transport", new System.Collections.Generic.List<object>(3) { F_dv_pair("enabled", F_dv_field(v_enabled)), F_dv_pair("gain", F_dv_field(v_gain)), F_dv_pair("bands", F_dv_field(new System.Collections.Generic.List<object>(0) { })) }) });
    }

    public static object F_dv_stage_store_owned(object v_owned, object v_ref, object v_cell)
    {
        if (R.Bool((R.Bool(R.cow_enabled()) ? (object)(true) : (object)(R.stateindex_enabled()))))
        {
            object v_fast__13029 = R.stage_store_cow(v_owned, v_ref, v_cell);
            if (R.Bool(R.not(R.eq(v_fast__13029, false))))
            {
                return v_fast__13029;
            }
        }
        object v_objects__13131 = F_dv_get(v_owned, "objects");
        if (R.Bool(R.not(F_dv_has_key(v_objects__13131, F_dv_get(v_ref, "object")))))
        {
            _ = F_dv_require(false, R.cat("Unknown object identity: ", R.json_stringify(new System.Collections.Generic.List<object>(7) { F_dv_pair("operation", "dv_stage_store_owned"), F_dv_pair("object", F_dv_get(v_ref, "object")), F_dv_pair("world", F_dv_get(v_owned, "id")), F_dv_pair("domain", F_dv_get(v_owned, "domain")), F_dv_pair("subject", F_dv_get(v_owned, "subject")), F_dv_pair("facet", F_dv_get(v_ref, "facet")), F_dv_pair("field", F_dv_get(v_ref, "field")) })));
        }
        object v_object__13545 = F_dv_get(v_objects__13131, F_dv_get(v_ref, "object"));
        object v_facets__13590 = F_dv_get(v_object__13545, "facets");
        object v_fields__13621 = F_dv_find(v_facets__13590, F_dv_get(v_ref, "facet"), new System.Collections.Generic.List<object>(0) { });
        object v_updated__13669 = F_dv_with(v_object__13545, "facets", F_dv_with(v_facets__13590, F_dv_get(v_ref, "facet"), F_dv_with(v_fields__13621, F_dv_get(v_ref, "field"), v_cell)));
        object v_i__13775 = R.number("0");
        while (R.Bool(R.lt(v_i__13775, R.count(v_objects__13131))))
        {
            if (R.Bool(R.eq(R.at(R.at(v_objects__13131, v_i__13775), R.number("0")), F_dv_get(v_ref, "object"))))
            {
                _ = R.replace(v_objects__13131, v_i__13775, F_dv_pair(F_dv_clone(R.at(R.at(v_objects__13131, v_i__13775), R.number("0"))), F_dv_clone(v_updated__13669)));
            }
            v_i__13775 = R.add(v_i__13775, R.number("1"));
        }
        return v_owned;
    }
}
