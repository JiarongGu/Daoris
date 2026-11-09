using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// One answer of <c>git cat-file --batch</c> to one question, read by this one parser: the header <c>&lt;id&gt; &lt;type&gt; &lt;size&gt;</c>
/// and the bytes after it, or <c>&lt;name&gt; missing</c>. The size is the header's, the object's whole size however many of its bytes
/// were read, so nothing is decided on a listing's size (with lazy fetch off, <c>ls-tree -l</c> prints a filtered blob's as <c>BAD</c>
/// and exits 0).
/// </summary>
/// <param name="Missing">Git holds no object of the name asked: a filtered blob of a partial clone among them.</param>
/// <param name="Type">The object's type; empty where it is missing.</param>
/// <param name="Size">Its whole size in bytes; 0 where it is missing.</param>
/// <param name="Start">Where its bytes begin in the bytes read.</param>
/// <param name="Count">How many of its bytes the read holds.</param>
internal sealed record GitObjectAnswer(bool Missing, string Type, long Size, int Start, int Count)
{
    /// <summary>How long a found object's header may be: an id of 64, the longest type and a size, with room to spare. A missing one echoes its question beside it.</summary>
    internal const int HeaderBound = 128;

    internal static int HeaderRoom(string question) => HeaderBound + Encoding.UTF8.GetByteCount(question);

    /// <summary>The answer at <paramref name="at"/> in the first <paramref name="count"/> of <paramref name="bytes"/>; null where git gives none such.</summary>
    internal static GitObjectAnswer? Parse(byte[] bytes, int count, int at = 0)
    {
        var end = at < count ? Array.IndexOf(bytes, (byte)'\n', at, Math.Min(count, bytes.Length) - at) : -1;
        if (end < 0) return null;
        var header = Encoding.UTF8.GetString(bytes, at, end - at);
        if (header.EndsWith(" missing", StringComparison.Ordinal)) return new GitObjectAnswer(true, "", 0, end + 1, 0);
        if (header.Split(' ') is not [{ Length: > 0 }, { Length: > 0 } type, var sized]
            || !long.TryParse(sized, NumberStyles.None, CultureInfo.InvariantCulture, out var size))
        {
            return null;
        }

        var start = end + 1;
        return new GitObjectAnswer(false, type, size, start, (int)Math.Min(size, Math.Max(0, count - start)));
    }
}
