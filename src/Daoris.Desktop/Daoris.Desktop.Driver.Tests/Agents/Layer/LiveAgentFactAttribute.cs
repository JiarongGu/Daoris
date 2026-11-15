namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A test that runs a real agent's program on this machine, under the person's own sign-in and against its maker's service: skipped, saying
/// why, where the program is not on the path or real model turns are not asked for, by the suite's one switch for them
/// (<see cref="RealReaderTheoryAttribute.Variable"/>), since each run spends the account's allowance.
/// </summary>
internal sealed class LiveAgentFactAttribute : FactAttribute
{
    /// <param name="program">The program's file name as the path names it, without its extension.</param>
    /// <param name="key">A variable the run needs set, where the agent is held by a key alone; none where its sign-in is its own.</param>
    public LiveAgentFactAttribute(string program, string? key = null)
    {
        if (Environment.GetEnvironmentVariable(RealReaderTheoryAttribute.Variable) != "1")
        {
            Skip = $"a real model's turn on the person's own sign-in: set {RealReaderTheoryAttribute.Variable}=1 to run it";
        }
        else if (Located(program) is null)
        {
            Skip = $"`{program}` is not on this machine's path";
        }
        else if (key is not null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Skip = $"`{program}` is held by a key alone: set {key} to run it";
        }
    }

    /// <summary>The program's file on the path, the first of an executable, a command shim or a bare file; null where none is.</summary>
    public static string? Located(string program) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .SelectMany(folder => new[] { ".exe", ".cmd", "" }.Select(extension => Path.Combine(folder.Trim('"'), program + extension)))
        .FirstOrDefault(File.Exists);
}
