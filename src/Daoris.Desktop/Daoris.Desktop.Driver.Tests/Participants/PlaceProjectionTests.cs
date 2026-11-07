namespace Daoris.Desktop.Driver.Tests;

using Daoris.Driver;

/// <summary>The chain a chosen place is read through, and a registry of many undriven repositories answered as counts.</summary>
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

    /// <summary>A place choosing a version copied from a default follows that default, and names what the copy changed.</summary>
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
                var planned = WorkflowOperations.PlanGraph(home, null, id, null, shipped.Name, "terminal", "2026-11-01T09:00:00Z", graph,
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

    private static RoleCatalog Seeds() => RoleCatalog.ReadHome(Path.Combine(AppContext.BaseDirectory, "no-such-home"));

    private static string Wire(object value) => System.Text.Json.JsonSerializer.Serialize(value);


    /// <summary>A saved version of each shipped delivery lands as Current does for the same land.</summary>
    [Fact]
    public void A_saved_version_lands_as_Current_does_for_the_same_land()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "place-projection", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var store = WorkflowPorts.Store(home);
            var shipped = WorkflowDefaults.All.Where(each => each.SetUp).ToList();
            Assert.NotEmpty(shipped);
            foreach (var each in shipped)
            {
                var id = $"copy-{each.Id}";
                var planned = WorkflowOperations.PlanGraph(home, null, id, null, each.Name, "terminal", "2026-11-01T09:00:00Z",
                    WorkflowPresets.WithGraphGates(each.Graph(), opinion: false, look: false), new WorkflowFrom(each.Id, each.Digest));
                store.Write(id, planned.Plan.Result!);

                var saved = WorkflowSelection.Apply(DriverConfig.Empty with { Drivable = ["engine"] }, new WorkflowUse(id, "engine", null, null));

                // A saved workflow's delivery is the default it is a copy of, with nothing the copy changed.
                var lands = PlaceProjection.ValuesAt(saved, "engine", "aurora", PlaceSource.RepositoryLevel, store).Lands;
                Assert.Equal(new SetupMatch(each.Id, []), lands);
            }

            // With no store to read the version from, it lands as nothing known.
            var unread = WorkflowSelection.Apply(DriverConfig.Empty with { Drivable = ["engine"] }, new WorkflowUse("copy-merge-after-accept", "engine", null, null));
            Assert.Null(PlaceProjection.ValuesAt(unread, "engine", "aurora", PlaceSource.RepositoryLevel).Lands);
        }
        finally
        {
            GitSeed.Discard(home);
        }
    }

    /// <summary>The Sessions and Workflow summaries equal the readers a start uses, with where each is set.</summary>
    [Fact]
    public void The_look_helpers_and_standing_answer_equal_their_readers_with_where_each_was_set()
    {
        var config = DriverConfig.Parse("""
            {
              "drivable": ["engine", "game"],
              "workspaceReviews": { "aurora": { "required": true, "environments": [{ "name": "local", "kind": "local", "procedure": "README.md", "address": "http://localhost:4200" }] } },
              "reviews": { "game": false },
              "helpers": { "engine": { "mode": "on" } },
              "workspaceHelpers": { "aurora": { "mode": "off" } },
              "standing": { "engine": { "says": "ask before you push" } }
            }
            """);

        var engine = PlaceProjection.ValuesAt(config, "engine", "aurora", PlaceSource.RepositoryLevel);
        Assert.Equal(new PlaceValue("on", "workspace"), engine.Look);
        Assert.Equal(new PlaceValue("on", "here"), engine.Helpers);
        Assert.Equal(new PlaceValue("ask before you push", "here"), engine.Answer);

        var game = PlaceProjection.ValuesAt(config, "game", "aurora", PlaceSource.RepositoryLevel);
        Assert.Equal(new PlaceValue("off", "here"), game.Look);
        Assert.Equal(new PlaceValue("off", "workspace"), game.Helpers);
        Assert.Equal(new PlaceValue(null, "default"), game.Answer);

        var workspace = PlaceProjection.ValuesAt(config, "", "aurora", PlaceSource.WorkspaceLevel);
        Assert.Equal(new PlaceValue("on", "here"), workspace.Look);
        Assert.Equal(new PlaceValue("off", "here"), workspace.Helpers);
        Assert.Equal(new PlaceValue(null, "default"), workspace.Answer);

        var bare = PlaceProjection.ValuesAt(DriverConfig.Empty, "site", "borealis", PlaceSource.RepositoryLevel);
        Assert.Equal(new PlaceValue(null, "default"), bare.Look);
        Assert.Equal(new PlaceValue(HelperRules.Resolve(DriverConfig.Empty, "site", "borealis").Rule.Mode, "default"), bare.Helpers);
    }

    /// <summary>Each work names its role and where it sits, as the catalogue the start reads says.</summary>
    [Fact]
    public void Each_work_names_its_role_and_scope_as_the_catalogue_says()
    {
        var registry = new[] { new RepoView("engine", true, "root", "aurora") };
        var config = DriverConfig.Empty with { Drivable = ["engine"] };
        var catalogue = Seeds();

        var read = PlaceProjection.Of(config, registry, () => catalogue, new PlacesAsk());

        foreach (var place in new[] { (read.Repositories.Single(), new RolePlace("aurora", "engine")), (read.Workspaces.Single().Place, new RolePlace("aurora")), (read.Machine, new RolePlace()) })
        {
            Assert.Equal(RoleWork.Codes, place.Item1.Works.Select(work => work.Work));
            foreach (var work in place.Item1.Works)
            {
                var role = catalogue.RoleFor(work.Work, place.Item2).Role;
                Assert.Equal(role?.Id, work.Role);
                Assert.Equal(role?.Name, work.RoleName);
                Assert.Equal(role?.Scope.Kind, work.RoleScope?.Kind);
                Assert.Equal(role?.Scope.Id, work.RoleScope?.Id);
            }
        }
    }

    /// <summary>Adoption is none, running (an open or taken set-up quest) or adopted, and a running one is listed with the driven.</summary>
    [Fact]
    public void Adoption_is_read_from_the_set_up_quest_and_a_running_one_is_listed_with_the_driven()
    {
        var registry = new[]
        {
            new RepoView("engine", true, "root", "aurora"), new RepoView("api", false, "root", "aurora"),
            new RepoView("site", false, "root", "aurora"), new RepoView("done", false, "root", "aurora"), new RepoView("late", true, "root", "aurora"),
        };
        var config = DriverConfig.Empty with { Drivable = ["engine"] };
        var title = SetupQuests.Title(SetupQuests.SetUp, new DateOnly(2026, 11, 1));
        QuestView[] quests =
        [
            new("q1", "ask #a1", "api", title, "", "Open"), new("q2", "ask #a1", "done", title, "", "Done"),
            new("q3", "ask #a1", "late", title, "", "Taken"), new("q4", "ask #a1", "site", "Something else", "", "Open"),
        ];

        var read = PlaceProjection.Of(config, registry, Seeds, new PlacesAsk(), quests: quests);

        Assert.Equal(["api", "engine"], read.Repositories.Select(row => row.Repository));
        var api = read.Repositories.Single(row => row.Repository == "api");
        Assert.Equal((false, "running"), (api.Driven, api.Adoption));
        Assert.Equal("adopted", read.Repositories.Single(row => row.Repository == "engine").Adoption);
        Assert.Equal((1, 4, 1), (read.Workspaces.Single().Driven, read.Workspaces.Single().NotDriven, read.Workspaces.Single().Running));
        var fold = PlaceProjection.Of(config, registry, Seeds, new PlacesAsk(Fold: "aurora"), quests: quests).Undriven!;
        Assert.Equal(["done", "late", "site"], fold.Rows.Select(row => row.Repository));
        Assert.Equal(3, fold.Total);

        var detail = (string repository) => PlaceProjection.Of(config, registry, Seeds, new PlacesAsk(Detail: new PlaceTarget("aurora", repository)), quests: quests).Detail!;
        Assert.Equal("none", detail("site").Adoption);
        Assert.Equal("none", detail("done").Adoption);
        Assert.Equal("adopted", detail("late").Adoption);

        // No quests read: nothing runs, and the old answer stands.
        var none = PlaceProjection.Of(config, registry, Seeds, new PlacesAsk());
        Assert.Equal(["engine"], none.Repositories.Select(row => row.Repository));
        Assert.Equal(0, none.Workspaces.Single().Running);
    }

    /// <summary>A place's detail is its list row built by the same readers, driven or not, and a workspace's is its own row.</summary>
    [Fact]
    public void A_place_s_detail_equals_its_row_and_its_readers_driven_or_not()
    {
        var registry = new[] { new RepoView("engine", true, "root", "aurora"), new RepoView("tools", false, null, "aurora") };
        var config = DriverConfig.Parse("""{ "drivable": ["engine"], "holds": ["tools"] }""");

        PlacesRead Ask(PlaceTarget target) => PlaceProjection.Of(config, registry, Seeds, new PlacesAsk(Detail: target));

        var driven = Ask(new("aurora", "engine"));
        Assert.Equal(Wire(driven.Repositories.Single()), Wire(driven.Detail!));

        var tools = Ask(new("aurora", "tools")).Detail!;
        Assert.Equal((false, true, false, false), (tools.Driven, tools.Held, tools.Checkout, tools.Adopted));
        Assert.Equal(Wire(PlaceProjection.ValuesAt(config, "tools", "aurora", PlaceSource.RepositoryLevel)), Wire(tools.Values!));
        Assert.Null(tools.Values!.Lands);
        Assert.Equal(RoleWork.Codes, tools.Works.Select(work => work.Work));

        var workspace = Ask(new("aurora", null));
        Assert.Equal(Wire(workspace.Workspaces.Single().Place), Wire(workspace.Detail!));

        Assert.Null(Ask(new("aurora", "missing")).Detail);
        Assert.Null(Ask(new("elsewhere", "engine")).Detail);
        Assert.Null(PlaceProjection.Of(config, registry, Seeds, new PlacesAsk()).Detail);
    }

    /// <summary>The members of a workspace are all of them, not the page in hand.</summary>
    [Fact]
    public void The_members_of_a_workspace_are_the_whole_list_with_driving_checkout_and_adoption()
    {
        var registry = Enumerable.Range(1, 30).Select(number => new RepoView($"many-{number:00}", number % 2 == 0, number == 3 ? null : "root", "aurora"))
            .Append(new RepoView("engine", true, "root", "aurora")).Append(new RepoView("other", true, "root", "borealis")).ToList();
        var config = DriverConfig.Empty with { Drivable = ["engine", "many-02"] };
        QuestView[] quests = [new("q1", "ask #a1", "many-01", SetupQuests.Title(SetupQuests.SetUp, new DateOnly(2026, 11, 1)), "", "Open")];

        var read = PlaceProjection.Of(config, registry, Seeds, new PlacesAsk(Limit: 1, Members: "aurora"), quests: quests);

        var members = read.Members!;
        Assert.Equal("aurora", members.Workspace);
        Assert.Equal(31, members.Rows.Count);
        Assert.DoesNotContain(members.Rows, row => row.Repository == "other");
        Assert.Equal(["engine", "many-02"], members.Rows.Where(row => row.Driven).Select(row => row.Repository));
        Assert.Equal(new PlaceMember("many-01", false, true, "running"), members.Rows.Single(row => row.Repository == "many-01"));
        Assert.Equal(new PlaceMember("many-03", false, false, "none"), members.Rows.Single(row => row.Repository == "many-03"));
        Assert.Equal("adopted", members.Rows.Single(row => row.Repository == "many-04").Adoption);
        Assert.Null(PlaceProjection.Of(config, registry, Seeds, new PlacesAsk()).Members);
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
