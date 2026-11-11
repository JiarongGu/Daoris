using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A pass's turn over more runs than <see cref="NodeRuntime.UnitsPerPass"/>: the run a door asked for and a run whose record the pass's own
/// words moved are judged first, whatever the number of runs, the rotation filling what is left; a door waiting on its run is told as soon
/// as that run's judgement is kept, before the pass ends.
/// </summary>
public sealed class AskedRunTurnTests : NodeRunFixture
{
    private const string Reviewed = """
        {"schemaVersion":2,"entries":["work"],"end":"handed",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"w>r","kind":"after","from":"work","to":"review"},{"id":"r>h","kind":"after","from":"review","to":"handed"}]}
        """;

    /// <summary>Two turns and more: many open runs, each its quest done on its own branch.</summary>
    private static readonly string[] Runs = [.. Enumerable.Range(0, (2 * NodeRuntime.UnitsPerPass) + 6).Select(at => $"q{at:D2}")];

    /// <summary>A run before where the rotation goes on once every run is judged, so the next turn's rotation leaves it.</summary>
    private const string Pressed = "q20";

    public AskedRunTurnTests()
    {
        var digest = RunHome.SaveGraph(_home, Reviewed);
        foreach (var run in Runs)
        {
            RunHome.Bind(_home, run, digest, _now);
            _branchTips[Branch(run)] = Tip1;
        }

        _units =
        [
            .. Runs.Select(run => RunHome.QuestUnit(RunHome.Quest(run, "Done"))),
            .. Runs.Select(run => RunHome.Branch("unit-" + run, Branch(run), Opened, [run])),
        ];
    }

    private static string Branch(string run) => "daoris/" + run;

    private NodeStatus Status(string run, string node) => Files.Read(RunUnits.IdOf(run))!.Statuses[node];

    /// <summary>Every run judged once, its review waiting at the first tip: three turns, the rotation left after the third.</summary>
    private async Task<NodeRuntime> JudgedAsync()
    {
        var runtime = Runtime();
        for (var turn = 0; turn < 3; turn++) await RunPassAsync(runtime);
        Assert.All(Runs, run => Assert.Equal((NodeStates.Active, Tip1), (Status(run, "review").State, Status(run, "review").Binding!.SubjectRevision)));
        return runtime;
    }

    [Fact]
    public async Task The_persons_accept_applied_at_a_whole_pass_readies_its_runs_landing_in_that_pass_whatever_the_number_of_runs()
    {
        var runtime = await JudgedAsync();
        Assert.True(RunNodeWords.Word(Files, Pressed, "review", NodeWordActs.Accept, null, () => _now).Done);

        var pass = await RunPassAsync(runtime);

        Assert.Equal(RunUnits.IdOf(Pressed), pass.Evaluated[0]);
        Assert.Equal(NodeRuntime.UnitsPerPass, pass.Evaluated.Count);
        var handed = Status(Pressed, "handed");
        Assert.Equal((NodeStates.Reached, NodeStates.Active, Tip1), (Status(Pressed, "review").State, handed.State, handed.Binding!.SubjectRevision));
    }

    [Fact]
    public async Task A_pass_a_door_asks_for_its_run_judges_that_run_first_and_tells_the_door_once_its_judgement_is_kept()
    {
        var runtime = await JudgedAsync();
        _branchTips[Branch(Pressed)] = Tip2;
        var judged = new List<string>();

        var pass = await runtime.PassAsync(RunLook() with { Asked = new HashSet<string>([RunUnits.IdOf(Pressed)]), Judged = judged.Add },
            CancellationToken.None);

        Assert.Equal(RunUnits.IdOf(Pressed), pass.Evaluated[0]);
        Assert.Equal(pass.Evaluated, judged);
        Assert.Equal((NodeStates.Active, Tip2), (Status(Pressed, "review").State, Status(Pressed, "review").Binding!.SubjectRevision));
    }
}
