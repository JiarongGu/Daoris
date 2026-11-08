namespace Daoris.Driver;

/// <summary>How a line's slot is let go.</summary>
public static class LineEndKinds
{
    public const string Output = "output";

    public const string NoOutput = "no-output";

    public const string Uncommitted = "uncommitted";

    public const string Held = "held";
}

/// <summary>
/// One let-go of a line: the tip the next judgement compares, the slot a returning line prefers, the facts a finish's manifest needs where
/// the slot serves another line by then, and the licence to grow a no-output branch again.
/// </summary>
/// <param name="Number">The slot it is let go in.</param>
/// <param name="Read">The moment the slot is read.</param>
/// <param name="Left">The names of its own ignored paths; empty for none.</param>
/// <param name="Toolchain">The key of its rules' <c>toolchain</c> outputs at its tip, read at the let-go.</param>
public sealed record LineEnd(
    string Workspace, string Repository, string Line, string Branch, string Session, int Number, string Kind, string Tip, DateTimeOffset Read,
    string Left, string Toolchain, DateTimeOffset At);
