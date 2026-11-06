// @mechanism: dual_ontology_state
// @layer: core/ontology
// @contracts: References

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

// --- Source: References.cs ---
namespace DuelView.Core
{


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
}

// --- Source: Context.cs ---
namespace DuelView.Compiled
{


// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
    // Value-storage and query mechanisms only. Hosts supply their validated
    // session policy before world initialization; standalone Core calls keep
    // the environment defaults captured by their own execution context.
    public readonly record struct RuntimeOptions(
        bool LazyInternal, bool Cow, bool Mask, bool MaskVerify,
        bool AppendShare, bool ReadView, bool RecordCache, bool PlanCache,
        bool DeclarationCache, bool Predicate, bool PredicateCache,
        bool StateIndex, bool RuntimeSpeed);

    // Cache ownership follows a session. Standalone host callers receive
    // their own execution-flow context on first use.
    public sealed class ValueContext
    {
        internal readonly object Storage = new ValueCaches();
    }
    private sealed class ValueCaches
    {
        internal readonly ConditionalWeakTable<FrozenList, DirtyQueues> DirtyIndices = new();
        internal readonly object SnapshotLock = new();
        internal RuntimeOptions Options = new(
            LazyInternal: Environment.GetEnvironmentVariable("DV_OPT_LAZYINTERNAL") == "1",
            Cow: Environment.GetEnvironmentVariable("DV_OPT_COW") == "1",
            Mask: Environment.GetEnvironmentVariable("DV_OPT_MASK") == "1",
            MaskVerify: Environment.GetEnvironmentVariable("DV_OPT_MASK_VERIFY") == "1",
            AppendShare: Environment.GetEnvironmentVariable("DV_OPT_APPENDSHARE") != "0",
            ReadView: Environment.GetEnvironmentVariable("DV_OPT_READVIEW") == "1",
            RecordCache: Environment.GetEnvironmentVariable("DV_OPT_RECORDCACHE") == "1",
            PlanCache: Environment.GetEnvironmentVariable("DV_OPT_PLANCACHE") != "0",
            DeclarationCache: Environment.GetEnvironmentVariable("DV_OPT_DECLCACHE") != "0",
            Predicate: Environment.GetEnvironmentVariable("DV_OPT_PREDICATE") == "1",
            PredicateCache: Environment.GetEnvironmentVariable("DV_OPT_PREDICATECACHE") == "1",
            StateIndex: Environment.GetEnvironmentVariable("DV_OPT_STATEINDEX") != "0",
            RuntimeSpeed: Environment.GetEnvironmentVariable("DV_OPT_RUNTIMESPEED") != "0");
        internal bool RuntimeOptionsFixed;
        internal bool LazyInternalEnabled => Options.LazyInternal;
        internal bool CowEnabled => Options.Cow;
        internal bool MaskEnabled => Options.Mask;
        internal bool MaskVerify => Options.MaskVerify;
        internal readonly ConditionalWeakTable<FrozenList, CellFlags> CellMasks = new();
        // Certified immutable append prefixes bound copying of growing logs.
        // Replacement and nested writes invalidate them; 0 keeps flat storage.
        internal bool AppendShareEnabled => Options.AppendShare;
        internal bool ReadViewEnabled => Options.ReadView;
        internal bool RecordCacheEnabled => Options.RecordCache;
        internal bool PlanCacheEnabled => Options.PlanCache;
        internal bool DeclarationCacheEnabled => Options.DeclarationCache;
        internal bool PredicateEnabled => Options.Predicate;
        internal bool PredicateCacheEnabled => Options.PredicateCache;
        internal readonly ConditionalWeakTable<FrozenList, PredicateCellCache> PredicateCells = new();
        internal readonly ConditionalWeakTable<FrozenList, RecordShape> RecordShapes = new();
        internal readonly ConditionalWeakTable<FrozenList, RefLookupText> RefLookupTexts = new();
        internal readonly FrozenList EmptySnapshot = new(Array.Empty<object>(), unchecked(14695981039346656037UL * 1099511628211UL), 14695981039346656037UL)
        {
            CompatibleSequenceHash = 14695981039346656037UL,
            MembershipHash = unchecked(14695981039346656037UL * 1099511628211UL) ^ 5UL,
            MembershipHashChecked = true,
            MembershipHashEligible = true
        };
        internal readonly MutableStateTable MutableStates = new();
        internal bool StateIndexEnabled => Options.StateIndex;
        internal bool RuntimeSpeedEnabled => Options.RuntimeSpeed;
        internal readonly ConditionalWeakTable<FrozenList, ValueMemo> ValueMemos = new();
        internal object? MemoReadMiss;
        internal readonly ConditionalWeakTable<FrozenList, JsonEncoding> JsonEncodings = new();
        internal readonly Dictionary<ulong, List<WeakReference<FrozenList>>> SnapshotPool = new();
        internal readonly Queue<ulong> SnapshotBucketOrder = new();
        internal int SnapshotMaintenanceTicks;
        internal int SnapshotGeneration = GC.CollectionCount(2);
        internal int SnapshotSweepRemaining;
        internal readonly Dictionary<ulong, List<RecordLayout>> RecordLayouts = new();
        internal int RecordLayoutCount;
        internal long RecordLayoutCharacters;
        internal readonly object RawLock = new();
        internal RawDagWriter? RawEvidence;
        internal bool BinaryRecording;
    }
    private static readonly AsyncLocal<ValueContext?> ValuesContext = new();
    private static ValueCaches Caches => (ValueCaches)(ValuesContext.Value ??= new ValueContext()).Storage;
    public static IDisposable UseValues(ValueContext context) => new ValueScope(context);
    public static RuntimeOptions CurrentRuntimeOptions
    {
        get
        {
            var caches = Caches;
            lock (caches.SnapshotLock) return caches.Options;
        }
    }
    public static bool RuntimeOptionsFixed
    {
        get
        {
            var caches = Caches;
            lock (caches.SnapshotLock) return caches.RuntimeOptionsFixed;
        }
    }
    // Configuration is execution-context local and may only change once.
    // Reapplying the same policy is harmless; a running session cannot switch
    // mechanisms under its existing snapshots, indexes or memoized values.
    public static void ConfigureRuntimeOptions(RuntimeOptions options)
    {
        var caches = Caches;
        lock (caches.SnapshotLock)
        {
            if (caches.RuntimeOptionsFixed)
            {
                if (caches.Options != options)
                    throw new InvalidOperationException("Runtime options are fixed for this value context.");
                return;
            }
            caches.Options = options;
            caches.RuntimeOptionsFixed = true;
        }
    }
    public static void FixRuntimeOptions()
    {
        var caches = Caches;
        lock (caches.SnapshotLock) caches.RuntimeOptionsFixed = true;
    }
    private static readonly AsyncLocal<object?> ContentContext = new();
    public static IDisposable UseContent(object definitions) => new ContentScope(definitions);
    private sealed class ContentScope : IDisposable
    {
        private readonly object? previous = ContentContext.Value;
        public ContentScope(object definitions) => ContentContext.Value = definitions;
        public void Dispose() => ContentContext.Value = previous;
    }
    public static object content(object category, object id)
    {
        var definitions = ContentContext.Value ?? throw new InvalidOperationException("Content catalog is not installed for this session");
        return record_find(record_find(definitions, category, new List<object>()), id, false);
    }
    private sealed class ValueScope : IDisposable
    {
        private readonly ValueContext? previous = ValuesContext.Value;
        internal ValueScope(ValueContext context) => ValuesContext.Value = context ?? throw new ArgumentNullException(nameof(context));
        public void Dispose() => ValuesContext.Value = previous;
    }
    private static readonly AsyncLocal<Action<string, string>?> PhaseContext = new();
    public static IDisposable UsePhases(Action<string, string>? observer) => new PhaseScope(observer);
    private sealed class PhaseScope : IDisposable
    {
        private readonly Action<string, string>? previous = PhaseContext.Value;
        public PhaseScope(Action<string, string>? observer) => PhaseContext.Value = observer;
        public void Dispose() => PhaseContext.Value = previous;
    }
    public static object phase(object stage, object actor)
    {
        PhaseContext.Value?.Invoke(Text(stage), Text(actor));
        return true;
    }
    // Execution-context local: concurrent host sessions never change one another's policy.
    // Standalone host/audit programs retain complete evidence by default.
    private static readonly AsyncLocal<bool?> AuditContext = new();
    public static bool AuditMode { get => AuditContext.Value ?? true; set => AuditContext.Value = value; }
    public static object audit_mode() => AuditMode;
    public static IDisposable UseAudit(bool enabled) => new AuditScope(enabled);
    private sealed class AuditScope : IDisposable
    {
        private readonly bool? previous = AuditContext.Value;
        public AuditScope(bool enabled) => AuditContext.Value = enabled;
        public void Dispose() => AuditContext.Value = previous;
    }
    private static readonly AsyncLocal<bool?> RawRecordingContext = new();
    public static IDisposable UseRawRecording(bool enabled) => new RawRecordingScope(enabled);
    private sealed class RawRecordingScope : IDisposable
    {
        private readonly bool? previous = RawRecordingContext.Value;
        public RawRecordingScope(bool enabled) => RawRecordingContext.Value = enabled;
        public void Dispose() => RawRecordingContext.Value = previous;
    }
    private static readonly AsyncLocal<TextWriter?> ConsoleOutputContext = new();
    public static IDisposable UseOutput(TextWriter writer) => new ConsoleOutputScope(writer);
    private sealed class ConsoleOutputScope : IDisposable
    {
        private readonly TextWriter? previous = ConsoleOutputContext.Value;
        public ConsoleOutputScope(TextWriter writer) => ConsoleOutputContext.Value = writer;
        public void Dispose() => ConsoleOutputContext.Value = previous;
    }
    // Reuse only a previously validated snapshot. Freezing an arbitrary fresh
    // graph here could inspect unused malformed/cyclic branches before the
    // original permission/field error. memo_get has already prepared these
    // backbones on the hot dv_truth miss path.
}
}

// --- Source: Environment.cs ---
namespace DuelView.Core
{


/// <summary>World / Environment: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_dirty_environment_index(object v_world, object v_processes)
    {
        v_world = R.snapshot(v_world);
        v_processes = R.snapshot(v_processes);
        object v_key__355750 = new System.Collections.Generic.List<object>(2) { "dirty-environment-index-v1", F_dv_get(v_world, "adoptions") };
        object v_cached__355812 = R.memo_read(v_processes, v_key__355750);
        if (R.Bool(R.at(v_cached__355812, R.number("0"))))
        {
            return R.at(v_cached__355812, R.number("1"));
        }
        object v_index__355900 = new System.Collections.Generic.List<object>(0) { };
        object v_i__355919 = R.number("0");
        while (R.Bool(R.lt(v_i__355919, R.count(v_processes))))
        {
            object v_law__355972 = F_dv_get(F_dv_get(F_dv_get(v_world, "adoptions"), F_dv_get(R.at(v_processes, v_i__355919), "law")), "law");
            object v_refs__356036 = F_dvs_dirty_refs(F_dv_get(v_law__355972, "premise"));
            object v_j__356083 = R.number("0");
            while (R.Bool(R.lt(v_j__356083, R.count(F_dv_get(v_law__355972, "effects")))))
            {
                object v_effect__356146 = R.at(F_dv_get(v_law__355972, "effects"), v_j__356083);
                _ = F_dv_require((R.Bool(R.eq(F_dv_get(v_effect__356146, "op"), "assign")) ? (object)(true) : (object)(R.eq(F_dv_get(v_effect__356146, "op"), "increment"))), R.cat("Unindexed effect operator: ", F_dv_get(v_effect__356146, "op")));
                v_refs__356036 = F_dvp_merge_refs(v_refs__356036, new System.Collections.Generic.List<object>(1) { F_dv_get(v_effect__356146, "ref") });
                v_j__356083 = R.add(v_j__356083, R.number("1"));
            }
            v_j__356083 = R.number("0");
            while (R.Bool(R.lt(v_j__356083, R.count(v_refs__356036))))
            {
                object v_subject__356508 = F_dv_find(R.at(v_refs__356036, v_j__356083), "subject", "");
                _ = F_dv_require((R.Bool(R.eq(v_subject__356508, "")) ? (object)(true) : (object)(R.eq(v_subject__356508, F_dv_get(v_world, "subject")))), "Cross-subject reference access");
                v_j__356083 = R.add(v_j__356083, R.number("1"));
            }
            _ = R.push(v_index__355900, new System.Collections.Generic.List<object>(2) { F_dv_pair("law", F_dv_get(R.at(v_processes, v_i__355919), "law")), F_dv_pair("refs", v_refs__356036) });
            v_i__355919 = R.add(v_i__355919, R.number("1"));
        }
        return R.memo_put(v_processes, v_key__355750, R.snapshot(v_index__355900));
    }

    public static object F_dvs_dirty_verify_skipped(object v_world, object v_processes, object v_first, object v_until, object v_time)
    {
        v_world = R.snapshot(v_world);
        v_processes = R.snapshot(v_processes);
        object v_i__356889 = v_first;
        while (R.Bool(R.lt(v_i__356889, v_until)))
        {
            object v_adoption__356932 = F_dv_get(F_dv_get(v_world, "adoptions"), F_dv_get(R.at(v_processes, v_i__356889), "law"));
            if (R.Bool((R.Bool((R.Bool(F_dv_get(v_adoption__356932, "active")) ? (object)(R.eq(F_dv_get(v_adoption__356932, "mode"), "enforce")) : (object)(false))) ? (object)(R.eq(F_dv_truth_uncached(v_world, F_dv_get(F_dv_get(v_adoption__356932, "law"), "premise")), "true")) : (object)(false))))
            {
                object v_staged__357121 = R.snapshot(F_dv_stage(v_world, F_dv_get(F_dv_get(v_adoption__356932, "law"), "effects"), v_time, F_dv_get(v_adoption__356932, "basis"), F_dv_get(v_adoption__356932, "confidence")));
                _ = F_dv_require((R.Bool(R.not(R.eq(F_dv_get(v_staged__357121, "status"), "ready"))) ? (object)(true) : (object)(R.not(F_dvs_reason_changes(F_dv_get(v_staged__357121, "changes"))))), R.cat("Dirty queue omitted rule: ", F_dv_get(F_dv_get(v_adoption__356932, "law"), "name")));
            }
            v_i__356889 = R.add(v_i__356889, R.number("1"));
        }
        return true;
    }

    public static object F_dvs_environment(object v_physical, object v_story, object v_events, object v_parent)
    {
        v_physical = R.snapshot(v_physical);
        v_story = R.snapshot(v_story);
        object v_dirty_config__357455 = F_dv_find(v_story, "run_config", new System.Collections.Generic.List<object>(0) { });
        object v_readview__357511 = F_dv_find(F_dv_find(v_story, "run_config", new System.Collections.Generic.List<object>(0) { }), "opt_readview", false);
        object v_world__357595 = R.snapshot(F_dvs_simulation_environment(v_physical, v_story, v_events, v_parent));
        object v_time__357671 = F_dv_find(v_world__357595, "time", R.number("0"));
        object v_clock__357712 = F_dv_find(v_story, "clock_ref", false);
        if (R.Bool(R.not(R.eq(v_clock__357712, false))))
        {
            v_world__357595 = R.snapshot(F_dv_store(v_world__357595, v_clock__357712, F_dv_cell(v_time__357671, R.number("1"), v_time__357671, new System.Collections.Generic.List<object>(1) { v_parent })));
        }
        object v_processes__357859 = F_dvs_simulation_unmanaged_processes(v_world__357595, F_dv_find(v_story, "world_processes", new System.Collections.Generic.List<object>(0) { }));
        if (R.Bool(v_readview__357511))
        {
            v_world__357595 = R.snapshot(v_world__357595);
            v_processes__357859 = R.snapshot(v_processes__357859);
        }
        object v_dirty__358055 = F_dv_find(v_dirty_config__357455, "opt_dirty", false);
        object v_dependency_index__358113 = new System.Collections.Generic.List<object>(0) { };
        object v_verify__358143 = false;
        if (R.Bool(v_dirty__358055))
        {
            v_processes__357859 = R.snapshot(v_processes__357859);
            v_dependency_index__358113 = F_dvs_dirty_environment_index(v_world__357595, v_processes__357859);
            object v_verify_every__358300 = F_dv_find(v_dirty_config__357455, "opt_dirty_verify", R.number("0"));
            if (R.Bool(R.lt(R.number("0"), v_verify_every__358300)))
            {
                object v_tick_key__358405 = new System.Collections.Generic.List<object>(1) { "dirty-environment-verify-tick" };
                object v_tick__358466 = R.memo_get(v_processes__357859, v_tick_key__358405);
                object v_calls__358522 = R.number("1");
                if (R.Bool(R.at(v_tick__358466, R.number("0"))))
                {
                    v_calls__358522 = R.add(R.at(v_tick__358466, R.number("1")), R.number("1"));
                }
                if (R.Bool(R.not(R.lt(v_calls__358522, v_verify_every__358300))))
                {
                    v_verify__358143 = true;
                    v_calls__358522 = R.number("0");
                }
                _ = R.memo_put(v_processes__357859, v_tick_key__358405, v_calls__358522);
            }
        }
        object v_i__358746 = R.number("0");
        if (R.Bool(v_dirty__358055))
        {
            v_i__358746 = R.dirty_next(v_world__357595, v_dependency_index__358113, R.sub(R.number("0"), R.number("1")));
            if (R.Bool(v_verify__358143))
            {
                _ = F_dvs_dirty_verify_skipped(v_world__357595, v_processes__357859, R.number("0"), v_i__358746, v_time__357671);
            }
        }
        while (R.Bool(R.lt(v_i__358746, R.count(v_processes__357859))))
        {
            object v_process__358954 = R.at(v_processes__357859, v_i__358746);
            object v_adoption__358989 = F_dv_get(F_dv_get(v_world__357595, "adoptions"), F_dv_get(v_process__358954, "law"));
            if (R.Bool((R.Bool((R.Bool(F_dv_get(v_adoption__358989, "active")) ? (object)(R.eq(F_dv_get(v_adoption__358989, "mode"), "enforce")) : (object)(false))) ? (object)(R.eq(F_dvs_dirty_truth(v_world__357595, F_dv_get(F_dv_get(v_adoption__358989, "law"), "premise"), v_dirty_config__357455, F_dv_get(F_dv_get(v_adoption__358989, "law"), "name")), "true")) : (object)(false))))
            {
                object v_staged__359204 = R.snapshot(F_dv_stage(v_world__357595, F_dv_get(F_dv_get(v_adoption__358989, "law"), "effects"), v_time__357671, F_dv_get(v_adoption__358989, "basis"), F_dv_get(v_adoption__358989, "confidence")));
                object v_changed__359310 = false;
                object v_j__359342 = R.number("0");
                while (R.Bool(R.lt(v_j__359342, R.count(F_dv_get(v_staged__359204, "changes")))))
                {
                    object v_change__359416 = R.at(F_dv_get(v_staged__359204, "changes"), v_j__359342);
                    if (R.Bool(R.not(F_dv_same(F_dvs_reason_value(F_dv_get(v_change__359416, "before")), F_dvs_reason_value(F_dv_get(v_change__359416, "after"))))))
                    {
                        v_changed__359310 = true;
                    }
                    v_j__359342 = R.add(v_j__359342, R.number("1"));
                }
                if (R.Bool((R.Bool(R.eq(F_dv_get(v_staged__359204, "status"), "ready")) ? (object)(v_changed__359310) : (object)(false))))
                {
                    object v_id__359674 = R.cat("environment-", R.show(R.count(v_events)));
                    object v_before__359749 = v_world__357595;
                    object v_execution__359784 = R.snapshot(F_dv_execute(v_world__357595, F_dv_get(v_process__358954, "law"), v_id__359674, v_time__357671, F_dv_get(v_world__357595, "revision")));
                    v_world__357595 = F_dv_get(v_execution__359784, "world");
                    if (R.Bool((R.Bool(R.eq(F_dv_get(v_execution__359784, "status"), "applied")) ? (object)(F_dv_find(v_process__358954, "once", false)) : (object)(false))))
                    {
                        v_world__357595 = F_dv_suspend(v_world__357595, F_dv_get(v_process__358954, "law"));
                    }
                    if (R.Bool(v_readview__357511))
                    {
                        v_world__357595 = R.snapshot(v_world__357595);
                    }
                    v_parent = F_dvs_event(v_events, "physical_process", "", v_time__357671, new System.Collections.Generic.List<object>(1) { v_parent }, new System.Collections.Generic.List<object>(5) { F_dv_pair("process", v_process__358954), F_dv_pair("revision_before", F_dv_get(v_before__359749, "revision")), F_dv_pair("revision_after", F_dv_get(v_world__357595, "revision")), F_dv_pair("suspended", R.not(F_dv_get(F_dv_get(F_dv_get(v_world__357595, "adoptions"), F_dv_get(v_process__358954, "law")), "active"))), F_dv_pair("execution", F_dv_without(v_execution__359784, "world")) });
                }
            }
            object v_previous__360408 = v_i__358746;
            if (R.Bool(v_dirty__358055))
            {
                v_i__358746 = R.dirty_next(v_world__357595, v_dependency_index__358113, v_previous__360408);
                if (R.Bool(v_verify__358143))
                {
                    _ = F_dvs_dirty_verify_skipped(v_world__357595, v_processes__357859, R.add(v_previous__360408, R.number("1")), v_i__358746, v_time__357671);
                }
            }
            else
            {
                v_i__358746 = R.add(v_i__358746, R.number("1"));
            }
        }
        return v_world__357595;
    }

    public static object F_dvs_environment_boundary(object v_condition, object v_clock, object v_now, object v_due)
    {
        return F_dv_get(F_dv_time_boundary(v_condition, v_clock, v_now, v_due, "minute"), "next");
    }

    public static object F_dvs_dirty_clock_plan(object v_condition, object v_clock)
    {
        object v_key__361311 = new System.Collections.Generic.List<object>(2) { "dirty-clock-plan-v2", v_clock };
        object v_cached__361356 = R.memo_get(v_condition, v_key__361311);
        if (R.Bool(R.at(v_cached__361356, R.number("0"))))
        {
            return R.at(v_cached__361356, R.number("1"));
        }
        object v_op__361444 = F_dv_get(v_condition, "op");
        object v_minute__361470 = false;
        object v_thresholds__361493 = new System.Collections.Generic.List<object>(0) { };
        object v_aged__361517 = new System.Collections.Generic.List<object>(0) { };
        if (R.Bool(R.eq(v_op__361444, "held_for")))
        {
            _ = F_dv_require(F_dv_get(F_dv_condition_inputs(v_condition), "complete"), "Invalid held-for clock declaration");
            return R.memo_put(v_condition, v_key__361311, new System.Collections.Generic.List<object>(3) { F_dv_pair("minute", false), F_dv_pair("thresholds", new System.Collections.Generic.List<object>(0) { }), F_dv_pair("aged", new System.Collections.Generic.List<object>(1) { v_condition }) });
        }
        if (R.Bool(R.eq(v_op__361444, "constant")))
        {
            _ = F_dv_require(F_dv_has(new System.Collections.Generic.List<object>(3) { "true", "false", "unknown" }, F_dv_get(v_condition, "value")), "Invalid constant clock condition");
        }
        else
        {
            if (R.Bool(R.eq(v_op__361444, "not")))
            {
                object v_child__361936 = F_dvs_dirty_clock_plan(F_dv_get(v_condition, "item"), v_clock);
                v_minute__361470 = F_dv_get(v_child__361936, "minute");
                v_thresholds__361493 = F_dv_get(v_child__361936, "thresholds");
                v_aged__361517 = F_dv_get(v_child__361936, "aged");
            }
            else
            {
                if (R.Bool(R.eq(v_op__361444, "all")))
                {
                    object v_i__362156 = R.number("0");
                    while (R.Bool(R.lt(v_i__362156, R.count(F_dv_get(v_condition, "items")))))
                    {
                        object v_child__362239 = F_dvs_dirty_clock_plan(R.at(F_dv_get(v_condition, "items"), v_i__362156), v_clock);
                        v_minute__361470 = (R.Bool(v_minute__361470) ? (object)(true) : (object)(F_dv_get(v_child__362239, "minute")));
                        v_aged__361517 = F_dvp_merge_refs(v_aged__361517, F_dv_get(v_child__362239, "aged"));
                        object v_j__362431 = R.number("0");
                        while (R.Bool(R.lt(v_j__362431, R.count(F_dv_get(v_child__362239, "thresholds")))))
                        {
                            if (R.Bool(R.not(F_dv_has(v_thresholds__361493, R.at(F_dv_get(v_child__362239, "thresholds"), v_j__362431)))))
                            {
                                _ = R.push(v_thresholds__361493, R.at(F_dv_get(v_child__362239, "thresholds"), v_j__362431));
                            }
                            v_j__362431 = R.add(v_j__362431, R.number("1"));
                        }
                        v_i__362156 = R.add(v_i__362156, R.number("1"));
                    }
                }
                else
                {
                    _ = F_dv_require((R.Bool(R.eq(v_op__361444, "equals")) ? (object)(true) : (object)(R.eq(v_op__361444, "at_least"))), R.cat("Unindexed clock operator: ", v_op__361444));
                    _ = F_dv_require(F_dv_get(F_dv_condition_inputs(v_condition), "complete"), "Invalid clock dependency declaration");
                    if (R.Bool(R.not(R.eq(v_clock, false))))
                    {
                        object v_relation__362990 = F_dv_time_ref_relation(F_dv_get(v_condition, "ref"), v_clock);
                        if (R.Bool(R.eq(v_relation__362990, "unknown")))
                        {
                            v_minute__361470 = true;
                        }
                        else
                        {
                            if (R.Bool(R.eq(v_relation__362990, "same")))
                            {
                                if (R.Bool((R.Bool(R.eq(v_op__361444, "at_least")) ? (object)(R.is_numeric_value(F_dv_get(v_condition, "value"))) : (object)(false))))
                                {
                                    _ = R.push(v_thresholds__361493, F_dv_get(v_condition, "value"));
                                }
                                else
                                {
                                    v_minute__361470 = true;
                                }
                            }
                        }
                    }
                }
            }
        }
        return R.memo_put(v_condition, v_key__361311, new System.Collections.Generic.List<object>(3) { F_dv_pair("minute", v_minute__361470), F_dv_pair("thresholds", v_thresholds__361493), F_dv_pair("aged", v_aged__361517) });
    }

    public static object F_dvs_dirty_clock_boundary(object v_world, object v_processes, object v_clock, object v_now, object v_due)
    {
        object v_key__363612 = new System.Collections.Generic.List<object>(3) { "dirty-world-clock-plan-v2", F_dv_get(v_world, "adoptions"), v_clock };
        object v_cached__363680 = R.memo_get(v_processes, v_key__363612);
        object v_plan__363725 = false;
        if (R.Bool(R.at(v_cached__363680, R.number("0"))))
        {
            v_plan__363725 = R.at(v_cached__363680, R.number("1"));
        }
        else
        {
            object v_minute__363803 = false;
            object v_thresholds__363830 = new System.Collections.Generic.List<object>(0) { };
            object v_aged__363858 = new System.Collections.Generic.List<object>(0) { };
            object v_i__363880 = R.number("0");
            while (R.Bool(R.lt(v_i__363880, R.count(v_processes))))
            {
                object v_adoption__363941 = F_dv_get(F_dv_get(v_world, "adoptions"), F_dv_get(R.at(v_processes, v_i__363880), "law"));
                if (R.Bool((R.Bool(F_dv_get(v_adoption__363941, "active")) ? (object)(R.eq(F_dv_get(v_adoption__363941, "mode"), "enforce")) : (object)(false))))
                {
                    object v_child__364077 = F_dvs_dirty_clock_plan(F_dv_get(F_dv_get(v_adoption__363941, "law"), "premise"), v_clock);
                    v_minute__363803 = (R.Bool(v_minute__363803) ? (object)(true) : (object)(F_dv_get(v_child__364077, "minute")));
                    v_aged__363858 = F_dvp_merge_refs(v_aged__363858, F_dv_get(v_child__364077, "aged"));
                    object v_j__364259 = R.number("0");
                    while (R.Bool(R.lt(v_j__364259, R.count(F_dv_get(v_child__364077, "thresholds")))))
                    {
                        if (R.Bool(R.not(F_dv_has(v_thresholds__363830, R.at(F_dv_get(v_child__364077, "thresholds"), v_j__364259)))))
                        {
                            _ = R.push(v_thresholds__363830, R.at(F_dv_get(v_child__364077, "thresholds"), v_j__364259));
                        }
                        v_j__364259 = R.add(v_j__364259, R.number("1"));
                    }
                }
                v_i__363880 = R.add(v_i__363880, R.number("1"));
            }
            v_plan__363725 = R.memo_put(v_processes, v_key__363612, new System.Collections.Generic.List<object>(3) { F_dv_pair("minute", v_minute__363803), F_dv_pair("thresholds", v_thresholds__363830), F_dv_pair("aged", v_aged__363858) });
        }
        if (R.Bool(R.lt(v_due, v_now)))
        {
            return v_due;
        }
        object v_next__364652 = v_due;
        if (R.Bool(F_dv_get(v_plan__363725, "minute")))
        {
            v_next__364652 = F_dv_min(v_next__364652, R.add(v_now, R.number("1")));
        }
        object v_i__364728 = R.number("0");
        while (R.Bool(R.lt(v_i__364728, R.count(F_dv_get(v_plan__363725, "thresholds")))))
        {
            object v_threshold__364787 = R.at(F_dv_get(v_plan__363725, "thresholds"), v_i__364728);
            if (R.Bool((R.Bool(R.lt(v_now, v_threshold__364787)) ? (object)(R.not(R.lt(v_due, v_threshold__364787))) : (object)(false))))
            {
                object v_delta__364883 = R.sub(v_threshold__364787, v_now);
                object v_steps__364923 = R.number("1");
                while (R.Bool(R.lt(v_steps__364923, v_delta__364883)))
                {
                    v_steps__364923 = R.add(v_steps__364923, R.number("1"));
                }
                v_next__364652 = F_dv_min(v_next__364652, R.add(v_now, v_steps__364923));
            }
            v_i__364728 = R.add(v_i__364728, R.number("1"));
        }
        v_i__364728 = R.number("0");
        while (R.Bool(R.lt(v_i__364728, R.count(F_dv_get(v_plan__363725, "aged")))))
        {
            object v_condition__365120 = F_dv_time_materialize(v_world, R.at(F_dv_get(v_plan__363725, "aged"), v_i__364728));
            v_next__364652 = F_dv_min(v_next__364652, F_dv_get(F_dv_time_boundary(v_condition__365120, v_clock, v_now, v_due, "minute"), "next"));
            v_i__364728 = R.add(v_i__364728, R.number("1"));
        }
        return v_next__364652;
    }

    public static object F_dvs_environment_reference_next_time(object v_world, object v_processes, object v_clock, object v_now, object v_due)
    {
        v_world = R.snapshot(v_world);
        v_processes = R.snapshot(v_processes);
        object v_next__365530 = v_due;
        object v_i__365549 = R.number("0");
        while (R.Bool(R.lt(v_i__365549, R.count(v_processes))))
        {
            object v_adoption__365602 = F_dv_get(F_dv_get(v_world, "adoptions"), F_dv_get(R.at(v_processes, v_i__365549), "law"));
            if (R.Bool((R.Bool(F_dv_get(v_adoption__365602, "active")) ? (object)(R.eq(F_dv_get(v_adoption__365602, "mode"), "enforce")) : (object)(false))))
            {
                v_next__365530 = F_dv_min(v_next__365530, F_dvs_environment_boundary(F_dv_time_materialize(v_world, F_dv_get(F_dv_get(v_adoption__365602, "law"), "premise")), v_clock, v_now, v_due));
                if (R.Bool(R.eq(F_dv_truth_uncached(v_world, F_dv_get(F_dv_get(v_adoption__365602, "law"), "premise")), "true")))
                {
                    object v_staged__365932 = R.snapshot(F_dv_stage(v_world, F_dv_get(F_dv_get(v_adoption__365602, "law"), "effects"), v_now, F_dv_get(v_adoption__365602, "basis"), F_dv_get(v_adoption__365602, "confidence")));
                    object v_j__366041 = R.number("0");
                    while (R.Bool(R.lt(v_j__366041, R.count(F_dv_get(v_staged__365932, "changes")))))
                    {
                        object v_change__366123 = R.at(F_dv_get(v_staged__365932, "changes"), v_j__366041);
                        if (R.Bool((R.Bool(F_dv_same(F_dv_get(v_change__366123, "ref"), v_clock)) ? (object)(true) : (object)(R.not(F_dv_same(F_dvs_reason_value(F_dv_get(v_change__366123, "before")), F_dvs_reason_value(F_dv_get(v_change__366123, "after"))))))))
                        {
                            return F_dv_min(v_due, R.add(v_now, R.number("1")));
                        }
                        v_j__366041 = R.add(v_j__366041, R.number("1"));
                    }
                }
            }
            v_i__365549 = R.add(v_i__365549, R.number("1"));
        }
        return v_next__365530;
    }

    public static object F_dvs_environment_next_time(object v_world, object v_story, object v_due)
    {
        v_world = R.snapshot(v_world);
        v_story = R.snapshot(v_story);
        object v_readview__366455 = F_dv_find(F_dv_find(v_story, "run_config", new System.Collections.Generic.List<object>(0) { }), "opt_readview", false);
        if (R.Bool(v_readview__366455))
        {
            v_world = R.snapshot(v_world);
        }
        object v_clock__366591 = F_dv_find(v_story, "clock_ref", false);
        object v_now__366642 = F_dv_get(v_world, "time");
        object v_next__366667 = v_due;
        object v_processes__366686 = F_dvs_simulation_unmanaged_processes(v_world, F_dv_find(v_story, "world_processes", new System.Collections.Generic.List<object>(0) { }));
        if (R.Bool(v_readview__366455))
        {
            v_processes__366686 = R.snapshot(v_processes__366686);
        }
        object v_dirty_config__366847 = F_dv_find(v_story, "run_config", new System.Collections.Generic.List<object>(0) { });
        object v_dirty__366903 = F_dv_find(v_dirty_config__366847, "opt_dirty", false);
        object v_verify_clock__366961 = false;
        object v_original_next__366990 = v_due;
        if (R.Bool(v_dirty__366903))
        {
            v_processes__366686 = R.snapshot(v_processes__366686);
            v_next__366667 = F_dvs_dirty_clock_boundary(v_world, v_processes__366686, v_clock__366591, v_now__366642, v_due);
            object v_every__367154 = F_dv_find(v_dirty_config__366847, "opt_dirty_verify", R.number("0"));
            if (R.Bool(R.lt(R.number("0"), v_every__367154)))
            {
                object v_key__367245 = new System.Collections.Generic.List<object>(1) { "dirty-clock-verify-tick" };
                object v_previous__367295 = R.memo_get(v_processes__366686, v_key__367245);
                object v_calls__367350 = R.number("1");
                if (R.Bool(R.at(v_previous__367295, R.number("0"))))
                {
                    v_calls__367350 = R.add(R.at(v_previous__367295, R.number("1")), R.number("1"));
                }
                if (R.Bool(R.not(R.lt(v_calls__367350, v_every__367154))))
                {
                    v_verify_clock__366961 = true;
                    v_calls__367350 = R.number("0");
                }
                _ = R.memo_put(v_processes__366686, v_key__367245, v_calls__367350);
            }
        }
        if (R.Bool(v_verify_clock__366961))
        {
            v_original_next__366990 = F_dvs_environment_reference_next_time(v_world, v_processes__366686, v_clock__366591, v_now__366642, v_due);
        }
        object v_i__367692 = R.number("0");
        while (R.Bool(R.lt(v_i__367692, R.count(v_processes__366686))))
        {
            object v_adoption__367745 = F_dv_get(F_dv_get(v_world, "adoptions"), F_dv_get(R.at(v_processes__366686, v_i__367692), "law"));
            if (R.Bool((R.Bool(F_dv_get(v_adoption__367745, "active")) ? (object)(R.eq(F_dv_get(v_adoption__367745, "mode"), "enforce")) : (object)(false))))
            {
                if (R.Bool(R.not(v_dirty__366903)))
                {
                    v_next__366667 = F_dv_min(v_next__366667, F_dvs_environment_boundary(F_dv_time_materialize(v_world, F_dv_get(F_dv_get(v_adoption__367745, "law"), "premise")), v_clock__366591, v_now__366642, v_due));
                }
                if (R.Bool(R.eq(F_dv_truth(v_world, F_dv_get(F_dv_get(v_adoption__367745, "law"), "premise")), "true")))
                {
                    object v_staged__368096 = R.snapshot(F_dv_stage(v_world, F_dv_get(F_dv_get(v_adoption__367745, "law"), "effects"), v_now__366642, F_dv_get(v_adoption__367745, "basis"), F_dv_get(v_adoption__367745, "confidence")));
                    object v_j__368205 = R.number("0");
                    while (R.Bool(R.lt(v_j__368205, R.count(F_dv_get(v_staged__368096, "changes")))))
                    {
                        object v_change__368287 = R.at(F_dv_get(v_staged__368096, "changes"), v_j__368205);
                        if (R.Bool((R.Bool(F_dv_same(F_dv_get(v_change__368287, "ref"), v_clock__366591)) ? (object)(true) : (object)(R.not(F_dv_same(F_dvs_reason_value(F_dv_get(v_change__368287, "before")), F_dvs_reason_value(F_dv_get(v_change__368287, "after"))))))))
                        {
                            object v_immediate__368473 = F_dv_min(v_due, R.add(v_now__366642, R.number("1")));
                            if (R.Bool(v_verify_clock__366961))
                            {
                                _ = F_dv_require(R.eq(v_immediate__368473, v_original_next__366990), "Dirty clock live-process boundary differs from reference scan");
                            }
                            return F_dvs_simulation_next_time(v_world, v_immediate__368473);
                        }
                        v_j__368205 = R.add(v_j__368205, R.number("1"));
                    }
                }
            }
            v_i__367692 = R.add(v_i__367692, R.number("1"));
        }
        if (R.Bool(v_verify_clock__366961))
        {
            _ = F_dv_require(R.eq(v_next__366667, v_original_next__366990), "Dirty clock boundary differs from complete declaration scan");
        }
        return F_dvs_simulation_next_time(v_world, v_next__366667);
    }
}
}
