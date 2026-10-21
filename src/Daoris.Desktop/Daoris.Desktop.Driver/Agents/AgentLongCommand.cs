namespace Daoris.Driver;

/// <summary>
/// The agent family's long verbs (PUSH1f2e5, D194, the alone-path design §1.4): <c>daoris-driver agent install</c> and <c>key</c>, the words
/// <c>daoris agent install|key …</c> hands this program. No route takes either (§2): each acts on the terminal's word, refused from a session's
/// terminal (D194 point 5), and holds no lease at its top. <c>install</c> runs the agent's own installer at this terminal, which writes the
/// machine and not the home, so it holds none at all; <c>key</c> reads the key from the terminal's standard input, holding nothing while the
/// person types, and publishes the key's account under the home's lease, waited for and never refused. Today's text is kept
/// (<c>fixtures/agents-text.json</c>).
/// </summary>
/// <remarks>
/// Exit codes are the CLI's: 0 done · 1 a session's terminal · 2 a refusal of the words, a key or an operation, an installer that did not
/// end well, and the person's Ctrl+C.
/// </remarks>
internal static class AgentLongCommand
{
    private const string Prefix = "daoris";

    /// <summary>A session's terminal (<c>DAORIS_SESSION_ID</c>): a person's press acting alone is refused there, nothing done.</summary>
    private static async Task<bool> RefusedHereAsync(AgentsWorld world)
    {
        if (world.Instance.Environment(DriverChoicesCommand.SessionVariable) is not { Length: > 0 }) return false;
        await world.Instance.Error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// <c>install &lt;agent&gt;</c> (D49 §4): the agent's own installer, never automatic, on the system's npm whatever Daoris runs node as
    /// (TOOLS5, D121 §2.7; <see cref="HarnessActions.OnTheSystem"/>), started at this terminal (<see cref="HarnessActions.TerminalRunAsync"/>).
    /// Its exit is the CLI's <c>relay</c>'s: 0 where the installer ended well, else 2.
    /// </summary>
    internal static async Task<int> InstallAsync(IReadOnlyList<string> args, AgentsWorld world, CancellationToken ct)
    {
        if (await RefusedHereAsync(world).ConfigureAwait(false)) return 1;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var path = AgentsCommand.WiringPath(world);
        var (name, toolchain, _, _) = AgentsCommand.Agent(args, "install", "<agent>", world, path);
        if (toolchain.Install is not { Count: > 0 } install)
        {
            throw new AgentWords(
                $"`{name}` declares no installer, so Daoris has no sanctioned way to install it. Install it with its own tooling; "
                + "`daoris agent list` will find it afterwards.");
        }

        Say($"{Prefix}: installing `{name}` with its own installer — nothing here is automatic (D49 §4).");
        IReadOnlyList<string> command;
        try
        {
            // The machine's npm, on the PATH this terminal has: a managed node's npm would install the agent into its own folder.
            command = HarnessActions.OnTheSystem(install, world.Instance.Environment(Tools.PathVariable) ?? "");
        }
        catch (DriverException refused)
        {
            throw new AgentWords(refused.Message);
        }

        return await TerminalAsync(world, command, toolchain, $"`{name}`'s installer", ct).ConfigureAwait(false);
    }

    /// <summary>An agent's own command at this terminal, its exit mapped as the CLI's <c>relay</c> mapped it: 0 is 0, anything else 2.</summary>
    private static async Task<int> TerminalAsync(
        AgentsWorld world, IReadOnlyList<string> command, HarnessToolchain toolchain, string what, CancellationToken ct)
    {
        var error = world.Instance.Error;
        int code;
        try
        {
            code = await HarnessActions.TerminalRunAsync(command, toolchain, profileHome: null, world.Instance.Home, world.Instance.Out.WriteLine, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await error.WriteLineAsync($"{Prefix}: stopped at your Ctrl+C — {what} was not started.").ConfigureAwait(false);
            return 2;
        }
        catch (DriverException refused)
        {
            await error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
            return 2;
        }

        if (ct.IsCancellationRequested)
        {
            // The Ctrl+C reached it too: it ended by its own rule, and what it had written is the machine's.
            await error.WriteLineAsync($"{Prefix}: stopped at your Ctrl+C — {what} ended by its own rule (it exited {code}).")
                .ConfigureAwait(false);
            return 2;
        }

        // A failed install is not a policy decision: anything but 0 is a tool error.
        return code == 0 ? 0 : 2;
    }

    /// <summary>
    /// <c>key &lt;agent&gt; [--account &lt;account&gt;]</c> (AGT3, D67 §1; KEYREPLACE1a): an account that is an API key, the key read from
    /// standard input and never an argument, which is in the process list and the shell's history. A door's key is its owner's account
    /// (AGT7). With <c>--account</c>, a new key into an account that is a key, by its id or its name: its id stays, so its name, lists and
    /// defaults stand. Every check that needs no key is made before the key is read; the key's account is then written under the home's
    /// lease (<see cref="HomeLease.ForPublicationAsync"/>) through <see cref="HarnessKeys"/>, the window's own writer.
    /// </summary>
    internal static async Task<int> KeyAsync(IReadOnlyList<string> args, AgentsWorld world, CancellationToken ct)
    {
        if (await RefusedHereAsync(world).ConfigureAwait(false)) return 1;
        var error = world.Instance.Error;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var path = AgentsCommand.WiringPath(world);
        var home = HarnessSettings.HomeOf(path);
        var (name, toolchain, _, roster) = AgentsCommand.Agent(args, "key", "<agent>", world, path);
        var replacing = AgentsCommand.FlagValue(args, "--account");
        if (AgentsCommand.Operands(args, AgentsCommand.Valued).Count > 2)
        {
            throw new AgentWords(
                "the key goes on stdin, not on the command line — pipe it in, or type it and end the input. "
                + "One given as an argument is now in your shell history; revoke it if that matters.");
        }

        // A key given for a door is its owner's account (AGT7), and it takes the owner's variable.
        var owner = toolchain.Owner(name);
        if ((toolchain.AccountOf is null ? toolchain : roster.AccountToolchain(name))?.KeyVariable is not { Length: > 0 })
        {
            throw new AgentWords($"`{name}` takes no API key from Daoris — sign in with its own login instead.");
        }

        string? account = null;
        if (replacing is not null)
        {
            var accounts = HarnessSettings.Profiles(home, owner);
            account = AgentsCommand.AccountNamed(home, owner, replacing);
            if (!accounts.Contains(account, StringComparer.Ordinal))
            {
                throw new AgentWords($"`{owner}` has no account `{replacing.Trim()}` on this machine — accounts that exist: "
                    + (accounts.Count > 0 ? string.Join(", ", accounts) : "(none)"));
            }

            if (HarnessKeys.Of(home, owner, account) is null)
            {
                throw new AgentWords(
                    $"`{account}` is not an API key account — it is a sign-in, so sign in again with `daoris agent login {owner}`.");
            }
        }

        string key;
        try
        {
            // To the end of the input: a pipe's end, or the person's end of input at a console. Ctrl+C stops the wait for it: the
            // console's reader reads synchronously, so it reads on a thread of its own, left to end with the process.
            var read = Task.Run(() => world.Instance.In.ReadToEndAsync(ct), CancellationToken.None);
            key = (await read.WaitAsync(ct).ConfigureAwait(false)).Trim();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await error.WriteLineAsync($"{Prefix}: stopped at your Ctrl+C — no key was read, and nothing was written.").ConfigureAwait(false);
            return 2;
        }

        if (key.Length == 0 || key.Any(char.IsWhiteSpace)) throw new AgentWords("that is not an API key — it is blank, or has spaces in it.");

        try
        {
            using (await HomeLease.ForPublicationAsync(home, "daoris-driver agent key",
                holder => error.WriteLine(
                    $"{Prefix}: waiting for {holder.Named} to let this home go; the key is written then, and Ctrl+C stops."),
                ct, world.Waiting, world.Now).ConfigureAwait(false))
            {
                account = account is null ? HarnessKeys.Add(home, owner, key) : HarnessKeys.Replace(home, owner, account, key);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await error.WriteLineAsync($"{Prefix}: stopped at your Ctrl+C — the key was not written.").ConfigureAwait(false);
            return 2;
        }
        catch (DriverException refused)
        {
            // The keys file refused the write, as the window's is refused: one line with its code (D194 point 6).
            await error.WriteLineAsync($"{Prefix}: {refused.Message} ({ChoiceCodes.Of(refused)})").ConfigureAwait(false);
            return 2;
        }
        catch (IOException held)
        {
            // The file's edit lock, held by another writer for the whole of its wait: nothing was written.
            await error.WriteLineAsync($"{Prefix}: {held.Message}").ConfigureAwait(false);
            return 2;
        }

        var kept = DaorisHome.Entry(home, HomeEntry.Keys);
        if (replacing is null)
        {
            Say($"{Prefix}: `{owner}` account `{account}` is the API key {HarnessKeys.Handle(key)}.");
            Say($"  Kept in {kept} — machine-local, tracked by nothing, shown back only as its last four.");
            var id = ShellWord.Of(account, "<account>");
            Say($"  `daoris agent profile rename {owner} {id} <name>` gives it a name of yours (ACCT2), and");
            Say($"  `daoris agent profile join {owner} {id} <workspace>…|--machine` puts it in a list a start runs on.");
        }
        else
        {
            Say($"{Prefix}: `{owner}` account `{account}` is now the API key {HarnessKeys.Handle(key)}; its name, lists and defaults are as they were.");
            Say($"  Kept in {kept} — machine-local, tracked by nothing, shown back only as its last four.");
            Say("  A running desktop reads the account again at its next press; this terminal cannot tell it sooner.");
        }

        return 0;
    }
}
