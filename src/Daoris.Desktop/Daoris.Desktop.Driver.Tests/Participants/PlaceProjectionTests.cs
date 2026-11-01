namespace Daoris.Desktop.Driver.Tests;

using Daoris.Driver;

/// <summary>AGENTSPLACE1a: the chain a chosen place is read through, and a registry of many undriven repositories answered as counts.</summary>
public sealed class PlaceProjectionTests
{
    [Fact]
    public void A_repository_s_chain_is_itself_its_workspace_and_this_machine_nearest_first()
    {
        Assert.Equal(
            [new RolePlace("aurora", "engine"), new RolePlace("aurora"), new RolePlace()],
            PlaceProjection.ChainOf(new RolePlace("aurora", "engine")));
        Assert.Equal([new RolePlace("aurora"), new RolePlace()], PlaceProjection.ChainOf(new RolePlace("aurora")));
        Assert.Equal([new RolePlace()], PlaceProjection.ChainOf(new RolePlace()));
    }

    [Fact]
    public void A_home_with_many_undriven_repositories_answers_counts_and_no_rows_for_them()
    {
        var registry = Enumerable.Range(1, 500).Select(number => new RepoView($"imported-{number:000}", true, "root", "aurora")).Append(new RepoView("engine", true, "root", "aurora")).ToList();
        var config = DriverConfig.Empty with { Drivable = ["engine"] };

        var read = PlaceProjection.Of(config, registry, () => RoleCatalog.ReadHome(Path.Combine(AppContext.BaseDirectory, "no-such-home")), new PlacesAsk());

        Assert.Equal(["engine"], read.Repositories.Select(row => row.Repository));
        Assert.Equal((1, 500), (read.Workspaces.Single().Driven, read.Workspaces.Single().NotDriven));
        Assert.Null(read.Undriven);
    }

    [Theory]
    [InlineData("repository", "repository", "here")]
    [InlineData("repository-kind", "repository", "here")]
    [InlineData("repository-landed", "repository", "here")]
    [InlineData("workspace-kind", "repository", "workspace")]
    [InlineData("workspace", "workspace", "here")]
    [InlineData("machine", "workspace", "machine")]
    [InlineData("current", "repository", "default")]
    [InlineData("default", "workspace", "default")]
    public void A_reader_s_source_word_is_said_from_the_page_it_is_read_on(string raw, string level, string said) =>
        Assert.Equal(said, PlaceSource.At(raw, level));
}
