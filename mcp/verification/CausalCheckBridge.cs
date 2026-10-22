using DuelView.Compiled;
using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

// Tool-only binary intake. The domain judgment stays in 70-checks.dv.
internal static class CausalCheckBridge
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3 && args.Length != 4)
                throw new ArgumentException("Usage: causal-check manifest.dv.json raw-events.dvdag report.dvdag [--opt-causal-index=on|off]");
            var option = args.Length == 4 ? args[3] : "--opt-causal-index=on";
            if (option != "--opt-causal-index=on" && option != "--opt-causal-index=off")
                throw new ArgumentException("Unknown causal index switch");
            R.ConfigureCausalIndex(option == "--opt-causal-index=on");
            R.Arguments = args;
            using var output = R.UseOutput(Console.Error);
            using var audit = R.UseAudit(true);
            using var recording = R.UseRawRecording(false);
            var manifest = R.json_parse(R.read(args[0]));
            var expectedHash = R.Text(R.record_find(manifest, "raw_sha256", ""));
            var stage = Stopwatch.StartNew();
            Console.Error.WriteLine("causal-stage dag-read-start " + option);
            var sealedValue = ReadSealedJournal(args[1], expectedHash);
            Console.Error.WriteLine("causal-stage dag-read-complete elapsed_ms=" + stage.Elapsed.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture));
            stage.Restart();
            Console.Error.WriteLine("causal-stage domain-check-start");
            var report = DVProgram.JudgeBinaryEvidence(manifest, sealedValue);
            Console.Error.WriteLine("causal-stage domain-check-complete elapsed_ms=" + stage.Elapsed.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture));
            R.WriteEvidenceFile(args[2], report);
            var summary = R.record_with(DVProgram.BinaryEvidenceSummary(report), "report", Path.GetFullPath(args[2]));
            using var stream = Console.OpenStandardOutput();
            stream.Write(BinaryEvidence.Magic);
            BinaryEvidence.WriteFrame(stream, summary);
            stream.Flush();
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
    }

    private static object ReadSealedJournal(string path, string expectedHash)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (actualHash != expectedHash) throw new InvalidDataException("Raw DAG differs from its captured source hash.");
        stream.Position = 0;
        BinaryEvidence.RequireHeader(stream);
        var nodes = new List<object>();
        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        var previous = new string('0', 64);
        long sequence = 0, eventCount = 0;
        object? final = null;
        var initial = false;
        var sealedFile = false;
        object Node(long id) => id >= 0 && id < nodes.Count ? nodes[(int)id] :
            throw new InvalidDataException("DAG references a missing or forward node.");
        while (true)
        {
            var value = BinaryEvidence.ReadFrame(stream, out var end);
            if (end) break;
            if (sealedFile) throw new InvalidDataException("DAG contains frames after its seal.");
            var envelope = value as Dictionary<string, object?> ?? throw new InvalidDataException("Invalid DAG envelope.");
            if (envelope.Count != 4 || (long)envelope["seq"]! != sequence || (string)envelope["prev"]! != previous)
                throw new InvalidDataException("DAG sequence or checksum predecessor differs.");
            var payload = (Dictionary<string, object?>)envelope["payload"]!;
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(Encoding.ASCII.GetBytes(previous + "\n" + sequence.ToString(CultureInfo.InvariantCulture) + "\n"));
            digest.AppendData(BinaryEvidence.Encode(payload));
            var checksum = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            if ((string)envelope["sha256"]! != checksum) throw new InvalidDataException("DAG checksum mismatch.");
            previous = checksum;
            var kind = (string)payload["kind"]!;
            if (sequence++ == 0)
            {
                if (kind != "header" || (string)payload["format"]! != "dv-raw-dag-v2")
                    throw new InvalidDataException("Unsupported causal evidence format.");
                continue;
            }
            if (final is not null && kind != "seal") throw new InvalidDataException("Final checkpoint is not followed by its seal.");
            switch (kind)
            {
                case "node":
                    if ((long)payload["node_id"]! != nodes.Count) throw new InvalidDataException("DAG node order is incomplete.");
                    var children = new List<object>();
                    foreach (var item in (List<object?>)payload["items"]!)
                    {
                        if (item is Dictionary<string, object?> atom)
                        {
                            if (atom.Count != 1) throw new InvalidDataException("Invalid DAG atom.");
                            if (atom.TryGetValue("node", out var reference)) children.Add(Node((long)reference!));
                            else if (atom.TryGetValue("rational", out var text))
                            {
                                var exact = Rational.From((string)text!);
                                if (exact.ToString() != (string)text!) throw new InvalidDataException("Noncanonical rational atom.");
                                children.Add(exact);
                            }
                            else throw new InvalidDataException("Unknown DAG atom.");
                        }
                        else children.Add(item is long integer ? (object)(double)integer :
                            item ?? throw new InvalidDataException("Null DV atom."));
                    }
                    nodes.Add(R.snapshot(children));
                    break;
                case "event":
                    var id = (string)payload["event_id"]!;
                    if ((long)payload["ordinal"]! != eventCount || !eventIds.Add(id) ||
                        R.Text(R.record_find(Node((long)payload["root"]!), "id", "")) != id)
                        throw new InvalidDataException("DAG event commit differs from its original value.");
                    eventCount++;
                    break;
                case "checkpoint":
                    if ((long)payload["events"]! != eventCount) throw new InvalidDataException("Checkpoint event count differs.");
                    var root = Node((long)payload["root"]!);
                    var name = (string)payload["name"]!;
                    initial |= name == "initial";
                    if (name == "final") final = root;
                    break;
                case "seal":
                    if (!initial || final is null || payload["final_checkpoint"] is not true ||
                        (long)payload["events"]! != eventCount || (long)payload["nodes"]! != nodes.Count)
                        throw new InvalidDataException("DAG seal is incomplete.");
                    sealedFile = true;
                    break;
                default: throw new InvalidDataException("Unknown DAG frame: " + kind);
            }
        }
        return sealedFile && final is not null ? final : throw new InvalidDataException("Causal evidence has no complete final seal.");
    }
}

public static partial class DVProgram
{
    public static object JudgeBinaryEvidence(object manifest, object sealedValue) => F_v_binary_judge(manifest, sealedValue);
    public static object BinaryEvidenceSummary(object report) => F_v_binary_summary(report);
}
