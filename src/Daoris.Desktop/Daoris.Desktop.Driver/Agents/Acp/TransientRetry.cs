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

    /// <summary>
    /// Told as each wait begins, with the moment it ends and the next try goes (AGENTERRORS1a2): the quiet <c>agent.transient</c> event
    /// the session's head reads for its <i>Trying again at</i>.
    /// </summary>
    public Action<TransientEpisode, DateTimeOffset>? WaitsUntil { get; init; }

    /// <summary>The go-on's words, with the provider's own sentence (<c>retry.go-on</c>): the agent's word, English only, as <c>limit.reset</c> is.</summary>
    /// <remarks>
    /// Its last clause is the guarantee's other half (Codex's owed read of D210, finding 9): Daoris never replays the original prompt or
    /// an accepted word, but an effect whose result the failure cut off may or may not have landed, so the agent checks before it
    /// repeats one.
    /// </remarks>
    public static string GoOnText(string? words) =>
        $"The last request failed on the provider's side ({words ?? "no words"}). Go on where you stopped, after anything said above. "
        + "Before you repeat a step whose result you did not see, check whether it already took effect.";

    /// <summary>The conversation's one line (§8's <c>agent.transient-retrying</c>), the schedule's tries and minutes said as <i>up to</i>.</summary>
    public static Noted Retrying(string agent, string? words, DateTimeOffset next)
    {
        var minutes = (int)TransientRetry.Delays.Sum(delay => delay.TotalMinutes);
        var line = Noted.Of(
            NoteCodes.AgentTransientRetrying,
            $"{agent}'s provider failed. Daoris tries again in this conversation: up to {TransientRetry.Bound} times, after {minutes} "
            + $"minutes of waits between them, the next at {next:yyyy-MM-dd HH:mm} UTC:",
            ("agent", agent), ("count", TransientRetry.Bound), ("minutes", minutes),
            ("at", next.UtcDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture)));
        return words is { Length: > 0 } said ? line.Then(" ", Noted.Said(said, NoteBy.Program)) : line;
    }

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
/// The words a transient episode holds, waits on and carries (AGENTERRORS1a, AGENTERRORS1b): a driven run's inbox
/// (<see cref="DrivenInbox"/>) or a conversation's turns (<see cref="ChatTurns"/>). The episode is the session's own, so the one seat
/// serves both; what differs is where the person's words wait.
/// </summary>
internal interface IEpisodeWords
{
    /// <summary>From the episode's first failure and each try's: every word is held, none goes alone, so the turns to settle are only those already written.</summary>
    void Settling();

    /// <summary>A wait begins: completes once a word that makes a turn is held, at once where one already is.</summary>
    Task Waiting();

    /// <summary>The wait is over, a try is on the wire: a word said from now goes as it did.</summary>
    void Waited();

    /// <summary>The answer of the oldest word already on the wire and not yet settled, or null where none is.</summary>
    Task<string>? TakeSent();

    /// <summary>Every held word a try carries, in the order said, each dispatched as its door dispatches one.</summary>
    IReadOnlyList<ChatMessage> TakeHeld();
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

    /// <summary>The tries made so far, a failed settling turn among them; never past <see cref="TransientRetry.Bound"/>.</summary>
    public int Tries { get; internal set; }

    /// <summary>
    /// The turns already on the wire that failed while the wire settled after the tries were spent (Codex's owed read of D210, finding 2):
    /// written before the episode could hold them, so each runs its own turn, and none counts a try past the bound or authorizes one.
    /// </summary>
    public int SettledPast { get; internal set; }

    /// <summary>The time actually waited between tries, read off the handed clock.</summary>
    public TimeSpan Waited { get; internal set; }

    /// <summary>Whether the run ended past the bound (<c>ended.transient</c>).</summary>
    public bool Exhausted { get; internal set; }

    /// <summary>
    /// Whether it opened at the open (AGENTERRORS1a2, §5.6): the opening operation was refused, so nothing was prompted. Past the bound, its
    /// end is <i>nothing prompted</i>: Daoris's go-on comes off by its id, and the person's carried words wait for a word of theirs said
    /// after the end, by the hold's word rule's time, with no mark written.
    /// </summary>
    public bool AtOpen { get; internal set; }

    /// <summary>The go-on as its dispatcher kept it, once its first try was written; null before, and where it was kept nowhere.</summary>
    public string? GoOnId { get; internal set; }
}

/// <summary>
/// A run that ended past its transient bound (§5.1): its provider kept failing after every try, so the record ends <c>failed</c> with
/// <c>ended.transient</c>, no strike, and the planner holds its quest for the person.
/// </summary>
/// <summary>
/// Shape 2 (AGENTERRORS1a2, the design §0, §4.2): a word the person said during a turn was taken, and its own prompt was then refused, so
/// the turn that failed is the one the word opened. Carried from the word's send to the run's loop, which reads <see cref="Refusal"/> as
/// any prompt site's failure; never past the run.
/// </summary>
public sealed class AcpWordTurnFailed(AcpRefusal refusal) : DriverException(refusal.Message)
{
    /// <summary>The refusal that answered the word's prompt, as the door said it.</summary>
    public AcpRefusal Refusal { get; } = refusal;
}

public sealed class AcpTransientExhausted(TransientEpisode episode)
    : DriverException($"the ACP agent's provider kept failing after {episode.Tries} tries: {episode.Words}")
{
    public TransientEpisode Episode { get; } = episode;
}
