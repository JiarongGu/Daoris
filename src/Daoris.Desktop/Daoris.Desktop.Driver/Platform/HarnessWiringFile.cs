namespace Daoris.Driver;

/// <summary>
/// Where the agents' wiring file is (FOUNDATION1 mod-v, the design §14.3: the type moved down to its one owner): the override's variable
/// and the file of a home, which the install's download clearing names beside the tools' own, so a subdomain below the agents reads the
/// path without naming their settings. <see cref="HarnessSettings"/> answers through it, so every reader keeps its spelling.
/// </summary>
public static class HarnessWiringFile
{
    /// <summary>The variable that names the file in place of the home's own.</summary>
    public const string PathVariable = "DAORIS_HARNESS_CONFIG";

    /// <summary>
    /// The file for a reader handed its home (a driver, a loop, a chat runner): the override where set, as every surface reads
    /// it, else that home's own file, never the one the process's home variable names.
    /// </summary>
    public static string ResolvePath(string home) =>
        Environment.GetEnvironmentVariable(PathVariable) ?? DaorisHome.Entry(home, HomeEntry.Harnesses);
}
