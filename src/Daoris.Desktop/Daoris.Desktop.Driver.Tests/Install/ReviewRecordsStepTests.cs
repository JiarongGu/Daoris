using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The migration's <c>review-records</c> step: a word an older build kept on a branchless review, before a review had a record to name, is kept
/// as legacy only where the run's pinned graph has nothing after its review, its receipt saying it was given before Daoris drew a revision;
/// under any other graph it is dropped and said on the run, and a supervisor's such word is dropped stale either way. A branch run's words stand.
/// </summary>
public sealed class ReviewRecordsStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "review-records", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Pressed = DateTimeOffset.Parse("2026-11-10T09:12:00Z");

    private const string InPlace = """
        {"schemaVersion":2,"entries":["work"],"end":"review",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"review"}]}
        """;

    private const string Landing = """
        {"schemaVersion":2,"entries":["work"],"end":"landing",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"review"},{"id":"b","kind":"after","from":"review","to":"landing"},
                  {"id":"c","kind":"back","from":"review","on":"rejected","to":"work"}]}
        """;

    public void Dispose() => GitSeed.Discard(_home);

    private NodeRecordFiles Records => new(_home, () => true);

    /// <summary>Run <paramref name="run"/> on <paramref name="graph"/>, its review at <paramref name="state"/> with <paramref name="words"/> pending on it.</summary>
    private void Recorded(string run, string graph, string workflow, string state, NodeAcceptance? binding, params NodeWord[] words)
    {
        RunHome.Bind(_home, run, RunHome.SaveGraph(_home, graph, workflow), Pressed.AddHours(-1), workflow);
        var unit = RunUnits.IdOf(run);
        Records.Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["review"] = new(unit, "review", state) { Attempt = binding?.Attempt ?? 0, Binding = binding },
            },
            Pending = words,
        });
    }

    private static NodeWord EarlyAccept() => new("review", NodeWordActs.Accept, null, Pressed) { Kept = true };

    private static HomeMigrationRun Run(string home) => new(home, HomeMigration.Name);

    [Fact]
    public void A_legacy_accept_under_a_pinned_graph_with_nothing_after_its_review_is_kept_and_said_as_given_before_a_revision()
    {
        Recorded("q1", InPlace, "in-place", NodeStates.Ready, null, EarlyAccept());
        var run = Run(_home);

        new ReviewRecordsStep().Run(run);

        var word = Assert.Single(Records.Read(RunUnits.IdOf("q1"))!.Pending);
        Assert.Equal((NodeWordActs.Accept, true), (word.Act, word.Kept));
        Assert.Equal(ReviewLegacy.Receipt(Pressed), word.Words);
        Assert.Contains("before Daoris drew a revision", word.Words);
        Assert.Equal(1, run.Counts["review-records.legacy-kept"]);
    }

    [Fact]
    public void A_legacy_accept_under_a_graph_with_a_step_after_its_review_is_dropped_and_said_on_the_run()
    {
        Recorded("q1", Landing, "merge", NodeStates.Ready, null, EarlyAccept());

        new ReviewRecordsStep().Run(Run(_home));

        var record = Records.Read(RunUnits.IdOf("q1"))!;
        Assert.Empty(record.Pending);
        var said = Assert.Single(record.History);
        Assert.Equal(("review", ReviewLegacy.Dropped), (said.Was.Node, said.Why));
    }

    [Fact]
    public void A_supervisors_unscoped_word_on_a_branchless_review_is_dropped_stale_even_where_nothing_follows()
    {
        var binding = new NodeAcceptance(RunUnits.IdOf("q1"), null, "gph-x", "review", 1, 1);
        Recorded("q1", InPlace, "in-place", NodeStates.Active, binding,
            new NodeWord("review", NodeWordActs.Accept, "Looks right.", Pressed) { By = new NodeWordBy("landing", "sd-1"), Attempt = 1 });
        var run = Run(_home);

        new ReviewRecordsStep().Run(run);

        var record = Records.Read(RunUnits.IdOf("q1"))!;
        Assert.Empty(record.Pending);
        Assert.Equal(ReviewLegacy.Stale, Assert.Single(record.History).Why);
        Assert.Equal(1, run.Counts["review-records.legacy-stale"]);
    }

    [Fact]
    public void A_branch_runs_words_stand_and_the_step_run_twice_changes_nothing_more()
    {
        var tip = new string('a', 40);
        Recorded("q1", Landing, "merge", NodeStates.Ready, null, EarlyAccept() with { Tip = tip });
        var binding = new NodeAcceptance(RunUnits.IdOf("q2"), tip, "gph-x", "review", 1, 2);
        Recorded("q2", Landing, "merge", NodeStates.Active, binding, new NodeWord("review", NodeWordActs.Accept, null, Pressed));
        Recorded("q3", InPlace, "in-place", NodeStates.Ready, null, EarlyAccept());
        var first = Run(_home);
        new ReviewRecordsStep().Run(first);
        var (one, two, three) = (Read("q1"), Read("q2"), Read("q3"));
        var again = Run(_home);

        new ReviewRecordsStep().Run(again);

        Assert.DoesNotContain(first.Changed, path => path.Contains(NodeRecordFiles.KeyOf(RunUnits.IdOf("q1")), StringComparison.Ordinal));
        Assert.DoesNotContain(first.Changed, path => path.Contains(NodeRecordFiles.KeyOf(RunUnits.IdOf("q2")), StringComparison.Ordinal));
        Assert.Equal((one, two, three), (Read("q1"), Read("q2"), Read("q3")));
        Assert.Empty(again.Changed);
    }

    private string Read(string run) => File.ReadAllText(NodeRecordFiles.PathOf(_home, RunUnits.IdOf(run)));
}
