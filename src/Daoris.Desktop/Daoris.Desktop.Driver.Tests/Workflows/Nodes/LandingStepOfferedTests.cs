using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A landing step is listed for a supervisor only where the step offers an act its grants cover.</summary>
public sealed class LandingStepOfferedTests
{
    private static readonly LandingStep Review = new("r1", "review", 1, "engine", ["accept"]);

    [Fact]
    public void ROLESREVIEW1e_a_step_offers_the_covered_acts_it_takes_and_none_where_it_takes_none_of_them()
    {
        Assert.Equal(["accept"], Review.Offered(["accept", "reject"]));
        Assert.Empty(Review.Offered(["reject"]));
        Assert.Empty(Review.Offered([]));
    }
}
