using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>Whether a profile has been logged into — as the harness itself answers it.</summary>
public enum LoginState
{
    /// <summary>
    /// Nobody could say. The harness has no way to be asked, or answered in a shape this build does
    /// not read. <b>Permissive at spawn</b>, deliberately: refusing work because a tool reworded its
    /// own status line would be a worse failure than letting the harness refuse for itself.
    /// </summary>
    Unknown,

    /// <summary>The harness says this configuration home is logged in.</summary>
    In,

    /// <summary>The harness says it is not. This is the one state that refuses a spawn.</summary>
    Out,
}

/// <summary>
/// How a harness answers "is this configuration home logged in?" — the arguments to ask with, and the
/// two shapes its answer takes.
/// </summary>
/// <remarks>
/// <para><b>Patterns, not exit codes.</b> Both supported harnesses exit 0 whether or not they are
/// logged in, so the exit code says nothing and the OUTPUT is the answer. Verified against the real
/// binaries before this was written, because every clause of it is a claim about somebody else's
/// tool.</para>
///
/// <para><b>Neither pattern matching means <see cref="LoginState.Unknown"/></b>, never logged-out: an
/// answer this build cannot read is not evidence.</para>
/// </remarks>
/// <param name="Arguments">What to run, under the profile's configuration home.</param>
/// <param name="LoggedIn">A pattern whose presence means yes.</param>
/// <param name="LoggedOut">A pattern whose presence means no.</param>
public sealed record LoginQuestion(IReadOnlyList<string> Arguments, string LoggedIn, string LoggedOut);

/// <summary>
/// What Daoris knows about a harness AS A TOOL (D49 §4) — where its binary is, how to ask its version,
/// which environment variable names its configuration home, and how to run its OWN install, update and
/// login flows.
/// </summary>
/// <remarks>
/// <para><b>Daoris manages directories and names, never secrets.</b> Nothing here reads a credential;
/// <see cref="Login"/> runs the harness's own flow INTO a profile directory, so whatever it stores
/// stays in its own store, under the user's OS account, exactly where it lives without Daoris.</para>
///
/// <para><b>Install and update are the harness's own mechanism</b>, never a download Daoris invents:
/// a tool that installed its dependencies by a route their authors did not publish is a tool nobody
/// can support. They are person-actions and stay out of every gate.</para>
///
/// <para>A harness with no <see cref="ProfileVariable"/> supports no profiles, and asking for one on
/// it is refused rather than silently ignored — a spawn that quietly ran as the wrong account is the
/// failure this whole feature exists to prevent.</para>
/// </remarks>
/// <param name="Binary">The default command, when the driver config names none.</param>
/// <param name="VersionArguments">How to ask the version. Recorded at spawn, so "which tool produced this" is answerable.</param>
/// <param name="ProfileVariable">The environment seam: the variable naming this harness's configuration home.</param>
/// <param name="Install">
/// A WHOLE command, because the harness may not exist yet — installing one that is absent cannot be
/// arguments to a binary that is not there. It is usually that harness's package manager.
/// </param>
/// <param name="UpdateArguments">
/// Arguments TO the binary: only a harness that is present can update itself, and a present harness
/// updates through its own mechanism rather than through a second copy of its installer.
/// </param>
/// <param name="LoginArguments">
/// Arguments TO the binary, run with the profile's configuration home in the environment. It must be
/// the harness itself, because the credential it obtains belongs in the harness's own store.
/// </param>
/// <param name="LoginCheck">How to ask a profile's login state. Null leaves every profile <see cref="LoginState.Unknown"/>.</param>
public sealed record HarnessToolchain(
    IReadOnlyList<string> Binary,
    IReadOnlyList<string> VersionArguments,
    string? ProfileVariable = null,
    IReadOnlyList<string>? Install = null,
    IReadOnlyList<string>? UpdateArguments = null,
    IReadOnlyList<string>? LoginArguments = null,
    LoginQuestion? LoginCheck = null)
{
    /// <summary>The command this harness actually runs as: the machine's configured one, or the declared one.</summary>
    public IReadOnlyList<string> Command(IReadOnlyList<string>? configured) =>
        configured is { Count: > 0 } ? configured : Binary;
}

/// <summary>
/// One profile as it stands: its name, the directory Daoris owns the location of, and what the harness
/// says about logging in there. <b>Never anything from inside it.</b>
/// </summary>
public sealed record ProfileReport(string Name, string Home, LoginState Login);

/// <summary>
/// One harness as this machine has it (D49 §4): present or absent, its version, its profiles.
/// </summary>
/// <param name="Problem">Why it could not be probed — the sentence a person acts on, when absent.</param>
public sealed record HarnessReport(
    string Adapter,
    bool Present,
    string? Version,
    string? Problem,
    string? ProfileVariable,
    string? MachineDefault,
    IReadOnlyList<ProfileReport> Profiles);

/// <summary>
/// The person's harness wiring: which named profile each harness runs as, per machine and optionally
/// per workspace (D49 §4). <b>Machine-local and tracked by nothing</b>, exactly like the remotes map.
/// </summary>
/// <remarks>
/// <para><b>The FILE is the contract</b> (`~/.daoris/harnesses.json`), and so is the directory layout
/// beside it — the CLI's `daoris harness` and this class share no code, because the driver links
/// against no CLI and the CLI has no .NET. The twins move together, the same rule the remotes map's
/// three copies established (WSP3).</para>
///
/// <para><b>Silence means the harness's own home.</b> A machine that has never named a profile spawns
/// exactly as it did before this existed — the environment seam is not set at all, and the harness
/// uses the configuration home it always has. That is what keeps "Daoris works alone" (D48 §2a) true
/// through a feature about accounts: pointing someone who never asked for profiles at a fresh
/// configuration directory would log them out of their own tool.</para>
/// </remarks>
/// <param name="Defaults">Harness → profile, for this machine.</param>
/// <param name="Workspaces">Workspace → harness → profile. The natural cut: a work account for the work circle.</param>
public sealed record HarnessSettings(
    IReadOnlyDictionary<string, string>? Defaults = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? Workspaces = null)
{
    public IReadOnlyDictionary<string, string> Defaults { get; init; } =
        Defaults ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Workspaces { get; init; } =
        Workspaces ?? new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    public const string PathVariable = "DAORIS_HARNESS_CONFIG";

    /// <summary>The conventional home, beside the driver's own config and the remotes map.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris", "harnesses.json");

    /// <summary>The file every surface reads and writes — the override, or the conventional home.</summary>
    public static string ResolvePath() =>
        Environment.GetEnvironmentVariable(PathVariable) ?? DefaultPath;

    /// <summary>The directory the profile tree and the wiring file share. The machine's `.daoris`.</summary>
    public static string HomeOf(string settingsPath) =>
        Path.GetDirectoryName(Path.GetFullPath(settingsPath))!;

    /// <summary>
    /// A missing file is a machine that named no profiles — not an error. Neither is an unreadable
    /// one: this is wiring, and absent wiring is the documented default (D21), so a hand-mangled file
    /// must never be what stops a driver coming up.
    /// </summary>
    public static HarnessSettings Load(string path)
    {
        if (!File.Exists(path)) return new HarnessSettings();

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new HarnessSettings();

            var defaults = ReadMap(document.RootElement, "defaults");
            var workspaces = new Dictionary<string, IReadOnlyDictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);

            if (document.RootElement.TryGetProperty("workspaces", out var circles)
                && circles.ValueKind == JsonValueKind.Object)
            {
                foreach (var circle in circles.EnumerateObject())
                {
                    if (circle.Value.ValueKind != JsonValueKind.Object) continue;
                    workspaces[circle.Name] = ReadMap(circle.Value, null);
                }
            }

            return new HarnessSettings(defaults, workspaces);
        }
        catch (Exception error)
            when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new HarnessSettings();
        }
    }

    /// <summary>
    /// Write the wiring back — atomically, beside-then-rename, like every write in this family.
    /// </summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();

            writer.WriteStartObject("defaults");
            foreach (var (harness, profile) in Defaults.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                writer.WriteString(harness, profile);
            }

            writer.WriteEndObject();

            writer.WriteStartObject("workspaces");
            foreach (var (workspace, map) in Workspaces.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (map.Count == 0) continue;
                writer.WriteStartObject(workspace);
                foreach (var (harness, profile) in map.OrderBy(e => e.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(harness, profile);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        var beside = path + ".writing";
        File.WriteAllText(beside, System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n");
        File.Move(beside, path, overwrite: true);
    }

    /// <summary>
    /// Which profile a spawn runs as: the person's pick, then the workspace's default, then the
    /// machine's, then <b>none at all</b> — and none means the harness's own configuration home.
    /// </summary>
    public string? Resolve(string harness, string? workspace, string? chosen)
    {
        if (!string.IsNullOrWhiteSpace(chosen)) return chosen.Trim();

        if (!string.IsNullOrWhiteSpace(workspace)
            && Workspaces.TryGetValue(workspace.Trim(), out var circle)
            && circle.TryGetValue(harness, out var perCircle)
            && !string.IsNullOrWhiteSpace(perCircle))
        {
            return perCircle;
        }

        return Defaults.TryGetValue(harness, out var machine) && !string.IsNullOrWhiteSpace(machine)
            ? machine
            : null;
    }

    /// <summary>Set or clear this machine's default profile for a harness. Null clears.</summary>
    public HarnessSettings WithDefault(string harness, string? profile)
    {
        var defaults = new Dictionary<string, string>(Defaults, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(profile)) defaults.Remove(harness);
        else defaults[harness] = profile.Trim();

        return this with { Defaults = defaults };
    }

    /// <summary>Set or clear one workspace's default profile for a harness. Null clears.</summary>
    public HarnessSettings WithWorkspaceDefault(string workspace, string harness, string? profile)
    {
        var workspaces = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            Workspaces, StringComparer.OrdinalIgnoreCase);
        var circle = workspaces.TryGetValue(workspace, out var existing)
            ? new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(profile)) circle.Remove(harness);
        else circle[harness] = profile.Trim();

        if (circle.Count == 0) workspaces.Remove(workspace);
        else workspaces[workspace] = circle;

        return this with { Workspaces = workspaces };
    }

    /// <summary>
    /// Where a named profile's configuration home is. <b>Daoris owns this location and nothing
    /// inside it.</b>
    /// </summary>
    public static string ProfileHome(string home, string harness, string profile) =>
        Path.Combine(home, "harnesses", Name(harness, "harness name"), Name(profile, "profile name"));

    /// <summary>
    /// The profiles that exist for a harness — <b>the directories that exist</b>, sorted. There is no
    /// register of profiles to disagree with the disk, which is the same reason the registry became
    /// the authority rather than a view over a scan (D48 §3): one truth, not two.
    /// </summary>
    public static IReadOnlyList<string> Profiles(string home, string harness)
    {
        var root = Path.Combine(home, "harnesses", Name(harness, "harness name"));
        if (!Directory.Exists(root)) return [];

        return [.. Directory.EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// A name that will become a directory. Refused rather than normalised: a name carrying a
    /// separator or a traversal is a request to own a location somewhere else, and quietly rewriting
    /// it would put a profile where nobody would look for it.
    /// </summary>
    private static string Name(string value, string what)
    {
        var trimmed = value?.Trim() ?? "";
        var bad = trimmed.Length == 0
            || trimmed is "." or ".."
            || trimmed.Contains('/') || trimmed.Contains('\\')
            || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

        return bad
            ? throw new DriverException(
                $"`{value}` is not a usable {what} — letters, digits, dashes. A profile is a directory "
                + "Daoris owns the location of, so its name may not point anywhere else.")
            : trimmed;
    }

    private static IReadOnlyDictionary<string, string> ReadMap(JsonElement parent, string? property)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var element = parent;
        if (property is not null
            && (!parent.TryGetProperty(property, out element) || element.ValueKind != JsonValueKind.Object))
        {
            return map;
        }

        foreach (var entry in element.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.String
                && entry.Value.GetString() is { Length: > 0 } profile)
            {
                map[entry.Name] = profile;
            }
        }

        return map;
    }
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
    public static async Task<HarnessReport> ProbeAsync(
        string adapter,
        HarnessToolchain toolchain,
        IReadOnlyList<string>? command,
        HarnessSettings settings,
        string home,
        CancellationToken ct = default)
    {
        var resolved = toolchain.Command(command);
        var version = await AskAsync(resolved, toolchain.VersionArguments, profileHome: null, toolchain, ct)
            .ConfigureAwait(false);

        var present = version.Ran;
        var profiles = new List<ProfileReport>();

        foreach (var name in HarnessSettings.Profiles(home, adapter))
        {
            var profileHome = HarnessSettings.ProfileHome(home, adapter, name);
            profiles.Add(new ProfileReport(
                name,
                profileHome,
                present ? await LoginAsync(resolved, toolchain, profileHome, ct).ConfigureAwait(false)
                        : LoginState.Unknown));
        }

        return new HarnessReport(
            adapter,
            present,
            present ? FirstLine(version.Output) : null,
            present ? null : version.Problem,
            toolchain.ProfileVariable,
            settings.Defaults.TryGetValue(adapter, out var machine) ? machine : null,
            profiles);
    }

    /// <summary>
    /// What the harness says about logging in here. <b>One boolean is taken and nothing else</b> — a
    /// real harness volunteers an email, an organisation and a subscription tier alongside it, and
    /// none of that is Daoris's to hold, log, or put on a roster.
    /// </summary>
    private static async Task<LoginState> LoginAsync(
        IReadOnlyList<string> resolved, HarnessToolchain toolchain, string profileHome, CancellationToken ct)
    {
        if (toolchain.LoginCheck is not { } question) return LoginState.Unknown;

        var answer = await AskAsync(resolved, question.Arguments, profileHome, toolchain, ct)
            .ConfigureAwait(false);
        if (!answer.Ran) return LoginState.Unknown;

        // Matched in this order because a "logged in" pattern is the specific one; and neither
        // matching leaves it unknown rather than out, which is what keeps a reworded status line from
        // refusing a spawn that would have worked.
        if (Matches(answer.Output, question.LoggedIn)) return LoginState.In;
        return Matches(answer.Output, question.LoggedOut) ? LoginState.Out : LoginState.Unknown;
    }

    private static bool Matches(string output, string pattern)
    {
        try
        {
            return Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            // A pattern this build cannot run is no evidence, exactly like an answer it cannot read.
            return false;
        }
    }

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
        CancellationToken ct)
    {
        if (resolved.Count == 0) return (false, "", "no command to run");

        var info = new ProcessStartInfo
        {
            FileName = resolved[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var part in resolved.Skip(1)) info.ArgumentList.Add(part);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        Apply(info, toolchain, profileHome);

        try
        {
            using var process = Process.Start(info);
            if (process is null) return (false, "", $"`{resolved[0]}` did not start");

            using var patience = CancellationTokenSource.CreateLinkedTokenSource(ct);
            patience.CancelAfter(Patience);

            var stdout = process.StandardOutput.ReadToEndAsync(patience.Token);
            var stderr = process.StandardError.ReadToEndAsync(patience.Token);
            try
            {
                await process.WaitForExitAsync(patience.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);

                // The two reads are still outstanding, and abandoning them leaves faulted tasks
                // nobody observes — a process that hung is exactly when they fault. Awaited to
                // completion and discarded: the answer is already "it did not answer".
                await Task.WhenAll(stdout, stderr).ContinueWith(
                    _ => { }, TaskScheduler.Default).ConfigureAwait(false);

                return (false, "", $"`{resolved[0]}` did not answer within {Patience.TotalSeconds:0}s");
            }

            // Both streams: a harness that reports its version on stderr is not an absent harness.
            return (true, $"{await stdout.ConfigureAwait(false)}\n{await stderr.ConfigureAwait(false)}", null);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException)
        {
            return (false, "", $"`{resolved[0]}` is not on this machine's PATH — {error.Message}");
        }
    }

    /// <summary>
    /// Put a profile's configuration home into a process's environment, through the seam the harness
    /// itself provides for it. <b>Creating the directory is part of selecting it</b>: at least one
    /// supported harness refuses to start when its home variable names a path that does not exist,
    /// and Daoris owns that location by design.
    /// </summary>
    internal static void Apply(ProcessStartInfo info, HarnessToolchain toolchain, string? profileHome)
    {
        if (profileHome is null) return;

        if (toolchain.ProfileVariable is not { Length: > 0 } variable)
        {
            throw new DriverException(
                "that harness has no configuration-home variable, so Daoris cannot run it as a named "
                + "profile. Its accounts are managed with its own tooling.");
        }

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
public sealed record HarnessSelection(
    string? Refusal, string? Profile = null, string? ProfileHome = null, string? Version = null)
{
    public bool Allowed => Refusal is null;
}

/// <summary>
/// What this machine's harnesses are, and which account a spawn runs as (D49 §4) — <b>one judgement
/// for both doors</b>, driven and chat, for the same reason <see cref="Planner"/> is pure and the
/// service has one ledger: two copies of "may this start" drift, and the drift is invisible.
/// </summary>
/// <remarks>
/// <para><b>Probes are cached, and a refusal is re-checked before it is given.</b> Detection spawns a
/// process, so doing it every tick for every quest would be absurd; but a cached "absent" or "logged
/// out" would keep refusing after the person did exactly what the refusal told them to. So a yes is
/// trusted and a no is asked again — which costs one process on the path that was about to fail
/// anyway.</para>
///
/// <para><b>The wiring file is re-read, never held.</b> Same rule as `driver.json`: the file is the
/// truth and the surfaces are editors over it (D50), so a profile default changed from a terminal
/// takes effect on the next spawn without restarting anything.</para>
/// </remarks>
public sealed class HarnessRoster(AdapterSet adapters, string? settingsPath = null)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, HarnessReport> _seen =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where the wiring lives. The profile tree sits beside it.</summary>
    public string SettingsPath { get; } = settingsPath ?? HarnessSettings.ResolvePath();

    /// <summary>The directory profiles live under — `~/.daoris` on an ordinary machine.</summary>
    public string Home => HarnessSettings.HomeOf(SettingsPath);

    /// <summary>The person's harness wiring, as it stands on disk right now.</summary>
    public HarnessSettings Settings => HarnessSettings.Load(SettingsPath);

    /// <summary>The harnesses this build knows, whether or not they are installed.</summary>
    public IReadOnlyList<string> Known => adapters.Names;

    /// <summary>This harness's toolchain, or null for an adapter that declares none.</summary>
    public HarnessToolchain? Toolchain(string adapter) => adapters.Resolve(adapter).Toolchain;

    /// <summary>
    /// One harness as this machine has it. Cached after the first look; <paramref name="refresh"/>
    /// asks again, which is what the roster surface's refresh and every refusal do.
    /// </summary>
    public async Task<HarnessReport?> ReportAsync(
        string adapter, DriverConfig config, bool refresh = false, CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain is not { } toolchain) return null;

        if (!refresh && _seen.TryGetValue(resolved.Name, out var cached)) return cached;

        var report = await HarnessProbe.ProbeAsync(
            resolved.Name, toolchain, config.Commands.GetValueOrDefault(resolved.Name),
            Settings, Home, ct).ConfigureAwait(false);

        _seen[resolved.Name] = report;
        return report;
    }

    /// <summary>Every harness this build knows, probed — what a roster surface renders.</summary>
    public async Task<IReadOnlyList<HarnessReport>> RosterAsync(
        DriverConfig config, bool refresh = false, CancellationToken ct = default)
    {
        var reports = new List<HarnessReport>();
        foreach (var name in Known)
        {
            if (await ReportAsync(name, config, refresh, ct).ConfigureAwait(false) is { } report)
            {
                reports.Add(report);
            }
        }

        return reports;
    }

    /// <summary>
    /// May a session spawn on this harness, and as which account? The two refusals mirror each other
    /// deliberately — a missing binary and a logged-out profile are the same kind of answer, and each
    /// <b>names the action that fixes it</b> rather than failing bare.
    /// </summary>
    /// <param name="workspace">The repository's circle, for the per-workspace default (D49 §4).</param>
    /// <param name="chosen">The person's pick for this session, when they made one.</param>
    public async Task<HarnessSelection> SelectAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen,
        CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);

        // An adapter that declares no toolchain has nothing to check: it spawns exactly as it did
        // before this existed. Purely additive, which is what lets a new adapter arrive without
        // answering questions about installers it may not have.
        if (resolved.Toolchain is not { } toolchain)
        {
            return new HarnessSelection(Refusal: null);
        }

        var settings = Settings;
        var profile = settings.Resolve(resolved.Name, workspace, chosen);

        var report = await ReportAsync(resolved.Name, config, refresh: false, ct).ConfigureAwait(false);
        if (report is { Present: false })
        {
            // Asked again before refusing: the cached answer may predate the install the last refusal
            // asked for.
            report = await ReportAsync(resolved.Name, config, refresh: true, ct).ConfigureAwait(false);
        }

        if (report is { Present: false })
        {
            var install = toolchain.Install is { Count: > 0 }
                ? $"`daoris harness install {resolved.Name}` installs it"
                : $"install it with its own tooling ({string.Join(' ', toolchain.Command(config.Commands.GetValueOrDefault(resolved.Name)))})";

            return new HarnessSelection(
                $"`{resolved.Name}` is not installed on this machine, so there is nothing to spawn — "
                + $"{install}. Daoris never installs a harness unasked: a tool that changed under a "
                + "running loop is a moving target.");
        }

        if (profile is null) return new HarnessSelection(null, null, null, report?.Version);

        var home = HarnessSettings.ProfileHome(Home, resolved.Name, profile);
        var login = report?.Profiles.FirstOrDefault(
            p => string.Equals(p.Name, profile, StringComparison.OrdinalIgnoreCase))?.Login
            ?? LoginState.Unknown;

        if (login == LoginState.Out)
        {
            // Same re-ask as above, and for the same reason: the person may have just logged in.
            report = await ReportAsync(resolved.Name, config, refresh: true, ct).ConfigureAwait(false);
            login = report?.Profiles.FirstOrDefault(
                p => string.Equals(p.Name, profile, StringComparison.OrdinalIgnoreCase))?.Login
                ?? LoginState.Unknown;
        }

        if (login == LoginState.Out)
        {
            return new HarnessSelection(
                $"the `{resolved.Name}` profile `{profile}` is not logged in, so a session would have "
                + $"nothing to run as — `daoris harness login {resolved.Name} --profile {profile}` runs "
                + "the harness's own login flow into it. Daoris manages the directory and the name; the "
                + "credential stays in the harness's own store.");
        }

        return new HarnessSelection(null, profile, home, report?.Version);
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
    public static Task<int> InstallAsync(
        HarnessToolchain toolchain, Action<string> write, CancellationToken ct = default) =>
        toolchain.Install is { Count: > 0 } install
            ? RunAsync(install, toolchain, profileHome: null, write, ct)
            : throw new DriverException(
                "that harness declares no installer, so Daoris has no sanctioned way to install it. "
                + "Install it with its own tooling; Daoris will find it on the next probe.");

    /// <summary>Update a present harness through its own updater.</summary>
    public static Task<int> UpdateAsync(
        HarnessToolchain toolchain, IReadOnlyList<string>? command, Action<string> write,
        CancellationToken ct = default) =>
        toolchain.UpdateArguments is { Count: > 0 } update
            ? RunAsync([.. toolchain.Command(command), .. update], toolchain, profileHome: null, write, ct)
            : throw new DriverException("that harness declares no updater — it updates itself, or its package manager does.");

    /// <summary>
    /// Run the harness's own login flow INTO a profile. The directory is Daoris's; everything that
    /// lands in it is the harness's.
    /// </summary>
    public static Task<int> LoginAsync(
        HarnessToolchain toolchain, IReadOnlyList<string>? command, string profileHome,
        Action<string> write, CancellationToken ct = default) =>
        toolchain.LoginArguments is { Count: > 0 } login
            ? RunAsync([.. toolchain.Command(command), .. login], toolchain, profileHome, write, ct)
            : throw new DriverException(
                "that harness declares no login flow — log in with its own tooling, pointing its "
                + "configuration-home variable at the profile directory.");

    /// <summary>
    /// Spawn and relay. Both streams, line by line, in the order they arrive — the same shape the
    /// session console takes, because this is a process like any other.
    /// </summary>
    internal static async Task<int> RunAsync(
        IReadOnlyList<string> command, HarnessToolchain toolchain, string? profileHome,
        Action<string> write, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = command[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var part in command.Skip(1)) info.ArgumentList.Add(part);
        HarnessProbe.Apply(info, toolchain, profileHome);

        write($"$ {string.Join(' ', command)}");

        using var process = Process.Start(info)
            ?? throw new DriverException($"`{command[0]}` did not start");

        var pump = async (TextReader reader) =>
        {
            while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            {
                lock (write) write(line);
            }
        };

        await Task.WhenAll(pump(process.StandardOutput), pump(process.StandardError)).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode;
    }
}
