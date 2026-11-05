using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// An answer kept with the store's mark is answered again only while no writer, this connection or another process's, has moved
/// the file since.
/// </summary>
public sealed class MarkedAnswersTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-05T10:00:00Z");

    private readonly string _file = Path.Combine(Path.GetTempPath(), "daoris-mark-" + Guid.NewGuid().ToString("N")[..8] + ".db");
    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection($"Data Source={_file};Pooling=False");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        File.Delete(_file);
    }

    [Fact]
    public async Task The_mark_holds_while_nothing_is_written_and_moves_on_this_connection_s_write_and_on_another_s()
    {
        var first = await _sessions.MarkAsync();
        await _sessions.ListAsync(includeClosed: true);
        Assert.Equal(first, await _sessions.MarkAsync());

        await _sessions.CreateAsync("q1", "Owner", "stub", Now);
        var written = await _sessions.MarkAsync();
        Assert.NotEqual(first, written);

        await using (var other = new SqliteConnection($"Data Source={_file};Pooling=False"))
        {
            await other.OpenAsync();
            await using var command = other.CreateCommand();
            command.CommandText = "UPDATE sessions SET note = 'from a connector' WHERE quest = 'q1'";
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        Assert.NotEqual(written, await _sessions.MarkAsync());
    }

    [Fact]
    public async Task An_answer_is_made_once_at_a_mark_shared_by_readers_at_once_and_made_again_after_a_write()
    {
        var made = 0;
        var kept = new MarkedAnswers<string, int>(_sessions.MarkAsync);
        Task<int> Make() => Task.Run(async () =>
        {
            await Task.Delay(50);
            return Interlocked.Increment(ref made);
        });

        var together = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => kept.AnswerAsync("all", Make)));
        Assert.All(together, answer => Assert.Equal(1, answer));
        Assert.Equal(1, await kept.AnswerAsync("all", Make));
        Assert.Equal(2, await kept.AnswerAsync("other", Make));

        await _sessions.CreateAsync("q2", "Owner", "stub", Now);
        Assert.Equal(3, await kept.AnswerAsync("all", Make));
        Assert.Equal(3, made);
    }
}
