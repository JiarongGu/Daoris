namespace Daoris.Driver;

/// <summary>What one reading of a move's delivery came to (<see cref="MoveDelivery.ReadAsync"/>).</summary>
public enum MoveDeliveryOutcome
{
    /// <summary>Closed <c>delivered</c>: every word it moved has <c>sent</c>, and none waits on its destination.</summary>
    Delivered,

    /// <summary>Held for the person, naming its <i>left</i> and uncertain words and its begun effect; it may have closed <c>resolved</c> at once.</summary>
    Held,

    /// <summary>Nothing begun and nothing lost: its words wait on its destination never attempted, or its effect is still to make. The caller delivers.</summary>
    GoesOn,

    /// <summary>Nothing settled now: its destination's <c>said</c> could not be read, or a word sent still waits there after its take. Read again later.</summary>
    Unread,

    /// <summary>It had ended otherwise: fallen back, or closed superseded or gone.</summary>
    Ended,
}

/// <summary>A reading of a move's delivery: the move as it stands after, and each word it moved as the reading found it.</summary>
/// <param name="Move">The move as it stands after the reading.</param>
/// <param name="Outcome">What the reading came to.</param>
public sealed record MoveDelivered(MoveRecord Move, MoveDeliveryOutcome Outcome)
{
    /// <summary>The words with <c>sent</c>: written to their agent, taken, and taken off their destination where they still waited.</summary>
    public IReadOnlyList<MoveRecordWord> Taken { get; init; } = [];

    /// <summary>The words gone from the destination without <c>sent</c>: <i>left</i>, never sent again by the move, the person's offered for saying again.</summary>
    public IReadOnlyList<MoveRecordWord> Left { get; init; } = [];

    /// <summary>The words still waiting attempted and without <c>sent</c>: uncertain, settled and said as any attempted word is.</summary>
    public IReadOnlyList<MoveRecordWord> Uncertain { get; init; } = [];

    /// <summary>The words still waiting never attempted: no write carried them, and the move may still deliver them.</summary>
    public IReadOnlyList<MoveRecordWord> Waiting { get; init; } = [];

    /// <summary>The effect it marked and whose outcome no record holds, which its hold names and nothing makes again by itself.</summary>
    public MoveEffect? Begun { get; init; }

    /// <summary>The move as its hold wrote it, where this reading held it: what the hold's note is said from.</summary>
    public MoveRecord? Held { get; init; }
}

/// <summary>
/// The moved words' delivery and the one reading of a crash at <c>delivering</c> (FOUNDATION1f-i part 3, D212; the foundation design §3.3,
/// §13.4 step 4, §13.5's <c>opened</c> and <c>delivering</c> rows, §19.4). Rows f-ii to f-vi adopt it, each where its door's write returns:
/// the live limit move first (FOUNDATION1f-ii part a, through <see cref="ConversationMoves"/>).
/// </summary>
/// <remarks>
/// <para><b>The order.</b> The prompt's mark and the service's acknowledged attempt, then the write; <b>as the write returns, one
/// transaction writes each word's <c>sent</c></b> (<see cref="Wrote"/>), the effect's outcome with it; then the take, the dispatcher having
/// the service take the words off <c>said</c>; and only then <c>delivered</c>, which the store writes only once every word the move moved
/// has <c>sent</c>. So no word waits on <c>said</c> while its move says <c>delivered</c>.</para>
/// <para><b>The one reading</b> (<see cref="ReadAsync"/>), at the take's return and at a come-up's take-up alike, reads <c>sent</c> and the
/// destination's <c>said</c>, never the settlement and never a session event: a word with <c>sent</c> is taken, taken off where it still
/// waits; one gone without it is <i>left</i>; one waiting, attempted and without it, uncertain; one waiting never attempted is still to
/// deliver. The move closes <c>delivered</c> once every word has <c>sent</c> and none waits; it is held for the person where a word is
/// <i>left</i> or uncertain, or an effect it marked has no outcome, naming them; otherwise the caller goes on.</para>
/// </remarks>
/// <param name="store">The move record, under this process's run.</param>
/// <param name="said">The destinations' <c>said</c>.</param>
public sealed class MoveDelivery(MoveStore store, IMoveSaid said)
{
    /// <summary>The phases the reading reads a move's words at: from the open's answer to the delivery (§13.5's <c>opened</c>, <c>copied</c>, <c>verified</c> and <c>delivering</c> rows).</summary>
    public static readonly IReadOnlyList<MoveRecordPhase> Reads =
        [MoveRecordPhase.Opened, MoveRecordPhase.Copied, MoveRecordPhase.Verified, MoveRecordPhase.Delivering];

    private readonly MoveResolve _resolve = new(store, said);

    /// <summary>
    /// As the door's write returns: each written word's <c>sent</c>, and the effect's outcome, in one transaction under the claim. False where
    /// that transaction failed, and those words then read <i>left</i> once taken, the conservative side. Never faults.
    /// </summary>
    public bool Wrote(MoveClaim claim, IReadOnlyCollection<string> transfers)
    {
        try
        {
            store.Sent(claim, transfers);
            return true;
        }
        catch (Exception error) when (error is DriverException or Microsoft.Data.Sqlite.SqliteException)
        {
            return false;
        }
    }

    /// <summary>
    /// The delivery's tail as the door's write returns: <c>sent</c> (<see cref="Wrote"/>); then the take of the written words off the
    /// destination, made whether or not <c>sent</c> was written, since the turn took them; then the reading, which closes <c>delivered</c> only
    /// once every word has <c>sent</c>, and holds the move where a <c>sent</c> failed.
    /// </summary>
    /// <exception cref="MoveClaimLostException">The claim no longer holds the move.</exception>
    public async Task<MoveDelivered> WroteAsync(MoveClaim claim, IReadOnlyCollection<string> transfers, CancellationToken ct = default)
    {
        var move = store.Read(claim.Move) ?? throw new MoveClaimLostException(claim, "the move is gone, with its record");
        List<string> written = [.. store.WordsOf(claim.Move).Where(word => transfers.Contains(word.Transfer, StringComparer.Ordinal)).Select(word => word.Word)];
        Wrote(claim, transfers);
        if (move.Destination is { } destination) await MoveSaying.TakeAsync(said, destination, written, ct).ConfigureAwait(false);
        return await ReadAsync(claim, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The one reading of a move's delivery under its claim, at the take's return or at a come-up's take-up (§13.4 step 4; §13.5's <c>opened</c>
    /// and <c>delivering</c> rows): <c>sent</c> and the destination's <c>said</c> alone. A word sent and still waiting is taken off as taken,
    /// what the destination holds after read again; the move is then closed <c>delivered</c> where every word it moved has <c>sent</c>, none waits
    /// and, for a move of no word, its effect's outcome is recorded; held, naming its words <i>left</i> and uncertain and its begun effect, where
    /// a word is <i>left</i> or uncertain or an effect it marked has no outcome, its uncertain words then settled and said and §3.3's one rule
    /// read; and otherwise the caller's to go on with. A move held or ended is answered by its phase, a hold read by the one rule.
    /// </summary>
    /// <exception cref="MoveClaimLostException">The claim no longer holds the move.</exception>
    public async Task<MoveDelivered> ReadAsync(MoveClaim claim, CancellationToken ct = default)
    {
        var move = store.Read(claim.Move) ?? throw new MoveClaimLostException(claim, "the move is gone, with its record");
        switch (move.Phase)
        {
            case MoveRecordPhase.Delivered:
                return new MoveDelivered(move, MoveDeliveryOutcome.Delivered) { Taken = store.WordsOf(move.Id) };
            case MoveRecordPhase.Held:
                return new MoveDelivered(await _resolve.ReadAsync(claim, ct).ConfigureAwait(false), MoveDeliveryOutcome.Held) { Held = move };
            case MoveRecordPhase.Closed when move.HeldWhy is not null:
                return new MoveDelivered(move, MoveDeliveryOutcome.Held) { Held = move };
            case MoveRecordPhase.FellBack or MoveRecordPhase.Closed or null:
                return new MoveDelivered(move, MoveDeliveryOutcome.Ended);
            case { } phase when !Reads.Contains(phase):
                return new MoveDelivered(move, MoveDeliveryOutcome.GoesOn);
        }

        // §13.4 step 5: before anything is delivered, in every phase, the line's head is read.
        if (await HeldPastAsync(claim, ct).ConfigureAwait(false) is { } past) return past;

        var words = store.WordsOf(move.Id);
        var read = await WordsAsync(move, words, ct).ConfigureAwait(false);
        if (read is null) return new MoveDelivered(move, MoveDeliveryOutcome.Unread);

        // A word sent is taken: where it still waits it is taken off as taken, and what the destination holds after is read again.
        if (read.StillWaiting.Count > 0)
        {
            await MoveSaying.TakeAsync(said, move.Destination!, [.. read.StillWaiting.Select(word => word.Word)], ct).ConfigureAwait(false);
            read = await WordsAsync(move, words, ct).ConfigureAwait(false);
            if (read is null) return new MoveDelivered(move, MoveDeliveryOutcome.Unread);
        }

        var begun = move.Effect is { Outcome: null } effect ? effect : null;
        var found = new MoveDelivered(move, MoveDeliveryOutcome.GoesOn)
        {
            Taken = read.Taken, Left = read.Left, Uncertain = read.Uncertain, Waiting = read.Waiting, Begun = begun,
        };
        var everySent = words.Count > 0 ? read.Taken.Count == words.Count : move.Effect is { Outcome: not null };
        if (move.Phase == MoveRecordPhase.Delivering && everySent)
        {
            return read.StillWaiting.Count > 0
                ? found with { Outcome = MoveDeliveryOutcome.Unread }
                : found with { Move = store.Advance(claim, MoveRecordPhase.Delivered), Outcome = MoveDeliveryOutcome.Delivered };
        }

        if (read.Left.Count == 0 && read.Uncertain.Count == 0 && begun is null) return found;

        // Held for the person, naming what it could not deliver; its uncertain words then settled and said, and the one rule read.
        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var word in read.Left) named[word.Transfer] = MoveWordDispositions.Left;
        foreach (var word in read.Uncertain) named[word.Transfer] = MoveWordDispositions.Uncertain;
        var held = store.Hold(claim, named.Count > 0 ? MoveHoldWhy.Undelivered : MoveHoldWhy.Unrecorded, named);
        return found with { Move = await _resolve.ReadAsync(claim, ct).ConfigureAwait(false), Outcome = MoveDeliveryOutcome.Held, Held = held };
    }

    /// <summary>
    /// Where the line's head has moved past the move's destination before everything was delivered there (§13.4 step 5, §19.3), which only an
    /// older build makes: nothing is delivered, and the move is held <see cref="MoveHoldWhy.Superseded"/>. It names the person's own words still
    /// waiting never attempted there, <see cref="MoveWordDispositions.Held"/>, for the person to take back and say at the head; the words gone
    /// without <c>sent</c>, <i>left</i>; and the attempted ones, uncertain, settled and said as before. Another author's waiting words stay on
    /// the destination as claims and are named by nothing. A word with <c>sent</c> that still waits is taken off as taken, since its agent
    /// was written. Null where nothing is left to deliver, the destination is the head, or the head cannot be told now.
    /// </summary>
    /// <exception cref="MoveClaimLostException">The claim no longer holds the move.</exception>
    public async Task<MoveDelivered?> HeldPastAsync(MoveClaim claim, CancellationToken ct = default)
    {
        var move = store.Read(claim.Move) ?? throw new MoveClaimLostException(claim, "the move is gone, with its record");
        if (move.Phase is not { } phase || !Reads.Contains(phase) || move.Destination is not { } destination) return null;
        var words = store.WordsOf(move.Id);

        // Nothing is left to deliver: every word it moved is disposed, or a move of no word has written its prompt.
        if (words.Count > 0 ? words.All(word => word.Disposition is not null) : move.Effect is { Outcome: not null }) return null;
        if (await MoveSaying.HeadPastAsync(said, destination, ct).ConfigureAwait(false) is null) return null;

        var read = await WordsAsync(move, words, ct).ConfigureAwait(false);
        if (read is null) return new MoveDelivered(move, MoveDeliveryOutcome.Unread);
        if (read.StillWaiting.Count > 0)
        {
            await MoveSaying.TakeAsync(said, destination, [.. read.StillWaiting.Select(word => word.Word)], ct).ConfigureAwait(false);
            read = await WordsAsync(move, words, ct).ConfigureAwait(false);
            if (read is null) return new MoveDelivered(move, MoveDeliveryOutcome.Unread);
        }

        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var word in read.Left) named[word.Transfer] = MoveWordDispositions.Left;
        foreach (var word in read.Uncertain) named[word.Transfer] = MoveWordDispositions.Uncertain;
        foreach (var word in read.Waiting.Except(read.Claims)) named[word.Transfer] = MoveWordDispositions.Held;
        var held = store.Hold(claim, MoveHoldWhy.Superseded, named);
        return new MoveDelivered(await _resolve.ReadAsync(claim, ct).ConfigureAwait(false), MoveDeliveryOutcome.Held)
        {
            Taken = read.Taken, Left = read.Left, Uncertain = read.Uncertain, Waiting = read.Waiting,
            Begun = move.Effect is { Outcome: null } effect ? effect : null, Held = held,
        };
    }

    /// <summary>Each word the move moved as its destination's <c>said</c> shows it now; null where that cannot be read.</summary>
    private async Task<WordsRead?> WordsAsync(MoveRecord move, IReadOnlyList<MoveRecordWord> words, CancellationToken ct)
    {
        IReadOnlyList<MoveWaiting> waiting = [];
        if (words.Count > 0 && move.Destination is { } destination)
        {
            if (await MoveSaying.WaitingAsync(said, destination, ct).ConfigureAwait(false) is not { } now) return null;
            waiting = now;
        }

        MoveWaiting? Waits(MoveRecordWord word) => waiting.FirstOrDefault(each => string.Equals(each.Word, word.Word, StringComparison.Ordinal));
        List<MoveRecordWord> taken = [.. words.Where(word => word.Disposition == MoveWordDispositions.Sent)];
        List<MoveRecordWord> open = [.. words.Where(word => word.Disposition is null)];
        var waits = open.Where(word => Waits(word) is { Attempted: false }).ToList();
        return new WordsRead(
            taken, [.. taken.Where(word => Waits(word) is not null)], [.. open.Where(word => Waits(word) is null)],
            [.. open.Where(word => Waits(word) is { Attempted: true })], waits, [.. waits.Where(word => Waits(word) is { Person: false })]);
    }

    /// <summary>
    /// The words as the reading found them: sent (and of those, still waiting), gone without <c>sent</c>, waiting attempted, waiting never
    /// attempted, and of those another author's words, a claim the person is never offered as theirs to say.
    /// </summary>
    private sealed record WordsRead(
        IReadOnlyList<MoveRecordWord> Taken, IReadOnlyList<MoveRecordWord> StillWaiting, IReadOnlyList<MoveRecordWord> Left,
        IReadOnlyList<MoveRecordWord> Uncertain, IReadOnlyList<MoveRecordWord> Waiting, IReadOnlyList<MoveRecordWord> Claims);
}
