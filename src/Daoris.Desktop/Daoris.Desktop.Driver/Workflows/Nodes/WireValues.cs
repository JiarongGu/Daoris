namespace Daoris.Driver;

/// <summary>
/// The values a <c>data</c> wire carries between the steps a landing is composed of, each a port's schema. A node fed one binds to it
/// (<see cref="RunSubject.Input"/>), so a moved input rebinds only what it feeds.
/// </summary>
public static class WireValues
{
    /// <summary>
    /// A run's work: its branch, its bound tip, its tree and the session that is its newest executor, read in the pass at the branch's tip.
    /// The run's entry carries it (<c>claimed</c>'s <c>work</c>).
    /// </summary>
    public const string Work = "work";

    /// <summary>A commit (the port's revision), the commit it is made onto (<see cref="NodePort.Onto"/>) and the step ref that keeps it (its ref).</summary>
    public const string Commit = "commit";

    /// <summary>A ref a step moved (its ref), the commit it moved it to (its revision) and its producer, which every reader of it is bound to.</summary>
    public const string Ref = "ref";

    /// <summary>What a step could not settle, structured, with its producer and its stop's identity (<see cref="NodePort.Stop"/>).</summary>
    public const string Problem = "problem";

    public static readonly IReadOnlyList<string> All = [Work, Commit, Ref, Problem];

    /// <summary>Whether <paramref name="schema"/> is one of the values.</summary>
    public static bool Is(string? schema) => schema is not null && All.Contains(schema, StringComparer.Ordinal);
}

/// <summary>The two sorts of <see cref="WireValues.Problem"/>, by what a step could not settle.</summary>
public static class ProblemSorts
{
    /// <summary>A stop of a replay: the stop's commit and subject, its <c>onto</c>, each file with its hunks, what is settled beside it.</summary>
    public const string Conflict = "conflict";

    /// <summary>The paths of the work's tree no commit carries, each with the blob the tree held.</summary>
    public const string Uncommitted = "uncommitted";

    public static readonly IReadOnlyList<string> All = [Conflict, Uncommitted];
}
