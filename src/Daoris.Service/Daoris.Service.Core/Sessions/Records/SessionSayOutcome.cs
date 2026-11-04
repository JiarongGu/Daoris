namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="SessionSayRefusal.None"/> when the words were kept.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Session">The session as it now stands, when the words were kept.</param>
/// <param name="Word">The word as it was kept, with its id, when they were; a reword's new word (SAIDBACK1a).</param>
public sealed record SessionSayOutcome(SessionSayRefusal Refusal, string Message, Session? Session, SaidWord? Word = null)
{
    /// <summary>The word a take-back took off the record, as it waited (SAIDBACK1a), when it did.</summary>
    public SaidWord? Taken { get; init; }

    /// <summary>
    /// The say was answered with the word its producer's key already kept (COWORK1b, the message-urgency design §6): nothing was kept
    /// now, so nothing beside the say is kept again either.
    /// </summary>
    public bool Again { get; init; }

    /// <summary>
    /// The say was answered <b>settled</b> (FOUNDATION1 row p, the design §3.4 rule 5): the key's word left a record of the line within the
    /// horizon, and this names that record. Its own terminal kind, never a word: <see cref="Refusal"/> is none, <see cref="Session"/> and
    /// <see cref="Word"/> are null, and nothing was kept now, queued, steered, attempted or taken. Only a say that asked for it
    /// (<c>settled</c>) is answered so.
    /// </summary>
    public string? SettledOn { get; init; }

    /// <summary>The head of the record's line, where the say was refused <see cref="SessionSayRefusal.Continued"/> (row d-ii): the words go there.</summary>
    public string? Head { get; init; }

    /// <summary>
    /// Where a move's word went (LINESTOGETHER1b): the head it was kept on, or, answering a retry, the record it waits on or left; and the
    /// head that refused it, where a running head did.
    /// </summary>
    public string? MovedTo { get; init; }

    /// <summary>
    /// The words answered a park (ANSWER1b), which its door keeps on the ask at once. False for words to an ended record, which are
    /// kept there once taken, and for words kept for a running record's run (COWORK1c), which its run keeps there as it takes them.
    /// </summary>
    public bool Answers { get; init; }

    /// <summary>The quest of a session that stood down, where that refused the words.</summary>
    public string? Quest { get; init; }

    /// <summary>The ask an intake answers, where that refused the words.</summary>
    public string? Ask { get; init; }

    /// <summary>The machine a teammate's record ran on, where that refused the words.</summary>
    public string? Origin { get; init; }

    /// <summary>The second opinion a reviewer's record read the work for, where that refused the words.</summary>
    public string? Opinion { get; init; }
}
