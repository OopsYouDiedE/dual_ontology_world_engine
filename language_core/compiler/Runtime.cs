using System.Globalization;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;

namespace DuelView.Compiled;

// Platform/value ABI only. No reader, AST walker, evaluator or code generator.
public static class R
{
    public static string[] Arguments = [];
    public static string Text(object x) => x as string ?? throw new Exception("Expected Text");
    public static double Num(object x) => x is double n && double.IsFinite(n) ? n : throw new Exception("Expected finite Number");
    public static bool Bool(object x) => x is bool b ? b : throw new Exception("Expected Bool");
    public static List<object> Items(object x) => x as List<object> ?? throw new Exception("Expected List");
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
    // Persistent spine only: mirror dvs_replace's equality and selected reads.
    public static object list_with_index(object items, object index, object value)
    {
        var source = Items(items);
        var result = new List<object>(source.Count);
        for (var i = 0; i < source.Count; i++)
            result.Add(Bool(eq((double)i, index)) ? value : ReadItem(items, i));
        return result;
    }
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
                var entry = entries[i];
                var k = entry is List<object> pl && pl.Count > 0 ? pl[0] : Items(entry)[0];
                if (ReferenceEquals(k, name) || (k is string s && string.Equals(s, name, StringComparison.Ordinal)))
                    return (double)i;
            }
            return -1d;
        }
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            var k = entry is List<object> pl && pl.Count > 0 ? pl[0] : Items(entry)[0];
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
                        var pairItems = pair is List<object> pl ? pl : Items(pair);
                        var val = pairItems[1];
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
                var k = entry is List<object> pl && pl.Count > 0 ? pl[0] : Items(entry)[0];
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
                var k = entry is List<object> pl && pl.Count > 0 ? pl[0] : Items(entry)[0];
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
        var pItems = p is List<object> pl2 ? pl2 : Items(p);
        var v = pItems[1];
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
        if (a is not List<object> left || b is not List<object> right || left.Count != right.Count) return false;
        var leftBase = a as FrozenList;
        var rightBase = b as FrozenList;
        if (leftBase is null && MutableStates.TryGetValue(left, out var ls)) leftBase = ls.Backbone;
        if (rightBase is null && MutableStates.TryGetValue(right, out var rs)) rightBase = rs.Backbone;
        if (leftBase is not null && ReferenceEquals(leftBase, rightBase)) return true;
        // Different structural hashes cannot reject equality: exact and floating numbers compare by value.
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
            if (frozen is null && MutableStates.TryGetValue(values, out var state)) frozen = state.Backbone;
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
            if (frozen is null && MutableStates.TryGetValue(values, out var state)) frozen = state.Backbone;
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
        var result = new List<object>(entries.Count + 1);
        var frozen = Items(snapshot(record));
        var found = false;
        if (key is string name)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var k = entry is List<object> pl && pl.Count > 0 ? pl[0] : Items(entry)[0];
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
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var k = entry is List<object> pl && pl.Count > 0 ? pl[0] : Items(entry)[0];
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
        if (frozen.Count >= 8 && (changes.Count > 1 || frozen.Count >= 16)
            && TryStringRecordUpdate(frozen, changes, out var stringResult)) return stringResult;
        var result = new List<object>(frozen.Count + changes.Count);
        var keys = new List<object>(frozen.Count + changes.Count);
        HashSet<string>? stringKeys = new(StringComparer.Ordinal);
        foreach (var item in frozen)
        {
            var key = item is List<object> pl && pl.Count > 0 ? pl[0] : Items(item)[0];
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
            var itemPl = item is List<object> pl ? pl : Items(item);
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
    private static bool TryStringRecordUpdate(List<object> record, List<object> changes, out List<object> result)
    {
        result = null!;
        foreach (var item in record)
            if (item is not List<object> { Count: 2 } pair || pair[0] is not string) return false;
        foreach (var item in changes)
            if (item is not List<object> { Count: 2 } pair || pair[0] is not string) return false;
        var updates = new Dictionary<string, object>(changes.Count, StringComparer.Ordinal);
        foreach (List<object> pair in changes) updates[(string)pair[0]] = pair[1];
        var present = new HashSet<string>(StringComparer.Ordinal);
        result = new List<object>(record.Count + changes.Count);
        foreach (List<object> pair in record)
        {
            var key = (string)pair[0];
            result.Add(updates.TryGetValue(key, out var value)
                ? new List<object> { key, copy_value(value) } : pair);
            present.Add(key);
        }
        foreach (List<object> pair in changes)
        {
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
        if (depth <= 0 || value is not List<object> list) return false;
        hash = 14695981039346656037UL;
        foreach (var item in list)
        {
            if (!TryQueryMembershipHash(item, depth - 1, out var child)) return false;
            hash = unchecked((hash ^ child) * 1099511628211UL);
        }
        hash = unchecked((hash ^ (ulong)list.Count) * 1099511628211UL) ^ 5UL;
        return true;
    }
    private static bool TryMembershipListHash(List<object> values, out ulong hash)
    {
        hash = 14695981039346656037UL;
        foreach (var value in values)
        {
            if (!TryMembershipHash(value, out var child)) return false;
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
        if (!MutableStates.TryGetValue(list, out var initial)) return;
        // No ancestor can depend on this owner; invalidate only its own caches.
        if (initial.Parents.Count == 0)
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
    private sealed class JsonContext
    {
        public bool InEvents;
        public string? CurrentEventType;
    }
    // Serialization preserves the supplied DV value. Evidence projection belongs
    // to explicit DV reporting APIs, never to implicit field-name filtering.
    private static void WriteJsonFast(Utf8JsonWriter writer, object value, JsonContext? ctx = null)
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
            case List<object> list:
                if (list.Count == 2 && list[0] is string key)
                {
                    if (key is "history" or "simulations")
                    {
                        writer.WriteStartArray();
                        writer.WriteStringValue(key);
                        writer.WriteStartArray();
                        writer.WriteEndArray();
                        writer.WriteEndArray();
                        return;
                    }
                    if (ctx != null && ctx.InEvents && ctx.CurrentEventType != "inference" && key is "adoptions" or "source_adoptions")
                    {
                        writer.WriteStartArray();
                        writer.WriteStringValue(key);
                        writer.WriteStartArray();
                        writer.WriteEndArray();
                        writer.WriteEndArray();
                        return;
                    }
                    if (ctx != null && ctx.InEvents && (key is "world" or "simulation" or "predicted" or "trace" or "catalog" || (ctx.CurrentEventType == "model_argument_unavailable" && key is "competitors" or "factor_coverage")))
                    {
                        writer.WriteStartArray();
                        writer.WriteStringValue(key);
                        writer.WriteStartArray();
                        writer.WriteEndArray();
                        writer.WriteEndArray();
                        return;
                    }
                    if (key == "events" && list[1] is List<object> eventsList)
                    {
                        writer.WriteStartArray();
                        writer.WriteStringValue("events");
                        writer.WriteStartArray();
                        var eventCtx = new JsonContext { InEvents = true };
                        for (var i = 0; i < eventsList.Count; i++)
                        {
                            var evt = eventsList[i];
                            eventCtx.CurrentEventType = null;
                            if (evt is List<object> evtPairs)
                            {
                                for (var p = 0; p < evtPairs.Count; p++)
                                {
                                    if (evtPairs[p] is List<object> pair && pair.Count == 2 && pair[0] is "type" && pair[1] is string t)
                                    {
                                        eventCtx.CurrentEventType = t;
                                        break;
                                    }
                                }
                            }
                            WriteJsonFast(writer, evt, eventCtx);
                            if (writer.BytesPending >= 16 * 1024 * 1024)
                                writer.Flush();
                        }
                        writer.WriteEndArray();
                        writer.WriteEndArray();
                        return;
                    }
                }
                writer.WriteStartArray();
                for (var i = 0; i < list.Count; i++)
                {
                    WriteJsonFast(writer, list[i], ctx);
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
            WriteJsonFast(writer, value, new JsonContext());
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }
    private static readonly object OutputLock = new();
    private static readonly Stream StandardOutput = Console.OpenStandardOutput();
    private static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    // Same value encoding as json_stringify, without a whole-packet UTF-16 copy.
    public static object json_print(object value)
    {
        lock (OutputLock)
        {
            using var writer = new Utf8JsonWriter(StandardOutput, new JsonWriterOptions { SkipValidation = true });
            WriteJsonFast(writer, value, new JsonContext());
            writer.Flush();
            StandardOutput.Write(NewLineBytes);
            StandardOutput.Flush();
        }
        return true;
    }

    // Opt-in evidence I/O only. The disabled hook never traverses the supplied value.
    // Values are frozen before registration; neither recording nor references mutate them.
    private static RawDagWriter? RawEvidence;
    private static readonly object RawLock = new();
    public static object raw_open(object path)
    {
        lock (RawLock)
        {
            if (RawEvidence is not null) throw new InvalidOperationException("Raw recorder already opened; abort a failed recorder before reopening");
            RawEvidence = new RawDagWriter(Text(path));
            return true;
        }
    }
    public static object raw_event(object value)
    {
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
            if (RawEvidence is null || value is not List<object>) return false;
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
        private readonly ArrayBufferWriter<byte> frameBuffer = new();
        private long nextNode;
        private long sequence;
        private long flushedSequence;
        private long eventCount;
        private string previous = new('0', 64);
        private bool finished;
        private bool failed;

        internal RawDagWriter(string path)
        {
            destination = Path.GetFullPath(path);
            partial = destination + ".partial";
            if (File.Exists(destination)) throw new IOException("Evidence destination already exists: " + destination);
            stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024);
            try
            {
                Frame(writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("kind", "header");
                    writer.WriteString("format", "dv-raw-dag-v1");
                    writer.WriteString("value_encoding", "DV arrays; typed rational atoms; trace-json or dv-tagged recovery");
                    writer.WriteString("checksum", "sha256(previous-ascii + LF + sequence-ascii + LF + payload-utf8)");
                    writer.WriteString("scope", "lossless frozen values; final checkpoint required for complete sealing");
                    writer.WriteEndObject();
                });
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
        private long Frame(Action<Utf8JsonWriter> payload)
        {
            RequireOpen();
            try
            {
                // A frame contains one shallow node or commit, never an expanded DAG.
                frameBuffer.Clear();
                using (var writer = new Utf8JsonWriter(frameBuffer)) { payload(writer); writer.Flush(); }
                var bytes = frameBuffer.WrittenSpan;
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                digest.AppendData(Encoding.ASCII.GetBytes(previous + "\n" + sequence.ToString(CultureInfo.InvariantCulture) + "\n"));
                digest.AppendData(bytes);
                var checksum = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("seq", sequence);
                    writer.WriteString("prev", previous);
                    writer.WritePropertyName("payload");
                    writer.WriteRawValue(bytes, skipInputValidation: true);
                    writer.WriteString("sha256", checksum);
                    writer.WriteEndObject();
                    writer.Flush();
                }
                stream.WriteByte((byte)'\n');
                previous = checksum;
                return sequence++;
            }
            catch { failed = true; throw; }
        }
        private RawNode Node(object value)
        {
            RequireOpen();
            if (value is not List<object>) throw new ArgumentException("Raw DAG roots must be DV lists/records");
            var frozen = (FrozenList)snapshot(value);
            if (nodes.TryGetValue(frozen, out var known)) return known;
            var children = new RawNode?[frozen.Count];
            for (var i = 0; i < frozen.Count; i++)
                if (frozen[i] is List<object>) children[i] = Node(frozen[i]);
            var id = nextNode++;
            var frame = Frame(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("kind", "node");
                writer.WriteNumber("node_id", id);
                writer.WriteStartArray("items");
                for (var i = 0; i < frozen.Count; i++)
                {
                    if (children[i] is { } child)
                    {
                        writer.WriteStartObject();
                        writer.WriteNumber("node", child.Id);
                        writer.WriteEndObject();
                    }
                    else if (frozen[i] is Rational exact)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("rational", exact.ToString());
                        writer.WriteEndObject();
                    }
                    else WriteJsonFast(writer, frozen[i]);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            });
            var result = new RawNode(id, frame);
            nodes.Add(frozen, result);
            return result;
        }
        internal void Event(object value)
        {
            RequireOpen();
            var frozen = snapshot(value);
            var id = Text(record_find(frozen, "id", false));
            if (!eventIds.Add(id))
            {
                failed = true;
                throw new InvalidOperationException("Repeated raw event ID: " + id);
            }
            var node = Node(frozen);
            Frame(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("kind", "event");
                writer.WriteString("event_id", id);
                writer.WriteNumber("root", node.Id);
                writer.WriteNumber("ordinal", eventCount);
                writer.WriteEndObject();
            });
            // Commit is last, and visible before the caller publishes the event.
            FlushCommit();
            eventCount++;
        }
        internal void Checkpoint(string name, object value)
        {
            var node = Node(value);
            Frame(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("kind", "checkpoint");
                writer.WriteString("name", name);
                writer.WriteNumber("root", node.Id);
                writer.WriteNumber("events", eventCount);
                writer.WriteEndObject();
            });
            FlushCommit();
        }
        internal object Reference(object value)
        {
            var node = Node(value);
            FlushCommit();
            return snapshot(new List<object>
            {
                new List<object> { "format", "dv-raw-dag-v1" },
                new List<object> { "journal", Path.GetFileName(destination) },
                new List<object> { "partial_journal", Path.GetFileName(partial) },
                new List<object> { "node_id", node.Id.ToString(CultureInfo.InvariantCulture) },
                new List<object> { "definition_frame", node.Frame.ToString(CultureInfo.InvariantCulture) }
            });
        }
        internal void Finish(object value)
        {
            Checkpoint("final", value);
            Frame(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("kind", "seal");
                writer.WriteNumber("events", eventCount);
                writer.WriteNumber("nodes", nextNode);
                writer.WriteBoolean("final_checkpoint", true);
                writer.WriteEndObject();
            });
            try
            {
                FlushCommit();
                stream.Dispose();
                // Neither incomplete frames nor failed I/O publish the completed filename.
                File.Move(partial, destination);
                finished = true;
            }
            catch { failed = true; throw; }
        }
        internal void Abort()
        {
            failed = true;
            stream.Dispose();
        }
    }
    public static object json_write(object path, object value)
    {
        var destination = Text(path);
        var partial = destination + ".partial";
        if (File.Exists(destination)) throw new IOException("Evidence destination already exists: " + destination);
        using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024 * 1024))
        {
            // Optional lossless storage encoding; the JSON value is unchanged.
            // Dispose compression before publishing the final filename.
            using var compressed = destination.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true) : null;
            using var writer = new Utf8JsonWriter(compressed is null ? stream : compressed, new JsonWriterOptions { SkipValidation = true });
            WriteJsonFast(writer, value);
            writer.Flush();
        }
        File.Move(partial, destination);
        return true;
    }
    public static object read(object p) => File.ReadAllText(Text(p), new UTF8Encoding(false, true));
    public static object write(object p, object s) { File.WriteAllText(Text(p), Text(s), new UTF8Encoding(false, true)); return s; }
    public static object print(object a)
    {
        lock (OutputLock) { Console.WriteLine(Text(show(a))); Console.Out.Flush(); }
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
