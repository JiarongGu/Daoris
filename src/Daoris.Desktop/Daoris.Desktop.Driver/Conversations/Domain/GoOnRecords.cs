namespace Daoris.Driver;

/// <summary>Words a record could not go on with, by their ids, why by a code, and when it was judged (MSG1b).</summary>
/// <param name="Said">The ids of the words judged, as the record's <c>said</c> names them.</param>
/// <param name="Why">The reason's code (<see cref="ContinueWhy"/>): what the page says its own sentence for.</param>
public sealed record GoOnMark(IReadOnlyList<string> Said, string Why, DateTimeOffset At)
{
    /// <summary>
    /// The agent whose answer the answers-alone bound held (COWORK1e, <see cref="GoOnMarks.AnswersAlone"/>), by its adapter, which the
    /// person's wait is worded by. Null for every other mark, and for one kept before the field, which reads as it did.
    /// </summary>
    public string? Agent { get; init; }
}

/// <summary>A session's mark, as every mark this home keeps is listed for the waits the person answers (COWORK1e).</summary>
public sealed record SessionMark(string Session, GoOnMark Mark);

/// <summary>What the person's answer to an answers-alone wait came to (COWORK1e): whether it stands, and where not, its code and sentence.</summary>
/// <param name="Why">Why not, by a code of <see cref="AnswersAloneWord"/>; null where it stands.</param>
/// <param name="Message">The sentence, in the terminal's words; the screen words <see cref="Why"/> itself.</param>
public sealed record AloneAnswer(bool Stands, string? Why, string Message);

/// <summary>The person's choice to go on in a new session (MSG1g): the ids of the words waiting when they chose, and when.</summary>
public sealed record NewSessionChoice(IReadOnlyList<string> Said, DateTimeOffset At);

/// <summary>What *Go on in a new session* came to (MSG1g): kept for the next look, or refused by a code, and its sentence.</summary>
/// <param name="Sent">The choice is kept: the driver's next look carries the words on in a new session.</param>
/// <param name="Why">Why not, by a code of <see cref="WordsNever"/> or <see cref="GoOnNew"/>; null where it was kept.</param>
/// <param name="Message">The sentence, in the terminal's words; the screen words <see cref="Why"/> itself.</param>
public sealed record GoOnNewAnswer(bool Sent, string? Why, string Message);

/// <summary>The harness's own conversation a session ran, as its wire named it, and the adapter that opened it (ANSWER1a).</summary>
/// <param name="Adapter">The Daoris adapter whose door opened it: a conversation is only that door's to resume.</param>
/// <param name="Conversation">The id the harness gave it: ACP's <c>sessionId</c>, or the native door's <c>session_id</c>.</param>
public sealed record HarnessConversation(string Adapter, string Conversation);
