namespace Daoris.Driver;

/// <summary>
/// How a session's work lands: a form, for <see cref="LandingForm.Branch"/> the pattern that names the
/// branch.
/// </summary>
/// <param name="Plugin">
/// The plugin a branch rule hands the new branch to (WSR4, D100): spoken to on <see cref="HookPoints.Land"/>
/// once the branch exists, it pushes and opens the pull request for its platform. Null — the default — is
/// the branch left for the person to push, as it always was.
/// </param>
/// <param name="AutoAccept">
/// <i>Accept automatically</i> (LAND2a, D145): a quest's done lands its work as the person's Accept would, and the rule's
/// plugin pushes it and opens the pull request with no press, the pull request being the last human step. A merge rule takes it
/// too, since the workflow decides (D145's note of 2026-11-01); off, the default, is the press as it always was. It rides the rule, so a repository's own rule replaces
/// its workspace's switch with the rest of it.
/// </param>
/// <param name="Key">
/// The shape of the repository's ticket keys (PROCESS1b-i, D164's amendment point 2): a regular expression the pattern's
/// <c>{ticket}</c> is found by, in the title, then the request, then its links (<see cref="LandingRules.Ticket"/>). Null is
/// <see cref="LandingRules.DefaultKey"/>. Only a branch rule whose pattern says <c>{ticket}</c> takes one.
/// </param>
/// <param name="Squash">
/// The pull request's branch as one commit (PROCESS1b-ii, D164's amendment point 2 and its PROCESS1b-ii note): Accept makes it
/// from the session's tree, its parent where the session left the line, its message the quest's title and the session's commits'
/// subjects (<see cref="SquashMessage"/>), with git's own identity in the repository. Only a branch rule takes it; off, the
/// default, is the branch at the session's own commits, as it always was. A tree on a branch of its own is never squashed.
/// </param>
public sealed record LandingRule(
    string Form, string? Pattern = null, string? Plugin = null, bool AutoAccept = false, string? Key = null,
    bool Squash = false)
{
    public static readonly LandingRule Merge = new(LandingForm.Merge);

    /// <summary>
    /// The work's cycle after the landing (FABRIC-GRAPH1; the fabric events design §C2, §E7): a graph fragment <c>{ nodes, wires, entries,
    /// end, cleanup }</c>, its JSON text as the file holds it, compact. Null is the default, today's: ended when the line holds the work,
    /// the cleanup at <i>held</i>. It rides the rule (D164's amendment), so a repository's own rule replaces its workspace's cycle with the
    /// rest of it; a door with no field for it keeps it (<see cref="DriverConfig.WithLanding"/>). <see cref="NodeGraphHomes"/> reads it.
    /// </summary>
    public string? Cycle { get; init; }

    /// <summary>
    /// How this install integrates the repository's line (INTEGRATE1d; D215 point 2, the integration design §4.1): only on a branch
    /// rule with no plugin and no <see cref="Squash"/>, inherited whole with the rule (D164), so a build that knows no
    /// <c>integrate</c> reads the rule as the branch rule it is and merges nothing. Null is none: the branch is the result, as
    /// always. A door with no field for it keeps it (<see cref="DriverConfig.WithLanding"/>); only
    /// <see cref="DriverConfig.WithLandingIntegrate"/> sets or clears it. Nothing acts on it yet.
    /// </summary>
    public LandingIntegrate? Integrate { get; init; }
}
