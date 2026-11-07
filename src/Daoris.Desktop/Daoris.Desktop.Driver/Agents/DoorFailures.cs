namespace Daoris.Driver;

/// <summary>
/// Each transport's <see cref="DoorFailure"/> from its own wire (the agent-provider design §2.7, row l): the payload sits with the readings
/// below Agents, and what builds it from a protocol refusal or a pipe result stays with the transport that reads that wire.
/// </summary>
public static class DoorFailures
{
    /// <summary>The protocol door's prompt failure (the agent-errors design §4.2): the refusal's code, its <c>data.errorKind</c> and its own sentence.</summary>
    public static DoorFailure OfPrompt(AcpRefusal refused) =>
        new("protocol", refused.Code, refused.ErrorKind, null, refused.Words, DoorWhere.Prompt);

    /// <summary>
    /// The protocol door's failure met at <paramref name="where"/>: a refusal's code and words where the failure carried one, else its message
    /// alone, with no code and no kind.
    /// </summary>
    public static DoorFailure OfProtocol(Exception error, string where) => error switch
    {
        AcpRefusal refused => OfPrompt(refused) with { Where = where },
        AcpWordTurnFailed { Refusal: { } refused } => OfPrompt(refused) with { Where = where },
        _ => new("protocol", null, null, null, error.Message, where),
    };

    /// <summary>The native door's failed <c>result</c>: its own words, the wire carrying no code and no kind there.</summary>
    public static DoorFailure OfResult(string words) => new("native", null, null, null, words, DoorWhere.Result);

    public static DoorFailure OfTurnEnd(string words) => new("protocol", null, null, null, words, DoorWhere.Prompt);
}
