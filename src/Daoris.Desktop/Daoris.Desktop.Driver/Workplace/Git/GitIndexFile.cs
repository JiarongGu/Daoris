using System.Buffers.Binary;
using System.Text;

namespace Daoris.Driver;

/// <summary>One entry of a tree's index at stage 0: its path, mode and blob id, and the stat git recorded for it.</summary>
/// <param name="Path">The path as git stores it, slashes between its parts.</param>
/// <param name="Mode">The entry's mode: <c>0o100644</c> or <c>0o100755</c>.</param>
/// <param name="Id">The blob's full id.</param>
/// <param name="Size">The size git recorded, cut to 32 bits as git cuts it; zero where git smudged a racily clean entry.</param>
/// <param name="Seconds">The write time's seconds git recorded.</param>
/// <param name="Nanoseconds">The write time's nanoseconds git recorded.</param>
/// <param name="ChangedSeconds">The ctime's seconds git recorded: on Windows, the file's creation time.</param>
/// <param name="ChangedNanoseconds">The ctime's nanoseconds git recorded.</param>
internal sealed record GitIndexEntry(string Path, int Mode, string Id, uint Size, uint Seconds, uint Nanoseconds, uint ChangedSeconds, uint ChangedNanoseconds);

/// <summary>
/// A tree's index read from its file as git writes it, nothing started and nothing written (versions 2 and 3, SHA-1 ids): the entries
/// and the file's own write time, which says which entries git would call racy. Null with a word where an entry or an extension asks
/// what this reader does not say: a gitlink (a submodule), a symbolic link, an unmerged stage, an assume-unchanged, skip-worktree
/// (a sparse tree) or intent-to-add entry, a split or sparse index, version 4's compressed names, or a file it cannot read whole.
/// </summary>
internal static class GitIndexFile
{
    internal const int Regular = 0x81A4; // 0o100644
    internal const int Executable = 0x81ED; // 0o100755

    private const int Fixed = 62;
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The index at <paramref name="path"/>: its entries and write time, or the word that sends the read to git.</summary>
    internal static (IReadOnlyList<GitIndexEntry> Entries, DateTime Written)? Read(string path, out string? refusal)
    {
        refusal = null;
        byte[] bytes;
        DateTime written;
        try
        {
            if (!File.Exists(path)) return ([], DateTime.MinValue);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                written = File.GetLastWriteTimeUtc(path);
                bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            refusal = "index-unread";
            return null;
        }

        return Parse(bytes, written, out refusal);
    }

    internal static (IReadOnlyList<GitIndexEntry> Entries, DateTime Written)? Parse(byte[] bytes, DateTime written, out string? refusal)
    {
        refusal = "index-shape";
        if (bytes.Length < 12 + 20 || bytes[0] != 'D' || bytes[1] != 'I' || bytes[2] != 'R' || bytes[3] != 'C') return null;
        var version = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4));
        if (version is not (2 or 3))
        {
            refusal = "index-version";
            return null;
        }

        var count = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8));
        var end = bytes.Length - 20;
        var entries = new List<GitIndexEntry>((int)Math.Min(count, 1 << 20));
        var at = 12;
        for (var each = 0u; each < count; each++)
        {
            if (at + Fixed > end) return null;
            var span = bytes.AsSpan(at);
            var mode = (int)BinaryPrimitives.ReadUInt32BigEndian(span[24..]);
            var flags = BinaryPrimitives.ReadUInt16BigEndian(span[60..]);
            var extended = (flags & 0x4000) != 0;
            var named = at + Fixed + (extended ? 2 : 0);
            var nul = Array.IndexOf(bytes, (byte)0, named, end - named);
            if (nul < 0) return null;
            string name;
            try
            {
                name = Strict.GetString(bytes, named, nul - named);
            }
            catch (DecoderFallbackException)
            {
                refusal = "index-name";
                return null;
            }

            if ((flags & 0x3000) != 0)
            {
                refusal = "unmerged";
                return null;
            }

            if ((flags & 0x8000) != 0 || extended)
            {
                // Assume-unchanged, and version 3's skip-worktree (a sparse tree) and intent-to-add.
                refusal = "entry-flags";
                return null;
            }

            if (mode is not (Regular or Executable))
            {
                refusal = mode == 0xE000 ? "submodule" : "entry-mode";
                return null;
            }

            entries.Add(new GitIndexEntry(
                name, mode, Convert.ToHexStringLower(span.Slice(40, 20)), BinaryPrimitives.ReadUInt32BigEndian(span[36..]),
                BinaryPrimitives.ReadUInt32BigEndian(span[8..]), BinaryPrimitives.ReadUInt32BigEndian(span[12..]),
                BinaryPrimitives.ReadUInt32BigEndian(span), BinaryPrimitives.ReadUInt32BigEndian(span[4..])));
            // An entry is padded with one to eight NULs to a multiple of eight bytes.
            var length = Fixed + (extended ? 2 : 0) + (nul - named);
            at += (length + 8) & ~7;
        }

        while (at + 8 <= end)
        {
            var signature = Encoding.ASCII.GetString(bytes, at, 4);
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at + 4));
            if (signature is "link" or "sdir")
            {
                refusal = signature == "link" ? "split-index" : "sparse-index";
                return null;
            }

            // A required extension (its first letter not a capital) this reader does not know changes what the entries mean.
            if (signature[0] is < 'A' or > 'Z')
            {
                refusal = "index-extension";
                return null;
            }

            if (size > end - at - 8) return null;
            at += 8 + (int)size;
        }

        refusal = null;
        return (entries, written);
    }
}
