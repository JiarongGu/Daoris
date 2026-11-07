using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A <c>land</c>'s target is the tree's line, the only line a repository declares; no delivery is written for any other.</summary>
public sealed class LandTargetTests
{
    private static BoundLand Bound(string method, string target) => new(method, target, false, null, null, "{}", "digest", "workflow:w@1:r");

    private static Task<(TreeDelivery? Delivery, TreeLanding? Refusal)> Start(BoundLand bound) =>
        CurrentDeliveries.StartAsync(
            "unused-home", Landing.Merge, "repo", "ws", "unused-root",
            new LandingSubject("s1", null, "t"), "0123456789abcdef0123456789abcdef01234567", "main", "feature/x", null, AcceptedBy.Person, false,
            () => null, CancellationToken.None, bound);

    [Theory]
    [InlineData("merge")]
    [InlineData("integrate")]
    public async Task A_target_other_than_the_trees_line_is_refused_before_any_delivery(string method)
    {
        var (delivery, refusal) = await Start(Bound(method, "release"));
        Assert.Null(delivery);
        Assert.NotNull(refusal);
        Assert.False(refusal!.Landed);
        Assert.Contains("release", refusal.Message);
    }
}
