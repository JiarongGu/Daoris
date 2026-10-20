using System.Diagnostics;
using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The change stream hears another process (FABRIC-EVENT3b, the fabric events design §A4.2): each session's connector writes the
/// store from its own process, so the host's observer must see a commit no connection of its own made. The host runs as the shell
/// starts it, over a scratch home of its own; this test's process is the connector, writing the host's file directly.
/// </summary>
public sealed class ChangeStreamProcessTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "daoris-changes-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 20 && Directory.Exists(_scratch); attempt++)
        {
            try
            {
                Directory.Delete(_scratch, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    /// <summary>
    /// A connector's commit wakes the wait, naming its family; and a wait still held never holds the host's stop, which the shell
    /// waits for (FLAKE2): the stop answers it.
    /// </summary>
    [Fact]
    public async Task A_write_from_another_process_wakes_the_wait()
    {
        using var host = await RealHost.StartAsync(_scratch, new Dictionary<string, string?> { [InputEndStop.Variable] = "1" });
        using var http = new HttpClient { Timeout = Poll.Patience };

        var since = Since(await http.GetStringAsync($"{host.Url}{ChangeStream.Route}"));
        var waiting = http.GetStringAsync($"{host.Url}{ChangeStream.Route}?since={since}&wait=30");
        await Task.Delay(300);
        Assert.False(waiting.IsCompleted, "a current cursor was answered before anything moved");

        // The connector's write: its own connection, in this process, to the file the host serves.
        var clock = Stopwatch.StartNew();
        await using (var connector = new SqliteConnection($"Data Source={Path.Combine(_scratch, "index", "knowledge.db")};Pooling=False"))
        {
            await connector.OpenAsync();
            var sessions = await SessionStore.OpenAsync(connector);
            await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        }

        var answer = JsonDocument.Parse(await waiting.WaitAsync(Poll.Patience)).RootElement;
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"{clock.Elapsed}");
        Assert.Equal(
            [ChangeFamilies.Sessions],
            answer.GetProperty("moved").EnumerateArray().Select(family => family.GetString()!).ToArray());

        // A wait held as the host stops: answered by the stop, never waited out.
        var held = http.GetStringAsync($"{host.Url}{ChangeStream.Route}?since={Since(answer.GetRawText())}&wait=30");
        await Task.Delay(300);
        clock.Restart();
        Assert.Null(host.StopByInput(Poll.Patience));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"the stop took {clock.Elapsed}");
        var stopped = JsonDocument.Parse(await held.WaitAsync(TimeSpan.FromSeconds(5))).RootElement;
        Assert.Empty(stopped.GetProperty("moved").EnumerateArray());
    }

    /// <summary>
    /// PUSH1d1 (D182 point 5): an ask a connector writes from its own process wakes the host's held wait, naming the asks family
    /// alone, so the windows hear it without waiting for the look.
    /// </summary>
    [Fact]
    public async Task An_ask_written_from_another_process_wakes_the_wait()
    {
        using var host = await RealHost.StartAsync(_scratch, new Dictionary<string, string?> { [InputEndStop.Variable] = "1" });
        using var http = new HttpClient { Timeout = Poll.Patience };

        var since = Since(await http.GetStringAsync($"{host.Url}{ChangeStream.Route}"));
        var waiting = http.GetStringAsync($"{host.Url}{ChangeStream.Route}?since={since}&wait=30");
        await Task.Delay(300);
        Assert.False(waiting.IsCompleted, "a current cursor was answered before anything moved");

        var clock = Stopwatch.StartNew();
        await using (var connector = new SqliteConnection($"Data Source={Path.Combine(_scratch, "index", "knowledge.db")};Pooling=False"))
        {
            await connector.OpenAsync();
            var asks = await AskStore.OpenAsync(connector);
            var now = DateTimeOffset.UtcNow;
            await asks.SaveAsync(new Ask("a5c0de", Workspaces.Default, "Make the bridge faster", AskState.Open, "declarations", now, now));
        }

        var answer = JsonDocument.Parse(await waiting.WaitAsync(Poll.Patience)).RootElement;
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"{clock.Elapsed}");
        Assert.Equal(
            [ChangeFamilies.Asks],
            answer.GetProperty("moved").EnumerateArray().Select(family => family.GetString()!).ToArray());
        Assert.Null(host.StopByInput(Poll.Patience));
    }

    private static string Since(string body) => JsonDocument.Parse(body).RootElement.GetProperty("since").GetString()!;
}
