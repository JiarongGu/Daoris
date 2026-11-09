using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>The open's repair of a source's reopen a build before the field kept no row of: it writes only a row its replay changes.</summary>
public sealed class QuestReopenRepairTests : QuestReopenFixture
{
    /// <summary>
    /// A step that submitted, met its source's reopen and went on holds both operations and no reopen of its own, which no
    /// replay can set, so opening the store again writes nothing.
    /// </summary>
    [Fact]
    public async Task A_step_gone_on_after_its_source_reopened_is_not_replayed_at_every_open()
    {
        var (quest, first, step) = await Sealed();
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(step.Id, "take", null, Now)).Refusal);
        Assert.True((await _quests.SeedBound(step.Id, Graph, Now)).Moved);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(step.Id, "ready", "Verified.", Now, answers: [Met(1), Met(2)])).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await Reopen(quest, first)).Refusal);
        await Ready(quest);
        Assert.Equal(QuestRespondRefusal.None, (await Found(quest)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.ContinueAsync(step.Id, Now)).Refusal);
        Assert.Null((await Find(step.Id)).Reopened);
        await using (var watch = _connection.CreateCommand())
        {
            watch.CommandText = """
                CREATE TABLE cache_writes (quest TEXT NOT NULL);
                CREATE TRIGGER cache_written AFTER UPDATE ON quests BEGIN INSERT INTO cache_writes VALUES (new.id); END;
                """;
            await watch.ExecuteNonQueryAsync();
        }

        await QuestStore.OpenAsync(_connection);

        await using var count = _connection.CreateCommand();
        count.CommandText = "SELECT group_concat(quest) FROM cache_writes";
        Assert.Equal(DBNull.Value, await count.ExecuteScalarAsync());
    }
}
