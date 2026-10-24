namespace Daoris.Driver;

/// <summary>
/// The retry a run hands its protocol door for a transient failure (AGENTERRORS1a, D210, the agent-errors design §5.1): the reader as a
/// delegate, the clock its waits are read off, and the owner's check before each try. <see cref="AcpSession"/> has no adapter, table or
/// clock of its own, so the capture hands it this; a run handed none keeps today's end, whatever the reader would say.
/// </summary>
/// <remarks>
/// <para><b>Where.</b> Inside <see cref="AcpSession.RunAsync"/>, around its prompt sites: only there is a live session left to prompt.
/// The capture's catch is too late, since by then the reader has ended and stdin is closing.</para>
///
/// <para><b>Row 1a hands one to driven quest sessions alone</b> (§5.1, round 3, R2-O6): a start's and a continuation's. Intakes,
/// helpers, opinions, partners and voices get theirs in row 1d, conversations theirs in row 1b.</para>
///
/// <para><b>What it keeps</b> once the run is over, for the conclusion: whether an episode opened, the words a failed prompt left
/// unacknowledged, the last error read, and the end past the bound. One instance serves one run.</para>
/// </remarks>
public sealed class TransientRetry
{
    /// <summary>
    /// The waits before each try (§5.1, round 2, C6): 1, 2, 4, 8 and 16 minutes, 31 minutes in all, one constant set for every door and
    /// every kind of run. The bound is the count of tries, five; a try's own time is the harness's and is not counted.
    /// </summary>
    public static IReadOnlyList<TimeSpan> Delays { get; } =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(16)];

    /// <summary>The bound: how many tries an episode makes before the run ends past it.</summary>
    public static int Bound => Delays.Count;

    /// <summary>The one reader (<see cref="AgentErrors.Read"/>), closed over the adapter set and the adapter: a prompt's refusal as its kind.</summary>
    public required Func<AcpRefusal, AgentError> Read { get; init; }

    /// <summary>The clock every wait is read off (<c>.claude/knowledge/held-clocks.md</c>): the machine's in production, a held one in a test.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>
    /// The owner's check before each try (§5.5, <c>TransientRetry.Wanted</c>): whether another turn is still wanted. Null, or one that
    /// throws, answers yes, as an unread look must never end the episode as though the work were done.
    /// </summary>
    public Func<CancellationToken, Task<bool>>? Wanted { get; init; }

    /// <summary>
    /// Keeps Daoris's go-on (<c>retry.go-on</c>) through the run's one dispatcher, once per episode before its first try (§5.1, round 3's
    /// read, R3-O5): answers the word as its dispatcher hands it, marked attempted before the wire and taken off by its id once written,
    /// or null where it was kept nowhere and goes in this process alone.
    /// </summary>
    public Func<TransientEpisode, string, CancellationToken, Task<ChatMessage?>>? KeepGoOn { get; init; }

    /// <summary>Told at an episode's first failure, with when its first try goes: the one line the conversation gains (<c>agent.transient-retrying</c>).</summary>
    public Action<TransientEpisode, DateTimeOffset>? Began { get; init; }

    /// <summary>Told as each try goes, its number from one, and when the next would, or null where it is the last: the quiet <c>agent.transient</c> event and the machine log.</summary>
    public Action<TransientEpisode, int, DateTimeOffset?>? Tried { get; init; }

    /// <summary>
    /// Told as a wait begins (true) and ends (false): the watch reads the run as waiting on Daoris's own timer, never quiet
    /// (<c>WaitsOnTransient</c>, §5.1), and a wait's end stamps the session heard (round 3, R2-O7).
    /// </summary>
    public Action<bool>? Waits { get; init; }

    /// <summary>The go-on's words, with the provider's own sentence (<c>retry.go-on</c>): the agent's word, English only, as <c>limit.reset</c> is.</summary>
    public static string GoOnText(string? words) =>
        $"The last request failed on the provider's side ({words ?? "no words"}). Go on where you stopped, after anything said above.";

    private readonly Lock _gate = new();
    private readonly List<ChatMessage> _uncertain = [];

    /// <summary>Whether an episode opened in this run: a run with none keeps today's judgement of its words, TURNFAIL1's included (§5.1).</summary>
    public bool Opened { get; private set; }

    /// <summary>The last episode, or null where none opened.</summary>
    public TransientEpisode? Episode { get; private set; }

    /// <summary>The last error the reader answered in this run, any kind: what a limit read by its code is cooled from (§3.1, §5.2).</summary>
    public AgentError? Last { get; private set; }

    /// <summary>
    /// The person's words on the wire whose prompt met the error before any update of its turn (§5.1): D188's COWORK1c uncertain words,
    /// said by the <c>lost</c> note under their ids and never sent again.
    /// </summary>
    public IReadOnlyList<ChatMessage> Uncertain
    {
        get { lock (_gate) return [.. _uncertain]; }
    }

    /// <summary>
    /// Whether the run's first prompt met its error before any update of its turn (§5.1): a resume's blocks, which carry no message on
    /// the door, are then unacknowledged rather than taken, and the conclusion says them by the <c>lost</c> note.
    /// </summary>
    public bool OpeningUnacknowledged { get; internal set; }

    internal TransientEpisode Open(AgentError first)
    {
        var episode = new TransientEpisode(Clock.GetUtcNow(), first);
        lock (_gate)
        {
            Opened = true;
            Episode = episode;
            Last = first;
        }

        return episode;
    }

    internal void Kept(AgentError read)
    {
        lock (_gate) Last = read;
    }

    internal void Unacknowledged(IEnumerable<ChatMessage> words)
    {
        lock (_gate) _uncertain.AddRange(words);
    }

    /// <summary>The owner's check, an unread one a yes (§5.5).</summary>
    internal async Task<bool> WantedAsync(CancellationToken ct)
    {
        if (Wanted is null) return true;
        try
        {
            return await Wanted(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return true;
        }
    }
}

/// <summary>
/// One transient episode (§5.1): from its first failure to a try that answers, another kind, the owner's no, or the bound. Named by when
/// its first failure was read.
/// </summary>
public sealed class TransientEpisode(DateTimeOffset began, AgentError first)
{
    /// <summary>When its first failure was read: its name (<c>DaorisKeys.TransientRetry</c>).</summary>
    public DateTimeOffset Began { get; } = began;

    /// <summary>The failure that opened it.</summary>
    public AgentError First { get; } = first;

    /// <summary>The provider's own sentence, as the first failure said it: what the line, the go-on and the end quote.</summary>
    public string? Words => First.Failure.Words;

    /// <summary>The tries made so far, a failed settling turn among them.</summary>
    public int Tries { get; internal set; }

    /// <summary>The time actually waited between tries, read off the handed clock.</summary>
    public TimeSpan Waited { get; internal set; }

    /// <summary>Whether the run ended past the bound (<c>ended.transient</c>).</summary>
    public bool Exhausted { get; internal set; }

    /// <summary>The go-on as its dispatcher kept it, once its first try was written; null before, and where it was kept nowhere.</summary>
    public string? GoOnId { get; internal set; }
}

/// <summary>
/// A run that ended past its transient bound (§5.1): its provider kept failing after every try, so the record ends <c>failed</c> with
/// <c>ended.transient</c>, no strike, and the planner holds its quest for the person.
/// </summary>
public sealed class AcpTransientExhausted(TransientEpisode episode)
    : DriverException($"the ACP agent's provider kept failing after {episode.Tries} tries: {episode.Words}")
{
    public TransientEpisode Episode { get; } = episode;
}
