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

// Storage codec only. JSON remains an in-memory host/compiler exchange format.
public static class BinaryEvidence
{
    public static readonly byte[] Magic = "DVLOG2\r\n"u8.ToArray();
    private const int MaxFrame = 256 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public static void RequireBinaryPath(string path)
    {
        var parts = Path.GetFileName(path).Split('.');
        if (parts.Any(p => p.Equals("json", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("jsonl", StringComparison.OrdinalIgnoreCase) || p.Equals("gz", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("JSON recording is forbidden; use .dvlog/.dvdag/.dvsave");
    }
    public static byte[] Encode(object? value)
    {
        var writer = new Encoder();
        writer.Value(value);
        return writer.WrittenSpan.ToArray();
    }
    // One writer owns its buffer. DAG frames can append their already encoded
    // payload directly to an envelope without a second object-graph traversal.
    internal sealed class Encoder
    {
        private readonly ArrayBufferWriter<byte> output = new(1024);
        internal ReadOnlySpan<byte> WrittenSpan => output.WrittenSpan;
        internal int Count => output.WrittenCount;
        internal void Clear() => output.Clear();
        private void Byte(byte value)
        {
            output.GetSpan(1)[0] = value; output.Advance(1);
        }
        private void Int32(int value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(output.GetSpan(4), value);
            output.Advance(4);
        }
        internal void Integer(long value)
        {
            var bytes = output.GetSpan(9); bytes[0] = 3;
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes[1..], value);
            output.Advance(9);
        }
        internal void Text(string text)
        {
            var length = Utf8.GetByteCount(text);
            Byte(5); Int32(length);
            var written = Utf8.GetBytes(text.AsSpan(), output.GetSpan(length));
            output.Advance(written);
        }
        internal void Utf8Text(ReadOnlySpan<byte> text)
        {
            Byte(5); Int32(text.Length);
            text.CopyTo(output.GetSpan(text.Length)); output.Advance(text.Length);
        }
        internal void Map(int count) { Byte(7); Int32(count); }
        internal void List(int count) { Byte(8); Int32(count); }
        internal void Flag(bool value) => Byte(value ? (byte)2 : (byte)1);
        internal void Value(object? value, int depth = 0)
        {
            if (depth > 512) throw new InvalidDataException("Binary evidence nesting exceeds 512");
            switch (value)
            {
                case null: Byte(0); break;
                case bool flag: Flag(flag); break;
                case int number: Integer(number); break;
                case long number: Integer(number); break;
                case double number when double.IsFinite(number):
                    Byte(4);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(output.GetSpan(8), BitConverter.DoubleToInt64Bits(number));
                    output.Advance(8); break;
                case string text: Text(text); break;
                case IDictionary<string, object?> map:
                    Map(map.Count);
                    foreach (var entry in map) { Text(entry.Key); Value(entry.Value, depth + 1); }
                    break;
                case object?[] array:
                    // Host arrays are storage values, not members of the DV value domain.
                    List(array.Length);
                    foreach (var item in array) Value(item, depth + 1);
                    break;
                case JsonElement element: Value(FromJson(element), depth); break;
                default:
                    if (!R.TryItems(value!, out var items)) throw new InvalidDataException("Unsupported binary evidence atom");
                    List(items.Count);
                    for (var i = 0; i < items.Count; i++) Value(items[i], depth + 1);
                    break;
            }
        }
    }
    public static object? FromJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(p => p.Name, p => FromJson(p.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select(FromJson).ToArray(),
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetInt64(out var integer) ? (object)integer : value.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null,
        _ => throw new InvalidDataException("Unsupported host record value")
    };
    public static object? Decode(byte[] payload)
    {
        using var input = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(input, Utf8);
        var result = ReadValue(reader, payload.Length, 0);
        if (input.Position != input.Length) throw new InvalidDataException("Trailing binary value bytes");
        return result;
    }
    private static object? ReadValue(BinaryReader reader, int limit, int depth)
    {
        if (depth > 512) throw new InvalidDataException("Binary evidence nesting exceeds 512");
        switch (reader.ReadByte())
        {
            case 0: return null;
            case 1: return false;
            case 2: return true;
            case 3: return reader.ReadInt64();
            case 4:
                var number = reader.ReadDouble();
                if (!double.IsFinite(number)) throw new InvalidDataException("Nonfinite evidence number");
                return number;
            case 5:
                var size = reader.ReadInt32();
                if (size < 0 || size > limit) throw new InvalidDataException("Invalid binary text length");
                var bytes = reader.ReadBytes(size);
                if (bytes.Length != size) throw new EndOfStreamException();
                return Utf8.GetString(bytes);
            case 7:
                var count = reader.ReadInt32();
                if (count < 0 || count > limit) throw new InvalidDataException("Invalid binary map length");
                var map = new Dictionary<string, object?>();
                for (var i = 0; i < count; i++)
                {
                    var key = ReadValue(reader, limit, depth + 1) as string ?? throw new InvalidDataException("Binary map key is not text");
                    map.Add(key, ReadValue(reader, limit, depth + 1));
                }
                return map;
            case 8:
                var length = reader.ReadInt32();
                if (length < 0 || length > limit) throw new InvalidDataException("Invalid binary list length");
                var list = new List<object?>();
                for (var i = 0; i < length; i++) list.Add(ReadValue(reader, limit, depth + 1));
                return list;
            default: throw new InvalidDataException("Unknown binary value tag");
        }
    }
    public static void WriteFrame(Stream stream, object? value)
    {
        var writer = new Encoder();
        writer.Value(value);
        WriteEncodedFrame(stream, writer.WrittenSpan);
    }
    internal static void WriteEncodedFrame(Stream stream, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxFrame) throw new InvalidDataException("Binary frame exceeds size limit");
        Span<byte> prefix = stackalloc byte[4];
        Span<byte> checksum = stackalloc byte[32];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        SHA256.HashData(payload, checksum);
        stream.Write(prefix); stream.Write(checksum); stream.Write(payload);
    }
    public static object? ReadFrame(Stream stream, out bool end)
    {
        Span<byte> prefix = stackalloc byte[4];
        var first = stream.ReadByte();
        end = first < 0;
        if (end) return null;
        prefix[0] = (byte)first; stream.ReadExactly(prefix[1..]);
        var size = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (size < 0 || size > MaxFrame) throw new InvalidDataException("Invalid binary frame size");
        var checksum = new byte[32]; stream.ReadExactly(checksum);
        var payload = new byte[size]; stream.ReadExactly(payload);
        if (!CryptographicOperations.FixedTimeEquals(checksum, SHA256.HashData(payload))) throw new InvalidDataException("Binary frame checksum mismatch");
        return Decode(payload);
    }
    public static void RequireHeader(Stream stream)
    {
        var header = new byte[Magic.Length]; stream.ReadExactly(header);
        if (!header.AsSpan().SequenceEqual(Magic)) throw new InvalidDataException("Unsupported binary evidence header");
    }
    public static void WriteJsonRecords(string path, IEnumerable<string> records)
    {
        RequireBinaryPath(path);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        output.Write(Magic);
        WriteJsonRecords(output, records);
    }
    public static void AppendJsonRecords(string path, string records)
    {
        RequireBinaryPath(path);
        using var output = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        if (output.Length == 0) output.Write(Magic);
        else { RequireHeader(output); output.Position = output.Length; }
        WriteJsonRecords(output, records.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }
    private static void WriteJsonRecords(Stream stream, IEnumerable<string> records)
    {
        foreach (var record in records)
        {
            using var document = JsonDocument.Parse(record);
            WriteFrame(stream, document.RootElement);
        }
    }
}

// Platform/value ABI only. No reader, AST walker, evaluator or code generator.
public static class R
{
    // Cache ownership follows a session, not the process. Standalone compiler
    // callers receive their own execution-flow context on first use.
    public sealed class ValueContext
    {
        internal readonly object Storage = new ValueCaches();
    }
    private sealed class ValueCaches
    {
        internal readonly object SnapshotLock = new();
        // Sample once per execution context; CLI captures this in run_config.
        internal readonly bool RecordCacheEnabled = Environment.GetEnvironmentVariable("DV_OPT_RECORDCACHE") == "1";
        internal readonly ConditionalWeakTable<FrozenList, RecordShape> RecordShapes = new();
        internal readonly ConditionalWeakTable<FrozenList, RefLookupText> RefLookupTexts = new();
        internal readonly FrozenList EmptySnapshot = new(Array.Empty<object>(), unchecked(14695981039346656037UL * 1099511628211UL), 14695981039346656037UL)
        {
            CompatibleSequenceHash = 14695981039346656037UL,
            MembershipHash = unchecked(14695981039346656037UL * 1099511628211UL) ^ 5UL,
            MembershipHashChecked = true,
            MembershipHashEligible = true
        };
        internal readonly ConditionalWeakTable<List<object>, MutableState> MutableStates = new();
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
    public static string[] Arguments = [];
    public static string Text(object x) => x as string ?? throw new Exception("Expected Text");
    public static double Num(object x) => x is double n && double.IsFinite(n) ? n : throw new Exception("Expected finite Number");
    public static bool Bool(object x) => x is bool b ? b : throw new Exception("Expected Bool");
    public static IReadOnlyList<object> Items(object x) => TryItems(x, out var items)
        ? items : throw new Exception("Expected List");
    // Read interface is not a value-domain predicate: external arrays and other
    // IReadOnlyList implementations are deliberately not accepted as DV lists.
    public static bool IsDvList(object value) => value is List<object> or FrozenList;
    public static bool TryItems(object value, out IReadOnlyList<object> items)
    {
        switch (value)
        {
            case List<object> mutable: items = mutable; return true;
            case FrozenList frozen: items = frozen; return true;
            default: items = null!; return false;
        }
    }
    private static readonly object NumberZero = 0d;
    private static readonly object NumberOne = 1d;
    private static readonly object NumberMinusOne = -1d;
    // Numeric atoms are immutable. Reuse small exact integers without sharing
    // mutable value state; preserve the distinct binary representation of -0.
    private static readonly object[] SmallNumbers = CreateSmallNumbers();
    private static object[] CreateSmallNumbers()
    {
        var values = new object[1025];
        values[0] = NumberZero; values[1] = NumberOne;
        for (var i = 2; i < values.Length; i++) values[i] = (double)i;
        return values;
    }
    private static object NumberValue(double number)
    {
        if (!double.IsFinite(number)) throw new Exception("Expected finite Number");
        if (number >= 0 && number < SmallNumbers.Length && number == Math.Truncate(number))
        {
            if (number != 0 || BitConverter.DoubleToInt64Bits(number) == 0) return SmallNumbers[(int)number];
        }
        if (number == -1) return NumberMinusOne;
        return number;
    }
    public static object number(object s)
    {
        // Validate Text before selecting exact finite spellings. All other
        // input, including negative zero, keeps the original parse/error path.
        var text = Text(s);
        return text switch
        {
            "0" => NumberZero,
            "1" => NumberOne,
            "-1" => NumberMinusOne,
            _ => NumberValue(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture))
        };
    }
    public static object is_number(object s) => double.TryParse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n);
    // Runtime value predicate; unlike is_number, this never parses or requires Text.
    public static object is_text(object value) => value is string;
    public static object is_numeric_value(object value) => value is double n && double.IsFinite(n) || value is Rational q && q.Denominator.Sign > 0;
    public static object add(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Add(Rational.From(b)) : NumberValue(Num(a) + Num(b));
    public static object sub(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Subtract(Rational.From(b)) : NumberValue(Num(a) - Num(b));
    public static object mul(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Multiply(Rational.From(b)) : NumberValue(Num(a) * Num(b));
    public static object div(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Divide(Rational.From(b)) : NumberValue(Num(a) / Num(b));
    public static object lt(object a, object b) => (a is Rational || b is Rational ? Rational.From(a).CompareTo(Rational.From(b)) < 0 : Num(a) < Num(b)) ? BoolTrue : BoolFalse;
    private static bool EqValue(object a, object b) => (a is Rational || b is Rational) && (a is Rational || a is double) && (b is Rational || b is double) ? Rational.From(a).Equals(Rational.From(b)) : Equals(a, b);
    private static readonly object BoolTrue = true;
    private static readonly object BoolFalse = false;
    public static object eq(object a, object b) => EqValue(a, b) ? BoolTrue : BoolFalse;
    public static object exact(object a) => Rational.From(a);
    public static object unit(object a)
    {
        var value = Rational.From(a);
        if (value.CompareTo(Rational.From(0d)) < 0 || value.CompareTo(Rational.From(1d)) > 0) throw new Exception("Numeric refinement requires 0 <= value <= 1");
        return value;
    }
    public static object floor(object a) => NumberValue(Math.Floor(Num(a)));
    public static object not(object a) => Bool(a) ? BoolFalse : BoolTrue;
    public static object cat(object a, object b) => Text(a) + Text(b);
    // Generic linear concatenation, including UTF-16 fragments used by bootstrap.
    public static object text_join(object values)
    {
        var result = new StringBuilder();
        foreach (var value in Items(values)) result.Append(Text(value));
        return result.ToString();
    }
    public static object length(object a) => NumberValue(Text(a).Length);
    private static int Index(object a) { var n = Num(a); return n >= 0 && n <= int.MaxValue && n == Math.Truncate(n) ? (int)n : throw new Exception("Invalid index"); }
    public static object slice(object a, object start, object count) => Text(a).Substring(Index(start), Index(count));
    public static object chr(object a, object i) => Text(a)[Index(i)].ToString();
    public static object code(object a) => Text(a).Length == 1 ? (double)Text(a)[0] : throw new Exception("Expected one UTF-16 unit");
    public static object show(object a) => a switch { string s => s, Rational q => q.ToString(), double n => n.ToString("R", CultureInfo.InvariantCulture), bool b => b ? "true" : "false", List<object> list => FormatList(list), FrozenList frozen => FormatList(frozen), _ => throw new Exception("Cannot format value") };
    private static string FormatList(IReadOnlyList<object> list)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(show(list[i]));
        }
        sb.Append(']');
        return sb.ToString();
    }
    public static object id_start(object c) => Text(c) == "_" || char.IsLetter(Text(c), 0);
    public static object id_part(object c) => Text(c) == "_" || char.IsLetterOrDigit(Text(c), 0) || CharUnicodeInfo.GetUnicodeCategory(Text(c), 0) == UnicodeCategory.NonSpacingMark;
    public static object normalize(object s) => Text(s).Normalize(NormalizationForm.FormC);
    public static object replace(object list, object index, object value) { Invalidate(list); MutableItems(list)[Index(index)] = value; return list; }
    public static object list() => new List<object>();
    public static object push(object a, object b)
    {
        var items = MutableItems(a);
        if (!MutableStates.TryGetValue(items, out var state))
        {
            items.Add(b);
            return a;
        }
        // Retain only a certified, unchanged immutable prefix. A later write to
        // any old child invalidates this prefix through the ordinary owner links.
        var prefix = state.Backbone ?? state.AppendBase;
        Invalidate(a);
        items.Add(b);
        state.AppendBase = prefix;
        return a;
    }
    public static object at(object a, object i) => ReadItem(a, Index(i));
    // Persistent spine only: mirror dvs_replace's equality and selected reads.
    public static object list_with_index(object items, object index, object value)
    {
        var source = Items(items);
        var result = new List<object>(source.Count);
        if (index is double selected)
        {
            // The source position is always a finite non-negative integer.
            // Double equality therefore has the same result as eq here,
            // including negative zero, fractions and non-finite indices.
            for (var i = 0; i < source.Count; i++)
                result.Add(i == selected ? value : ReadItem(items, i));
            return result;
        }
        for (var i = 0; i < source.Count; i++)
            result.Add(Bool(eq(NumberValue(i), index)) ? value : ReadItem(items, i));
        return result;
    }
    public static object count(object a) => a switch
    {
        FrozenList frozen => NumberValue(frozen.Count),
        List<object> mutable => NumberValue(mutable.Count),
        _ => NumberValue(Items(a).Count)
    };
    private static object RawItem(object owner, int index) => owner is FrozenList frozen
        ? frozen[index] : (owner as List<object> ?? throw new Exception("Expected List"))[index];
    // Tiny records stay linear until repeated lookup amortizes an index.
    private static bool RecordHot(object record)
    {
        if (record is FrozenList frozen) return Interlocked.Increment(ref frozen.RecordReads) > 3;
        return ++MutableStates.GetOrCreateValue((List<object>)record).RecordReads > 3;
    }
    public static object record_index(object record, object key)
    {
        var entries = Items(record);
        if (key is string name)
        {
            if (entries.Count >= 8 || RecordHot(record))
            {
                var frozen = record as FrozenList;
                if (frozen is null && entries is List<object> mutable && MutableStates.TryGetValue(mutable, out var state)) frozen = state.Backbone;
                var index = frozen is not null ? FrozenIndex(frozen) : MutableIndex((List<object>)entries);
                if (index is not null) return index.TryGetValue(name, out var found) ? NumberValue(found) : NumberMinusOne;
            }
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var k = RawItem(entry, 0);
                if (ReferenceEquals(k, name) || (k is string s && string.Equals(s, name, StringComparison.Ordinal)))
                    return NumberValue(i);
            }
            return NumberMinusOne;
        }
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            var k = RawItem(entry, 0);
            if (Bool(eq(k, key))) return NumberValue(i);
        }
        return NumberMinusOne;
    }
    public static object record_find(object record, object key, object fallback)
    {
        var entries = Items(record);
        int foundIndex = -1;
        if (key is string name)
        {
            if (entries.Count >= 4 || RecordHot(record))
            {
                var frozen = record as FrozenList;
                if (frozen is null && entries is List<object> mutable && MutableStates.TryGetValue(mutable, out var state)) frozen = state.Backbone;
                var index = frozen is not null ? FrozenIndex(frozen) : MutableIndex((List<object>)entries);
                if (index is not null)
                {
                    if (index.TryGetValue(name, out foundIndex))
                    {
                        var pair = entries[foundIndex];
                        var val = RawItem(pair, 1);
                        if (record is not FrozenList && val is FrozenList)
                            return ReadItem(ReadItem(record, foundIndex), 1);
                        return val;
                    }
                    if (fallback is string s && s == "__dv_missing_field_sentinel__")
                        throw new Exception("Missing field: " + Text(key));
                    return fallback;
                }
            }
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var k = RawItem(entry, 0);
                if (ReferenceEquals(k, name) || (k is string s && string.Equals(s, name, StringComparison.Ordinal)))
                {
                    foundIndex = i;
                    break;
                }
            }
        }
        else
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var k = RawItem(entry, 0);
                if (Bool(eq(k, key)))
                {
                    foundIndex = i;
                    break;
                }
            }
        }
        if (foundIndex < 0)
        {
            if (fallback is string s && s == "__dv_missing_field_sentinel__")
                throw new Exception("Missing field: " + Text(key));
            return fallback;
        }
        var p = entries[foundIndex];
        var v = RawItem(p, 1);
        if (record is not FrozenList && v is FrozenList)
            return ReadItem(ReadItem(record, foundIndex), 1);
        return v;
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
    private static readonly object MissingSentinel = new();

    // Structural value equality reads existing branches without materializing mutable copies.
    public static object same_value(object a, object b) => SameValue(a, b) ? BoolTrue : BoolFalse;
    private static bool SameValue(object a, object b)
    {
        if (ReferenceEquals(a, b) || EqValue(a, b)) return true;
        if (!TryItems(a, out var left) || !TryItems(b, out var right) || left.Count != right.Count) return false;
        var leftBase = a as FrozenList;
        var rightBase = b as FrozenList;
        if (leftBase is null && left is List<object> leftMutable && MutableStates.TryGetValue(leftMutable, out var ls)) leftBase = ls.Backbone;
        if (rightBase is null && right is List<object> rightMutable && MutableStates.TryGetValue(rightMutable, out var rs)) rightBase = rs.Backbone;
        if (leftBase is not null && ReferenceEquals(leftBase, rightBase)) return true;
        // The snapshot identity hash distinguishes exact/floating representation.
        // Only the value-compatible membership hash may reject structural equality.
        // Numeric collisions deliberately fall through to the original ordered comparison.
        if (leftBase is not null && rightBase is not null
            && TryMembershipHash(leftBase, out var leftHash)
            && TryMembershipHash(rightBase, out var rightHash) && leftHash != rightHash) return false;
        for (var i = 0; i < left.Count; i++)
            if (!SameValue(left[i], right[i])) return false;
        return true;
    }
    // Membership is a structural-value operation; reading never thaws child lists.
    public static object has_value(object items, object value) => HasValue(items, value) ? BoolTrue : BoolFalse;
    private static bool HasValue(object items, object value)
    {
        var values = Items(items);
        if (value is string name)
        {
            var frozen = items as FrozenList;
            if (frozen is null && values is List<object> mutable && MutableStates.TryGetValue(mutable, out var state)) frozen = state.Backbone;
            if (frozen is not null && FrozenStrings(frozen) is { } strings) return strings.Contains(name);
            for (var i = 0; i < values.Count; i++)
            {
                var v = values[i];
                if (ReferenceEquals(v, name) || (v is string s && string.Equals(s, name, StringComparison.Ordinal)))
                    return true;
            }
            return false;
        }
        // Index only an already-frozen container or an unchanged mutable copy.
        // Unsupported/non-finite atoms retain the original ordered scan.
        if (values.Count >= 8)
        {
            var frozen = items as FrozenList;
            if (frozen is null && values is List<object> mutable && MutableStates.TryGetValue(mutable, out var state)) frozen = state.Backbone;
            if (frozen is not null && FrozenMembership(frozen) is { } index
                && TryQueryMembershipHash(value, 8, out var hash))
            {
                if (!index.TryGetValue(hash, out var candidates)) return false;
                foreach (var candidate in candidates)
                    if (ReferenceEquals(candidate, value) || SameValue(candidate, value)) return true;
                return false;
            }
        }
        for (var i = 0; i < values.Count; i++)
            if (ReferenceEquals(values[i], value) || SameValue(values[i], value)) return true;
        return false;
    }
    // Ordered independent-copy record update; unchanged branches stay immutable until read.
    public static object record_with(object record, object key, object value)
    {
        var entries = Items(record);
        var entryCount = entries.Count;
        var result = new List<object>(entryCount + 1);
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
                    result.Add(new List<object> { key, snapshot(value) });
                    found = true;
                }
                else result.Add(frozen[i]);
            }
        }
        if (!found) result.Add(key is string textKey ? FrozenTextPair(textKey, snapshot(value)) : new List<object> { key, snapshot(value) });
        if (found) InheritRecordLayout(result, frozen);
        return result;
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
        var result = new List<object>(frozen.Count + changes.Count);
        var keys = new List<object>(frozen.Count + changes.Count);
        HashSet<string>? stringKeys = new(StringComparer.Ordinal);
        foreach (var item in frozen)
        {
            var key = RawItem(item, 0);
            var updatedVal = record_find(updates, key, MissingSentinel);
            if (!ReferenceEquals(updatedVal, MissingSentinel))
                result.Add(key is string textKey ? FrozenTextPair(textKey, snapshot(updatedVal)) : new List<object> { key, snapshot(updatedVal) });
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
            result.Add(key is string textKey ? FrozenTextPair(textKey, value) : new List<object> { key, value });
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
        result = new List<object>(record.Count + changes.Count);
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
        // The caller has already frozen the value. Unknown external atoms keep
        // the existing delayed validation path instead of introducing an error
        // at record construction. Only valid immutable DV values use this path.
        if (value is not (string or double or bool or Rational or FrozenList))
            return new List<object> { key, value };
        var caches = Caches;
        lock (caches.SnapshotLock)
        {
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
        internal readonly Dictionary<object, object> Values = new(MemoKeyComparer.Instance);
        internal readonly Queue<object> Order = new();
    }
    private static ConditionalWeakTable<FrozenList, ValueMemo> ValueMemos => Caches.ValueMemos;
    public static object memo_get(object owner, object key)
    {
        var caches = Caches;
        lock (caches.SnapshotLock)
        {
            var identity = owner is FrozenList fl ? fl : (FrozenList)SnapshotValue(Items(owner), caches);
            if (caches.ValueMemos.TryGetValue(identity, out var ownerMemo) && ownerMemo.TryPartition(key, out var partition))
            {
                if (partition.Values.TryGetValue(key, out var result))
                    return new List<object>(2) { BoolTrue, copy_value(result) };
            }
            else
            {
                // Retain the original validation on a new owner or partition,
                // without allocating an empty partition just for a miss.
                _ = SnapshotValue(key, caches);
                if (ownerMemo is not null) ArgumentNullException.ThrowIfNull(key);
            }
            return new List<object>(2) { BoolFalse, BoolFalse };
        }
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
            memo.Values[frozenKey] = SnapshotValue(value, caches);
            return value;
        }
    }
    public static object is_list(object a) => IsDvList(a);
    public static object copy_value(object value)
    {
        if (!IsDvList(value)) return value;
        // Every copy owns its root. Descendants are immutable until accessed through this owner.
        var frozen = value is FrozenList ? value : snapshot(value);
        return Thaw((FrozenList)frozen);
    }
    // Evidence values are immutable copies. Interning changes storage, never their JSON shape.
    // Mutable copies remain independent through copy_value; only snapshot() uses this pool.
    private sealed class FrozenList : IReadOnlyList<object>
    {
        // Only SnapshotValue supplies this newly allocated, fully frozen array.
        // It is adopted once and never exposed or modified after publication.
        private readonly object[] children;
        private static readonly List<object> InvalidIndexSource = [];
        internal FrozenList(object[] ownedChildren, ulong hash, ulong sequenceHash)
        {
            children = ownedChildren;
            Hash = hash;
            SequenceHash = sequenceHash;
        }
        // List copies the private array into its own backing store in one pass;
        // the immutable source remains private and cannot be mutated by its owner.
        internal List<object> CopyRoot()
        {
            if (children.Length == 0) return new List<object>();
            // Histories and event roots usually append immediately after copying.
            // Reserve a short suffix once instead of allocating an exact-size
            // copy and then a second, doubled array for its very first append.
            var extra = children.Length >= 64 ? 16 : 2;
            var capacity = children.Length <= int.MaxValue - extra ? children.Length + extra : children.Length;
            var result = new List<object>(capacity);
            result.AddRange(children);
            return result;
        }
        internal void CopyPrefixTo(object[] destination) => Array.Copy(children, destination, children.Length);
        public int Count => children.Length;
        public object this[int index]
        {
            get
            {
                // Preserve List's invalid-index exception at the same read.
                if ((uint)index >= (uint)children.Length) return InvalidIndexSource[index];
                return children[index];
            }
        }
        public Enumerator GetEnumerator() => new(children);
        IEnumerator<object> IEnumerable<object>.GetEnumerator() => children.Length == 0
            ? ((IEnumerable<object>)InvalidIndexSource).GetEnumerator() : GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => children.Length == 0
            ? ((IEnumerable)InvalidIndexSource).GetEnumerator() : GetEnumerator();
        public struct Enumerator : IEnumerator<object>
        {
            private readonly object[] values;
            private int index;
            private object? current;
            internal Enumerator(object[] values) { this.values = values; index = 0; current = null; }
            public object Current => current!;
            object IEnumerator.Current => index == 0 || index == values.Length + 1
                ? throw new InvalidOperationException("Enumeration has either not started or has already finished.") : Current;
            public bool MoveNext()
            {
                if (index < values.Length) { current = values[index++]; return true; }
                index = values.Length + 1; current = null; return false;
            }
            void IEnumerator.Reset() { index = 0; current = null; }
            public void Dispose() { }
        }
        internal readonly ulong Hash;
        internal readonly ulong SequenceHash;
        internal ulong CompatibleSequenceHash;
        internal int RecordReads;
        internal volatile bool IndexChecked;
        internal Dictionary<string, int>? Index;
        internal volatile bool StringsChecked;
        internal HashSet<string>? Strings;
        internal volatile bool MembershipHashChecked;
        internal bool MembershipHashEligible;
        internal ulong MembershipHash;
        internal volatile bool MembershipChecked;
        internal Dictionary<ulong, List<object>>? Membership;
    }
    // Numeric atoms deliberately share a token: exact and floating values may
    // compare equal. Hash collisions only add comparisons; same_value decides.
    private static bool TryMembershipHash(object value, out ulong hash)
    {
        switch (value)
        {
            case string text:
                hash = unchecked((ulong)StringComparer.Ordinal.GetHashCode(text) * 1099511628211UL) ^ 1UL;
                return true;
            case bool flag:
                hash = flag ? 2UL : 3UL;
                return true;
            case double number when double.IsFinite(number):
            case Rational { Denominator.Sign: > 0 }:
                hash = 4UL;
                return true;
            case FrozenList frozen:
                if (frozen.MembershipHashChecked)
                {
                    hash = frozen.MembershipHash;
                    return frozen.MembershipHashEligible;
                }
                lock (SnapshotLock)
                {
                    if (!frozen.MembershipHashChecked)
                    {
                        frozen.MembershipHashEligible = TryMembershipListHash(frozen, out frozen.MembershipHash);
                        frozen.MembershipHashChecked = true;
                    }
                    hash = frozen.MembershipHash;
                    return frozen.MembershipHashEligible;
                }
            case List<object> list when MutableStates.TryGetValue(list, out var state) && state.Backbone is not null:
                // A mutable copy may borrow its unchanged backbone. A fresh or
                // invalidated mutable graph is never traversed or cached here.
                return TryMembershipHash(state.Backbone, out hash);
            default:
                hash = 0;
                return false;
        }
    }
    // Fresh query lists are read without materializing/caching them. Depth
    // bounds cyclic or deep graphs; ineligible queries keep the original scan.
    private static bool TryQueryMembershipHash(object value, int depth, out ulong hash)
    {
        if (TryMembershipHash(value, out hash)) return true;
        if (depth <= 0 || !TryItems(value, out var list)) return false;
        hash = 14695981039346656037UL;
        foreach (var item in list)
        {
            if (!TryQueryMembershipHash(item, depth - 1, out var child)) return false;
            hash = unchecked((hash ^ child) * 1099511628211UL);
        }
        hash = unchecked((hash ^ (ulong)list.Count) * 1099511628211UL) ^ 5UL;
        return true;
    }
    private static bool TryMembershipListHash(IReadOnlyList<object> values, out ulong hash)
    {
        hash = 14695981039346656037UL;
        for (var i = 0; i < values.Count; i++)
        {
            if (!TryMembershipHash(values[i], out var child)) return false;
            hash = unchecked((hash ^ child) * 1099511628211UL);
        }
        hash = unchecked((hash ^ (ulong)values.Count) * 1099511628211UL) ^ 5UL;
        return true;
    }
    private static Dictionary<ulong, List<object>>? FrozenMembership(FrozenList values)
    {
        if (values.MembershipChecked) return values.Membership;
        lock (SnapshotLock)
        {
            if (values.MembershipChecked) return values.Membership;
            var index = new Dictionary<ulong, List<object>>();
            foreach (var value in values)
            {
                if (!TryMembershipHash(value, out var hash))
                {
                    values.MembershipChecked = true;
                    return null;
                }
                if (!index.TryGetValue(hash, out var bucket)) index[hash] = bucket = new();
                bucket.Add(value);
            }
            values.Membership = index;
            values.MembershipChecked = true;
            return index;
        }
    }
    private static HashSet<string>? FrozenStrings(FrozenList values)
    {
        if (values.StringsChecked) return values.Strings;
        lock (SnapshotLock)
        {
            if (values.StringsChecked) return values.Strings;
            var strings = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (value is not string text) { values.StringsChecked = true; return null; }
                strings.Add(text);
            }
            values.Strings = strings;
            values.StringsChecked = true;
            return strings;
        }
    }
    private sealed class MutableState
    {
        internal int RecordReads;
        internal FrozenList? Backbone;
        internal FrozenList? AppendBase;
        internal bool IndexChecked;
        internal Dictionary<string, int>? Index;
        internal object? Invalidation;
        // Most thawed branches have exactly one owner. Do not allocate a List
        // and its backing array for the common single-parent case.
        internal WeakReference<List<object>>? Parent;
        internal List<WeakReference<List<object>>>? OtherParents;
    }
    private static ConditionalWeakTable<List<object>, MutableState> MutableStates => Caches.MutableStates;
    private static List<object> Thaw(FrozenList frozen)
    {
        var result = frozen.CopyRoot();
        MutableStates.Add(result, new MutableState { Backbone = frozen });
        return result;
    }
    private static void LinkParent(List<object> child, List<object> parent) => LinkParent(child, parent, MutableStates);
    private static void LinkParent(List<object> child, List<object> parent, ConditionalWeakTable<List<object>, MutableState> states)
    {
        var state = states.GetOrCreateValue(child);
        if (state.Parent is null) { state.Parent = new(parent); return; }
        if (!state.Parent.TryGetTarget(out var first)) { state.Parent.SetTarget(parent); return; }
        if (ReferenceEquals(first, parent)) return;
        var others = state.OtherParents ??= new();
        for (var i = others.Count - 1; i >= 0; i--)
        {
            if (!others[i].TryGetTarget(out var existing)) others.RemoveAt(i);
            else if (ReferenceEquals(existing, parent)) return;
        }
        others.Add(new WeakReference<List<object>>(parent));
    }
    private static void Invalidate(object value)
    {
        var list = MutableItems(value);
        var states = MutableStates;
        if (!states.TryGetValue(list, out var initial)) return;
        // No ancestor can depend on this owner; invalidate only its own caches.
        if (initial.Parent is null && initial.OtherParents is not { Count: > 0 })
        {
            initial.Backbone = null;
            initial.AppendBase = null;
            initial.IndexChecked = false;
            initial.Index = null;
            return;
        }
        // An operation-local identity handles aliases and cycles without a
        // HashSet for every write. The usual single-owner path is iterative;
        // allocate a work stack only when the graph actually branches.
        var invalidation = new object();
        Stack<List<object>>? pending = null;
        List<object>? current = list;
        while (current is not null)
        {
            List<object>? next = null;
            if (states.TryGetValue(current, out var state) && !ReferenceEquals(state.Invalidation, invalidation))
            {
                state.Invalidation = invalidation;
                state.Backbone = null;
                state.AppendBase = null;
                state.IndexChecked = false;
                state.Index = null;
                if (state.Parent is { } first)
                {
                    if (first.TryGetTarget(out var parent)) next = parent;
                    else state.Parent = null;
                }
                if (state.OtherParents is { } others)
                    for (var i = others.Count - 1; i >= 0; i--)
                    {
                        if (!others[i].TryGetTarget(out var parent)) { others.RemoveAt(i); continue; }
                        if (next is null) next = parent;
                        else (pending ??= new Stack<List<object>>()).Push(parent);
                    }
            }
            if (next is null && pending is not null && pending.TryPop(out var queued)) next = queued;
            current = next;
        }
    }
    // Metadata only: keys and immutable last-slot indices, never field values.
    private sealed class RecordLayout(string[] ownedKeys, Dictionary<string, int> lastSlots)
    {
        internal readonly string[] OrderedKeys = ownedKeys;
        internal readonly Dictionary<string, int> LastSlots = lastSlots;
    }
    private static Dictionary<ulong, List<RecordLayout>> RecordLayouts => Caches.RecordLayouts;
    private static int RecordLayoutCount { get => Caches.RecordLayoutCount; set => Caches.RecordLayoutCount = value; }
    private static long RecordLayoutCharacters { get => Caches.RecordLayoutCharacters; set => Caches.RecordLayoutCharacters = value; }
    private const int RecordLayoutLimit = 128;
    private const int RecordLayoutSlotLimit = 128;
    private const long RecordLayoutCharacterLimit = 4096;
    private const long RecordLayoutPoolCharacterLimit = 65536;
    private static Dictionary<string, int>? FrozenIndex(FrozenList record)
    {
        if (record.IndexChecked) return record.Index;
        lock (SnapshotLock)
        {
            if (record.IndexChecked) return record.Index;
            var allText = true;
            long characters = 0;
            ulong fingerprint = 14695981039346656037UL;
            // Validate every pair before any cache lookup, including mixed keys.
            // No value slot is read, and invalid pairs retain the original fallback.
            foreach (var item in record)
            {
                if (!TryItems(item, out var pair) || pair.Count == 0)
                { record.IndexChecked = true; return null; }
                if (pair[0] is string key)
                {
                    if (record.Count > RecordLayoutSlotLimit) continue;
                    characters += key.Length;
                    fingerprint = unchecked((fingerprint ^ (ulong)StringComparer.Ordinal.GetHashCode(key)) * 1099511628211UL);
                }
                else allText = false;
            }
            fingerprint = unchecked((fingerprint ^ (ulong)record.Count) * 1099511628211UL);
            if (allText && record.Count <= RecordLayoutSlotLimit && characters <= RecordLayoutCharacterLimit)
            {
                if (RecordLayouts.TryGetValue(fingerprint, out var bucket))
                    foreach (var layout in bucket)
                    {
                        if (layout.OrderedKeys.Length != record.Count) continue;
                        var same = true;
                        for (var i = 0; i < record.Count; i++)
                            if (!string.Equals(layout.OrderedKeys[i], (string)((FrozenList)record[i])[0], StringComparison.Ordinal))
                            { same = false; break; }
                        if (!same) continue;
                        record.Index = layout.LastSlots;
                        record.IndexChecked = true;
                        return layout.LastSlots;
                    }
                // Miss only: do not allocate a keys array or dictionary on a hit.
                var keys = new string[record.Count];
                var slots = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < record.Count; i++)
                {
                    var key = keys[i] = (string)((FrozenList)record[i])[0];
                    slots[key] = i;
                }
                if (RecordLayoutCount >= RecordLayoutLimit || RecordLayoutCharacters + characters > RecordLayoutPoolCharacterLimit)
                {
                    // Existing FrozenList.Index references keep their dictionaries.
                    // Clear only pool roots, never any published dictionary.
                    RecordLayouts.Clear();
                    RecordLayoutCount = 0;
                    RecordLayoutCharacters = 0;
                }
                if (!RecordLayouts.TryGetValue(fingerprint, out bucket)) RecordLayouts[fingerprint] = bucket = new();
                bucket.Add(new RecordLayout(keys, slots));
                RecordLayoutCount++;
                RecordLayoutCharacters += characters;
                record.Index = slots;
                record.IndexChecked = true;
                return slots;
            }
            // Mixed keys, large tables and excessive text retain the local index.
            // Admission limits never reject an otherwise valid DV value.
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < record.Count; i++)
                if (((FrozenList)record[i])[0] is string key) index[key] = i;
            record.Index = index;
            record.IndexChecked = true;
            return index;
        }
    }
    private static Dictionary<string, int>? MutableIndex(List<object> record)
    {
        var state = MutableStates.GetOrCreateValue(record);
        if (state.IndexChecked) return state.Index;
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        // Link all key pairs so edits invalidate the owner, including irregular records.
        var regular = true;
        for (var i = 0; i < record.Count; i++)
        {
            if (!TryItems(record[i], out var pair)) { regular = false; continue; }
            if (pair is List<object> mutablePair) LinkParent(mutablePair, record);
            if (pair.Count == 0) { regular = false; continue; }
            if (pair[0] is string key) index[key] = i;
        }
        state.Index = regular ? index : null;
        state.IndexChecked = true;
        return state.Index;
    }
    private static List<object> MutableItems(object value) => value is FrozenList
        ? throw new InvalidOperationException("Cannot mutate an evidence snapshot; copy it first")
        : value as List<object> ?? throw new Exception("Expected List");
    private static object SnapshotLock => Caches.SnapshotLock;
    // Weak targets do not remove their dictionary keys or queue entries. After a
    // full collection, retire those dead buckets incrementally, including trees
    // reached through memo_get/put rather than the public snapshot entry point.
    // Live identities are never evicted: memo keys and DAG sharing depend on them.
    private static void MaintainSnapshots(ValueCaches caches)
    {
        if ((++caches.SnapshotMaintenanceTicks & 63) != 0) return;
        var generation = GC.CollectionCount(2);
        if (generation != caches.SnapshotGeneration)
        {
            caches.SnapshotGeneration = generation;
            caches.SnapshotSweepRemaining = caches.SnapshotBucketOrder.Count;
        }
        var budget = Math.Min(128, caches.SnapshotSweepRemaining);
        caches.SnapshotSweepRemaining -= budget;
        while (budget-- > 0 && caches.SnapshotBucketOrder.TryDequeue(out var key))
        {
            var bucket = caches.SnapshotPool[key];
            bucket.RemoveAll(reference => !reference.TryGetTarget(out _));
            if (bucket.Count == 0) caches.SnapshotPool.Remove(key);
            else caches.SnapshotBucketOrder.Enqueue(key);
        }
    }
    public static object snapshot(object value)
    {
        if (value is not List<object>) return value;
        var caches = Caches;
        lock (caches.SnapshotLock)
        {
            MaintainSnapshots(caches);
            return SnapshotValue(value, caches);
        }
    }
    private static object SnapshotValue(object value) => SnapshotValue(value, Caches);
    private static object SnapshotValue(object value, ValueCaches caches)
    {
        if (value is FrozenList || value is not List<object> items) return value;
        if (items.Count == 0)
        {
            // A fresh empty literal needs no weak mutable-state registration.
            // Existing parent links remain intact and still invalidate normally.
            if (caches.MutableStates.TryGetValue(items, out var emptyState)) emptyState.Backbone = caches.EmptySnapshot;
            return caches.EmptySnapshot;
        }
        var state = caches.MutableStates.GetOrCreateValue(items);
        if (state.Backbone is not null) return state.Backbone;
        if (items.Count == 2 && items[0] is string key
            && items[1] is string or double or bool or Rational or FrozenList or List<object>)
        {
            // Text-headed pairs are common transient values. Reuse the same
            // frozen-pair pool without allocating a candidate array on a hit.
            // Link a mutable child before freezing, just as the general path
            // does, so its later writes invalidate this owner's backbone.
            var valuePart = items[1];
            if (valuePart is List<object> mutablePart) LinkParent(mutablePart, items, caches.MutableStates);
            var pair = (FrozenList)FrozenTextPair(key, SnapshotValue(valuePart, caches));
            state.Backbone = pair;
            state.AppendBase = null;
            return pair;
        }
        MaintainSnapshots(caches);
        var children = items.Count == 0 ? Array.Empty<object>() : new object[items.Count];
        ulong hash = 14695981039346656037UL;
        ulong compatibleHash = hash;
        var compatible = true;
        var start = 0;
        if (state.AppendBase is { } prefix && prefix.Count >= 64 && prefix.Count <= items.Count)
        {
            prefix.CopyPrefixTo(children);
            start = prefix.Count;
            hash = prefix.SequenceHash;
            compatibleHash = prefix.CompatibleSequenceHash;
            compatible = prefix.MembershipHashEligible;
        }
        for (var i = start; i < items.Count; i++)
        {
            if (items[i] is List<object> mutableChild) LinkParent(mutableChild, items, caches.MutableStates);
            var child = children[i] = SnapshotValue(items[i], caches);
            if (TryMembershipHash(child, out var valueHash))
                compatibleHash = unchecked((compatibleHash ^ valueHash) * 1099511628211UL);
            else compatible = false;
            var part = child is FrozenList list ? list.Hash : unchecked((ulong)HashCode.Combine(child.GetType(), child));
            hash = unchecked((hash ^ part) * 1099511628211UL);
        }
        var sequenceHash = hash;
        hash = unchecked((hash ^ (ulong)items.Count) * 1099511628211UL);
        if (!caches.SnapshotPool.TryGetValue(hash, out var bucket))
        {
            caches.SnapshotPool[hash] = bucket = new();
            caches.SnapshotBucketOrder.Enqueue(hash);
        }
        for (var i = bucket.Count - 1; i >= 0; i--)
        {
            if (!bucket[i].TryGetTarget(out var existing)) { bucket.RemoveAt(i); continue; }
            if (existing.Count != children.Length) continue;
            var same = true;
            for (var j = 0; same && j < children.Length; j++)
                same = existing[j] is double left && children[j] is double right
                    ? BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right)
                    : Equals(existing[j], children[j]);
            if (same) { state.Backbone = existing; state.AppendBase = null; return existing; }
        }
        var frozen = new FrozenList(children, hash, sequenceHash)
        {
            Index = state.Index,
            IndexChecked = state.IndexChecked,
            MembershipHash = unchecked((compatibleHash ^ (ulong)items.Count) * 1099511628211UL) ^ 5UL,
            MembershipHashEligible = compatible,
            MembershipHashChecked = true
        };
        frozen.CompatibleSequenceHash = compatibleHash;
        bucket.Add(new WeakReference<FrozenList>(frozen));
        state.Backbone = frozen;
        state.AppendBase = null;
        return frozen;
    }
    private static object ReadItem(object owner, int index)
    {
        if (owner is FrozenList readOnly) return readOnly[index];
        var items = MutableItems(owner);
        var value = items[index];
        if (value is FrozenList frozen)
        {
            // Materialize only this owner's selected branch, preserving siblings.
            value = Thaw(frozen);
            LinkParent((List<object>)value, items);
            items[index] = value;
        }
        return value;
    }
    public static object fail(object a) => throw new Exception(Text(a));
    public static object arg(object i) => Arguments[Index(i)];
    public static object arg_count() => (double)Arguments.Length;
    public static object json_parse(object text)
    {
        using var document = JsonDocument.Parse(Text(text), new JsonDocumentOptions { MaxDepth = 256 });
        return ReadJson(document.RootElement);
    }
    private static object ReadJson(JsonElement item) => item.ValueKind switch
    {
        JsonValueKind.Array => item.EnumerateArray().Select(ReadJson).ToList(),
        JsonValueKind.String => item.GetString()!,
        JsonValueKind.Number => Num(item.GetDouble()),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new Exception("DV persistence supports arrays, text, finite numbers and booleans")
    };
    // Serialization preserves the supplied DV value. Evidence projection belongs
    // to explicit DV reporting APIs, never to implicit field-name filtering.
    private static void WriteJsonFast(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string s:
                writer.WriteStringValue(s);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case Rational q:
                Span<char> qBuf = stackalloc char[128];
                if (q.Denominator.IsOne)
                {
                    if (q.Numerator.TryFormat(qBuf, out int written, provider: CultureInfo.InvariantCulture))
                    {
                        writer.WriteStringValue(qBuf[..written]);
                        break;
                    }
                }
                else
                {
                    if (q.Numerator.TryFormat(qBuf, out int nWritten, provider: CultureInfo.InvariantCulture))
                    {
                        qBuf[nWritten] = '/';
                        if (q.Denominator.TryFormat(qBuf[(nWritten + 1)..], out int dWritten, provider: CultureInfo.InvariantCulture))
                        {
                            writer.WriteStringValue(qBuf[..(nWritten + 1 + dWritten)]);
                            break;
                        }
                    }
                }
                writer.WriteStringValue(q.ToString());
                break;
            case object dvList when TryItems(dvList, out var list):
                writer.WriteStartArray();
                for (var i = 0; i < list.Count; i++)
                {
                    WriteJsonFast(writer, list[i]);
                    if (writer.BytesPending >= 16 * 1024 * 1024) writer.Flush();
                }
                writer.WriteEndArray();
                break;
            default:
                throw new Exception("DV persistence supports arrays, text, finite numbers and booleans");
        }
    }
    private sealed class JsonEncoding(string text)
    {
        internal readonly string Text = text;
    }
    private static ConditionalWeakTable<FrozenList, JsonEncoding> JsonEncodings => Caches.JsonEncodings;
    public static object json_stringify(object value)
    {
        // Only frozen identities are cacheable. Mutable values retain the exact
        // pre-existing serialization path and never reuse a stale encoding.
        if (value is FrozenList frozen)
            return JsonEncodings.GetValue(frozen, key => new JsonEncoding(EncodeJson(key))).Text;
        return EncodeJson(value);
    }
    private static string EncodeJson(object value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { SkipValidation = true }))
        {
            WriteJsonFast(writer, value);
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }
    private static readonly object OutputLock = new();
    private static readonly Stream StandardOutput = Console.OpenStandardOutput();
    private static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    // Same value encoding as json_stringify, without a whole-packet UTF-16 copy.
    private static bool binaryOutputHeader;
    private static bool binaryRecording { get => Caches.BinaryRecording; set => Caches.BinaryRecording = value; }
    public static object json_print(object value)
    {
        lock (OutputLock)
        {
            if (binaryRecording || Environment.GetEnvironmentVariable("DV_BINARY_STDOUT") == "1")
            {
                if (!binaryOutputHeader) { StandardOutput.Write(BinaryEvidence.Magic); binaryOutputHeader = true; }
                // Projection packets are bounded values; exact atoms remain tagged.
                BinaryEvidence.WriteFrame(StandardOutput, BinaryProjection(value)); StandardOutput.Flush(); return true;
            }
            using var writer = new Utf8JsonWriter(StandardOutput, new JsonWriterOptions { SkipValidation = true });
            WriteJsonFast(writer, value);
            writer.Flush();
            StandardOutput.Write(NewLineBytes);
            StandardOutput.Flush();
        }
        return true;
    }

    // Opt-in evidence I/O only. The disabled hook never traverses the supplied value.
    // Values are frozen before registration; neither recording nor references mutate them.
    private static object BinaryProjection(object value)
    {
        if (value is Rational exact) return exact.ToString();
        if (TryItems(value, out var items)) return items.Select(BinaryProjection).ToList();
        return value;
    }
    private static RawDagWriter? RawEvidence { get => Caches.RawEvidence; set => Caches.RawEvidence = value; }
    private static object RawLock => Caches.RawLock;
    public static object raw_open(object path)
    {
        if (!AuditMode) throw new InvalidOperationException("Raw evidence requires audit mode");
        lock (RawLock)
        {
            if (RawEvidence is not null) throw new InvalidOperationException("Raw recorder already opened; abort a failed recorder before reopening");
            RawEvidence = new RawDagWriter(Text(path));
            binaryRecording = true;
            return true;
        }
    }
    public static object raw_event(object value)
    {
        if (!AuditMode || RawRecordingContext.Value is false) return true;
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Event(value);
            return true;
        }
    }
    public static object raw_checkpoint(object name, object value)
    {
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Checkpoint(Text(name), value);
            return true;
        }
    }
    public static object raw_finish(object value)
    {
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Finish(value);
            RawEvidence = null;
            return true;
        }
    }
    // Explicit recovery only: preserve the partial file, publish no seal/final name.
    public static object raw_abort()
    {
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Abort();
            RawEvidence = null;
            return true;
        }
    }
    public static object raw_ref(object value)
    {
        lock (RawLock)
        {
            if (RawEvidence is null || !IsDvList(value)) return false;
            return RawEvidence.Reference(value);
        }
    }

    private sealed record RawNode(long Id, long Frame);
    private sealed class RawDagWriter
    {
        private readonly long clockStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        private readonly double startupMilliseconds = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds;
        private readonly string destination;
        private readonly string partial;
        private readonly FileStream stream;
        private readonly ConditionalWeakTable<FrozenList, RawNode> nodes = new();
        private readonly HashSet<string> eventIds = new(StringComparer.Ordinal);
        private readonly BinaryEvidence.Encoder frameBuffer = new();
        private readonly IncrementalHash chainDigest;
        private readonly byte[] previous = Encoding.ASCII.GetBytes(new string('0', 64));
        private long nextNode, sequence, flushedSequence, eventCount;
        private int payloadOffset;
        private bool finished, failed;
        internal RawDagWriter(string path)
        {
            BinaryEvidence.RequireBinaryPath(path);
            destination = Path.GetFullPath(path); partial = destination + ".partial";
            if (File.Exists(destination)) throw new IOException("Evidence destination already exists: " + destination);
            stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024);
            try
            {
                chainDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                stream.Write(BinaryEvidence.Magic);
                BeginFrame("header", 5);
                frameBuffer.Text("format"); frameBuffer.Text("dv-raw-dag-v2");
                frameBuffer.Text("value_encoding"); frameBuffer.Text("typed binary atoms and prior node references");
                frameBuffer.Text("checksum"); frameBuffer.Text("sha256(previous-ascii + LF + sequence-ascii + LF + binary-payload)");
                frameBuffer.Text("clock_origin"); frameBuffer.Text("process-start UTC anchor plus monotonic elapsed; event commit emission, includes initialization");
                CompleteFrame();
                FlushCommit();
            }
            catch { chainDigest?.Dispose(); stream.Dispose(); throw; }
        }
        private void RequireOpen()
        {
            if (finished || failed) throw new InvalidOperationException("Raw recorder is closed or failed");
        }
        private void FlushCommit()
        {
            if (flushedSequence == sequence) return;
            try { stream.Flush(); flushedSequence = sequence; }
            catch { failed = true; throw; }
        }
        private void BeginFrame(string kind, int fields)
        {
            RequireOpen();
            frameBuffer.Clear();
            frameBuffer.Map(4);
            frameBuffer.Text("seq"); frameBuffer.Integer(sequence);
            frameBuffer.Text("prev"); frameBuffer.Utf8Text(previous);
            frameBuffer.Text("payload");
            payloadOffset = frameBuffer.Count;
            frameBuffer.Map(fields);
            frameBuffer.Text("kind"); frameBuffer.Text(kind);
        }
        private long CompleteFrame()
        {
            try
            {
                Span<byte> number = stackalloc byte[20];
                System.Buffers.Text.Utf8Formatter.TryFormat(sequence, number, out var numberLength);
                chainDigest.AppendData(previous);
                chainDigest.AppendData("\n"u8);
                chainDigest.AppendData(number[..numberLength]);
                chainDigest.AppendData("\n"u8);
                chainDigest.AppendData(frameBuffer.WrittenSpan[payloadOffset..]);
                Span<byte> checksum = stackalloc byte[32];
                if (!chainDigest.TryGetHashAndReset(checksum, out var checksumLength) || checksumLength != checksum.Length)
                    throw new InvalidDataException("Invalid DAG digest length");
                Span<byte> text = stackalloc byte[64];
                ReadOnlySpan<byte> hex = "0123456789abcdef"u8;
                for (var i = 0; i < checksum.Length; i++)
                {
                    text[i * 2] = hex[checksum[i] >> 4];
                    text[i * 2 + 1] = hex[checksum[i] & 15];
                }
                frameBuffer.Text("sha256"); frameBuffer.Utf8Text(text);
                BinaryEvidence.WriteEncodedFrame(stream, frameBuffer.WrittenSpan);
                text.CopyTo(previous);
                return sequence++;
            }
            catch { failed = true; throw; }
        }
        private RawNode Node(object value)
        {
            RequireOpen();
            if (!IsDvList(value)) throw new ArgumentException("Raw DAG roots must be DV lists/records");
            return FrozenNode((FrozenList)snapshot(value));
        }
        private RawNode FrozenNode(FrozenList frozen)
        {
            if (nodes.TryGetValue(frozen, out var known)) return known;
            try
            {
                // Define children before starting this parent's envelope. The
                // second pass only reads writer-local identities; it allocates
                // no reference dictionaries or duplicate payload tree.
                for (var i = 0; i < frozen.Count; i++)
                    if (frozen[i] is FrozenList child) FrozenNode(child);
                var id = nextNode++;
                BeginFrame("node", 3);
                frameBuffer.Text("node_id"); frameBuffer.Integer(id);
                frameBuffer.Text("items"); frameBuffer.List(frozen.Count);
                for (var i = 0; i < frozen.Count; i++)
                {
                    var item = frozen[i];
                    if (item is FrozenList child)
                    {
                        if (!nodes.TryGetValue(child, out var node)) throw new InvalidDataException("Missing prior DAG identity");
                        frameBuffer.Map(1); frameBuffer.Text("node"); frameBuffer.Integer(node.Id);
                    }
                    else if (item is Rational exact)
                    {
                        frameBuffer.Map(1); frameBuffer.Text("rational"); frameBuffer.Text(exact.ToString());
                    }
                    else frameBuffer.Value(item, 3);
                }
                var frame = CompleteFrame();
                var result = new RawNode(id, frame); nodes.Add(frozen, result); return result;
            }
            catch { failed = true; throw; }
        }
        internal void Event(object value)
        {
            var frozen = snapshot(value); var id = Text(record_find(frozen, "id", false));
            if (!eventIds.Add(id)) { failed = true; throw new InvalidOperationException("Repeated raw event ID: " + id); }
            var node = Node(frozen);
            try
            {
                BeginFrame("event", 5);
                frameBuffer.Text("event_id"); frameBuffer.Text(id);
                frameBuffer.Text("root"); frameBuffer.Integer(node.Id);
                frameBuffer.Text("ordinal"); frameBuffer.Integer(eventCount);
                frameBuffer.Text("process_elapsed_ms"); frameBuffer.Value(startupMilliseconds + System.Diagnostics.Stopwatch.GetElapsedTime(clockStarted).TotalMilliseconds, 0);
                CompleteFrame();
                FlushCommit(); eventCount++;
            }
            catch { failed = true; throw; }
        }
        internal void Checkpoint(string name, object value)
        {
            var node = Node(value);
            try
            {
                BeginFrame("checkpoint", 4);
                frameBuffer.Text("name"); frameBuffer.Text(name);
                frameBuffer.Text("root"); frameBuffer.Integer(node.Id);
                frameBuffer.Text("events"); frameBuffer.Integer(eventCount);
                CompleteFrame(); FlushCommit();
            }
            catch { failed = true; throw; }
        }
        internal object Reference(object value)
        {
            var node = Node(value); FlushCommit();
            return snapshot(new List<object>
            {
                new List<object> { "format", "dv-raw-dag-v2" },
                new List<object> { "journal", Path.GetFileName(destination) },
                new List<object> { "partial_journal", Path.GetFileName(partial) },
                new List<object> { "node_id", node.Id.ToString(CultureInfo.InvariantCulture) },
                new List<object> { "definition_frame", node.Frame.ToString(CultureInfo.InvariantCulture) }
            });
        }
        internal void Finish(object value)
        {
            Checkpoint("final", value);
            try
            {
                BeginFrame("seal", 4);
                frameBuffer.Text("events"); frameBuffer.Integer(eventCount);
                frameBuffer.Text("nodes"); frameBuffer.Integer(nextNode);
                frameBuffer.Text("final_checkpoint"); frameBuffer.Flag(true);
                CompleteFrame();
                FlushCommit(); stream.Dispose(); chainDigest.Dispose(); File.Move(partial, destination); finished = true;
            }
            catch { failed = true; stream.Dispose(); chainDigest.Dispose(); throw; }
        }
        internal void Abort() { failed = true; stream.Dispose(); chainDigest.Dispose(); }
    }
    public static void WriteEvidenceFile(string path, object value)
    {
        var writer = new RawDagWriter(path);
        try { writer.Checkpoint("initial", value); writer.Finish(value); }
        catch { writer.Abort(); throw; }
    }
    public static object ReadEvidenceFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        BinaryEvidence.RequireHeader(stream);
        var values = new List<object>(); var previous = new string('0', 64); long sequence = 0;
        object? final = null; bool sealedFile = false, initial = false;
        while (true)
        {
            var raw = BinaryEvidence.ReadFrame(stream, out var end); if (end) break;
            if (sealedFile) throw new InvalidDataException("Frames after final seal");
            var envelope = raw as Dictionary<string, object?> ?? throw new InvalidDataException("Invalid DAG envelope");
            if (envelope.Count != 4 || (long)envelope["seq"]! != sequence || (string)envelope["prev"]! != previous)
                throw new InvalidDataException("DAG sequence/checksum predecessor mismatch");
            var payload = (Dictionary<string, object?>)envelope["payload"]!;
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(Encoding.ASCII.GetBytes(previous + "\n" + sequence.ToString(CultureInfo.InvariantCulture) + "\n"));
            digest.AppendData(BinaryEvidence.Encode(payload));
            var checksum = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            if ((string)envelope["sha256"]! != checksum) throw new InvalidDataException("DAG checksum mismatch");
            previous = checksum; var kind = (string)payload["kind"]!;
            if (sequence++ == 0)
            {
                if (kind != "header" || (string)payload["format"]! != "dv-raw-dag-v2") throw new InvalidDataException("Unsupported DAG header");
                continue;
            }
            if (final is not null && kind != "seal") throw new InvalidDataException("Unsealed final checkpoint");
            if (kind == "node")
            {
                if ((long)payload["node_id"]! != values.Count) throw new InvalidDataException("Invalid DAG node order");
                var items = (List<object?>)payload["items"]!; var value = new List<object>();
                foreach (var item in items)
                {
                    if (item is Dictionary<string, object?> atom)
                    {
                        if (atom.Count != 1) throw new InvalidDataException("Invalid DAG atom");
                        if (atom.TryGetValue("node", out var reference))
                        {
                            var index = (long)reference!;
                            if (index < 0 || index >= values.Count) throw new InvalidDataException("Missing DAG node");
                            value.Add(values[(int)index]);
                        }
                        else if (atom.TryGetValue("rational", out var exact)) value.Add(Rational.From((string)exact!));
                        else throw new InvalidDataException("Unknown DAG atom");
                    }
                    else value.Add(item is long integer ? (object)(double)integer : item ?? throw new InvalidDataException("Null DV atom"));
                }
                values.Add(snapshot(value));
            }
            else if (kind == "checkpoint")
            {
                var root = (long)payload["root"]!;
                if (root < 0 || root >= values.Count || (long)payload["events"]! != 0) throw new InvalidDataException("Invalid value-file checkpoint");
                initial |= (string)payload["name"]! == "initial";
                if ((string)payload["name"]! == "final") final = values[(int)root];
            }
            else if (kind == "seal")
            {
                if (!initial || final is null || payload["final_checkpoint"] is not true || (long)payload["nodes"]! != values.Count || (long)payload["events"]! != 0)
                    throw new InvalidDataException("Invalid binary value seal");
                sealedFile = true;
            }
            else throw new InvalidDataException("Unsupported frame in binary value file");
        }
        return sealedFile && final is not null ? final : throw new InvalidDataException("Incomplete binary value file");
    }
    public static object json_write(object path, object value) => throw new InvalidOperationException("JSON recording is forbidden; use raw DAG binary evidence");
    public static object compiler_cache_read(object path)
    {
        var filename = Text(path);
        return File.Exists(filename) ? json_parse(read(filename)) : false;
    }
    public static object compiler_cache_write(object path, object value)
    {
        var filename = Path.GetFullPath(Text(path));
        Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
        var temporary = filename + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, Text(json_stringify(value)), new UTF8Encoding(false));
        File.Move(temporary, filename, overwrite: true);
        return value;
    }
    public static object read(object p) => File.ReadAllText(Text(p), new UTF8Encoding(false, true));
    public static object write(object p, object s) { File.WriteAllText(Text(p), Text(s), new UTF8Encoding(false, true)); return s; }
    public static object print(object a)
    {
        lock (OutputLock)
        {
            if (ConsoleOutputContext.Value is { } output)
            {
                output.WriteLine(Text(show(a)));
                output.Flush();
                return a;
            }
            if (binaryRecording || Environment.GetEnvironmentVariable("DV_BINARY_STDOUT") == "1")
            {
                Console.Error.WriteLine(Text(show(a)));
                Console.Error.Flush();
            }
            else
            {
                Console.WriteLine(Text(show(a)));
                Console.Out.Flush();
            }
        }
        return a;
    }
    public static object Block(Func<object> f) => f();
}


// Exact arithmetic is a value ABI, independent of cognitive or plot semantics.
[JsonConverter(typeof(RationalJsonConverter))]
public sealed record Rational
{
    private static readonly Rational Zero = new(BigInteger.Zero, BigInteger.One);
    private static readonly Rational One = new(BigInteger.One, BigInteger.One);
    private static readonly Rational MinusOne = new(BigInteger.MinusOne, BigInteger.One);
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }
    public Rational(BigInteger n, BigInteger d)
    {
        if (d.IsZero) throw new DivideByZeroException();
        if (d.Sign < 0) { n = -n; d = -d; }
        if (d.IsOne) { Numerator = n; Denominator = BigInteger.One; return; }
        if (n.IsZero) { Numerator = BigInteger.Zero; Denominator = BigInteger.One; return; }
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        Numerator = n / gcd; Denominator = d / gcd;
    }
    public static Rational From(object value)
    {
        if (value is Rational q) return q;
        string text;
        if (value is double d)
        {
            d = R.Num(d);
            if (d == 0) return Zero;
            if (d == 1) return One;
            if (d == -1) return MinusOne;
            // Within the safe-integer range, the round-trip decimal spelling
            // denotes this exact integer. Larger values and fractions must keep
            // the original decimal conversion, not their binary significand.
            if (d >= -9007199254740991d && d <= 9007199254740991d && d == Math.Truncate(d))
                return new Rational(new BigInteger((long)d), BigInteger.One);
            text = d.ToString("R", CultureInfo.InvariantCulture);
        }
        else text = R.Text(value);
        var slash = text.IndexOf('/');
        if (slash >= 0) return new(BigInteger.Parse(text[..slash], CultureInfo.InvariantCulture), BigInteger.Parse(text[(slash+1)..], CultureInfo.InvariantCulture));
        var exponent = text.IndexOfAny(['e', 'E']);
        int power = 0;
        if (exponent >= 0) { power = int.Parse(text[(exponent+1)..], CultureInfo.InvariantCulture); text = text[..exponent]; }
        var point = text.IndexOf('.');
        if (point >= 0) { power -= text.Length - point - 1; text = text.Remove(point, 1); }
        var n = BigInteger.Parse(text, CultureInfo.InvariantCulture);
        return power >= 0 ? new(n * BigInteger.Pow(10, power), 1) : new(n, BigInteger.Pow(10, -power));
    }
    public Rational Add(Rational b)
    {
        if (b.Numerator.IsZero) return this;
        if (Numerator.IsZero) return b;
        return Denominator == b.Denominator
            ? new Rational(Numerator + b.Numerator, Denominator)
            : new Rational(Numerator*b.Denominator+b.Numerator*Denominator, Denominator*b.Denominator);
    }
    public Rational Subtract(Rational b)
    {
        if (b.Numerator.IsZero) return this;
        return Denominator == b.Denominator
            ? new Rational(Numerator - b.Numerator, Denominator)
            : new Rational(Numerator*b.Denominator-b.Numerator*Denominator, Denominator*b.Denominator);
    }
    public Rational Multiply(Rational b)
    {
        if (Numerator.IsZero || b.Numerator.IsZero) return Zero;
        if (b.Numerator.IsOne && b.Denominator.IsOne) return this;
        if (Numerator.IsOne && Denominator.IsOne) return b;
        return new Rational(Numerator*b.Numerator, Denominator*b.Denominator);
    }
    public Rational Divide(Rational b)
    {
        if (b.Numerator.IsZero) throw new DivideByZeroException();
        if (Numerator.IsZero) return Zero;
        if (b.Numerator.IsOne && b.Denominator.IsOne) return this;
        return new Rational(Numerator*b.Denominator, Denominator*b.Numerator);
    }
    public int CompareTo(Rational b) => Denominator == b.Denominator
        ? Numerator.CompareTo(b.Numerator) : (Numerator*b.Denominator).CompareTo(b.Numerator*Denominator);
    public override string ToString() => Denominator.IsOne ? Numerator.ToString(CultureInfo.InvariantCulture) : Numerator.ToString(CultureInfo.InvariantCulture)+"/"+Denominator.ToString(CultureInfo.InvariantCulture);
}
public sealed class RationalJsonConverter : JsonConverter<Rational>
{
    public override Rational Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => Rational.From(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, Rational value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
