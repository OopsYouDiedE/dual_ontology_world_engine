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
                    return readOnly ? MemoReadResult(caches, BoolTrue, result)
                        : new List<object>(2) { BoolTrue, copy_value(result) };
            }
            else
            {
                // Retain the original validation on a new owner or partition,
                // without allocating an empty partition just for a miss.
                _ = SnapshotValue(key, caches);
                if (ownerMemo is not null) ArgumentNullException.ThrowIfNull(key);
            }
            return readOnly ? MemoReadResult(caches, BoolFalse, BoolFalse)
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
            memo.Values[frozenKey] = SnapshotValue(value, caches);
            return value;
        }
    }
}
