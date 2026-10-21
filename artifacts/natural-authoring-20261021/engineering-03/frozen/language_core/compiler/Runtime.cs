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
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Utf8, leaveOpen: true);
        WriteValue(writer, value, 0);
        return output.ToArray();
    }
    private static void WriteValue(BinaryWriter writer, object? value, int depth)
    {
        if (depth > 512) throw new InvalidDataException("Binary evidence nesting exceeds 512");
        switch (value)
        {
            case null: writer.Write((byte)0); break;
            case bool flag: writer.Write((byte)(flag ? 2 : 1)); break;
            case int number: writer.Write((byte)3); writer.Write((long)number); break;
            case long number: writer.Write((byte)3); writer.Write(number); break;
            case double number when double.IsFinite(number): writer.Write((byte)4); writer.Write(number); break;
            case string text:
                var bytes = Utf8.GetBytes(text);
                writer.Write((byte)5); writer.Write(bytes.Length); writer.Write(bytes); break;
            case IDictionary<string, object?> map:
                writer.Write((byte)7); writer.Write(map.Count);
                foreach (var entry in map) { WriteValue(writer, entry.Key, depth + 1); WriteValue(writer, entry.Value, depth + 1); }
                break;
            case object?[] array:
                // Host JSON arrays decode to CLR arrays, not DV List/FrozenList.
                // This storage case does not expand the runtime's DV value domain.
                writer.Write((byte)8); writer.Write(array.Length);
                foreach (var item in array) WriteValue(writer, item, depth + 1);
                break;
            case JsonElement element: WriteValue(writer, FromJson(element), depth); break;
            default:
                if (!R.TryItems(value!, out var items)) throw new InvalidDataException("Unsupported binary evidence atom");
                writer.Write((byte)8); writer.Write(items.Count);
                foreach (var item in items) WriteValue(writer, item, depth + 1);
                break;
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
        var payload = Encode(value);
        if (payload.Length > MaxFrame) throw new InvalidDataException("Binary frame exceeds size limit");
        Span<byte> prefix = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        stream.Write(prefix); stream.Write(SHA256.HashData(payload)); stream.Write(payload);
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
            _ => Num(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture))
        };
    }
    public static object is_number(object s) => double.TryParse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n);
    // Runtime value predicate; unlike is_number, this never parses or requires Text.
    public static object is_text(object value) => value is string;
    public static object is_numeric_value(object value) => value is double n && double.IsFinite(n) || value is Rational q && q.Denominator.Sign > 0;
    public static object add(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Add(Rational.From(b)) : Num(Num(a) + Num(b));
    public static object sub(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Subtract(Rational.From(b)) : Num(Num(a) - Num(b));
    public static object mul(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Multiply(Rational.From(b)) : Num(Num(a) * Num(b));
    public static object div(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Divide(Rational.From(b)) : Num(Num(a) / Num(b));
    public static object lt(object a, object b) => a is Rational || b is Rational ? Rational.From(a).CompareTo(Rational.From(b)) < 0 : Num(a) < Num(b);
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
    public static object floor(object a) => Math.Floor(Num(a));
    public static object not(object a) => !Bool(a);
    public static object cat(object a, object b) => Text(a) + Text(b);
    // Generic linear concatenation, including UTF-16 fragments used by bootstrap.
    public static object text_join(object values)
    {
        var result = new StringBuilder();
        foreach (var value in Items(values)) result.Append(Text(value));
        return result.ToString();
    }
    public static object length(object a) => (double)Text(a).Length;
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
    public static object push(object a, object b) { Invalidate(a); MutableItems(a).Add(b); return a; }
    public static object at(object a, object i) => ReadItem(a, Index(i));
    // Persistent spine only: mirror dvs_replace's equality and selected reads.
    public static object list_with_index(object items, object index, object value)
    {
        var source = Items(items);
        var result = new List<object>(source.Count);
        for (var i = 0; i < source.Count; i++)
            result.Add(Bool(eq((double)i, index)) ? value : ReadItem(items, i));
        return result;
    }
    public static object count(object a) => a switch
    {
        FrozenList frozen => (double)frozen.Count,
        List<object> mutable => (double)mutable.Count,
        _ => (double)Items(a).Count
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
                if (index is not null) return index.TryGetValue(name, out var found) ? (double)found : -1d;
            }
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var k = RawItem(entry, 0);
                if (ReferenceEquals(k, name) || (k is string s && string.Equals(s, name, StringComparison.Ordinal)))
                    return (double)i;
            }
            return -1d;
        }
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            var k = RawItem(entry, 0);
            if (Bool(eq(k, key))) return (double)i;
        }
        return -1d;
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
    private static readonly object MissingSentinel = new();
    private static readonly List<object> MemoMiss = new() { false, false };

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
                    result.Add(new List<object> { key, copy_value(value) });
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
                    result.Add(new List<object> { key, copy_value(value) });
                    found = true;
                }
                else result.Add(frozen[i]);
            }
        }
        if (!found) result.Add(new List<object> { key, copy_value(value) });
        return result;
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
        var frozen = Items(snapshot(record));
        // Small one-off records favor the original short reverse scans.
        if (frozen.Count >= 4 && (changes.Count > 1 || frozen.Count >= 8)
            && TryStringRecordUpdate(frozen, changes, out var stringResult)) return stringResult;
        var result = new List<object>(frozen.Count + changes.Count);
        var keys = new List<object>(frozen.Count + changes.Count);
        HashSet<string>? stringKeys = new(StringComparer.Ordinal);
        foreach (var item in frozen)
        {
            var key = RawItem(item, 0);
            var updatedVal = record_find(updates, key, MissingSentinel);
            if (!ReferenceEquals(updatedVal, MissingSentinel))
                result.Add(new List<object> { key, copy_value(updatedVal) });
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
            result.Add(new List<object> { key, copy_value(record_find(updates, key, false)) });
            keys.Add(key);
        }
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
                ? new List<object> { key, copy_value(value) } : item);
            present.Add(key);
        }
        foreach (var item in changes)
        {
            var pair = Items(item);
            var key = (string)pair[0];
            if (present.Add(key)) result.Add(new List<object> { key, copy_value(updates[key]) });
        }
        return true;
    }
    private sealed class ValueMemo
    {
        internal readonly Dictionary<string, MemoPartition> Partitions = new(StringComparer.Ordinal);
        internal MemoPartition Partition(object key)
        {
            var name = TryItems(key, out var parts) && parts.Count > 0 && parts[0] is string tag ? tag : "";
            if (!Partitions.TryGetValue(name, out var partition)) Partitions[name] = partition = new();
            return partition;
        }
    }
    private sealed class MemoPartition
    {
        internal readonly Dictionary<object, object> Values = new();
        internal readonly Queue<object> Order = new();
    }
    private static readonly ConditionalWeakTable<FrozenList, ValueMemo> ValueMemos = new();
    public static object memo_get(object owner, object key)
    {
        lock (SnapshotLock)
        {
            var identity = owner is FrozenList fl ? fl : (FrozenList)SnapshotValue(Items(owner));
            var frozenKey = key is FrozenList fk ? fk : SnapshotValue(key);
            if (ValueMemos.TryGetValue(identity, out var ownerMemo) && ownerMemo.Partition(frozenKey).Values.TryGetValue(frozenKey, out var result))
                return new List<object> { true, copy_value(result) };
            return MemoMiss;
        }
    }
    public static object memo_put(object owner, object key, object value)
    {
        lock (SnapshotLock)
        {
            var identity = (FrozenList)SnapshotValue(Items(owner));
            var frozenKey = SnapshotValue(key);
            var memo = ValueMemos.GetOrCreateValue(identity).Partition(frozenKey);
            if (!memo.Values.ContainsKey(frozenKey))
            {
                if (memo.Values.Count >= 512) memo.Values.Remove(memo.Order.Dequeue());
                memo.Order.Enqueue(frozenKey);
            }
            memo.Values[frozenKey] = SnapshotValue(value);
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
        internal FrozenList(object[] ownedChildren, ulong hash)
        {
            children = ownedChildren;
            Hash = hash;
        }
        // List copies the private array into its own backing store in one pass;
        // the immutable source remains private and cannot be mutated by its owner.
        internal List<object> CopyRoot() => new(children);
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
        internal bool IndexChecked;
        internal Dictionary<string, int>? Index;
        // Most thawed branches have exactly one owner. Do not allocate a List
        // and its backing array for the common single-parent case.
        internal WeakReference<List<object>>? Parent;
        internal List<WeakReference<List<object>>>? OtherParents;
    }
    private static readonly ConditionalWeakTable<List<object>, MutableState> MutableStates = new();
    private static List<object> Thaw(FrozenList frozen)
    {
        var result = frozen.CopyRoot();
        MutableStates.Add(result, new MutableState { Backbone = frozen });
        return result;
    }
    private static void LinkParent(List<object> child, List<object> parent)
    {
        var state = MutableStates.GetOrCreateValue(child);
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
        if (!MutableStates.TryGetValue(list, out var initial)) return;
        // No ancestor can depend on this owner; invalidate only its own caches.
        if (initial.Parent is null && initial.OtherParents is not { Count: > 0 })
        {
            initial.Backbone = null;
            initial.IndexChecked = false;
            initial.Index = null;
            return;
        }
        var visited = new HashSet<List<object>>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<List<object>>();
        pending.Push(list);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current) || !MutableStates.TryGetValue(current, out var state)) continue;
            state.Backbone = null;
            state.IndexChecked = false;
            state.Index = null;
            if (state.Parent is { } first)
            {
                if (first.TryGetTarget(out var parent)) pending.Push(parent);
                else state.Parent = null;
            }
            if (state.OtherParents is { } others)
                for (var i = others.Count - 1; i >= 0; i--)
                    if (others[i].TryGetTarget(out var parent)) pending.Push(parent);
                    else others.RemoveAt(i);
        }
    }
    // Metadata only: keys and immutable last-slot indices, never field values.
    private sealed class RecordLayout(string[] ownedKeys, Dictionary<string, int> lastSlots)
    {
        internal readonly string[] OrderedKeys = ownedKeys;
        internal readonly Dictionary<string, int> LastSlots = lastSlots;
    }
    private static readonly Dictionary<ulong, List<RecordLayout>> RecordLayouts = new();
    private static int RecordLayoutCount;
    private static long RecordLayoutCharacters;
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
    private static readonly object SnapshotLock = new();
    private static readonly Dictionary<ulong, List<WeakReference<FrozenList>>> SnapshotPool = new();
    private static readonly Queue<ulong> SnapshotBucketOrder = new();
    private static int SnapshotMaintenanceTicks;
    private static int SnapshotGeneration = GC.CollectionCount(2);
    private static int SnapshotSweepRemaining;
    // Weak targets do not remove their dictionary keys or queue entries. After a
    // full collection, retire those dead buckets incrementally, including trees
    // reached through memo_get/put rather than the public snapshot entry point.
    // Live identities are never evicted: memo keys and DAG sharing depend on them.
    private static void MaintainSnapshots()
    {
        if ((++SnapshotMaintenanceTicks & 63) != 0) return;
        var generation = GC.CollectionCount(2);
        if (generation != SnapshotGeneration)
        {
            SnapshotGeneration = generation;
            SnapshotSweepRemaining = SnapshotBucketOrder.Count;
        }
        var budget = Math.Min(128, SnapshotSweepRemaining);
        SnapshotSweepRemaining -= budget;
        while (budget-- > 0 && SnapshotBucketOrder.TryDequeue(out var key))
        {
            var bucket = SnapshotPool[key];
            bucket.RemoveAll(reference => !reference.TryGetTarget(out _));
            if (bucket.Count == 0) SnapshotPool.Remove(key);
            else SnapshotBucketOrder.Enqueue(key);
        }
    }
    public static object snapshot(object value)
    {
        lock (SnapshotLock)
        {
            MaintainSnapshots();
            return SnapshotValue(value);
        }
    }
    private static object SnapshotValue(object value)
    {
        if (value is FrozenList || value is not List<object> items) return value;
        var state = MutableStates.GetOrCreateValue(items);
        if (state.Backbone is not null) return state.Backbone;
        MaintainSnapshots();
        var children = items.Count == 0 ? Array.Empty<object>() : new object[items.Count];
        ulong hash = 14695981039346656037UL;
        ulong compatibleHash = hash;
        var compatible = true;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is List<object> mutableChild) LinkParent(mutableChild, items);
            var child = children[i] = SnapshotValue(items[i]);
            if (TryMembershipHash(child, out var valueHash))
                compatibleHash = unchecked((compatibleHash ^ valueHash) * 1099511628211UL);
            else compatible = false;
            var part = child is FrozenList list ? list.Hash : unchecked((ulong)HashCode.Combine(child.GetType(), child));
            hash = unchecked((hash ^ part) * 1099511628211UL);
        }
        hash = unchecked((hash ^ (ulong)items.Count) * 1099511628211UL);
        if (!SnapshotPool.TryGetValue(hash, out var bucket))
        {
            SnapshotPool[hash] = bucket = new();
            SnapshotBucketOrder.Enqueue(hash);
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
            if (same) { state.Backbone = existing; return existing; }
        }
        var frozen = new FrozenList(children, hash)
        {
            MembershipHash = unchecked((compatibleHash ^ (ulong)items.Count) * 1099511628211UL) ^ 5UL,
            MembershipHashEligible = compatible,
            MembershipHashChecked = true
        };
        bucket.Add(new WeakReference<FrozenList>(frozen));
        state.Backbone = frozen;
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
    private static readonly ConditionalWeakTable<FrozenList, JsonEncoding> JsonEncodings = new();
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
    private static bool binaryRecording;
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
    private static RawDagWriter? RawEvidence;
    private static readonly object RawLock = new();
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
        if (!AuditMode) return true;
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
        private readonly string destination;
        private readonly string partial;
        private readonly FileStream stream;
        private readonly ConditionalWeakTable<FrozenList, RawNode> nodes = new();
        private readonly HashSet<string> eventIds = new(StringComparer.Ordinal);
        private long nextNode, sequence, flushedSequence, eventCount;
        private string previous = new('0', 64);
        private bool finished, failed;
        private static Dictionary<string, object?> Map(params (string Key, object? Value)[] fields) => fields.ToDictionary(x => x.Key, x => x.Value);
        internal RawDagWriter(string path)
        {
            BinaryEvidence.RequireBinaryPath(path);
            destination = Path.GetFullPath(path); partial = destination + ".partial";
            if (File.Exists(destination)) throw new IOException("Evidence destination already exists: " + destination);
            stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024);
            try
            {
                stream.Write(BinaryEvidence.Magic);
                Frame(Map(("kind", "header"), ("format", "dv-raw-dag-v2"),
                    ("value_encoding", "typed binary atoms and prior node references"),
                    ("checksum", "sha256(previous-ascii + LF + sequence-ascii + LF + binary-payload)")));
                FlushCommit();
            }
            catch { stream.Dispose(); throw; }
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
        private long Frame(Dictionary<string, object?> payload)
        {
            RequireOpen();
            try
            {
                var bytes = BinaryEvidence.Encode(payload);
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                digest.AppendData(Encoding.ASCII.GetBytes(previous + "\n" + sequence.ToString(CultureInfo.InvariantCulture) + "\n"));
                digest.AppendData(bytes);
                var checksum = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
                BinaryEvidence.WriteFrame(stream, Map(("seq", sequence), ("prev", previous), ("payload", payload), ("sha256", checksum)));
                previous = checksum;
                return sequence++;
            }
            catch { failed = true; throw; }
        }
        private RawNode Node(object value)
        {
            RequireOpen();
            if (!IsDvList(value)) throw new ArgumentException("Raw DAG roots must be DV lists/records");
            var frozen = (FrozenList)snapshot(value);
            if (nodes.TryGetValue(frozen, out var known)) return known;
            var items = new List<object?>();
            for (var i = 0; i < frozen.Count; i++)
                items.Add(IsDvList(frozen[i]) ? Map(("node", Node(frozen[i]).Id)) :
                    frozen[i] is Rational exact ? Map(("rational", exact.ToString())) : frozen[i]);
            var id = nextNode++;
            var frame = Frame(Map(("kind", "node"), ("node_id", id), ("items", items)));
            var result = new RawNode(id, frame); nodes.Add(frozen, result); return result;
        }
        internal void Event(object value)
        {
            var frozen = snapshot(value); var id = Text(record_find(frozen, "id", false));
            if (!eventIds.Add(id)) { failed = true; throw new InvalidOperationException("Repeated raw event ID: " + id); }
            var node = Node(frozen);
            Frame(Map(("kind", "event"), ("event_id", id), ("root", node.Id), ("ordinal", eventCount)));
            FlushCommit(); eventCount++;
        }
        internal void Checkpoint(string name, object value)
        {
            var node = Node(value);
            Frame(Map(("kind", "checkpoint"), ("name", name), ("root", node.Id), ("events", eventCount))); FlushCommit();
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
            Frame(Map(("kind", "seal"), ("events", eventCount), ("nodes", nextNode), ("final_checkpoint", true)));
            try { FlushCommit(); stream.Dispose(); File.Move(partial, destination); finished = true; }
            catch { failed = true; stream.Dispose(); throw; }
        }
        internal void Abort() { failed = true; stream.Dispose(); }
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
    public static object read(object p) => File.ReadAllText(Text(p), new UTF8Encoding(false, true));
    public static object write(object p, object s) { File.WriteAllText(Text(p), Text(s), new UTF8Encoding(false, true)); return s; }
    public static object print(object a)
    {
        lock (OutputLock)
        {
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
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }
    public Rational(BigInteger n, BigInteger d)
    {
        if (d.IsZero) throw new DivideByZeroException();
        if (d.Sign < 0) { n = -n; d = -d; }
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        Numerator = n / gcd; Denominator = d / gcd;
    }
    public static Rational From(object value)
    {
        if (value is Rational q) return q;
        var text = value is double d ? R.Num(d).ToString("R", CultureInfo.InvariantCulture) : R.Text(value);
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
    public Rational Add(Rational b) => new(Numerator*b.Denominator+b.Numerator*Denominator, Denominator*b.Denominator);
    public Rational Subtract(Rational b) => new(Numerator*b.Denominator-b.Numerator*Denominator, Denominator*b.Denominator);
    public Rational Multiply(Rational b) => new(Numerator*b.Numerator, Denominator*b.Denominator);
    public Rational Divide(Rational b) => new(Numerator*b.Denominator, Denominator*b.Numerator);
    public int CompareTo(Rational b) => (Numerator*b.Denominator).CompareTo(b.Numerator*Denominator);
    public override string ToString() => Denominator.IsOne ? Numerator.ToString(CultureInfo.InvariantCulture) : Numerator.ToString(CultureInfo.InvariantCulture)+"/"+Denominator.ToString(CultureInfo.InvariantCulture);
}
public sealed class RationalJsonConverter : JsonConverter<Rational>
{
    public override Rational Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => Rational.From(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, Rational value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
