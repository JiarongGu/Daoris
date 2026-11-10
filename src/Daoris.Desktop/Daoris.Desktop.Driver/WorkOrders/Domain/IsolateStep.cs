using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// An <c>isolate</c> step as the start reads it: the isolation kind, how many of the place's sessions work apart at once (null for any,
/// bounded by the machine's cap alone), and whether a first start whose declared paths meet earlier work not yet returned waits for it.
/// A place whose workflow has no such step runs one session at a time in its own place. Workflows reads it from a graph
/// (<c>IsolateSteps</c>) and serves it to the look through <see cref="IWorkOrdersWorkflows.Isolations"/>.
/// </summary>
public sealed record IsolateStep(string Kind, int? AtOnce, bool OverlapWaits)
{
    public const string KindOption = "kind";

    public const string AtOnceOption = "at-once";

    public const string OverlapOption = "overlap";

    /// <summary>The place kind's first isolation that can make a copy: a git place's worktree.</summary>
    public const string Auto = "auto";

    public const string Worktree = "worktree";

    public const string Any = "any";

    public const string Go = "go";

    public const string Wait = "wait";

    private const int Fewest = 2;

    private const int Most = 24;

    /// <summary>The kinds a step may name: those this build makes a copy with.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Auto, Worktree];

    public static IReadOnlyList<string> Counts { get; } =
        [Any, .. Enumerable.Range(Fewest, Most - Fewest + 1).Select(count => count.ToString(CultureInfo.InvariantCulture))];

    public static IReadOnlyList<string> Overlaps { get; } = [Go, Wait];

    /// <summary>A worktree for each session, as many at once as the cap allows, starting beside overlapping work.</summary>
    public static IsolateStep Trees { get; } = new(Worktree, null, false);
}
