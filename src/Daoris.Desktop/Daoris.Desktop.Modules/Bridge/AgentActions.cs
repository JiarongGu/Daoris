using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A sign-in's phases as the driver writes them into its relay (SIGNIN1, the UI/UX design §3.2): each a step the driver
/// performs or observes itself, never one guessed from the tool's words.
/// </summary>
internal static class SignInPhase
{
    /// <summary>From the press until the tool's process is up.</summary>
    public const string Start = "start";

    /// <summary>The tool's process up until it exits: whatever it does with the browser is this one phase.</summary>
    public const string Browser = "browser";

    /// <summary>The driver's own read of who signed in.</summary>
    public const string Who = "who";

    /// <summary>The driver's own write of the lists the account joins.</summary>
    public const string Lists = "lists";
}

/// <summary>
/// The one harness action running on this machine, and the path that starts it (PART10f, D166): Agents' <c>HARNESS_ACTION</c>,
/// <c>HARNESS_INPUT</c> and <c>HARNESS_CANCEL</c> and Ask Daoris's Apply of an update or a pin start, answer and stop through
/// this one instance, held by <see cref="DriverContext.Actions"/>, so the two doors share the one slot and cannot differ (HELP6).
/// No handler class holds another's state. (Named for what it holds: the Driver library's static
/// <see cref="HarnessActions"/> is the processes themselves.)
/// </summary>
public sealed class AgentActions(IEventBus events, DriverLoop loop)
{
    private readonly IEventBus _events = events;
    private readonly DriverLoop _loop = loop;

    // The harness action running now, by `harness:action` — one at a time by construction, and the
    // two things a screen may do to it while it runs: answer the prompt it printed, or stop it.
    private readonly ConcurrentDictionary<string, HarnessRun> _actions = new();

    // Which `harness:action` holds the one slot, from its start until `RunActionAsync` ends it (REV3).
    private string? _acting;

    // What each `harness:action` said in its latest run, and the life of the one running (SIGNIN1).
    private readonly ConcurrentDictionary<string, Said> _said = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Life> _lives = new(StringComparer.Ordinal);

    // Each `harness:action`'s latest run to its end, its news included: what a sign-in's switch waits for (CODEXLOGIN1).
    private readonly ConcurrentDictionary<string, Task> _works = new(StringComparer.Ordinal);

    /// <summary>
    /// Every action <c>HARNESS_ACTION</c> takes, in the order its unknown-action refusal names them: the words a refused one is
    /// logged by (SIGNIN1), so the log never holds a word the page sent that is not one of them.
    /// </summary>
    internal static readonly IReadOnlyList<string> Words =
    [
        "install", "update", "login", "login-new", "key-add", "key-replace", "pin", "unpin",
        "profile-add", "profile-remove", "profile-default", "profile-rename", "profile-join",
    ];

    /// <summary>
    /// A harness action's output, relayed as it happens — the same console the session drawer already
    /// renders (D49 §2), because an install that printed nothing until it finished is indistinguishable
    /// from one that hung.
    /// </summary>
    /// <remarks>
    /// <para>Keyed by `harness:action` rather than a session id: it is not a session, has no record,
    /// and must never look like one. The page subscribes to it exactly as it subscribes to a
    /// session's.</para>
    ///
    /// <para>The sequence still counts, because the page's merge rule is "drop anything already
    /// seen" — every line arriving as number 0 would render as one line repeatedly overwritten.</para>
    ///
    /// <para><b>The latest run's lines are kept</b> (SIGNIN1), bounded as a session's are, and answered over
    /// <c>TAIL_SESSION</c> (<see cref="Tail"/>): a sign-in's panel mounted after its link was printed, as one is on coming
    /// back to its page, read nothing, and its phases are lines too. The number goes on across runs of one action, so a panel
    /// that stayed up through a *Try again* merges the second run's lines after the first's rather than dropping them as seen.</para>
    /// </remarks>
    internal Action<string> Relay(string harness, string action)
    {
        var key = $"{harness}:{action}";
        return line => Say(key, line);
    }

    /// <summary>One line into <paramref name="key"/>'s relay: kept for a late panel, read for the log's ask, then sent.</summary>
    private void Say(string key, string text)
    {
        var said = _said.GetOrAdd(key, _ => new Said());
        ConsoleLine line;
        lock (said)
        {
            line = new ConsoleLine(++said.Sequence, text);
            said.Lines.Enqueue(line);
            while (said.Lines.Count > SessionOutput.LinesPerSession)
            {
                said.Lines.Dequeue();
                said.Dropped += 1;
            }
        }

        if (_lives.TryGetValue(key, out var life) && life.Action is "login" or "login-new") life.Heard(text, _loop.Log);
        _ = DriverLoop.EmitOutput(_events, key, [line]);
    }

    /// <summary>
    /// What <paramref name="key"/>'s latest run has said since <paramref name="after"/>, and whether it still runs; null where
    /// no action of that key has said anything here (SIGNIN1). <c>TAIL_SESSION</c> answers it for a key no session holds.
    /// </summary>
    internal ConsoleTail? Tail(string key, long after)
    {
        if (!_said.TryGetValue(key, out var said)) return null;
        lock (said)
        {
            return new([.. said.Lines.Where(line => line.Sequence > after)], said.Sequence, Volatile.Read(ref _acting) == key, said.Dropped);
        }
    }

    /// <summary>
    /// The line a sign-in's phase is written as, with its start (SIGNIN1, the UI/UX design §3.2): the record separator, which
    /// begins no line a tool prints (<see cref="HarnessActions.Clean"/> drops it), then the phase and when it began, in UTC.
    /// </summary>
    internal static string PhaseLine(string phase, DateTimeOffset at) =>
        "\u001e" + JsonSerializer.Serialize(new
        {
            phase,
            at = at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        });

    private void Phase(Life life, string phase) => Say(life.Key, PhaseLine(phase, DateTimeOffset.UtcNow));

    /// <summary>
    /// A process action on a harness, answered once it has started — <c>HARNESS_ACTION</c>'s own start,
    /// and the one Ask Daoris's Apply of an update or a pin calls too (HELP6), so the two cannot differ.
    /// </summary>
    /// <remarks>
    /// 🔴 A process action is answered when the process has STARTED, and its end is news
    /// (HARNESS_ENDED) — the same way a conversation's ending is (D49 §3). It waits on a network, or on
    /// a person in a browser, and a request that waited with it timed out on the bridge at thirty seconds
    /// while the login ran on: the page closed its panel, the row said nothing had changed, and the
    /// process kept waiting for a browser nobody was told about (measured on the installed shell,
    /// 2026-09-23). While it runs the page may answer it or stop it (HARNESS_INPUT, HARNESS_CANCEL).
    /// </remarks>
    /// <param name="ended">Told the exit code and any problem once the process ends, after the news goes out.</param>
    /// <param name="joins">The lists a sign-in puts its account in at its end (ACCT1): a workspace's name, or null for this machine's.</param>
    /// <param name="name">A new account's name, the person's, given as it is made (ACCT2).</param>
    /// <param name="way">A sign-in's way (CODEXLOGIN1): the agent's own sign-in, its browser first, or its device code.</param>
    /// <param name="switching">
    /// Whether this sign-in takes the place of the same one running another way (CODEXLOGIN1): that one is stopped, its end
    /// told as switched, before this one starts.
    /// </param>
    /// <returns>The account a sign-in reaches: the one named, or a new account's fresh id; null for any other action.</returns>
    internal async Task<string?> StartProcessActionAsync(
        string harness, string action, string? profile, string? version, HarnessToolchain toolchain,
        IReadOnlyList<string>? command, Action<string> stream, DriverConfig config, Action<int, string?>? ended = null,
        IReadOnlyList<string?>? joins = null, string? name = null, SignInWay way = SignInWay.Browser, bool switching = false)
    {
        var owner = toolchain.Owner(harness);
        var key = $"{harness}:{action}";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signsIn = action is "login" or "login-new";
        // Its life from the press (SIGNIN1): each phase's start, for its panel and the machine log.
        var life = new Life(key, harness, action);
        Action<HarnessRun> track = run =>
        {
            _actions[key] = run;
            // Stopped for a switch before its process was up (CODEXLOGIN1): it stops as soon as there is one to stop.
            if (life.Cancelled) run.Cancel();
            if (life.Up is null)
            {
                life.Up = life.Now;
                _loop.Log?.Info("harness.started", ("harness", harness), ("action", action), ("ms", life.Up));
                // The tool's process is up: what it does with the browser now is one phase, ending when it exits (R2).
                if (signsIn) Phase(life, SignInPhase.Browser);
            }

            started.TrySetResult();
        };
        var home = _loop.Harnesses.Home;
        var accounts = HarnessSettings.Profiles(home, owner);
        var names = AccountNames.Of(home, owner);
        // 🔴 Signing in to a NEW account (D66 §3): the account is made by the sign-in, not named before it. It opens under
        // a fresh id (ACCT2) — nobody knows whose it is yet — and the tool's own answer names who once it ends, offered to
        // the person as its name.
        var fresh = action == "login-new" ? AccountNames.NewId(home, owner) : null;
        // 🔴 Signing back in to an account that is here (ACCT1): the one named, by its id or its name, else the machine's
        // default — never a new folder. The install's owner signed in to bring a signed-out account back and got a new
        // account no list held.
        string? target = null;
        if (action == "login")
        {
            var reached = AccountNames.SignInTarget(accounts, names, _loop.Harnesses.Settings, owner, profile);
            target = reached.Account ?? throw new DriverException(AccountNames.SignInRefusal(owner, reached, accounts, names));
        }

        // What it joins and is called is refused before anything starts, so a refusal costs nothing.
        var lists = joins ?? [];
        if (action is "login" or "login-new") RefuseJoins(_loop, owner, lists);
        if (fresh is not null && name is not null
            && AccountNames.Problem([.. accounts, fresh], names, fresh, name) is { } problem)
        {
            throw new DriverException(problem.Kind == AccountNameProblemKind.Taken
                ? $"`{name.Trim()}` already names `{problem.Other}`, so nothing was signed in — each `{owner}` account has a name of its own — {AccountNames.TakenWayOut(name.Trim())}"
                : $"`{name.Trim()}` is not a name a terminal can type, so nothing was signed in — a name is one word, with no "
                  + $"space or backtick, not starting with a dash, at most {AccountNames.Longest} characters.");
        }

        var profileHome = HarnessSettings.ProfileHome(home, owner, fresh ?? target ?? profile ?? "default");
        // 🔴 A sign-in is its accounts' agent's (AGT7, CODEXACCT1): a door onto another agent runs that agent's own flow, on the
        // binary its status question asks: the command named for it, its pin, else PATH's (D57 rule 4). `codex-acp` has no flow
        // of its own and runs `codex login`, its browser first, or `codex login --device-auth` asked for (CODEXLOGIN1); refused
        // as a door with none, Agents → Codex offered no account while `daoris agent login codex --new` made one (D50), and
        // the install's only `codex` is its pin.
        var signing = action is "login" or "login-new" ? _loop.Harnesses.AccountAgentOf(harness) : null;
        var signingToolchain = signing?.Toolchain ?? toolchain;
        var signingRun = signing is null ? null : _loop.Harnesses.SignInCommand(harness, config);
        if (signsIn)
        {
            // The way asked is one the agent has (CODEXLOGIN1), refused before anything starts where it is not.
            SignInWays.Arguments(owner, signingToolchain, way);
            // 🔴 Its browser's answer needs a port of this machine (CODEXLOGIN1): each it tries is taken for a moment, once per
            // press, and where none can be the sign-in is refused in plain words, the device code offered, nothing made and the
            // running one left as it is. Never read from Windows' table alone: a range is not the only holder.
            if (way == SignInWay.Browser && SignInPorts.Refusal(owner, signingToolchain, _loop.Harnesses.Ports) is { } held)
            {
                throw new DriverException(held);
            }
        }

        Func<Task<int>> run = action switch
        {
            "install" => () => HarnessActions.InstallAsync(toolchain, stream, CancellationToken.None, track),
            "update" => () => UpdateAsync(harness, toolchain, command, stream, CancellationToken.None, track),
            "login" => () => SignInToAsync(
                harness, owner, target!, signingToolchain, signingRun!, profileHome, stream, config, track, lists, life, way),
            "login-new" => () => SignInAsync(
                harness, owner, fresh!, signingToolchain, signingRun!, profileHome, stream, config, track, lists, name, life, way),
            // The managed toolchain (TOOL2/D57) — the desktop's half of `daoris agent pin|unpin`, over
            // the same file.
            _ => () => PinAsync(harness, toolchain, stream, version!, CancellationToken.None, track),
        };

        // 🔴 A switch stops the same sign-in running another way, and waits for its end before this one claims the slot
        // (CODEXLOGIN1): the person may change ways at any time, since a device code a workspace refuses may say so only on
        // the maker's page while the tool waits out its fifteen minutes. The child is Daoris's own, stopped as *Cancel* stops it.
        if (switching && signsIn) await StopForSwitchAsync(key);

        // 🔴 One at a time on this machine, claimed as the action starts and released by
        // `RunActionAsync` however it ends (REV3). The page re-enabled its buttons once the request
        // answered `started`, and a second login under the same name took the first's place in
        // `_actions`: the first could no longer be answered or stopped, and its end removed the second's
        // entry. Claimed last, so nothing above can throw with it held.
        if (Interlocked.CompareExchange(ref _acting, key, null) is { } busy)
        {
            throw Refusals.Because(
                Refusals.HarnessActionBusy,
                $"{busy} is still running — wait for it to end, or stop it, before starting another.",
                ("running", busy));
        }

        // The run's lines are its own from here, and a sign-in says its first phase (SIGNIN1): a panel mounted later reads them.
        var said = _said.GetOrAdd(key, _ => new Said());
        lock (said)
        {
            said.Lines.Clear();
            said.Dropped = 0;
        }

        _lives[key] = life;
        if (signsIn) Phase(life, SignInPhase.Start);

        var reaching = fresh ?? target ?? profile;
        var work = RunActionAsync(key, harness, action, reaching, run, started.Task, config, life, ended);
        _works[key] = work;

        await Task.WhenAny(started.Task, work);
        // A refusal before the process started — no installer, no login flow, a binary that did not
        // start — travels as a refusal, exactly as it did when the request waited.
        if (work.IsCompleted) await work;
        return action is "login" or "login-new" ? reaching : null;
    }

    /// <summary>
    /// Stop the sign-in running under <paramref name="key"/> for one by another way (CODEXLOGIN1), and wait for its end, its
    /// news told as switched so the page keeps its panel. Nothing running there is nothing to stop: the switch starts as a
    /// press would.
    /// </summary>
    private async Task StopForSwitchAsync(string key)
    {
        if (Volatile.Read(ref _acting) != key || !_works.TryGetValue(key, out var running)) return;
        if (_lives.TryGetValue(key, out var life))
        {
            life.Switched = true;
            life.Cancelled = true;
        }

        if (_actions.TryGetValue(key, out var run)) run.Cancel();
        try
        {
            await running;
        }
        catch (Exception)
        {
            // Its end was said as news; this one starts whatever it was.
        }
    }

    /// <summary>
    /// A process action to its end, and the end announced — after the request that started it has
    /// been answered. A failure before the process started is the caller's to refuse; one after it
    /// is news like any other end, because nobody awaits this any more.
    /// </summary>
    private async Task RunActionAsync(
        string key, string harness, string action, string? profile, Func<Task<int>> run, Task started,
        DriverConfig config, Life life, Action<int, string?>? ended = null)
    {
        int code;
        try
        {
            code = await run();
            life.Exited ??= life.Now;
        }
        catch (Exception error) when (started.IsCompletedSuccessfully)
        {
            await AnnounceAsync(harness, action, profile, -1, error.Message, config, life);
            Ended(life, "error", null);
            Tell(-1, error.Message);
            return;
        }
        finally
        {
            _actions.TryRemove(key, out _);
            _lives.TryRemove(key, out _);
            // The slot goes with the action, however it ended — before it started included.
            Volatile.Write(ref _acting, null);
        }

        await AnnounceAsync(harness, action, profile, code, null, config, life);
        Ended(life, life.Cancelled ? "cancelled" : code != 0 ? "failed" : life.Nobody ? "nobody" : "done", code);
        Tell(code, null);

        // Whoever asked to hear the end (HELP6: Ask Daoris's conversation) hears it after the news; a
        // listener that fails costs its own view, never the end the screen was told.
        void Tell(int exit, string? problem)
        {
            try
            {
                ended?.Invoke(exit, problem);
            }
            catch (Exception)
            {
                // The news already went out.
            }
        }
    }

    /// <summary>
    /// An action's end in the machine log (SIGNIN1, the machine-log design §4): how it ended by a word, its exit code, and each
    /// phase's milliseconds, null for one it never reached. Never the tool's output, a link, a code or who signed in.
    /// </summary>
    private void Ended(Life life, string outcome, int? code)
    {
        var now = life.Now;
        _loop.Log?.Info("harness.ended",
            ("harness", life.Harness), ("action", life.Action), ("outcome", outcome), ("code", code),
            ("startMs", life.Up), ("toolMs", life.Exited - life.Up),
            ("whoMs", life.WhoDone - life.Who), ("listsMs", life.Joined - life.Lists), ("ms", now));
    }

    private async Task AnnounceAsync(
        string harness, string action, string? profile, int code, string? problem, DriverConfig config, Life? life = null)
    {
        // What the act changed is read again before the news, and nothing else (ROSTER1): a sign-in's end reads its
        // account, the binary an install, an update or a pin changed; a sign-in to another account read its own already, and
        // a sign-in back in to one read who before it joined the lists (SIGNIN1).
        HarnessReport? report = null;
        var signingIn = action is "login" or "login-new" && profile is not null;
        try
        {
            await AgentsRoutes.ChangedAsync(_loop, harness, action, config, CancellationToken.None);
            report = action == "login" && profile is not null
                ? life?.Read ?? await _loop.Harnesses.ReportAsync(harness, config, refresh: true, CancellationToken.None, account: profile)
                : signingIn ? await _loop.Harnesses.ReportAsync(harness, config, ct: CancellationToken.None) : null;
        }
        catch (Exception)
        {
            // The roster says what was last read on the page's next question; the end is still news.
        }

        var signedIn = signingIn ? report?.Profiles.FirstOrDefault(each => each.Name == profile) : null;
        var owner = _loop.Harnesses.Toolchain(harness)?.Owner(harness) ?? harness;
        await _events.EmitAsync("DAORIS", "HARNESS_ENDED", new
        {
            Harness = harness, Action = action, Profile = profile, ExitCode = code, Problem = problem,
            // Who signed in (D66 §3) — the tool's own answer, from the reading the sign-in's end made, so
            // the sentence a person hears names the account the way they know it.
            Account = signedIn?.Account,
            // The organisation and the plan beside who (ACCOUNTID1), from the same reading, written nowhere.
            signedIn?.Org,
            signedIn?.Plan,
            // The name offered for it (ACCOUNTID1, AccountNames.Offered): who signed in, or with the organisation beside it
            // where another account already has the email, so accepting it names the account; null where none tells them
            // apart, and the person names it. Offered, never written (D66 §3).
            Offered = signedIn is { Account: { } who }
                ? Offer(owner, signedIn.Name, who, signedIn.Org, signedIn.Plan)
                : null,
            // Whether a sign-in to another account left one behind: it does only when it finished.
            Kept = action == "login-new" && profile is not null
                ? Directory.Exists(HarnessSettings.ProfileHome(
                    _loop.Harnesses.Home, _loop.Harnesses.Toolchain(harness)?.Owner(harness) ?? harness, profile))
                : (bool?)null,
            // Where the account signed in runs now, its joins made (ACCT1): each list and default holding it, this machine a
            // null workspace; none is an account no start runs on, which the page asks about at the sign-in's end.
            Places = signingIn
                ? AgentsRoutes.PlacesShown(_loop.Harnesses.Settings.PlacesOf(owner, profile!))
                : null,
            // Whether the person's *Cancel* stopped it (SIGNIN1), so its end is no failure to say: the page closes its panel.
            Cancelled = life?.Cancelled ?? false,
            // Whether it was stopped for the same sign-in by another way (CODEXLOGIN1), which runs on in the same panel: the page
            // says nothing of this end and keeps the panel. Absent from an older shell.
            Switched = life?.Switched ?? false,
        });
    }

    /// <summary>The name a sign-in's end offers an account (ACCOUNTID1), or null where the names do not read.</summary>
    private string? Offer(string owner, string account, string who, string? org, string? plan)
    {
        var home = _loop.Harnesses.Home;
        try
        {
            return AccountNames.Offered(HarnessSettings.Profiles(home, owner), AccountNames.Of(home, owner), account, who, org, plan);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The end is news whatever the names say; the page offers nothing and the person names it.
            return null;
        }
    }

    /// <summary>
    /// Sign in to another account (D66 §3): the tool's own login flow into a fresh directory, kept
    /// only when the sign-in finished.
    /// </summary>
    /// <remarks>
    /// "Finished" is the tool's exit code and then the tool's own word, asked of that one home: a
    /// zero exit whose home still reports signed OUT signed nobody in. An answer it cannot give is
    /// kept, by the rule every unknown login state follows (SES3) — the person watched the sign-in
    /// and can remove it. Otherwise the directory existed only for this sign-in, and goes: the list
    /// is exactly what it was before the press, whether the tool failed, was stopped, or never
    /// started.
    /// </remarks>
    /// <param name="harness">The door pressed, whose account agent's end question reads the new account.</param>
    /// <param name="owner">Whose accounts the door runs as (AGT7): the new account's folder is under it.</param>
    /// <param name="toolchain">The account agent's toolchain, whose own flow signs in (CODEXACCT1).</param>
    /// <param name="run">What the account agent runs as: its named command, its pin, else PATH's (D57 rule 4).</param>
    private async Task<int> SignInAsync(
        string harness, string owner, string fresh, HarnessToolchain toolchain, HarnessCommand run,
        string profileHome, Action<string> stream, DriverConfig config, Action<HarnessRun> track,
        IReadOnlyList<string?> joins, string? name, Life life, SignInWay way)
    {
        var code = -1;
        try
        {
            code = await HarnessActions.LoginAsync(
                toolchain, run, profileHome, stream, CancellationToken.None, track, fresh: true, way: way);
            return code;
        }
        finally
        {
            life.Exited ??= life.Now;
            // The tool has exited: what the driver does now are phases of its own (SIGNIN1), who signed in, then the lists.
            if (code == 0)
            {
                life.Who = life.Now;
                Phase(life, SignInPhase.Who);
            }

            var said = code == 0
                ? await _loop.Harnesses.LoginOfAsync(harness, config, fresh, CancellationToken.None)
                : new LoginRead(LoginState.Out, null);
            if (code == 0) life.WhoDone = life.Now;

            if (code != 0 || said.Login == LoginState.Out)
            {
                life.Nobody = code == 0;
                try
                {
                    HarnessSettings.RemoveProfile(_loop.Harnesses.Home, owner, fresh);
                    _loop.Harnesses.Removed(owner, fresh);
                    stream(life.Switched
                        ? "this way was stopped for the other, so nothing was kept from it — the account opened for it is gone again."
                        : "nothing was signed in, so nothing was kept — the account opened for it is gone again.");
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    stream($"nothing was signed in, and {profileHome} could not be removed — {error.Message}");
                }
            }
            else
            {
                // Named where the person named it (ACCT2); who signed in is offered on the page, never written here (D66 §3).
                var called = Kept(() => AccountNames.Rename(
                    _loop.Harnesses.Home, owner, HarnessSettings.Profiles(_loop.Harnesses.Home, owner), fresh, name), name is not null, stream);
                var accounts = HarnessSettings.Profiles(_loop.Harnesses.Home, owner);
                var apart = said.Account is { } who
                    ? AccountNames.ApartWords(accounts, AccountNames.Of(_loop.Harnesses.Home, owner), fresh, who, said.Org, said.Plan)
                    : null;
                stream(AgentsRoutes.SignedInSentence(said.Account, owner, called, fresh, apart));
                life.Lists = life.Now;
                Phase(life, SignInPhase.Lists);
                Placed(owner, fresh, joins, stream);
                life.Joined = life.Now;
            }
        }
    }

    /// <summary>
    /// Sign back in to an account that is here (ACCT1): the tool's own login flow into its folder, never a new one, and at
    /// its end the lists the person named joined and where it runs said.
    /// </summary>
    /// <remarks>
    /// Who signed in is read before the lists are joined (SIGNIN1, the UI/UX design §3.2's phases), and the end's news says it
    /// from that one reading rather than asking the tool again; where the reading failed, the end reads as before.
    /// </remarks>
    private async Task<int> SignInToAsync(
        string harness, string owner, string account, HarnessToolchain toolchain, HarnessCommand run, string profileHome,
        Action<string> stream, DriverConfig config, Action<HarnessRun> track, IReadOnlyList<string?> joins, Life life,
        SignInWay way)
    {
        var code = await HarnessActions.LoginAsync(toolchain, run, profileHome, stream, CancellationToken.None, track, way: way);
        life.Exited = life.Now;
        if (code != 0) return code;

        life.Who = life.Now;
        Phase(life, SignInPhase.Who);
        try
        {
            life.Read = await _loop.Harnesses.ReportAsync(harness, config, refresh: true, CancellationToken.None, account: account);
        }
        catch (Exception)
        {
            // The end's news reads it again, as it did before this reading came first.
        }

        life.WhoDone = life.Now;
        life.Lists = life.Now;
        Phase(life, SignInPhase.Lists);
        Placed(owner, account, joins, stream);
        life.Joined = life.Now;
        return code;
    }

    /// <summary>
    /// A sign-in's end beside who signed in (ACCT1): the lists named joined, and where the account runs said — each list and
    /// default that holds it, or that none does and so no start runs on it. A list that could not be written is said, and
    /// the sign-in stands.
    /// </summary>
    private void Placed(string owner, string account, IReadOnlyList<string?> joins, Action<string> stream)
    {
        IReadOnlyList<AccountPlace> places;
        try
        {
            places = Joined(_loop, owner, account, joins);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            stream($"the account was kept, and joined no list — {error.Message}");
            places = _loop.Harnesses.Settings.PlacesOf(owner, account);
        }

        var shown = AccountNames.Shown(_loop.Harnesses.Home, owner, account);
        stream(PlacesSaid(_loop.Harnesses.Settings, owner, shown, places));
    }

    /// <summary>
    /// Where an account runs, said at a sign-in's end. A scope that names no account takes all of Daoris's accounts in turn
    /// (OWNSPACE1a, D175), so where this machine names nothing an account no list holds is still taken, and "no start runs on
    /// it" is said only where the machine's own list or default names others (OWNSPACE1b-B).
    /// </summary>
    public static string PlacesSaid(HarnessSettings settings, string owner, string shown, IReadOnlyList<AccountPlace> places)
    {
        if (places.Count > 0) return $"`{shown}` runs work in {string.Join(", ", places.Select(PlaceWords))}.";
        var machine = settings.ResolveScope(owner, null);
        return machine.List.Count == 0 && machine.Begins is null
            ? $"no list and no default names `{shown}`, and none names another, so Daoris takes its accounts in turn."
            : $"no list and no default holds `{shown}`, so no start runs on it — put it in a workspace's list from its row.";
    }

    /// <summary>One scope an account runs in, as a person reads it: <c>this machine's list, as its default</c>.</summary>
    private static string PlaceWords(AccountPlace place)
    {
        var scope = place.Workspace is null ? "this machine's" : $"`{place.Workspace}`'s";
        return place is { List: true, Default: true } ? $"{scope} list, as its default" : $"{scope} {(place.List ? "list" : "default")}";
    }

    /// <summary>A name given at a sign-in's end, or null where none was asked or it could not be kept, which is said.</summary>
    private static string? Kept(Func<string?> rename, bool asked, Action<string> stream)
    {
        if (!asked) return null;
        try
        {
            return rename();
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            stream($"the account was kept, and not named — {error.Message}");
            return null;
        }
    }

    /// <summary>
    /// Each list a sign-in named, refused before anything starts where it cannot be joined (ACCT1): a workspace that takes
    /// this machine's list, naming none of its own, is refused naming both, so a refusal costs nothing.
    /// </summary>
    private static void RefuseJoins(DriverLoop loop, string owner, IReadOnlyList<string?> joins)
    {
        var settings = loop.Harnesses.Settings;
        foreach (var workspace in joins)
        {
            if (settings.JoinProblemOf(owner, workspace) is { } problem) throw new DriverException(problem.Sentence(owner));
        }
    }

    /// <summary>The account put into each list named, in order, written once; where it runs now (ACCT1). Agents' <c>profile-join</c> calls it too.</summary>
    internal static IReadOnlyList<AccountPlace> Joined(DriverLoop loop, string owner, string account, IReadOnlyList<string?> joins)
    {
        RefuseJoins(loop, owner, joins);
        var settings = joins.Aggregate(loop.Harnesses.Settings, (next, workspace) => next.WithJoined(owner, account, workspace));
        if (joins.Count > 0) settings.Save(loop.Harnesses.SettingsPath);
        return settings.PlacesOf(owner, account);
    }

    /// <summary>The action running under `harness:action`, or the refusal that names it.</summary>
    internal HarnessRun Running(IpcRequest request)
    {
        var harness = PayloadHelper.GetRequiredValue<string>(request.Payload, "harness");
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        return _actions.TryGetValue($"{harness}:{action}", out var run)
            ? run
            : throw Refusals.Because(
                Refusals.HarnessActionIdle,
                $"nothing is running for `{harness}` {action} — it finished, or was never started.",
                ("harness", harness), ("action", action));
    }

    /// <summary>Stop the action running under `harness:action`, its end then told to the log as cancelled (SIGNIN1).</summary>
    internal void Cancel(IpcRequest request)
    {
        var run = Running(request);
        var key = $"{PayloadHelper.GetRequiredValue<string>(request.Payload, "harness")}:{PayloadHelper.GetRequiredValue<string>(request.Payload, "action")}";
        if (_lives.TryGetValue(key, out var life)) life.Cancelled = true;
        run.Cancel();
    }

    /// <summary>What one `harness:action` said in its latest run, bounded as a session's console is (SIGNIN1).</summary>
    private sealed class Said
    {
        public readonly Queue<ConsoleLine> Lines = new();
        public long Sequence;
        public long Dropped;
    }

    /// <summary>
    /// A process action's life (SIGNIN1): when each of its phases began, in milliseconds from the press by the driver's own
    /// clock, how it ended, and a sign-in back in's reading of who, which its end's news says rather than reading again.
    /// </summary>
    private sealed class Life(string key, string harness, string action)
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private int _prompted;

        public string Key { get; } = key;

        public string Harness { get; } = harness;

        public string Action { get; } = action;

        public long Now => _clock.ElapsedMilliseconds;

        public long? Up { get; set; }

        public long? Exited { get; set; }

        public long? Who { get; set; }

        public long? WhoDone { get; set; }

        public long? Lists { get; set; }

        public long? Joined { get; set; }

        public bool Cancelled { get; set; }

        /// <summary>Stopped for the same sign-in by another way (CODEXLOGIN1); <see cref="Cancelled"/> with it.</summary>
        public bool Switched { get; set; }

        public bool Nobody { get; set; }

        public HarnessReport? Read { get; set; }

        /// <summary>
        /// A line the tool printed: the first that offers the person a link, read by its shape alone, is when it asked them
        /// (<c>harness.prompted</c>), once. The link is never written.
        /// </summary>
        public void Heard(string text, MachineLog? log)
        {
            if (Up is not { } up || Volatile.Read(ref _prompted) != 0) return;
            if (!text.Contains("https://", StringComparison.OrdinalIgnoreCase) && !text.Contains("http://", StringComparison.OrdinalIgnoreCase)) return;
            if (Interlocked.Exchange(ref _prompted, 1) != 0) return;
            log?.Info("harness.prompted", ("harness", Harness), ("action", Action), ("ms", Now - up));
        }
    }

    /// <summary>
    /// Install a version into the directory Daoris owns, and pin to it — <b>in that order</b>
    /// (TOOL2/D57).
    /// </summary>
    /// <remarks>
    /// 🔴 The pin is written only after the install succeeded. A pin naming a version that is not
    /// there refuses every spawn, so writing it first would turn a failed download into a machine
    /// that cannot start a session.
    /// </remarks>
    private async Task<int> PinAsync(
        string harness, HarnessToolchain toolchain, Action<string> stream, string version,
        CancellationToken ct, Action<HarnessRun> started)
    {
        var code = await HarnessActions.PinAsync(
            toolchain, _loop.Harnesses.Home, harness, version, stream, ct, started);

        if (code == 0) _loop.Harnesses.Settings.WithVersion(harness, version).Save(_loop.Harnesses.SettingsPath);
        return code;
    }

    /// <summary>
    /// Update a door (USE1a): a pinned one moves its pin to the newest release, written the way
    /// <see cref="PinAsync"/> writes one — only after that version is installed — and an unpinned one
    /// runs its own updater. The machine's pin, the one the roster shows.
    /// </summary>
    private Task<int> UpdateAsync(
        string harness, HarnessToolchain toolchain, IReadOnlyList<string>? command, Action<string> stream,
        CancellationToken ct, Action<HarnessRun> started) =>
        HarnessActions.UpdateAsync(
            toolchain, command, _loop.Harnesses.Home, harness,
            _loop.Harnesses.Settings.ResolveVersion(harness, null, null),
            version => _loop.Harnesses.Settings.WithVersion(harness, version).Save(_loop.Harnesses.SettingsPath),
            stream, ct, started);
}
