using DuelView.Compiled;
using System.Text.Json;

// Reference algorithms copied verbatim in behavior from Runtime before the two optimizations.
internal static class AbiDifferential
{
    private static object ReferenceHas(object items, object value)
    {
        var values = R.Items(items);
        for (var i = 0; i < values.Count; i++)
            if (R.Bool(R.same_value(values[i], value))) return true;
        return false;
    }
    private static object ReferenceUpdate(object record, object updates)
    {
        var changes = R.Items(updates);
        if (changes.Count == 0) return record;
        updates = R.snapshot(updates);
        changes = R.Items(updates);
        var frozen = R.Items(R.snapshot(record));
        var result = new List<object>(frozen.Count + changes.Count);
        var keys = new List<object>(frozen.Count + changes.Count);
        foreach (var item in frozen)
        {
            var key = R.Items(item)[0];
            if (Convert.ToDouble(R.record_index(updates, key)) >= 0)
                result.Add(new List<object> { key, R.copy_value(R.record_find(updates, key, false)) });
            else result.Add(item);
            keys.Add(key);
        }
        foreach (var item in changes)
        {
            var key = R.Items(item)[0];
            if (keys.Exists(existing => R.Bool(R.same_value(existing, key)))) continue;
            result.Add(new List<object> { key, R.copy_value(R.record_find(updates, key, false)) });
            keys.Add(key);
        }
        return result;
    }
    private static List<object> Pair(object key, object value) => new() { key, value };
    private static void Check(bool condition, string issue) { if (!condition) throw new Exception(issue); }
    private static string Describe(Func<object> operation)
    {
        try { return "value:" + R.Text(R.json_stringify(operation())); }
        catch (Exception error) { return "error:" + error.GetType().FullName + ":" + error.Message; }
    }
    internal static void Run(string output)
    {
        object[] lists = { new List<object>(), new List<object> { "a", "a", "A", "汉字", "e\u0301", "é" },
            new List<object> { "a", 1d, Rational.From("1/3"), new List<object> { "nested" } },
            new List<object> { Rational.From("1"), 2d }, new List<object> { Pair("k", "v"), "z" } };
        object[] queries = { "a", "A", "汉字", "e\u0301", "é", "absent", 1d, Rational.From("1"), Rational.From("1/3"), new List<object> { "nested" }, Pair("k", "v") };
        foreach (var list in lists)
        foreach (var view in new[] { list, R.snapshot(list), R.copy_value(R.snapshot(list)) })
        foreach (var query in queries)
            Check(Equals(ReferenceHas(view, query), R.has_value(view, query)), "Membership differs from structural reference");

        var source = new List<object> { "a", "b" };
        var snapshot = R.snapshot(source);
        R.has_value(source, "a");
        R.replace(source, 0d, "new");
        Check(!R.Bool(R.has_value(source, "a")) && R.Bool(R.has_value(source, "new")), "Mutable membership cache survived replace");
        Check(R.Bool(R.has_value(snapshot, "a")) && !R.Bool(R.has_value(snapshot, "new")), "Frozen membership changed with source");
        var copy = R.copy_value(snapshot);
        R.has_value(copy, "a");
        R.push(copy, "added");
        Check(R.Bool(R.has_value(copy, "added")) && !R.Bool(R.has_value(snapshot, "added")), "Copied membership cache survived append");

        object[] keys = { "a", "b", "A", "汉字", 1d, Rational.From("1"), Pair("nested", "key") };
        var random = new Random(20261018);
        for (var seed = 0; seed < 120; seed++)
        {
            var record = new List<object>(); var updates = new List<object>();
            var keyLimit = seed < 60 ? 4 : keys.Length;
            for (var i = 0; i < seed % 19; i++) record.Add(Pair(keys[random.Next(keyLimit)], Pair("old", (double)i)));
            for (var i = 0; i < seed % 13; i++) updates.Add(Pair(keys[random.Next(keyLimit)], Pair("new", (double)i)));
            var beforeRecord = R.Text(R.json_stringify(record)); var beforeUpdates = R.Text(R.json_stringify(updates));
            Check(Describe(() => ReferenceUpdate(R.copy_value(record), R.copy_value(updates))) == Describe(() => R.record_with_many(R.copy_value(record), R.copy_value(updates))), "Ordered record update differs from reference");
            Check(beforeRecord == R.Text(R.json_stringify(record)) && beforeUpdates == R.Text(R.json_stringify(updates)), "Update changed its input");
            if (updates.Count == 0) Check(ReferenceEquals(record, R.record_with_many(record, updates)), "Empty update lost identity");
        }
        foreach (var malformed in new object[] { new List<object> { "bad" }, new List<object> { new List<object>() } })
            Check(Describe(() => ReferenceUpdate(malformed, new List<object> { Pair("a", 1d) })) == Describe(() => R.record_with_many(malformed, new List<object> { Pair("a", 1d) })), "Malformed record changed first error");

        var nested = new List<object> { "before" };
        var original = new List<object> { Pair("retained", nested), Pair("updated", 0d) };
        var evidence = R.snapshot(original);
        var result = R.record_with_many(original, new List<object> { Pair("updated", 1d), Pair("appended", nested) });
        R.replace(R.record_find(result, "retained", false), 0d, "result-only");
        R.replace(R.record_find(result, "appended", false), 0d, "append-only");
        Check(R.Text(R.at(nested, 0d)) == "before" && R.Text(R.at(R.record_find(evidence, "retained", false), 0d)) == "before", "Nested output mutation changed source/evidence");
        File.WriteAllText(Path.Combine(output, "abi-differential.json"), JsonSerializer.Serialize(new {
            kind = "neutral-abi-reference-comparison", formal_session_executed = false,
            membership = "ordinal Unicode strings; mixed exact/floating numbers; nested records; frozen/mutable/copied inputs",
            updates = "ordered duplicate-key results and malformed-record first errors match retained reference",
            mutation = "source, evidence, and independently modified output branches retain expected values",
            fixture_random_seed = 20261018
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
