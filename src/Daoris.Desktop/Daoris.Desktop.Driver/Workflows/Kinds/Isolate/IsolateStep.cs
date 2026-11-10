using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// An <c>isolate</c> step as the start reads it: the isolation kind, how many of the place's sessions work apart at once (null for any,
/// bounded by the machine's cap alone), and whether a first start whose declared paths meet earlier work not yet returned waits for it.
/// A place whose workflow has no such step runs one session at a time in its own place.
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

    /// <summary>The first option of <paramref name="node"/> its choices do not hold, or null.</summary>
    public static string? Problem(NodeDeclaration node)
    {
        foreach (var (name, choices) in new (string, IReadOnlyList<string>)[] { (KindOption, Kinds), (AtOnceOption, Counts), (OverlapOption, Overlaps) })
        {
            if (node.Options.ContainsKey(name) && !(NodeOptions.Text(node, name) is { } text && choices.Contains(text, StringComparer.Ordinal))) return name;
        }

        return null;
    }

    public static IsolateStep Of(NodeDeclaration node) => new(
        NodeOptions.Text(node, KindOption) ?? Auto,
        int.TryParse(NodeOptions.Text(node, AtOnceOption), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null,
        NodeOptions.Text(node, OverlapOption) == Wait);

    /// <summary>The graph's isolate step, or null where it has none.</summary>
    public static IsolateStep? In(NodeGraph? graph) =>
        graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Isolate) is { } step ? Of(step) : null;

    /// <summary>The step written as a node, its options those that differ from an unwritten one's.</summary>
    public NodeDeclaration Declaration(string id)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Kind != Auto) options[KindOption] = $"\"{Kind}\"";
        if (AtOnce is { } count) options[AtOnceOption] = $"\"{count.ToString(CultureInfo.InvariantCulture)}\"";
        if (OverlapWaits) options[OverlapOption] = $"\"{Wait}\"";
        return new NodeDeclaration(id, NodeKinds.Isolate, 1, NodeReach.Observed) { Options = options };
    }
}
