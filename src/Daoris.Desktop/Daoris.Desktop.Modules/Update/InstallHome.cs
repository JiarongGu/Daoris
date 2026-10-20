using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>What establishing an install's home did — for the person, once, on the channel they act on.</summary>
/// <param name="Home">The home: the install's `data/`, beside its launcher.</param>
/// <param name="SetForUser">True when the user's environment gained the variable — this start, not any earlier one.</param>
/// <param name="Moved">The entries that moved in from a `~/.daoris` of before D63, by name.</param>
/// <param name="Failed">The entries that should have moved and could not — still where they were, named with the reason.</param>
/// <param name="Notice">One sentence for the person, naming the home and anything that moved, could not, or was overridden.</param>
/// <param name="Overrode">
/// The home this start inherited from the account and did not use (HOME1, D105): another install's
/// `data/`, or this one's before it moved. Null when nothing was inherited, or it was this install's own.
/// </param>
/// <param name="Placing">What this start's move into D172's places did (HOMELAYOUT3), or null where none was tried.</param>
public sealed record HomeEstablished(
    string Home, bool SetForUser, IReadOnlyList<string> Moved, IReadOnlyList<string> Failed, string Notice,
    string? Overrode = null, HomePlaced? Placing = null)
{
    /// <summary>The entries this start renamed into their D172 places (HOMELAYOUT3).</summary>
    public IReadOnlyList<HomeMove> Placed => Placing?.Moved ?? [];

    /// <summary>The entries that should have moved into their places and could not, each still read where it was, with why.</summary>
    public IReadOnlyList<HomeMove> NotPlaced => Placing?.Failed ?? [];

    /// <summary>Why this start tried no move at all (an update not settled, another process of the folder, the lease held), or null.</summary>
    public string? PlaceHeld => Placing?.Held;

    /// <summary>
    /// True when the person should hear about it: something moved, something could not, their
    /// account's environment changed, or the home their account names is not the one this window runs on. A move held off for
    /// this start is the machine log's to say, not the window's: the next start tries again.
    /// </summary>
    public bool Worth => SetForUser || Overrode is not null || Moved.Count > 0 || Failed.Count > 0
        || Placed.Count > 0 || NotPlaced.Count > 0;
}

/// <summary>
/// How the home this process runs on stands to the account's <c>DAORIS_HOME</c>, which is the folder a terminal's
/// <c>daoris</c> reads (LEFT2) — what Settings → Driver's home hint says of a terminal, from a field rather than
/// from the notice's English.
/// </summary>
/// <remarks>
/// Named for the relation rather than for one of its outcomes, because there are three: a start named for itself
/// alone is neither the account's home nor an override of it, and D105 writes no notice for it.
/// </remarks>
public static class HomeAccount
{
    /// <summary>The account's variable names this home, so a terminal's daoris reads it too.</summary>
    public const string Same = "same";

    /// <summary>This install's own <c>data/</c> replaced another folder the account's variable names (D105); the notice says which.</summary>
    public const string Overridden = "overridden";

    /// <summary>
    /// The home was named for this start alone — a gate's scratch home, a terminal's one-off, the dev loop's — and the
    /// account's variable names another folder or none. A terminal's daoris does not read this one.
    /// </summary>
    public const string ThisStart = "this-start";
}

/// <summary>
/// The installed application's own home (D63). The install's `data` folder, beside its launcher, is the Daoris
/// home: this process and everything it spawns read it from <c>DAORIS_HOME</c>, and the user's
/// environment is offered the same variable so a terminal's <c>daoris</c> and a session's MCP host
/// meet the same machine. <b>Nothing goes under the user profile</b> — not a file, not a pointer.
/// </summary>
/// <remarks>
/// <para>Runs before anything else the shell constructs, because <see cref="DriverConfig"/> and the
/// harness roster capture their path at construction — the home has to exist before they ask.</para>
///
/// <para><b>The install's own `data/` wins over a home inherited from the account</b> (HOME1, D105): a
/// second or moved install runs on its own, leaves the account's variable as it is, and says so. A
/// home named for this start alone — not the account's — is still respected whole.</para>
///
/// <para>A workspace build is not an install and is left alone: the dev loop names its scratch home
/// itself, and a gate that redirected everything but this would still write into a real folder.
/// What tells the two apart is the publish marker, which only <c>desktop-publish</c> writes.</para>
///
/// <para>Every effect is injected so the tests set nothing on the machine that runs them.</para>
/// </remarks>
public static class InstallHome
{
    /// <summary>The file `desktop-publish` writes at an install's root, and nothing else does.</summary>
    public const string Marker = DaorisHome.InstallMarker;

    /// <summary>The home's name, beside the executable.</summary>
    public const string Folder = DaorisHome.InstallData;

    /// <summary>What a `~/.daoris` of before D63 keeps: the CLI's own service install (`publish:service --install`), which the CLI moves when it is asked to.</summary>
    private static readonly HashSet<string> Stays = new(StringComparer.OrdinalIgnoreCase) { "bin" };

    /// <summary>The folder the shell runs from inside an install (D93), beside `data/`.</summary>
    public const string AppFolder = DaorisHome.InstallApp;

    /// <summary>
    /// What the install's own runtime writes under `data/` before Daoris has: the engine's profile
    /// (`chromium`, and `webview2` from before D92) and the window's geometry; and what a verb the install carries may leave
    /// before the first start, the home's id and its lease (HOMEID1, HOMELEASE1). Their presence does not make the home a
    /// home that has state.
    /// </summary>
    private static readonly HashSet<string> Runtime = new(StringComparer.OrdinalIgnoreCase)
    {
        "config", "webview2", "chromium", HomeIdentity.FileName, HomeLease.FileName,
    };

    /// <summary>
    /// The install a shell runs in (D93): its own folder when that holds the marker, the folder above
    /// when the shell runs from a marked install's `app/`, where the launcher at the root starts it —
    /// and its own folder otherwise, which is no install.
    /// </summary>
    public static string RootOf(string baseDirectory)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        if (File.Exists(Path.Combine(full, Marker))) return full;
        return string.Equals(Path.GetFileName(full), AppFolder, StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(full) is { } parent
            && File.Exists(Path.Combine(parent, Marker))
                ? parent
                : full;
    }

    /// <summary>
    /// The folder a shell running from an install's <c>app/</c> stands in instead (APPCWD1, D139 §5): the install's root, while
    /// it stands in <c>app/</c> or below it, as the launcher started it. Null otherwise — a workspace build, or a start given a
    /// working folder elsewhere — which is left as it was.
    /// </summary>
    /// <remarks>
    /// Every child started without a working folder of its own inherits the shell's, and on Windows a process's working folder
    /// keeps the folders above it from being renamed while it lives: a browser, a harness or a tool started from <c>app/</c>,
    /// running from somewhere else, held the swap's move of <c>app/</c> and was not among what the launcher waits for.
    /// </remarks>
    public static string? WorkingFolder(string baseDirectory, string current)
    {
        var root = RootOf(baseDirectory);
        var app = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        if (string.Equals(root, app, StringComparison.OrdinalIgnoreCase)) return null;
        var here = Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
        return string.Equals(here, app, StringComparison.OrdinalIgnoreCase)
            || here.StartsWith(app + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? root
                : null;
    }

    /// <summary>
    /// Establish the home for an install, or answer null when this is not one, when the environment
    /// already names this install's own home, or when it names a home for this start alone — which is
    /// respected whole. A home the environment only inherited from the account, naming another folder,
    /// is overridden: the install's own `data/` wins (HOME1, D105).
    /// </summary>
    /// <param name="leaseWait">
    /// How long the move waits for the home's lease while another act holds it (HOMELEASE1); <see cref="HomeLease.Wait"/>.
    /// </param>
    /// <param name="othersRunning">
    /// The processes of the install's folder other than this one and its launcher, which hold the move into D172's places off
    /// (HOMELAYOUT3); <see cref="HomePlacing.OthersRunningFrom"/>.
    /// </param>
    public static HomeEstablished? Establish(
        string baseDirectory,
        Func<string, string?> processEnvironment,
        Action<string, string> setProcess,
        Func<string?> userEnvironment,
        Action<string> setUser,
        string legacy,
        TimeSpan? leaseWait = null,
        Func<string, IReadOnlyList<int>>? othersRunning = null)
    {
        var root = RootOf(baseDirectory);
        if (!File.Exists(Path.Combine(root, Marker))) return null;

        var home = Path.Combine(root, Folder);
        var account = userEnvironment();
        string? overrode = null;
        if (DaorisHome.Resolve(processEnvironment) is { } named)
        {
            // The install's own home, as the account names it: nothing to establish, and its move into D172's places runs on it
            // all the same (HOMELAYOUT3), said where it moved or could not.
            if (SamePath(named, home))
            {
                var placing = HomePlacing.Place(root, home, othersRunning ?? HomePlacing.OthersRunningFrom);
                return placing.Any
                    ? new HomeEstablished(home, false, [], [], Said($"Daoris home: {home}", placing) + ".", Placing: placing)
                    : null;
            }

            // 🔴 HOME1: the account's variable reaches every process the person starts, so a second or
            // moved install inherited the first one's home and ran on it without a word. A value that
            // equals the account's was inherited, and the install's own `data/` wins over it. A value
            // that differs was named for this start alone — a gate's scratch home, a terminal's
            // one-off — and is respected, or no gate could keep an install off the real machine.
            if (!SamePath(named, account)) return null;
            overrode = named;
        }

        Directory.CreateDirectory(home);
        setProcess(DaorisHome.Variable, home);

        // Once, and never over one the person set themselves: a user variable is theirs to change,
        // and a shell that reset it on every start would make the setting impossible to keep. That
        // holds for an overridden one too (D105): rewriting it would move every terminal to whichever
        // install was opened last, so it is left, and the notice says a terminal still reads it.
        var setForUser = false;
        if (string.IsNullOrWhiteSpace(account))
        {
            setUser(home);
            setForUser = true;
        }

        var (moved, failed) = HasState(home) ? ([], []) : Migrate(legacy, home, leaseWait ?? HomeLease.Wait);

        // Then the move into D172's places (HOMELAYOUT3, the design §7.6): what D63's move-in laid at the old places is moved on
        // with the rest, under the same rules.
        var placed = HomePlacing.Place(root, home, othersRunning ?? HomePlacing.OthersRunningFrom);

        var notice = $"Daoris home: {home}";
        if (overrode is not null)
        {
            notice += $" — this install's own data folder, not {overrode}, which {DaorisHome.Variable} names for your "
                + $"account; that variable is left as it is, so a terminal's daoris still reads {overrode} until you change it";
        }
        if (moved.Count > 0) notice += $" — moved in from {legacy}: {string.Join(", ", moved)}";
        if (failed.Count > 0) notice += $" — could not move {string.Join(", ", failed)}; still at {legacy}";
        notice = Said(notice, placed);
        if (setForUser) notice += $" — {DaorisHome.Variable} set for your account, so a terminal's daoris sees the same machine";

        return new HomeEstablished(home, setForUser, moved, failed, notice + ".", overrode, placed.Any ? placed : null);
    }

    /// <summary>The notice with what the move into D172's places did added, once (the design §7.5), as D63's move-in is said.</summary>
    private static string Said(string notice, HomePlaced placed)
    {
        if (placed.Moved.Count > 0)
        {
            notice += $" — moved into the Daoris folder's own folders (D172): {string.Join(", ", placed.Moved.Select(move => move.Entry))}";
        }

        if (placed.Failed.Count > 0)
        {
            notice += $" — could not move {string.Join("; ", placed.Failed.Select(move => $"{move.Entry} ({move.Reason})"))}; each is "
                + "read where it is, and the next start tries again";
        }

        return notice;
    }

    /// <summary>
    /// What the move into D172's places did, into the machine log (the design §7.5, the machine log design §4): a
    /// <c>home.moved</c> line for each rename, a <c>home.move-failed</c> line for each that could not be, a
    /// <c>home.move-recovered</c> line for each a stopped start left and this one put back, and <c>home.move-held</c> where
    /// nothing was tried. Written once the log is open, which the move ran before, since the log is one of the entries it moves.
    /// </summary>
    public static void Log(HomeEstablished? established, MachineLog log)
    {
        if (established?.Placing is not { } placed) return;
        foreach (var move in placed.Moved) log.Info("home.moved", ("entry", move.Entry), ("from", move.From), ("to", move.To));
        foreach (var move in placed.Recovered) log.Info("home.move-recovered", ("entry", move.Entry), ("from", move.From), ("to", move.To));
        foreach (var move in placed.Failed) log.Warn("home.move-failed", ("entry", move.Entry), ("reason", move.Reason));
        if (placed.Held is { } held) log.Info("home.move-held", ("reason", held));
    }

    /// <summary>
    /// The window's memory, the UI kit's data area (D172 point 4, the design §6.2), for a kit rooted at <paramref name="root"/>:
    /// where that root is an install, its home's <see cref="HomeEntry.Window"/>, <c>profiles/window/</c> once moved and the
    /// install's <c>data/</c> while its <c>chromium/</c> or <c>config/</c> is still there; elsewhere null, the kit's own.
    /// </summary>
    public static string? WindowMemory(string root) =>
        File.Exists(Path.Combine(root, Marker)) ? DaorisHome.Entry(root, HomeEntry.Window) : null;

    /// <summary>
    /// The window's memory for a run whose one root was named with <c>--app-root</c> (HOMELAYOUT4, the design §6.1): a
    /// development run's scratch Daoris folder, which is no install and is its own home, so the same entry as an install's.
    /// </summary>
    public static string WindowMemoryOfRun(string root) => DaorisHome.Entry(root, HomeEntry.Window);

    /// <summary>The folder a <c>--app-root</c> argument names (<c>--app-root &lt;folder&gt;</c> or <c>--app-root=&lt;folder&gt;</c>), whole; null when none is given.</summary>
    public static string? AppRootOf(IReadOnlyList<string> args)
    {
        const string name = "--app-root";
        for (var at = 0; at < args.Count; at++)
        {
            string? value = null;
            if (args[at] == name) value = at + 1 < args.Count ? args[at + 1] : null;
            else if (args[at].StartsWith(name + "=", StringComparison.Ordinal)) value = args[at][(name.Length + 1)..];
            if (!string.IsNullOrWhiteSpace(value)) return Path.GetFullPath(value);
        }

        return null;
    }

    /// <summary>
    /// How <paramref name="home"/>, the home this process runs on once established, stands to <paramref name="account"/>,
    /// the account's variable as it now stands (LEFT2) — a <see cref="HomeAccount"/> value.
    /// </summary>
    /// <remarks>
    /// An override is said as one, since the notice then names the folder a terminal reads. Otherwise the two
    /// spellings are compared as <see cref="Establish"/> compares them: the same folder, or a home this start named
    /// for itself. No variable for the account is not this folder, whatever the process was handed.
    /// </remarks>
    public static string AccountOf(string home, HomeEstablished? established, string? account) =>
        established?.Overrode is not null ? HomeAccount.Overridden
        : SamePath(home, account) ? HomeAccount.Same
        : HomeAccount.ThisStart;

    /// <summary>
    /// The account's own <c>DAORIS_HOME</c>, which every process the person starts inherits — or null off Windows,
    /// where the user-level store is not a notion (the shell is Windows-only, D63).
    /// </summary>
    public static string? AccountVariable() => OperatingSystem.IsWindows()
        ? Environment.GetEnvironmentVariable(DaorisHome.Variable, EnvironmentVariableTarget.User)
        : null;

    /// <summary>
    /// Two spellings of one folder: full, without a trailing separator, and without regard to case
    /// where the file system has none — a variable a person typed need not match the path this built.
    /// </summary>
    private static bool SamePath(string a, string? b)
    {
        if (string.IsNullOrWhiteSpace(b)) return false;
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(a.Trim())),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(b.Trim())),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A value that is no path at all is not this folder.
            return false;
        }
    }

    /// <summary>The same, against this process, this user, the profile directory a previous version used, and this machine's processes.</summary>
    public static HomeEstablished? Establish(string baseDirectory) => Establish(
        baseDirectory,
        Environment.GetEnvironmentVariable,
        (name, value) => Environment.SetEnvironmentVariable(name, value),
        AccountVariable,
        value =>
        {
            // The user-level store is a Windows notion; elsewhere the person's shell profile is
            // theirs, and the notice tells them what to set.
            if (OperatingSystem.IsWindows())
                Environment.SetEnvironmentVariable(DaorisHome.Variable, value, EnvironmentVariableTarget.User);
        },
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris"),
        othersRunning: HomePlacing.OthersRunningFrom);

    private static bool HasState(string home) =>
        Directory.EnumerateFileSystemEntries(home)
            .Select(Path.GetFileName)
            .Any(name => name is not null && !Runtime.Contains(name));

    /// <summary>
    /// The move, under the home's lease (HOMELEASE1, the terminal RPC design §5): taken for the move alone, so no verb acting
    /// alone writes beside it, and waited for up to <paramref name="within"/> where another act holds it; still held then,
    /// nothing moves, and the holder is said as what could not move and why. Judged again under the lease, since another start
    /// may have moved it meanwhile.
    /// </summary>
    private static (List<string> Moved, List<string> Failed) Migrate(string legacy, string home, TimeSpan within)
    {
        if (!Directory.Exists(legacy)) return ([], []);

        try
        {
            using var lease = HomeLease.Await(home, LeaseKind.Migration, null, within, out var holder);
            if (lease is null) return ([], [$"anything, since {holder!.Named} held this home; the next start moves it"]);
            return HasState(home) ? ([], []) : MoveIn(legacy, home);
        }
        catch (HomeLeaseException error)
        {
            return ([], [$"anything ({error.Message.TrimEnd('.')})"]);
        }
    }

    /// <summary>
    /// Move a legacy profile directory's state in — every entry but the ones that stay — one by one,
    /// so a file something still holds open costs that file and not the start.
    /// </summary>
    private static (List<string> Moved, List<string> Failed) MoveIn(string legacy, string home)
    {
        var moved = new List<string>();
        var failed = new List<string>();
        if (!Directory.Exists(legacy)) return (moved, failed);

        foreach (var entry in Directory.EnumerateFileSystemEntries(legacy))
        {
            var name = Path.GetFileName(entry);
            if (Stays.Contains(name)) continue;

            try
            {
                var target = Path.Combine(home, name);
                if (Directory.Exists(entry)) MoveDirectory(entry, target);
                else File.Move(entry, target, overwrite: false);
                moved.Add(name);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                failed.Add($"{name} ({error.Message.TrimEnd('.')})");
            }
        }

        return (moved, failed);
    }

    /// <summary>
    /// A move that crosses volumes — `Directory.Move` refuses one, and an install on another drive
    /// than the profile is the ordinary case, not the odd one. Copy, then remove the source only once
    /// every file of it has landed.
    /// </summary>
    private static void MoveDirectory(string source, string target)
    {
        if (string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(source, target);
            return;
        }

        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: false);
        Directory.Delete(source, recursive: true);
    }
}
