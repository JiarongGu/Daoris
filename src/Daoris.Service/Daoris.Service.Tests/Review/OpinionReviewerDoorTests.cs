using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// An opinion's reviewer names its door by <c>door</c>: a row kept with <c>adapter</c> in its asking JSON has it
/// renamed as the store opens, every other part of the reviewer kept, and a second open changes nothing.
/// </summary>
public sealed class OpinionReviewerDoorTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task A_row_naming_its_reviewers_adapter_reads_it_as_the_door_and_the_rename_runs_once()
    {
        var opinions = await OpinionStore.OpenAsync(_connection);
        var made = new Opinion(
            "o-1", Opinions.Landing, Opinions.First, "w-1", "s-1", new OpinionCandidate("Owner", "b", "t", ["t"]),
            new OpinionReviewer("codex-acp", Opinions.AnotherMaker) { Product = "Codex", Account = "work" },
            DateTimeOffset.Parse("2026-10-09T10:00:00Z"))
        {
            Posture = Opinions.CopyAlone,
        };
        await opinions.CreateAsync(made);
        await using (var age = _connection.CreateCommand())
        {
            age.CommandText = "UPDATE opinions SET asking = json_set(json_remove(asking, '$.reviewer.door'), '$.reviewer.adapter', 'codex-acp')";
            await age.ExecuteNonQueryAsync();
        }

        var reopened = await OpinionStore.OpenAsync(_connection);
        await OpinionStore.OpenAsync(_connection);

        var read = (await reopened.FindAsync("o-1"))!;
        Assert.Equal(("codex-acp", "Codex", "work"), (read.Reviewer.Door, read.Reviewer.Product, read.Reviewer.Account));
        await using var probe = _connection.CreateCommand();
        probe.CommandText = "SELECT json_extract(asking, '$.reviewer.adapter') FROM opinions";
        Assert.Equal(DBNull.Value, await probe.ExecuteScalarAsync());
    }
}
