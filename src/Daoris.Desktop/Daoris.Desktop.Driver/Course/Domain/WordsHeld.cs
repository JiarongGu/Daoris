namespace Daoris.Driver;

/// <summary>
/// What a loop did with words a terminal said to a session (MSG1e, D137 §5.2), written beside the request for the door that
/// asked: the shape the screen's routes answer (<c>{sent, reaches, why}</c>), with the id a record gave words it kept.
/// </summary>
/// <param name="Sent">Held or taken: they reach the session, now or when it can.</param>
/// <param name="Reaches">
/// <c>next-step</c>, <c>turn-end</c> or <c>resume</c>; null where they are taken at once (a conversation between turns) or the
/// door is not known yet (a driven session still opening).
/// </param>
/// <param name="Why">
/// Where nothing took them: a code of <see cref="WordsNever"/>, <see cref="Running"/> or <see cref="Unreached"/>. Null with a
/// <see cref="Message"/> where the loop could not say.
/// </param>
/// <remarks>A record alone, in Conversations' Domain (STRUCT3h, D166's note), so a Domain that answers with it may name it.</remarks>
public sealed record WordsHeld(bool Sent, string? Reaches, string? Why)
{
    /// <summary>
    /// The record still runs where this loop's registry does not reach its door: a session winding up, or one still opening. Its
    /// record ends or opens in a moment, so the asker says the words again; nothing here would hold them meanwhile.
    /// </summary>
    public const string Running = "running";

    /// <summary>A conversation this loop runs, whose turns take only the words its runner is handed: the window's box.</summary>
    public const string Unreached = "unreached";

    /// <summary>The id the record gave the words it kept, which its events say again where the session took them.</summary>
    public string? Word { get; init; }

    /// <summary>
    /// The person's *Send now* was carried out (STEER2c): steered into the running turn, or held with the turn stopped so they go
    /// next. Where it was not, <see cref="Why"/> is its <see cref="SendNowWhy"/> code beside <c>Sent</c>. False for an older answer.
    /// </summary>
    public bool Now { get; init; }

    /// <summary>The sentence where nothing took them: the service's, or why the loop could not.</summary>
    public string? Message { get; init; }

    public static WordsHeld Refused(string why, string? message = null) => new(false, null, why) { Message = message };

    public static WordsHeld Failed(string message) => new(false, null, null) { Message = message };
}
