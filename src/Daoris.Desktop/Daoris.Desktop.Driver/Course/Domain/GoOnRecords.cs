namespace Daoris.Driver;

/// <summary>Words a record could not go on with, by their ids, why by a code, and when it is judged.</summary>
/// <param name="Said">The ids of the words judged, as the record's <c>said</c> names them.</param>
/// <param name="Why">The reason's code (<see cref="ContinueWhy"/>): what the page says its own sentence for.</param>
public sealed record GoOnMark(IReadOnlyList<string> Said, string Why, DateTimeOffset At)
{
    /// <summary>
    /// The agent whose answer the answers-alone bound held (<see cref="GoOnMarks.AnswersAlone"/>), by its adapter, which the
    /// person's wait is worded by. Null for every other mark, and for one kept before the field, which reads as it did.
    /// </summary>
    public string? Agent { get; init; }
}

/// <summary>A session's mark, as every mark this home keeps is listed for the waits the person answers.</summary>
public sealed record SessionMark(string Session, GoOnMark Mark);

/// <summary>What the person's answer to an answers-alone wait came to: whether it stands, and where not, its code and sentence.</summary>
/// <param name="Why">Why not, by a code of <see cref="AnswersAloneWord"/>; null where it stands.</param>
/// <param name="Message">The sentence, in the terminal's words; the screen words <see cref="Why"/> itself.</param>
public sealed record AloneAnswer(bool Stands, string? Why, string Message);

/// <summary>The person's choice to go on in a new session: the ids of the words waiting when they chose, and when.</summary>
public sealed record NewSessionChoice(IReadOnlyList<string> Said, DateTimeOffset At);

/// <summary>What *Go on in a new session* came to: kept for the next look, or refused by a code, and its sentence.</summary>
/// <param name="Sent">The choice is kept: the driver's next look carries the words on in a new session.</param>
/// <param name="Why">Why not, by a code of <see cref="WordsNever"/> or <see cref="GoOnNew"/>; null where it is kept.</param>
/// <param name="Message">The sentence, in the terminal's words; the screen words <see cref="Why"/> itself.</param>
public sealed record GoOnNewAnswer(bool Sent, string? Why, string Message);
