namespace Daoris.Driver;

/// <summary>
/// The one rule of whether a start's words go on in its record's own conversation (MSG1g, D137 §2.2; D205 point 1; the agent-provider design
/// §7 row 3, s-ii): words the planner took up, on the record's own door. The continuation's walk asks it of the agent its start runs on, and the
/// sessions' groups of the agent a fresh plan's start would go on with, so the group's report of a resume says what the continuation does.
/// </summary>
/// <remarks>
/// The record's door, never the machine's <c>adapter</c> key: a record that ran on Build's second choice goes on in its own conversation. A
/// record that ran on another agent than the one asked is carried on by the walk's pick, since its conversation cannot go on there whichever
/// account runs (D131 §2's <c>adapter</c>). Never judged by the quest's <c>awaits</c> (SAYHELD1).
/// </remarks>
public static class OwnResume
{
    /// <summary>The record the start's words go on in, on its own conversation; null for every other start.</summary>
    /// <param name="agent">The agent the start goes on with.</param>
    /// <param name="keptOn">The agent a session's kept conversation was bound under, where its record names none.</param>
    public static PriorSession? Of(Consideration start, string agent, Func<string, string?> keptOn)
    {
        if (!start.GoesOn || start.Resumes is not { WordsWaiting: true } words) return null;
        var ranOn = words.Adapter ?? keptOn(words.Session);
        return ranOn is not { Length: > 0 } || string.Equals(ranOn, agent, StringComparison.OrdinalIgnoreCase) ? words : null;
    }
}
