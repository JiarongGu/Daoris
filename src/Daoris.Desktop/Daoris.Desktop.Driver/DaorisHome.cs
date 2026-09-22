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
/// </remarks>
public static class DaorisHome
{
    public const string Variable = "DAORIS_HOME";

    /// <summary>The one sentence every refusal for a missing home carries.</summary>
    public const string Sentence =
        "no Daoris home: set DAORIS_HOME to the application's data folder (the `data` directory beside "
        + "daoris-desktop.exe), or name the file's own variable. Daoris keeps nothing under the user profile.";

    /// <summary>The home, or null when the environment names none. Blank is none.</summary>
    public static string? Resolve(Func<string, string?> environment)
    {
        var home = environment(Variable);
        return string.IsNullOrWhiteSpace(home) ? null : home.Trim();
    }

    /// <summary>The home from the process environment, or null.</summary>
    public static string? Resolve() => Resolve(Environment.GetEnvironmentVariable);

    /// <summary>A file under the home, or null when there is no home.</summary>
    public static string? File(Func<string, string?> environment, string name) =>
        Resolve(environment) is { } home ? Path.Combine(home, name) : null;

    /// <summary>A file under the process environment's home, or null.</summary>
    public static string? File(string name) => File(Environment.GetEnvironmentVariable, name);

    /// <summary>
    /// A file under the home, or a refusal naming the variable and the file — for the management
    /// class, which edits and therefore must know where.
    /// </summary>
    public static string Require(Func<string, string?> environment, string name) =>
        File(environment, name)
        ?? throw new DriverException($"{Sentence} (wanted: {name})");

    /// <summary>The same, from the process environment.</summary>
    public static string Require(string name) => Require(Environment.GetEnvironmentVariable, name);
}
