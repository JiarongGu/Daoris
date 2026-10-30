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
    /// <summary>
    /// The wait these tests ask for, the longest the host holds one. The real host's clock is the machine's, so an answer is told
    /// woken (or answered by the stop) from one at its end by coming before it, counted from the ask (TESTCLOCK3): the code's own
    /// bound, where a few seconds of the test's were the machine's load.
    /// </summary>
    private static readonly TimeSpan Wait = ChangeStream.LongestWait;

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
        var asked = Stopwatch.StartNew();
        var waiting = http.GetStringAsync($"{host.Url}{ChangeStream.Route}?since={since}&wait={(int)Wait.TotalSeconds}");
        await Task.Delay(300);
        Assert.False(waiting.IsCompleted, "a current cursor was answered before anything moved");

        // The connector's write: its own connection, in this process, to the file the host serves.
        await using (var connector = new SqliteConnection($"Data Source={Path.Combine(_scratch, "index", "knowledge.db")};Pooling=False"))
        {
            await connector.OpenAsync();
            var sessions = await SessionStore.OpenAsync(connector);
            await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        }

        var answer = JsonDocument.Parse(await waiting.WaitAsync(Poll.Patience)).RootElement;
        Assert.True(asked.Elapsed < Wait, $"answered at its wait's end, not woken: {asked.Elapsed}");
        Assert.Equal(
            [ChangeFamilies.Sessions],
            answer.GetProperty("moved").EnumerateArray().Select(family => family.GetString()!).ToArray());

        // A wait held as the host stops: answered by the stop, never waited out, so the stop never waits for it.
        asked.Restart();
        var held = http.GetStringAsync($"{host.Url}{ChangeStream.Route}?since={Since(answer.GetRawText())}&wait={(int)Wait.TotalSeconds}");
        await Task.Delay(300);
        Assert.Null(host.StopByInput(Poll.Patience));
        var stopped = JsonDocument.Parse(await held.WaitAsync(Poll.Patience)).RootElement;
        Assert.True(asked.Elapsed < Wait, $"the stop and its answer waited out the held wait: {asked.Elapsed}");
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
        var asked = Stopwatch.StartNew();
        var waiting = http.GetStringAsync($"{host.Url}{ChangeStream.Route}?since={since}&wait={(int)Wait.TotalSeconds}");
        await Task.Delay(300);
        Assert.False(waiting.IsCompleted, "a current cursor was answered before anything moved");

        await using (var connector = new SqliteConnection($"Data Source={Path.Combine(_scratch, "index", "knowledge.db")};Pooling=False"))
        {
            await connector.OpenAsync();
            var asks = await AskStore.OpenAsync(connector);
            var now = DateTimeOffset.UtcNow;
            await asks.SaveAsync(new Ask("a5c0de", Workspaces.Default, "Make the bridge faster", AskState.Open, "declarations", now, now));
        }

        var answer = JsonDocument.Parse(await waiting.WaitAsync(Poll.Patience)).RootElement;
        Assert.True(asked.Elapsed < Wait, $"answered at its wait's end, not woken: {asked.Elapsed}");
        Assert.Equal(
            [ChangeFamilies.Asks],
            answer.GetProperty("moved").EnumerateArray().Select(family => family.GetString()!).ToArray());
        Assert.Null(host.StopByInput(Poll.Patience));
    }

    /// <summary>
    /// FOUNDATION1 row k part 2 (the foundation design §4.1, F10): a journal-only append a connector's process makes, a word taken, wakes a reader
    /// that asks for the journal and wakes an older client's reader by nothing. The older client's reader is the driver's own
    /// (<c>Driver/Service/LedgerChanges.cs</c>), which a service test cannot run: it asks the route as that reader does (<c>wait</c>, then
    /// <c>since</c>, no other parameter) and wakes its <c>ledger</c> topic for any family the answer names, which is what the cases below judge.
    /// </summary>
    [Fact]
    public async Task A_word_taken_from_another_process_wakes_the_reader_that_asks_for_the_journal_and_the_older_clients_by_nothing()
    {
        using var host = await RealHost.StartAsync(_scratch, new Dictionary<string, string?> { [InputEndStop.Variable] = "1" });
        using var http = new HttpClient { Timeout = Poll.Patience };
        await using var connector = new SqliteConnection($"Data Source={Path.Combine(_scratch, "index", "knowledge.db")};Pooling=False");
        await connector.OpenAsync();
        var sessions = await SessionStore.OpenAsync(connector);
        var held = await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        await sessions.KeepSaidAsync(held.Id, new SaidWord("w1", "One.", DateTimeOffset.UtcNow, []));

        var since = Since(await http.GetStringAsync($"{host.Url}{ChangeStream.Route}"));
        var seconds = (int)Wait.TotalSeconds;
        // LedgerChanges.RunAsync's own ask, and its rule: the topic wakes for any family named.
        var older = http.GetStringAsync($"{host.Url}{ChangeStream.Route}?wait={seconds}&since={Uri.EscapeDataString(since)}");
        var asking = http.GetStringAsync($"{host.Url}{ChangeStream.Route}?wait={seconds}&since={Uri.EscapeDataString(since)}&journal=1");
        await Task.Delay(300);
        Assert.False(older.IsCompleted || asking.IsCompleted, "a current cursor was answered before anything moved");

        var asked = Stopwatch.StartNew();
        await sessions.TakeSaidAsync(held.Id, ["w1"]);
        var named = JsonDocument.Parse(await asking.WaitAsync(Poll.Patience)).RootElement;
        Assert.True(asked.Elapsed < Wait, $"answered at its wait's end, not woken: {asked.Elapsed}");
        Assert.Equal([ChangeFamilies.Journal], Moved(named));
        await Task.Delay(500);
        Assert.False(older.IsCompleted, "the older client's reader was woken by a journal-only append: its ledger topic would wake");

        // Alive: its own family wakes it, naming that family alone.
        await sessions.CreateAsync(null, "Keeper", "stub", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        Assert.Equal([ChangeFamilies.Sessions], Moved(JsonDocument.Parse(await older.WaitAsync(Poll.Patience)).RootElement));
        Assert.Null(host.StopByInput(Poll.Patience));
    }

    private static string[] Moved(JsonElement answer) => [.. answer.GetProperty("moved").EnumerateArray().Select(family => family.GetString()!)];

    private static string Since(string body) => JsonDocument.Parse(body).RootElement.GetProperty("since").GetString()!;
}
