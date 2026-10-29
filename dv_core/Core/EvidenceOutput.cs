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
    private static readonly object OutputLock = new();
    private static readonly Stream StandardOutput = Console.OpenStandardOutput();
    private static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    // Same value encoding as json_stringify, without a whole-packet UTF-16 copy.
    private static bool binaryOutputHeader;
    private static bool binaryRecording { get => Caches.BinaryRecording; set => Caches.BinaryRecording = value; }
    public static object json_print(object value)
    {
        lock (OutputLock)
        {
            if (binaryRecording || Environment.GetEnvironmentVariable("DV_BINARY_STDOUT") == "1")
            {
                if (!binaryOutputHeader) { StandardOutput.Write(BinaryEvidence.Magic); binaryOutputHeader = true; }
                // Projection packets are bounded values; exact atoms remain tagged.
                BinaryEvidence.WriteFrame(StandardOutput, BinaryProjection(value)); StandardOutput.Flush(); return true;
            }
            using var writer = new Utf8JsonWriter(StandardOutput, new JsonWriterOptions { SkipValidation = true });
            WriteJsonFast(writer, value);
            writer.Flush();
            StandardOutput.Write(NewLineBytes);
            StandardOutput.Flush();
        }
        return true;
    }

    // Opt-in evidence I/O only. The disabled hook never traverses the supplied value.
    // Values are frozen before registration; neither recording nor references mutate them.
    private static object BinaryProjection(object value)
    {
        if (value is Rational exact) return exact.ToString();
        if (TryItems(value, out var items)) return items.Select(BinaryProjection).ToList();
        return value;
    }
    private static RawDagWriter? RawEvidence { get => Caches.RawEvidence; set => Caches.RawEvidence = value; }
    private static object RawLock => Caches.RawLock;
    public static object raw_open(object path)
    {
        if (!AuditMode) throw new InvalidOperationException("Raw evidence requires audit mode");
        lock (RawLock)
        {
            if (RawEvidence is not null) throw new InvalidOperationException("Raw recorder already opened; abort a failed recorder before reopening");
            RawEvidence = new RawDagWriter(Text(path));
            binaryRecording = true;
            return true;
        }
    }
    public static object raw_event(object value)
    {
        if (!AuditMode || RawRecordingContext.Value is false) return true;
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Event(value);
            return true;
        }
    }
    public static object raw_checkpoint(object name, object value)
    {
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Checkpoint(Text(name), value);
            return true;
        }
    }
    public static object raw_finish(object value)
    {
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Finish(value);
            RawEvidence = null;
            return true;
        }
    }
    // Explicit recovery only: preserve the partial file, publish no seal/final name.
    public static object raw_abort()
    {
        lock (RawLock)
        {
            if (RawEvidence is null) return false;
            RawEvidence.Abort();
            RawEvidence = null;
            return true;
        }
    }
    public static object raw_ref(object value)
    {
        lock (RawLock)
        {
            if (RawEvidence is null || !IsDvList(value)) return false;
            return RawEvidence.Reference(value);
        }
    }

    private sealed record RawNode(long Id, long Frame);
    private sealed class RawDagWriter
    {
        private readonly long clockStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        private readonly double startupMilliseconds = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds;
        private readonly string destination;
        private readonly string partial;
        private readonly FileStream stream;
        private readonly ConditionalWeakTable<FrozenList, RawNode> nodes = new();
        private readonly HashSet<string> eventIds = new(StringComparer.Ordinal);
        private readonly BinaryEvidence.Encoder frameBuffer = new();
        private readonly IncrementalHash chainDigest;
        private readonly byte[] previous = Encoding.ASCII.GetBytes(new string('0', 64));
        private long nextNode, sequence, flushedSequence, eventCount;
        private int payloadOffset;
        private bool finished, failed;
        internal RawDagWriter(string path)
        {
            BinaryEvidence.RequireBinaryPath(path);
            destination = Path.GetFullPath(path); partial = destination + ".partial";
            if (File.Exists(destination)) throw new IOException("Evidence destination already exists: " + destination);
            stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024);
            try
            {
                chainDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                stream.Write(BinaryEvidence.Magic);
                BeginFrame("header", 5);
                frameBuffer.Text("format"); frameBuffer.Text("dv-raw-dag-v2");
                frameBuffer.Text("value_encoding"); frameBuffer.Text("typed binary atoms and prior node references");
                frameBuffer.Text("checksum"); frameBuffer.Text("sha256(previous-ascii + LF + sequence-ascii + LF + binary-payload)");
                frameBuffer.Text("clock_origin"); frameBuffer.Text("process-start UTC anchor plus monotonic elapsed; event commit emission, includes initialization");
                CompleteFrame();
                FlushCommit();
            }
            catch { chainDigest?.Dispose(); stream.Dispose(); throw; }
        }
        private void RequireOpen()
        {
            if (finished || failed) throw new InvalidOperationException("Raw recorder is closed or failed");
        }
        private void FlushCommit()
        {
            if (flushedSequence == sequence) return;
            try { stream.Flush(); flushedSequence = sequence; }
            catch { failed = true; throw; }
        }
        private void BeginFrame(string kind, int fields)
        {
            RequireOpen();
            frameBuffer.Clear();
            frameBuffer.Map(4);
            frameBuffer.Text("seq"); frameBuffer.Integer(sequence);
            frameBuffer.Text("prev"); frameBuffer.Utf8Text(previous);
            frameBuffer.Text("payload");
            payloadOffset = frameBuffer.Count;
            frameBuffer.Map(fields);
            frameBuffer.Text("kind"); frameBuffer.Text(kind);
        }
        private long CompleteFrame()
        {
            try
            {
                Span<byte> number = stackalloc byte[20];
                System.Buffers.Text.Utf8Formatter.TryFormat(sequence, number, out var numberLength);
                chainDigest.AppendData(previous);
                chainDigest.AppendData("\n"u8);
                chainDigest.AppendData(number[..numberLength]);
                chainDigest.AppendData("\n"u8);
                chainDigest.AppendData(frameBuffer.WrittenSpan[payloadOffset..]);
                Span<byte> checksum = stackalloc byte[32];
                if (!chainDigest.TryGetHashAndReset(checksum, out var checksumLength) || checksumLength != checksum.Length)
                    throw new InvalidDataException("Invalid DAG digest length");
                Span<byte> text = stackalloc byte[64];
                ReadOnlySpan<byte> hex = "0123456789abcdef"u8;
                for (var i = 0; i < checksum.Length; i++)
                {
                    text[i * 2] = hex[checksum[i] >> 4];
                    text[i * 2 + 1] = hex[checksum[i] & 15];
                }
                frameBuffer.Text("sha256"); frameBuffer.Utf8Text(text);
                BinaryEvidence.WriteEncodedFrame(stream, frameBuffer.WrittenSpan);
                text.CopyTo(previous);
                return sequence++;
            }
            catch { failed = true; throw; }
        }
        private RawNode Node(object value)
        {
            RequireOpen();
            if (!IsDvList(value)) throw new ArgumentException("Raw DAG roots must be DV lists/records");
            return FrozenNode((FrozenList)snapshot(value));
        }
        private RawNode FrozenNode(FrozenList frozen)
        {
            if (nodes.TryGetValue(frozen, out var known)) return known;
            try
            {
                // Define children before starting this parent's envelope. The
                // second pass only reads writer-local identities; it allocates
                // no reference dictionaries or duplicate payload tree.
                for (var i = 0; i < frozen.Count; i++)
                    if (frozen[i] is FrozenList child) FrozenNode(child);
                var id = nextNode++;
                BeginFrame("node", 3);
                frameBuffer.Text("node_id"); frameBuffer.Integer(id);
                frameBuffer.Text("items"); frameBuffer.List(frozen.Count);
                for (var i = 0; i < frozen.Count; i++)
                {
                    var item = frozen[i];
                    if (item is FrozenList child)
                    {
                        if (!nodes.TryGetValue(child, out var node)) throw new InvalidDataException("Missing prior DAG identity");
                        frameBuffer.Map(1); frameBuffer.Text("node"); frameBuffer.Integer(node.Id);
                    }
                    else if (item is Rational exact)
                    {
                        frameBuffer.Map(1); frameBuffer.Text("rational"); frameBuffer.Text(exact.ToString());
                    }
                    else frameBuffer.Value(item, 3);
                }
                var frame = CompleteFrame();
                var result = new RawNode(id, frame); nodes.Add(frozen, result); return result;
            }
            catch { failed = true; throw; }
        }
        internal void Event(object value)
        {
            var frozen = snapshot(value); var id = Text(record_find(frozen, "id", false));
            if (!eventIds.Add(id)) { failed = true; throw new InvalidOperationException("Repeated raw event ID: " + id); }
            var node = Node(frozen);
            try
            {
                BeginFrame("event", 5);
                frameBuffer.Text("event_id"); frameBuffer.Text(id);
                frameBuffer.Text("root"); frameBuffer.Integer(node.Id);
                frameBuffer.Text("ordinal"); frameBuffer.Integer(eventCount);
                frameBuffer.Text("process_elapsed_ms"); frameBuffer.Value(startupMilliseconds + System.Diagnostics.Stopwatch.GetElapsedTime(clockStarted).TotalMilliseconds, 0);
                CompleteFrame();
                FlushCommit(); eventCount++;
            }
            catch { failed = true; throw; }
        }
        internal void Checkpoint(string name, object value)
        {
            var node = Node(value);
            try
            {
                BeginFrame("checkpoint", 4);
                frameBuffer.Text("name"); frameBuffer.Text(name);
                frameBuffer.Text("root"); frameBuffer.Integer(node.Id);
                frameBuffer.Text("events"); frameBuffer.Integer(eventCount);
                CompleteFrame(); FlushCommit();
            }
            catch { failed = true; throw; }
        }
        internal object Reference(object value)
        {
            var node = Node(value); FlushCommit();
            return snapshot(new List<object>
            {
                new List<object> { "format", "dv-raw-dag-v2" },
                new List<object> { "journal", Path.GetFileName(destination) },
                new List<object> { "partial_journal", Path.GetFileName(partial) },
                new List<object> { "node_id", node.Id.ToString(CultureInfo.InvariantCulture) },
                new List<object> { "definition_frame", node.Frame.ToString(CultureInfo.InvariantCulture) }
            });
        }
        internal void Finish(object value)
        {
            Checkpoint("final", value);
            try
            {
                BeginFrame("seal", 4);
                frameBuffer.Text("events"); frameBuffer.Integer(eventCount);
                frameBuffer.Text("nodes"); frameBuffer.Integer(nextNode);
                frameBuffer.Text("final_checkpoint"); frameBuffer.Flag(true);
                CompleteFrame();
                FlushCommit(); stream.Dispose(); chainDigest.Dispose(); File.Move(partial, destination); finished = true;
            }
            catch { failed = true; stream.Dispose(); chainDigest.Dispose(); throw; }
        }
        internal void Abort() { failed = true; stream.Dispose(); chainDigest.Dispose(); }
    }
    public static void WriteEvidenceFile(string path, object value)
    {
        var writer = new RawDagWriter(path);
        try { writer.Checkpoint("initial", value); writer.Finish(value); }
        catch { writer.Abort(); throw; }
    }
    public static object ReadEvidenceFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        BinaryEvidence.RequireHeader(stream);
        var values = new List<object>(); var previous = new string('0', 64); long sequence = 0;
        object? final = null; bool sealedFile = false, initial = false;
        while (true)
        {
            var raw = BinaryEvidence.ReadFrame(stream, out var end); if (end) break;
            if (sealedFile) throw new InvalidDataException("Frames after final seal");
            var envelope = raw as Dictionary<string, object?> ?? throw new InvalidDataException("Invalid DAG envelope");
            if (envelope.Count != 4 || (long)envelope["seq"]! != sequence || (string)envelope["prev"]! != previous)
                throw new InvalidDataException("DAG sequence/checksum predecessor mismatch");
            var payload = (Dictionary<string, object?>)envelope["payload"]!;
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(Encoding.ASCII.GetBytes(previous + "\n" + sequence.ToString(CultureInfo.InvariantCulture) + "\n"));
            digest.AppendData(BinaryEvidence.Encode(payload));
            var checksum = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            if ((string)envelope["sha256"]! != checksum) throw new InvalidDataException("DAG checksum mismatch");
            previous = checksum; var kind = (string)payload["kind"]!;
            if (sequence++ == 0)
            {
                if (kind != "header" || (string)payload["format"]! != "dv-raw-dag-v2") throw new InvalidDataException("Unsupported DAG header");
                continue;
            }
            if (final is not null && kind != "seal") throw new InvalidDataException("Unsealed final checkpoint");
            if (kind == "node")
            {
                if ((long)payload["node_id"]! != values.Count) throw new InvalidDataException("Invalid DAG node order");
                var items = (List<object?>)payload["items"]!; var value = new List<object>();
                foreach (var item in items)
                {
                    if (item is Dictionary<string, object?> atom)
                    {
                        if (atom.Count != 1) throw new InvalidDataException("Invalid DAG atom");
                        if (atom.TryGetValue("node", out var reference))
                        {
                            var index = (long)reference!;
                            if (index < 0 || index >= values.Count) throw new InvalidDataException("Missing DAG node");
                            value.Add(values[(int)index]);
                        }
                        else if (atom.TryGetValue("rational", out var exact)) value.Add(Rational.From((string)exact!));
                        else throw new InvalidDataException("Unknown DAG atom");
                    }
                    else value.Add(item is long integer ? (object)(double)integer : item ?? throw new InvalidDataException("Null DV atom"));
                }
                values.Add(snapshot(value));
            }
            else if (kind == "checkpoint")
            {
                var root = (long)payload["root"]!;
                if (root < 0 || root >= values.Count || (long)payload["events"]! != 0) throw new InvalidDataException("Invalid value-file checkpoint");
                initial |= (string)payload["name"]! == "initial";
                if ((string)payload["name"]! == "final") final = values[(int)root];
            }
            else if (kind == "seal")
            {
                if (!initial || final is null || payload["final_checkpoint"] is not true || (long)payload["nodes"]! != values.Count || (long)payload["events"]! != 0)
                    throw new InvalidDataException("Invalid binary value seal");
                sealedFile = true;
            }
            else throw new InvalidDataException("Unsupported frame in binary value file");
        }
        return sealedFile && final is not null ? final : throw new InvalidDataException("Incomplete binary value file");
    }
    public static object json_write(object path, object value) => throw new InvalidOperationException("JSON recording is forbidden; use raw DAG binary evidence");
}
