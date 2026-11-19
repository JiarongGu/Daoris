using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>What establishing an install's home did — for the person, once, on the channel they act on.</summary>
/// <param name="Home">The home: the install's `data/`, beside its launcher.</param>
/// <param name="SetForUser">True when the user's environment gained the variable — this start, not any earlier one.</param>
/// <param name="Notice">One sentence for the person, naming the home and whether it is overridden.</param>
/// <param name="Overrode">
/// The home this start inherited from the account and did not use: another install's
/// `data/`, or this one's before it moved. Null when nothing is inherited, or it is this install's own.
/// </param>
public sealed record HomeEstablished(string Home, bool SetForUser, string Notice, string? Overrode = null)
{
    /// <summary>
    /// True when the person should hear about it: their account's environment changed, or the home their account names is not the
    /// one this window runs on.
    /// </summary>
    public bool Worth => SetForUser || Overrode is not null;
}

/// <summary>
/// How the home this process runs on stands to the account's <c>DAORIS_HOME</c>, which is the folder a terminal's
/// <c>daoris</c> reads — what Settings → This Daoris's terminal row says of a terminal, from a field rather than
/// from the notice's English.
/// </summary>
/// <remarks>
/// Named for the relation rather than for one of its outcomes, because there are three: a start named for itself
/// alone is neither the account's home nor an override of it, and no notice is written for it.
/// </remarks>
public static class HomeAccount
{
    /// <summary>The account's variable names this home, so a terminal's daoris reads it too.</summary>
    public const string Same = "same";

    /// <summary>This install's own <c>data/</c> replaced another folder the account's variable names; the notice says which.</summary>
    public const string Overridden = "overridden";

    /// <summary>
    /// The home is named for this start alone — a gate's scratch home, a terminal's one-off, the dev loop's — and the
    /// account's variable names another folder or none. A terminal's daoris does not read this one.
    /// </summary>
    public const string ThisStart = "this-start";
}

/// <summary>
/// The installed application's own home. The install's `data` folder, beside its launcher, is the Daoris
/// home: this process and everything it spawns read it from <c>DAORIS_HOME</c>, and the user's
/// environment is offered the same variable so a terminal's <c>daoris</c> and a session's MCP host
/// meet the same machine. <b>Nothing goes under the user profile</b> — not a file, not a pointer.
/// </summary>
/// <remarks>
/// <para>Runs before anything else the shell constructs, because <see cref="DriverConfig"/> and the
/// harness roster capture their path at construction — the home has to exist before they ask.</para>
///
/// <para><b>The install's own `data/` wins over a home inherited from the account</b>: a
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

    /// <summary>The folder the shell runs from inside an install, beside `data/`.</summary>
    public const string AppFolder = DaorisHome.InstallApp;

    /// <summary>
    /// The install a shell runs in: its own folder when that holds the marker, the folder above
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
    /// The folder a shell running from an install's <c>app/</c> stands in instead: the install's root, while
    /// it stands in <c>app/</c> or below it, as the launcher started it. Null otherwise — a workspace build, or a start given a
    /// working folder elsewhere — which is left alone.
    /// </summary>
    /// <remarks>
    /// Every child started without a working folder of its own inherits the shell's, and on Windows a process's working folder
    /// keeps the folders above it from being renamed while it lives: a browser, a harness or a tool started from <c>app/</c>,
    /// running from somewhere else, holds the swap's move of <c>app/</c> and is not among what the launcher waits for.
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
    /// is overridden: the install's own `data/` wins.
    /// </summary>
    public static HomeEstablished? Establish(
        string baseDirectory,
        Func<string, string?> processEnvironment,
        Action<string, string> setProcess,
        Func<string?> userEnvironment,
        Action<string> setUser)
    {
        var root = RootOf(baseDirectory);
        if (!File.Exists(Path.Combine(root, Marker))) return null;

        var home = Path.Combine(root, Folder);
        var account = userEnvironment();
        string? overrode = null;
        if (DaorisHome.Resolve(processEnvironment) is { } named)
        {
            // The install's own home, as the account names it: nothing to establish.
            if (SamePath(named, home)) return null;

            // The account's variable reaches every process the person starts, so a second or
            // moved install inherited the first one's home and ran on it without a word. A value that
            // equals the account's is inherited, and the install's own `data/` wins over it. A value
            // that differs is named for this start alone — a gate's scratch home, a terminal's
            // one-off — and is respected, or no gate could keep an install off the real machine.
            if (!SamePath(named, account)) return null;
            overrode = named;
        }

        Directory.CreateDirectory(home);
        setProcess(DaorisHome.Variable, home);

        // Once, and never over one the person set themselves: a user variable is theirs to change,
        // and a shell that reset it on every start would make the setting impossible to keep. That
        // holds for an overridden one too: rewriting it would move every terminal to whichever
        // install is opened last, so it is left, and the notice says a terminal still reads it.
        var setForUser = false;
        if (string.IsNullOrWhiteSpace(account))
        {
            setUser(home);
            setForUser = true;
        }

        var notice = $"Daoris home: {home}";
        if (overrode is not null)
        {
            notice += $" — this install's own data folder, not {overrode}, which {DaorisHome.Variable} names for your "
                + $"account; that variable is left as it is, so a terminal's daoris still reads {overrode} until you change it";
        }
        if (setForUser) notice += $" — {DaorisHome.Variable} set for your account, so a terminal's daoris sees the same machine";

        return new HomeEstablished(home, setForUser, notice + ".", overrode);
    }

    /// <summary>
    /// The window's memory, the UI kit's data area, for a kit rooted at <paramref name="root"/>:
    /// where that root is an install, its home's <see cref="HomeEntry.Window"/>, <c>profiles/window/</c> once moved and the
    /// install's <c>data/</c> while its <c>chromium/</c> or <c>config/</c> is still there; elsewhere null, the kit's own.
    /// </summary>
    public static string? WindowMemory(string root) =>
        File.Exists(Path.Combine(root, Marker)) ? DaorisHome.Entry(root, HomeEntry.Window) : null;

    /// <summary>
    /// The window's memory for a run whose one root is named with <c>--app-root</c>: a
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
    /// the account's variable as it now stands — a <see cref="HomeAccount"/> value.
    /// </summary>
    /// <remarks>
    /// An override is said as one, since the notice then names the folder a terminal reads. Otherwise the two
    /// spellings are compared as <see cref="Establish"/> compares them: the same folder, or a home this start named
    /// for itself. No variable for the account is not this folder, whatever the process is handed.
    /// </remarks>
    public static string AccountOf(string home, HomeEstablished? established, string? account) =>
        established?.Overrode is not null ? HomeAccount.Overridden
        : SamePath(home, account) ? HomeAccount.Same
        : HomeAccount.ThisStart;

    /// <summary>
    /// The account's own <c>DAORIS_HOME</c>, which every process the person starts inherits — or null off Windows,
    /// where the user-level store is not a notion (the shell is Windows-only).
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

    /// <summary>The same, against this process and this user.</summary>
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
        });
}
