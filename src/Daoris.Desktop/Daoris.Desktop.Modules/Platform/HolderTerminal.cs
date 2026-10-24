using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The holder's terminal (SUPERVISOR1a-ii, D208 point 2; amended by STEWARDROLE1a-i, D209 §2.2 and §6.1): the one start Daoris builds
/// that adds the holder key, made by the window itself at the owner's <i>Appoint</i>, so the key never passes through the page, the
/// clipboard or a shell's history. <b>It is a tab of Daoris's own terminal panel</b> (<see cref="TerminalModule"/>), never a separate
/// console window of the system's, and <b>its process is Daoris's own pinned harness, started by Daoris</b>: the managed, pinned
/// <c>claude-code</c> by its absolute path, its hash checked just before, with <see cref="SessionArguments"/> as its argument vector
/// (no shell between to parse it) and an environment Daoris composes (<see cref="Environment"/>), never its own inherited.
/// </summary>
/// <remarks>
/// <para><b>In the holder's own folder</b>, <c>work/rooms/roles/holders/&lt;holder id&gt;/</c> under <see cref="HomeEntry.RoleRooms"/>,
/// created at the holder's start and never moved or emptied while the holder lives; the folders of holders that no longer live are
/// removed at the next holder's start.</para>
/// <para><b>On a configuration Daoris writes</b>: <see cref="ConfigVariable"/> names <c>work/rooms/roles/config/</c>, whose
/// <c>settings.json</c> Daoris owns byte for byte and in which no file that would load something stands (<see cref="Absent"/>);
/// everything else there is the harness's own state, its sign-in among it, and is left alone.</para>
/// <para><b>Its first lines say what it is</b>, written to the tab by the panel before the harness's own output; no line holds the key.</para>
/// </remarks>
public static class HolderTerminal
{
    /// <summary>
    /// The holder's initial prompt (D209 §2.2, Codex's final read, finding 12): the argument vector's last element, the harness's
    /// positional prompt and so the model's first input. Fixed text, naming no key, no appointment and no particular role.
    /// </summary>
    public const string InitialPrompt = "You hold a role Daoris appointed. Run `daoris-driver roles brief` and do as it says.";

    /// <summary>The variable naming the harness's configuration directory.</summary>
    public const string ConfigVariable = "CLAUDE_CONFIG_DIR";

    /// <summary>The holder's <c>settings.json</c>, byte for byte: no hook, no plugin, no status line, no helper command.</summary>
    public const string Settings = "{\"disableAllHooks\":true}";

    /// <summary>
    /// The holder's argument vector after the image (D209 §2.2, stated whole there): user settings alone, hooks off again above every
    /// source but managed settings, an empty MCP configuration and only it, every role's verb and the open reads by their exact
    /// prefixes, and the initial prompt last. Each one element; the walk checks the started process's vector against it.
    /// </summary>
    public static IReadOnlyList<string> SessionArguments { get; } =
    [
        "--setting-sources", "user", "--settings", Settings, "--strict-mcp-config", "--mcp-config", "{\"mcpServers\":{}}",
        "--allowedTools", "Read", "Grep", "Glob", "Bash(daoris-driver sessions supervise:*)", "Bash(daoris-driver steward:*)",
        "Bash(daoris-driver roles brief)", "Bash(daoris-driver sessions --json:*)", "Bash(daoris-driver notices:*)",
        "Bash(daoris-driver history:*)",
        InitialPrompt,
    ];

    /// <summary>What must not stand in the holder's configuration directory: an instruction file, another settings file, and the folders that load something.</summary>
    public static IReadOnlyList<string> Absent { get; } = ["CLAUDE.md", "settings.local.json", "plugins", "commands", "agents", "skills", "hooks"];

    /// <summary>The harness the holder runs, as Daoris's managed install names it.</summary>
    public const string Harness = "claude-code";

    /// <summary>
    /// The shell a role's tab runs where it is still a shell (D209 §6.1): the system's own Windows PowerShell, by its location under
    /// <c>%SystemRoot%</c>, never the tools file's <c>pwsh</c> nor PATH's.
    /// </summary>
    public static TerminalShell SystemShell(Func<string, string?>? variable = null)
    {
        var root = (variable ?? System.Environment.GetEnvironmentVariable)("SystemRoot") is { Length: > 0 } named ? named : @"C:\Windows";
        return new TerminalShell(
            TerminalShells.WindowsPowerShell, Path.Combine(root, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"), ["-NoLogo", "-NoProfile"]);
    }

    /// <summary>The folder every holder's own folder is in.</summary>
    public static string Holders(string home) => DaorisHome.Entry(home, HomeEntry.RoleRooms, "holders");

    /// <summary>A holder's own folder, by the id Daoris drew for it.</summary>
    public static string Folder(string home, string holder) => Path.Combine(Holders(home), holder);

    /// <summary>The holders' harness configuration directory, outside every repository.</summary>
    public static string Config(string home) => DaorisHome.Entry(home, HomeEntry.RoleRooms, "config");

    /// <summary>
    /// A new holder's folder made, and the folders of holders no longer living removed (D209 §2.2): never one in <paramref name="living"/>,
    /// so a live holder's folder is never moved or emptied. The new holder's id is new, so its folder holds nothing an agent wrote.
    /// </summary>
    public static string Started(string home, string holder, IEnumerable<string> living)
    {
        var keep = new HashSet<string>(living, StringComparer.OrdinalIgnoreCase) { holder };
        var holders = Holders(home);
        if (Directory.Exists(holders))
        {
            foreach (var folder in Directory.EnumerateDirectories(holders))
            {
                if (keep.Contains(Path.GetFileName(folder))) continue;
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // Held this moment by a process that outlived its holder: the next holder's start takes it.
                }
            }
        }

        var own = Folder(home, holder);
        if (Directory.Exists(own)) Directory.Delete(own, recursive: true);
        Directory.CreateDirectory(own);
        return own;
    }

    /// <summary>
    /// The configuration directory's owned files written again (§6.1), at each holder's start: <c>settings.json</c> exactly
    /// <see cref="Settings"/>, and every <see cref="Absent"/> entry removed. The harness's own state is left alone.
    /// </summary>
    public static string Own(string home)
    {
        var config = Config(home);
        Directory.CreateDirectory(config);
        foreach (var name in Absent)
        {
            var path = Path.Combine(config, name);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }

        AtomicFile.WriteBytes(Path.Combine(config, "settings.json"), Encoding.UTF8.GetBytes(Settings));
        return config;
    }

    /// <summary>Whether the configuration directory's owned files stand as <see cref="Own"/> wrote them: null where they do, else what differs.</summary>
    public static string? Owned(string config)
    {
        try
        {
            var settings = Path.Combine(config, "settings.json");
            if (!File.Exists(settings) || !File.ReadAllBytes(settings).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Settings))) return "settings.json";
            return Absent.FirstOrDefault(name => File.Exists(Path.Combine(config, name)) || Directory.Exists(Path.Combine(config, name)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "unread";
        }
    }

    /// <summary>The variables of the system's a process needs, taken from this process's environment by name and nothing else.</summary>
    public static IReadOnlyList<string> SystemVariables { get; } =
    [
        "SystemRoot", "SystemDrive", "windir", "ComSpec", "PATHEXT", "TEMP", "TMP", "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "APPDATA",
        "LOCALAPPDATA", "USERNAME", "USERDOMAIN", "COMPUTERNAME", "ProgramData", "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432",
        "CommonProgramFiles", "CommonProgramFiles(x86)", "CommonProgramW6432", "ALLUSERSPROFILE", "PUBLIC", "OS", "NUMBER_OF_PROCESSORS",
        "PROCESSOR_ARCHITECTURE",
    ];

    /// <summary>
    /// The environment Daoris composes for the holder's harness (§6.1), whole, inheriting nothing: the system's variables by name, a
    /// <c>PATH</c> of the install's own <paramref name="app"/> folder and the system's folders, the home, <see cref="ConfigVariable"/>, the
    /// harness's updater off, and the key. No loader or runtime variable of Daoris's own environment (<c>NODE_OPTIONS</c> and its kind).
    /// </summary>
    /// <param name="app">The folder Daoris's own executables are in, <c>daoris-driver</c> among them.</param>
    /// <param name="variable">This process's environment, read by name; a test hands its own.</param>
    public static IReadOnlyDictionary<string, string> Environment(
        string home, string key, string app, Func<string, string?>? variable = null)
    {
        variable ??= System.Environment.GetEnvironmentVariable;
        var composed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in SystemVariables)
        {
            if (variable(name) is { Length: > 0 } value) composed[name] = value;
        }

        var root = composed.TryGetValue("SystemRoot", out var system) ? system : @"C:\Windows";
        composed["PATH"] = string.Join(';',
            app.TrimEnd('\\', '/'), Path.Combine(root, "System32"), root, Path.Combine(root, "System32", "Wbem"),
            Path.Combine(root, "System32", "WindowsPowerShell", "v1.0"));
        composed[DaorisHome.Variable] = home;
        composed[ConfigVariable] = Config(home);
        composed["DISABLE_AUTOUPDATER"] = "1";
        composed[HolderKey.Variable] = key;
        return composed;
    }

    /// <summary>The tab's title, naming its roles (<i>Daoris: the steward's and supervisor's terminal</i>).</summary>
    public static string Title(IReadOnlyList<string> roles) =>
        roles.Count switch
        {
            0 => "Daoris: a role's terminal",
            1 => $"Daoris: the {roles[0]}'s terminal",
            _ => $"Daoris: the {string.Join("'s, ", roles.Take(roles.Count - 1))}'s and {roles[^1]}'s terminal",
        };

    /// <summary>What the tab says first, before the harness's own output: what it is, never the key.</summary>
    public static string Intro(DateTimeOffset at, IReadOnlyList<string> roles)
    {
        string[] lines =
        [
            $"{Title(roles)}.",
            $"Daoris started its own pinned Claude Code here at {Clock(at)}, for {string.Join(" and ", roles.Select(role => "the " + role))}.",
            "Every program it starts holds the role's key: never build, test, merge or run a branch's code here, and never print the",
            "environment. Sign in once if it asks; Daoris forgets the key when it restarts.",
        ];
        return string.Join("\r\n", lines) + "\r\n\r\n";
    }

    /// <summary>What the tab says when a role is added to its holder (§2.2): the owner's line; the session reads the verbs' answers.</summary>
    public static string Added(string role, DateTimeOffset at, IReadOnlyList<string> roles) =>
        $"\r\nThe {role} was added to this terminal at {Clock(at)}: {Title(roles)}.\r\n";

    /// <summary>What the tab says when its holder's last role ends (§2.2).</summary>
    public const string TurnedOff = "\r\nThe roles this terminal held are turned off: its key admits nothing now. Close this tab.\r\n";

    private static string Clock(DateTimeOffset at) =>
        at.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// The harness a holder's tab runs (STEWARDROLE1a-i, D209 §6.1): Daoris's managed, pinned <c>claude-code</c> in its maker's layout,
/// <c>&lt;toolchain&gt;/claude-code/&lt;pin&gt;/bin/claude.exe</c>, its bytes the ones its verified install recorded. No pin, no binary at
/// the pin, no record or other bytes refuse <see cref="Refused"/>; it never falls back to PATH's <c>claude</c>.
/// </summary>
public static class HolderHarness
{
    /// <summary>The refusal of an appointment whose holder's harness is not Daoris's own verified pin.</summary>
    public const string Refused = "holder-harness";

    /// <summary>The managed binary for the machine's pin, checked now; or null with why.</summary>
    public static (string? Binary, string? Why) Find(string home)
    {
        string? version;
        try
        {
            version = HarnessSettings.Load(HarnessSettings.ResolvePath(home)).ResolveVersion(HolderTerminal.Harness, null, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return (null, "the pin could not be read");
        }

        if (string.IsNullOrWhiteSpace(version)) return (null, "no version of claude-code is pinned");
        var managed = HarnessSettings.ManagedHome(home, HolderTerminal.Harness, version);
        var binary = Path.Combine(managed, "bin", OperatingSystem.IsWindows() ? "claude.exe" : "claude");
        if (!File.Exists(binary)) return (null, $"claude-code {version} is not installed from its maker's channel");
        var recorded = VerifiedBinary.Recorded(managed, binary);
        if (recorded is null) return (null, $"claude-code {version} holds no record of the hash its install verified");
        return VerifiedBinary.HashOf(binary) == recorded ? (binary, null) : (null, $"claude-code {version}'s binary is not the one its install verified");
    }
}

/// <summary>What a holder's tab is opened with (STEWARDROLE1a-i): the holder's id, its key, its roles, the moment, and the holders living now, whose folders stay.</summary>
public sealed record HolderOpen(string Holder, string Key, IReadOnlyList<string> Roles, DateTimeOffset At, IReadOnlyList<string> Living);

/// <summary>A holder's tab opened, with its harness's process; or not, with why (<see cref="HolderHarness.Refused"/> or <c>unstarted</c>) and what to say.</summary>
public sealed record HolderOpened(string? Tab, HolderProcess? Process, string? Why, string? Message);

/// <summary>
/// The holder's harness process as the terminal module started it (D209 §6.1): its id and creation time, which name it, the image it
/// was started from and the configuration directory it was handed. The judge's walk reads the process itself against these.
/// </summary>
public sealed record HolderProcess(int Id, DateTime Created, string Image, string Config);
