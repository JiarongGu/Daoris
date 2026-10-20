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
            from: word.Daoris is null ? null : DaorisWords.From, key: word.Key ?? word.Daoris?.Key,
            level: MessageLevels.Spelled(word.Level), running: true);

    public async Task<bool> TakeOffAsync(IReadOnlyList<string> ids, CancellationToken ct) =>
        (await service.TakenAsync(session, ids, by: null, ct).ConfigureAwait(false)).Ok;
}

/// <summary>What keeping a word for a running session's run did (COWORK1c).</summary>
public enum InboxKeep
{
    /// <summary>Kept on the record, and held, sent or steered under the record's id.</summary>
    Held,

    /// <summary>Kept on the record as the run stopped taking words: it waits there for the session's go-on.</summary>
    OnRecord,

    /// <summary>The record refused it, by the service's word: nothing holds it.</summary>
    Refused,

    /// <summary>The run takes no more words: nothing was kept, and the caller keeps it on the record as it would for an ended one.</summary>
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
