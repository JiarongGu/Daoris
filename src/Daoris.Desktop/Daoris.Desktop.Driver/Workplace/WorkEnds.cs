namespace Daoris.Driver;

/// <summary>One end the end keeping wrote: the home whose work it is, the session whose work the line now holds, and its quest.</summary>
public sealed record WorkEnd(string Home, string Session, string? Quest);

/// <summary>
/// The ends the end keeping writes (D244's note on <c>work.ended</c>): whichever caller
/// asked it (link 1, the look's tidy, Accept's tidy, a gone landing's retirement), each entry it writes ended that names a session is said
/// here, each session once a pass, and the lease's hook set hears its home. Where nothing of that home listens (a verb alone, a test),
/// nothing hears it, as an event minted while no plugin listens.
/// </summary>
public static class WorkEnds
{
    private static readonly object Gate = new();

    private static readonly List<Listener> Listening = [];

    /// <summary>This process wrote these ends under <paramref name="home"/>, one per session. Never throws.</summary>
    internal static void Said(string home, IEnumerable<(string Session, string? Quest)> ends)
    {
        string full;
        try
        {
            full = Path.GetFullPath(home);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        Listener[] heard;
        lock (Gate) heard = [.. Listening.Where(each => string.Equals(each.Home, full, StringComparison.OrdinalIgnoreCase))];
        if (heard.Length == 0) return;
        foreach (var (session, quest) in ends.Where(end => end.Session.Length > 0).DistinctBy(end => end.Session, StringComparer.Ordinal))
        {
            foreach (var each in heard)
            {
                try
                {
                    each.Heard(new WorkEnd(full, session, quest));
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    // Unheard: the end is kept on its entry, which every reader reads; only the wake is lost, never the record.
                }
            }
        }
    }

    /// <summary>Hear the ends written under <paramref name="home"/> until the answer is disposed.</summary>
    public static IDisposable Listen(string home, Action<WorkEnd> heard)
    {
        var listener = new Listener(Path.GetFullPath(home), heard);
        lock (Gate) Listening.Add(listener);
        return listener;
    }

    private sealed class Listener(string home, Action<WorkEnd> heard) : IDisposable
    {
        public string Home { get; } = home;

        public Action<WorkEnd> Heard { get; } = heard;

        public void Dispose()
        {
            lock (Gate) Listening.Remove(this);
        }
    }
}
