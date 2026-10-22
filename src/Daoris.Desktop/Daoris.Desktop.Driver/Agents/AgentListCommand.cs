namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver agent [list]</c> (PUSH1f2e4, D194, the alone-path design §5): this machine's agents and the accounts they run as, the
/// words <c>daoris agent</c> and <c>daoris agent list</c> hand this program. Each agent is asked its version by the binary a start
/// would run (<see cref="HarnessProbe"/>, D57 rule 4), and each of its accounts whether it is signed in, under the account's probe lock;
/// what an account was read as is kept where the driver keeps its own readings (<c>reads.json</c>, AGENTREAD1). Beneath each agent its
/// pin, its accounts with each one's cool-off and last reading, each list with how it is used and its next start, and the plugins'
/// declared harnesses after the build's (D64). A read: it holds no lease and is said from a session's terminal. Its print is the CLI's
/// <c>list</c> and <c>accountLines</c>, ported whole (<c>fixtures/agents-text.json</c>).
/// </summary>
internal static class AgentListCommand
{
    private const string Prefix = "daoris";

    /// <summary>The name column's width before an agent's lines, as the CLI's <c>padEnd(14)</c>.</summary>
    private const int Name = 14;

    private static readonly string Under = $"  {"",-Name} ";

    /// <summary>The list, said: the wiring file read, each agent of the build, each plugin's harness, and what an account is.</summary>
    public static async Task<int> RunAsync(AgentsWorld world, CancellationToken ct)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var path = AgentsCommand.WiringPath(world);
        var home = HarnessSettings.HomeOf(path);
        // A wiring file that does not read is no wiring, as a start reads it (D21): the list says what is there. Read as the set (POOL1b,
        // the account-pool design §2.4), as a start reads it; the receipts of what the fold changed are the projection's too.
        var settings = HarnessSettings.LoadSet(path);
        var set = world.Adapters(home);
        var now = world.Now();
        Say($"{Prefix}: {path}");

        foreach (var name in AgentsCommand.Named(world, path).Where(each => set.DeclaredBy(each) is null))
        {
            var toolchain = ToolchainOf(set, name)!;
            var report = await ProbeAsync(name, toolchain, settings, home, now, ct).ConfigureAwait(false);
            Say("");
            // What it IS, before what state it is in: `dsh` meant nothing to the owner until it said whose.
            var state = report.Present ? report.Version ?? "null" : $"absent — {report.Problem}";
            if (toolchain.Product is { } product)
            {
                Say($"  {name,-Name} {product}{(toolchain.Maker is { } maker ? $", by {maker}" : "")}");
                Say($"{Under}{state}");
            }
            else
            {
                Say($"  {name,-Name} {state}");
            }

            if (!report.Present && toolchain.Install is not null) Say($"{Under}`daoris agent install {name}` installs it, with its own installer");

            // The pin, and whether it is in force (TOOL2/D57): a pin whose directory is not there is said missing, with what the driver does
            // about it, since running whatever is on PATH would be a different tool than the one asked for.
            if (settings.ResolveVersion(name, null, chosen: null) is { } pinned)
            {
                var binary = HarnessSettings.ManagedBinary(home, name, pinned, toolchain.Binary);
                Say($"{Under}pinned {pinned} — "
                    + (binary is not null ? $"managed: {binary}" : "NOT INSTALLED, so the driver refuses to start a session on it"));
                if (binary is null) Say($"{Under}`daoris agent pin {name} {pinned}` installs it");
            }

            foreach (var (circle, map) in settings.WorkspaceVersions)
            {
                if (map.TryGetValue(name, out var version) && version is { Length: > 0 }) Say($"{Under}pinned {version} for the `{circle}` workspace");
            }

            // One account list per tool (TOOL6g): a door onto another agent's accounts lists them nowhere of its own, since its owner
            // listed them with what the agent said of each.
            if (toolchain.AccountOf is { Length: > 0 } owner && ToolchainOf(set, owner) is { } owned)
            {
                var whose = owned.Product ?? owner;
                Say($"{Under}its accounts are {whose}'s, listed under `{owner}`: this is another way {whose} runs on them");
                continue;
            }

            foreach (var line in AccountLines(name, toolchain, report, settings, home, set, now, world.Zone)) Say(line);
        }

        // The harnesses this machine's plugins declare (D64), beside the build's with the plugin each came from. One with no version
        // question is asked whether it is THERE rather than run, as the roster asks it: an ACP agent started bare waits on its stdin.
        foreach (var name in set.Names.Where(each => set.DeclaredBy(each) is not null))
        {
            var toolchain = set.Resolve(name).Toolchain!;
            Say("");
            var command = toolchain.Binary.Count > 0 ? toolchain.Binary[0] : "";
            string state;
            if (toolchain.ProbeByPresence)
            {
                state = CommandPresence.Resolvable(command) ? "present (no version question declared)" : $"absent — `{command}` is not there";
            }
            else
            {
                var probed = await ProbeAsync(name, toolchain, settings, home, now, ct).ConfigureAwait(false);
                state = probed.Present ? probed.Version ?? "null" : $"absent — {probed.Problem}";
            }

            Say($"  {name,-Name} {state}  (declared by plugin `{set.DeclaredBy(name)}`)");
            var existing = HarnessSettings.Profiles(home, name);
            Say($"{Under}{(existing.Count > 0 ? $"accounts: {string.Join(", ", existing)}" : NoAccounts(name))}");
        }

        Say("");
        Say("  An account is a directory Daoris owns the location of. The credential inside it belongs to");
        Say("  the agent's own store. An API key is the one secret Daoris keeps: `agent key`, in keys.json.");
        return 0;
    }

    /// <summary>An agent's toolchain: its door's, or the agent a door runs as with no door here (<see cref="AdapterSet.Holder"/>).</summary>
    private static HarnessToolchain? ToolchainOf(AdapterSet set, string agent) =>
        set.Names.Contains(agent, StringComparer.OrdinalIgnoreCase) ? set.Resolve(agent).Toolchain : set.Holder(agent);

    /// <summary>
    /// One agent probed as the CLI's <c>probe</c> asked it: its version by the binary a start runs, each of its own accounts' sign-in (a
    /// door's are its owner's, and are asked under the owner), never the tool's own sign-in, which is the person's. Each reading is kept
    /// in <c>reads.json</c> at the answer's moment. A pin with nothing installed at it is absent, its accounts listed unknown.
    /// </summary>
    private static async Task<HarnessReport> ProbeAsync(
        string name, HarnessToolchain toolchain, HarnessSettings settings, string home, DateTimeOffset now, CancellationToken ct)
    {
        var owner = toolchain.Owner(name);
        var report = await HarnessProbe.ProbeAsync(
            name, toolchain, command: null, settings, home, ct,
            // A door asks no account: its list is its owner's, said under the owner.
            asks: owner == name ? null : _ => false,
            clock: () => now,
            answered: (account, said, at) =>
            {
                try
                {
                    AccountReads.Keep(home, owner, account, said.Login, at);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // A reading not kept costs the screen what this one learned, never the answer the terminal gives.
                }
            }).ConfigureAwait(false);
        if (report.Present || report.Profiles.Count > 0) return report;

        return report with
        {
            Profiles = [.. HarnessSettings.Profiles(home, owner).Select(account => new ProfileReport(
                account, HarnessSettings.ProfileHome(home, owner, account), LoginState.Unknown,
                Key: HarnessKeys.Of(home, owner, account) is { } key ? HarnessKeys.Handle(key) : null))],
        };
    }

    /// <summary>An agent with no Daoris account (OWNSPACE1d, D175): its starts wait for a sign-in, never run in the tool's own home.</summary>
    private static string NoAccounts(string agent) =>
        $"no accounts — Daoris's sessions wait until you sign in: `daoris agent login {agent} --new`";

    private static string Word(LoginState login) => login switch
    {
        LoginState.In => "in",
        LoginState.Out => "out",
        _ => "unknown",
    };

    private static bool Same(string? one, string? other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What the list says under one agent about its accounts (D49 §4, D66 §3, TOOL4e): each account and what marks it, each one's cool-off
    /// and last reading under it, the tool's own sign-in's cool-off and reading (CODEXUSE3), each list with how it is used and its next
    /// start (TOOL6f), and, where a start would otherwise reach the person's own sign-in, that Daoris starts none there (D175).
    /// </summary>
    private static List<string> AccountLines(
        string name, HarnessToolchain toolchain, HarnessReport report, HarnessSettings settings, string home, AdapterSet set,
        DateTimeOffset now, TimeZoneInfo zone)
    {
        var owner = toolchain.Owner(name);
        var lines = new List<string>();
        // Each account by the person's name for it, else its id (ACCT2); the name column fits the longest (ACCTNAME1, floor 16).
        string Shown(string account) => AccountNames.Shown(home, owner, account);
        var col = Math.Max(16, report.Profiles.Select(profile => Shown(profile.Name).Length).DefaultIfEmpty(0).Max());
        var gap = new string(' ', col);
        var cooling = AccountCooling.Read(home, now);
        CoolingEntry? CoolingOf(string? account) => cooling.FirstOrDefault(entry => Same(entry.Agent, owner) && Same(entry.Account ?? "", account ?? ""));

        // OWNSPACE1d (D175): Daoris starts nothing in the tool's own home, so with no account its starts wait for a sign-in.
        if (report.Profiles.Count == 0) lines.Add($"{Under}{NoAccounts(owner)}");

        // POOL1b (D197, the account-pool design §5.1): every account is one of the set, so none is marked a default or said to run nowhere.
        var scope = settings.Set(owner);
        foreach (var profile in report.Profiles)
        {
            List<string> marks = [.. Shown(profile.Name) != profile.Name ? [$"id {profile.Name}"] : Array.Empty<string>()];

            // 🔴 A key account is never "in": the tool says so for any key, a wrong one included (AGT3).
            lines.Add($"{Under}{Shown(profile.Name).PadRight(col)} {(profile.Key is not null ? "unchecked" : Word(profile.Login)),-9}"
                + WhoWords(profile, Shown(profile.Name), report.Profiles)
                + (profile.Key is { } handle ? $" API key {handle}" : "")
                + (marks.Count > 0 ? $" ({string.Join(", ", marks)})" : ""));

            if (CoolingOf(profile.Name) is { } cooled) lines.Add($"{Under}{gap} {AgentAccountsCommand.CoolingLine(cooled, now, zone)}");
            // What its agent last said about its windows, and how long ago (TOOL6c, D130 §3.2): a reading, never a judgement.
            if (AccountWindows.SaidOf(home, owner, profile.Name, now) is { } said)
            {
                lines.Add($"{Under}{gap} {AgentAccountsCommand.SaidLine(said, now, zone, use: null)}");
            }
        }

        // The set (POOL1b, the account-pool design §5.1): its order, how it is used beneath it (TOOL6a) and its next start (TOOL6f), one
        // for every workspace.
        var says = AgentAccountsCommand.Says(set, owner);
        var product = ToolchainOf(set, owner)?.Product ?? owner;
        if (scope.List.Count > 0)
        {
            lines.Add($"{Under}{"order".PadRight(col)} {string.Join(", then ", scope.List.Select(Shown))}, for every workspace");
            var (rows, notes) = AgentAccountsCommand.UseLines(scope, product, says, Shown);
            foreach (var (label, value) in rows) lines.Add($"{Under}{gap} {label}: {value}");
            foreach (var note in notes) lines.Add($"{Under}{gap} {note}");
            foreach (var line in NextStartBeneath(scope, owner, home, now, zone, says, Shown)) lines.Add($"{Under}{gap} {line}");
        }

        // Then the person's own sign-in, outside the set (D197 point 3, §3): no state, never asked. Its cool-off is Daoris's own record of a
        // conversation begun there, which `profile ready --own` still ends.
        if (toolchain.AccountOf is null && toolchain.LoginArguments is not null && report.Present)
        {
            // OWNSPACE1d (D175): Daoris never starts in it; what began there is read where it is and goes on there.
            lines.Add($"{Under}your own sign-in, the account `{name}` uses at your own terminal, is not one of Daoris's accounts:");
            lines.Add($"{Under}Daoris never asks it, signs it in or out, or starts a session on it; a conversation begun there goes on there —");
            lines.Add($"{Under}`daoris agent login {name} --new` signs in to one of Daoris's");
        }

        if (CoolingOf(null) is { } ownCooling) lines.Add($"{Under}its own sign-in: {AgentAccountsCommand.CoolingLine(ownCooling, now, zone)}");

        // What the fold changed while it is unsaid (D197 point 7, §6.3), until *Got it* on the agent's page marks it said.
        string[] unsaid = [.. (settings.Pooled.GetValueOrDefault(owner) ?? []).Where(receipt => !receipt.Said).SelectMany(receipt => PoolLines(receipt, Shown))];
        if (unsaid.Length > 0)
        {
            lines.Add($"{Under}your {product} accounts now serve every workspace:");
            foreach (var line in unsaid) lines.Add($"{Under}  {line}");
            lines.Add($"{Under}  Agents → the agent's page → Got it marks this said.");
        }

        return lines;
    }

    /// <summary>
    /// One receipt of the fold in the terminal's words (D197 point 7, the account-pool design §4.2's <c>agents.pooled.*</c> English): each
    /// scope that named fewer accounts than the set, each first moved, an account placed, the way of use set, each setting not kept and
    /// each name gone.
    /// </summary>
    internal static IEnumerable<string> PoolLines(PoolReceipt receipt, Func<string, string> shown)
    {
        var count = receipt.Order.Count;
        string Named(FoldedScope scope) => scope.Workspace is { } workspace ? $"`{workspace}`" : "";
        foreach (var scope in receipt.Scopes)
        {
            IReadOnlyList<string> had = scope.List.Count > 0 ? scope.List : scope.Default is { } only ? [only] : [];
            if (had.Count == 0 || had.Count >= count) continue;
            var accounts = string.Join(", ", had.Select(shown));
            yield return (scope.Workspace is not null, had.Count == 1) switch
            {
                (true, true) => $"{Named(scope)} ran on {accounts} alone; it now runs on all {count}.",
                (true, false) => $"{Named(scope)} had its own accounts ({accounts}); it now runs on all {count}.",
                (false, true) => $"Workspaces that named no accounts ran on {accounts} alone; they now run on all {count}.",
                _ => $"Workspaces that named no accounts ran on {accounts}; they now run on all {count}.",
            };
        }

        if (receipt.InOrder is { } begins)
        {
            yield return $"Their starts still begin at {shown(begins)}, since the accounts are now used one by one, in order.";
        }

        foreach (var moved in receipt.Moved)
        {
            yield return moved.Workspace is { } workspace
                ? $"`{workspace}`'s starts began at {shown(moved.Account)}; it is number {moved.Place} in the order now."
                : $"Starts of workspaces that named no accounts began at {shown(moved.Account)}; it is number {moved.Place} in the order now.";
        }

        foreach (var joined in receipt.Joined)
        {
            yield return $"{shown(joined.Account)} was not in the order; it is number {joined.Place} now, and every workspace's work may run on it.";
        }

        foreach (var setting in receipt.NotKept)
        {
            yield return setting.Workspace is { } workspace
                ? $"Not kept: `{workspace}`'s {setting.Setting} ({setting.Value.GetRawText()})."
                : $"Not kept: {setting.Setting} ({setting.Value.GetRawText()}), set for workspaces that named no accounts.";
        }

        foreach (var gone in receipt.Gone) yield return $"{gone} was named but is no account here, so it was left out.";
    }

    /// <summary>
    /// What the list says beneath a scope about its next start (TOOL6f; D130 §16.4): the walk's steps as <c>profile use</c> names them,
    /// each account of the scope cooling now and until when, the wait where every account a start may take is cooling (driven work's
    /// alone where the kept account is ready), and where the account it takes is named. A scope with no list is its default alone.
    /// </summary>
    /// <remarks>
    /// 🔴 It never names the account a step would choose: fewest running and least recently started read Daoris's session records, which
    /// this verb does not read, so the pointer names the screen that asks the driver's own judgement (<see cref="AccountRotation"/>).
    /// </remarks>
    private static List<string> NextStartBeneath(
        RotationScope scope, string owner, string home, DateTimeOffset now, TimeZoneInfo zone, bool says, Func<string, string> shown)
    {
        IReadOnlyList<string> uses = scope.List.Count > 0 ? scope.List : scope.Default is { } only ? [only] : [];
        var lines = new List<string>();
        if (scope.List.Count > 0)
        {
            var anySaid = scope.List.Any(account => AccountWindows.SaidOf(home, owner, account, now) is not null);
            var (row, note) = AgentAccountsCommand.NextStartLines(scope, says, anySaid, shown);
            lines.Add($"next start: {row}");
            if (note is not null) lines.Add(note);
        }

        // Each account as the list spells it, with its cool-off's end where `cooling.json` holds one (names compare without case).
        var cooling = AccountCooling.Read(home, now);
        List<(string Account, DateTimeOffset Until)> held = [.. uses.SelectMany(account =>
            cooling.FirstOrDefault(entry => Same(entry.Agent, owner) && Same(entry.Account ?? "", account)) is { } entry
                ? [(account, entry.Until)]
                : Array.Empty<(string, DateTimeOffset)>())];
        string Soonest(IEnumerable<(string Account, DateTimeOffset Until)> of) => CoolingWords.When(of.Min(each => each.Until), zone);
        if (held.Count > 0)
        {
            var each = string.Join("; ", held.Select(entry => $"`{shown(entry.Account)}` is cooling until {CoolingWords.When(entry.Until, zone)}"));
            // Driven work passes a kept account that leaves it another (§4.6), so it waits once every other account cools.
            var keep = scope.Use.Keep is { } kept && ScopeProblem.Of(null, scope.List, kept) is null ? kept : null;
            var driven = held.Where(entry => entry.Account != keep).ToList();
            if (uses.Count == 1)
            {
                lines.Add($"held now: {each}, so the next start waits until then");
            }
            else
            {
                lines.Add($"held now: {each}");
                if (held.Count == uses.Count)
                {
                    lines.Add($"every account of Daoris's is cooling, so the next start waits until {Soonest(held)}");
                }
                else if (keep is not null && driven.Count == uses.Count - 1)
                {
                    lines.Add($"every account but `{shown(keep)}`, kept for conversations, is cooling, so driven work waits until {Soonest(driven)}");
                }
            }
        }

        // UX6e2: the screen that names it is the agent's page in the Agents place (D150 §5.2).
        if (scope.List.Count > 1)
        {
            lines.Add("Agents → the agent's page → How accounts are used names the account it takes, from the sessions Daoris "
                + "runs and its last starts, which this terminal does not read");
        }

        return lines;
    }

    /// <summary>
    /// Who signed in, as the list says it after an account's name and state (D66 §3): dropped where it is the name already (ACCTNAME1);
    /// and where another account signed in with the same email (ACCOUNTID1), what tells this one apart after it, each said once, so a
    /// name that is the email with it says neither again.
    /// </summary>
    private static string WhoWords(ProfileReport profile, string name, IReadOnlyList<ProfileReport> profiles)
    {
        var apart = ApartBy(profile, profiles);
        var said = apart is not null && profile.Account is { Length: > 0 } email ? AccountNames.Beside(email.Trim(), apart) : null;
        var named = said is not null && Same(said, name);
        string[] words =
        [
            .. profile.Account is { Length: > 0 } who && !Same(who, name) && !named ? [who] : Array.Empty<string>(),
            .. apart is not null && !named ? [apart] : Array.Empty<string>(),
        ];
        return words.Length > 0 ? $" {string.Join(" · ", words)}" : "";
    }

    /// <summary>
    /// What tells an account apart from another signed in with its email (ACCOUNTID1): its organisation, where no such other account has
    /// it, else its plan likewise; null where no other account has its email, or where neither tells them apart.
    /// </summary>
    private static string? ApartBy(ProfileReport profile, IReadOnlyList<ProfileReport> profiles)
    {
        if (profile.Account is not { Length: > 0 } email) return null;
        var twins = profiles.Where(other => !ReferenceEquals(other, profile) && other.Account is { Length: > 0 } theirs && Same(theirs, email)).ToList();
        if (twins.Count == 0) return null;
        foreach (var part in new Func<ProfileReport, string?>[] { each => each.Org, each => each.Plan })
        {
            if (part(profile) is { Length: > 0 } mine && twins.All(other => part(other) is not { Length: > 0 } theirs || !Same(theirs, mine))) return mine;
        }

        return null;
    }
}
