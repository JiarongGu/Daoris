using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The tidy node is reached where the landing record holds what its door ended at the tip; only a monitor's <c>when merged</c> starts it.</summary>
public sealed class TidyNodeTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tidy-node", Guid.NewGuid().ToString("N")[..8]);

    public TidyNodeTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static LandedBranch Landing(PullRequestState? state = null) =>
        new("engine", "default", "feature/q1-fix", "main", "c0ffee00", "s-q1", "q1", "Fix", DateTimeOffset.UtcNow)
        {
            PullRequest = "https://example.invalid/acme/engine/pull/1", Plugin = "acme-host", PullRequestState = state, Squashed = "feed0002", Units = ["u1", "u2"],
        };

    [Fact]
    public void It_is_reached_only_where_its_record_holds_the_landings_tip_and_nothing_fails_it()
    {
        Assert.False(TidyReading.Tidied(Landing()));
        Assert.False(TidyReading.Tidied(Landing() with { Tidied = new ChainTidied("0ld0ld00", DateTimeOffset.UtcNow) }));
        Assert.True(TidyReading.Tidied(Landing() with
        {
            Tidied = new ChainTidied("c0ffee00", DateTimeOffset.UtcNow) { Parts = [new("work/one", "feed0001") { KeptOn = "refs/daoris/discarded/work/one" }] },
        }));
        Assert.Null(NodeKit.Row(NodeKinds.Tidy)!.FailureBy);
        Assert.False(NodeKit.Row(NodeKinds.Tidy)!.PersonSkips);
    }

    [Fact]
    public void The_save_refuses_a_tidy_no_when_merged_way_from_a_monitor_reaches()
    {
        const string watched = """{"id":"m","kind":"after","from":"pull-request","to":"monitor"},""";
        Assert.Empty(NodeGraphs.Read(MonitorNodeTests.Graph(MonitorNodeTests.Watching, watched + MonitorNodeTests.Routes), onSave: true).Problems);

        var afterIt = """{"id":"t","kind":"after","from":"monitor","to":"tidy"},{"id":"x","kind":"when","from":"monitor","to":"closed","outcome":"closed"}""";
        var onClosed = """{"id":"t","kind":"when","from":"monitor","to":"tidy","outcome":"closed"},{"id":"x","kind":"when","from":"monitor","to":"closed","outcome":"merged"}""";
        foreach (var wires in new[] { afterIt, onClosed })
        {
            var (graph, problems) = NodeGraphs.Read(MonitorNodeTests.Graph(MonitorNodeTests.Watching, watched + wires), onSave: false);
            Assert.Null(graph);
            Assert.Equal([GraphRefusal.TidyMerged], problems.Where(problem => problem.Node == "tidy").Select(problem => problem.Code));
        }

        // A way on from the merged route still reaches it.
        var later = """{"id":"t","kind":"when","from":"monitor","to":"closed","outcome":"merged"},{"id":"y","kind":"after","from":"closed","to":"tidy"},"""
                    + """{"id":"x","kind":"when","from":"monitor","to":"note","outcome":"closed"}""";
        var note = """,{"id":"note","kind":"human","version":1,"reach":"pressed"}""";
        Assert.Empty(NodeGraphs.Read(MonitorNodeTests.Graph(MonitorNodeTests.Watching, watched + later, note), onSave: true).Problems);
    }

    [Fact]
    public async Task A_closed_request_starts_nothing_on_its_door_ends_nothing_and_fails_nothing()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Landing(new PullRequestState(PullRequestStates.Abandoned)));
        var tidy = new NodeDeclaration("tidy", NodeKinds.Tidy, 1, NodeReach.Performed);
        var door = new TidyDoor(_home);

        Assert.True(door.Start(new TidyStart("run:q1", tidy, "engine", "default", _home, "feature/q1-fix", "c0ffee00")));
        await door.IdleAsync();

        Assert.Null(landings.Of("engine", "feature/q1-fix")!.Tidied);
        Assert.False(File.Exists(NodeRecordFiles.PathOf(_home, "run:q1")));
    }

    [Fact]
    public void Its_record_reads_back_whole_and_names_a_part_only_at_the_tip_it_ended()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Landing());
        var tidied = new ChainTidied("c0ffee00", DateTimeOffset.Parse("2026-11-06T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture))
        {
            Parts =
            [
                new("work/one", "feed0001") { KeptOn = "refs/daoris/discarded/work/one", Act = "act-1" },
                new("work/two", "feed0002") { KeptOn = "refs/daoris/discarded/work/two", Kept = "it is checked out at a tree" },
            ],
        };

        landings.Tidied("engine", "feature/q1-fix", tidied);

        var read = new LandedBranches(_home).Of("engine", "feature/q1-fix")!;
        Assert.Equal(tidied, read.Tidied);
        Assert.Equal("act-1", ChainTidies.Of([read], "ENGINE", "work/one", "feed0001")!.Value.Part.Act);
        Assert.Null(ChainTidies.Of([read], "engine", "work/one", "feed0009"));
        Assert.Null(ChainTidies.Of([read], "engine", "work/three", "feed0001"));
    }
}
