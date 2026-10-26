namespace Daoris.Driver;

/// <summary>
/// What the service said of the person's words to a parked or ended session of this machine's (MSG1a's say door, MSG1d):
/// whether it kept them for the session to go on with, and its sentence.
/// </summary>
/// <param name="Kept">The words are on the record, waiting for it to go on with them.</param>
/// <param name="Message">The service's sentence: its yes, or its refusal, verbatim.</param>
public sealed record SayAnswer(bool Kept, string Message)
{
    /// <summary>
    /// The refusal's word, which a reader acts on instead of the sentence: <c>running</c>, <c>not-ours</c>, <c>intake</c>,
    /// <c>stood-down</c>, <c>not-found</c> or <c>no-words</c>. Null when kept, and for a host with no door.
    /// </summary>
    public string? Refusal { get; init; }

    /// <summary>The word as kept: its id, which the record's events say again where the session took it. Null when refused, and when settled.</summary>
    public SaidWordView? Word { get; init; }

    /// <summary>
    /// The record the key's word left, where the host answered the say <b>settled</b> (FOUNDATION1 row p, the design §3.4 rule 5): the word was
    /// kept by an earlier say under this key, left a record of the line and is not to be kept or sent again. Its own terminal kind: <see cref="Kept"/>
    /// reads true, so a caller that only asks whether the words are on the line reads it as kept, and <see cref="Word"/> is null, so no reader
    /// finds a word to queue, steer, attempt, take or show. Only a keyed say asks for it; null from a host before the row and for any other answer.
    /// </summary>
    public string? Settled { get; init; }
}

/// <summary>
/// What the service said of marking words attempted before the wire (COWORK1c's attempt door; FOUNDATION1 row p, the design §3.4 rule 5): whether
/// the mark was acknowledged and which of the words asked it names marked. A word it does not name is not attempted and does not go.
/// </summary>
/// <param name="Ok">The record acknowledged the mark: it answered, and not with a refusal.</param>
/// <param name="Message">The service's sentence, or why there was none.</param>
/// <param name="Marked">The words asked that the answer marked, in the order asked: none where the mark was not acknowledged.</param>
/// <param name="Unmarked">The words asked that no longer wait on the record, or all of them where the mark was not acknowledged.</param>
public sealed record AttemptAnswer(bool Ok, string Message, IReadOnlyList<string> Marked, IReadOnlyList<string> Unmarked);

/// <summary>
/// What the service said of one word waiting on a record that the person took back or reworded (SAIDBACK1a's take-back door,
/// D137 §2.4): whether it left the record, and its sentence.
/// </summary>
/// <param name="Done">The word is off the record; a reword's new words wait in its stead.</param>
/// <param name="Message">The service's sentence: its yes, or why not, verbatim.</param>
/// <param name="Why">
/// Why not, as the service's word: <c>taken</c>, where the word no longer waits or the record runs again, or the say door's
/// refusals (<c>not-ours</c>, <c>intake</c>, <c>stood-down</c>, <c>not-found</c>, <c>no-words</c>, …). Null when done, and for a
/// host with no door.
/// </param>
/// <param name="Word">A reword's new word, with the id it now waits under. Null otherwise.</param>
public sealed record TakeBackAnswer(bool Done, string Message, string? Why = null, SaidWordView? Word = null);

/// <summary>
/// What the ledger said of deleting a session's record (SESSUX1f, D126 §5.4): whether it did, or would; and if not its
/// sentence, its word (<c>not-found</c>, <c>not-ours</c>, <c>live</c>, <c>served-quest</c>, <c>named</c>,
/// <c>on-remote</c>), which a reader acts on instead of the sentence, and the facts the word names.
/// </summary>
/// <param name="Taken">It was deleted, or the judgement says it would be.</param>
/// <param name="Message">The ledger's sentence: its yes, or its refusal, verbatim.</param>
public sealed record SessionDeleteAnswer(bool Taken, string Message)
{
    public string? Refusal { get; init; }
    public string? Quest { get; init; }
    public string? Ask { get; init; }
    public string? Origin { get; init; }
    public string? Workspace { get; init; }
}

/// <summary>What the service said to an ask, a publish or a close (D65 §1a).</summary>
/// <param name="Ok">Whether it did what was asked — false is a refusal, said in <paramref name="Message"/>.</param>
/// <param name="Message">The service's whole sentence, verbatim.</param>
/// <param name="AskId">The ask it answered about, when it answered with one.</param>
/// <param name="QuestId">The quest this call published, when it published one.</param>
public sealed record AskAnswer(bool Ok, string Message, string? AskId, string? QuestId);
