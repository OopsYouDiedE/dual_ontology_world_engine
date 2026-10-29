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
    public static object fail(object a) => throw new Exception(Text(a));
    public static object json_parse(object text)
    {
        using var document = JsonDocument.Parse(Text(text), new JsonDocumentOptions { MaxDepth = 256 });
        return ReadJson(document.RootElement);
    }
    private static object ReadJson(JsonElement item) => item.ValueKind switch
    {
        JsonValueKind.Array => item.EnumerateArray().Select(ReadJson).ToList(),
        JsonValueKind.String => item.GetString()!,
        JsonValueKind.Number => Num(item.GetDouble()),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new Exception("DV persistence supports arrays, text, finite numbers and booleans")
    };
    // Serialization preserves the supplied DV value. Evidence projection belongs
    // to explicit DV reporting APIs, never to implicit field-name filtering.
    private static void WriteJsonFast(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string s:
                writer.WriteStringValue(s);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case Rational q:
                Span<char> qBuf = stackalloc char[128];
                if (q.Denominator.IsOne)
                {
                    if (q.Numerator.TryFormat(qBuf, out int written, provider: CultureInfo.InvariantCulture))
                    {
                        writer.WriteStringValue(qBuf[..written]);
                        break;
                    }
                }
                else
                {
                    if (q.Numerator.TryFormat(qBuf, out int nWritten, provider: CultureInfo.InvariantCulture))
                    {
                        qBuf[nWritten] = '/';
                        if (q.Denominator.TryFormat(qBuf[(nWritten + 1)..], out int dWritten, provider: CultureInfo.InvariantCulture))
                        {
                            writer.WriteStringValue(qBuf[..(nWritten + 1 + dWritten)]);
                            break;
                        }
                    }
                }
                writer.WriteStringValue(q.ToString());
                break;
            case object dvList when TryItems(dvList, out var list):
                writer.WriteStartArray();
                for (var i = 0; i < list.Count; i++)
                {
                    WriteJsonFast(writer, list[i]);
                    if (writer.BytesPending >= 16 * 1024 * 1024) writer.Flush();
                }
                writer.WriteEndArray();
                break;
            default:
                throw new Exception("DV persistence supports arrays, text, finite numbers and booleans");
        }
    }
    private sealed class JsonEncoding(string text)
    {
        internal readonly string Text = text;
    }
    private static ConditionalWeakTable<FrozenList, JsonEncoding> JsonEncodings => Caches.JsonEncodings;
    public static object json_stringify(object value)
    {
        // Only frozen identities are cacheable. Mutable values retain the exact
        // pre-existing serialization path and never reuse a stale encoding.
        if (value is FrozenList frozen)
            return JsonEncodings.GetValue(frozen, key => new JsonEncoding(EncodeJson(key))).Text;
        return EncodeJson(value);
    }
    private static string EncodeJson(object value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { SkipValidation = true }))
        {
            WriteJsonFast(writer, value);
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }
    public static object print(object a)
    {
        lock (OutputLock)
        {
            if (ConsoleOutputContext.Value is { } output)
            {
                output.WriteLine(Text(show(a)));
                output.Flush();
                return a;
            }
            if (binaryRecording || Environment.GetEnvironmentVariable("DV_BINARY_STDOUT") == "1")
            {
                Console.Error.WriteLine(Text(show(a)));
                Console.Error.Flush();
            }
            else
            {
                Console.WriteLine(Text(show(a)));
                Console.Out.Flush();
            }
        }
        return a;
    }
    public static object Block(Func<object> f) => f();
}
