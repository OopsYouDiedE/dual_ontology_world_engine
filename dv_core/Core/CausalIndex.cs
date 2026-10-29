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
    private static readonly AsyncLocal<CausalLookup?> CausalLookupContext = new();
    public static void ConfigureCausalIndex(bool enabled) => CausalLookupContext.Value = enabled ? new CausalLookup() : null;
    private sealed class CausalLookup
    {
        internal object? Owner;
        internal Dictionary<string, object> Events = new(StringComparer.Ordinal);
        internal Dictionary<string, string[]> Parents = new(StringComparer.Ordinal);
        internal Dictionary<string, CausalClosure> Closures = new(StringComparer.Ordinal);
        internal Dictionary<string, int> FirstPositions = new(StringComparer.Ordinal);
        internal Queue<string> ClosureOrder = new();
        internal bool Bind(object events)
        {
            // Called only after the explicit checker switch was enabled.
            // snapshot reuses a valid mutable Backbone and invalidation rebuilds
            // it after edits; fresh DV prefixes acquire their own frozen identity.
            try { events = snapshot(events); }
            catch (Exception) { return false; }
            if (ReferenceEquals(Owner, events)) return true;
            // Unusual inputs retain the original DV error/equality path.
            if (events is not FrozenList || !TryItems(events, out var entries)) return false;
            var byId = new Dictionary<string, object>(StringComparer.Ordinal);
            var parents = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            var position = 0;
            try
            {
                foreach (var entry in entries)
                {
                    if (record_find(entry, "id", false) is not string id) return false;
                    var rawParents = record_find(entry, "parents", new List<object>());
                    if (!TryItems(rawParents, out var items)) return false;
                    var names = new string[items.Count];
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i] is not string name) return false;
                        names[i] = name;
                    }
                    positions.TryAdd(id, position++);
                    byId[id] = entry; parents[id] = names; // original last match
                }
            }
            catch (Exception) { return false; }
            Owner = events; Events = byId; Parents = parents; FirstPositions = positions;
            Closures.Clear(); ClosureOrder.Clear();
            return true;
        }
        internal sealed record CausalClosure(object Ordered, HashSet<string> Members);
        internal CausalClosure Closure(string child)
        {
            if (Closures.TryGetValue(child, out var cached)) return cached;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(); queue.Enqueue(child);
            var order = new List<object>();
            while (queue.Count != 0)
            {
                var id = queue.Dequeue();
                if (!visited.Add(id)) continue;
                order.Add(id);
                if (Parents.TryGetValue(id, out var parents))
                    foreach (var parent in parents) queue.Enqueue(parent);
            }
            var frozen = new CausalClosure(snapshot(order), visited);
            // At most eight complete closures, and one event-list index.
            if (ClosureOrder.Count == 8) Closures.Remove(ClosureOrder.Dequeue());
            ClosureOrder.Enqueue(child); Closures.Add(child, frozen);
            return frozen;
        }
    }
    private static object CausalResult(bool supported, object value) => new List<object> { supported, value };
    public static object causal_event_index(object events, object id)
    {
        var cache = CausalLookupContext.Value;
        if (cache is null || id is not string name || !cache.Bind(events)) return CausalResult(false, false);
        return CausalResult(true, cache.Events.TryGetValue(name, out var found) ? found : new List<object>());
    }
    public static object causal_ancestors_index(object events, object child)
    {
        var cache = CausalLookupContext.Value;
        if (cache is null || child is not string name || !cache.Bind(events)) return CausalResult(false, false);
        return CausalResult(true, cache.Closure(name).Ordered);
    }
    public static object causal_ancestor_index(object events, object child, object ancestor)
    {
        var cache = CausalLookupContext.Value;
        if (cache is null || child is not string name || ancestor is not string target || !cache.Bind(events)) return CausalResult(false, false);
        // Full BFS is finite for the admitted graph and preserves missing-ID
        // and self-membership semantics; no checks are removed by this lookup.
        return CausalResult(true, cache.Closure(name).Members.Contains(target));
    }

    public static object causal_prior_index(object events, object position, object id)
    {
        var cache = CausalLookupContext.Value;
        if (cache is null || id is not string name || !cache.Bind(events)) return CausalResult(false, false);
        return CausalResult(true, cache.FirstPositions.TryGetValue(name, out var first) && first < Index(position));
    }
}
