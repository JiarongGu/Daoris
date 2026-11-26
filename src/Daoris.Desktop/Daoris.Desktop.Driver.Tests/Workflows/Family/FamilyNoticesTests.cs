using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A hold past its edge's lag bound leaves the person one notice, by edge and the hold's first red, in the consumer's workspace.</summary>
public sealed class FamilyNoticesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 9, 30, 0, TimeSpan.Zero);
    private static readonly string B = new('b', 40);

    [Fact]
    public void A_hold_is_one_notice_naming_the_consumer_the_producer_and_the_commit_held_in_the_consumers_workspace()
    {
        var registry = new[] { new RepoView("web", true, "x", "team") };

        var occurrence = Assert.Single(FamilyNotices.Occurrences([new FamilyHold("web", "core", B, T0, 2)], registry, T0.AddDays(5)));

        Assert.Equal("family-lag:web.core.202611260930", occurrence.Key);
        var notice = occurrence.Notice!;
        Assert.Equal(("family-lag", "web", "core", new string('b', 12), "team"), (notice.Kind, notice.Repository, notice.Act, notice.Code, notice.Workspace));
        Assert.Equal(T0.AddDays(5), notice.At);
    }

    [Fact]
    public void A_later_red_of_the_same_hold_is_the_same_notice_and_a_new_hold_is_another()
    {
        var first = FamilyNotices.Occurrences([new FamilyHold("web", "core", B, T0, 1)], [], T0.AddDays(5)).Single().Key;
        var later = FamilyNotices.Occurrences([new FamilyHold("web", "core", new string('c', 40), T0, 3)], [], T0.AddDays(6)).Single().Key;
        var fresh = FamilyNotices.Occurrences([new FamilyHold("web", "core", B, T0.AddDays(10), 4)], [], T0.AddDays(15)).Single().Key;

        Assert.Equal(first, later);
        Assert.NotEqual(first, fresh);
    }

    [Fact]
    public void No_hold_no_notice()
    {
        Assert.Empty(FamilyNotices.Occurrences([], [], T0));
    }
}
