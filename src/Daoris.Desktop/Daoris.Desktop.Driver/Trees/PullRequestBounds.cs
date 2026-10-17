using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// D148's minute per pull request, one for every occasion in this process (FABRIC-END6c, the review of FABRIC-END6): a look, the tidy and
/// a due moment each take an entry's minute before they ask it, so an entry one of them is asking, or asked within the minute, is not
/// asked by another, answered or not. The record's own last ask (<see cref="PullRequestAsking.Due"/>) still counts beside it; this one
/// also holds an ask still in flight, and a plugin not ready, which keeps nothing on the record.
/// </summary>
/// <remarks>
/// The minute counts from the time the entry's own request went out (<see cref="StateAnswer.AskedAt"/>), not from the occasion's start: frames go one
/// at a time, so a slow one before it would otherwise move the minute's start earlier. <i>Ask again</i>, a person's door, asks whatever
/// the minute says, and its ask still counts here.
/// </remarks>
public static class PullRequestMinute
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Taken = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Take the entry's minute at <paramref name="at"/>: false where it was taken within the minute before, and nothing changes.</summary>
    public static bool TryTake(string home, LandedBranch entry, DateTimeOffset at)
    {
        var key = Key(home, entry);
        while (true)
        {
            if (!Taken.TryGetValue(key, out var was))
            {
                if (Taken.TryAdd(key, at)) return true;
                continue;
            }

            if (at - was < PullRequestAsking.Fresh) return false;
            if (Taken.TryUpdate(key, at, was)) return true;
        }
    }

    /// <summary>Whether the entry's minute was taken within the minute before <paramref name="now"/>.</summary>
    public static bool Within(string home, LandedBranch entry, DateTimeOffset now) =>
        Taken.TryGetValue(Key(home, entry), out var was) && now - was < PullRequestAsking.Fresh;

    /// <summary>The entry was asked at <paramref name="at"/>, its own request's time: kept where it is the later.</summary>
    public static void Asked(string home, LandedBranch entry, DateTimeOffset at) =>
        Taken.AddOrUpdate(Key(home, entry), at, (_, was) => was > at ? was : at);

    /// <summary>A minute taken at <paramref name="at"/> whose ask never went out (the occasion's bound came first), given back; one taken since stays.</summary>
    public static void Release(string home, LandedBranch entry, DateTimeOffset at) =>
        Taken.TryRemove(new KeyValuePair<string, DateTimeOffset>(Key(home, entry), at));

    private static string Key(string home, LandedBranch entry) =>
        $"{Homes.Full(home)}|{entry.Repository}|{entry.Branch}|{entry.Session}|{entry.LandedAt:O}";
}

/// <summary>
/// A repository held, in this process, by what a due moment must not run beside (FABRIC-END6c): a removing press and bringing it up to
/// date, through the replay's lock (<see cref="TreeLock.TryReplaying"/>), and a landing (<c>TreeLands.LandAsync</c>). Holds nest; the last
/// one let go tells the moments (<see cref="PullRequestMoments.Released"/>).
/// </summary>
/// <remarks>
/// <b>A moment skips a held repository, and is owed</b>, rather than wait for it: the moments run one at a time, and a landing's plugin
/// push or a press's removals can take long enough to hold every other repository's moment behind it. A skipped moment fetches and asks
/// nothing, and the repository is asked once the hold is let go, as Daoris's push is. A press or a landing that starts while a moment
/// runs is never made to wait for it: D148 asks nothing at a press, and nothing waits on a plugin's answer there.
/// </remarks>
public static class RepositoryHolds
{
    private static readonly Dictionary<string, int> Holds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Hold the repository of <paramref name="home"/> until the answer is disposed.</summary>
    public static IDisposable Hold(string home, string repository)
    {
        var key = Key(home, repository);
        lock (Holds) Holds[key] = Holds.GetValueOrDefault(key) + 1;
        return new Holding(home, repository, key);
    }

    /// <summary>Whether something holds the repository of <paramref name="home"/> now.</summary>
    public static bool Held(string home, string repository)
    {
        lock (Holds) return Holds.ContainsKey(Key(home, repository));
    }

    private static string Key(string home, string repository) => $"{Homes.Full(home)}|{repository.Trim()}";

    private sealed class Holding(string home, string repository, string key) : IDisposable
    {
        private int _gone;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _gone, 1) == 1) return;
            bool last;
            lock (Holds)
            {
                var count = Holds.GetValueOrDefault(key) - 1;
                last = count <= 0;
                if (last) Holds.Remove(key);
                else Holds[key] = count;
            }

            if (last) PullRequestMoments.Released(home, repository);
        }
    }
}

/// <summary>
/// The window's focus kept until there is someone to tell (FABRIC-END6c): the main window can take the person's focus before the loop's
/// watch, or the watch's moments, exist, and that focus was the only one a start had. One pending flag, not a queue: any number of focuses
/// before is one moment after.
/// </summary>
public sealed class PendingFocus
{
    private int _pending;
    private volatile Action? _tell;

    /// <summary>Whether a focus waits for someone to tell.</summary>
    public bool Pending => Volatile.Read(ref _pending) == 1;

    /// <summary>The window has the person's focus: told now where someone listens, else kept. Never blocks.</summary>
    public void Focused()
    {
        Interlocked.Exchange(ref _pending, 1);
        Deliver();
    }

    /// <summary>Who is told from now on, a focus kept before told at once; null where nobody is, so the next focus is kept.</summary>
    public void Ready(Action? tell)
    {
        _tell = tell;
        Deliver();
    }

    // The flag is taken by exchange on both sides, so a focus racing the listener's arrival is told exactly once.
    private void Deliver()
    {
        if (_tell is { } tell && Interlocked.Exchange(ref _pending, 0) == 1) tell();
    }
}

/// <summary>A home's full path, as the moments key their listeners by it; the path as given where it cannot be made full.</summary>
internal static class Homes
{
    internal static string Full(string home)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return home;
        }
    }
}
