using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>setup</c> kind's writer (LAYOUT8, D170 point 1; WORKFLOWCLEAN1 1f): a place's set-up of registered repositories, by the
/// registry's names, whose adoption part is <c>daoris-driver place … --adoption</c>; its one door is <c>place</c>.
/// </summary>
public sealed class HelpSetupProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_setup_is_written_with_the_repository_it_sets_up()
    {
        var (id, message) = Box().ProposeSetup(" atlas ", "the person asked to include it in Daoris", "h1", Now, new HelpSetupPlace("keep", Adoption: true));

        var written = Written(id!);
        Assert.Equal(
            ("setup", "place", "atlas"),
            (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString()));
        // The common fields a kind without them writes null, so the workspace's door WSSETUP7 adds has its field already.
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("workspace").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("value").ValueKind);
        Assert.Equal("the person asked to include it in Daoris", written.GetProperty("why").GetString());
        Assert.Equal("h1", written.GetProperty("by").GetProperty("session").GetString());
        Assert.Equal("proposed", written.GetProperty("state").GetString());
        Assert.Contains($"Proposed `#{id}`", message);
    }

    /// <summary>AGENTSPLACE1k: with a place ask the door is <c>place</c>, the ask in the file's own <c>place</c>, the repository the first of its repositories.</summary>
    [Fact]
    public void A_place_ask_is_written_as_the_place_door_with_its_repositories_and_parts()
    {
        var place = new HelpSetupPlace("merge-after-accept", ["tools", "ATLAS"], Opinion: true, Adoption: true, Builds: "library", Drive: true);

        var (id, message) = Box().ProposeSetup(" atlas ", "the person asked to set both up", "h1", Now, place);

        var written = Written(id!);
        Assert.Equal(
            ("setup", "place", "atlas"),
            (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString()));
        var ask = written.GetProperty("place");
        Assert.Equal("merge-after-accept", ask.GetProperty("preset").GetString());
        Assert.Equal(["atlas", "tools"], ask.GetProperty("repositories").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(
            (true, false, true, "library", true),
            (ask.GetProperty("opinion").GetBoolean(), ask.GetProperty("look").GetBoolean(), ask.GetProperty("adoption").GetBoolean(),
                ask.GetProperty("builds").GetString(), ask.GetProperty("drive").GetBoolean()));
        Assert.Contains($"Proposed `#{id}`", message);
    }

    /// <summary>WORKFLOWCLEAN1 1f: the repository-alone door is gone, so a set-up that names no delivery is refused and nothing is written.</summary>
    [Fact]
    public void A_setup_without_a_place_ask_is_refused_naming_the_preset_and_nothing_is_written()
    {
        var (id, message) = Box().ProposeSetup("atlas", "the person asked", "h1", Now);

        Assert.Null(id);
        Assert.Contains("names its delivery, the `preset` (`keep` leaves how its work lands as it is)", message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    [Theory]
    [InlineData("", "tools", "names its delivery")]
    [InlineData("merge after", "tools", "one word")]
    [InlineData("merge", "C:/work/tools", "never by its folder")]
    [InlineData("merge", "to ols", "one word")]
    public void A_malformed_place_ask_is_refused_with_nothing_written(string preset, string other, string says)
    {
        var (id, message) = Box().ProposeSetup("atlas", "a reason", "h1", Now, new HelpSetupPlace(preset, [other]));

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    /// <summary>The shape, checked here and nothing more; whether the press would publish is the driver's, by `--plan`'s refusals.</summary>
    [Theory]
    [InlineData("", "a reason", "names the repository to set up")]
    [InlineData("  ", "a reason", "names the repository to set up")]
    [InlineData("atlas game", "a reason", "one word")]
    [InlineData("C:/work/atlas", "a reason", "never by its folder")]
    [InlineData("../atlas", "a reason", "never by its folder")]
    [InlineData("atlas", " ", "needs its reason")]
    public void A_malformed_setup_proposal_is_refused_with_nothing_written(string repository, string why, string says)
    {
        var (id, message) = Box().ProposeSetup(repository, why, "h1", Now, new HelpSetupPlace("keep"));

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }
}
