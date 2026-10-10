namespace Daoris.Service.Tests;

/// <summary>
/// The <c>repository</c> kind's writer (ENTRY1d1, D161's ENTRY1d note): a registered repository moved to a workspace on this
/// machine, by its name and the workspace's, as the Manage drawer's *Move to workspace* moves it. A move needs no path
/// (D48 §7), so none is written.
/// </summary>
public sealed class HelpRepositoryProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_move_is_written_with_the_repository_and_the_workspace()
    {
        var (id, message) = Box().ProposeRepository(" engine ", " studio ", "the person wants engine beside the studio's work", "h1", Now);

        var written = Written(id!);
        Assert.Equal(
            ("repository", "wire", "engine", "studio"),
            (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(),
                written.GetProperty("target").GetString(), written.GetProperty("value").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("workspace").ValueKind);
        Assert.Equal("the person wants engine beside the studio's work", written.GetProperty("why").GetString());
        Assert.Equal("h1", written.GetProperty("by").GetProperty("session").GetString());
        Assert.Equal("proposed", written.GetProperty("state").GetString());
        Assert.Contains($"Proposed `#{id}`", message);
    }

    /// <summary>A workspace's name is the person's, as the drawer's free text takes it: a space in it is no refusal.</summary>
    [Fact]
    public void A_workspace_named_with_a_space_is_written_as_given()
    {
        var (id, _) = Box().ProposeRepository("engine", "R&D lab", "the person named it", "h1", Now);

        Assert.Equal("R&D lab", Written(id!).GetProperty("value").GetString());
    }

    /// <summary>The shape, checked here and nothing more; whether the repository is registered, and where it is, is the driver's.</summary>
    [Theory]
    [InlineData("", "studio", "a reason", "names the repository, as Repositories lists it")]
    [InlineData("engine", " ", "a reason", "names the workspace to move `engine` to")]
    [InlineData("my engine", "studio", "a reason", "one word")]
    [InlineData("C:/src/engine", "studio", "a reason", "never by its folder")]
    [InlineData("engine", "studio", " ", "needs its reason")]
    public void A_malformed_move_is_refused_with_nothing_written(string repository, string workspace, string why, string says)
    {
        var (id, message) = Box().ProposeRepository(repository, workspace, why, "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
