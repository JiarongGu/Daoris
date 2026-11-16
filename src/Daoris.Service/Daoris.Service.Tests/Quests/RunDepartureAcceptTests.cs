using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A quest worked in a workflow's run stays Taken once its agent's done is in: a done that departed from a requirement is
/// held for the person's yes at the run's <c>quest.holds</c>, and their one accept takes it there as it takes a closed done's.
/// </summary>
public sealed class RunDepartureAcceptTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-16T02:00:00Z");

    private static readonly QuestRequirement Report = new("write the report", "The report is in docs/report.md.");

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: "/trees/owner"), Now);
        _exchange = new QuestExchange(service, _quests);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>A quest bound to its run at its first start, taken, and its agent's done in, departing from its requirement.</summary>
    private async Task<(Quest Quest, QuestRespondOutcome Done)> Departed()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now, requirements: [Report]);
        Assert.True((await _quests.BindAsync(quest.Id, quest.Id, Now)).Moved);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(quest.Id, "take", null, Now)).Refusal);
        var departed = new QuestAnswer(1, null, Departed: "It went into the wiki instead.", Quote: Report.Quote);
        var done = await _exchange.RespondAsync(quest.Id, "done", "Built.", Now.AddMinutes(1), answers: [departed]);
        Assert.Equal(QuestRespondRefusal.None, done.Refusal);
        return (done.Quest!, done);
    }

    [Fact]
    public async Task The_persons_accept_takes_a_submitted_done_held_for_its_departure()
    {
        var (quest, _) = await Departed();
        Assert.Equal((QuestStatus.Taken, QuestHold.Departed), (quest.Status, quest.Hold));

        var accepted = await _exchange.AcceptAsync(quest.Id, Now.AddMinutes(5));

        Assert.Equal(QuestRespondRefusal.None, accepted.Refusal);
        Assert.Contains("Accepted the departure", accepted.Message);
        var standing = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal((QuestStatus.Taken, (QuestHold?)null, Now.AddMinutes(5)), (standing.Status, standing.Hold, standing.Accepted));
    }

    /// <summary>
    /// 🔴 A second yes on the submitted done says its departure is accepted already, as a closed done's does: never that only a
    /// closed done takes a yes, which sends the person looking for a door already passed.
    /// </summary>
    [Fact]
    public async Task A_second_yes_on_a_submitted_done_says_it_was_already_accepted()
    {
        var (quest, _) = await Departed();
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.AcceptAsync(quest.Id, Now.AddMinutes(5))).Refusal);

        var again = await _exchange.AcceptAsync(quest.Id, Now.AddMinutes(6));

        Assert.Equal(QuestRespondRefusal.NotHeld, again.Refusal);
        Assert.Contains("already accepted", again.Message);
        Assert.DoesNotContain("only a quest closed done", again.Message);
    }

    /// <summary>🔴 A yes on a taken quest whose done is not in says that is what waits, never that only a closed done takes one.</summary>
    [Fact]
    public async Task A_yes_before_the_done_is_in_says_its_done_is_not_in_yet()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", Now, requirements: [Report]);
        Assert.True((await _quests.BindAsync(quest.Id, quest.Id, Now)).Moved);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(quest.Id, "take", null, Now)).Refusal);

        var early = await _exchange.AcceptAsync(quest.Id, Now.AddMinutes(1));

        Assert.Equal(QuestRespondRefusal.NotHeld, early.Refusal);
        Assert.Contains("its done is not in yet", early.Message);
        Assert.DoesNotContain("only a quest closed done", early.Message);
    }

    /// <summary>🔴 The done's answer to its session says its run waits on the person's yes, and names the accept that takes it.</summary>
    [Fact]
    public async Task The_dones_answer_says_its_run_waits_on_the_yes_and_names_the_accept()
    {
        var (quest, done) = await Departed();

        Assert.Contains("its workflow's run waits on it, and writes its Done at its end, only once they accept it", done.Message);
        Assert.Contains($"`daoris quest accept {quest.Id}`", done.Message);
        Assert.DoesNotContain("nothing follows it", done.Message);
    }
}
