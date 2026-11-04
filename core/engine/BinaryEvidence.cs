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

// Storage codec only. JSON remains an in-memory host/compiler exchange format.
public static class BinaryEvidence
{
    public static readonly byte[] Magic = "DVLOG2\r\n"u8.ToArray();
    private const int MaxFrame = 256 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public static void RequireBinaryPath(string path)
    {
        var parts = Path.GetFileName(path).Split('.');
        if (parts.Any(p => p.Equals("json", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("jsonl", StringComparison.OrdinalIgnoreCase) || p.Equals("gz", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("JSON recording is forbidden; use .dvlog/.dvdag/.dvsave");
    }
    public static byte[] Encode(object? value)
    {
        var writer = new Encoder();
        writer.Value(value);
        return writer.WrittenSpan.ToArray();
    }
    // One writer owns its buffer. DAG frames can append their already encoded
    // payload directly to an envelope without a second object-graph traversal.
    internal sealed class Encoder
    {
        private readonly ArrayBufferWriter<byte> output = new(1024);
        internal ReadOnlySpan<byte> WrittenSpan => output.WrittenSpan;
        internal int Count => output.WrittenCount;
        internal void Clear() => output.Clear();
        private void Byte(byte value)
        {
            output.GetSpan(1)[0] = value; output.Advance(1);
        }
        private void Int32(int value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(output.GetSpan(4), value);
            output.Advance(4);
        }
        internal void Integer(long value)
        {
            var bytes = output.GetSpan(9); bytes[0] = 3;
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes[1..], value);
            output.Advance(9);
        }
        internal void Text(string text)
        {
            var length = Utf8.GetByteCount(text);
            Byte(5); Int32(length);
            var written = Utf8.GetBytes(text.AsSpan(), output.GetSpan(length));
            output.Advance(written);
        }
        internal void Utf8Text(ReadOnlySpan<byte> text)
        {
            Byte(5); Int32(text.Length);
            text.CopyTo(output.GetSpan(text.Length)); output.Advance(text.Length);
        }
        internal void Map(int count) { Byte(7); Int32(count); }
        internal void List(int count) { Byte(8); Int32(count); }
        internal void Flag(bool value) => Byte(value ? (byte)2 : (byte)1);
        internal void Value(object? value, int depth = 0)
        {
            if (depth > 512) throw new InvalidDataException("Binary evidence nesting exceeds 512");
            switch (value)
            {
                case null: Byte(0); break;
                case bool flag: Flag(flag); break;
                case int number: Integer(number); break;
                case long number: Integer(number); break;
                case double number when double.IsFinite(number):
                    Byte(4);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(output.GetSpan(8), BitConverter.DoubleToInt64Bits(number));
                    output.Advance(8); break;
                case string text: Text(text); break;
                case IDictionary<string, object?> map:
                    Map(map.Count);
                    foreach (var entry in map) { Text(entry.Key); Value(entry.Value, depth + 1); }
                    break;
                case object?[] array:
                    // Host arrays are storage values, not members of the DV value domain.
                    List(array.Length);
                    foreach (var item in array) Value(item, depth + 1);
                    break;
                case JsonElement element: Value(FromJson(element), depth); break;
                default:
                    if (!R.TryItems(value!, out var items)) throw new InvalidDataException("Unsupported binary evidence atom");
                    List(items.Count);
                    for (var i = 0; i < items.Count; i++) Value(items[i], depth + 1);
                    break;
            }
        }
    }
    public static object? FromJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(p => p.Name, p => FromJson(p.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select(FromJson).ToArray(),
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetInt64(out var integer) ? (object)integer : value.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null,
        _ => throw new InvalidDataException("Unsupported host record value")
    };
    public static object? Decode(byte[] payload)
    {
        using var input = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(input, Utf8);
        var result = ReadValue(reader, payload.Length, 0);
        if (input.Position != input.Length) throw new InvalidDataException("Trailing binary value bytes");
        return result;
    }
    private static object? ReadValue(BinaryReader reader, int limit, int depth)
    {
        if (depth > 512) throw new InvalidDataException("Binary evidence nesting exceeds 512");
        switch (reader.ReadByte())
        {
            case 0: return null;
            case 1: return false;
            case 2: return true;
            case 3: return reader.ReadInt64();
            case 4:
                var number = reader.ReadDouble();
                if (!double.IsFinite(number)) throw new InvalidDataException("Nonfinite evidence number");
                return number;
            case 5:
                var size = reader.ReadInt32();
                if (size < 0 || size > limit) throw new InvalidDataException("Invalid binary text length");
                var bytes = reader.ReadBytes(size);
                if (bytes.Length != size) throw new EndOfStreamException();
                return Utf8.GetString(bytes);
            case 7:
                var count = reader.ReadInt32();
                if (count < 0 || count > limit) throw new InvalidDataException("Invalid binary map length");
                var map = new Dictionary<string, object?>();
                for (var i = 0; i < count; i++)
                {
                    var key = ReadValue(reader, limit, depth + 1) as string ?? throw new InvalidDataException("Binary map key is not text");
                    map.Add(key, ReadValue(reader, limit, depth + 1));
                }
                return map;
            case 8:
                var length = reader.ReadInt32();
                if (length < 0 || length > limit) throw new InvalidDataException("Invalid binary list length");
                var list = new List<object?>();
                for (var i = 0; i < length; i++) list.Add(ReadValue(reader, limit, depth + 1));
                return list;
            default: throw new InvalidDataException("Unknown binary value tag");
        }
    }
    public static void WriteFrame(Stream stream, object? value)
    {
        var writer = new Encoder();
        writer.Value(value);
        WriteEncodedFrame(stream, writer.WrittenSpan);
    }
    internal static void WriteEncodedFrame(Stream stream, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxFrame) throw new InvalidDataException("Binary frame exceeds size limit");
        Span<byte> prefix = stackalloc byte[4];
        Span<byte> checksum = stackalloc byte[32];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        SHA256.HashData(payload, checksum);
        stream.Write(prefix); stream.Write(checksum); stream.Write(payload);
    }
    public static object? ReadFrame(Stream stream, out bool end)
    {
        Span<byte> prefix = stackalloc byte[4];
        var first = stream.ReadByte();
        end = first < 0;
        if (end) return null;
        prefix[0] = (byte)first; stream.ReadExactly(prefix[1..]);
        var size = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (size < 0 || size > MaxFrame) throw new InvalidDataException("Invalid binary frame size");
        var checksum = new byte[32]; stream.ReadExactly(checksum);
        var payload = new byte[size]; stream.ReadExactly(payload);
        if (!CryptographicOperations.FixedTimeEquals(checksum, SHA256.HashData(payload))) throw new InvalidDataException("Binary frame checksum mismatch");
        return Decode(payload);
    }
    public static void RequireHeader(Stream stream)
    {
        var header = new byte[Magic.Length]; stream.ReadExactly(header);
        if (!header.AsSpan().SequenceEqual(Magic)) throw new InvalidDataException("Unsupported binary evidence header");
    }
    public static void WriteJsonRecords(string path, IEnumerable<string> records)
    {
        RequireBinaryPath(path);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        output.Write(Magic);
        WriteJsonRecords(output, records);
    }
    public static void WriteRecords(string path, IEnumerable<object?> records)
    {
        RequireBinaryPath(path);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        output.Write(Magic);
        foreach (var record in records) WriteFrame(output, record);
    }
    public static void AppendJsonRecords(string path, string records)
    {
        RequireBinaryPath(path);
        using var output = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        if (output.Length == 0) output.Write(Magic);
        else { RequireHeader(output); output.Position = output.Length; }
        WriteJsonRecords(output, records.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }
    private static void WriteJsonRecords(Stream stream, IEnumerable<string> records)
    {
        foreach (var record in records)
        {
            using var document = JsonDocument.Parse(record);
            WriteFrame(stream, document.RootElement);
        }
    }
}

