using DuelView.Compiled;
using System.Text.Json;

// Tool-only loader. Value semantics match DvSession.Restore/ReadValue, including
// exact $dv_rational atoms. It performs no initialization or state edits.
internal static class DebugCheckpointResume
{
    public static int Main(string[] args)
    {
        R.Arguments = args;
        try
        {
            if (args.Length <= 10 || string.IsNullOrWhiteSpace(args[10]))
                throw new InvalidOperationException("A validated tagged checkpoint Save is required");
            using var document = JsonDocument.Parse(File.ReadAllText(args[10]), new JsonDocumentOptions { MaxDepth = 512 });
            _ = DVProgram.ResumeDebugCheckpoint(ReadValue(document.RootElement));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
    }
    private static object ReadValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.EnumerateArray().Select(ReadValue).ToList(),
        JsonValueKind.Object when value.TryGetProperty("$dv_rational", out var exact) => Rational.From(exact.GetString()!),
        JsonValueKind.Object => value.EnumerateObject().Select(p => (object)new List<object> { p.Name, ReadValue(p.Value) }).ToList(),
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new JsonException("Unsupported DV checkpoint value")
    };
}
public static partial class DVProgram
{
    public static object ResumeDebugCheckpoint(object checkpoint) => F_dvg_debug_resume(checkpoint);
}
