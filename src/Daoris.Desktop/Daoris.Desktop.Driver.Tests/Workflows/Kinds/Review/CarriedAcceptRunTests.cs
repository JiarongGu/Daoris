using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A carried Accept through the node runtime stands for its landing's repair alone: it names the record the person accepted, and a new
/// handback or a send-back before the review is accepted again lapses it, so the review asks the person again.
/// </summary>
public sealed class CarriedAcceptRunTests : NodeRunFixture
{
    private const string Graph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},
                  {"id":"r>h","kind":"after","from":"review","to":"handed"}]}
        """;

    private void OnBranch(string tip, long sequence)
    {
        _branchTips["daoris/s1"] = tip;
        var done = RunHome.Quest(RunId, "Done") with { From = "ask:a1", Submission = new QuestSubmissionView("m1", sequence, _now, $"answer {sequence}") };
        _units = [RunHome.QuestUnit(done), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    /// <summary>The review accepted at <see cref="NodeRunFixture.Tip1"/>, and the landing's repair kept for it as the performer keeps one.</summary>
    private async Task<int> AcceptedAndCarriedAsync()
    {
        BoundTo(Graph);
        OnBranch(Tip1, 1);
        await RunPassAsync(Runtime());
        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Accept, null, () => _now).Done);
        await RunPassAsync(Runtime());
        var accepted = RunStatus("review");
        Assert.Equal(NodeStates.Reached, accepted.State);
        new WorkplaceWorkflowsOperations().KeepRepair(_home, () => true, RunUnit, "review", Tip1, "carry-1", 1, _now);
        return accepted.Acceptance!.Attempt;
    }

    [Fact]
    public async Task The_repair_names_the_review_attempt_its_accept_was_given_on()
    {
        var attempt = await AcceptedAndCarriedAsync();

        Assert.Equal(attempt, Assert.Single(Files.Read(RunUnit)!.Repairs).Attempt);
    }

    [Fact]
    public async Task A_new_handback_lapses_the_carried_accept_and_the_review_asks_again()
    {
        await AcceptedAndCarriedAsync();

        OnBranch(Tip2, 2);
        await RunPassAsync(Runtime());
        await RunPassAsync(Runtime());

        Assert.Equal(NodeStates.Active, RunStatus("review").State);
        Assert.Empty(Files.Read(RunUnit)!.Repairs);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }

    [Fact]
    public async Task A_send_back_lapses_the_carried_accept()
    {
        await AcceptedAndCarriedAsync();
        OnBranch(Tip2, 1);
        await RunPassAsync(Runtime());
        Assert.Equal(NodeStates.Active, RunStatus("review").State);
        Assert.NotEmpty(Files.Read(RunUnit)!.Repairs);

        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Changes, "Not like this.", () => _now).Done);
        await RunPassAsync(Runtime());

        Assert.Equal(NodeStates.Rejected, RunStatus("review").State);
        Assert.Empty(Files.Read(RunUnit)!.Repairs);
    }
}
