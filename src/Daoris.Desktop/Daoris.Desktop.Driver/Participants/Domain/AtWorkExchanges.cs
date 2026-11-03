using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>
/// One request of the attended line as <i>At work</i> draws it (COMMS1i, the comms design §7): a co-worker asked, a quest waited on, a
/// successor asked for. Plain values the layer above maps from the exchanges' read (<c>Exchanges.Of</c>), so the composition names no
/// work order (design §13 point 9). Delivery, what Daoris saw, and outcome, the protocol's own word, stay apart.
/// </summary>
/// <param name="Id">The exchange's id: <c>helper:h7</c>, <c>piece:p1</c>, <c>quest:q9</c> or <c>hand-over:&lt;id&gt;</c>.</param>
/// <param name="Part">The part of the line it belongs to: after a hand-over, a predecessor's open request is the line's.</param>
/// <param name="Expects"><c>answer</c>, <c>piece</c>, <c>work</c> or <c>successor</c>.</param>
/// <param name="To">The agent asked, the repository a quest went to, or the word the request names where none is known yet.</param>
/// <param name="Holds">Whether the line waits on it: it has not come back.</param>
public sealed record AtWorkExchange(string Id, string Part, string Expects, string To, string Delivery, string Outcome, bool Holds)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? At { get; init; }

    /// <summary>The session that took it up: a helper's or a partner's.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Session { get; init; }

    /// <summary>The agent that took it up, where the record says.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Agent { get; init; }

    /// <summary>When its answer came, where one came.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Answered { get; init; }

    /// <summary>A quest's lanes, as an address writes them.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Lanes { get; init; }

    /// <summary>A source read earlier than the rest of the look.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stale { get; init; }
}

/// <summary>The attended line's requests, oldest first, and the sources that could not be read: said, never drawn as none.</summary>
public sealed record AtWorkExchanges(IReadOnlyList<AtWorkExchange> Rows, IReadOnlyList<string> Unread)
{
    public static AtWorkExchanges None { get; } = new([], []);
}

/// <summary>The exchanges half's port (COMMS1i), which the layer above fills: the attended line's requests. Null reads none.</summary>
public delegate AtWorkExchanges AtWorkExchanging(string attended);
