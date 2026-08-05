using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

public sealed class QuestStoreTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-05T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private Task<Quest> Publish(string title = "Adopt the canon") =>
        _quests.PublishAsync("Asker", "Owner", title, "Four rules collide.", Now);

    /// <summary>
    /// A quest lives HERE, not in the receiving repository's files. That is the correction the first
    /// version needed: writing into a sibling's tree is an outside edit however small, and it is what
    /// `repository-owns-its-work` exists to prevent.
    /// </summary>
    [Fact]
    public async Task A_published_quest_is_open_and_addressed()
    {
        var quest = await Publish();

        Assert.Equal(QuestStatus.Open, quest.Status);
        Assert.Equal("Asker", quest.From);
        Assert.Equal("Owner", quest.To);
        Assert.Contains("Four rules collide", quest.Body);
    }

    /// <summary>An agent that retries should not produce a second copy of the same ask.</summary>
    [Fact]
    public async Task Publishing_the_same_quest_twice_returns_the_first()
    {
        var first = await Publish();
        var second = await Publish();

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await _quests.ListAsync());
    }

    [Fact]
    public async Task Taking_moves_the_status_without_closing_it()
    {
        var quest = await Publish();

        var taken = await _quests.SetStatusAsync(quest.Id, QuestStatus.Taken, null, Now.AddDays(1));

        Assert.Equal(QuestStatus.Taken, taken!.Status);
        Assert.Single(await _quests.ListAsync());
    }

    /// <summary>The reason is the part the asker can act on; a bare refusal tells them nothing.</summary>
    [Fact]
    public async Task Declining_records_its_reason_and_closes_the_quest()
    {
        var quest = await Publish();

        var declined = await _quests.SetStatusAsync(
            quest.Id, QuestStatus.Declined, "That rule is deliberately local here.", Now.AddDays(1));

        Assert.Equal(QuestStatus.Declined, declined!.Status);
        Assert.Contains("deliberately local", declined.Note);
        Assert.Empty(await _quests.ListAsync());
        Assert.Single(await _quests.ListAsync(includeClosed: true));
    }

    [Fact]
    public async Task Quests_can_be_read_by_who_owes_them()
    {
        await Publish();
        await _quests.PublishAsync("Asker", "Someone", "Different ask", "why", Now);

        Assert.Single(await _quests.ListAsync("Owner"));
        Assert.Empty(await _quests.ListAsync("Nobody"));
    }

    /// <summary>Outstanding work first: a finished queue pushing live work off the end stops being read.</summary>
    [Fact]
    public async Task Open_and_taken_sort_before_closed()
    {
        var done = await _quests.PublishAsync("Asker", "Owner", "Finished", "b", Now);
        await _quests.SetStatusAsync(done.Id, QuestStatus.Done, null, Now);
        await Publish("Still open");

        var listed = await _quests.ListAsync("Owner", includeClosed: true);

        Assert.Equal("Still open", listed[0].Title);
    }

    [Fact]
    public async Task An_unknown_id_yields_nothing_rather_than_throwing()
    {
        Assert.Null(await _quests.SetStatusAsync("zzzzzz", QuestStatus.Taken, null, Now));
        Assert.Null(await _quests.FindAsync("zzzzzz"));
    }
}
