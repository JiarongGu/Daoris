namespace Daoris.Knowledge;

/// <summary>
/// Where every machine-local file Daoris owns lives (D63): one home, named by <c>DAORIS_HOME</c>, and
/// <b>no default under the user profile</b>. The application's own folder is the home — the installed
/// desktop sets the variable for itself and everything it spawns — and a terminal or a session's MCP
/// host reads the same variable from the user's environment. A host the install carries, started with none named, takes
/// the install's own <c>data/</c> (<see cref="Adopt(string)"/>, SERVICEHOME2). Absent both, there is no home: a host refuses
/// to start with <see cref="Sentence"/> rather than writing somewhere nobody pointed it.
/// </summary>
/// <remarks>
/// One of three twins — the CLI's <c>home.ts</c> and the driver's <c>DaorisHome</c> hold the same
/// contract in their own languages and share no code, the way the remotes map's three copies do
/// (WSP3). The environment is the contract; the three tests move together.
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

    /// <summary>
    /// The file `desktop-publish` writes at an install's root, and nothing else does: the driver's
    /// <c>DaorisHome.InstallMarker</c> and the CLI's <c>INSTALL_MARKER</c>, duplicated deliberately (twins).
    /// </summary>
    public const string InstallMarker = "INSTALLED.md";

    /// <summary>The folder the install's programs run from (D93), beside <see cref="InstallData"/>.</summary>
    public const string InstallApp = "app";

    /// <summary>The install's own home (D63), beside <see cref="InstallApp"/>.</summary>
    public const string InstallData = "data";

    /// <summary>
    /// The install a program runs from (SERVICEHOME2, D171 point 1, as the driver's FINDDAORIS1): the folder above the
    /// <c>app/</c> it runs in, however deep (the connector is <c>app/daoris-knowledge/</c>, the HTTP host
    /// <c>app/daoris-knowledge-http/</c>), where that folder holds the marker. Null for a workspace build, which is no install.
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
    /// The home a host finds with nobody to tell it (SERVICEHOME2): the variable, which still wins (the desktop names it for
    /// the hosts it starts, as do the dev loop, a rehearsal and a second home), else its install's own <c>data/</c>, else none.
    /// </summary>
    public static string? Resolve(Func<string, string?> environment, string baseDirectory) =>
        Resolve(environment) ?? OfInstall(baseDirectory);

    /// <summary>
    /// Take the install's home as this process's <c>DAORIS_HOME</c> where none is named (SERVICEHOME2), as the desktop does
    /// for itself (D63): a connector a person's own agent starts from its <c>.mcp.json</c>, or a host started from a
    /// terminal, then keeps its index, its machine log and its quest files on the install's machine. The home it set, or
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

    /// <summary>A file under the home, or null when there is no home.</summary>
    public static string? File(Func<string, string?> environment, string name) =>
        Resolve(environment) is { } home ? Path.Combine(home, name) : null;

    /// <summary>A file under the process environment's home, or null.</summary>
    public static string? File(string name) => File(Environment.GetEnvironmentVariable, name);
}
