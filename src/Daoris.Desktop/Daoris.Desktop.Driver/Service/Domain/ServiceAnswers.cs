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

    /// <summary>The word as kept: its id, which the record's events say again where the session took it. Null when refused.</summary>
    public SaidWordView? Word { get; init; }
}

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
