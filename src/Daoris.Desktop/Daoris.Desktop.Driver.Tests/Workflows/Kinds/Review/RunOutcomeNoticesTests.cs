using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <i>Done and failed</i>'s run outcomes (<see cref="RunOutcomeNotices"/>): one per watched step after a run's review, by run, node and
/// attempt, read from the run's record at every look so one a lost write left is kept the next time, and a step still going leaves none.
/// </summary>
public sealed class RunOutcomeNoticesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "run-outcomes", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    private const string Graph = """
        {"schemaVersion":2,"entries":["work"],"end":"push",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"daoris/{quest}"}},
                  {"id":"push","kind":"push","version":1,"reach":"performed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"review"},{"id":"b","kind":"after","from":"review","to":"landing"},
                  {"id":"c","kind":"after","from":"landing","to":"push"}]}
        """;

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void Each_failed_step_after_the_review_is_one_outcome_by_run_node_and_attempt_and_a_step_reached_or_going_on_is_none()
    {
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, Graph), T);
        var unit = RunUnits.IdOf("q1");
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["review"] = new(unit, "review", NodeStates.Reached) { Attempt = 1, Since = T },
                ["landing"] = new(unit, "landing", NodeStates.Reached) { Attempt = 1, Since = T.AddMinutes(1) },
                ["push"] = new(unit, "push", NodeStates.Failed) { Attempt = 2, Since = T.AddMinutes(2), Code = "rejected" },
            },
        });

        var told = RunOutcomeNotices.Read(_home, T.AddMinutes(5)).Occurrences;

        Assert.Equal(["outcome:q1/push/2"], told.Select(each => each.Key));
        var push = told[0].Notice!;
        Assert.Equal((RunOutcomeNotices.Kind, "q1", "push", NodeKinds.Push, NodeStates.Failed, "rejected", 2),
            (push.Kind, push.Quest, push.Act, push.Route, push.State, push.Code, push.Count));
        Assert.Empty(RunOutcomeNotices.Read(_home, T.AddDays(8)).Occurrences);
    }

    /// <summary>One read of the runs gives both the steps to keep and the notices that moved on; a step past the news' horizon is no new news but its notice still stands.</summary>
    [Fact]
    public void One_read_of_the_runs_answers_both_what_to_keep_and_what_moved_on()
    {
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, Graph), T);
        var unit = RunUnits.IdOf("q1");
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["review"] = new(unit, "review", NodeStates.Reached) { Attempt = 1, Since = T },
                ["push"] = new(unit, "push", NodeStates.Failed) { Attempt = 2, Since = T.AddMinutes(2), Code = "rejected" },
            },
        });
        var now = T.AddDays(8);
        var held = new[] { new Notice("outcome:q1/push/2", RunOutcomeNotices.Kind, T), new Notice("outcome:q1/push/1", RunOutcomeNotices.Kind, T) };

        var read = RunOutcomeNotices.Read(_home, now);

        Assert.Empty(read.Occurrences);
        Assert.Equal(["outcome:q1/push/1"], read.Moved(held));
    }

    [Fact]
    public void A_kept_failure_clears_when_its_step_has_moved_on_or_its_run_is_gone_and_stands_while_the_step_still_fails_that_attempt()
    {
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, Graph), T);
        var unit = RunUnits.IdOf("q1");
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["review"] = new(unit, "review", NodeStates.Reached) { Attempt = 1, Since = T },
                ["landing"] = new(unit, "landing", NodeStates.Reached) { Attempt = 2, Since = T.AddMinutes(1) },
                ["push"] = new(unit, "push", NodeStates.Failed) { Attempt = 2, Since = T.AddMinutes(2), Code = "rejected" },
            },
        });
        static Notice Failed(string run, string node, int attempt) =>
            new($"outcome:{run}/{node}/{attempt}", RunOutcomeNotices.Kind, T, Quest: run, Act: node, State: NodeStates.Failed, Count: attempt);

        var held = new[] { Failed("q1", "landing", 1), Failed("q1", "push", 2), Failed("q1", "push", 1), Failed("gone", "push", 1) };

        Assert.Equal(
            ["outcome:gone/push/1", "outcome:q1/landing/1", "outcome:q1/push/1"],
            RunOutcomeNotices.Read(_home, T.AddMinutes(5)).Moved(held).Order(StringComparer.Ordinal));
    }
}
