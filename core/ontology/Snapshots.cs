// @mechanism: dual_ontology_state
// @layer: core/ontology
// @contracts: Snapshots

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

// --- Source: Snapshots.cs ---
namespace DuelView.Compiled
{


// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
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
        // Large append-only snapshots may borrow a certified immutable prefix.
        // Depth is bounded at construction; the public DV/CLR List ABI stays intact.
        private readonly FrozenList? prefix;
        private readonly int prefixCount;
        private readonly int count;
        internal readonly int PrefixDepth;
        private static readonly List<object> InvalidIndexSource = [];
        internal FrozenList(object[] ownedChildren, ulong hash, ulong sequenceHash, FrozenList? prefix = null)
        {
            children = ownedChildren;
            this.prefix = prefix;
            prefixCount = prefix?.Count ?? 0;
            count = prefixCount + children.Length;
            PrefixDepth = prefix is null ? 0 : prefix.PrefixDepth + 1;
            Hash = hash;
            SequenceHash = sequenceHash;
        }
        // List copies the private array into its own backing store in one pass;
        // the immutable source remains private and cannot be mutated by its owner.
        internal List<object> CopyRoot(MutableStateTable? owner = null)
        {
            if (Count == 0) return owner is null ? new List<object>() : new OwnedList(0, owner);
            var extra = Count >= 64 ? 16 : 2;
            var capacity = Count <= int.MaxValue - extra ? Count + extra : Count;
            List<object> result = owner is null ? new List<object>(capacity) : new OwnedList(capacity, owner);
            AppendTo(result);
            return result;
        }
        private void AppendTo(List<object> target)
        {
            prefix?.AppendTo(target);
            target.AddRange(children);
        }
        internal void CopyPrefixTo(object[] destination)
        {
            prefix?.CopyPrefixTo(destination);
            Array.Copy(children, 0, destination, prefixCount, children.Length);
        }
        public int Count => count;
        internal ReadOnlySpan<object> SuffixValues => children;
        internal FrozenList? Prefix => prefix;
        public object this[int index]
        {
            get
            {
                if ((uint)index >= (uint)count) return InvalidIndexSource[index];
                var owner = this;
                while (owner.prefix is not null && index < owner.prefixCount) owner = owner.prefix;
                return owner.children[index - owner.prefixCount];
            }
        }
        public Enumerator GetEnumerator() => new(children, prefix is null ? null : this);
        IEnumerator<object> IEnumerable<object>.GetEnumerator() => Count == 0
            ? ((IEnumerable<object>)InvalidIndexSource).GetEnumerator() : GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Count == 0
            ? ((IEnumerable)InvalidIndexSource).GetEnumerator() : GetEnumerator();
        public struct Enumerator : IEnumerator<object>
        {
            private readonly object[] values;
            private readonly FrozenList? owner;
            private readonly int count;
            private int index;
            private object? current;
            internal Enumerator(object[] values, FrozenList? owner)
            { this.values = values; this.owner = owner; count = owner?.Count ?? values.Length; index = 0; current = null; }
            public object Current => current!;
            object IEnumerator.Current => index == 0 || index == count + 1
                ? throw new InvalidOperationException("Enumeration has either not started or has already finished.") : Current;
            public bool MoveNext()
            {
                if (index < count)
                {
                    current = owner is null ? values[index] : owner[index];
                    index++;
                    return true;
                }
                index = count + 1; current = null; return false;
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
    private static HashSet<string> FrozenStrings(FrozenList values)
    {
        if (values.StringsChecked) return values.Strings!;
        lock (SnapshotLock)
        {
            if (values.StringsChecked) return values.Strings!;
            var strings = new HashSet<string>(StringComparer.Ordinal);
            // Each immutable append node indexes only its own suffix. Reuse
            // the certified prefix instead of rebuilding growing log indexes.
            // A string query cannot match a nonstring atom or nested list.
            foreach (var value in values.SuffixValues)
            {
                if (value is string text) strings.Add(text);
            }
            values.Strings = strings;
            values.StringsChecked = true;
            return strings;
        }
    }
    private static bool FrozenHasString(FrozenList values, string name)
    {
        for (FrozenList? node = values; node is not null; node = node.Prefix)
            if (FrozenStrings(node).Contains(name)) return true;
        return false;
    }
    private sealed class MutableState
    {
        internal int RecordReads;
        internal bool StatePairShapeChecked, StatePairShape;
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
    // Runtime-owned roots retain the public List<object> ABI while keeping their
    // ownership metadata beside the list. External/plain lists and crossings
    // into another ValueContext still use that context's weak table.
    private sealed class OwnedList(int capacity, MutableStateTable owner) : List<object>(capacity)
    {
        internal readonly MutableStateTable Owner = owner;
        internal MutableState? State;
    }
    private sealed class MutableStateTable
    {
        private readonly ConditionalWeakTable<List<object>, MutableState> external = new();
        internal bool TryGetValue(List<object> list, out MutableState state)
        {
            if (list is OwnedList owned && ReferenceEquals(owned.Owner, this))
            { state = owned.State!; return state is not null; }
            return external.TryGetValue(list, out state!);
        }
        internal MutableState GetOrCreateValue(List<object> list)
        {
            if (list is OwnedList owned && ReferenceEquals(owned.Owner, this))
                return owned.State ??= new MutableState();
            return external.GetOrCreateValue(list);
        }
        internal void Add(List<object> list, MutableState state)
        {
            if (list is OwnedList owned && ReferenceEquals(owned.Owner, this))
            {
                if (owned.State is not null) throw new ArgumentException("Duplicate mutable owner");
                owned.State = state;
            }
            else external.Add(list, state);
        }
    }
    private static MutableStateTable MutableStates => Caches.MutableStates;
    private static List<object> Thaw(FrozenList frozen, List<object>? parent = null)
    {
        var caches = Caches;
        var result = frozen.CopyRoot(caches.RuntimeSpeedEnabled ? caches.MutableStates : null);
        var state = new MutableState { Backbone = frozen };
        if (parent is not null && caches.RuntimeSpeedEnabled) state.Parent = new(parent);
        caches.MutableStates.Add(result, state);
        if (parent is not null && !caches.RuntimeSpeedEnabled) LinkParent(result, parent, caches.MutableStates);
        return result;
    }
    private static void LinkParent(List<object> child, List<object> parent) => LinkParent(child, parent, MutableStates);
    private static void LinkParent(List<object> child, List<object> parent, MutableStateTable states)
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
            initial.StatePairShapeChecked = false;
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
                state.StatePairShapeChecked = false;
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
            var frozenPart = caches.RuntimeSpeedEnabled && valuePart is not List<object> ? valuePart : SnapshotValue(valuePart, caches);
            var pair = (FrozenList)(caches.RuntimeSpeedEnabled
                ? FrozenTextPairCore(key, frozenPart, caches) : FrozenTextPair(key, frozenPart));
            state.Backbone = pair;
            state.AppendBase = null;
            return pair;
        }
        MaintainSnapshots(caches);
        // Share at most four append segments, then flatten using the old path.
        // Only a prefix certified by push/Invalidate is eligible. Replacements
        // and nested writes clear AppendBase before reaching this operation.
        var append = state.AppendBase;
        var sharedPrefix = caches.AppendShareEnabled && append is not null
            && append.Count >= 1024 && append.Count < items.Count
            && items.Count - append.Count <= 64 && append.PrefixDepth < 4 ? append : null;
        var offset = sharedPrefix?.Count ?? 0;
        var children = new object[items.Count - offset];
        ulong hash = 14695981039346656037UL;
        ulong compatibleHash = hash;
        var compatible = true;
        var start = 0;
        if (state.AppendBase is { } prefix && prefix.Count >= 64 && prefix.Count <= items.Count)
        {
            if (sharedPrefix is null) prefix.CopyPrefixTo(children);
            start = prefix.Count;
            hash = prefix.SequenceHash;
            compatibleHash = prefix.CompatibleSequenceHash;
            compatible = prefix.MembershipHashEligible;
        }
        for (var i = start; i < items.Count; i++)
        {
            if (items[i] is List<object> mutableChild) LinkParent(mutableChild, items, caches.MutableStates);
            var item = items[i];
            var child = children[i - offset] = caches.RuntimeSpeedEnabled && item is not List<object>
                ? item : SnapshotValue(item, caches);
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
            if (existing.Count != items.Count) continue;
            var same = true;
            for (var j = 0; same && j < items.Count; j++)
            {
                var candidate = j < offset ? sharedPrefix![j] : children[j - offset];
                same = existing[j] is double left && candidate is double right
                    ? BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right)
                    : Equals(existing[j], candidate);
            }
            if (same) { state.Backbone = existing; state.AppendBase = null; return existing; }
        }
        var frozen = new FrozenList(children, hash, sequenceHash, sharedPrefix)
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
            value = Thaw(frozen, items);
            items[index] = value;
        }
        return value;
    }
}
}

// --- Source: Lifecycle.cs ---
namespace DuelView.Core
{


/// <summary>Session / Lifecycle: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dvs_session_open(object v_story, object v_capability)
    {
        _ = F_dv_require(R.eq(F_dv_get(v_story, "format"), "dv-session-v1"), "Unsupported story data dialect");
        object v_physical__286709 = F_dvs_simulation_open(v_story, F_dv_clone(F_dv_get(v_story, "physical")));
        object v_actors__286781 = F_dv_clone(F_dv_get(v_story, "actors"));
        object v_events__286821 = new System.Collections.Generic.List<object>(0) { };
        object v_bindings__286841 = F_dv_get(v_story, "bindings");
        object v_costs__286875 = F_dv_get(v_story, "cost_model");
        object v_run_start__286908 = F_dv_find(F_dv_get(v_story, "physical"), "time", R.number("0"));
        object v_duration_budget__286963 = F_dv_find(v_story, "game_duration_budget", R.number("0"));
        object v_horizon__287031 = R.number("0");
        object v_duration_blocked__287051 = false;
        object v_limit__287084 = F_dv_get(v_story, "max_actions");
        object v_cursor__287118 = R.number("0");
        object v_executed__287137 = R.number("0");
        object v_idle__287158 = R.number("0");
        object v_status__287175 = "action-window-exhausted";
        object v_initial__287218 = new System.Collections.Generic.List<object>(0) { };
        v_horizon__287031 = R.add(v_run_start__286908, v_duration_budget__286963);
        _ = F_dv_require(R.not(R.lt(v_duration_budget__286963, R.number("0"))), "Negative game duration budget");
        v_initial__287218 = new System.Collections.Generic.List<object>(2) { F_dv_pair("physical", v_physical__286709), F_dv_pair("actors", v_actors__286781) };
        object v_i__287415 = R.number("0");
        while (R.Bool(R.lt(v_i__287415, R.count(v_actors__286781))))
        {
            object v_actor__287467 = F_dvs_initialize_cognition(R.at(v_actors__286781, v_i__287415), F_dv_find(v_story, "opening_seed", R.number("0")), v_i__287415);
            if (R.Bool(F_dv_and(R.eq(v_capability, "disabled"), R.eq(F_dv_get(v_actor__287467, "id"), F_dv_find(v_story, "capability_actor", "")))))
            {
                v_actor__287467 = F_dv_with(v_actor__287467, "policy", F_dv_with(F_dv_get(v_actor__287467, "policy"), "model_budget", R.number("0")));
            }
            v_actors__286781 = F_dvs_replace(v_actors__286781, v_i__287415, F_dv_with(v_actor__287467, "ledger", F_dv_action_cost_ledger(F_dv_get(v_actor__287467, "id"), F_dv_get(v_actor__287467, "balance"))));
            v_i__287415 = R.add(v_i__287415, R.number("1"));
        }
        v_initial__287218 = new System.Collections.Generic.List<object>(2) { F_dv_pair("physical", v_physical__286709), F_dv_pair("actors", v_actors__286781) };
        _ = F_dvs_event(v_events__286821, "session_start", "", F_dv_find(v_physical__286709, "time", R.number("0")), new System.Collections.Generic.List<object>(0) { }, F_dv_with(new System.Collections.Generic.List<object>(3) { F_dv_pair("initial", v_initial__287218), F_dv_pair("capability", v_capability), F_dv_pair("source_refs", F_dv_find(v_story, "source_refs", new System.Collections.Generic.List<object>(0) { })) }, "time_origin", new System.Collections.Generic.List<object>(4) { F_dv_pair("run_start", v_run_start__286908), F_dv_pair("duration_budget", v_duration_budget__286963), F_dv_pair("horizon", v_horizon__287031), F_dv_pair("unit", "minute") }));
        return new System.Collections.Generic.List<object>(16) { F_dv_pair("story", v_story), F_dv_pair("physical", v_physical__286709), F_dv_pair("actors", v_actors__286781), F_dv_pair("events", v_events__286821), F_dv_pair("bindings", v_bindings__286841), F_dv_pair("costs", v_costs__286875), F_dv_pair("run_start", v_run_start__286908), F_dv_pair("duration_budget", v_duration_budget__286963), F_dv_pair("horizon", v_horizon__287031), F_dv_pair("duration_blocked", v_duration_blocked__287051), F_dv_pair("limit", v_limit__287084), F_dv_pair("cursor", v_cursor__287118), F_dv_pair("executed", v_executed__287137), F_dv_pair("idle", v_idle__287158), F_dv_pair("status", v_status__287175), F_dv_pair("initial", v_initial__287218) };
    }

    public static object F_dvs_session_active(object v_state)
    {
        if (R.Items(F_dv_get(v_state, "actors")).Any(actor => F_dv_find(actor, "in_flight_action", false) is not false))
            return true;
        object v_time__288623 = F_dv_find(F_dv_get(v_state, "physical"), "time", R.number("0"));
        if (R.Bool((R.Bool(R.lt(F_dv_get(v_state, "horizon"), v_time__288623)) ? (object)(true) : (object)(R.not(R.lt(F_dv_get(v_state, "executed"), F_dv_get(v_state, "limit")))))))
        {
            return false;
        }
        if (R.Bool(R.eq(v_time__288623, F_dv_get(v_state, "horizon"))))
        {
            if (R.Bool(R.not(R.eq(F_dv_find(v_state, "pending", false), false))))
            {
                return true;
            }
            object v_i__289016 = R.number("0");
            while (R.Bool(R.lt(v_i__289016, R.count(F_dv_get(v_state, "actors")))))
            {
                object v_queued__289080 = F_dv_find(R.at(F_dv_get(v_state, "actors"), v_i__289016), "scheduled_action", false);
                if (R.Bool(R.not(R.eq(v_queued__289080, false))))
                {
                    if (R.Bool(R.not(R.lt(v_time__288623, F_dv_get(v_queued__289080, "due")))))
                    {
                        return true;
                    }
                }
                v_i__289016 = R.add(v_i__289016, R.number("1"));
            }
            return false;
        }
        if (R.Bool(F_dv_has_key(F_dv_get(v_state, "physical"), "simulation")))
        {
            return true;
        }
        return (R.Bool(R.lt(F_dv_get(v_state, "idle"), R.count(F_dv_get(v_state, "actors")))) ? (object)(true) : (object)(R.lt(R.number("0"), F_dvs_scheduled_count(F_dv_get(v_state, "actors")))));
    }

    public static object F_dvs_session_status(object v_state)
    {
        object v_status__345400 = F_dv_get(v_state, "status");
        if (R.Bool(R.not(R.lt(F_dv_get(v_state, "idle"), R.count(F_dv_get(v_state, "actors"))))))
        {
            v_status__345400 = "no-admissible-action-or-budget";
            if (R.Bool(F_dv_get(v_state, "duration_blocked")))
            {
                v_status__345400 = "budget-exhausted";
            }
        }
        if (R.Bool(R.not(R.lt(F_dv_get(F_dv_get(v_state, "physical"), "time"), F_dv_get(v_state, "horizon")))))
        {
            v_status__345400 = "budget-exhausted";
        }
        if (R.Bool(F_dvs_session_active(v_state)))
        {
            v_status__345400 = "active";
        }
        if (R.Bool(R.lt(R.number("0"), R.count(F_dv_get(v_state, "events")))))
        {
            object v_last__345780 = R.at(F_dv_get(v_state, "events"), R.sub(R.count(F_dv_get(v_state, "events")), R.number("1")));
            if (R.Bool(R.eq(F_dv_get(v_last__345780, "type"), "session_stopped")))
            {
                v_status__345400 = F_dv_get(F_dv_get(v_last__345780, "data"), "reason");
            }
        }
        return v_status__345400;
    }

    public static object F_dvs_trace_snapshot(object v_state)
    {
        object v_story__345966 = F_dv_get(v_state, "story");
        object v_physical__345994 = F_dv_get(v_state, "physical");
        object v_actors__346028 = F_dv_get(v_state, "actors");
        object v_events__346058 = F_dv_get(v_state, "events");
        object v_costs__346088 = F_dv_get(v_state, "costs");
        object v_bindings__346116 = F_dv_get(v_state, "bindings");
        object v_initial__346150 = F_dv_get(v_state, "initial");
        object v_run_start__346182 = F_dv_get(v_state, "run_start");
        object v_duration_budget__346218 = F_dv_get(v_state, "duration_budget");
        object v_horizon__346266 = F_dv_get(v_state, "horizon");
        object v_status__346298 = F_dvs_session_status(v_state);
        return new System.Collections.Generic.List<object>(8) { F_dv_pair("events", v_events__346058), F_dv_pair("initial", v_initial__346150), F_dv_pair("final", new System.Collections.Generic.List<object>(2) { F_dv_pair("physical", v_physical__345994), F_dv_pair("actors", v_actors__346028) }), F_dv_pair("status", v_status__346298), F_dv_pair("time", F_dv_find(v_physical__345994, "time", R.number("0"))), F_dv_pair("game_time_unit", "minute"), F_dv_pair("configuration", new System.Collections.Generic.List<object>(6) { F_dv_pair("cost_model", v_costs__346088), F_dv_pair("formal_answers", F_dv_find(v_story__345966, "formal_answers", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("source_refs", F_dv_find(v_story__345966, "source_refs", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("time_origin", new System.Collections.Generic.List<object>(4) { F_dv_pair("run_start", v_run_start__346182), F_dv_pair("duration_budget", v_duration_budget__346218), F_dv_pair("horizon", v_horizon__346266), F_dv_pair("unit", "minute") }), F_dv_pair("report_labels", F_dv_find(v_story__345966, "report_labels", new System.Collections.Generic.List<object>(0) { })), F_dv_pair("bindings", v_bindings__346116) }), F_dv_pair("scope", F_dv_find(v_story__345966, "execution_scope", "DV formal model; no Godot host input or geometry")) };
    }

    public static object F_dvs_session_close(object v_state)
    {
        v_state = R.snapshot(v_state);
        object v_events__347020 = F_dv_clone(F_dv_get(v_state, "events"));
        if (R.Bool((R.Bool(R.eq(R.count(v_events__347020), R.number("0"))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(R.at(v_events__347020, R.sub(R.count(v_events__347020), R.number("1"))), "type"), "session_stopped"))))))
        {
            object v_parent__347152 = F_dv_get(R.at(v_events__347020, R.sub(R.count(v_events__347020), R.number("1"))), "id");
            object v_status__347205 = F_dvs_session_status(v_state);
            if (R.Bool(R.eq(v_status__347205, "active")))
            {
                v_status__347205 = "host-closed-during-active-session";
            }
            _ = F_dvs_event(v_events__347020, "session_stopped", "", F_dv_get(F_dv_get(v_state, "physical"), "time"), new System.Collections.Generic.List<object>(1) { v_parent__347152 }, new System.Collections.Generic.List<object>(2) { F_dv_pair("reason", v_status__347205), F_dv_pair("actions_executed", F_dv_get(v_state, "executed")) });
            _ = R.print(R.json_stringify(new System.Collections.Generic.List<object>(3) { F_dv_pair("type", "session_stopped"), F_dv_pair("status", v_status__347205), F_dv_pair("game_time", F_dv_get(F_dv_get(v_state, "physical"), "time")) }));
        }
        return F_dvs_trace_snapshot(F_dvs_owned_state_update(v_state, new System.Collections.Generic.List<object>(1) { F_dv_pair("events", v_events__347020) }));
    }

    public static object F_dvs_owned_state_update(object v_state, object v_updates)
    {
        object v_result__355095 = new System.Collections.Generic.List<object>(0) { };
        object v_keys__355115 = new System.Collections.Generic.List<object>(0) { };
        object v_i__355133 = R.number("0");
        while (R.Bool(R.lt(v_i__355133, R.count(v_state))))
        {
            object v_key__355182 = R.at(R.at(v_state, v_i__355133), R.number("0"));
            _ = R.push(v_result__355095, F_dv_pair(v_key__355182, F_dv_find(v_updates, v_key__355182, R.at(R.at(v_state, v_i__355133), R.number("1")))));
            _ = R.push(v_keys__355115, v_key__355182);
            v_i__355133 = R.add(v_i__355133, R.number("1"));
        }
        v_i__355133 = R.number("0");
        while (R.Bool(R.lt(v_i__355133, R.count(v_updates))))
        {
            object v_key__355374 = R.at(R.at(v_updates, v_i__355133), R.number("0"));
            if (R.Bool(R.not(F_dv_has(v_keys__355115, v_key__355374))))
            {
                _ = R.push(v_result__355095, F_dv_pair(v_key__355374, R.at(R.at(v_updates, v_i__355133), R.number("1"))));
                _ = R.push(v_keys__355115, v_key__355374);
            }
            v_i__355133 = R.add(v_i__355133, R.number("1"));
        }
        return v_result__355095;
    }
}
}
