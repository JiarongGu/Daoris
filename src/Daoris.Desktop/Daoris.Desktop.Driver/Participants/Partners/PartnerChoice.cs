namespace Daoris.Driver;

/// <summary>
/// The agent a piece is written by: the one the person's ask names, walked as a
/// helper's is (<see cref="ReviewerChoice"/>) for its account, then admitted only where its confinement is measured in the effective
/// configuration a start would hand it.
/// </summary>
/// <remarks>
/// Admission reads the measurements table (<see cref="PartnerMeasurements"/>) twice: here, whether any row could admit the agent at the
/// version its account resolved to; and at the start, before anything of the partner's is made, whether a row holds the whole effective
/// configuration the start would hand it. Until M2 writes a row, every agent is refused <see cref="PartnerWhy.NotMeasured"/>, the
/// person's own ask included. A test's stand-in rows are read only where a test hands them in, never at a production start.
/// </remarks>
public static class PartnerChoice
{
    /// <param name="measured">The measurements a test hands in for its stand-ins, or null for a production start's, which reads the code's table.</param>
    /// <param name="passed">
    /// The (entry, account) this piece's call already passed at an open's refusal: the named agent's other accounts are
    /// walked, then its other entries, and never another agent.
    /// </param>
    public static async Task<ReviewerChoice> ChooseAsync(
        HarnessRoster roster, PartnerAsk ask, string? leadAdapter, DriverConfig config, string? workspace,
        IReadOnlyList<PartnerMeasurement>? measured = null, CancellationToken ct = default, IReadOnlyList<ReviewerPassed>? passed = null,
        Func<RoleCatalog>? catalogue = null)
    {
        // The person's ask names its agent. Nothing is substituted for a name.
        if (ask.Agent is not { Length: > 0 } named)
        {
            return new ReviewerChoice(null, null, PartnerWhy.NotListed, "a piece names the agent that writes it, and this one names none.");
        }

        // Named by the ask, so the actor supplies only the model and effort of the agent named, as its entries for it in order.
        var walked = await ReviewerChoice.ChooseAsync(
                roster, OpinionRule.None with { Reviewers = [named] }, leadAdapter is { Length: > 0 } adapter ? [adapter] : [], config, workspace, ct,
                "partner", passed, catalogue: catalogue)
            .ConfigureAwait(false);
        return walked.Chosen ? Admit(walked, measured ?? PartnerMeasurements.Rows, production: measured is null) : walked;
    }

    /// <summary>
    /// Whether a chosen agent may go on to its start as a partner: only where a row of <paramref name="rows"/> could admit it at the version
    /// its account resolved to. The start then holds its whole effective configuration to a row (<see cref="PartnerMeasurements.Admitting"/>).
    /// </summary>
    public static ReviewerChoice Admit(ReviewerChoice chosen, IReadOnlyList<PartnerMeasurement> rows, bool production) =>
        PartnerMeasurements.MayAdmit(rows, chosen.Reviewer ?? "", chosen.Selection?.Version, production)
            ? chosen
            : new(null, null, PartnerWhy.NotMeasured,
                $"{chosen.Reviewer} is not admitted as a partner: no measurement of its confinement holds the configuration a start would hand it.");
}

/// <summary>How a partner's turn ended, from what Daoris observed of it, never from its words.</summary>
public static class PartnerTurns
{
    /// <param name="byPerson">The person's <i>Stop…</i> ended it.</param>
    /// <param name="exitCode">Its exit; null where its bound cut it.</param>
    /// <param name="limited">Its account's limit is met.</param>
    public static string End(bool byPerson, int? exitCode, bool limited) =>
        byPerson ? PartnerEnds.Stopped
        : exitCode is null ? PartnerEnds.OutOfTime
        : limited ? PartnerEnds.Limited
        : PartnerEnds.Answered;
}
