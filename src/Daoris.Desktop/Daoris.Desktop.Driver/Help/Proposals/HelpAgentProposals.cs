using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>agent</c> proposal (HELP6): an agent's Update or a pin — its door <c>update</c> or <c>pin</c>,
/// its target the agent, its value a pin's version — judged by what the Agents screen offers and what
/// <c>HARNESS_ACTION</c> refuses: Update where the roster's <c>updates</c> names one and the agent is
/// installed, a pin where the door declares a package or a channel, of one exact release.
/// </summary>
/// <remarks>
/// <para>Applied as <c>HARNESS_ACTION</c>'s own start, one at a time; its end is said into the conversation after
/// what the Apply did (<see cref="HelpProposals.ApplyAsync"/>).</para>
///
/// <para>Since HELP10 (D110) a third door, <c>default</c>: which of its accounts an agent runs as by default, its value
/// the account and its workspace one for a single workspace, judged as <c>daoris agent profile default</c> judges it —
/// an account that exists — and applied as <c>HARNESS_ACTION</c>'s own <c>profile-default</c>.</para>
/// </remarks>
internal sealed partial class HelpAgentProposals : IHelpProposalKind
{
    public string Kind => "agent";

    // HELP6: every door built since HELP1c, each a card the person applies the same way.
    public string Tool => "agent_propose";

    public IReadOnlyList<string> Doors { get; } = ["update", "pin", "default"];

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        if (proposal.Door == "default") return Default(proposal, facts);

        var name = proposal.Target?.Trim() ?? "";
        var version = proposal.Value?.Trim() ?? "";
        var update = proposal.Door == "update";
        var terminal = update ? $"daoris agent update {name}" : $"daoris agent pin {name} {version}";
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (proposal.Door is not ("update" or "pin")) return Refused($"`{proposal.Door}` is not an agent's change — `update`, `pin` or `default`.");
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return Refused($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.");
        }

        if (update)
        {
            if (!door.Present)
            {
                return Refused($"`{name}` is not installed on this machine, so there is nothing to update — install it under "
                    + $"Settings → Agents & accounts, or with `daoris agent install {name}`.");
            }

            return door.Updates switch
            {
                "pin" => new HelpPlan(null,
                    $"Update `{name}`: move its pin from {door.Pinned} to the newest release, installed before the pin moves.", terminal, null),
                "tool" => new HelpPlan(null, $"Update `{name}` with its own updater.", terminal, null),
                _ => Refused(door.Pinned is { Length: > 0 } pinned
                    ? $"`{name}` offers no Update: it is pinned at {pinned} and declares no package or release channel to find a newer "
                      + $"version in. Pin another with `daoris agent pin {name} <version>`, or unpin it."
                    : $"`{name}` offers no Update: it declares no updater — it updates itself, or its package manager does."),
            };
        }

        if (version.Length == 0) return Refused("a pin names one exact release, such as 2.1.300.");
        if (door.Package is not { Length: > 0 } && door.Channel is not { Length: > 0 })
        {
            return Refused($"`{name}` declares no package or release channel, so Daoris has no sanctioned way to fetch a version of it. "
                + "Install it with its own tooling and Daoris will find it on PATH.");
        }

        if (door.Channel is { Length: > 0 } channel)
        {
            // The channel's own refusals, in its words: the one channel this build installs from, and a version
            // it can verify (AGT2b).
            if (channel != ClaudeReleases.Channel)
            {
                return Refused($"this build installs from no `{channel}` channel, so nothing would be fetched or pinned. "
                    + $"`daoris agent pin {name} {version}` in a terminal knows every channel Daoris does.");
            }

            try
            {
                ClaudeReleases.RefuseVersion(version);
            }
            catch (DriverException refused)
            {
                return Refused(refused.Message);
            }
        }
        else if (!ExactRelease().IsMatch(version))
        {
            // npm would take a pointer, and a pin naming one would change under a running arrangement (USE1a).
            return Refused($"`{version}` is not one exact release — a pin names one, like 1.2.3, never a pointer such as latest.");
        }

        return new HelpPlan(null,
            $"Pin `{name}` to {version}: install that version where Daoris keeps it, then run it"
            + (door.Pinned is { Length: > 0 } was && was != version ? $" in place of {was}." : "."),
            terminal, null);
    }

    /// <summary>
    /// <c>default</c> (HELP10): which account an agent's sessions run as, for the machine or one workspace (D49 §4), as
    /// <c>daoris agent profile default</c> takes it — an account that exists, never one made by naming it — said in its
    /// words. A door's default is its owner's (AGT7), so the command and the sentence name the owner.
    /// </summary>
    /// <remarks>
    /// The screen can also clear a default back to the tool's own home, which no terminal verb does; that stays the
    /// screen's press, so a default here always names an account.
    /// </remarks>
    private static HelpPlan Default(HelpProposal proposal, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        var account = proposal.Value?.Trim() ?? "";
        var workspace = proposal.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return new HelpPlan($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.", "", "", null);
        }

        var owner = door.AccountsOf;
        var terminal = $"daoris agent profile default {owner} {account}" + (workspace is null ? "" : $" --workspace {workspace}");
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (account.Length == 0)
        {
            return Refused($"a default names the account `{owner}` runs as — one the room lists under it; the tool's own home "
                + "is chosen on the screen.");
        }

        if (!door.Accounts.Contains(account, StringComparer.Ordinal))
        {
            return Refused($"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                + (door.Accounts.Count > 0 ? string.Join(", ", door.Accounts) : "(none)"));
        }

        if (workspace is not null && !facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.");
        }

        return new HelpPlan(null,
            workspace is null
                ? $"This machine runs `{owner}` as `{account}` by default."
                : $"Sessions in `{workspace}` run `{owner}` as `{account}`.",
            terminal, null);
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var (proposal, plan, id) = (applying.Proposal, applying.Plan, applying.Id);
        var harness = proposal.Target!.Trim();
        if (proposal.Door == "default")
        {
            try
            {
                await applying.Doors.SetDefaultAccountAsync(
                    harness, proposal.Value!.Trim(), proposal.Workspace?.Trim() is { Length: > 0 } w ? w : null, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
            }

            return applying.Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null);
        }

        var doing = proposal.Door == "pin" ? $"pin of `{harness}` to {proposal.Value?.Trim()}" : $"update of `{harness}`";
        try
        {
            await applying.Doors.StartAgentActionAsync(
                harness, proposal.Door, proposal.Door == "pin" ? proposal.Value?.Trim() : null,
                (code, problem) => applying.Later(problem is null && code == 0
                    ? $"The {doing} (`#{id}`) finished."
                    : $"The {doing} (`#{id}`) did not finish: {problem ?? $"it ended with exit code {code}"}."),
                ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
        }

        return applying.Settled(true, $"Started: `#{id}` — {plan.Describe} (`{plan.Terminal}`) Its end is said here when it finishes.", null);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$")]
    private static partial System.Text.RegularExpressions.Regex ExactRelease();
}

public partial interface IHelpDoors
{
    /// <summary>
    /// <c>HARNESS_ACTION</c>'s own start of an update or a pin: answered once the process has started,
    /// its end told to <paramref name="ended"/> with its exit code and any problem.
    /// </summary>
    Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct);

    /// <summary>
    /// <c>HARNESS_ACTION</c>'s own <c>profile-default</c> (HELP10): the account a door's owner runs as, for the machine,
    /// or for <paramref name="workspace"/> alone, written as the screen writes it and the roster asked again.
    /// </summary>
    Task SetDefaultAccountAsync(string harness, string account, string? workspace, CancellationToken ct);
}
