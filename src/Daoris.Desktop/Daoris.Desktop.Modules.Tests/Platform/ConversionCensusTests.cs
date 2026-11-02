using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// WORKFLOWCLEAN1g-ii: the census the loop keeps. The row's wait is the places it lists while it has not ended, told when that changes;
/// none before the first reading, and none once it has ended.
/// </summary>
public sealed class ConversionCensusTests : Bridge
{
    private static LegacyCensus Holding(params string[] places) =>
        new([], [.. places.Select(each => new LegacyPlaceRow(each, false, "default", ["steps"], false, false))], [], []);

    [Fact]
    [Trait("Category", "Census")]
    public void A_census_that_holds_places_is_a_wait_with_their_count_until_it_ends()
    {
        var census = new ConversionCensus(Bus);
        Assert.Null(census.Wait());

        Assert.True(census.Tell(Holding("aurora", "borealis")));
        Assert.Equal(2, census.Wait()!["places"]!.GetValue<int>());
        Assert.False(census.Tell(Holding("aurora", "borealis")));

        // An apply leaves nothing old: the wait is gone, and said.
        Assert.True(census.Tell(new([], [], [], [])));
        Assert.Null(census.Wait());
        Assert.Equal(2, Raised.Count(each => each.Type == ConversionCensus.Event));
    }

    /// <summary>
    /// A burst of driver changes while one reading runs reads once more after it, not once each: the reading asks the service for the
    /// registry and every quest. Each caller is answered by a reading that began after its call.
    /// </summary>
    [Fact]
    public async Task Refreshes_asked_while_one_reads_are_one_more_reading_begun_after_them()
    {
        var census = new ConversionCensus(Bus);
        var first = new TaskCompletionSource();
        var readings = 0;
        Task<LegacyCensus> Read(CancellationToken _)
        {
            var reading = Interlocked.Increment(ref readings);
            return reading == 1 ? first.Task.ContinueWith(_ => Holding("aurora"), TaskScheduler.Default) : Task.FromResult(Holding("aurora", "borealis"));
        }

        var running = census.RefreshAsync(Read, default);
        var asked = Enumerable.Range(0, 5).Select(_ => census.RefreshAsync(Read, default)).ToList();
        Assert.All(asked, each => Assert.False(each.IsCompleted));

        first.SetResult();
        await Task.WhenAll([running, .. asked]).WaitAsync(Poll.Patience);

        Assert.Equal(2, readings);
        Assert.Equal(2, census.Wait()!["places"]!.GetValue<int>());
    }
}
