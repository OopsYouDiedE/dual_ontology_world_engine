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
    // Cache ownership follows a session, not the process. Standalone compiler
    // callers receive their own execution-flow context on first use.
    public sealed class ValueContext
    {
        internal readonly object Storage = new ValueCaches();
    }
    private sealed class ValueCaches
    {
        internal readonly ConditionalWeakTable<FrozenList, DirtyQueues> DirtyIndices = new();
        internal readonly object SnapshotLock = new();
        internal readonly bool LazyInternalEnabled = Environment.GetEnvironmentVariable("DV_OPT_LAZYINTERNAL") == "1";
        internal readonly bool CowEnabled = Environment.GetEnvironmentVariable("DV_OPT_COW") == "1";
        internal readonly bool MaskEnabled = Environment.GetEnvironmentVariable("DV_OPT_MASK") == "1";
        internal readonly bool MaskVerify = Environment.GetEnvironmentVariable("DV_OPT_MASK_VERIFY") == "1";
        internal readonly ConditionalWeakTable<FrozenList, CellFlags> CellMasks = new();
        // Certified immutable append prefixes bound copying of growing logs.
        // Replacement and nested writes invalidate them; 0 keeps flat storage.
        internal readonly bool AppendShareEnabled = Environment.GetEnvironmentVariable("DV_OPT_APPENDSHARE") != "0";
        internal readonly bool ReadViewEnabled = Environment.GetEnvironmentVariable("DV_OPT_READVIEW") == "1";
        // Sample once per execution context; CLI captures this in run_config.
        internal readonly bool RecordCacheEnabled = Environment.GetEnvironmentVariable("DV_OPT_RECORDCACHE") == "1";
        internal readonly bool PlanCacheEnabled = Environment.GetEnvironmentVariable("DV_OPT_PLANCACHE") != "0";
        internal readonly bool DeclarationCacheEnabled = Environment.GetEnvironmentVariable("DV_OPT_DECLCACHE") != "0";
        internal readonly bool PredicateEnabled = Environment.GetEnvironmentVariable("DV_OPT_PREDICATE") == "1";
        internal readonly bool PredicateCacheEnabled = Environment.GetEnvironmentVariable("DV_OPT_PREDICATECACHE") == "1";
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
        internal readonly bool StateIndexEnabled = Environment.GetEnvironmentVariable("DV_OPT_STATEINDEX") != "0";
        internal readonly bool RuntimeSpeedEnabled = Environment.GetEnvironmentVariable("DV_OPT_RUNTIMESPEED") != "0";
        internal readonly ConditionalWeakTable<FrozenList, ValueMemo> ValueMemos = new();
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
    // Standalone compiler/audit programs retain complete evidence by default.
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
