namespace Daoris.Knowledge;

/// <summary>
/// The anchors one file's entries are given, each unique within the file, so no two of its entries share an id
/// (REV3, ORIENT2h3).
/// </summary>
/// <remarks>
/// <para>A title is its own anchor the first time and the title and its count after (<c>A</c>, <c>A (2)</c>), the
/// rule every reader that splits a file has used since REV3. 🔴 Counting titles is not reserving anchors: a title that
/// is literally another title's counted anchor, <c>A</c>, <c>A</c>, <c>A (2)</c>, gave <c>A (2)</c> twice, and the
/// store's primary key failed the whole refresh on the second. So each anchor given is reserved, and a title whose
/// anchor is already taken counts on to the next free one (<c>A (2) (2)</c>).</para>
///
/// <para>Where every anchor was unique, each is what it was, since a title's first try is the anchor the count gave:
/// an id is how a hit is fetched again and how a refresh replaces an entry rather than adding one.</para>
///
/// <para>One instance per file, since an id is the repository, the path and the anchor: two files never share one.</para>
/// </remarks>
internal sealed class EntryAnchors
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    /// <summary>
    /// The anchor of the next entry titled <paramref name="title"/>, reserved: the title the first time, the title and
    /// its count after, and past an anchor already given, the next count free.
    /// </summary>
    public string Next(string title)
    {
        var count = _counts.GetValueOrDefault(title) + 1;
        var anchor = count == 1 ? title : $"{title} ({count})";
        while (!_taken.Add(anchor))
        {
            count++;
            anchor = $"{title} ({count})";
        }
        _counts[title] = count;
        return anchor;
    }
}
