using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A harness action while it runs (D49 §4): the one thing a screen may send it, and the way it is
/// stopped. A login prints a prompt and waits — <i>paste the code</i> — and a process nobody can
/// answer or stop is a page with every control disabled until the window closes.
/// </summary>
public sealed class HarnessRun
{
    private readonly Process? _process;
    private readonly CancellationTokenSource? _download;

    internal HarnessRun(Process process) => _process = process;

    /// <summary>
    /// A pin from a maker's channel (AGT2b): no process at all — Daoris downloads it — so there is
    /// nothing to answer, and stopping it is cancelling the download.
    /// </summary>
    internal HarnessRun(CancellationTokenSource download) => _download = download;

    /// <summary>Answer the harness's prompt — one line, as a terminal would send it.</summary>
    public void Send(string line)
    {
        if (_process is null) return; // A download asks nothing.
        try
        {
            _process.StandardInput.WriteLine(line);
            _process.StandardInput.Flush();
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
        {
            // Gone already: the answer arrived after the process stopped needing one.
        }
    }

    /// <summary>End it, and everything it started. The browser page it opened is the browser's and stays.</summary>
    public void Cancel()
    {
        try
        {
            _download?.Cancel();
            if (_process is { HasExited: false }) _process.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or ObjectDisposedException)
        {
            // Ended between the look and the kill.
        }
    }
}

/// <summary>
/// The person's explicit actions on a harness (D49 §4): install it, update it, log a profile in —
/// each by <b>that harness's own official mechanism</b>, spawned as a process like any other and
/// streamed line by line to whoever asked.
/// </summary>
/// <remarks>
/// <para><b>Never automatic, never mid-session, never unasked.</b> A gate's tool must not change
/// between two runs nobody diffed, so none of this is reachable from the loop — only from a person
/// pressing something, in the desktop or in a terminal.</para>
///
/// <para><b>No credential passes through here.</b> Login spawns the harness's own flow with the
/// profile's configuration home in the environment; whatever it obtains, it stores itself, where it
/// would have stored it anyway.</para>
/// </remarks>
public static class HarnessActions
{
    /// <summary>Install a harness with its own installer — a whole command, since it may not exist yet.</summary>
    /// <remarks>
    /// 🔴 <b>On the system's npm</b> (TOOLS5, D121 §2.7), whatever Tools runs node as: an install is the agent's own
    /// installer, into the machine, and that is the machine's npm's job. A managed npm's global folder may be its node
    /// version's, which the next version would lose. With no npm on the machine it refuses, naming <c>agent pin</c>.
    /// </remarks>
    public static Task<int> InstallAsync(
        HarnessToolchain toolchain, Action<string> write, CancellationToken ct = default,
        Action<HarnessRun>? started = null) =>
        toolchain.Install is { Count: > 0 } install
            ? RunAsync(OnTheSystem(install, Environment.GetEnvironmentVariable(Tools.PathVariable)), toolchain, profileHome: null, write, ct, started)
            : throw new DriverException(
                "that agent declares no installer, so Daoris has no sanctioned way to install it. "
                + "Install it with its own tooling; Daoris will find it on the next probe.");

    /// <summary>
    /// Install one version into the directory Daoris owns (TOOL2/D57), leaving the machine's own
    /// install alone: from the maker's own channel, verified, where the toolchain declares one
    /// (AGT2b) — else npm's own `--prefix`, aimed somewhere Daoris chose.
    /// </summary>
    /// <param name="transport">How a channel is reached — the network, unless a test holds its own.</param>
    /// <param name="npm">The package manager a package pin runs — npm, unless a test holds a stand-in.</param>
    public static Task<int> PinAsync(
        HarnessToolchain toolchain, string home, string harness, string version, Action<string> write,
        CancellationToken ct = default, Action<HarnessRun>? started = null, HttpMessageHandler? transport = null,
        IReadOnlyList<string>? npm = null) =>
        toolchain.Channel is { Length: > 0 } channel
            ? PinFromChannelAsync(toolchain, channel, home, harness, version, write, ct, started, transport)
            : toolchain.Package is { Length: > 0 } package
            ? RunAsync(
                // The npm Tools resolves (TOOLS5, §2.7): its prefix is named, so the node it installs for is Tools' node.
                ThroughTools(home, [.. npm ?? Npm, "install", "--prefix", HarnessSettings.ManagedHome(home, harness, version),
                 $"{package}@{version}"]),
                toolchain, profileHome: null, write, ct, started)
            : throw new DriverException(
                "that agent declares no package, so Daoris has no sanctioned way to fetch a version "
                + "of it. Install it with its own tooling and Daoris will find it on PATH.");

    /// <summary>
    /// A pin from the maker's channel (AGT2b). 🔴 A version it cannot verify is refused before a byte
    /// is fetched — and never handed to npm instead, which would be the same trust by another road.
    /// An install already in place is the proof it verified, so re-pinning it fetches nothing.
    /// </summary>
    private static async Task<int> PinFromChannelAsync(
        HarnessToolchain toolchain, string channel, string home, string harness, string version,
        Action<string> write, CancellationToken ct, Action<HarnessRun>? started, HttpMessageHandler? transport)
    {
        ReleaseChannels.RefuseVersion(channel, version);

        if (HarnessSettings.ManagedBinary(home, harness, version, toolchain.Binary) is { } present)
        {
            write($"{toolchain.Product ?? harness} {version} is already installed where Daoris keeps it — nothing was downloaded.");
            write(present);
            return 0;
        }

        using var download = CancellationTokenSource.CreateLinkedTokenSource(ct);
        started?.Invoke(new HarnessRun(download));
        write($"installing {toolchain.Product ?? harness} {version} from {toolchain.Maker ?? "its maker"}'s own release "
            + "channel, into a directory Daoris owns.");
        // The window's loop holds its home already, so the staging is published at once (the alone-path design §1.4).
        var staged = await ReleaseChannels.StageAsync(
            channel, HarnessSettings.ManagedHome(home, harness, version), version, write, download.Token, transport).ConfigureAwait(false);
        ReleaseChannels.Publish(staged, write);
        return 0;
    }

    private static readonly string[] Npm = ["npm"];

    /// <summary>
    /// A command whose first word a tool answers for, with that word the file Tools resolves (TOOLS5, D121 §2.4):
    /// <c>npm</c> in a pin is the managed node's npm when node is managed. A whole path, or a name no tool answers
    /// for, is left as named; the system's tool PATH does not find keeps its bare name, so the start says so as before.
    /// </summary>
    /// <exception cref="DriverException">The tool's way cannot run: it never falls back to <c>PATH</c>.</exception>
    internal static IReadOnlyList<string> ThroughTools(string home, IReadOnlyList<string> command)
    {
        if (command.Count == 0 || Tools.ResolveCommand(Tools.Read(home), home, command[0]) is not { } tool) return command;
        if (tool.Refused) throw new DriverException($"{tool.Problem} — so `{command[0]}` was not run.");
        return tool.File is { } file ? [file, .. command.Skip(1)] : command;
    }

    /// <summary>
    /// A command whose first word a tool answers for, found on the system's <paramref name="path"/> whatever Tools runs
    /// that tool as (TOOLS5, §2.7): <c>agent install</c> is the machine's npm's job. Any other first word is left as
    /// named.
    /// </summary>
    /// <exception cref="DriverException">The system has no such program: the install is refused, naming <c>agent pin</c>.</exception>
    internal static IReadOnlyList<string> OnTheSystem(IReadOnlyList<string> command, string? path)
    {
        if (command.Count == 0 || !Tools.Declared.Any(tool => tool.Answers.Contains(command[0], StringComparer.Ordinal))) return command;
        var file = CommandPresence.Resolve(command[0], path, startable: true)
            ?? throw new DriverException(
                $"`{command[0]}` is not on this machine's PATH, and installing an agent is the machine's own {command[0]}'s job, "
                + "whatever Daoris runs it as. `daoris agent pin <agent> <version>` installs one where Daoris keeps it instead.");
        return [file, .. command.Skip(1)];
    }

    /// <summary>
    /// Which Update a door has (USE1a): <c>"pin"</c> when it is pinned and declares a package or a
    /// channel, so Update moves the pin to the newest release; <c>"tool"</c> when it is unpinned and
    /// declares an updater of its own; otherwise null, and a surface offers no Update at all.
    /// </summary>
    /// <remarks>
    /// A pinned door's own updater is never the answer: it would change the copy on <c>PATH</c> or the
    /// configured command, not the copy sessions run. The CLI's <c>agent update</c> branches the same way.
    /// </remarks>
    /// <param name="pinned">This machine's pin for the door, or null.</param>
    public static string? UpdateOf(HarnessToolchain toolchain, string? pinned) =>
        !string.IsNullOrWhiteSpace(pinned)
            ? toolchain.Package is { Length: > 0 } || toolchain.Channel is { Length: > 0 } ? "pin" : null
            : toolchain.UpdateArguments is { Count: > 0 } ? "tool" : null;

    /// <summary>
    /// Update a door (USE1a), by <see cref="UpdateOf"/>: move a pin to the newest release, or run the
    /// door's own updater. A door with neither is refused, as it always was.
    /// </summary>
    /// <param name="pinned">This machine's pin for the door, or null.</param>
    /// <param name="pin">
    /// Writes the new pin — called only once the version it names is installed, exactly as the pin
    /// action writes its own.
    /// </param>
    /// <param name="transport">How a channel is reached — the network, unless a test holds its own.</param>
    /// <param name="npm">The package manager a package door asks and installs with — npm, unless a test holds a stand-in.</param>
    public static Task<int> UpdateAsync(
        HarnessToolchain toolchain, IReadOnlyList<string>? command, string home, string harness, string? pinned,
        Action<string> pin, Action<string> write, CancellationToken ct = default, Action<HarnessRun>? started = null,
        HttpMessageHandler? transport = null, IReadOnlyList<string>? npm = null) =>
        UpdateOf(toolchain, pinned) switch
        {
            "pin" => MovePinAsync(toolchain, home, harness, pinned!.Trim(), pin, write, ct, started, transport, npm),
            "tool" => RunAsync(
                [.. toolchain.Command(command), .. toolchain.UpdateArguments!], toolchain, profileHome: null, write, ct, started),
            _ => throw new DriverException(!string.IsNullOrWhiteSpace(pinned)
                ? $"that agent is pinned at {pinned} and declares no package or release channel to find a newer "
                  + $"version in. Pin another with `daoris agent pin {harness} <version>`, or unpin it."
                : "that agent declares no updater — it updates itself, or its package manager does."),
        };

    /// <summary>
    /// Move a pin to the newest release (USE1a): resolve the newest to one exact version, then pin it
    /// through <see cref="PinAsync"/> — the channel's verified install, or npm's. A pin never names a
    /// pointer such as <c>latest</c>.
    /// </summary>
    private static async Task<int> MovePinAsync(
        HarnessToolchain toolchain, string home, string harness, string pinned, Action<string> pin,
        Action<string> write, CancellationToken ct, Action<HarnessRun>? started, HttpMessageHandler? transport,
        IReadOnlyList<string>? npm)
    {
        string newest;
        if (toolchain.Channel is { Length: > 0 } channel)
        {
            write($"{harness} is pinned at {pinned}. Asking {toolchain.Maker ?? "its maker"}'s own release channel "
                + "which release is newest.");
            // No process asks the pointer, so stopping it is cancelling the request.
            using var asking = CancellationTokenSource.CreateLinkedTokenSource(ct);
            started?.Invoke(new HarnessRun(asking));
            try
            {
                newest = await ReleaseChannels.LatestAsync(channel, asking.Token, transport).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                throw new DriverException($"{error.Message} The pin stays at {pinned}.");
            }
        }
        else
        {
            write($"{harness} is pinned at {pinned}. Asking npm which release of {toolchain.Package} is newest.");
            IReadOnlyList<string> asking;
            try
            {
                asking = ThroughTools(home, npm ?? Npm);
            }
            catch (DriverException error)
            {
                throw new DriverException($"{error.Message} The pin stays at {pinned}.");
            }

            newest = await NewestOnNpmAsync(toolchain, asking, pinned, write, ct, started).ConfigureAwait(false);
        }

        if (newest == pinned && HarnessSettings.ManagedBinary(home, harness, pinned, toolchain.Binary) is not null)
        {
            write($"{pinned} is already the newest release — nothing was fetched, and the pin stays.");
            return 0;
        }
        if (CompareReleases(newest, pinned) < 0)
        {
            write($"the pin, {pinned}, is newer than the newest release ({newest}) — nothing was fetched, and the pin "
                + "stays. Update never moves a pin backwards.");
            return 0;
        }

        write(newest == pinned
            ? $"{pinned} is the newest release and is not installed here — installing it."
            : $"{pinned} → {newest}");
        var code = await PinAsync(toolchain, home, harness, newest, write, ct, started, transport, npm).ConfigureAwait(false);
        if (code == 0) pin(newest);
        else write($"nothing was pinned: the pin stays at {pinned}.");
        return code;
    }

    /// <summary>
    /// The newest version npm publishes of a door's package, asked with <c>npm view &lt;package&gt;
    /// version</c> — a spawn of the same kind the pin's <c>npm install</c> is, streamed the same way.
    /// Anything but one version is a refusal that says so.
    /// </summary>
    private static async Task<string> NewestOnNpmAsync(
        HarnessToolchain toolchain, IReadOnlyList<string> npm, string pinned, Action<string> write,
        CancellationToken ct, Action<HarnessRun>? started)
    {
        var package = toolchain.Package!;
        var stays = $"Nothing was fetched or pinned, and the pin stays at {pinned}.";
        var heard = new List<string>();

        int code;
        try
        {
            code = await RunAsync(
                [.. npm, "view", package, "version"], toolchain, profileHome: null,
                line =>
                {
                    // RunAsync echoes the command first; that line is Daoris's, not npm's answer.
                    if (!line.StartsWith("$ ", StringComparison.Ordinal)) heard.Add(line);
                    write(line);
                },
                ct, started).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            throw new DriverException($"{error.Message} {stays}");
        }

        var said = heard.Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        var shown = said is null ? "" : $" ({TextBounds.Clip(said, 160)})";
        if (code != 0)
        {
            throw new DriverException($"npm could not say which release of {package} is newest — it exited {code}{shown}. {stays}");
        }

        return VersionFromNpm(heard)
            ?? throw new DriverException($"npm answered no single version of {package}{shown}. {stays}");
    }

    /// <summary>One exact release, as npm and the channels write it — a prerelease or build suffix included.</summary>
    private static readonly Regex Release = new(
        @"^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.Compiled);

    /// <summary>
    /// The one version <c>npm view &lt;package&gt; version</c> answered, or null when it answered none
    /// or several (USE1a). A warning npm prints beside it is not a version, and quotes around one are
    /// npm's own. The CLI's <c>versionFromNpm</c> is the twin.
    /// </summary>
    public static string? VersionFromNpm(IEnumerable<string> lines)
    {
        var found = lines
            .Select(line => line.Trim().Trim('\'', '"'))
            .Where(line => Release.IsMatch(line))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>
    /// Which of two releases is newer: negative when <paramref name="a"/> is older, positive when newer,
    /// and null when either is not a version this can order. A release outranks its own prereleases.
    /// </summary>
    internal static int? CompareReleases(string a, string b)
    {
        var left = Release.Match(a);
        var right = Release.Match(b);
        if (!left.Success || !right.Success) return null;

        for (var at = 1; at <= 3; at++)
        {
            // Compared as digit strings, so a number too long for any integer type orders rather than throws.
            var (x, y) = (left.Groups[at].Value.TrimStart('0'), right.Groups[at].Value.TrimStart('0'));
            var difference = x.Length != y.Length ? x.Length.CompareTo(y.Length) : string.CompareOrdinal(x, y);
            if (difference != 0) return difference;
        }

        var (l, r) = (left.Groups[4], right.Groups[4]);
        if (l.Success == r.Success) return l.Success ? string.CompareOrdinal(l.Value, r.Value) : 0;
        return l.Success ? -1 : 1;
    }

    /// <summary>
    /// Run the harness's own login flow INTO a profile. The directory is Daoris's; everything that
    /// lands in it is the harness's.
    /// </summary>
    /// <remarks>
    /// <para>A sign-in that ends well ends that account's cool-off (TOOL4d, D125 §2.3): the directory may now hold another
    /// account. Both the screen's sign-ins come through here.</para>
    /// <para>🔴 <b>Into an account that is there, never a new folder</b> (ACCT1): a sign-in to an account is refused, before
    /// anything starts and with nothing made, where its folder is not there; only a sign-in to a new account
    /// (<paramref name="fresh"/>) opens one. The install's owner signed in to bring a signed-out account back and got a new
    /// account no list held.</para>
    /// </remarks>
    /// <param name="run">
    /// What the agent runs as (<see cref="HarnessProbe.CommandOf"/>, D57 rule 4): its named command, its pin, else
    /// <c>PATH</c>'s, as its status question asks it (CODEXACCT1).
    /// </param>
    /// <param name="fresh">Whether this sign-in opens a new account's folder; false signs in to one that must be there.</param>
    /// <param name="way">The tool's own sign-in, first, or its device code (CODEXLOGIN1).</param>
    public static async Task<int> LoginAsync(
        HarnessToolchain toolchain, HarnessCommand run, string profileHome,
        Action<string> write, CancellationToken ct = default, Action<HarnessRun>? started = null, bool fresh = false,
        SignInWay way = SignInWay.Browser)
    {
        var info = PrepareLogin(toolchain, run, profileHome, fresh, way);
        var code = await RunAsync(info, [.. run.Run, .. SignInWays.Arguments(run.Harness, toolchain, way)], write, ct, started)
            .ConfigureAwait(false);
        if (code == 0)
        {
            // And it is marked, so a start held on the account signed out asks it again (TOOL6g).
            ProbeLock.MarkSignedIn(profileHome, DateTimeOffset.UtcNow);
            try
            {
                AccountCooling.SignedIn(profileHome, DateTimeOffset.UtcNow);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The sign-in stands; the cool-off ends at its reset instead.
            }
        }

        return code;
    }

    /// <summary>
    /// The sign-in's start, prepared and not started (CODEXACCT1): the agent's own flow on the binary <paramref name="run"/>
    /// names, its pin's environment where it is the pin (AGT2), the account's folder made and named in the agent's variable.
    /// Refused, with nothing made, for an agent with no sign-in flow, a device code asked of one that has none (CODEXLOGIN1), an
    /// account that is not here (ACCT1), and a pin with nothing installed at it, which is never <c>PATH</c>'s instead (D57).
    /// </summary>
    /// <param name="way">The tool's own sign-in, first, or its device code (CODEXLOGIN1).</param>
    public static ProcessStartInfo PrepareLogin(
        HarnessToolchain toolchain, HarnessCommand run, string profileHome, bool fresh, SignInWay way = SignInWay.Browser)
    {
        var login = SignInWays.Arguments(run.Harness, toolchain, way);

        if (!fresh && !Directory.Exists(profileHome))
        {
            throw new DriverException(
                "that account is not on this machine, so nothing was signed in and no account was made. Sign in to a new "
                + "account instead, or name one that is here.");
        }

        if (run.Missing)
        {
            throw new DriverException(
                $"`{run.Harness}` is pinned to {run.Pinned} on this machine, and nothing is installed at that version, so "
                + $"nothing was signed in — `daoris agent pin {run.Harness} {run.Pinned}` installs it, and "
                + $"`daoris agent unpin {run.Harness}` goes back to PATH.");
        }

        return Prepare([.. run.Run, .. login], toolchain, profileHome, run.Managed);
    }

    /// <summary>
    /// Spawn and relay. Both streams, line by line, in the order they arrive — the same shape the
    /// session console takes, because this is a process like any other.
    /// </summary>
    internal static Task<int> RunAsync(
        IReadOnlyList<string> command, HarnessToolchain toolchain, string? profileHome,
        Action<string> write, CancellationToken ct, Action<HarnessRun>? started = null) =>
        RunAsync(Prepare(command, toolchain, profileHome), command, write, ct, started);

    /// <summary>
    /// An agent's own command at a terminal (PUSH1f2e5, the alone-path design §1.1, <i>The second hop</i>): its installer, as the CLI's
    /// <c>relay</c> started it, said first as <c>$ command</c> and a blank line, then started with the console's three streams left as
    /// the host's and no window flag, so the person answers its prompts, it finds a terminal, and Ctrl+C reaches it. The same facts as
    /// the window's start (<see cref="Tools.Hand(ProcessStartInfo, string?)"/> from <paramref name="home"/>, <see cref="HarnessProbe.Apply"/>,
    /// the account's variable, the pin's environment) through the same start gate. Its exit code, once it ended: the person's Ctrl+C,
    /// which <paramref name="ct"/> carries, reached the agent too, so the host waits for it to end by its own rule.
    /// </summary>
    /// <exception cref="DriverException">It could not be started.</exception>
    /// <exception cref="OperationCanceledException">Stopped before it started, waiting for a place at the start gate.</exception>
    public static async Task<int> TerminalRunAsync(
        IReadOnlyList<string> command, HarnessToolchain toolchain, string? profileHome, string? home, Action<string> write,
        CancellationToken ct, string? managed = null)
    {
        write($"  $ {string.Join(' ', command)}");
        write("");

        var info = Prepare(command, toolchain, profileHome, managed, terminal: true, home);
        Process process;
        try
        {
            process = await ProcessStarts.Shared.StartAsync(info, ct).ConfigureAwait(false)
                ?? throw new DriverException($"`{command[0]}` did not start");
        }
        catch (Win32Exception error)
        {
            // The CLI's words for a start that failed: Daoris runs the agent's own tooling and carries no copy of it.
            throw new DriverException(
                $"`{command[0]}` could not be run — {error.Message.TrimEnd('.')}. "
                + "Daoris spawns the agent's own tooling; it does not vendor a copy of it.");
        }

        using var _ = process;
        // Never ended by the token: the agent shares the console, has the person's Ctrl+C itself, and ends by its own rule.
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        return process.ExitCode;
    }

    /// <summary>
    /// A harness action's start: its streams redirected as UTF-8, the tools' environment, and the account and binary
    /// through the one seam every spawn takes (<see cref="HarnessProbe.Apply"/>), with the pin's environment where
    /// <paramref name="managed"/> is the pin's executable (AGT2). At a <paramref name="terminal"/> its streams are the host's
    /// console's and it may have a window, the tools read from <paramref name="home"/>'s file (<see cref="TerminalRunAsync"/>).
    /// </summary>
    private static ProcessStartInfo Prepare(
        IReadOnlyList<string> command, HarnessToolchain toolchain, string? profileHome, string? managed = null, bool terminal = false,
        string? home = null)
    {
        var info = terminal
            ? new ProcessStartInfo { FileName = command[0], UseShellExecute = false }
            : new ProcessStartInfo
            {
                FileName = command[0],
                // Its stdin is the screen's: a login asks for the code the browser shows, and a
                // process nobody can answer waits for ever. Its output is relayed to the console
                // below, never a window of its own — and read as UTF-8, because a harness writes it so
                // and the console's codepage is not the transcript (the Adapters.Shell rule).
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
        foreach (var part in command.Skip(1)) info.ArgumentList.Add(part);
        // An agent action is a child like a session (TOOLS5): the tools' PATH, before the file is resolved on it. A terminal's are its
        // home's, the one the verb was handed.
        if (terminal) Tools.Hand(info, home);
        else Tools.Hand(info);
        // Resolves the file Windows can start too (WindowsShim, USE1f): one line for every spawn.
        HarnessProbe.Apply(info, toolchain, profileHome, binary: managed);
        return info;
    }

    /// <summary>Start <paramref name="info"/>, said as <paramref name="command"/>, and relay it to its end.</summary>
    private static async Task<int> RunAsync(
        ProcessStartInfo info, IReadOnlyList<string> command, Action<string> write, CancellationToken ct,
        Action<HarnessRun>? started)
    {
        write($"$ {string.Join(' ', command)}");

        Process process;
        try
        {
            // Through the start gate (PROCSTART2): an agent's install or sign-in is a press, so a person's start (PARALLEL1d);
            // its caller's token ends its wait for a place as it ends the run (PARALLEL1e).
            process = await ProcessStarts.Shared.StartAsync(info, ct).ConfigureAwait(false)
                ?? throw new DriverException($"`{command[0]}` did not start");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Said by the driver, not by Win32: its message names the working directory, and one that
            // reached the page as a bare exception became the generic "something refused" (REV3).
            throw new DriverException(
                $"`{command[0]}` could not be started — it is not on this machine's PATH, or it could not be run. "
                + "Daoris runs the agent's own tooling; install that first, or run it from a terminal.");
        }

        using var _ = process;
        started?.Invoke(new HarnessRun(process));

        await Task.WhenAll(PumpAsync(process.StandardOutput, write, ct), PumpAsync(process.StandardError, write, ct))
            .ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode;
    }

    /// <summary>
    /// The file to start for <paramref name="command"/> — on Windows, the shim a bare name resolves to.
    /// </summary>
    /// <remarks>
    /// 🔴 Every declared installer is `npm …`, and on Windows `npm` is `npm.cmd`: started by its bare
    /// name with no shell, Windows appends only `.exe` and finds nothing (REV3; the CLI's `spawnable` is
    /// the twin, fixed 2026-09-22). An agent npm installed globally is the same shape (USE1f), so every
    /// spawn of a harness comes through here, by <see cref="HarnessProbe.Apply"/>. A shim is then run by
    /// `cmd.exe`, which parses its arguments again and stops at a line break — so an argument it would
    /// reinterpret is refused rather than escaped. An installer's are paths and `package@version`s, and
    /// never carry one; the pipe door's prompt does, and is refused on a shim rather than cut short.
    /// </remarks>
    internal static string WindowsShim(string command, IEnumerable<string> arguments, string? path)
    {
        if (!OperatingSystem.IsWindows()) return command;

        var file = CommandPresence.Resolve(command, path, startable: true) ?? command;
        if (!file.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            && !file.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            return file;
        }

        foreach (var argument in arguments)
        {
            if (argument.IndexOfAny(['"', '%', '&', '|', '<', '>', '^', '\r', '\n']) >= 0)
            {
                // Named by its first line, short: a whole prompt in a refusal is a page, not a sentence.
                var line = argument.Split('\n')[0].TrimEnd('\r');
                var shown = line.Length > 60 || line.Length < argument.Length ? $"{line[..Math.Min(line.Length, 60)]}…" : line;
                throw new DriverException(
                    $"`{shown}` cannot be passed to a Windows command shim safely ({Path.GetFileName(file)} is one), "
                    + "so it was not started. Run the agent's own tooling from a terminal, or point Daoris at the "
                    + "tool's own executable: a pin, or its path in driver.json's `commands`.");
            }
        }

        return file;
    }

    /// <summary>How long a partial line may sit before it is taken for a prompt.</summary>
    internal static readonly TimeSpan PromptQuiet = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Relay a stream line by line — <b>and a line that has no end</b>. A harness that asks something
    /// prints its prompt without a newline and waits; a pump that delivers only whole lines holds
    /// back the one line the person has to answer, and a login sat on a blank console with its
    /// <i>paste the code</i> never shown (measured on the real binary, 2026-09-23). What is buffered
    /// when the stream goes quiet is delivered as a line of its own.
    /// </summary>
    internal static async Task PumpAsync(TextReader reader, Action<string> write, CancellationToken ct)
    {
        var buffer = new char[1024];
        var pending = new StringBuilder();

        void Deliver(bool evenEmpty)
        {
            if (pending.Length == 0 && !evenEmpty) return;
            var line = Clean(pending.ToString());
            pending.Clear();
            lock (write) write(line);
        }

        var read = reader.ReadAsync(buffer, ct).AsTask();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var quiet = Task.Delay(PromptQuiet, ct);
            if (await Task.WhenAny(read, quiet).ConfigureAwait(false) != read)
            {
                Deliver(evenEmpty: false);
                continue;
            }

            var count = await read.ConfigureAwait(false);
            if (count == 0)
            {
                Deliver(evenEmpty: false);
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var ch = buffer[i];
                if (ch == '\n') Deliver(evenEmpty: true);
                else if (ch != '\r') pending.Append(ch);
            }

            read = reader.ReadAsync(buffer, ct).AsTask();
        }
    }

    // OSC (a hyperlink, a title), then CSI (colour, cursor), then any other two-byte escape; then any control byte left but a tab.
    private static readonly Regex Escapes = new(
        @"\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)|\x1b\[[0-9;?]*[ -/]*[@-~]|\x1b[@-Z\\-_]|[\x00-\x08\x0a-\x1f\x7f]",
        RegexOptions.Compiled);

    /// <summary>
    /// The text without the terminal's own instructions. A harness writes for a terminal, and a
    /// sign-in link arrives wrapped as a hyperlink escape (OSC 8) — the address, then the address
    /// again as its own label — which a console well renders as the URL twice around a scatter of
    /// brackets and semicolons. The label is kept; the wrapping goes, and colours with it.
    /// </summary>
    /// <remarks>
    /// No other control byte is the tool's words either, and one is the driver's own (SIGNIN1): a sign-in's phase is a relayed
    /// line that begins with the record separator, so a tool's line keeps none and cannot be read as a phase.
    /// </remarks>
    internal static string Clean(string text) => Escapes.Replace(text, "");
}
