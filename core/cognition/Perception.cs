// @mechanism: perception_and_erosion
// @layer: core/cognition
// @contracts: Perception

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVCognitionKernel;
using static DuelView.Core.DVKernel;

// --- Source: Perception.cs ---
namespace DuelView.Core
{


/// <summary>World / Perception: native Core mechanisms.</summary>
public static partial class DVCognitionKernel
{
    // Binding tables are immutable identities here. Each index contains only
    // the projection declared by that exact table, never world or mind facts.
    private sealed class ProjectionBindingIndices
    {
        internal readonly Dictionary<string, Dictionary<string, (string Local, bool Ambiguous)>?> Subjects = new(StringComparer.Ordinal);
    }
    private static readonly ConditionalWeakTable<object, ProjectionBindingIndices> ProjectionBindings = new();

    private static bool TryIndexedProjection(object bindings, object physical, object subject, out object local)
    {
        local = "";
        if (physical is not string physicalName || subject is not string subjectName) return false;
        _ = R.Items(bindings);
        var frozen = R.snapshot(bindings);
        var cache = ProjectionBindings.GetValue(frozen, static _ => new ProjectionBindingIndices());
        lock (cache)
        {
            if (!cache.Subjects.TryGetValue(subjectName, out var index))
            {
                index = new(StringComparer.Ordinal);
                foreach (var binding in R.Items(frozen))
                {
                    if (!R.Bool(R.eq(F_dv_get(binding, "subject"), subject))) continue;
                    var source = F_dv_get(binding, "physical");
                    var target = F_dv_get(binding, "local");
                    // Preserve the general DV key/equality path for unusual
                    // identity values rather than converting their meaning.
                    if (source is not string sourceName || target is not string targetName)
                    {
                        index = null;
                        break;
                    }
                    var ambiguous = index.TryGetValue(sourceName, out var previous) &&
                        (previous.Ambiguous || previous.Local != "");
                    index[sourceName] = (targetName, ambiguous);
                }
                cache.Subjects[subjectName] = index;
            }
            if (index is null) return false;
            if (!index.TryGetValue(physicalName, out var found)) return true;
            _ = F_dv_require(!found.Ambiguous, "Ambiguous projection binding");
            local = found.Local;
            return true;
        }
    }
    public static object F_door_open(object v_door)
    {
        object v_law__42648;
        v_law__42648 = F_dv_axiom("door_open", R.number("1"), F_dv_matches(v_door, "mechanical", new System.Collections.Generic.List<object>(2) { F_dv_pair("inside_latch", false), F_dv_pair("outside_latch", false) }), F_dv_writes(v_door, "mechanical", new System.Collections.Generic.List<object>(1) { F_dv_pair("closed", false) }));
        return v_law__42648;
    }

    public static object F_door_self_close(object v_door)
    {
        return F_dv_axiom("door_self_close", R.number("1"), F_dv_matches(v_door, "mechanical", new System.Collections.Generic.List<object>(2) { F_dv_pair("automatic", true), F_dv_pair("held", false) }), F_dv_writes(v_door, "mechanical", new System.Collections.Generic.List<object>(2) { F_dv_pair("closed", true), F_dv_pair("gap", R.number("0")) }));
    }

    public static object F_door_latch(object v_door)
    {
        return F_dv_axiom("door_latch", R.number("1"), F_dv_matches(v_door, "mechanical", new System.Collections.Generic.List<object>(1) { F_dv_pair("closed", true) }), F_dv_writes(v_door, "mechanical", new System.Collections.Generic.List<object>(1) { F_dv_pair("inside_latch", true) }));
    }

    public static object F_door_seal(object v_door)
    {
        return F_dv_axiom("door_seal", R.number("1"), F_dv_matches(v_door, "mechanical", new System.Collections.Generic.List<object>(1) { F_dv_pair("closed", true) }), F_dv_writes(v_door, "mechanical", new System.Collections.Generic.List<object>(1) { F_dv_pair("sealed", true) }));
    }

    public static object F_channel_reconfigure(object v_channel, object v_gain)
    {
        return F_dv_axiom("channel_reconfigure", R.number("1"), F_dv_true(), F_dv_writes(v_channel, "transport", new System.Collections.Generic.List<object>(1) { F_dv_pair("gain", F_dv_unit(v_gain)) }));
    }

    public static object F_channel_flow(object v_channel)
    {
        return F_dv_axiom("channel_flow", R.number("1"), F_dv_matches(v_channel, "transport", new System.Collections.Generic.List<object>(1) { F_dv_pair("enabled", true) }), new System.Collections.Generic.List<object>(0) { });
    }

    public static object F_dv_binding(object v_physical, object v_local, object v_subject)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("physical", v_physical), F_dv_pair("local", v_local), F_dv_pair("subject", v_subject) };
    }

    public static object F_dv_bind(object v_physical, object v_mind, object v_aliases)
    {
        _ = F_dv_domain(v_physical, "physical");
        _ = F_dv_domain(v_mind, "mind");
        _ = F_dv_require(F_dv_record_like(v_aliases), "Bindings require physical-to-local identity pairs");
        object v_next__44120 = F_dv_clone(v_mind);
        object v_bindings__44150 = new System.Collections.Generic.List<object>(0) { };
        object v_i__44172 = R.number("0");
        while (R.Bool(R.lt(v_i__44172, R.count(v_aliases))))
        {
            object v_source__44223 = R.at(R.at(v_aliases, v_i__44172), R.number("0"));
            object v_local__44258 = R.at(R.at(v_aliases, v_i__44172), R.number("1"));
            _ = F_dv_require(F_dv_has_key(F_dv_get(v_physical, "objects"), v_source__44223), "Cannot bind an unknown physical object");
            _ = F_dv_require((R.Bool(R.lt(R.number("0"), R.length(v_source__44223))) ? (object)(R.lt(R.number("0"), R.length(v_local__44258))) : (object)(false)), "Binding identities must be nonempty");
            _ = R.push(v_bindings__44150, F_dv_binding(v_source__44223, v_local__44258, F_dv_get(v_mind, "subject")));
            if (R.Bool(R.not(F_dv_has_key(F_dv_get(v_next__44120, "objects"), v_local__44258))))
            {
                v_next__44120 = F_dv_add_object(v_next__44120, F_dv_object(v_local__44258, "perceived", new System.Collections.Generic.List<object>(0) { }));
            }
            v_i__44172 = R.add(v_i__44172, R.number("1"));
        }
        _ = F_dv_validate_bindings(v_bindings__44150, F_dv_get(v_mind, "subject"));
        return new System.Collections.Generic.List<object>(5) { F_dv_pair("mind", v_next__44120), F_dv_pair("bindings", v_bindings__44150), F_dv_pair("subject", F_dv_get(v_mind, "subject")), F_dv_pair("source_world", F_dv_get(v_physical, "id")), F_dv_pair("source_revision", F_dv_get(v_physical, "revision")) };
    }

    public static object F_dv_view(object v_physical, object v_subject, object v_aliases)
    {
        return F_dv_bind(v_physical, F_dv_world(R.cat("mind:", v_subject), "mind", v_subject), v_aliases);
    }

    public static object F_dv_local(object v_bindings, object v_physical, object v_subject)
    {
        if (TryIndexedProjection(v_bindings, v_physical, v_subject, out var indexed)) return indexed;
        object v_lookup_key__45210 = new System.Collections.Generic.List<object>(2) { R.cat("projection-local:", v_subject), v_physical };
        object v_lookup__45280 = R.memo_get(v_bindings, v_lookup_key__45210);
        if (R.Bool(R.at(v_lookup__45280, R.number("0"))))
        {
            _ = F_dv_require(R.not(F_dv_get(R.at(v_lookup__45280, R.number("1")), "ambiguous")), "Ambiguous projection binding");
            return F_dv_get(R.at(v_lookup__45280, R.number("1")), "local");
        }
        object v_key__45456 = new System.Collections.Generic.List<object>(2) { "projection-index", v_subject };
        object v_cached__45500 = R.memo_get(v_bindings, v_key__45456);
        object v_index__45544 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.at(v_cached__45500, R.number("0"))))
        {
            v_index__45544 = R.at(v_cached__45500, R.number("1"));
        }
        else
        {
            object v_i__45621 = R.number("0");
            while (R.Bool(R.lt(v_i__45621, R.count(v_bindings))))
            {
                object v_binding__45681 = R.at(v_bindings, v_i__45621);
                if (R.Bool(R.eq(F_dv_get(v_binding__45681, "subject"), v_subject)))
                {
                    object v_previous__45766 = F_dv_find(v_index__45544, F_dv_get(v_binding__45681, "physical"), new System.Collections.Generic.List<object>(2) { F_dv_pair("local", ""), F_dv_pair("ambiguous", false) });
                    object v_entry__45865 = new System.Collections.Generic.List<object>(2) { F_dv_pair("local", F_dv_get(v_binding__45681, "local")), F_dv_pair("ambiguous", (R.Bool(F_dv_get(v_previous__45766, "ambiguous")) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_previous__45766, "local"), ""))))) };
                    object v_slot__45975 = R.record_index(v_index__45544, F_dv_get(v_binding__45681, "physical"));
                    if (R.Bool(R.lt(v_slot__45975, R.number("0"))))
                    {
                        _ = R.push(v_index__45544, F_dv_pair(F_dv_get(v_binding__45681, "physical"), v_entry__45865));
                    }
                    else
                    {
                        _ = R.replace(v_index__45544, v_slot__45975, F_dv_pair(F_dv_get(v_binding__45681, "physical"), v_entry__45865));
                    }
                }
                v_i__45621 = R.add(v_i__45621, R.number("1"));
            }
            _ = R.memo_put(v_bindings, v_key__45456, v_index__45544);
        }
        object v_found__46293 = F_dv_find(v_index__45544, v_physical, new System.Collections.Generic.List<object>(2) { F_dv_pair("local", ""), F_dv_pair("ambiguous", false) });
        _ = R.memo_put(v_bindings, v_lookup_key__45210, v_found__46293);
        _ = F_dv_require(R.not(F_dv_get(v_found__46293, "ambiguous")), "Ambiguous projection binding");
        return F_dv_get(v_found__46293, "local");
    }

    public static object F_dv_validate_bindings(object v_bindings, object v_subject)
    {
        object v_key__46550 = new System.Collections.Generic.List<object>(2) { "validated-projection", v_subject };
        if (R.Bool(R.at(R.memo_get(v_bindings, v_key__46550), R.number("0"))))
        {
            return true;
        }
        object v_i__46656 = R.number("0");
        object v_locals__46670 = new System.Collections.Generic.List<object>(0) { };
        object v_physicals__46690 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__46656, R.count(v_bindings))))
        {
            object v_b__46753 = R.at(v_bindings, v_i__46656);
            if (R.Bool(R.eq(F_dv_get(v_b__46753, "subject"), v_subject)))
            {
                _ = F_dv_require(R.lt(R.number("0"), R.length(F_dv_get(v_b__46753, "local"))), "Empty local identity");
                _ = F_dv_require(R.not(F_dv_has(v_locals__46670, F_dv_get(v_b__46753, "local"))), "Local projection cannot bind two objects");
                _ = F_dv_require(R.not(F_dv_has(v_physicals__46690, F_dv_get(v_b__46753, "physical"))), "Ambiguous physical binding");
                _ = R.push(v_locals__46670, F_dv_get(v_b__46753, "local"));
                _ = R.push(v_physicals__46690, F_dv_get(v_b__46753, "physical"));
            }
            v_i__46656 = R.add(v_i__46656, R.number("1"));
        }
        return R.memo_put(v_bindings, v_key__46550, true);
    }

    public static object F_dv_project_value(object v_value, object v_bindings, object v_subject)
    {
        if (R.Bool(R.is_list(v_value)))
        {
            if (R.Bool(F_dv_record_like(v_value)))
            {
                if (R.Bool(F_dv_same(F_dv_find(v_value, "type", ""), "entity-ref")))
                {
                    object v_local__47403 = F_dv_local(v_bindings, F_dv_get(v_value, "id"), v_subject);
                    if (R.Bool(R.eq(v_local__47403, "")))
                    {
                        return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", false), F_dv_pair("value", false) };
                    }
                    else
                    {
                        return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", true), F_dv_pair("value", F_dv_entity_ref(v_local__47403)) };
                    }
                }
                else
                {
                    if (R.Bool(F_dv_or(F_dv_same(F_dv_find(v_value, "type", ""), "ref"), F_dv_and(F_dv_has_key(v_value, "object"), F_dv_and(F_dv_has_key(v_value, "facet"), F_dv_has_key(v_value, "field"))))))
                    {
                        object v_local__47852 = F_dv_local(v_bindings, F_dv_get(v_value, "object"), v_subject);
                        if (R.Bool(R.eq(v_local__47852, "")))
                        {
                            return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", false), F_dv_pair("value", false) };
                        }
                        else
                        {
                            return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", true), F_dv_pair("value", F_dv_scoped_ref(v_subject, v_local__47852, F_dv_get(v_value, "facet"), F_dv_get(v_value, "field"))) };
                        }
                    }
                    else
                    {
                        if (R.Bool(F_dv_has_key(v_value, "op")))
                        {
                            object v_op__48241 = F_dv_get(v_value, "op");
                            if (R.Bool(R.eq(v_op__48241, "constant")))
                            {
                                return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", true), F_dv_pair("value", v_value) };
                            }
                            else
                            {
                                if (R.Bool(F_dv_or(R.eq(v_op__48241, "equals"), R.eq(v_op__48241, "at_least"))))
                                {
                                    object v_p_ref__48511 = F_dv_project_value(F_dv_get(v_value, "ref"), v_bindings, v_subject);
                                    object v_p_val__48602 = F_dv_project_value(F_dv_get(v_value, "value"), v_bindings, v_subject);
                                    if (R.Bool(F_dv_and(F_dv_get(v_p_ref__48511, "known"), F_dv_get(v_p_val__48602, "known"))))
                                    {
                                        return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", true), F_dv_pair("value", F_dv_with_many(v_value, new System.Collections.Generic.List<object>(2) { F_dv_pair("ref", F_dv_get(v_p_ref__48511, "value")), F_dv_pair("value", F_dv_get(v_p_val__48602, "value")) })) };
                                    }
                                    else
                                    {
                                        return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", false), F_dv_pair("value", false) };
                                    }
                                }
                                else
                                {
                                    if (R.Bool(R.eq(v_op__48241, "not")))
                                    {
                                        object v_p_item__49099 = F_dv_project_value(F_dv_get(v_value, "item"), v_bindings, v_subject);
                                        if (R.Bool(F_dv_get(v_p_item__49099, "known")))
                                        {
                                            return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", true), F_dv_pair("value", F_dv_with(v_value, "item", F_dv_get(v_p_item__49099, "value"))) };
                                        }
                                        else
                                        {
                                            return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", false), F_dv_pair("value", false) };
                                        }
                                    }
                                    else
                                    {
                                        if (R.Bool(R.eq(v_op__48241, "all")))
                                        {
                                            object v_items__49577 = F_dv_get(v_value, "items");
                                            object v_i__49641 = R.number("0");
                                            object v_p_items__49691 = new System.Collections.Generic.List<object>(0) { };
                                            object v_all_known__49748 = true;
                                            while (R.Bool(R.lt(v_i__49641, R.count(v_items__49577))))
                                            {
                                                object v_p_sub__49882 = F_dv_project_value(R.at(v_items__49577, v_i__49641), v_bindings, v_subject);
                                                v_all_known__49748 = F_dv_and(v_all_known__49748, F_dv_get(v_p_sub__49882, "known"));
                                                _ = R.push(v_p_items__49691, F_dv_get(v_p_sub__49882, "value"));
                                                v_i__49641 = R.add(v_i__49641, R.number("1"));
                                            }
                                            if (R.Bool(v_all_known__49748))
                                            {
                                                return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", true), F_dv_pair("value", F_dv_with(v_value, "items", v_p_items__49691)) };
                                            }
                                            else
                                            {
                                                return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", false), F_dv_pair("value", false) };
                                            }
                                        }
                                        else
                                        {
                                            return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", false), F_dv_pair("value", false) };
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            object v_i__50623 = R.number("0");
                            object v_items__50657 = new System.Collections.Generic.List<object>(0) { };
                            object v_known__50696 = true;
                            while (R.Bool(R.lt(v_i__50623, R.count(v_value))))
                            {
                                object v_projected__50794 = F_dv_project_value(R.at(v_value, v_i__50623), v_bindings, v_subject);
                                v_known__50696 = F_dv_and(v_known__50696, F_dv_get(v_projected__50794, "known"));
                                _ = R.push(v_items__50657, F_dv_get(v_projected__50794, "value"));
                                v_i__50623 = R.add(v_i__50623, R.number("1"));
                            }
                            return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", v_known__50696), F_dv_pair("value", v_items__50657) };
                        }
                    }
                }
            }
            else
            {
                object v_i__51113 = R.number("0");
                object v_items__51135 = new System.Collections.Generic.List<object>(0) { };
                object v_known__51162 = true;
                while (R.Bool(R.lt(v_i__51113, R.count(v_value))))
                {
                    object v_projected__51236 = F_dv_project_value(R.at(v_value, v_i__51113), v_bindings, v_subject);
                    v_known__51162 = F_dv_and(v_known__51162, F_dv_get(v_projected__51236, "known"));
                    _ = R.push(v_items__51135, F_dv_get(v_projected__51236, "value"));
                    v_i__51113 = R.add(v_i__51113, R.number("1"));
                }
                return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", v_known__51162), F_dv_pair("value", v_items__51135) };
            }
        }
        else
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("known", true), F_dv_pair("value", v_value) };
        }
    }

    public static object F_dv_observation(object v_id, object v_subject, object v_ref, object v_value, object v_source, object v_origin, object v_time, object v_trust, object v_dependency)
    {
        _ = F_dv_unit(v_trust);
        _ = F_dv_require(R.not(R.lt(v_time, R.number("0"))), "Negative observation time");
        _ = F_dv_require(F_dv_has(F_dv_append(F_dv_list3("perception", "testimony", "recall"), F_dv_pair("inference", "initial")), v_source), "Observations use perception, testimony, recall, inference or initial");
        return F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", v_id), F_dv_pair("subject", v_subject), F_dv_pair("ref", v_ref), F_dv_pair("value", F_dv_clone(v_value)) }, new System.Collections.Generic.List<object>(5) { F_dv_pair("source", v_source), F_dv_pair("origin", v_origin), F_dv_pair("time", v_time), F_dv_pair("trust", v_trust), F_dv_pair("dependency", v_dependency) });
    }

    public static object F_dv_perceive(object v_physical, object v_mind, object v_bindings, object v_grants, object v_event_id, object v_time)
    {
        _ = F_dv_domain(v_physical, "physical");
        _ = F_dv_domain(v_mind, "mind");
        _ = F_dv_validate_bindings(v_bindings, F_dv_get(v_mind, "subject"));
        object v_observations__52271 = new System.Collections.Generic.List<object>(0) { };
        object v_i__52297 = R.number("0");
        object v_subject__52311 = F_dv_get(v_mind, "subject");
        while (R.Bool(R.lt(v_i__52297, R.count(v_grants))))
        {
            object v_ref__52380 = R.at(v_grants, v_i__52297);
            object v_local__52408 = F_dv_local(v_bindings, F_dv_get(v_ref__52380, "object"), v_subject__52311);
            object v_cell__52468 = F_dv_read(v_physical, v_ref__52380);
            if (R.Bool(F_dv_and(R.not(R.eq(v_local__52408, "")), F_dv_get(v_cell__52468, "known"))))
            {
                object v_mapped__52566 = F_dv_project_value(F_dv_get(v_cell__52468, "value"), v_bindings, v_subject__52311);
                object v_local_ref__52639 = F_dv_scoped_ref(v_subject__52311, v_local__52408, F_dv_get(v_ref__52380, "facet"), F_dv_get(v_ref__52380, "field"));
                if (R.Bool(F_dv_get(v_mapped__52566, "known")))
                {
                    _ = R.push(v_observations__52271, F_dv_with(F_dv_observation(F_dv_list3(v_event_id, v_subject__52311, v_local_ref__52639), v_subject__52311, v_local_ref__52639, F_dv_get(v_mapped__52566, "value"), "perception", "direct", v_time, R.number("1"), F_dv_pair(v_event_id, v_local_ref__52639)), "world_version", new System.Collections.Generic.List<object>(2) { F_dv_pair("id", F_dv_get(v_physical, "id")), F_dv_pair("revision", F_dv_get(v_physical, "revision")) }));
                }
            }
            v_i__52297 = R.add(v_i__52297, R.number("1"));
        }
        return F_dv_cognition_observations(v_mind, v_observations__52271);
    }

    public static object F_dv_perceive_changes(object v_receipt, object v_mind, object v_bindings, object v_grants)
    {
        _ = F_dv_require(R.eq(F_dv_get(v_receipt, "status"), "applied"), "Only applied world changes can be perceived");
        object v_changes__53237 = F_dv_get(v_receipt, "changes");
        object v_exposed__53271 = new System.Collections.Generic.List<object>(0) { };
        object v_i__53292 = R.number("0");
        while (R.Bool(R.lt(v_i__53292, R.count(v_changes__53237))))
        {
            object v_ref__53345 = F_dv_get(R.at(v_changes__53237, v_i__53292), "ref");
            if (R.Bool(F_dv_has(v_grants, v_ref__53345)))
            {
                _ = R.push(v_exposed__53271, v_ref__53345);
            }
            v_i__53292 = R.add(v_i__53292, R.number("1"));
        }
        return F_dv_perceive(F_dv_get(v_receipt, "world"), v_mind, v_bindings, v_exposed__53271, F_dv_get(v_receipt, "event"), F_dv_get(v_receipt, "time"));
    }

    public static object F_dv_observation_profile(object v_source_weights, object v_threshold)
    {
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("weights", v_source_weights), F_dv_pair("threshold", F_dv_unit(v_threshold)) };
    }

    public static object F_dv_weight(object v_observation, object v_profile)
    {
        object v_weights__53716 = F_dv_get(v_profile, "weights");
        object v_source__53750 = F_dv_get(v_observation, "source");
        object v_weight__53786 = F_dv_find(v_weights__53716, v_source__53750, R.number("1"));
        _ = F_dv_require(R.not(R.lt(v_weight__53786, R.number("0"))), "Negative evidence weight");
        return R.mul(v_weight__53786, F_dv_unit(F_dv_get(v_observation, "trust")));
    }

    public static object F_dv_replace_claim(object v_claims, object v_observation)
    {
        object v_result__53986 = new System.Collections.Generic.List<object>(0) { };
        object v_i__54006 = R.number("0");
        while (R.Bool(R.lt(v_i__54006, R.count(v_claims))))
        {
            object v_old__54058 = R.at(v_claims, v_i__54006);
            if (R.Bool(F_dv_same(F_dv_get(v_old__54058, "dependency"), F_dv_get(v_observation, "dependency"))))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_result__53986, F_dv_clone(v_old__54058));
            }
            v_i__54006 = R.add(v_i__54006, R.number("1"));
        }
        _ = R.push(v_result__53986, F_dv_clone(v_observation));
        return v_result__53986;
    }

    public static object F_dv_aggregate(object v_claims, object v_profile, object v_time)
    {
        object v_alternatives__54339 = new System.Collections.Generic.List<object>(0) { };
        object v_i__54365 = R.number("0");
        object v_total__54379 = R.number("0");
        object v_best__54397 = R.number("0");
        object v_chosen__54414 = false;
        object v_best_rel__54437 = R.number("0");
        while (R.Bool(R.lt(v_i__54365, R.count(v_claims))))
        {
            object v_claim__54496 = R.at(v_claims, v_i__54365);
            object v_weight__54526 = F_dv_weight(v_claim__54496, v_profile);
            object v_value__54573 = F_dv_get(v_claim__54496, "value");
            object v_j__54605 = R.number("0");
            object v_found__54623 = false;
            object v_new_items__54649 = new System.Collections.Generic.List<object>(0) { };
            while (R.Bool(R.lt(v_j__54605, R.count(v_alternatives__54339))))
            {
                object v_alternative__54724 = R.at(v_alternatives__54339, v_j__54605);
                if (R.Bool(F_dv_same(R.at(v_alternative__54724, R.number("0")), v_value__54573)))
                {
                    _ = R.push(v_new_items__54649, F_dv_list3(v_value__54573, R.add(R.at(v_alternative__54724, R.number("1")), v_weight__54526), F_dv_max(R.at(v_alternative__54724, R.number("2")), F_dv_unit(F_dv_get(v_claim__54496, "trust")))));
                    v_found__54623 = true;
                }
                else
                {
                    _ = R.push(v_new_items__54649, v_alternative__54724);
                }
                v_j__54605 = R.add(v_j__54605, R.number("1"));
            }
            if (R.Bool(F_dv_or(v_found__54623, R.eq(v_weight__54526, R.number("0")))))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_new_items__54649, F_dv_list3(v_value__54573, v_weight__54526, F_dv_unit(F_dv_get(v_claim__54496, "trust"))));
            }
            v_alternatives__54339 = v_new_items__54649;
            v_total__54379 = R.add(v_total__54379, v_weight__54526);
            v_i__54365 = R.add(v_i__54365, R.number("1"));
        }
        v_i__54365 = R.number("0");
        object v_distribution__55299 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(R.lt(v_i__54365, R.count(v_alternatives__54339))))
        {
            object v_alternative__55369 = R.at(v_alternatives__54339, v_i__54365);
            object v_weight__55411 = R.at(v_alternative__55369, R.number("1"));
            object v_rel__55447 = R.at(v_alternative__55369, R.number("2"));
            if (R.Bool(R.lt(v_best__54397, v_weight__55411)))
            {
                v_best__54397 = v_weight__55411;
                v_chosen__54414 = R.at(v_alternative__55369, R.number("0"));
                v_best_rel__54437 = v_rel__55447;
            }
            object v_local_1__55597 = false;
            if (R.Bool(R.eq(v_total__54379, R.number("0"))))
            {
                v_local_1__55597 = R.number("0");
            }
            else
            {
                v_local_1__55597 = R.div(v_weight__55411, v_total__54379);
            }
            _ = R.push(v_distribution__55299, F_dv_pair(R.at(v_alternative__55369, R.number("0")), v_local_1__55597));
            v_i__54365 = R.add(v_i__54365, R.number("1"));
        }
        object v_local_2__55807 = false;
        if (R.Bool(R.eq(v_total__54379, R.number("0"))))
        {
            v_local_2__55807 = R.number("0");
        }
        else
        {
            v_local_2__55807 = R.div(v_best__54397, v_total__54379);
        }
        object v_support_share__55915 = v_local_2__55807;
        object v_epistemic_confidence__56106 = R.mul(v_support_share__55915, v_best_rel__54437);
        object v_cell__56164 = F_dv_cell(v_chosen__54414, v_epistemic_confidence__56106, v_time, v_claims);
        return F_dv_with_many(v_cell__56164, new System.Collections.Generic.List<object>(6) { F_dv_pair("known", F_dv_and(R.lt(R.number("0"), v_total__54379), R.not(R.lt(v_epistemic_confidence__56106, F_dv_get(v_profile, "threshold"))))), F_dv_pair("conflicted", R.lt(R.number("1"), R.count(v_alternatives__54339))), F_dv_pair("claims", v_claims), F_dv_pair("alternatives", v_distribution__55299), F_dv_pair("support_share", v_support_share__55915), F_dv_pair("source_reliability", v_best_rel__54437) });
    }

    public static object F_dv_update_mind(object v_mind, object v_observations, object v_expected_revision, object v_profile)
    {
        _ = F_dv_domain(v_mind, "mind");
        _ = F_dv_require(R.eq(F_dv_get(v_mind, "revision"), v_expected_revision), "Stale cognition revision");
        v_observations = F_dv_cognition_observations(v_mind, v_observations);
        object v_next__56741 = F_dv_clone(v_mind);
        object v_i__56771 = R.number("0");
        object v_accepted__56785 = new System.Collections.Generic.List<object>(0) { };
        object v_dispositions__56807 = new System.Collections.Generic.List<object>(0) { };
        object v_latest_time__56833 = F_dv_find(v_mind, "time", R.number("0"));
        while (R.Bool(R.lt(v_i__56771, R.count(v_observations))))
        {
            object v_observation__56924 = R.at(v_observations, v_i__56771);
            object v_id__56966 = F_dv_get(v_observation__56924, "id");
            object v_ref__56998 = F_dv_get(v_observation__56924, "ref");
            object v_object_id__57032 = F_dv_get(v_ref__56998, "object");
            _ = F_dv_require(R.eq(F_dv_get(v_observation__56924, "subject"), F_dv_get(v_mind, "subject")), "Foreign observation");
            _ = F_dv_require(R.not(F_dv_has(F_dv_get(v_next__56741, "seen"), v_id__56966)), "Repeated observation");
            _ = F_dv_weight(v_observation__56924, v_profile);
            if (R.Bool(F_dv_has_key(F_dv_get(v_next__56741, "objects"), v_object_id__57032)))
            {
                _ = false;
            }
            else
            {
                v_next__56741 = F_dv_add_object(v_next__56741, F_dv_object(v_object_id__57032, "perceived", new System.Collections.Generic.List<object>(0) { }));
            }
            object v_old__57418 = F_dv_read(v_next__56741, v_ref__56998);
            object v_time__57455 = F_dv_get(v_observation__56924, "time");
            v_latest_time__56833 = F_dv_max(v_latest_time__56833, v_time__57455);
            if (R.Bool(R.lt(v_time__57455, F_dv_get(v_old__57418, "time"))))
            {
                _ = R.push(v_dispositions__56807, F_dv_pair(v_id__56966, "older-than-current"));
            }
            else
            {
                object v_is_perception__57656 = R.eq(F_dv_get(v_observation__56924, "source"), "perception");
                object v_version__57725 = F_dv_find(v_observation__56924, "world_version", false);
                object v_previous_version__57796 = F_dv_find(v_old__57418, "world_version", false);
                object v_later_version__57868 = false;
                if (R.Bool((R.Bool(R.not(R.eq(v_version__57725, false))) ? (object)(R.not(R.eq(v_previous_version__57796, false))) : (object)(false))))
                {
                    v_later_version__57868 = (R.Bool(R.eq(F_dv_get(v_version__57725, "id"), F_dv_get(v_previous_version__57796, "id"))) ? (object)(R.lt(F_dv_get(v_previous_version__57796, "revision"), F_dv_get(v_version__57725, "revision"))) : (object)(false));
                }
                object v_replaces_current__58088 = (R.Bool(v_is_perception__57656) ? (object)((R.Bool(R.lt(F_dv_get(v_old__57418, "time"), v_time__57455)) ? (object)(true) : (object)(v_later_version__57868))) : (object)(false));
                object v_local_1__58176 = false;
                if (R.Bool(v_replaces_current__58088))
                {
                    v_local_1__58176 = new System.Collections.Generic.List<object>(0) { };
                }
                else
                {
                    v_local_1__58176 = F_dv_get(v_old__57418, "claims");
                }
                object v_claims__58325 = v_local_1__58176;
                if (R.Bool(F_dv_and(F_dv_get(v_old__57418, "known"), R.eq(R.count(v_claims__58325), R.number("0")))))
                {
                    if (R.Bool(R.not(v_replaces_current__58088)))
                    {
                        v_claims__58325 = F_dv_single(F_dv_with_many(new System.Collections.Generic.List<object>(4) { F_dv_pair("id", F_dv_pair("initial", v_ref__56998)), F_dv_pair("subject", F_dv_get(v_next__56741, "subject")), F_dv_pair("ref", v_ref__56998), F_dv_pair("value", F_dv_get(v_old__57418, "value")) }, new System.Collections.Generic.List<object>(5) { F_dv_pair("source", "initial"), F_dv_pair("origin", "initial"), F_dv_pair("time", F_dv_get(v_old__57418, "time")), F_dv_pair("trust", F_dv_get(v_old__57418, "confidence")), F_dv_pair("dependency", F_dv_pair("initial", v_ref__56998)) }));
                    }
                }
                object v_new_time__58729 = F_dv_max(F_dv_get(v_old__57418, "time"), v_time__57455);
                object v_updated__58779 = F_dv_aggregate(F_dv_replace_claim(v_claims__58325, v_observation__56924), v_profile, v_new_time__58729);
                if (R.Bool((R.Bool(v_is_perception__57656) ? (object)(R.not(R.eq(v_version__57725, false))) : (object)(false))))
                {
                    v_updated__58779 = F_dv_with(v_updated__58779, "world_version", v_version__57725);
                }
                else
                {
                    if (R.Bool(R.not(R.eq(v_previous_version__57796, false))))
                    {
                        v_updated__58779 = F_dv_with(v_updated__58779, "world_version", v_previous_version__57796);
                    }
                }
                v_next__56741 = F_dv_stage_store_owned(v_next__56741, v_ref__56998, v_updated__58779);
                _ = R.push(v_dispositions__56807, F_dv_pair(v_id__56966, "updated"));
            }
            _ = R.push(F_dv_get(v_next__56741, "seen"), F_dv_clone(v_id__56966));
            _ = R.push(v_accepted__56785, F_dv_clone(v_observation__56924));
            v_i__56771 = R.add(v_i__56771, R.number("1"));
        }
        object v_local_2__59366 = false;
        if (R.Bool(R.eq(R.count(v_observations), R.number("0"))))
        {
            v_local_2__59366 = v_expected_revision;
        }
        else
        {
            v_local_2__59366 = R.add(v_expected_revision, R.number("1"));
        }
        _ = R.push(F_dv_get(v_next__56741, "history"), new System.Collections.Generic.List<object>(3) { F_dv_pair("kind", "observation-batch"), F_dv_pair("observations", v_accepted__56785), F_dv_pair("dispositions", v_dispositions__56807) });
        v_next__56741 = F_dv_with_many(v_next__56741, new System.Collections.Generic.List<object>(2) { F_dv_pair("time", v_latest_time__56833), F_dv_pair("revision", v_local_2__59366) });
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("world", v_next__56741), F_dv_pair("dispositions", v_dispositions__56807), F_dv_pair("revision", F_dv_get(v_next__56741, "revision")) };
    }

    public static object F_dv_segment(object v_content, object v_strength, object v_band)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("content", v_content), F_dv_pair("strength", v_strength), F_dv_pair("band", v_band) };
    }

    public static object F_dv_signal(object v_location, object v_medium, object v_segments)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("location", F_dv_entity_ref(v_location)), F_dv_pair("medium", v_medium), F_dv_pair("segments", v_segments) };
    }

    public static object F_dv_filter_signal(object v_segments, object v_gain, object v_threshold, object v_bands)
    {
        object v_result__60210 = new System.Collections.Generic.List<object>(0) { };
        object v_i__60230 = R.number("0");
        while (R.Bool(R.lt(v_i__60230, R.count(v_segments))))
        {
            object v_segment__60284 = R.at(v_segments, v_i__60230);
            object v_strength__60318 = R.mul(F_dv_get(v_segment__60284, "strength"), v_gain);
            if (R.Bool(F_dv_and(R.not(R.lt(v_strength__60318, v_threshold)), F_dv_or(R.eq(R.count(v_bands), R.number("0")), F_dv_has(v_bands, F_dv_get(v_segment__60284, "band"))))))
            {
                _ = R.push(v_result__60210, F_dv_segment(F_dv_get(v_segment__60284, "content"), v_strength__60318, F_dv_get(v_segment__60284, "band")));
            }
            v_i__60230 = R.add(v_i__60230, R.number("1"));
        }
        return v_result__60210;
    }

    public static object F_dv_transport(object v_world, object v_path, object v_signal, object v_threshold)
    {
        _ = F_dv_require(R.not(R.lt(v_threshold, R.number("0"))), "Negative perception threshold");
        object v_i__60712 = R.number("0");
        object v_location__60726 = F_dv_get(v_signal, "location");
        object v_segments__60761 = F_dv_clone(F_dv_get(v_signal, "segments"));
        object v_status__60806 = "delivered";
        object v_trace__60835 = new System.Collections.Generic.List<object>(0) { };
        while (R.Bool(F_dv_and(R.eq(v_status__60806, "delivered"), R.lt(v_i__60712, R.count(v_path)))))
        {
            object v_channel__60923 = R.at(v_path, v_i__60712);
            object v_objects__60953 = F_dv_get(v_world, "objects");
            _ = F_dv_require(F_dv_has_key(v_objects__60953, v_channel__60923), "Unknown channel");
            _ = F_dv_require(R.eq(F_dv_truth(v_world, F_dv_fact(F_dv_ref(v_channel__60923, "identity", "kind"), "channel")), "true"), "Transport object must be known as a channel");
            object v_from__61209 = F_dv_read(v_world, F_dv_ref(v_channel__60923, "link", "from"));
            object v_to__61276 = F_dv_read(v_world, F_dv_ref(v_channel__60923, "link", "to"));
            object v_medium__61339 = F_dv_read(v_world, F_dv_ref(v_channel__60923, "link", "medium"));
            object v_gate__61410 = F_dv_read(v_world, F_dv_ref(v_channel__60923, "link", "gate"));
            object v_enabled__61477 = F_dv_read(v_world, F_dv_ref(v_channel__60923, "transport", "enabled"));
            object v_gain__61555 = F_dv_read(v_world, F_dv_ref(v_channel__60923, "transport", "gain"));
            if (R.Bool(F_dv_and(F_dv_and(F_dv_uncontested(v_from__61209), F_dv_uncontested(v_to__61276)), F_dv_and(F_dv_and(F_dv_uncontested(v_medium__61339), F_dv_uncontested(v_gate__61410)), F_dv_and(F_dv_uncontested(v_enabled__61477), F_dv_uncontested(v_gain__61555))))))
            {
                _ = F_dv_require(F_dv_same(v_location__60726, F_dv_get(v_from__61209, "value")), "Discontinuous channel path");
                _ = F_dv_require(R.eq(F_dv_get(v_signal, "medium"), F_dv_get(v_medium__61339, "value")), "Channel medium mismatch");
                object v_gate_truth__61984 = F_dv_truth(v_world, F_dv_get(v_gate__61410, "value"));
                if (R.Bool(F_dv_and(F_dv_get(v_enabled__61477, "value"), R.eq(v_gate_truth__61984, "true"))))
                {
                    object v_bands_cell__62107 = F_dv_read(v_world, F_dv_ref(v_channel__60923, "transport", "bands"));
                    if (R.Bool(F_dv_uncontested(v_bands_cell__62107)))
                    {
                        v_segments__60761 = F_dv_filter_signal(v_segments__60761, F_dv_unit(F_dv_get(v_gain__61555, "value")), v_threshold, F_dv_get(v_bands_cell__62107, "value"));
                        v_location__60726 = F_dv_get(v_to__61276, "value");
                        _ = R.push(v_trace__60835, v_channel__60923);
                    }
                    else
                    {
                        v_status__60806 = "unknown";
                    }
                }
                else
                {
                    object v_local_1__62512 = false;
                    if (R.Bool(R.eq(v_gate_truth__61984, "unknown")))
                    {
                        v_local_1__62512 = "unknown";
                    }
                    else
                    {
                        v_local_1__62512 = "blocked";
                    }
                    v_status__60806 = v_local_1__62512;
                }
            }
            else
            {
                v_status__60806 = "unknown";
            }
            v_i__60712 = R.add(v_i__60712, R.number("1"));
        }
        object v_local_2__62782 = false;
        if (R.Bool(R.eq(v_status__60806, "delivered")))
        {
            v_local_2__62782 = v_segments__60761;
        }
        else
        {
            v_local_2__62782 = new System.Collections.Generic.List<object>(0) { };
        }
        return F_dv_with(new System.Collections.Generic.List<object>(4) { F_dv_pair("status", v_status__60806), F_dv_pair("location", v_location__60726), F_dv_pair("segments", v_local_2__62782), F_dv_pair("trace", v_trace__60835) }, "world_revision", F_dv_get(v_world, "revision"));
    }

    public static object F_dv_hear(object v_delivery, object v_subject, object v_local_ref, object v_perceived_origin, object v_event_id, object v_time)
    {
        _ = F_dv_require(R.eq(F_dv_get(v_delivery, "status"), "delivered"), "No delivered signal");
        _ = F_dv_require(R.eq(F_dv_find(v_delivery, "hypothetical", true), false), "Predicted or unauthorised transmission cannot become perception");
        return F_dv_observation(v_event_id, v_subject, v_local_ref, F_dv_get(v_delivery, "segments"), "perception", v_perceived_origin, v_time, R.number("1"), v_event_id);
    }

    public static object F_dv_transport_theory(object v_law)
    {
        return F_dv_with(v_law, "operation", "attenuate-filter");
    }

    public static object F_dv_transmit(object v_world, object v_law_name, object v_path, object v_signal, object v_threshold)
    {
        object v_adoption__63583 = F_dv_get(F_dv_get(v_world, "adoptions"), v_law_name);
        object v_law__63636 = F_dv_get(v_adoption__63583, "law");
        _ = F_dv_require(F_dv_get(v_adoption__63583, "active"), "Transmission law is not active");
        _ = F_dv_require(R.eq(F_dv_get(v_law__63636, "operation"), "attenuate-filter"), "Not a transport theory");
        object v_local_1__63809 = false;
        if (R.Bool(R.eq(F_dv_get(v_world, "domain"), "physical")))
        {
            v_local_1__63809 = R.eq(F_dv_get(v_adoption__63583, "mode"), "enforce");
        }
        else
        {
            v_local_1__63809 = F_dv_and(R.eq(F_dv_get(v_world, "domain"), "mind"), R.eq(F_dv_get(v_adoption__63583, "mode"), "predict"));
        }
        _ = F_dv_require(v_local_1__63809, "Wrong transmission adoption");
        _ = F_dv_require(R.eq(F_dv_truth(v_world, F_dv_get(v_law__63636, "premise")), "true"), "Transmission premise is not established");
        return F_dv_with_many(F_dv_transport(v_world, v_path, v_signal, v_threshold), new System.Collections.Generic.List<object>(2) { F_dv_pair("basis", F_dv_get(v_adoption__63583, "basis")), F_dv_pair("hypothetical", R.not(R.eq(F_dv_get(v_world, "domain"), "physical"))) });
    }

    public static object F_dv_cognition_hidden(object v_mind, object v_object)
    {
        return F_dv_has(F_dv_find(F_dv_find(v_mind, "CognitionFilter", new System.Collections.Generic.List<object>(0) { }), "hidden_objects", new System.Collections.Generic.List<object>(0) { }), v_object);
    }

    public static object F_dv_cognition_observations(object v_mind, object v_observations)
    {
        object v_filter__6875 = F_dv_find(v_mind, "CognitionFilter", new System.Collections.Generic.List<object>(0) { });
        if (R.Bool(F_dv_find(v_filter__6875, "anterograde", false)))
        {
            return new System.Collections.Generic.List<object>(0) { };
        }
        object v_accepted__6993 = new System.Collections.Generic.List<object>(0) { };
        object v_i__7015 = R.number("0");
        while (R.Bool(R.lt(v_i__7015, R.count(v_observations))))
        {
            if (R.Bool(R.not(F_dv_cognition_hidden(v_mind, F_dv_get(F_dv_get(R.at(v_observations, v_i__7015), "ref"), "object")))))
            {
                _ = R.push(v_accepted__6993, R.at(v_observations, v_i__7015));
            }
            v_i__7015 = R.add(v_i__7015, R.number("1"));
        }
        return v_accepted__6993;
    }
}
}

// --- Source: PerceptionBindings.cs ---
namespace DuelView.Runtime
{


/// <summary>Maps only explicitly declared physical identities into one owner's local
/// vocabulary. An unbound reference cannot enter that owner's evidence.</summary>
public sealed class PerceptionBindings
{
    private readonly Dictionary<(string Actor, string Physical), string> local = [];
    private static readonly JsonNode Unmapped = new JsonObject();

    public PerceptionBindings(JsonElement bindings)
    {
        foreach (var binding in bindings.Items())
        {
            var actor = binding.Get("subject").Text();
            var physical = binding.Get("physical").Text();
            var target = binding.Get("local").Text();
            if (actor.Length == 0 || physical.Length == 0 || target.Length == 0 ||
                local.TryGetValue((actor, physical), out var existing) && existing != target)
                throw new InvalidDataException("Ambiguous or invalid perception binding.");
            local[(actor, physical)] = target;
        }
    }

    public FieldKey? Project(string actor, FieldKey physical) => local.TryGetValue((actor, physical.Object), out var target)
        ? physical with { Object = target } : null;

    public JsonElement? Project(string actor, JsonElement value)
    {
        var projected = Visit(actor, value, 0);
        return ReferenceEquals(projected, Unmapped) ? null : JsonSerializer.SerializeToElement(projected);
    }

    private JsonNode? Visit(string actor, JsonElement value, int depth)
    {
        if (depth > 128) throw new InvalidDataException("Perception value depth exceeded.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.Get("type").Text() == "ref")
            {
                var scope = value.Get("subject").Text();
                if (scope.Length > 0 && scope != actor) return Unmapped;
                var key = Project(actor, FieldKey.Read(value, actor));
                if (key is null) return Unmapped;
                return new JsonObject { ["type"] = "ref", ["subject"] = actor,
                    ["object"] = key.Value.Object, ["facet"] = key.Value.Facet, ["field"] = key.Value.Field };
            }
            var result = new JsonObject();
            foreach (var item in value.EnumerateObject())
            {
                var child = Visit(actor, item.Value, depth + 1);
                if (ReferenceEquals(child, Unmapped)) return Unmapped;
                result.Add(item.Name, child);
            }
            return result;
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            var result = new JsonArray();
            foreach (var item in value.EnumerateArray())
            {
                var child = Visit(actor, item, depth + 1);
                if (ReferenceEquals(child, Unmapped)) return Unmapped;
                result.Add(child);
            }
            return result;
        }
        if (value.ValueKind == JsonValueKind.Undefined) return Unmapped;
        if (value.ValueKind == JsonValueKind.Null) return null;
        return JsonNode.Parse(value.GetRawText());
    }
}
}
