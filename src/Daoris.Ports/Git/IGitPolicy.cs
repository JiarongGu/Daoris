namespace Daoris.Ports.Git;

/// <summary>Which of the git port's runs is asked, which the application's policy reads to count, place and classify it.</summary>
public enum GitRunKind
{
    /// <summary>Read to its end, both streams whole.</summary>
    Whole,

    /// <summary>Read as it answers, stopped once its reader has what it needs.</summary>
    Streamed,

    /// <summary>Its first bytes, as bytes.</summary>
    Bytes,
}

/// <summary>
/// How one git starts, as the application resolved it: the program by its whole path, every argument it takes, the variables it takes
/// beside the host's, the bound it runs under, or the refusal of a git the person chose that cannot run.
/// </summary>
/// <param name="Program">The program git runs as, by its whole path; the bare name where the system's git is not found, so the start fails in the system's words. Null where <paramref name="Refusal"/> is said.</param>
/// <param name="Arguments">Every argument it takes, the application's own first.</param>
/// <param name="Environment">The variables it takes beside the host's; a null value removes the variable.</param>
/// <param name="Bound">How long it may run.</param>
/// <param name="Refusal">Why no git runs, in the resolution's words, which an answer carries as git's error and which never falls back to another git.</param>
public sealed record GitRunSpec(
    string? Program, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string?> Environment, TimeSpan Bound, string? Refusal = null);

/// <summary>
/// What Daoris decides about a git that the adapter that runs it does not: which program it is (the tools' choice, the home's
/// entries, what a lazy fetch needs), whether the git library answers it without a start, the scheduler's place it waits for, and the
/// count it is kept in. The root hands it to the git adapter, which runs what it resolves and decides none of it.
/// </summary>
public interface IGitPolicy
{
    /// <summary>The answer of a read the git library gives without starting git, or null where git must run.</summary>
    Task<GitAnswer?> AnswerInProcessAsync(GitAsk ask, CancellationToken ct);

    /// <summary>How <paramref name="ask"/> starts: its program, its arguments, its variables and its bound, or why no git runs.</summary>
    Task<GitRunSpec> StartAsync(GitAsk ask, GitRunKind kind, CancellationToken ct);

    /// <summary>
    /// Keeps <paramref name="run"/>, a whole start and run of <paramref name="ask"/>, in the counts its flow holds, and times it. Any
    /// answer <paramref name="run"/> gives stands.
    /// </summary>
    Task<T> LaunchAsync<T>(GitAsk ask, GitRunKind kind, Func<Task<T>> run);

    /// <summary>
    /// Runs <paramref name="run"/> in the place the scheduler issues <paramref name="ask"/>, which cancels its token where nobody waits
    /// for the answer any longer. A start that is refused a place throws, and a git that outlived its kill is thrown as the caller's
    /// cancellation (<see cref="Processes.ProcessUnendedException"/>).
    /// </summary>
    Task<T> PlaceAsync<T>(GitAsk ask, GitRunSpec start, GitRunKind kind, Func<CancellationToken, Task<T>> run, CancellationToken ct);

    /// <summary>Tells that the git of <paramref name="id"/> did not end at its kill, <paramref name="reason"/> saying why, as it is found.</summary>
    void Unended(int id, string reason);

    /// <summary>The program git runs as for a home, with the moment its file changed, or null where none resolves.</summary>
    string? InUse(string? home);
}
