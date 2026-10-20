namespace Daoris.Driver;

/// <summary>
/// Daoris's own words to a session (COWORK1b, D188; the message-urgency design §2.2, §4.3): a reopen told, a step's go-on. Marked as
/// Daoris's wherever they go, never the person's, and dropped where they are delivered once the fact they state no longer holds,
/// judged by the key each carries, never by its sentence.
/// </summary>
/// <remarks>
/// <para><b>Where the judge sits.</b> A word on a running session's inbox is judged at the run's take, the one point it leaves for the
/// wire: the protocol door's next prompt (<c>AcpSession.RunAsync</c>) and the native door's resumed run (<c>DriverContinuations.GoOnAsync</c>).
/// A word the inbox sends at the next step goes the moment the look that held it judged it. A word waiting on a record is judged by
/// the look before the planner's go-on would deliver it (<see cref="ReopenTelling"/>), from the quests that look read.</para>
///
/// <para><b>An unanswered judge keeps the word.</b> Where the quest cannot be read the word goes: the look that held it found its fact
/// standing, and a word lost is worse than one a later submission answered a moment before.</para>
/// </remarks>
public static class DaorisWords
{
    /// <summary>The author a word of Daoris's names on a record (<c>from</c>), and on the record's <c>user</c> event.</summary>
    public const string From = "daoris";

    /// <summary>The driver's English for <see cref="SessionEventCodes.Dropped"/>: what a page that does not know the code shows.</summary>
    public const string DroppedLine = "— Daoris did not send this: what it said no longer holds.";

    /// <summary>
    /// Whether this held word still goes, asked where it leaves for the wire: every word but Daoris's whose fact no longer holds.
    /// Told delivered where it goes.
    /// </summary>
    /// <exception cref="OperationCanceledException">The run is closing.</exception>
    public static async Task<bool> GoesAsync(ChatMessage word, CancellationToken ct)
    {
        if (word.Daoris is not { } daoris) return true;
        if (daoris.Stands is { } stands)
        {
            try
            {
                if (!await stands(ct).ConfigureAwait(false)) return false;
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // Unanswered: the word goes (the remarks).
            }
        }

        Delivered(word);
        return true;
    }

    /// <summary>Tell a word of Daoris's it was delivered; a listener's failure is its own.</summary>
    internal static void Delivered(ChatMessage word)
    {
        try
        {
            word.Daoris?.Delivered?.Invoke();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The word went either way: what forgets it was told is the telling's own memory.
        }
    }

    /// <summary>
    /// The note a word of Daoris's leaves where it was dropped: its code, which the page words as <c>work.conversation.dropped</c>, and
    /// the word's id where the record showed it held, so the page settles it there.
    /// </summary>
    public static SessionEvent Dropped(ChatMessage word) => new()
    {
        Kind = SessionEventKind.Note,
        Text = DroppedLine,
        Words = word.Id is { } id ? [id] : null,
        Code = SessionEventCodes.Dropped,
    };

    /// <summary>The words that still go, in their order, each dropped one said in <paramref name="note"/>.</summary>
    /// <exception cref="OperationCanceledException">The run is closing.</exception>
    public static async Task<IReadOnlyList<ChatMessage>> GoingAsync(
        IReadOnlyList<ChatMessage> words, Action<SessionEvent> note, CancellationToken ct)
    {
        var going = new List<ChatMessage>(words.Count);
        foreach (var word in words)
        {
            if (await GoesAsync(word, ct).ConfigureAwait(false)) going.Add(word);
            else note(Dropped(word));
        }

        return going;
    }
}

/// <summary>
/// The mark a word of Daoris's carries to a running session (COWORK1b): its fact's key, the judge of whether that fact still holds
/// where the word is delivered, and who is told it went.
/// </summary>
/// <param name="Key">The fact's key (<see cref="DaorisKeys"/>), as the word on a record keeps it.</param>
public sealed record DaorisWord(string Key)
{
    /// <summary>Whether the fact still holds, asked where the word leaves a hold for the wire; null always goes.</summary>
    public Func<CancellationToken, Task<bool>>? Stands { get; init; }

    /// <summary>Told once the word went to the session: where the telling writes its once-mark.</summary>
    public Action? Delivered { get; init; }
}

/// <summary>A fact a key of Daoris's names (<see cref="DaorisKeys.Read"/>): its kind, its quest, and a submission by machine and sequence.</summary>
public sealed record DaorisFact(string Kind, string Quest, string Machine, long Sequence);

/// <summary>
/// The keys Daoris's facts give their words (COWORK1b, the message-urgency design §6): a reopen's, the quest and the reopen's machine
/// and sequence; a step's go-on, the step and the submission it grew from. One word per key on a record, and what a word's fact is
/// judged by, never its sentence.
/// </summary>
public static class DaorisKeys
{
    public const string ReopenKind = "reopen";

    public const string GoneOnKind = "gone-on";

    /// <summary>A reopen's key: the quest and the reopen's machine and sequence.</summary>
    public static string Reopen(string quest, string machine, long sequence) => $"{ReopenKind}/{quest}/{machine}/{sequence}";

    /// <summary>A reopen's key, from the reopen as the service lists it.</summary>
    public static string Reopen(string quest, QuestReopenView reopen) => Reopen(quest, reopen.Machine, reopen.Sequence);

    /// <summary>A step's go-on's key: the step and the submission it grew from.</summary>
    public static string GoneOn(string step, QuestSubmissionRef grew) => $"{GoneOnKind}/{step}/{grew.Machine}/{grew.Sequence}";

    /// <summary>The fact a key names; null for none, or a key that is not one of Daoris's facts.</summary>
    /// <remarks>Read from both ends, since a machine's name sits between the quest and the sequence.</remarks>
    public static DaorisFact? Read(string? key)
    {
        if (key is null) return null;
        var kind = key.IndexOf('/');
        var quest = kind < 0 ? -1 : key.IndexOf('/', kind + 1);
        var sequence = key.LastIndexOf('/');
        if (quest < 0 || sequence <= quest + 1 || !long.TryParse(key.AsSpan(sequence + 1), out var number)) return null;

        var named = key[..kind];
        return named is ReopenKind or GoneOnKind
            ? new DaorisFact(named, key[(kind + 1)..quest], key[(quest + 1)..sequence], number)
            : null;
    }

    /// <summary>
    /// Whether the fact still holds of <paramref name="quest"/> as it stands now: a reopen while the quest is taken with that same
    /// reopen, which a later submission answers; a go-on while the step is taken and unheld, grown from that same submission, with no
    /// result of its own again. A quest the service no longer holds holds none.
    /// </summary>
    public static bool Stands(DaorisFact fact, QuestView? quest) => fact.Kind switch
    {
        ReopenKind => quest is { Status: "Taken", Reopened: { } reopen }
                      && string.Equals(reopen.Machine, fact.Machine, StringComparison.Ordinal) && reopen.Sequence == fact.Sequence,
        GoneOnKind => quest is { Status: "Taken", HeldByReopen: false, Submission: null, GrewFrom: { } grew }
                      && string.Equals(grew.Machine, fact.Machine, StringComparison.Ordinal) && grew.Sequence == fact.Sequence,
        _ => true,
    };
}
