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
/// <c>order</c>), <c>keep</c> (an account of the list, or JSON null for none), since SWITCH1d <c>switch</c> and <c>curve</c>
/// (each window's points, or JSON null for none), and the old <c>early</c> and <c>near</c>, still read, each a field of
/// the proposal's file beside its target and workspace — judged as <c>daoris agent profile use</c> judges it and applied as
/// the screen's <c>ACCOUNT_USE</c>. Since TOOL4l-b it is listed in <see cref="Doors"/>, with <c>order</c> (a scope's list written
/// whole, or a workspace sent back to the machine's accounts) and <c>ready</c> (<i>Try now</i>, an account's cool-off ended),
/// since the service's <c>agent_propose</c> writes all three (TOOL4l-a).</para>
///
/// <para>Since POOL1b (D197 point 6, the account-pool design §5.3) an agent's accounts are one set: <c>use</c> and <c>order</c> act on
/// it, each applied only where the service marked it so (<c>set: true</c>), and refuse a workspace in the terminal's words; a stored
/// proposal without the mark, whose <c>order</c> may have meant <i>only these accounts</i>, is refused at its Apply. <c>default</c> left
/// <see cref="Doors"/>, and a stored one is refused, naming <c>order</c>.</para>
/// </remarks>
internal sealed partial class HelpAgentProposals : IHelpProposalKind
{
    public string Kind => "agent";

    // HELP6: every door built since HELP1c, each a card the person applies the same way.
    public string Tool => "agent_propose";

    public IReadOnlyList<string> Doors { get; } = ["update", "pin", "use", "order", "ready"];

    /// <summary>
    /// The <c>use</c> door's four fields, read from any file that carries one: a field left out is no change. And, from an
    /// agent's own file, the <c>order</c> door's list or clear and the <c>ready</c> door's account (TOOL4l-b).
    /// </summary>
    public HelpProposal Read(HelpProposal proposal, JsonElement file)
    {
        if (file.ValueKind != JsonValueKind.Object) return proposal;
        proposal = ReadUse(proposal, file);
        if (proposal.Kind != "agent") return proposal;

        // POOL1b: the service's mark that the proposal was written for the one set (§5.3).
        if (proposal.Door is "use" or "order")
        {
            proposal = proposal with { ForSet = file.TryGetProperty("set", out var set) && set.ValueKind == JsonValueKind.True };
        }

        if (proposal.Door == "order")
        {
            IReadOnlyList<string> accounts = file.TryGetProperty("accounts", out var list) && list.ValueKind == JsonValueKind.Array
                ? [.. list.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
                : [];
            var clear = file.TryGetProperty("clear", out var flag) && flag.ValueKind == JsonValueKind.True;
            return proposal with { AccountOrder = new AccountOrder(accounts, clear) };
        }

        return proposal.Door == "ready" ? proposal with { ReadyAccount = Json.Text(file, "account") } : proposal;
    }

    private static HelpProposal ReadUse(HelpProposal proposal, JsonElement file)
    {
        var use = Json.Text(file, "use");
        var keeps = file.TryGetProperty("keep", out var keep);
        bool? early = file.TryGetProperty("early", out var flag)
            ? flag.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
            : null;
        int? near = file.TryGetProperty("near", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var whole)
            ? whole
            : null;
        // SWITCH1d: the switch and the person's curve, a curve of JSON null taking it out; one that breaks a rule is kept as the
        // twins' sentence, which the plan refuses with.
        var switched = Json.Text(file, "switch");
        var curves = file.TryGetProperty("curve", out var curve);
        var noCurve = curves && curve.ValueKind == JsonValueKind.Null;
        var problem = curves && !noCurve ? UseCurve.ProblemOf(curve) : null;
        if (use is null && !keeps && early is null && near is null && switched is null && !curves) return proposal;

        return proposal with
        {
            AccountUse = new UseChange(
                Use: use,
                Keep: keeps && keep.ValueKind == JsonValueKind.String ? keep.GetString() : null,
                NoKeep: keeps && keep.ValueKind == JsonValueKind.Null,
                Early: early,
                Near: near,
                Switch: switched,
                Curve: curves && !noCurve && problem is null ? UseCurve.Read(curve) : null,
                NoCurve: noCurve),
            AccountCurveRefused = problem?.Sentence,
        };
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        if (proposal.Door == "default") return Retired(proposal, facts);
        if (proposal.Door is "use" or "order" && !proposal.ForSet) return Unmarked(proposal);
        if (proposal.Door == "use") return Use(proposal, facts);
        if (proposal.Door == "order") return Order(proposal, facts);
        if (proposal.Door == "ready") return Ready(proposal, facts);

        var name = proposal.Target?.Trim() ?? "";
        var version = proposal.Value?.Trim() ?? "";
        var update = proposal.Door == "update";
        var terminal = update ? $"daoris agent update {name}" : $"daoris agent pin {name} {version}";
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (proposal.Door is not ("update" or "pin")) return Refused($"`{proposal.Door}` is not an agent's change — `update`, `pin`, `use`, `order` or `ready`.");
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return Refused($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.");
        }

        if (update)
        {
            if (!door.Present)
            {
                // UX6e2: an agent's ways in, Install among them, are on its page in the Agents place since UX6e.
                return Refused($"`{name}` is not installed on this machine, so there is nothing to update — install it under "
                    + $"Agents → the agent's page → Ways in → Install, or with `daoris agent install {name}`.");
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
    /// A stored <c>default</c> (HELP10, retired by POOL1b, D197 point 6, the account-pool design §5.3): refused at its Apply, naming
    /// <c>order</c>, which says where the set's starts begin. A door's accounts are its owner's (AGT7), so the sentence names the owner.
    /// </summary>
    private static HelpPlan Retired(HelpProposal proposal, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        var owner = facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal))?.AccountsOf ?? name;
        var account = proposal.Value?.Trim() is { Length: > 0 } named ? named : null;
        return new HelpPlan($"`default` is retired: {OneSetWords.Default(owner, account)}. Propose `order` with the account first.", "", "", null);
    }

    /// <summary>
    /// A stored <c>use</c> or <c>order</c> written before the <c>set</c> mark (POOL1b, §5.3): for the machine's or a workspace's list, and
    /// an <c>order</c> of it may have said which accounts alone may run, so it is refused, never applied to the set.
    /// </summary>
    private static HelpPlan Unmarked(HelpProposal proposal) => new($"this `{proposal.Door}` was {OneSetWords.Unmarked}.", "", "", null);

    /// <summary>A workspace named on <c>use</c> or <c>order</c>, refused in the terminal's words (POOL1b, §5.3).</summary>
    private static string? WorkspaceRefused(HelpProposal proposal) =>
        proposal.Workspace?.Trim() is { Length: > 0 } ? $"{OneSetWords.Workspace("workspace")}." : null;

    /// <summary>The set's order as the facts read it (the projection, POOL1a2): every account of the owner's; none where nothing was read.</summary>
    private static IReadOnlyList<string> SetOf(HarnessSettings? wiring, string owner) => wiring?.Set(owner).List ?? [];

    /// <summary>
    /// <c>use</c> (TOOL4g, D130 §16.6; POOL1b, §5.3): how the set is used, its one entry of settings, as <c>daoris agent profile use</c>
    /// takes it — an agent with an account of Daoris's, a way to use accounts and a switch this build knows, a curve that keeps its rules
    /// (SWITCH1d), a near from 50 to 99, and a kept account of the set that leaves driven work another — said in its words, the switch and
    /// the curve in the curve's terms. A door's accounts are its owner's (AGT7), so the command and the sentence name the owner.
    /// </summary>
    private static HelpPlan Use(HelpProposal proposal, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return new HelpPlan($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.", "", "", null);
        }

        var owner = door.AccountsOf;
        var change = proposal.AccountUse ?? new UseChange();
        // The person's curve as the terminal spells it, a `--curve <window>=<elapsed>:<used>,…` a window (SWITCH1d).
        var curve = change.Curve is { } set
            ? string.Concat(UseCurve.Windows.Where(window => set[window] is not null).Select(window => $" --curve {window}={Spelled(set[window]!, ",")}"))
            : "";
        var terminal = $"daoris agent profile use {owner}"
            + (change.Use is { Length: > 0 } mode ? $" {mode}" : "")
            + (change.Keep?.Trim() is { Length: > 0 } kept ? $" --keep {ShellWord.Of(kept, ShellWord.Account)}" : change.NoKeep ? " --no-keep" : "")
            + (change.Early is { } early ? $" --early {(early ? "on" : "off")}" : "")
            + (change.Near is { } near ? $" --near {near}" : "")
            + (change.Switch is { Length: > 0 } switched ? $" --switch {switched}" : "")
            + (change.NoCurve ? " --no-curve" : curve);
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (WorkspaceRefused(proposal) is { } scoped) return Refused(scoped);
        if (proposal.AccountCurveRefused is { } broken) return Refused(broken);

        if (change.Use is null && change.Keep is null && !change.NoKeep && change.Early is null && change.Near is null
            && change.Switch is null && change.Curve is null && !change.NoCurve)
        {
            return Refused("a use names how the accounts are used: `use` (goal or order), `keep` (one of the accounts, or none), "
                + "`switch` (smart, curve or off) or `curve` (your own points, a window each).");
        }

        var list = SetOf(facts.Wiring, owner);
        if (list.Count == 0)
        {
            return Refused($"`{owner}` has no account of Daoris's, so there is nothing to use — `daoris agent login {owner} --new` signs in to "
                + "one, and how its accounts are used is set beside them.");
        }

        if (change.Use is { } way && !RotationUse.Modes.Contains(way, StringComparer.Ordinal))
        {
            return Refused($"`{way}` is not a way to use accounts — goal (make the most of them) or order (one by one, in order).");
        }

        if (change.Switch is { } when && !RotationUse.Switches.Contains(when, StringComparer.Ordinal))
        {
            return Refused($"`{when}` is not when to switch accounts — smart (Daoris's curve), curve (your own) or off.");
        }

        if (change.Near is { } percent && percent is < RotationUse.NearLowest or > RotationUse.NearHighest)
        {
            return Refused($"near is a whole percent from {RotationUse.NearLowest} to {RotationUse.NearHighest}, not {percent}: it holds "
                + "your own curve's five-hour window at that share.");
        }

        if (change.Keep?.Trim() is { Length: > 0 } keep)
        {
            switch (ScopeProblem.Of(null, list, keep))
            {
                case { Kind: ScopeProblemKind.Keep }:
                    return Refused($"`{keep}` is not one of `{owner}`'s accounts ({string.Join(", then ", list)}) — the kept account is one "
                        + "of Daoris's accounts.");
                case { Kind: ScopeProblemKind.Alone }:
                    return Refused($"`{owner}` has no account of Daoris's but `{keep}`, so keeping it for conversations would leave driven "
                        + "work none.");
            }
        }

        var said = new List<string>();
        if (change.Use is { } chosen) said.Add(chosen == "order" ? "uses its accounts one by one, in order" : "makes the most of its accounts");
        if (change.Keep?.Trim() is { Length: > 0 } held) said.Add($"keeps `{held}` for conversations");
        else if (change.NoKeep) said.Add("keeps no account for conversations");
        // SWITCH1d: in the curve's terms, the old names too, as the file reads them on (D165 §6).
        if (change.Switch is { } chosenSwitch) said.Add(Switching(chosenSwitch));
        else if (change.Early is { } switches) said.Add(Switching(switches ? "smart" : "off"));
        if (change.Near is { } at) said.Add($"holds its own curve's five-hour window at {at}%");
        if (change.NoCurve) said.Add("clears its own curve, every window back to Daoris's");
        else if (change.Curve is { } points)
        {
            foreach (var window in UseCurve.Windows.Where(window => points[window] is not null))
            {
                said.Add($"sets its own curve's {WindowSaid(window)} window to {Spelled(points[window]!, ", ")}");
            }
        }

        var joined = said.Count == 1 ? said[0] : $"{string.Join(", ", said.Take(said.Count - 1))} and {said[^1]}";
        return new HelpPlan(null, $"For every workspace, `{owner}` {joined}.", terminal, null);

        static string Switching(string when) => when switch
        {
            "curve" => "switches accounts by its own curve",
            "off" => "does not switch accounts while a session runs",
            _ => "switches accounts by Daoris's curve",
        };

        static string WindowSaid(string window) => window == "session" ? "five-hour" : window;
    }

    /// <summary>A window's points as the terminal spells them, <c>elapsed:used</c>, joined by <paramref name="by"/>.</summary>
    private static string Spelled(IReadOnlyList<CurvePoint> points, string by) =>
        string.Join(by, points.Select(point => $"{point.Elapsed}:{point.Used}"));

    /// <summary>
    /// <c>order</c> (TOOL4l-b, D130 §9; POOL1b, §5.3): the set's order, as <c>daoris agent profile order</c> takes it — the accounts named
    /// first, in that order, each an account here and once, and the rest after in their order — or <c>clear</c>, the ids' order again,
    /// clearing no setting; said in its words. A door's accounts are its owner's (AGT7).
    /// </summary>
    private static HelpPlan Order(HelpProposal proposal, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return new HelpPlan($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.", "", "", null);
        }

        var owner = door.AccountsOf;
        var change = proposal.AccountOrder ?? new AccountOrder([], false);
        IReadOnlyList<string> list = [.. change.Accounts.Select(each => each.Trim()).Where(each => each.Length > 0)];
        var terminal = change.Clear
            ? $"daoris agent profile order {owner} --clear"
            : $"daoris agent profile order {owner} {ShellWord.Words(list, ShellWord.Account)}";
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (WorkspaceRefused(proposal) is { } scoped) return Refused(scoped);
        if (list.Count > 0 == change.Clear)
        {
            return Refused(change.Clear
                ? "`order` puts accounts first or returns to the ids' order, never both."
                : "`order` names the accounts to put first, in order, or clears it back to the ids' order.");
        }

        if (change.Clear)
        {
            string[] ids = [.. door.Accounts.Order(StringComparer.Ordinal)];
            return new HelpPlan(null,
                $"`{owner}`'s order goes back to its accounts' ids"
                + (ids.Length > 0 ? $": {string.Join(", then ", ids.Select(each => $"`{each}`"))}" : "")
                + ". No setting is cleared.", terminal, null);
        }

        if (HarnessSettings.OrderProblem(door.Accounts, list) is { } problem) return Refused(problem.Sentence(owner, door.Accounts));

        // The named first, then the rest in the set's order, then any account it does not hold yet, by id (AccountSet.Whole).
        IReadOnlyList<string> whole = [.. list, .. SetOf(facts.Wiring, owner).Concat(door.Accounts.Order(StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal).Where(each => door.Accounts.Contains(each, StringComparer.Ordinal) && !list.Contains(each, StringComparer.Ordinal))];
        return new HelpPlan(null,
            $"`{owner}`'s order is {string.Join(", then ", whole.Select(each => $"`{each}`"))}, for every workspace: one by one, in order, "
            + "begins at the first, and make the most of them uses the order only to break a tie.",
            terminal, null);
    }

    /// <summary>
    /// <c>ready</c> (TOOL4l-b, D125 §2.3): <i>Try now</i>, an account's cool-off ended early as <c>daoris agent profile ready</c>
    /// ends it, since the person may know the limit was raised. An account of the door's owner (AGT7) that is cooling, or
    /// <c>own</c>, the tool's own sign-in, when that is; one not cooling has nothing to end, as the terminal says.
    /// </summary>
    private static HelpPlan Ready(HelpProposal proposal, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return new HelpPlan($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.", "", "", null);
        }

        var owner = door.AccountsOf;
        var account = proposal.ReadyAccount?.Trim() ?? "";
        var own = account == OwnSignIn;
        var terminal = $"daoris agent profile ready {owner} " + (own ? "--own" : ShellWord.Of(account, ShellWord.Account));
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (account.Length == 0)
        {
            return Refused($"`ready` names the account whose cool-off ends, or `{OwnSignIn}` for the tool's own sign-in.");
        }

        if (!own && !door.Accounts.Contains(account, StringComparer.Ordinal))
        {
            return Refused($"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                + (door.Accounts.Count > 0 ? string.Join(", ", door.Accounts) : "(none)"));
        }

        var cooling = facts.Cooling.Where(each => string.Equals(each.Agent, owner, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!cooling.Any(each => own ? string.IsNullOrEmpty(each.Account) : string.Equals(each.Account, account, StringComparison.OrdinalIgnoreCase)))
        {
            var who = own ? $"`{owner}`'s own sign-in" : $"`{account}`";
            return Refused($"{who} is not cooling, so there is nothing to end — "
                + (cooling.Count > 0
                    ? $"cooling now: {string.Join(", ", cooling.Select(each => string.IsNullOrEmpty(each.Account) ? "its own sign-in" : $"`{each.Account}`"))}."
                    : $"no account of `{owner}` is cooling."));
        }

        // The tool's own sign-in is read-only (D175): only an old cool-off of it ends, and no start runs on it.
        return new HelpPlan(null,
            own
                ? $"End the old cool-off of `{owner}`'s own sign-in. Daoris starts no session in it; this only clears what was recorded."
                : $"Try `{account}` now: its cool-off ends, so starts may run on it again. If the limit "
                    + "still holds, the agent refuses the next start at once and the account cools again.",
            terminal, null);
    }

    /// <summary>What a <c>ready</c> names for the tool's own sign-in, as the service's writer and the terminal's <c>--own</c> do.</summary>
    private const string OwnSignIn = "own";

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var (proposal, plan, id) = (applying.Proposal, applying.Plan, applying.Id);
        var harness = proposal.Target!.Trim();
        // POOL1b (§5.3): a retired door or an unmarked list's proposal is never applied to the set, whatever judged it.
        if (proposal.Door == "default" || (proposal.Door is "use" or "order" && !proposal.ForSet))
        {
            var why = proposal.Door == "default" ? $"`default` is retired: {OneSetWords.Default(harness)}." : $"this `{proposal.Door}` was {OneSetWords.Unmarked}.";
            return applying.Settled(false, $"Not applied: `#{id}` — {why}", why);
        }

        if (proposal.Door == "ready")
        {
            var account = proposal.ReadyAccount?.Trim() is { } named && named != OwnSignIn ? named : null;
            bool ended;
            try
            {
                ended = await applying.Doors.ReadyAccountAsync(harness, account, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
            }

            // Cooling at the judge and not at the press: the file moved between, so nothing was changed (as the terminal says).
            if (!ended)
            {
                const string passed = "it is not cooling any more, so nothing changed.";
                return applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {passed}", passed);
            }

            return applying.Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null);
        }

        if (proposal.Door == "order")
        {
            var order = proposal.AccountOrder ?? new AccountOrder([], false);
            try
            {
                await applying.Doors.SetAccountOrderAsync(harness, order.Accounts, order.Clear, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
            }

            return applying.Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null);
        }

        if (proposal.Door == "use")
        {
            try
            {
                await applying.Doors.SetAccountUseAsync(harness, proposal.AccountUse ?? new UseChange(), ct).ConfigureAwait(false);
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

public interface IHelpAgentDoors
{
    /// <summary>
    /// <c>HARNESS_ACTION</c>'s own start of an update or a pin: answered once the process has started,
    /// its end told to <paramref name="ended"/> with its exit code and any problem.
    /// </summary>
    Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct);

    /// <summary>
    /// <c>ACCOUNT_USE</c>'s own <c>use</c> (TOOL4g; POOL1b): how a door's owner's set is used, refused as the screen refuses it and
    /// written as it writes it. A workspace's and <c>HARNESS_ACTION</c>'s <c>profile-default</c> left with D197.
    /// </summary>
    Task SetAccountUseAsync(string harness, UseChange change, CancellationToken ct);

    /// <summary>
    /// <c>ACCOUNT_USE</c>'s own <c>order</c> (TOOL4l-b; POOL1b): a door's owner's set ordered, the accounts named first and the rest after,
    /// or, with <paramref name="clear"/>, returned to the ids' order; refused as the screen refuses it and written as it writes it.
    /// </summary>
    Task SetAccountOrderAsync(string harness, IReadOnlyList<string> accounts, bool clear, CancellationToken ct);

    /// <summary>
    /// <c>ACCOUNT_USE</c>'s own <c>ready</c> (TOOL4l-b): <i>Try now</i>, an account's cool-off ended, or the tool's own sign-in's
    /// where <paramref name="account"/> is null; true when it was cooling, then the loop asked to look.
    /// </summary>
    Task<bool> ReadyAccountAsync(string harness, string? account, CancellationToken ct);
}

/// <summary>The <c>order</c> door's fields (TOOL4l-b): the list in walk order, or a workspace's list cleared.</summary>
public sealed record AccountOrder(IReadOnlyList<string> Accounts, bool Clear);


