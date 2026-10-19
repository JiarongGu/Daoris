using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Where every machine-local file Daoris owns lives (D63): one home, named by <c>DAORIS_HOME</c>, and
/// <b>no default under the user profile</b>. The application's own folder is the home — the installed
/// desktop sets the variable for itself and everything it spawns — and a terminal's <c>daoris</c>
/// reads the same variable from the user's environment. Absent, there is no home: the management
/// class refuses with <see cref="Sentence"/> rather than writing somewhere nobody pointed it.
/// </summary>
/// <remarks>
/// One of three twins — the CLI's <c>home.ts</c> and the service's <c>DaorisHome</c> hold the same
/// contract in their own languages and share no code, the way the remotes map's three copies do
/// (WSP3). The environment is the contract; the three tests move together.
///
/// <para><b>Each file and folder under the home is an entry of D172's table</b> (<see cref="HomeEntry"/>, HOMELAYOUT2), and a
/// reader asks for the entry by name, never for a path it joined itself (<see cref="Entry"/>; the CLI's
/// <c>home-census.test.ts</c> fails a join by hand). The three twins hold the same table, cell for cell.</para>
/// </remarks>
public static class DaorisHome
{
    public const string Variable = "DAORIS_HOME";

    /// <summary>The one sentence every refusal for a missing home carries.</summary>
    public const string Sentence =
        "no Daoris home: set DAORIS_HOME to the application's data folder (the `data` directory beside "
        + "Daoris.exe), or name the file's own variable. Daoris keeps nothing under the user profile.";

    /// <summary>
    /// The refusal for an entry found in both its places (D172 point 5's review note): the entry's name, its new place and its old
    /// one. The CLI's <c>HOME_TWICE</c> and the service's <c>Twice</c> are the same words.
    /// </summary>
    public const string Twice =
        "{0} is in two places, {1} and {2}, and Daoris will not choose between them: move away or remove the one you do not "
        + "mean (D172 point 5).";

    /// <summary>The home, or null when the environment names none. Blank is none.</summary>
    public static string? Resolve(Func<string, string?> environment)
    {
        var home = environment(Variable);
        return string.IsNullOrWhiteSpace(home) ? null : home.Trim();
    }

    /// <summary>The home from the process environment, or null.</summary>
    public static string? Resolve() => Resolve(Environment.GetEnvironmentVariable);

    /// <summary>The file `desktop-publish` writes at an install's root, and nothing else does: the desktop's <c>InstallHome.Marker</c>.</summary>
    public const string InstallMarker = "INSTALLED.md";

    /// <summary>The folder the install's programs run from (D93), beside <see cref="InstallData"/>.</summary>
    public const string InstallApp = "app";

    /// <summary>The install's own home (D63), beside <see cref="InstallApp"/>.</summary>
    public const string InstallData = "data";

    /// <summary>
    /// The install a program runs from (FINDDAORIS1, D171 point 1): the folder above the <c>app/</c> it runs in, however deep
    /// (the desktop is <c>app/</c>, the driver host <c>app/daoris-driver/</c>), where that folder holds the marker. Null for a
    /// workspace build, which is no install.
    /// </summary>
    public static string? InstallOf(string baseDirectory)
    {
        for (var directory = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)));
             directory?.Parent is { } parent;
             directory = parent)
        {
            if (string.Equals(directory.Name, InstallApp, StringComparison.OrdinalIgnoreCase)
                && System.IO.File.Exists(Path.Combine(parent.FullName, InstallMarker)))
            {
                return parent.FullName;
            }
        }

        return null;
    }

    /// <summary>The install's own <c>data/</c> for a program it carries, or null for a workspace build.</summary>
    public static string? OfInstall(string baseDirectory) =>
        InstallOf(baseDirectory) is { } install ? Path.Combine(install, InstallData) : null;

    /// <summary>
    /// The home a program finds with nobody to tell it (FINDDAORIS1, D171 point 1): the variable, which still wins (the dev
    /// loop, a rehearsal, a second home), else its install's own <c>data/</c>, else none.
    /// </summary>
    public static string? Resolve(Func<string, string?> environment, string baseDirectory) =>
        Resolve(environment) ?? OfInstall(baseDirectory);

    /// <summary>
    /// Take the install's home as this process's <c>DAORIS_HOME</c> where none is named (FINDDAORIS1), as the desktop does for
    /// itself (D63): every file, the machine log and each process it starts then read the same machine. The home it set, or
    /// null when one was named or this is no install.
    /// </summary>
    public static string? Adopt(Func<string, string?> environment, Action<string, string> setProcess, string baseDirectory)
    {
        if (Resolve(environment) is not null || OfInstall(baseDirectory) is not { } home) return null;
        setProcess(Variable, home);
        return home;
    }

    /// <summary>The same, on this process.</summary>
    public static string? Adopt(string baseDirectory) =>
        Adopt(Environment.GetEnvironmentVariable, (name, value) => Environment.SetEnvironmentVariable(name, value), baseDirectory);

    /// <summary>
    /// Where an entry is in a home, by D172 point 5 and its review note (the install layout design §6.2): its new place where
    /// that holds what the entry is, else its old place where that is there, else its new place, where a writer makes it.
    /// Where both hold it, neither is chosen: <see cref="HomeEntryTwiceException"/> names both, for the caller to say.
    /// </summary>
    /// <param name="home">The home, as <see cref="Resolve()"/> answers it.</param>
    /// <param name="entry">The entry, by name: a row of <see cref="HomeEntry.Table"/>.</param>
    /// <param name="asked">
    /// For an entry kept per item, the item, one part for each <c>*</c> of its place, asked per item so an old one is read where
    /// it is and a new one made in the new place; asked short of its parts, the folder holding them, which both places hold by
    /// design, so it is never refused. What is asked beyond the item, or under an entry kept whole, is joined under the place
    /// found: it is the entry's own.
    /// </param>
    /// <exception cref="HomeEntryTwiceException">Both places hold the entry, or the item.</exception>
    public static string Entry(string home, HomeEntry entry, params string[] asked)
    {
        var (next, rest, whole) = Expand(home, entry.New, asked);
        if (!string.Equals(entry.New, entry.Old, StringComparison.Ordinal))
        {
            var (old, _, _) = Expand(home, entry.Old, asked);
            next = Choose(entry, next, old, whole);
        }

        return rest.Length == 0 ? next : Path.Combine([next, .. rest]);
    }

    /// <summary>
    /// Both places of an entry, asked as <see cref="Entry"/> is, without the disk: for a list of its items, which reads both
    /// (the design §6.2). The new place first, then the old, once each.
    /// </summary>
    public static IReadOnlyList<string> Places(string home, HomeEntry entry, params string[] asked)
    {
        var (next, rest, _) = Expand(home, entry.New, asked);
        var (old, _, _) = Expand(home, entry.Old, asked);
        return [.. new[] { next, old }.Select(place => rest.Length == 0 ? place : Path.Combine([place, .. rest])).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>An entry under the environment's home, or null when there is no home.</summary>
    public static string? File(Func<string, string?> environment, HomeEntry entry) =>
        Resolve(environment) is { } home ? Entry(home, entry) : null;

    /// <summary>An entry under the process environment's home, or null.</summary>
    public static string? File(HomeEntry entry) => File(Environment.GetEnvironmentVariable, entry);

    /// <summary>
    /// An entry under the home, or a refusal naming the variable and the entry's place — for the management class, which edits
    /// and therefore must know where.
    /// </summary>
    public static string Require(Func<string, string?> environment, HomeEntry entry) =>
        File(environment, entry)
        ?? throw new DriverException($"{Sentence} (wanted: {entry.New})");

    /// <summary>The same, from the process environment.</summary>
    public static string Require(HomeEntry entry) => Require(Environment.GetEnvironmentVariable, entry);

    /// <summary>
    /// A place under the home: its <c>/</c> parts, each <c>*</c> taking the next part asked. Whole where every item part was
    /// asked; the rest asked is what lies under it.
    /// </summary>
    private static (string Place, string[] Under, bool Whole) Expand(string home, string place, string[] asked)
    {
        var parts = new List<string> { home };
        var at = 0;
        foreach (var part in place.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains('*'))
            {
                if (at >= asked.Length) return (Path.Combine([.. parts]), [], false);
                parts.Add(part.Replace("*", asked[at++], StringComparison.Ordinal));
            }
            else
            {
                parts.Add(part);
            }
        }

        return (Path.Combine([.. parts]), asked[at..], true);
    }

    /// <summary>D172 point 5 between an entry's two places, the new one taken only where it holds what the entry is.</summary>
    private static string Choose(HomeEntry entry, string next, string old, bool whole)
    {
        var held = Holds(entry, next);
        if (held && whole && Holds(entry, old)) throw new HomeEntryTwiceException(entry.Name, next, old);
        if (held) return next;
        return There(entry, old) ? old : next;
    }

    /// <summary>A place is there where it is on the disk, or, for an entry known by the names it holds, where it holds one.</summary>
    private static bool There(HomeEntry entry, string place) =>
        entry.Holds.Length > 0 ? entry.Holds.Any(name => Path.Exists(Path.Combine(place, name))) : Path.Exists(place);

    /// <summary>
    /// A place holds what the entry is: one of its names, where it is known by them; else a folder that is not empty, or a file
    /// that is not empty and, for JSON, parses. An empty or half-made place holds nothing.
    /// </summary>
    private static bool Holds(HomeEntry entry, string place)
    {
        if (entry.Holds.Length > 0) return There(entry, place);
        if (Directory.Exists(place)) return Directory.EnumerateFileSystemEntries(place).Any();
        if (!System.IO.File.Exists(place) || new FileInfo(place).Length == 0) return false;
        if (!place.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            using var stream = System.IO.File.OpenRead(place);
            // Strict JSON, a byte-order mark aside, as the CLI's twin reads it: the twins judge a file alike.
            using var parsed = JsonDocument.Parse(stream);
            return true;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>A top folder of the Daoris folder (D172 point 1): the category an entry is in.</summary>
public enum HomeCategory
{
    Data,
    Profiles,
    Work,
    Cache,
    Run,
    Logs,
}

/// <summary>
/// An entry of D172's table (the install layout design §3, HOMELAYOUT2): its name, its category, its new place and its old one,
/// each <c>/</c>-separated under the home, a <c>*</c> for each part of an item where the entry is kept per item, and the names
/// that make a place there where the entry is known by them (none: the place itself). <see cref="DaorisHome.Entry"/> reads it.
/// </summary>
/// <remarks>
/// One of three twins: the CLI's <c>HOME_ENTRIES</c> (<c>home.ts</c>) and the service's <c>HomeEntry</c> hold the same rows in
/// the same order, which <c>home.test.ts</c> holds cell for cell. 🔴 Every new place is still the old one (HOMELAYOUT2): the
/// table names the entries, and HOMELAYOUT3 switches the new places on with the move.
/// </remarks>
public sealed record HomeEntry(string Name, HomeCategory Category, string New, string Old, params string[] Holds)
{
    public static readonly HomeEntry DriverConfig = new("driver", HomeCategory.Data, "driver.json", "driver.json");
    public static readonly HomeEntry Harnesses = new("harnesses", HomeCategory.Data, "harnesses.json", "harnesses.json");
    public static readonly HomeEntry Accounts = new("accounts", HomeCategory.Data, "accounts.json", "accounts.json");
    public static readonly HomeEntry Cooling = new("cooling", HomeCategory.Data, "cooling.json", "cooling.json");
    public static readonly HomeEntry Windows = new("windows", HomeCategory.Data, "windows.json", "windows.json");
    public static readonly HomeEntry Reads = new("reads", HomeCategory.Data, "reads.json", "reads.json");
    public static readonly HomeEntry Permissions = new("permissions", HomeCategory.Data, "permissions.json", "permissions.json");
    public static readonly HomeEntry RuleProposals = new("rule-proposals", HomeCategory.Data, "proposals", "proposals");
    public static readonly HomeEntry Tools = new("tools", HomeCategory.Data, "tools.json", "tools.json");
    public static readonly HomeEntry PluginState = new("plugin-state", HomeCategory.Data, "plugins.json", "plugins.json");
    public static readonly HomeEntry Plugins = new("plugins", HomeCategory.Data, "plugins", "plugins");
    public static readonly HomeEntry PluginApprovals = new("plugin-approvals", HomeCategory.Data, "plugin-approvals.json", "plugin-approvals.json");
    public static readonly HomeEntry Usage = new("usage", HomeCategory.Data, "usage.json", "usage.json");
    public static readonly HomeEntry Landings = new("landings", HomeCategory.Data, "landings.json", "landings.json");
    public static readonly HomeEntry SessionBranches = new("session-branches", HomeCategory.Data, "session-branches.json", "session-branches.json");
    public static readonly HomeEntry LinesMoved = new("lines-moved", HomeCategory.Data, "lines-moved.json", "lines-moved.json");
    public static readonly HomeEntry RegistryFollowed = new("registry-followed", HomeCategory.Data, "registry-followed.json", "registry-followed.json");
    public static readonly HomeEntry Abandoned = new("abandoned", HomeCategory.Data, "abandoned.json", "abandoned.json");
    public static readonly HomeEntry Update = new("update", HomeCategory.Data, "update.json", "update.json");
    public static readonly HomeEntry Knowledge = new("knowledge", HomeCategory.Data, "knowledge.db", "knowledge.db");
    public static readonly HomeEntry Sessions = new("sessions", HomeCategory.Data, "sessions", "sessions");
    public static readonly HomeEntry Helpers = new("helpers", HomeCategory.Data, "helpers", "helpers");
    public static readonly HomeEntry Handed = new("handed", HomeCategory.Data, "handed", "handed");
    public static readonly HomeEntry Setup = new("setup", HomeCategory.Data, "setup", "setup");
    public static readonly HomeEntry Workflows = new("workflows", HomeCategory.Data, "workflows", "workflows");
    public static readonly HomeEntry HelpProposals = new("help-proposals", HomeCategory.Data, "help/proposals", "help/proposals");
    public static readonly HomeEntry OpinionGates = new("opinion-gates", HomeCategory.Data, "opinions/gates", "opinions/gates");
    public static readonly HomeEntry OpinionDues = new("opinion-dues", HomeCategory.Data, "opinions/due.json", "opinions/due.json");
    public static readonly HomeEntry BrowserSettings = new("browser-settings", HomeCategory.Data, "browser/settings.json", "browser/settings.json");
    public static readonly HomeEntry BrowserFavorites = new("browser-favorites", HomeCategory.Data, "browser/favorites.json", "browser/favorites.json");
    public static readonly HomeEntry HomeId = new("home-id", HomeCategory.Data, "home.json", "home.json");
    public static readonly HomeEntry Brainstorms = new("brainstorms", HomeCategory.Data, "brainstorms", "brainstorms");
    public static readonly HomeEntry UsageHistory = new("usage-history", HomeCategory.Data, "usage-history", "usage-history");
    public static readonly HomeEntry Keys = new("keys", HomeCategory.Profiles, "keys.json", "keys.json");
    public static readonly HomeEntry Remotes = new("remotes", HomeCategory.Profiles, "remotes.json", "remotes.json");
    public static readonly HomeEntry AgentHomes = new("agent-homes", HomeCategory.Profiles, "harnesses/*/*", "harnesses/*/*");
    public static readonly HomeEntry BrowserEngine = new("browser-engine", HomeCategory.Profiles, "browser/engine", "browser/engine");
    public static readonly HomeEntry BrowserEdge = new("browser-edge", HomeCategory.Profiles, "browser/edge", "browser/edge");
    public static readonly HomeEntry EdgeCookies = new("edge-cookies", HomeCategory.Profiles, "browser/edge-session-cookies.bin", "browser/edge-session-cookies.bin");
    public static readonly HomeEntry Window = new("window", HomeCategory.Profiles, "", "", "chromium", "config");
    public static readonly HomeEntry Trees = new("trees", HomeCategory.Work, "trees/*/*/*", "trees/*/*/*");
    public static readonly HomeEntry HelpRoom = new("help-room", HomeCategory.Work, "help", "help", "CLAUDE.md");
    public static readonly HomeEntry Intake = new("intake", HomeCategory.Work, "intake/*", "intake/*");
    public static readonly HomeEntry OpinionPackets = new("opinion-packets", HomeCategory.Work, "opinions/*", "opinions/*");
    public static readonly HomeEntry QuestFiles = new("quest-files", HomeCategory.Work, "quests/*", "quests/*");
    public static readonly HomeEntry AskFiles = new("ask-files", HomeCategory.Work, "asks/*", "asks/*");
    public static readonly HomeEntry SessionFiles = new("session-files", HomeCategory.Work, "sessions/*/files", "sessions/*/files");
    public static readonly HomeEntry Proof = new("proof", HomeCategory.Work, "proof/*", "proof/*");
    public static readonly HomeEntry UsageRead = new("usage-read", HomeCategory.Work, "usage-read", "usage-read");
    public static readonly HomeEntry BrainstormRooms = new("brainstorm-rooms", HomeCategory.Work, "brainstorm-rooms/*", "brainstorm-rooms/*");
    public static readonly HomeEntry Toolchain = new("toolchain", HomeCategory.Cache, "toolchain", "toolchain");
    public static readonly HomeEntry ToolVersions = new("tool-versions", HomeCategory.Cache, "tools", "tools");
    public static readonly HomeEntry Bin = new("bin", HomeCategory.Cache, "bin", "bin");
    public static readonly HomeEntry HomeLease = new("home-lease", HomeCategory.Run, "home.lease", "home.lease");
    public static readonly HomeEntry DriverLock = new("driver-lock", HomeCategory.Run, "driver.lock", "driver.lock");
    public static readonly HomeEntry Running = new("running", HomeCategory.Run, "running.json", "running.json");
    public static readonly HomeEntry Locks = new("locks", HomeCategory.Run, "locks", "locks");
    public static readonly HomeEntry Spawn = new("spawn", HomeCategory.Run, "spawn", "spawn");
    public static readonly HomeEntry Starts = new("starts", HomeCategory.Run, "starts", "starts");
    public static readonly HomeEntry Hooks = new("hooks", HomeCategory.Run, "hooks", "hooks");
    public static readonly HomeEntry Probing = new("probing", HomeCategory.Run, "harnesses/.probing", "harnesses/.probing");
    public static readonly HomeEntry SessionPids = new("session-pids", HomeCategory.Run, "sessions/*.pid", "sessions/*.pid");
    public static readonly HomeEntry Requests = new("requests", HomeCategory.Run, "sessions/requests", "sessions/requests");
    public static readonly HomeEntry EdgeRecord = new("edge-record", HomeCategory.Run, "browser/edge.json", "browser/edge.json");
    public static readonly HomeEntry Pipe = new("pipe", HomeCategory.Run, "pipe", "pipe");
    public static readonly HomeEntry Logs = new("logs", HomeCategory.Logs, "logs", "logs");

    /// <summary>Every entry, in the table's order, which the twins hold in the same order.</summary>
    public static IReadOnlyList<HomeEntry> Table { get; } =
    [
        DriverConfig, Harnesses, Accounts, Cooling, Windows, Reads, Permissions, RuleProposals, Tools, PluginState, Plugins,
        PluginApprovals, Usage,
        Landings, SessionBranches, LinesMoved, RegistryFollowed, Abandoned, Update, Knowledge, Sessions, Helpers, Handed, Setup,
        Workflows, HelpProposals, OpinionGates, OpinionDues, BrowserSettings, BrowserFavorites, HomeId, Brainstorms, UsageHistory,
        Keys, Remotes, AgentHomes, BrowserEngine, BrowserEdge, EdgeCookies, Window,
        Trees, HelpRoom, Intake, OpinionPackets, QuestFiles, AskFiles, SessionFiles, Proof, UsageRead, BrainstormRooms,
        Toolchain, ToolVersions, Bin,
        HomeLease, DriverLock, Running, Locks, Spawn, Starts, Hooks, Probing, SessionPids, Requests, EdgeRecord, Pipe,
        Logs,
    ];

    /// <summary>The entry of this name, or null where the table holds none.</summary>
    public static HomeEntry? Named(string name) => Table.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
}

/// <summary>
/// An entry found in both its places (D172 point 5's review note): neither is chosen, and the refusal names both, in
/// <see cref="DaorisHome.Twice"/>'s words, for the caller to say.
/// </summary>
public sealed class HomeEntryTwiceException(string name, string next, string old)
    : DriverException(string.Format(System.Globalization.CultureInfo.InvariantCulture, DaorisHome.Twice, name, next, old))
{
    /// <summary>The entry's new place, holding it.</summary>
    public string New { get; } = next;

    /// <summary>Its old place, holding it too.</summary>
    public string Old { get; } = old;
}
