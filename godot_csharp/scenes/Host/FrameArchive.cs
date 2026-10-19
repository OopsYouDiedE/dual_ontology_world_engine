using Godot;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Security.Cryptography;

// Owns pixel copies only. Worker never touches Godot resources or scene objects.
public partial class FrameArchive : Node
{
    private readonly ConcurrentQueue<string> completed = new();
    private Task tail = Task.CompletedTask;
    private int pending;
    private long origin;
    public void Configure(long clockUs) => origin = Stopwatch.GetTimestamp() - clockUs * Stopwatch.Frequency / 1000000;
    private long Clock() => (Stopwatch.GetTimestamp() - origin) * 1000000 / Stopwatch.Frequency;
    public bool HasCapacity() => Volatile.Read(ref pending) < 8;
    public bool IsIdle() => Volatile.Read(ref pending) == 0;
    public void Enqueue(byte[] pixels, int width, int height, string path, long capturedUs)
    {
        if (!HasCapacity()) throw new InvalidOperationException("Frame archive capacity exhausted");
        if (pixels.Length != checked(width * height * 4)) throw new ArgumentException("Expected owned RGBA8 pixels");
        var queued = Clock();
        Interlocked.Increment(ref pending);
        tail = tail.ContinueWith(_ =>
        {
            var begin = Clock();
            long encoded = begin;
            long written = begin;
            string? error = null;
            try
            {
                var png = Encode(pixels, width, height);
                encoded = Clock();
                // A completed PNG appears atomically; a failed write retains its .partial.
                using (var stream = new FileStream(path + ".partial", FileMode.CreateNew, System.IO.FileAccess.Write)) stream.Write(png);
                System.IO.File.Move(path + ".partial", path, overwrite: false);
                written = Clock();
            }
            catch (Exception failure) { error = failure.ToString(); }
            finally
            {
                var pixelHash = Convert.ToHexString(SHA256.HashData(pixels)).ToLowerInvariant();
                completed.Enqueue(JsonSerializer.Serialize(new { file = System.IO.Path.GetFileName(path), captured_us = capturedUs,
                    queued_us = queued, begin_us = begin, encoded_us = encoded, written_us = written, end_us = Clock(),
                    rgba_sha256 = pixelHash, error }));
                Interlocked.Decrement(ref pending);
            }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }
    public Godot.Collections.Array PollCompleted()
    {
        Godot.Collections.Array rows = new();
        while (completed.TryDequeue(out var row)) rows.Add(row);
        return rows;
    }
    private static byte[] Encode(byte[] pixels, int width, int height)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var stride = checked(width * 4);
            for (var row = 0; row < height; row++)
            {
                zlib.WriteByte(0); // PNG filter None: lossless original viewport bytes.
                zlib.Write(pixels.AsSpan(row * stride, stride));
            }
        }
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; header[9] = 6; // 8-bit RGBA, no palette or interlace.
        Chunk(png, "IHDR"u8, header);
        Chunk(png, "IDAT"u8, compressed.ToArray());
        Chunk(png, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return png.ToArray();
    }
    private static void Chunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        stream.Write(number); stream.Write(type); stream.Write(data);
        uint crc = 0xffffffff;
        foreach (var b in type) crc = CrcTable[(crc ^ b) & 255] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 255] ^ (crc >> 8);
        BinaryPrimitives.WriteUInt32BigEndian(number, crc ^ 0xffffffff);
        stream.Write(number);
    }
    private static readonly uint[] CrcTable = BuildCrc();
    private static uint[] BuildCrc()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var bit = 0; bit < 8; bit++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
