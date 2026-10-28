namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's room as the events say it (FOUNDATION1 mod-xii, the design §14.2's round 4): the attention headline tells its answer
/// apart, and the participant-work rule tells its sessions by the same spelling. Events is the lowest subdomain that reads it, so the
/// spelling is declared here and the rule, which sits above, aliases it.
/// </summary>
internal static class AskDaorisRoom
{
    /// <summary>
    /// The "repository" Ask Daoris's sessions are recorded in: a colon is in no folder name, so no registered repository is ever
    /// called this.
    /// </summary>
    /// <remarks>A twin (`.claude/knowledge/twins.md`) of <c>HelpRoom.Repository</c>, the service's <c>SessionLedger.HelpRepository</c>
    /// and the page's <c>HELP_REPOSITORY</c>; each side's test holds the spelling.</remarks>
    internal const string HelpRepository = "daoris:help";
}
