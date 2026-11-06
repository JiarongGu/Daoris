using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The runtime starts, observes and keeps every kind through the kit (D247, NODEKIT1 1b): it asks a node's unit whether it observes, starts or is
/// pressed, and names no kind to choose what to do. The evaluator's own kinds, <c>all</c> and <c>stop</c>, are its wiring and stay its.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class NodeDispatchCensusTests
{
    private static readonly string[] Dispatching =
    [
        "Workflows/Nodes/NodeRuntime.cs",
        "Workflows/Kinds/NodeObserving.cs",
        "Workflows/Nodes/RunNodeWords.cs",
        "Workflows/Nodes/WireEvaluation.cs",
        "Workflows/Nodes/NodeRunKeeping.cs",
        "Loop/Watch/WatchNodes.cs",
    ];

    /// <summary>The kinds a dispatching file may still name: the evaluator's join and sink, and what the run's keeping reads beside its doors.</summary>
    private static readonly Dictionary<string, string[]> Kept = new(StringComparer.Ordinal)
    {
        ["Workflows/Nodes/WireEvaluation.cs"] = [nameof(NodeKinds.All), nameof(NodeKinds.Stop)],
        ["Workflows/Nodes/NodeRunKeeping.cs"] = [nameof(NodeKinds.Claimed), nameof(NodeKinds.Integrate), nameof(NodeKinds.Request), nameof(NodeKinds.Review)],
    };

    [Fact]
    public void No_dispatching_file_chooses_by_a_kinds_name()
    {
        var root = Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), "Daoris.Desktop.Driver");
        var found = new List<string>();
        foreach (var file in Dispatching)
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            var allowed = Kept.GetValueOrDefault(file) ?? [];
            found.AddRange(Named().Matches(text).Select(match => match.Groups[1].Value).Where(name => !allowed.Contains(name)).Distinct()
                .Select(name => $"{file}: NodeKinds.{name}"));
            found.AddRange(Lists().Matches(text).Select(match => $"{file}: {match.Value}"));
        }

        Assert.Empty(found);
    }

    [Fact]
    public void The_watch_builds_no_kinds_door_and_the_runtime_holds_none_by_name()
    {
        var root = Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), "Daoris.Desktop.Driver");
        var watch = File.ReadAllText(Path.Combine(root, "Loop/Watch/WatchNodes.cs"));
        Assert.DoesNotMatch(@"new (LandingDoor|PushDoor|TidyDoor|CheckExecution)\(", watch);
        foreach (var door in new[] { "Landing", "Pushing", "Tidying", "Checks", "Integration", "Requests" })
        {
            Assert.Null(typeof(NodeRuntime).GetProperty(door));
        }

        Assert.NotNull(typeof(NodeRuntime).GetProperty(nameof(NodeRuntime.Doors)));
    }

    [GeneratedRegex(@"NodeKinds\.([A-Z]\w*)")]
    private static partial Regex Named();

    /// <summary>A list of kinds kept beside the kit, or a start loop by kind.</summary>
    [GeneratedRegex(@"\bObserved\s*=|RunBegins|\bBegins\(string|StartLandings|StartPushes|StartTidies")]
    private static partial Regex Lists();
}
