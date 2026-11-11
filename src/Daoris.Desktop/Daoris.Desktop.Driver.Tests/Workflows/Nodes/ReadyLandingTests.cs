using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The install's run whose landing stayed ready with no attempt: its content and its review reached at the branch's tip, its landing made ready
/// after them, its session ended and its quest done. The next pass begins the landing at that tip, so the person's press lands it.
/// </summary>
public sealed class ReadyLandingTests : NodeRunFixture
{
    /// <summary>The install's shape: the claim, the content, the person's review, the landing and its push.</summary>
    private const string ContentReviewLanding = """
        {"schemaVersion":2,"entries":["work"],"end":"push",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"content","kind":"content.holds","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"feature/{slug}-{quest}"}},
                  {"id":"push","kind":"push","version":1,"reach":"performed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"content"},{"id":"b","kind":"after","from":"content","to":"review"},
                  {"id":"e","kind":"after","from":"review","to":"landing"},{"id":"c","kind":"after","from":"landing","to":"push"}]}
        """;

    private NodeStatus At(string node, string digest, string state, string? by = null)
    {
        var bound = new NodeAcceptance(RunUnit, Tip1, digest, node, 1, 1) { Subject = "unit-b1" };
        return new NodeStatus(RunUnit, node, state) { Attempt = 1, Tries = 1, Binding = bound, Acceptance = bound, By = by, Since = _now };
    }

    /// <summary>The branch's tip read in the pass, or not read (the shape on the install): either way the landing begins at the reviewed tip.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_landing_left_ready_after_its_review_is_begun_at_the_tip_the_review_reached(bool tipRead)
    {
        var digest = RunHome.SaveGraph(_home, ContentReviewLanding);
        RunHome.Bind(_home, RunId, digest, _now);
        if (tipRead) _branchTips["daoris/s1"] = Tip1;
        var branch = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]) with { Subject = new WorkSubject("daoris/s1", Tip1) };
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), branch];
        var work = new NodeStatus(RunUnit, "work", NodeStates.Reached) { Attempt = 1, Tries = 1, Since = _now };
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["work"] = work, ["content"] = At("content", digest, NodeStates.Reached, "holds"),
                ["review"] = At("review", digest, NodeStates.Reached, NodeSource.Person),
                ["landing"] = new(RunUnit, "landing", NodeStates.Ready),
            },
        });

        await RunPassAsync(Runtime());

        var landing = RunStatus("landing");
        Assert.Equal(NodeStates.Active, landing.State);
        Assert.Equal(("unit-b1", Tip1), (landing.Binding?.Subject, landing.Binding?.SubjectRevision));
    }
}
