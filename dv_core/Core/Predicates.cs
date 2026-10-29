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
    private sealed class DirtyBinding
    {
        internal string Facet = "", Field = "";
        internal readonly SortedSet<int> Rules = new();
        internal object Previous = PredicateMissingCell;
    }
    private sealed class DirtyObject
    {
        internal object Previous = PredicateMissingCell;
        internal readonly Dictionary<(string, string), DirtyBinding> Bindings = new();
    }
    private sealed class DirtyQueue
    {
        internal readonly Dictionary<string, DirtyObject> Objects = new(StringComparer.Ordinal);
        internal readonly SortedSet<int> Pending = new();
        internal readonly List<string> Laws = new();
        internal object Adoptions = PredicateMissingCell;
        internal int Count;
    }
    private sealed class DirtyQueues
    {
        internal readonly Dictionary<string, DirtyQueue> Subjects = new(StringComparer.Ordinal);
    }
    private static object DirtyLookup(object record, string key) => record_find(record, key, PredicateMissingCell);
    private static DirtyQueue DirtyBuild(FrozenList index)
    {
        var queue = new DirtyQueue();
        var entries = Items(index);
        queue.Count = entries.Count;
        for (var i = 0; i < entries.Count; i++)
        {
            queue.Pending.Add(i);
            queue.Laws.Add(Text(PredicateField(entries[i], "law")));
            foreach (var reference in Items(PredicateField(entries[i], "refs")))
            {
                var id = Text(PredicateField(reference, "object"));
                var facet = Text(PredicateField(reference, "facet"));
                var field = Text(PredicateField(reference, "field"));
                if (!queue.Objects.TryGetValue(id, out var entity)) queue.Objects.Add(id, entity = new DirtyObject());
                if (!entity.Bindings.TryGetValue((facet, field), out var binding))
                    entity.Bindings.Add((facet, field), binding = new DirtyBinding { Facet = facet, Field = field });
                binding.Rules.Add(i);
            }
        }
        return queue;
    }
    public static object dirty_next(object worldValue, object indexValue, object afterValue)
    {
        var world = (FrozenList)snapshot(worldValue);
        var index = (FrozenList)snapshot(indexValue);
        var after = checked((int)Num(afterValue));
        var subject = Text(PredicateField(world, "subject"));
        var holders = Caches.DirtyIndices.GetOrCreateValue(index);
        lock (holders)
        {
            if (!holders.Subjects.TryGetValue(subject, out var queue))
                holders.Subjects.Add(subject, queue = DirtyBuild(index));
            var objects = PredicateField(world, "objects");
            // Enumerate relevant objects, not rules. Unchanged immutable objects
            // stop traversal; a changed cell wakes every declared dependent rule.
            foreach (var pair in queue.Objects)
            {
                var entity = DirtyLookup(objects, pair.Key);
                var bindingObject = pair.Value;
                if (ReferenceEquals(entity, bindingObject.Previous)) continue;
                var existenceChanged = ReferenceEquals(entity, PredicateMissingCell) != ReferenceEquals(bindingObject.Previous, PredicateMissingCell);
                foreach (var binding in bindingObject.Bindings.Values)
                {
                    var cell = ReferenceEquals(entity, PredicateMissingCell) ? PredicateMissingCell
                        : PredicateReadCell(entity, binding.Facet, binding.Field);
                    if (existenceChanged || !ReferenceEquals(cell, binding.Previous))
                        queue.Pending.UnionWith(binding.Rules);
                    binding.Previous = cell;
                }
                bindingObject.Previous = entity;
            }
            var adoptions = PredicateField(world, "adoptions");
            if (!ReferenceEquals(adoptions, queue.Adoptions))
            {
                for (var i = 0; i < queue.Laws.Count; i++)
                    if (ReferenceEquals(queue.Adoptions, PredicateMissingCell)
                        || !ReferenceEquals(DirtyLookup(adoptions, queue.Laws[i]), DirtyLookup(queue.Adoptions, queue.Laws[i])))
                        queue.Pending.Add(i);
                queue.Adoptions = adoptions;
            }
            // Earlier dirty rules remain queued for the next original pass.
            // Later rules affected by this pass are inserted before its next step.
            var available = queue.Pending.GetViewBetween(after + 1, queue.Count);
            if (available.Count == 0) return NumberValue(queue.Count);
            var next = available.Min;
            queue.Pending.Remove(next);
            return NumberValue(next);
        }
    }

    private sealed class PredicateCellCache
    {
        internal readonly Dictionary<(string Facet, string Field), object> Cells = new();
    }
    private static readonly object PredicateMissingCell = new();
    private static object PredicateReadCell(object entity, object facet, object field)
    {
        var facets = PredicateField(entity, "facets");
        if (Num(record_index(facets, facet)) < 0) return PredicateMissingCell;
        var fields = record_find(facets, facet, "__dv_missing_field_sentinel__");
        if (Num(record_index(fields, field)) < 0) return PredicateMissingCell;
        return record_find(fields, field, "__dv_missing_field_sentinel__");
    }
    private static object PredicateReadCellCached(object entity, object facet, object field, ValueCaches caches)
    {
        // Non-text keys retain runtime EqValue/reference semantics unchanged.
        if (entity is not FrozenList frozen || facet is not string facetName || field is not string fieldName)
            return PredicateReadCell(entity, facet, field);
        var cache = caches.PredicateCells.GetOrCreateValue(frozen);
        lock (cache)
        {
            var key = (facetName, fieldName);
            if (cache.Cells.TryGetValue(key, out var cell)) return cell;
            // Store only successful reads or declared absence, never exceptions.
            cell = PredicateReadCell(frozen, facetName, fieldName);
            // Bound arbitrary missing-field probes; eviction changes work only.
            if (cache.Cells.Count >= 256) cache.Cells.Clear();
            cache.Cells.Add(key, cell);
            return cell;
        }
    }
    public static object lazyinternal_enabled() => Caches.LazyInternalEnabled ? BoolTrue : BoolFalse;
    public static object predicate_enabled() => Caches.PredicateEnabled ? BoolTrue : BoolFalse;
    private static object PredicateField(object record, string key) => record_find(record, key, "__dv_missing_field_sentinel__");
    // False delegates unsupported representations to the original DV path.
    // Borrow only immutable graphs. No mutable cell or world result is cached.
    public static object predicate_leaf(object world, object proposition)
    {
        // Do not substitute a mutable owner's backbone: reference-valued keys
        // and subjects must retain the original lookup/equality behavior.
        var frozenWorld = world as FrozenList;
        var frozenProposition = proposition as FrozenList;
        if (frozenWorld is null || frozenProposition is null) return BoolFalse;
        var op = PredicateField(frozenProposition, "op");
        if (!EqValue(op, "equals") && !EqValue(op, "at_least")) return BoolFalse;
        var reference = PredicateField(frozenProposition, "ref");
        // Access rejection precedes missing-object handling, as in dv_read.
        if (Num(record_index(reference, "subject")) >= 0)
        {
            var subject = PredicateField(reference, "subject");
            if (!EqValue(subject, "") && !EqValue(subject, PredicateField(frozenWorld, "subject")))
                throw new Exception("Cross-subject reference access");
        }
        var objects = PredicateField(frozenWorld, "objects");
        var id = PredicateField(reference, "object");
        if (Num(record_index(objects, id)) < 0) return "unknown";
        var entity = record_find(objects, id, "__dv_missing_field_sentinel__");
        // dv_read reads both ref fields to build its key before reading facets.
        var facet = PredicateField(reference, "facet");
        var field = PredicateField(reference, "field");
        // World/subject checks and both reference-field reads precede reuse.
        // Immutable object identity permits reuse across unrelated world updates.
        var caches = Caches;
        var cell = caches.PredicateCacheEnabled
            ? PredicateReadCellCached(entity, facet, field, caches)
            : PredicateReadCell(entity, facet, field);
        if (ReferenceEquals(cell, PredicateMissingCell)) return "unknown";
        // dv_or arguments are eager: missing conflicted errors even if unknown.
        // Its boolean type is checked only when known=true.
        if (caches.MaskEnabled && TryCellFlags(cell, out var flags))
        {
            if (flags != 1) return "unknown";
        }
        else
        {
            var known = Bool(PredicateField(cell, "known"));
            var conflicted = PredicateField(cell, "conflicted");
            if (!known || Bool(conflicted)) return "unknown";
        }
        var actual = PredicateField(cell, "value");
        var expected = PredicateField(frozenProposition, "value");
        if (EqValue(op, "equals")) return SameValue(actual, expected) ? "true" : "false";
        return Bool(lt(actual, expected)) ? "false" : "true";
    }
    public static object stateindex_enabled() => Caches.StateIndexEnabled ? BoolTrue : BoolFalse;
    public static object cow_enabled() => Caches.CowEnabled ? BoolTrue : BoolFalse;
    public static object mask_enabled() => Caches.MaskEnabled ? BoolTrue : BoolFalse;
    // Inspect record spines without exposing/thawing any child. Irregular ABI
    // inputs return to the existing DV implementation before any write occurs.
    private static bool TryRawField(object record, string key, out object value, out int slot)
    {
        value = false; slot = -1;
        if (!TryItems(record, out var entries)) return false;
        for (var i = 0; i < entries.Count; i++)
        {
            if (!TryItems(entries[i], out var pair) || pair.Count != 2 || pair[0] is not string name) return false;
            if (name == key) { value = pair[1]; slot = i; }
        }
        return true;
    }
    // Reuse the ordinary record slot index; the only extra metadata certifies
    // strict pair shape. Irregular/duplicate-key ABI values keep the DV path.
    private static bool TryStateField(object record, string key, out object value, out int slot,
        out Dictionary<string, int>? index)
    {
        value = false; slot = -1; index = null;
        if (!TryItems(record, out var entries)) return false;
        var frozen = UnchangedSnapshot(record, Caches);
        if (frozen is not null)
        {
            if (!Bool(record_like_cached(frozen))) return false;
            index = FrozenIndex(frozen);
        }
        else if (record is List<object> mutable)
        {
            var state = MutableStates.GetOrCreateValue(mutable);
            if (!state.StatePairShapeChecked)
            {
                state.StatePairShape = RecordLikeValue(mutable);
                state.StatePairShapeChecked = true;
            }
            if (!state.StatePairShape) return false;
            index = MutableIndex(mutable); // links mutable key pairs for invalidation
        }
        if (index is null || index.Count != entries.Count) return false;
        if (index.TryGetValue(key, out slot)) value = RawItem(entries[slot], 1);
        else slot = -1;
        return true;
    }
    private static object StageStoreIndexed(object owned, object reference, object cell)
    {
        if (!TryRawField(reference, "object", out var rawId, out var idSlot) || idSlot < 0 || rawId is not string id
            || !TryRawField(reference, "facet", out var rawFacet, out var facetSlot) || facetSlot < 0 || rawFacet is not string facet
            || !TryRawField(reference, "field", out var rawField, out var fieldSlot) || fieldSlot < 0 || rawField is not string field)
            return BoolFalse;
        if (!TryStateField(owned, "objects", out var objects, out var objectsSlot, out _) || objectsSlot < 0
            || !TryStateField(objects, id, out var entity, out var entitySlot, out var objectIndex) || entitySlot < 0
            || !TryStateField(entity, "facets", out var facets, out var facetsSlot, out _) || facetsSlot < 0
            || !TryStateField(facets, facet, out var fields, out var fieldsSlot, out _)) return BoolFalse;
        if (fieldsSlot < 0) fields = new List<object>();
        if (!TryStateField(fields, field, out _, out _, out _)) return BoolFalse;
        var updated = record_with(entity, "facets", record_with(facets, facet, record_with(fields, field, cell)));
        var mutableObjects = ReadItem(ReadItem(owned, objectsSlot), 1);
        // The selected key is unchanged and unique. Preserve the validated slots
        // after the value-only write; ancestor snapshots are still invalidated.
        replace(mutableObjects, NumberValue(entitySlot), FrozenTextPair(id, snapshot(updated)));
        var state = MutableStates.GetOrCreateValue(MutableItems(mutableObjects));
        state.Index = objectIndex;
        state.IndexChecked = true;
        state.StatePairShape = true;
        state.StatePairShapeChecked = true;
        return owned;
    }
    public static object stage_store_cow(object owned, object reference, object cell)
    {
        // Admission and law/effect order remain DV's responsibility. This fast
        // path substitutes only the private builder's record update operation.
        if (owned is not List<object>) return BoolFalse;
        if (Caches.StateIndexEnabled)
        {
            var indexed = StageStoreIndexed(owned, reference, cell);
            if (indexed is not false) return indexed;
        }
        if (!TryRawField(reference, "object", out var rawId, out var idSlot) || idSlot < 0 || rawId is not string id
            || !TryRawField(reference, "facet", out var rawFacet, out var facetSlot) || facetSlot < 0 || rawFacet is not string facet
            || !TryRawField(reference, "field", out var rawField, out var fieldSlot) || fieldSlot < 0 || rawField is not string field)
            return BoolFalse;
        if (!TryRawField(owned, "objects", out var objects, out var objectsSlot) || objectsSlot < 0
            || !TryRawField(objects, id, out var entity, out var entitySlot) || entitySlot < 0
            || !TryRawField(entity, "facets", out var facets, out var facetsSlot) || facetsSlot < 0
            || !TryRawField(facets, facet, out var fields, out var fieldsSlot)) return BoolFalse;
        if (fieldsSlot < 0) fields = new List<object>();
        if (!TryRawField(fields, field, out _, out _)) return BoolFalse;
        var updated = record_with(entity, "facets", record_with(facets, facet, record_with(fields, field, cell)));
        // Materialize just the owned world->objects spine. The old DV loop read
        // objects[i] through at(), thawing every unrelated object pair. Raw key
        // reads preserve those frozen siblings and still update every duplicate.
        var mutableObjects = ReadItem(ReadItem(owned, objectsSlot), 1);
        var entries = Items(mutableObjects);
        for (var i = 0; i < entries.Count; i++)
            if (Equals(RawItem(entries[i], 0), id))
                replace(mutableObjects, NumberValue(i), new List<object> { id, copy_value(updated) });
        return owned;
    }
    private sealed record CellFlags(byte Bits);
    private static bool TryCellFlags(object cell, out byte bits)
    {
        bits = 0;
        var caches = Caches;
        var frozen = UnchangedSnapshot(cell, caches);
        if (frozen is null) return false;
        if (!caches.CellMasks.TryGetValue(frozen, out var cached))
        {
            // Malformed cells retain the caller's exact original error order.
            var known = record_find(frozen, "known", MissingSentinel);
            if (known is not bool knownFlag) return false;
            var conflicted = record_find(frozen, "conflicted", MissingSentinel);
            if (conflicted is not bool conflictFlag) return false;
            var value = new CellFlags((byte)((knownFlag ? 1 : 0) | (conflictFlag ? 2 : 0)));
            cached = caches.CellMasks.GetValue(frozen, _ => value);
        }
        bits = cached.Bits;
        if (caches.MaskVerify)
        {
            var known = Bool(PredicateField(frozen, "known"));
            var conflicted = Bool(PredicateField(frozen, "conflicted"));
            var encoded = (byte)((known ? 1 : 0) | (conflicted ? 2 : 0));
            if (bits != encoded || known != ((bits & 1) != 0) || conflicted != ((bits & 2) != 0) || (bits & ~3) != 0)
                throw new InvalidDataException("Cell mask/record bidirectional mismatch: known/conflicted");
        }
        return true;
    }
    public static object cell_uncontested(object cell)
    {
        if (Caches.MaskEnabled && TryCellFlags(cell, out var flags)) return flags == 1 ? BoolTrue : BoolFalse;
        // dv_and receives known before eagerly evaluating not(conflicted).
        var known = PredicateField(cell, "known");
        var conflicted = Bool(PredicateField(cell, "conflicted"));
        return Bool(known) && !conflicted ? BoolTrue : BoolFalse;
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
            if (frozen is not null && values.Count >= 8) return FrozenHasString(frozen, name);
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
}
