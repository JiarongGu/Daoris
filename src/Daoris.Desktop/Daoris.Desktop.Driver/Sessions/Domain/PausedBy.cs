namespace Daoris.Driver;

/// <summary>
/// Whose pause holds a quest (PAUSE1b, D132 point 3): an ask's, over every quest of its work, or a quest's own, over its
/// work. What the planner's <see cref="StartVerdict.Paused"/> names, and what *Resume* takes away.
/// </summary>
/// <param name="Id">The ask's or the quest's id, as <c>driver.json</c> spells it.</param>
public sealed record PausedBy(WorkScope Scope, string Id)
{
    /// <summary><c>ask</c> or <c>quest</c>: the scope's public spelling, which the page and the machine log read.</summary>
    public string Word => Scope == WorkScope.Ask ? "ask" : "quest";

    /// <summary>The terminal's door that resumes it (D50, design §7.2).</summary>
    public string Door => Scope == WorkScope.Ask ? $"daoris-driver ask --resume {Id}" : $"daoris-driver quest resume {Id}";

    /// <summary>
    /// What the record of a session this pause stopped says (design §4.1): a person reads it, and nothing decides from it
    /// (D104). Never translated: a record's note is data.
    /// </summary>
    public string Note => Noted.Note;

    /// <summary><see cref="Note"/> with its code and the work's id (LANG1a, the language design §4 rows 52–53).</summary>
    public Noted Noted => Scope == WorkScope.Ask
        ? Noted.Of(NoteCodes.StoppedPausedAsk, $"paused with ask `#{Id}`.", ("ask", Id))
        : Noted.Of(NoteCodes.StoppedPausedQuest, $"paused with quest `#{Id}`.", ("quest", Id));
}
