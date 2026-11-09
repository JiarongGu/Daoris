namespace Daoris.Service.Http.Tests;

/// <summary>
/// The publish door over a quest this machine forgot. Forgotten here by the store's own rows, as a clear of a quest a remote
/// numbered leaves them, since this host has no remote.
/// </summary>
public sealed class ForgottenQuestDoorTests(LocalHost host) : IClassFixture<LocalHost>
{
    /// <summary>
    /// A request key makes a forgotten quest's id again, which the remote still holds closed, so the door refuses it with 409
    /// and the exchange's sentence, as a closed quest's move is. The same plain words are a new ask under the next id.
    /// </summary>
    [Fact]
    public async Task A_forgotten_quests_key_is_refused_with_409_and_its_plain_words_publish_under_the_next_id()
    {
        const string title = "A quest a remote held, forgotten here";
        var keyed = await ClosedAndForgottenAsync(title, "run-1/ask-keeper/1");
        var plain = await ClosedAndForgottenAsync(title, request: null);

        var refused = await PublishAsync(title, "run-1/ask-keeper/1");
        var next = await PublishAsync(title, request: null);

        Assert.Equal(409, refused.Status);
        Assert.StartsWith($"Quest `#{keyed}` was cleared from this machine;", refused.Error);
        Assert.Equal(200, next.Status);
        var id = next.Json.GetProperty("quest").GetProperty("id").GetString();
        Assert.DoesNotContain(id, new[] { keyed, plain });
    }

    private Task<Answer> PublishAsync(string title, string? request) =>
        host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title, body = "Asked.", request });

    private async Task<string> ClosedAndForgottenAsync(string title, string? request)
    {
        var published = await PublishAsync(title, request);
        Assert.Equal(200, published.Status);
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "done", reason = "Landed." })).Status);

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={host.Database}");
        await connection.OpenAsync();
        await using var forget = connection.CreateCommand();
        forget.CommandText = """
            INSERT INTO quest_forgotten (id, at) VALUES ($id, '2026-10-07T10:00:00Z');
            DELETE FROM quest_log WHERE quest = $id; DELETE FROM quests WHERE id = $id;
            """;
        forget.Parameters.AddWithValue("$id", quest);
        await forget.ExecuteNonQueryAsync();
        return quest;
    }
}
