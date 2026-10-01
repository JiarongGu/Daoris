using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A refusal by one named check (TOOLS4, D121 §3.6): an archive or a download that failed it.</summary>
/// <param name="check">The code both twins spell — <c>hash</c>, <c>outside</c>, <c>truncated</c>, … — so a screen and a
/// terminal name the same failure. The CLI's <c>RefusalError</c> carries the same codes.</param>
public sealed class ToolRefusal(string check, string message) : DriverException(message)
{
    public string Check { get; } = check;
}

/// <summary>
/// A tool's archive, unpacked whole (TOOLS4, D121 §3.6): the twin of the CLI's <c>zipfile.ts</c> and
/// <c>tarball.ts</c>, held by one table.
/// </summary>
/// <remarks>
/// <para>Only ever opened AFTER its hash and size matched the list's. That makes the bytes the ones the list vouched
/// for; it does not make every name inside safe to write, so the rules hold whoever wrote the archive, and each
/// refusal names its check: <c>absolute</c>, <c>outside</c>, <c>stream</c>, <c>link</c>, <c>entry</c> (neither a
/// file nor a folder), <c>encrypted</c>, <c>method</c>, <c>checksum</c>, <c>truncated</c> and <c>damaged</c>.</para>
/// <para>🔴 <b>Read by hand, inflated by .NET.</b> The design named .NET's own readers. Measured on .NET 10 before a
/// line was written: <see cref="ZipArchive"/> reads a stored entry whose CRC-32 is wrong without complaint and names no
/// entry's compression method, <c>TarReader</c> reads a header that fails its checksum and an archive with no end, and
/// <see cref="GZipStream"/> reads a gzip cut before its trailer as if whole. So the records are walked here, as the CLI
/// walks them, and the bytes are inflated by <see cref="DeflateStream"/> and <see cref="GZipStream"/>.</para>
/// </remarks>
public static partial class ToolInstall
{
    private const uint EndRecord = 0x06054b50;
    private const uint Zip64Locator = 0x07064b50;
    private const uint Zip64End = 0x06064b50;
    private const uint DirectoryEntry = 0x02014b50;
    private const uint LocalEntry = 0x04034b50;

    /// <summary>The end record is 22 bytes and may carry a comment of up to 65,535 after it.</summary>
    private const int EndSearch = 22 + 0xffff;

    private const int UnixType = 0xF000;
    private const int UnixLink = 0xA000;

    /// <summary>The most a name-carrying tar entry may hold. A real one is a path; a larger one is not a name.</summary>
    private const int MetadataLimit = 64 * 1024;

    private const UnixFileMode Runnable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const UnixFileMode Plain = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    /// Unpack a verified archive whole into <paramref name="into"/>, and find the executable its list names there
    /// (§3.6). The CLI's <c>unpackPackage</c>.
    /// </summary>
    /// <returns>The executable's path.</returns>
    /// <exception cref="ToolRefusal">The archive's own check, <c>archive</c> for a kind this build does not unpack, or
    /// <c>exe</c> when the executable is not a file in it.</exception>
    public static string Unpack(string archive, string kind, string into, string exe)
    {
        switch (kind)
        {
            case "zip":
                Unzip(archive, into);
                break;
            case "tar.gz":
                UntarGz(archive, into);
                break;
            default:
                throw new ToolRefusal("archive", $"`{kind}` is not an archive this build unpacks — zip or tar.gz");
        }

        var file = Path.Combine([into, .. exe.Split('/')]);
        if (!File.Exists(file))
        {
            throw new ToolRefusal("exe", $"the archive holds no file at `{exe}`, the executable its list names — nothing of it is kept");
        }

        // Nothing is patched (§3.6): the executable bit off Windows is the one change, and it is the file system's.
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, Runnable);
        return file;
    }

    /// <summary>The CRC-32 a zip and a gzip state for what they hold. The CLI's <c>crc32</c>.</summary>
    internal static uint Crc32(ReadOnlySpan<byte> data, uint previous = 0)
    {
        var c = previous ^ 0xffffffff;
        foreach (var value in data) c = CrcTable[(c ^ value) & 0xff] ^ (c >> 8);
        return c ^ 0xffffffff;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }

    /// <summary>
    /// Where an entry lands, or null for the archive's own root; refused when that is outside. The CLI's
    /// <c>placed</c>, in <c>zipfile.ts</c> and <c>tarball.ts</c>.
    /// </summary>
    internal static string? Placed(string name, string root)
    {
        if (Regex.IsMatch(name, @"^[\\/]") || Regex.IsMatch(name, "^[A-Za-z]:"))
        {
            throw new ToolRefusal("absolute", $"the archive names `{name}`, an absolute path — an entry lands inside the folder it is "
                + "unpacked into or nowhere");
        }

        var segments = Regex.Split(name, @"[\\/]+").Where(segment => segment is not ("" or ".")).ToArray();
        if (segments.Contains("..", StringComparer.Ordinal))
        {
            throw new ToolRefusal("outside", $"the archive names `{name}`, which would land outside the folder it is unpacked into");
        }
        if (segments.Any(segment => segment.Contains(':', StringComparison.Ordinal)))
        {
            throw new ToolRefusal("stream", $"the archive names `{name}`, which Windows would read as a stream of another file");
        }
        if (segments.Length == 0) return null;

        var path = Path.Combine([root, .. segments]);
        if (!Path.GetFullPath(path).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ToolRefusal("outside", $"the archive names `{name}`, which would land outside the folder it is unpacked into");
        }

        return path;
    }

    // ── zip ────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>One entry of the central directory, as the archive states it.</summary>
    private sealed record ZipEntry(string Name, int Flags, int Method, uint Crc, long Compressed, long Size, long Offset, int Mode);

    private static void Unzip(string archive, string into)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(into));
        Directory.CreateDirectory(root);
        using var file = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read, 81920);

        foreach (var entry in ZipDirectory(file))
        {
            var target = Placed(entry.Name, root);
            if ((entry.Mode & UnixType) == UnixLink)
            {
                throw new ToolRefusal("link", $"the archive holds a symbolic link at `{entry.Name}`, and Daoris creates no link from "
                    + "an archive — a link is a name that can point anywhere");
            }
            if ((entry.Flags & 1) != 0)
            {
                throw new ToolRefusal("encrypted", $"the archive holds `{entry.Name}` encrypted, and an entry nothing can check is not unpacked");
            }
            if (entry.Method is not (0 or 8))
            {
                throw new ToolRefusal("method", $"the archive holds `{entry.Name}` compressed by method {entry.Method}, and only stored "
                    + "and deflate are unpacked");
            }
            if (target is null) continue;

            var start = DataStart(file, entry);
            if (entry.Name.EndsWith('/') || entry.Name.EndsWith('\\'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            UnzipEntry(file, entry, start, target);
            if (!OperatingSystem.IsWindows() && entry.Mode != 0) File.SetUnixFileMode(target, (entry.Mode & 0x49) != 0 ? Runnable : Plain);
        }
    }

    /// <summary>The central directory's entries, in order, from the end record (and zip64's, where it says so).</summary>
    private static List<ZipEntry> ZipDirectory(FileStream file)
    {
        var size = file.Length;
        var tailLength = (int)Math.Min(size, EndSearch);
        var tail = ReadAt(file, size - tailLength, tailLength);

        var at = -1;
        for (var i = tail.Length - 22; i >= 0; i--)
        {
            if (U32(tail, i) == EndRecord && i + 22 + U16(tail, i + 20) == tail.Length)
            {
                at = i;
                break;
            }
        }

        if (at < 0)
        {
            throw new ToolRefusal("truncated", "the archive has no end record — it is truncated or damaged, and a partial archive is not "
                + "unpacked as if it were whole");
        }

        var endAt = size - tailLength + at;
        if (U16(tail, at + 4) != 0 || U16(tail, at + 6) != 0)
        {
            throw new ToolRefusal("damaged", "the archive is split across disks, which a tool's archive never is");
        }

        long count = U16(tail, at + 10);
        long length = U32(tail, at + 12);
        long offset = U32(tail, at + 16);
        var limit = endAt;

        if (count == 0xffff || length == 0xffffffff || offset == 0xffffffff)
        {
            var locatorAt = endAt - 20;
            if (locatorAt < 0 || U32(ReadAt(file, locatorAt, 4), 0) != Zip64Locator)
            {
                throw new ToolRefusal("damaged", "the archive's end record points to a zip64 record it does not carry");
            }

            var zip64At = Whole(BinaryPrimitives.ReadUInt64LittleEndian(ReadAt(file, locatorAt + 8, 8)));
            if (zip64At + 56 > locatorAt) throw new ToolRefusal("truncated", "the archive's zip64 record runs past where it ends");
            var record = ReadAt(file, zip64At, 56);
            if (U32(record, 0) != Zip64End) throw new ToolRefusal("damaged", "the archive's zip64 record is not one");
            count = Whole(BinaryPrimitives.ReadUInt64LittleEndian(record.AsSpan(32)));
            length = Whole(BinaryPrimitives.ReadUInt64LittleEndian(record.AsSpan(40)));
            offset = Whole(BinaryPrimitives.ReadUInt64LittleEndian(record.AsSpan(48)));
            limit = zip64At;
        }

        if (offset + length > limit)
        {
            throw new ToolRefusal("truncated", "the archive's directory runs past where it ends — it is truncated or damaged");
        }

        var records = ReadAt(file, offset, checked((int)length));
        var entries = new List<ZipEntry>();
        var p = 0;
        for (long n = 0; n < count; n++)
        {
            if (p + 46 > records.Length || U32(records, p) != DirectoryEntry)
            {
                throw new ToolRefusal("damaged", "the archive's directory holds a record that is not an entry");
            }

            var nameLength = U16(records, p + 28);
            var extraLength = U16(records, p + 30);
            var commentLength = U16(records, p + 32);
            if (p + 46 + nameLength + extraLength + commentLength > records.Length)
            {
                throw new ToolRefusal("damaged", "the archive's directory holds an entry that runs past it");
            }

            var name = Encoding.UTF8.GetString(records, p + 46, nameLength);
            long compressed = U32(records, p + 20);
            long entrySize = U32(records, p + 24);
            long entryOffset = U32(records, p + 42);
            Zip64Fields(records.AsSpan(p + 46 + nameLength, extraLength), name, ref entrySize, ref compressed, ref entryOffset);
            entries.Add(new ZipEntry(
                name, U16(records, p + 8), U16(records, p + 10), U32(records, p + 16), compressed, entrySize, entryOffset,
                (int)(U32(records, p + 38) >> 16)));
            p += 46 + nameLength + extraLength + commentLength;
        }

        return entries;
    }

    /// <summary>Zip64's extended information (0x0001): the fields its directory entry left at their limit, in order.</summary>
    private static void Zip64Fields(ReadOnlySpan<byte> extra, string name, ref long size, ref long compressed, ref long offset)
    {
        var p = 0;
        while (p + 4 <= extra.Length)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(extra[p..]);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(extra[(p + 2)..]);
            if (id == 0x0001)
            {
                var q = p + 4;
                var end = Math.Min(extra.Length, p + 4 + length);
                long Next(ReadOnlySpan<byte> from)
                {
                    if (q + 8 > end) throw new ToolRefusal("damaged", $"the archive's zip64 fields for `{name}` are short");
                    var value = Whole(BinaryPrimitives.ReadUInt64LittleEndian(from[q..]));
                    q += 8;
                    return value;
                }

                if (size == 0xffffffff) size = Next(extra);
                if (compressed == 0xffffffff) compressed = Next(extra);
                if (offset == 0xffffffff) offset = Next(extra);
                return;
            }

            p += 4 + length;
        }
    }

    /// <summary>Where an entry's bytes begin: after its local record, whose name and extra may differ from the directory's.</summary>
    private static long DataStart(FileStream file, ZipEntry entry)
    {
        if (entry.Offset + 30 > file.Length) throw new ToolRefusal("truncated", $"the archive ends before `{entry.Name}` does");
        var local = ReadAt(file, entry.Offset, 30);
        if (U32(local, 0) != LocalEntry)
        {
            throw new ToolRefusal("damaged", $"the archive's record for `{entry.Name}` is not where its directory says");
        }

        var start = entry.Offset + 30 + U16(local, 26) + U16(local, 28);
        if (start + entry.Compressed > file.Length) throw new ToolRefusal("truncated", $"the archive ends before `{entry.Name}` does");
        return start;
    }

    /// <summary>One entry's bytes to <paramref name="to"/>, inflated where deflated, held to the CRC-32 and size it states.</summary>
    private static void UnzipEntry(FileStream file, ZipEntry entry, long start, string to)
    {
        file.Seek(start, SeekOrigin.Begin);
        using var slice = new Slice(file, entry.Compressed);
        using var source = entry.Method == 8 ? new DeflateStream(slice, CompressionMode.Decompress, leaveOpen: true) : (Stream)slice;

        uint crc = 0;
        long length = 0;
        using (var target = File.Create(to))
        {
            var buffer = new byte[81920];
            int read;
            try
            {
                while ((read = source.Read(buffer)) > 0)
                {
                    crc = Crc32(buffer.AsSpan(0, read), crc);
                    length += read;
                    target.Write(buffer, 0, read);
                }
            }
            catch (InvalidDataException error)
            {
                throw new ToolRefusal("damaged", $"the archive's `{entry.Name}` did not inflate ({error.Message})");
            }
        }

        if (length != entry.Size || crc != entry.Crc)
        {
            throw new ToolRefusal("checksum", $"the archive's `{entry.Name}` fails its own check — {length} bytes with CRC-32 "
                + $"{crc:x8}, where it states {entry.Size} with {entry.Crc:x8}");
        }
    }

    private static byte[] ReadAt(FileStream file, long position, int length)
    {
        var buffer = new byte[length];
        file.Seek(position, SeekOrigin.Begin);
        if (!Fill(file, buffer)) throw new ToolRefusal("truncated", "the archive ends before a record it names");
        return buffer;
    }

    /// <summary>A 64-bit field as a number, refused where the CLI's number could not hold it exactly.</summary>
    private static long Whole(ulong value) =>
        value <= 9007199254740991UL ? (long)value : throw new ToolRefusal("damaged", "the archive names an offset no file has");

    private static int U16(byte[] bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at));

    private static uint U32(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));

    // ── tar.gz ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static void UntarGz(string archive, string into)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(into));
        Directory.CreateDirectory(root);
        using var file = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read, 81920);

        uint crc;
        long length;
        try
        {
            using var gzip = new GZipStream(file, CompressionMode.Decompress, leaveOpen: true);
            using var counted = new Counted(gzip);
            ReadTar(counted, root);
            // What follows the end blocks is padding; the trailer that vouches for all of it is read below.
            counted.CopyTo(Stream.Null);
            (crc, length) = (counted.Crc, counted.Total);
        }
        catch (InvalidDataException error)
        {
            throw new ToolRefusal("damaged", $"the package is damaged — it did not decompress ({error.Message})");
        }

        // GZipStream reads a gzip cut before its trailer as if whole, so the trailer is held here: the CRC-32 and
        // the size of everything it holds, in its last eight bytes.
        if (file.Length < 18) throw new ToolRefusal("truncated", "the package ends before its gzip trailer does — it is truncated");
        var trailer = ReadAt(file, file.Length - 8, 8);
        if (U32(trailer, 0) != crc || U32(trailer, 4) != (uint)length)
        {
            throw new ToolRefusal("truncated", "the package ends before its gzip trailer does — it is truncated");
        }
    }

    /// <summary>A tar, entry by entry: the CLI's <c>extractTarGz</c>, and its rules, read the same way.</summary>
    private static void ReadTar(Stream tar, string root)
    {
        var header = new byte[512];
        string? nextPath = null;
        long? nextSize = null;

        while (true)
        {
            if (!Fill(tar, header)) throw TarTruncated();
            if (header.All(value => value == 0)) return;
            TarChecksum(header);

            var type = (char)header[156];
            var size = nextSize ?? TarSize(header);
            var padding = (512 - size % 512) % 512;
            var raw = nextPath ?? TarName(header);
            (nextPath, nextSize) = (null, null);

            switch (type)
            {
                case 'x' or 'L' or 'K' or 'g':
                    if (size > MetadataLimit) throw new ToolRefusal("damaged", $"the package holds a {size}-byte name, which is not a name");
                    var body = new byte[size];
                    if (!Fill(tar, body)) throw TarTruncated();
                    Skip(tar, padding);
                    Absorb(type, body, ref nextPath, ref nextSize);
                    break;

                case '0' or '\0' or '7':
                    var target = Placed(raw, root);
                    if (target is null)
                    {
                        Skip(tar, size + padding);
                        break;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    using (var output = File.Create(target)) Copy(tar, output, size);
                    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(target, (TarOctal(header, 100, 8) & 0x49) != 0 ? Runnable : Plain);
                    Skip(tar, padding);
                    break;

                case '5':
                    if (Placed(raw, root) is { } folder) Directory.CreateDirectory(folder);
                    Skip(tar, size + padding);
                    break;

                case '1' or '2':
                    throw new ToolRefusal("link", $"the package holds a {(type == '2' ? "symbolic" : "hard")} link at `{raw}`, and Daoris "
                        + "creates no link from an archive — a link is a name that can point anywhere");

                default:
                    throw new ToolRefusal("entry", $"the package holds `{raw}` as an entry of type '{type}', which is not a file or a directory");
            }
        }
    }

    private static ToolRefusal TarTruncated() =>
        new("truncated", "the package ends before its last entry does — it is truncated or damaged, and a partial package is not "
            + "unpacked as if it were whole");

    private static void TarChecksum(byte[] header)
    {
        var stated = TarOctal(header, 148, 8);
        long sum = 0;
        for (var at = 0; at < 512; at++) sum += at is >= 148 and < 156 ? 0x20 : header[at];
        if (sum != stated)
        {
            throw new ToolRefusal("checksum", "the package holds a header that fails its own checksum — it is damaged, and a tarball is "
                + "not guessed at");
        }
    }

    /// <summary>What an extended header says about the entry after it: its name, and possibly its size.</summary>
    private static void Absorb(char type, byte[] body, ref string? path, ref long? size)
    {
        if (type == 'L')
        {
            path = Encoding.UTF8.GetString(body).TrimEnd('\0');
            return;
        }

        // A link target (`K`) or a global header (`g`) says nothing used here.
        if (type != 'x') return;

        var text = Encoding.UTF8.GetString(body);
        var at = 0;
        while (at < text.Length)
        {
            var space = text.IndexOf(' ', at);
            if (space < 0 || !int.TryParse(text.AsSpan(at, space - at), out var length) || length <= 0 || at + length > text.Length)
            {
                throw new ToolRefusal("damaged", "the package holds an unreadable extended header");
            }

            var record = text[(space + 1)..(at + length - 1)];
            var equals = record.IndexOf('=');
            var key = equals < 0 ? record : record[..equals];
            var value = equals < 0 ? "" : record[(equals + 1)..];
            if (key == "path") path = value;
            if (key == "size" && long.TryParse(value, out var stated)) size = stated;
            at += length;
        }
    }

    private static string TarName(byte[] header)
    {
        var name = TarText(header, 0, 100);
        // ustar splits a long name across a prefix and the name field.
        var prefix = Encoding.Latin1.GetString(header, 257, 5) == "ustar" ? TarText(header, 345, 155) : "";
        return prefix.Length > 0 ? $"{prefix}/{name}" : name;
    }

    private static long TarSize(byte[] header)
    {
        // GNU's base-256 form, for sizes the octal field cannot hold.
        if ((header[124] & 0x80) != 0)
        {
            long value = 0;
            for (var at = 125; at < 136; at++) value = value * 256 + header[at];
            return value;
        }

        return TarOctal(header, 124, 12);
    }

    private static long TarOctal(byte[] header, int at, int length)
    {
        var digits = TarText(header, at, length).Trim();
        long value = 0;
        foreach (var digit in digits)
        {
            if (digit is < '0' or > '7') break;
            value = value * 8 + (digit - '0');
        }

        return value;
    }

    private static string TarText(byte[] header, int at, int length)
    {
        var field = header.AsSpan(at, length);
        var end = field.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? field : field[..end]);
    }

    /// <summary>Fill the buffer from the stream; false when the stream ends first.</summary>
    private static bool Fill(Stream stream, byte[] buffer)
    {
        var done = 0;
        while (done < buffer.Length)
        {
            var read = stream.Read(buffer, done, buffer.Length - done);
            if (read == 0) return false;
            done += read;
        }

        return true;
    }

    private static void Skip(Stream stream, long count) => Copy(stream, Stream.Null, count);

    private static void Copy(Stream from, Stream to, long count)
    {
        var buffer = new byte[81920];
        while (count > 0)
        {
            var read = from.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0) throw TarTruncated();
            to.Write(buffer, 0, read);
            count -= read;
        }
    }

    /// <summary>The next <c>length</c> bytes of a stream, read and never disposed with it.</summary>
    private sealed class Slice(Stream inner, long length) : Stream
    {
        private long _left = length;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_left <= 0) return 0;
            var read = inner.Read(buffer, offset, (int)Math.Min(count, _left));
            _left -= read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A stream read through, counting its bytes and their CRC-32 as they pass.</summary>
    private sealed class Counted(Stream inner) : Stream
    {
        public uint Crc { get; private set; }

        /// <summary>How many bytes have passed.</summary>
        public long Total { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            Crc = Crc32(buffer.AsSpan(offset, read), Crc);
            Total += read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
