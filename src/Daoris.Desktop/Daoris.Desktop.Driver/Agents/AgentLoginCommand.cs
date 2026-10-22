namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver agent login</c> (PUSH1f2e6, D194, the alone-path design §1.1 and §1.4), the words <c>daoris agent login …</c> hands this
/// program: the agent's own sign-in, run at this terminal into an account's folder, the person answering its prompts. No route takes it (§2),
/// it is refused from a session's terminal (D194 point 5), and it holds no lease at its top: the folder is made before the flow and never
/// moved, and the sign-in's end (its mark, its cool-off ended, its name and its joins) is published under the home's lease, waited for and
/// never refused. Today's text is kept (<c>fixtures/agents-text.json</c>).
/// </summary>
/// <remarks>
/// Exit codes are the CLI's: 0 signed in · 1 a session's terminal · 2 a refusal of the words, a sign-in that did not finish, and the
/// person's Ctrl+C.
/// </remarks>
internal static class AgentLoginCommand
{
    private const string Prefix = "daoris";

    /// <summary>
    /// <c>login &lt;agent&gt; [--profile &lt;account&gt;] [--device-code]</c>, or <c>--new [--name &lt;name&gt;]</c> (D66 §3, ACCT1, ACCT2,
    /// CODEXLOGIN1; <c>--join</c> and <c>--join-machine</c> refused since POOL1b, D197): every check that needs no flow is made first, the browser's ports tried once
    /// where the agent waits on them, then the binary a start runs signs in (CODEXACCT1b, D57 rule 4), its pin before <c>PATH</c>'s.
    /// </summary>
    internal static async Task<int> LoginAsync(IReadOnlyList<string> args, AgentsWorld world, CancellationToken ct)
    {
        if (world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
        {
            await world.Instance.Error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
            return 1;
        }

        var path = AgentsCommand.WiringPath(world);
        var home = HarnessSettings.HomeOf(path);
        var (name, toolchain, _, _) = AgentsCommand.Agent(args, "login", "<agent>", world, path);
        if (toolchain.LoginArguments is not { Count: > 0 })
        {
            throw new AgentWords(
                $"`{name}` declares no login flow — log in with its own tooling, pointing {toolchain.ProfileVariable} at the profile directory.");
        }

        // CODEXLOGIN1: the tool's own sign-in first, its device code where asked.
        var byCode = args.Contains("--device-code");
        if (byCode && toolchain.DeviceLoginArguments is not { Count: > 0 })
        {
            throw new AgentWords($"`{name}` signs in by no device code, so nothing was signed in — its own sign-in is its one way.");
        }

        var login = SignInWays.Arguments(name, toolchain, byCode ? SignInWay.DeviceCode : SignInWay.Browser);

        // POOL1b (D197 point 6, the account-pool design §5.1): there are no lists to join, and a new account joins Daoris's accounts by
        // itself, so `--join` and `--join-machine` are refused before anything starts.
        if (args.Contains("--join") || args.Contains("--join-machine")) throw new AgentWords(OneSetWords.Join);
        var fresh = args.Contains("--new");
        if (fresh && args.Contains("--profile"))
        {
            throw new AgentWords(
                $"`--new` signs in to a new account and `--profile` to one that is here — `daoris agent login {name} --new`, or "
                + $"`daoris agent login {name} --profile {ShellWord.Of(AgentsCommand.FlagValue(args, "--profile") ?? "", ShellWord.Account)}`.");
        }

        if (!fresh && args.Contains("--name"))
        {
            throw new AgentWords(
                "`--name` names a new account as it is made (`--new`); an account that is here is named with `daoris agent "
                + $"profile rename {name} <account> <name>`.");
        }

        // CODEXACCT1b: the binary `agent list` asks, the pin before PATH's, refused before anything is made where the pin has nothing
        // installed at it; the sign-in and its end's question both run it.
        var settings = HarnessSettings.LoadSet(path);
        var binary = HarnessProbe.CommandOf(name, toolchain, command: null, settings, home);
        if (binary.Missing)
        {
            throw new AgentWords(
                $"`{name}` is pinned to {binary.Pinned} on this machine, and nothing is installed at that version, so nothing was signed in — "
                + $"`daoris agent pin {name} {binary.Pinned}` installs it, and `daoris agent unpin {name}` goes back to PATH.");
        }

        // The same verb by the other way, said where one way did not sign in (CODEXLOGIN1): a workspace may refuse either.
        var typed = string.Join(' ', ["daoris agent login", name, .. args.Skip(2).Where(word => word != "--device-code")]);
        string? otherWay = toolchain.DeviceLoginArguments is not { Count: > 0 } ? null
            : byCode ? $"`{typed}` signs in through your browser instead."
            : $"`{typed} --device-code` signs in with a device code instead.";

        // The browser's ports, each tried once before anything is made: where none can be taken, refused in the driver's words.
        if (!byCode && toolchain.LoginPorts is { Count: > 0 } ports)
        {
            var held = new List<(int Port, PortHold Hold)>();
            foreach (var port in ports)
            {
                var now = world.Ports(port);
                if (now == PortHold.Free)
                {
                    held.Clear();
                    break;
                }

                held.Add((port, now));
            }

            if (held.Count > 0)
            {
                throw new AgentWords(
                    $"{SignInPorts.Held(name, login, held)} Nothing was signed in — {otherWay ?? "close what holds it, or restart, and try again."}");
            }
        }

        var signIn = new SignIn(world, name, toolchain, home, path, settings, binary, login, ct);
        var code = fresh
            ? await signIn.NewAsync(AgentsCommand.FlagValue(args, "--name")).ConfigureAwait(false)
            : await signIn.ToAsync(AgentsCommand.FlagValue(args, "--profile")).ConfigureAwait(false);
        if (code != 0 && otherWay is not null && !ct.IsCancellationRequested) world.Instance.Out.WriteLine($"{Prefix}: {otherWay}");
        return code;
    }

    /// <summary>One sign-in's run: the flow at this terminal, then its end published under the home's lease.</summary>
    private sealed class SignIn(
        AgentsWorld world, string agent, HarnessToolchain toolchain, string home, string path, HarnessSettings before, HarnessCommand binary,
        IReadOnlyList<string> login, CancellationToken ct)
    {
        private void Say(string line) => world.Instance.Out.WriteLine(line);

        private void Told(string line) => world.Instance.Error.WriteLine(line);

        /// <summary>
        /// A new account (D66 §3, ACCT2): the flow into a folder of a fresh id, kept only where the sign-in finished — the agent exited 0
        /// and does not call that home signed out. A sign-in that did not finish, was stopped, or threw is removed in a <c>finally</c>, so the
        /// list is as it was.
        /// </summary>
        public async Task<int> NewAsync(string? named)
        {
            var id = AccountNames.NewId(home, agent);
            var wanted = named?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
            if (wanted is not null
                && AccountNames.Problem([.. HarnessSettings.Profiles(home, agent), id], AccountNames.Of(home, agent), id, wanted) is { } problem)
            {
                throw new AgentWords(problem.Kind == AccountNameProblemKind.Taken
                    ? $"`{wanted}` already names `{problem.Other}` — each `{agent}` account has a name of its own, so nothing was signed in — "
                      + AccountNames.TakenWayOut(wanted)
                    : $"`{wanted}` is not a name a terminal can type — a name is one word, with no space or backtick, not starting with a "
                      + "dash, at most 64 characters; nothing was signed in.");
            }

            var where = HarnessSettings.ProfileHome(home, agent, id);
            RefuseScratch(where);
            // Made for the sign-in, before the flow, and never moved after: an agent's own store may be keyed to its path.
            Directory.CreateDirectory(where);
            Say($"{Prefix}: signing in to a new `{agent}` account, with its own login flow.");
            Say($"  {where}");
            Say("  Daoris chose the directory and nothing else: whatever you sign in with is stored by the");
            Say("  agent, in its own store, under your OS account — Daoris never sees it.");

            var code = 2;
            var kept = false;
            try
            {
                code = await RunAsync(where).ConfigureAwait(false);
                // Asked of the binary the sign-in ran, so the answer is about the sign-in that just happened.
                var said = code == 0 ? await AskAsync(where, id).ConfigureAwait(false) : default;
                kept = code == 0 && said.Login != LoginState.Out;
                // A Ctrl+C while its end waited kept the account, signed in, and is the person's stop: exit 2.
                if (kept && !await EndAsync(id, said, wanted, fresh: true).ConfigureAwait(false)) return 2;
            }
            finally
            {
                if (!kept)
                {
                    try
                    {
                        HarnessSettings.RemoveProfile(home, agent, id);
                        Say($"{Prefix}: nothing was signed in, so nothing was kept — the account opened for it is gone again.");
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        Told($"{Prefix}: nothing was signed in, and {where} could not be removed — {error.Message}");
                    }
                }
            }

            // The CLI's: an agent that exited 0 and signed nobody in is no failure of the verb's, and said so above.
            return code == 0 ? 0 : 2;
        }

        /// <summary>
        /// An account that is here (ACCT1): the one named, by its id or its name, else the order's first (POOL1b, the account-pool design
        /// §5.1) — never a new folder, and with no account of Daoris's refused, never the tool's own home.
        /// </summary>
        public async Task<int> ToAsync(string? named)
        {
            var accounts = HarnessSettings.Profiles(home, agent);
            var names = AccountNames.Of(home, agent);
            var given = named?.Trim() is { Length: > 0 } spelled ? spelled : before.FirstOf(agent);
            if (given is null)
            {
                throw new AgentWords($"`{agent}` has no account of Daoris's to sign in to, and a sign-in into an account never makes one — "
                    + $"`daoris agent login {agent} --new` signs in to a new account.");
            }

            var target = AccountNames.SignInTarget(accounts, names, before, agent, given);
            if (target.Account is not { } account) throw new AgentWords(AccountNames.SignInRefusal(agent, target, accounts, names));

            var where = HarnessSettings.ProfileHome(home, agent, account);
            RefuseScratch(where);
            Say($"{Prefix}: running `{agent}`'s own login flow into the account `{AccountNames.Said(names, account)}`.");
            Say($"  {where}");
            Say("  Daoris chose the directory and nothing else: whatever you sign in with is stored by");
            Say("  the agent, in its own store, under your OS account — Daoris never sees it.");
            var code = await RunAsync(where).ConfigureAwait(false);
            if (code != 0) return code;

            // Who signed in is read before the end is published (SIGNIN1), as the window's sign-in reads it.
            return await EndAsync(account, await AskAsync(where, account).ConfigureAwait(false), wanted: null, fresh: false).ConfigureAwait(false)
                ? 0
                : 2;
        }

        /// <summary>
        /// 🔴 LIVELOGIN1: never the real agent under a scratch home, which signs the person's browser in on their real account. Refused
        /// after the verb's own refusals, which start nothing and say the better reason, and before the account's folder is made.
        /// </summary>
        private void RefuseScratch(string where)
        {
            if (ScratchSignIns.Refusal(toolchain, binary, where) is { } scratch) throw new AgentWords(scratch);
        }

        /// <summary>The flow at this terminal (<see cref="HarnessActions.TerminalRunAsync"/>), its exit mapped as the CLI's relay: 0, else 2.</summary>
        private async Task<int> RunAsync(string where)
        {
            int code;
            try
            {
                code = await HarnessActions.TerminalRunAsync(
                    [.. binary.Run, .. login], toolchain, where, home, Say, ct, binary.Managed).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Told($"{Prefix}: stopped at your Ctrl+C — `{agent}`'s sign-in was not started.");
                return 2;
            }
            catch (DriverException refused)
            {
                Told($"{Prefix}: {refused.Message}");
                return 2;
            }

            if (ct.IsCancellationRequested)
            {
                Told($"{Prefix}: stopped at your Ctrl+C — `{agent}`'s sign-in ended by its own rule (it exited {code}).");
                return 2;
            }

            return code == 0 ? 0 : 2;
        }

        /// <summary>
        /// Who signed in (D66 §3, ACCOUNTID1), asked of that one home under its probe lock (TOOL6g); unknown where the agent has no question
        /// or another holder kept the lock.
        /// </summary>
        private async Task<HarnessProbe.LoginAnswer> AskAsync(string where, string account) =>
            await HarnessProbe.AskAccountAsync(
                agent, toolchain, command: null, before, home, where, CancellationToken.None, ProbeLock.PathOf(home, agent, account), key: null)
                .ConfigureAwait(false);

        /// <summary>
        /// The sign-in's end, published under the home's lease (the alone-path design §1.4): the cool-off ended (D125 §2.3) and the mark
        /// (TOOL6g), the reading kept (ROSTER1), its name where the person named it (ACCT2) and a new account placed last in the order (POOL1a);
        /// then who signed in, and for a new account that it joined Daoris's accounts. A Ctrl+C while the publication waits keeps the account, signed in, and says what was not written and how to finish it.
        /// </summary>
        private async Task<bool> EndAsync(string account, HarnessProbe.LoginAnswer said, string? wanted, bool fresh)
        {
            var where = HarnessSettings.ProfileHome(home, agent, account);
            string? called = null;
            try
            {
                using (await HomeLease.ForPublicationAsync(home, "daoris-driver agent login",
                    holder => Told($"{Prefix}: waiting for {holder.Named} to let this home go; the sign-in's end is written then, and Ctrl+C stops."),
                    ct, world.Waiting, world.Now).ConfigureAwait(false))
                {
                    var now = world.Now();
                    try
                    {
                        AccountCooling.SignedIn(where, now);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        // The sign-in stands; the cool-off ends at its reset instead.
                    }

                    ProbeLock.MarkSignedIn(where, now);
                    if (said.Asked)
                    {
                        try
                        {
                            AccountReads.Keep(home, agent, account, said.Login, now);
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                        {
                            // A reading not kept costs the screen what this one learned, never the sign-in.
                        }
                    }

                    if (wanted is not null)
                    {
                        try
                        {
                            called = AccountNames.Rename(home, agent, HarnessSettings.Profiles(home, agent), account, wanted);
                        }
                        catch (AccountNameException error)
                        {
                            // The sign-in stands; the name is the person's to give again.
                            Say($"{Prefix}: the account was kept, and not named — {error.Message}");
                        }
                    }

                    // A new account goes last in the order, whatever its id (POOL1a, the account-pool design §2.2).
                    if (fresh) HarnessSettings.Placed(path, agent, account);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The order places it at the next change of the file (POOL1a1), so only a name is left to finish.
                Told($"{Prefix}: stopped at your Ctrl+C — the account `{account}` was kept, signed in, and its end was not written"
                    + (wanted is null
                        ? "."
                        : $": `daoris agent profile rename {agent} {ShellWord.Of(account, ShellWord.Account)} {ShellWord.Of(wanted, ShellWord.Name)}` finishes it."));
                return false;
            }

            var names = AccountNames.Of(home, agent);
            var accounts = HarnessSettings.Profiles(home, agent);
            if (fresh)
            {
                // ACCTNAME1, ACCOUNTID1c: the email offered as the name said once, and where it is another account's, what tells them apart.
                var apart = AccountNames.ApartWords(accounts, names, account, said.Account, said.Org, said.Plan);
                Say($"{Prefix}: {AccountNames.SignedInSentence(said.Account, agent, called, account, apart)}");
            }
            else
            {
                called = AccountNames.NameOf(home, agent, account);
            }

            // ACCOUNTID1: who signed in offered as its name, with the organisation beside it where another account has the email, or none;
            // offered only where it has no name of the person's.
            var offered = called is null ? AccountNames.Offered(accounts, names, account, said.Account, said.Org, said.Plan) : null;
            if (called is null && offered is { Length: > 0 })
            {
                Say($"  `daoris agent profile rename {agent} {ShellWord.Of(account, ShellWord.Account)} {ShellWord.Of(offered, ShellWord.Name)}` names it "
                    + $"{offered}; it reads as `{account}` until it has a name.");
            }
            else if (called is null)
            {
                Say($"  `daoris agent profile rename {agent} {ShellWord.Of(account, ShellWord.Account)} <name>` gives it a name of yours.");
            }

            // A new account's end says it joined, in the bridge's words for the page (POOL1a2, the account-pool design §4.4); a sign-back-in
            // says nothing of it.
            if (fresh) Say($"  {OneSetWords.Joined(AccountNames.Said(names, account))}");
            return true;
        }
    }
}
