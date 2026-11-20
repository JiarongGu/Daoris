using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The run steps that wait on the person, read from the bound runs and each run's node record: a person's task waits for their Done,
/// an approval for their Approve, and a step their word is already kept on waits on no one.
/// </summary>
public sealed class PersonWaitsTests : NodeRunFixture
{
    private const string TaskGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"look","kind":"task","version":1,"reach":"pressed","outcomes":["done"]},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"look"},
                  {"id":"l>h","kind":"when","from":"look","to":"handed","outcome":"done"}]}
        """;

    [Fact]
    public async Task A_runs_active_task_waits_on_the_person_until_their_word_is_kept_on_it()
    {
        BoundTo(TaskGraph);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        await RunPassAsync(Runtime());
        Assert.Empty(PersonWaitReader.Of([], Files.Read));

        var waits = PersonWaitReader.Read(_home);
        Assert.Equal([(RunId, "look", PersonWaits.Task)], waits.Select(wait => (wait.Run, wait.Node, wait.Why)));
        Assert.Equal(RunStatus("look").Since, waits[0].Since);

        Assert.True(RunNodeWords.Word(Files, RunId, "look", NodeWordActs.Stop, "not wanted", () => _now).Done);
        Assert.Empty(PersonWaitReader.Read(_home));
    }

    [Fact]
    public async Task A_task_another_holder_holds_waits_on_that_holder_not_the_person()
    {
        BoundTo(TaskGraph);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        await RunPassAsync(Runtime());
        var unit = RunUnits.IdOf(RunId);
        new StepTenureFiles(_home, () => true).Begin(unit, [("look", RunStatus("look").Attempt, StepRoles.Owner, CastHolders.Person, _now, TenureWhy.Cast)]);
        Assert.Single(PersonWaitReader.Read(_home));

        new StepTenureFiles(_home, () => true).HandOver(unit, "look", RunStatus("look").Attempt, 1, "agent:beta", TenureWhy.HandOver, _now);

        Assert.Empty(PersonWaitReader.Read(_home));
    }

    private const string CopyGraph = """
        {"schemaVersion":2,"entries":["apart"],"end":"return",
         "nodes":[{"id":"apart","kind":"isolate","version":1,"reach":"observed","options":{"kind":"copy"}},{"id":"work","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},{"id":"return","kind":"return","version":1,"reach":"performed"}],
         "wires":[{"id":"a","kind":"after","from":"apart","to":"work"},{"id":"b","kind":"after","from":"work","to":"review"},
                  {"id":"c","kind":"after","from":"review","to":"return"},{"id":"d","kind":"back","from":"review","on":"rejected","to":"work"}]}
        """;

    [Theory]
    [InlineData(ReturnNode.BothChanged, PersonWaits.Choose)]
    [InlineData(ReturnNode.PlaceBusy, null)]
    public void A_return_waiting_on_files_changed_on_both_sides_waits_on_the_persons_choice_and_a_busy_one_on_no_one(string code, string? why)
    {
        BoundTo(CopyGraph);
        var unit = RunUnits.IdOf(RunId);
        Files.Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["return"] = new(unit, "return", NodeStates.Active) { Attempt = 1, Code = code, Since = _now },
            },
        });

        new StepVacancyFiles(_home).Keep(unit, [new("return", 1, _now, true, false)]);
        Assert.Equal(why is null ? [] : [(RunId, "return", why, true)], PersonWaitReader.Read(_home).Select(wait => (wait.Run, wait.Node, wait.Why, wait.Vacant)));
        YouHold(StepWaiting.RoleOf(PersonWaits.Choose));
        Assert.Equal(why is null ? [] : [(RunId, "return", why)], PersonWaitReader.Read(_home).Select(wait => (wait.Run, wait.Node, wait.Why)));
    }

    private void YouHold(string role) =>
        Assert.Null(new PlaceCastFiles(_home, () => true).Cast(PlaceRiskBook.MachineKey, role, CastHolders.Person, "mine", _now));

    /// <summary>The shape of the work workspace's pull-request runs: the step failed `no-token` and is kept for the person, its run not ended.</summary>
    [Theory]
    [InlineData(null, PersonWaits.Left)]
    [InlineData(HeldResolutions.Again, null)]
    [InlineData(HeldResolutions.Stop, null)]
    public void A_step_failed_and_kept_for_the_person_in_a_run_not_ended_leaves_the_work_until_their_word_is_on_it(string? resolution, string? why)
    {
        BoundTo(CopyGraph);
        var unit = RunUnits.IdOf(RunId);
        var call = new WorkPerformCall(unit, "return", 1, "i-1", "k-1", "tip-1", [], _now);
        Files.Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["return"] = new(unit, "return", NodeStates.Failed) { Attempt = 1, Code = "no-token", Since = _now },
            },
            Attempts =
            [
                new NodeAttempt("return", 1, "acme.deliver", call, AttemptStates.Settled)
                { RecordedAt = _now, Outcome = "failed", Code = "no-token", Resolution = resolution },
            ],
        });

        YouHold(StepWaiting.RoleOf(PersonWaits.Left));
        Assert.Equal(why is null ? [] : [(RunId, "return", why)], PersonWaitReader.Read(_home).Select(wait => (wait.Run, wait.Node, wait.Why)));
    }

    private const string ReleaseGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"release",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"},
                  {"id":"release","kind":"acme.deploy/release","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>r","kind":"after","from":"handed","to":"release"}]}
        """;

    /// <summary>The release whose stage waits on an approval: the actor its plugin names, held by the person, waits on them, as What needs you lists it.</summary>
    [Theory]
    [InlineData("approver", PersonWaits.Actor)]
    [InlineData(null, null)]
    public void A_plugin_step_waiting_on_an_actor_the_person_holds_waits_on_them_and_one_going_on_on_no_one(string? actor, string? why)
    {
        BoundTo(ReleaseGraph);
        var unit = RunUnits.IdOf(RunId);
        var said = _now.AddMinutes(-3);
        var call = new WorkPerformCall("unit-b1", "acme.deploy/release", 1, "i-1", "k-1", "tip-1", [], _now.AddHours(1));
        Files.Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["release"] = new(unit, "release", NodeStates.Active) { Attempt = 1, Tries = 1, Unreconciled = 1, Since = _now.AddMinutes(-10) },
            },
            Attempts =
            [
                new NodeAttempt("release", 1, "acme.deploy", call, AttemptStates.Sent)
                {
                    RecordedAt = _now, Generation = 1, Deliveries = 1,
                    Progress = new StepProgress(actor, "Approve Production in Azure DevOps.", "https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90", "Production", said),
                },
            ],
        });

        new StepVacancyFiles(_home).Keep(unit, [new("release", 1, _now, true, false)]);
        Assert.Equal(why is null ? [] : [(RunId, "release", true)], PersonWaitReader.Read(_home).Select(wait => (wait.Run, wait.Node, wait.Vacant)));
        YouHold(StepRoles.Approver);
        var waits = PersonWaitReader.Read(_home).Select(wait => (wait.Run, wait.Node, wait.Why, (string?)wait.Role, wait.Since)).ToList();

        Assert.Equal(why is null ? [] : [(RunId, "release", why, actor, (DateTimeOffset?)said)], waits);
    }

    [Theory]
    [InlineData(RunOutcomes.Failed, "return", PersonWaits.Left)]
    [InlineData(RunOutcomes.Failed, null, null)]
    [InlineData(RunOutcomes.Stopped, "return", null)]
    [InlineData(RunOutcomes.Completed, null, null)]
    public void A_run_that_ended_failed_at_a_step_leaves_that_step_and_one_stopped_or_completed_leaves_none(string outcome, string? node, string? why)
    {
        BoundTo(CopyGraph);
        var unit = RunUnits.IdOf(RunId);
        Files.Write(new NodeRecord(unit)
        {
            Ended = new RunEnd(RunId, _now) { Outcome = outcome, Node = node },
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["return"] = new(unit, "return", outcome == RunOutcomes.Failed ? NodeStates.Failed : NodeStates.Reached) { Attempt = 1, Since = _now },
            },
        });

        var waits = PersonWaitReader.Read(_home).Select(wait => (wait.Run, wait.Node, wait.Why, wait.Since)).ToList();

        Assert.Equal(why is null ? [] : [(RunId, node!, why, (DateTimeOffset?)_now)], waits);
    }
}
