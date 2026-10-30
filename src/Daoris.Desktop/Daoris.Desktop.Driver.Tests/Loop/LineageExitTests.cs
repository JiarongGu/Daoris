using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1a rule 4: every path on which Loop read a process's exit tells the token set, and writes that process's <c>end</c>
/// where the run concludes nothing by it: a wait thrown as the loop closes, its kill having read the exit, and a drain whose capture
/// faulted past the exit, each before the throw leaves the run.
/// </summary>
public sealed class LineageExitTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lineage-exit-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly LineageTokens _tokens = new(Environment.ProcessId, DateTime.UtcNow);

    private readonly List<string> _told = [];

    private readonly IDisposable _within;

    public LineageExitTests()
    {
        Directory.CreateDirectory(_home);
        _tokens.Moved += token =>
        {
            lock (_told) _told.Add(token);
        };
        _within = LineageTokens.Within(_tokens);
    }

    public void Dispose()
    {
        _within.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_wait_thrown_as_the_loop_closes_writes_its_end_unclean_and_lets_its_token_go_before_the_throw_leaves()
    {
        var driver = Driver();
        var proc = ChildLineage.Opened(_home, "s1");
        using var closed = new CancellationTokenSource();
        await closed.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => driver.WaitedAsync("s1", proc, Task.FromCanceled<int?>(closed.Token), closed.Token));

        var end = Lines()[^1];
        Assert.Equal(["start", "end"], Lines().Select(line => line.GetProperty("by").GetString()));
        Assert.Equal(proc, end.GetProperty("proc").GetString());
        Assert.False(end.GetProperty("clean").GetBoolean());
        Assert.Equal(TokenReading.Gone, _tokens.Reading(proc));
        Assert.Equal([proc], _told);
    }

    [Fact]
    public async Task Loops_word_of_an_exit_it_read_lets_a_token_the_set_never_watched_go_and_writes_no_end_before_the_drain()
    {
        var driver = Driver();
        var proc = ChildLineage.Opened(_home, "s1");
        Assert.Equal(TokenReading.Unwatched, _tokens.Reading(proc));

        Assert.Equal(0, await driver.WaitedAsync("s1", proc, Task.FromResult<int?>(0), CancellationToken.None));

        Assert.Equal(TokenReading.Gone, _tokens.Reading(proc));
        Assert.Equal([proc], _told);
        Assert.Equal(["start"], Lines().Select(line => line.GetProperty("by").GetString()));
    }

    [Fact]
    public async Task A_drain_whose_capture_faults_past_the_exit_writes_its_end_unclean_before_the_fault_leaves()
    {
        var driver = Driver();
        var proc = ChildLineage.Opened(_home, "s1");

        var thrown = await Assert.ThrowsAsync<IOException>(
            () => driver.DrainedAsync("s1", proc, Task.FromException(new IOException("the transcript would not take a line"))));

        Assert.Equal("the transcript would not take a line", thrown.Message);
        var end = Lines()[^1];
        Assert.Equal("end", end.GetProperty("by").GetString());
        Assert.Equal(proc, end.GetProperty("proc").GetString());
        Assert.False(end.GetProperty("clean").GetBoolean());
        Assert.Equal(TokenReading.Gone, _tokens.Reading(proc));
    }

    private List<JsonElement> Lines() =>
        [.. File.ReadAllLines(ChildLineage.PathOf(_home, "s1")).Select(line => JsonDocument.Parse(line).RootElement)];

    private Daoris.Driver.Driver Driver() =>
        new(new ServiceClient("http://127.0.0.1:9", null), DriverConfig.Empty, AdapterSet.Built(), _home);
}
