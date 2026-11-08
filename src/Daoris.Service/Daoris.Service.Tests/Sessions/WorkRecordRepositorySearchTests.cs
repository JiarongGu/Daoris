using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>A search naming repositories finds a record of one of them however many records of others rank above it.</summary>
public sealed class WorkRecordRepositorySearchTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private WorkRecordStore _records = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-24T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        await QuestStore.OpenAsync(_connection);
        await SessionStore.OpenAsync(_connection);
        _records = await WorkRecordStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static WorkRecordPost Post(string repository, string folder, string subject) =>
        new("default", repository, folder, $"/work/trees/games/{repository}/{folder}", Now.AddHours(-1), null, "daoris/s-1", "main",
            new WorkRecordHold("line", "main", "branch"), "tip9", "base0", null, [new WorkRecordCommit("c1", subject, [])], null,
            new WorkRecordLeft([], 0, false), Whole: false);

    [Fact]
    public async Task A_repository_filter_reaches_a_record_ranked_below_the_two_hundred_rows_of_others()
    {
        for (var each = 0; each < 250; each++) await _records.PostAsync(Post("Noisy", $"n-{each}", "pelican pelican pelican pelican"), Now);
        await _records.PostAsync(Post("Owner", "o-1", "Teach the pelican to land"), Now);

        var hits = await _records.SearchRecordsAsync(new KnowledgeQuery("pelican") { Repositories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "owner" } });

        var hit = Assert.Single(hits);
        Assert.Equal("Owner", hit.Entry.Repository);
    }
}
