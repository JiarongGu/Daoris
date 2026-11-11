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
                  {"id":"look","kind":"human","version":1,"reach":"pressed","outcomes":["done"]},
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
}
