namespace Daoris.Driver;

/// <summary>
/// The reviewer chosen over this machine's agents (XAGENT1b, D155 point 4, the second-agent design §3): the Domain's walk
/// (<see cref="ReviewerChoice"/>), each entry asked of the account walk, unchanged.
/// </summary>
/// <remarks>
/// <b>Named where its callers always named it</b> (STRUCT3c, D166's note): <c>ReviewerChoice.ChooseAsync</c> is a static member of
/// a C# 14 extension block on the Domain's <see cref="ReviewerChoice"/>. The walk's order and words are the Domain's; asking
/// <see cref="HarnessRoster.SelectAsync"/>, which reads the machine's accounts, is this layer's.
/// </remarks>
public static class ReviewerChoices
{
    extension(ReviewerChoice)
    {
        /// <summary>
        /// Choose the reviewer of work the <paramref name="working"/> agents wrote, from <paramref name="rule"/>'s reviewers, for a
        /// repository in <paramref name="workspace"/>.
        /// </summary>
        /// <param name="roster">The machine's agents, the build's and its plugins', and the account walk.</param>
        /// <param name="working">The adapters of the sessions whose work is read: their families are the work's.</param>
        public static async Task<ReviewerChoice> ChooseAsync(
            HarnessRoster roster, OpinionRule rule, IReadOnlyCollection<string> working, DriverConfig config, string? workspace,
            CancellationToken ct = default) =>
            await ReviewerChoice.WalkAsync(roster.Adapters, rule, working, entry => TryAsync(roster, entry, config, workspace, ct)).ConfigureAwait(false);
    }

    /// <summary>One reviewer asked of the account walk: its selection where it may run, else what held it.</summary>
    private static async Task<(HarnessSelection? Selection, ReviewerTried? Held)> TryAsync(
        HarnessRoster roster, ReviewerChoice.Entry entry, DriverConfig config, string? workspace, CancellationToken ct)
    {
        var (name, label) = (entry.Family.Agent, entry.Label);
        // Named, never guessed (D23): a name this machine has no agent of is said, and nothing is asked.
        if (!entry.Known)
        {
            return (null, new ReviewerTried(
                name, label, ReviewerHeld.NotInstalled,
                $"`{name}` is no agent on this machine: neither this build nor an installed plugin declares it."));
        }

        HarnessSelection selection;
        try
        {
            selection = await roster.SelectAsync(name, config, workspace, chosen: null, StartKind.Driven, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return (null, new ReviewerTried(name, label, ReviewerHeld.Refused, error.Message));
        }

        if (selection.Allowed) return (selection, null);

        // What held it, from the walk's facts and never its words: a cool-off waits for a time, so it is named first; a list
        // with none cooling and an account not signed in waits for a sign-in, which its sentence names.
        var held = selection switch
        {
            { Absent: AgentAbsence.NotInstalled } => ReviewerHeld.NotInstalled,
            { Absent: AgentAbsence.PinNotInstalled } => ReviewerHeld.PinNotInstalled,
            { Cooling: not null } => ReviewerHeld.Cooling,
            { NotReady: AccountReadiness.SignedOut } or { SignedOut: not null } => ReviewerHeld.SignedOut,
            _ => ReviewerHeld.Refused,
        };
        return (null, new ReviewerTried(name, label, held, selection.Refusal!) { Cooling = selection.Cooling });
    }
}
