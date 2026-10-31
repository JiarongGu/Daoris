using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

public sealed class LineHeadTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private SessionStore _sessions = null!;
    private QuestStore _quests = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task<Session> RecordAsync(string? quest = null, string? from = null, int minute = 0) =>
        await _sessions.CreateAsync(quest, "Owner", "stub", Now.AddMinutes(minute), kind: quest is null ? SessionKind.Chat : SessionKind.Driven, from: from);

    [Fact]
    public async Task A_part_carried_on_by_from_has_the_last_part_as_its_head()
    {
        var first = await RecordAsync();
        var second = await RecordAsync(from: first.Id, minute: 1);
        var third = await RecordAsync(from: second.Id, minute: 2);
        Assert.Equal(third.Id, (await Lines.HeadOfLineAsync(_sessions, _quests, first))!.Id);
    }

    [Fact]
    public async Task A_chains_head_is_its_newest_steps_session()
    {
        var one = await _quests.PublishAsync("a", "Owner", "one", "b", Now);
        var two = await _quests.PublishAsync("a", "Owner", "two", "b", Now.AddMinutes(1), parent: one.Id);
        var three = await _quests.PublishAsync("a", "Owner", "three", "b", Now.AddMinutes(2), parent: two.Id);
        var onOne = await RecordAsync(one.Id);
        var onTwo = await RecordAsync(two.Id, minute: 1);
        var onThree = await RecordAsync(three.Id, minute: 2);
        Assert.Equal(onThree.Id, (await Lines.HeadOfLineAsync(_sessions, _quests, onOne))!.Id);
        Assert.Equal(onThree.Id, (await Lines.HeadOfLineAsync(_sessions, _quests, onTwo))!.Id);
    }

    [Fact]
    public async Task A_chain_whose_next_step_has_no_session_yet_ends_at_the_last_that_has_one()
    {
        var one = await _quests.PublishAsync("a", "Owner", "one", "b", Now);
        await _quests.PublishAsync("a", "Owner", "two", "b", Now.AddMinutes(1), parent: one.Id);
        var onOne = await RecordAsync(one.Id);
        Assert.Equal(onOne.Id, (await Lines.HeadOfLineAsync(_sessions, _quests, onOne))!.Id);
    }
}
