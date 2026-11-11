using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The runtime starts, observes and keeps every kind through the kit: it asks a node's unit whether it observes, starts or is
/// pressed, and names no kind to choose what to do; the save's graph rules (<c>NodeGraphs</c>) name none either. The evaluator's own kinds, <c>all</c> and <c>stop</c>, are its wiring and stay its.
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
        "Workflows/Nodes/NodeGoverning.cs",
        "Workflows/Nodes/NodeGraphs.cs",
        "Workflows/Nodes/WorkPerformer.cs",
        "Workflows/Nodes/NodeEvaluations.cs",
        "Loop/Watch/WatchNodes.cs",
    ];

    /// <summary>
    /// The kinds a dispatching file may still name: the evaluator's join and sink, what the run's keeping reads beside its doors, and the graph
    /// that integrates.
    /// </summary>
    private static readonly Dictionary<string, string[]> Kept = new(StringComparer.Ordinal)
    {
        ["Workflows/Nodes/WireEvaluation.cs"] = [nameof(NodeKinds.All), nameof(NodeKinds.Stop)],
        ["Workflows/Nodes/NodeRunKeeping.cs"] = [nameof(NodeKinds.Claimed), nameof(NodeKinds.Integrate), nameof(NodeKinds.Request), nameof(NodeKinds.Review)],
        ["Workflows/Nodes/NodeGoverning.cs"] = [nameof(NodeKinds.Integrate)],
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

    /// <summary>The performer asks a node's unit whether it is started on a port through the attempt journal, and the doors for that port.</summary>
    [Fact]
    public void The_performer_holds_no_port_by_a_kinds_name()
    {
        Assert.Null(typeof(WorkPerformer).GetProperty("Checks"));
        Assert.NotNull(typeof(WorkPerformer).GetProperty(nameof(WorkPerformer.Doors)));
        Assert.Equal([NodeKinds.Check], NodeKit.Built.Units.Where(unit => unit is IJournalled).Select(unit => unit.Row.Kind));
    }

    /// <summary>The pass's reads are chosen by what each unit declares it waits on, and the landing by the one unit that makes it.</summary>
    [Fact]
    public void The_pass_reads_by_what_each_unit_asks()
    {
        string[] Asking(PassRead read) => [.. NodeKit.Built.Units.Where(unit => unit is IAsksReads asks && asks.Asks.HasFlag(read)).Select(unit => unit.Row.Kind)];

        Assert.Equal([NodeKinds.Look, NodeKinds.Opinion], Asking(PassRead.Gates).Order(StringComparer.Ordinal));
        Assert.Equal([NodeKinds.ContentHolds, NodeKinds.Land], Asking(PassRead.Content));
        Assert.Equal([NodeKinds.Push], Asking(PassRead.Remote));
        Assert.Equal([NodeKinds.Land], NodeKit.Built.Units.Where(unit => unit is IMakesLanding).Select(unit => unit.Row.Kind));
    }

    [GeneratedRegex(@"NodeKinds\.([A-Z]\w*)")]
    private static partial Regex Named();

    /// <summary>A list of kinds kept beside the kit, a start loop by kind, or a kind's own settling called by name.</summary>
    [GeneratedRegex(@"\bObserved\s*=|RunBegins|\bBegins\(string|StartLandings|StartPushes|StartTidies|LandAttemptSettling")]
    private static partial Regex Lists();
}
