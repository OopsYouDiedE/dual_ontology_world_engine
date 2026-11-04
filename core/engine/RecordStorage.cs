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
