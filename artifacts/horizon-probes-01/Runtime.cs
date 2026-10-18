using System.Globalization;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json;

namespace DuelView.Compiled;

// Platform/value ABI only. No reader, AST walker, evaluator or code generator.
public static class R
{
    public static string[] Arguments = [];
    public static string Text(object x) => x as string ?? throw new Exception("Expected Text");
    public static double Num(object x) => x is double n && double.IsFinite(n) ? n : throw new Exception("Expected finite Number");
    public static bool Bool(object x) => x is bool b ? b : throw new Exception("Expected Bool");
    public static List<object> Items(object x) => x as List<object> ?? throw new Exception("Expected List");
    public static object number(object s) => Num(double.Parse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture));
    public static object is_number(object s) => double.TryParse(Text(s), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n);
    // Runtime value predicate; unlike is_number, this never parses or requires Text.
    public static object is_numeric_value(object value) => value is double n && double.IsFinite(n) || value is Rational q && q.Denominator.Sign > 0;
    public static object add(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Add(Rational.From(b)) : Num(Num(a) + Num(b));
    public static object sub(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Subtract(Rational.From(b)) : Num(Num(a) - Num(b));
    public static object mul(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Multiply(Rational.From(b)) : Num(Num(a) * Num(b));
    public static object div(object a, object b) => a is Rational || b is Rational ? Rational.From(a).Divide(Rational.From(b)) : Num(Num(a) / Num(b));
    public static object lt(object a, object b) => a is Rational || b is Rational ? Rational.From(a).CompareTo(Rational.From(b)) < 0 : Num(a) < Num(b);
    public static object eq(object a, object b) => (a is Rational || b is Rational) && (a is Rational || a is double) && (b is Rational || b is double) ? Rational.From(a).Equals(Rational.From(b)) : Equals(a, b);
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
    public static object length(object a) => (double)Text(a).Length;
    private static int Index(object a) { var n = Num(a); return n >= 0 && n <= int.MaxValue && n == Math.Truncate(n) ? (int)n : throw new Exception("Invalid index"); }
    public static object slice(object a, object start, object count) => Text(a).Substring(Index(start), Index(count));
    public static object chr(object a, object i) => Text(a)[Index(i)].ToString();
    public static object code(object a) => Text(a).Length == 1 ? (double)Text(a)[0] : throw new Exception("Expected one UTF-16 unit");
    public static object show(object a) => a switch { string s => s, Rational q => q.ToString(), double n => n.ToString("R", CultureInfo.InvariantCulture), bool b => b ? "true" : "false", List<object> list => FormatList(list), _ => throw new Exception("Cannot format value") };
    private static string FormatList(List<object> list)
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
    public static object count(object a) => (double)Items(a).Count;
    public static object record_index(object record, object key)
    {
        var entries = Items(record);
        if (key is string name)
        {
            if (entries.Count >= 8)
            {
                var frozen = record as FrozenList;
                if (frozen is null && MutableStates.TryGetValue(entries, out var state)) frozen = state.Backbone;
                var index = frozen is not null ? FrozenIndex(frozen) : MutableIndex(entries);
                if (index is not null) return index.TryGetValue(name, out var found) ? (double)found : -1d;
            }
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var k = Items(entries[i])[0];
                if (ReferenceEquals(k, name) || (k is string s && string.Equals(s, name, StringComparison.Ordinal)))
                    return (double)i;
            }
            return -1d;
        }
        for (var i = entries.Count - 1; i >= 0; i--)
            if (Bool(eq(Items(entries[i])[0], key))) return (double)i;
        return -1d;
    }
    public static object record_find(object record, object key, object fallback)
    {
        var entries = Items(record);
        int foundIndex = -1;
        if (key is string name)
        {
            if (entries.Count >= 8)
            {
                var frozen = record as FrozenList;
                if (frozen is null && MutableStates.TryGetValue(entries, out var state)) frozen = state.Backbone;
                var index = frozen is not null ? FrozenIndex(frozen) : MutableIndex(entries);
                if (index is not null)
                {
                    if (index.TryGetValue(name, out foundIndex))
                    {
                        var pair = entries[foundIndex];
                        var pairItems = Items(pair);
                        var val = pairItems[1];
                        if (record is not FrozenList && val is FrozenList)
                            return ReadItem(ReadItem(record, foundIndex), 1);
                        return val;
                    }
                    return fallback;
                }
            }
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var k = Items(entries[i])[0];
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
                if (Bool(eq(Items(entries[i])[0], key)))
                {
                    foundIndex = i;
                    break;
                }
            }
        }
        if (foundIndex < 0) return fallback;
        var p = entries[foundIndex];
        var pItems = Items(p);
        var v = pItems[1];
        if (record is not FrozenList && v is FrozenList)
            return ReadItem(ReadItem(record, foundIndex), 1);
        return v;
    }
    // Structural value equality reads existing branches without materializing mutable copies.
    public static object same_value(object a, object b)
    {
        if (Bool(eq(a, b))) return true;
        if (a is not List<object> left || b is not List<object> right || left.Count != right.Count) return false;
        var leftBase = a as FrozenList;
        var rightBase = b as FrozenList;
        if (leftBase is null && MutableStates.TryGetValue(left, out var ls)) leftBase = ls.Backbone;
        if (rightBase is null && MutableStates.TryGetValue(right, out var rs)) rightBase = rs.Backbone;
        if (leftBase is not null && ReferenceEquals(leftBase, rightBase)) return true;
        // Different structural hashes cannot reject equality: exact and floating numbers compare by value.
        for (var i = 0; i < left.Count; i++)
            if (!Bool(same_value(left[i], right[i]))) return false;
        return true;
    }
    // Membership is a structural-value operation; reading never thaws child lists.
    public static object has_value(object items, object value)
    {
        var values = Items(items);
        if (value is string name)
        {
            var frozen = items as FrozenList;
            if (frozen is null && MutableStates.TryGetValue(values, out var state)) frozen = state.Backbone;
            if (frozen is not null && FrozenStrings(frozen) is { } strings) return strings.Contains(name);
        }
        for (var i = 0; i < values.Count; i++)
            if (Bool(same_value(values[i], value))) return true;
        return false;
    }
    // Ordered independent-copy record update; unchanged branches stay immutable until read.
    public static object record_with(object record, object key, object value)
    {
        var entries = Items(record);
        var result = new List<object>(entries.Count + 1);
        var frozen = Items(snapshot(record));
        var found = false;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (Bool(eq(Items(entry)[0], key)))
            {
                result.Add(new List<object> { key, copy_value(value) });
                found = true;
            }
            else result.Add(frozen[i]);
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
        var result = new List<object>(frozen.Count + changes.Count);
        var keys = new List<object>(frozen.Count + changes.Count);
        HashSet<string>? stringKeys = new(StringComparer.Ordinal);
        foreach (var item in frozen)
        {
            var key = Items(item)[0];
            if (Convert.ToDouble(record_index(updates, key)) >= 0)
                result.Add(new List<object> { key, copy_value(record_find(updates, key, false)) });
            else result.Add(item);
            keys.Add(key);
            if (key is string name) stringKeys?.Add(name);
            else stringKeys = null;
        }
        foreach (var item in changes)
        {
            var key = Items(item)[0];
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
    private sealed class ValueMemo
    {
        internal readonly Dictionary<string, MemoPartition> Partitions = new(StringComparer.Ordinal);
        internal MemoPartition Partition(object key)
        {
            var name = key is List<object> parts && parts.Count > 0 && parts[0] is string tag ? tag : "";
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
            var identity = (FrozenList)SnapshotValue(Items(owner));
            var frozenKey = SnapshotValue(key);
            if (ValueMemos.TryGetValue(identity, out var ownerMemo) && ownerMemo.Partition(frozenKey).Values.TryGetValue(frozenKey, out var result))
                return new List<object> { true, copy_value(result) };
            return new List<object> { false, false };
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
    public static object is_list(object a) => a is List<object>;
    public static object copy_value(object value)
    {
        if (value is not List<object>) return value;
        // Every copy owns its root. Descendants are immutable until accessed through this owner.
        var frozen = value is FrozenList ? value : snapshot(value);
        return Thaw((FrozenList)frozen);
    }
    // Evidence values are immutable copies. Interning changes storage, never their JSON shape.
    // Mutable copies remain independent through copy_value; only snapshot() uses this pool.
    private sealed class FrozenList(int capacity, ulong hash) : List<object>(capacity)
    {
        internal readonly ulong Hash = hash;
        internal volatile bool IndexChecked;
        internal Dictionary<string, int>? Index;
        internal volatile bool StringsChecked;
        internal HashSet<string>? Strings;
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
        internal FrozenList? Backbone;
        internal bool IndexChecked;
        internal Dictionary<string, int>? Index;
        internal readonly List<WeakReference<List<object>>> Parents = new();
    }
    private static readonly ConditionalWeakTable<List<object>, MutableState> MutableStates = new();
    private static List<object> Thaw(FrozenList frozen)
    {
        var result = new List<object>(frozen);
        MutableStates.Add(result, new MutableState { Backbone = frozen });
        return result;
    }
    private static void LinkParent(List<object> child, List<object> parent)
    {
        if (child is FrozenList) return;
        var state = MutableStates.GetOrCreateValue(child);
        for (var i = state.Parents.Count - 1; i >= 0; i--)
        {
            if (!state.Parents[i].TryGetTarget(out var existing)) state.Parents.RemoveAt(i);
            else if (ReferenceEquals(existing, parent)) return;
        }
        state.Parents.Add(new WeakReference<List<object>>(parent));
    }
    private static void Invalidate(object value)
    {
        var list = MutableItems(value);
        if (!MutableStates.TryGetValue(list, out _)) return;
        var visited = new HashSet<List<object>>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<List<object>>();
        pending.Push(list);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current) || !MutableStates.TryGetValue(current, out var state)) continue;
            state.Backbone = null;
            state.IndexChecked = false;
            state.Index = null;
            for (var i = state.Parents.Count - 1; i >= 0; i--)
                if (state.Parents[i].TryGetTarget(out var parent)) pending.Push(parent);
                else state.Parents.RemoveAt(i);
        }
    }
    private static Dictionary<string, int>? FrozenIndex(FrozenList record)
    {
        if (record.IndexChecked) return record.Index;
        lock (SnapshotLock)
        {
            if (record.IndexChecked) return record.Index;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in record)
                if (item is not List<object> pair || pair.Count == 0)
                { record.IndexChecked = true; return null; }
            for (var i = 0; i < record.Count; i++)
                if (Items(record[i])[0] is string key) index[key] = i;
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
            if (record[i] is not List<object> pair) { regular = false; continue; }
            LinkParent(pair, record);
            if (pair.Count == 0) { regular = false; continue; }
            if (pair[0] is string key) index[key] = i;
        }
        state.Index = regular ? index : null;
        state.IndexChecked = true;
        return state.Index;
    }
    private static List<object> MutableItems(object value) => value is FrozenList
        ? throw new InvalidOperationException("Cannot mutate an evidence snapshot; copy it first")
        : Items(value);
    private static readonly object SnapshotLock = new();
    private static readonly Dictionary<ulong, List<WeakReference<FrozenList>>> SnapshotPool = new();
    private static readonly Queue<ulong> SnapshotBucketOrder = new();
    private static int SnapshotSweeps;
    public static object snapshot(object value)
    {
        lock (SnapshotLock)
        {
            var result = SnapshotValue(value);
            if (++SnapshotSweeps % 1024 == 0 && SnapshotBucketOrder.Count != 0)
            {
                var key = SnapshotBucketOrder.Dequeue();
                var bucket = SnapshotPool[key];
                bucket.RemoveAll(reference => !reference.TryGetTarget(out _));
                if (bucket.Count == 0) SnapshotPool.Remove(key);
                else SnapshotBucketOrder.Enqueue(key);
            }
            return result;
        }
    }
    private static object SnapshotValue(object value)
    {
        if (value is FrozenList || value is not List<object> items) return value;
        var state = MutableStates.GetOrCreateValue(items);
        if (state.Backbone is not null) return state.Backbone;
        var children = new object[items.Count];
        ulong hash = 14695981039346656037UL;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is List<object> mutableChild) LinkParent(mutableChild, items);
            var child = children[i] = SnapshotValue(items[i]);
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
        var frozen = new FrozenList(children.Length, hash);
        frozen.AddRange(children);
        bucket.Add(new WeakReference<FrozenList>(frozen));
        state.Backbone = frozen;
        return frozen;
    }
    private static object ReadItem(object owner, int index)
    {
        var items = Items(owner);
        var value = items[index];
        if (owner is not FrozenList && value is FrozenList frozen)
        {
            // Install the branch in this mutable owner only. Sibling copies retain the frozen branch.
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
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        MaxDepth = 512
    };
    public static object json_stringify(object value) => JsonSerializer.Serialize(value, JsonWriteOptions);
    public static object json_write(object path, object value)
    {
        var destination = Text(path);
        var partial = destination + ".partial";
        if (File.Exists(destination)) throw new IOException("Evidence destination already exists: " + destination);
        using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write))
            JsonSerializer.Serialize(stream, value, JsonWriteOptions);
        File.Move(partial, destination);
        return true;
    }
    public static object read(object p) => File.ReadAllText(Text(p), new UTF8Encoding(false, true));
    public static object write(object p, object s) { File.WriteAllText(Text(p), Text(s), new UTF8Encoding(false, true)); return s; }
    public static object print(object a) { Console.WriteLine(Text(show(a))); return a; }
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
