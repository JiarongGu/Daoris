namespace Daoris.Driver;

/// <summary>
/// What the next start would run as and how the roster is wired for it: the last reading, the facts the walk judges and the start's wiring.
/// </summary>
internal sealed class RosterNext(HarnessRoster roster)
{
    /// <summary>
    /// What is known of one agent, asking it nothing (COMPANYVIEW1c, D150 point 5): its binary as its last answer this process said, none
    /// where none was asked, or a pin's install; and each account a start of <paramref name="kind"/> walks, in the walk's order, as the
    /// walk finds it before any question (a cool-off, a refusal its provider gave) beside its last sign-in reading. No account, binary or
    /// server is asked, and no start is counted. Null for an adapter that declares no toolchain.
    /// </summary>
    /// <exception cref="DriverException">No agent of that name on this machine.</exception>
    public AgentLastRead? LastRead(string adapter, DriverConfig config, string? workspace = null, StartKind kind = StartKind.Driven)
    {
        var resolved = roster.Adapters.Resolve(adapter);
        if (resolved.Toolchain is not { } toolchain) return null;
        var owner = roster.Adapters.Registry.OwnerOfAgent(resolved.Name).Value;
        var settings = roster.Settings;
        var named = settings.Set(owner);
        // As the walk reads its scope: where the person named no account, the agent's Daoris accounts on this machine (OWNSPACE1a).
        var ours = named.List.Count == 0 && named.Begins is null && !toolchain.StartsInOwnHome;
        var scope = ours ? roster.Walked(named, owner, toolchain) : named;
        var now = roster.Clock();
        var (order, facts) = roster.OrderOf(owner, scope, kind, picked: null, ownHome: false, now);
        IReadOnlyList<string> present;
        try
        {
            present = HarnessSettings.Profiles(roster.Home, owner);
        }
        catch (DriverException)
        {
            present = [];
        }

        var reads = AccountReads.Of(roster.Home, owner);
        AccountRead? ReadOf(string? account) =>
            account is null ? reads.Own
            : present.FirstOrDefault(name => string.Equals(name, account, StringComparison.OrdinalIgnoreCase)) is { } listed ? reads.Accounts.GetValueOrDefault(listed)
            : null;
        IReadOnlyList<AccountLastRead> accounts = ours && order is [null]
            ? []
            : [.. order.Select(account => new AccountLastRead(account, Before(owner, account, now, fact: FactOf(facts, account)), ReadOf(account)))];

        var known = roster._binaries.TryGetValue(resolved.Name, out var binary) ? binary : null;
        // The binary a start would run (TOOL2/D57): an explicit command, then a pin's managed install, which is its presence.
        if (config.Commands.GetValueOrDefault(resolved.Name) is not { Count: > 0 } && settings.ResolveVersion(resolved.Name, workspace, chosen: null) is { } pinned)
        {
            var managed = HarnessSettings.ManagedBinary(roster.Home, resolved.Name, pinned, toolchain.Binary) is not null;
            return new AgentLastRead(resolved.Name, owner, managed, managed ? (known is { Present: true } ? known.Version : pinned) : null, accounts) { Pinned = pinned };
        }

        return new AgentLastRead(resolved.Name, owner, known?.Present, known is { Present: true } ? known.Version : null, accounts);
    }

    /// <summary>
    /// Which account the next start on <paramref name="agent"/>'s accounts would take, in any workspace since every one reads the
    /// set (POOL1a2, D197 point 1), the step that chose it, and what holds each other account (TOOL6e, D130 §3–§4): what the agent's
    /// page in Agents says.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The walk's own pieces, never a second judgement</b>, as <see cref="WiringAsync"/> holds: the one scope
    /// <see cref="SelectAsync"/> reads, what it knows of each account, its order, and each account as it finds it before any
    /// probe (cooling, refused), with the agent's word on who is signed in as its last reading left it (ROSTER1). Nothing is
    /// probed, spawned or counted as a start chosen, so the page may ask at every tick; a sign-in the last reading did not see
    /// is said by a press, or by the start that asks again.
    /// </remarks>
    /// <param name="agent">Whose accounts: the accounts' owner (AGT7), a door's being its owner's.</param>
    /// <param name="kind">Driven work, which drops the kept account, or a conversation (§4.6).</param>
    public NextStart Next(string agent, StartKind kind = StartKind.Driven)
    {
        // The set a start walks (POOL1a2): its whole order; with no account, a wait.
        var scope = roster.Settings.Set(agent);
        var toolchain = OwnersToolchain(agent);
        if (toolchain is not null) scope = roster.Walked(scope, agent, toolchain);
        // The card speaks for the account's own door; each other door whose answer differs is said apart (s-ix o1 to o3).
        var doors = toolchain is null ? [] : DoorsDiffering(agent, toolchain.StartsInOwnHome);
        if (toolchain is { StartsInOwnHome: false } && scope.List.Count == 0 && scope.Begins is null) return new NextStart(null, NextReason.NoAccount, []) { Doors = doors };

        var now = roster.Clock();
        IReadOnlyList<string> present;
        try
        {
            present = HarnessSettings.Profiles(roster.Home, agent);
        }
        catch (DriverException)
        {
            present = [];
        }

        var facts = scope.List.Count == 0 ? HarnessRoster.NoFacts : Facts(agent, scope.List, now);
        IReadOnlyList<string?> order = scope.List.Count == 0 ? [scope.Begins] : [.. AccountRotation.Order(scope, kind, present, facts, now)];
        var reads = AccountReads.Of(roster.Home, agent);
        var tried = order.Select(account => Before(agent, account, now, fact: FactOf(facts, account)) switch
        {
            { IsReady: true, Account: { } named } ready when SaidOut(reads, named) => ready with { Readiness = AccountReadiness.SignedOut },
            var state => state,
        }).ToList();
        return AccountRotation.Next(scope, kind, tried, present, facts, now) with { Doors = doors };
    }

    /// <summary>The doors onto <paramref name="owner"/>'s accounts whose start-in-own-home answer is not <paramref name="cardState"/>, in ordinal order.</summary>
    private IReadOnlyList<DoorStart> DoorsDiffering(string owner, bool cardState)
    {
        var differing = new List<DoorStart>();
        foreach (var name in roster.Adapters.Names.Order(StringComparer.Ordinal))
        {
            HarnessToolchain? toolchain;
            try
            {
                toolchain = roster.Adapters.Resolve(name).Toolchain;
            }
            catch (DriverException)
            {
                continue;
            }

            if (toolchain is null || !string.Equals(roster.Adapters.Registry.OwnerOfAgent(name).Value, owner, StringComparison.OrdinalIgnoreCase)) continue;
            if (toolchain.StartsInOwnHome != cardState) differing.Add(new DoorStart(name, toolchain.StartsInOwnHome));
        }

        return differing;
    }

    /// <summary>
    /// The toolchain of the accounts' owner (AGT7), which is no adapter's name where only its doors are (`codex` behind
    /// `codex-acp`): the adapter of that name, else the first door whose owner it is, else the holder this build declares for it
    /// (CODEXACCT1); null where none declares one. What <see cref="Next"/> reads whether a start may run in its own home by
    /// (OWNSPACE1a), since the page asks it by the owner.
    /// </summary>
    private HarnessToolchain? OwnersToolchain(string owner)
    {
        HarnessToolchain? door = null;
        foreach (var name in roster.Adapters.Names)
        {
            HarnessToolchain? toolchain;
            try
            {
                toolchain = roster.Adapters.Resolve(name).Toolchain;
            }
            catch (DriverException)
            {
                continue;
            }

            if (toolchain is null || !string.Equals(roster.Adapters.Registry.OwnerOfAgent(name).Value, owner, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(name, owner, StringComparison.OrdinalIgnoreCase)) return toolchain;
            door ??= toolchain;
        }

        return door ?? roster.Adapters.Registry.ToolchainOf(owner);
    }

    /// <summary>
    /// The agents Daoris has set up and signed in on this machine (HELPERDEFAULT1, D163's 2026-10-24 note), in the set's order: each
    /// adapter whose accounts' owner (AGT7) has an account of Daoris's on disk that its last reading said is signed in (ROSTER1).
    /// Read from the accounts and their readings, never probed, so a look may ask it; the tool's own sign-in is the person's (D175),
    /// and counts for nothing here.
    /// </summary>
    public IReadOnlyList<string> SetUpAgents()
    {
        var setUp = new List<string>();
        foreach (var name in roster.Adapters.Names)
        {
            HarnessToolchain? toolchain;
            try
            {
                toolchain = roster.Adapters.Resolve(name).Toolchain;
            }
            catch (DriverException)
            {
                continue;
            }

            if (toolchain is null) continue;
            var owner = roster.Adapters.Registry.OwnerOfAgent(name).Value;
            IReadOnlyList<string> present;
            try
            {
                present = HarnessSettings.Profiles(roster.Home, owner);
            }
            catch (DriverException)
            {
                continue;
            }

            var reads = AccountReads.Of(roster.Home, owner);
            if (present.Any(account => reads.Accounts.GetValueOrDefault(account)?.Login == LoginState.In)) setUp.Add(name);
        }

        return setUp;
    }

    internal static AccountFacts? FactOf(IReadOnlyDictionary<string, AccountFacts> facts, string? account) =>
        account is not null && facts.TryGetValue(account, out var fact) ? fact : null;

    /// <summary>Whether the last reading of an owner's account said it is signed out (ROSTER1): kept under the home, never asked.</summary>
    internal static bool SaidOut(AgentReads reads, string account) =>
        reads.Accounts.GetValueOrDefault(account)?.Login == LoginState.Out;

    /// <summary>
    /// What a start on this account would meet before any probe (TOOL4f, D125 §3.3): its cool-off, a file read, then a
    /// refusal its provider gave (AGT3b), held in memory; otherwise ready, until the agent says it is signed out.
    /// </summary>
    /// <param name="names">The owner's names by id, which a cooling account's hold names it by (ACCT2b); null says the id.</param>
    /// <param name="fact">What the walk knows of the account: a reading at its limit cools it until that window's reset (LIMITPICK1).</param>
    internal AccountState Before(
        string owner, string? account, DateTimeOffset now, IReadOnlyDictionary<string, string>? names = null, AccountFacts? fact = null)
    {
        if ((AccountCooling.Of(roster.Home, owner, account, now) ?? AccountRotation.Spent(owner, account, fact, now)) is { } cooling)
        {
            var hold = CoolingWords.HoldOf(cooling, roster.Zone, names);
            return new AccountState(account, AccountReadiness.Cooling, cooling, hold.Note) { RefusalNote = hold };
        }

        return roster._refused.TryGetValue(HarnessRoster.AccountKey(owner, account), out var refused)
            ? new AccountState(account, AccountReadiness.Refused, Refusal: refused.Reason) { RefusalNote = refused.Note }
            : new AccountState(account, AccountReadiness.Ready);
    }

    /// <summary>
    /// What the walk knows of each account of a list (§16.4), and what its agent last said about its windows where a door
    /// carried it (TOOL6c, §5.2).
    /// </summary>
    internal Dictionary<string, AccountFacts> Facts(string owner, IReadOnlyList<string> list, DateTimeOffset now, long except = 0)
    {
        var facts = new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);
        lock (roster._load)
        {
            foreach (var account in list)
            {
                // A record ended `choice-not-offered` ran nothing (D205 §3.5(c)): neither running nor the account's last start.
                var sessions = roster._looked.Where(each => Same(each.Owner, owner) && Same(each.Session.Profile, account) && !each.Session.NotOffered)
                    .Select(each => each.Session).ToList();
                // A re-ask's own count left out (D205 §4.1): its account is ranked as if the start had not chosen it.
                var chosen = roster._chosen.Where(start => start.Number != except && Same(start.Owner, owner) && Same(start.Account, account)).ToList();
                facts[account] = new AccountFacts(
                    Running: sessions.Count(session => session.Running) + chosen.Count,
                    LastStarted: sessions.Select(session => session.Created).Where(created => created is not null).Max(),
                    Chosen: chosen.Count == 0 ? null : chosen.Max(start => start.Number));
            }
        }

        foreach (var account in list)
        {
            try
            {
                // A weekly reset a limit told is not read here (USAGERESET1): it says when, not how much would lapse, which
                // only the week's reading says.
                facts[account] = facts[account] with { Said = AccountWindows.SaidOf(roster.Home, owner, account, now) };
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A reading not read is nothing said, as one never told is.
            }
        }

        return facts;

        static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What a report says of one account: its last reading and when; null where the report lists none of that name.</summary>
    private static ProfileReport? ProfileOf(HarnessReport? report, string account) =>
        report?.Profiles.FirstOrDefault(p => string.Equals(p.Name, account, StringComparison.OrdinalIgnoreCase));

    /// <summary>What the agent last said about one account's sign-in, from a report; unknown where it says nothing.</summary>
    private static LoginState LoginOf(HarnessReport? report, string account) => ProfileOf(report, account)?.Login ?? LoginState.Unknown;

    /// <summary>
    /// The agent's other accounts a person could pick instead of a cooling one (D125 §3.3): not cooling, not refused, and
    /// not signed out, as the last reading of each says (ROSTER1). Nothing is asked of an account.
    /// </summary>
    internal async Task<IReadOnlyList<string>> ReadyAsync(
        string adapter, HarnessToolchain toolchain, DriverConfig config, string? picked, DateTimeOffset now, CancellationToken ct)
    {
        var owner = roster.Adapters.Registry.OwnerOfAgent(adapter).Value;
        var others = HarnessSettings.Profiles(roster.Home, owner)
            .Where(name => !string.Equals(name, picked, StringComparison.OrdinalIgnoreCase))
            .Where(name => Before(owner, name, now).IsReady)
            .ToList();
        if (others.Count == 0) return [];

        var report = await roster.ReportAsync(roster.ReportingDoor(adapter, toolchain), config, refresh: false, ct).ConfigureAwait(false);
        return report is { Present: false } ? [] : [.. others.Where(name => LoginOf(report, name) != LoginState.Out)];
    }

    /// <summary>
    /// What a start in this workspace would run on, and where each part came from (MAP1b, D67 §3).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Read through <see cref="SelectAsync"/>, never beside it.</b> The design's rule is that the
    /// picture cannot disagree with the loop, and a second resolution is exactly how it would: the
    /// account is the selection's — its scope is the agent's one set (<see cref="HarnessSettings.Set"/>, POOL1a2), which
    /// <see cref="SelectAsync"/> reads — and whether the start happens, and at which version, is the
    /// selection itself, asked without being counted as a start chosen. Nothing from the selection's
    /// machine-local half (the home, the binary, the key) is kept.
    /// </remarks>
    public async Task<StartWiring> WiringAsync(
        string adapter, DriverConfig config, string? workspace, CancellationToken ct = default)
    {
        var resolved = roster.Adapters.Resolve(adapter);
        var toolchain = resolved.Toolchain;
        var owner = roster.Adapters.Registry.OwnerOfAgent(resolved.Name).Value;
        var settings = roster.Settings;

        // The start's one set (POOL1a2, the account-pool design §2.4): where it begins, the order's first, read as the machine's
        // and never a workspace's (`StartWiring.ProfileFrom`); unset where the agent has no account.
        var scope = toolchain is null ? null : settings.Set(owner);
        var profileFrom = scope?.Begins is null ? ChoiceFrom.Unset : ChoiceFrom.Machine;
        // The same precedence `SelectAsync` applies: a command `driver.json` names has the last word.
        var commanded = config.Commands.GetValueOrDefault(resolved.Name) is { Count: > 0 };
        var (pinned, versionFrom) = commanded || toolchain is null
            ? (null, ChoiceFrom.Unset)
            : settings.ResolveVersionFrom(resolved.Name, workspace, chosen: null);

        // Asked as a start would ask, and counted as none: a panel starts nothing.
        // And asking no account nor binary (ROSTER1, D150 point 5): read from the last readings, as a view opens.
        var selection = await roster.ChooseAsync(adapter, config, workspace, chosen: null, StartKind.Driven, counts: false, ct, asks: false).ConfigureAwait(false);

        // The account a start would take (TOOL4f, TOOL6b): the walk's, with where the scope begins beside it when that is
        // another, so the panel never shows an account a start would not take; where begins, when the start is held.
        return new StartWiring(
            resolved.Name, owner, selection.Profile ?? scope?.Begins, profileFrom,
            selection.Version ?? pinned, versionFrom, commanded, selection.Refusal)
        {
            RotatedFrom = selection.Rotated?.From,
        };
    }

    // The windows of an account asked of its agent's own server, where its door carries none (CODEXUSE1, D125's CODEXUSE1 note):
    // kept in windows.json as a door's frame is (AccountWindows.Said), so near, pace, a start's line of what each account said,
    // `daoris agent list` and the page read a Codex account as they read a Claude Code one.
    //  - When, as ROSTER1 reads an account: at a person's press, the agent's or the account's; and at a start's walk over a list,
    //    where switching before the limit and pace read it, for each account of the list the start could take whose last reading
    //    is older than CodexUsage.Fresh. Never at a look, a panel, a pick or the page's next start.
    //  - The tool's own sign-in only at a person's press for it (CODEXUSE3): its row's Read, the agent's Read again and the
    //    roster's read-all, as its status question is asked (TOOL6g), since it is the person's own and their terminal runs on it.
    //    Its server is started with no account's home, so it answers wherever the agent keeps its own sign-in (D63: that home is
    //    the agent's), and its reading is kept under AccountWindows.Own. A walk never asks it: it is in no list (D125 §3.7), so
    //    its reading moves no start.
    //  - How, as a status question is asked (TOOL6g): under the account's ProbeLock, the own sign-in's under its own, and not at
    //    all while a session of Daoris's runs on it, whose own process may refresh its token; at a walk never an account cooling,
    //    refused, read signed out, or that is a key, whose plan has no windows. A reading not had is unknown and keeps nothing (D57).
}
