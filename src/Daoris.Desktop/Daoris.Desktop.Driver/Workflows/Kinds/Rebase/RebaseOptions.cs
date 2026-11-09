namespace Daoris.Driver;

/// <summary>A <c>rebase</c>'s outcomes, each a route a <c>when</c> wire follows.</summary>
public static class RebaseOutcomes
{
    /// <summary>The work replayed onto the target's tip, already standing on it, or wholly held by it; its <c>commit</c> carried.</summary>
    public const string Ready = "ready";

    /// <summary>A stop it may not settle; its <c>problem</c> carried.</summary>
    public const string Conflicted = "conflicted";

    /// <summary>The work's tree holds paths no commit carries; its <c>problem</c> carried.</summary>
    public const string Uncommitted = "uncommitted";

    public static readonly IReadOnlyList<string> All = [Ready, Conflicted, Uncommitted];
}

/// <summary>
/// A <c>rebase</c>'s <c>onto</c>: <c>target</c> (the default) or <c>line</c> read as the line the run's branch grows from, or a branch by its
/// name; and its <c>fetch</c>. The <c>target</c> of a <c>merge</c> its commit feeds is read once that kind is built; until then it is the line.
/// </summary>
public static class RebaseOnto
{
    public const string Option = "onto";

    public const string Fetch = "fetch";

    public const string Target = "target";

    public const string Line = "line";

    /// <summary>The full ref <paramref name="node"/> replays onto, <paramref name="line"/> being the run's line; null where it names the line and none is known.</summary>
    public static string? Of(NodeDeclaration node, string? line) => NodeOptions.Text(node, Option) switch
    {
        null or Target or Line => line is { Length: > 0 } ? $"refs/heads/{line}" : null,
        var branch => branch.StartsWith("refs/", StringComparison.Ordinal) ? branch : $"refs/heads/{branch}",
    };
}
