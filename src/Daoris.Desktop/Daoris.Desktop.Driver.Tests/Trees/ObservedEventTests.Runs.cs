using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CUTOVER1c (the cutover design §2.4.1): a run's observed plugin node told under its subject's unit, its session's branch before the
/// landing and the landed branch at its landing's tip after it, and its event mapped back to the run's record. A kept observation names its
/// subject, and a landing's telling is judged on the tip told, which the landing's record keeps. Each seen failing first.
/// </summary>
public sealed partial class ObservedEventTests
{
    private const string B2 = "engine~feature-b2";

    private static readonly DateTimeOffset Opened = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The plugin's observed deploy between the claim and the landing: its subject the run's session branch.</summary>
    private const string DeployedBeforeHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deployed","kind":"acme.deploy/deployed","version":1,"reach":"observed"},
                  {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deployed"},{"id":"d>h","kind":"after","from":"deployed","to":"handed"}]}
        """;

    /// <summary>The plugin's observed deploy after the landing: its subject the run's branch at its landing.</summary>
    private const string DeployedAfterHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"deployed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"},
                  {"id":"deployed","kind":"acme.deploy/deployed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>d","kind":"after","from":"handed","to":"deployed"}]}
        """;

    private static string RunOf(string run) => RunUnits.IdOf(run);

    /// <summary>
    /// An event at the run's session branch reaches the run's record, bound through the frame that told its plugin the branch unit; the kept
    /// observation names that subject, and the branch unit's own record is never written.
    /// </summary>
    [Fact]
    public async Task An_event_at_a_runs_session_branch_reaches_the_runs_record_under_its_subjects_unit()
    {
        RunHome.Bind(_home, "q7", RunHome.SaveGraph(_home, DeployedBeforeHanded), _now);
        _units = [RunHome.QuestUnit(RunHome.Quest("q7", "Done")), RunHome.Branch(A, "feature/a", Opened, ["q7"])];
        var runtime = Runtime();
        await PassAsync(runtime, "{}");

        var told = Assert.Single(_port.SentTo(Plugin, 1)!.Units);
        Assert.Equal((A, "feature/a", C1), (told.Unit, told.Branch, told.Tip));
        Assert.Equal(["deployed"], told.Awaiting);

        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now));
        await PassAsync(runtime, "{}", whole: false);

        var record = Record(RunOf("q7"));
        Assert.Equal((NodeStates.Reached, A, C1), (record.Statuses["deployed"].State, record.Statuses["deployed"].Acceptance!.Subject,
            record.Statuses["deployed"].Acceptance!.SubjectRevision));
        Assert.Equal(A, Assert.Single(record.Observations).Subject);
        Assert.Equal(NodeStates.Active, record.Statuses["handed"].State);
        Assert.False(File.Exists(NodeRecordFiles.PathOf(_home, A)));
        Assert.Empty(_port.Refusals);
    }

    /// <summary>
    /// The chain's next session replaces the run's branch with another at the same commit: the node is begun again there, and the same event on the
    /// new branch is taken, though the old branch's, at that commit and under that id, is remembered (§2.2: a branch at an equal commit inherits
    /// no replay from the one it replaced).
    /// </summary>
    [Fact]
    public async Task An_event_on_a_branch_that_replaced_another_at_the_same_commit_is_taken_though_the_old_ones_is_remembered()
    {
        RunHome.Bind(_home, "q7", RunHome.SaveGraph(_home, DeployedBeforeHanded), _now);
        var q7 = RunHome.QuestUnit(RunHome.Quest("q7", "Done"));
        var a = RunHome.Branch(A, "feature/a", Opened, ["q7"]);
        _units = [q7, a];
        var runtime = Runtime();
        await PassAsync(runtime, "{}");
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now));
        await PassAsync(runtime, "{}", whole: false);
        Assert.Equal(NodeStates.Reached, Record(RunOf("q7")).Statuses["deployed"].State);

        _units = [q7, RunHome.QuestUnit(RunHome.Quest("q8", "Done", parent: "q7")), a, RunHome.Branch(B2, "feature/b", Opened.AddHours(1), ["q8"])];
        await PassAsync(runtime, "{}");
        var rebound = Record(RunOf("q7")).Statuses["deployed"];
        Assert.Equal((NodeStates.Active, B2, C1), (rebound.State, rebound.Binding!.Subject, rebound.Binding.SubjectRevision));

        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now, branch: "feature/b"));
        await PassAsync(runtime, "{}", whole: false);

        var record = Record(RunOf("q7"));
        Assert.Equal((NodeStates.Reached, B2), (record.Statuses["deployed"].State, record.Statuses["deployed"].Acceptance!.Subject));
        Assert.Equal([A, B2], record.Observations.Select(each => each.Subject));
        Assert.Empty(_port.Refusals);
    }

    /// <summary>
    /// Two runs landed on one branch, each at its own tip: each run's node after its landing is told that branch at its landing's tip, and each
    /// takes its own event, judged on the tip told, so the first run's is taken though the later landing has moved the branch on.
    /// </summary>
    [Fact]
    public async Task Two_runs_landings_on_one_landed_branch_each_take_their_own_event_one_after_a_later_landing_moved_it()
    {
        var digest = RunHome.SaveGraph(_home, DeployedAfterHanded);
        RunHome.Bind(_home, "q7", digest, _now);
        RunHome.Bind(_home, "q8", digest, _now);
        _tips["feature/b"] = C2;
        _tips["release"] = C2;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest("q7", "Done")), RunHome.QuestUnit(RunHome.Quest("q8", "Done")),
            RunHome.Branch(A, "feature/a", Opened, ["q7"], RunHome.Landing(A, "release", C1, _now, "q7")),
            RunHome.Branch(B2, "feature/b", Opened, ["q8"], RunHome.Landing(B2, "release", C2, _now.AddMinutes(1), "q8")),
        ];
        var runtime = Runtime();
        await PassAsync(runtime, "{}");

        var told = _port.SentTo(Plugin, 1)!.Units.Where(unit => unit.Branch == "release").OrderBy(unit => unit.Tip, StringComparer.Ordinal).ToList();
        Assert.Equal([(A, C1), (B2, C2)], told.Select(unit => (unit.Unit!, unit.Tip)));

        _port.Heard.Enqueue(Event(WorkStates.Reached, "e-a", _now, commit: C1, branch: "release"));
        _port.Heard.Enqueue(Event(WorkStates.Reached, "e-b", _now, commit: C2, branch: "release"));
        await PassAsync(runtime, "{}", whole: false);

        Assert.Equal((NodeStates.Reached, C1), (Record(RunOf("q7")).Statuses["deployed"].State, Record(RunOf("q7")).Statuses["deployed"].Acceptance!.SubjectRevision));
        Assert.Equal((NodeStates.Reached, C2), (Record(RunOf("q8")).Statuses["deployed"].State, Record(RunOf("q8")).Statuses["deployed"].Acceptance!.SubjectRevision));
        Assert.Empty(_port.Refusals);
    }

    /// <summary>
    /// One landing carrying two runs' work, so one branch at one tip for both: neither is told it, the pass says so, and an event there moves
    /// neither run's node (§2.4.1; the person's skip passes it, CUTOVER1f).
    /// </summary>
    [Fact]
    public async Task One_branch_told_for_two_runs_at_one_tip_is_told_for_neither()
    {
        var digest = RunHome.SaveGraph(_home, DeployedAfterHanded);
        RunHome.Bind(_home, "q7", digest, _now);
        RunHome.Bind(_home, "q8", digest, _now);
        _tips["release"] = C1;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest("q7", "Done")), RunHome.QuestUnit(RunHome.Quest("q8", "Done")),
            RunHome.Branch(A, "feature/a", Opened, ["q7"], RunHome.Landing(A, "release", C1, _now, "q7")),
            RunHome.Branch(B2, "feature/b", Opened, ["q8"], RunHome.Landing(B2, "release", C1, _now, "q8")),
        ];
        var runtime = Runtime();
        var pass = await PassAsync(runtime, "{}");

        Assert.DoesNotContain(_port.SentTo(Plugin, 1)?.Units ?? [], unit => unit.Branch == "release");
        Assert.Single(pass.Said, line => line.Contains("`release`", StringComparison.Ordinal) && line.Contains("neither", StringComparison.Ordinal));

        _port.Heard.Enqueue(Event(WorkStates.Reached, "e1", _now, commit: C1, branch: "release"));
        await PassAsync(runtime, "{}", whole: false);

        Assert.Equal((NodeStates.Active, NodeStates.Active), (Record(RunOf("q7")).Statuses["deployed"].State, Record(RunOf("q8")).Statuses["deployed"].State));
        Assert.Equal([(Plugin, WorkRefusals.NotWatched)], _port.Refusals);
    }
}
