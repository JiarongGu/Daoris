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

    /// <summary>DEFAULTCONFIG1a: under Current, every place of a fresh home follows the default its landing rule matches, with nothing of its own.</summary>
    [Fact]
    public void A_fresh_home_s_places_each_follow_their_default_with_nothing_different()
    {
        var registry = new[] { new RepoView("engine", true, "root", "aurora"), new RepoView("tools", true, "root", "aurora") };
        var config = DriverConfig.Empty with { Drivable = ["engine", "tools"] };

        var read = PlaceProjection.Of(config, registry, () => RoleCatalog.ReadHome(Path.Combine(AppContext.BaseDirectory, "no-such-home")), new PlacesAsk());

        var places = read.Workspaces.Select(each => each.Place).Concat(read.Repositories).ToList();
        Assert.Equal(3, places.Count);
        Assert.All(places, place => Assert.Equal(new PlaceDefault("merge-after-accept", []), place.Values!.Follows));
    }

    /// <summary>DEFAULTCONFIG1a: a rule edited at a place names the options it differs in from the default it follows; the workspace keeps its own.</summary>
    [Fact]
    public void A_rule_edit_names_what_differs_from_the_default_the_place_follows()
    {
        var config = DriverConfig.Empty with { Drivable = ["engine"] };
        var branch = config.WithLanding("engine", new LandingRule(LandingForm.Branch, "f/{quest}", AutoAccept: true));
        var integrated = config.WithLanding("engine", new LandingRule(LandingForm.Branch, "f/{quest}", AutoAccept: true)
        {
            Integrate = new LandingIntegrate(Request: LandingIntegrate.Done, Gates: "declared"),
        });

        Assert.Equal(new PlaceDefault("branch-to-push", ["accept `automatic`"]), PlaceProjection.ValuesAt(branch, "engine", "aurora", PlaceSource.RepositoryLevel).Follows);
        Assert.Equal(new PlaceDefault("merge-after-accept", []), PlaceProjection.ValuesAt(branch, "", "aurora", PlaceSource.WorkspaceLevel).Follows);
        Assert.Equal(new PlaceDefault("integrated", ["accept `automatic`", "request `done`"]),
            PlaceProjection.ValuesAt(integrated, "engine", "aurora", PlaceSource.RepositoryLevel).Follows);
    }

    /// <summary>DEFAULTCONFIG1a: a place choosing a version copied from a default follows that default, and names what the copy changed.</summary>
    [Fact]
    public void A_place_choosing_a_copy_of_a_default_follows_it_and_names_what_the_copy_changed()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "place-projection", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var store = WorkflowPorts.Store(home);
            var shipped = WorkflowDefaults.Of("integrated")!;
            foreach (var (id, opinion) in new[] { ("same", false), ("opinion", true) })
            {
                var graph = WorkflowPresets.WithGraphGates(shipped.Graph(), opinion, look: false);
                var planned = WorkflowOperations.PlanGraph(home, null, id, null, shipped.Names.En, "terminal", "2026-11-01T09:00:00Z", graph,
                    new WorkflowFrom(shipped.Id, shipped.Digest));
                store.Write(id, planned.Plan.Result!);
            }

            DriverConfig Choosing(string id) => WorkflowSelection.Apply(DriverConfig.Empty with { Drivable = ["engine"] }, new WorkflowUse(id, "engine", null, null));

            Assert.Equal(new PlaceDefault("integrated", []),
                PlaceProjection.ValuesAt(Choosing("same"), "engine", "aurora", PlaceSource.RepositoryLevel, store).Follows);
            Assert.Equal(new PlaceDefault("integrated", ["opinion"]),
                PlaceProjection.ValuesAt(Choosing("opinion"), "engine", "aurora", PlaceSource.RepositoryLevel, store).Follows);
            Assert.Null(PlaceProjection.ValuesAt(Choosing("opinion"), "engine", "aurora", PlaceSource.RepositoryLevel).Follows);
        }
        finally
        {
            GitSeed.Discard(home);
        }
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
