using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One record's change on a home of a working machine's size (<see cref="BenchHome"/>, 3,000 records), read as the window's
/// sessions slice reads it: the whole list asked and parsed again, against the list kept by its change since the last mark. The kept
/// list is held equal to the whole one, record by record and in order, and its read to its budget.
/// </summary>
/// <remarks>
/// Each read follows one write, so the service makes its answer again for both and the two differ only in what crosses the wire and is
/// parsed. The change's read is held under the whole read beside it, which a neighbour's load slows alike, and under a quarter of a
/// second: the slice's read runs at every change a press or the driver makes, and what is left of it is the service making its list
/// again. It runs alone, as the home's other bench does.
/// </remarks>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class SessionChangeBenchProcessTests(ITestOutputHelper output) : IAsyncLifetime
{
    private static readonly TimeSpan ChangeBudget = TimeSpan.FromMilliseconds(250);

    private BenchHome? _home;

    private BenchHome Home => _home!;

    public async Task InitializeAsync() => _home = await BenchHome.MakeAsync(3000, 2, 0);

    public async Task DisposeAsync()
    {
        if (_home is not null) await _home.DisposeAsync();
    }

    [Fact]
    public async Task One_change_is_read_as_its_change_and_holds_the_whole_list()
    {
        var kept = Home.Client.SessionList.Kept;
        await kept.ReadAsync();
        var target = Home.Trees[^1].Session;
        await Home.Client.SessionList.JsonAsync(true);

        await Home.Client.Words.SayAsync(target, "The first word.");
        var watch = Stopwatch.StartNew();
        var whole = JsonNode.Parse(await Home.Client.SessionList.JsonAsync(true))!.AsArray();
        var wholeTook = watch.Elapsed;

        await Home.Client.Words.SayAsync(target, "The second word.");
        watch.Restart();
        var changed = await kept.ReadAsync();
        var changeTook = watch.Elapsed;
        var now = JsonNode.Parse(await Home.Client.SessionList.JsonAsync(true))!.AsArray();

        output.WriteLine($"one change, the whole list read again: {wholeTook.TotalMilliseconds:F0} ms ({whole.Count} records)");
        output.WriteLine($"one change, its change laid over the kept list: {changeTook.TotalMilliseconds:F0} ms");
        Assert.Equal(now.Select(record => record!.ToJsonString()), changed.Select(record => record.ToJsonString()));
        Assert.True(changeTook < wholeTook, $"the change took {changeTook.TotalMilliseconds:F0} ms, the whole list {wholeTook.TotalMilliseconds:F0} ms");
        Assert.True(changeTook < ChangeBudget, $"the change took {changeTook.TotalMilliseconds:F0} ms, past {ChangeBudget.TotalMilliseconds} ms");
    }
}
