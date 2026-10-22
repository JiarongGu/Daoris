using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Why an order was refused (TOOL4e, D125 §3.1): the account it names that is not on this machine, or the one it names
/// twice. Both doors refuse it — the terminal's <c>daoris agent profile order</c> and the screen's — as
/// <c>profile default</c> refuses a name that does not exist.
/// </summary>
public sealed record RotationProblem(string Account, bool Twice)
{
    /// <summary>The refusal a person reads: which account, and what is there to name instead.</summary>
    public string Sentence(string agent, IReadOnlyCollection<string> accounts) => Twice
        ? $"`{Account}` is named twice — an order names each account once, in the order rotation tries them."
        : $"`{agent}` has no account `{Account}` on this machine, so an order cannot name it — accounts there: "
          + $"{(accounts.Count > 0 ? string.Join(", ", accounts) : "(none)")}.";
}

/// <summary>
/// The keys of accounts that are API keys (AGT3, D67 §1) — <c>keys.json</c> under the home, keyed by
/// agent and then account.
/// </summary>
/// <remarks>
/// <para><b>Beside the account, never inside it</b>: the account's directory is the tool's (D49 §4).
/// Plaintext at rest, as <c>remotes.json</c>'s deployment keys and the tools' own credential files
/// are; tracked by nothing; no HTTP surface (D47 §4). A person sees a key only as its
/// <see cref="Handle"/>. The CLI's <c>keys</c> helpers are the twin, over the same file.</para>
/// </remarks>
public static class HarnessKeys
{
    /// <summary>The key's last four characters — every Anthropic key begins the same way.</summary>
    public static string Handle(string key) => key.Length > 4 ? $"…{key[^4..]}" : "…";

    /// <summary>
    /// Make an account that is this key: a fresh id (ACCT2, <see cref="AccountNames.NewId"/>), its directory, and the key
    /// kept beside it. A blank key is refused before anything is made.
    /// </summary>
    public static string Add(string home, string harness, string key)
    {
        var trimmed = key?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            throw new DriverException("that is not an API key — it is blank, or has spaces in it.");
        }

        var account = HarnessSettings.NextAccount(home, harness);

        // The key first (KEEPALL1), as the CLI's `addKeyAccount`: a `keys.json` that does not read refuses here, before an account
        // directory exists. Written over, it lost every other account's key.
        var keys = Read(home);
        var read = Written(keys);
        if (!keys.TryGetValue(harness, out var held)) keys[harness] = held = new(StringComparer.Ordinal);
        held[account] = trimmed;
        Write(home, read, keys);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(home, harness, account));

        // Marked as a sign-in is (TOOL6g): a name a removed account had carries no word that it is signed out.
        ProbeLock.MarkSignedIn(HarnessSettings.ProfileHome(home, harness, account), DateTimeOffset.UtcNow);
        HarnessSettings.Placed(HarnessSettings.ResolvePath(home), harness, account);

        // A new key into an account ends a cool-off its name still carried (TOOL4d, D125 §2.3): whatever was spent, it
        // was not this key. Never the reason a key is not kept.
        try
        {
            AccountCooling.End(home, harness, account, DateTimeOffset.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The key is kept; the cool-off ends at its reset instead.
        }

        return account;
    }

    /// <summary>
    /// Write a new key into an account that is already a key (KEYREPLACE1a): the repair for a key its provider refused. The
    /// account keeps its id, so its name, lists, defaults and order stand (ACCT2); its last reading and its cool-off, what
    /// marked it refused on disk, are cleared and it is marked signed in as <see cref="Add"/> does. The roster's in-memory
    /// refusal is the caller's (<c>Harnesses.Removed</c> and <c>LookedAgain</c>). The CLI's <c>replaceKeyAccount</c> is the twin.
    /// An account not on this machine, one that is not a key, and a blank or spaced key are refused before anything is written.
    /// </summary>
    public static string Replace(string home, string harness, string account, string key)
    {
        var named = HarnessSettings.Profiles(home, harness).FirstOrDefault(each => string.Equals(each, account, StringComparison.Ordinal))
            ?? throw new DriverException(
                $"`{harness}` has no account `{account}` on this machine — accounts that exist: "
                + (HarnessSettings.Profiles(home, harness) is { Count: > 0 } accounts ? string.Join(", ", accounts) : "(none)"));
        var keys = Read(home);
        if (!keys.TryGetValue(harness, out var held) || !held.ContainsKey(named))
        {
            throw new DriverException(
                $"`{named}` is not an API key account — it is a sign-in, so sign in again with `daoris agent login {harness}`.");
        }

        var trimmed = key?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            throw new DriverException("that is not an API key — it is blank, or has spaces in it.");
        }

        var read = Written(keys);
        held[named] = trimmed;
        Write(home, read, keys);

        // What marked it refused is cleared, none the reason the key is not kept.
        try
        {
            AccountReads.Forget(home, harness, named);
            AccountCooling.End(home, harness, named, DateTimeOffset.UtcNow);
            ProbeLock.MarkSignedIn(HarnessSettings.ProfileHome(home, harness, named), DateTimeOffset.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The key is kept; the next press reads the account again.
        }

        return named;
    }

    /// <summary>The key an account is, or null for a sign-in.</summary>
    public static string? Of(string home, string harness, string profile) =>
        Read(home).TryGetValue(harness, out var held) && held.TryGetValue(profile, out var key) ? key : null;

    /// <summary>Forget an account's key. Nothing to forget is an answer.</summary>
    public static void Remove(string home, string harness, string profile)
    {
        var keys = Read(home);
        var read = Written(keys);
        if (!keys.TryGetValue(harness, out var held) || !held.Remove(profile)) return;
        if (held.Count == 0) keys.Remove(harness);
        Write(home, read, keys);
    }

    private static Dictionary<string, Dictionary<string, string>> Read(string home)
    {
        var keys = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var path = DaorisHome.Entry(home, HomeEntry.Keys);
        if (!File.Exists(path)) return keys;

        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return keys;
            foreach (var agent in document.RootElement.EnumerateObject())
            {
                if (agent.Value.ValueKind != JsonValueKind.Object) continue;
                var held = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in agent.Value.EnumerateObject())
                {
                    // A value that is not text is no key, and is kept as written (KEEPALL1): read as one, it threw.
                    if (entry.Value.ValueKind == JsonValueKind.String && entry.Value.GetString() is { Length: > 0 } key) held[entry.Name] = key;
                }

                keys[agent.Name] = held;
            }
        }
        catch (JsonException)
        {
            // An unreadable file holds no key Daoris can hand anyone; the account then asks for one.
        }

        return keys;
    }

    /// <summary>
    /// The edit from <paramref name="read"/>, the keys as read and written, made onto the file as it stands (KEEPALL1,
    /// <see cref="JsonKeeping.Save"/>): a key this build has no field for stays where it is, as written, at the top and inside an agent's
    /// keys, and a file that does not read is refused, never written over. Rebuilt from the keys it read, an edit over a file that did
    /// not read wrote every other account's key away. The CLI's <c>writeKeys</c> does the same.
    /// </summary>
    private static void Write(string home, string read, Dictionary<string, Dictionary<string, string>> keys)
    {
        var path = DaorisHome.Entry(home, HomeEntry.Keys);
        JsonKeeping.Save(path, File.Exists(path), read, Written(keys));
    }

    /// <summary>The keys' own shape: agent then account, each sorted.</summary>
    private static string Written(Dictionary<string, Dictionary<string, string>> keys) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        foreach (var (agent, held) in keys.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(agent);
            foreach (var (account, key) in held.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                writer.WriteString(account, key);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    });
}

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
        HarnessToolchain toolchain,
        IReadOnlyList<string>? command,
        HarnessSettings settings,
        string home,
        CancellationToken ct = default,
        Func<string?, bool>? busy = null,
        HarnessReport? prior = null,
        Func<string, bool>? asks = null,
        Func<DateTimeOffset>? clock = null,
        Action<string?, LoginRead, DateTimeOffset>? answered = null)
    {
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
                toolchain.ProfileVariable,
                settings.FirstOf(toolchain.Owner(adapter)),
                []);
        }

        var version = toolchain.ProbeByPresence
            ? (Ran: resolved.Count > 0 && CommandPresence.Resolvable(resolved[0]), Output: "", Problem: (string?)null)
            : await AskAsync(resolved, toolchain.VersionArguments, profileHome: null, toolchain, ct, isManaged)
                .ConfigureAwait(false);

        var present = version.Ran;
        var profiles = new List<ProfileReport>();

        // A door lists its OWNER's accounts (AGT7): the same directories, so one tool shows one list.
        var owner = toolchain.Owner(adapter);
        foreach (var name in HarnessSettings.Profiles(home, owner))
        {
            var profileHome = HarnessSettings.ProfileHome(home, owner, name);
            var held = HarnessKeys.Of(home, owner, name);
            // An account that is a key is asked WITH its key (AGT3), as a session would run it, so
            // the roster says what the tool says — and names it by the key's handle, never the key.
            var key = toolchain.KeyVariable is { Length: > 0 } variable && held is not null
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
            toolchain.ProfileVariable,
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
        string adapter, HarnessToolchain toolchain, IReadOnlyList<string>? command,
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
        string adapter, HarnessToolchain toolchain, IReadOnlyList<string>? command,
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
        string adapter, HarnessToolchain toolchain, IReadOnlyList<string>? command, HarnessSettings settings, string home,
        string? workspace = null)
    {
        if (command is { Count: > 0 }) return new HarnessCommand(adapter, command, null, null);
        if (settings.ResolveVersion(adapter, workspace, chosen: null) is not { } pinned)
        {
            return new HarnessCommand(adapter, toolchain.Binary, null, null);
        }

        return HarnessSettings.ManagedBinary(home, adapter, pinned, toolchain.Binary) is { } managed
            ? new HarnessCommand(adapter, [managed, .. toolchain.Binary.Skip(1)], managed, pinned)
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
        IReadOnlyList<string> resolved, HarnessToolchain toolchain, string? profileHome, bool managed,
        CancellationToken ct, IReadOnlyDictionary<string, string>? account = null, string? lockPath = null,
        Func<bool>? busy = null, LoginRead kept = default, TimeSpan? patience = null)
    {
        if (toolchain.LoginCheck is not { } question) return new LoginAnswer(LoginState.Unknown, null, false);

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
        HarnessToolchain toolchain,
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
        Apply(info, toolchain, profileHome, binary: managed ? resolved[0] : null, account: account);

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

    /// <summary>
    /// Put a spawn's account and its binary onto a process, through the seams the harness itself
    /// provides. <b>Creating the profile directory is part of selecting it</b>: at least one
    /// supported harness refuses to start when its home variable names a path that does not exist,
    /// and Daoris owns that location by design.
    /// </summary>
    /// <param name="binary">
    /// The managed executable to run instead of whatever the adapter resolved (TOOL2/D57), or null
    /// for <c>PATH</c> — which is the unpinned case and therefore the usual one.
    /// </param>
    /// <remarks>
    /// 🔴 <b>Both doors go through here</b>, which is why the pin is applied in this one place rather
    /// than inside each adapter's <c>Prepare</c>: an adapter that forgot it would spawn the wrong
    /// binary and record the pinned version beside it.
    /// </remarks>
    internal static void Apply(
        ProcessStartInfo info, HarnessToolchain toolchain, string? profileHome, string? binary = null,
        string? claudeExecutable = null, IReadOnlyDictionary<string, string>? account = null)
    {
        // What the account itself carries — an API key in the tool's own variable (AGT3). Set here,
        // the one line both doors take, so no spawn of a key account can go out without its key.
        foreach (var (name, value) in account ?? new Dictionary<string, string>())
        {
            info.Environment[name] = value;
        }

        // The arguments the adapter built stay exactly as they are: same tool, different location.
        if (binary is { Length: > 0 })
        {
            info.FileName = binary;

            // 🔴 And the version it was pinned at (AGT2): a pinned Claude Code reported its own
            // auto-updates enabled, and would have moved itself under the pin.
            foreach (var (name, value) in toolchain.PinnedEnvironment ?? new Dictionary<string, string>())
            {
                info.Environment[name] = value;
            }
        }

        // 🔴 USE1f: what Windows can START, by the resolver installers already use. A bare name from
        // PATH starts only as an `.exe`, so an agent npm installed globally (a `.cmd` shim, with a
        // POSIX script beside it) probed absent and every start on it was held, though the CLI found
        // it. Here because the probe and both doors' spawns all come through here, as the pin does;
        // and a shim is held to the same rule about its arguments that a pinned `.cmd` now is.
        info.FileName = HarnessActions.WindowsShim(
            info.FileName, info.ArgumentList,
            info.Environment.TryGetValue("PATH", out var path) ? path : null);

        // The ACP adapter runs the Agent SDK, which finds its CLI through its own seam (ACP2, §1a).
        // Applied here for the reason everything else here is: one line, both doors, no adapter that
        // can forget it. Null leaves it unset, so the SDK looks where it always did.
        ClaudeAcp.PointAtClaude(info, claudeExecutable);

        if (profileHome is null) return;

        if (toolchain.ProfileVariable is not { Length: > 0 } variable)
        {
            throw new DriverException(
                "that agent has no configuration-home variable, so Daoris cannot run it as a named "
                + "account. Its accounts are managed with its own tooling.");
        }

        // 🔴 Created, not assumed to exist: `codex-acp` exits 1 before `initialize` completes when
        // `CODEX_HOME` names a path that is not there, while the Claude adapter creates its own. The
        // toolchain's `ProfileMustExist` names which harnesses depend on this line (ACP3).
        Directory.CreateDirectory(profileHome);
        info.Environment[variable] = profileHome;
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

/// <param name="Refusal">Why this spawn must not happen — null when it may.</param>
/// <param name="Profile">The profile it runs as, or null for the harness's own configuration home.</param>
/// <param name="ProfileHome">Where that profile lives. Machine-local: it goes into no record and over no wire.</param>
/// <param name="Version">The harness version observed, for the record.</param>
/// <param name="Binary">
/// The managed executable this spawn runs (TOOL2/D57), or <b>null for whatever is on <c>PATH</c></b>
/// — which is the unpinned case and therefore the usual one. Machine-local like the profile home:
/// it is a path, so it goes into no record and over no wire.
/// </param>
/// <param name="ClaudeExecutable">
/// The managed <c>claude</c> the ACP adapter's Agent SDK should run (ACP2, §1a), or null to let it
/// find its own. It reads <b>the pipe door's pin</b> deliberately: the CLI and the account belong to
/// <c>claude-code</c>, and the ACP adapter is a separate package that merely runs it — so a person
/// who pinned <c>claude</c> gets that <c>claude</c> over either door, which is the point of pinning.
/// </param>
/// <param name="Environment">
/// What the account itself puts on the spawn — for an account that is an API key (AGT3), the key in
/// the tool's own variable. Machine-local like the profile home: it goes into no record and over no
/// wire. Null for a sign-in or the tool's own home.
/// </param>
public sealed record HarnessSelection(
    string? Refusal, string? Profile = null, string? ProfileHome = null, string? Version = null,
    string? Binary = null, string? ClaudeExecutable = null,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    public bool Allowed => Refusal is null;

    /// <summary>
    /// The account's cool-off when that is why this spawn must not happen (TOOL4d, D125 §4), and null otherwise: a
    /// hold that waits for a time, where every other refusal waits for a person.
    /// </summary>
    /// <remarks>
    /// Held over an order (TOOL4f), it is the cool-off of the account that is ready first, which is when the start may
    /// run again; for a pick refused, the pick's own.
    /// </remarks>
    public CoolingEntry? Cooling { get; init; }

    /// <summary>
    /// The account the start's scope begins at and why the start runs elsewhere (TOOL4f, D125 §3.3; TOOL6b, D130 §13 as
    /// §16 amends it); null when it runs where its scope begins.
    /// </summary>
    public RotatedStart? Rotated { get; init; }

    /// <summary>
    /// The step of the goal's walk that chose the account, where a list offered a choice (TOOL6b, D130 §16.4): what the
    /// start's record says first. Null under <c>use: order</c>, for a pick, and where the scope offered one account.
    /// </summary>
    public AccountChoice? Choice { get; init; }

    /// <summary>
    /// The steps of the walk that chose the account, by code (UX7d-1, D152's UX7d-1 note): the first, and the rest's where the
    /// first account was passed for itself, each its English and its reason with its own values. <see cref="Choice"/>'s
    /// clause is all of them joined and <see cref="Rotated"/>'s the first. Null for a pick and a scope with no list.
    /// Machine-local: it names accounts.
    /// </summary>
    public IReadOnlyList<StepSaid>? Steps { get; init; }

    /// <summary>
    /// What each account of the scope's list last said about its windows, as the start's first line says it (TOOL6c, D130
    /// §16.4), with its lines by code (UX7d-1); null where none has said anything, and for a pick or a scope with no list.
    /// Machine-local: it names accounts.
    /// </summary>
    public Noted? AccountsSaid { get; init; }

    /// <summary><see cref="AccountsSaid"/>'s English: the sentence the start's first line ends with.</summary>
    public string? SaidLine => AccountsSaid?.Note;

    /// <summary>
    /// Why the one account a refused start asked for was not ready (MSG1g): cooling, refused, or signed out as the agent said;
    /// null where the start was refused for anything else (no agent, a pin nobody installed), or ran.
    /// </summary>
    public AccountReadiness? NotReady { get; init; }

    /// <summary>
    /// The accounts a held start passed because they are not signed in (TOOL6g), whether it then waits on a cooling one or on
    /// none; null where it passed none, or ran. What the hold's sentence names with each sign-in, as facts for the page and
    /// the log.
    /// </summary>
    public SignedOutAccounts? SignedOut { get; init; }

    /// <summary>
    /// Why the agent itself cannot run, where that is why this start must not happen (XAGENT1b, the second-agent design §3.3):
    /// nothing of it installed, or a pin with nothing installed at it (D57). Null where the agent may run, or the start was
    /// held for an account (<see cref="NotReady"/>). A fact beside the sentence, so a reader judges no words.
    /// </summary>
    public AgentAbsence? Absent { get; init; }

    /// <summary>
    /// <see cref="Refusal"/> by code (ROSTERREFUSE1), its English the same to the byte: the kind of refusal, and the account by its
    /// owner and id where it names one, never by the name the person gave it. Null where the refusal is not one a held note can
    /// carry by code (a wait over a list, a pick refused, a provider's refusal, an agent with its own tooling, a refusal with
    /// more said after it); the note then carries its words. A fact beside the sentence, so a reader judges no words.
    /// </summary>
    public Noted? RefusalNote { get; init; }
}

/// <summary>Why an agent itself cannot run (XAGENT1b): what <see cref="HarnessSelection.Absent"/> says.</summary>
public enum AgentAbsence
{
    /// <summary>Nothing of it is installed on this machine: no binary to spawn.</summary>
    NotInstalled,

    /// <summary>Pinned to a version nothing is installed at (D57): refused, never run from <c>PATH</c> instead.</summary>
    PinNotInstalled,
}

/// <summary>Which rung of the resolution answered (D49 §4, TOOL2): the order a start asks in.</summary>
public enum ChoiceFrom
{
    /// <summary>The person picked it for this one session.</summary>
    Picked,

    /// <summary>This workspace's default.</summary>
    Workspace,

    /// <summary>This machine's default.</summary>
    Machine,

    /// <summary>Nothing set: the agent's own sign-in for an account, <c>PATH</c> for a version.</summary>
    Unset,
}

/// <summary>
/// What a start in one workspace would run on, and where each part came from (MAP1b) — the wiring
/// panel's answer, never a second judgement. Carries names and versions only: no home, no binary path
/// and no key, so it is safe on any surface the roster already reaches.
/// </summary>
/// <param name="Adapter">The adapter a start spawns — `driver.json`'s choice.</param>
/// <param name="Owner">Whose accounts it runs as (AGT7): the adapter itself, or the tool a door opens onto.</param>
/// <param name="Profile">The account, by its directory name; null is the agent's own sign-in.</param>
/// <param name="ProfileFrom">Which rung chose it.</param>
/// <param name="Version">The version it would run: the probed one, else the pin that was asked for.</param>
/// <param name="VersionFrom">Which rung pinned it; unset is <c>PATH</c>.</param>
/// <param name="Commanded">`driver.json` names the command outright, which outranks any pin.</param>
/// <param name="Refusal">Why a start would be held, in the driver's own words; null when it would run.</param>
public sealed record StartWiring(
    string Adapter, string Owner, string? Profile, ChoiceFrom ProfileFrom,
    string? Version, ChoiceFrom VersionFrom, bool Commanded, string? Refusal)
{
    /// <summary>
    /// The account <see cref="ProfileFrom"/>'s rung named, when the start runs on another account of the person's order
    /// because that one is not ready (TOOL4f, D125 §3.3): <see cref="Profile"/> is then the account the start takes, so
    /// the panel never shows an account a start would not take. Null when nothing rotated.
    /// </summary>
    public string? RotatedFrom { get; init; }
}
