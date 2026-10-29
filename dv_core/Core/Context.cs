using System.Globalization;
using System.Threading;
using System.Collections;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;

namespace DuelView.Compiled;

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
