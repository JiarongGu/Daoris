namespace Daoris.Driver;

/// <summary>
/// A running session's record as its one dispatcher reaches it (COWORK1c, D188 point 6; the message-urgency design §6): a word is
/// kept on the record's <c>said</c> before anything holds it, and taken off by its id once it is on the wire.
/// </summary>
public interface ISaidDoor
{
    /// <summary>
    /// Keep one word for the session's run, after the words already waiting, with its level, its author and its producer's key: the
    /// answer carries the record's id for it, or, under a key a waiting word holds, that word.
    /// </summary>
    Task<SayAnswer> KeepAsync(ChatMessage word, CancellationToken ct);

    /// <summary>Take these words off the record by their ids: the session took them, or the person took them back. False where it did not.</summary>
    Task<bool> TakeOffAsync(IReadOnlyList<string> ids, CancellationToken ct);

    /// <summary>
    /// Mark these words attempted on the record, before the wire (COWORK1c, the design §6): the first moment kept. False where it did not.
    /// </summary>
    Task<bool> AttemptAsync(IReadOnlyList<string> ids, CancellationToken ct);
}

/// <summary>
/// The service's say and taken doors for one session of this machine's (COWORK1c): what a driven session's inbox and a conversation's
/// turns keep their words through. Kept with <c>running</c>, so the record keeps them while it runs; a service from before refuses
/// them as <c>running</c>, and the dispatcher then holds them in this process alone, as before.
/// </summary>
public sealed class SaidDoor(ServiceClient service, string session) : ISaidDoor
{
    public Task<SayAnswer> KeepAsync(ChatMessage word, CancellationToken ct) =>
        service.SayAsync(
            session, word.Text, word.Files.Count > 0 ? [.. word.Files.Select(file => file.Name)] : null, ct,
            // SUPERVISOR1a-i (the supervisor design §6.1): a supervisor's word is carried as its, which the service keeps only from the
            // release's own say, never dropped to no author and kept as the person's.
            from: word.Daoris is not null ? DaorisWords.From : word.Supervised is not null ? SupervisorHanded.From : null,
            key: word.Key ?? word.Daoris?.Key,
            level: MessageLevels.Spelled(word.Level), running: true);

    public async Task<bool> TakeOffAsync(IReadOnlyList<string> ids, CancellationToken ct) =>
        (await service.TakenAsync(session, ids, by: null, ct).ConfigureAwait(false)).Ok;

    public async Task<bool> AttemptAsync(IReadOnlyList<string> ids, CancellationToken ct) =>
        (await service.AttemptAsync(session, ids, ct).ConfigureAwait(false)).Ok;

    /// <summary>
    /// The words a go-on carries from a record that ended or parked (COWORK1c, the message-urgency design §6), marked attempted before
    /// the resumed run writes them, so a shell cut off under that run never sends them again. Never faults: unmarked, they still go.
    /// </summary>
    internal static async Task AttemptedAsync(ServiceClient service, string session, IReadOnlyList<SaidWordView> words)
    {
        List<string> ids = [.. words.Select(word => word.Id).OfType<string>()];
        if (ids.Count == 0) return;
        try
        {
            await service.AttemptAsync(session, ids).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException
                                          or ObjectDisposedException or System.Text.Json.JsonException)
        {
            // Unmarked, the words still go (above).
        }
    }

    /// <summary>
    /// A kept word as its dispatcher hands it to a door (COWORK1c): marked attempted through <paramref name="door"/> before the wire,
    /// and told <paramref name="went"/> once written. A word kept nowhere, or with no door yet, is handed as it is.
    /// </summary>
    internal static ChatMessage Dispatched(ChatMessage word, ISaidDoor? door, Action<ChatMessage> went)
    {
        if (!word.Kept || word.Id is not { } id || door is null) return word;
        return word with
        {
            Attempting = async () =>
            {
                try
                {
                    await door.AttemptAsync([id], CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException
                                                  or ObjectDisposedException or System.Text.Json.JsonException)
                {
                    // Unmarked, the word still goes: a shell cut off before its take-off may send it again, the rare cost of a record
                    // that did not answer, against a word never sent.
                }
            },
            Went = () => went(word),
        };
    }
}

/// <summary>What keeping a word for a running session's run did (COWORK1c).</summary>
public enum InboxKeep
{
    /// <summary>Kept on the record, and held, sent or steered under the record's id.</summary>
    Held,

    /// <summary>The record refused it, by the service's word: nothing holds it.</summary>
    Refused,

    /// <summary>
    /// The run takes no more words: nothing stays on its record, which is ending, and the caller holds the word as words said while a
    /// session winds up are held, or keeps it on a record that has ended.
    /// </summary>
    Closed,
}

/// <summary>What keeping a word for a running session's run did (COWORK1c), with the record's answer and, held, where it reaches.</summary>
/// <param name="Said">The say door's answer; null where nothing was asked of it.</param>
public sealed record InboxKept(InboxKeep Outcome, SayAnswer? Said = null)
{
    /// <summary>Where a held word reaches the session; null until the door is known, and for any other outcome.</summary>
    public DrivenReach? Reach { get; init; }

    /// <summary>A word at <c>now</c>, held: what the door did with it (<see cref="DrivenInbox.HoldNowAsync"/>).</summary>
    public SentNow? Now { get; init; }

    public static readonly InboxKept Closed = new(InboxKeep.Closed);
}
