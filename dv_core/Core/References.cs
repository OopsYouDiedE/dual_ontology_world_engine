using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>World / References: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dv_entity_ref(object v_id)
    {
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("type", "entity-ref"), F_dv_pair("id", v_id) };
    }

    public static object F_dv_ref(object v_object_id, object v_facet, object v_field)
    {
        return new System.Collections.Generic.List<object>(4) { F_dv_pair("type", "ref"), F_dv_pair("object", v_object_id), F_dv_pair("facet", v_facet), F_dv_pair("field", v_field) };
    }

    public static object F_dv_scoped_ref(object v_subject, object v_object_id, object v_facet, object v_field)
    {
        return F_dv_with(F_dv_ref(v_object_id, v_facet, v_field), "subject", v_subject);
    }

    public static object F_dv_same_local_ref(object v_subject, object v_left, object v_right)
    {
        if (R.Bool((R.Bool(R.not(R.eq(F_dv_find(v_left, "subject", v_subject), v_subject))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_right, "subject", v_subject), v_subject))))))
        {
            return false;
        }
        return (R.Bool((R.Bool((R.Bool(R.eq(F_dv_get(v_left, "type"), F_dv_get(v_right, "type"))) ? (object)(R.eq(F_dv_get(v_left, "object"), F_dv_get(v_right, "object"))) : (object)(false))) ? (object)(R.eq(F_dv_get(v_left, "facet"), F_dv_get(v_right, "facet"))) : (object)(false))) ? (object)(R.eq(F_dv_get(v_left, "field"), F_dv_get(v_right, "field"))) : (object)(false));
    }

    public static object F_dv_ref_key_text(object v_value)
    {
        if (R.Bool(R.record_cache_enabled()))
        {
            return R.ref_key_text_cached(v_value);
        }
        if (R.Bool(R.not(R.is_text(v_value))))
        {
            return false;
        }
        object v_i__3541 = R.number("0");
        while (R.Bool(R.lt(v_i__3541, R.length(v_value))))
        {
            object v_unit__3591 = R.code(R.chr(v_value, v_i__3541));
            if (R.Bool((R.Bool((R.Bool(R.not(R.lt(v_unit__3591, R.number("55296")))) ? (object)(R.not(R.lt(R.number("57343"), v_unit__3591))) : (object)(false))) ? (object)(true) : (object)(R.eq(v_unit__3591, R.number("65533"))))))
            {
                return false;
            }
            v_i__3541 = R.add(v_i__3541, R.number("1"));
        }
        return true;
    }

    public static object F_dv_ref_lookup_key(object v_ref)
    {
        if (R.Bool(R.record_cache_enabled()))
        {
            return R.ref_lookup_key_cached(v_ref);
        }
        if (R.Bool(R.not(R.is_list(v_ref))))
        {
            return R.cat("J", R.json_stringify(v_ref));
        }
        object v_count__4016 = R.count(v_ref);
        if (R.Bool((R.Bool(R.not(R.eq(v_count__4016, R.number("4")))) ? (object)(R.not(R.eq(v_count__4016, R.number("5")))) : (object)(false))))
        {
            return R.cat("J", R.json_stringify(v_ref));
        }
        object v_fields__4131 = new System.Collections.Generic.List<object>(5) { "type", "object", "facet", "field", "subject" };
        object v_parts__4196 = new System.Collections.Generic.List<object>(0) { };
        object v_i__4215 = R.number("0");
        while (R.Bool(R.lt(v_i__4215, v_count__4016)))
        {
            object v_pair__4254 = R.at(v_ref, v_i__4215);
            if (R.Bool(R.not(R.is_list(v_pair__4254))))
            {
                return R.cat("J", R.json_stringify(v_ref));
            }
            if (R.Bool(R.not(R.eq(R.count(v_pair__4254), R.number("2")))))
            {
                return R.cat("J", R.json_stringify(v_ref));
            }
            if (R.Bool((R.Bool(R.not(R.eq(R.at(v_pair__4254, R.number("0")), R.at(v_fields__4131, v_i__4215)))) ? (object)(true) : (object)(R.not(F_dv_ref_key_text(R.at(v_pair__4254, R.number("1"))))))))
            {
                return R.cat("J", R.json_stringify(v_ref));
            }
            _ = R.push(v_parts__4196, R.at(v_pair__4254, R.number("1")));
            v_i__4215 = R.add(v_i__4215, R.number("1"));
        }
        if (R.Bool(R.not(R.eq(R.at(v_parts__4196, R.number("0")), "ref"))))
        {
            return R.cat("J", R.json_stringify(v_ref));
        }
        object v_result__4698 = R.cat("R", R.cat(R.show(v_count__4016), ":"));
        v_i__4215 = R.number("0");
        while (R.Bool(R.lt(v_i__4215, v_count__4016)))
        {
            v_result__4698 = R.cat(v_result__4698, R.cat(R.show(R.length(R.at(v_parts__4196, v_i__4215))), R.cat(":", R.at(v_parts__4196, v_i__4215))));
            v_i__4215 = R.add(v_i__4215, R.number("1"));
        }
        return v_result__4698;
    }
}
