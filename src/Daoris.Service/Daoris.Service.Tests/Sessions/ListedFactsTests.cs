using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A list's facts read for all its records at once answer every record's line head and entry key as the store's reads, one at a
/// time, answer them: the same walk and the same rule over the same rows.
/// </summary>
public sealed class ListedFactsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-05T10:00:00Z");

    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;
    private QuestStore _quests = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
        _quests = await QuestStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Every_record_s_head_and_entry_are_the_ones_the_store_s_single_reads_answer()
    {
        var quest = await _quests.PublishAsync("ask #a1b2c3", "Keeper", "A step", "Go on.", Now);
        var step = await _quests.PublishAsync("Keeper", "Keeper", "The next step", "Go on.", Now.AddMinutes(1), parent: quest.Id);
        var asked = await _quests.PublishAsync("Asker", "Keeper", "Asked by a chat", "Go on.", Now.AddMinutes(2));

        // Three attempts at one quest made at one moment, so only the table's own order tells the newest.
        var attempts = new List<Session>();
        for (var i = 0; i < 3; i++) attempts.Add(await _sessions.CreateAsync(quest.Id, "Keeper", "stub", Now));
        await _sessions.CreateAsync(step.Id, "Keeper", "stub", Now.AddMinutes(3));
        var chat = await _sessions.CreateAsync(null, "Keeper", "stub", Now.AddMinutes(4), kind: SessionKind.Chat);
        var handed = await _sessions.CreateAsync(null, "Keeper", "stub", Now.AddMinutes(5), kind: SessionKind.Chat, from: chat.Id, fromWhy: "handed over");
        await _sessions.CreateAsync(null, "Keeper", "stub", Now.AddMinutes(5), kind: SessionKind.Chat, from: chat.Id, fromWhy: "handed over");
        await _sessions.CreateAsync(asked.Id, "Keeper", "stub", Now.AddMinutes(6));
        await _sessions.MarkTookAsync(handed.Id, asked.Id);
        await _sessions.MirrorAsync(attempts[0] with { Id = "other/" + attempts[0].Id, Origin = "other" });
        foreach (var each in await _sessions.ListAsync(includeClosed: true))
        {
            await _sessions.SetStateAsync(each.Id, SessionState.Completed, "Done.", null, null, Now.AddMinutes(9));
        }

        var listed = await _sessions.ListAsync(includeClosed: true);
        var facts = await ListedFacts.ReadAsync(_sessions, _quests, listed);
        var stored = new StoreEntryFacts(_quests, _sessions);
        Assert.True(listed.Count >= 9);
        foreach (var record in listed)
        {
            Assert.Equal(
                (await Lines.HeadOfLineAsync(_sessions, _quests, record))?.Id,
                (await Lines.HeadOfLineAsync(facts, record))?.Id);
            Assert.Equal(await SessionEntries.KeyAsync(record, stored), await SessionEntries.KeyAsync(record, facts));
        }
    }
}
