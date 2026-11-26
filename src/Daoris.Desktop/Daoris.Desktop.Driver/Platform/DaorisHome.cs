namespace Daoris.Driver;

/// <summary>
/// Where every machine-local file Daoris owns lives: one home, named by <c>DAORIS_HOME</c>, and
/// <b>no default under the user profile</b>. The application's own folder is the home — the installed
/// desktop sets the variable for itself and everything it spawns — and a terminal's <c>daoris</c>
/// reads the same variable from the user's environment. Absent, there is no home: the management
/// class refuses with <see cref="Sentence"/> rather than writing somewhere nobody pointed it.
/// </summary>
/// <remarks>
/// One of three twins — the CLI's <c>home.ts</c> and the service's <c>DaorisHome</c> hold the same
/// contract in their own languages and share no code, the way the remotes map's three copies do.
/// The environment is the contract; the three tests move together.
///
/// <para><b>Each file and folder under the home is an entry of the table</b> (<see cref="HomeEntry"/>), and a
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

    /// <summary>The folder the install's programs run from, beside <see cref="InstallData"/>.</summary>
    public const string InstallApp = "app";

    public const string InstallUpdate = "update";
    public const string InstallLauncher = "Daoris.exe";

    /// <summary>The install's own home, beside <see cref="InstallApp"/>.</summary>
    public const string InstallData = "data";

    /// <summary>
    /// The install a program runs from: the folder above the <c>app/</c> it runs in, however deep
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
    /// The home a program finds with nobody to tell it: the variable, which still wins (the dev
    /// loop, a rehearsal, a second home), else its install's own <c>data/</c>, else none.
    /// </summary>
    public static string? Resolve(Func<string, string?> environment, string baseDirectory) =>
        Resolve(environment) ?? OfInstall(baseDirectory);

    /// <summary>
    /// Take the install's home as this process's <c>DAORIS_HOME</c> where none is named, as the desktop does for
    /// itself: every file, the machine log and each process it starts then read the same machine. The home it set, or
    /// null when one is named or this is no install.
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

    /// <summary>Where an entry is in a home: its place under the Daoris folder (<see cref="FolderOf"/>), its category's folder made where the Daoris folder is there.</summary>
    /// <param name="home">The home, as <see cref="Resolve()"/> answers it.</param>
    /// <param name="entry">The entry, by name: a row of <see cref="HomeEntry.Table"/>.</param>
    /// <param name="asked">
    /// For an entry kept per item, the item, one part for each <c>*</c> of its place; asked short of its parts, the folder
    /// holding them. What is asked beyond the item, or under an entry kept whole, is joined under the place: it is the entry's own.
    /// </param>
    public static string Entry(string home, HomeEntry entry, params string[] asked)
    {
        var folder = FolderOf(home);
        var (place, rest) = Expand(folder, entry.New, asked);
        MakeCategory(folder, entry);
        return rest.Length == 0 ? place : Path.Combine([place, .. rest]);
    }

    /// <summary>
    /// The folder of an entry's category, made where the Daoris folder is there: a writer of a file the home's
    /// top holds (<c>driver.json</c>, <c>keys.json</c>, <c>running.json</c>) writes into its category's folder, which a new home
    /// has not made yet. Nothing else is made: an entry's own folders are its writer's, and a home that is not there stays not there.
    /// </summary>
    private static void MakeCategory(string folder, HomeEntry entry)
    {
        var parts = entry.New.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;
        var category = Path.Combine(folder, parts[0]);
        if (Directory.Exists(category) || !Directory.Exists(folder)) return;
        try
        {
            Directory.CreateDirectory(category);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The writer that follows meets the same refusal and says it.
        }
    }

    /// <summary>
    /// The Daoris folder a home names: an install's folder (it holds <see cref="InstallMarker"/>); the <c>data/</c> of an
    /// install, which names the same install; and any other home, which is the folder itself.
    /// </summary>
    public static string FolderOf(string home)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(home);
        if (System.IO.File.Exists(Path.Combine(trimmed, InstallMarker))) return trimmed;
        if (string.Equals(Path.GetFileName(trimmed), InstallData, StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(trimmed) is { Length: > 0 } parent
            && System.IO.File.Exists(Path.Combine(parent, InstallMarker)))
        {
            return parent;
        }

        return trimmed;
    }

    /// <summary>
    /// Where an install carries the doctrine tool's launchers, from its root: beside the
    /// application, in <c>app/</c>. Twins: the CLI's <c>INSTALL_BIN</c>, the publish's <c>CLI_BIN</c>, which lays them
    /// out; <c>desktop-publish.test.ts</c> reads all three.
    /// </summary>
    public static readonly IReadOnlyList<string> InstallBinLayout = ["app", "bin"];

    /// <summary>
    /// The launchers' folder in the home's Daoris folder (<see cref="FolderOf"/>), where it is a folder:
    /// an install's <c>app/bin/</c> whichever way its home is named, and a development run's scratch root's. Null for a Daoris
    /// folder with none, and for an <c>app/bin</c> that is not a folder.
    /// </summary>
    public static string? InstallBin(string home)
    {
        var bin = Path.Combine([FolderOf(Path.GetFullPath(home)), .. InstallBinLayout]);
        return Directory.Exists(bin) ? bin : null;
    }

    /// <summary>
    /// The home a file or folder of it is in, the entry it is named: undone from the entry's place, the Daoris
    /// folder, spelled as its <c>data/</c> where that folder is an install, as an install's home is named; a path the
    /// entry does not end, its own folder. For the readers that hold a file rather than its home (<c>DriverConfig.HomeOf</c>,
    /// <c>HarnessSettings.HomeOf</c>, the in-app browser's profile). An entry kept per item is not asked here.
    /// </summary>
    public static string HomeOf(string path, HomeEntry entry)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (Undone(full, entry.New) is { } folder)
        {
            return System.IO.File.Exists(Path.Combine(folder, InstallMarker)) ? Path.Combine(folder, InstallData) : folder;
        }

        return Path.GetDirectoryName(full)!;
    }

    /// <summary>The folder a place's parts sit under in <paramref name="full"/>, or null where it does not end with them.</summary>
    private static string? Undone(string full, string place)
    {
        var at = full;
        foreach (var part in place.Split('/', StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            if (!string.Equals(Path.GetFileName(at), part, PathCase) || Path.GetDirectoryName(at) is not { Length: > 0 } parent) return null;
            at = parent;
        }

        return at;
    }

    private static StringComparison PathCase =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

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

    /// <summary>A place under the home: its <c>/</c> parts, each <c>*</c> taking the next part asked; the rest asked is what lies under it.</summary>
    private static (string Place, string[] Under) Expand(string home, string place, string[] asked)
    {
        var parts = new List<string> { home };
        var at = 0;
        foreach (var part in place.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains('*'))
            {
                if (at >= asked.Length) return (Path.Combine([.. parts]), []);
                parts.Add(part.Replace("*", asked[at++], StringComparison.Ordinal));
            }
            else
            {
                parts.Add(part);
            }
        }

        return (Path.Combine([.. parts]), asked[at..]);
    }
}

/// <summary>A top folder of the Daoris folder: the category an entry is in.</summary>
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
/// An entry of the home's table: its name, its category, its place under the Daoris folder
/// (<see cref="DaorisHome.FolderOf"/>), <c>/</c>-separated, a <c>*</c> for each
/// part of an item where the entry is kept per item, and the names that make a place there where the entry is known by them
/// (none: the place itself). <see cref="DaorisHome.Entry"/> reads it.
/// </summary>
/// <remarks>
/// One of three twins: the CLI's <c>HOME_ENTRIES</c> (<c>home.ts</c>) and the service's <c>HomeEntry</c> hold the same rows in
/// the same order, which <c>home.test.ts</c> holds cell for cell. 🔴 Each place is under
/// the top folder its category names, and an install's <c>data/</c> entries stay where they are.
/// </remarks>
public sealed record HomeEntry(string Name, HomeCategory Category, string New, params string[] Holds)
{
    public static readonly HomeEntry DriverConfig = new("driver", HomeCategory.Data, "data/driver.json");
    public static readonly HomeEntry Harnesses = new("harnesses", HomeCategory.Data, "data/harnesses.json");
    public static readonly HomeEntry Accounts = new("accounts", HomeCategory.Data, "data/accounts.json");
    public static readonly HomeEntry Cooling = new("cooling", HomeCategory.Data, "data/cooling.json");
    public static readonly HomeEntry Windows = new("windows", HomeCategory.Data, "data/windows.json");
    public static readonly HomeEntry Reads = new("reads", HomeCategory.Data, "data/reads.json");
    public static readonly HomeEntry Permissions = new("permissions", HomeCategory.Data, "data/permissions.json");
    public static readonly HomeEntry Files = new("files", HomeCategory.Data, "data/files.json");
    public static readonly HomeEntry FilesFound = new("files-found", HomeCategory.Data, "data/files-found.json");
    public static readonly HomeEntry FilesAsks = new("files-asks", HomeCategory.Data, "data/files-asks.json");
    public static readonly HomeEntry RuleProposals = new("rule-proposals", HomeCategory.Data, "data/proposals");
    public static readonly HomeEntry Tools = new("tools", HomeCategory.Data, "data/tools.json");
    public static readonly HomeEntry PluginState = new("plugin-state", HomeCategory.Data, "data/plugins.json");
    public static readonly HomeEntry Plugins = new("plugins", HomeCategory.Data, "data/plugins");
    public static readonly HomeEntry PluginApprovals = new("plugin-approvals", HomeCategory.Data, "data/plugin-approvals.json");
    public static readonly HomeEntry Usage = new("usage", HomeCategory.Data, "data/usage.json");
    public static readonly HomeEntry Landings = new("landings", HomeCategory.Data, "data/landings.json");
    public static readonly HomeEntry SessionBranches = new("session-branches", HomeCategory.Data, "data/session-branches.json");
    public static readonly HomeEntry LinesMoved = new("lines-moved", HomeCategory.Data, "data/lines-moved.json");
    public static readonly HomeEntry OverlapsTold = new("overlaps-told", HomeCategory.Data, "data/overlaps-told.json");
    public static readonly HomeEntry RegistryFollowed = new("registry-followed", HomeCategory.Data, "data/registry-followed.json");
    public static readonly HomeEntry Abandoned = new("abandoned", HomeCategory.Data, "data/abandoned.json");
    public static readonly HomeEntry Update = new("update", HomeCategory.Data, "data/update.json");
    public static readonly HomeEntry Knowledge = new("knowledge", HomeCategory.Data, "data/knowledge.db");
    // The desktop's own store, a database of the machine's, opened by the driver library alone.
    public static readonly HomeEntry DesktopDb = new("desktop-db", HomeCategory.Data, "data/desktop.db");
    public static readonly HomeEntry Memory = new("memory", HomeCategory.Data, "data/memory.db");
    public static readonly HomeEntry Sessions = new("sessions", HomeCategory.Data, "data/sessions");
    public static readonly HomeEntry Helpers = new("helpers", HomeCategory.Data, "data/helpers");
    public static readonly HomeEntry Partners = new("partners", HomeCategory.Data, "data/partners");
    public static readonly HomeEntry Checks = new("checks", HomeCategory.Data, "data/checks");
    public static readonly HomeEntry Handed = new("handed", HomeCategory.Data, "data/handed");
    public static readonly HomeEntry Setup = new("setup", HomeCategory.Data, "data/setup");
    public static readonly HomeEntry Workflows = new("workflows", HomeCategory.Data, "data/workflows");
    public static readonly HomeEntry ConfiguredAccess = new("configured-access", HomeCategory.Data, "data/configured-access.json");
    public static readonly HomeEntry OpinionGates = new("opinion-gates", HomeCategory.Data, "data/opinions/gates");
    public static readonly HomeEntry OpinionDues = new("opinion-dues", HomeCategory.Data, "data/opinions/due.json");
    public static readonly HomeEntry BrowserSettings = new("browser-settings", HomeCategory.Data, "data/browser/settings.json");
    public static readonly HomeEntry BrowserFavorites = new("browser-favorites", HomeCategory.Data, "data/browser/favorites.json");
    public static readonly HomeEntry HomeId = new("home-id", HomeCategory.Data, "data/home.json");
    public static readonly HomeEntry Brainstorms = new("brainstorms", HomeCategory.Data, "data/brainstorms");
    public static readonly HomeEntry UsageHistory = new("usage-history", HomeCategory.Data, "data/usage-history");
    public static readonly HomeEntry Keeping = new("keeping", HomeCategory.Data, "data/keeping.json");
    public static readonly HomeEntry Notices = new("notices", HomeCategory.Data, "data/notices.json");
    public static readonly HomeEntry CheckBudgets = new("check-budgets", HomeCategory.Data, "data/check-budgets.json");
    public static readonly HomeEntry GitIdentities = new("git-identities", HomeCategory.Data, "data/git-identities.json");
    // What each agent, version and account last offered and refused, a reading the walk judges.
    public static readonly HomeEntry AgentOffers = new("agent-offers", HomeCategory.Data, "data/agent-offers.json");
    // A terminal's presses waiting for the person, one record each, kept across a restart in
    // data/ (backed up), never run/ (anyone may clear it while Daoris is closed); never a key in it.
    public static readonly HomeEntry PipeWaiting = new("pipe-waiting", HomeCategory.Data, "data/pipe-waiting");
    // The bytes a file step of the act journal replaces and writes, one folder per act, let go of
    // with its record; data/ (backed up), since they are the person's files as they stood.
    public static readonly HomeEntry ActFiles = new("act-files", HomeCategory.Data, "data/act-files/*");
    // A place's apply written before its first effect, one file per plan's digest, each operation's
    // outcome kept there so a restart settles it; data/ (backed up), since it says what is done to the person's configuration.
    public static readonly HomeEntry PlaceReceipts = new("place-receipts", HomeCategory.Data, "data/place-receipts");
    // The steward desk's requests kept across a restart, an open one read as interrupted; no caller's key, no grant.
    public static readonly HomeEntry StewardDesk = new("steward-desk", HomeCategory.Data, "data/steward-desk.json");
    // The newest build that has run on the home, which the launcher refuses an older one by.
    public static readonly HomeEntry LastBuild = new("last-build", HomeCategory.Data, "data/last-build.json");
    public static readonly HomeEntry TerminalActs = new("terminal-acts", HomeCategory.Data, "data/terminal-acts.json");
    public static readonly HomeEntry UxTrail = new("ux-trail", HomeCategory.Data, "data/ux-trail");
    public static readonly HomeEntry Priority = new("priority", HomeCategory.Data, "data/priority.json");
    public static readonly HomeEntry Keys = new("keys", HomeCategory.Profiles, "profiles/keys.json");
    public static readonly HomeEntry Remotes = new("remotes", HomeCategory.Profiles, "profiles/remotes.json");
    public static readonly HomeEntry AgentHomes = new("agent-homes", HomeCategory.Profiles, "profiles/agents/*/*");
    public static readonly HomeEntry BrowserEngine = new("browser-engine", HomeCategory.Profiles, "profiles/browser/engine");
    public static readonly HomeEntry BrowserEdge = new("browser-edge", HomeCategory.Profiles, "profiles/browser/edge");
    public static readonly HomeEntry EdgeCookies = new("edge-cookies", HomeCategory.Profiles, "profiles/browser/edge-session-cookies.bin");
    public static readonly HomeEntry GitKeys = new("git-keys", HomeCategory.Profiles, "profiles/git/keys");
    public static readonly HomeEntry GitTokens = new("git-tokens", HomeCategory.Profiles, "profiles/git/tokens");
    public static readonly HomeEntry PluginPrivate = new("plugin-private", HomeCategory.Profiles, "profiles/plugins");
    public static readonly HomeEntry GitSshConfig = new("git-ssh-config", HomeCategory.Profiles, "profiles/git/ssh_config");
    public static readonly HomeEntry GitKnownHosts = new("git-known-hosts", HomeCategory.Profiles, "profiles/git/known_hosts");
    public static readonly HomeEntry GitConfig = new("git-config", HomeCategory.Profiles, "profiles/git/global.gitconfig");
    public static readonly HomeEntry Window = new("window", HomeCategory.Profiles, "profiles/window", "chromium", "config");
    // The keys of a terminal's sent presses, kept as long as a press's card lives; a file
    // that holds a key is a sign-in, never under run/, which anyone may clear while Daoris is closed.
    public static readonly HomeEntry PipeSent = new("pipe-sent", HomeCategory.Profiles, "profiles/pipe-sent");
    // The secret the dependency store's keys digest each install variable's value under: made once here, never synced or shown.
    public static readonly HomeEntry DepsSecret = new("deps-secret", HomeCategory.Profiles, "profiles/deps-secret");
    public static readonly HomeEntry Trees = new("trees", HomeCategory.Work, "work/trees/*/*/*");
    public static readonly HomeEntry HelpRoom = new("help-room", HomeCategory.Work, "work/rooms/help", "CLAUDE.md");
    public static readonly HomeEntry HelpRooms = new("help-rooms", HomeCategory.Work, "work/rooms/help-*", "CLAUDE.md");
    public static readonly HomeEntry Intake = new("intake", HomeCategory.Work, "work/rooms/intake/*");
    public static readonly HomeEntry OpinionPackets = new("opinion-packets", HomeCategory.Work, "work/opinions/*");
    public static readonly HomeEntry QuestFiles = new("quest-files", HomeCategory.Work, "work/files/quests/*");
    public static readonly HomeEntry AskFiles = new("ask-files", HomeCategory.Work, "work/files/asks/*");
    public static readonly HomeEntry SessionFiles = new("session-files", HomeCategory.Work, "work/files/sessions/*");
    public static readonly HomeEntry Proof = new("proof", HomeCategory.Work, "work/files/proof/*");
    public static readonly HomeEntry UsageRead = new("usage-read", HomeCategory.Work, "work/usage-read");
    public static readonly HomeEntry BrainstormRooms = new("brainstorm-rooms", HomeCategory.Work, "work/brainstorm-rooms/*");
    // A role holder's own folder, outside every repository, emptied at each appointment; the window alone writes there.
    public static readonly HomeEntry RoleRooms = new("role-rooms", HomeCategory.Work, "work/rooms/roles/*");
    // The dependency store's entries, one per key, and each tree's files record beside the others.
    public static readonly HomeEntry Deps = new("deps", HomeCategory.Work, "work/deps/*");
    public static readonly HomeEntry TreeFiles = new("tree-files", HomeCategory.Work, "work/files/trees/*");
    public static readonly HomeEntry Places = new("places", HomeCategory.Work, "work/places/*/*");
    public static readonly HomeEntry Plans = new("plans", HomeCategory.Work, "work/plans/*");
    public static readonly HomeEntry Evidence = new("evidence", HomeCategory.Work, "work/evidence/*");
    public static readonly HomeEntry Toolchain = new("toolchain", HomeCategory.Cache, "cache/toolchain");
    public static readonly HomeEntry ToolVersions = new("tool-versions", HomeCategory.Cache, "cache/tools");
    public static readonly HomeEntry Bin = new("bin", HomeCategory.Cache, "cache/bin");
    public static readonly HomeEntry HomeLease = new("home-lease", HomeCategory.Run, "run/home.lease");
    public static readonly HomeEntry Running = new("running", HomeCategory.Run, "run/running.json");
    public static readonly HomeEntry Locks = new("locks", HomeCategory.Run, "run/locks");
    public static readonly HomeEntry Spawn = new("spawn", HomeCategory.Run, "run/spawn");
    public static readonly HomeEntry Starts = new("starts", HomeCategory.Run, "run/starts");
    public static readonly HomeEntry Hooks = new("hooks", HomeCategory.Run, "run/hooks");
    public static readonly HomeEntry Probing = new("probing", HomeCategory.Run, "run/probing");
    public static readonly HomeEntry SessionPids = new("session-pids", HomeCategory.Run, "run/sessions/*.pid");
    public static readonly HomeEntry Requests = new("requests", HomeCategory.Run, "run/requests");
    public static readonly HomeEntry EdgeRecord = new("edge-record", HomeCategory.Run, "run/edge.json");
    public static readonly HomeEntry StopAsk = new("stop-ask", HomeCategory.Run, "run/stop-ask.json");
    public static readonly HomeEntry Pipe = new("pipe", HomeCategory.Run, "run/pipe");
    public static readonly HomeEntry GitHeld = new("git-held", HomeCategory.Run, "run/git/held.json");
    public static readonly HomeEntry GitGrants = new("git-grants", HomeCategory.Run, "run/git/grants");
    public static readonly HomeEntry GitUsed = new("git-used", HomeCategory.Run, "run/git/used");
    public static readonly HomeEntry GitAsks = new("git-asks", HomeCategory.Run, "run/git/asks");
    public static readonly HomeEntry GitCheckouts = new("git-checkouts", HomeCategory.Run, "run/git/checkouts.json");
    public static readonly HomeEntry GitLibrary = new("git-library", HomeCategory.Run, "run/git/library.json");
    public static readonly HomeEntry GitLibraryLeases = new("git-library-leases", HomeCategory.Run, "run/git/library-leases");
    public static readonly HomeEntry GitHome = new("git-home", HomeCategory.Run, "run/git/home");
    public static readonly HomeEntry GitSessions = new("git-sessions", HomeCategory.Run, "run/git/sessions");
    public static readonly HomeEntry ClockMark = new("clock-mark", HomeCategory.Run, "run/clock.mark");
    public static readonly HomeEntry Logs = new("logs", HomeCategory.Logs, "logs");

    /// <summary>Every entry, in the table's order, which the twins hold in the same order.</summary>
    public static IReadOnlyList<HomeEntry> Table { get; } =
    [
        DriverConfig, Harnesses, Accounts, Cooling, Windows, Reads, Permissions, Files, FilesFound, FilesAsks, RuleProposals, Tools, PluginState, Plugins,
        PluginApprovals, Usage,
        Landings, SessionBranches, LinesMoved, OverlapsTold, RegistryFollowed, Abandoned, Update, Knowledge, DesktopDb, Memory, Sessions, Helpers, Partners, Checks, Handed, Setup,
        Workflows, ConfiguredAccess, OpinionGates, OpinionDues, BrowserSettings, BrowserFavorites, HomeId, Brainstorms,
        UsageHistory, Keeping, Notices, CheckBudgets, GitIdentities, AgentOffers, PipeWaiting, ActFiles, PlaceReceipts, StewardDesk,
        LastBuild, TerminalActs, UxTrail, Priority,
        Keys, Remotes, AgentHomes, BrowserEngine, BrowserEdge, EdgeCookies, GitKeys, GitTokens, PluginPrivate, GitSshConfig, GitKnownHosts, GitConfig, Window, PipeSent,
        DepsSecret,
        Trees, HelpRoom, HelpRooms, Intake, OpinionPackets, QuestFiles, AskFiles, SessionFiles, Proof, UsageRead, BrainstormRooms, RoleRooms,
        Deps, TreeFiles, Places, Plans, Evidence,
        Toolchain, ToolVersions, Bin,
        HomeLease, Running, Locks, Spawn, Starts, Hooks, Probing, SessionPids, Requests, EdgeRecord, StopAsk, Pipe,
        GitHeld, GitGrants, GitUsed, GitAsks, GitCheckouts, GitLibrary, GitLibraryLeases, GitHome, GitSessions, ClockMark, Logs,
    ];

    /// <summary>The entry of this name, or null where the table holds none.</summary>
    public static HomeEntry? Named(string name) => Table.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
}
