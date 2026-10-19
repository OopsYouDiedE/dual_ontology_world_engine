using DuelView.Compiled;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using System.Text.Json;

static class Probe
{
    static List<object> L(params object[] values) => new(values);
    static List<object> P(object key, object value) => L(key, value);
    static readonly SortedDictionary<string, object?> Observations = new(StringComparer.Ordinal);
    static string Json(object value) => R.Text(R.json_stringify(value));
    static string Outcome(Func<object?> action)
    {
        try { var value = action(); return value is null ? "null" : "value:" + Json(value); }
        catch (Exception ex) { return "error:" + ex.GetType().FullName + ":" + ex.Message; }
    }
    static void Capture(string name, Func<object?> action) => Observations.Add(name, Outcome(action));
    static void Need(bool condition, string message) { if (!condition) throw new Exception(message); }
    static object Tree() => L(P("plain", 2d), P("exact", R.exact("1/3")), P("nested", L(true, "neutral", R.exact("9007199254740993"))), P("empty", L()), P("duplicate", "first"), P("duplicate", "last"));
    static void Enumerate(string name, object value)
    {
        var generic = ((IEnumerable<object>)value).GetEnumerator();
        Capture(name + ".generic.before", () => generic.Current);
        var seen = new List<object>();
        while (generic.MoveNext()) seen.Add(generic.Current);
        Capture(name + ".values", () => seen);
        Capture(name + ".generic.after", () => generic.Current);
        generic.Dispose();
        Capture(name + ".generic.disposed-move", () => generic.MoveNext());
        var nonGeneric = ((IEnumerable)value).GetEnumerator();
        Capture(name + ".nongeneric.before", () => nonGeneric.Current);
        while (nonGeneric.MoveNext()) { }
        Capture(name + ".nongeneric.after", () => nonGeneric.Current);
        nonGeneric.Reset();
        Capture(name + ".nongeneric.reset-before", () => nonGeneric.Current);
        Capture(name + ".nongeneric.reset-first", () => nonGeneric.MoveNext() ? nonGeneric.Current : "empty");
    }
    static string SemanticDigest(IEnumerable<object> roots)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var root in roots)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Json(root)));
            hash.AppendData(new byte[] { 10 });
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    static void Performance(string output)
    {
        // Each performance invocation is a fresh process with identical input.
        // Keep every returned frozen root and copy alive across both workloads.
        var warmRoots = new List<object>();
        for (var n = 0; n < 32; n++) warmRoots.Add(R.snapshot(L(P("warm", "warm-" + n), P("branch", L("warm-child-" + n)))));
        object[] Inputs(string prefix, int count, int fields) => Enumerable.Range(0, count).Select(n => (object)Enumerable.Range(0, fields).Select(k => (object)P("k" + k, L(prefix + ":" + n + ":" + k, "payload", true))).ToList()).ToArray();
        var smallInputs = Inputs("small", 2500, 3);
        var largeInputs = Inputs("large", 256, 128);
        var smallRoots = new List<object>(smallInputs.Length);
        var largeRoots = new List<object>(largeInputs.Length);
        var copies = new List<object>(1500);
        var updates = new List<object>(1500);
        var replacementTags = Enumerable.Range(0, 1500).Select(n => "replacement-" + n).ToArray();
        var metrics = new List<object>();
        void Measure(string name, Action action)
        {
            var watch = new Stopwatch();
            var collections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            watch.Start(); action(); watch.Stop();
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            metrics.Add(new { name, elapsed_ticks = watch.ElapsedTicks, elapsed_milliseconds = watch.Elapsed.TotalMilliseconds, allocated_bytes = bytes, gen0_collections = GC.CollectionCount(0) - collections[0], gen1_collections = GC.CollectionCount(1) - collections[1], gen2_collections = GC.CollectionCount(2) - collections[2] });
        }
        Measure("freeze-unique-small", () => { foreach (var input in smallInputs) smallRoots.Add(R.snapshot(input)); });
        Measure("freeze-unique-large", () => { foreach (var input in largeInputs) largeRoots.Add(R.snapshot(input)); });
        Measure("independent-copy-read-sparse-update", () =>
        {
            for (var n = 0; n < 1500; n++)
            {
                var copy = R.copy_value(largeRoots[n % largeRoots.Count]);
                var branch = R.record_find(copy, "k3", false);
                R.at(branch, 0d);
                R.push(branch, replacementTags[n]);
                copies.Add(copy);
                updates.Add(R.record_with(copy, "k4", replacementTags[n]));
            }
        });
        // No hashing/JSON work is charged to the timed container operations.
        var semantics = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["small-roots"] = SemanticDigest(smallRoots),
            ["large-roots-after-copy"] = SemanticDigest(largeRoots),
            ["copy-branches"] = SemanticDigest(copies),
            ["sparse-updates"] = SemanticDigest(updates)
        };
        Need(Json(largeRoots[0]) == Json(largeInputs[0]), "performance sample mutated frozen original");
        Need(Json(copies[0]) != Json(largeRoots[0]), "performance copy did not change");
        File.WriteAllText(Path.Combine(output, "performance-semantics.json"), JsonSerializer.Serialize(semantics, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "performance-metrics.json"), JsonSerializer.Serialize(new { scope = "finite neutral container sample; not game performance", small_trees = smallInputs.Length, small_fields = 3, large_trees = largeInputs.Length, large_fields = 128, independent_copies = copies.Count, sparse_updates = updates.Count, stopwatch_frequency = Stopwatch.Frequency, metrics }, new JsonSerializerOptions { WriteIndented = true }));
        GC.KeepAlive(warmRoots); GC.KeepAlive(smallInputs); GC.KeepAlive(largeInputs);
        GC.KeepAlive(smallRoots); GC.KeepAlive(largeRoots); GC.KeepAlive(copies); GC.KeepAlive(updates);
    }
    static void Layout(string output)
    {
        var results = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var kept = new List<object>();
        object Record(string[] keys, string value)
        {
            var root = R.snapshot(keys.Select((key, slot) => (object)P(key, value + ":" + slot)).ToList());
            kept.Add(root); return root;
        }
        void Check(string label, object root, object key)
        {
            results[label + ".index"] = Outcome(() => R.record_index(root, key));
            results[label + ".find"] = Outcome(() => R.record_find(root, key, "missing"));
        }
        void PoolBounds()
        {
            var count = typeof(R).GetField("RecordLayoutCount", BindingFlags.NonPublic | BindingFlags.Static);
            var chars = typeof(R).GetField("RecordLayoutCharacters", BindingFlags.NonPublic | BindingFlags.Static);
            if (count is not null && chars is not null)
                Need((int)count.GetValue(null)! <= 128 && (long)chars.GetValue(null)! <= 65536, "layout admission bounds");
        }
        var keys8 = Enumerable.Range(0, 8).Select(i => "field" + i).ToArray();
        var first = Record(keys8, "first");
        Check("same.first", first, "field3"); Check("same.second", Record(keys8, "second"), "field3");
        var duplicateKeys = new[] { "a", "b", "a", "c", "d", "e", "f", "g" };
        var duplicate = Record(duplicateKeys, "duplicate");
        Check("duplicate", duplicate, "a");
        Check("duplicate.reordered", Record(new[] { "a", "a", "b", "c", "d", "e", "f", "g" }, "reordered"), "a");
        results["duplicate.with-first"] = Outcome(() => R.record_with(duplicate, "a", "replacement"));
        results["duplicate.many"] = Outcome(() => R.record_with_many(duplicate, L(P("a", "u1"), P("a", "u2"))));
        var shortPairs = R.snapshot(keys8.Select(key => (object)L(key)).ToList()); kept.Add(shortPairs);
        Check("one-slot-pair", shortPairs, "field3");
        var longPairs = R.snapshot(keys8.Select(key => (object)L(key, "long-value", "extra")).ToList()); kept.Add(longPairs);
        Check("three-slot-pair", longPairs, "field3");
        var mixed = keys8.Select((key, i) => (object)P(i == 3 ? (object)3d : key, "mixed" + i)).ToList();
        Check("mixed.text", R.snapshot(mixed), "field6"); Check("mixed.numeric", R.snapshot(mixed), 3d);
        foreach (var position in new[] { 0, 7 }) foreach (var bad in new object[] { L(), "not-pair", new object[] { "array-key", "value" } })
        {
            var broken = keys8.Select(key => (object)P(key, "value")).ToList(); broken[position] = bad;
            // External arrays remain atoms in snapshot and must not become pairs.
            var root = R.snapshot(broken); kept.Add(root);
            var label = "bad." + position + "." + (bad is string ? "atom" : bad is object[] ? "array" : "empty");
            Check(label, root, position == 0 ? "field7" : "field0");
            results[label + ".with"] = Outcome(() => R.record_with(root, "field0", "new"));
        }
        for (var round = 0; round < 2; round++) for (var shape = 0; shape < 129; shape++)
        {
            var root = Record(Enumerable.Range(0, 8).Select(i => "shape-" + shape + ":" + i).ToArray(), "round-" + round);
            Check("churn." + round + "." + shape, root, "shape-" + shape + ":7"); PoolBounds();
        }
        Check("old-after-eviction", first, "field3");
        foreach (var fields in new[] { 128, 129 })
        {
            var keys = Enumerable.Range(0, fields).Select(i => "wide" + i).ToArray();
            Check("width." + fields, Record(keys, "wide"), "wide" + (fields - 1));
        }
        foreach (var length in new[] { 512, 513 })
        {
            var keys = Enumerable.Range(0, 8).Select(i => new string('界', length - 1) + i).ToArray();
            Check("unicode." + length, Record(keys, "unicode"), keys[7]); PoolBounds();
        }
        for (var shape = 0; shape < 18; shape++)
        {
            var keys = Enumerable.Range(0, 8).Select(i => new string('文', 506) + shape.ToString("D3", CultureInfo.InvariantCulture) + i.ToString("D3", CultureInfo.InvariantCulture)).ToArray();
            Check("character-pool." + shape, Record(keys, "characters"), keys[7]); PoolBounds();
        }
        var ordinal = new[] { "", "A", "a", "é", "e\u0301", "😀", "終", "A" };
        var ordinalCopy = ordinal.Select(key => new string(key.ToCharArray())).ToArray();
        Check("ordinal-original", Record(ordinal, "ord1"), "A"); Check("ordinal-equal-distinct-ref", Record(ordinalCopy, "ord2"), "A");
        Check("ordinal-combining", Record(ordinal, "ord3"), "e\u0301");
        var copy = R.copy_value(first); var sibling = R.copy_value(first);
        R.replace(R.at(copy, 3d), 0d, "changed-key");
        Check("alias.changed-old", copy, "field3"); Check("alias.changed-new", copy, "changed-key");
        Check("alias.sibling", sibling, "field3"); Check("alias.original", first, "field3");
        var metrics = new List<object>();
        void Measure(string label, Action action)
        {
            var watch = new Stopwatch(); var allocated = GC.GetAllocatedBytesForCurrentThread();
            watch.Start(); action(); watch.Stop(); var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            metrics.Add(new { label, elapsed_ticks = watch.ElapsedTicks, elapsed_milliseconds = watch.Elapsed.TotalMilliseconds, allocated_bytes = bytes });
        }
        foreach (var width in new[] { 8, 128 })
        {
            var roots = new List<object>(); var keys = Enumerable.Range(0, width).Select(i => "sample" + i).ToArray();
            Measure("construct-freeze." + width, () => { for (var i = 0; i < 1000; i++) roots.Add(Record(keys, "sample-" + i)); });
            Measure("first-index." + width, () => { foreach (var root in roots) R.record_index(root, keys[width - 1]); });
            Measure("repeat-index." + width, () => { for (var repeat = 0; repeat < 10; repeat++) foreach (var root in roots) R.record_index(root, keys[width - 1]); });
            results["sample." + width] = SemanticDigest(roots.Select(root => R.record_find(root, keys[width - 1], false)));
        }
        results["final.original"] = Json(first);
        File.WriteAllText(Path.Combine(output, "layout-semantics.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "layout-metrics.json"), JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        GC.KeepAlive(kept); GC.KeepAlive(first); GC.KeepAlive(copy); GC.KeepAlive(sibling);
    }
    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        var output = args[0];
        if (args.Length > 1 && args[1] == "layout") { Layout(output); return; }
        if (args.Length > 1 && args[1] == "performance") { Performance(output); return; }
        var frozen = R.snapshot(Tree());
        Need(R.Bool(R.is_list(frozen)), "frozen is DV list");
        Need(!(frozen is object[]), "frozen backing array is private");
        Capture("frozen.show", () => R.show(frozen));
        Capture("frozen.count", () => R.count(frozen));
        foreach (var foreign in new object[] { new object[] { "x" }, new ReadOnlyCollection<object>(L("x")) })
        {
            var tag = foreign is object[] ? "array" : "read-only-collection";
            Capture(tag + ".is-list", () => R.is_list(foreign));
            Capture(tag + ".items", () => { R.Items(foreign); return true; });
            Capture(tag + ".count", () => R.count(foreign));
            Capture(tag + ".at", () => R.at(foreign, 0d));
            Capture(tag + ".push", () => R.push(foreign, "x"));
            Need(!R.Bool(R.is_list(foreign)), tag + " rejected");
        }
        Capture("frozen.push", () => R.push(frozen, "x"));
        Capture("frozen.replace-before-bad-index", () => R.replace(frozen, "invalid", "x"));
        foreach (var i in new object[] { -1d, 0.5d, 200d, "bad" })
        {
            Capture("frozen.at." + i, () => R.at(frozen, i));
            Capture("mutable.at." + i, () => R.at(Tree(), i));
        }
        Enumerate("enum.frozen", frozen);
        Enumerate("enum.empty-frozen", R.snapshot(L()));
        Enumerate("enum.mutable", Tree());
        var duplicate = L(P("k", "first"), P("other", "middle"), P("k", "last"));
        foreach (var tuple in new[] { ("small", duplicate), ("indexed", L(Enumerable.Range(0, 9).Select(i => (object)P("n" + i, (double)i)).Concat(duplicate).ToArray())) })
        {
            var record = R.snapshot(tuple.Item2);
            Capture(tuple.Item1 + ".find-last", () => R.record_find(record, "k", false));
            Capture(tuple.Item1 + ".index-last", () => R.record_index(record, "k"));
            Capture(tuple.Item1 + ".with-first", () => R.record_with(record, "k", "replacement"));
            Capture(tuple.Item1 + ".many-all", () => R.record_with_many(record, L(P("k", "u1"), P("new", 1d), P("k", "u2"), P("new", 2d))));
        }
        var badRecords = new[] { L("bad", P("k", "tail")), L(P("k", "head"), "bad"), L(L(), P("k", "tail")), L(P("k", "head"), L()), L(P(1d, "number"), L("k", "value", "extra")) };
        for (var n = 0; n < badRecords.Length; n++)
        {
            var record = R.snapshot(badRecords[n]);
            Capture("bad." + n + ".find", () => R.record_find(record, "k", false));
            Capture("bad." + n + ".missing", () => R.record_find(record, "absent", "__dv_missing_field_sentinel__"));
            Capture("bad." + n + ".with", () => R.record_with(record, "k", "new"));
            Capture("bad." + n + ".many", () => R.record_with_many(record, L(P("k", "new"))));
            Capture("bad." + n + ".empty-updates", () => ReferenceEquals(record, R.record_with_many(record, L())));
        }
        Capture("nontext.lookup", () => R.record_find(R.snapshot(badRecords[4]), 1d, false));
        var shared = L("shared"); var a = L(P("a", shared)); var b = L(P("b", shared));
        var oldA = R.snapshot(a); var oldB = R.snapshot(b);
        R.push(shared, "changed");
        Capture("aliases.invalidated-a", () => !R.Bool(R.same_value(R.snapshot(a), oldA)));
        Capture("aliases.invalidated-b", () => !R.Bool(R.same_value(R.snapshot(b), oldB)));
        Need(!R.Bool(R.same_value(R.snapshot(a), oldA)) && !R.Bool(R.same_value(R.snapshot(b), oldB)), "all parents invalidated");
        var copyA = R.copy_value(frozen); var copyB = R.copy_value(frozen);
        R.push(R.record_find(copyA, "nested", false), "private");
        Capture("copy.original", () => frozen); Capture("copy.sibling", () => copyB); Capture("copy.changed", () => copyA);
        Need(Json(frozen) == Json(copyB) && Json(copyA) != Json(copyB), "independent children");
        var indexed = Enumerable.Range(0, 12).Select(i => (object)P("k" + i, (double)i)).ToList();
        var oldIndexed = R.snapshot(indexed); R.record_index(indexed, "k5"); R.replace(R.at(indexed, 5d), 0d, "renamed");
        Capture("index.invalidated", () => R.record_index(indexed, "k5")); Capture("index.old-stable", () => R.record_index(oldIndexed, "k5"));
        var cycleA = L(); var cycleB = L(); cycleA.Add(cycleB); cycleB.Add(cycleA);
        for (var i = 1; i < 8; i++) { cycleA.Add(P("a" + i, 0d)); cycleB.Add(P("b" + i, 0d)); }
        R.record_index(cycleA, "absent"); R.record_index(cycleB, "absent"); R.replace(cycleA, 1d, P("changed", 1d));
        Capture("cycle.invalidation-completed", () => R.record_index(cycleA, "changed"));
        var memoOwner = L("owner"); var memoKey = L("partition", R.exact(1d)); R.memo_put(memoOwner, memoKey, L("memo-value"));
        var memoRead = R.at(R.memo_get(memoOwner, memoKey), 1d); R.push(memoRead, "private");
        Capture("memo.independent", () => R.memo_get(memoOwner, memoKey));
        Need(ReferenceEquals(frozen, R.snapshot(Tree())), "live canonical identity");
        Capture("exact.same", () => R.same_value(L(R.exact(1d), -0d), L(1d, 0d)));

        DVProgram.Neutral = frozen;
        var session = DvSession.OpenCampaign();
        File.WriteAllText(Path.Combine(output, "session-save.json"), session.Save());
        session.WriteSave(Path.Combine(output, "session-save-stream.json"));
        var restored = DvSession.Restore(session.Save());
        File.WriteAllText(Path.Combine(output, "session-restored-save.json"), restored.Save());
        File.WriteAllText(Path.Combine(output, "session-trace.json"), session.Trace());
        session.WriteTrace(Path.Combine(output, "session-trace-stream.json"));
        File.WriteAllText(Path.Combine(output, "session-host.json"), session.Snapshot("neutral"));
        Capture("session.active", () => session.Active);
        Capture("session.prepare", () => session.Prepare());
        Capture("session.restored-trace", () => restored.Trace());
        Capture("session.complete-parser", () => restored.Complete("{\"k\":{\"$dv_rational\":\"2/7\"}}", "neutral"));
        var saveJson = File.ReadAllText(Path.Combine(output, "session-save.json"));
        Need(saveJson.Contains("$dv_rational"), "native Save retains exact tags");
        Need(session.Trace().Contains("1/3") && !session.Trace().Contains("$dv_rational"), "Trace uses exact strings");
        Need(session.Save() == File.ReadAllText(Path.Combine(output, "session-save-stream.json")), "Save stream matches");
        Need(session.Save() == DvSession.Restore(session.Save()).Save(), "Save restore round trip");
        using (var trace = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "session-trace-stream.json"))))
            Need(trace.RootElement.ValueKind == JsonValueKind.Array, "stream trace is an array");
        R.raw_open(Path.Combine(output, "neutral.dag.jsonl")); R.raw_checkpoint("initial", frozen);
        for (var i = 0; i < 12; i++) R.raw_event(L(P("id", "neutral-" + i), P("payload", frozen), P("exact", R.exact("2/7"))));
        Capture("raw.reference", () => R.raw_ref(frozen)); R.raw_finish(L(P("final", frozen)));
        R.raw_open(Path.Combine(output, "failure.dag.jsonl")); var ev = L(P("id", "duplicate-id"), P("payload", frozen)); R.raw_event(ev);
        Capture("raw.duplicate", () => R.raw_event(ev)); R.raw_abort();
        File.WriteAllText(Path.Combine(output, "observations.json"), JsonSerializer.Serialize(Observations, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Neutral container and host boundary observations saved; no game session executed.");
    }
}
