using DuelView.Compiled;
using System.Text;
using System.Text.Json;

// Neutral ABI/persistence probe. No generated game or DV decision code is linked.
internal static class Probe
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool Refused(Action operation)
    {
        try { operation(); return false; }
        catch (IOException) { return true; }
    }

    public static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Supply a new evidence directory");
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new IOException("Probe output already exists: " + output);
        Directory.CreateDirectory(output);
        AbiDifferential.Run(output);

        var target = Path.Combine(output, "trace.json");
        var payload = new List<object> { Rational.From("1/3"), "neutral", true };
        R.json_write(target, payload);
        var original = File.ReadAllBytes(target);
        Require(Refused(() => R.json_write(target, new List<object> { "replacement" })), "Existing trace was not refused");
        Require(original.SequenceEqual(File.ReadAllBytes(target)), "Existing trace bytes changed");
        Require(!File.Exists(target + ".partial"), "Refused final target created a partial");

        var interrupted = Path.Combine(output, "interrupted.json");
        var partial = Encoding.UTF8.GetBytes("sealed-partial-neutral");
        File.WriteAllBytes(interrupted + ".partial", partial);
        Require(Refused(() => R.json_write(interrupted, payload)), "Existing partial was not refused");
        Require(partial.SequenceEqual(File.ReadAllBytes(interrupted + ".partial")), "Partial evidence bytes changed");
        Require(!File.Exists(interrupted), "Existing partial was published as a complete trace");

        var leaf = "{\"$dv_rational\":\"1/3\"}";
        const int depth = 300;
        var nested = new string('[', depth) + leaf + new string(']', depth);
        var session = DvSession.Restore(nested);
        var text = session.Save();
        var savePath = Path.Combine(output, "neutral-save.json");
        session.WriteSave(savePath);
        Require(text == File.ReadAllText(savePath), "Save and WriteSave have different wire formats");
        Require(DvSession.Restore(text).Save() == text, "Deep save round trip changed format");
        using (var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 512 }))
        {
            var element = doc.RootElement;
            for (var i = 0; i < depth; i++) element = element[0];
            Require(element.GetProperty("$dv_rational").GetString() == "1/3", "Exact rational tag or value changed");
        }
        using (var doc = JsonDocument.Parse(File.ReadAllText(target)))
            Require(doc.RootElement[0].GetString() == "1/3", "Trace rational is not the original string format");

        // Retained evidence may share storage; ordinary copies must remain mutable and independent.
        var mutable = new List<object> { new List<object> { "before" } };
        var frozen = R.snapshot(mutable);
        var independent = R.copy_value(frozen);
        R.replace(R.at(independent, 0d), 0d, "after");
        Require(R.Text(R.at(R.at(frozen, 0d), 0d)) == "before", "Copy mutation changed frozen evidence");
        var mutationRefused = false;
        try { R.push(frozen, "forbidden"); }
        catch (InvalidOperationException) { mutationRefused = true; }
        Require(mutationRefused, "Frozen evidence accepted mutation");

        var report = new { kind = "neutral-engineering-probe", formal_session_executed = false,
            trace_existing_bytes_preserved = true, partial_existing_bytes_preserved = true,
            trace_rational_format = "string", save_rational_format = "$dv_rational object",
            nested_array_depth = depth, save_write_save_identical = true,
            restore_round_trip_identical = true, frozen_evidence_immutable = true };
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
        return 0;
    }
}

// Every decision-related facade is a trap. Only Restore/Save/WriteSave are exercised.
public static partial class DVProgram
{
    private static object Unavailable() => throw new InvalidOperationException("The neutral probe must not execute a session");
    private static object F_dvg_tracked_actors() => Unavailable();
    private static object F_dvg_chronological_trace(object state) => Unavailable();
    private static object F_dvg_run_campaign(object maxSteps) => Unavailable();
    private static object F_dvg_open() => Unavailable();
    private static object F_dvs_set_player(object state, object actor) => Unavailable();
    private static object F_dvs_select_player(object state, object id) => Unavailable();
    private static object F_dvg_snapshot(object state, object viewer) => Unavailable();
    private static object F_dvs_session_open(object scenario, object enabled) => Unavailable();
    private static object F_dvs_session_active(object state) => Unavailable();
    private static object F_dvg_prepare(object state) => Unavailable();
    private static object F_dvg_complete(object state, object receipt) => Unavailable();
    private static object F_dvg_intent(object state) => Unavailable();
    private static object F_dvg_presentation(object state, object viewer) => Unavailable();
    private static object F_dvs_session_close(object state) => Unavailable();
}
