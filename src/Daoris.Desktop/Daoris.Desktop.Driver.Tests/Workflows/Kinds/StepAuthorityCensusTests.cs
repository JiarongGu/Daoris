using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The run's own step (<see cref="StepAuthority"/>) is set where a kind's work begins and nowhere else. Source: its runner is called by the
/// four setters alone, and under <c>Workflows/Kinds/</c> only the door runner suppresses the pass's flow or runs a task. Doors: every kind
/// whose node is kept on a door it builds runs that door on the runner, so its work reads its start's authority.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class StepAuthorityCensusTests
{
    private static readonly string[] Setters =
    [
        "Workflows/Kinds/NodeDoorRuns.cs", "Workflows/Kinds/Integrate/IntegrateStart.cs", "Workflows/Nodes/WorkPerformer.cs",
        "Workflows/Kinds/Resolve/ResolveDoor.cs",
    ];

    private const string Runner = "Workflows/Kinds/NodeDoorRuns.cs";

    [GeneratedRegex(@"StepAuthority\.RunAsync\(")]
    private static partial Regex Sets();

    [GeneratedRegex(@"ExecutionContext\.SuppressFlow\(|Task\.Run\(")]
    private static partial Regex LeavesThePass();

    private static string Root => Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), "Daoris.Desktop.Driver");

    private static IEnumerable<(string Path, string Text)> Sources() =>
        Directory.EnumerateFiles(Root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(file => (Path.GetRelativePath(Root, file).Replace('\\', '/'), File.ReadAllText(file)));

    /// <summary>The files that set the authority beside the four, and those under the kinds that leave the pass beside the runner.</summary>
    internal static IReadOnlyList<string> Strays(IEnumerable<(string Path, string Text)> sources) =>
        [.. sources.Where(each => Sets().IsMatch(each.Text) && !Setters.Contains(each.Path) && each.Path != "Workplace/StepAuthority.cs")
             .Select(each => $"{each.Path} sets the run's own step")
             .Concat(sources.Where(each => each.Path.StartsWith("Workflows/Kinds/", StringComparison.Ordinal) && each.Path != Runner && LeavesThePass().IsMatch(each.Text))
                 .Select(each => $"{each.Path} runs a door's work off the pass by itself"))];

    [Fact]
    public void The_run_s_own_step_is_set_by_the_four_alone_and_only_the_door_runner_leaves_the_pass()
    {
        Assert.Empty(Strays(Sources()));
    }

    [Fact]
    public void A_planted_fifth_setter_and_a_door_running_its_own_task_are_each_found()
    {
        var planted = Strays(
        [
            ("Workplace/Planted.cs", "await StepAuthority.RunAsync(authority, work);"),
            ("Workflows/Kinds/Planted/PlantedDoor.cs", "using (ExecutionContext.SuppressFlow()) { _ = Task.Run(work); }"),
        ]);

        Assert.Equal(["Workplace/Planted.cs sets the run's own step", "Workflows/Kinds/Planted/PlantedDoor.cs runs a door's work off the pass by itself"], planted);
    }

    /// <summary>Every door a kind keeps its node on, built from a wiring that holds every piece; a kind journalled or started through its own attempts is not one.</summary>
    private static IReadOnlyList<(string Kind, INodeDoor Door)> KeptDoors(string home)
    {
        var wiring = new NodeDoorWiring(home, () => true)
        {
            Landing = () => null!, Turning = _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()), Resolvers = new CensusResolvers(),
            Reserve = (_, _) => Task.FromResult(new TreeReserved(null, null, "census")),
        };
        return [.. NodeKit.Built.Units
            .Where(unit => unit is IStarts and not IJournalled and not IntegrateNode and not RequestNode)
            .Select(unit => (unit.Row.Kind, Door: ((IStarts)unit).Door(wiring)))
            .Where(each => each.Door is not null)
            .Select(each => (each.Kind, each.Door!))];
    }

    /// <summary>The kinds whose door runs its work some other way than on the runner, so the work reads no authority.</summary>
    internal static IReadOnlyList<string> Unrun(IEnumerable<(string Kind, INodeDoor Door)> doors) =>
        [.. doors.Where(each => each.Door is not IDoorRuns).Select(each => each.Kind)];

    [Fact]
    public async Task Every_door_a_kind_keeps_its_node_on_runs_its_work_under_its_start_s_authority()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "test-homes", Guid.NewGuid().ToString("N")[..8]);
        var doors = KeptDoors(home);
        Assert.Contains(doors, each => each.Kind == NodeKinds.Rebase);
        Assert.Contains(doors, each => each.Kind == NodeKinds.Resolve);
        Assert.Empty(Unrun(doors));
        foreach (var (kind, door) in doors)
        {
            var runs = ((IDoorRuns)door).Runs;
            var authority = StepAuthority.OfDoor("r-12", kind, 1);
            StepAuthority? read = null;
            Assert.True(runs.Start(new CensusStart(kind) { Authority = authority }, _ =>
            {
                read = StepAuthority.Current;
                return Task.CompletedTask;
            }, _ => true), kind);
            await runs.IdleAsync();
            Assert.Equal(authority, read);
        }
    }

    [Fact]
    public void A_door_kind_whose_work_reads_no_authority_is_found()
    {
        Assert.Equal(["planted"], Unrun([("planted", new PlantedDoor())]));
    }

    private sealed record CensusStart(string Kind) : NodeDoorStart
    {
        public override string Key => Kind;
    }

    private sealed class PlantedDoor : INodeDoor;

    private sealed class CensusResolvers : IResolvers
    {
        public Task<(string? Session, string? Why)> SessionAsync(TreeHandOff row, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> AskAsync(TreeHandOff row, string session, string words, Func<bool> lapsed, CancellationToken ct) => throw new NotSupportedException();

        public Task StopAsync(TreeHandOff row, CancellationToken ct) => throw new NotSupportedException();
    }
}
