namespace Daoris.Driver;

/// <summary>
/// An ended chat going on: its record as it ended, what its resumed run carries and learns (<see cref="ResumeAsk"/>),
/// and whether its words went, which on the native door needs its line on stdin and its harness naming the conversation. The
/// runner's run takes one in and its watch tells it when the words went (<see cref="ChatGoOn.WentOnAsync"/>).
/// </summary>
internal sealed class GoingOn(PriorSession record, ResumeAsk ask)
{
    private readonly object _gate = new();
    private bool _sent;
    private bool _taken;

    public PriorSession Record => record;

    public ResumeAsk Ask => ask;

    /// <summary>
    /// The completion of its first turn where that turn answers another agent's findings:
    /// its reading frozen before another word is taken. Null for every other going-on.
    /// </summary>
    public Func<bool, Task>? Answering { get; init; }

    /// <summary>
    /// Its process's lineage token, issued as its move returned with its <c>start</c>, which the runner carries to the
    /// spawn; null for its one retry, which the runner brackets anew.
    /// </summary>
    public string? Proc { get; init; }

    public ChatLimitCarry? Carried { get; init; }

    /// <summary>The words are on the wire. True once, where they went now: at once, or where the harness already named it.</summary>
    /// <param name="named">Whether they went only once the harness has named its conversation too (the native door).</param>
    public bool Sent(bool named)
    {
        lock (_gate)
        {
            _sent = true;
            return Take(named);
        }
    }

    /// <summary>The native door's harness named its conversation. True once, where the words went now.</summary>
    public bool Named()
    {
        lock (_gate)
        {
            ask.Named = true;
            return Take(named: true);
        }
    }

    private bool Take(bool named)
    {
        if (_taken || !_sent || (named && !ask.Named)) return false;
        _taken = true;
        ask.Prompted = true;
        return true;
    }
}
