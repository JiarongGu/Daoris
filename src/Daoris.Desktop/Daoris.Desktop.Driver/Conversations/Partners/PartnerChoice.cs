namespace Daoris.Driver;

/// <summary>
/// The agent a piece is written by (COAGENT1b; the co-agent design §2.3 step 3, §3.3): the one the person's ask names, walked as a
/// helper's is (<see cref="ReviewerChoice"/>) for its account, then admitted only where its confinement was measured in the effective
/// configuration a start would hand it.
/// </summary>
/// <remarks>
/// The measurements table is a later sub-feature of COAGENT1b (D199's part-four note), and until it holds a row nothing is measured:
/// every agent the walk chooses is refused <see cref="PartnerWhy.NotMeasured"/>, the person's own ask included (D199 point 4). A test
/// hands its own choice through the loop's <c>PartnerChoices</c>, which is never a production start.
/// </remarks>
public static class PartnerChoice
{
    public static async Task<ReviewerChoice> ChooseAsync(
        HarnessRoster roster, PartnerAsk ask, string? leadAdapter, DriverConfig config, string? workspace, CancellationToken ct = default)
    {
        // The person's ask names its agent; a lead's ask under the rule is COAGENT1d's. Nothing is substituted for a name.
        if (ask.Agent is not { Length: > 0 } named)
        {
            return new ReviewerChoice(null, null, PartnerWhy.NotListed, "a piece names the agent that writes it, and this one names none.");
        }

        var walked = await ReviewerChoice.ChooseAsync(
            roster, OpinionRule.None with { Reviewers = [named] }, leadAdapter is { Length: > 0 } adapter ? [adapter] : [], config, workspace, ct)
            .ConfigureAwait(false);
        return walked.Chosen ? Admit(walked) : walked;
    }

    /// <summary>
    /// Whether a chosen agent may start as a partner: only by a measured row naming its effective configuration. None is held yet, so
    /// none is admitted.
    /// </summary>
    public static ReviewerChoice Admit(ReviewerChoice chosen) =>
        new(null, null, PartnerWhy.NotMeasured,
            $"{chosen.Reviewer} is not admitted as a partner: no measurement of its confinement holds the configuration a start would hand it.");
}

/// <summary>How a partner's turn ended, from what Daoris observed of it (the design §2.3 steps 6 and 11), never from its words.</summary>
public static class PartnerTurns
{
    /// <param name="byPerson">The person's <i>Stop…</i> ended it.</param>
    /// <param name="exitCode">Its exit; null where its bound cut it.</param>
    /// <param name="limited">Its account's limit was met.</param>
    public static string End(bool byPerson, int? exitCode, bool limited) =>
        byPerson ? PartnerEnds.Stopped
        : exitCode is null ? PartnerEnds.OutOfTime
        : limited ? PartnerEnds.Limited
        : PartnerEnds.Answered;
}
