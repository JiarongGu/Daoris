using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Detection (D49 §4): locate a harness, ask its version, and ask each profile whether it is logged
/// in. <b>Free and read-only</b> — it runs the tool's own reporting commands and writes nothing.
/// </summary>
/// <remarks>
/// Acting is the person's click: install, update and login spawn the harness's own mechanism and are
/// never run by a gate, never mid-session, and never unasked. A harness changing under a running loop
/// is the moving-target problem `reaching-in` documents, one layer down.
/// </remarks>
public static class HarnessProbe
{
    /// <summary>How long a reporting command may take before it counts as no answer.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    /// <summary>Probe one harness: present, version, and every profile's login state.</summary>
    /// <remarks>
    /// <para>🔴 <b>One status question per account at a time</b> (TOOL6g): each is asked under that account's
    /// <see cref="ProbeLock"/>, so two probes, two adapters onto one account, or the CLI's listing never ask one account at
    /// once. The agent may refresh an expired token to answer, and two refreshes with one single-use refresh token signed
    /// every account on the install out.</para>
    /// <para><b>Never an account a session of Daoris's runs on</b> (<paramref name="busy"/>): that session's own process
    /// refreshes its token, so the probe keeps out, and says what <paramref name="prior"/> said of it, or unknown.</para>
    /// <para>🔴 <b>Never the tool's own sign-in</b> (POOL1a3, D197 point 3; Fable's F2): it is the person's own, which their
    /// terminal and their other sessions run on, and the agent may refresh their token to answer. Every question runs in an
    /// account's home under the home; <paramref name="prior"/>'s word on the own sign-in stands, or unknown.</para>
    /// </remarks>
    /// <param name="busy">Whether a session of Daoris's runs on an account now, by its name.</param>
    /// <param name="prior">The last report of this harness, whose word on an account not asked, and on the own sign-in, stands.</param>
    /// <param name="asks">
    /// Which accounts to ask, by name; null asks every one (ROSTER1). One not asked keeps <paramref name="prior"/>'s word and
    /// when it was read, so the binary can be asked with no account asked at all.
    /// </param>
    /// <param name="clock">When an answer came, for <see cref="ProfileReport.Read"/>; the system's, unless a roster's.</param>
    /// <param name="answered">
    /// Told each question that was asked, the account's name, with what it said: the roster keeps
    /// the state under the home and who, the organisation and the plan in memory (D66 §3, ACCOUNTID1).
    /// </param>
    public static async Task<HarnessReport> ProbeAsync(
        string adapter,
        AccountAgent toolchain,
        IReadOnlyList<string>? command,
        HarnessSettings settings,
        string home,
        CancellationToken ct = default,
        Func<string?, bool>? busy = null,
        HarnessReport? prior = null,
        Func<string, bool>? asks = null,
        Func<DateTimeOffset>? clock = null,
        Action<string?, LoginRead, DateTimeOffset>? answered = null,
        string? accountOf = null)
    {
        // Whose accounts it lists (AGT7): the key the registry answers (<c>ProviderRegistry.OwnerOfAgent</c>), the agent itself where none is given.
        var owner = accountOf ?? adapter;
        var now = clock ?? (() => DateTimeOffset.UtcNow);
        // 🔴 Rule 4, applied HERE and not only where a session spawns: the explicit command, then the
        // managed pin, then PATH. A presence answer computed from PATH while a pin is set is an answer
        // about a different program — measured both ways: the selector vetoed a working pin as "not
        // installed", and the CLI twin reported the machine's own binary as the pinned one.
        var run = CommandOf(adapter, toolchain, command, settings, home);
        var resolved = run.Run;
        var isManaged = run.Managed is not null;
        if (run.Missing)
        {
            // Pinned with nothing installed at it: absent, naming the version. Never a fall back
            // to PATH — running a different version than the one asked for and reporting success
            // is the failure the pin exists to prevent.
            return new HarnessReport(
                adapter, false, null,
                $"pinned to {run.Pinned} on this machine, and nothing is installed at that version — "
                + $"`daoris agent pin {adapter} {run.Pinned}` installs it, and "
                + $"`daoris agent unpin {adapter}` goes back to PATH",
                toolchain.Home.Variable,
                settings.FirstOf(owner),
                []);
        }

        var version = toolchain.Program.ProbeByPresence
            ? (Ran: resolved.Count > 0 && CommandPresence.Resolvable(resolved[0]), Output: "", Problem: (string?)null)
            : await AskAsync(resolved, toolchain.Program.VersionArguments, profileHome: null, toolchain, ct, isManaged)
                .ConfigureAwait(false);

        var present = version.Ran;
        var profiles = new List<ProfileReport>();

        // A door lists its OWNER's accounts (AGT7): the same directories, so one tool shows one list.
        foreach (var name in HarnessSettings.Profiles(home, owner))
        {
            var profileHome = HarnessSettings.ProfileHome(home, owner, name);
            var held = HarnessKeys.Of(home, owner, name);
            // An account that is a key is asked WITH its key (AGT3), as a session would run it, so
            // the roster says what the tool says — and names it by the key's handle, never the key.
            var key = toolchain.Accounts.KeyVariable is { Length: > 0 } variable && held is not null
                ? new Dictionary<string, string> { [variable] = held }
                : null;
            var said = prior?.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            var kept = Kept(said);
            var answer = !present ? new LoginAnswer(LoginState.Unknown, null, false)
                : asks is not null && !asks(name) ? LoginAnswer.Of(kept, asked: false)
                : await LoginAsync(
                    resolved, toolchain, profileHome, isManaged, ct, key, ProbeLock.PathOf(home, owner, name),
                    busy is null ? null : () => busy(name), kept).ConfigureAwait(false);
            profiles.Add(new ProfileReport(
                name, profileHome, answer.Login, answer.Account, held is null ? null : HarnessKeys.Handle(held),
                Moment(answer, kept, said?.Read, name), AccountNames.NameOf(home, owner, name), answer.Org, answer.Plan));
        }

        // 🔴 The tool's own home is never asked (POOL1a3, D197 point 3): its kept word stands. Asked with the seam unset, the
        // tool answered for the person's own sign-in, and might refresh their token beside their terminal's to do it.
        var keptOwn = new LoginRead(prior?.OwnLogin ?? LoginState.Unknown, prior?.OwnAccount, prior?.OwnOrg, prior?.OwnPlan);
        var ownAnswer = !present ? new LoginAnswer(LoginState.Unknown, null, false) : LoginAnswer.Of(keptOwn, asked: false);

        return new HarnessReport(
            adapter,
            present,
            present ? FirstLine(version.Output) : null,
            present ? null : version.Problem,
            toolchain.Home.Variable,
            // The order's first, where the machine's default was (POOL1a2, the account-pool design §2.2).
            settings.FirstOf(owner),
            profiles,
            ownAnswer.Login,
            ownAnswer.Account,
            Moment(ownAnswer, keptOwn, prior?.OwnRead, null),
            ownAnswer.Org,
            ownAnswer.Plan);

        // When an answer was read: now where the question was asked, and told; a word kept from before keeps its moment.
        DateTimeOffset? Moment(LoginAnswer answer, LoginRead kept, DateTimeOffset? before, string? account)
        {
            if (!answer.Asked) return answer.Said == kept ? before : null;
            var at = now();
            answered?.Invoke(account, answer.Said, at);
            return at;
        }
    }

    /// <summary>What the last report said of an account a session now runs on: its word stands, or unknown where it said none.</summary>
    private static LoginRead Kept(ProfileReport? said) =>
        said is null ? new LoginRead(LoginState.Unknown, null) : new LoginRead(said.Login, said.Account, said.Org, said.Plan);

    /// <summary>
    /// One status question's outcome (ROSTER1): what the agent said — on a yes who, with the organisation and the plan beside
    /// them (ACCOUNTID1) — and whether it was asked at all. One never asked (no login question, another holder kept the
    /// account's lock, a session of Daoris's runs on it) is no reading, and writes nothing.
    /// </summary>
    internal readonly record struct LoginAnswer(LoginState Login, string? Account, bool Asked, string? Org = null, string? Plan = null)
    {
        /// <summary>What it said, without whether it was asked.</summary>
        public LoginRead Said => new(Login, Account, Org, Plan);

        /// <summary>
        /// Not asked because another holder kept the account's lock past the asker's patience (PARALLEL1c): a question may be in
        /// flight on it, so a start's walk leaves the account unknown for that walk, never runs beside it.
        /// </summary>
        public bool Locked { get; init; }

        /// <summary>An answer saying what <paramref name="said"/> says.</summary>
        public static LoginAnswer Of(LoginRead said, bool asked) => new(said.Login, said.Account, asked, said.Org, said.Plan);
    }

    /// <summary>
    /// What the harness says about logging in to ONE home — a profile's, or its own when
    /// <paramref name="profileHome"/> is null — asked of the binary a spawn would run. What a sign-in
    /// asks when it ends (D66 §3), without probing every other account on the machine to learn it.
    /// </summary>
    /// <param name="lockPath">The account's <see cref="ProbeLock"/>, asked under it (TOOL6g); null asks with none.</param>
    /// <param name="key">An account that is a key, asked with it as a session runs it (AGT3); null for a sign-in.</param>
    public static async Task<(LoginState Login, string? Account)> AskLoginAsync(
        string adapter, AccountAgent toolchain, IReadOnlyList<string>? command,
        HarnessSettings settings, string home, string? profileHome, CancellationToken ct = default, string? lockPath = null,
        IReadOnlyDictionary<string, string>? key = null)
    {
        var answer = await AskAccountAsync(adapter, toolchain, command, settings, home, profileHome, ct, lockPath, key).ConfigureAwait(false);
        return (answer.Login, answer.Account);
    }

    /// <summary>
    /// <see cref="AskLoginAsync"/>, saying whether the question was asked at all (ROSTER1): a pin with nothing installed at
    /// it, an agent with no login question, a lock another holder kept, or an account a session of Daoris's runs on
    /// (<paramref name="busy"/>, whose answer is then <paramref name="kept"/>) is no reading.
    /// </summary>
    /// <param name="patience">How long the lock is waited for (<see cref="ProbeLock.Patience"/> where null).</param>
    internal static async Task<LoginAnswer> AskAccountAsync(
        string adapter, AccountAgent toolchain, IReadOnlyList<string>? command,
        HarnessSettings settings, string home, string? profileHome, CancellationToken ct, string? lockPath,
        IReadOnlyDictionary<string, string>? key, Func<bool>? busy = null, LoginRead kept = default, TimeSpan? patience = null)
    {
        // The pin, as the probe resolves it; nothing installed at it is nobody to ask.
        var run = CommandOf(adapter, toolchain, command, settings, home);
        return run.Missing
            ? new LoginAnswer(LoginState.Unknown, null, false)
            : await LoginAsync(run.Run, toolchain, profileHome, managed: run.Managed is not null, ct, key, lockPath, busy, kept, patience)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// What one of a harness's own commands runs as (D57 rule 4, the order every spawn takes): the command <c>driver.json</c>
    /// names for <paramref name="adapter"/>, then this machine's pin of it (<paramref name="workspace"/>'s where it pins its own),
    /// then its declared binary on <c>PATH</c>. A pin with nothing installed at it is <see cref="HarnessCommand.Missing"/>: nobody
    /// to run, never <c>PATH</c>'s.
    /// </summary>
    /// <remarks>
    /// The status question and a sign-in both take it (CODEXACCT1), so a sign-in runs the binary its end's question asks:
    /// the screen's ran <c>PATH</c>'s, and a machine whose only <c>codex</c> is the pin could not sign one in.
    /// </remarks>
    public static HarnessCommand CommandOf(
        string adapter, AccountAgent toolchain, IReadOnlyList<string>? command, HarnessSettings settings, string home,
        string? workspace = null)
    {
        if (command is { Count: > 0 }) return new HarnessCommand(adapter, command, null, null);
        if (settings.ResolveVersion(adapter, workspace, chosen: null) is not { } pinned)
        {
            return new HarnessCommand(adapter, toolchain.Program.Binary, null, null);
        }

        return HarnessSettings.ManagedBinary(home, adapter, pinned, toolchain.Program.Binary) is { } managed
            ? new HarnessCommand(adapter, [managed, .. toolchain.Program.Binary.Skip(1)], managed, pinned)
            : new HarnessCommand(adapter, [], null, pinned);
    }

    /// <summary>
    /// What the harness says about logging in here: the boolean, and — when the toolchain asks and the
    /// answer is yes — <b>who</b> (D66 §3), with the organisation and the plan a real harness volunteers beside them
    /// (ACCOUNTID1): what tells two sign-ins of one email apart. Read fresh and shown, and never written or logged.
    /// </summary>
    /// <param name="lockPath">
    /// The home's <see cref="ProbeLock"/> (TOOL6g): asked only while holding it, and not at all, unknown, when another holder
    /// keeps it past its patience. Null asks with no lock.
    /// </param>
    /// <param name="busy">
    /// Whether a session of Daoris's runs on the home, asked once the lock is held, so a start that took the lock to count
    /// itself is never asked about in between (TOOL6g); <paramref name="kept"/> is then the answer, and nothing runs.
    /// </param>
    /// <param name="patience">How long the lock is waited for (<see cref="ProbeLock.Patience"/> where null).</param>
    /// <returns>The answer, and whether the question was asked: none asked is no reading (ROSTER1).</returns>
    private static async Task<LoginAnswer> LoginAsync(
        IReadOnlyList<string> resolved, AccountAgent toolchain, string? profileHome, bool managed,
        CancellationToken ct, IReadOnlyDictionary<string, string>? account = null, string? lockPath = null,
        Func<bool>? busy = null, LoginRead kept = default, TimeSpan? patience = null)
    {
        if (toolchain.Accounts.Login is not { } question) return new LoginAnswer(LoginState.Unknown, null, false);

        // One status question per account at a time, in this process and across them (TOOL6g).
        await using var held = lockPath is null ? null : await ProbeLock.TakeAsync(lockPath, patience, ct).ConfigureAwait(false);
        if (lockPath is not null && held is null) return new LoginAnswer(LoginState.Unknown, null, false) { Locked = true };
        if (busy?.Invoke() == true) return LoginAnswer.Of(kept, asked: false);

        var answer = await AskAsync(resolved, question.Arguments, profileHome, toolchain, ct, managed, account)
            .ConfigureAwait(false);
        if (!answer.Ran) return new LoginAnswer(LoginState.Unknown, null, true);

        // Read with the "logged in" pattern first, because it is the specific one; and neither matching leaves it unknown
        // rather than out, which is what keeps a reworded status line from refusing a spawn that would have worked.
        return LoginAnswer.Of(question.Read(answer.Output), asked: true);
    }

    /// <summary>The runtime's error code for a binary that is not there — ERROR_FILE_NOT_FOUND and ENOENT alike.</summary>
    private const int FileNotFound = 2;

    /// <summary>
    /// Run one of the harness's reporting commands and collect what it said. <b>Both supported
    /// harnesses exit 0 whether or not they are logged in</b>, so the exit code is deliberately not
    /// consulted — the output is the answer, and "it ran at all" is the only thing the code is asked.
    /// </summary>
    internal static async Task<(bool Ran, string Output, string? Problem)> AskAsync(
        IReadOnlyList<string> resolved,
        IReadOnlyList<string> arguments,
        string? profileHome,
        AccountAgent toolchain,
        CancellationToken ct,
        bool managed = false,
        IReadOnlyDictionary<string, string>? account = null)
    {
        if (resolved.Count == 0) return (false, "", "no command to run");

        var info = new ProcessStartInfo
        {
            FileName = resolved[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Read as the tools write, not in the console's code page, which garbled a plugin's words on
            // a Chinese-locale machine (PLUG8); a sign-in status can carry a name.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true, // a probe from a window must not open a console (Adapters.Shell)
        };
        foreach (var part in resolved.Skip(1)) info.ArgumentList.Add(part);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        // A probe is a child like a session (TOOLS5): asked on the PATH a session would be started on.
        Tools.Hand(info);
        // A pinned binary is asked the way a session runs it (AGT2), so asking is not when it moves.
        Shell.Equip(info, toolchain, profileHome, binary: managed ? resolved[0] : null, account: account);

        var run = await ChildProcess.RunAsync(info, Patience, ct, closeInput: false).ConfigureAwait(false);
        if (!run.Started)
        {
            return run.Error switch
            {
                null => (false, "", $"`{resolved[0]}` did not start"),
                // 🔴 The sentence, and only the sentence. The runtime's own message for this case — "An
                // error occurred trying to start process '…' with working directory '<cwd>'. The system
                // cannot find the file specified." — restates the fact and adds a machine path, and the
                // deployed application's roster showed both. Any OTHER failure keeps the runtime's words:
                // a binary that exists and will not start is news the sentence alone does not carry.
                System.ComponentModel.Win32Exception { NativeErrorCode: FileNotFound } => (false, "", $"`{resolved[0]}` is not on this machine's PATH"),
                var error => (false, "", $"`{resolved[0]}` is not on this machine's PATH — {error.Message}"),
            };
        }

        if (run.TimedOut) return (false, "", $"`{resolved[0]}` did not answer within {Patience.TotalSeconds:0}s");

        // Both streams: a harness that reports its version on stderr is not an absent harness.
        return (true, $"{run.Stdout}\n{run.Stderr}", null);
    }

    private static string? FirstLine(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            if (line.Trim() is { Length: > 0 } text) return text;
        }

        return null;
    }
}
