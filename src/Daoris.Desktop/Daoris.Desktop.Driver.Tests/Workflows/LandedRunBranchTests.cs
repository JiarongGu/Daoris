using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run whose landing has reached keeps the steps bound to the landed branch, so it never goes back past its landing and the step after it
/// (a push, a pull request, a release) when another branch comes to stand as the run's: a newer one of another repository's work on its ask,
/// or an earlier attempt's left standing once the look tidied the landed one away. The chain coming back here still takes it back.
/// </summary>
public sealed class LandedRunBranchTests : NodeRunFixture
{
    /// <summary>The claim, the landing, and the plugin's performed step after it: a release whose effect is outside the machine.</summary>
    private const string LandedThenReleased = """
        {"schemaVersion":2,"entries":["claimed"],"end":"release",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"},
                  {"id":"release","kind":"acme.deploy/deploy","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>r","kind":"after","from":"handed","to":"release"}]}
        """;

    private readonly WorkUnit _quest = RunHome.QuestUnit(RunHome.Quest(RunId, "Done"));

    private readonly WorkUnit _b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened.AddHours(1), [RunId]);

    /// <summary>The run landed from <c>unit-b1</c> at <see cref="NodeRunFixture.Tip1"/>, and its release sent once.</summary>
    private async Task<(NodeRuntime Runtime, WorkUnit Landed)> ReleasingAsync(params WorkUnit[] beside)
    {
        BoundTo(LandedThenReleased);
        _branchTips["daoris/s1"] = Tip1;
        _units = [_quest, _b1, .. beside];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        var landed = _b1 with { Records = _b1.Records with { Landings = [RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId)] } };
        _units = [_quest, landed, .. beside];
        await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (RunStatus("handed").State, RunStatus("release").State));
        Assert.Single(_port.Sent);
        return (runtime, landed);
    }

    private void StaysAtItsRelease()
    {
        var handed = RunStatus("handed");
        Assert.Equal((NodeStates.Reached, "unit-b1", Tip1), (handed.State, handed.Acceptance?.Subject, handed.Acceptance?.SubjectRevision));
        var release = RunStatus("release");
        Assert.Equal((NodeStates.Active, 1), (release.State, release.Attempt));
        Assert.Equal(NodeStates.Reached, RunStatus("claimed").State);
        Assert.Single(_port.Sent);
    }

    /// <summary>
    /// Another repository's branch on the same ask, opened later, reads as the run quest's work (its landing was named for the chain's first
    /// quest): the run's landing and its release stay where they stood, and nothing is landed or sent again.
    /// </summary>
    [Fact]
    public async Task A_newer_branch_of_another_repository_on_its_quest_never_takes_a_landed_run_back()
    {
        var (runtime, landed) = await ReleasingAsync();
        var elsewhere = RunHome.Branch("unit-c1", "daoris/s9", Opened.AddHours(2), [RunId]) with
        {
            Owner = new WorkOwner("other", "default", SessionBranches.FileName, WorkRecord.DriverWriter),
        };
        _branchTips["daoris/s9"] = Tip2;
        _units = [_quest, landed, elsewhere];

        await RunPassAsync(runtime);
        await RunPassAsync(runtime);

        StaysAtItsRelease();
    }

    /// <summary>
    /// The look's tidy removed the landed branch and its landing from the record, and an earlier attempt's session branch still stands: the
    /// run stays at its release rather than going back to judge, land and push that earlier branch.
    /// </summary>
    [Fact]
    public async Task An_earlier_attempts_branch_left_standing_once_the_landed_one_is_tidied_never_takes_the_run_back()
    {
        var earlier = RunHome.Branch("unit-b0", "daoris/s0", Opened, [RunId]);
        _branchTips["daoris/s0"] = Tip2;
        var (runtime, _) = await ReleasingAsync(earlier);
        _branchTips.Remove("daoris/s1");
        _units = [_quest, earlier];

        await RunPassAsync(runtime);
        await RunPassAsync(runtime);

        StaysAtItsRelease();
    }

    /// <summary>The chain coming back here with a quest of its own still takes a landed run back to the claim, on the new branch.</summary>
    [Fact]
    public async Task The_chain_coming_back_still_takes_a_landed_run_back_to_its_claim()
    {
        var (runtime, landed) = await ReleasingAsync();
        var next = RunHome.QuestUnit(RunHome.Quest("q8", "Taken", parent: RunId));
        var b2 = RunHome.Branch("unit-b2", "daoris/s2", Opened.AddHours(2), ["q8"]);
        _branchTips["daoris/s2"] = Tip2;
        _units = [_quest, next, landed, b2];

        await RunPassAsync(runtime);

        var claimed = RunStatus("claimed");
        Assert.Equal((NodeStates.Active, "quest:q8"), (claimed.State, claimed.Binding!.Subject));
        Assert.Equal(NodeStates.Blocked, RunStatus("handed").State);

        // The release sent before stays told on the landing it was sent for, so its plugin keeps running and its answer reaches the record.
        var told = Assert.Single(_port.Told[^1].GetValueOrDefault(Plugin) ?? []);
        Assert.Equal(("unit-b1", "feature/q7", Tip1, true), (told.Unit, told.Branch, told.Tip, told.Landing));
        Assert.Equal(["deploy"], told.Awaiting);
    }
}
