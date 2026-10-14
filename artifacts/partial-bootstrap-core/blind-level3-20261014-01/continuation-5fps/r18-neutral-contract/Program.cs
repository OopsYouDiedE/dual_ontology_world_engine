using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

static List<object> Rec(params (string key, object value)[] fields) => fields.Select(f => (object)new List<object> { f.key, f.value }).ToList();
static List<object> Seq(params object[] items) => items.ToList();
static object Read(object value, string key) => ((List<object>)value).Cast<List<object>>().First(p => (string)p[0] == key)[1];
static object Call(Type type, string name, params object[] args) => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
static string Json(object value) => JsonSerializer.Serialize(value);
static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
static string Failure(Action action)
{
    try { action(); }
    catch (TargetInvocationException error) { return error.InnerException!.Message; }
    throw new Exception("Expected validation rejection did not occur");
}
static object Law(string id) => Rec(("name", id), ("version", 1d), ("premise", Rec(("op", "constant"), ("value", "true"))), ("effects", Seq()));
static object Basis() => Seq(Rec(("source", "neutral-contract")));
static object World(string domain) => Rec(("domain", domain), ("adoptions", Seq()), ("revision", 2d), ("objects", Rec(("preserved", Seq("text", true)))), ("history", Seq()));

var current = typeof(DvSession).Assembly.GetType("DVProgram")!;
var context = new AssemblyLoadContext("reference-r17", true);
var reference = context.LoadFromAssemblyPath(Path.GetFullPath(args[0])).GetType("DVProgram")!;
var output = Path.GetFullPath(args[1]);
Require(!Directory.Exists(output), "Require a new neutral evidence directory");
Directory.CreateDirectory(output);

foreach (var domain in new[] { "physical", "mind" })
{
    var mode = domain == "physical" ? "enforce" : "predict";
    var seed = Call(current, "F_dv_adopt", World(domain), Law("seed"), mode, 1d, Basis());
    var before = Json(seed);
    var owned = Call(current, "F_dv_clone", Read(seed, "adoptions"));
    var sequential = seed;
    foreach (var id in new[] { "alpha", "beta" })
    {
        sequential = Call(reference, "F_dv_adopt", sequential, Law(id), mode, 1d, Basis());
        Call(current, "F_dv_adopt_into", domain, owned, Law(id), mode, 1d, Basis());
    }
    var batch = Call(current, "F_dv_finish_owned_adoptions", seed, owned);
    Require(Json(batch) == Json(sequential), "Batch changed values, ordering or revision relative to R17 sequential adoption");
    Require(Json(seed) == before, "Builder changed inherited world");
    var stable = Json(batch);
    ((List<object>)owned).Clear();
    Require(Json(batch) == stable && Json(seed) == before, "Finished world aliases builder");
    var empty = Call(current, "F_dv_clone", Read(seed, "adoptions"));
    Require(ReferenceEquals(seed, Call(current, "F_dv_finish_owned_adoptions", seed, empty)), "Empty batch changed world identity");
    foreach (var invalid in new[] {
        (law: Law("new"), confidence: 2d, basis: (object)Seq(), mode: "invalid"),
        (law: Law("seed"), confidence: 1d, basis: (object)Seq(), mode: "invalid"),
        (law: Law("seed"), confidence: 1d, basis: Basis(), mode: "invalid"),
        (law: Law("new"), confidence: 1d, basis: Basis(), mode: "invalid") })
    {
        var table = Call(current, "F_dv_clone", Read(seed, "adoptions"));
        var prior = Json(table);
        var oldError = Failure(() => Call(reference, "F_dv_adopt", seed, invalid.law, invalid.mode, invalid.confidence, invalid.basis));
        var newError = Failure(() => Call(current, "F_dv_adopt_into", domain, table, invalid.law, invalid.mode, invalid.confidence, invalid.basis));
        Require(oldError == newError && Json(table) == prior && Json(seed) == before, "Validation order or failure isolation changed");
    }
    var corrupt = (List<object>)Call(current, "F_dv_clone", Read(seed, "adoptions"));
    corrupt[0] = Seq("forged", true);
    Require(Failure(() => Call(current, "F_dv_finish_owned_adoptions", seed, corrupt)).Contains("changed inherited"), "Changed prefix was accepted");
}

var referenceRef = Rec(("type", "ref"), ("object", "neutral"), ("facet", "state"), ("field", "done"));
var mind = Rec(("domain", "mind"), ("subject", "owner"), ("time", 10d), ("revision", 0d), ("objects", Rec(("neutral", Rec(("facets", Rec(("state", Rec(("done", Call(current, "F_dv_field", false)))))))))));
var actor = Rec(("mind", mind));
var stop = Rec(("op", "equals"), ("ref", referenceRef), ("value", true));
var goal = Rec(("kind", "evidence"), ("ref", referenceRef), ("minimum_time", 0d), ("refresh_until", stop));
var goals = Seq(goal, Rec(("kind", "evidence"), ("minimum_time", 0d)));
var priorMind = Json(mind);
var priorGoals = Json(goals);
var refreshed = (List<object>)Call(current, "F_dvp_refresh_current_evidence", actor, goals);
Require(Convert.ToDouble(Read(refreshed[0], "minimum_time")) == 10d && Json(mind) == priorMind && Json(goals) == priorGoals, "Freshness update changed knowledge or cached goals");
Require(Convert.ToDouble(Read(refreshed[1], "minimum_time")) == 0d, "Unmarked goal refreshed");
var doneMind = Call(current, "F_dv_store", mind, referenceRef, Call(current, "F_dv_field", true));
var settled = (List<object>)Call(current, "F_dvp_refresh_current_evidence", Rec(("mind", doneMind)), goals);
Require(Convert.ToDouble(Read(settled[0], "minimum_time")) == 0d, "Known true stop predicate still refreshed");

var exact = new Dictionary<string, string> { ["$dv_rational"] = "1/3" };
var physical = Rec(("time", 0d), ("metadata", Seq(exact, "1/3", "文字\n\"quote\"", -1.25d, true)));
var initial = Rec(("physical", physical), ("actors", Seq()));
var state = Rec(("story", Rec()), ("physical", physical), ("actors", Seq()),
    ("events", Seq(Rec(("id", "e0"), ("type", "neutral"), ("data", exact)))),
    ("bindings", Seq()), ("costs", Seq()), ("run_start", 0d), ("duration_budget", 10d), ("horizon", 10d),
    ("duration_blocked", false), ("limit", 10d), ("cursor", 0d), ("executed", 0d), ("idle", 0d),
    ("status", "running"), ("initial", initial));
var serialized = Json(state);
var compatibility = DvSession.Restore(serialized);
var streaming = DvSession.Restore(serialized);
var expectedTrace = Encoding.UTF8.GetBytes(compatibility.Trace());
var tracePath = Path.Combine(output, "trace.json");
streaming.WriteTrace(tracePath);
Require(expectedTrace.SequenceEqual(File.ReadAllBytes(tracePath)), "Stream trace bytes differ from existing trace format");
var expectedSave = Encoding.UTF8.GetBytes(streaming.Save());
var savePath = Path.Combine(output, "save.json");
streaming.WriteSave(savePath);
Require(expectedSave.SequenceEqual(File.ReadAllBytes(savePath)), "Stream save bytes differ from tagged snapshot format");
Require(DvSession.Restore(File.ReadAllText(savePath)).Save() == streaming.Save(), "Exact snapshot restore changed values");
var beforeRejectedWrite = streaming.Save();
try { streaming.WriteTrace(tracePath); throw new Exception("Existing trace was overwritten"); }
catch (IOException) { }
Require(streaming.Save() == beforeRejectedWrite && expectedTrace.SequenceEqual(File.ReadAllBytes(tracePath)), "Rejected write appended another close event or changed evidence");
var options = (JsonSerializerOptions)typeof(DvSession).GetField("SnapshotOptions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
var payload = Enumerable.Repeat<object>(new string('x', 4096), 65536).ToList();
using var sink = new CountingSink();
JsonSerializer.Serialize(sink, (object)payload, options);
Console.WriteLine($"Neutral serializer buffer observation: logical bytes={sink.Length}; largest stream write={sink.LargestWrite}.");
Require(sink.Length > 256L * 1024 * 1024 && sink.LargestWrite <= 1024 * 1024, "Stream serializer buffered the logical document instead of bounded writes");
Console.WriteLine("Neutral generated-DV contract: adoption values/order/revisions equal R17 sequential output; validation first errors and isolation retained; inherited prefix protected; evidence refresh changes only marked goal freshness and stops on owned truth. Stream trace/save bytes match existing formats, exact tagged values restore, existing evidence is not overwritten.");

sealed class CountingSink : Stream
{
    private long written;
    public int LargestWrite { get; private set; }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => written;
    public override long Position { get => written; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) { written += count; LargestWrite = Math.Max(LargestWrite, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { written += buffer.Length; LargestWrite = Math.Max(LargestWrite, buffer.Length); }
}
