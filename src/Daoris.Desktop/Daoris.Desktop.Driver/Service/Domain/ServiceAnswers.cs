namespace Daoris.Driver;

/// <summary>
/// What the service said of the person's words to a parked or ended session of this machine's:
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
    /// The record the key's word left, where the host answered the say <b>settled</b>: the word has been
    /// kept by an earlier say under this key, left a record of the line and is not to be kept or sent again. Its own terminal kind: <see cref="Kept"/>
    /// reads true, so a caller that only asks whether the words are on the line reads it as kept, and <see cref="Word"/> is null, so no reader
    /// finds a word to queue, steer, attempt, take or show. Only a keyed say asks for it; null from an older host and for any other answer.
    /// </summary>
    public string? Settled { get; init; }

    /// <summary>
    /// The say door's refusal of a word said to a record that is not its line's head:
    /// <see cref="Refusal"/>'s word, beside which the answer names the head as <see cref="Head"/>. The same as <c>WordsNever.Continued</c>, the
    /// driver's own code for a conversation that went on in another, so a reader that knows only that code reads it as its own.
    /// </summary>
    public const string Continued = "continued";

    /// <summary>
    /// The line's head the say door names where it refuses the word <see cref="Continued"/>: the record the word
    /// is said to instead, whole, by a caller that passes it on. Null for any other answer, a <c>continued</c> that names none among them, and
    /// from a host without the <c>head</c> mark, which never refuses so.
    /// </summary>
    public string? Head { get; init; }
}

/// <summary>
/// What the service said of marking words attempted before the wire: whether
/// the mark is acknowledged and which of the words asked it names marked. A word it does not name is not attempted and does not go.
/// </summary>
/// <param name="Ok">The record acknowledged the mark: it answered, and not with a refusal.</param>
/// <param name="Message">The service's sentence, or why there is none.</param>
/// <param name="Marked">The words asked that the answer marks, in the order asked: none where the mark is not acknowledged.</param>
/// <param name="Unmarked">The words asked that no longer wait on the record, or all of them where the mark is not acknowledged.</param>
public sealed record AttemptAnswer(bool Ok, string Message, IReadOnlyList<string> Marked, IReadOnlyList<string> Unmarked);

/// <summary>
/// What the service said of one word waiting on a record that the person took back or reworded:
/// whether it left the record, and its sentence.
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
/// What the service said of a word held on a part that the person moved to the head.
/// </summary>
/// <param name="Moved">The word waits at the head now, or a retry found it moved already.</param>
/// <param name="Message">The service's sentence, verbatim.</param>
/// <param name="Why">
/// Why not, as the service's word: <c>taken</c>, the say door's refusals, and the move's own (<c>not-held</c>, <c>files</c>, <c>at-head</c>,
/// <c>not-yours</c>). Null when moved, and for a host with no door.
/// </param>
public sealed record MoveAnswer(bool Moved, string Message, string? Why = null)
{
    /// <summary>The head the word is kept on; a retry's record where it waits or that it left; a running head that refuses it.</summary>
    public string? To { get; init; }

    /// <summary>A retry the service answers moved already: nothing is kept now.</summary>
    public bool Again { get; init; }

    /// <summary>The word as kept at the head, with its new id. Null otherwise.</summary>
    public SaidWordView? Word { get; init; }
}

/// <summary>
/// What the ledger said of deleting a session's record: whether it did, or would; and if not its
/// sentence, its word (<c>not-found</c>, <c>not-ours</c>, <c>live</c>, <c>served-quest</c>, <c>named</c>,
/// <c>on-remote</c>), which a reader acts on instead of the sentence, and the facts the word names.
/// </summary>
/// <param name="Taken">It is deleted, or the judgement says it would be.</param>
/// <param name="Message">The ledger's sentence: its yes, or its refusal, verbatim.</param>
public sealed record SessionDeleteAnswer(bool Taken, string Message)
{
    public string? Refusal { get; init; }
    public string? Quest { get; init; }
    public string? Ask { get; init; }
    public string? Origin { get; init; }
    public string? Workspace { get; init; }
}

/// <summary>What the service said to an ask, a publish or a close.</summary>
/// <param name="Ok">Whether it did what is asked — false is a refusal, said in <paramref name="Message"/>.</param>
/// <param name="Message">The service's whole sentence, verbatim.</param>
/// <param name="AskId">The ask it answers about, when it answers with one.</param>
/// <param name="QuestId">The quest this call publishes, when it publishes one.</param>
public sealed record AskAnswer(bool Ok, string Message, string? AskId, string? QuestId);
