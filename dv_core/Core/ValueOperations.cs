using DuelView.Compiled;

namespace DuelView.Core;

/// <summary>Values / Records: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dv_pair(object v_a, object v_b)
    {
        var result = R.NewValueList(2);
        result.Add(v_a);
        result.Add(v_b);
        return result;
    }

    public static object F_dv_single(object v_a)
    {
        var result = R.NewValueList(1);
        result.Add(v_a);
        return result;
    }

    public static object F_dv_list3(object v_a, object v_b, object v_c)
    {
        var result = R.NewValueList(3);
        result.Add(v_a);
        result.Add(v_b);
        result.Add(v_c);
        return result;
    }

    public static object F_dv_and(object v_a, object v_b)
    {
        if (R.Bool(v_a))
        {
            return v_b;
        }
        else
        {
            return false;
        }
    }

    public static object F_dv_or(object v_a, object v_b)
    {
        if (R.Bool(v_a))
        {
            return true;
        }
        else
        {
            return v_b;
        }
    }

    public static object F_dv_require(object v_condition, object v_message)
    {
        if (R.Bool(v_condition))
        {
            return true;
        }
        else
        {
            return R.fail(v_message);
        }
    }

    public static object F_dv_clone(object v_value)
    {
        return R.copy_value(v_value);
    }

    public static object F_dv_same(object v_a, object v_b)
    {
        return R.same_value(v_a, v_b);
    }

    public static object F_dv_append(object v_items, object v_value)
    {
        object v_r__616 = F_dv_clone(v_items);
        _ = R.push(v_r__616, R.snapshot(v_value));
        return v_r__616;
    }

    public static object F_dv_has(object v_items, object v_value)
    {
        return R.has_value(v_items, v_value);
    }

    public static object F_dv_find(object v_record, object v_key, object v_fallback)
    {
        return R.record_find(v_record, v_key, v_fallback);
    }

    public static object F_dv_has_key(object v_record, object v_key)
    {
        return R.not(R.lt(R.record_index(v_record, v_key), R.number("0")));
    }

    public static object F_dv_record_like(object value) => R.record_like(value);

    public static object F_dv_get(object v_record, object v_key)
    {
        return R.record_find(v_record, v_key, "__dv_missing_field_sentinel__");
    }

    public static object F_dv_with(object v_record, object v_key, object v_value)
    {
        return R.record_with(v_record, v_key, v_value);
    }

    public static object F_dv_without(object v_record, object v_key)
    {
        v_record = R.snapshot(v_record);
        object v_result__1787 = R.NewValueList(0);
        object v_i__1807 = R.number("0");
        while (R.Bool(R.lt(v_i__1807, R.count(v_record))))
        {
            object v_entry__1859 = R.at(v_record, v_i__1807);
            if (R.Bool(R.eq(R.at(v_entry__1859, R.number("0")), v_key)))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_result__1787, R.snapshot(v_entry__1859));
            }
            v_i__1807 = R.add(v_i__1807, R.number("1"));
        }
        return v_result__1787;
    }

    public static object F_dv_unit(object v_x)
    {
        _ = F_dv_require(F_dv_and(R.not(R.lt(v_x, R.number("0"))), R.not(R.lt(R.number("1"), v_x))), "Expected weight in [0,1]");
        return v_x;
    }

    public static object F_dv_unknown()
    {
        var result = R.NewValueList(6);
        result.Add(F_dv_pair("known", false));
        result.Add(F_dv_pair("value", false));
        result.Add(F_dv_pair("confidence", R.number("0")));
        result.Add(F_dv_pair("time", R.sub(R.number("0"), R.number("1"))));
        result.Add(F_dv_pair("claims", R.NewValueList(0)));
        result.Add(F_dv_pair("conflicted", false));
        return result;
    }

    public static object F_dv_min(object v_a, object v_b)
    {
        if (R.Bool(R.lt(v_a, v_b)))
        {
            return v_a;
        }
        else
        {
            return v_b;
        }
    }

    public static object F_dv_max(object v_a, object v_b)
    {
        if (R.Bool(R.lt(v_a, v_b)))
        {
            return v_b;
        }
        else
        {
            return v_a;
        }
    }

    public static object F_dv_cell(object v_value, object v_confidence, object v_time, object v_claims)
    {
        var cell = R.NewValueList(4);
        cell.Add(F_dv_pair("known", true));
        cell.Add(F_dv_pair("value", R.snapshot(v_value)));
        cell.Add(F_dv_pair("confidence", F_dv_unit(v_confidence)));
        cell.Add(F_dv_pair("time", v_time));
        var updates = R.NewValueList(3);
        updates.Add(F_dv_pair("claims", R.NewValueList(0)));
        updates.Add(F_dv_pair("basis", v_claims));
        updates.Add(F_dv_pair("conflicted", false));
        return F_dv_with_many(cell, updates);
    }

    public static object F_dv_with_many(object v_record, object v_updates)
    {
        return R.record_with_many(v_record, v_updates);
    }

    public static object F_dv_with_path(object v_record, object v_keys, object v_depth, object v_value)
    {
        object v_key__11950 = R.at(v_keys, v_depth);
        object v_result__11976 = R.NewValueList(0);
        object v_found__11996 = false;
        object v_i__12018 = R.number("0");
        while (R.Bool(R.lt(v_i__12018, R.count(v_record))))
        {
            object v_entry__12068 = R.at(v_record, v_i__12018);
            if (R.Bool(R.eq(R.at(v_entry__12068, R.number("0")), v_key__11950)))
            {
                object v_next__12130 = false;
                if (R.Bool(R.eq(R.add(v_depth, R.number("1")), R.count(v_keys))))
                {
                    v_next__12130 = F_dv_clone(v_value);
                }
                else
                {
                    v_next__12130 = F_dv_with_path(F_dv_get(v_record, v_key__11950), v_keys, R.add(v_depth, R.number("1")), v_value);
                }
                _ = R.push(v_result__11976, F_dv_pair(F_dv_clone(R.at(v_entry__12068, R.number("0"))), v_next__12130));
                v_found__11996 = true;
            }
            else
            {
                _ = R.push(v_result__11976, R.snapshot(v_entry__12068));
            }
            v_i__12018 = R.add(v_i__12018, R.number("1"));
        }
        if (R.Bool(R.not(v_found__11996)))
        {
            object v_next__12523 = false;
            if (R.Bool(R.eq(R.add(v_depth, R.number("1")), R.count(v_keys))))
            {
                v_next__12523 = F_dv_clone(v_value);
            }
            else
            {
                v_next__12523 = F_dv_with_path(R.NewValueList(0), v_keys, R.add(v_depth, R.number("1")), v_value);
            }
            _ = R.push(v_result__11976, F_dv_pair(F_dv_clone(v_key__11950), v_next__12523));
        }
        return v_result__11976;
    }

    public static object F_dvs_replace(object v_items, object v_index, object v_value)
    {
        return R.list_with_index(R.snapshot(v_items), v_index, v_value);
    }

    /// <summary>First matching identity in an ordered record list; -1 when absent.</summary>
    public static object F_dv_index_by_id(object items, object id)
    {
        var entries = R.Items(items);
        for (var index = 0; index < entries.Count; index++)
            if (R.Bool(R.eq(F_dv_get(entries[index], "id"), id))) return (double)index;
        return -1d;
    }
}
