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
/// <para><b>Today's take is the delivery here.</b> The moved words are taken off the new part before its session is spawned, into the ask its
/// instruction reads (<see cref="DriverContinuations.HandedOnAsync"/>), so their <c>sent</c> is written where that take returns
/// (<see cref="DeliveredAsync"/>), then the reading that closes the move <c>delivered</c>; a carry-on that hands no word closes on its prompt's
/// outcome at the same point. Part b of the row moves <c>sent</c> to each door's write return, the native start's and the protocol door's first
/// prompt's, and the take after it; nothing here sends a word.</para>
/// <para><b>Ended wherever it stops.</b> The record is let go (<see cref="DisposeAsync"/>) on every way out of the start, ended by its phase
/// unless it is delivered, so a refused open, a refused write or a start that fails leaves no standing move to hold the line.</para>
/// </remarks>
internal sealed class CarryOnRecord(ConversationMove move) : IAsyncDisposable
{
    private int _settled;

    /// <summary>
    /// The move a carry-on decides, or null for a start that keeps none: one that continues nothing, and one the quest's hand-over made, which is
    /// row f-v's (<paramref name="handedFrom"/>). Executor <c>driven</c>, its brief the new part's, its why and asker the record's own link, and
    /// written at <c>old-ended</c>, the part it goes on from having ended.
    /// </summary>
    /// <param name="continues">The session this start continues, why and who asked (<c>Driver.ContinuesFrom</c>).</param>
    /// <param name="handedFrom">The start carries on from the very session the quest was handed over from.</param>
    /// <param name="agent">The agent the new part opens on.</param>
    /// <param name="account">The settled selection's account, by profile name, where it is settled.</param>
    public static MoveDecision? Decision(ContinuedFrom? continues, bool handedFrom, string agent, string? account) =>
        continues is null || handedFrom
            ? null
            : new MoveDecision(continues.Session, MoveExecutors.Driven, MoveHows.Brief, continues.Why, continues.By)
            {
                FromEnded = true, Account = account, RecipeWhy = continues.Why, Agent = agent,
            };

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
    /// The words handed are taken (<see cref="DriverContinuations.HandedOnAsync"/>): their <c>sent</c> and the prompt's outcome in one transaction
    /// (<paramref name="transfers"/>, the transfers of the words the take took, none for a carry-on that moved none), then the one reading that closes
    /// the move <c>delivered</c> once every word it moved has <c>sent</c>. Never faults.
    /// </summary>
    public async Task DeliveredAsync(IReadOnlyCollection<string> transfers)
    {
        if (Interlocked.Exchange(ref _settled, 1) != 0) return;
        move.Wire(transfers);
        await move.ReadAsync().ConfigureAwait(false);
    }

    /// <summary>The carry-on goes no further: the move ended by its phase, its prompt known unwritten, unless it is delivered. Never faults.</summary>
    public async Task EndAsync()
    {
        if (Interlocked.Exchange(ref _settled, 1) != 0) return;
        await move.EndAsync().ConfigureAwait(false);
    }

    /// <summary>Let go of the record on every way out of the start: ended by its phase unless it is delivered.</summary>
    public async ValueTask DisposeAsync() => await EndAsync().ConfigureAwait(false);
}
