using System.Text.Json;
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
/// an account that exists, and since TOOL4g one its scope's own list holds (D130 §3.1) — and applied as
/// <c>HARNESS_ACTION</c>'s own <c>profile-default</c>.</para>
///
/// <para>Since TOOL4g the <c>use</c> door's judge (D130 §9, §16.6): how a scope's list is used — <c>use</c> (<c>goal</c> or
/// <c>order</c>), <c>keep</c> (an account of the list, or JSON null for none), <c>early</c> and <c>near</c>, each a field of
/// the proposal's file beside its target and workspace — judged as <c>daoris agent profile use</c> judges it and applied as
/// the screen's <c>ACCOUNT_USE</c>. 🔴 It is not in <see cref="Doors"/> yet: a door listed there is one the service's
/// <c>agent_propose</c> writes (<c>HelpProposalKindsTests</c>), and the service's half is another lane's row, so the room
/// offers it once the service writes the four fields above.</para>
/// </remarks>
internal sealed partial class HelpAgentProposals : IHelpProposalKind
{
    public string Kind => "agent";

    // HELP6: every door built since HELP1c, each a card the person applies the same way.
    public string Tool => "agent_propose";

    public IReadOnlyList<string> Doors { get; } = ["update", "pin", "default"];

    /// <summary>The <c>use</c> door's four fields, read from any file that carries one: a field left out is no change.</summary>
    public HelpProposal Read(HelpProposal proposal, JsonElement file)
    {
        if (file.ValueKind != JsonValueKind.Object) return proposal;
        var use = HelpProposals.Text(file, "use");
        var keeps = file.TryGetProperty("keep", out var keep);
        bool? early = file.TryGetProperty("early", out var flag)
            ? flag.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
            : null;
        int? near = file.TryGetProperty("near", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var whole)
            ? whole
            : null;
        if (use is null && !keeps && early is null && near is null) return proposal;

        return proposal with
        {
            AccountUse = new UseChange(
                Use: use,
                Keep: keeps && keep.ValueKind == JsonValueKind.String ? keep.GetString() : null,
                NoKeep: keeps && keep.ValueKind == JsonValueKind.Null,
                Early: early,
                Near: near),
        };
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        if (proposal.Door == "default") return Default(proposal, facts);
        if (proposal.Door == "use") return Use(proposal, facts);

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
                    + $"Settings → Agents, or with `daoris agent install {name}`.");
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

        // D130 §3.1 (TOOL4g, as the terminal and the screen refuse it): a default is where its scope's starts begin within
        // its own list, so one the list does not hold is refused. A scope with no list of its own takes any account.
        var list = OwnList(facts.Wiring, owner, workspace);
        if (ScopeProblem.Of(account, list, null) is { Kind: ScopeProblemKind.Default })
        {
            return Refused($"`{owner}`'s list {Where(workspace)} is {string.Join(", then ", list)}, and `{account}` is not in it — the list "
                + "is every account its starts may run on, and the default is where they begin within it. "
                + $"`daoris agent profile order {owner} {string.Join(' ', list.Append(account))}{Scoped(workspace)}` adds it, or make "
                + "one of the list the default.");
        }

        return new HelpPlan(null,
            workspace is null
                ? $"This machine runs `{owner}` as `{account}` by default."
                : $"Sessions in `{workspace}` run `{owner}` as `{account}`.",
            terminal, null);
    }

    /// <summary>
    /// <c>use</c> (TOOL4g, D130 §16.6): how a scope's list is used, for the machine or one workspace, as
    /// <c>daoris agent profile use</c> takes it — a scope with a list of its own, a way to use accounts this build knows, a
    /// near from 50 to 99, and a kept account of the list that leaves driven work another — said in its words. A door's
    /// accounts are its owner's (AGT7), so the command and the sentence name the owner.
    /// </summary>
    private static HelpPlan Use(HelpProposal proposal, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        var workspace = proposal.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return new HelpPlan($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.", "", "", null);
        }

        var owner = door.AccountsOf;
        var change = proposal.AccountUse ?? new UseChange();
        var terminal = $"daoris agent profile use {owner}"
            + (change.Use is { Length: > 0 } mode ? $" {mode}" : "")
            + (change.Keep?.Trim() is { Length: > 0 } kept ? $" --keep {kept}" : change.NoKeep ? " --no-keep" : "")
            + (change.Early is { } early ? $" --early {(early ? "on" : "off")}" : "")
            + (change.Near is { } near ? $" --near {near}" : "")
            + Scoped(workspace);
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (workspace is not null && !facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.");
        }

        if (change.Use is null && change.Keep is null && !change.NoKeep && change.Early is null && change.Near is null)
        {
            return Refused("a use names how the list is used: `use` (goal or order), `keep` (an account of the list, or none), "
                + "`early` (switch before the limit, or not) or `near` (a whole percent from 50 to 99).");
        }

        var list = OwnList(facts.Wiring, owner, workspace);
        if (list.Count == 0)
        {
            return Refused($"{(workspace is null ? "this machine" : $"`{workspace}`")} has no list of its own for `{owner}`, so there is "
                + $"nothing to use — `daoris agent profile order {owner} <account>…{Scoped(workspace)}` gives it one, and how it is "
                + "used is set beside it.");
        }

        if (change.Use is { } way && !RotationUse.Modes.Contains(way, StringComparer.Ordinal))
        {
            return Refused($"`{way}` is not a way to use accounts — goal (make the most of them) or order (one by one, in order).");
        }

        if (change.Near is { } percent && percent is < RotationUse.NearLowest or > RotationUse.NearHighest)
        {
            return Refused($"near is a whole percent from {RotationUse.NearLowest} to {RotationUse.NearHighest}, not {percent}: where an "
                + "agent gives only how much of a window is used, an account at or over it is near its limit.");
        }

        if (change.Keep?.Trim() is { Length: > 0 } keep)
        {
            switch (ScopeProblem.Of(null, list, keep))
            {
                case { Kind: ScopeProblemKind.Keep }:
                    return Refused($"`{keep}` is not in `{owner}`'s list {Where(workspace)} ({string.Join(", then ", list)}) — the kept "
                        + "account is one of the list.");
                case { Kind: ScopeProblemKind.Alone }:
                    return Refused($"`{owner}`'s list {Where(workspace)} holds no account but `{keep}`, so keeping it for conversations "
                        + "would leave driven work none.");
            }
        }

        var said = new List<string>();
        if (change.Use is { } chosen) said.Add(chosen == "order" ? "uses its accounts one by one, in order" : "makes the most of its accounts");
        if (change.Keep?.Trim() is { Length: > 0 } held) said.Add($"keeps `{held}` for conversations");
        else if (change.NoKeep) said.Add("keeps no account for conversations");
        if (change.Early is { } switches) said.Add(switches ? "switches before the limit" : "does not switch before the limit");
        if (change.Near is { } at) said.Add($"counts an account near its limit at {at}%");
        var joined = said.Count == 1 ? said[0] : $"{string.Join(", ", said.Take(said.Count - 1))} and {said[^1]}";
        return new HelpPlan(null, $"{(workspace is null ? "On this machine" : $"In `{workspace}`")}, `{owner}` {joined}.", terminal, null);
    }

    /// <summary>A scope's own list, as the wiring holds it: the machine's, or the workspace's own; none where nothing was read.</summary>
    private static IReadOnlyList<string> OwnList(HarnessSettings? wiring, string owner, string? workspace) =>
        wiring is null ? []
        : workspace is null ? wiring.Rotation.GetValueOrDefault(owner) ?? []
        : wiring.WorkspaceRotation.TryGetValue(workspace, out var lists) ? lists.GetValueOrDefault(owner) ?? [] : [];

    private static string Where(string? workspace) => workspace is null ? "on this machine" : $"in `{workspace}`";

    private static string Scoped(string? workspace) => workspace is null ? "" : $" --workspace {workspace}";

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var (proposal, plan, id) = (applying.Proposal, applying.Plan, applying.Id);
        var harness = proposal.Target!.Trim();
        if (proposal.Door == "use")
        {
            try
            {
                await applying.Doors.SetAccountUseAsync(
                    harness, proposal.AccountUse ?? new UseChange(), proposal.Workspace?.Trim() is { Length: > 0 } scope ? scope : null, ct)
                    .ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
            }

            return applying.Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null);
        }

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

    /// <summary>
    /// <c>ACCOUNT_USE</c>'s own <c>use</c> (TOOL4g): how a door's owner's list is used, for the machine or for
    /// <paramref name="workspace"/> alone, refused as the screen refuses it and written as it writes it.
    /// </summary>
    Task SetAccountUseAsync(string harness, UseChange change, string? workspace, CancellationToken ct);
}

/// <summary>The machine's wiring a proposal is judged against (TOOL4g): <c>harnesses.json</c> as the screen reads it.</summary>
public sealed partial record HelpMachineFacts
{
    /// <summary>The lists, defaults and settings, for a default's scope and a use's; null where nothing was read.</summary>
    public HarnessSettings? Wiring { get; init; }
}

/// <summary>The <c>use</c> door's fields (TOOL4g), as <see cref="HelpAgentProposals.Read"/> reads them from a file.</summary>
public sealed partial record HelpProposal
{
    /// <summary>How a scope's list is used, as the proposal names it; null where it names nothing of it.</summary>
    public UseChange? AccountUse { get; init; }
}
