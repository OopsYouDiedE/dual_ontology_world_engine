// @mechanism: dual_ontology_state
// @layer: core/ontology
// @contracts: State

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using System.Buffers;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System.Threading;
using System;

// --- Source: State.cs ---
namespace DuelView.Core
{


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
}

// --- Source: RecordStorage.cs ---
namespace DuelView.Compiled
{


// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
    private static bool RecordHot(object record)
    {
        if (record is FrozenList frozen) return Interlocked.Increment(ref frozen.RecordReads) > 3;
        return ++MutableStates.GetOrCreateValue((List<object>)record).RecordReads > 3;
    }
    // Index and value lookup share key equality, last-slot selection and cache
    // ownership. Their different small-record thresholds remain intentional.
    private static int FindRecordIndex(object record, IReadOnlyList<object> entries, object key, int cacheThreshold)
    {
        // Empty fallbacks and tiny records need no mutable ownership metadata
        // or hash index. Keep the same last-slot comparison and missing result.
        if (entries.Count == 0) return -1;
        if (key is string name)
        {
            if (entries.Count > 2 && (entries.Count >= cacheThreshold || RecordHot(record)))
            {
                var frozen = record as FrozenList;
                if (frozen is null && entries is List<object> mutable && MutableStates.TryGetValue(mutable, out var state)) frozen = state.Backbone;
                var index = frozen is not null ? FrozenIndex(frozen) : MutableIndex((List<object>)entries);
                if (index is not null) return index.TryGetValue(name, out var found) ? found : -1;
            }
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var candidate = RawItem(entries[i], 0);
                if (ReferenceEquals(candidate, name) || (candidate is string text && string.Equals(text, name, StringComparison.Ordinal)))
                    return i;
            }
            return -1;
        }
        for (var i = entries.Count - 1; i >= 0; i--)
            if (Bool(eq(RawItem(entries[i], 0), key))) return i;
        return -1;
    }
    public static object record_index(object record, object key) =>
        NumberValue(FindRecordIndex(record, Items(record), key, 8));

    public static object record_find(object record, object key, object fallback)
    {
        var entries = Items(record);
        var found = FindRecordIndex(record, entries, key, 4);
        if (found < 0)
        {
            if (fallback is string sentinel && sentinel == "__dv_missing_field_sentinel__")
                throw new Exception("Missing field: " + Text(key));
            return fallback;
        }
        var value = RawItem(entries[found], 1);
        // A mutable owner must acquire its own branch before returning a frozen
        // child. The shared lookup above must never turn this into a raw borrow.
        return record is not FrozenList && value is FrozenList
            ? ReadItem(ReadItem(record, found), 1) : value;
    }
    // Pure structural projections only. Never retain a mutable lookup result.
    private sealed record RecordShape(bool Valid);
    private sealed record RefLookupText(string Text);
    public static object record_cache_enabled() => Caches.RecordCacheEnabled ? BoolTrue : BoolFalse;
    private static FrozenList? UnchangedSnapshot(object value, ValueCaches caches)
    {
        if (value is FrozenList frozen) return frozen;
        if (value is List<object> list && caches.MutableStates.TryGetValue(list, out var state)) return state.Backbone;
        return null;
    }
    private static bool RecordLikeValue(object value)
    {
        if (!TryItems(value, out var entries)) return false;
        for (var i = 0; i < entries.Count; i++)
            if (!TryItems(entries[i], out var pair) || pair.Count != 2) return false;
        return true;
    }
    public static object record_like(object value) => Caches.RecordCacheEnabled
        ? record_like_cached(value) : RecordLikeValue(value) ? BoolTrue : BoolFalse;
    public static object record_like_cached(object value)
    {
        var caches = Caches;
        var frozen = UnchangedSnapshot(value, caches);
        var valid = frozen is null ? RecordLikeValue(value)
            : caches.RecordShapes.GetValue(frozen, key => new RecordShape(RecordLikeValue(key))).Valid;
        return valid ? BoolTrue : BoolFalse;
    }
    private static bool RefKeyText(object value)
    {
        if (value is not string text) return false;
        // Match DV's UTF-16-unit rule, including rejection of valid surrogate pairs.
        foreach (var unit in text)
            if (char.IsSurrogate(unit) || unit == '\uFFFD') return false;
        return true;
    }
    public static object ref_key_text_cached(object value) => RefKeyText(value) ? BoolTrue : BoolFalse;
    private static readonly string[] RefLookupFields = ["type", "object", "facet", "field", "subject"];
    private static string RefLookupKeyValue(object value)
    {
        if (!TryItems(value, out var entries) || (entries.Count != 4 && entries.Count != 5))
            return "J" + (string)json_stringify(value);
        for (var i = 0; i < entries.Count; i++)
        {
            if (!TryItems(entries[i], out var pair) || pair.Count != 2
                || !Equals(pair[0], RefLookupFields[i]) || !RefKeyText(pair[1]))
                return "J" + (string)json_stringify(value);
        }
        if (!Equals(Items(entries[0])[1], "ref")) return "J" + (string)json_stringify(value);
        var result = new StringBuilder("R");
        result.Append(entries.Count.ToString(CultureInfo.InvariantCulture)).Append(':');
        for (var i = 0; i < entries.Count; i++)
        {
            var text = (string)Items(entries[i])[1];
            result.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text);
        }
        return result.ToString();
    }
    public static object ref_lookup_key_cached(object value)
    {
        var caches = Caches;
        var frozen = UnchangedSnapshot(value, caches);
        return frozen is null ? RefLookupKeyValue(value)
            : caches.RefLookupTexts.GetValue(frozen, key => new RefLookupText(RefLookupKeyValue(key))).Text;
    }
    // Reverse dependency scheduling is session-local and retains immutable inputs.
    public static object record_with(object record, object key, object value)
    {
        var entries = Items(record);
        var entryCount = entries.Count;
        var result = NewMutableList(entryCount + 1);
        var frozen = (FrozenList)snapshot(record);
        var found = false;
        if (key is string name)
        {
            for (var i = 0; i < entryCount; i++)
            {
                var entry = RawItem(record, i);
                var k = RawItem(entry, 0);
                if (!found && (ReferenceEquals(k, name) || (k is string s && string.Equals(s, name, StringComparison.Ordinal))))
                {
                    result.Add(FrozenTextPair(name, snapshot(value)));
                    found = true;
                }
                else result.Add(frozen[i]);
            }
        }
        else
        {
            for (var i = 0; i < entryCount; i++)
            {
                var entry = RawItem(record, i);
                var k = RawItem(entry, 0);
                if (!found && Bool(eq(k, key)))
                {
                    result.Add(NewMutablePair(key, snapshot(value)));
                    found = true;
                }
                else result.Add(frozen[i]);
            }
        }
        if (!found) result.Add(key is string textKey ? FrozenTextPair(textKey, snapshot(value)) : NewMutablePair(key, snapshot(value)));
        if (found) InheritRecordLayout(result, frozen);
        return result;
    }
    private static List<object> NewMutablePair(object key, object value)
    {
        var pair = NewMutableList(2);
        pair.Add(key);
        pair.Add(value);
        return pair;
    }
    // A value-only edit preserves the key slots. Link mutable field pairs before
    // publishing the borrowed index so a later key edit invalidates the owner.
    private static void InheritRecordLayout(List<object> result, FrozenList source)
    {
        if (!source.IndexChecked) return;
        var state = MutableStates.GetOrCreateValue(result);
        foreach (var pair in result)
            if (pair is List<object> mutablePair) LinkParent(mutablePair, result);
        state.Index = source.Index;
        state.IndexChecked = true;
    }
    // Batch independent-copy update, preserving ordered pairs and duplicate-key rules.
    public static object record_with_many(object record, object updates)
    {
        var changes = Items(updates);
        if (changes.Count == 0) return record;
        // Lookups may thaw a mutable owner's child slots. Iterate a frozen update
        // table so materialization cannot invalidate the enumeration itself.
        updates = snapshot(updates);
        changes = Items(updates);
        var frozen = (FrozenList)snapshot(record);
        // Small one-off records favor the original short reverse scans.
        if (frozen.Count >= 4 && (changes.Count > 1 || frozen.Count >= 8)
            && TryStringRecordUpdate(frozen, changes, out var stringResult))
        {
            if (stringResult.Count == frozen.Count) InheritRecordLayout(stringResult, frozen);
            return stringResult;
        }
        var result = NewMutableList(frozen.Count + changes.Count);
        var keys = new List<object>(frozen.Count + changes.Count);
        HashSet<string>? stringKeys = new(StringComparer.Ordinal);
        foreach (var item in frozen)
        {
            var key = RawItem(item, 0);
            var updatedVal = record_find(updates, key, MissingSentinel);
            if (!ReferenceEquals(updatedVal, MissingSentinel))
                result.Add(key is string textKey ? FrozenTextPair(textKey, snapshot(updatedVal)) : NewMutablePair(key, snapshot(updatedVal)));
            else result.Add(item);
            keys.Add(key);
            if (key is string name) stringKeys?.Add(name);
            else stringKeys = null;
        }
        foreach (var item in changes)
        {
            var itemPl = Items(item);
            var key = itemPl.Count > 0 ? itemPl[0] : Items(item)[0];
            if (key is string name && stringKeys is not null)
            {
                if (!stringKeys.Add(name)) continue;
            }
            else
            {
                stringKeys = null;
                if (keys.Exists(existing => Bool(same_value(existing, key)))) continue;
            }
            var value = snapshot(record_find(updates, key, false));
            result.Add(key is string textKey ? FrozenTextPair(textKey, value) : NewMutablePair(key, value));
            keys.Add(key);
        }
        if (result.Count == frozen.Count) InheritRecordLayout(result, frozen);
        return result;
    }
    // Specialize only well-formed string-key tables. Other ABI inputs retain
    // the original path, including its validation and first-error behavior.
    private static bool TryStringRecordUpdate(IReadOnlyList<object> record, IReadOnlyList<object> changes, out List<object> result)
    {
        result = null!;
        foreach (var item in record)
            if (!TryItems(item, out var pair) || pair.Count != 2 || pair[0] is not string) return false;
        foreach (var item in changes)
            if (!TryItems(item, out var pair) || pair.Count != 2 || pair[0] is not string) return false;
        var updates = new Dictionary<string, object>(changes.Count, StringComparer.Ordinal);
        foreach (var item in changes) { var pair = Items(item); updates[(string)pair[0]] = pair[1]; }
        var present = new HashSet<string>(StringComparer.Ordinal);
        result = NewMutableList(record.Count + changes.Count);
        foreach (var item in record)
        {
            var pair = Items(item);
            var key = (string)pair[0];
            result.Add(updates.TryGetValue(key, out var value)
                ? FrozenTextPair(key, snapshot(value)) : item);
            present.Add(key);
        }
        foreach (var item in changes)
        {
            var pair = Items(item);
            var key = (string)pair[0];
            if (present.Add(key)) result.Add(FrozenTextPair(key, snapshot(updates[key])));
        }
        return true;
    }
    private static object FrozenTextPair(string key, object value)
    {
        // Public construction must acquire the same context lock as snapshot.
        var caches = Caches;
        lock (caches.SnapshotLock) return FrozenTextPairCore(key, value, caches);
    }
    private static object FrozenTextPairCore(string key, object value, ValueCaches caches)
    {
        if (value is not (string or double or bool or Rational or FrozenList))
            return NewMutablePair(key, value);
        MaintainSnapshots(caches);
        var keyHash = unchecked((ulong)HashCode.Combine(key.GetType(), key));
        var valueHash = value is FrozenList child ? child.Hash : unchecked((ulong)HashCode.Combine(value.GetType(), value));
        var hash = unchecked((14695981039346656037UL ^ keyHash) * 1099511628211UL);
        hash = unchecked((hash ^ valueHash) * 1099511628211UL);
        var sequenceHash = hash;
        hash = unchecked((hash ^ 2UL) * 1099511628211UL);
        if (!caches.SnapshotPool.TryGetValue(hash, out var bucket))
        {
            caches.SnapshotPool[hash] = bucket = new();
            caches.SnapshotBucketOrder.Enqueue(hash);
        }
        for (var i = bucket.Count - 1; i >= 0; i--)
        {
            if (!bucket[i].TryGetTarget(out var existing)) { bucket.RemoveAt(i); continue; }
            if (existing.Count != 2 || !Equals(existing[0], key)) continue;
            var equal = existing[1] is double left && value is double right
                ? BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right)
                : Equals(existing[1], value);
            if (equal) return existing;
        }
        var pair = new FrozenList(new object[] { key, value }, hash, sequenceHash);
        var keyEligible = TryMembershipHash(key, out var compatibleKey);
        var valueEligible = TryMembershipHash(value, out var compatibleValue);
        var eligible = keyEligible && valueEligible;
        if (eligible)
        {
            var compatible = unchecked((14695981039346656037UL ^ compatibleKey) * 1099511628211UL);
            compatible = unchecked((compatible ^ compatibleValue) * 1099511628211UL);
            pair.CompatibleSequenceHash = compatible;
            pair.MembershipHash = unchecked((compatible ^ 2UL) * 1099511628211UL) ^ 5UL;
        }
        pair.MembershipHashEligible = eligible;
        pair.MembershipHashChecked = true;
        bucket.Add(new WeakReference<FrozenList>(pair));
        return pair;
    }
}
}

// --- Source: Memoization.cs ---
namespace DuelView.Compiled
{


// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
    private sealed class ValueMemo
    {
        internal readonly Dictionary<string, MemoPartition> Partitions = new(StringComparer.Ordinal);
        private static string PartitionName(object key) => TryItems(key, out var parts) && parts.Count > 0 && parts[0] is string tag ? tag : "";
        internal bool TryPartition(object key, out MemoPartition partition) => Partitions.TryGetValue(PartitionName(key), out partition!);
        internal MemoPartition Partition(object key)
        {
            var name = PartitionName(key);
            if (!Partitions.TryGetValue(name, out var partition)) Partitions[name] = partition = new();
            return partition;
        }
    }
    private sealed class MemoKeyComparer : IEqualityComparer<object>
    {
        internal static readonly MemoKeyComparer Instance = new();
        public new bool Equals(object? left, object? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            if (!TryItems(left, out var a) || !TryItems(right, out var b)) return object.Equals(left, right);
            return SameStorageList(a, b);
        }
        private static bool SameStorageList(IReadOnlyList<object> left, IReadOnlyList<object> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++)
            {
                var a = left[i]; var b = right[i];
                if (ReferenceEquals(a, b)) continue;
                if (a is double x && b is double y)
                {
                    if (BitConverter.DoubleToInt64Bits(x) != BitConverter.DoubleToInt64Bits(y)) return false;
                }
                else if (TryItems(a, out var al) && TryItems(b, out var bl))
                {
                    if (!SameStorageList(al, bl)) return false;
                }
                else if (!object.Equals(a, b)) return false;
            }
            return true;
        }
        public int GetHashCode(object value)
        {
            var hash = StorageHash(value, Caches);
            return unchecked((int)(hash ^ (hash >> 32)));
        }
        private static ulong StorageHash(object value, ValueCaches caches)
        {
            if (value is FrozenList frozen) return frozen.Hash;
            if (value is not List<object> list) return unchecked((ulong)HashCode.Combine(value.GetType(), value));
            if (caches.MutableStates.TryGetValue(list, out var state) && state.Backbone is { } basis) return basis.Hash;
            var hash = 14695981039346656037UL;
            for (var i = 0; i < list.Count; i++)
                hash = unchecked((hash ^ StorageHash(list[i], caches)) * 1099511628211UL);
            return unchecked((hash ^ (ulong)list.Count) * 1099511628211UL);
        }
    }
    private sealed class MemoPartition
    {
        // Only put stores frozen keys. A lookup may borrow its current raw key
        // during this call; the comparer never retains or changes that input.
        internal readonly Dictionary<object, MemoEntry> Values = new(MemoKeyComparer.Instance);
        internal readonly Queue<object> Order = new();
    }
    private sealed class MemoEntry(object value)
    {
        internal object Value = value;
        // The wrapper is immutable, just like its cached value. Keep it with
        // this entry so repeated pure reads do not allocate and freeze it again.
        internal object? ReadResult;
    }
    private static ConditionalWeakTable<FrozenList, ValueMemo> ValueMemos => Caches.ValueMemos;
    // Cache groups select reuse only. Keys, computations and unrelated memo groups
    // remain unchanged; the caller takes its ordinary computation on a miss.
    public static object memo_group_enabled(object group) => group switch
    {
        "plan" => Caches.PlanCacheEnabled ? BoolTrue : BoolFalse,
        "declaration" => Caches.DeclarationCacheEnabled ? BoolTrue : BoolFalse,
        _ => throw new ArgumentException("Unknown memo control group", nameof(group))
    };
    public static object memo_get(object owner, object key) => MemoLookup(owner, key, false);
    // Pure-query callers may borrow a frozen cached value. The ordinary API
    // still returns independent writable copies with its original ownership.
    public static object memo_read(object owner, object key) => MemoLookup(owner, key, true);
    private static object MemoLookup(object owner, object key, bool readOnly)
    {
        var caches = Caches;
        lock (caches.SnapshotLock)
        {
            var identity = owner is FrozenList fl ? fl : (FrozenList)SnapshotValue(Items(owner), caches);
            if (caches.ValueMemos.TryGetValue(identity, out var ownerMemo) && ownerMemo.TryPartition(key, out var partition))
            {
                if (partition.Values.TryGetValue(key, out var result))
                    return readOnly ? (result.ReadResult ??= MemoReadResult(caches, BoolTrue, result.Value))
                        : new List<object>(2) { BoolTrue, copy_value(result.Value) };
            }
            else
            {
                // Retain the original validation on a new owner or partition,
                // without allocating an empty partition just for a miss.
                _ = SnapshotValue(key, caches);
                if (ownerMemo is not null) ArgumentNullException.ThrowIfNull(key);
            }
            return readOnly ? (caches.MemoReadMiss ??= MemoReadResult(caches, BoolFalse, BoolFalse))
                : new List<object>(2) { BoolFalse, BoolFalse };
        }
    }
    private static object MemoReadResult(ValueCaches caches, object found, object value)
    {
        var result = NewMutableList(2);
        result.Add(found);
        result.Add(value);
        return SnapshotValue(result, caches);
    }
    public static object memo_put(object owner, object key, object value)
    {
        var caches = Caches;
        lock (caches.SnapshotLock)
        {
            var identity = (FrozenList)SnapshotValue(Items(owner), caches);
            var frozenKey = SnapshotValue(key, caches);
            var memo = caches.ValueMemos.GetOrCreateValue(identity).Partition(frozenKey);
            if (!memo.Values.ContainsKey(frozenKey))
            {
                if (memo.Values.Count >= 512) memo.Values.Remove(memo.Order.Dequeue());
                memo.Order.Enqueue(frozenKey);
            }
            var frozenValue = SnapshotValue(value, caches);
            if (memo.Values.TryGetValue(frozenKey, out var entry))
            {
                entry.Value = frozenValue;
                entry.ReadResult = null;
            }
            else memo.Values[frozenKey] = new MemoEntry(frozenValue);
            return value;
        }
    }
}
}


namespace DuelView.Runtime
{
    public sealed record Fact(JsonElement Value, bool Known, bool Conflicted, double Confidence, double ObservedAt, long Revision);
    public sealed record FieldChange(FieldKey Key, Fact? Before, Fact After);
    public sealed record SavedFact(FieldKey Key, Fact Fact);
    public sealed record FactStoreState(string Subject, long Revision, SavedFact[] Facts);

    public sealed class FactStore
    {
        private readonly Dictionary<FieldKey, Fact> facts = [];
        public string Subject { get; }
        public long Revision { get; private set; }
        public FactStore(string subject) => Subject = subject;
        public Fact? Read(FieldKey key) => facts.GetValueOrDefault(key);
        public FactStoreState SaveState() => new(Subject, Revision, facts.Select(pair => new SavedFact(pair.Key, pair.Value)).ToArray());
        public void RestoreState(FactStoreState state)
        {
            if (state.Subject != Subject || state.Revision < 0 || state.Facts is null) throw new InvalidDataException("Invalid fact store checkpoint owner or revision.");
            var restored = new Dictionary<FieldKey, Fact>();
            foreach (var entry in state.Facts)
            {
                var key = entry.Key; var value = entry.Fact;
                if (string.IsNullOrWhiteSpace(key.Object) || string.IsNullOrWhiteSpace(key.Facet) || string.IsNullOrWhiteSpace(key.Field) ||
                   value is null || value.Value.ValueKind == JsonValueKind.Undefined || value.Revision < 0 || value.Revision > state.Revision ||
                   !double.IsFinite(value.Confidence) || value.Confidence is < 0 or > 1 || !double.IsFinite(value.ObservedAt) || value.ObservedAt < 0 || !restored.TryAdd(key, value))
                    throw new InvalidDataException("Invalid fact store checkpoint entry.");
            }
            facts.Clear(); foreach (var item in restored) facts.Add(item.Key, item.Value); Revision = state.Revision;
        }
        public IEnumerable<KeyValuePair<FieldKey, Fact>> Entries => facts;
        public void Initialize(JsonElement objects)
        {
            foreach (var obj in objects.Fields())
                foreach (var facet in obj.Value.Get("facets").Fields())
                    foreach (var field in facet.Value.Fields())
                    {
                        var cell = field.Value;
                        facts.Add(new(obj.Name, facet.Name, field.Name), new(cell.Get("value"), cell.Get("known").Flag(),
                            cell.Get("conflicted").Flag(), cell.Get("confidence").Number(), cell.Get("time").Number(), 0));
                    }
        }
        public IReadOnlyList<FieldChange> Apply(IReadOnlyDictionary<FieldKey, Fact> writes)
        {
            var changes = new List<FieldChange>();
            foreach (var (key, value) in writes)
            {
                var before = Read(key);
                if (before is not null && before.Known == value.Known && before.Conflicted == value.Conflicted &&
                    before.Confidence == value.Confidence && Values.Same(before.Value, value.Value)) continue;
                changes.Add(new(key, before, value with { Revision = Revision + 1 }));
            }
            if (changes.Count == 0) return changes;
            Revision++;
            foreach (var change in changes) facts[change.Key] = change.After;
            return changes;
        }
    }
}

