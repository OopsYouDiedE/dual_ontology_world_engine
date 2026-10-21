using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static object Invoke(Type type, string name, object[] arguments)
    {
        var method = type.GetMethod(name, Flags) ?? throw new MissingMethodException(type.FullName, name);
        return method.Invoke(null, arguments) ?? throw new InvalidOperationException(name + " returned null");
    }
    private static object Argument(Assembly assembly, JsonElement specification)
    {
        string kind = specification.GetProperty("kind").GetString()!;
        if (kind == "text") return specification.GetProperty("value").GetString()!;
        if (kind == "bool") return specification.GetProperty("value").GetBoolean();
        Type runtime = assembly.GetType("DuelView.Compiled.R", true)!;
        if (kind == "number") return Invoke(runtime, "number", new object[] { specification.GetProperty("value").GetRawText() });
        if (kind == "data-item")
        {
            string name = specification.GetProperty("function").GetString()!;
            if (name != "c1n_rows" && name != "c2_norms") throw new InvalidOperationException("Unapproved fixture provider " + name);
            object list = Invoke(assembly.GetType("DVProgram", true)!, "F_" + name, Array.Empty<object>());
            double index = specification.GetProperty("index").GetDouble();
            return Invoke(runtime, "at", new object[] { list, index });
        }
        throw new InvalidOperationException("Unknown argument fixture " + kind);
    }
    private static string Evaluate(Assembly assembly, JsonElement specification)
    {
        string name = specification.GetProperty("function").GetString()!;
        if (!name.StartsWith("c1", StringComparison.Ordinal) && !name.StartsWith("c2", StringComparison.Ordinal))
            throw new InvalidOperationException("Only listed chapter data declarations may be evaluated");
        object[] arguments = specification.GetProperty("arguments").EnumerateArray().Select(x => Argument(assembly, x)).ToArray();
        object value = Invoke(assembly.GetType("DVProgram", true)!, "F_" + name, arguments);
        return (string)Invoke(assembly.GetType("DuelView.Compiled.R", true)!, "json_stringify", new object[] { value });
    }
    private static string? Difference(JsonElement left, JsonElement right, string path)
    {
        if (left.ValueKind != right.ValueKind) return path + ": JSON kinds differ";
        if (left.ValueKind == JsonValueKind.Array)
        {
            if (left.GetArrayLength() != right.GetArrayLength()) return path + ": array lengths differ";
            for (int i = 0; i < left.GetArrayLength(); i++)
            {
                string? mismatch = Difference(left[i], right[i], path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                if (mismatch != null) return mismatch;
            }
            return null;
        }
        if (left.ValueKind == JsonValueKind.Object)
        {
            var a = left.EnumerateObject().ToArray(); var b = right.EnumerateObject().ToArray();
            if (a.Length != b.Length) return path + ": object lengths differ";
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Name != b[i].Name) return path + ": property names or order differ";
                string? mismatch = Difference(a[i].Value, b[i].Value, path + "." + a[i].Name);
                if (mismatch != null) return mismatch;
            }
            return null;
        }
        bool same = left.ValueKind switch
        {
            JsonValueKind.String => left.GetString() == right.GetString(),
            // Both values use the identical serializer. Compare its exact number
            // representation so large integers cannot collapse through double.
            JsonValueKind.Number => left.GetRawText() == right.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText()
        };
        return same ? null : path + ": values differ";
    }
    private static Dictionary<string, string> ExceptionRecord(Exception error)
    {
        while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
        return new() { ["type"] = error.GetType().FullName ?? "Exception", ["message"] = error.Message,
                       ["details"] = error.ToString() };
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static void WriteNew(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }
    private static int Main(string[] args)
    {
        Assembly before = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        Assembly after = Assembly.LoadFrom(Path.GetFullPath(args[1]));
        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(args[2]));
        string output = args[3];
        var results = new List<Dictionary<string, object?>>();
        bool failure = false;
        foreach (JsonElement specification in plan.RootElement.GetProperty("cases").EnumerateArray())
        {
            string id = specification.GetProperty("id").GetString()!;
            var result = new Dictionary<string, object?> {
                ["id"] = id, ["function"] = specification.GetProperty("function").GetString(),
                ["label"] = specification.GetProperty("label").GetString() };
            string? left = null, right = null;
            try { left = Evaluate(before, specification); }
            catch (Exception error) { result["before_exception"] = ExceptionRecord(error); }
            try { right = Evaluate(after, specification); }
            catch (Exception error) { result["after_exception"] = ExceptionRecord(error); }
            if (left == null || right == null)
            {
                result["status"] = "exception"; failure = true;
            }
            else
            {
                result["before_sha256"] = Hash(left); result["after_sha256"] = Hash(right);
                using JsonDocument a = JsonDocument.Parse(left, new JsonDocumentOptions { MaxDepth = 256 });
                using JsonDocument b = JsonDocument.Parse(right, new JsonDocumentOptions { MaxDepth = 256 });
                string? mismatch = Difference(a.RootElement, b.RootElement, "$");
                result["status"] = mismatch == null ? "equal" : "different";
                if (mismatch != null)
                {
                    result["difference"] = mismatch;
                    WriteNew(Path.Combine(output, id + ".before.txt"), left);
                    WriteNew(Path.Combine(output, id + ".after.txt"), right);
                    failure = true;
                }
            }
            results.Add(result);
        }
        WriteNew(Path.Combine(output, "declaration-results.json"), JsonSerializer.Serialize(results, Options) + "\n");
        Console.WriteLine(failure ? "Declaration comparison has differences or uncovered exceptions; inspect declaration-results.json."
                                  : "Every planned declaration returned equivalent serialized structure and text.");
        return failure ? 1 : 0;
    }
}
