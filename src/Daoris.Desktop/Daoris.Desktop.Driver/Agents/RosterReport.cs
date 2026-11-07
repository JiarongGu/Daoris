using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The roster's reading of each harness and its accounts: the report, a press, the binary's question, one account's status and its sign-in.
/// </summary>
internal sealed class RosterReport(HarnessRoster roster)
{
    /// <summary>
    /// One harness as this machine has it: what was last read of each of its accounts and its own sign-in, with when, and
    /// its binary. Without a press nothing is asked of an account (ROSTER1, D150 §5.3); <paramref name="refresh"/> is the
    /// person's press, reading every account one at a time, or with <paramref name="account"/> that account alone.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>No account is asked to answer a look</b>, at a cold cache either: a start, a panel, the page's roster and a
    /// restart read <see cref="AccountReads"/>, so the first roster read after the application starts asks nothing, where it
    /// asked every named account (UX6e). The binary is asked its version once per binary, kept under the home and asked again
    /// where its files changed (PERF3), which is a question of the binary and never of an account's home (§5.3).</para>
    /// <para>🔴 <b>One press of a harness at a time</b> (TOOL6g): a caller that finds one in flight takes its answer. Each
    /// account inside it is asked under its <see cref="ProbeLock"/>, and one a session of Daoris's runs on is not asked and
    /// keeps its word and its time. A press is a person looking again, so it lets a refused account of that agent through
    /// (AGT3b), and ends the tool's own home's cool-off (D125 §3.7) without asking it.</para>
    /// <para>🔴 <b>The tool's own sign-in is never asked</b> (POOL1a3, D197 point 3): it is the person's, and the agent may
    /// refresh their token to answer, beside their own terminal's. Its kept reading is said as it was.</para>
    /// </remarks>
    /// <param name="account">With <paramref name="refresh"/>, the one account a press reads: an account's own *Read again*.</param>
    public async Task<HarnessReport?> ReportAsync(
        string adapter, DriverConfig config, bool refresh = false, CancellationToken ct = default, string? account = null)
    {
        var resolved = roster.Adapters.Door(adapter);
        if (roster.Adapters.AgentNamed(resolved.Id.Value) is not { } toolchain) return null;
        var name = resolved.Id.Value;
        var owner = roster.Adapters.OwnerOfAgent(name).Value;

        if (refresh && account is not null)
        {
            var named = HarnessSettings.Profiles(roster.Home, owner).FirstOrDefault(each => string.Equals(each, account, StringComparison.OrdinalIgnoreCase))
                ?? throw new DriverException(
                    $"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                    + (HarnessSettings.Profiles(roster.Home, owner) is { Count: > 0 } accounts ? string.Join(", ", accounts) : "(none)"));
            roster._refused.TryRemove(HarnessRoster.AccountKey(owner, named), out _);
            // Asked the way its owner asks when this build carries the owner (AGT7): a door declares no login question.
            var asker = AnswersFor(name, toolchain);
            await AskOneAsync(asker.Name, asker, owner, named, config, ct).ConfigureAwait(false);
            // And its windows, where its agent's server is asked them (CODEXUSE1).
            await roster.AskUsageAsync(name, owner, named, config, ct).ConfigureAwait(false);
        }
        else if (refresh)
        {
            foreach (var refused in roster._refused.Keys.Where(key => key.StartsWith(owner + "/", StringComparison.OrdinalIgnoreCase)))
            {
                roster._refused.TryRemove(refused, out _);
            }

            // The own home's cool-off ends at the agent's *Read again* as at the roster's (D197's acceptance note): a sign-in
            // there happens at the person's terminal, which Daoris does not see, and the cool-off's line sends them here.
            roster.EndOwnCooling(owner);
            await Fly($"{name}/press", () => PressAsync(name, toolchain, owner, config), ct).ConfigureAwait(false);
        }

        return Composed(name, toolchain, await BinaryOfAsync(name, toolchain, config, again: false, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Ask an adapter's binary again whether it is there and its version, and no account (ROSTER1): what an install, an
    /// update or a pin changes, and what a start held on an absent agent asks before it refuses.
    /// </summary>
    public async Task BinaryChangedAsync(string adapter, DriverConfig config, CancellationToken ct = default)
    {
        var resolved = roster.Adapters.Door(adapter);
        if (roster.Adapters.AgentNamed(resolved.Id.Value) is not { } toolchain) return;
        await BinaryOfAsync(resolved.Id.Value, toolchain, config, again: true, ct).ConfigureAwait(false);
    }

    /// <summary>The person's press on one agent: every account of Daoris's asked one at a time, each answer kept; never the own sign-in.</summary>
    private async Task PressAsync(string name, AccountAgent toolchain, string owner, DriverConfig config)
    {
        var prior = Composed(name, toolchain, new HarnessRoster.Binary(true, null, null));
        // 🔴 A door onto an agent this build carries no door of (CODEXACCT1: `codex-acp`, `codex`) asks its binary as itself,
        // and its accounts as that agent asks them, with the agent's own binary: no press of the agent's would, and the door
        // has no status question.
        var holder = HolderOf(name, toolchain);
        var report = await HarnessProbe.ProbeAsync(
            name, toolchain, config.Commands.GetValueOrDefault(name), roster.Settings, roster.Home, CancellationToken.None,
            busy: account => Busy(owner, account), prior: prior,
            asks: holder is null ? null : _ => false, clock: roster.Clock,
            answered: (account, said, at) => roster.Keep(owner, account, said, at), accountOf: owner, again: true).ConfigureAwait(false);
        if (holder is { } agent)
        {
            await HarnessProbe.ProbeAsync(
                agent.Name, agent, config.Commands.GetValueOrDefault(agent.Name), roster.Settings, roster.Home, CancellationToken.None,
                busy: account => Busy(owner, account), prior: prior, clock: roster.Clock,
                answered: (account, said, at) => roster.Keep(owner, account, said, at), again: true).ConfigureAwait(false);
        }
        // And each account's windows, where its agent's server is asked them (CODEXUSE1); the own sign-in's never (D197 point 3).
        await roster.PressUsageAsync(name, owner, config, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// What the binary answered: kept under the home and asked again only where its files changed (PERF3), or where
    /// <paramref name="again"/>; never an account.
    /// </summary>
    private Task<HarnessRoster.Binary> BinaryOfAsync(string name, AccountAgent toolchain, DriverConfig config, bool again, CancellationToken ct) =>
        Fly($"{name}/binary{(again ? "/again" : "")}", async () =>
        {
            var report = await HarnessProbe.ProbeAsync(
                name, toolchain, config.Commands.GetValueOrDefault(name), roster.Settings, roster.Home, CancellationToken.None,
                asks: _ => false, clock: roster.Clock, accountOf: roster.Adapters.OwnerOfAgent(name).Value, again: again).ConfigureAwait(false);
            return new HarnessRoster.Binary(report.Present, report.Version, report.Problem);
        }, ct);

    /// <summary>
    /// One harness's report from what is known (ROSTER1): the accounts on disk now, each with what was last read of it and
    /// when, the tool's own sign-in likewise, and the binary's last answer. Nothing is asked.
    /// </summary>
    private HarnessReport Composed(string name, AccountAgent toolchain, HarnessRoster.Binary binary)
    {
        var owner = roster.Adapters.OwnerOfAgent(name).Value;
        var reads =AccountReads.Of(roster.Home, owner);
        var names = AccountNames.Of(roster.Home, owner);
        var profiles = HarnessSettings.Profiles(roster.Home, owner).Select(account =>
        {
            var read = reads.Accounts.GetValueOrDefault(account);
            var held = HarnessKeys.Of(roster.Home, owner, account);
            var who = WhoOf(owner, account, read);
            return new ProfileReport(
                account, HarnessSettings.ProfileHome(roster.Home, owner, account), read?.Login ?? LoginState.Unknown,
                who?.Account, held is null ? null : HarnessKeys.Handle(held), read?.At,
                names.GetValueOrDefault(account), who?.Org, who?.Plan);
        }).ToList();

        // HELPSTUCK1: the folders beside the accounts that are no account, each with whether Daoris kept a sign-in of it while it
        // was listed as one, so a sign-in in a lock's folder is said rather than lost.
        var strays = HarnessSettings.Strays(roster.Home, owner).Select(stray => new StrayFolder(
            stray,
            reads.Accounts.GetValueOrDefault(stray)?.Login == LoginState.In
            || ProbeLock.SignedIn(roster.Home, owner, stray) is not null
            || HarnessKeys.Of(roster.Home, owner, stray) is not null)).ToList();

        var own = WhoOf(owner, null, reads.Own);
        return new HarnessReport(
            name,
            binary.Present,
            binary.Present ? binary.Version : null,
            binary.Present ? null : binary.Problem,
            toolchain.Home.Variable,
            // The order's first, where the machine's default was (POOL1a2, the account-pool design §2.2).
            roster.Settings.FirstOf(owner),
            profiles,
            reads.Own?.Login ?? LoginState.Unknown,
            own?.Account,
            reads.Own?.At,
            own?.Org,
            own?.Plan)
        {
            Strays = strays,
        };
    }

    /// <summary>
    /// Who the reading this process made of an account said is signed in, with the organisation and the plan beside them
    /// (ACCOUNTID1), where that reading is the one kept and said yes; null otherwise.
    /// </summary>
    private LoginRead? WhoOf(string owner, string? account, AccountRead? read) =>
        read is not null && roster._who.TryGetValue(HarnessRoster.AccountKey(owner, account), out var said) && Second(said.At) == read.At
        && said.Said.Login == LoginState.In
            ? said.Said
            : null;

    private static DateTimeOffset Second(DateTimeOffset at) =>
        new(at.UtcTicks - (at.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

    /// <summary>
    /// Run <paramref name="work"/> unless one under <paramref name="flight"/> is in flight, whose end is then awaited instead
    /// (TOOL6g): its own task, never a caller's token, so a caller that stops waiting leaves the others their answer.
    /// </summary>
    private Task Fly(string flight, Func<Task> work, CancellationToken ct) =>
        Fly(flight, async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        }, ct);

    /// <summary><see cref="Fly(string, Func{Task}, CancellationToken)"/>, with the flight's answer shared by every caller that came.</summary>
    private async Task<T> Fly<T>(string flight, Func<Task<T>> work, CancellationToken ct)
    {
        Task<T> running;
        lock (_probing)
        {
            if (_probing.TryGetValue(flight, out var flying))
            {
                running = (Task<T>)flying;
            }
            else
            {
                running = Task.Run(work, CancellationToken.None);
                _probing[flight] = running;
                var started = running;
                _ = started.ContinueWith(_ =>
                {
                    lock (_probing)
                    {
                        if (_probing.TryGetValue(flight, out var current) && current == started) _probing.Remove(flight);
                    }
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        return await running.WaitAsync(ct).ConfigureAwait(false);
    }

    // What is in flight for each harness, by name and kind — a press, with or without the own sign-in, or its binary
    // (TOOL6g): one at a time, its answer shared by every caller that came. Two flights never ask one account at once: each
    // account has its lock.
    private readonly Dictionary<string, Task> _probing = new(StringComparer.OrdinalIgnoreCase);

    // When one account's status was last asked on its own (TOOL6g), by owner and account.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _askedAt =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the agent's word that an account is signed out is asked again, that account alone (TOOL6g): once per sign-out.
    /// The word is the later of its last reading (<paramref name="read"/>, kept under the home, ROSTER1) and its own last
    /// question; it stands until a sign-in or a key into the account at the terminal marks it
    /// (<see cref="ProbeLock.SignedInPathOf"/>) after the word, or until the word is <see cref="SignedOutAskedAgain"/> old.
    /// A sign-in on the screen reads that account as it ends, and a press reads it.
    /// </summary>
    internal bool AskAgain(string owner, string account, DateTimeOffset? read)
    {
        var said = read ?? DateTimeOffset.MinValue;
        if (_askedAt.TryGetValue(HarnessRoster.AccountKey(owner, account), out var alone) && alone > said) said = alone;
        return ProbeLock.SignedIn(roster.Home, owner, account) is { } marked && marked > said
               || roster.Clock() - said >= HarnessRoster.SignedOutAskedAgain;
    }

    /// <summary>
    /// Ask one account's status, under its lock, with its key where it is a key (AGT3), and keep the answer under the home
    /// (ROSTER1), so the next start, the screen's next start (<see cref="Next"/>) and a restart read it too: a start asking a
    /// signed-out account again (TOOL6g), and an account's own *Read again*. One a session of Daoris's runs on is not asked,
    /// and keeps its word and its time. Its lock is waited for <see cref="LockPatience"/>: one not had is said
    /// (<see cref="HarnessProbe.LoginAnswer.Locked"/>), so a walk leaves the account unknown for itself (PARALLEL1c).
    /// </summary>
    internal async Task<HarnessProbe.LoginAnswer> AskOneAsync(
        string adapter, AccountAgent toolchain, string owner, string account, DriverConfig config, CancellationToken ct)
    {
        var key = toolchain.Accounts.KeyVariable is { Length: > 0 } variable && HarnessKeys.Of(roster.Home, owner, account) is { } held
            ? new Dictionary<string, string> { [variable] = held }
            : null;
        var before = AccountReads.Of(roster.Home, owner).Accounts.GetValueOrDefault(account);
        var answer = roster.Asking is { } standIn
            ? new HarnessProbe.LoginAnswer(await standIn(owner, account, ct).ConfigureAwait(false), null, true)
            : await HarnessProbe.AskAccountAsync(
                adapter, toolchain, config.Commands.GetValueOrDefault(adapter), roster.Settings, roster.Home,
                HarnessSettings.ProfileHome(roster.Home, owner, account), ct, ProbeLock.PathOf(roster.Home, owner, account), key,
                busy: () => Busy(owner, account), kept: new LoginRead(before?.Login ?? LoginState.Unknown, null),
                patience: roster.LockPatience).ConfigureAwait(false);
        // A mark this question answered is spent, whatever the file's clock says against the roster's.
        var asked = roster.Clock();
        if (ProbeLock.SignedIn(roster.Home, owner, account) is { } marked && marked > asked) asked = marked;
        _askedAt[HarnessRoster.AccountKey(owner, account)] = asked;

        if (answer.Asked) roster.Keep(owner, account, answer.Said, roster.Clock());
        return answer;
    }

    /// <summary>
    /// Whether a session of Daoris's runs on an account of <paramref name="owner"/>'s now (TOOL6g): a running record the last
    /// look read, or a start chosen on it since; <paramref name="account"/> null is the tool's own sign-in. Its own process
    /// may refresh the account's token, so no probe asks it then.
    /// </summary>
    internal bool Busy(string owner, string? account)
    {
        lock (roster._load)
        {
            return roster._looked.Any(each => each.Session.Running
                                       && string.Equals(each.Owner, owner, StringComparison.OrdinalIgnoreCase)
                                       && string.Equals(each.Session.Profile ?? "", account ?? "", StringComparison.OrdinalIgnoreCase))
                   || (account is not null && roster._chosen.Any(start =>
                       string.Equals(start.Owner, owner, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(start.Account, account, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>
    /// What the harness says about ONE of its profiles — the question a sign-in asks as it ends
    /// (D66 §3), answered without probing every other account to learn it, and kept as that account's reading (ROSTER1):
    /// who signed in, with the organisation and the plan beside them (ACCOUNTID1).
    /// </summary>
    public async Task<LoginRead> LoginOfAsync(
        string adapter, DriverConfig config, string profile, CancellationToken ct = default)
    {
        var resolved = roster.Adapters.Door(adapter);
        if (roster.Adapters.AgentNamed(resolved.Id.Value) is not { } toolchain) return new LoginRead(LoginState.Unknown, null);

        // A door's account is its owner's (AGT7), asked the way the owner asks when this build has it.
        var owner = roster.Adapters.OwnerOfAgent(resolved.Id.Value).Value;
        var asker = AnswersFor(resolved.Id.Value, toolchain);
        var answer = await HarnessProbe.AskAccountAsync(
            asker.Name, asker, config.Commands.GetValueOrDefault(asker.Name), roster.Settings, roster.Home,
            HarnessSettings.ProfileHome(roster.Home, owner, profile), ct, ProbeLock.PathOf(roster.Home, owner, profile), key: null).ConfigureAwait(false);
        if (answer.Asked) roster.Keep(owner, profile, answer.Said, roster.Clock());
        return answer.Said;
    }

    /// <summary>
    /// The agent that answers for this adapter's accounts (AGT7, CODEXACCT1): what a sign-in on the door runs, with that agent's
    /// configured command, and what its accounts are asked with. Null for an adapter that declares no program.
    /// </summary>
    public AccountAgent? AccountAgentOf(string adapter) => roster.Adapters.AccountAgentOf(new DoorId(adapter));

    /// <summary>
    /// What a sign-in on this door runs as (CODEXACCT1): its account agent's binary, resolved as that agent's status question
    /// resolves it (<see cref="HarnessProbe.CommandOf"/>, D57 rule 4): the command named for it, its pin, else <c>PATH</c>'s.
    /// The install's only <c>codex</c> is its pin, so a sign-in on <c>PATH</c>'s could not start there. Null for an
    /// adapter that declares no toolchain.
    /// </summary>
    public HarnessCommand? SignInCommand(string adapter, DriverConfig config) =>
        AccountAgentOf(adapter) is { } agent
            ? HarnessProbe.CommandOf(agent.Name, agent, config.Commands.GetValueOrDefault(agent.Name), roster.Settings, roster.Home)
            : null;

    /// <summary>
    /// Which agent answers for a door's ACCOUNTS (AGT7): its owner when this build carries a door of it — the owner has the
    /// login question and the key variable — else the owner as the set declares it where it carries no door of it
    /// (<see cref="ProviderRegistry.AgentNamed"/>, CODEXACCT1: <c>codex</c> for <c>codex-acp</c>), else the door itself.
    /// </summary>
    /// <remarks>
    /// 🔴 A declared owner is no adapter: its name is never handed to <see cref="ReportAsync"/> or a press, which resolve
    /// adapters. <see cref="HolderOf"/> says which agent that is.
    /// </remarks>
    internal AccountAgent AnswersFor(string name, AccountAgent own) => roster.Adapters.AccountAgentOf(new DoorId(name)) ?? own;

    /// <summary>
    /// The agent a door asks its accounts through where this build carries no door of it (CODEXACCT1), or null where the
    /// door answers for its accounts itself or its owner's own door does: what a press on the door asks with, since no
    /// press of the owner's would.
    /// </summary>
    private AccountAgent? HolderOf(string name, AccountAgent toolchain)
    {
        var agent = AnswersFor(name, toolchain);
        return string.Equals(agent.Name, name, StringComparison.OrdinalIgnoreCase)
               || roster.Adapters.Names.Contains(agent.Name, StringComparer.OrdinalIgnoreCase)
            ? null
            : agent;
    }

    /// <summary>The adapter whose report says a door's accounts (AGT7): its owner's door where this build carries one, else itself.</summary>
    internal string ReportingDoor(string name, AccountAgent toolchain) =>
        AnswersFor(name, toolchain).Name is var agent && roster.Adapters.Names.Contains(agent, StringComparer.OrdinalIgnoreCase) ? agent : name;

    /// <summary>Every harness this build knows, probed — what a roster surface renders.</summary>
    /// <remarks>
    /// 🔴 <b>A door with nothing to run on this machine is not on the roster.</b> The stub's toolchain
    /// has no binary of its own — the "binary" is whatever `driver.json` names, which is what lets
    /// the rehearsal gate the roster with no model in it — and a deployed application names nothing,
    /// so it listed an agent tool called <c>stub</c> with an install button and nothing to install.
    /// Structural rather than a name: name a command for it and it is a real door again. The spawn
    /// path is untouched — <see cref="SelectAsync"/> still answers for any adapter it is asked about.
    /// </remarks>
    public async Task<IReadOnlyList<HarnessReport>> RosterAsync(
        DriverConfig config, bool refresh = false, CancellationToken ct = default)
    {
        // A person looking again is asking to try again (AGT3b): a refused account is let through. And the tool's own
        // home's cool-off ends (D125 §3.7): it means whoever was signed in there when the limit came, and a sign-in at
        // the person's own terminal since is not something Daoris sees. A named account's reset stands.
        if (refresh) roster.LookedAgain();

        var reports = new List<HarnessReport>();
        var registry = roster.Adapters;
        foreach (var name in roster.Known)
        {
            // The listing's rule (AGENTPROVIDER1 row b): a door naming a binary, or one the home's commands configure.
            if (!registry.Listed(new DoorId(name), config.Commands.GetValueOrDefault(name) is { Count: > 0 })) continue;

            // The tool's own sign-in is never asked, at a press either (POOL1a3, D197 point 3; Fable's F2): it is theirs, and
            // their terminal and their other sessions run on it.
            if (await ReportAsync(name, config, refresh, ct).ConfigureAwait(false) is { } report)
            {
                reports.Add(report);
            }
        }

        return reports;
    }
}
