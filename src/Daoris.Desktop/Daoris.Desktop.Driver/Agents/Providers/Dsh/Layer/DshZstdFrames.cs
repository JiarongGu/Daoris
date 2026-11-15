using System.Buffers.Binary;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A dsh session log as dsh keeps it under its default compression: one Zstandard frame for the header line and one for the events, each a
/// frame whose blocks are stored raw, which any Zstandard decoder reads and which needs no compressor here. dsh scans frames by their
/// structure, wants the header alone in the first, and checks a checksum only where a frame carries one, so these carry none.
/// </summary>
internal static class DshZstdFrames
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private const uint Magic = 0xFD2FB528;

    /// <summary>Single-segment frame with a four-byte content size and no checksum or dictionary.</summary>
    private const byte Descriptor = 0xA0;

    private const int Block = 65_536;

    /// <summary>The file as dsh keeps it: <paramref name="jsonl"/>'s first line a frame of its own, the rest a second.</summary>
    public static byte[] OfLog(string jsonl)
    {
        var bytes = Utf8.GetBytes(jsonl);
        var header = Array.IndexOf(bytes, (byte)'\n') + 1;
        if (header <= 0) throw new FormatException("a session log opens on its header line.");
        using var file = new MemoryStream();
        Frame(file, bytes.AsSpan(0, header));
        if (header < bytes.Length) Frame(file, bytes.AsSpan(header));
        return file.ToArray();
    }

    private static void Frame(Stream into, ReadOnlySpan<byte> content)
    {
        Span<byte> head = stackalloc byte[9];
        BinaryPrimitives.WriteUInt32LittleEndian(head, Magic);
        head[4] = Descriptor;
        BinaryPrimitives.WriteUInt32LittleEndian(head[5..], (uint)content.Length);
        into.Write(head);
        for (var at = 0; at < content.Length; at += Block)
        {
            var size = Math.Min(Block, content.Length - at);
            var last = at + size >= content.Length;
            Span<byte> block = stackalloc byte[3];
            var word = ((uint)size << 3) | (last ? 1u : 0u);
            block[0] = (byte)word;
            block[1] = (byte)(word >> 8);
            block[2] = (byte)(word >> 16);
            into.Write(block);
            into.Write(content.Slice(at, size));
        }
    }
}
