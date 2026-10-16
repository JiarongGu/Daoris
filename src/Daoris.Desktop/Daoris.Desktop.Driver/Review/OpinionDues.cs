namespace Daoris.Driver;

/// <summary>
/// The opinions owed by themselves on this machine (XAGENT1f): <c>&lt;home&gt;/opinions/due.json</c>. Written at a driven record's
/// conclusion, read and closed by the look. Machine-local (D47 §4), as the opinions are.
/// </summary>
/// <remarks>
/// What is kept, and for how long, is this use case's; the file and its format are
/// <see cref="OpinionGateFiles"/>'.
/// </remarks>
public sealed class OpinionDues(string home)
{
    public const string FileName = "due.json";

    /// <summary>How many closed entries are kept, the newest by when they closed; an open entry is never dropped.</summary>
    public const int ClosedKept = 200;

    // One writer at a time in this process: the look and a conclusion may write at once.
    private static readonly object Gate = new();

    public string FilePath => DaorisHome.Entry(home, HomeEntry.OpinionDues);

    /// <summary>Every entry, in the order they became due.</summary>
    public IReadOnlyList<OpinionDue> All() => OpinionGateFiles.ReadDues(FilePath);

    /// <summary>The entries still owed.</summary>
    public IReadOnlyList<OpinionDue> Open() => [.. All().Where(entry => entry.Closed is null)];

    /// <summary>A session's work owed an opinion: one owed before for the same session and occasion is opened again.</summary>
    public void Due(OpinionDue entry) => Edit(all =>
    {
        all.RemoveAll(each => Same(each.Session, entry.Session) && each.Occasion == entry.Occasion);
        all.Add(entry);
    });

    /// <summary>Close a session's entries for an occasion, with why.</summary>
    public void Close(string session, string occasion, string why, DateTimeOffset at) => Edit(all =>
    {
        for (var i = 0; i < all.Count; i++)
        {
            if (Same(all[i].Session, session) && all[i].Occasion == occasion && all[i].Closed is null)
            {
                all[i] = all[i] with { Closed = at, Why = why };
            }
        }
    });

    private void Edit(Action<List<OpinionDue>> change)
    {
        lock (Gate)
        {
            var all = All().ToList();
            change(all);
            var closed = all.Where(entry => entry.Closed is not null).OrderByDescending(entry => entry.Closed).Skip(ClosedKept).ToHashSet();
            all.RemoveAll(closed.Contains);
            OpinionGateFiles.WriteDues(FilePath, all);
        }
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
