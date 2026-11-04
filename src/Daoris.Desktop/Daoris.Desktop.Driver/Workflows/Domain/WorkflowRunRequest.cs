namespace Daoris.Driver;

/// <summary>
/// One request the work's sessions made, drawn inside the work (INTEGRATE1o part o2; the integration design §17.3): a quest they published, or a
/// helper they asked, each the record's own. Never stored: derived from the quests' and sessions' records at each look.
/// </summary>
/// <param name="Kind">One of <see cref="WorkflowRunRequestKinds"/>.</param>
/// <param name="Id">The quest's id, or the helper's session.</param>
/// <param name="To">The repository the quest was asked of; the helper's agent.</param>
/// <param name="State">A quest's status lowercased (<c>open</c>, <c>taken</c>, <c>done</c>, <c>declined</c>); a helper's: <c>working</c>, <c>done</c>, <c>failed</c> or <c>stopped</c>.</param>
public sealed record WorkflowRunRequest(string Kind, string Id, string To, string State)
{
    /// <summary>The session of the work that made it: the quest's publisher, the session a helper serves.</summary>
    public string? Session { get; init; }

    /// <summary>Whether a session of the work waits on its answer (D79): its quest names it and it is not closed. Never true of a helper.</summary>
    public bool Awaits { get; init; }

    /// <summary>What its close said, a done's note or a decline's reason; a helper's own note. Shown as written.</summary>
    public string? Words { get; init; }

    /// <summary>When it last moved, where its record kept a moment.</summary>
    public DateTimeOffset? At { get; init; }
}
