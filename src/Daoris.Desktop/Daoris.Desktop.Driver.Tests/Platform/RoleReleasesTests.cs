using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>roleReleases</c> in <c>driver.json</c> (STEWARDROLE1b, D209 §4.3, §5.1a, Codex's final read, finding 7): the steward's releases are
/// kept in a key of their own, never in the owner's <c>released</c> or <c>transientReleased</c>, whose readers ignore any field they do
/// not know. So the owner's readers find no release of the steward's to take as theirs, a rewrite without the key leaves the quest held,
/// and an owner's own release beside it stays usable.
/// </summary>
public sealed class RoleReleasesTests
{
    private static readonly RoleRelease Stewards = new(RoleReleaseKinds.Stop, "s1", "steward", "a1");

    [Fact]
    public void A_role_s_release_survives_the_file_in_its_own_key_and_is_written_only_when_set()
    {
        var released = DriverConfig.Empty.WithRoleReleased("#q1", Stewards);
        var json = released.ToJson();

        Assert.Equal(Stewards, DriverConfig.Parse(json).RoleReleaseFor("Q1"));
        Assert.Contains("\"roleReleases\": {", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"released\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("transientReleased", json, StringComparison.Ordinal);
        Assert.DoesNotContain("roleReleases", DriverConfig.Empty.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>The owner's readers read none of it: no stop and no transient end of that session is released by it.</summary>
    [Fact]
    public void The_owner_s_readers_take_no_release_of_the_steward_s_as_the_owner_s()
    {
        var read = DriverConfig.Parse("""{"roleReleases":{"q1":{"kind":"stop","session":"s1","by":"steward","act":"a1"}}}""");

        Assert.Null(read.ReleasedFor("q1"));
        Assert.False(read.Releases("q1", "s1"));
        Assert.False(read.ReleasesTransient("q1", "s1", DateTimeOffset.UtcNow));
        Assert.NotNull(read.RoleReleaseFor("q1"));
    }

    /// <summary>
    /// A rewrite that drops the key (a build before it, or a withdrawal) leaves the quest held, and the owner's own release beside it is
    /// kept and still releases its quest.
    /// </summary>
    [Fact]
    public void A_rewrite_dropping_the_key_leaves_the_owner_s_release_usable()
    {
        var both = DriverConfig.Empty.WithReleased("q2", "s9").WithRoleReleased("q1", Stewards);

        var withdrawn = DriverConfig.Parse(both.WithoutRoleRelease("q1").ToJson());

        Assert.Null(withdrawn.RoleReleaseFor("q1"));
        Assert.False(withdrawn.Releases("q1", "s1"));
        Assert.True(withdrawn.Releases("q2", "s9"));
        Assert.DoesNotContain("roleReleases", withdrawn.ToJson(), StringComparison.Ordinal);
        Assert.Same(DriverConfig.Empty, DriverConfig.Empty.WithoutRoleRelease("q1"));
    }

    /// <summary>An entry missing any of its four fields is none; a closed quest's role release goes with its other releases.</summary>
    [Theory]
    [InlineData("""{"roleReleases":{"q1":{"kind":"stop","session":"s1","by":"steward"}}}""")]
    [InlineData("""{"roleReleases":{"q1":{"kind":"stop","session":" ","by":"steward","act":"a1"}}}""")]
    [InlineData("""{"roleReleases":{"q1":"s1"}}""")]
    [InlineData("""{"roleReleases":["q1"]}""")]
    public void An_entry_that_is_not_whole_is_none(string file) => Assert.Null(DriverConfig.Parse(file).RoleReleaseFor("q1"));

    [Fact]
    public void A_closed_quest_s_role_release_goes_and_a_blank_one_is_refused()
    {
        Assert.Null(DriverConfig.Empty.WithRoleReleased("q1", Stewards).WithoutQuest("q1").RoleReleaseFor("q1"));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithRoleReleased("q1", Stewards with { Act = " " }));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithRoleReleased(" ", Stewards));
    }
}
