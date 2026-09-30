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
public sealed record HomeEstablished(
    string Home, bool SetForUser, IReadOnlyList<string> Moved, IReadOnlyList<string> Failed, string Notice,
    string? Overrode = null)
{
    /// <summary>
    /// True when the person should hear about it: something moved, something could not, their
    /// account's environment changed, or the home their account names is not the one this window runs on.
    /// </summary>
    public bool Worth => SetForUser || Overrode is not null || Moved.Count > 0 || Failed.Count > 0;
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
    public const string Marker = "INSTALLED.md";

    /// <summary>The home's name, beside the executable.</summary>
    public const string Folder = "data";

    /// <summary>What a `~/.daoris` of before D63 keeps: the CLI's own service install (`publish:service --install`), which the CLI moves when it is asked to.</summary>
    private static readonly HashSet<string> Stays = new(StringComparer.OrdinalIgnoreCase) { "bin" };

    /// <summary>The folder the shell runs from inside an install (D93), beside `data/`.</summary>
    public const string AppFolder = "app";

    /// <summary>
    /// What the install's own runtime writes under `data/` before Daoris has: the engine's profile
    /// (`chromium`, and `webview2` from before D92) and the window's geometry. Their presence does not
    /// make the home a home that has state.
    /// </summary>
    private static readonly HashSet<string> Runtime = new(StringComparer.OrdinalIgnoreCase) { "config", "webview2", "chromium" };

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
    /// Establish the home for an install, or answer null when this is not one, when the environment
    /// already names this install's own home, or when it names a home for this start alone — which is
    /// respected whole. A home the environment only inherited from the account, naming another folder,
    /// is overridden: the install's own `data/` wins (HOME1, D105).
    /// </summary>
    public static HomeEstablished? Establish(
        string baseDirectory,
        Func<string, string?> processEnvironment,
        Action<string, string> setProcess,
        Func<string?> userEnvironment,
        Action<string> setUser,
        string legacy)
    {
        var root = RootOf(baseDirectory);
        if (!File.Exists(Path.Combine(root, Marker))) return null;

        var home = Path.Combine(root, Folder);
        var account = userEnvironment();
        string? overrode = null;
        if (DaorisHome.Resolve(processEnvironment) is { } named)
        {
            if (SamePath(named, home)) return null;

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

        var (moved, failed) = HasState(home) ? ([], []) : MoveIn(legacy, home);

        var notice = $"Daoris home: {home}";
        if (overrode is not null)
        {
            notice += $" — this install's own data folder, not {overrode}, which {DaorisHome.Variable} names for your "
                + $"account; that variable is left as it is, so a terminal's daoris still reads {overrode} until you change it";
        }
        if (moved.Count > 0) notice += $" — moved in from {legacy}: {string.Join(", ", moved)}";
        if (failed.Count > 0) notice += $" — could not move {string.Join(", ", failed)}; still at {legacy}";
        if (setForUser) notice += $" — {DaorisHome.Variable} set for your account, so a terminal's daoris sees the same machine";

        return new HomeEstablished(home, setForUser, moved, failed, notice + ".", overrode);
    }

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

    /// <summary>The same, against this process, this user, and the profile directory a previous version used.</summary>
    public static HomeEstablished? Establish(string baseDirectory) => Establish(
        baseDirectory,
        Environment.GetEnvironmentVariable,
        (name, value) => Environment.SetEnvironmentVariable(name, value),
        () => OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable(DaorisHome.Variable, EnvironmentVariableTarget.User)
            : null,
        value =>
        {
            // The user-level store is a Windows notion; elsewhere the person's shell profile is
            // theirs, and the notice tells them what to set.
            if (OperatingSystem.IsWindows())
                Environment.SetEnvironmentVariable(DaorisHome.Variable, value, EnvironmentVariableTarget.User);
        },
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris"));

    private static bool HasState(string home) =>
        Directory.EnumerateFileSystemEntries(home)
            .Select(Path.GetFileName)
            .Any(name => name is not null && !Runtime.Contains(name));

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
