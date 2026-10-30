namespace Daoris.Driver;

/// <summary>
/// The driver's carry-on of a quest's line keeps its move record (FOUNDATION1f-iv part a, D212; the foundation design §3.3, §13.4 steps 1 to 4,
/// §19.4): a line carried on to a new part, after a failure, a limit or a curve, decided before its open, written phase by phase before what each
/// fences, and ended by its phase wherever the carry-on goes no further, so a crash between the old part's end and the new part's words is taken
/// up rather than lost or sent twice. The one store and the one reading of a delivery are the live limit move's (<see cref="ConversationMoves"/>,
/// executor <c>driven</c> here); this is the driven line's use of them, over <c>Driver.RunAsync</c>.
/// </summary>
/// <remarks>
/// <para><b>From an ended record.</b> A driven carry-on goes on from a session that has ended, so its move is written at <c>old-ended</c> (§3.3:
/// a move from an ended record has no part to end), and the old part's end is no phase of its own.</para>
/// <para><b>The delivery is the door's write.</b> The moved words are read, and shown, into the ask the new part's instruction reads
/// (<see cref="DriverContinuations.HandedOnAsync"/>), and handed here with the take that follows (<see cref="Handed"/>); each word's <c>sent</c>
/// is written where the door's write of the first prompt returns (<see cref="Written"/>: the native door's stdin write, the protocol door's first
/// prompt, or a transient try's), then the take, then the reading that closes the move <c>delivered</c>. A carry-on that hands no word closes on
/// its prompt's outcome at the same point. Nothing here sends a word.</para>
/// <para><b>Ended wherever it stops.</b> The record is let go (<see cref="DisposeAsync"/>) on every way out of the start, ended by its phase
/// unless its prompt was written, so a refused open, a refused write or a start that fails leaves no standing move to hold the line, and a
/// word whose write never returned reads unsent, its take never made.</para>
/// </remarks>
internal sealed class CarryOnRecord(ConversationMove move) : IAsyncDisposable
{
    private int _settled;

    private IReadOnlyCollection<string> _transfers = [];

    private Func<Task>? _take;

    private Task? _taking;

    /// <summary>
    /// The move a carry-on decides, or null for a start that keeps none: one that continues nothing. Executor <c>driven</c>, its brief the new
    /// part's, its why and asker the record's own link, and written at <c>old-ended</c>, the part it goes on from having ended. A carry-on the
    /// quest's hand-over starts (FOUNDATION1f-v part c) goes on from the hand-over's own move where the ask decided one (<see cref="Of"/>); this is
    /// its decision where none stands, an agent's ask taken at its turn's end or an older build's take having left none.
    /// </summary>
    /// <param name="continues">The session this start continues, why and who asked (<c>Driver.ContinuesFrom</c>).</param>
    /// <param name="agent">The agent the new part opens on.</param>
    /// <param name="account">The settled selection's account, by profile name, where it is settled.</param>
    public static MoveDecision? Decision(ContinuedFrom? continues, string agent, string? account) =>
        continues is null
            ? null
            : new MoveDecision(continues.Session, MoveExecutors.Driven, MoveHows.Brief, continues.Why, continues.By)
            {
                FromEnded = true, Account = account, RecipeWhy = continues.Why, Agent = agent,
            };

    /// <summary>
    /// The record a start keeps, from what its decision came to (FOUNDATION1f-v part c; §3.3: a hand-over is one move from ask to delivery): the
    /// move the carry-on decided, or, for the carry-on the quest's hand-over starts, the hand-over's own move that stands for the session it goes
    /// on from, decided at the ask and left at <c>old-ended</c> by the look's take (or at <c>decided</c> or <c>quiescing</c> where that take was
    /// cut short, which <see cref="OldEnded"/> then writes), claimed by this run and naming the agent the start opens on. Null where the move
    /// standing is another's: a start that did not come of the hand-over, another run's claim, another agent's, or one past its open.
    /// </summary>
    /// <param name="moves">The driven holder, which answers a move as this run's executor of it.</param>
    /// <param name="begun">What the decision came to.</param>
    /// <param name="handedFrom">The start carries on from the very session the quest was handed over from.</param>
    /// <param name="agent">The agent the new part opens on.</param>
    internal static CarryOnRecord? Of(ConversationMoves moves, ConversationMoveBegun begun, bool handedFrom, string agent)
    {
        if (begun.Move is { } decided) return new CarryOnRecord(decided);
        return handedFrom
               && begun.Standing is { Agent: { } named } standing
               && ConversationMoves.DrivenBeforeOpen(standing)
               && string.Equals(named, agent, StringComparison.OrdinalIgnoreCase)
               && moves.Claimed(standing) is { } mine
            ? new CarryOnRecord(mine)
            : null;
    }

    /// <summary>The part the start goes on from has ended (§13.4 step 1): written again where the decision wrote it, and for a hand-over's move the first time; the store's refusal where it would not take it.</summary>
    public string? OldEnded() => move.OldEnded();

    /// <summary>The open asked under <paramref name="key"/>, the key its keyed open is sent with; the store's refusal where it would not take it.</summary>
    public string? Opening(string key) => move.Opening(key);

    /// <summary>
    /// The open's answer and the delivery's fence (§13.4 steps 2 to 4): <c>opened</c> with every word the open moved by its id on the new part,
    /// the line's head read before anything is taken, then <c>delivering</c> and the prompt's mark. The store's refusal where it would not take
    /// a write, or the sentence of a head an older build has moved past the new part; null where the take may be made.
    /// </summary>
    public async Task<string?> FenceAsync(string destination, IReadOnlyList<MovedWord> moved)
    {
        move.Answered();
        var fenced = move.Opened(destination, moved);
        if (fenced is null && await move.SupersededAsync().ConfigureAwait(false)) fenced = "the line went on past its new part";
        return fenced ?? move.Delivering();
    }

    /// <summary>
    /// The words the first prompt carries (<see cref="DriverContinuations.HandedOnAsync"/>), named before the door writes it: the transfers of the
    /// moved words it was handed, none for a carry-on that moved none, and the take that follows <c>sent</c> (null where nothing is to take). The
    /// take is only held here; the door's write return (<see cref="Written"/>) writes <c>sent</c> and then makes it.
    /// </summary>
    public void Handed(IReadOnlyCollection<string> transfers, Func<Task>? take)
    {
        _transfers = transfers;
        _take = take;
    }

    /// <summary>
    /// The door's write of the first prompt has returned (FOUNDATION1f-iv part b; §19.4): <c>sent</c> for each moved word it carried and the prompt's
    /// outcome in one transaction, then, off the door's thread, the take of those words off the new part and the one reading that closes the move
    /// <c>delivered</c> once every word it moved has <c>sent</c>. Once: the protocol door tells it at its first prompt's write and again at a
    /// transient try's, the native door at its stdin write's return. A carry-on already ended is left as it was. Never faults.
    /// </summary>
    public void Written()
    {
        if (Interlocked.Exchange(ref _settled, 1) != 0) return;
        move.Wire(_transfers);
        var take = _take;
        _taking = Task.Run(async () =>
        {
            try
            {
                if (take is not null) await take().ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // The take did not come out: the word has `sent` and still waits, which the reading takes off as taken.
            }

            await move.ReadAsync().ConfigureAwait(false);
        });
    }

    /// <summary>The carry-on goes no further: the move ended by its phase, its prompt known unwritten, unless it is delivered. Never faults.</summary>
    public async Task EndAsync()
    {
        if (Interlocked.Exchange(ref _settled, 1) != 0) return;
        await move.EndAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Let go of the record on every way out of the start: ended by its phase unless its prompt was written, in which case the take that follows
    /// is awaited, so a run never ends with a delivery in flight.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await EndAsync().ConfigureAwait(false);
        if (_taking is { } taking) await taking.ConfigureAwait(false);
    }
}
