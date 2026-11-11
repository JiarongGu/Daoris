namespace Daoris.Driver;

/// <summary>
/// The second check hooked to live conversations: each <c>turn</c> event the record appends is handed to <see cref="AskDaorisTurnEnds"/>,
/// which returns at once, so no turn, press or start waits on a selection; on its own task a turn of a session with no Ask Daoris
/// receipt (<see cref="HelpWired"/>) is passed over, and the start's catch-up runs ahead of every end.
/// </summary>
public sealed class AskDaorisWatch : IDisposable
{
    private readonly ISessionEvents _events;
    private readonly AskDaorisTurnEnds _ends;

    /// <param name="catchUp">The start's catch-up, run once ahead of every turn's end.</param>
    /// <param name="select">The selection of one Ask Daoris conversation whose turn ended.</param>
    public AskDaorisWatch(
        string home, ISessionEvents events, Func<CancellationToken, Task> catchUp, Func<string, CancellationToken, Task> select,
        Action<string, Exception>? failed = null)
    {
        _events = events;
        _ends = new AskDaorisTurnEnds((session, ct) => Conversation(home, session) ? select(session, ct) : Task.CompletedTask, failed, catchUp);
        events.Evented += Heard;
    }

    /// <summary>The watch over <paramref name="home"/>'s conversations, selecting with its presses and its answered holds.</summary>
    public static AskDaorisWatch Of(
        string home, SessionEvents events, Func<string, IReadOnlyList<TurnPress>> presses, Func<string, IReadOnlySet<string>> held,
        Func<DateTimeOffset> clock, Action<string, Exception>? failed = null)
    {
        var check = new AskDaorisSecondCheck(home, events, presses, held, clock);
        return new(home, events, ct => check.CatchUpAsync(Conversations(home), ct), check.EndedAsync, failed);
    }

    /// <summary>Complete once the start's catch-up has run.</summary>
    public Task CaughtUp => _ends.First;

    /// <summary>Whether <paramref name="session"/> is an Ask Daoris conversation: one whose start kept a receipt.</summary>
    public static bool Conversation(string home, string session) => new HelpWired(home).Read(session).Count > 0;

    /// <summary>Every Ask Daoris conversation of the home, by its receipts.</summary>
    private static IEnumerable<string> Conversations(string home)
    {
        var sessions = DaorisHome.Entry(home, HomeEntry.Sessions);
        if (!Directory.Exists(sessions)) return [];
        return Directory.EnumerateFiles(sessions, "*" + HelpWired.Suffix).Select(Path.GetFileName).OfType<string>()
            .Select(name => name[..^HelpWired.Suffix.Length]).Where(SessionEventText.IsId).ToList();
    }

    private void Heard(string session, SessionEvent e)
    {
        if (e.Kind == SessionEventKind.Turn) _ends.Ended(session);
    }

    public void Dispose()
    {
        _events.Evented -= Heard;
        _ends.Dispose();
    }
}
