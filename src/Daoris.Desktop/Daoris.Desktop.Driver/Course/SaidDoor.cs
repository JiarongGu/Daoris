namespace Daoris.Driver;

/// <summary>
/// A running session's record as its one dispatcher reaches it: a word is
/// kept on the record's <c>said</c> before anything holds it, and taken off by its id once it is on the wire.
/// </summary>
public interface ISaidDoor
{
    /// <summary>
    /// Keep one word for the session's run, after the words already waiting, with its level, its author and its producer's key: the
    /// answer carries the record's id for it, or, under a key a waiting word holds, that word.
    /// </summary>
    Task<SayAnswer> KeepAsync(ChatMessage word, CancellationToken ct);

    /// <summary>Take these words off the record by their ids: the session took them, or the person took them back. False where it does not.</summary>
    Task<bool> TakeOffAsync(IReadOnlyList<string> ids, CancellationToken ct);

    /// <summary>
    /// Mark these words attempted on the record, before the wire: the first moment kept. The ids the record named
    /// marked: a word it does not name is not attempted and does not go; none where it does not
    /// acknowledge the mark at all.
    /// </summary>
    Task<IReadOnlyList<string>> AttemptAsync(IReadOnlyList<string> ids, CancellationToken ct);
}

/// <summary>
/// The service's say and taken doors for one session of this machine's: what a driven session's inbox and a conversation's
/// turns keep their words through. Kept with <c>running</c>, so the record keeps them while it runs; a service that refuses
/// them as <c>running</c> leaves the dispatcher holding them in this process alone. A record that is no longer its line's
/// head is refused <c>continued</c>, naming the head, which the dispatcher holds nowhere and answers to its caller.
/// </summary>
public sealed class SaidDoor(ServiceClient service, string session) : ISaidDoor
{
    public Task<SayAnswer> KeepAsync(ChatMessage word, CancellationToken ct) =>
        service.SayAsync(
            session, word.Text, word.Files.Count > 0 ? [.. word.Files.Select(file => file.Name)] : null, ct,
            // A supervisor's word is carried as its, which the service keeps only from the
            // release's own say, never dropped to no author and kept as the person's.
            from: word.Daoris is not null ? DaorisWords.From : word.Supervised is not null ? SupervisorHanded.From : null,
            key: word.Key ?? word.Daoris?.Key,
            level: MessageLevels.Spelled(word.Level), running: true);

    public async Task<bool> TakeOffAsync(IReadOnlyList<string> ids, CancellationToken ct) =>
        (await service.TakenAsync(session, ids, by: null, ct).ConfigureAwait(false)).Ok;

    public async Task<IReadOnlyList<string>> AttemptAsync(IReadOnlyList<string> ids, CancellationToken ct) =>
        (await service.AttemptAsync(session, ids, ct).ConfigureAwait(false)).Marked;

    /// <summary>
    /// The words a go-on carries from a record that ended or parked, marked attempted before
    /// the resumed run writes them, so a shell cut off under that run never sends them again. The ids the record named marked:
    /// a word it does not name does not go, and none where the record does not acknowledge
    /// the mark, which is a refusal, a host without the door, an answer that does not come (its commit may have landed: the word then
    /// waits marked, settled uncertain at the next come-up and never sent again by itself) or one that does not read. Never faults.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> AttemptedAsync(ServiceClient service, string session, IReadOnlyList<SaidWordView> words)
    {
        List<string> ids = [.. words.Select(word => word.Id).OfType<string>()];
        if (ids.Count == 0) return [];
        try
        {
            return (await service.AttemptAsync(session, ids).ConfigureAwait(false)).Marked;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException
                                          or ObjectDisposedException or System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// A message as it goes once the record named its words marked: itself where nothing marks it
    /// (a word kept nowhere) or <see cref="ChatMessage.Attempting"/> names its id; a go-on carrying words under their ids narrowed to the words
    /// named, so an answer naming fewer than asked sends only those; and null where nothing it carries is named. A null message is not
    /// written, its words wait on their record, and <paramref name="unsent"/> is told how many of them it left there.
    /// </summary>
    internal static async Task<ChatMessage?> AcknowledgedAsync(ChatMessage message, Action<int> unsent)
    {
        if (message.Attempting is not { } attempting) return message;
        var marked = await attempting().ConfigureAwait(false);
        if (message.GoesOn is { } ask)
        {
            var asked = ask.Ids.Count;
            ask.Narrow(marked);
            if (ask.Ids.Count < asked) unsent(asked - ask.Ids.Count);
            // Nothing it carries is named: the go-on's own first line and appendix go for no word, so none goes.
            if (asked > 0 && ask.Ids.Count == 0) return null;
            return ask.Ids.Count == asked ? message : message with { Text = ask.Prompt };
        }

        if (message.Id is not { } id || marked.Contains(id, StringComparer.Ordinal)) return message;
        unsent(1);
        return null;
    }

    /// <summary>
    /// A kept word as its dispatcher hands it to a door: marked attempted through <paramref name="door"/> before the wire,
    /// and told <paramref name="went"/> once written. A word kept nowhere, or with no door yet, is handed as it is.
    /// </summary>
    internal static ChatMessage Dispatched(ChatMessage word, ISaidDoor? door, Action<ChatMessage> went)
    {
        if (!word.Kept || word.Id is not { } id || door is null) return word;
        return word with
        {
            // Acknowledged before the wire: a word the record does not name marked is not written, and waits
            // on its record. Once the mark fails open, the rare cost of a record that does not answer, against a word never sent.
            Attempting = async () =>
            {
                try
                {
                    return await door.AttemptAsync([id], CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException
                                                  or ObjectDisposedException or System.Text.Json.JsonException)
                {
                    return [];
                }
            },
            Went = () => went(word),
        };
    }
}

/// <summary>What keeping a word for a running session's run did.</summary>
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

    /// <summary>
    /// The record is no longer its line's head: the say door refused the word
    /// <c>continued</c>, naming the head (<see cref="InboxKept.Head"/>). Nothing holds, sends, steers, attempts or takes it here, and it is never
    /// held in this process instead, as a refusal from a host that cannot keep a running record is: the caller says it at the head.
    /// </summary>
    Continued,
}

/// <summary>What keeping a word for a running session's run did, with the record's answer and, held, where it reaches.</summary>
/// <param name="Said">The say door's answer; null where nothing is asked of it.</param>
public sealed record InboxKept(InboxKeep Outcome, SayAnswer? Said = null)
{
    /// <summary>
    /// The record answered the say <b>settled</b>: a retry of a word held already, which left a
    /// record of the line. The outcome reads <see cref="InboxKeep.Held"/>, so a caller that asks only whether the word is held reads it so,
    /// and nothing is kept, held, sent, steered, attempted or taken now.
    /// </summary>
    public bool Settled => Said?.Settled is not null;

    /// <summary>The line's head the record named where the outcome is <see cref="InboxKeep.Continued"/>: where the word is said instead.</summary>
    public string? Head => Said?.Head;

    /// <summary>Where a held word reaches the session; null until the door is known, and for any other outcome.</summary>
    public DrivenReach? Reach { get; init; }

    /// <summary>A word at <c>now</c>, held: what the door did with it (<see cref="DrivenInbox.HoldNowAsync"/>).</summary>
    public SentNow? Now { get; init; }

    public static readonly InboxKept Closed = new(InboxKeep.Closed);
}
