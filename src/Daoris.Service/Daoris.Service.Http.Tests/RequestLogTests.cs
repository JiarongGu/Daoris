using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The host's half of the machine log (LOG1a, D94): a request that failed is one <c>request.failed</c>
/// line, by its route's pattern and status — never the id in its path, never its query — in the home's
/// <c>logs/</c>, beside the host's own start.
/// </summary>
public sealed class RequestLogTests : IDisposable
{
    private readonly LocalHost _host = new();

    public void Dispose() => _host.Dispose();

    private static JsonElement Parse(string line) => JsonDocument.Parse(line).RootElement;

    [Fact]
    public async Task A_request_that_fails_with_a_500_is_one_line_by_its_routes_pattern_and_never_its_query()
    {
        // The store fails underneath the host: the table an ask is read from is gone. No route has an
        // input that throws, so the failure comes from where a real one would — the store.
        await using (var connection = new SqliteConnection($"Data Source={_host.Database};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            drop.CommandText = "DROP TABLE asks";
            await drop.ExecuteNonQueryAsync();
        }

        // The host answers the throw itself (HOSTLOG1): a 500 with its sentence, and the line on the way out.
        var failed = await _host.GetAsync("/api/asks/secret-id-in-the-path?workspace=secret-words-in-the-query");
        Assert.Equal(500, failed.Status);
        Assert.Equal(Daoris.Knowledge.Http.UnhandledRequests.Logged, failed.Error);

        var lines = _host.LogLines();
        var logged = Assert.Single(lines, line => Parse(line).GetProperty("event").GetString() == "request.failed");
        var entry = Parse(logged);
        Assert.Equal("warn", entry.GetProperty("level").GetString());
        Assert.Equal("host", entry.GetProperty("source").GetString());
        var data = entry.GetProperty("data");
        Assert.Equal("GET", data.GetProperty("method").GetString());
        Assert.Equal("/api/asks/{id}", data.GetProperty("route").GetString());
        Assert.Equal(500, data.GetProperty("status").GetInt32());
        Assert.True(data.TryGetProperty("ms", out _));

        // Not in that line, and not in any other the request caused — the framework's included.
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain("secret-id-in-the-path", line);
            Assert.DoesNotContain("secret-words-in-the-query", line);
        });
        Assert.Contains(lines, line => Parse(line).GetProperty("event").GetString() == "app.started"
            && Parse(line).GetProperty("data").GetProperty("mode").GetString() == "local");
    }

    /// <summary>A request that answered — refusals included — is not a failure, and writes nothing.</summary>
    [Fact]
    public async Task A_request_that_answered_writes_no_line()
    {
        Assert.Equal(404, (await _host.GetAsync("/api/asks/nobody-asked-this")).Status);
        Assert.Equal(400, (await _host.GetAsync("/api/search")).Status);

        Assert.DoesNotContain(_host.LogLines(), line => Parse(line).GetProperty("event").GetString() == "request.failed");
    }
}
