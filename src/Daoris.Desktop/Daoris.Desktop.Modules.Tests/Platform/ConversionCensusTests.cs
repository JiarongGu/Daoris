using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// WORKFLOWCLEAN1g-ii, ONEWAY1ae: the census the loop keeps, every part's. The row's wait is the old records the parts hold while some part has not
/// ended, counted by part and told when that changes; none before the first reading, and none once each has ended. A part that does not answer
/// refuses the reading, and what was held is kept.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class ConversionCensusTests : Bridge
{
    private static IReadOnlyList<PartCensus> Holding(int workflows, int settings = 0) =>
        [new("workflows", workflows, workflows == 0), new("settings", settings, settings == 0)];

    [Fact]
    public void A_census_that_holds_old_records_is_a_wait_with_their_count_by_part_until_it_ends()
    {
        var census = new ConversionCensus(Bus);
        Assert.Null(census.Wait());

        Assert.True(census.Tell(Holding(2, 1)));
        Assert.Equal(3, census.Wait()!["records"]!.GetValue<int>());
        Assert.Equal(1, census.Wait()!["parts"]!["settings"]!.GetValue<int>());
        Assert.False(census.Tell(Holding(2, 1)));
        Assert.True(census.Tell(Holding(2)));

        // An apply leaves nothing old: the wait is gone, and said.
        Assert.True(census.Tell(Holding(0)));
        Assert.Null(census.Wait());
        Assert.Equal(3, Raised.Count(each => each.Type == ConversionCensus.Event));
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
        Task<IReadOnlyList<PartCensus>> Read(CancellationToken _)
        {
            var reading = Interlocked.Increment(ref readings);
            return reading == 1 ? first.Task.ContinueWith(_ => Holding(1), TaskScheduler.Default) : Task.FromResult(Holding(2));
        }

        var running = census.RefreshAsync(Read, default);
        // A reading yields before it begins, and a call made before then shares it: the burst comes once the first has begun.
        var until = DateTime.UtcNow + Poll.Patience;
        while (Volatile.Read(ref readings) < 1)
        {
            Assert.True(DateTime.UtcNow < until, "the first reading never began");
            await Task.Delay(5);
        }

        var asked = Enumerable.Range(0, 5).Select(_ => census.RefreshAsync(Read, default)).ToList();
        Assert.All(asked, each => Assert.False(each.IsCompleted));

        first.SetResult();
        await Task.WhenAll([running, .. asked]).WaitAsync(Poll.Patience);

        Assert.Equal(2, readings);
        Assert.Equal(2, census.Wait()!["records"]!.GetValue<int>());
    }
}
