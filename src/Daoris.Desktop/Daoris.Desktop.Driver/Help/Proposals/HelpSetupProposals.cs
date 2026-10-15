namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>setup</c> proposal (LAYOUT8, D170 point 1, D117 §6.1): a registered repository set up for every agent — its
/// door <c>setup</c>, its target the repository as the registry names it — judged by the set-up press's own plan, the one
/// <c>daoris-driver setup &lt;repository&gt; --plan</c> prints, and applied as that press: the rule into the repository's scope,
/// then one ask to it, as the person's (D124 §2).
/// </summary>
/// <remarks>
/// <para><b>Why a door and not a shell</b> (D170 point 1): the owner asked Ask Daoris to include a registered, not adopted
/// repository, and it could only name the terminal command. A helper that ran commands could run any driver verb, a change
/// nobody confirmed (D89, HELP4), so the act is a card the person applies, as every other.</para>
///
/// <para><b>The press's own refusals</b>, every one at once in its order (D124 §2.1): a proposal the press would refuse is never
/// shown, and the helper hears each refusal with the door that answers it. The plan is read where a proposal is pending
/// (<see cref="HelpMachineFacts.Setups"/>), since it reads the line with git and asks the doctrine tool its version.</para>
///
/// <para><b>Room for WSSETUP7</b> (D124 §4.5): its <c>register</c> door and a workspace's plan with its pause, resume and stop
/// are doors of this kind, each refused here until built.</para>
/// </remarks>
internal sealed class HelpSetupProposals : IHelpProposalKind
{
    public string Kind => "setup";

    public string Tool => "setup_propose";

    public IReadOnlyList<string> Doors { get; } = ["setup"];

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        if (proposal.Door != "setup") return new($"`{proposal.Door}` is not a door of a set-up — `setup`.", "", "", null);
        if (facts.Setups.FirstOrDefault(each => string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase)) is not { } setup)
        {
            return new($"what a set-up of `{name}` would ask was not read here.", "", $"daoris-driver setup {name}", null);
        }

        // The registry's spelling, as the press and the terminal say it.
        var terminal = $"daoris-driver setup {setup.Repository}";
        if (!setup.Pressable)
        {
            return new(setup.Refusals.Count == 0
                ? $"there is nothing to ask of `{setup.Repository}`."
                : string.Join(" ", setup.Refusals.Select(refusal => refusal.Sentence)), "", terminal, null);
        }

        var carried = setup.Adapter is { } adapter ? $", carried by its own session on `{adapter}`" : ", carried by its own session";
        var lands = setup.Landing is { } landing ? $" and landed {SetupPressRules.LandsAs(landing)}" : "";
        return new HelpPlan(null,
            $"Set `{setup.Repository}` up for every agent: publish one quest to it in workspace `{setup.Workspace}`, as your ask, "
            + $"“{setup.Title}”{carried}{lands}; and add the {SetupPressRules.Rules.Count} doctrine commands its session runs to "
            + $"`{setup.Repository}`'s rules. Daoris writes nothing into `{setup.Repository}`: its session does the work on a branch "
            + "of its own.",
            terminal, null)
        {
            Setup = setup,
        };
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var setup = applying.Plan.Setup!;
        var outcome = await applying.Doors.SetUpAsync(setup, ct).ConfigureAwait(false);
        return applying.Settled(outcome.Published,
            $"{(outcome.Published ? "Applied" : "Not applied")}: `#{applying.Id}` (`{applying.Plan.Terminal}`) — {Said(setup, outcome)}",
            outcome.Message);
    }

    /// <summary>What the press came to, as the terminal says it: the service's sentence, the ask and quest, the rules added.</summary>
    private static string Said(SetupPlan setup, SetupOutcome outcome)
    {
        if (!outcome.Published) return $"{outcome.Message} So nothing was published, and the rules this press added were taken back.";

        var made = new[] { outcome.AskId is { } ask ? $"ask `#{ask}`" : null, outcome.QuestId is { } quest ? $"quest `#{quest}`" : null }
            .OfType<string>()
            .ToList();
        var said = outcome.Message + (made.Count > 0 ? $" ({string.Join(", ", made)})" : "");
        // HELPSETUP1: a repository's rules are on its Setup → Reach since UX6f (D150 §3.1), as the terminal's press says.
        return outcome.Added.Count == 0
            ? said
            : $"{said}; added to `{setup.Repository}`'s rules: {string.Join(", ", outcome.Added.Select(rule => $"`{rule}`"))}, taken "
              + "back in Repositories → the repository's page → Setup → Reach, or "
              + $"`daoris agent rules remove <rule> --repository {setup.Repository}`.";
    }
}

public interface IHelpSetupDoors
{
    /// <summary>
    /// The set-up press (LAYOUT8), as <c>daoris-driver setup &lt;repository&gt;</c> presses the plan it read: the rule into the
    /// repository's scope, then one ask to it; a refusal from the service takes back the rules it added
    /// (<see cref="SetupPress.ApplyAsync"/>).
    /// </summary>
    Task<SetupOutcome> SetUpAsync(SetupPlan plan, CancellationToken ct);
}
