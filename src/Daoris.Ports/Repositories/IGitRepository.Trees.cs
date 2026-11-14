namespace Daoris.Ports.Repositories;

/// <summary>One git an act's effect step asks, started suspended in the act's job.</summary>
/// <param name="Folder">The checkout it runs in.</param>
/// <param name="Ordinal">The step's place in its act, which the act's journal proves the git's pid under.</param>
/// <param name="Arguments">Git's arguments.</param>
/// <param name="Input">What is written to its standard input, or null.</param>
/// <param name="Environment">Variables set for this one git, or null.</param>
public sealed record GitHeldRun(string Folder, int Ordinal, IReadOnlyList<string> Arguments, string? Input, IReadOnlyDictionary<string, string>? Environment);

/// <summary>What one held git did: its exit, its words, and whether it outlived its bound or its end.</summary>
/// <param name="Exit">Its exit code, or null where it never ran or ended no way the act can read.</param>
/// <param name="Stdout">What it wrote.</param>
/// <param name="Stderr">What it said of itself.</param>
/// <param name="Unended">Whether its processes were still alive past the step's bound.</param>
/// <param name="Said">The act's own words for a start that did not happen or a stop that did not end.</param>
public sealed record GitHeldAnswer(int? Exit, string Stdout, string Stderr, bool Unended, string Said);

/// <summary>The process ring's start of one held git: the real repository calls it, a fake never does.</summary>
public delegate Task<GitHeldAnswer> GitHeldStart(GitHeldRun run);

/// <summary>Where a checkout's git directories stand, read as git reports them.</summary>
/// <param name="GitDir">The checkout's own git directory.</param>
/// <param name="CommonDir">The directory its refs and configuration are shared in.</param>
public sealed record GitLayout(string GitDir, string CommonDir);

public partial interface IGitRepository
{
    /// <summary>
    /// Runs one git of a journaled act's effect step. The real repository starts it through <paramref name="start"/>, which holds the act's
    /// job and journal and so proves the process; a fake answers the run itself and records it.
    /// </summary>
    Task<GitHeldAnswer> RunHeldAsync(GitHeldRun run, GitHeldStart start);

    /// <summary>Where the checkout's git directories stand; null where git could not say.</summary>
    Task<GitLayout?> LayoutAsync(string folder, CancellationToken ct);
}
