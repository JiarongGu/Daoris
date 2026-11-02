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
}
