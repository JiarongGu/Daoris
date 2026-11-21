using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check red past its rounds is the act <c>work.recover</c>: with nothing configured Daoris gives the run's check up with what it tried,
/// its run ending as gave up, journaled and said, and the person is not held; a row classing it <c>owner</c> keeps it for the person, and
/// with no policy read it stays the person's.
/// </summary>
public sealed class SpentChecksTests : NodeRunFixture
{
    private const string Checked = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"check","kind":"check","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>k","kind":"after","from":"claimed","to":"check"},
                  {"id":"k>h","kind":"after","from":"check","to":"handed"},
                  {"id":"k>c","kind":"back","from":"check","to":"claimed","on":"failed"}]}
        """;

    private static NodeAttempt Red(int attempt) =>
        new("check", attempt, NodeKinds.Check, new WorkPerformCall(RunUnit, NodeKinds.Check, 1, $"i-{attempt}", $"k-{attempt}", Tip1, [], DateTimeOffset.UnixEpoch),
            AttemptStates.Settled) { Outcome = WorkStates.Failed };

    private void Spent()
    {
        BoundTo(Checked);
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = new(RunUnit, "claimed", NodeStates.Reached) { Attempt = 1 },
                ["check"] = new(RunUnit, "check", NodeStates.Failed) { Attempt = 3, Code = "gate-failed" },
            },
            Attempts = [Red(1), Red(2), Red(3)],
        });
    }

    private static DriverConfig Config(string json) => DriverConfig.Parse(json);

    [Fact]
    public void With_nothing_configured_Daoris_gives_the_check_up_with_what_was_tried_and_nobody_is_held()
    {
        Spent();
        var said = new List<string>();

        var given = SpentChecks.Decide(Files, RunId, "check", 3, Config("""{"risk":{}}"""), RunHome.Repository, "default", "the gate `cli` failed", _now, said);

        Assert.True(given, string.Join(" | ", said));
        var stop = Assert.Single(Files.Read(RunUnit)!.Pending);
        Assert.Equal(("check", NodeWordActs.Stop, true), (stop.Node, stop.Act, stop.GaveUp));
        Assert.Contains("Tried:", stop.Words, StringComparison.Ordinal);
        Assert.Contains("3 red rounds", stop.Words, StringComparison.Ordinal);
        Assert.Single(said, line => line.Contains("Daoris gave it up", StringComparison.Ordinal));
        var line = Assert.Single(RiskJournal.Read(_home));
        Assert.Equal((RiskActs.WorkRecover, RiskVerdict.Pass, RunId, "check"), (line.Kind, line.Outcome, line.Run, line.Step));
    }

    [Fact]
    public void Classed_owner_the_spent_check_stays_the_persons_and_with_no_policy_read_it_stays_as_it_was()
    {
        Spent();
        var said = new List<string>();

        Assert.False(SpentChecks.Decide(Files, RunId, "check", 3, Config("""{"risk":{"machine":[{"kind":"work.recover","class":"owner"}]}}"""),
            RunHome.Repository, "default", null, _now, said));
        Assert.False(SpentChecks.Decide(Files, RunId, "check", 3, null, RunHome.Repository, "default", null, _now, said));

        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Empty(said);
        Assert.False(SpentChecks.Passes(Config("""{"risk":{"machine":[{"kind":"work.recover","class":"owner"}]}}"""), "default", RunHome.Repository));
        Assert.Contains("nobody is asked", SpentChecks.Words("check", null, null, 3), StringComparison.Ordinal);
    }
}
