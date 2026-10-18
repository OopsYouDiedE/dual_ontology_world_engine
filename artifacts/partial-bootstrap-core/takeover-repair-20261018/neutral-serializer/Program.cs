using DuelView.Compiled;
using System.Text.Json;

static List<object> Pair(string key, object value) => new() { key, value };
static List<object> Record(params object[] fields) => new(fields);
var payload = Record(
    Pair("history", new List<object> { "neutral-history" }),
    Pair("simulations", new List<object> { "neutral-simulation" }),
    Pair("events", new List<object> { Record(
        Pair("type", "model_argument_unavailable"),
        Pair("data", Record(Pair("world", Record(Pair("history", new List<object> { "nested-history" }))),
                            Pair("competitors", new List<object> { "neutral-competitor" }),
                            Pair("adoptions", new List<object> { "neutral-adoption" }),
                            Pair("source_adoptions", new List<object> { "neutral-source" })))) }),
    Pair("exact", R.div(R.exact(1d), R.exact(3d))));
var text = (string)R.json_stringify(payload);
using var parsed = JsonDocument.Parse(text);
var restored = R.json_parse(text);
if (!(bool)R.same_value(R.record_find(restored, "history", false), R.record_find(payload, "history", false)))
    throw new Exception("History was changed during serialization");
foreach (var marker in new[] { "neutral-simulation", "nested-history", "neutral-competitor", "neutral-adoption", "neutral-source", "1/3" })
    if (!text.Contains(marker, StringComparison.Ordinal)) throw new Exception("Missing serialized value: " + marker);
Console.WriteLine(text);
Console.WriteLine("Supplied history, simulations, nested model data, and exact rational text are preserved. No session was opened.");
var destination = Path.Combine(AppContext.BaseDirectory, "neutral-output.json.gz");
R.json_write(destination, payload);
using var compressed = new System.IO.Compression.GZipStream(File.OpenRead(destination), System.IO.Compression.CompressionMode.Decompress);
using var reader = new StreamReader(compressed);
if (reader.ReadToEnd() != text) throw new Exception("Compressed trace changed JSON bytes");
Console.WriteLine("Gzip expands to the identical JSON bytes; final name published after compression closed.");
