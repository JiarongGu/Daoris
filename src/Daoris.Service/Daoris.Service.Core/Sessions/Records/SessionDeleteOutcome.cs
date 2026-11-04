namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="SessionDeleteRefusal.None"/> when it was deleted, or would be.</param>
/// <param name="Message">The full answer, phrased once here.</param>
/// <param name="Session">The record judged, when there is one.</param>
public sealed record SessionDeleteOutcome(SessionDeleteRefusal Refusal, string Message, Session? Session)
{
    /// <summary>
    /// The quest it served, or the quest it published, where that refused it. Null for a chat that took one, whose record
    /// says it took a quest but not which (CHATTAKE1).
    /// </summary>
    public string? Quest { get; init; }

    /// <summary>The ask that names it as its intake, where that refused it.</summary>
    public string? Ask { get; init; }

    /// <summary>The machine a teammate's record ran on, where that refused it.</summary>
    public string? Origin { get; init; }

    /// <summary>The workspace whose remote holds it, where that refused it.</summary>
    public string? Workspace { get; init; }
}
