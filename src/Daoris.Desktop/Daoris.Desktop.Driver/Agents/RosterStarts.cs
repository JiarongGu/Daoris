namespace Daoris.Driver;

/// <summary>
/// The numbered starts in flight: which account each reserved, and the reservation given back.
/// </summary>
internal sealed class RosterStarts(HarnessRoster roster)
{
    private long _numbered;

    /// <summary>
    /// The number of the last start chosen: taken by the driver before it reads a look's records, so <see cref="Look"/>
    /// keeps every start chosen while they were read.
    /// </summary>
    public long Mark()
    {
        lock (roster._load) return _numbered;
    }

    /// <summary>
    /// The driver's look read this machine's session records: what the walk counts from now on, with the starts
    /// chosen after <paramref name="mark"/>. A look begins only once the last look's starts have opened their records, so
    /// every start chosen before it is one of them.
    /// </summary>
    public void Look(IReadOnlyList<SessionStarted> sessions, long mark)
    {
        var owned = sessions.Select(session => (OwnerOf(session.Adapter), session)).ToList();
        lock (roster._load)
        {
            roster._looked = owned;
            roster._chosen.RemoveAll(start => start.Number <= mark);
        }
    }

    /// <summary>A start chosen on an account: one of its sessions until the next look reads the records. Its number, to take it back.</summary>
    internal long Chose(string owner, string account)
    {
        lock (roster._load)
        {
            roster._chosen.Add((++_numbered, owner, account));
            return _numbered;
        }
    }

    /// <summary>A start counted on an account that does not run there after all: no longer one of its sessions.</summary>
    internal void Unchose(long number)
    {
        lock (roster._load) roster._chosen.RemoveAll(start => start.Number == number);
    }

    /// <summary>
    /// A start's one count given back after its opened process has stopped and before any replacement is chosen:
    /// idempotent, both halves under the count's lock. The start's number is taken back (nothing once a look has
    /// covered it), and where <paramref name="session"/> is given, a look's reading of that record is made not running and out of the
    /// last started, so the refusing account is neither busy nor the start just made.
    /// </summary>
    /// <param name="number">The start's count, as its selection carries it; 0 for none.</param>
    /// <param name="session">The record it opened, or null where none opened.</param>
    public void Release(long number, string? session = null)
    {
        if (number > 0) roster._startLedger?.ReleaseBound(number);
        lock (roster._load)
        {
            if (number > 0) roster._chosen.RemoveAll(start => start.Number == number);
            if (session is null) return;
            roster._looked =
            [
                .. roster._looked.Select(each => string.Equals(each.Session.Id, session, StringComparison.Ordinal)
                    ? (each.Owner, each.Session with { Running = false, NotOffered = true })
                    : each),
            ];
        }
    }

    /// <summary>Whose accounts a session on <paramref name="adapter"/> ran as: its owner, or the adapter where it is unknown.</summary>
    private string OwnerOf(string adapter)
    {
        try
        {
            return roster.Adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase) && roster.Adapters.Door(adapter) is { } resolved
                ? roster.Adapters.OwnerOfAgent(resolved.Id.Value).Value
                : adapter;
        }
        catch (DriverException)
        {
            return adapter;
        }
    }
}
